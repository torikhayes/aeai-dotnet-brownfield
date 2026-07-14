namespace eShop.PaymentProcessor.TokenLedger.Apis;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using eShop.ServiceDefaults;

internal static class TokensApi
{
    private static readonly string[] ValidCategories =
        ["Driver", "Fairway Wood", "Hybrid", "Iron Set", "Wedge", "Putter", "Other"];

    private static readonly string[] ValidConditions =
        ["New", "Excellent", "Good", "Fair"];

    public static IEndpointRouteBuilder MapTokensApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tokens");

        // US2: GET /api/tokens/balance
        group.MapGet("/balance", async (HttpContext context, TokenLedgerService service) =>
        {
            var userId = context.User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var balance = await service.GetBalance(userId);
            return Results.Ok(new { balance });
        }).RequireAuthorization();

        // US3: GET /api/tokens/transactions
        group.MapGet("/transactions", async (
            HttpContext context,
            TokenLedgerService service,
            int page = 1,
            int pageSize = 20) =>
        {
            var userId = context.User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var (totalCount, items) = await service.GetTransactions(userId, page, pageSize);

            return Results.Ok(new
            {
                totalCount,
                page,
                pageSize,
                items = items.Select(t => new
                {
                    t.Id,
                    t.Amount,
                    t.Reason,
                    t.LookupTableVersion,
                    t.CreatedAt,
                    t.RelatedEventId,
                    t.CatalogItemId,
                }),
            });
        }).RequireAuthorization();

        // US4: GET /api/tokens/reward-preview (no auth)
        group.MapGet("/reward-preview", async (
            TokenLedgerService service,
            string? category,
            string? condition) =>
        {
            if (string.IsNullOrWhiteSpace(category) || !ValidCategories.Contains(category))
                return Results.BadRequest(new { error = "invalid_parameter", detail = "category is missing or not a recognised value." });

            if (string.IsNullOrWhiteSpace(condition) || !ValidConditions.Contains(condition))
                return Results.BadRequest(new { error = "invalid_parameter", detail = "condition is missing or not a recognised value." });

            var result = await service.GetRewardPreview(category, condition);

            if (result is null)
                return Results.NotFound();

            return Results.Ok(new { tokenAmount = result.Value.tokenAmount, tableVersion = result.Value.tableVersion });
        });

        // FR-011: POST /api/tokens/spend (internal mesh only)
        group.MapPost("/spend", async (
            HttpContext context,
            TokenLedgerService service,
            SpendRequest body) =>
        {
            if (string.IsNullOrWhiteSpace(body.UserId) || string.IsNullOrWhiteSpace(body.OrderId))
                return Results.BadRequest(new { error = "validation_error", detail = "userId and orderId are required." });

            if (body.Amount <= 0)
                return Results.BadRequest(new { error = "validation_error", detail = "amount must be a positive integer." });

            var (result, newBalance, errorDetail) = await service.SpendTokens(body.UserId, body.Amount, body.OrderId);

            return result switch
            {
                TokenLedgerService.SpendResult.Success =>
                    Results.Ok(new { newBalance }),
                TokenLedgerService.SpendResult.AlreadyProcessed =>
                    Results.Conflict(new { error = "already_processed", detail = errorDetail }),
                TokenLedgerService.SpendResult.InsufficientBalance =>
                    Results.BadRequest(new { error = "insufficient_balance", detail = errorDetail }),
                TokenLedgerService.SpendResult.ValidationError =>
                    Results.BadRequest(new { error = "validation_error", detail = errorDetail }),
                TokenLedgerService.SpendResult.RetriesExhausted =>
                    Results.Json(new { title = "Service Unavailable", detail = errorDetail }, statusCode: 503),
                _ => Results.StatusCode(500),
            };
        }).RequireAuthorization();

        return app;
    }

    private record SpendRequest(string UserId, int Amount, string OrderId);
}
