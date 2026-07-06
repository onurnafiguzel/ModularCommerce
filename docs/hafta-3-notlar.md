# Hafta 3 Karar Notları — Inventory: Korumasız Rezervasyon → Oversell Kanıtı → Optimistic Concurrency

> Roadmap Hafta 3 çıktısı: naive vs rowversion oversell karşılaştırma tablosu (README'de).
> Kanıt bu hafta K6 yerine repo'nun İLK Testcontainers integration testinden üretildi (bilinçli karar, aşağıda).

## Alınan kararlar

1. **Domain modeli:** `StockItem` aggregate (ProductId, OnHand, `Reserved` **sayacı**;
   `Available = OnHand − Reserved` türetilmiş) + `Reservation` ayrı, navigation'sız insert-only entity
   (Status: Active/Committed/Released/Expired, `ExpiresAtUtc = +5 dk`, FR-3.2). Sayaç, tek
   `stock_items` satırını serileşme noktası yapar — concurrency token tam bu satırda anlam kazanır.
   `Reservation` yalnızca `StockItem.Reserve()` içinden yaratılır (internal factory).
2. **Strateji deseni:** `IReservationStrategy` port'u Application'da; `Naive` ve
   `OptimisticConcurrency` adapter'ları Infrastructure'da. Seçim `Inventory:ReservationStrategy`
   config'inden kayıt anında; bilinmeyen değer startup'ta patlar. Hafta 4 aynı switch'e
   `RedisLock` ekleyecek — üç strateji aynı endpoint/handler ile ölçülür.
3. **Concurrency token: Postgres `xmin` sistem kolonu** (`Property<uint>("xmin").IsRowVersion()`,
   shadow property). Şemaya kolon eklemez, domain'e alan sızdırmaz. Çakışma
   `DbUpdateConcurrencyException` → strateji içinde 409 `Inventory.ConcurrencyConflict`
   ("tekrar deneyin") Result'ına çevrilir.
4. **Naive'in dürüstlüğü (kritik mekanizma):** token hep map'li olmasına rağmen naive yol
   yapısal olarak korumasız: StockItem `AsNoTracking` yüklenir (EF UPDATE üretmez → token devreye
   giremez), sayaç korumasız raw UPDATE ile artırılır (WHERE'de ne xmin ne stok kontrolü),
   SaveChanges yalnızca Reservation INSERT eder. `Reservation`'da StockItem navigation'ı BİLEREK yok.
   **`Reserved <= OnHand` CHECK constraint'i BİLEREK yok** — DB koruması naive'i gizlice korur,
   demoyu geçersiz kılar (configuration dosyasında uyarı yorumu var).
5. **Sunucu tarafı retry yok:** NFR-3.4 (kesin CP: belirsizlikte reddet) + NFR-3.3 (retry fırtınası
   fairness'ı bozar). İki ayırt edilebilir 409: `ConcurrencyConflict` (retryable) vs
   `InsufficientStock` (terminal) — istemci ProblemDetails `title`'dan ayırt eder.
6. **Yetersiz stok = 409 Conflict** (400 değil): istek iyi biçimli, kaynak durumuyla çakışıyor;
   mevcut `ErrorType.Conflict → 409` eşlemesi sıfır Shared değişikliğiyle kullanıldı.
7. **Sabit seed GUID'leri:** `1111...` (oversell hedefi, OnHand 10), `2222...` (yük hedefi, 1M),
   `3333...` (manuel, 100). Catalog'un random ürün id'lerinden bilinçli bağımsız — gerçek eşleme
   Ordering ile (Hafta 6). Dev-only `PUT /api/inventory/dev/stock/{productId}` reset endpoint'i
   deterministik test başlangıcı sağlar (yalnız Development).

## Kanıt: 100 paralel istek, 10 stok (Testcontainers + gerçek PostgreSQL)

Test: `tests/ModularCommerce.Inventory.IntegrationTests/ReservationConcurrencyTests.cs`
(start-gate ile eşzamanlı bırakılan 100 görev; 5 Temmuz 2026 koşusu):

| Strateji | Deneme | "Başarılı" | OnHand | Reserved | Oversell | Çakışma-retry |
|---|---|---|---|---|---|---|
| Naive | 100 | 100 | 10 | 100 | **90** | — |
| OptimisticConcurrency (xmin) | 100 | **10** | 10 | 10 | **0** | 207 |

Manuel doğrulama (Host + Postgres):
- `POST /api/inventory/reservations` → 201 + Location + `status=Active`, `expiresAtUtc=+5dk`
- 10 stok tükendikten sonraki istek → 409 `Inventory.InsufficientStock` (problem+json)
- `quantity=0` → 400; `GET /stock/{id}` onHand/reserved/available doğru; dev reset çalışıyor

## Bilinçli ertelemeler

| Konu | Neden |
|---|---|
| **K6 yük testi altyapısı (kullanıcı kararı)** | Oversell kanıtı Testcontainers ile `dotnet test`'te tekrarlanabilir üretildi; 1000 VU + p95 (NFR-3.2) ölçümü K6 ile ileriki haftada. Sabit GUID'ler + dev reset endpoint'i K6'ya hazır |
| Rezervasyon TTL süpürme job'ı + commit/release compensation | Roadmap Hafta 9; `ExpiresAtUtc`/`Status` alanları şimdiden yazılıyor |
| Redis distributed lock | Roadmap Hafta 4 — aynı strateji switch'ine üçüncü değer |
| `ProductSoldOut`/`StockReserved` dispatch'i | Outbox Hafta 7; şimdilik yalnızca raise (Catalog `ProductCreated` emsali) |
| `Inventory.Contracts` yüzeyi | Hafta 6 — şekli tüketici (Ordering) belirler |
| Kampanya stoğu | Kampanya özelliğiyle birlikte |

## Riskler / notlar

- Naive oversell testi doğası gereği nondeterministik; start-gate + 100 paralel görev ile pratikte
  her turda tetikleniyor (3 tur güvencesi var). İlk koşuda 1. turda tetiklendi.
- Integration testleri Docker Desktop ister; `[Trait("Category","Integration")]` ile filtrelenebilir:
  `dotnet test --filter "Category!=Integration"`.
- `dotnet ef migrations add` önce derler sonra dosya üretir — migration sonrası Host'u asla
  `--no-build` ile çalıştırma (Hafta 2 dersi).
