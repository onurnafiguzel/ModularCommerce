using FluentValidation;

namespace ModularCommerce.Identity.Application.Auth.Logout;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(c => c.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token boş olamaz.");

        RuleFor(c => c.UserId)
            .NotEmpty()
            .WithMessage("Kullanıcı kimliği boş olamaz.");
    }
}
