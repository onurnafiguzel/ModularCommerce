using Microsoft.EntityFrameworkCore;
using ModularCommerce.Ordering.Domain.Orders;

namespace ModularCommerce.Ordering.Infrastructure.Persistence;

public sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options)
    : DbContext(options)
{
    public const string Schema = "ordering";
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderingDbContext).Assembly);
    }
}
