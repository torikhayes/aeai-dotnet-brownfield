namespace eShop.PaymentProcessor.TokenLedger.Model;

public class TokenAwardLookupEntry
{
    public Guid Id { get; set; }
    public string ClubCategory { get; set; } = default!;    // e.g. "Driver", "Iron Set"
    public string ConditionGrade { get; set; } = default!;  // "New" | "Excellent" | "Good" | "Fair"
    public int TokenAmount { get; set; }
    public string TableVersion { get; set; } = default!;    // e.g. "1.0.0"
    public DateTime EffectiveFrom { get; set; }
}
