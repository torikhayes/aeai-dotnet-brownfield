namespace eShop.PaymentProcessor.TokenLedger.Services;

internal static class TokenLedgerExtensions
{
    public static void AddTokenLedger(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<TokenDbContext>("tokendb");
        builder.Services.AddMigration<TokenDbContext, TokenDbSeeder>();
        builder.Services.AddScoped<TokenLedgerService>();

        builder.Services.AddOptions<TokenOptions>()
            .BindConfiguration(nameof(TokenOptions));
    }
}
