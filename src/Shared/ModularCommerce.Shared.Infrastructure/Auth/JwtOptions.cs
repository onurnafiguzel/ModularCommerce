namespace ModularCommerce.Shared.Infrastructure.Auth;
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 simetrik anahtar için makul alt sınır (32 karakter = 256 bit).</summary>
    public const int MinSigningKeyLength = 32;

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;

    /// <summary>Dev'de appsettings'ten gelir; prod anahtar yönetimi Hafta 12 (Key Vault).</summary>
    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 7;
}
