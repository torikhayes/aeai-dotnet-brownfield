namespace eShop.PaymentProcessor.IntegrationEvents.EventHandling;

public class ClubListingVerifiedIntegrationEventHandler(
    TokenLedgerService tokenLedgerService,
    ILogger<ClubListingVerifiedIntegrationEventHandler> logger) :
    IIntegrationEventHandler<ClubListingVerifiedIntegrationEvent>
{
    public async Task Handle(ClubListingVerifiedIntegrationEvent @event)
    {
        logger.LogInformation("Handling integration event: {IntegrationEventId} - ({@IntegrationEvent})", @event.Id, @event);

        await tokenLedgerService.AwardTokens(@event);
    }
}
