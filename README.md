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

## Bilinçli ertelemeler (evolution path)

- API Gateway yok: tek deployable'da middleware pipeline aynı işi görür.
  Bir modül servise ayrıldığında Host, YARP ile proxy'ye evrilir.
- Elasticsearch yok: arama Postgres full-text ile başlar; ölçek gerektirdiğinde CDC + ES yolu açık.
- Virtual waiting room yok: kampanya yükü önce rate limiting + rezervasyon TTL ile yönetilir.
- Identity şeması, compliance gereksinimi doğduğunda sıfır kod değişikliğiyle ayrı DB'ye taşınabilir.
