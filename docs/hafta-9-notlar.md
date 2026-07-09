# Hafta 9 Karar Notları — Compensation + TTL

> Roadmap Hafta 9 çıktısı: **rezervasyon TTL süpürücüsü** (reserve-sonrası crash penceresi) +
> **kapsamlı sipariş iptali** (Cancel → refund + stok iade). Kanıt: crash/iptal senaryosunun log
> ve test kanıtı. **Oversell=0 (NFR-3.1) her tasarım kararında korundu.**

## KRİTİK BULGU — PaymentPending order TTL'i pratikte boştur

`CheckoutHandler` **senkron**: sipariş yalnız ödeme başarılıysa **doğrudan `Paid` olarak** persist
edilir; başarısızsa hiç yazılmaz. `MarkPaymentPending` yalnız bellekte transit geçiştir. Sonuç:
**DB'de kalıcı `PaymentPending` sipariş asla oluşmaz** → FR-5.5'in "PaymentPending order TTL
süpürücüsü" **pratikte hiç satır bulmaz**. Bu yüzden yazılmadı (yanıltıcı olurdu). W9'un gerçek
işi bu bulgudan türedi:

1. **Inventory rezervasyon TTL süpürücüsü** — başarısız checkout'tan kalan yetim Active
   rezervasyonlar (release-retry tükenmesi, P1 crash: reserve edildi ama sipariş yazılmadan
   crash). Kod yorumu zaten "iz W9 TTL süpürücüsüne kalır" diyordu.
2. **P2 reconcile** — Paid persist edildi ama commit öncesi crash → Active kalan rezervasyon.
   Bunu körlemesine expire etmek **gerçek oversell** yaratır (W7 notu).
3. **Kullanıcı-tetikli Cancel** — Paid siparişin iptali (refund + stok iade).

## Mimari akışlar

**(a) TTL Süpürücü + P2 Reconcile** (`ReservationTtlSweeper`, 30sn poll)
```
Active + ExpiresAtUtc<now rezervasyonları bul  [kısmi index ix_reservations_active_expiry]
  → IOrderReservationReconciler.ClassifyAsync(ids)   [Inventory → Ordering.Contracts]
       Paid siparişe bağlı? (order_lines JOIN orders WHERE Status='Paid')
  → bound=true  → CommitAsync (P2 reconcile: OnHand-=q, Available DEĞİŞMEZ, ASLA expire)
    bound=false → ExpireAsync (yetim: Reserved-=q, stok iade, FR-3.3)
  → classify HATA → batch'e DOKUNULMAZ (şüphede expire yok — NFR-3.1)
```

**(b) Kapsamlı Cancel** (`CancelOrderHandler`)
```
GetByIdAsync [TRACKING]; sahiplik (CustomerId!=userId → 404)
order.Cancel("cancel")  → Paid→Cancelled (matris genişledi) + OrderCancelled event
foreach line: ReturnAsync(ReservationId)  (Committed→Returned, OnHand+=q) → best-effort WARN
RefundAsync(key, amount)  → Completed→Refunded + audit + FakePsp → FAIL → Cancel geri sarılır
orders.SaveChangesAsync()  → Cancelled + OrderCancelled outbox atomik (interceptor)
```

## SOLID prensipleri

- **SRP:** `ReservationTtlSweeper` yalnız poll→classify→commit/expire; `OrderReservationReconciler`
  yalnız "Paid'e bağlı mı"; `CancelOrderHandler` yalnız telafi sırası. İş kuralları (sayaç
  mutasyonu, geçiş, refund) Domain'de.
- **OCP:** `OrderingIntegrationEventRegistry`'ye `OrderCancelled` **ikinci girdi** olarak eklendi —
  interceptor/dispatcher değişmedi (W8 genişleme noktasının kanıtı). `ExecuteWithRetryAsync`
  Expire/Return için yeni operation lambda'sıyla yeniden kullanıldı (xmin-retry iskeleti aynı).
- **LSP:** `ReservationTtlSweeper : BackgroundService` (OutboxDispatcher simetrisi); Payment
  `Refund()` terminal-immutability'yi bozmadan Completed→Refunded ekler.
- **ISP:** `IOrderReservationReconciler` tek metot — Inventory, Ordering'in `IOrderRepository`/
  iç modelini görmez; süpürücü dar `IStockReservationService` metotlarını çağırır.
- **DIP:** Süpürücünün çekirdeği `SweepBatchAsync(db, reservations, reconciler, ct)` parametreli
  (test seam); `CancelOrderHandler` yalnız Contracts portlarına bağlı (EF görmez — Kural 3).

## Tasarım kalıpları

- **Background Job / Template Method:** süpürücü sabit poll-loop iskeleti (Quartz/Hangfire yok).
- **Strategy/Policy:** "Paid'e bağlı mı" politikası Ordering'de (`IOrderReservationReconciler`) —
  Inventory'ye gömülü SQL şema kaçağı olurdu.
- **Adapter (ACL):** `OrderReservationReconciler` ve `PaymentService.RefundAsync` domain/EF'yi
  Contracts POCO'suna çevirir.
- **Compensation (hafif saga adımı):** Cancel, Reserve/Commit'in tersini (Return + Refund) sırayla
  uygular; ağır saga state-persist yerine sıralı best-effort/kritik.
- **Idempotent Guard:** Expire/Return/Refund erken-return (at-least-once süpürücü + çift iptal
  koruması); distributed lock yerine durum guard + xmin.

## Alınan kararlar

1. **P2 reconcile Ordering.Contracts sorgusuyla** (kullanıcı kararı). Süpürücü Paid-bağlı
   rezervasyonu **Commit'e** çevirir (satılmış stoğu kalıcı düş — Available değişmez, oversell=0),
   yetimi Expire eder. Inventory→Ordering.Contracts yeni bağımlılığı eklendi — yalnız Contracts
   katmanı (proje-referans döngüsü yok, ArchitectureTests 4 kural yeşil doğruladı).
2. **Cancel kapsamlı** (kullanıcı kararı): `Paid→Cancelled` matrise eklendi (42→49 hücre matris
   testi); Payment refund primitifi (W7 borcu) + Inventory return primitifi (commit'i geri açar).
3. **`StockItem.Expire`** (Active→Expired, Reserved-=q — Release'in Status'u farklı ikizi) ve
   **`StockItem.Return`** (Committed→Returned, **yalnız OnHand+=q** — commit OnHand ve Reserved'ı
   birlikte düşürmüştü, iade yalnız OnHand'i geri açar → Available yükselir). İkisi de idempotent.
4. **`ReservationStatus.Returned=4`** (Released'dan ayrı — audit netliği: "iptalle iade" ≠
   "checkout telafisi").
5. **Sıra (Cancel): önce Return (best-effort), sonra Refund (kritik).** Refund başarısızsa iptal
   **persist edilmez** (sipariş Paid kalır) — para iadesi kritik, stok iadesi idempotent/ucuz.
6. **Süpürücü sınıflandırma hatasında batch'e dokunmaz** (D9): ClassifyAsync fırlarsa exception
   ExecuteAsync'te yakalanır, hiçbir rezervasyon expire edilmez → şüphede oversell riski sıfır.
7. **Kısmi index** `ix_reservations_active_expiry` (`WHERE "Status" = 'Active'`) — süpürücünün
   sıcak sorgusu, tablo Committed/Expired satırlarıyla büyüse de ucuz kalır.
8. **`OrderCancelled` yayınlanır** (registry ikinci girdisi) — W10 tüketicisi (Shipping/
   Notification) dinleyecek; şimdi OCP'nin somut kanıtı.
9. **Repository'ye `SaveChangesAsync`** eklendi (mevcut yalnız AddAsync'ti) — GetByIdAsync TRACKING
   yüklediğinden Cancel mutasyonu bununla kalıcılaşır ve interceptor OrderCancelled'ı outbox'a yazar.

## Doğrulama (9 Temmuz 2026)

### Testler — tümü yeşil (12 proje)
- **Inventory.UnitTests 36** (+11: StockItemExpire 5, StockItemReturn 6).
- **Ordering.UnitTests 81** (+6: matris (Paid,Cancelled) + Cancel→OrderCancelled/yanlış-duyuru,
  CancelOrderHandler 6 senaryo).
- **Payment.UnitTests 13** (+4: refund Completed→Refunded/idempotent/Pending-Failed→NotRefundable).
- **Inventory.IntegrationTests 18** (+5): sweeper seam (yetim→Expire, P2→Commit, classify-hata→dokunma)
  + **full-stack oversell=0** (yetim gerçekten Expire + Reserved iade; **P2 Paid-bağlı Commit'e
  çevrilir, Available ARTMAZ**).
- **Ordering.IntegrationTests 9** (+1): reconciler adapter gerçek Postgres (Paid→bound, Cancelled/
  bilinmeyen→değil; owned SelectMany çevirisi doğrulandı).
- **Payment.IntegrationTests 10** (+3): refund kalıcılık + idempotent (tek audit) + NotRefundable.
- **ArchitectureTests 32** — 4 kural yeşil (Inventory→Ordering.Contracts sınırı bozmadı).

### Uçtan uca (canlı, gerçek Postgres/RabbitMQ)
```
KAPSAMLI CANCEL:
  checkout → 201 Paid, onHand 100→97 (commit), reserved=0
  POST /orders/{id}/cancel → 204; sipariş=Cancelled; onHand 97→100 (stok iade) + refund

TTL SÜPÜRÜCÜ (canlı log):
  [INF] TTL sweep: 100 aday, 0 reconcile-commit, 100 expire   (her 30sn)
  reservations: Expired sayacı 0 → 500 (5 turda tırmandı) — yetim rezervasyonlar iade ediliyor
```
Not: Host DB'sinde önceki K6 yük testlerinden **4230 Active yetim rezervasyon** birikmişti;
süpürücü `ORDER BY ExpiresAtUtc` ile 100/tur boşaltıyor. Bu, canlı ortamda yetim-expire
mekanizmasının çalıştığının doğrudan kanıtıdır (0 reconcile-commit = hiçbiri Paid siparişe bağlı
değil, hepsi doğru şekilde yetim sınıflandırıldı). P2 reconcile-commit'in kesin sayaç etkisi
(Commit, Available değişmez) integration testte kanıtlandı — canlı P2 penceresi checkout ortasında
crash gerektirir, tekrar-üretimi integration test seviyesinde yapıldı.

## Bilinçli ertelemeler

| Konu | Bu hafta | Hafta |
|---|---|---|
| PaymentPending-order TTL süpürücüsü | Senkron checkout hiç Pending persist etmez → yazılmadı | — |
| OrderCancelled / StockExpired / StockReturned / PaymentRefunded tüketicileri | Yayınlanır/raise edilir, tüketici yok | W10 |
| Gerçek PSP refund çağrısı | FakePsp deterministik | W12+ |
| Inventory outbox (StockExpired/Returned dispatch) | Yalnız domain iz | — |
| Süpürücü dağıtık kilit (çift-instance) | Tek-instance; Expire/Commit idempotent zararsız | W12+ |

## Riskler / notlar

- **Shared'a SIFIR kod değişikliği** — süpürücü Inventory-lokal, reconciler Ordering-lokal;
  BackgroundService/CreateAsyncScope yerleşik; Result/Error zaten Shared.Kernel'de.
- **Inventory→Ordering.Contracts** ilk "ters yön" modül bağımlılığı (Ordering zaten
  Inventory.Contracts'a bağlı). Yalnız Contracts katmanı olduğundan proje-referans döngüsü yok;
  kavramsal çift yön kabul edildi (P2 reconcile için Ordering'in "Paid'e bağlı mı" bilgisi şart).
- **Expire ≠ Release, Return ≠ Release:** dört durum (Released/Expired/Committed/Returned) ayrı iz
  bırakır — süpürücü telafisi ("TTL doldu") ile checkout telafisi ("release") audit'te ayrışır.
- **Concurrent sweeper + checkout aynı rezervasyona:** xmin retry + durum guard'lar güvenli
  reddeder (Committed→Expire=NotExpirable, Expired→Commit=NotCommittable); süpürücü Paid-bağlıyı
  zaten Commit'e yönlendirir.
- Test sayısı 281 → **~308** (Inventory +16, Ordering +7, Payment +7).
