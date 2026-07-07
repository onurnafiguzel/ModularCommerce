using FluentValidation;

namespace ModularCommerce.Identity.Application.Auth.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty()
            .WithMessage("E-posta boş olamaz.");

        RuleFor(c => c.Password)
            .NotEmpty()
            .WithMessage("Şifre boş olamaz.");
    }
}
