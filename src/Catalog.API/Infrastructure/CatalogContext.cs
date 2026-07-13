namespace eShop.Catalog.API.Infrastructure;

/// <remarks>
/// Add migrations using the following command inside the 'Catalog.API' project directory:
///
/// dotnet ef migrations add --context CatalogContext [migration-name]
/// </remarks>
public class CatalogContext : DbContext
{
    public CatalogContext(DbContextOptions<CatalogContext> options, IConfiguration configuration) : base(options)
    {
    }

    public required DbSet<CatalogItem> CatalogItems { get; set; }
    public required DbSet<CatalogItemRating> CatalogItemRatings { get; set; }
    public required DbSet<CatalogItemFavorite> CatalogItemFavorites { get; set; }
    public required DbSet<CatalogBrand> CatalogBrands { get; set; }
    public required DbSet<CatalogType> CatalogTypes { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasPostgresExtension("vector");
        builder.ApplyConfiguration(new CatalogBrandEntityTypeConfiguration());
        builder.ApplyConfiguration(new CatalogTypeEntityTypeConfiguration());
        builder.ApplyConfiguration(new CatalogItemEntityTypeConfiguration());
        builder.ApplyConfiguration(new CatalogItemRatingEntityTypeConfiguration());
        builder.ApplyConfiguration(new CatalogItemFavoriteEntityTypeConfiguration());

        // Add the outbox table to this context
        builder.UseIntegrationEventLogs();
    }
}
