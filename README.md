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

### Oversell kanıtı: Naive vs Optimistic Concurrency (Hafta 3)

10 stokluk ürüne, start-gate ile **aynı anda** bırakılan 100 paralel rezervasyon isteği
(Testcontainers + gerçek PostgreSQL; test: `ReservationConcurrencyTests`):

| Strateji | Deneme | "Başarılı" | OnHand | Reserved | **Oversell** | Çakışma (retry) |
|---|---|---|---|---|---|---|
| Naive (korumasız check-then-act) | 100 | **100** | 10 | 100 | **90** | — |
| Optimistic concurrency (xmin) | 100 | **10** | 10 | 10 | **0** | 207 → istemci retry'ı ile tam 10 |

- Naive yol, domain'deki `Available` kontrolünü **bayat snapshot** üzerinde yapar: 100 istek
  aynı anda `Available=10` okur, hepsi geçer, hepsi yazar. İş kuralı doğru yerde, ama eşzamanlılık
  koruması yok — ders bu.
- Optimistic yol aynı domain kuralını çalıştırır; farkı `UPDATE ... WHERE xmin = @token`
  koşuludur. Kaybeden istek 409 `Inventory.ConcurrencyConflict` ("tekrar deneyin") alır —
  sunucu retry YAPMAZ (kesin CP, NFR-3.4); istemci retry'ı ile sonuç **tam 10**.
- Tekrarlamak için: Docker Desktop açıkken
  `dotnet test tests/ModularCommerce.Inventory.IntegrationTests`.
- Üçüncü strateji (Redis distributed lock) ve K6 yük senaryoları (1000 VU + p95) sonraki
  haftalarda aynı tabloya eklenecek.

## Bilinçli ertelemeler (evolution path)

- API Gateway yok: tek deployable'da middleware pipeline aynı işi görür.
  Bir modül servise ayrıldığında Host, YARP ile proxy'ye evrilir.
- Elasticsearch yok: arama Postgres full-text ile başlar; ölçek gerektirdiğinde CDC + ES yolu açık.
- Virtual waiting room yok: kampanya yükü önce rate limiting + rezervasyon TTL ile yönetilir.
- Identity şeması, compliance gereksinimi doğduğunda sıfır kod değişikliğiyle ayrı DB'ye taşınabilir.
