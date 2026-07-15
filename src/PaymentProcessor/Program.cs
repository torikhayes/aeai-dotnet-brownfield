using eShop.PaymentProcessor.TokenLedger.Apis;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddTokenLedger();

builder.AddDefaultAuthentication();

builder.AddRabbitMqEventBus("EventBus")
    .AddSubscription<OrderStatusChangedToStockConfirmedIntegrationEvent, OrderStatusChangedToStockConfirmedIntegrationEventHandler>()
    .AddSubscription<OrderCreationFailedIntegrationEvent, OrderCreationFailedIntegrationEventHandler>()
    .AddSubscription<ClubListingVerifiedIntegrationEvent, ClubListingVerifiedIntegrationEventHandler>();

builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration(nameof(PaymentOptions));

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapTokensApi();

await app.RunAsync();
