namespace ModularCommerce.Discovery.Infrastructure.Embedding;

/// <summary>
/// Embedding API'sinin geçici hatası (ağ, 429, 5xx, timeout) — resilience pipeline'ının retry/breaker
/// göreceği tetikleyici. Kalıcı hatalardan (400 kötü istek) ayrıdır.
/// </summary>
public sealed class EmbeddingTransientException(string message, Exception? inner = null)
    : Exception(message, inner);
