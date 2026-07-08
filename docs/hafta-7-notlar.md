# Hafta 7 Karar Notları — Payment: Senkron Ödeme + Resiliency + Stok Commit

> Roadmap Hafta 7 çıktısı (revize plan): **ödeme checkout isteğinin İÇİNDE senkron alınır** —
> başarılı ödeme siparişi `Paid` yapar ve stoğu kalıcı düşürür (Commit); başarısız ödeme
> rezervasyonları release edip kullanıcıya hatayı anında döner. Tek istek, iki adım YOK.
> Kanıtlar: **100 paralel istek → tek charge** (§10.2) ve **circuit breaker open/close
> logları** (§10.3) — ikisi de aşağıda ölçümle.

## Yeni checkout zinciri (tek istek)

```
key ön-kontrol → sepet → catalog → rezervasyon(lar)
  → Order.Create → MarkStockReserved → MarkPaymentPending      (bellekte)
  → IPaymentService.ChargeAsync(...)          [1. hakem: payments unique index → TEK charge]
      başarısız → ReleaseAll → hata (sipariş HİÇ persist edilmez)
      başarılı  → MarkPaid → orders.AddAsync  [2. hakem: orders unique index → TEK sipariş]
          duplicate → ReleaseAll(kendi rez.) → kazananı replay (commit YOK)
          başarılı  → CommitAllAsync (best-effort, CancellationToken.None)
  → sepet temizliği (best-effort) → 201 (status=Paid, history 4 satır)
```

**İki hakem işbölümü:** Payment'ın `(customer_id, idempotency_key)` unique index'i
double-charge'ı imkansız kılar (FR-6.2); Ordering'in aynı şekilli index'i çift siparişi
imkansız kılar (FR-5.4). Charge, persist'ten önce yapıldığından Payment hakemi önce konuşur;
charge kazananı ile order kazananı FARKLI istekler olabilir — sorun değil, ödeme sonucu
kimlikli ve replay'lenebilirdir.

## Alınan kararlar

1. **İşlem sırası: reserve → charge → persist(Paid) → commit.** Sipariş yalnız ödeme
   başarılıysa, TEK SaveChanges ile Paid durumunda doğar (PaymentPending'den bellekte transit
   geçilir — history 4 satırla dürüst kalır, geçiş matrisi bozulmaz). "Önce persist sonra
   öde" iki yazma + yarım sipariş + replay kilidi demekti; reddedildi.
2. **Payment idempotency key = checkout `Idempotency-Key`, müşteri kapsamlı.** Repository'nin
   23505 + `ConstraintName == ix_payments_customer_id_idempotency_key` eşleşmesi OrderRepository
   deseninin birebir kopyası. Farklı müşteri aynı key'i kullanabilir (testli).
3. **Kalıcılık modeli: tek `payments` satırı (Pending → Completed/Failed) + append-only
   `payment_attempts` owned tablosu (NFR-6.4).** Her PSP çağrı denemesi — Polly retry'ları
   dahil — outcome/psp_tx/error_code/latency_ms ile ayrı satır; attempts asla güncellenmez.
   Enum'lar string tutuldu: audit tablosu insan gözüyle okunmalı.
4. **Timeout = terminal Failed.** Pipeline (retry'lar dahil) tükenirse satır
   `Failed("timeout")` finalize edilir; aynı key kopyayı döner. Sahte PSP'de takılan çağrı
   gerçek charge üretmediği için bu DÜRÜSTTÜR; gerçek PSP'de bu pencere "charge olmuş
   olabilir" demektir → status-query reconciliation W9+ notu.
5. **`PspUnavailable` finalize EDİLMEZ — Pending satır silinir.** Breaker açıkken/transient
   tükenince PSP'ye ulaşılamamıştır (charge belirsizliği yok); satır Failed yazılsaydı replay,
   retryable ilan ettiğimiz hatayı terminale kilitlerdi. Bedeli: hiç başarmamış transient
   denemelerin audit'i kaybolur (başarılı ödemenin retry hikayesi attempts'te ZATEN kalıcı) —
   gerçek PSP entegrasyonunda bu satır silinmez, reconciliation'a devredilir (W9+).
6. **Eşzamanlı aynı-key charge — üç kaybeden yolu:** terminal satır → kopyası döner (declined
   DAHİL, FR-6.2); taze Pending → `Payment.InFlight` 409 (retryable — istemci aynı key ile
   döner); bayat Pending (`StalePendingSeconds=30` geçmiş) → **xmin korumalı takeover**: satır
   `Reclaim()` ile tazelenir (rakipler InFlight görür), devralmayı kaybeden
   DbUpdateConcurrencyException ile InFlight'a düşer. Takeover olmadan "Pending + crash" o
   key'i sonsuza dek kilitlerdi (P3 penceresi).
7. **Retryable/terminal 409 sözleşmesi genişledi:** retryable set artık
   `Inventory.ConcurrencyConflict`, `Inventory.LockTimeout`, `Payment.InFlight`,
   `Payment.PspUnavailable`; terminal: `Payment.Declined`, `Payment.Timeout`,
   `Payment.AmountMismatch` (aynı key kopyayı döner — yeni deneme YENİ key ister).
   `ResultExtensions`'a dokunulmadı: declined 402 değil 409 döner (Shared'a sıfır dokunma).
8. **Polly: `Microsoft.Extensions.Resilience` 10.0.0 merkeze eklendi** (Http.Resilience
   KULLANILMADI — PSP in-process, HttpClient yok; sahte PSP'yi sırf paket için HTTP arkasına
   koymak ölçümü kirletirdi). `AddResiliencePipeline("payment-psp")` + `AddResilienceEnricher`
   → breaker open/close logları Serilog'a bedava geldi (§10.3 kanıtının kaynağı).
9. **Pipeline bileşimi (dıştan içe):** toplam timeout 3 sn (NFR-6.2 üst sınırı) → retry
   (max 3, exponential backoff + jitter — YALNIZ `PspTransientException`/`TimeoutRejected`;
   **declined İŞ sonucudur, retry edilmez**) → circuit breaker (FailureRatio 0.5, Sampling
   10 sn, MinThroughput 8, Break 5 sn) → concurrency limiter 20/20 (bulkhead) → deneme-başı
   timeout 1 sn. NFR-6.1'in saydığı dört mekanizmanın dördü de var.
10. **Sahte PSP (FR-6.3):** `Payment:Psp` config bölümü — LatencyMs, DeclineRate, FailureRate
    (transient), TimeoutRate, StalePendingSeconds; oranlar 0 default'uyla normal koşu
    deterministik. Declined exception DEĞİL `PspResult` döner; transient hata
    `PspTransientException` fırlatır — retry ayrımı tip üzerinden.
11. **Strategy iskeleti (FR-6.5):** `IPaymentMethodStrategy` + yalnız `CardPaymentStrategy`;
    Wallet/BankTransfer W10+. `PaymentMethod` enum'u Domain'de tekrarlanır (Domain yalnız
    Shared.Kernel'e referans verebilir) — eşlemeyi adapter yapar. Ayrıca `Payment` sınıf adı
    modül namespace segmentiyle çakışır: Infrastructure/testlerde `PaymentAggregate` alias'ı.
12. **Inventory `Commit` primitifi (FR-3.3):** `StockItem.Commit(reservation)` — sahiplik,
    idempotent (Committed→no-op), yalnız Active, invariant korumaları; `OnHand−=q` VE
    `Reserved−=q` birlikte → **Available DEĞİŞMEZ → Commit hiçbir yarışta oversell üretemez**
    (testin en önemli iddiası). Adapter'da Release ile AYNI jitter'lı 10-deneme retry
    sözleşmesi — ortak `ExecuteWithRetryAsync` yardımcısına çıkarıldı (ikisi de decrement
    yönlü telafi/kesinleştirme: nihayetinde başarmalı).
13. **Commit best-effort:** commit hatası siparişi GERİ DÖNDÜRMEZ (para alındı, sipariş Paid;
    Reserved şişik kalır = undersell görünümü, oversell DEĞİL) — Warning izi W9 süpürücüsüne.
    Kaybeden yolda commit ASLA çağrılmaz (integration test id-bazında doğrular).
14. **Replay güvenliği:** Completed ödemenin key'i FARKLI tutar/para birimiyle gelirse
    `Payment.AmountMismatch` (P1 penceresinde sepet değişmişse eski ödeme yeni siparişe
    yapıştırılamaz). Kontrol Payment adapter'ında — sözleşmenin kendisi dürüst.
15. **FR-6.4 event'leri domain-event olarak raise edilir, dispatch edilmez** (PaymentCompleted/
    PaymentFailed); Contracts'a integration event taşınması outbox ile W8'de. Dev kanıt
    endpoint'i `GET /api/payment/dev/payments` yalnız Development'ta map edilir (Inventory
    dev-stok deseni) — K6 "tek charge"ı HTTP katmanında bununla doğrular.

## Yarış / crash penceresi analizi

**Aynı key 100 paralel:** hepsi rezerve edip charge dener → payments index 1 kazanan bırakır,
99 kaybeden InFlight-retry/replay ile kazananın sonucuna yakınsar → orders index 1 sipariş
bırakır → 99 kaybeden kendi rezervasyonlarını release edip kazananı replay eder.

| Pencere | Durum | Düzelme yolu |
|---|---|---|
| P1: charge Completed, order persist öncesi crash | payment var, sipariş yok, rezervasyonlar Active | Aynı key retry SELF-HEALING: yeniden rezerve → charge REPLAY (yeni charge yok) → persist. Sepet değiştiyse AmountMismatch korur. Rezervasyon izi W9 TTL |
| P2: Paid persist edildi, commit öncesi crash | Reserved şişik (undersell görünümü, oversell değil) | **W9 süpürücü tasarım girdisi: Paid siparişe bağlı Active rezervasyonu expire etmek GERÇEK oversell yaratır — commit'e çevirerek reconcile etmeli** |
| P3: payment Pending + crash | key kilitli görünür | 30 sn sonra stale-takeover devralır; taze pencerede istemci InFlight-409 ile bekler |
| P4: charge Completed + AddAsync exception | para alınmış, sipariş yok | release + rethrow (500); P1 ile aynı self-healing. Refund primitifi bilinçli YOK (W8/9) |

## Ölçüm sonuçları (8 Temmuz 2026, 12 çekirdek, OptimisticConcurrency, PSP LatencyMs=0)

### K6 checkout smoke — 20 VU × 30 sn, TEK sıcak ürün (ödeme + commit zinciri dahil)
| Metrik | Hedef | Sonuç |
|---|---|---|
| Checkout isteği p95 | NFR-5.1 < 500 ms | **245 ms** ✓ (Hafta 6: 27 ms — fark aşağıda) |
| Başarılı sipariş | — | 2.184 (≈48 sipariş/sn; H6: 121/sn) |
| Retryable 409 | tasarlanmış yanıt | 9.025 (~4 çakışma/sipariş) |
| http_req_failed | < %1 | **%0,00** ✓ |
| 30 retry'ı tüketen iterasyon | — | 4 / 2.137 (%0,19) |

**Neden p95 27 ms → 245 ms?** Zincire üç kalıcı yazma eklendi: payments insert (Pending) +
finalize (Completed + attempt) + satır başına stok commit'i (sıcak satırda xmin retry'lı).
NFR-5.1 bütçesinin (500 ms) hâlâ yarısındayız; sıcak-satır commit çakışması RedisLock
stratejisiyle beklemeye çevrilebilir (H4 bulgusunun devamı).

### K6 payment_idempotency_100 — aynı key ile 100 PARALEL checkout (kanıt §10.2)
| Check | Sonuç |
|---|---|
| Terminalde tam 1×201 | ✓ |
| 99×200 replay, hepsi aynı sipariş kimliği | ✓ |
| Payment dev endpoint'i: TEK Completed charge | ✓ |

Aynı kanıtın laboratuvar hali: `ChargeIdempotencyRaceTests` — gerçek Postgres'e karşı 100
paralel `ChargeAsync` → DB'de TAM 1 Completed satır + **PSP sayacı == 1** + hepsi aynı
PaymentId (`dotnet test` ile tekrarlanabilir).

### K6 breaker koşusu — FailureRate=0.5, 20 VU × 60 sn (kanıt §10.3)
| Gözlem | Sonuç |
|---|---|
| Host logları: `OnCircuitOpened` | **11 kez** |
| `OnCircuitHalfOpened` → `OnCircuitClosed` | 10 / **5 kez** (tam open→half-open→close döngüleri) |
| k6: 409 `Payment.PspUnavailable` (breaker açıkken hızlı ret) | 4.452 |
| Buna rağmen tamamlanan sipariş | 251 (%50 hata altında sistem akmaya devam etti) |

Log örneği:
```
[14:45:13 ERR] Resilience event occurred. EventName: 'OnCircuitOpened',
               Source: 'payment-psp/(null)/CircuitBreaker', Result: 'psp_5xx'
[14:45:33 INF] Resilience event occurred. EventName: 'OnCircuitClosed', ...
```

### K6 declined-sızıntı koşusu — DeclineRate=1, 40 checkout
| Check | Sonuç |
|---|---|
| Her checkout'un terminali 409 `Payment.Declined` | 40/40 ✓ |
| Koşu sonunda sıcak üründe `reserved=0` (sızıntı yok) | ✓ |

### Uçtan uca kanıt (manuel)
```
POST /api/ordering/checkout (Idempotency-Key)  → 201, status=Paid, history 4 satır:
     null->Created, Created->StockReserved, StockReserved->PaymentPending, PaymentPending->Paid
GET  /api/inventory/stock/{id}                 → onHand 10→8, reserved=0 (KALICI düşüş)
POST aynı key                                  → 200 + AYNI sipariş
POST header'sız                                → 400
GET  /api/payment/dev/payments                 → 1 satır: Completed, attempts=1, psp-tx dolu
Sepet                                          → 0 satır
DeclineRate=1 ile: checkout 409 Declined → AYNI key yine 409 (attempts hâlâ 1 — replay
PSP'ye gitmedi, FR-6.2) → reserved=0
```

## Bilinçli ertelemeler

| Konu | Bu hafta | Hafta |
|---|---|---|
| Outbox + event dispatch (PaymentCompleted/Failed, OrderPaid) | Raise-edilir-dispatch-edilmez | W8 |
| Payment integration event'lerinin Contracts'a taşınması | Domain event | W8 |
| Refund/void primitifi (P4 parasal telafisi) | Completed kalır, aynı key retry siparişi tamamlar | W8/9 |
| TTL süpürücü + P2 reconciliation (Paid siparişin Active rezervasyonu) | Analiz notu süpürücünün tasarım girdisi | W9 |
| Cancel endpoint'i + iade | Domain hazır | W9 |
| Wallet/BankTransfer stratejileri | Arayüz iskeleti hazır | W10+ |
| Gerçek-PSP timeout reconciliation (status query) | Timeout → Failed finalize (sahte PSP'de dürüst) | W9+ |

## Riskler / notlar

- **Shared'a SIFIR kod değişikliği** — 402 eşlemesi (ResultExtensions) bilinçli reddedildi;
  `Directory.Packages.props`'a tek `PackageVersion` satırı eklendi (merkezi manifest,
  konvansiyonun gereği).
- Test sayısı 243 → **275** (+6 StockItemCommit, +4 CommitReservation, +9 PaymentTests,
  +7 Payment.IntegrationTests, +6 CheckoutHandler ödeme dalları; toplam 12 test projesi).
- `payments` key namespace'i bugün tek tüketicili (Ordering checkout'u); ikinci üretici
  gelirse kapsam yeniden değerlendirilir.
- FakePspClient singleton'dır ve `Random.Shared` kullanır; deterministik testler kendi
  `PspOptions` örneğiyle kurar (oran tabanlı davranış yalnız K6 koşularında).
