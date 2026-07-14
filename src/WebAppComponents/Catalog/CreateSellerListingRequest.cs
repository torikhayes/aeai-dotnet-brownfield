using System.ComponentModel.DataAnnotations;

namespace eShop.WebAppComponents.Catalog;

public class CreateSellerListingRequest
{
    [Required(ErrorMessage = "Club name is required.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Price is required.")]
    [Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "Price must be between 0.01 and 1,000,000.")]
    public decimal? Price { get; set; }

    [Required(ErrorMessage = "Please select a club type.")]
    public int? CatalogTypeId { get; set; }

    [Required(ErrorMessage = "Please select a club brand.")]
    public int? CatalogBrandId { get; set; }

    [Required]
    public string Condition { get; set; } = "Good";

    public string[] PhotoUrls { get; set; } = [];

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(1900, 2100)]
    public int? ManufactureYear { get; set; }

    public string? Tags { get; set; }
}
