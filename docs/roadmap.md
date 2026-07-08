# ModularCommerce — 12 Haftalık Yol Haritası

> İlke: **Cross-cutting altyapı, onu ilk talep eden feature ile birlikte gelir; asla önce gelmez.**
> Middleware, JWT, resiliency gibi bileşenler kendi başına değer üretmez; bir feature'ın
> ihtiyacı olarak geldiklerinde hem doğru tasarlanır hem de commit geçmişinde
> "neden var oldukları" görünür.

| Hafta | Modül / Feature | Beraberinde gelen cross-cutting | Kanıt / Çıktı |
|---|---|---|---|
| 1 ✅ | İskelet, Host, `IModule` altyapısı | Central Package Management, NetArchTest mimari sınır testleri | Çalışan health endpoint'leri |
| 2 ✅ | **Catalog**: ürün listeleme/detay, seed data | EF Core + şema/modül deseni, global exception handler + Problem Details | Postman'de gerçek ürün JSON'ı |
| 3 ✅ (K6 hariç) | **Inventory**: stok + bilerek korumasız rezervasyon → oversell kanıtı → optimistic concurrency (xmin) | Testcontainers ile ilk integration test; K6 altyapısı bilinçli ertelendi (kanıt integration testinden) | Naive vs rowversion karşılaştırma tablosu (README) → **LinkedIn post #1** |
| 4 ✅ | **Inventory**: Redis distributed lock + üç stratejinin karşılaştırması | Redis bağlantısı, correlation id + Serilog structured logging | Üç stratejinin K6 karşılaştırma tablosu (README'ye) → **post #2** |
| 5 ✅ | **Identity**: signup / login / JWT + **Cart** (Redis tabanlı sepet) | JWT authentication middleware, endpoint authorization | Login olup sepete ürün ekleme akışı |
| 6 ✅ | **Ordering**: checkout, sipariş state machine, `Inventory.Contracts` üzerinden rezervasyon | İlk modüller arası senkron çağrı, FluentValidation pipeline behavior | Uçtan uca checkout → sipariş `Created` |
| 7 ✅ | **Payment senkron checkout zincirinde**: sahte PSP (`Payment.Contracts.IPaymentService`), ödeme başarılı → sipariş `Paid` + stok commit; başarısız → rezervasyon release + kullanıcıya hata (tek istek, iki adım YOK) | Resiliency pipeline (Polly: retry, circuit breaker, timeout) — ilk "dış" çağrı burada doğuyor; rezervasyon `Commit` primitifi | 100 paralel istek → tek charge; ödeme reddi → reserved=0 (sızıntı yok) → **post #3 (idempotency)** |
| 8 ✅ | **Outbox + event bus**: `OrderPaid` event'i, RabbitMQ+MassTransit entegrasyonu | Outbox dispatcher background service | Event'in RabbitMQ management UI'da görünmesi → **post #4 (outbox pattern)** |
| 9 | **Compensation + TTL**: reserve-sonrası crash penceresi için rezervasyon TTL → `Expired` süpürücü; sipariş iptali (`Cancel`) + stok iade | Rezervasyon TTL background job | Crash/iptal senaryosunun log ve test kanıtı |
| 10 | **Shipping + Notification**: event tüketicileri (`OrderPaid` dinler) | Idempotent consumer, dead-letter queue | Tam akış: checkout+ödeme (tek istek) → kargo → bildirim logu |
| 11 | Uçtan uca sertleştirme | Rate limiting, health checks, OpenTelemetry, K6 full checkout senaryosu | Kampanya (flash sale) simülasyon raporu → **post #5** |
| 12 | **Azure**: Container Apps + managed Postgres/Redis/Service Bus + CI/CD | GitHub Actions (mimari testler pipeline'da koşar) | Canlı URL — CV'ye giren link |

## Okuma notları

1. **Sıralama risk odaklı:** Projenin en değerli hikayesi (race condition kanıtı) 3. haftada
   geliyor, 8. haftada değil. Motivasyonun en yüksek olduğu dönemde en gösterişli iş yapılıyor;
   proje yarıda kalsa bile elde satılabilir bir hikaye oluyor.
2. **Her LinkedIn postu bir haftanın doğal atığı** — içerik üretmek için ekstra iş yok.
3. **Hafta kayabilir, sıra bozulmaz:** Her hafta bir öncekinin çıktısına yaslanıyor.
4. **Günlük kontrol sorusu (21.30):** Bugün commit attım mı? Outreach mesajı attım mı?
5. **Tasarım revizyonu (Hafta 6 sonrası):** Ödeme, event ile tetiklenen ayrı bir adım değil,
   checkout isteğinin İÇİNDE senkron alınır (ordering + payment tek process). Başarılı ödeme
   siparişi ve stok düşümünü aynı zincirde kesinleştirir; başarısız ödeme rezervasyonları
   release edip kullanıcıya hatayı anında döner. Sonuçları: eski "saga: PaymentFailed → stok
   iade" haftası eridi (telafi artık senkron try/catch-release); event bus'ın ilk olgusu
   `OrderCreated` değil `OrderPaid`; NFR-5.1 latency bütçesine PSP çağrısı dahil oldu.

## Bilinçli ertelemeler

API Gateway, Elasticsearch + CDC, virtual waiting room, GraphQL/BFF katmanı ve Identity'nin
ayrı veritabanına taşınması bilinçli olarak kapsam dışıdır; gerekçeleri ve evrim yolları
[README](../README.md) ve [requirements](requirements.md) dokümanlarındadır.