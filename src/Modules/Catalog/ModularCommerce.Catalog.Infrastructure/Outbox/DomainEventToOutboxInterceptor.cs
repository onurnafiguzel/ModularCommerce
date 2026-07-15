using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Catalog.Infrastructure.Outbox;

/// <summary>
/// SRP: TEK işi ChangeTracker'daki Entity'lerin domain event'lerini toplayıp integration mapping'i
/// olanları OutboxMessage'a yazmaktır. Command handler event bilmez. Yazım ürün ile AYNI SaveChanges'e
/// girdiği için outbox satırı mutasyonla atomiktir (SaveChanges patlarsa ikisi de geri sarılır).
/// </summary>
public sealed class DomainEventToOutboxInterceptor(IIntegrationEventMapper mapper)
    : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        CollectOutboxMessages(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CollectOutboxMessages(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void CollectOutboxMessages(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var entities = context.ChangeTracker.Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        foreach (var entity in entities)
        {
            foreach (var domainEvent in entity.DomainEvents)
            {
                // OCP: yalnız registry'de karşılığı olan event'ler dışa terfi eder.
                if (mapper.TryMap(domainEvent) is not { } mapped)
                {
                    continue;
                }

                context.Set<OutboxMessage>().Add(new OutboxMessage
                {
                    Type = mapped.Type,
                    Content = JsonSerializer.Serialize(mapped.IntegrationEvent, JsonOptions),
                    OccurredOnUtc = domainEvent.OccurredOnUtc,
                });
            }

            // Event'ler outbox'a geçti; entity üzerinde tekrar toplanmasınlar.
            entity.ClearDomainEvents();
        }
    }
}
