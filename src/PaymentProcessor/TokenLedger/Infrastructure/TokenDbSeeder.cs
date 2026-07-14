namespace eShop.PaymentProcessor.TokenLedger.Infrastructure;

public class TokenDbSeeder : IDbSeeder<TokenDbContext>
{
    private const string TableVersion = "1.0.0";

    public async Task SeedAsync(TokenDbContext context)
    {
        if (await context.TokenAwardLookupEntries.AnyAsync())
        {
            return; // Already seeded — idempotent
        }

        var effectiveFrom = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var entries = new[]
        {
            ("Driver",       "New",       100),
            ("Driver",       "Excellent",  80),
            ("Driver",       "Good",       60),
            ("Driver",       "Fair",       40),
            ("Fairway Wood", "New",        80),
            ("Fairway Wood", "Excellent",  65),
            ("Fairway Wood", "Good",       50),
            ("Fairway Wood", "Fair",       30),
            ("Hybrid",       "New",        70),
            ("Hybrid",       "Excellent",  55),
            ("Hybrid",       "Good",       40),
            ("Hybrid",       "Fair",       25),
            ("Iron Set",     "New",       120),
            ("Iron Set",     "Excellent",  95),
            ("Iron Set",     "Good",       70),
            ("Iron Set",     "Fair",       45),
            ("Wedge",        "New",        60),
            ("Wedge",        "Excellent",  48),
            ("Wedge",        "Good",       35),
            ("Wedge",        "Fair",       20),
            ("Putter",       "New",        90),
            ("Putter",       "Excellent",  72),
            ("Putter",       "Good",       54),
            ("Putter",       "Fair",       35),
            ("Other",        "New",        50),
            ("Other",        "Excellent",  40),
            ("Other",        "Good",       30),
            ("Other",        "Fair",       15),
        };

        foreach (var (category, condition, amount) in entries)
        {
            context.TokenAwardLookupEntries.Add(new TokenAwardLookupEntry
            {
                Id = Guid.NewGuid(),
                ClubCategory = category,
                ConditionGrade = condition,
                TokenAmount = amount,
                TableVersion = TableVersion,
                EffectiveFrom = effectiveFrom,
            });
        }

        await context.SaveChangesAsync();
    }
}
