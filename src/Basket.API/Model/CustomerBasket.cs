namespace eShop.Basket.API.Model;

public enum BasketPaymentMethod
{
    Cash = 0,
    Tokens = 1
}

public class CustomerBasket
{
    public string BuyerId { get; set; }

    public List<BasketItem> Items { get; set; } = [];

    public BasketPaymentMethod PaymentMethod { get; set; } = BasketPaymentMethod.Cash;

    public CustomerBasket() { }

    public CustomerBasket(string customerId)
    {
        BuyerId = customerId;
    }
}
