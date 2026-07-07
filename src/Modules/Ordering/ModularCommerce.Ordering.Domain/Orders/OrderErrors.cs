using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Ordering.Domain.Orders;

public static class OrderErrors
{
    public static Error InvalidStateTransition(OrderStatus from, OrderStatus to) => Error.Conflict(
        "Ordering.Order.InvalidStateTransition",
        $"Sipariş '{from}' durumundan '{to}' durumuna geçemez.");
    public static Error NotFound(Guid orderId) => Error.NotFound(
        "Ordering.Order.NotFound",
        $"'{orderId}' kimlikli sipariş bulunamadı.");
    public static readonly Error InvalidCustomerId = Error.Validation(
        "Ordering.Order.InvalidCustomerId",
        "Müşteri kimliği boş olamaz.");
    public static readonly Error InvalidIdempotencyKey = Error.Validation(
        "Ordering.Order.InvalidIdempotencyKey",
        $"Idempotency anahtarı boş olamaz ve en fazla {Order.IdempotencyKeyMaxLength} karakter olabilir.");
    public static readonly Error NoLines = Error.Validation(
        "Ordering.Order.NoLines",
        "Sipariş en az bir satır içermelidir.");
    public static readonly Error InvalidLine = Error.Validation(
        "Ordering.Order.InvalidLine",
        "Sipariş satırı geçersiz: ürün, ad, adet ve fiyat dolu ve pozitif olmalıdır.");
    public static readonly Error CurrencyMismatch = Error.Validation(
        "Ordering.Order.CurrencyMismatch",
        "Tüm sipariş satırları aynı para biriminde olmalıdır.");
    public static readonly Error EmptyCart = Error.Validation(
        "Ordering.Checkout.EmptyCart",
        "Sepet boş; checkout yapılamaz.");
    public static Error ProductUnavailable(Guid productId) => Error.Conflict(
        "Ordering.Checkout.ProductUnavailable",
        $"'{productId}' ürünü satışta değil; sepetinizi güncelleyip tekrar deneyin.");
    public static readonly Error DuplicateIdempotencyKey = Error.Conflict(
        "Ordering.Checkout.DuplicateIdempotencyKey",
        "Bu istek zaten işlendi.");
}
