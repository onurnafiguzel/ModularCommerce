namespace ModularCommerce.Ordering.Contracts;

/// <summary>
/// TTL süpürücüsünün P2 penceresi sorgusu (kullanıcı kararı 1): süresi dolmuş bir rezervasyonun
/// canlı (Paid) bir siparişe bağlı olup olmadığını Ordering söyler — Inventory bunu bilemez
/// (modül sınırı). Bağlı olanlar Commit'e çevrilir (reconcile, oversell=0 KESİN); yetimler
/// Expire edilir. "Paid'e bağlı mı" POLİTİKASI Ordering'e aittir (SRP/Strategy).
/// </summary>
public interface IOrderReservationReconciler
{
    Task<IReadOnlyList<ReservationClassification>> ClassifyAsync(
        IReadOnlyCollection<Guid> reservationIds,
        CancellationToken cancellationToken);
}

/// <summary>Tek bir rezervasyonun sınıflandırması: canlı (Paid) siparişe bağlı mı?</summary>
public sealed record ReservationClassification(Guid ReservationId, bool IsBoundToLiveOrder);
