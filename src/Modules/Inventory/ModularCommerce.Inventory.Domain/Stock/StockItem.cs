using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Inventory.Domain.Stock;

public sealed class StockItem : Entity
{
    public Guid ProductId { get; private set; }
    public int OnHand { get; private set; }
    public int Reserved { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Kullanılabilir stok; map'lenmez, her zaman türetilir.</summary>
    public int Available => OnHand - Reserved;

    /// <summary>EF Core materialization için; uygulama kodu asla çağırmaz.</summary>
    private StockItem()
    {
    }

    private StockItem(Guid productId, int onHand)
    {
        var utcNow = DateTime.UtcNow;

        ProductId = productId;
        OnHand = onHand;
        Reserved = 0;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    public static Result<StockItem> Create(Guid productId, int onHand)
    {
        if (productId == Guid.Empty)
        {
            return Result.Failure<StockItem>(InventoryErrors.InvalidProductId);
        }

        if (onHand < 0)
        {
            return Result.Failure<StockItem>(InventoryErrors.InvalidOnHand);
        }

        return Result.Success(new StockItem(productId, onHand));
    }

    public Result<Reservation> Reserve(int quantity)
    {
        if (quantity <= 0)
        {
            return Result.Failure<Reservation>(InventoryErrors.InvalidQuantity);
        }

        if (quantity > Available)
        {
            return Result.Failure<Reservation>(InventoryErrors.InsufficientStock);
        }

        Reserved += quantity;
        UpdatedAtUtc = DateTime.UtcNow;

        var reservation = Reservation.CreateFor(this, quantity);
        Raise(new StockReserved(ProductId, reservation.Id, quantity, UpdatedAtUtc));

        if (Available == 0)
        {
            Raise(new ProductSoldOut(ProductId, UpdatedAtUtc));
        }

        return Result.Success(reservation);
    }

    public Result Release(Reservation reservation)
    {
        if (reservation.StockItemId != Id)
        {
            return Result.Failure(InventoryErrors.ReservationMismatch);
        }

        if (reservation.Status == ReservationStatus.Released)
        {
            return Result.Success();
        }

        if (reservation.Status != ReservationStatus.Active)
        {
            return Result.Failure(InventoryErrors.ReservationNotReleasable);
        }

        if (Reserved - reservation.Quantity < 0)
        {
            return Result.Failure(InventoryErrors.ReleaseInvariantViolated);
        }

        Reserved -= reservation.Quantity;
        UpdatedAtUtc = DateTime.UtcNow;
        reservation.MarkReleased();

        Raise(new StockReleased(ProductId, reservation.Id, reservation.Quantity, UpdatedAtUtc));

        return Result.Success();
    }
}
