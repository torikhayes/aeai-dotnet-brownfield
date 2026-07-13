using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Asp.Versioning;
using eShop.Catalog.API.Model;

namespace eShop.Catalog.FunctionalTests;

[Collection("Catalog tests")]
public sealed class CatalogApiRatingTests : IClassFixture<CatalogApiFixture>
{
    private readonly CatalogApiFixture _fixture;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public CatalogApiRatingTests(CatalogApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RateItemRequiresAuthentication()
    {
        var client = _fixture.CreateClient(new ApiVersion(2.0));

        var response = await client.PostAsJsonAsync("/api/catalog/items/1/rate", new RateCatalogItemRequest(5), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task RateItemRejectsInvalidValues(int stars)
    {
        var client = _fixture.CreateAuthenticatedClient(new ApiVersion(2.0), "buyer-1");

        var response = await client.PostAsJsonAsync("/api/catalog/items/1/rate", new RateCatalogItemRequest(stars), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RateItemUpdatesAggregateAndUpsertsSameUserRating()
    {
        var client = _fixture.CreateAuthenticatedClient(new ApiVersion(2.0), "buyer-2");

        var response = await client.PostAsJsonAsync("/api/catalog/items/1/rate", new RateCatalogItemRequest(3), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        response = await client.PostAsJsonAsync("/api/catalog/items/1/rate", new RateCatalogItemRequest(5), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        response = await client.GetAsync("/api/catalog/items/1", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var item = JsonSerializer.Deserialize<CatalogItem>(body, _jsonSerializerOptions);

        Assert.Equal(1, item.RatingCount);
        Assert.Equal(5f, item.AverageRating);
    }

    private sealed record RateCatalogItemRequest(int Stars);
}