namespace ModularCommerce.Mcp.Auth;

/// <summary>
/// ModularCommerce.Host'a yapılacak çağrılar için, belirli bir müşteri adına kısa ömürlü bir JWT
/// üretir. Host'un ana Jwt:SigningKey'inden AYRI bir issuer/signing key (Jwt:Mcp) kullanır —
/// bu anahtar ele geçirilse bile yalnız "modularcommerce-mcp" issuer'lı token üretilebilir.
/// </summary>
public interface IServiceJwtIssuer
{
    string IssueToken(Guid customerId);
}
