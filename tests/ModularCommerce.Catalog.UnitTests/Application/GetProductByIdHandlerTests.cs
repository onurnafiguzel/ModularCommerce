using FluentAssertions;
using ModularCommerce.Catalog.Application.Abstractions;
using ModularCommerce.Catalog.Application.Products.Common;
using ModularCommerce.Catalog.Application.Products.GetProductById;
using ModularCommerce.Catalog.Domain.Products;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Catalog.UnitTests.Application;

public class GetProductByIdHandlerTests
{
    private readonly IProductQueries _queries = Substitute.For<IProductQueries>();

    [Fact(DisplayName = "Ürün yoksa domain katalogundan NotFound döner")]
    public async Task HandleAsync_WhenProductMissing_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _queries.GetProductByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((ProductDetailResponse?)null);

        var handler = new GetProductByIdHandler(_queries);
        var result = await handler.HandleAsync(id, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.NotFound(id));
    }

    [Fact(DisplayName = "Ürün varsa okuma-modeli yanıtı aynen döner")]
    public async Task HandleAsync_WhenProductExists_ReturnsDetailResponse()
    {
        var id = Guid.NewGuid();
        var detail = new ProductDetailResponse(
            id, "Kablosuz Kulaklık", "Açıklama", "ELK-1001", 2499.90m, "TRY", 120, true, DateTime.UtcNow);
        _queries.GetProductByIdAsync(id, Arg.Any<CancellationToken>()).Returns(detail);

        var handler = new GetProductByIdHandler(_queries);
        var result = await handler.HandleAsync(id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(detail);
    }
}
