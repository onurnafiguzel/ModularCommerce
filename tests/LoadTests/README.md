# K6 Yük Testleri

## Ön koşullar

```powershell
winget install k6.k6              # veya: choco install k6
docker compose up -d postgres redis
```

Host **Development** modunda çalışmalı (dev-only stok reset endpoint'i kullanılır)
ve HTTP portu hedeflenir (49822 — self-signed TLS derdi yok).

## Sabit seed ürünleri

| ProductId | OnHand | Amaç |
|---|---|---|
| `11111111-1111-1111-1111-111111111111` | 10 | Oversell senaryosu (NFR-3.1) |
| `22222222-2222-2222-2222-222222222222` | 1.000.000 | Smoke/p95 senaryosu (NFR-3.2) |
| `33333333-3333-3333-3333-333333333333` | 100 | Manuel test |

## Üç stratejinin karşılaştırma koşusu

Strateji kayıt anında seçilir → her koşu arasında Host restart gerekir
(bilinçli tasarım: karşılaştırmayı dürüst tutar).

```powershell
# 1) Naive (oversell BEKLENİR — hikayenin "kötü" tarafı)
$env:Inventory__ReservationStrategy = 'Naive'
dotnet run --project src/Bootstrapper/ModularCommerce.Host   # ayrı terminalde
k6 run tests/LoadTests/scenarios/inventory-oversell.js

# 2) Optimistic concurrency (xmin) — STRICT: tam ≤10 başarı threshold'u
$env:Inventory__ReservationStrategy = 'OptimisticConcurrency'  # Host'u yeniden başlat
k6 run -e STRICT=1 tests/LoadTests/scenarios/inventory-oversell.js

# 3) Redis distributed lock — STRICT + çakışma yerine kilit bekleme beklenir
$env:Inventory__ReservationStrategy = 'RedisLock'              # Host'u yeniden başlat
k6 run -e STRICT=1 tests/LoadTests/scenarios/inventory-oversell.js

# p95 ölçümü (NFR-3.2, kilit bekleme dahil) — RedisLock aktifken
k6 run tests/LoadTests/scenarios/inventory-smoke.js
```

## Identity + Cart smoke koşuları (Hafta 5)

Strateji seçimi gerektirmez; Host default ayarlarla çalışırken koşulur.
Seeder yoktur: script'ler setup'ta kendi kullanıcılarını signup ile yaratır.

```powershell
# Login dalgası (NFR-1.1 p95<200ms, NFR-1.4 200 RPS) — PBKDF2 doğrulaması dahil
k6 run tests/LoadTests/scenarios/identity-login-smoke.js

# Sepet okuma/yazma (NFR-4.1 p95<50ms) — JWT doğrulama + Redis dahil.
# Her VU kendi hesabı/sepetiyle koşar (tek sepette last-write-wins yarışını
# ölçüme karıştırmamak için — bkz. script başındaki not).
k6 run tests/LoadTests/scenarios/cart-smoke.js
```

## Checkout smoke koşusu (Hafta 6, Hafta 7'de ödeme zinciriyle güncellendi)

Üç senaryo tek script'te: gecikme (NFR-5.1 p95<500ms — artık ödeme + stok commit
dahil) + idempotency burst (5 paralel aynı key → tam 1×201 + 4×200) +
payment_idempotency_100 (kanıt §10.2: 100 paralel aynı key → tek sipariş + Payment
dev endpoint'inde TEK Completed charge). Ürün setup'ta katalogdan seçilir, stok
dev endpoint'iyle basılır.

NFR-6.2 notu: PSP simülasyon gecikmesi p95 ölçümüne dahil edilmez — Host'u
`Payment__Psp__LatencyMs=0` ile başlatın.

```powershell
$env:Payment__Psp__LatencyMs = '0'
dotnet run --project src/Bootstrapper/ModularCommerce.Host   # ayrı terminalde
k6 run tests/LoadTests/scenarios/checkout-smoke.js
```

## Payment resiliency koşuları (Hafta 7)

Host'un PSP env ayarına göre iki AYRI koşu (Host restart gerekir — strateji
karşılaştırma reçetesiyle aynı disiplin):

```powershell
# 1) Circuit breaker kanıtı (§10.3): %50 transient hata altında breaker'ın
#    açılıp kapandığı Host'un Serilog çıktısında görülür:
#    "Resilience event occurred. EventName: 'OnCircuitOpened' ..." / 'OnCircuitClosed'
$env:Payment__Psp__FailureRate = '0.5'
dotnet run --project src/Bootstrapper/ModularCommerce.Host   # ayrı terminalde
k6 run -e MODE=breaker tests/LoadTests/scenarios/payment-resiliency.js
# k6 tarafında psp_unavailable_409 sayacı = breaker açıkken PSP'ye GİTMEDEN
# hızlı reddedilen istekler.

# 2) "Ödeme reddi → sızıntı yok" kanıtı: her checkout'un terminali 409
#    Payment.Declined, sipariş yazılmaz, koşu sonunda reserved=0 (teardown check'i).
$env:Payment__Psp__FailureRate = '0'
$env:Payment__Psp__DeclineRate = '1'                          # Host'u yeniden başlat
k6 run -e MODE=declined tests/LoadTests/scenarios/payment-resiliency.js
```

Retryable/terminal 409 sözleşmesi: `Inventory.ConcurrencyConflict`,
`Inventory.LockTimeout`, `Payment.InFlight`, `Payment.PspUnavailable` → AYNI
Idempotency-Key ile tekrar dene; `Payment.Declined`, `Payment.Timeout` →
TERMİNAL, aynı key kopyayı döner (FR-6.2), yeni deneme yeni key ister.

## Flash-sale koşusu (Hafta 11 — sertleştirme kanıtı)

Düşük stoklu **sıcak** ürüne rampalı (ramping-arrival-rate) checkout yükü; asıl kanıt **oversell=0**
(commit edilen adet başlangıç stoğunu aşmaz → `onHand` negatife düşmez). Yanıt karışımı: 201 satış +
409 `InsufficientStock` (stok bitti, doğru) + 429 (rate limit) + retryable-409.

**ÖN KOŞUL:** yük üreteci tek IP'den yüzlerce signup yaptığından `auth` rate limiti (IP bazlı 10/60s)
senaryonun kendi kullanıcı üretimini boğar. Bu senaryo checkout **oversell**'ini ölçer, auth
throttle'ını değil — Host'u auth limiti **gevşetilmiş** başlatın (auth throttle'ı README kök kanıt
bölümündeki login demo'su ayrıca gösterir):

```powershell
$env:RateLimiting__Auth__PermitLimit = '100000'
$env:RateLimiting__Auth__WindowSeconds = '1'
$env:Payment__Psp__LatencyMs = '0'
dotnet run --project src/Bootstrapper/ModularCommerce.Host   # ayrı terminalde
k6 run tests/LoadTests/scenarios/flash-sale.js
# İsteğe bağlı: -e HOT_STOCK=50 -e MAX_VUS=600
```

Beklenen teardown: `FLASH SALE SONUÇ: başlangıç=50 onHand=0 ... SATILAN=50 OVERSELL=0`.
Threshold `http_req_duration{endpoint:checkout} p(95)<500` (NFR-5.1) geçmeli; oversell tespit edilirse
teardown testi abort eder.

## Sonuç okuma

- `successful_reservations` — 201 sayısı (Optimistic/RedisLock'ta tam 10 olmalı; Naive'de >>10)
- `concurrency_conflicts` — 409 `Inventory.ConcurrencyConflict` (optimistic'in retry'ları)
- `lock_timeouts` — 409 `Inventory.LockTimeout` (redis lock'ın bekleme bütçesi dolanları)
- `sold_out_rejections` — 409 `Inventory.InsufficientStock` (terminal)
- Teardown satırı gerçek DB durumunu basar: `OVERSELL=n` (Naive'de >0, diğerlerinde 0)
- Smoke: `http_req_duration p(95)<150` threshold'u geçmeli

Sonuçlar kök `README.md`'deki karşılaştırma tablosuna işlenir.
