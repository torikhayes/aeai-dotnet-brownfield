namespace eShop.Ordering.API.Application.Models;

public class CustomerBasket
{
    public string BuyerId { get; set; }
    public List<BasketItem> Items { get; set; }
    public CheckoutPaymentMethod PaymentMethod { get; set; } = CheckoutPaymentMethod.Cash;

    public CustomerBasket(string buyerId, List<BasketItem> items, CheckoutPaymentMethod paymentMethod = CheckoutPaymentMethod.Cash)
    {
        BuyerId = buyerId;
        Items = items;
        PaymentMethod = paymentMethod;
    }
}
