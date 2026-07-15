namespace eShop.PaymentProcessor.UnitTests.TokenLedger;

using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging.Abstractions;

[TestClass]
public class TokenLedgerServiceTests
{
    private static TokenDbContext CreateDb(string dbName)
    {
        var opts = new DbContextOptionsBuilder<TokenDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new TokenDbContext(opts);
    }

    private static TokenLedgerService CreateService(TokenDbContext db, int maxRetries = 3)
    {
        var optsMon = Substitute.For<IOptionsMonitor<TokenOptions>>();
        optsMon.CurrentValue.Returns(new TokenOptions { MaxConcurrencyRetries = maxRetries });
        return new TokenLedgerService(db, optsMon, NullLogger<TokenLedgerService>.Instance);
    }

    private static ClubListingVerifiedIntegrationEvent MakeEvent(
        string? sellerId = "user-1",
        string catalogItemId = "item-1",
        string category = "Driver",
        string condition = "Excellent")
    {
        return new ClubListingVerifiedIntegrationEvent(
            sellerId ?? string.Empty,
            catalogItemId,
            category,
            condition);
    }

    // ── US1 tests ─────────────────────────────────────────────────────────

    // T014(a): AwardTokens creates wallet on first award
    [TestMethod]
    public async Task AwardTokens_CreatesWallet_OnFirstAward()
    {
        using var db = CreateDb(nameof(AwardTokens_CreatesWallet_OnFirstAward));
        await SeedLookupAsync(db);
        var svc = CreateService(db);

        await svc.AwardTokens(MakeEvent());

        var wallet = await db.TokenWallets.FindAsync("user-1");
        Assert.IsNotNull(wallet);
        Assert.AreEqual(80, wallet.Balance); // Driver/Excellent = 80
    }

    // T014(b): duplicate EventId does not double-credit
    [TestMethod]
    public async Task AwardTokens_DuplicateEventId_DoesNotDoublePay()
    {
        using var db = CreateDb(nameof(AwardTokens_DuplicateEventId_DoesNotDoublePay));
        await SeedLookupAsync(db);
        var svc = CreateService(db);

        var evt = MakeEvent();
        await svc.AwardTokens(evt);
        await svc.AwardTokens(evt); // Same event, same Id

        var wallet = await db.TokenWallets.FindAsync("user-1");
        Assert.IsNotNull(wallet);
        Assert.AreEqual(80, wallet.Balance); // Not 160
    }

    // T014(c): duplicate CatalogItemId is rejected
    [TestMethod]
    public async Task AwardTokens_DuplicateCatalogItemId_IsRejected()
    {
        using var db = CreateDb(nameof(AwardTokens_DuplicateCatalogItemId_IsRejected));
        await SeedLookupAsync(db);
        var svc = CreateService(db);

        var evt1 = MakeEvent(catalogItemId: "item-abc");
        var evt2 = new ClubListingVerifiedIntegrationEvent("user-1", "item-abc", "Driver", "Excellent");
        // Give evt2 a different integration event ID by constructing a new one
        // (same CatalogItemId but different event — simulates resubmission)
        await svc.AwardTokens(evt1);

        // Try again with new event but same catalogItemId
        using var db2 = db; // share the same DB
        await svc.AwardTokens(evt2);

        // Balance should still be 80 (only awarded once)
        var wallet = await db.TokenWallets.FindAsync("user-1");
        Assert.IsNotNull(wallet);
        Assert.AreEqual(80, wallet.Balance);
    }

    // T014(d): DbUpdateConcurrencyException triggers retry (tested via service result)
    // We test the retry path via a subclassed DbContext that throws on first SaveChanges
    [TestMethod]
    public async Task AwardTokens_ConcurrencyException_RetriesUpToMaxRetries()
    {
        var dbName = nameof(AwardTokens_ConcurrencyException_RetriesUpToMaxRetries);

        // Seed lookup using a regular context so the throw count is not consumed
        using (var seedDb = CreateDb(dbName))
        {
            await SeedLookupAsync(seedDb);
        }

        using var db = new ThrowingOnFirstSaveTokenDbContext(
            new DbContextOptionsBuilder<TokenDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options,
            throwsOnSave: 1);
        var svc = CreateService(db, maxRetries: 3);

        // Should succeed on the second attempt
        await svc.AwardTokens(MakeEvent());

        var wallet = await db.TokenWallets.FindAsync("user-1");
        Assert.IsNotNull(wallet);
        Assert.AreEqual(80, wallet.Balance);
    }

    // T014(e): no SellerId → no credit, no error (silent skip)
    [TestMethod]
    public async Task AwardTokens_NoSellerId_SkipsSilently()
    {
        using var db = CreateDb(nameof(AwardTokens_NoSellerId_SkipsSilently));
        await SeedLookupAsync(db);
        var svc = CreateService(db);

        await svc.AwardTokens(MakeEvent(sellerId: null));

        Assert.AreEqual(0, await db.TokenWallets.CountAsync());
        Assert.AreEqual(0, await db.TokenTransactions.CountAsync());
    }

    // T014(f): successful award emits a structured Information log entry
    [TestMethod]
    public async Task AwardTokens_Success_EmitsStructuredLog()
    {
        using var db = CreateDb(nameof(AwardTokens_Success_EmitsStructuredLog));
        await SeedLookupAsync(db);

        var optsMon = Substitute.For<IOptionsMonitor<TokenOptions>>();
        optsMon.CurrentValue.Returns(new TokenOptions { MaxConcurrencyRetries = 3 });

        var loggerMock = Substitute.For<ILogger<TokenLedgerService>>();
        var svc = new TokenLedgerService(db, optsMon, loggerMock);

        await svc.AwardTokens(MakeEvent());

        loggerMock.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ── US2 tests ─────────────────────────────────────────────────────────

    // T021(a): GetBalance returns 0 for unknown UserId without DB write
    [TestMethod]
    public async Task GetBalance_UnknownUserId_ReturnsZeroWithoutDbWrite()
    {
        using var db = CreateDb(nameof(GetBalance_UnknownUserId_ReturnsZeroWithoutDbWrite));
        var svc = CreateService(db);

        var balance = await svc.GetBalance("unknown-user");

        Assert.AreEqual(0, balance);
        Assert.AreEqual(0, await db.TokenWallets.CountAsync()); // No write
    }

    // T021(b): GetBalance returns correct value when wallet exists
    [TestMethod]
    public async Task GetBalance_ExistingWallet_ReturnsBalance()
    {
        using var db = CreateDb(nameof(GetBalance_ExistingWallet_ReturnsBalance));
        db.TokenWallets.Add(new TokenWallet { UserId = "user-x", Balance = 150 });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var balance = await svc.GetBalance("user-x");

        Assert.AreEqual(150, balance);
    }

    // ── US3 tests ─────────────────────────────────────────────────────────

    // T025a(a): GetTransactions returns empty result for unknown UserId
    [TestMethod]
    public async Task GetTransactions_UnknownUserId_ReturnsEmpty()
    {
        using var db = CreateDb(nameof(GetTransactions_UnknownUserId_ReturnsEmpty));
        var svc = CreateService(db);

        var (totalCount, items) = await svc.GetTransactions("unknown", 1, 20);

        Assert.AreEqual(0, totalCount);
        Assert.AreEqual(0, items.Count);
    }

    // T025a(b): results are returned in reverse-chronological order
    [TestMethod]
    public async Task GetTransactions_ReturnsReverseChronologicalOrder()
    {
        using var db = CreateDb(nameof(GetTransactions_ReturnsReverseChronologicalOrder));
        var now = DateTime.UtcNow;
        db.TokenTransactions.AddRange(
            new TokenTransaction { Id = Guid.NewGuid(), UserId = "u1", Amount = 80, Reason = "earn", RelatedEventId = "e1", CreatedAt = now.AddMinutes(-10) },
            new TokenTransaction { Id = Guid.NewGuid(), UserId = "u1", Amount = -80, Reason = "purchase debit", RelatedEventId = "e2", CreatedAt = now });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var (_, items) = await svc.GetTransactions("u1", 1, 20);

        Assert.AreEqual(2, items.Count);
        Assert.IsTrue(items[0].CreatedAt >= items[1].CreatedAt); // newest first
    }

    // T025a(c): earn transaction has non-null catalogItemId and relatedEventId
    [TestMethod]
    public async Task GetTransactions_EarnTransaction_HasCatalogItemId()
    {
        using var db = CreateDb(nameof(GetTransactions_EarnTransaction_HasCatalogItemId));
        db.TokenTransactions.Add(new TokenTransaction
        {
            Id = Guid.NewGuid(), UserId = "u1", Amount = 80, Reason = "earn",
            RelatedEventId = "evt-1", CatalogItemId = "item-1", LookupTableVersion = "1.0.0",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var (_, items) = await svc.GetTransactions("u1", 1, 20);

        Assert.IsNotNull(items[0].CatalogItemId);
        Assert.IsNotNull(items[0].RelatedEventId);
    }

    // T025a(d): spend transaction has null catalogItemId
    [TestMethod]
    public async Task GetTransactions_SpendTransaction_HasNullCatalogItemId()
    {
        using var db = CreateDb(nameof(GetTransactions_SpendTransaction_HasNullCatalogItemId));
        db.TokenTransactions.Add(new TokenTransaction
        {
            Id = Guid.NewGuid(), UserId = "u1", Amount = -50, Reason = "purchase debit",
            RelatedEventId = "order-1", CatalogItemId = null, LookupTableVersion = null,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var (_, items) = await svc.GetTransactions("u1", 1, 20);

        Assert.IsNull(items[0].CatalogItemId);
    }

    // T025a(e): pageSize cap of 100 is enforced
    [TestMethod]
    public async Task GetTransactions_PageSizeCappedAt100()
    {
        using var db = CreateDb(nameof(GetTransactions_PageSizeCappedAt100));
        // Insert 101 transactions
        for (int i = 0; i < 101; i++)
        {
            db.TokenTransactions.Add(new TokenTransaction
            {
                Id = Guid.NewGuid(), UserId = "u1", Amount = 1, Reason = "earn",
                RelatedEventId = $"evt-{i}", CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var (totalCount, items) = await svc.GetTransactions("u1", 1, 200); // request 200, should be capped

        Assert.AreEqual(101, totalCount);
        Assert.AreEqual(100, items.Count); // capped
    }

    // T025a(f): page beyond total count returns empty items
    [TestMethod]
    public async Task GetTransactions_PageBeyondTotal_ReturnsEmpty()
    {
        using var db = CreateDb(nameof(GetTransactions_PageBeyondTotal_ReturnsEmpty));
        db.TokenTransactions.Add(new TokenTransaction
        {
            Id = Guid.NewGuid(), UserId = "u1", Amount = 80, Reason = "earn",
            RelatedEventId = "e1", CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var (totalCount, items) = await svc.GetTransactions("u1", page: 99, pageSize: 20);

        Assert.AreEqual(1, totalCount);
        Assert.AreEqual(0, items.Count);
    }

    // ── Spend tests ───────────────────────────────────────────────────────

    // T029(a): SpendTokens debits balance and inserts TokenTransaction
    [TestMethod]
    public async Task SpendTokens_DebitsBalanceAndInsertsTransaction()
    {
        using var db = CreateDb(nameof(SpendTokens_DebitsBalanceAndInsertsTransaction));
        db.TokenWallets.Add(new TokenWallet { UserId = "buyer", Balance = 150 });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var (result, newBalance, _) = await svc.SpendTokens("buyer", 80, "order-1");

        Assert.AreEqual(TokenLedgerService.SpendResult.Success, result);
        Assert.AreEqual(70, newBalance);
        var tx = await db.TokenTransactions.SingleAsync();
        Assert.AreEqual(-80, tx.Amount);
        Assert.IsNull(tx.CatalogItemId);
    }

    // T029(b): insufficient balance returns error, balance unchanged
    [TestMethod]
    public async Task SpendTokens_InsufficientBalance_ReturnsError()
    {
        using var db = CreateDb(nameof(SpendTokens_InsufficientBalance_ReturnsError));
        db.TokenWallets.Add(new TokenWallet { UserId = "buyer", Balance = 30 });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var (result, _, _) = await svc.SpendTokens("buyer", 80, "order-1");

        Assert.AreEqual(TokenLedgerService.SpendResult.InsufficientBalance, result);
        var wallet = await db.TokenWallets.FindAsync("buyer");
        Assert.AreEqual(30, wallet!.Balance); // unchanged
        Assert.AreEqual(0, await db.TokenTransactions.CountAsync());
    }

    // T029(c): duplicate orderId returns AlreadyProcessed without double-debit
    [TestMethod]
    public async Task SpendTokens_DuplicateOrderId_ReturnsAlreadyProcessed()
    {
        using var db = CreateDb(nameof(SpendTokens_DuplicateOrderId_ReturnsAlreadyProcessed));
        db.TokenWallets.Add(new TokenWallet { UserId = "buyer", Balance = 150 });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        await svc.SpendTokens("buyer", 80, "order-1");
        var (result, _, _) = await svc.SpendTokens("buyer", 80, "order-1");

        Assert.AreEqual(TokenLedgerService.SpendResult.AlreadyProcessed, result);
        var wallet = await db.TokenWallets.FindAsync("buyer");
        Assert.AreEqual(70, wallet!.Balance); // not debited twice
    }

    [TestMethod]
    public async Task SpendTokens_CheckoutDebit_OrderIdIsIdempotent()
    {
        using var db = CreateDb(nameof(SpendTokens_CheckoutDebit_OrderIdIsIdempotent));
        db.TokenWallets.Add(new TokenWallet { UserId = "buyer", Balance = 200 });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var first = await svc.SpendTokens("buyer", 90, "checkout-order-1");
        var second = await svc.SpendTokens("buyer", 90, "checkout-order-1");

        Assert.AreEqual(TokenLedgerService.SpendResult.Success, first.result);
        Assert.AreEqual(TokenLedgerService.SpendResult.AlreadyProcessed, second.result);
        var wallet = await db.TokenWallets.FindAsync("buyer");
        Assert.AreEqual(110, wallet!.Balance);
        Assert.AreEqual(1, await db.TokenTransactions.CountAsync(t => t.RelatedEventId == "checkout-order-1"));
    }

    // T029(d): concurrent debit triggers RowVersion retry
    [TestMethod]
    public async Task SpendTokens_ConcurrencyException_Retries()
    {
        var dbName = nameof(SpendTokens_ConcurrencyException_Retries);

        // Seed wallet using a regular context so the throw count is not consumed
        using (var seedDb = CreateDb(dbName))
        {
            seedDb.TokenWallets.Add(new TokenWallet { UserId = "buyer", Balance = 150 });
            await seedDb.SaveChangesAsync();
        }

        using var db = new ThrowingOnFirstSaveTokenDbContext(
            new DbContextOptionsBuilder<TokenDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options,
            throwsOnSave: 1);
        var svc = CreateService(db, maxRetries: 3);

        var (result, newBalance, _) = await svc.SpendTokens("buyer", 80, "order-retry");

        Assert.AreEqual(TokenLedgerService.SpendResult.Success, result);
        Assert.AreEqual(70, newBalance);
    }

    // T029(e): final balance is >= 0 after concurrent debits exhausting balance
    [TestMethod]
    public async Task SpendTokens_BalanceNeverGoesNegative()
    {
        using var db = CreateDb(nameof(SpendTokens_BalanceNeverGoesNegative));
        db.TokenWallets.Add(new TokenWallet { UserId = "buyer", Balance = 80 });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var (r1, b1, _) = await svc.SpendTokens("buyer", 80, "order-a");
        var (r2, _, _) = await svc.SpendTokens("buyer", 80, "order-b"); // Should fail

        Assert.AreEqual(TokenLedgerService.SpendResult.Success, r1);
        Assert.AreEqual(TokenLedgerService.SpendResult.InsufficientBalance, r2);
        Assert.AreEqual(0, b1);
    }

    // T029(f): amount = 0 returns ValidationError, no DB write
    [TestMethod]
    public async Task SpendTokens_ZeroAmount_ReturnsValidationError()
    {
        using var db = CreateDb(nameof(SpendTokens_ZeroAmount_ReturnsValidationError));
        db.TokenWallets.Add(new TokenWallet { UserId = "buyer", Balance = 100 });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var (result, _, _) = await svc.SpendTokens("buyer", 0, "order-z");

        Assert.AreEqual(TokenLedgerService.SpendResult.ValidationError, result);
        Assert.AreEqual(0, await db.TokenTransactions.CountAsync());
    }

    // T029(g): successful debit emits a structured Information log entry
    [TestMethod]
    public async Task SpendTokens_Success_EmitsStructuredLog()
    {
        using var db = CreateDb(nameof(SpendTokens_Success_EmitsStructuredLog));
        db.TokenWallets.Add(new TokenWallet { UserId = "buyer", Balance = 150 });
        await db.SaveChangesAsync();

        var optsMon = Substitute.For<IOptionsMonitor<TokenOptions>>();
        optsMon.CurrentValue.Returns(new TokenOptions { MaxConcurrencyRetries = 3 });
        var loggerMock = Substitute.For<ILogger<TokenLedgerService>>();
        var svc = new TokenLedgerService(db, optsMon, loggerMock);

        await svc.SpendTokens("buyer", 80, "order-log");

        loggerMock.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [TestMethod]
    public async Task RefundTokens_DuplicateOrderId_IsIdempotent()
    {
        using var db = CreateDb(nameof(RefundTokens_DuplicateOrderId_IsIdempotent));
        db.TokenWallets.Add(new TokenWallet { UserId = "buyer", Balance = 10 });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.RefundTokens("buyer", 90, "checkout-order-2", "order_persistence_failed");
        await svc.RefundTokens("buyer", 90, "checkout-order-2", "order_persistence_failed");

        var wallet = await db.TokenWallets.FindAsync("buyer");
        Assert.AreEqual(100, wallet!.Balance);
        Assert.AreEqual(1, await db.TokenTransactions.CountAsync(t => t.RelatedEventId == "refund:checkout-order-2"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static async Task SeedLookupAsync(TokenDbContext db)
    {
        var effectiveFrom = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        db.TokenAwardLookupEntries.Add(new TokenAwardLookupEntry
        {
            Id = Guid.NewGuid(),
            ClubCategory = "Driver",
            ConditionGrade = "Excellent",
            TokenAmount = 80,
            TableVersion = "1.0.0",
            EffectiveFrom = effectiveFrom,
        });
        await db.SaveChangesAsync();
    }

    // Subclass that throws DbUpdateConcurrencyException on the first N saves
    private class ThrowingOnFirstSaveTokenDbContext : TokenDbContext
    {
        private int _throwsRemaining;

        public ThrowingOnFirstSaveTokenDbContext(DbContextOptions<TokenDbContext> options, int throwsOnSave)
            : base(options)
        {
            _throwsRemaining = throwsOnSave;
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_throwsRemaining > 0)
            {
                _throwsRemaining--;
                throw new DbUpdateConcurrencyException("Simulated concurrency conflict", []);
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
