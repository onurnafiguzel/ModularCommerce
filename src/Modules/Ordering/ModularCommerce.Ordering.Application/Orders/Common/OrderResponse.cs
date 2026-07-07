using ModularCommerce.Ordering.Domain.Orders;

namespace ModularCommerce.Ordering.Application.Orders.Common;

public sealed record OrderResponse(
    Guid Id,
    string Status,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAtUtc,
    IReadOnlyList<OrderLineResponse> Lines,
    IReadOnlyList<OrderStatusChangeResponse> History)
{
    public static OrderResponse FromOrder(Order order)
        => new(
            order.Id,
            order.Status.ToString(),
            order.TotalAmount,
            order.Currency,
            order.CreatedAtUtc,
            [.. order.Lines.Select(l => new OrderLineResponse(
                l.ProductId, l.ProductName, l.UnitPrice, l.Currency, l.Quantity))],
            [.. order.StatusHistory.Select(h => new OrderStatusChangeResponse(
                h.FromStatus?.ToString(), h.ToStatus.ToString(), h.OccurredAtUtc, h.TriggeredBy))]);
}

public sealed record OrderLineResponse(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    string Currency,
    int Quantity);

/// <summary>NFR-5.3 kanıtı yanıtta görünür: her geçiş kim/ne zaman/hangi tetikleyici.</summary>
public sealed record OrderStatusChangeResponse(
    string? FromStatus,
    string ToStatus,
    DateTime OccurredAtUtc,
    string TriggeredBy);

public sealed record OrderSummaryResponse(
    Guid Id,
    string Status,
    decimal TotalAmount,
    string Currency,
    int LineCount,
    DateTime CreatedAtUtc);
