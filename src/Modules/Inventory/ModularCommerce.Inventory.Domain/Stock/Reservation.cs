using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Domain.Stock;

public sealed class Reservation : Entity
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    public Guid StockItemId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    private Reservation()
    {
    }

    private Reservation(Guid stockItemId, Guid productId, int quantity)
    {
        var utcNow = DateTime.UtcNow;

        StockItemId = stockItemId;
        ProductId = productId;
        Quantity = quantity;
        Status = ReservationStatus.Active;
        CreatedAtUtc = utcNow;
        ExpiresAtUtc = utcNow.Add(DefaultTtl);
    }

    /// <summary>Yalnızca StockItem aggregate'i çağırır; invariant'lar orada doğrulanmıştır.</summary>
    internal static Reservation CreateFor(StockItem stockItem, int quantity)
        => new(stockItem.Id, stockItem.ProductId, quantity);

    /// <summary>Yalnızca StockItem.Release çağırır (CreateFor simetrisi).</summary>
    internal void MarkReleased() => Status = ReservationStatus.Released;
    internal void MarkCommitted() => Status = ReservationStatus.Committed;

    /// <summary>Yalnızca StockItem.Expire çağırır (TTL süpürücüsü sahipsiz rezervasyonda).</summary>
    internal void MarkExpired() => Status = ReservationStatus.Expired;

    /// <summary>Yalnızca StockItem.Return çağırır (sipariş iptalinde commit edilmiş stok iadesi).</summary>
    internal void MarkReturned() => Status = ReservationStatus.Returned;
}
