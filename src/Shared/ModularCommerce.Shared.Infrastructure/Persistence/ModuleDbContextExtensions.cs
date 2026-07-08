using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModularCommerce.Shared.Infrastructure.Persistence;

public static class ModuleDbContextExtensions
{
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string schema,
        Action<IServiceProvider, DbContextOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Database yapılandırması bulunamadı");

        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schema));

            // Opsiyonel modül-lokal yapılandırma (örn. interceptor). null default olduğu
            // için mevcut modüller değişmeden çalışır; interceptor yalnız onu bağlayan
            // modülün DbContext'inde devrededir (başka modüle sızmaz).
            configure?.Invoke(serviceProvider, options);
        });

        // Development'ta otomatik migrate + seed; kayıt sırası = çalışma sırası.
        services.AddHostedService<MigrateAndSeedHostedService<TContext>>();

        return services;
    }
}
