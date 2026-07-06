# Hafta 4 Karar Notları — Redis Distributed Lock + Üç Stratejinin K6 Karşılaştırması + Serilog/Correlation Id

> Roadmap Hafta 4 çıktısı: üç stratejinin karşılaştırma tablosu README'de (LinkedIn post #2).
> Geçen hafta ertelenen K6 bu hafta kuruldu; kanıt iki katmanlı: Testcontainers (doğruluk) + K6 (yük).

## Alınan kararlar

1. **El yapımı tek-node Redis kilidi** (`RedisDistributedLock`): edinme `SET key token NX PX`,
   bırakma token karşılaştıran Lua script (yalnız sahibi bırakır). Anahtar ürün başına:
   `inventory:lock:stock:{productId}`. RedLock.net alınmadı — tek node, paket zaten mevcut
   (StackExchange.Redis), eğitim değeri repo'nun amacı. **UYARI:** bu kilit tek-node garantisidir;
   Redis cluster/failover'da kilit garanti kaybeder — çok-node için Redlock algoritması gerekir
   (bilinçli erteleme).
2. **TTL 5 sn, bekleme bütçesi 100 ms** (`Inventory:RedisLock:{TtlSeconds, WaitBudgetMs}`):
   TTL, yazma p95'inin ~2 kat büyüklük üstü (kritik bölge içinde dolma riski pratikte sıfır);
   bekleme 5–15 ms jitter'lı denemelerle (thundering-herd kilitli adımda senkronize olmasın —
   NFR-3.3). Bütçe dolarsa **409 `Inventory.LockTimeout`** (yeni kod): ConcurrencyConflict'ten
   ayrı kod, tabloda "çakışma" ile "kilit beklemesi"ni ayrı sayılabilir yapar.
3. **Savunma hattı 2:** xmin token'ı map'li kaldı. Kilit TTL'i kritik bölgede dolarsa (beklenmez)
   ikinci yazarın çakışmasını optimistic token yine yakalar — strateji bunu warning logla raporlar.
   Naive'in raw-UPDATE yolu kilit stratejisinde BİLEREK kullanılmadı: kilit, korumasız yazmanın
   mazereti değildir.
4. **Redis erişilemezse** (`RedisConnectionException/Timeout`): 500 `Inventory.LockUnavailable` —
   kesin CP (NFR-3.4): kilit servisi belirsizken "tekrar dene" (409) yalan olur, istek reddedilir.
   Host boot'u etkilenmez (`abortConnect=false`).
5. **Shared değişiklikleri (yalnız iki):**
   - `Redis/RedisExtensions.cs` — `AddRedis`: singleton `IConnectionMultiplexer`
     (tüketiciler: Inventory lock bugün, Cart H5, Catalog cache NFR-2.3).
   - `Observability/CorrelationIdMiddleware.cs` — X-Correlation-Id kabul/üret/echo +
     Serilog `LogContext`. Host'ta `UseExceptionHandler`'dan ÖNCE bağlanır; ProblemDetails'e
     `correlationId` extension'ı eklendi.
   `IDistributedLock` port'u bilinçli olarak modül-lokal (Inventory.Application) — ikinci
   tüketici (Catalog single-flight) çıkınca Shared'a terfi eder.
6. **Serilog:** `UseSerilog(ReadFrom.Configuration)` + `UseSerilogRequestLogging`; appsettings'te
   `Logging` → `Serilog` bölümü. **Ders:** `Enrich: [FromLogContext]` olmadan LogContext özellikleri
   loglara AKMAZ — ilk koşuda CorrelationId boş çıktı, enrichment eklenince düzeldi.
7. **K6 semantiği:** 409 tasarlanmış iş yanıtıdır (ConcurrencyConflict/LockTimeout = "tekrar dene",
   InsufficientStock = terminal) — smoke script'i `http.expectedStatuses(201, 409)` kullanır ki
   `http_req_failed` yalnız gerçek hataları (5xx) saysın.

## Ölçüm sonuçları (6 Temmuz 2026)

### Doğruluk — Testcontainers, 100 paralel, OnHand=10
| Strateji | Başarılı | Oversell | Çakışma-retry | Kilit timeout |
|---|---|---|---|---|
| Naive | 100 | **90** | — | — |
| Optimistic | **10** | 0 | 121 | — |
| RedisLock | **10** | 0 | **0** | 4 |

### Yük — K6 burst, 1000 VU, OnHand=10 (istemci retryable-409'da tekrar dener)
| Strateji | Başarılı | Oversell | Çakışma-retry | Kilit timeout | p95 |
|---|---|---|---|---|---|
| Naive | 229 | **219** | — | — | 1,67 sn |
| Optimistic (STRICT ✓) | **10** | 0 | 971 | — | 505 ms |
| RedisLock (STRICT ✓) | **10** | 0 | **0** | 2.560 | 6,05 sn* |

(*) Stok bittiği halde LockTimeout retry'ı yapan istemci kuyruğu — terminal InsufficientStock'a
ulaşana dek süren beklemeler. Burst senaryosunun doğası; NFR-3.2 ölçümü smoke'tadır.

### Sürekli yük — K6 smoke, 50 VU × 30 sn, TEK sıcak ürün (bilinçli contention benchmark'ı)
| Strateji | p95 (NFR-3.2 < 150 ms) | Başarılı yazma/sn | Başarı oranı |
|---|---|---|---|
| Optimistic | **71 ms** ✓ | ≈ 90 | %7,7 |
| RedisLock | **114 ms** ✓ | ≈ **155** | %30 |

**Haftanın bulgusu:** Tek sıcak satırda sürekli yük altında optimistic'in çakışma oranı %92'ye
çıkıyor — NFR-3.1'deki "optimistic yetersiz kalırsa distributed lock" cümlesi ölçümle doğrulandı.
Kilit, çakışmayı beklemeye çevirerek aynı satırdan ~1,7× daha fazla başarılı yazma geçiriyor;
bedeli ~40 ms ek p95. Gerçek dünyada yük birçok ürüne dağılır — tekil sıcak ürün (flash sale)
tam da bu projenin senaryosu.

### CorrelationId kanıtı
```
[15:25:41 INF] [korelasyon-kanit-42] Kilit alındı: 33333333-... (bekleme 5 ms)
[15:25:42 INF] [korelasyon-kanit-42] HTTP POST /api/inventory/reservations responded 201 in 448.4 ms
```
Modül logu ve request logu aynı id'yi taşıyor; hata yanıtı da taşıyor:
`{"title":"Inventory.StockItem.NotFound", ..., "correlationId":"korelasyon-hata-99"}`.

## Bilinçli ertelemeler
| Konu | Neden |
|---|---|
| `IDistributedLock`'ın Shared'a terfisi | İkinci tüketici (Catalog single-flight, NFR-2.3) gelince |
| `ErrorType.ServiceUnavailable`/503 | LockUnavailable şimdilik 500; tek kullanım için Shared'ı genişletmeye değmez |
| RedLock çok-node | Tek node compose ortamı; uyarı dokümante edildi |
| Rezervasyon TTL süpürme + compensation | Roadmap Hafta 9 |
| OpenTelemetry | Roadmap Hafta 11 (paket merkezde hazır) |

## Riskler / notlar
- Kilit TTL'i kritik bölgede dolarsa iki yazar oluşabilir → xmin ikinci hat olarak yakalar
  (strateji warning loglar; integration testi çakışma=0 assert'i ile gözler).
- Integration testlerinde kilit bekleme bütçesi bilinçli geniş (2 sn): 100 görev tek ürünün
  kilidinde sıralanır; amaç doğruluk assert'i. Üretim varsayılanı 100 ms.
- K6 koşuları strateji başına Host restart ister (kayıt anı seçimi — karşılaştırmayı dürüst tutar).
