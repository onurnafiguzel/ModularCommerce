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

## Sonuç okuma

- `successful_reservations` — 201 sayısı (Optimistic/RedisLock'ta tam 10 olmalı; Naive'de >>10)
- `concurrency_conflicts` — 409 `Inventory.ConcurrencyConflict` (optimistic'in retry'ları)
- `lock_timeouts` — 409 `Inventory.LockTimeout` (redis lock'ın bekleme bütçesi dolanları)
- `sold_out_rejections` — 409 `Inventory.InsufficientStock` (terminal)
- Teardown satırı gerçek DB durumunu basar: `OVERSELL=n` (Naive'de >0, diğerlerinde 0)
- Smoke: `http_req_duration p(95)<150` threshold'u geçmeli

Sonuçlar kök `README.md`'deki karşılaştırma tablosuna işlenir.
