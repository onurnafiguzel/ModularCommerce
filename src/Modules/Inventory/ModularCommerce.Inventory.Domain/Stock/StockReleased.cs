using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Domain.Stock;
public sealed record StockReleased(
    Guid ProductId,
    Guid ReservationId,
    int Quantity,
    DateTime OccurredOnUtc) : IDomainEvent;
