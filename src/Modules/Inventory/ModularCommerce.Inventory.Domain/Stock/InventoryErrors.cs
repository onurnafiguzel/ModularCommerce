using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Domain.Stock;

public static class InventoryErrors
{
    public static Error StockItemNotFound(Guid productId) => Error.NotFound(
        "Inventory.StockItem.NotFound",
        $"'{productId}' ürünü için stok kaydı bulunamadı.");

    public static Error ReservationNotFound(Guid reservationId) => Error.NotFound(
        "Inventory.Reservation.NotFound",
        $"'{reservationId}' kimlikli rezervasyon bulunamadı.");

    public static readonly Error InvalidProductId = Error.Validation(
        "Inventory.StockItem.InvalidProductId",
        "Ürün kimliği boş olamaz.");

    public static readonly Error InvalidOnHand = Error.Validation(
        "Inventory.StockItem.InvalidOnHand",
        "Stok adedi negatif olamaz.");

    public static readonly Error InvalidQuantity = Error.Validation(
        "Inventory.Reservation.InvalidQuantity",
        "Rezervasyon adedi 1 veya daha büyük olmalıdır.");

    public static readonly Error InsufficientStock = Error.Conflict(
        "Inventory.InsufficientStock",
        "Yeterli stok yok.");

    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "Inventory.ConcurrencyConflict",
        "Stok bilgisi güncellendi, lütfen tekrar deneyin.");

    public static readonly Error LockTimeout = Error.Conflict(
        "Inventory.LockTimeout",
        "Stok şu anda yoğun, lütfen tekrar deneyin.");

    public static readonly Error LockUnavailable = Error.Failure(
        "Inventory.LockUnavailable",
        "Stok kilidi servisine ulaşılamıyor, işlem reddedildi.");
}
