using System.Text.RegularExpressions;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Catalog.Domain.Products;

public sealed partial class Product : Entity
{
    public const int NameMaxLength = 200;
    public const int SkuMaxLength = 50;
    public const int DescriptionMaxLength = 2000;

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string Sku { get; private set; }
    public Money Price { get; private set; }
    public int StockQuantity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>EF Core materialization için; uygulama kodu asla çağırmaz.</summary>
    private Product()
    {
        Name = null!;
        Sku = null!;
        Price = null!;
    }

    private Product(string name, string? description, string sku, Money price, int stockQuantity)
    {
        var utcNow = DateTime.UtcNow;

        Name = name;
        Description = description;
        Sku = sku;
        Price = price;
        StockQuantity = stockQuantity;
        IsActive = true;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public static Result<Product> Create(
        string name,
        string? description,
        string sku,
        Money price,
        int stockQuantity)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > NameMaxLength)
        {
            return Result.Failure<Product>(ProductErrors.InvalidName);
        }

        if (string.IsNullOrWhiteSpace(sku) || sku.Length > SkuMaxLength || !SkuPattern().IsMatch(sku))
        {
            return Result.Failure<Product>(ProductErrors.InvalidSku);
        }

        if (stockQuantity < 0)
        {
            return Result.Failure<Product>(ProductErrors.NegativeStock);
        }

        var product = new Product(name.Trim(), NormalizeDescription(description), sku, price, stockQuantity);
        product.Raise(new ProductCreated(
            product.Id, product.Name, product.Description, product.Sku, product.IsActive, product.CreatedAtUtc));

        return Result.Success(product);
    }

    /// <summary>
    /// Aranabilir alanları (ad/açıklama/fiyat/aktiflik) günceller ve ProductUpdated yayınlar. SKU kimlik
    /// niteliğinde olduğundan değişmez. Stok Inventory'nin işidir; burada güncellenmez.
    /// </summary>
    public Result Update(string name, string? description, Money price, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > NameMaxLength)
        {
            return Result.Failure(ProductErrors.InvalidName);
        }

        Name = name.Trim();
        Description = NormalizeDescription(description);
        Price = price;
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;

        Raise(new ProductUpdated(Id, Name, Description, Sku, IsActive, UpdatedAtUtc));

        return Result.Success();
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed[..Math.Min(trimmed.Length, DescriptionMaxLength)];
    }

    [GeneratedRegex("^[A-Z0-9]+(-[A-Z0-9]+)*$")]
    private static partial Regex SkuPattern();
}
