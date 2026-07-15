using FluentValidation;

namespace ModularCommerce.Discovery.Application.Search;

public sealed class SearchQueryValidator : AbstractValidator<SearchQuery>
{
    public const int MaxTopN = 50;

    public SearchQueryValidator()
    {
        RuleFor(q => q.Query).NotEmpty().MaximumLength(1000);
        RuleFor(q => q.TopN).InclusiveBetween(1, MaxTopN);
    }
}
