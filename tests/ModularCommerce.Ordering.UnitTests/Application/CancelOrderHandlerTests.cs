using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModularCommerce.Inventory.Contracts;
using ModularCommerce.Ordering.Application.Orders.Cancel;
using ModularCommerce.Ordering.Domain.Orders;
using ModularCommerce.Payment.Contracts;
using ModularCommerce.Shared.Kernel;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Ordering.UnitTests.Application;

/// <summary>
/// Kapsamlı iptal telafi orkestrasyonu (W9): sahiplik, geçiş, stok iadesi (best-effort),
/// refund (kritik — başarısızsa geri sarım), SaveChanges sırası.
/// </summary>
public class CancelOrderHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IStockReservationService _stockReservation = Substitute.For<IStockReservationService>();
    private readonly IPaymentService _paymentService = Substitute.For<IPaymentService>();
    private readonly CancelOrderHandler _handler;

    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly Guid _reservationId = Guid.NewGuid();

    public CancelOrderHandlerTests()
    {
        _stockReservation.ReturnAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _paymentService.RefundAsync(Arg.Any<RefundRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new RefundResultDto(Guid.NewGuid(), "refund-tx", DateTime.UtcNow)));
        _orders.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());

        _handler = new CancelOrderHandler(
            _orders, _stockReservation, _paymentService, NullLogger<CancelOrderHandler>.Instance);
    }

    /// <summary>Paid duruma kadar taşınmış gerçek bir sipariş (rezervasyon id'li tek satır).</summary>
    private Order PaidOrder()
    {
        var order = Order.Create(
            _customerId, "iptal-key",
            [new OrderLineDraft(_productId, "Ürün", 100m, "TRY", 2, _reservationId)],
            "checkout").Value;
        order.MarkStockReserved("checkout");
        order.MarkPaymentPending("checkout");
        order.MarkPaid("checkout");
        return order;
    }

    [Fact(DisplayName = "Mutlu yol: sahiplik OK → Cancelled + her satır iade + refund + SaveChanges")]
    public async Task Handle_HappyPath_ReturnsStockRefundsAndSaves()
    {
        var order = PaidOrder();
        _orders.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.HandleAsync(order.Id, _customerId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        await _stockReservation.Received(1).ReturnAsync(_reservationId, Arg.Any<CancellationToken>());
        await _paymentService.Received(1).RefundAsync(
            Arg.Is<RefundRequest>(r =>
                r.CustomerId == _customerId && r.IdempotencyKey == "iptal-key" && r.Amount == 200m),
            Arg.Any<CancellationToken>());
        await _orders.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sahip değilse 404 NotFound (varlık sızdırılmaz), telafi yapılmaz")]
    public async Task Handle_NotOwner_ReturnsNotFound()
    {
        var order = PaidOrder();
        _orders.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.HandleAsync(order.Id, Guid.NewGuid(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        await _stockReservation.DidNotReceive().ReturnAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _paymentService.DidNotReceive().RefundAsync(Arg.Any<RefundRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Bilinmeyen sipariş → 404 NotFound")]
    public async Task Handle_UnknownOrder_ReturnsNotFound()
    {
        _orders.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Order?)null);

        var result = await _handler.HandleAsync(Guid.NewGuid(), _customerId, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact(DisplayName = "Shipped sipariş iptal edilemez → InvalidStateTransition, telafi yapılmaz")]
    public async Task Handle_ShippedOrder_ReturnsInvalidStateTransition()
    {
        var order = PaidOrder();
        order.MarkShipped("ship"); // Paid → Shipped; artık iptal edilemez
        _orders.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await _handler.HandleAsync(order.Id, _customerId, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ordering.Order.InvalidStateTransition");
        await _paymentService.DidNotReceive().RefundAsync(Arg.Any<RefundRequest>(), Arg.Any<CancellationToken>());
        await _orders.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Refund başarısızsa iptal GERİ SARILIR: SaveChanges çağrılmaz (sipariş Paid kalır)")]
    public async Task Handle_WhenRefundFails_DoesNotPersistCancellation()
    {
        var order = PaidOrder();
        _orders.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        var refundError = Error.Conflict("Payment.NotRefundable", "iade edilemez");
        _paymentService.RefundAsync(Arg.Any<RefundRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<RefundResultDto>(refundError));

        var result = await _handler.HandleAsync(order.Id, _customerId, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(refundError);
        await _orders.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Stok iadesi başarısız olsa da iptal devam eder (best-effort; refund + SaveChanges yine olur)")]
    public async Task Handle_WhenReturnFails_StillProceeds()
    {
        var order = PaidOrder();
        _orders.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _stockReservation.ReturnAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Conflict("Inventory.Reservation.NotReturnable", "iade edilemez")));

        var result = await _handler.HandleAsync(order.Id, _customerId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue("stok iadesi best-effort; iptal parasal telafiyle tamamlanır");
        await _paymentService.Received(1).RefundAsync(Arg.Any<RefundRequest>(), Arg.Any<CancellationToken>());
        await _orders.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
