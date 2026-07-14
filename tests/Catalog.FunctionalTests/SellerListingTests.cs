using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Asp.Versioning;
using Asp.Versioning.Http;
using eShop.Catalog.API;
using eShop.Catalog.API.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eShop.Catalog.FunctionalTests;

/// <summary>Fake auth handler for seller listing tests. Any request with Bearer token "seller-{userId}" authenticates as that user.</summary>
public class SellerTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuth";
    public const string TestSeller1 = "seller-user-001";
    public const string TestSeller2 = "seller-user-002";

    public SellerTestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ")) return Task.FromResult(AuthenticateResult.NoResult());

        var userId = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(userId)) return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[] { new Claim("sub", userId), new Claim(ClaimTypes.Name, userId) };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>Fixture that overrides auth with SellerTestAuthHandler for seller endpoint tests.</summary>
public sealed class SellerCatalogApiFixture : CatalogApiFixture
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            // Replace JWT auth with test handler so we can control the user identity
            services.AddAuthentication(SellerTestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, SellerTestAuthHandler>(SellerTestAuthHandler.SchemeName, null);
        });
    }
}

public sealed class SellerListingTests : IClassFixture<SellerCatalogApiFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private static readonly ApiVersion V1 = new(1, 0);

    // Resolved once per test class from the seeded DB
    private int _typeId;
    private int _brandId;

    public SellerListingTests(SellerCatalogApiFixture fixture) => _factory = fixture;

    private HttpClient AuthClient(string userId)
    {
        var handler = new ApiVersionHandler(new QueryStringApiVersionWriter(), V1);
        var client = _factory.CreateDefaultClient(handler);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId);
        return client;
    }

    private HttpClient AnonClient()
    {
        var handler = new ApiVersionHandler(new QueryStringApiVersionWriter(), V1);
        return _factory.CreateDefaultClient(handler);
    }

    /// <summary>Fetch valid CatalogTypeId/CatalogBrandId from the seeded DB.</summary>
    private async Task<(int typeId, int brandId)> GetValidIdsAsync()
    {
        if (_typeId != 0) return (_typeId, _brandId);
        var client = AnonClient();
        var resp = await client.GetAsync("/api/catalog/items?pageIndex=0&pageSize=1");
        resp.EnsureSuccessStatusCode();
        var page = JsonSerializer.Deserialize<PaginatedItems<CatalogItem>>(
            await resp.Content.ReadAsStringAsync(), _json);
        var item = page!.Data.First();
        _typeId = item.CatalogTypeId;
        _brandId = item.CatalogBrandId;
        return (_typeId, _brandId);
    }

    private async Task<CreateSellerListingRequest> ValidListingAsync(string suffix = "")
    {
        var (typeId, brandId) = await GetValidIdsAsync();
        return new CreateSellerListingRequest(
            Name: $"Callaway Driver Test {suffix}",
            Price: 149.99m,
            CatalogTypeId: typeId,
            CatalogBrandId: brandId,
            Condition: "Excellent",
            PhotoUrls: ["https://example.com/photo1.jpg"]);
    }

    // -----------------------------------------------------------------------
    // T008: Authenticated seller can create a listing with SellerId from JWT
    // -----------------------------------------------------------------------
    [Fact]
    public async Task CreateListing_Authenticated_ReturnsCreatedWithSellerId()
    {
        var client = AuthClient(SellerTestAuthHandler.TestSeller1);

        var response = await client.PostAsJsonAsync("/api/catalog/items/listings", await ValidListingAsync("T008"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);

        // Verify the item exists and has the correct SellerId
        var itemResponse = await client.GetAsync(location, TestContext.Current.CancellationToken);
        itemResponse.EnsureSuccessStatusCode();
        var item = JsonSerializer.Deserialize<CatalogItem>(
            await itemResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), _json);
        Assert.Equal(SellerTestAuthHandler.TestSeller1, item!.SellerId);
        Assert.Equal(1, item.AvailableStock);
    }

    // -----------------------------------------------------------------------
    // T009: Unauthenticated create returns 401
    // -----------------------------------------------------------------------
    [Fact]
    public async Task CreateListing_Unauthenticated_Returns401()
    {
        var client = AnonClient();

        var response = await client.PostAsJsonAsync("/api/catalog/items/listings", await ValidListingAsync("T009"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // T010: Missing required fields returns 400
    // -----------------------------------------------------------------------
    [Fact]
    public async Task CreateListing_MissingRequiredFields_Returns400()
    {
        var client = AuthClient(SellerTestAuthHandler.TestSeller1);
        // Price = 0 and no name — send raw JSON with missing Name
        var badPayload = new { Price = 0, CatalogTypeId = 1, CatalogBrandId = 1, Condition = "Good", PhotoUrls = new[] { "https://example.com/p.jpg" } };

        var response = await client.PostAsJsonAsync("/api/catalog/items/listings", badPayload, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // T011: No photos returns 400 (Principle IV)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task CreateListing_NoPhotos_Returns400()
    {
        var client = AuthClient(SellerTestAuthHandler.TestSeller1);
        var noPhoto = new CreateSellerListingRequest("Test Club", 99m, 1, 1, "Good", []);

        var response = await client.PostAsJsonAsync("/api/catalog/items/listings", noPhoto, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("photo", body, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // T014: GET by-seller returns only that seller's active listings
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetBySeller_ReturnsOnlyThatSellersItems()
    {
        var seller1Client = AuthClient(SellerTestAuthHandler.TestSeller1);
        var anonClient = AnonClient();

        // Create 2 listings for seller1, 1 for seller2
        await seller1Client.PostAsJsonAsync("/api/catalog/items/listings", await ValidListingAsync("byS1a"), TestContext.Current.CancellationToken);
        await seller1Client.PostAsJsonAsync("/api/catalog/items/listings", await ValidListingAsync("byS1b"), TestContext.Current.CancellationToken);

        var seller2Client = AuthClient(SellerTestAuthHandler.TestSeller2);
        await seller2Client.PostAsJsonAsync("/api/catalog/items/listings", await ValidListingAsync("byS2"), TestContext.Current.CancellationToken);

        // Fetch seller1's listings (public endpoint)
        var response = await anonClient.GetAsync($"/api/catalog/items/by-seller/{SellerTestAuthHandler.TestSeller1}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var result = JsonSerializer.Deserialize<PaginatedItems<CatalogItem>>(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), _json);

        Assert.True(result!.Data.All(x => x.SellerId == SellerTestAuthHandler.TestSeller1));
        Assert.DoesNotContain(result.Data, x => x.SellerId == SellerTestAuthHandler.TestSeller2);
    }

    // -----------------------------------------------------------------------
    // T015: Seller with no listings returns empty (not 404)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetBySeller_NoListings_ReturnsEmptyNotNotFound()
    {
        var client = AnonClient();

        var response = await client.GetAsync("/api/catalog/items/by-seller/nonexistent-seller-xyz", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = JsonSerializer.Deserialize<PaginatedItems<CatalogItem>>(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), _json);
        Assert.Equal(0, result!.Count);
    }

    // -----------------------------------------------------------------------
    // T017: GET my-listings returns only the caller's items
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetMyListings_ReturnsOnlyCallerItems()
    {
        var seller1 = AuthClient(SellerTestAuthHandler.TestSeller1);
        var seller2 = AuthClient(SellerTestAuthHandler.TestSeller2);

        await seller1.PostAsJsonAsync("/api/catalog/items/listings", await ValidListingAsync("myS1"), TestContext.Current.CancellationToken);
        await seller2.PostAsJsonAsync("/api/catalog/items/listings", await ValidListingAsync("myS2"), TestContext.Current.CancellationToken);

        var response = await seller1.GetAsync("/api/catalog/items/my-listings", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var result = JsonSerializer.Deserialize<PaginatedItems<CatalogItem>>(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), _json);

        Assert.True(result!.Data.All(x => x.SellerId == SellerTestAuthHandler.TestSeller1));
        Assert.DoesNotContain(result.Data, x => x.SellerId == SellerTestAuthHandler.TestSeller2);
    }

    // -----------------------------------------------------------------------
    // T018: Other seller's items don't appear in my-listings
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GetMyListings_DoesNotReturnOtherSellerItems()
    {
        var seller2 = AuthClient(SellerTestAuthHandler.TestSeller2);
        await seller2.PostAsJsonAsync("/api/catalog/items/listings", await ValidListingAsync("t018"), TestContext.Current.CancellationToken);

        // Seller1 should NOT see seller2's item
        var seller1 = AuthClient(SellerTestAuthHandler.TestSeller1);
        var response = await seller1.GetAsync("/api/catalog/items/my-listings", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var result = JsonSerializer.Deserialize<PaginatedItems<CatalogItem>>(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), _json);

        Assert.DoesNotContain(result!.Data, x => x.SellerId == SellerTestAuthHandler.TestSeller2);
    }

    // -----------------------------------------------------------------------
    // T020: Owner can deactivate their listing (AvailableStock → 0)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task DeactivateListing_Owner_SetsAvailableStockToZero()
    {
        var seller = AuthClient(SellerTestAuthHandler.TestSeller1);
        var create = await seller.PostAsJsonAsync("/api/catalog/items/listings", await ValidListingAsync("T020"), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var location = create.Headers.Location!.ToString();
        var id = int.Parse(location.Split('/').Last());

        var delete = await seller.DeleteAsync($"/api/catalog/items/listings/{id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // Verify the item is no longer active
        var bySellerResp = await AnonClient().GetAsync(
            $"/api/catalog/items/by-seller/{SellerTestAuthHandler.TestSeller1}", TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<PaginatedItems<CatalogItem>>(
            await bySellerResp.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), _json);
        Assert.DoesNotContain(result!.Data, x => x.Id == id);
    }

    // -----------------------------------------------------------------------
    // T021: Non-owner deactivate returns 403
    // -----------------------------------------------------------------------
    [Fact]
    public async Task DeactivateListing_NonOwner_Returns403()
    {
        var seller1 = AuthClient(SellerTestAuthHandler.TestSeller1);
        var create = await seller1.PostAsJsonAsync("/api/catalog/items/listings", await ValidListingAsync("T021"), TestContext.Current.CancellationToken);
        var location = create.Headers.Location!.ToString();
        var id = int.Parse(location.Split('/').Last());

        var seller2 = AuthClient(SellerTestAuthHandler.TestSeller2);
        var response = await seller2.DeleteAsync($"/api/catalog/items/listings/{id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
