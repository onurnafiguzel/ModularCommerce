using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Cart.Domain.Carts;

public static class CartErrors
{
    public static readonly Error InvalidCustomerId = Error.Validation(
        "Cart.InvalidCustomerId",
        "Müşteri kimliği boş olamaz.");

    public static readonly Error InvalidProductId = Error.Validation(
        "Cart.InvalidProductId",
        "Ürün kimliği boş olamaz.");

    public static readonly Error InvalidQuantity = Error.Validation(
        "Cart.InvalidQuantity",
        "Adet 1 veya daha büyük olmalıdır.");

    public static readonly Error QuantityLimitExceeded = Error.Validation(
        "Cart.QuantityLimitExceeded",
        $"Bir üründen sepete en fazla {Carts.Cart.MaxQuantityPerLine} adet eklenebilir.");

    public static readonly Error LineLimitExceeded = Error.Validation(
        "Cart.LineLimitExceeded",
        $"Sepette en fazla {Carts.Cart.MaxLines} farklı ürün olabilir.");

    public static Error ItemNotFound(Guid productId) => Error.NotFound(
        "Cart.ItemNotFound",
        $"'{productId}' ürünü sepette yok.");

    public static readonly Error StorageUnavailable = Error.Failure(
        "Cart.StorageUnavailable",
        "Sepet servisine şu anda ulaşılamıyor, lütfen tekrar deneyin.");
}
