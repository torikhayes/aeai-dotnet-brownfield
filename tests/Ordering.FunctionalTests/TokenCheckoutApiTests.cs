using System.Net;
using System.Text;
using System.Text.Json;
using Asp.Versioning;
using Asp.Versioning.Http;
using eShop.Ordering.API.Application.IntegrationEvents;
using eShop.Ordering.API.Application.IntegrationEvents.Events;
using eShop.Ordering.API.Application.Models;
using eShop.Ordering.API.Application.Services;
using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace eShop.Ordering.FunctionalTests;

public sealed class TokenCheckoutApiTests : IClassFixture<OrderingApiFixture>
{
    private readonly OrderingApiFixture _fixture;

    public TokenCheckoutApiTests(OrderingApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateOrder_WithTokens_WhenSpendSucceeds_ReturnsOk()
    {
        _fixture.FakeTokenSpendClient.Reset(FakeTokenSpendMode.Success);
        using var client = CreateClient();

        var response = await PostCreateOrderAsync(client, BuildOrderRequest(CheckoutPaymentMethod.Tokens), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithTokens_WhenInsufficientBalance_ReturnsBadRequest()
    {
        _fixture.FakeTokenSpendClient.Reset(FakeTokenSpendMode.InsufficientBalance);
        using var client = CreateClient();

        var response = await PostCreateOrderAsync(client, BuildOrderRequest(CheckoutPaymentMethod.Tokens), Guid.NewGuid());
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("insufficient_balance", body);
    }

    [Fact]
    public async Task CreateOrder_WithCash_DoesNotInvokeTokenSpend()
    {
        _fixture.FakeTokenSpendClient.Reset(FakeTokenSpendMode.InsufficientBalance);
        using var client = CreateClient();

        var response = await PostCreateOrderAsync(client, BuildOrderRequest(CheckoutPaymentMethod.Cash), Guid.NewGuid());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, _fixture.FakeTokenSpendClient.SpendCallCount);
    }

    [Fact]
    public async Task CreateOrder_WithTokens_WhenDebitRejected_ReturnsErrorResponse()
    {
        _fixture.FakeTokenSpendClient.Reset(FakeTokenSpendMode.InsufficientBalance);
        using var client = CreateClient();

        var response = await PostCreateOrderAsync(client, BuildOrderRequest(CheckoutPaymentMethod.Tokens), Guid.NewGuid());
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("insufficient_balance", body);
    }

    [Fact]
    public async Task CreateOrder_WithTokens_WhenPersistenceFails_EmitsCompensationEvent()
    {
        _fixture.FakeTokenSpendClient.Reset(FakeTokenSpendMode.Success);
        var capture = new CapturingOrderingIntegrationEventService();
        var failingRepository = new FailingOrderRepository();
        using var client = CreateClient(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IOrderingIntegrationEventService>();
                services.AddSingleton<IOrderingIntegrationEventService>(capture);

                services.RemoveAll<IOrderRepository>();
                services.AddSingleton<IOrderRepository>(failingRepository);
            });
        });

        var response = await PostCreateOrderAsync(client, BuildOrderRequest(CheckoutPaymentMethod.Tokens), Guid.NewGuid());
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("order_create_failed", body);
        Assert.Equal(1, _fixture.FakeTokenSpendClient.SpendCallCount);
    }

    private HttpClient CreateClient(Action<IWebHostBuilder>? configure = null)
    {
        var versionHandler = new ApiVersionHandler(new QueryStringApiVersionWriter(), new ApiVersion(1.0));

        WebApplicationFactory<Program> factory = _fixture;
        if (configure is not null)
        {
            factory = factory.WithWebHostBuilder(configure);
        }

        return factory.CreateDefaultClient(versionHandler);
    }

    private static async Task<HttpResponseMessage> PostCreateOrderAsync(HttpClient client, CreateOrderRequest request, Guid requestId)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "api/orders")
        {
            Content = new StringContent(JsonSerializer.Serialize(request), UTF8Encoding.UTF8, "application/json")
        };
        message.Headers.Add("x-requestid", requestId.ToString());

        return await client.SendAsync(message, TestContext.Current.CancellationToken);
    }

    private static CreateOrderRequest BuildOrderRequest(CheckoutPaymentMethod paymentMethod)
    {
        var item = new BasketItem
        {
            Id = "basket-1",
            ProductId = 42,
            ProductName = "Test Club",
            UnitPrice = 90m,
            OldUnitPrice = 0m,
            Quantity = 1,
            PictureUrl = "pic"
        };

        return new CreateOrderRequest(
            UserId: "buyer-1",
            UserName: "Buyer",
            City: "Austin",
            Street: "123 Pine St",
            State: "TX",
            Country: "US",
            ZipCode: "73301",
            CardNumber: "XXXXXXXXXXXX0005",
            CardHolderName: "Buyer",
            CardExpiration: DateTime.UtcNow.AddYears(1),
            CardSecurityNumber: "123",
            CardTypeId: 1,
            Buyer: "buyer-1",
            Items: [item],
            PaymentMethod: paymentMethod);
    }
}
