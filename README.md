# ModularCommerce

Modüler monolith mimarisiyle geliştirilmiş, production kaygılarıyla tasarlanmış bir e-ticaret platformu.
Bu proje, yazdığım Medium makalelerindeki kavramların (race condition yönetimi, idempotency,
API resiliency, K6 yük testleri) çalışan ve ölçülebilir bir sistemde birleşimidir.

## Mimari

- **8 modül** (Identity, Catalog, Cart, Inventory, Ordering, Payment, Shipping, Notification),
  her biri Api / Application / Domain / Infrastructure / Contracts katmanlarıyla.
- **Tek deployable**: `ModularCommerce.Host` composition root'tur; modüller `IModule`
  sözleşmesiyle kendilerini kaydeder.
- **Sınır kuralları**: Bir modül, diğer modülün yalnızca `Contracts` projesine referans verebilir.
  Bu kural üç katmanda zorlanır: NetArchTest (derleme), ayrı PostgreSQL şeması + modül başına
  DB kullanıcısı (çalışma zamanı), code review (insan).
- **İletişim**: Senkron sorgular Contracts arayüzleri ile in-process; olgular (event'ler)
  outbox → RabbitMQ üzerinden asenkron.
- Detaylar: [docs/requirements.md](docs/requirements.md)

## Çalıştırma

```bash
docker compose up -d
dotnet run --project src/Bootstrapper/ModularCommerce.Host
# http://localhost:5000/           -> modül listesi
# http://localhost:5000/api/catalog/health
```

## Kanıt yükümlülükleri

Bu projede her iddia ölçümle kanıtlanır (bkz. requirements §10):
oversell = 0, duplicate ödeme = 0, checkout p95 < 500 ms.

### Oversell kanıtı: üç stratejinin karşılaştırması (Hafta 3-4)

**Katman 1 — Doğruluk (Testcontainers, `dotnet test` ile tekrarlanabilir):** 10 stokluk ürüne
start-gate ile **aynı anda** bırakılan 100 paralel rezervasyon
(`ReservationConcurrencyTests` + `RedisLockConcurrencyTests`):

| Strateji | Deneme | "Başarılı" | Reserved/OnHand | **Oversell** | Çakışma-retry | Kilit timeout |
|---|---|---|---|---|---|---|
| Naive (korumasız check-then-act) | 100 | **100** | 100 / 10 | **90** | — | — |
| Optimistic concurrency (xmin) | 100 | **10** | 10 / 10 | **0** | 121 | — |
| Redis distributed lock | 100 | **10** | 10 / 10 | **0** | **0** | 4 |

**Katman 2 — Yük (K6, 1000 VU × 1 istek, aynı ürün; istemci 409-retryable'da tekrar dener):**

| Strateji | "Başarılı" (201) | **Oversell** | Çakışma-retry | Kilit timeout | Toplam istek | p95 |
|---|---|---|---|---|---|---|
| Naive | **229** | **219** | — | — | 1.002 | 1,67 sn |
| Optimistic (xmin) | **10** ✓ | **0** | 971 | — | 1.973 | 505 ms |
| Redis lock | **10** ✓ | **0** | **0** | 2.560 | 3.525 | 6,05 sn* |

**Katman 3 — Sürekli yük altında etkin yazma kapasitesi (K6 smoke: 50 VU × 30 sn, TEK sıcak ürün;
409 tasarlanmış yanıttır):** NFR-3.2 hedefi p95 < 150 ms — kilit/çakışma beklemesi dahil.

| Strateji | p95 | Başarılı yazma/sn | Retryable-409/sn | Başarı oranı |
|---|---|---|---|---|
| Optimistic (xmin) | **71 ms** ✓ | ≈ 90 | ≈ 1.081 | %7,7 |
| Redis lock | **114 ms** ✓ | ≈ **155** | ≈ 355 | %30 |

Okuma notları:
- **Naive:** iş kuralı doğru yerde (domain) ama bayat snapshot'la çalışıyor — 1000 VU'da 219 oversell.
- **Optimistic:** sıfır oversell; bedeli çakışma-retry trafiği. Tek sıcak satırda sürekli yükte
  çakışma oranı %92'ye çıkıyor — requirements'ın "yetersiz kalırsa" senaryosu ölçümle görünür oldu.
- **Redis lock:** çakışmayı beklemeye çevirir (çakışma=0); aynı sıcak satırda **~1,7× daha fazla
  başarılı yazma** geçirir. (*) Burst'teki 6 sn p95, stok bittiği halde retry eden istemci
  kuyruğunun ürünüdür; smoke ölçümü (114 ms) NFR-3.2'yi karşılar.
- Tekrarlamak için: `dotnet test tests/ModularCommerce.Inventory.IntegrationTests` (doğruluk)
  ve [tests/LoadTests/README.md](tests/LoadTests/README.md) (K6 koşu tarifi).

### Kimlik + sepet akışı (Hafta 5)

Login olup sepete ürün ekleme akışı canlı; endpoint authorization JWT ile:

```
GET /api/cart (tokensiz)          → 401
POST /api/identity/signup         → 201 (aynı e-posta → 409)
POST /api/identity/login          → 200 (access 15 dk + refresh 7 gün, rotasyonlu)
POST /api/cart/items (Bearer)     → 200 + "sepete eklemek rezervasyon değildir" uyarısı (FR-4.4)
redis-cli TTL cart:{userId}       → 604800 (7 gün, yazmada kayar)
```

K6 smoke ölçümleri (12 çekirdek, 30 sn koşular):

| Senaryo | Hedef | Sonuç |
|---|---|---|
| Login dalgası (200 RPS, `constant-arrival-rate`) | NFR-1.1 p95 < 200 ms | **20,9 ms** ✓ (6.000 login, %0 hata) |
| Sepet okuma/yazma (20 VU, JWT + Redis dahil) | NFR-4.1 p95 < 50 ms | **2,88 ms** ✓ (231k istek, %0 hata) |

Haftanın bulgusu: varsayılan PBKDF2 iterasyonu (100k, ~150 ms/doğrulama) 200 RPS'te CPU'yu
doyurup p95'i 3,8 sn'ye çıkardı; `IterationCount=20k` bilinçli trade-off'uyla NFR karşılandı —
ayrıntı ve gerekçe [docs/hafta-5-notlar.md](docs/hafta-5-notlar.md).

### Checkout + idempotency akışı (Hafta 6)

İlk modüller arası senkron çağrılar: checkout, Cart/Catalog/Inventory'ye YALNIZ Contracts
arayüzleriyle gider (4. mimari test `Contracts_should_be_self_contained` bunu sabitler).
Sipariş state machine'i domain'de (7 durum × 6 geçiş tam matris testli); her geçiş
`order_status_history` tablosuna iz bırakır.

```
POST /api/ordering/checkout (Idempotency-Key) → 201, status=StockReserved
POST aynı key ikinci kez                       → 200 + AYNI sipariş (FR-5.4)
GET  /api/ordering/orders/{id}                 → history: ∅→Created, Created→StockReserved
kısmi rezervasyon hatası                        → önceki rezervasyonlar release (sızıntı 0)
```

K6 ölçümleri (20 VU × 30 sn, tek sıcak ürün, OptimisticConcurrency):

| Senaryo | Hedef | Sonuç |
|---|---|---|
| Checkout zinciri p95 | NFR-5.1 < 500 ms | **27 ms** ✓ (3.957 sipariş, ≈121/sn, %0 hata) |
| Aynı key ile 5 PARALEL checkout ×50 | FR-5.4: tek sipariş | **50/50**: tam 1×201 + 4×200, hepsi aynı id ✓ |

Sıcak üründe sipariş başına ~5 retryable-409 üretildi (istemci aynı key ile tekrar dener —
idempotency'nin var olma sebebi); ayrıntı [docs/hafta-6-notlar.md](docs/hafta-6-notlar.md).

### Senkron ödeme + tek charge + circuit breaker (Hafta 7)

Ödeme, checkout isteğinin İÇİNDE senkron alınır (tek istek, iki adım yok): başarılı ödeme
siparişi `Paid` yapar ve stoğu KALICI düşürür (`Commit`: OnHand ve Reserved birlikte azalır —
Available değişmez, commit oversell üretemez); başarısız ödeme rezervasyonları release eder,
sipariş hiç yazılmaz. Sahte PSP, Polly pipeline'ı (timeout 3sn → retry+jitter → circuit
breaker → bulkhead → deneme-başı timeout 1sn) arkasında.

```
POST /api/ordering/checkout  → 201 status=Paid, history 4 satır (…→PaymentPending→Paid)
GET  /api/inventory/stock    → onHand 10→8, reserved=0 (kalıcı düşüş)
ödeme reddi (DeclineRate=1)  → 409 Payment.Declined; sipariş YOK, reserved=0 (sızıntı yok);
                               aynı key yine 409 — PSP'ye gitmeden kopya döner (FR-6.2)
```

Kanıt ölçümleri (requirements §10.2 ve §10.3):

| Kanıt | Sonuç |
|---|---|
| Aynı key ile **100 paralel** checkout (K6 + Testcontainers) | tam 1×201 + 99×200, hepsi aynı sipariş; payments tablosunda **TEK Completed**, PSP sayacı **1** ✓ |
| Checkout zinciri p95 (ödeme + commit dahil, NFR-5.1 < 500 ms) | **245 ms** ✓ (2.184 sipariş, %0 hata) |
| PSP %50 hata + 60 sn yük → breaker döngüsü (Serilog) | `OnCircuitOpened` ×11, `OnCircuitClosed` ×5; breaker açıkken 4.452 hızlı-ret, buna rağmen 251 sipariş ✓ |
| Ödeme reddi → stok sızıntısı | 40/40 checkout 409 `Payment.Declined`, sonunda **reserved=0** ✓ |

Breaker log örneği: `Resilience event occurred. EventName: 'OnCircuitOpened', Source:
'payment-psp/…/CircuitBreaker', Result: 'psp_5xx'` — ayrıntı ve karar gerekçeleri
[docs/hafta-7-notlar.md](docs/hafta-7-notlar.md).

### El yapımı Transactional Outbox (Hafta 8)

Sipariş `Paid` olduğunda `OrderPaid` olayı dış dünyaya duyurulur — ama **atomik** olarak
(NFR-5.2). El yapımı outbox: `OrderPaid` domain event'i, bir `SaveChangesInterceptor` ile
sipariş satırıyla **aynı transaction'da** `ordering.outbox_messages` tablosuna yazılır; ayrı bir
`BackgroundService` (dispatcher) bekleyenleri poll edip MassTransit ile RabbitMQ'ya publish eder
ve işaretler. Dual-write problemi (DB commit + broker publish ayrı ayrı → biri patlarsa
tutarsızlık) böyle çözülür.

```
POST /api/ordering/checkout → 201 Paid
  → ordering.outbox_messages: OrderPaid satırı (Order ile atomik)
  → dispatcher publish → RabbitMQ fanout exchange "…IntegrationEvents:OrderPaid"
  → OrderPaidLoggingConsumer: "[W8 log-only] OrderPaid alındı: OrderId=… Tutar=…"
  → outbox satırı: ProcessedOnUtc dolu (ISLENDI)
```

Kanıtlar:

| Kanıt | Sonuç |
|---|---|
| **Atomiklik** — SaveChanges patlarsa (23505) NE sipariş NE outbox yazılır | Testcontainers ✓ (geri sarım) |
| Yalnız `OrderPaid` terfi eder (OrderCreated/OrderStatusChanged outbox'a girmez) | interceptor testi ✓ |
| Publish→consume akışı | MassTransit `ITestHarness`: `Published`+`Consumed` ✓ |
| Uçtan uca | RabbitMQ UI'da `OrderPaid` fanout exchange + consumer kuyruğu + Host logu + outbox ISLENDI ✓ |

Ayrıntı, SOLID/pattern haritası ve karar gerekçeleri [docs/hafta-8-notlar.md](docs/hafta-8-notlar.md).

### Rezervasyon TTL süpürücü + kapsamlı Cancel (Hafta 9)

Reserve-sonrası crash penceresinde askıda kalan rezervasyonları bir `BackgroundService`
(`ReservationTtlSweeper`, 30sn poll) temizler — ama **oversell=0 kesinliğini bozmadan**: süresi
geçmiş her Active rezervasyonu Ordering'e sorar (`IOrderReservationReconciler`), Paid siparişe
bağlı olanı **Commit'e** çevirir (satılmış stok — Available değişmez), yetim olanı **Expire** eder
(stok iade). Ayrıca kullanıcı siparişini iptal edebilir (`POST /orders/{id}/cancel`): Payment
**refund** + Inventory **return** (commit edilmiş stok OnHand'e geri) + sipariş `Cancelled`.

```
KAPSAMLI CANCEL:
  checkout → 201 Paid, onHand 100→97 (commit)
  POST /orders/{id}/cancel → 204; Cancelled; onHand 97→100 (iade) + refund

TTL SÜPÜRÜCÜ (canlı log): "TTL sweep: 100 aday, 0 reconcile-commit, 100 expire" (her 30sn)
```

| Kanıt | Sonuç |
|---|---|
| **P2 oversell=0** — süpürücü Paid-bağlı rezervasyonu Commit'e çevirir, Available ARTMAZ | Testcontainers ✓ |
| Yetim rezervasyon → Expire + Reserved iade | Testcontainers ✓ + canlı log (Expired 0→500) |
| Sınıflandırma hatası → batch'e dokunulmaz (şüphede expire yok) | seam testi ✓ |
| Kapsamlı Cancel → Refunded + OnHand geri + Cancelled + OrderCancelled outbox | canlı E2E ✓ |
| Refund idempotent (çift iptal → tek para iadesi) | Testcontainers ✓ |

Kritik bulgu: checkout senkron olduğundan sipariş doğrudan `Paid` yazılır — hiçbir sipariş
`PaymentPending` kalmaz, dolayısıyla FR-5.5'in "PaymentPending order TTL"i pratikte boştur; W9'un
işi Inventory rezervasyon TTL'i + P2 reconcile'dir. Ayrıntı [docs/hafta-9-notlar.md](docs/hafta-9-notlar.md).

### Notification: idempotent consumer + DLQ (Hafta 10)

`OrderPaid` olayını **Notification** tüketir ve sipariş onayı bildirimi (e-posta + webhook
**simülasyonu**) üretir. İki cross-cutting garanti: **idempotent tüketici** (aynı olay 2 kez → 2
bildirim ÜRETMEZ, NFR-8.1) ve **dead-letter queue** (teslim başarısızsa retry → `_error` kuyruğu,
FR-8.3). İdempotency **el yapımı inbox** ile: `notification.processed_messages` tablosu, PK'sı
gerçek eşzamanlılık hakemi.

**Kritik tasarım:** idempotency anahtarı **iş anahtarıdır** (`"OrderPaid:{OrderId}"`), MassTransit
`MessageId` DEĞİL — el yapımı outbox her publish'te yeni MessageId ürettiği için MessageId
tekrarları dedup etmezdi.

```
checkout+ödeme (tek istek) → 201 Paid → outbox → OrderPaid publish
  → order-paid-notification kuyruğu → OrderPaidNotificationConsumer
  → processed_messages boş → e-posta + webhook SİM → 2 audit satırı + processed-marker (tek txn)
  → GET /api/notification/dev/logs/{orderId} → 2 satır (email + webhook)

DLQ DEMO (Notification__Delivery__FailureRate=1.0):
  consumer 3 kez dener, hepsi knob'la fırlar → RabbitMQ UI: order-paid-notification_error kuyruğu dolu
```

| Kanıt | Sonuç |
|---|---|
| İlk işleme → kanal başına 1 audit + 1 processed satırı | Testcontainers ✓ |
| **Aynı OrderId, farklı MessageId** → yine tek bildirim (iş-anahtarı dedup) | Testcontainers ✓ |
| Eşzamanlı çift teslim → PK hakemi 1 set bırakır, iki başarı | Testcontainers ✓ |
| Teslim hatası (knob=1.0) → consumer fault, hiçbir satır kalıcı olmaz | harness ✓ |
| Uçtan uca DLQ | RabbitMQ UI'da `order-paid-notification_error` kuyruğu ✓ (manuel) |

Kapsam notu: Shipping bu hafta descope edildi (yalnız Notification). Ayrıntı, SOLID/pattern haritası
ve "MessageId neden yetmez" gerekçesi [docs/hafta-10-notlar.md](docs/hafta-10-notlar.md).

### Uçtan uca sertleştirme: rate limiting + health + flash-sale (Hafta 11)

Kampanya yükü altında sistem hem **doğru** (oversell=0) hem **korunaklı** (rate limiting) hem
**gözlemlenebilir** (health checks) kalır. **Sıfır yeni NuGet paketi** — rate limiting framework
yerleşiği, health check'ler el yapımı (Postgres+Redis custom probe + MassTransit yerleşiği).

**Katmanlı rate limiting** (kullanıcı/IP partition): global catch-all + `auth` (login/signup, IP
bazlı sıkı — brute-force koruması) + `checkout` (kullanıcı bazlı, burst-absorbing). Reddedilen istek
`429 + Retry-After + ProblemDetails(RateLimited)` döner — istemci kontratına yeni terminal-olmayan
sinyal: `Retry-After` kadar bekle, tekrar dene (retryable-409'un "hemen aynı key"inden farklı).

**Liveness/Readiness ayrımı** (Container Apps/K8s deseni): `/health/live` probsuz (süreç ayakta mı —
geçici bağımlılık düşüşü container'ı öldürmesin), `/health/ready` Postgres+Redis+RabbitMQ probları
(biri düşerse 503 → LB routing keser).

```
GET /health/live  → 200 {"status":"Healthy","checks":[]}
GET /health/ready → 200 postgres:Healthy + redis:Healthy + masstransit-bus:Healthy
POST /api/identity/login ×12 (aynı IP) → #1..10: 401, #11..12: 429 Retry-After: 10
  429 gövdesi: {"type":"RateLimited","status":429,"correlationId":"..."}

flash-sale.js (rampalı yük, düşük stoklu sıcak ürün):
  → teardown "FLASH SALE SONUÇ: başlangıç=50 onHand=0 SATILAN=50 OVERSELL=0"
```

| Kanıt | Sonuç |
|---|---|
| `/health/ready` gerçek prob çalıştırır (Postgres+Redis+RabbitMQ) | canlı E2E ✓ + Testcontainers ✓ |
| Bağımlılık düşünce readiness Unhealthy (503), liveness etkilenmez | Testcontainers ✓ |
| Auth limiti aşımı → 429 + Retry-After + ProblemDetails | canlı E2E ✓ |
| Checkout policy 100-paralel burst'ü kabul (§10.2 kırılmaz) | rate-limiter sizing testi ✓ |
| Flash-sale ramp altında **oversell=0** | K6 teardown ✓ (manuel) |

Kritik tasarım: sıkı per-user checkout limiti §10.2'nin 100-paralel idempotency kanıtını kırardı →
checkout policy **burst-absorbing** (permit+queue ≥ 100, validate ile zorlanır); gösterilebilir 429
**login/signup**'ta (IP bazlı). OpenTelemetry bu hafta kapsam dışı (ertelendi). Ayrıntı
[docs/hafta-11-notlar.md](docs/hafta-11-notlar.md).

## Bilinçli ertelemeler (evolution path)

- API Gateway yok: tek deployable'da middleware pipeline aynı işi görür.
  Bir modül servise ayrıldığında Host, YARP ile proxy'ye evrilir.
- Elasticsearch yok: arama Postgres full-text ile başlar; ölçek gerektirdiğinde CDC + ES yolu açık.
- Virtual waiting room yok: kampanya yükü önce rate limiting + rezervasyon TTL ile yönetilir.
- Identity şeması, compliance gereksinimi doğduğunda sıfır kod değişikliğiyle ayrı DB'ye taşınabilir.
