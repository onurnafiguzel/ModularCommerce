using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Ordering.Domain.Orders;
public sealed class OrderLine
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public Money UnitPrice { get; private set; } = null!;
    public int Quantity { get; private set; }

    public Guid ReservationId { get; private set; }
    public Money LineTotal => UnitPrice.Multiply(Quantity);

    private OrderLine()
    {
    }

    internal OrderLine(
        Guid productId,
        string productName,
        Money unitPrice,
        int quantity,
        Guid reservationId)
    {
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
        ReservationId = reservationId;
    }
}

public sealed record OrderLineDraft(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    string Currency,
    int Quantity,
    Guid ReservationId);
