namespace ModularCommerce.Shared.Kernel;

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
