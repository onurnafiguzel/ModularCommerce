using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ModularCommerce.Shared.Infrastructure.Auth;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(JwtOptions.SectionName);
        var options = section.Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                $"'{JwtOptions.SectionName}' yapılandırma bölümü bulunamadı");

        // Fail-fast (AddRedis emsali): eksik/zayıf anahtarla boot edilmez.
        if (string.IsNullOrWhiteSpace(options.SigningKey)
            || options.SigningKey.Length < JwtOptions.MinSigningKeyLength)
        {
            throw new InvalidOperationException(
                $"Jwt:SigningKey en az {JwtOptions.MinSigningKeyLength} karakter olmalıdır");
        }

        services.Configure<JwtOptions>(section);

        // issuer → signing key eşlemesi. Host'un KENDİ anahtarıyla başlar; Jwt:Mcp
        // yapılandırılmışsa (MCP deploy edilmişse) ikinci, AYRI bir issuer/key çifti eklenir.
        // IssuerSigningKeyResolver issuer'ı belirli bir key'e BAĞLAR — basit bir
        // IssuerSigningKeys+ValidIssuers listesi YETERSİZDİR, çünkü imzayı listedeki
        // HERHANGİ bir key'e karşı doğrular, hangi issuer'ın hangi key'i kullanması
        // gerektiğini kilitlemez (MCP key'iyle imzalanmış ama iss=Host olan sahte bir
        // token da geçerdi).
        var issuerKeys = new Dictionary<string, SecurityKey>
        {
            [options.Issuer] = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        };

        if (options.Mcp is { Issuer.Length: > 0, SigningKey.Length: > 0 } mcp)
        {
            issuerKeys[mcp.Issuer] = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(mcp.SigningKey));
        }

        var validIssuers = issuerKeys.Keys.ToArray();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                // "sub" claim'i ClaimTypes.NameIdentifier'a çevrilmeden ham okunur —
                // ClaimsPrincipalExtensions.GetUserId buna güvenir.
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuers = validIssuers,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = (_, securityToken, _, _) =>
                        // Issuer, SecurityToken'ın TÜM alt tiplerinde (JwtSecurityToken,
                        // JsonWebToken) base class seviyesinde tanımlı — tipe özel cast gerekmez.
                        issuerKeys.TryGetValue(securityToken.Issuer, out var key)
                            ? [key]
                            : [],
                };
            });

        services.AddAuthorization();

        return services;
    }
}
