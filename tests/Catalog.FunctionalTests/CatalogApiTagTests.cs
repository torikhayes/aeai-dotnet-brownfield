using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Asp.Versioning;
using eShop.Catalog.API.Model;

namespace eShop.Catalog.FunctionalTests;

[Collection("Catalog tests")]
public sealed class CatalogApiTagTests : IClassFixture<CatalogApiFixture>
{
    private readonly CatalogApiFixture _fixture;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public CatalogApiTagTests(CatalogApiFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateListingWithTagsThenSearchByTagReturnsItem()
    {
        var sellerClient = _fixture.CreateAuthenticatedClient(new ApiVersion(2.0), "seller-1");
        var item = new CatalogItem("Test Tag Driver")
        {
            CatalogBrandId = 1,
            CatalogTypeId = 1,
            Price = 42,
            AvailableStock = 1,
            RestockThreshold = 1,
            MaxStockThreshold = 1,
            Tags = "graphite-shaft,left-handed"
        };

        var createResponse = await sellerClient.PostAsJsonAsync("/api/catalog/items", item, TestContext.Current.CancellationToken);
        createResponse.EnsureSuccessStatusCode();

        var searchClient = _fixture.CreateClient(new ApiVersion(2.0));
        var response = await searchClient.GetAsync("/api/catalog/items?pageIndex=0&pageSize=25&tag=left-handed", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<PaginatedItems<CatalogItem>>(body, _jsonSerializerOptions);

        Assert.Contains(result.Data, catalogItem => catalogItem.Name == "Test Tag Driver");
    }

    [Fact]
    public async Task SellerCanUpdateTagsOnOwnListing()
    {
        var sellerClient = _fixture.CreateAuthenticatedClient(new ApiVersion(2.0), "seller-2");
        var item = new CatalogItem("Test Tag Wedge")
        {
            CatalogBrandId = 1,
            CatalogTypeId = 1,
            Price = 55,
            AvailableStock = 1,
            RestockThreshold = 1,
            MaxStockThreshold = 1
        };

        var createResponse = await sellerClient.PostAsJsonAsync("/api/catalog/items", item, TestContext.Current.CancellationToken);
        createResponse.EnsureSuccessStatusCode();

        var createdId = GetCreatedItemId(createResponse);

        var patchResponse = await sellerClient.PatchAsJsonAsync($"/api/catalog/items/{createdId}/tags", new UpdateCatalogItemTagsRequest(["tour-issue", "left-handed"]), TestContext.Current.CancellationToken);
        patchResponse.EnsureSuccessStatusCode();

        var getResponse = await sellerClient.GetAsync($"/api/catalog/items/{createdId}", TestContext.Current.CancellationToken);
        getResponse.EnsureSuccessStatusCode();

        var body = await getResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var updatedItem = JsonSerializer.Deserialize<CatalogItem>(body, _jsonSerializerOptions);

        Assert.Equal("tour-issue,left-handed", updatedItem.Tags);
    }

    private static int GetCreatedItemId(HttpResponseMessage response)
    {
        var location = response.Headers.Location ?? throw new InvalidOperationException("Create response did not include a location header.");
        var locationValue = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
        var lastSegment = locationValue.TrimEnd('/').Split('/').Last();
        return int.Parse(lastSegment);
    }

    private sealed record UpdateCatalogItemTagsRequest(string[] Tags);
}