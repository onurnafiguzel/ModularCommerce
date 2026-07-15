using Microsoft.EntityFrameworkCore;
using ModularCommerce.Discovery.Domain.Embeddings;

namespace ModularCommerce.Discovery.Infrastructure.Persistence;

/// <summary>
/// Discovery şeması. Skaler kolonları (ProductId/SourceTextHash/UpdatedAtUtc) EF ile eşler ve migration'ı
/// bu modelden üretir; `Embedding` vektör kolonu pgvector `vector(N)` tipindedir ve EF tarafından
/// Ignore edilir — kolon migration'da raw SQL ile eklenir, okuma/yazma <see cref="ProductVectorRepository"/>
/// içinde ham NpgsqlDataSource + Pgvector ile yapılır (Pgvector.EntityFrameworkCore EF10 ile uyumsuz).
/// </summary>
public sealed class DiscoveryDbContext(DbContextOptions<DiscoveryDbContext> options)
    : DbContext(options)
{
    public const string Schema = "discovery";

    public DbSet<ProductEmbedding> ProductEmbeddings => Set<ProductEmbedding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DiscoveryDbContext).Assembly);
    }
}
