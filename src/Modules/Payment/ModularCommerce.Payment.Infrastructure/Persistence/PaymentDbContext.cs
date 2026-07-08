using Microsoft.EntityFrameworkCore;
using PaymentAggregate = ModularCommerce.Payment.Domain.Payments.Payment;

namespace ModularCommerce.Payment.Infrastructure.Persistence;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options)
    : DbContext(options)
{
    public const string Schema = "payment";
    public DbSet<PaymentAggregate> Payments => Set<PaymentAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentDbContext).Assembly);
    }
}
