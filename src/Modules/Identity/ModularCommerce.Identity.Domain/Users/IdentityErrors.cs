using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Identity.Domain.Users;

public static class IdentityErrors
{
    public static readonly Error InvalidEmail = Error.Validation(
        "Identity.User.InvalidEmail",
        "Geçerli bir e-posta adresi giriniz.");
    public static readonly Error EmptyPasswordHash = Error.Validation(
        "Identity.User.EmptyPasswordHash",
        "Şifre özeti boş olamaz.");
    public static readonly Error EmailAlreadyExists = Error.Conflict(
        "Identity.EmailAlreadyExists",
        "Bu e-posta adresiyle kayıtlı bir kullanıcı zaten var.");
    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Identity.InvalidCredentials",
        "E-posta veya şifre hatalı.");
    public static readonly Error RefreshTokenInvalid = Error.Unauthorized(
        "Identity.RefreshTokenInvalid",
        "Oturum yenilenemedi, lütfen tekrar giriş yapın.");
    public static readonly Error InvalidUserId = Error.Validation(
        "Identity.RefreshToken.InvalidUserId",
        "Kullanıcı kimliği boş olamaz.");
    public static readonly Error EmptyTokenHash = Error.Validation(
        "Identity.RefreshToken.EmptyTokenHash",
        "Token özeti boş olamaz.");
    public static readonly Error InvalidExpiry = Error.Validation(
        "Identity.RefreshToken.InvalidExpiry",
        "Son kullanma zamanı gelecekte olmalıdır.");
}
