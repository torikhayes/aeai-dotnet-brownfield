namespace eShop.PaymentProcessor.TokenLedger.Services;

using System.Diagnostics;
using Microsoft.Extensions.Logging;

public class TokenLedgerService(
    TokenDbContext db,
    IOptionsMonitor<TokenOptions> options,
    ILogger<TokenLedgerService> logger)
{
    private static readonly ActivitySource ActivitySource = new("PaymentProcessor.TokenLedger");

    // ── US1: Earn tokens when a listing is verified ────────────────────────

    public async Task AwardTokens(ClubListingVerifiedIntegrationEvent @event)
    {
        if (string.IsNullOrWhiteSpace(@event.SellerId))
        {
            logger.LogInformation("Skipping token award: no SellerId on event {EventId}", @event.Id);
            return;
        }

        var eventId = @event.Id.ToString();

        // Idempotency: skip if already processed this event
        if (await db.TokenTransactions.AnyAsync(t => t.RelatedEventId == eventId))
        {
            return;
        }

        // Idempotency: skip if this listing was already awarded
        if (await db.TokenAwardedListings.AnyAsync(l => l.CatalogItemId == @event.CatalogItemId))
        {
            return;
        }

        var lookupEntry = await GetActiveLookupEntryAsync(@event.Category, @event.Condition);
        if (lookupEntry is null)
        {
            logger.LogWarning("No active lookup entry for {Category}/{Condition}", @event.Category, @event.Condition);
            return;
        }

        var reason = $"{@event.Category}/{@event.Condition} listing verified";
        var maxRetries = options.CurrentValue.MaxConcurrencyRetries;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var activity = ActivitySource.StartActivity("AwardTokens");
                activity?.SetTag("token.userId", @event.SellerId);
                activity?.SetTag("token.amount", lookupEntry.TokenAmount);
                activity?.SetTag("token.reason", reason);

                var wallet = await db.TokenWallets.FindAsync(@event.SellerId);
                if (wallet is null)
                {
                    wallet = new TokenWallet { UserId = @event.SellerId, Balance = 0 };
                    db.TokenWallets.Add(wallet);
                }

                wallet.Balance += lookupEntry.TokenAmount;

                var transaction = new TokenTransaction
                {
                    Id = Guid.NewGuid(),
                    UserId = @event.SellerId,
                    Amount = lookupEntry.TokenAmount,
                    Reason = reason,
                    RelatedEventId = eventId,
                    LookupTableVersion = lookupEntry.TableVersion,
                    CatalogItemId = @event.CatalogItemId,
                    CreatedAt = DateTime.UtcNow,
                };
                db.TokenTransactions.Add(transaction);

                db.TokenAwardedListings.Add(new TokenAwardedListing
                {
                    CatalogItemId = @event.CatalogItemId,
                    TransactionId = transaction.Id,
                    AwardedAt = DateTime.UtcNow,
                });

                await db.SaveChangesAsync();

                logger.LogInformation(
                    "Token award: UserId={UserId} Amount={Amount} Reason={Reason} RelatedEventId={RelatedEventId}",
                    @event.SellerId, lookupEntry.TokenAmount, reason, eventId);

                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries - 1)
            {
                // Detach stale entries and retry
                foreach (var entry in db.ChangeTracker.Entries())
                {
                    entry.State = EntityState.Detached;
                }
            }
        }

        throw new InvalidOperationException(
            $"Could not commit token award after {maxRetries} attempts.");
    }

    // ── US2: Get balance ───────────────────────────────────────────────────

    public async Task<int> GetBalance(string userId)
    {
        var wallet = await db.TokenWallets.FindAsync(userId);
        return wallet?.Balance ?? 0;
    }

    // ── US3: Get transaction history ───────────────────────────────────────

    public async Task<(int totalCount, IReadOnlyList<TokenTransaction> items)> GetTransactions(
        string userId, int page, int pageSize)
    {
        const int MaxPageSize = 100;
        pageSize = Math.Min(pageSize, MaxPageSize);
        page = Math.Max(page, 1);

        var query = db.TokenTransactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (totalCount, items);
    }

    // ── US4: Reward preview ────────────────────────────────────────────────

    public async Task<(int tokenAmount, string tableVersion)?> GetRewardPreview(
        string category, string condition)
    {
        var entry = await GetActiveLookupEntryAsync(category, condition);
        if (entry is null) return null;
        return (entry.TokenAmount, entry.TableVersion);
    }

    // ── FR-011: Spend tokens ───────────────────────────────────────────────

    public enum SpendResult { Success, InsufficientBalance, AlreadyProcessed, ValidationError, RetriesExhausted }

    public async Task<(SpendResult result, int newBalance, string? error)> SpendTokens(
        string userId, int amount, string orderId)
    {
        if (amount <= 0)
        {
            return (SpendResult.ValidationError, 0, "amount must be a positive integer");
        }

        // Idempotency: skip if already processed
        if (await db.TokenTransactions.AnyAsync(t => t.RelatedEventId == orderId))
        {
            return (SpendResult.AlreadyProcessed, 0, $"A spend transaction for orderId '{orderId}' already exists.");
        }

        var maxRetries = options.CurrentValue.MaxConcurrencyRetries;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var activity = ActivitySource.StartActivity("SpendTokens");
                activity?.SetTag("token.userId", userId);
                activity?.SetTag("token.amount", amount);
                activity?.SetTag("token.reason", "purchase debit");

                var wallet = await db.TokenWallets.FindAsync(userId);
                var currentBalance = wallet?.Balance ?? 0;

                if (currentBalance - amount < 0)
                {
                    return (SpendResult.InsufficientBalance, currentBalance,
                        $"User has {currentBalance} tokens; requested debit of {amount} would result in a negative balance.");
                }

                if (wallet is null)
                {
                    wallet = new TokenWallet { UserId = userId, Balance = 0 };
                    db.TokenWallets.Add(wallet);
                }

                wallet.Balance -= amount;

                db.TokenTransactions.Add(new TokenTransaction
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Amount = -amount,
                    Reason = "purchase debit",
                    RelatedEventId = orderId,
                    LookupTableVersion = null,
                    CatalogItemId = null,
                    CreatedAt = DateTime.UtcNow,
                });

                await db.SaveChangesAsync();

                logger.LogInformation(
                    "Token debit: UserId={UserId} Amount={Amount} Reason={Reason} RelatedEventId={RelatedEventId}",
                    userId, amount, "purchase debit", orderId);

                return (SpendResult.Success, wallet.Balance, null);
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries - 1)
            {
                foreach (var entry in db.ChangeTracker.Entries())
                {
                    entry.State = EntityState.Detached;
                }
            }
        }

        return (SpendResult.RetriesExhausted, 0, "Could not commit balance update after maximum retries.");
    }

    // ── Internal helpers ──────────────────────────────────────────────────

    private Task<TokenAwardLookupEntry?> GetActiveLookupEntryAsync(string category, string condition)
        => db.TokenAwardLookupEntries
            .Where(e => e.ClubCategory == category
                     && e.ConditionGrade == condition
                     && e.EffectiveFrom <= DateTime.UtcNow)
            .OrderByDescending(e => e.EffectiveFrom)
            .FirstOrDefaultAsync();
}
