namespace eShop.Ordering.API.Application.Commands;

using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;
using eShop.Ordering.API.Application.Services;
using eShop.Ordering.API.Application.IntegrationEvents.Events;

// Regular CommandHandler
public class CreateOrderCommandHandler
    : IRequestHandler<CreateOrderCommand, bool>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IIdentityService _identityService;
    private readonly IMediator _mediator;
    private readonly IOrderingIntegrationEventService _orderingIntegrationEventService;
    private readonly ITokenSpendClient _tokenSpendClient;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    // Using DI to inject infrastructure persistence Repositories
    public CreateOrderCommandHandler(IMediator mediator,
        IOrderingIntegrationEventService orderingIntegrationEventService,
        IOrderRepository orderRepository,
        IIdentityService identityService,
        ITokenSpendClient tokenSpendClient,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _orderingIntegrationEventService = orderingIntegrationEventService ?? throw new ArgumentNullException(nameof(orderingIntegrationEventService));
        _tokenSpendClient = tokenSpendClient ?? throw new ArgumentNullException(nameof(tokenSpendClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(CreateOrderCommand message, CancellationToken cancellationToken)
    {
        // Add Integration event to clean the basket
        var orderStartedIntegrationEvent = new OrderStartedIntegrationEvent(message.UserId);
        await _orderingIntegrationEventService.AddAndSaveEventAsync(orderStartedIntegrationEvent);

        // Add/Update the Buyer AggregateRoot
        // DDD patterns comment: Add child entities and value-objects through the Order Aggregate-Root
        // methods and constructor so validations, invariants and business logic 
        // make sure that consistency is preserved across the whole aggregate
        var address = new Address(message.Street, message.City, message.State, message.Country, message.ZipCode);
        var tokensApplied = message.PaymentMethod == OrderPaymentMethod.Tokens
            ? message.OrderItems.Sum(item => (int)Math.Round(item.UnitPrice * item.Units, MidpointRounding.AwayFromZero))
            : 0;

        var spendOrderId = Guid.NewGuid().ToString("N");

        if (message.PaymentMethod == OrderPaymentMethod.Tokens)
        {
            var spendResult = await _tokenSpendClient.SpendAsync(message.UserId, tokensApplied, spendOrderId, cancellationToken);
            if (spendResult.Result == TokenSpendResult.InsufficientBalance)
            {
                throw new InsufficientTokenBalanceException("insufficient_balance");
            }

            if (spendResult.Result == TokenSpendResult.ServiceUnavailable)
            {
                throw new TokenServiceUnavailableException("token_service_unavailable");
            }

            if (spendResult.Result != TokenSpendResult.Success)
            {
                _logger.LogWarning("Token spend failed for user {UserId} with result {Result}", message.UserId, spendResult.Result);
                throw new TokenServiceUnavailableException("token_service_unavailable");
            }
        }

        var order = new Order(message.UserId, message.UserName, address, message.CardTypeId, message.CardNumber, message.CardSecurityNumber,
            message.CardHolderName, message.CardExpiration, orderPaymentMethod: message.PaymentMethod, tokensApplied: tokensApplied);

        foreach (var item in message.OrderItems)
        {
            order.AddOrderItem(item.ProductId, item.ProductName, item.UnitPrice, item.Discount, item.PictureUrl, item.Units);
        }

        _logger.LogInformation("Creating Order - Order: {@Order}", order);

        _orderRepository.Add(order);

        var saved = await _orderRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
        if (!saved && message.PaymentMethod == OrderPaymentMethod.Tokens)
        {
            var rollbackEvent = new OrderCreationFailedIntegrationEvent(message.UserId, spendOrderId, tokensApplied, "order_persistence_failed");
            await _orderingIntegrationEventService.AddAndSaveEventAsync(rollbackEvent);
        }

        return saved;
    }
}


// Use for Idempotency in Command process
public class CreateOrderIdentifiedCommandHandler : IdentifiedCommandHandler<CreateOrderCommand, bool>
{
    public CreateOrderIdentifiedCommandHandler(
        IMediator mediator,
        IRequestManager requestManager,
        ILogger<IdentifiedCommandHandler<CreateOrderCommand, bool>> logger)
        : base(mediator, requestManager, logger)
    {
    }

    protected override bool CreateResultForDuplicateRequest()
    {
        return true; // Ignore duplicate requests for creating order.
    }
}
