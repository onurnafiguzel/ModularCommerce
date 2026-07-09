using Microsoft.Extensions.Logging;
using ModularCommerce.Inventory.Contracts;
using ModularCommerce.Ordering.Domain.Orders;
using ModularCommerce.Payment.Contracts;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Ordering.Application.Orders.Cancel;

/// <summary>
/// Kapsamlı sipariş iptali (W9): telafi orkestrasyonu. İş kuralları Domain'de
/// (Order.Cancel geçiş matrisi, StockItem.Return, Payment.Refund); bu handler yalnız SIRAYI
/// yönetir (Compensation deseni). Sıralama kritik (D5): önce stok iadesi (best-effort, idempotent),
/// SONRA refund (kritik — başarısızsa iptal persist EDİLMEZ, sipariş Paid kalır), en son
/// SaveChanges (Cancelled + OrderCancelled outbox atomik).
/// </summary>
public sealed class CancelOrderHandler(
    IOrderRepository orders,
    IStockReservationService stockReservation,
    IPaymentService paymentService,
    ILogger<CancelOrderHandler> logger)
{
    private const string Trigger = "cancel";

    public async Task<Result> HandleAsync(
        Guid orderId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        // TRACKING yükleme: order.Cancel mutasyonu SaveChanges ile kalıcılaşacak.
        var order = await orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null || order.CustomerId != customerId)
        {
            // Varlık sızdırılmaz: başkasının siparişi de "bulunamadı" (GetOrderHandler deseni).
            return Result.Failure(OrderErrors.NotFound(orderId));
        }

        // Domain matrisi: yalnız iptal-edilebilir durumlar (Paid dahil, Shipped hariç).
        var cancel = order.Cancel(Trigger);
        if (cancel.IsFailure)
        {
            return cancel;
        }

        // (1) Stok iadesi — best-effort: commit edilmiş stok OnHand'e geri. İptal token'ı YOK
        // (telafi tamamlanmalı); iade idempotenttir, başarısızlık iptali bozmaz (Warning + iz).
        foreach (var line in order.Lines)
        {
            var ret = await stockReservation.ReturnAsync(line.ReservationId, CancellationToken.None);
            if (ret.IsFailure)
            {
                logger.LogWarning(
                    "İptal stok iadesi başarısız: {ReservationId} ({ErrorCode})",
                    line.ReservationId, ret.Error.Code);
            }
        }

        // (2) Refund — KRİTİK: para iadesi başarısızsa iptal geri sarılır (order mutasyonu
        // persist edilmez, sipariş Paid kalır). Idempotent: aynı sipariş iki kez iptalde kopya.
        var refund = await paymentService.RefundAsync(
            new RefundRequest(order.CustomerId, order.Id, order.IdempotencyKey, order.TotalAmount),
            cancellationToken);
        if (refund.IsFailure)
        {
            return Result.Failure(refund.Error);
        }

        // (3) Cancelled + OrderCancelled outbox TEK transaction'da (interceptor).
        return await orders.SaveChangesAsync(cancellationToken);
    }
}
