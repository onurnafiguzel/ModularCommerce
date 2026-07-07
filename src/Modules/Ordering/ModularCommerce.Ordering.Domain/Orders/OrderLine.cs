namespace ModularCommerce.Ordering.Domain.Orders;
public sealed class OrderLine
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public decimal UnitPrice { get; private set; }
    public string Currency { get; private set; } = null!;
    public int Quantity { get; private set; }

    /// <summary>Satırı karşılayan Inventory rezervasyonu (telafi/commit izi).</summary>
    public Guid ReservationId { get; private set; }

    private OrderLine()
    {
    }

    internal OrderLine(OrderLineDraft draft)
    {
        ProductId = draft.ProductId;
        ProductName = draft.ProductName;
        UnitPrice = draft.UnitPrice;
        Currency = draft.Currency;
        Quantity = draft.Quantity;
        ReservationId = draft.ReservationId;
    }
}

public sealed record OrderLineDraft(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    string Currency,
    int Quantity,
    Guid ReservationId);
