using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Domain.Stock;

public sealed record ProductSoldOut(
    Guid ProductId, 
    DateTime OccurredOnUtc) : IDomainEvent;
