namespace eShop.PaymentProcessor.IntegrationEvents.Events;

public record OrderCreationFailedIntegrationEvent(string UserId, string OrderId, int Amount, string Reason) : IntegrationEvent;
