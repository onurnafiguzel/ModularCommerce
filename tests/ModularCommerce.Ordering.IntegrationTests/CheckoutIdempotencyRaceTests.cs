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
using ModularCommerce.Shared.Kernel;
using NSubstitute;
using Xunit;

namespace ModularCommerce.Ordering.IntegrationTests;

/// <summary>
/// FR-5.4'ün gerçek hakemi DB unique index'idir — bu testte GERÇEK olan tek parça
/// OrderRepository + Postgres'tir (sözleşmeler NSubstitute; full-stack fixture
/// bilinçli kurulmadı — bkz. hafta-6-notlar D19). Kanıt: iki paralel aynı-key
/// checkout → tek sipariş satırı + kaybedenin rezervasyonları release edilmiş.
/// </summary>
[Collection("OrderingPostgres")]
public sealed class CheckoutIdempotencyRaceTests(PostgresContainerFixture fixture)
{
    private static readonly Guid ProductId = Guid.NewGuid();

    private CheckoutHandler CreateHandler(
        OrderingDbContext context,
        ConcurrentBag<Guid> reservedIds,
        ConcurrentBag<Guid> releasedIds)
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

        return new CheckoutHandler(
            new OrderRepository(context),
            cartService,
            productReader,
            stockReservation,
            new CheckoutCommandValidator(),
            NullLogger<CheckoutHandler>.Instance);
    }

    [Fact(DisplayName = "İki paralel aynı-key checkout: DB'de TEK sipariş, iki yanıt aynı id, kaybedenin rezervasyonu release edilmiş (FR-5.4)")]
    public async Task ParallelSameKeyCheckouts_ProduceSingleOrder_AndLoserReleases()
    {
        var customerId = Guid.NewGuid();
        var key = $"yaris-{Guid.NewGuid():N}";
        var reservedIds = new ConcurrentBag<Guid>();
        var releasedIds = new ConcurrentBag<Guid>();

        // İki ayrı DbContext = iki ayrı "istek" scope'u; start-gate ile aynı anda bırakılır.
        var gate = new TaskCompletionSource();

        async Task<Result<CheckoutResponse>> RunCheckout()
        {
            await using var context = fixture.CreateContext();
            var handler = CreateHandler(context, reservedIds, releasedIds);
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

        // DB'de tek satır (unique index hakemliği).
        await using (var verify = fixture.CreateContext())
        {
            (await verify.Orders.CountAsync(
                o => o.CustomerId == customerId && o.IdempotencyKey == key)).Should().Be(1);
        }

        // Stok sızıntısı yok: kazananın rezervasyonları siparişte, kaybedeninkiler release edilmiş.
        var winnerId = results.First().Value.Order.Id;
        await using (var verify = fixture.CreateContext())
        {
            var winnerReservations = (await verify.Orders.SingleAsync(o => o.Id == winnerId))
                .Lines.Select(l => l.ReservationId).ToHashSet();

            var loserReservations = reservedIds.Except(winnerReservations).ToList();
            releasedIds.Should().BeEquivalentTo(
                loserReservations,
                "kaybedenin TÜM rezervasyonları (ve yalnız onlar) geri bırakılmalı");
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
