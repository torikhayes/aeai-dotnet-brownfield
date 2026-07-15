namespace eShop.Ordering.API.Application.Services;

using System.Net;
using System.Net.Http.Json;

public class TokenSpendClient(HttpClient httpClient, ILogger<TokenSpendClient> logger) : ITokenSpendClient
{
    public async Task<(TokenSpendResult Result, int NewBalance)> SpendAsync(string userId, int amount, string orderId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tokens/spend")
        {
            Content = JsonContent.Create(new
            {
                userId,
                amount,
                orderId,
            })
        };
        request.Headers.Add("X-Internal-Client", "ordering-api");

        var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<SpendResponse>(cancellationToken: cancellationToken);
            return (TokenSpendResult.Success, body?.NewBalance ?? 0);
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return (TokenSpendResult.InsufficientBalance, 0);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return (TokenSpendResult.AlreadyProcessed, 0);
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            return (TokenSpendResult.ServiceUnavailable, 0);
        }

        logger.LogWarning("Token spend request failed with status code {StatusCode}", (int)response.StatusCode);
        return (TokenSpendResult.UnknownFailure, 0);
    }

    private sealed class SpendResponse
    {
        public int NewBalance { get; init; }
    }
}
