using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Discovery.Domain.Embeddings;

/// <summary>
/// Bir ürünün aranabilir metninin vektör temsili (embedding). Kimliği ProductId'dir (Catalog'daki
/// ürünle bire bir). SourceTextHash, aynı metin için gereksiz yeniden-embedding'i önlemek içindir
/// (idempotent indeksleme). Vektör Domain'de <see cref="float"/>[] olarak yaşar — pgvector tipi
/// Infrastructure'da eşlenir, Domain saf kalır.
/// </summary>
public sealed class ProductEmbedding
{
    public Guid ProductId { get; private set; }
    public float[] Embedding { get; private set; }
    public string SourceTextHash { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>EF materialization için; uygulama kodu asla çağırmaz.</summary>
    private ProductEmbedding()
    {
        Embedding = null!;
        SourceTextHash = null!;
    }

    private ProductEmbedding(Guid productId, float[] embedding, string sourceTextHash)
    {
        ProductId = productId;
        Embedding = embedding;
        SourceTextHash = sourceTextHash;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static Result<ProductEmbedding> Create(Guid productId, float[] embedding, string sourceTextHash)
    {
        if (productId == Guid.Empty)
        {
            return Result.Failure<ProductEmbedding>(DiscoveryErrors.InvalidProductId);
        }

        if (embedding is null || embedding.Length == 0)
        {
            return Result.Failure<ProductEmbedding>(DiscoveryErrors.EmptyEmbedding);
        }

        if (string.IsNullOrWhiteSpace(sourceTextHash))
        {
            return Result.Failure<ProductEmbedding>(DiscoveryErrors.EmptySourceHash);
        }

        return Result.Success(new ProductEmbedding(productId, embedding, sourceTextHash));
    }
}
