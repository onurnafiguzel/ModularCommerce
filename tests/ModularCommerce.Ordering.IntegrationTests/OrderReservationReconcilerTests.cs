using FluentAssertions;
using ModularCommerce.Ordering.Domain.Orders;
using ModularCommerce.Ordering.Infrastructure.ContractAdapters;
using ModularCommerce.Ordering.IntegrationTests.Fixtures;
using Xunit;

namespace ModularCommerce.Ordering.IntegrationTests;

/// <summary>
/// TTL süpürücüsünün P2 sorgusunun Ordering tarafı (gerçek Postgres): yalnız Paid siparişin
/// satırındaki rezervasyon "canlı"dır. Cancelled/Expired sipariş satırları ve bilinmeyen id'ler
/// "bağlı değil" döner — süpürücü onları güvenle expire edebilir. Owned collection SelectMany
/// çevirisini de doğrular.
/// </summary>
[Collection("OrderingPostgres")]
public sealed class OrderReservationReconcilerTests(PostgresContainerFixture fixture)
{
    private static Order NewOrder(Guid reservationId, string key)
        => Order.Create(
            Guid.NewGuid(), key,
            [new OrderLineDraft(Guid.NewGuid(), "Ürün", 100m, "TRY", 1, reservationId)],
            "checkout").Value;

    [Fact(DisplayName = "Paid siparişe bağlı rezervasyon → IsBoundToLiveOrder=true; Cancelled/bilinmeyen → false")]
    public async Task Classify_OnlyPaidOrderReservations_AreBound()
    {
        var paidReservationId = Guid.NewGuid();
        var cancelledReservationId = Guid.NewGuid();
        var unknownReservationId = Guid.NewGuid();

        await using (var context = fixture.CreateContext())
        {
            var paid = NewOrder(paidReservationId, $"paid-{Guid.NewGuid():N}");
            paid.MarkStockReserved("t"); paid.MarkPaymentPending("t"); paid.MarkPaid("t");

            var cancelled = NewOrder(cancelledReservationId, $"cancel-{Guid.NewGuid():N}");
            cancelled.MarkStockReserved("t"); cancelled.MarkPaymentPending("t");
            cancelled.MarkPaid("t"); cancelled.Cancel("t");

            context.Orders.AddRange(paid, cancelled);
            await context.SaveChangesAsync();
        }

        await using (var context = fixture.CreateContext())
        {
            var reconciler = new OrderReservationReconciler(context);

            var result = await reconciler.ClassifyAsync(
                [paidReservationId, cancelledReservationId, unknownReservationId], CancellationToken.None);

            result.Should().HaveCount(3);
            result.Single(r => r.ReservationId == paidReservationId).IsBoundToLiveOrder
                .Should().BeTrue("yalnız Paid sipariş canlıdır");
            result.Single(r => r.ReservationId == cancelledReservationId).IsBoundToLiveOrder
                .Should().BeFalse("iptal edilmiş sipariş canlı değildir");
            result.Single(r => r.ReservationId == unknownReservationId).IsBoundToLiveOrder
                .Should().BeFalse("hiçbir siparişe bağlı olmayan yetim");
        }
    }
}
