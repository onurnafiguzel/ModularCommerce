using FluentAssertions;
using ModularCommerce.Catalog.Domain.Products;
using ModularCommerce.Catalog.Domain.ValueObjects;
using Xunit;

namespace ModularCommerce.Catalog.UnitTests.Domain;

/// <summary>
/// Product aggregate'inin invariant testleri — iş kurallarının asıl test yüzeyi burasıdır.
/// </summary>
public class ProductTests
{
    private static Money ValidPrice => Money.Create(100m).Value;

    [Fact(DisplayName = "Geçerli değerlerle ürün oluşturulur ve aktif başlar")]
    public void Create_WithValidValues_ReturnsActiveProduct()
    {
        var result = Product.Create("Kablosuz Kulaklık", "Açıklama", "ELK-1001", ValidPrice, 10);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Kablosuz Kulaklık");
        result.Value.Sku.Should().Be("ELK-1001");
        result.Value.IsActive.Should().BeTrue();
        result.Value.StockQuantity.Should().Be(10);
    }

    [Fact(DisplayName = "Başarılı oluşturma ProductCreated event'ini üretir")]
    public void Create_WithValidValues_RaisesProductCreated()
    {
        var result = Product.Create("Ürün", null, "SKU-1", ValidPrice, 0);

        result.Value.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProductCreated>()
            .Which.ProductId.Should().Be(result.Value.Id);
    }

    [Theory(DisplayName = "Boş veya aşırı uzun ad reddedilir")]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ReturnsFailure(string name)
    {
        var result = Product.Create(name, null, "SKU-1", ValidPrice, 0);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.InvalidName);
    }

    [Fact(DisplayName = "200 karakterden uzun ad reddedilir")]
    public void Create_WithTooLongName_ReturnsFailure()
    {
        var result = Product.Create(new string('a', Product.NameMaxLength + 1), null, "SKU-1", ValidPrice, 0);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.InvalidName);
    }

    [Theory(DisplayName = "Geçersiz SKU biçimi reddedilir")]
    [InlineData("")]
    [InlineData("elk-1001")]
    [InlineData("ELK 1001")]
    [InlineData("-ELK")]
    [InlineData("ELK-")]
    [InlineData("ÜRN-1")]
    public void Create_WithInvalidSku_ReturnsFailure(string sku)
    {
        var result = Product.Create("Ürün", null, sku, ValidPrice, 0);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.InvalidSku);
    }

    [Fact(DisplayName = "Negatif stok reddedilir")]
    public void Create_WithNegativeStock_ReturnsFailure()
    {
        var result = Product.Create("Ürün", null, "SKU-1", ValidPrice, -1);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.NegativeStock);
    }

    [Fact(DisplayName = "Ad ve açıklama kırpılır, boş açıklama null olur")]
    public void Create_TrimsNameAndNormalizesDescription()
    {
        var result = Product.Create("  Ürün  ", "   ", "SKU-1", ValidPrice, 0);

        result.Value.Name.Should().Be("Ürün");
        result.Value.Description.Should().BeNull();
    }
}
