using FluentValidation;

namespace ModularCommerce.Cart.Application.Carts.AddItem;

public sealed class AddItemCommandValidator : AbstractValidator<AddItemCommand>
{
    public AddItemCommandValidator()
    {
        RuleFor(c => c.ProductId)
            .NotEmpty()
            .WithMessage("Ürün kimliği boş olamaz.");

        RuleFor(c => c.Quantity)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Adet 1 veya daha büyük olmalıdır.");
    }
}
