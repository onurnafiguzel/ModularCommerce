using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Ordering.Domain.Orders;

/// <summary>
/// Sipariş aggregate'i. Yaşam döngüsü (FR-5.1) tek geçiş tablosuyla yönetilir;
/// geçersiz geçişler domain seviyesinde reddedilir (FR-5.2). Her geçiş hem
/// StatusHistory'ye (kalıcı iz, NFR-5.3) hem domain event'ine yazılır
/// (dispatch W7 outbox). Satırlar ürün adı/fiyat SNAPSHOT'ı taşır (FR-5.3).
/// </summary>
public sealed class Order : Entity
{
    public const int IdempotencyKeyMaxLength = 64;

    /// <summary>
    /// FR-5.1 geçiş tablosu — makinenin TEK kaynağı. W8/9 akışları (ödeme,
    /// TTL süpürücü) yalnız ilgili metodu çağırır, tabloya satır eklemez.
    /// </summary>
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Created] = [OrderStatus.StockReserved, OrderStatus.Cancelled],
        [OrderStatus.StockReserved] = [OrderStatus.PaymentPending, OrderStatus.Cancelled, OrderStatus.Expired],
        [OrderStatus.PaymentPending] = [OrderStatus.Paid, OrderStatus.Cancelled, OrderStatus.Expired],
        [OrderStatus.Paid] = [OrderStatus.Shipped],
        [OrderStatus.Shipped] = [],
        [OrderStatus.Cancelled] = [],
        [OrderStatus.Expired] = [],
    };

    private readonly List<OrderLine> _lines = [];
    private readonly List<OrderStatusChange> _statusHistory = [];

    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyList<OrderLine> Lines => _lines;
    public IReadOnlyList<OrderStatusChange> StatusHistory => _statusHistory;

    /// <summary>Türetilmiş — kolon olarak tutulmaz (snapshot satırlardan hesaplanır).</summary>
    public decimal TotalAmount => _lines.Sum(l => l.UnitPrice * l.Quantity);

    /// <summary>Create tek para birimini garanti eder (CurrencyMismatch).</summary>
    public string Currency => _lines[0].Currency;
    private Order()
    {
    }

    private Order(
        Guid customerId, 
        string idempotencyKey, 
        IEnumerable<OrderLine> lines)
    {
        var utcNow = DateTime.UtcNow;

        CustomerId = customerId;
        Status = OrderStatus.Created;
        IdempotencyKey = idempotencyKey;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
        _lines.AddRange(lines);
    }

    public static Result<Order> Create(
        Guid customerId,
        string idempotencyKey,
        IReadOnlyList<OrderLineDraft> lines,
        string triggeredBy)
    {
        if (customerId == Guid.Empty)
        {
            return Result.Failure<Order>(OrderErrors.InvalidCustomerId);
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > IdempotencyKeyMaxLength)
        {
            return Result.Failure<Order>(OrderErrors.InvalidIdempotencyKey);
        }

        if (lines.Count == 0)
        {
            return Result.Failure<Order>(OrderErrors.NoLines);
        }

        if (lines.Any(l =>
            l.ProductId == Guid.Empty
            || string.IsNullOrWhiteSpace(l.ProductName)
            || l.Quantity < 1
            || l.UnitPrice < 0
            || l.ReservationId == Guid.Empty))
        {
            return Result.Failure<Order>(OrderErrors.InvalidLine);
        }

        if (lines.Select(l => l.Currency).Distinct().Count() > 1)
        {
            return Result.Failure<Order>(OrderErrors.CurrencyMismatch);
        }

        var order = new Order(customerId, idempotencyKey, lines.Select(l => new OrderLine(l)));

        // Doğuş da izlenebilir bir geçiştir: ∅ → Created (NFR-5.3).
        order._statusHistory.Add(new OrderStatusChange(null, OrderStatus.Created, triggeredBy));
        order.Raise(new OrderCreated(order.Id, customerId, order.CreatedAtUtc));

        return Result.Success(order);
    }

    public Result MarkStockReserved(string triggeredBy) => TransitionTo(OrderStatus.StockReserved, triggeredBy);
    public Result MarkPaymentPending(string triggeredBy) => TransitionTo(OrderStatus.PaymentPending, triggeredBy);

    public Result MarkPaid(string triggeredBy)
    {
        var result = TransitionTo(OrderStatus.Paid, triggeredBy);
        if (result.IsFailure)
        {
            return result;
        }

        Raise(new OrderPaid(Id, CustomerId, TotalAmount, Currency, UpdatedAtUtc));
        return Result.Success();
    }

    public Result MarkShipped(string triggeredBy) => TransitionTo(OrderStatus.Shipped, triggeredBy);
    public Result Cancel(string triggeredBy) => TransitionTo(OrderStatus.Cancelled, triggeredBy);
    public Result Expire(string triggeredBy) => TransitionTo(OrderStatus.Expired, triggeredBy);

    private Result TransitionTo(OrderStatus next, string triggeredBy)
    {
        if (!AllowedTransitions[Status].Contains(next))
        {
            return Result.Failure(OrderErrors.InvalidStateTransition(Status, next));
        }

        var previous = Status;
        Status = next;
        UpdatedAtUtc = DateTime.UtcNow;

        _statusHistory.Add(new OrderStatusChange(previous, next, triggeredBy));
        Raise(new OrderStatusChanged(Id, previous, next, UpdatedAtUtc));

        return Result.Success();
    }
}
