using FluentValidation;

namespace ModularCommerce.Catalog.Application.Products.GetProducts;

public sealed class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    public const int PageSizeMax = 100;
    public const int SearchMaxLength = 100;

    public GetProductsQueryValidator()
    {
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Sayfa numarası 1 veya daha büyük olmalıdır.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, PageSizeMax)
            .WithMessage($"Sayfa boyutu 1 ile {PageSizeMax} arasında olmalıdır.");

        RuleFor(q => q.Search)
            .MaximumLength(SearchMaxLength)
            .WithMessage($"Arama metni en fazla {SearchMaxLength} karakter olabilir.");

        RuleFor(q => q.MinPrice)
            .GreaterThanOrEqualTo(0)
            .When(q => q.MinPrice.HasValue)
            .WithMessage("Alt fiyat sınırı negatif olamaz.");

        RuleFor(q => q.MaxPrice)
            .GreaterThanOrEqualTo(q => q.MinPrice)
            .When(q => q.MinPrice.HasValue && q.MaxPrice.HasValue)
            .WithMessage("Üst fiyat sınırı, alt fiyat sınırından küçük olamaz.");
    }
}
