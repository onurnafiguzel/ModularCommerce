using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ModularCommerce.Ordering.Domain.Orders;
using ModularCommerce.Ordering.Infrastructure.Outbox;
using ModularCommerce.Ordering.Infrastructure.Persistence;
using ModularCommerce.Ordering.IntegrationTests.Fixtures;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace ModularCommerce.Ordering.IntegrationTests;

/// <summary>
/// Outbox'ın gerçek Postgres'e karşı iki kritik garantisi: (1) atomiklik — sipariş ile
/// outbox satırı aynı transaction'da yazılır ya da hiç yazılmaz (NFR-5.2); (2) dispatcher
/// bekleyenleri publish edip işaretler, hata retry sayacını artırır.
/// Not: Content jsonb'dir; SQL LIKE desteklemez — testler outbox satırlarını belleğe alıp
/// (koleksiyon paylaşımlı DB'de tekil CustomerId'ye göre) filtreler.
/// </summary>
[Collection("OrderingPostgres")]
public sealed class OutboxIntegrationTests(PostgresContainerFixture fixture)
{
    private static Order PaidOrder(Guid customerId, string key)
    {
        var order = Order.Create(
            customerId, key,
            [new OrderLineDraft(Guid.NewGuid(), "Ürün", 100m, "TRY", 2, Guid.NewGuid())],
            "checkout").Value;
        order.MarkStockReserved("checkout");
        order.MarkPaymentPending("checkout");
        order.MarkPaid("checkout"); // OrderPaid domain event burada birikir
        return order;
    }

    /// <summary>Bu müşteriye ait outbox satırlarını belleğe alır (jsonb LIKE sorunundan kaçınır).</summary>
    private static async Task<List<OutboxMessage>> OutboxForCustomerAsync(
        OrderingDbContext context, Guid customerId)
    {
        var all = await context.OutboxMessages.AsNoTracking().ToListAsync();
        return all.Where(m => m.Content.Contains(customerId.ToString())).ToList();
    }

    [Fact(DisplayName = "MarkPaid sonrası SaveChanges: sipariş ile outbox satırı AYNI transaction'da yazılır (atomiklik)")]
    public async Task Checkout_WritesOrderPaidOutboxRow_Atomically()
    {
        var customerId = Guid.NewGuid();
        var key = $"outbox-{Guid.NewGuid():N}";

        await using (var context = fixture.CreateContextWithOutbox())
        {
            context.Orders.Add(PaidOrder(customerId, key));
            await context.SaveChangesAsync();
        }

        await using (var verify = fixture.CreateContext())
        {
            var order = await verify.Orders.SingleAsync(o => o.IdempotencyKey == key);
            var outbox = await OutboxForCustomerAsync(verify, customerId);

            outbox.Should().ContainSingle("yalnız OrderPaid dışa terfi eder (OrderCreated/StatusChanged değil)");
            outbox[0].Type.Should().Be("OrderPaid");
            outbox[0].ProcessedOnUtc.Should().BeNull("dispatcher henüz işlemedi");
            outbox[0].Content.Should().Contain(order.Id.ToString());
        }
    }

    [Fact(DisplayName = "SaveChanges patlarsa (duplicate key) NE sipariş NE outbox yazılır (atomiklik geri sarımı, NFR-5.2)")]
    public async Task WhenSaveChangesFails_NeitherOrderNorOutboxPersists()
    {
        var customerId = Guid.NewGuid();
        var key = $"atomik-{Guid.NewGuid():N}";

        // İlk sipariş başarıyla yazılır (1 outbox satırı üretir).
        await using (var seed = fixture.CreateContextWithOutbox())
        {
            seed.Orders.Add(PaidOrder(customerId, key));
            await seed.SaveChangesAsync();
        }

        // Aynı (müşteri, key) ikinci sipariş → unique index 23505 → SaveChanges patlar.
        await using (var conflict = fixture.CreateContextWithOutbox())
        {
            conflict.Orders.Add(PaidOrder(customerId, key));
            var act = async () => await conflict.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }

        // İkinci denemenin outbox satırı YAZILMAMIŞ olmalı: tam olarak 1 satır (ilk siparişin).
        await using (var verify = fixture.CreateContext())
        {
            (await OutboxForCustomerAsync(verify, customerId))
                .Should().ContainSingle("başarısız transaction outbox satırını da geri sarmalı");
        }
    }

    [Fact(DisplayName = "Dispatcher bekleyeni publish eder ve ProcessedOnUtc işaretler")]
    public async Task Dispatcher_PublishesAndMarks_PendingMessages()
    {
        var customerId = Guid.NewGuid();
        await using (var seed = fixture.CreateContextWithOutbox())
        {
            seed.Orders.Add(PaidOrder(customerId, $"dispatch-{Guid.NewGuid():N}"));
            await seed.SaveChangesAsync();
        }

        var publisher = Substitute.For<IPublishEndpoint>();
        var dispatcher = new OutboxDispatcher(Substitute.For<IServiceProvider>(), NullLogger<OutboxDispatcher>.Instance);

        await using (var context = fixture.CreateContext())
        {
            await dispatcher.ProcessBatchAsync(context, publisher, new OrderingIntegrationEventRegistry(), CancellationToken.None);
        }

        await publisher.Received().Publish(
            Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<CancellationToken>());

        await using (var verify = fixture.CreateContext())
        {
            (await OutboxForCustomerAsync(verify, customerId))
                .Should().OnlyContain(m => m.ProcessedOnUtc != null, "tüm bekleyenler işlendi");
        }
    }

    [Fact(DisplayName = "Publish hata verirse satır işaretlenmez, RetryCount artar (Error saklanır)")]
    public async Task Dispatcher_OnPublishError_IncrementsRetryCount()
    {
        var customerId = Guid.NewGuid();
        await using (var seed = fixture.CreateContextWithOutbox())
        {
            seed.Orders.Add(PaidOrder(customerId, $"retry-{Guid.NewGuid():N}"));
            await seed.SaveChangesAsync();
        }

        var publisher = Substitute.For<IPublishEndpoint>();
        publisher.Publish(Arg.Any<object>(), Arg.Any<Type>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("broker kapalı"));
        var dispatcher = new OutboxDispatcher(Substitute.For<IServiceProvider>(), NullLogger<OutboxDispatcher>.Instance);

        await using (var context = fixture.CreateContext())
        {
            await dispatcher.ProcessBatchAsync(context, publisher, new OrderingIntegrationEventRegistry(), CancellationToken.None);
        }

        await using (var verify = fixture.CreateContext())
        {
            var message = (await OutboxForCustomerAsync(verify, customerId)).Single();
            message.ProcessedOnUtc.Should().BeNull("publish başarısızsa mesaj bekliyor kalmalı");
            message.RetryCount.Should().Be(1);
            message.Error.Should().Contain("broker kapalı");
        }
    }
}
