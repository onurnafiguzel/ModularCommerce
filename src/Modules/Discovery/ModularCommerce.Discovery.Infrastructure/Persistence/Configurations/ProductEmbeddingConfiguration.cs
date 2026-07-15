using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularCommerce.Discovery.Domain.Embeddings;

namespace ModularCommerce.Discovery.Infrastructure.Persistence.Configurations;

public sealed class ProductEmbeddingConfiguration : IEntityTypeConfiguration<ProductEmbedding>
{
    public void Configure(EntityTypeBuilder<ProductEmbedding> builder)
    {
        builder.ToTable("product_embeddings");

        builder.HasKey(e => e.ProductId);
        builder.Property(e => e.ProductId).ValueGeneratedNever(); // Catalog ürün kimliğiyle bire bir

        builder.Property(e => e.SourceTextHash).HasMaxLength(64).IsRequired(); // SHA-256 hex
        builder.Property(e => e.UpdatedAtUtc).IsRequired();

        // Vektör kolonu EF'e görünmez (pgvector `vector(N)` tipi) — migration'da raw SQL ile eklenir,
        // ProductVectorRepository ham SQL ile okur/yazar. Model drift'i olmaz (kolon EF snapshot'ında yok).
        builder.Ignore(e => e.Embedding);
    }
}
