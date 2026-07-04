using Microsoft.EntityFrameworkCore;

namespace ModularCommerce.Shared.Infrastructure.Persistence;

public interface IDataSeeder<in TContext> where TContext : DbContext
{
    Task SeedAsync(TContext context, CancellationToken cancellationToken);
}
