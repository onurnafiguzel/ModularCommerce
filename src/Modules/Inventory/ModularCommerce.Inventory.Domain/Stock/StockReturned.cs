using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Domain.Stock;
public sealed record StockReturned(
    Guid ProductId,
    Guid ReservationId,
    int Quantity,
    DateTime OccurredOnUtc) : IDomainEvent;
