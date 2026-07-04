using FluentAssertions;
using ModularCommerce.Catalog.Application.Products.GetProducts;
using Xunit;

namespace ModularCommerce.Catalog.UnitTests.Application;

/// <summary>İstek biçimi doğrulaması — iş kuralı değil, HTTP-şekli koruması.</summary>
public class GetProductsQueryValidatorTests
{
    private readonly GetProductsQueryValidator _validator = new();

    [Fact(DisplayName = "Varsayılan sorgu geçerlidir")]
    public void Validate_DefaultQuery_IsValid()
        => _validator.Validate(new GetProductsQuery()).IsValid.Should().BeTrue();

    [Theory(DisplayName = "Geçersiz sayfa/sayfa boyutu reddedilir")]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, GetProductsQueryValidator.PageSizeMax + 1)]
    public void Validate_InvalidPaging_IsInvalid(int page, int pageSize)
        => _validator.Validate(new GetProductsQuery(page, pageSize)).IsValid.Should().BeFalse();

    [Fact(DisplayName = "Aşırı uzun arama metni reddedilir")]
    public void Validate_TooLongSearch_IsInvalid()
    {
        var query = new GetProductsQuery(Search: new string('a', GetProductsQueryValidator.SearchMaxLength + 1));

        _validator.Validate(query).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Alt fiyat üst fiyattan büyükse reddedilir")]
    public void Validate_MinPriceGreaterThanMaxPrice_IsInvalid()
    {
        var query = new GetProductsQuery(MinPrice: 100, MaxPrice: 50);

        _validator.Validate(query).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Negatif alt fiyat reddedilir")]
    public void Validate_NegativeMinPrice_IsInvalid()
        => _validator.Validate(new GetProductsQuery(MinPrice: -1)).IsValid.Should().BeFalse();

    [Fact(DisplayName = "Geçerli fiyat aralığı kabul edilir")]
    public void Validate_ValidPriceRange_IsValid()
        => _validator.Validate(new GetProductsQuery(MinPrice: 50, MaxPrice: 100)).IsValid.Should().BeTrue();
}
