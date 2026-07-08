using FluentAssertions;
using ModularCommerce.Ordering.Domain.Orders;
using ModularCommerce.Shared.Kernel;
using Xunit;

namespace ModularCommerce.Ordering.UnitTests.Domain;

/// <summary>
/// FR-5.2 kanıtı: TAM geçiş matrisi — 7 durum × 6 geçiş metodu.
/// İzinli hücreler başarır, kalan HER hücre InvalidStateTransition döner.
/// Tablo değişirse bu test tabloyla birlikte değişmek zorundadır (bilinçli çift kayıt).
/// </summary>
public class OrderStateMachineTests
{
    private static Order CreateOrder()
        => Order.Create(
            Guid.NewGuid(),
            "test-key",
            [new OrderLineDraft(Guid.NewGuid(), "Ürün", 100m, "TRY", 1, Guid.NewGuid())],
            "test").Value;

    /// <summary>Siparişi hedef duruma izinli yoldan taşır.</summary>
    private static Order OrderInStatus(OrderStatus status)
    {
        var order = CreateOrder();

        var path = status switch
        {
            OrderStatus.Created => Array.Empty<Action<Order>>(),
            OrderStatus.StockReserved => [o => o.MarkStockReserved("test")],
            OrderStatus.PaymentPending =>
                [o => o.MarkStockReserved("test"), o => o.MarkPaymentPending("test")],
            OrderStatus.Paid =>
                [o => o.MarkStockReserved("test"), o => o.MarkPaymentPending("test"), o => o.MarkPaid("test")],
            OrderStatus.Shipped =>
                [o => o.MarkStockReserved("test"), o => o.MarkPaymentPending("test"), o => o.MarkPaid("test"), o => o.MarkShipped("test")],
            OrderStatus.Cancelled => [o => o.Cancel("test")],
            OrderStatus.Expired =>
                [o => o.MarkStockReserved("test"), o => o.Expire("test")],
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

        foreach (var step in path)
        {
            step(order);
        }

        order.Status.Should().Be(status, "test kurulumunun kendisi geçerli yoldan ilerlemeli");
        return order;
    }

    private static Result Apply(Order order, OrderStatus target) => target switch
    {
        OrderStatus.StockReserved => order.MarkStockReserved("test"),
        OrderStatus.PaymentPending => order.MarkPaymentPending("test"),
        OrderStatus.Paid => order.MarkPaid("test"),
        OrderStatus.Shipped => order.MarkShipped("test"),
        OrderStatus.Cancelled => order.Cancel("test"),
        OrderStatus.Expired => order.Expire("test"),
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };

    /// <summary>FR-5.1 tablosunun izinli hücreleri.</summary>
    private static readonly HashSet<(OrderStatus From, OrderStatus To)> Allowed =
    [
        (OrderStatus.Created, OrderStatus.StockReserved),
        (OrderStatus.Created, OrderStatus.Cancelled),
        (OrderStatus.StockReserved, OrderStatus.PaymentPending),
        (OrderStatus.StockReserved, OrderStatus.Cancelled),
        (OrderStatus.StockReserved, OrderStatus.Expired),
        (OrderStatus.PaymentPending, OrderStatus.Paid),
        (OrderStatus.PaymentPending, OrderStatus.Cancelled),
        (OrderStatus.PaymentPending, OrderStatus.Expired),
        (OrderStatus.Paid, OrderStatus.Shipped),
    ];

    public static TheoryData<OrderStatus, OrderStatus> AllCells()
    {
        var data = new TheoryData<OrderStatus, OrderStatus>();
        var targets = new[]
        {
            OrderStatus.StockReserved, OrderStatus.PaymentPending, OrderStatus.Paid,
            OrderStatus.Shipped, OrderStatus.Cancelled, OrderStatus.Expired,
        };

        foreach (var from in Enum.GetValues<OrderStatus>())
        {
            foreach (var to in targets)
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Theory(DisplayName = "Geçiş matrisi: izinli hücre başarır, diğer HER hücre InvalidStateTransition (FR-5.2)")]
    [MemberData(nameof(AllCells))]
    public void TransitionMatrix_EnforcesFr51Table(OrderStatus from, OrderStatus to)
    {
        var order = OrderInStatus(from);

        var result = Apply(order, to);

        if (Allowed.Contains((from, to)))
        {
            result.IsSuccess.Should().BeTrue($"{from} → {to} FR-5.1 tablosunda izinli");
            order.Status.Should().Be(to);
        }
        else
        {
            result.IsFailure.Should().BeTrue($"{from} → {to} FR-5.1 tablosunda YOK");
            result.Error.Code.Should().Be("Ordering.Order.InvalidStateTransition");
            order.Status.Should().Be(from, "başarısız geçiş durumu değiştirmemeli");
        }
    }

    [Fact(DisplayName = "Her geçiş history'ye iz bırakır ve event raise eder (NFR-5.3)")]
    public void Transitions_AppendHistoryAndRaiseEvents()
    {
        var order = CreateOrder();

        order.MarkStockReserved("checkout");

        order.StatusHistory.Should().HaveCount(2);
        order.StatusHistory[0].FromStatus.Should().BeNull("doğuş ∅ → Created olarak izlenir");
        order.StatusHistory[0].ToStatus.Should().Be(OrderStatus.Created);
        order.StatusHistory[1].FromStatus.Should().Be(OrderStatus.Created);
        order.StatusHistory[1].ToStatus.Should().Be(OrderStatus.StockReserved);
        order.StatusHistory[1].TriggeredBy.Should().Be("checkout");

        order.DomainEvents.Should().SatisfyRespectively(
            first => first.Should().BeOfType<OrderCreated>(),
            second => second.Should().BeOfType<OrderStatusChanged>()
                .Which.ToStatus.Should().Be(OrderStatus.StockReserved));
    }

    [Fact(DisplayName = "MarkPaid genel OrderStatusChanged'e EK OLARAK niyetli OrderPaid event'i raise eder (W8 outbox işareti)")]
    public void MarkPaid_RaisesOrderPaid_InAdditionToStatusChanged()
    {
        var order = OrderInStatus(OrderStatus.PaymentPending);

        order.MarkPaid("checkout").IsSuccess.Should().BeTrue();

        // Son iki event: PaymentPending→Paid geçişi (history/iz) + niyetli OrderPaid (dışa duyuru).
        order.DomainEvents.Should().ContainSingle(e => e is OrderPaid)
            .Which.As<OrderPaid>().Should().Match<OrderPaid>(e =>
                e.OrderId == order.Id
                && e.CustomerId == order.CustomerId
                && e.TotalAmount == order.TotalAmount
                && e.Currency == order.Currency);

        order.DomainEvents.OfType<OrderStatusChanged>()
            .Should().Contain(e => e.ToStatus == OrderStatus.Paid,
                "geçiş izi (history) korunur, OrderPaid onun yerine geçmez");
    }

    [Fact(DisplayName = "Başarısız MarkPaid OrderPaid raise ETMEZ (yanlış duyuru olmaz)")]
    public void MarkPaid_WhenInvalid_DoesNotRaiseOrderPaid()
    {
        var order = OrderInStatus(OrderStatus.StockReserved); // PaymentPending değil → Paid geçişi geçersiz

        order.MarkPaid("checkout").IsFailure.Should().BeTrue();

        order.DomainEvents.Should().NotContain(e => e is OrderPaid);
    }
}
