namespace eShop.WebAppComponents.Catalog;

public record CatalogItem(
    int Id,
    string Name,
    string Description,
    decimal Price,
    string PictureUrl,
    int CatalogBrandId,
    CatalogBrand CatalogBrand,
    int CatalogTypeId,
    CatalogItemType CatalogType,
    int AvailableStock = 0,
    int ViewCount = 0,
    int FavoriteCount = 0,
    float AverageRating = 0,
    int RatingCount = 0,
    decimal? TokenPrice = null,
    string? SellerId = null,
    string? Condition = null,
    int? ManufactureYear = null,
    string? PhotoUrls = null);

public record CatalogResult(int PageIndex, int PageSize, int Count, List<CatalogItem> Data);
public record CatalogBrand(int Id, string Brand);
public record CatalogItemType(int Id, string Type);
