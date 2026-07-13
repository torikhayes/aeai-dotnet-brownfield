namespace eShop.Catalog.API.Infrastructure.EntityConfigurations;

class CatalogItemFavoriteEntityTypeConfiguration
    : IEntityTypeConfiguration<CatalogItemFavorite>
{
    public void Configure(EntityTypeBuilder<CatalogItemFavorite> builder)
    {
        builder.ToTable("CatalogItemFavorite");

        builder.Property(favorite => favorite.UserId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(favorite => favorite.CreatedAt)
            .HasColumnType("timestamp with time zone");

        builder.HasOne(favorite => favorite.CatalogItem)
            .WithMany()
            .HasForeignKey(favorite => favorite.CatalogItemId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasIndex(favorite => new { favorite.CatalogItemId, favorite.UserId })
            .IsUnique();
    }
}