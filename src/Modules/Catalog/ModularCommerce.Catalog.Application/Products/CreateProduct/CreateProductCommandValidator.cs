using FluentValidation;
using ModularCommerce.Catalog.Domain.Products;

namespace ModularCommerce.Catalog.Application.Products.CreateProduct;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Product.NameMaxLength);
        RuleFor(c => c.Description).MaximumLength(Product.DescriptionMaxLength);
        RuleFor(c => c.Sku).NotEmpty().MaximumLength(Product.SkuMaxLength);
        RuleFor(c => c.Price).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Currency).NotEmpty().Length(3);
        RuleFor(c => c.StockQuantity).GreaterThanOrEqualTo(0);
    }
}
