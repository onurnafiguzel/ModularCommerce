using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Domain.Stock;

public sealed record StockReserved(
    Guid ProductId,
    Guid ReservationId,
    int Quantity,
    DateTime OccurredOnUtc) : IDomainEvent;
