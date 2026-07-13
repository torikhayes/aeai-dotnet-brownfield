using System.ComponentModel.DataAnnotations;

namespace eShop.Catalog.API.Model;

public class CatalogItemRating
{
    public int Id { get; set; }

    public int CatalogItemId { get; set; }

    [Required]
    public required string UserId { get; set; }

    public int Stars { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public CatalogItem? CatalogItem { get; set; }
}