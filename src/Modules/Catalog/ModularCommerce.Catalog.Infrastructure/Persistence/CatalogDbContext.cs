using Microsoft.EntityFrameworkCore;
using ModularCommerce.Catalog.Domain.Products;
using ModularCommerce.Catalog.Infrastructure.Outbox;

namespace ModularCommerce.Catalog.Infrastructure.Persistence;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options)
    : DbContext(options)
{
    public const string Schema = "catalog";

    public DbSet<Product> Products => Set<Product>();

    // Transactional outbox: ürün mutasyonlarıyla AYNI transaction'da yazılır (interceptor), dispatcher taşır.
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}
