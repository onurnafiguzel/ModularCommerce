# Hafta 11 Karar Notları — Uçtan Uca Sertleştirme

> Roadmap Hafta 11 çıktısı: **rate limiting + gerçek health check'ler + K6 flash-sale senaryosu**.
> Kanıt: flash sale simülasyon raporu (oversell=0 + korunma 429'ları) → LinkedIn post #5.
> **Kullanıcı kararı: OpenTelemetry bu hafta yok** (ertelendi) → hafta üç cross-cutting işe indi.

## Kapsam kararları

- **OTel YOK** (kullanıcı kararı): trace/metrik pipeline'ı + collector/Jaeger/Grafana ertelendi.
  `Directory.Packages.props`'taki dangling `OpenTelemetry.Extensions.Hosting` pin'i olduğu gibi bırakıldı.
- **Rate limiting: katmanlı** (global + auth + checkout), kullanıcı/IP partition, 429 + Retry-After.
- **Health: liveness/readiness ayrımı** (`/health/live` + `/health/ready`), merkezi.
- **K6: odaklı** — yalnız `flash-sale.js` (lib refactor + handleSummary yok).
- **SIFIR yeni NuGet paketi**: rate limiting framework yerleşiği (`Microsoft.AspNetCore.RateLimiting`),
  health check'ler **el yapımı** (Postgres+Redis custom `IHealthCheck` + MassTransit yerleşiği) —
  projenin "el yapımı outbox/inbox" temasıyla tutarlı.

## Kritik gerilim — rate limiting mevcut yeşil kanıtı kırabilirdi

`checkout-smoke.js` §10.2 **aynı kullanıcıyla 100 PARALEL** aynı-key checkout gönderip "tek charge"
kanıtlıyor. **Sıkı per-user checkout limiti bu burst'ü 429'lardı → yeşil test kırılırdı.** Ayrıca
flash-sale her VU için ayrı kullanıcı yarattığından per-user checkout limiti hiç 429 üretmez.
Çözüm:
- **checkout policy burst-absorbing** (permit=120 + queue=120 ≥ 100) → 100-paralel kabul edilir;
  yalnız tek-kullanıcı SÜREKLİ kötüye kullanımı kesilir. (Options validate'i `Permit+Queue ≥ 100`
  invariant'ını zorlar — yanlış config build/başlangıçta patlar.)
- **auth policy** (login/signup, **IP bazlı**, sıkı 10/60s) → gösterilebilir 429 kaynağı budur
  (brute-force/flood). Flash-sale'in tek-IP login dalgası burada 429 üretir.
- **limitler appsettings ile yapılandırılabilir** → yük testi/ortam başına gevşetilir.

## 429 ↔ retryable-409 istemci sözleşmesi (yeni sinyal)

| Yanıt | Anlam | İstemci davranışı |
|---|---|---|
| `409 Inventory.ConcurrencyConflict/LockTimeout/Payment.InFlight/PspUnavailable` | Geçici çakışma | **AYNI key** ile HEMEN tekrar (mevcut) |
| `429 RateLimited` | Hız sınırı | **`Retry-After` kadar bekle**, sonra tekrar (YENİ) |
| `409 Payment.Declined/Timeout` | Terminal | Yeni key (mevcut) |

`flash-sale.js` 429'da `Retry-After` başlığı kadar bekleyip aynı key ile devam eder. Not: SlidingWindow
limiter her reddi için `RetryAfter` metadata'sı vermeyebilir → `OnRejected` metadata yoksa **pencere
kadar (Global.WindowSeconds) hint** yazar, böylece Retry-After **her 429'da** bulunur.

## Health: liveness vs readiness (Container Apps/K8s deseni)

- **`/health/live`** — `Predicate: _ => false` → hiç prob çalıştırmaz, yalnız "süreç ayakta mı".
  Geçici bir bağımlılık düşüşü container'ı **öldürmemeli** (yeniden başlatma fırtınası olmaz).
- **`/health/ready`** — `Predicate: Tags.Contains("ready")` → Postgres (`SELECT 1`) + Redis (`PING`)
  + `masstransit-bus`. Biri Unhealthy → **503** (load balancer routing'i keser, süreç yaşamaya devam).
- 8 modülün statik `/health`'i kaldırıldı → tek doğruluk kaynağı merkezi endpoint'ler.

## SOLID / Tasarım kalıpları (özet)

- **SRP:** `PostgresHealthCheck`/`RedisHealthCheck` (her biri tek bağımlılık), `HealthResponseWriter`
  (yalnız JSON), `AddRateLimiting`/`AddDependencyHealthChecks` (her extension tek cross-cutting).
- **OCP:** yeni bağımlılık = `.AddTypeActivatedCheck(..., tags:["ready"])`; yeni sıcak yol = yeni
  policy + `.RequireRateLimiting`; endpoint/predicate değişmez.
- **DIP:** health check'ler `IConfiguration`/`IConnectionMultiplexer`'a bağlı → testte Testcontainers.
- **Patterns:** Health Check/Probe, Liveness/Readiness ayrımı, Throttling/Bulkhead (partitioned limiter),
  Options/Knob (`RateLimitingOptions`), Strategy (global vs named policy), Decorator (`OnRejected` reddi
  tutarlı ProblemDetails'e sarar — GlobalExceptionHandler simetriği).

## Doğrulama (11 Temmuz 2026)

### Testler — Shared +6 (tümü yeşil)
- **Shared.IntegrationTests 6** (Testcontainers Postgres+Redis): Postgres/Redis health up→Healthy,
  down→Unhealthy; rate-limiter sizing (checkout 100-burst kabul, auth limit aşımı → reddet).
- **ArchitectureTests 32** yeşil (Shared.Infrastructure sınırı bozulmadı).

### Canlı E2E (docker compose + gerçek Host)
```
GET /health/live  → 200 {"status":"Healthy","checks":[]}                    (probsuz)
GET /health/ready → 200 postgres:Healthy + redis:Healthy + masstransit-bus:Healthy
  (masstransit-bus "ready" tag'ini taşıyor → readiness'a otomatik dahil — D10 doğrulandı)

POST /api/identity/login ×12 (aynı IP, geçersiz kimlik):
  #1..#10 → 401  (kabul edildi, kimlik yanlış)
  #11,#12 → 429 Retry-After: 10
  429 gövdesi: {"type":"RateLimited","title":"RateLimited","status":429,"detail":"...","correlationId":"..."}
```

### Flash-sale (manuel K6 — ÖN KOŞUL)
`flash-sale.js` düşük stoklu sıcak ürüne rampalı checkout yükü basar; teardown `OVERSELL=0` doğrular.
**Ön koşul:** yük üreteci tek IP'den yüzlerce signup yaptığından auth limiti bu senaryonun kendi
kullanıcı üretimini boğar → Host'u auth limiti gevşetilmiş çalıştır:
```
RateLimiting__Auth__PermitLimit=100000  RateLimiting__Auth__WindowSeconds=1  Payment__Psp__LatencyMs=0
k6 run tests/LoadTests/scenarios/flash-sale.js
→ teardown: "FLASH SALE SONUÇ: başlangıç=50 onHand=0 ... SATILAN=50 OVERSELL=0"
```
Checkout throttle (auth throttle değil) senaryonun konusudur; auth throttle ayrıca yukarıdaki login
demo'suyla kanıtlanır.

## Bilinçli ertelemeler

| Konu | Bu hafta | Hafta |
|---|---|---|
| OpenTelemetry (trace/metrik, collector/Jaeger/Grafana) | YOK (kullanıcı kararı) | W12+ |
| Distributed rate limiting (Redis-backed, çok-instance) | in-memory (tek-instance) | W12 Azure |
| K6 paylaşılan `lib/` + `handleSummary` raporu | YOK | sonraki |
| Shipping modülü | YOK (W10'dan ertelendi) | W12+ |

## Riskler / notlar

- **Rate limit ↔ §10.2:** checkout permit+queue ≥ 100 (validate zorlar) → mevcut idempotency kanıtı
  yeşil kalır.
- **Yük testi tek IP'den kendini boğar:** auth limiti gevşetilerek koşulur (yapılandırılabilir); 429
  flash-sale raporunda **korumanın çalıştığının kanıtı** sayılır, başarısızlık değil.
- **Liveness bağımlılık içermez** → geçici DB/Redis blip container'ı öldürmez.
- **Health + root endpoint'leri `.DisableRateLimiting()`** → prob'lar hız sınırına takılmaz.
