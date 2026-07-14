using eShop.WebAppComponents.Catalog;

namespace eShop.WebAppComponents.Services;

public interface IProductImageUrlProvider
{
    string GetProductImageUrl(CatalogItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.PhotoUrls))
        {
            var first = item.PhotoUrls.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
            if (!string.IsNullOrWhiteSpace(first))
                return first;
        }
        return GetProductImageUrl(item.Id);
    }

    string GetProductImageUrl(int productId);
}
