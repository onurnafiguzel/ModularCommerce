using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ModularCommerce.Shared.Infrastructure.Persistence;

public sealed class MigrateAndSeedHostedService<TContext>(
    IServiceProvider serviceProvider,
    IHostEnvironment environment,
    IConfiguration configuration) : IHostedService
    where TContext : DbContext
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var runMigrations = configuration.GetValue<bool>("RUN_MIGRATIONS");

        if (!runMigrations && !environment.IsDevelopment())
        {
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        await context.Database.MigrateAsync(cancellationToken);

        foreach (var seeder in scope.ServiceProvider.GetServices<IDataSeeder<TContext>>())
        {
            await seeder.SeedAsync(context, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
