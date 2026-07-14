namespace eShop.PaymentProcessor.IntegrationEvents.Events;

public record ClubListingVerifiedIntegrationEvent(
    string SellerId,
    string CatalogItemId,
    string Category,
    string Condition
) : IntegrationEvent;
