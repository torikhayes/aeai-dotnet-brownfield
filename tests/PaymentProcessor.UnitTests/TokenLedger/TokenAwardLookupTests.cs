namespace eShop.PaymentProcessor.UnitTests.TokenLedger;

using Microsoft.Extensions.Logging.Abstractions;

[TestClass]
public class TokenAwardLookupTests
{
    private static TokenDbContext CreateDb(string dbName)
    {
        var opts = new DbContextOptionsBuilder<TokenDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new TokenDbContext(opts);
    }

    private static TokenLedgerService CreateService(TokenDbContext db)
    {
        var optsMon = Substitute.For<IOptionsMonitor<TokenOptions>>();
        optsMon.CurrentValue.Returns(new TokenOptions { MaxConcurrencyRetries = 3 });
        return new TokenLedgerService(db, optsMon, NullLogger<TokenLedgerService>.Instance);
    }

    // T015(a): correct seed values returned for all 28 (category × condition) pairs
    [TestMethod]
    public async Task SeedValues_All28Pairs_ReturnCorrectAmounts()
    {
        using var db = CreateDb(nameof(SeedValues_All28Pairs_ReturnCorrectAmounts));
        var seeder = new TokenDbSeeder();
        await seeder.SeedAsync(db);
        var svc = CreateService(db);

        var expectedValues = new Dictionary<(string category, string condition), int>
        {
            [("Driver", "New")] = 100,
            [("Driver", "Excellent")] = 80,
            [("Driver", "Good")] = 60,
            [("Driver", "Fair")] = 40,
            [("Fairway Wood", "New")] = 80,
            [("Fairway Wood", "Excellent")] = 65,
            [("Fairway Wood", "Good")] = 50,
            [("Fairway Wood", "Fair")] = 30,
            [("Hybrid", "New")] = 70,
            [("Hybrid", "Excellent")] = 55,
            [("Hybrid", "Good")] = 40,
            [("Hybrid", "Fair")] = 25,
            [("Iron Set", "New")] = 120,
            [("Iron Set", "Excellent")] = 95,
            [("Iron Set", "Good")] = 70,
            [("Iron Set", "Fair")] = 45,
            [("Wedge", "New")] = 60,
            [("Wedge", "Excellent")] = 48,
            [("Wedge", "Good")] = 35,
            [("Wedge", "Fair")] = 20,
            [("Putter", "New")] = 90,
            [("Putter", "Excellent")] = 72,
            [("Putter", "Good")] = 54,
            [("Putter", "Fair")] = 35,
            [("Other", "New")] = 50,
            [("Other", "Excellent")] = 40,
            [("Other", "Good")] = 30,
            [("Other", "Fair")] = 15,
        };

        foreach (var ((category, condition), expectedAmount) in expectedValues)
        {
            var result = await svc.GetRewardPreview(category, condition);
            Assert.IsNotNull(result, $"No lookup entry for {category}/{condition}");
            Assert.AreEqual(expectedAmount, result.Value.tokenAmount,
                $"Wrong amount for {category}/{condition}: expected {expectedAmount}, got {result.Value.tokenAmount}");
        }

        Assert.AreEqual(28, expectedValues.Count, "Exactly 28 entries expected");
    }

    // T015(b): active version is latest EffectiveFrom ≤ UtcNow
    [TestMethod]
    public async Task GetRewardPreview_ReturnsLatestActiveVersion()
    {
        using var db = CreateDb(nameof(GetRewardPreview_ReturnsLatestActiveVersion));
        var past = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var recent = DateTime.UtcNow.AddHours(-1);

        db.TokenAwardLookupEntries.AddRange(
            new TokenAwardLookupEntry { Id = Guid.NewGuid(), ClubCategory = "Driver", ConditionGrade = "Good", TokenAmount = 60, TableVersion = "1.0.0", EffectiveFrom = past },
            new TokenAwardLookupEntry { Id = Guid.NewGuid(), ClubCategory = "Driver", ConditionGrade = "Good", TokenAmount = 65, TableVersion = "1.1.0", EffectiveFrom = recent });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var result = await svc.GetRewardPreview("Driver", "Good");

        Assert.IsNotNull(result);
        Assert.AreEqual(65, result.Value.tokenAmount);   // latest active version
        Assert.AreEqual("1.1.0", result.Value.tableVersion);
    }

    // T015(c): future EffectiveFrom row is not returned
    [TestMethod]
    public async Task GetRewardPreview_FutureEffectiveFrom_IsNotReturned()
    {
        using var db = CreateDb(nameof(GetRewardPreview_FutureEffectiveFrom_IsNotReturned));
        var futureDate = DateTime.UtcNow.AddDays(1);

        db.TokenAwardLookupEntries.Add(new TokenAwardLookupEntry
        {
            Id = Guid.NewGuid(),
            ClubCategory = "Wedge",
            ConditionGrade = "New",
            TokenAmount = 999,
            TableVersion = "2.0.0",
            EffectiveFrom = futureDate,
        });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var result = await svc.GetRewardPreview("Wedge", "New");

        Assert.IsNull(result, "Future-dated entry should not be returned");
    }

    // T026a(a): GetRewardPreview returns correct tokenAmount and tableVersion
    [TestMethod]
    public async Task GetRewardPreview_ValidPair_ReturnsCorrectAmountAndVersion()
    {
        using var db = CreateDb(nameof(GetRewardPreview_ValidPair_ReturnsCorrectAmountAndVersion));
        db.TokenAwardLookupEntries.Add(new TokenAwardLookupEntry
        {
            Id = Guid.NewGuid(),
            ClubCategory = "Putter",
            ConditionGrade = "Excellent",
            TokenAmount = 72,
            TableVersion = "1.0.0",
            EffectiveFrom = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var result = await svc.GetRewardPreview("Putter", "Excellent");

        Assert.IsNotNull(result);
        Assert.AreEqual(72, result.Value.tokenAmount);
        Assert.AreEqual("1.0.0", result.Value.tableVersion);
    }

    // T026a(b): GetRewardPreview returns null for unknown combination
    [TestMethod]
    public async Task GetRewardPreview_UnknownCombination_ReturnsNull()
    {
        using var db = CreateDb(nameof(GetRewardPreview_UnknownCombination_ReturnsNull));
        var svc = CreateService(db);

        var result = await svc.GetRewardPreview("UnknownCategory", "UnknownCondition");

        Assert.IsNull(result);
    }

    // T026a(c): active entry is the latest EffectiveFrom ≤ UtcNow, not a future-dated row
    [TestMethod]
    public async Task GetRewardPreview_MultipleVersions_ReturnsMostRecentNotFuture()
    {
        using var db = CreateDb(nameof(GetRewardPreview_MultipleVersions_ReturnsMostRecentNotFuture));
        var now = DateTime.UtcNow;
        db.TokenAwardLookupEntries.AddRange(
            new TokenAwardLookupEntry { Id = Guid.NewGuid(), ClubCategory = "Hybrid", ConditionGrade = "Fair", TokenAmount = 25, TableVersion = "1.0.0", EffectiveFrom = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new TokenAwardLookupEntry { Id = Guid.NewGuid(), ClubCategory = "Hybrid", ConditionGrade = "Fair", TokenAmount = 30, TableVersion = "1.1.0", EffectiveFrom = now.AddHours(-2) },
            new TokenAwardLookupEntry { Id = Guid.NewGuid(), ClubCategory = "Hybrid", ConditionGrade = "Fair", TokenAmount = 999, TableVersion = "2.0.0", EffectiveFrom = now.AddDays(1) } // future
        );
        await db.SaveChangesAsync();
        var svc = CreateService(db);

        var result = await svc.GetRewardPreview("Hybrid", "Fair");

        Assert.IsNotNull(result);
        Assert.AreEqual(30, result.Value.tokenAmount);   // 1.1.0, not 2.0.0 (future)
        Assert.AreEqual("1.1.0", result.Value.tableVersion);
    }
}
