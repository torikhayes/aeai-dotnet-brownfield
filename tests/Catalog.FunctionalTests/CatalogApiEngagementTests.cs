using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Asp.Versioning;
using eShop.Catalog.API.Model;

namespace eShop.Catalog.FunctionalTests;

[Collection("Catalog tests")]
public sealed class CatalogApiEngagementTests : IClassFixture<CatalogApiFixture>
{
    private readonly CatalogApiFixture _fixture;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public CatalogApiEngagementTests(CatalogApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetItemIncrementsViewCount()
    {
        var client = _fixture.CreateClient(new ApiVersion(2.0));

        var firstResponse = await client.GetAsync("/api/catalog/items/1", TestContext.Current.CancellationToken);
        firstResponse.EnsureSuccessStatusCode();
        var firstBody = await firstResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var firstItem = JsonSerializer.Deserialize<CatalogItem>(firstBody, _jsonSerializerOptions);

        var secondResponse = await client.GetAsync("/api/catalog/items/1", TestContext.Current.CancellationToken);
        secondResponse.EnsureSuccessStatusCode();
        var secondBody = await secondResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var secondItem = JsonSerializer.Deserialize<CatalogItem>(secondBody, _jsonSerializerOptions);

        Assert.Equal(firstItem.ViewCount + 1, secondItem.ViewCount);
    }

    [Fact]
    public async Task FavoriteToggleUpdatesFavoriteCount()
    {
        var client = _fixture.CreateAuthenticatedClient(new ApiVersion(2.0), "buyer-3");

        var response = await client.PostAsync("/api/catalog/items/1/favorite", null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        response = await client.GetAsync("/api/catalog/items/1", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var item = JsonSerializer.Deserialize<CatalogItem>(body, _jsonSerializerOptions);
        Assert.True(item.FavoriteCount >= 1);

        response = await client.PostAsync("/api/catalog/items/1/favorite", null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        response = await client.GetAsync("/api/catalog/items/1", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        item = JsonSerializer.Deserialize<CatalogItem>(body, _jsonSerializerOptions);

        Assert.Equal(0, item.FavoriteCount);
    }
}