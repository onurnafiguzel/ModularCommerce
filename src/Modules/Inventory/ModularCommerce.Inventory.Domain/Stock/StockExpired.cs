using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Domain.Stock;
public sealed record StockExpired(
    Guid ProductId,
    Guid ReservationId,
    int Quantity,
    DateTime OccurredOnUtc) : IDomainEvent;
