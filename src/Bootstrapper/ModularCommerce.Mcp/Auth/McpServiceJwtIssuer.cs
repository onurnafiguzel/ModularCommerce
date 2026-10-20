using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ModularCommerce.Shared.Infrastructure.Auth;

namespace ModularCommerce.Mcp.Auth;

public sealed class McpServiceJwtIssuer(IOptions<JwtOptions> jwtOptions) : IServiceJwtIssuer
{
    public string IssueToken(Guid customerId)
    {
        var options = jwtOptions.Value;
        var mcp = options.Mcp
            ?? throw new InvalidOperationException("Jwt:Mcp yapılandırması eksik.");

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(mcp.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: mcp.Issuer,
            audience: options.Audience,
            claims:
            [
                new Claim("sub", customerId.ToString()),
                // amr: hangi auth yönteminden geldiğini işaretler — bu token'ın MCP'den
                // geçtiğini loglama/audit/ileride yetki daraltma için ayırt edilebilir kılar.
                new Claim("amr", "mcp"),
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
