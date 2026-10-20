namespace ModularCommerce.Mcp.Auth;

/// <summary>
/// X-Api-Key header'ındaki anahtarı sabit bir CustomerId'ye çözer. İlk implementasyon
/// config/Secret tabanlı (<see cref="ConfigApiKeyStore"/>) — arayüz soyut olduğu için ileride
/// OAuth/DB tabanlı bir implementasyona geçmek yeni bir kayıt, kod değişikliği değil.
/// </summary>
public interface IApiKeyStore
{
    /// <summary>Anahtar geçerliyse bağlı CustomerId'yi, değilse null döner.</summary>
    Guid? ResolveCustomerId(string apiKey);
}
