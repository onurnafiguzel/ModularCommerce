using System.ComponentModel.DataAnnotations;

namespace ModularCommerce.Discovery.Infrastructure.Embedding;

public enum EmbeddingProvider
{
    /// <summary>Deterministik, sırsız sözde-vektör — default; testler ve API anahtarı olmadan çalışma.</summary>
    Fake = 0,

    /// <summary>Gerçek embedding API'si (Azure OpenAI / OpenAI) — HttpClient + resilience pipeline.</summary>
    Http = 1,
}

/// <summary>
/// Embedding sağlayıcı ayarları. Provider hardcode DEĞİL — config'ten seçilir. Doğrulama deklaratif
/// (`[Range]`) ve AddValidatedOptions ile başlangıçta fail-fast.
/// </summary>
public sealed record EmbeddingOptions
{
    public const string SectionName = "Embedding";

    public EmbeddingProvider Provider { get; init; } = EmbeddingProvider.Fake;

    /// <summary>Gerçek sağlayıcı endpoint'i (Http provider'da zorunlu).</summary>
    public string? Endpoint { get; init; }

    /// <summary>API anahtarı (Http provider'da; sırdır — env/secret ile verilir).</summary>
    public string? ApiKey { get; init; }

    /// <summary>Model adı (ör. text-embedding-3-small).</summary>
    public string Model { get; init; } = "text-embedding-3-small";

    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 10;
}
