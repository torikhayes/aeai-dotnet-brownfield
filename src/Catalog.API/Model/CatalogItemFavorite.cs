using System.ComponentModel.DataAnnotations;

namespace eShop.Catalog.API.Model;

public class CatalogItemFavorite
{
    public int Id { get; set; }

    public int CatalogItemId { get; set; }

    [Required]
    public required string UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public CatalogItem? CatalogItem { get; set; }
}