using FluentValidation;

namespace ModularCommerce.Inventory.Application.Reservations.ReserveStock;

public sealed class ReserveStockCommandValidator : AbstractValidator<ReserveStockCommand>
{
    public ReserveStockCommandValidator()
    {
        RuleFor(c => c.ProductId)
            .NotEmpty()
            .WithMessage("Ürün kimliği boş olamaz.");

        RuleFor(c => c.Quantity)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Rezervasyon adedi 1 veya daha büyük olmalıdır.");
    }
}
