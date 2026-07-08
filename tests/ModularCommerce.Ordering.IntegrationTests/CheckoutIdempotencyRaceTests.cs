using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ModularCommerce.Cart.Contracts;
using ModularCommerce.Catalog.Contracts;
using ModularCommerce.Inventory.Contracts;
using ModularCommerce.Ordering.Application.Orders.Checkout;
using ModularCommerce.Ordering.Domain.Orders;
using ModularCommerce.Ordering.Infrastructure.Persistence;
using ModularCommerce.Ordering.Infrastructure.Persistence.Repositories;
using ModularCommerce.Ordering.IntegrationTests.Fixtures;
using ModularCommerce.Payment.Contracts;
using ModularCommerce.Shared.Kernel;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Ordering.IntegrationTests;

/// <summary>
/// FR-5.4'ün gerçek hakemi DB unique index'idir — bu testte GERÇEK olan tek parça
/// OrderRepository + Postgres'tir (sözleşmeler NSubstitute; full-stack fixture
/// bilinçli kurulmadı — bkz. hafta-6-notlar D19). Kanıt: iki paralel aynı-key
/// checkout → tek sipariş satırı (DB'de Paid) + kaybedenin rezervasyonları release
/// edilmiş + commit YALNIZ kazananın satırları için çağrılmış. Ödemenin kendi
/// yarış hakemi Payment.IntegrationTests'te ayrıca kanıtlanır.
/// </summary>
[Collection("OrderingPostgres")]
public sealed class CheckoutIdempotencyRaceTests(PostgresContainerFixture fixture)
{
    private static readonly Guid ProductId = Guid.NewGuid();

    private CheckoutHandler CreateHandler(
        OrderingDbContext context,
        ConcurrentBag<Guid> reservedIds,
        ConcurrentBag<Guid> releasedIds,
        ConcurrentBag<Guid> committedIds)
    {
        var cartService = Substitute.For<ICartService>();
        cartService.GetItemsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<IReadOnlyList<CartLineDto>>([new CartLineDto(ProductId, 2)]));
        cartService.ClearAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var productReader = Substitute.For<IProductReader>();
        productReader.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([new ProductSnapshotDto(ProductId, "Yarış Ürünü", 100m, "TRY", true)]);

        var stockReservation = Substitute.For<IStockReservationService>();
        stockReservation.ReserveAsync(ProductId, 2, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var id = Guid.NewGuid();
                reservedIds.Add(id);
                return Result.Success(new StockReservationDto(id, ProductId, 2, DateTime.UtcNow.AddMinutes(5)));
            });
        stockReservation.ReleaseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                releasedIds.Add(call.Arg<Guid>());
                return Result.Success();
            });
        stockReservation.CommitAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                committedIds.Add(call.Arg<Guid>());
                return Result.Success();
            });

        // Ödeme sözleşme davranışı: her iki paralel istek de başarı görebilir (gerçekte
        // biri charge, diğeri replay olurdu — FR-6.2); sipariş tekilliğinin hakemi
        // yine Ordering index'idir.
        var paymentService = Substitute.For<IPaymentService>();
        paymentService.ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<ChargeRequest>();
                return Result.Success(new PaymentResultDto(
                    Guid.NewGuid(), request.Amount, request.Currency, "psp-tx", DateTime.UtcNow));
            });

        return new CheckoutHandler(
            new OrderRepository(context),
            cartService,
            productReader,
            stockReservation,
            paymentService,
            new CheckoutCommandValidator(),
            NullLogger<CheckoutHandler>.Instance);
    }

    [Fact(DisplayName = "İki paralel aynı-key checkout: DB'de TEK Paid sipariş, kaybeden release etmiş, commit YALNIZ kazananda (FR-5.4)")]
    public async Task ParallelSameKeyCheckouts_ProduceSingleOrder_AndLoserReleases()
    {
        var customerId = Guid.NewGuid();
        var key = $"yaris-{Guid.NewGuid():N}";
        var reservedIds = new ConcurrentBag<Guid>();
        var releasedIds = new ConcurrentBag<Guid>();
        var committedIds = new ConcurrentBag<Guid>();

        // İki ayrı DbContext = iki ayrı "istek" scope'u; start-gate ile aynı anda bırakılır.
        var gate = new TaskCompletionSource();

        async Task<Result<CheckoutResponse>> RunCheckout()
        {
            await using var context = fixture.CreateContext();
            var handler = CreateHandler(context, reservedIds, releasedIds, committedIds);
            await gate.Task;
            return await handler.HandleAsync(new CheckoutCommand(customerId, key), CancellationToken.None);
        }

        var first = RunCheckout();
        var second = RunCheckout();
        gate.SetResult();
        var results = await Task.WhenAll(first, second);

        // İki yanıt da başarılı ve AYNI siparişi gösteriyor.
        results.Should().OnlyContain(r => r.IsSuccess);
        results.Select(r => r.Value.Order.Id).Distinct().Should().HaveCount(1);
        results.Count(r => !r.Value.IsExisting).Should().Be(1, "yalnız kazanan 201 anlamı taşır");

        // DB'de tek satır (unique index hakemliği) ve sipariş Paid persist edilmiş.
        await using (var verify = fixture.CreateContext())
        {
            var stored = await verify.Orders.SingleAsync(
                o => o.CustomerId == customerId && o.IdempotencyKey == key);
            stored.Status.Should().Be(OrderStatus.Paid);
            stored.StatusHistory.Should().HaveCount(4,
                "∅→Created→StockReserved→PaymentPending→Paid izleri");
        }

        // Stok sızıntısı ve çift düşüş yok: kazananın rezervasyonları COMMIT edilmiş,
        // kaybedeninkiler RELEASE edilmiş — iki küme ayrık ve tam.
        var winnerId = results.First().Value.Order.Id;
        await using (var verify = fixture.CreateContext())
        {
            var winnerReservations = (await verify.Orders.SingleAsync(o => o.Id == winnerId))
                .Lines.Select(l => l.ReservationId).ToHashSet();

            var loserReservations = reservedIds.Except(winnerReservations).ToList();
            releasedIds.Should().BeEquivalentTo(
                loserReservations,
                "kaybedenin TÜM rezervasyonları (ve yalnız onlar) geri bırakılmalı");
            committedIds.Should().BeEquivalentTo(
                winnerReservations,
                "kalıcı düşüş yalnız kazananın satırları için yapılmalı");
        }
    }

    [Fact(DisplayName = "Repository: aynı (müşteri, key) ikinci kayıtta 23505 → DuplicateIdempotencyKey (yalnız ilgili constraint)")]
    public async Task AddAsync_OnDuplicateKey_ReturnsDuplicateError()
    {
        var customerId = Guid.NewGuid();
        var key = $"dup-{Guid.NewGuid():N}";

        static Order NewOrder(Guid customerId, string key)
            => Order.Create(
                customerId, key,
                [new OrderLineDraft(Guid.NewGuid(), "Ürün", 10m, "TRY", 1, Guid.NewGuid())],
                "test").Value;

        await using (var first = fixture.CreateContext())
        {
            (await new OrderRepository(first).AddAsync(NewOrder(customerId, key), CancellationToken.None))
                .IsSuccess.Should().BeTrue();
        }

        await using (var second = fixture.CreateContext())
        {
            var result = await new OrderRepository(second).AddAsync(NewOrder(customerId, key), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Should().Be(OrderErrors.DuplicateIdempotencyKey);
        }

        // Farklı müşteri AYNI key'i kullanabilir (kapsam müşteri-başına).
        await using (var other = fixture.CreateContext())
        {
            (await new OrderRepository(other).AddAsync(NewOrder(Guid.NewGuid(), key), CancellationToken.None))
                .IsSuccess.Should().BeTrue();
        }
    }
}
