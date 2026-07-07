using FluentValidation;

namespace ModularCommerce.Identity.Application.Auth.Refresh;

public sealed class RefreshCommandValidator : AbstractValidator<RefreshCommand>
{
    public RefreshCommandValidator()
    {
        RuleFor(c => c.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token boş olamaz.");
    }
}
