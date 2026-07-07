using ModularCommerce.Shared.Kernel;
namespace ModularCommerce.Identity.Domain.Users;
public sealed record UserRegistered(Guid UserId, string Email, DateTime OccurredOnUtc) : IDomainEvent;
