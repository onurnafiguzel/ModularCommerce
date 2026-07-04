using FluentAssertions;
using FluentValidation;
using ModularCommerce.Catalog.Application.Abstractions;
using ModularCommerce.Catalog.Application.Common;
using ModularCommerce.Catalog.Application.Products.Common;
using ModularCommerce.Catalog.Application.Products.GetProducts;
using ModularCommerce.Shared.Kernel;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Catalog.UnitTests.Application;

public class GetProductsHandlerTests
{
    private readonly IProductQueries _queries = Substitute.For<IProductQueries>();
    private readonly IValidator<GetProductsQuery> _validator = new GetProductsQueryValidator();

    private GetProductsHandler CreateHandler() => new(_queries, _validator);

    [Fact(DisplayName = "Geçersiz sayfalama Validation hatası döner, sorgu çalıştırılmaz")]
    public async Task HandleAsync_WithInvalidPaging_ReturnsValidationError()
    {
        var result = await CreateHandler().HandleAsync(
            new GetProductsQuery(Page: 0, PageSize: 0), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        await _queries.DidNotReceiveWithAnyArgs().GetProductsAsync(default!, default);
    }

    [Fact(DisplayName = "Geçerli sorgu read-side port'a delege edilir")]
    public async Task HandleAsync_WithValidQuery_DelegatesToQueries()
    {
        var query = new GetProductsQuery(Page: 1, PageSize: 10);
        var page = new PagedResponse<ProductSummaryResponse>([], 1, 10, 0);
        _queries.GetProductsAsync(query, Arg.Any<CancellationToken>()).Returns(page);

        var result = await CreateHandler().HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(page);
    }
}
