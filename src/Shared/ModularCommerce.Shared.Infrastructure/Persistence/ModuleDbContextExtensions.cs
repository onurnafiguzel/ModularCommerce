using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModularCommerce.Shared.Infrastructure.Persistence;

public static class ModuleDbContextExtensions
{
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string schema)
        where TContext : DbContext
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Database yapılandırması bulunamadı");

        services.AddDbContext<TContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema)));

        // Development'ta otomatik migrate + seed; kayıt sırası = çalışma sırası.
        services.AddHostedService<MigrateAndSeedHostedService<TContext>>();

        return services;
    }
}
