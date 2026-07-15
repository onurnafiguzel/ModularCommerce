namespace ModularCommerce.Catalog.Infrastructure.Outbox;

/// <summary>
/// Outbox satırı — anemik persistence kaydı (Entity DEĞİL: domain event taşımaz, davranışı yoktur).
/// Ürün mutasyonuyla AYNI transaction'da yazılır (atomiklik); CatalogOutboxDispatcher bekleyenleri
/// broker'a taşır ve ProcessedOnUtc ile işaretler. (Ordering.Outbox deseninin Catalog-lokal kopyası.)
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
