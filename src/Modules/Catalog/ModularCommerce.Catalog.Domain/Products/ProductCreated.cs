using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Catalog.Domain.Products;

public sealed record ProductCreated(Guid ProductId, DateTime OccurredOnUtc) : IDomainEvent;
