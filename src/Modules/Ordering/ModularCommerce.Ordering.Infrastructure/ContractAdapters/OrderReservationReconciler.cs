using Microsoft.EntityFrameworkCore;
using ModularCommerce.Ordering.Contracts;
using ModularCommerce.Ordering.Domain.Orders;
using ModularCommerce.Ordering.Infrastructure.Persistence;

namespace ModularCommerce.Ordering.Infrastructure.ContractAdapters;

/// <summary>
/// TTL süpürücüsünün P2 sorgusunun Ordering tarafı (ACL adapter): verilen rezervasyon
/// id'lerinden hangileri canlı (Paid) bir siparişin satırına bağlı. Yalnız Paid siparişler
/// sayılır — Cancelled/Expired sipariş satırlarının rezervasyonu artık "canlı" değildir,
/// süpürücü onları güvenle expire edebilir.
/// </summary>
public sealed class OrderReservationReconciler(OrderingDbContext context) : IOrderReservationReconciler
{
    public async Task<IReadOnlyList<ReservationClassification>> ClassifyAsync(
        IReadOnlyCollection<Guid> reservationIds,
        CancellationToken cancellationToken)
    {
        var boundIds = await context.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.Paid)
            .SelectMany(o => o.Lines)
            .Where(line => reservationIds.Contains(line.ReservationId))
            .Select(line => line.ReservationId)
            .ToListAsync(cancellationToken);

        var bound = boundIds.ToHashSet();

        return reservationIds
            .Select(id => new ReservationClassification(id, bound.Contains(id)))
            .ToList();
    }
}
