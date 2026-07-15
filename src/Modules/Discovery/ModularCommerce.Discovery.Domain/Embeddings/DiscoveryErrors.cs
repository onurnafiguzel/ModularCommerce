using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Discovery.Domain.Embeddings;

public static class DiscoveryErrors
{
    public static readonly Error InvalidProductId = Error.Validation(
        "Discovery.Embedding.InvalidProductId",
        "Ürün kimliği boş olamaz.");

    public static readonly Error EmptyEmbedding = Error.Validation(
        "Discovery.Embedding.Empty",
        "Embedding vektörü boş olamaz.");

    public static readonly Error EmptySourceHash = Error.Validation(
        "Discovery.Embedding.EmptySourceHash",
        "Kaynak metin özeti boş olamaz.");

    public static readonly Error EmptyQuery = Error.Validation(
        "Discovery.Search.EmptyQuery",
        "Arama sorgusu boş olamaz.");

    /// <summary>Embedding sağlayıcısı (dış API) geçici olarak ulaşılamıyor — istemci sonra tekrar dener.</summary>
    public static readonly Error EmbeddingUnavailable = Error.Conflict(
        "Discovery.Embedding.Unavailable",
        "Embedding servisi şu anda ulaşılamıyor, lütfen tekrar deneyin.",
        retryable: true);
}
