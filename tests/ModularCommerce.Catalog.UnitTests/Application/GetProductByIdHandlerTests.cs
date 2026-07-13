using FluentAssertions;
using ModularCommerce.Catalog.Application.Products.GetProductById;
using ModularCommerce.Catalog.Domain.Products;
using ModularCommerce.Shared.Kernel;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Catalog.UnitTests.Application;

public class GetProductByIdHandlerTests
{
    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();

    [Fact(DisplayName = "Ürün yoksa domain katalogundan NotFound döner")]
    public async Task HandleAsync_WhenProductMissing_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Product?)null);

        var handler = new GetProductByIdHandler(_repository);
        var result = await handler.HandleAsync(id, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.NotFound(id));
    }

    [Fact(DisplayName = "Ürün varsa alanlar DTO'ya doğru eşlenir")]
    public async Task HandleAsync_WhenProductExists_MapsToDetailResponse()
    {
        var product = Product.Create(
            "Kablosuz Kulaklık", "Açıklama", "ELK-1001",
            Money.Create(2499.90m).Value, 120).Value;
        _repository.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);

        var handler = new GetProductByIdHandler(_repository);
        var result = await handler.HandleAsync(product.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(product.Id);
        result.Value.Name.Should().Be("Kablosuz Kulaklık");
        result.Value.Description.Should().Be("Açıklama");
        result.Value.Sku.Should().Be("ELK-1001");
        result.Value.Price.Should().Be(2499.90m);
        result.Value.Currency.Should().Be("TRY");
        result.Value.StockQuantity.Should().Be(120);
        result.Value.IsActive.Should().BeTrue();
    }
}
