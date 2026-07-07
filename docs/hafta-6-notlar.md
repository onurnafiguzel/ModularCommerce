# Hafta 6 Karar Notları — Ordering: Checkout + State Machine + İlk Modüller Arası Senkron Çağrı

> Roadmap Hafta 6 çıktısı: **uçtan uca checkout → sipariş Created** (kanıt aşağıda; sipariş
> zincir tamamlandığı için StockReserved'da persist edilir, Created doğuşu history + event'te).
> Bu hafta Contracts projeleri İLK KEZ doldu — modüller arası senkron çağrının haftası.

## Alınan kararlar

1. **Contracts artık yalnızca Shared.Kernel'e referans verebilir** (Result/Error sözleşmenin
   parçası); modül iç katmanlarına referans **yeni 4. mimari testle** (`Contracts_should_be_self_contained`)
   yasaklandı. Gerekçe: Contracts→Domain sızıntısı kural 1'i TÜKETİCİ modül üzerinden transitive
   kırar ve hata yanıltıcı olur — bu test ihlali kendi adıyla gösterir. CLAUDE.md güncellendi.
2. **Üç sözleşme yüzeyi açıldı** (hepsi "ilk talep eden: Ordering checkout"):
   - `Inventory.Contracts.IStockReservationService` — `ReserveAsync` (aktif stratejiye delege;
     tüketici Naive/Optimistic/RedisLock'tan habersiz) + `ReleaseAsync` (telafi).
   - `Catalog.Contracts.IProductReader.GetByIdsAsync` — fiyat/ad snapshot'ı + satılabilirlik,
     TEK batch sorgu (N+1 yok). Salt okuma olduğundan Result sarmalaması bilinçli yok.
   - `Cart.Contracts.ICartService` — `GetItemsAsync` + `ClearAsync`. Sepet checkout'un
     KAYNAĞIDIR (FR-4.3): satırları istemciden almak fiyat doğrulamasını by-pass ederdi.
   Adapter'lar modül İÇİNDE yaşar ve her modül kendi Register'ında kaydeder; composition root
   tüm Register'ları Build'den önce koştuğundan cross-module DI çözümlemesi güvenlidir.
3. **Inventory'ye minimal Release eklendi** (haftanın zorunlu ön koşulu): bugüne dek HİÇBİR kod
   rezervasyonu Active'den çıkarmıyordu → çok satırlı checkout'ta kısmi başarısızlık kalıcı stok
   sızıntısı demekti. `StockItem.Release(reservation)`: sahiplik kontrolü, Reserved düşer,
   `StockReleased` raise, **idempotent** (Released→no-op). Commit ve TTL-expire bilinçli YOK (W8/9).
4. **Release retry'ı reserve'den FARKLI bir sözleşmedir:** CP reserve yolunda "belirsizlikte
   reddet" doğruydu; release TELAFİDİR — nihayetinde başarmalı, decrement yönü oversell riski
   taşımaz. Adapter xmin çakışmasında **10 denemeye kadar jitter'lı retry** yapar (plandaki 3
   deneme, 10 paralel release testinde yetersiz çıktı: her turda en az bir kazanan olduğundan
   sonuncu N tur bekleyebilir — tavan buna göre seçildi). Tükenirse rezervasyon Active KALIR
   (kaybolmaz), iz W9 TTL süpürücüsüne.
5. **Order state machine tek geçiş tablosunda** (`AllowedTransitions`); 6 geçiş metodu ortak
   `TransitionTo`'dan geçer. FR-5.2 kanıtı: 7 durum × 6 metot **tam matris testi** (42 hücre).
   Tüm metotlar bu hafta yazıldı; runtime'da yalnız Create + MarkStockReserved kullanılıyor —
   W8/9 yalnız endpoint/tetikleyici ekleyecek.
6. **Auditability (NFR-5.3) kalıcı tabloyla:** `order_status_history` (FromStatus?, ToStatus,
   OccurredAtUtc, TriggeredBy) sipariş ile AYNI SaveChanges'te yazılır (owned → atomik, NFR-5.4).
   Domain event'leri de raise ediliyor ama dispatch W7 (outbox) — yalnız event'e yaslanmak W7'ye
   kadar hiç iz bırakmazdı. **NFR-5.2 (outbox) bu hafta TAM KARŞILANMADI — bilinçli erteleme.**
7. **Idempotency (FR-5.4) iki katmanlı:** handler ön-kontrolü hızlı yol; nihai hakem
   `(customer_id, idempotency_key)` unique index'i. Repository 23505'i YALNIZ ilgili constraint
   adıyla eşleşince `DuplicateIdempotencyKey`'e çevirir (başka unique ihlaliyle karışmaz).
   Kapsam müşteri-başına: başkasının key'i sizin isteğinizi engelleyemez (testli).
8. **İkincil boş-sepet yarışı** (en sinsi FR-5.4 kırığı): paralel kazanan siparişi yazıp SEPETİ
   TEMİZLEMİŞ olabilir — kaybeden sepeti boş görünce EmptyCart dönmeden key'i BİR KEZ DAHA
   kontrol eder. K6 burst check'i (400/409 terminal kabul edilmez) bunu yük altında sabitler.
9. **Duplicate-persist telafisi:** yarışı persist'te kaybeden, KENDİ rezervasyonlarını release
   eder, kazananı `AsNoTracking` ile çeker (zehirli change tracker'a ikinci SaveChanges yok)
   ve 200 döner. Integration testi release'leri id-bazında doğrular — stok sızıntısı yok.
10. **Dağıtık transaction YOK (bilinçli):** rezervasyonlar Inventory'nin, sipariş Ordering'in
    kendi SaveChanges'i. Reserve-başarılı → persist-başarısız penceresi try/catch-release ile
    kapatıldı; **process crash penceresi kabul edildi** — rezervasyon TTL'i + W9 süpürücüsü
    güvenlik ağı. Sipariş StockReserved'da persist edilir (Created'da persist + ayrı update
    hem yalan hem iki yazma olurdu); Created doğuşu history'nin ilk satırı + OrderCreated event'i.
11. **Para: düz kolonlar** (`UnitPrice numeric(18,2)` + `Currency char(3)`), Catalog'un Money
    VO'su TAŞINMADI — snapshot veridir, davranış değil (FR-5.3). Money'nin Shared.Kernel'e
    terfisi "ikinci tüketici" kuralıyla savunulabilirdi ama kritik yola alakasız refactor
    sokmamak için ertelendi (terfi adayı).
12. **"FluentValidation pipeline behavior" = mevcut konvansiyon** (kullanıcı kararı, MediatR yok):
    her handler IValidator enjekte eder ve İLK İŞ doğrular — beş haftadır işleyen desen Ordering'de
    de aynen sürdü. Roadmap maddesi bu konvansiyonla karşılanmıştır.
13. **Aynı scoped context'te telafi inceliği:** başarısız reserve, InventoryDbContext tracker'ında
    KAYDEDİLMEMİŞ kirli durum bırakır (artmış Reserved + eklenmiş Reservation). Release her
    denemeye `ChangeTracker.Clear()` ile başlar — kirli durum asla ikinci SaveChanges'e taşınmaz.
14. **Ordering.Contracts boş; Ordering config bölümü yok** (bilinçli): Payment (W8) talep
    edince dolar; rezervasyon TTL'i Inventory'nin, PaymentPending TTL'i W8/9'un.

## Ölçüm sonuçları (7 Temmuz 2026, 12 çekirdek, OptimisticConcurrency stratejisi)

### K6 checkout smoke — 20 VU × 30 sn, TEK sıcak ürün
| Metrik | Hedef | Sonuç |
|---|---|---|
| Checkout isteği p95 (retry'lar dahil her istek) | NFR-5.1 < 500 ms | **27 ms** ✓ |
| Başarılı sipariş | — | 3.957 (≈121 sipariş/sn) |
| Retryable 409 (ConcurrencyConflict) | tasarlanmış yanıt | 19.439 (~5 çakışma/sipariş) |
| http_req_failed | < %1 | **%0,00** ✓ |
| 30 retry'ı tüketen iterasyon | — | 18 / 3.925 (%0,46 — bulgu, aşağıda) |

### K6 idempotency burst — 5 VU × 10 iterasyon, aynı key ile 5 PARALEL checkout
| Check | Sonuç |
|---|---|
| Terminalde tam 1×201 (tek sipariş) | **50/50** ✓ |
| 4×200 replay (400/409 terminal YOK) | **50/50** ✓ (toplam 200 replay) |
| 5 yanıt da aynı sipariş kimliği | **50/50** ✓ |

**Haftanın bulgusu:** Retryable-409 istemci sözleşmesi checkout'a da taşındı — aynı Idempotency-Key
ile retry, idempotency'nin tam da var olma sebebi. Tek sıcak üründe optimistic strateji sipariş
başına ~5 çakışma üretti ve iterasyonların %0,46'sı 30 denemeyi tüketti: H4'ün "optimistic
yetersiz kalırsa RedisLock" bulgusunun checkout zincirindeki karşılığı (RedisLock'la koşmak
çakışmaları beklemeye çevirir — README reçetesindeki strateji anahtarıyla denenebilir).

### Uçtan uca kanıt (roadmap H6 çıktısı)
```
POST /api/ordering/checkout (Idempotency-Key: kanit-*)   → 201 + Location, status=StockReserved
POST aynı key ikinci kez                                  → 200 + AYNI sipariş kimliği
GET  /api/inventory/stock/{id}                            → reserved: 0 → 2 (rezervasyon gerçek)
GET  /api/ordering/orders/{id} → history:
     null->Created [checkout], Created->StockReserved [checkout]   (NFR-5.3)
GET  /api/cart                                            → 0 satır (checkout sepeti temizledi)
Negatifler: header'sız → 400 InvalidCommand; katalogda olmayan ürün → 409 ProductUnavailable;
stok 1 + adet 2 → 409 InsufficientStock ve reserved=0 (SIZINTI YOK).
```

## Bilinçli ertelemeler
| Konu | Bu hafta | Hafta |
|---|---|---|
| Outbox + event dispatch (**NFR-5.2 tam karşılanmadı**) | Event'ler raise-edilir-dispatch-edilmez; kalıcı iz history tablosunda | W7 |
| PaymentPending TTL → Expired + stok iade (FR-5.5) | Yalnız `Expire()` domain metodu (matris testli) | W9 |
| Rezervasyon Commit + TTL süpürücü (`ExpiresAtUtc` hâlâ dekoratif) | Yalnız Release | W8/9 |
| Reserve-sonrası process crash penceresi | try/catch-release yazılım hatalarını kapatır | W9 |
| MarkPaymentPending tetikleyicisi, Cancel endpoint'i | Domain hazır, endpoint yok | W8 |
| Ordering.Contracts içeriği | Boş | W8 |
| Money → Shared.Kernel terfisi | Düz kolonlar | İhtiyaç anında |

## Riskler / notlar
- **Shared'a SIFIR kod değişikliği** — üç cazibe bilinçli reddedildi: Money terfisi, idempotency
  altyapısı genelleştirmesi (Payment W8 kendi ihtiyacını getirir), release-retry yardımcısı.
- Checkout'un satır maliyeti lineerdir (rezervasyonlar sıralı; tek DbContext paralelleşemez) —
  K6 tek satırla ölçtü; 50 satırlık sepetin zinciri ~50× rezervasyon süresi taşır.
- K6/E2E ürün bootstrap'i: Catalog seed id'leri rastgele, Inventory'ninkiler sabit — kesişim yok;
  ürün katalogdan seçilir, stok dev endpoint'iyle basılır (Host Development modunda olmalı).
- Sipariş listesi sabit son 20 kayıt (sayfalama talebi gelince genişler).
