using eShop.PaymentProcessor.TokenLedger.Model;

namespace eShop.PaymentProcessor.TokenLedger.Infrastructure;

public class TokenDbContext(DbContextOptions<TokenDbContext> options) : DbContext(options)
{
    public DbSet<TokenWallet> TokenWallets => Set<TokenWallet>();
    public DbSet<TokenTransaction> TokenTransactions => Set<TokenTransaction>();
    public DbSet<TokenAwardLookupEntry> TokenAwardLookupEntries => Set<TokenAwardLookupEntry>();
    public DbSet<TokenAwardedListing> TokenAwardedListings => Set<TokenAwardedListing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TokenWallet>(entity =>
        {
            entity.HasKey(w => w.UserId);
            entity.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
            entity.Property(w => w.Balance).HasDefaultValue(0);
        });

        modelBuilder.Entity<TokenTransaction>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.HasIndex(t => new { t.UserId, t.CreatedAt });
            entity.HasIndex(t => t.RelatedEventId).IsUnique();
            entity.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<TokenAwardLookupEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ClubCategory, e.ConditionGrade, e.TableVersion }).IsUnique();
        });

        modelBuilder.Entity<TokenAwardedListing>(entity =>
        {
            entity.HasKey(l => l.CatalogItemId);
            entity.Property(l => l.AwardedAt).HasDefaultValueSql("now()");
        });
    }
}
