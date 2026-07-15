using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Discovery.Application.Abstractions;

/// <summary>
/// Metni bir vektör embedding'e çeviren dış servis portu (provider-agnostik). Implementasyon
/// (Fake / Http) Infrastructure'da; sağlayıcı config ile seçilir. Geçici hatalar Result.Failure
/// (retryable) olarak döner — çağıran istisna yakalamak zorunda kalmaz.
/// </summary>
public interface IEmbeddingService
{
    Task<Result<float[]>> EmbedAsync(string text, CancellationToken cancellationToken);
}
