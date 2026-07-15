namespace ModularCommerce.Discovery.Infrastructure.Embedding;

public static class EmbeddingConstants
{
    /// <summary>
    /// Vektör boyutu — hem pgvector kolonu (vector(N)) hem Fake sağlayıcı hem gerçek modelin çıktısı
    /// bu değere uymalıdır (OpenAI text-embedding-3-small = 1536). Değiştirmek migration gerektirir,
    /// bu yüzden dağıtım başına sabittir (config'te değil).
    /// </summary>
    public const int Dimensions = 1536;
}
