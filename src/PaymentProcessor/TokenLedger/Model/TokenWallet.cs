namespace eShop.PaymentProcessor.TokenLedger.Model;

public class TokenWallet
{
    public string UserId { get; set; } = default!;   // PK — Identity sub claim
    public int Balance { get; set; }
}
