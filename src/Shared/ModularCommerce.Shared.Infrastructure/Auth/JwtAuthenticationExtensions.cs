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
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(options.SigningKey)),
                };
            });

        services.AddAuthorization();

        return services;
    }
}
