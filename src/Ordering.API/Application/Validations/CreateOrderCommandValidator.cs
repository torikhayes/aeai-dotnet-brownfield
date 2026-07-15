namespace eShop.Ordering.API.Application.Validations;

using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator(ILogger<CreateOrderCommandValidator> logger)
    {
        RuleFor(command => command.City).NotEmpty();
        RuleFor(command => command.Street).NotEmpty();
        RuleFor(command => command.State).NotEmpty();
        RuleFor(command => command.Country).NotEmpty();
        RuleFor(command => command.ZipCode).NotEmpty();
        RuleFor(command => command.CardNumber)
            .NotEmpty()
            .Length(12, 19)
            .When(command => command.PaymentMethod == OrderPaymentMethod.Cash);
        RuleFor(command => command.CardHolderName)
            .NotEmpty()
            .When(command => command.PaymentMethod == OrderPaymentMethod.Cash);
        RuleFor(command => command.CardExpiration)
            .NotEmpty()
            .Must(BeValidExpirationDate)
            .WithMessage("Please specify a valid card expiration date")
            .When(command => command.PaymentMethod == OrderPaymentMethod.Cash);
        RuleFor(command => command.CardSecurityNumber)
            .NotEmpty()
            .Length(3)
            .When(command => command.PaymentMethod == OrderPaymentMethod.Cash);
        RuleFor(command => command.CardTypeId)
            .NotEmpty()
            .When(command => command.PaymentMethod == OrderPaymentMethod.Cash);
        RuleFor(command => command.OrderItems).Must(ContainOrderItems).WithMessage("No order items found");

        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("INSTANCE CREATED - {ClassName}", GetType().Name);
        }
    }

    private bool BeValidExpirationDate(DateTime dateTime)
    {
        return dateTime >= DateTime.UtcNow;
    }

    private bool ContainOrderItems(IEnumerable<OrderItemDTO> orderItems)
    {
        return orderItems.Any();
    }
}
