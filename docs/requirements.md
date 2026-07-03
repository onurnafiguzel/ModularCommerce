# Gereksinim Dokümanı (FR / NFR) — Modüler E-Ticaret Platformu

> Genel amaçlı e-ticaret platformu (katalog, sepet, sipariş, ödeme, kargo). Flaş kampanya / sınırlı stok satışı, platformun bir özelliğidir. Modüler monolith, .NET.
> Bu doküman her modülün fonksiyonel (FR) ve fonksiyonel olmayan (NFR) gereksinimlerini,
> CAP konumlanmasını ve performans hedeflerini tanımlar.

---

## 0. Sistem geneli ilke: CAP kararı operasyon bazında verilir

CAP teoremi "sistem CP mi AP mi" diye sorulmaz; **her operasyon kendi trade-off'unu seçer.**
Platformun anayasası:

| Operasyon türü | Tercih | Gerekçe |
|---|---|---|
| Ürün görüntüleme, listeleme | **AP** (availability + eventual consistency) | 30 sn bayat fiyat göstermek kabul; sayfa açılmaması kabul değil |
| Stok rezervasyonu | **CP** (consistency) | Oversell = para iadesi + itibar kaybı. Gerekirse istek reddedilir |
| Ödeme | **CP** + idempotency | Double-spend asla. Belirsizlikte işlem reddedilir, tekrar denenir |
| Sepet | **AP** | Sepet kaybolabilir/bayatlayabilir; checkout'ta yeniden doğrulanır |
| Bildirim | **AP** + at-least-once | Geç mail sorun değil, kayıp mail tolere edilir ama hedeflenmez |

**Genel NFR hedefleri (kampanya anı yükü altında):**

| Metrik | Hedef |
|---|---|
| Kampanya (flash sale) anı tepe yükü | 1.000 eşzamanlı istek / tek ürün |
| Okuma p95 gecikme | < 100 ms |
| Yazma (checkout zinciri) p95 | < 500 ms |
| Oversell sayısı | **0** (kesin, K6 ile kanıtlanır) |
| Duplicate ödeme | **0** (kesin) |
| Uptime hedefi (simülasyon) | %99,9 |

---

## 1. Identity

**Fonksiyonel gereksinimler**
- FR-1.1: Kullanıcı e-posta + şifre ile kayıt olabilir (signup).
- FR-1.2: Kullanıcı login olup JWT access token + refresh token alır.
- FR-1.3: Refresh token ile access token yenilenebilir; logout'ta refresh token iptal edilir.
- FR-1.4: Şifreler geri döndürülemez şekilde hash'lenir (ASP.NET Identity / Argon2-bcrypt).
- FR-1.5: Aynı e-posta ile ikinci kayıt engellenir (unique constraint + anlamlı hata).

**Fonksiyonel olmayan gereksinimler**
- NFR-1.1 (Latency): Login p95 < 200 ms (hash doğrulama maliyeti dahil).
- NFR-1.2 (Security): Token doğrulama diğer modüllerde *lokal* yapılır (imza kontrolü) — her istekte Identity'e senkron çağrı YOK. Identity çökse bile mevcut token'larla sistem çalışır.
- NFR-1.3 (CAP): Kayıt/giriş CP; token doğrulama tasarım gereği Identity'den bağımsız (availability'yi artıran mimari karar).
- NFR-1.4 (Throughput): Kampanya öncesi login dalgası — 200 RPS sürdürülebilir.

---

## 2. Catalog

**Fonksiyonel gereksinimler**
- FR-2.1: Ürünler listelenebilir, filtrelenebilir, detay görüntülenebilir.
- FR-2.2: Ürünler opsiyonel olarak bir kampanyaya (flash sale) bağlanabilir: başlangıç/bitiş zamanı, kampanya fiyatı, sınırlı adet.
- FR-2.3: Kampanya başlamadan kampanya fiyatı uygulanmaz; kampanyalı ürün "yakında" rozetiyle görünür.
- FR-2.4: Fiyat bilgisi Catalog'a aittir; stok adedi Inventory'den event ile senkronize edilen *yaklaşık* değerdir ("son 3 ürün" rozeti).

**Fonksiyonel olmayan gereksinimler**
- NFR-2.1 (Latency): Liste/detay p95 < 100 ms; p99 < 250 ms.
- NFR-2.2 (Throughput): Kampanya anında en yüksek trafiği bu modül yer — 2.000 RPS okuma hedefi.
- NFR-2.3 (Caching): Redis cache-aside; TTL 30 sn + event bazlı invalidation. Cache stampede'e karşı tek uçuş (single-flight) koruması.
- NFR-2.4 (CAP): AP — bayat veri kabul, erişilemezlik kabul değil.

---

## 3. Inventory

**Fonksiyonel gereksinimler**
- FR-3.1: Ürün başına stok adedi tutulur; kampanyalı ürünler için ayrı kampanya stoğu tanımlanabilir.
- FR-3.2: Checkout sırasında stok *rezerve edilir* (düşülmez); rezervasyonun TTL'i vardır (örn. 5 dk).
- FR-3.3: Ödeme tamamlanınca rezervasyon kalıcı düşüşe çevrilir; ödeme başarısız/timeout olursa stok geri bırakılır (compensation).
- FR-3.4: Stok 0'a düşünce `ProductSoldOut` event'i yayınlanır (Catalog rozet günceller).

**Fonksiyonel olmayan gereksinimler**
- NFR-3.1 (Consistency — projenin kalbi): 10 stokluk ürüne 1.000 eşzamanlı istek geldiğinde **tam 10 rezervasyon** başarılı olur. Sıfır oversell. Optimistic concurrency (rowversion) → yetersiz kalırsa Redis distributed lock; her aşama K6 ile ölçülür ve karşılaştırılır.
- NFR-3.2 (Latency): Rezervasyon p95 < 150 ms — lock bekleme süresi dahil.
- NFR-3.3 (Fairness): İlk gelen ilk alır; retry fırtınasında starvation olmamalı.
- NFR-3.4 (CAP): Kesin CP. Belirsizlik durumunda istek reddedilir ("tekrar deneyin"), asla iyimser onay verilmez.

---

## 4. Cart

**Fonksiyonel gereksinimler**
- FR-4.1: Kullanıcı sepete ürün ekler/çıkarır, adet günceller.
- FR-4.2: Sepet Redis'te tutulur; TTL 7 gün.
- FR-4.3: Checkout'a geçişte sepet içeriği Catalog fiyatı ve Inventory stoğu ile *yeniden doğrulanır* — sepetteki fiyat/stok garanti değildir.
- FR-4.4: Kampanyalı ürünler sepette "sepete eklemek rezervasyon değildir" uyarısıyla gösterilir.

**Fonksiyonel olmayan gereksinimler**
- NFR-4.1 (Latency): Sepet okuma/yazma p95 < 50 ms (Redis).
- NFR-4.2 (CAP): AP — Redis node kaybında sepetin kaybolması kabul edilen risk; kritik veri değil.
- NFR-4.3 (Durability): Bilinçli düşük — sepet kaynak-of-truth değildir. (Mülakat cümlesi: "her veri aynı dayanıklılığı hak etmez.")

---

## 5. Ordering

**Fonksiyonel gereksinimler**
- FR-5.1: Checkout ile sipariş oluşur; yaşam döngüsü state machine ile yönetilir:
  `Created → StockReserved → PaymentPending → Paid → Shipped` / `Cancelled` / `Expired`.
- FR-5.2: Geçersiz geçişler (örn. `Cancelled → Paid`) domain seviyesinde engellenir.
- FR-5.3: Sipariş, ürün adı/fiyatını *snapshot* olarak kendi tablosunda saklar.
- FR-5.4: Aynı checkout isteğinin tekrarı (çift tık, retry) ikinci sipariş yaratmaz — client tarafı idempotency key.
- FR-5.5: `PaymentPending` durumunda TTL dolarsa sipariş `Expired` olur, stok geri bırakılır.

**Fonksiyonel olmayan gereksinimler**
- NFR-5.1 (Latency): Checkout zinciri (rezervasyon + sipariş yaratma) p95 < 500 ms.
- NFR-5.2 (Consistency): Sipariş durumu ile yayınlanan event'ler atomik — Outbox pattern zorunlu.
- NFR-5.3 (Auditability): Her state geçişi kim/ne zaman/hangi event ile izlenebilir.
- NFR-5.4 (CAP): CP — sipariş durumunda belirsizlik kabul edilmez.

---

## 6. Payment

**Fonksiyonel gereksinimler**
- FR-6.1: Sipariş için ödeme başlatılır; sahte PSP (payment service provider) simülasyonu ile konuşur.
- FR-6.2: Her ödeme isteği idempotency key taşır — aynı key ile ikinci istek, ilk sonucun kopyasını döner (double-charge imkansız).
- FR-6.3: PSP yanıtları: success / fail / timeout senaryoları simüle edilebilir (test edilebilirlik için).
- FR-6.4: Sonuç `PaymentCompleted` / `PaymentFailed` event'i olarak yayınlanır.
- FR-6.5: Ödeme stratejileri (kart / cüzdan / havale simülasyonu) Strategy pattern ile takılıp çıkarılabilir.

**Fonksiyonel olmayan gereksinimler**
- NFR-6.1 (Resiliency): PSP çağrısı Polly ile korunur — retry (exponential backoff + jitter), circuit breaker, timeout, bulkhead. PSP %100 çökse bile platformun geri kalanı (browse, sepet) etkilenmez.
- NFR-6.2 (Latency): Ödeme p95 < 300 ms (PSP simülasyon gecikmesi hariç); timeout üst sınırı 3 sn.
- NFR-6.3 (CAP): Kesin CP + exactly-once *etkisi* (at-least-once delivery + idempotent handler).
- NFR-6.4 (Auditability): Her ödeme denemesi, PSP yanıtı dahil, değiştirilemez log olarak saklanır.

---

## 7. Shipping

**Fonksiyonel gereksinimler**
- FR-7.1: `PaymentCompleted` event'i ile kargo kaydı otomatik açılır.
- FR-7.2: Kargo durumu simüle edilir: `Preparing → Shipped → Delivered`.
- FR-7.3: Kullanıcı sipariş üzerinden kargo durumunu sorgulayabilir.
- FR-7.4: Durum değişimleri `ShipmentStatusChanged` event'i yayınlar (Notification dinler).

**Fonksiyonel olmayan gereksinimler**
- NFR-7.1 (Latency): Durum sorgusu p95 < 100 ms.
- NFR-7.2 (CAP): AP — kargo durumu birkaç saniye bayat olabilir.
- NFR-7.3 (Decoupling): Shipping, Ordering'in iç durumunu bilmez; sadece event tüketir.

---

## 8. Notification

**Fonksiyonel gereksinimler**
- FR-8.1: Sipariş onayı, ödeme sonucu, kargo güncellemesi için e-posta/webhook simülasyonu.
- FR-8.2: Sadece event tüketir; hiçbir iş kararı vermez, hiçbir modül Notification'a senkron bağımlı değildir.
- FR-8.3: Gönderim başarısızsa retry kuyruğuna düşer (dead-letter dahil).

**Fonksiyonel olmayan gereksinimler**
- NFR-8.1 (Delivery): At-least-once + idempotent tüketici (aynı mailin 2 kez işlenmesi 2 mail üretmez).
- NFR-8.2 (Isolation): Notification tamamen çökse sistemin hiçbir kritik akışı etkilenmez.
- NFR-8.3 (CAP): AP.

---

## 9. Çapraz kesen NFR'ler (tüm modüller)

| Alan | Gereksinim |
|---|---|
| **Observability** | Structured logging (Serilog), correlation id tüm modül geçişlerinde taşınır; health check endpoint'leri modül bazında |
| **Test** | Modül başına unit + Testcontainers ile integration; NetArchTest ile mimari sınır testleri; K6 ile yük senaryoları |
| **Modülerlik** | Modüller arası referans sadece `*.Contracts`; şemalar arası SQL erişimi yok — CI'da mimari testle doğrulanır |
| **Evolvability** | Herhangi bir modül, Contracts arayüzü HTTP/gRPC'ye, in-process event bus RabbitMQ'ya çevrilerek ayrı servise çıkarılabilir olmalı |
| **Deployment** | Tek deployable; Docker Compose (Postgres, Redis, RabbitMQ) ile tek komutla ayağa kalkar |

---

## 10. Kanıt yükümlülükleri (definition of done)

Bu proje "çalışıyor" demekle bitmez; iddialar ölçümle kanıtlanır:

1. **Oversell kanıtı:** K6 senaryosu — naive kod ile oversell sayısı raporlanır → optimistic concurrency → distributed lock; üç sonucun karşılaştırma tablosu README'de.
2. **Idempotency kanıtı:** Aynı idempotency key ile 100 paralel ödeme isteği → tek charge.
3. **Resiliency kanıtı:** PSP %50 hata oranıyla simüle edilirken circuit breaker'ın açılıp kapanması loglarla gösterilir.
4. **Sınır kanıtı:** Mimari testler CI'da koşar; ihlal build'i kırar.
