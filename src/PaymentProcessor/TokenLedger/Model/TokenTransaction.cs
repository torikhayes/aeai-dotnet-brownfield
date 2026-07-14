namespace eShop.PaymentProcessor.TokenLedger.Model;

public class TokenTransaction
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public int Amount { get; set; }                        // positive = earn, negative = spend
    public string Reason { get; set; } = default!;        // e.g. "Driver/Excellent listing verified", "purchase debit"
    public string RelatedEventId { get; set; } = default!; // EventId from integration event — idempotency key
    public string? LookupTableVersion { get; set; }        // populated on earn transactions only
    public string? CatalogItemId { get; set; }             // populated on earn transactions only
    public DateTime CreatedAt { get; set; }
}
