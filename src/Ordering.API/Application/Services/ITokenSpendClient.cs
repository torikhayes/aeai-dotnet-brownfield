namespace eShop.Ordering.API.Application.Services;

public enum TokenSpendResult
{
    Success,
    InsufficientBalance,
    AlreadyProcessed,
    ServiceUnavailable,
    UnknownFailure,
}

public interface ITokenSpendClient
{
    Task<(TokenSpendResult Result, int NewBalance)> SpendAsync(string userId, int amount, string orderId, CancellationToken cancellationToken = default);
}
