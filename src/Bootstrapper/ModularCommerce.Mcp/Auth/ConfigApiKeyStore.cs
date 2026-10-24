using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace ModularCommerce.Mcp.Auth;

/// <summary>
/// "Mcp:ApiKeys" bölümünden (key = API key'in SHA-256 hex hash'i, value = CustomerId) okur.
/// API key'in DÜZ METNİ hiçbir yerde (config, Secret, log) saklanmaz — sadece hash'i. Karşılaştırma
/// sabit zamanlıdır (CryptographicOperations.FixedTimeEquals), timing-attack'e karşı.
/// </summary>
public sealed class ConfigApiKeyStore(IConfiguration configuration) : IApiKeyStore
{
    private const string SectionName = "Mcp:ApiKeys";

    public Guid? ResolveCustomerId(string apiKey)
    {
        var candidateHashBytes = Encoding.UTF8.GetBytes(Hash(apiKey));

        foreach (var entry in configuration.GetSection(SectionName).GetChildren())
        {
            var storedHashBytes = Encoding.UTF8.GetBytes(entry.Key);

            if (storedHashBytes.Length == candidateHashBytes.Length
                && CryptographicOperations.FixedTimeEquals(candidateHashBytes, storedHashBytes)
                && Guid.TryParse(entry.Value, out var customerId))
            {
                return customerId;
            }
        }

        return null;
    }

    /// <summary>Operatörün gerçek API key'ini bu hash'e çevirip config'e yazması için.</summary>
    internal static string Hash(string apiKey)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));
}
