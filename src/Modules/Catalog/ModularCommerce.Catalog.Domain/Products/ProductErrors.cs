using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Catalog.Domain.Products;

public static class ProductErrors
{
    public static Error NotFound(Guid productId) => Error.NotFound(
        "Catalog.Product.NotFound",
        $"'{productId}' kimlikli ürün bulunamadı.");

    public static readonly Error InvalidName = Error.Validation(
        "Catalog.Product.InvalidName",
        $"Ürün adı boş olamaz ve en fazla {Product.NameMaxLength} karakter olabilir.");

    public static readonly Error InvalidSku = Error.Validation(
        "Catalog.Product.InvalidSku",
        $"SKU boş olamaz, en fazla {Product.SkuMaxLength} karakter olabilir ve yalnızca " +
        "büyük harf, rakam ve tire içerebilir (örn. ELK-1001).");

    public static readonly Error NegativeStock = Error.Validation(
        "Catalog.Product.NegativeStock",
        "Stok adedi negatif olamaz.");
}
