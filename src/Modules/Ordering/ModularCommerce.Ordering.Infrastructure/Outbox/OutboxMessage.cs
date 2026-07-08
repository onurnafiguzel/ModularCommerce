namespace ModularCommerce.Ordering.Infrastructure.Outbox;

/// <summary>
/// Outbox satırı — anemik persistence kaydı (Entity DEĞİL: domain event taşımaz,
/// davranışı yoktur). Sipariş ile AYNI transaction'da yazılır (atomiklik, NFR-5.2);
/// OutboxDispatcher bekleyenleri broker'a taşır ve ProcessedOnUtc ile işaretler.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Type { get; init; } = null!;
    public string Content { get; init; } = null!;
    public DateTime OccurredOnUtc { get; init; }
    public DateTime? ProcessedOnUtc { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
}
