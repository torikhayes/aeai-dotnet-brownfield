namespace eShop.PaymentProcessor.IntegrationEvents.EventHandling;

public class OrderCreationFailedIntegrationEventHandler(
    TokenLedgerService tokenLedgerService,
    ILogger<OrderCreationFailedIntegrationEventHandler> logger)
    : IIntegrationEventHandler<OrderCreationFailedIntegrationEvent>
{
    public async Task Handle(OrderCreationFailedIntegrationEvent @event)
    {
        logger.LogInformation("Handling compensation event: {IntegrationEventId} - ({@IntegrationEvent})", @event.Id, @event);
        await tokenLedgerService.RefundTokens(@event.UserId, @event.Amount, @event.OrderId, @event.Reason);
    }
}
