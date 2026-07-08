using FluentValidation;
using Microsoft.Extensions.Logging;
using ModularCommerce.Cart.Contracts;
using ModularCommerce.Catalog.Contracts;
using ModularCommerce.Inventory.Contracts;
using ModularCommerce.Ordering.Application.Orders.Common;
using ModularCommerce.Ordering.Domain.Orders;
using ModularCommerce.Payment.Contracts;
using ModularCommerce.Shared.Kernel;

namespace ModularCommerce.Ordering.Application.Orders.Checkout;
public sealed class CheckoutHandler(
    IOrderRepository orders,
    ICartService cartService,
    IProductReader productReader,
    IStockReservationService stockReservation,
    IPaymentService paymentService,
    IValidator<CheckoutCommand> validator,
    ILogger<CheckoutHandler> logger)
{
    private const string Trigger = "checkout";

    public async Task<Result<CheckoutResponse>> HandleAsync(
        CheckoutCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<CheckoutResponse>(Error.Validation(
                "Ordering.Checkout.InvalidCommand",
                string.Join(" ", validation.Errors.Select(e => e.ErrorMessage))));
        }

        // (a) Hızlı yol: aynı key daha önce işlendiyse kopyasını dön (FR-5.4).
        var existing = await orders.GetByIdempotencyKeyAsync(
            command.CustomerId, command.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return Replay(existing);
        }

        // (b) Sepet checkout'un kaynağıdır (FR-4.3).
        var cartResult = await cartService.GetItemsAsync(command.CustomerId, cancellationToken);
        if (cartResult.IsFailure)
        {
            return Result.Failure<CheckoutResponse>(cartResult.Error);
        }

        var cartLines = cartResult.Value;
        if (cartLines.Count == 0)
        {
            // İkincil yarış: paralel kazanan siparişi yazıp SEPETİ TEMİZLEMİŞ olabilir —
            // key'i bir kez daha kontrol etmeden EmptyCart dönmek FR-5.4'ü yük altında kırar.
            existing = await orders.GetByIdempotencyKeyAsync(
                command.CustomerId, command.IdempotencyKey, cancellationToken);

            return existing is not null
                ? Replay(existing)
                : Result.Failure<CheckoutResponse>(OrderErrors.EmptyCart);
        }

        // (c) Fiyat/ad snapshot'ı + satılabilirlik: tek batch sorgu (N+1 yok).
        var products = (await productReader.GetByIdsAsync(
                [.. cartLines.Select(l => l.ProductId)], cancellationToken))
            .ToDictionary(p => p.ProductId);

        foreach (var line in cartLines)
        {
            if (!products.TryGetValue(line.ProductId, out var product) || !product.IsActive)
            {
                return Result.Failure<CheckoutResponse>(OrderErrors.ProductUnavailable(line.ProductId));
            }
        }

        // (d) Rezervasyonlar sırayla; ilk hatada öncekiler geri bırakılır (telafi).
        var reservedIds = new List<Guid>();
        var drafts = new List<OrderLineDraft>();

        foreach (var line in cartLines)
        {
            var reserve = await stockReservation.ReserveAsync(
                line.ProductId, line.Quantity, cancellationToken);

            if (reserve.IsFailure)
            {
                await ReleaseAllAsync(reservedIds);
                return Result.Failure<CheckoutResponse>(reserve.Error);
            }

            reservedIds.Add(reserve.Value.ReservationId);

            var product = products[line.ProductId];
            drafts.Add(new OrderLineDraft(
                line.ProductId,
                product.Name,
                product.Price,
                product.Currency,
                line.Quantity,
                reserve.Value.ReservationId));
        }

        // (e) Yaşam döngüsü domain'de: Created doğar, zincir tamamlandığı için
        // StockReserved'a geçer (her iki adım da history + event bırakır).
        var orderResult = Order.Create(command.CustomerId, command.IdempotencyKey, drafts, Trigger);
        if (orderResult.IsFailure)
        {
            await ReleaseAllAsync(reservedIds);
            return Result.Failure<CheckoutResponse>(orderResult.Error);
        }

        var order = orderResult.Value;

        var markResult = order.MarkStockReserved(Trigger);
        if (markResult.IsFailure)
        {
            await ReleaseAllAsync(reservedIds);
            return Result.Failure<CheckoutResponse>(markResult.Error);
        }

        // (e2) Ödeme SENKRON alınır (roadmap revizyonu: tek istek, iki adım yok).
        // PaymentPending'den transit geçilir — history dürüst kalır, matris bozulmaz.
        var pendingResult = order.MarkPaymentPending(Trigger);
        if (pendingResult.IsFailure)
        {
            await ReleaseAllAsync(reservedIds);
            return Result.Failure<CheckoutResponse>(pendingResult.Error);
        }

        // 1. hakem: Payment'ın unique index'i — aynı key ASLA iki kez charge edilemez
        // (FR-6.2). Başarısız ödemede sipariş HİÇ persist edilmez, rezervasyonlar
        // geri bırakılır, kullanıcı hatayı aynı istekte görür.
        var charge = await paymentService.ChargeAsync(
            new ChargeRequest(
                command.CustomerId,
                order.Id,
                command.IdempotencyKey,
                order.TotalAmount,
                order.Currency),
            cancellationToken);

        if (charge.IsFailure)
        {
            await ReleaseAllAsync(reservedIds);
            return Result.Failure<CheckoutResponse>(charge.Error);
        }

        var paidResult = order.MarkPaid(Trigger);
        if (paidResult.IsFailure)
        {
            await ReleaseAllAsync(reservedIds);
            return Result.Failure<CheckoutResponse>(paidResult.Error);
        }

        // (f) 2. hakem: Ordering'in unique index'i — aynı key tek sipariş (FR-5.4).
        Result addResult;
        try
        {
            addResult = await orders.AddAsync(order, cancellationToken);
        }
        catch
        {
            // Beklenmedik persist hatası: rezervasyonlar geri bırakılır, istisna
            // GlobalExceptionHandler'a yükselir (500).
            await ReleaseAllAsync(reservedIds);
            throw;
        }

        if (addResult.IsFailure)
        {
            // Kaybeden yol: kendi rezervasyonları release edilir, COMMIT ASLA çağrılmaz —
            // kalıcı düşüşü yalnız kazananın kendi akışı yapar (çift düşüş imkansız).
            await ReleaseAllAsync(reservedIds);

            if (addResult.Error == OrderErrors.DuplicateIdempotencyKey)
            {
                // Yarışı kaybettik: kazananın siparişi döner (FR-5.4); ödeme de zaten
                // replay'di (1. hakem) — ne çift charge ne çift sipariş.
                var winner = await orders.GetByIdempotencyKeyAsync(
                    command.CustomerId, command.IdempotencyKey, cancellationToken);

                if (winner is not null)
                {
                    return Replay(winner);
                }
            }

            return Result.Failure<CheckoutResponse>(addResult.Error);
        }

        // (f2) Para alındı + sipariş yazıldı → rezervasyonlar kalıcı düşüşe çevrilir
        // (FR-3.3). Best-effort: commit hatası siparişi GERİ DÖNDÜRMEZ — Reserved şişik
        // kalır (undersell görünümü, oversell DEĞİL), iz W9 süpürücüsüne.
        await CommitAllAsync(reservedIds);

        // (g) Sepet temizliği best-effort: sipariş yazıldı, sepet AP — hata
        // checkout'u geri döndürmez, yalnız loglanır (bayat sepet zararsızdır).
        var clearResult = await cartService.ClearAsync(command.CustomerId, cancellationToken);
        if (clearResult.IsFailure)
        {
            logger.LogWarning(
                "Checkout sonrası sepet temizlenemedi: {CustomerId} ({ErrorCode})",
                command.CustomerId, clearResult.Error.Code);
        }

        // (h) Yeni sipariş: endpoint 201 + Location üretir.
        return Result.Success(new CheckoutResponse(OrderResponse.FromOrder(order), IsExisting: false));
    }

    private static Result<CheckoutResponse> Replay(Order existing)
        => Result.Success(new CheckoutResponse(OrderResponse.FromOrder(existing), IsExisting: true));

    /// <summary>
    /// Telafi: alınmış rezervasyonları geri bırakır. İptal token'ı BİLEREK yok —
    /// istek iptal edilse bile telafi tamamlanmalıdır. Başarısız release stok
    /// sızıntısı bırakır; Warning izi W9 TTL süpürücüsüne kalır.
    /// </summary>
    private async Task ReleaseAllAsync(IReadOnlyList<Guid> reservationIds)
    {
        foreach (var reservationId in reservationIds)
        {
            var release = await stockReservation.ReleaseAsync(reservationId, CancellationToken.None);
            if (release.IsFailure)
            {
                logger.LogWarning(
                    "Telafi release başarısız: {ReservationId} ({ErrorCode})",
                    reservationId, release.Error.Code);
            }
        }
    }

    /// <summary>
    /// Kesinleştirme (ReleaseAllAsync simetrisi): para alınmış siparişin rezervasyonlarını
    /// kalıcı düşüşe çevirir. İptal token'ı yok — istek iptal edilse bile tamamlanmalıdır.
    /// </summary>
    private async Task CommitAllAsync(IReadOnlyList<Guid> reservationIds)
    {
        foreach (var reservationId in reservationIds)
        {
            var commit = await stockReservation.CommitAsync(reservationId, CancellationToken.None);
            if (commit.IsFailure)
            {
                logger.LogWarning(
                    "Rezervasyon commit başarısız: {ReservationId} ({ErrorCode})",
                    reservationId, commit.Error.Code);
            }
        }
    }
}
