namespace ModularCommerce.Ordering.Domain.Orders;

/// <summary>Sipariş yaşam döngüsü durumları (FR-5.1).</summary>
public enum OrderStatus
{
    Created = 0,
    StockReserved = 1,
    PaymentPending = 2,
    Paid = 3,
    Shipped = 4,
    Cancelled = 5,
    Expired = 6,
}
