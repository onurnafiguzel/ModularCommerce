using FluentValidation;
using ModularCommerce.Identity.Domain.Users;

namespace ModularCommerce.Identity.Application.Auth.Signup;
public sealed class SignupCommandValidator : AbstractValidator<SignupCommand>
{
    public SignupCommandValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty()
            .WithMessage("E-posta boş olamaz.")
            .MaximumLength(Email.MaxLength)
            .WithMessage($"E-posta en fazla {Email.MaxLength} karakter olabilir.");

        RuleFor(c => c.Password)
            .NotEmpty()
            .WithMessage("Şifre boş olamaz.")
            .MinimumLength(8)
            .WithMessage("Şifre en az 8 karakter olmalıdır.")
            .MaximumLength(128)
            .WithMessage("Şifre en fazla 128 karakter olabilir.");
    }
}
