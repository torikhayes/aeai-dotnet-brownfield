namespace eShop.Catalog.API.Infrastructure.EntityConfigurations;

class CatalogItemRatingEntityTypeConfiguration
    : IEntityTypeConfiguration<CatalogItemRating>
{
    public void Configure(EntityTypeBuilder<CatalogItemRating> builder)
    {
        builder.ToTable("CatalogItemRating");

        builder.Property(rating => rating.UserId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(rating => rating.CreatedAt)
            .HasColumnType("timestamp with time zone");

        builder.HasOne(rating => rating.CatalogItem)
            .WithMany()
            .HasForeignKey(rating => rating.CatalogItemId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasIndex(rating => new { rating.CatalogItemId, rating.UserId })
            .IsUnique();
    }
}