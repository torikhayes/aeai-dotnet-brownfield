namespace eShop.Ordering.API.Application.Services;

public class InsufficientTokenBalanceException : Exception
{
    public InsufficientTokenBalanceException(string message) : base(message)
    {
    }
}

public class TokenServiceUnavailableException : Exception
{
    public TokenServiceUnavailableException(string message) : base(message)
    {
    }
}
