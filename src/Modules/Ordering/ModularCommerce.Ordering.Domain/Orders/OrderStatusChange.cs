namespace ModularCommerce.Ordering.Domain.Orders;
public sealed class OrderStatusChange
{
    /// <summary>null = siparişin doğuşu (∅ → Created).</summary>
    public OrderStatus? FromStatus { get; private set; }
    public OrderStatus ToStatus { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>Geçişi tetikleyen akış: "checkout" (H6), ileride "payment", "ttl-sweeper".</summary>
    public string TriggeredBy { get; private set; } = null!;

    private OrderStatusChange()
    {
    }

    internal OrderStatusChange(OrderStatus? fromStatus, OrderStatus toStatus, string triggeredBy)
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
        OccurredAtUtc = DateTime.UtcNow;
        TriggeredBy = triggeredBy;
    }
}
