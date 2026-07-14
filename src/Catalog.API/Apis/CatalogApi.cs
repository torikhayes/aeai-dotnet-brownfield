using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Pgvector.EntityFrameworkCore;

namespace eShop.Catalog.API;

/// <summary>Request DTO for seller club listing creation (Spec 002).</summary>
public record CreateSellerListingRequest(
    [Required] string Name,
    [Required] decimal Price,
    [Required] int CatalogTypeId,
    [Required] int CatalogBrandId,
    [Required] string Condition,
    [Required] string[] PhotoUrls,
    string? Description = null,
    int? ManufactureYear = null,
    string? Tags = null);

public static class CatalogApi
{
    public static IEndpointRouteBuilder MapCatalogApi(this IEndpointRouteBuilder app)
    {
        // RouteGroupBuilder for catalog endpoints
        var vApi = app.NewVersionedApi("Catalog");
        var api = vApi.MapGroup("api/catalog").HasApiVersion(1, 0).HasApiVersion(2, 0);
        var v1 = vApi.MapGroup("api/catalog").HasApiVersion(1, 0);
        var v2 = vApi.MapGroup("api/catalog").HasApiVersion(2, 0);

        // Routes for querying catalog items.
        v1.MapGet("/items", GetAllItemsV1)
            .WithName("ListItems")
            .WithSummary("List catalog items")
            .WithDescription("Get a paginated list of items in the catalog.")
            .WithTags("Items");
        v2.MapGet("/items", GetAllItems)
            .WithName("ListItems-V2")
            .WithSummary("List catalog items")
            .WithDescription("Get a paginated list of items in the catalog.")
            .WithTags("Items");
        api.MapPost("/items/{id:int}/rate", RateItem)
            .RequireAuthorization()
            .WithName("RateItem")
            .WithSummary("Rate a catalog item")
            .WithDescription("Submit or update a star rating for a catalog item")
            .WithTags("Items");
        api.MapPost("/items/{id:int}/favorite", ToggleFavorite)
            .RequireAuthorization()
            .WithName("ToggleFavorite")
            .WithSummary("Toggle a favorite for a catalog item")
            .WithDescription("Toggle the authenticated user's favorite state for a catalog item")
            .WithTags("Items");
        api.MapPatch("/items/{id:int}/tags", UpdateTags)
            .RequireAuthorization()
            .WithName("UpdateTags")
            .WithSummary("Update catalog item tags")
            .WithDescription("Update the tags for a catalog item")
            .WithTags("Items");
        api.MapGet("/items/by", GetItemsByIds)
            .WithName("BatchGetItems")
            .WithSummary("Batch get catalog items")
            .WithDescription("Get multiple items from the catalog")
            .WithTags("Items");
        api.MapGet("/items/{id:int}", GetItemById)
            .WithName("GetItem")
            .WithSummary("Get catalog item")
            .WithDescription("Get an item from the catalog")
            .WithTags("Items");
        v1.MapGet("/items/by/{name:minlength(1)}", GetItemsByName)
            .WithName("GetItemsByName")
            .WithSummary("Get catalog items by name")
            .WithDescription("Get a paginated list of catalog items with the specified name.")
            .WithTags("Items");
        api.MapGet("/items/{id:int}/pic", GetItemPictureById)
            .WithName("GetItemPicture")
            .WithSummary("Get catalog item picture")
            .WithDescription("Get the picture for a catalog item")
            .WithTags("Items");

        // Routes for resolving catalog items using AI.
        v1.MapGet("/items/withsemanticrelevance/{text:minlength(1)}", GetItemsBySemanticRelevanceV1)
            .WithName("GetRelevantItems")
            .WithSummary("Search catalog for relevant items")
            .WithDescription("Search the catalog for items related to the specified text")
            .WithTags("Search");

                // Routes for resolving catalog items using AI.
        v2.MapGet("/items/withsemanticrelevance", GetItemsBySemanticRelevance)
            .WithName("GetRelevantItems-V2")
            .WithSummary("Search catalog for relevant items")
            .WithDescription("Search the catalog for items related to the specified text")
            .WithTags("Search");

        // Routes for resolving catalog items by type and brand.
        v1.MapGet("/items/type/{typeId}/brand/{brandId?}", GetItemsByBrandAndTypeId)
            .WithName("GetItemsByTypeAndBrand")
            .WithSummary("Get catalog items by type and brand")
            .WithDescription("Get catalog items of the specified type and brand")
            .WithTags("Types");
        v1.MapGet("/items/type/all/brand/{brandId:int?}", GetItemsByBrandId)
            .WithName("GetItemsByBrand")
            .WithSummary("List catalog items by brand")
            .WithDescription("Get a list of catalog items for the specified brand")
            .WithTags("Brands");
        api.MapGet("/catalogtypes",
            [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
            async (CatalogContext context) => await context.CatalogTypes.OrderBy(x => x.Type).ToListAsync())
            .WithName("ListItemTypes")
            .WithSummary("List catalog item types")
            .WithDescription("Get a list of the types of catalog items")
            .WithTags("Types");
        api.MapGet("/catalogbrands",
            [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
            async (CatalogContext context) => await context.CatalogBrands.OrderBy(x => x.Brand).ToListAsync())
            .WithName("ListItemBrands")
            .WithSummary("List catalog item brands")
            .WithDescription("Get a list of the brands of catalog items")
            .WithTags("Brands");

        // Routes for modifying catalog items.
        v1.MapPut("/items", UpdateItemV1)
            .WithName("UpdateItem")
            .WithSummary("Create or replace a catalog item")
            .WithDescription("Create or replace a catalog item")
            .WithTags("Items")
            .RequireAuthorization();
        v2.MapPut("/items/{id:int}", UpdateItem)
            .WithName("UpdateItem-V2")
            .WithSummary("Create or replace a catalog item")
            .WithDescription("Create or replace a catalog item")
            .WithTags("Items")
            .RequireAuthorization();
        api.MapPost("/items", CreateItem)
            .WithName("CreateItem")
            .WithSummary("Create a catalog item")
            .WithDescription("Create a new item in the catalog")
            .RequireAuthorization();
        api.MapDelete("/items/{id:int}", DeleteItemById)
            .WithName("DeleteItem")
            .WithSummary("Delete catalog item")
            .WithDescription("Delete the specified catalog item")
            .RequireAuthorization();

        // Seller listing endpoints (Spec 002)
        api.MapPost("/items/listings", CreateSellerListing)
            .WithName("CreateSellerListing")
            .WithSummary("Create a seller listing")
            .WithDescription("Create a new club listing as an authenticated seller")
            .WithTags("Seller")
            .RequireAuthorization();
        api.MapGet("/items/by-seller/{sellerId}", GetItemsBySeller)
            .WithName("GetItemsBySeller")
            .WithSummary("Get items by seller")
            .WithDescription("Get paginated club listings for a given seller")
            .WithTags("Seller");
        api.MapGet("/items/my-listings", GetMyListings)
            .WithName("GetMyListings")
            .WithSummary("Get my listings")
            .WithDescription("Get the authenticated seller's own club listings")
            .WithTags("Seller")
            .RequireAuthorization();
        api.MapDelete("/items/listings/{id:int}", DeactivateListing)
            .WithName("DeactivateListing")
            .WithSummary("Deactivate a seller listing")
            .WithDescription("Set AvailableStock to 0 for the caller's own listing")
            .WithTags("Seller")
            .RequireAuthorization();

        return app;
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Ok<PaginatedItems<CatalogItem>>> GetAllItemsV1(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services)
    {
        return await GetAllItems(paginationRequest, services, null, null, null, null);
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Ok<PaginatedItems<CatalogItem>>> GetAllItems(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The name of the item to return")] string? name,
        [Description("The type of items to return")] int? type,
        [Description("The brand of items to return")] int? brand,
        [Description("The tag of items to return")] string? tag)
    {
        var pageSize = paginationRequest.PageSize;
        var pageIndex = paginationRequest.PageIndex;

        var root = (IQueryable<CatalogItem>)services.Context.CatalogItems;

        if (name is not null)
        {
            root = root.Where(c => c.Name.StartsWith(name));
        }
        if (type is not null)
        {
            root = root.Where(c => c.CatalogTypeId == type);
        }
        if (brand is not null)
        {
            root = root.Where(c => c.CatalogBrandId == brand);
        }
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var normalizedTag = CatalogItem.NormalizeTag(tag);
            root = root.Where(c => c.Tags != null && EF.Functions.Like("," + c.Tags + ",", $"%,{normalizedTag},%"));
        }

        var totalItems = await root
            .LongCountAsync();

        var itemsOnPage = await root
            .OrderBy(c => c.Name)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToListAsync();

        return TypedResults.Ok(new PaginatedItems<CatalogItem>(pageIndex, pageSize, totalItems, itemsOnPage));
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Ok<List<CatalogItem>>> GetItemsByIds(
        [AsParameters] CatalogServices services,
        [Description("List of ids for catalog items to return")] int[] ids)
    {
        var items = await services.Context.CatalogItems.Where(item => ids.Contains(item.Id)).ToListAsync();
        return TypedResults.Ok(items);
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Results<Ok<CatalogItem>, NotFound, BadRequest<ProblemDetails>>> GetItemById(
        HttpContext httpContext,
        [AsParameters] CatalogServices services,
        [Description("The catalog item id")] int id)
    {
        if (id <= 0)
        {
            return TypedResults.BadRequest<ProblemDetails>(new (){
                Detail = "Id is not valid"
            });
        }

        var item = await services.Context.CatalogItems.Include(ci => ci.CatalogBrand).SingleOrDefaultAsync(ci => ci.Id == id);

        if (item == null)
        {
            return TypedResults.NotFound();
        }

        item.ViewCount++;
        await services.Context.SaveChangesAsync();

        return TypedResults.Ok(item);
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Results<NoContent, BadRequest<ProblemDetails>, NotFound>> RateItem(
        HttpContext httpContext,
        [AsParameters] CatalogServices services,
        [Description("The catalog item id")] int id,
        RateCatalogItemRequest request)
    {
        var userId = GetCurrentUserId(httpContext);
        if (userId is null)
        {
            return TypedResults.BadRequest<ProblemDetails>(new ProblemDetails { Detail = "Authenticated user is required." });
        }

        if (request.Stars is < 1 or > 5)
        {
            return TypedResults.BadRequest<ProblemDetails>(new ProblemDetails { Detail = "Stars must be between 1 and 5." });
        }

        var item = await services.Context.CatalogItems.SingleOrDefaultAsync(ci => ci.Id == id);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        var existingRating = await services.Context.CatalogItemRatings
            .SingleOrDefaultAsync(rating => rating.CatalogItemId == id && rating.UserId == userId);

        if (existingRating is null)
        {
            services.Context.CatalogItemRatings.Add(new CatalogItemRating
            {
                CatalogItemId = id,
                UserId = userId,
                Stars = request.Stars,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existingRating.Stars = request.Stars;
        }

        await services.Context.SaveChangesAsync();

        var aggregate = await services.Context.CatalogItemRatings
            .Where(rating => rating.CatalogItemId == id)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Average = group.Average(rating => rating.Stars)
            })
            .SingleAsync();

        item.RatingCount = aggregate.Count;
        item.AverageRating = (float)Math.Round(aggregate.Average, 1, MidpointRounding.AwayFromZero);
        await services.Context.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Results<NoContent, BadRequest<ProblemDetails>, NotFound>> ToggleFavorite(
        HttpContext httpContext,
        [AsParameters] CatalogServices services,
        [Description("The catalog item id")] int id)
    {
        var userId = GetCurrentUserId(httpContext);
        if (userId is null)
        {
            return TypedResults.BadRequest<ProblemDetails>(new ProblemDetails { Detail = "Authenticated user is required." });
        }

        var item = await services.Context.CatalogItems.SingleOrDefaultAsync(ci => ci.Id == id);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        var favorite = await services.Context.CatalogItemFavorites
            .SingleOrDefaultAsync(entry => entry.CatalogItemId == id && entry.UserId == userId);

        if (favorite is null)
        {
            services.Context.CatalogItemFavorites.Add(new CatalogItemFavorite
            {
                CatalogItemId = id,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            services.Context.CatalogItemFavorites.Remove(favorite);
        }

        await services.Context.SaveChangesAsync();

        item.FavoriteCount = await services.Context.CatalogItemFavorites.CountAsync(entry => entry.CatalogItemId == id);
        await services.Context.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Results<NoContent, BadRequest<ProblemDetails>, NotFound, ForbidHttpResult>> UpdateTags(
        HttpContext httpContext,
        [AsParameters] CatalogServices services,
        [Description("The catalog item id")] int id,
        UpdateCatalogItemTagsRequest request)
    {
        var userId = GetCurrentUserId(httpContext);
        if (userId is null)
        {
            return TypedResults.BadRequest<ProblemDetails>(new ProblemDetails { Detail = "Authenticated user is required." });
        }

        var item = await services.Context.CatalogItems.SingleOrDefaultAsync(ci => ci.Id == id);
        if (item is null)
        {
            return TypedResults.NotFound();
        }

        if (!string.Equals(item.SellerId, userId, StringComparison.Ordinal))
        {
            return TypedResults.Forbid();
        }

        item.Tags = CatalogItem.NormalizeTags(request.Tags);
        await services.Context.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Ok<PaginatedItems<CatalogItem>>> GetItemsByName(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The name of the item to return")] string name)
    {
        return await GetAllItems(paginationRequest, services, name, null, null, null);
    }

    [ProducesResponseType<byte[]>(StatusCodes.Status200OK, "application/octet-stream",
        [ "image/png", "image/gif", "image/jpeg", "image/bmp", "image/tiff",
          "image/wmf", "image/jp2", "image/svg+xml", "image/webp" ])]
    public static async Task<Results<PhysicalFileHttpResult,NotFound>> GetItemPictureById(
        CatalogContext context,
        IWebHostEnvironment environment,
        [Description("The catalog item id")] int id)
    {
        var item = await context.CatalogItems.FindAsync(id);

        if (item is null || item.PictureFileName is null)
        {
            return TypedResults.NotFound();
        }

        var path = GetFullPath(environment.ContentRootPath, item.PictureFileName);

        string imageFileExtension = Path.GetExtension(item.PictureFileName) ?? string.Empty;
        string mimetype = GetImageMimeTypeFromImageFileExtension(imageFileExtension);
        DateTime lastModified = File.GetLastWriteTimeUtc(path);

        return TypedResults.PhysicalFile(path, mimetype, lastModified: lastModified);
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Results<Ok<PaginatedItems<CatalogItem>>, RedirectToRouteHttpResult>> GetItemsBySemanticRelevanceV1(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The text string to use when search for related items in the catalog")] string text)

    {
        return await GetItemsBySemanticRelevance(paginationRequest, services, text);
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Results<Ok<PaginatedItems<CatalogItem>>, RedirectToRouteHttpResult>> GetItemsBySemanticRelevance(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The text string to use when search for related items in the catalog"), Required, MinLength(1)] string text)
    {
        var pageSize = paginationRequest.PageSize;
        var pageIndex = paginationRequest.PageIndex;

        if (!services.CatalogAI.IsEnabled)
        {
            return await GetItemsByName(paginationRequest, services, text);
        }

        // Create an embedding for the input search
        var vector = await services.CatalogAI.GetEmbeddingAsync(text);

        if (vector is null)
        {
            return await GetItemsByName(paginationRequest, services, text);
        }

        // Get the total number of items
        var totalItems = await services.Context.CatalogItems
            .LongCountAsync();

        // Get the next page of items, ordered by most similar (smallest distance) to the input search
        List<CatalogItem> itemsOnPage;
        if (services.Logger.IsEnabled(LogLevel.Debug))
        {
            var itemsWithDistance = await services.Context.CatalogItems
                .Where(c => c.Embedding != null)
                .Select(c => new { Item = c, Distance = c.Embedding!.CosineDistance(vector) })
                .OrderBy(c => c.Distance)
                .Skip(pageSize * pageIndex)
                .Take(pageSize)
                .ToListAsync();

            services.Logger.LogDebug("Results from {text}: {results}", text, string.Join(", ", itemsWithDistance.Select(i => $"{i.Item.Name} => {i.Distance}")));

            itemsOnPage = itemsWithDistance.Select(i => i.Item).ToList();
        }
        else
        {
            itemsOnPage = await services.Context.CatalogItems
                .Where(c => c.Embedding != null)
                .OrderBy(c => c.Embedding!.CosineDistance(vector))
                .Skip(pageSize * pageIndex)
                .Take(pageSize)
                .ToListAsync();
        }

        return TypedResults.Ok(new PaginatedItems<CatalogItem>(pageIndex, pageSize, totalItems, itemsOnPage));
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Ok<PaginatedItems<CatalogItem>>> GetItemsByBrandAndTypeId(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The type of items to return")] int typeId,
        [Description("The brand of items to return")] int? brandId)
    {
        return await GetAllItems(paginationRequest, services, null, typeId, brandId, null);
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Ok<PaginatedItems<CatalogItem>>> GetItemsByBrandId(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        [Description("The brand of items to return")] int? brandId)
    {
        return await GetAllItems(paginationRequest, services, null, null, brandId, null);
    }

    public static async Task<Results<Created, BadRequest<ProblemDetails>, NotFound<ProblemDetails>>> UpdateItemV1(
        HttpContext httpContext,
        [AsParameters] CatalogServices services,
        CatalogItem productToUpdate)
    {
        if (productToUpdate?.Id == null)
        {
            return TypedResults.BadRequest<ProblemDetails>(new (){
                Detail = "Item id must be provided in the request body."
            });
        }
        return await UpdateItem(httpContext, productToUpdate.Id, services, productToUpdate);
    }

    public static async Task<Results<Created, BadRequest<ProblemDetails>, NotFound<ProblemDetails>>> UpdateItem(
        HttpContext httpContext,
        [Description("The id of the catalog item to delete")] int id,
        [AsParameters] CatalogServices services,
        CatalogItem productToUpdate)
    {
        var catalogItem = await services.Context.CatalogItems.SingleOrDefaultAsync(i => i.Id == id);

        if (catalogItem == null)
        {
            return TypedResults.NotFound<ProblemDetails>(new (){
                Detail = $"Item with id {id} not found."
            });
        }

        var currentSellerId = catalogItem.SellerId;
        var currentViewCount = catalogItem.ViewCount;
        var currentFavoriteCount = catalogItem.FavoriteCount;
        var currentAverageRating = catalogItem.AverageRating;
        var currentRatingCount = catalogItem.RatingCount;
        var currentTags = catalogItem.Tags;

        // Update current product
        var catalogEntry = services.Context.Entry(catalogItem);
        catalogEntry.CurrentValues.SetValues(productToUpdate);

        catalogItem.SellerId = currentSellerId;
        catalogItem.ViewCount = currentViewCount;
        catalogItem.FavoriteCount = currentFavoriteCount;
        catalogItem.AverageRating = currentAverageRating;
        catalogItem.RatingCount = currentRatingCount;
        catalogItem.Tags = currentTags;

        catalogItem.Embedding = await services.CatalogAI.GetEmbeddingAsync(catalogItem);

        var priceEntry = catalogEntry.Property(i => i.Price);

        if (priceEntry.IsModified) // Save product's data and publish integration event through the Event Bus if price has changed
        {
            //Create Integration Event to be published through the Event Bus
            var priceChangedEvent = new ProductPriceChangedIntegrationEvent(catalogItem.Id, productToUpdate.Price, priceEntry.OriginalValue);

            // Achieving atomicity between original Catalog database operation and the IntegrationEventLog thanks to a local transaction
            await services.EventService.SaveEventAndCatalogContextChangesAsync(priceChangedEvent);

            // Publish through the Event Bus and mark the saved event as published
            await services.EventService.PublishThroughEventBusAsync(priceChangedEvent);
        }
        else // Just save the updated product because the Product's Price hasn't changed.
        {
            await services.Context.SaveChangesAsync();
        }
        return TypedResults.Created($"/api/catalog/items/{id}");
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Created> CreateItem(
        HttpContext httpContext,
        [AsParameters] CatalogServices services,
        CatalogItem product)
    {
        var item = new CatalogItem(product.Name)
        {
            Id = product.Id,
            SellerId = GetCurrentUserId(httpContext),
            CatalogBrandId = product.CatalogBrandId,
            CatalogTypeId = product.CatalogTypeId,
            Description = product.Description,
            PictureFileName = product.PictureFileName,
            Price = product.Price,
            AvailableStock = product.AvailableStock,
            RestockThreshold = product.RestockThreshold,
            MaxStockThreshold = product.MaxStockThreshold,
            Tags = CatalogItem.NormalizeTags(product.GetTags())
        };
        item.Embedding = await services.CatalogAI.GetEmbeddingAsync(item);

        services.Context.CatalogItems.Add(item);
        await services.Context.SaveChangesAsync();

        return TypedResults.Created($"/api/catalog/items/{item.Id}");
    }

    public static async Task<Results<NoContent, NotFound>> DeleteItemById(
        [AsParameters] CatalogServices services,
        [Description("The id of the catalog item to delete")] int id)
    {
        var item = services.Context.CatalogItems.SingleOrDefault(x => x.Id == id);

        if (item is null)
        {
            return TypedResults.NotFound();
        }

        services.Context.CatalogItems.Remove(item);
        await services.Context.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    private static string GetImageMimeTypeFromImageFileExtension(string extension) => extension switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".bmp" => "image/bmp",
        ".tiff" => "image/tiff",
        ".wmf" => "image/wmf",
        ".jp2" => "image/jp2",
        ".svg" => "image/svg+xml",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };

    public static string GetFullPath(string contentRootPath, string pictureFileName) =>
        Path.Combine(contentRootPath, "Pics", pictureFileName);

    private static string? GetCurrentUserId(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? httpContext.User.FindFirstValue("sub");
    // -------------------------------------------------------------------------
    // Spec 002: Seller listing endpoints
    // -------------------------------------------------------------------------

    private static readonly string[] ValidConditions = ["New", "Excellent", "Good", "Fair"];

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public static async Task<Results<Created, BadRequest<string>>> CreateSellerListing(
        [AsParameters] CatalogServices services,
        ClaimsPrincipal user,
        CreateSellerListingRequest request)
    {
        var sellerId = user.GetUserId();
        if (string.IsNullOrEmpty(sellerId))
            return TypedResults.BadRequest("Unable to determine seller identity.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return TypedResults.BadRequest("Name is required.");

        if (request.Price <= 0)
            return TypedResults.BadRequest("Price must be greater than zero.");

        if (!ValidConditions.Contains(request.Condition))
            return TypedResults.BadRequest($"Condition must be one of: {string.Join(", ", ValidConditions)}.");

        if (request.PhotoUrls is null || request.PhotoUrls.Length == 0)
            return TypedResults.BadRequest("At least one photo URL is required (Principle IV).");

        var item = new CatalogItem(request.Name)
        {
            CatalogTypeId = request.CatalogTypeId,
            CatalogBrandId = request.CatalogBrandId,
            Description = request.Description,
            Price = request.Price,
            AvailableStock = 1,
            RestockThreshold = 0,
            MaxStockThreshold = 1,
            SellerId = sellerId,
            Condition = request.Condition,
            ManufactureYear = request.ManufactureYear,
            PhotoUrls = string.Join(",", request.PhotoUrls),
        };
        item.Embedding = await services.CatalogAI.GetEmbeddingAsync(item);

        services.Context.CatalogItems.Add(item);
        await services.Context.SaveChangesAsync();

        return TypedResults.Created($"/api/catalog/items/{item.Id}");
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public static async Task<Ok<PaginatedItems<CatalogItem>>> GetItemsBySeller(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        string sellerId)
    {
        var pageSize = paginationRequest.PageSize;
        var pageIndex = paginationRequest.PageIndex;

        var query = services.Context.CatalogItems
            .Where(x => x.SellerId == sellerId && x.AvailableStock > 0);

        var totalItems = await query.LongCountAsync();
        var items = await query
            .OrderBy(x => x.Name)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToListAsync();

        return TypedResults.Ok(new PaginatedItems<CatalogItem>(pageIndex, pageSize, totalItems, items));
    }

    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public static async Task<Results<Ok<PaginatedItems<CatalogItem>>, UnauthorizedHttpResult>> GetMyListings(
        [AsParameters] PaginationRequest paginationRequest,
        [AsParameters] CatalogServices services,
        ClaimsPrincipal user)
    {
        var sellerId = user.GetUserId();
        if (string.IsNullOrEmpty(sellerId))
            return TypedResults.Unauthorized();

        var pageSize = paginationRequest.PageSize;
        var pageIndex = paginationRequest.PageIndex;

        var query = services.Context.CatalogItems
            .Where(x => x.SellerId == sellerId);

        var totalItems = await query.LongCountAsync();
        var items = await query
            .OrderByDescending(x => x.Id)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToListAsync();

        return TypedResults.Ok(new PaginatedItems<CatalogItem>(pageIndex, pageSize, totalItems, items));
    }

    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public static async Task<Results<NoContent, NotFound, ForbidHttpResult, UnauthorizedHttpResult>> DeactivateListing(
        [AsParameters] CatalogServices services,
        ClaimsPrincipal user,
        int id)
    {
        var sellerId = user.GetUserId();
        if (string.IsNullOrEmpty(sellerId))
            return TypedResults.Unauthorized();

        var item = await services.Context.CatalogItems.FindAsync(id);
        if (item is null)
            return TypedResults.NotFound();

        if (item.SellerId != sellerId)
            return TypedResults.Forbid();

        item.AvailableStock = 0;
        await services.Context.SaveChangesAsync();
        return TypedResults.NoContent();
    }
}

public sealed record RateCatalogItemRequest(int Stars);

public sealed record UpdateCatalogItemTagsRequest(string[]? Tags);
