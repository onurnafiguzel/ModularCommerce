using Microsoft.EntityFrameworkCore;

namespace ModularCommerce.Cart.Infrastructure.Persistence;

public sealed class CartDbContext(DbContextOptions<CartDbContext> options)
    : DbContext(options)
{
    public const string Schema = "cart";

    public DbSet<CartRecord> Carts => Set<CartRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CartDbContext).Assembly);
    }
}
