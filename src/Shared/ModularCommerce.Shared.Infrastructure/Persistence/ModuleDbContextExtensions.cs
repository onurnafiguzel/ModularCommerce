using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ModularCommerce.Shared.Infrastructure.Persistence;

public static class ModuleDbContextExtensions
{
    /// <summary>
    /// Her modülün AddModuleDbContext çağrısı KENDİ `UseNpgsql(connectionString)` çağrısını
    /// yapar — EF Core'un Npgsql sağlayıcısı her çağrı için AYRI bir NpgsqlDataSource/pool kurar,
    /// AYNI connection string metni bile olsa modüller arasında PAYLAŞILMAZ. 9 modülün (Shipping
    /// hariç, 8'i Postgres kullanıyor) HER BİRİ varsayılan Npgsql Max Pool Size'ı (100) ile
    /// çalışırsa, tek bir checkout isteği bile (Cart+Catalog+Inventory+Payment+Ordering'in
    /// AYRI pool'larını eşzamanlı kullanır) 100-paralel yük altında Postgres'in KENDİ
    /// max_connections sınırını (varsayılan 100) kolayca aşar ("sorry, too many clients already"
    /// — ölçüldü: tek bir 100-paralel place_order koşusunda pg_stat_activity 98/100'e çıktı).
    /// Formül (appsettings'ten ayarlanabilir, bkz. Npgsql:MaxPoolSizePerModule): modül_sayısı ×
    /// MaxPoolSizePerModule × replica_sayısı ≤ Postgres max_connections − headroom (health check,
    /// migration job, superuser_reserved_connections, doğrudan NpgsqlConnection kullanımları).
    /// Varsayılan 10: 8 modül × 10 × 1 replika (test/dev) = 80 ≤ 100, 20 bağlantı headroom
    /// bırakır. ÜRETİMDE (k8s/deployment.yaml: replicas=3) 8 × 10 × 3 = 240 — bu Postgres'in
    /// max_connections'ını AŞAR; prod deploy'u varsayılanı KORUMADAN ÖNCE bu değeri düşürmeli
    /// VEYA Postgres'in max_connections'ını buna göre yükseltmeli (README'de belgelenmiştir).
    /// </summary>
    private const int DefaultMaxPoolSizePerModule = 10;

    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string schema,
        Action<IServiceProvider, DbContextOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        var rawConnectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Database yapılandırması bulunamadı");

        var maxPoolSize = configuration.GetValue("Npgsql:MaxPoolSizePerModule", DefaultMaxPoolSizePerModule);
        var connectionString = new NpgsqlConnectionStringBuilder(rawConnectionString)
        {
            MaxPoolSize = maxPoolSize,
        }.ConnectionString;

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
