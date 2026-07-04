# Hafta 2 Karar Notları — Catalog + EF Core Şema/Modül Deseni + Problem Details

> Roadmap Hafta 2 çıktısı: `GET /api/catalog/products` ve `/products/{id}` gerçek ürün JSON'ı döner;
> hatalar RFC 9457 Problem Details biçimindedir. Kanıt senaryoları aşağıda.

## Alınan kararlar

1. **Şema/modül deseni:** Her modül kendi PostgreSQL şemasında yaşar; migration history tablosu da
   modülün şemasındadır (`catalog.__EFMigrationsHistory`). Merkezi kayıt
   `Shared.Infrastructure/Persistence/ModuleDbContextExtensions.AddModuleDbContext` içindedir —
   Hafta 3'te Inventory aynı çağrıyla gelir, history çakışması yapısal olarak imkansızdır.
2. **Migration stratejisi:** Migration'lar `dotnet ef` CLI ile üretilir ve modülün
   `Persistence/Migrations` klasöründe yaşar. Development'ta `MigrateAndSeedHostedService`
   uygulama trafik almadan önce migrate + seed yapar (F5 deneyimi); production'da
   `dotnet ef database update` açık komutu esastır.
3. **DDD çizgisi:** Tüm iş kuralları domain'de (`Product.Create`, `Money.Create`); Application
   yalnızca orkestrasyon yapar, FluentValidation yalnızca istek biçimini korur. Durumu değiştiren
   her yol (seed dahil) aggregate fabrikasından geçer. Okuma tarafı (`IProductQueries`)
   `AsNoTracking` + doğrudan DTO projeksiyonu ile aggregate'i baypas eder — sorgu durum
   değiştirmediği için bu bir ihlal değildir. Yeni mimari test
   (`Application_should_not_depend_on_ef_core`) bu ayrımı korur.
4. **Hata modeli:** `Error` artık `ErrorType` taşır (Validation/NotFound/Conflict/Failure);
   `ResultExtensions.ToHttpResult` bunu HTTP status koduna eşler. Global exception handler
   Host'ta bir kez bağlanır; `IModule`'e middleware hook'u eklenmedi (pipeline Host'un sorumluluğu).

## Bilinçli ertelemeler

| Konu | Neden ertelendi |
|---|---|
| Kampanya alanları (FR-2.2/2.3) | Hafta 2 kanıtı sade ürün JSON'ı; `Money` VO fiyat dikişini açık tutuyor |
| Modül başına DB kullanıcısı | Şema ayrımı + mimari testler sınırı koruyor; ikinci şema (Hafta 3) gelince değerlendirilecek |
| Serilog + correlation id | Roadmap Hafta 4; şimdilik yerleşik `ILogger` |
| Testcontainers integration testleri | Roadmap Hafta 3 (Inventory) ile paylaşılan fixture olarak gelecek |
| Redis cache (NFR-2.3) | Roadmap Hafta 4; seed hacminde `AsNoTracking` + SKU index yeterli |

## Kanıt senaryoları (3 Temmuz 2026'da doğrulandı)

```bash
docker compose up -d postgres
dotnet run --project src/Bootstrapper/ModularCommerce.Host
```

| İstek | Sonuç |
|---|---|
| `GET /api/catalog/products?page=1&pageSize=5` | 200 — totalCount=15, ada göre sıralı 5 ürün |
| `GET /api/catalog/products?search=kahve&minPrice=100&maxPrice=1000` | 200 — 1 ürün (EV-3002) |
| `GET /api/catalog/products/{id}` | 200 — ürün detayı (fiyat + para birimi ayrık alanlar) |
| `GET /api/catalog/products/{bilinmeyen-guid}` | 404 `application/problem+json`, title=`Catalog.Product.NotFound` |
| `GET /api/catalog/products?pageSize=0` | 400 `application/problem+json`, title=`Catalog.Products.InvalidQuery` |

Veritabanı: `catalog` şemasında `products` (15 satır) + `__EFMigrationsHistory`.
Testler: 24 mimari + 35 unit, tümü yeşil (`dotnet test`).

### GlobalExceptionHandler 500 kanıtı (4 Temmuz 2026)

Geçici bir `/api/catalog/boom` endpoint'i ile yakalanmamış exception fırlatıldı
(doğrulama sonrası endpoint silindi). Yanıt:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "Sunucu hatası",
  "status": 500,
  "detail": "Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.",
  "traceId": "0HNMPOI83FB85:00000002",
  "exception": "System.InvalidOperationException: Kasıtlı test exception'ı ..."
}
```

- `Content-Type: application/problem+json`; `exception` alanı yalnızca Development'ta dolu.
- Konsol logunda handler'ın kaydı:
  `fail: ...GlobalExceptionHandler[0] — Beklenmeyen hata: /api/catalog/boom isteği işlenirken exception oluştu`.
