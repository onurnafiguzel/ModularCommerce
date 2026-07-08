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
| 7 | **Outbox + event bus**: `OrderCreated` event'i, RabbitMQ+Masstransit entegrasyonu | Outbox dispatcher background service | Event'in RabbitMQ management UI'da görünmesi → **post #3 (outbox pattern)** |
| 8 | **Payment**: idempotency key + sahte PSP + Polly | Resiliency pipeline (retry, circuit breaker, timeout) — ilk dış çağrı burada doğuyor | 100 paralel istek → tek charge kanıtı → **post #4 (idempotency)** |
| 9 | **Saga / compensation**: `PaymentFailed` → stok iade, rezervasyon TTL → `Expired` | Rezervasyon TTL background job | Fail senaryosunun log ve test kanıtı |
| 10 | **Shipping + Notification**: event tüketicileri | Idempotent consumer, dead-letter queue | Tam akış: checkout → ödeme → kargo → bildirim logu |
| 11 | Uçtan uca sertleştirme | Rate limiting, health checks, OpenTelemetry, K6 full checkout senaryosu | Kampanya (flash sale) simülasyon raporu → **post #5** |
| 12 | **Azure**: Container Apps + managed Postgres/Redis/Service Bus + CI/CD | GitHub Actions (mimari testler pipeline'da koşar) | Canlı URL — CV'ye giren link |

## Okuma notları

1. **Sıralama risk odaklı:** Projenin en değerli hikayesi (race condition kanıtı) 3. haftada
   geliyor, 8. haftada değil. Motivasyonun en yüksek olduğu dönemde en gösterişli iş yapılıyor;
   proje yarıda kalsa bile elde satılabilir bir hikaye oluyor.
2. **Her LinkedIn postu bir haftanın doğal atığı** — içerik üretmek için ekstra iş yok.
3. **Hafta kayabilir, sıra bozulmaz:** Her hafta bir öncekinin çıktısına yaslanıyor.
4. **Günlük kontrol sorusu (21.30):** Bugün commit attım mı? Outreach mesajı attım mı?

## Bilinçli ertelemeler

API Gateway, Elasticsearch + CDC, virtual waiting room, GraphQL/BFF katmanı ve Identity'nin
ayrı veritabanına taşınması bilinçli olarak kapsam dışıdır; gerekçeleri ve evrim yolları
[README](../README.md) ve [requirements](requirements.md) dokümanlarındadır.