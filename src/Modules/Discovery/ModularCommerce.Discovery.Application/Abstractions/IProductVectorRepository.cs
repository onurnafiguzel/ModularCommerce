using ModularCommerce.Discovery.Domain.Embeddings;

namespace ModularCommerce.Discovery.Application.Abstractions;

/// <summary>Vektör benzerlik araması sonucu: ürün + benzerlik skoru (1 = birebir, 0 = ilgisiz).</summary>
public sealed record VectorMatch(Guid ProductId, double Score);

/// <summary>
/// Ürün embedding'lerinin kalıcılık portu. Implementasyon pgvector (kosinüs mesafesi) üzerinde
/// raw SQL ile çalışır (Infrastructure). Upsert PK=ProductId ile idempotenttir.
/// </summary>
public interface IProductVectorRepository
{
    Task UpsertAsync(ProductEmbedding embedding, CancellationToken cancellationToken);

    /// <summary>Mevcut kayıt varsa kaynak metin özetini döner; yoksa null (yeniden-embedding kararı için).</summary>
    Task<string?> GetSourceTextHashAsync(Guid productId, CancellationToken cancellationToken);

    /// <summary>Sorgu vektörüne en yakın topN ürünü kosinüs benzerliğiyle sıralı döner.</summary>
    Task<IReadOnlyList<VectorMatch>> SearchAsync(float[] query, int topN, CancellationToken cancellationToken);
}
