using System.ComponentModel.DataAnnotations;

namespace eShop.WebAppComponents.Catalog;

public class CreateSellerListingRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal Price { get; set; }

    [Range(1, int.MaxValue)]
    public int CatalogTypeId { get; set; }

    [Range(1, int.MaxValue)]
    public int CatalogBrandId { get; set; }

    [Required]
    public string Condition { get; set; } = "Good";

    public string[] PhotoUrls { get; set; } = [];

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(1900, 2100)]
    public int? ManufactureYear { get; set; }

    public string? Tags { get; set; }
}
