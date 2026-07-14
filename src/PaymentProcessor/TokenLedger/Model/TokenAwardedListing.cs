namespace eShop.PaymentProcessor.TokenLedger.Model;

public class TokenAwardedListing
{
    public string CatalogItemId { get; set; } = default!;  // PK
    public Guid TransactionId { get; set; }                // FK to TokenTransaction.Id
    public DateTime AwardedAt { get; set; }
}
