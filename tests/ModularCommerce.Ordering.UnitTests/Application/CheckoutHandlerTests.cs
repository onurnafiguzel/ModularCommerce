using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ModularCommerce.Cart.Contracts;
using ModularCommerce.Catalog.Contracts;
using ModularCommerce.Inventory.Contracts;
using ModularCommerce.Ordering.Application.Orders.Checkout;
using ModularCommerce.Ordering.Domain.Orders;
using ModularCommerce.Payment.Contracts;
using ModularCommerce.Shared.Kernel;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Ordering.UnitTests.Application;

/// <summary>
/// Checkout akışının tüm dallanmaları: replay, boş sepet + ikincil yarış, pasif ürün,
/// kısmi rezervasyon telafisi, SENKRON ödeme (başarı → Paid + commit; ret → release,
/// sipariş persist edilmez), duplicate-persist telafisi, persist-exception telafisi,
/// best-effort sepet temizliği ve commit.
/// </summary>
public class CheckoutHandlerTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly ICartService _cartService = Substitute.For<ICartService>();
    private readonly IProductReader _productReader = Substitute.For<IProductReader>();
    private readonly IStockReservationService _stockReservation = Substitute.For<IStockReservationService>();
    private readonly IPaymentService _paymentService = Substitute.For<IPaymentService>();
    private readonly CheckoutHandler _handler;

    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _productA = Guid.NewGuid();
    private readonly Guid _productB = Guid.NewGuid();

    public CheckoutHandlerTests()
    {
        _orders.AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _cartService.GetItemsAsync(_customerId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<CartLineDto>>([]));
        _cartService.ClearAsync(_customerId, Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _stockReservation.ReleaseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _stockReservation.CommitAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _paymentService.ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<ChargeRequest>();
                return Result.Success(new PaymentResultDto(
                    Guid.NewGuid(), request.Amount, request.Currency, "psp-tx", DateTime.UtcNow));
            });

        _handler = new CheckoutHandler(
            _orders, _cartService, _productReader, _stockReservation, _paymentService,
            new CheckoutCommandValidator(), NullLogger<CheckoutHandler>.Instance);
    }

    private CheckoutCommand Command(string key = "anahtar-1") => new(_customerId, key);

    private void SetupCart(params (Guid ProductId, int Quantity)[] lines)
        => _cartService.GetItemsAsync(_customerId, Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<CartLineDto>>(
                [.. lines.Select(l => new CartLineDto(l.ProductId, l.Quantity))]));

    private void SetupProducts(params ProductSnapshotDto[] products)
        => _productReader.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(products);

    private Guid SetupReservation(Guid productId, int quantity)
    {
        var reservationId = Guid.NewGuid();
        _stockReservation.ReserveAsync(productId, quantity, Arg.Any<CancellationToken>())
            .Returns(Result.Success(new StockReservationDto(
                reservationId, productId, quantity, DateTime.UtcNow.AddMinutes(5))));
        return reservationId;
    }

    private static Order ExistingOrder(Guid customerId, string key)
    {
        var order = Order.Create(
            customerId, key,
            [new OrderLineDraft(Guid.NewGuid(), "Mevcut", 10m, "TRY", 1, Guid.NewGuid())],
            "checkout").Value;
        order.MarkStockReserved("checkout");
        order.MarkPaymentPending("checkout");
        order.MarkPaid("checkout");
        return order;
    }

    [Fact(DisplayName = "Mutlu yol: rezervasyon + ödeme + Paid sipariş + satır başına commit + sepet temizliği")]
    public async Task Handle_HappyPath_CreatesPaidOrderAndCommitsStock()
    {
        SetupCart((_productA, 2), (_productB, 1));
        SetupProducts(
            new ProductSnapshotDto(_productA, "Ürün A", 100m, "TRY", true),
            new ProductSnapshotDto(_productB, "Ürün B", 50m, "TRY", true));
        var reservationA = SetupReservation(_productA, 2);
        var reservationB = SetupReservation(_productB, 1);

        var result = await _handler.HandleAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsExisting.Should().BeFalse();
        result.Value.Order.Status.Should().Be(nameof(OrderStatus.Paid));
        result.Value.Order.TotalAmount.Should().Be(250m);
        result.Value.Order.Lines.Should().HaveCount(2);
        result.Value.Order.History.Should().HaveCount(4,
            "∅→Created, Created→StockReserved, StockReserved→PaymentPending, PaymentPending→Paid");
        await _paymentService.Received(1).ChargeAsync(
            Arg.Is<ChargeRequest>(r => r.Amount == 250m && r.Currency == "TRY"),
            Arg.Any<CancellationToken>());
        await _stockReservation.Received(1).CommitAsync(reservationA, Arg.Any<CancellationToken>());
        await _stockReservation.Received(1).CommitAsync(reservationB, Arg.Any<CancellationToken>());
        await _cartService.Received(1).ClearAsync(_customerId, Arg.Any<CancellationToken>());
        await _stockReservation.DidNotReceive().ReleaseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Aynı key ikinci kez → mevcut sipariş replay (200, FR-5.4)")]
    public async Task Handle_WithExistingKey_ReturnsReplay()
    {
        var existing = ExistingOrder(_customerId, "anahtar-1");
        _orders.GetByIdempotencyKeyAsync(_customerId, "anahtar-1", Arg.Any<CancellationToken>())
            .Returns(existing);

        var result = await _handler.HandleAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsExisting.Should().BeTrue();
        result.Value.Order.Id.Should().Be(existing.Id);
        await _cartService.DidNotReceive().GetItemsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Boş sepet → 400 EmptyCart")]
    public async Task Handle_WithEmptyCart_ReturnsEmptyCart()
    {
        var result = await _handler.HandleAsync(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(OrderErrors.EmptyCart);
    }

    [Fact(DisplayName = "İkincil yarış: sepet boş AMA kazanan siparişi yazmış → replay, EmptyCart DEĞİL (FR-5.4)")]
    public async Task Handle_EmptyCartButWinnerExists_ReturnsReplay()
    {
        var winner = ExistingOrder(_customerId, "anahtar-1");
        // İlk kontrol: yok (henüz yazılmamış); sepet okunduğunda kazanan yazmış
        // ve SEPETİ TEMİZLEMİŞ; ikinci kontrol kazananı bulur.
        _orders.GetByIdempotencyKeyAsync(_customerId, "anahtar-1", Arg.Any<CancellationToken>())
            .Returns(null, winner);

        var result = await _handler.HandleAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsExisting.Should().BeTrue();
        result.Value.Order.Id.Should().Be(winner.Id);
    }

    [Fact(DisplayName = "Eksik/pasif ürün → 409 ProductUnavailable + HİÇ rezervasyon yapılmaz (FR-4.3)")]
    public async Task Handle_WithInactiveProduct_ReturnsProductUnavailable()
    {
        SetupCart((_productA, 1), (_productB, 1));
        SetupProducts(
            new ProductSnapshotDto(_productA, "Ürün A", 100m, "TRY", true),
            new ProductSnapshotDto(_productB, "Pasif", 50m, "TRY", false));

        var result = await _handler.HandleAsync(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Ordering.Checkout.ProductUnavailable");
        await _stockReservation.DidNotReceive()
            .ReserveAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "2. satır rezervasyonu düşerse 1.'in rezervasyonu release edilir, Inventory hatası aynen döner")]
    public async Task Handle_WhenSecondReservationFails_ReleasesFirst()
    {
        SetupCart((_productA, 2), (_productB, 1));
        SetupProducts(
            new ProductSnapshotDto(_productA, "Ürün A", 100m, "TRY", true),
            new ProductSnapshotDto(_productB, "Ürün B", 50m, "TRY", true));
        var firstReservationId = SetupReservation(_productA, 2);
        var insufficientStock = Error.Conflict("Inventory.InsufficientStock", "Yeterli stok yok.");
        _stockReservation.ReserveAsync(_productB, 1, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<StockReservationDto>(insufficientStock));

        var result = await _handler.HandleAsync(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(insufficientStock, "Inventory hatası sarmalanmadan iletilir");
        await _stockReservation.Received(1)
            .ReleaseAsync(firstReservationId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Persist duplicate (yarış kaybı): kendi rezervasyonları release edilir, KAZANANIN siparişi döner")]
    public async Task Handle_WhenPersistHitsDuplicate_ReleasesAndReturnsWinner()
    {
        SetupCart((_productA, 1));
        SetupProducts(new ProductSnapshotDto(_productA, "Ürün A", 100m, "TRY", true));
        var myReservationId = SetupReservation(_productA, 1);
        _orders.AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(OrderErrors.DuplicateIdempotencyKey));

        var winner = ExistingOrder(_customerId, "anahtar-1");
        _orders.GetByIdempotencyKeyAsync(_customerId, "anahtar-1", Arg.Any<CancellationToken>())
            .Returns(null, winner); // ön kontrol: yok; yarış sonrası: kazanan

        var result = await _handler.HandleAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsExisting.Should().BeTrue();
        result.Value.Order.Id.Should().Be(winner.Id);
        await _stockReservation.Received(1)
            .ReleaseAsync(myReservationId, Arg.Any<CancellationToken>());
        await _stockReservation.DidNotReceive()
            .CommitAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Persist beklenmedik exception fırlatırsa rezervasyonlar release edilir ve exception yükselir (500)")]
    public async Task Handle_WhenPersistThrows_ReleasesAndRethrows()
    {
        SetupCart((_productA, 1));
        SetupProducts(new ProductSnapshotDto(_productA, "Ürün A", 100m, "TRY", true));
        var myReservationId = SetupReservation(_productA, 1);
        _orders.AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns<Result>(_ => throw new InvalidOperationException("db koptu"));

        var act = () => _handler.HandleAsync(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _stockReservation.Received(1)
            .ReleaseAsync(myReservationId, Arg.Any<CancellationToken>());
        await _stockReservation.DidNotReceive()
            .CommitAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "Ödeme başarısız (declined/timeout/breaker): rezervasyonlar release, sipariş HİÇ persist edilmez, commit yok")]
    [InlineData("Payment.Declined")]
    [InlineData("Payment.Timeout")]
    [InlineData("Payment.PspUnavailable")]
    [InlineData("Payment.InFlight")]
    public async Task Handle_WhenChargeFails_ReleasesAndNeverPersists(string errorCode)
    {
        SetupCart((_productA, 2));
        SetupProducts(new ProductSnapshotDto(_productA, "Ürün A", 100m, "TRY", true));
        var myReservationId = SetupReservation(_productA, 2);
        var paymentError = Error.Conflict(errorCode, "Ödeme başarısız.");
        _paymentService.ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<PaymentResultDto>(paymentError));

        var result = await _handler.HandleAsync(Command(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(paymentError, "Payment hatası sarmalanmadan iletilir");
        await _stockReservation.Received(1)
            .ReleaseAsync(myReservationId, Arg.Any<CancellationToken>());
        await _orders.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _stockReservation.DidNotReceive()
            .CommitAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _cartService.DidNotReceive().ClearAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Ödeme, siparişin toplam tutarı ve checkout key'i ile istenir (FR-6.2 kapsam köprüsü)")]
    public async Task Handle_PassesOrderTotalsAndKeyToPayment()
    {
        SetupCart((_productA, 3));
        SetupProducts(new ProductSnapshotDto(_productA, "Ürün A", 40m, "TRY", true));
        SetupReservation(_productA, 3);

        await _handler.HandleAsync(Command(key: "kanit-77"), CancellationToken.None);

        await _paymentService.Received(1).ChargeAsync(
            Arg.Is<ChargeRequest>(r =>
                r.CustomerId == _customerId
                && r.IdempotencyKey == "kanit-77"
                && r.Amount == 120m
                && r.Currency == "TRY"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Commit başarısız olsa da checkout başarılıdır (para alındı; Warning izi W9 süpürücüsüne)")]
    public async Task Handle_WhenCommitFails_StillSucceeds()
    {
        SetupCart((_productA, 1));
        SetupProducts(new ProductSnapshotDto(_productA, "Ürün A", 100m, "TRY", true));
        SetupReservation(_productA, 1);
        _stockReservation.CommitAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Conflict("Inventory.ConcurrencyConflict", "çakışma")));

        var result = await _handler.HandleAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Order.Status.Should().Be(nameof(OrderStatus.Paid));
        await _stockReservation.DidNotReceive()
            .ReleaseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sepet temizliği başarısız olsa da checkout başarılıdır (sepet AP, best-effort)")]
    public async Task Handle_WhenCartClearFails_StillSucceeds()
    {
        SetupCart((_productA, 1));
        SetupProducts(new ProductSnapshotDto(_productA, "Ürün A", 100m, "TRY", true));
        SetupReservation(_productA, 1);
        _cartService.ClearAsync(_customerId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(Error.Failure("Cart.StorageUnavailable", "Redis yok")));

        var result = await _handler.HandleAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsExisting.Should().BeFalse();
    }

    [Fact(DisplayName = "Boş Idempotency-Key istek-şekli doğrulamasına takılır (400)")]
    public async Task Handle_WithMissingKey_ReturnsValidationError()
    {
        var result = await _handler.HandleAsync(Command(key: ""), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Ordering.Checkout.InvalidCommand");
    }
}
