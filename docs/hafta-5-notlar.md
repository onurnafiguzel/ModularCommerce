# Hafta 5 Karar Notları — Identity (signup/login/JWT) + Cart (Redis sepet) + JWT Auth Middleware

> Roadmap Hafta 5 çıktısı: **login olup sepete ürün ekleme akışı** (kanıt aşağıda).
> Cross-cutting ilkesi korundu: JWT doğrulama + endpoint authorization bu hafta geldi
> çünkü onları ilk talep eden feature (korumalı sepet) bu hafta.

## Alınan kararlar

1. **Full ASP.NET Identity ALINMADI** (kullanıcı kararı): kendi `User` aggregate'imiz
   (always-valid, Result deseni) + yalnız hash algoritması için `PasswordHasher<User>`
   (`Microsoft.Extensions.Identity.Core`), `IPasswordHasher` portu arkasında.
   UserManager/IdentityDbContext töreni yok; iş kuralları Domain'de kaldı.
   Kullanılmayan `Microsoft.AspNetCore.Identity.EntityFrameworkCore` bildirimi merkezden silindi.
2. **`ErrorType.Unauthorized` → 401 Shared.Kernel'e eklendi**: 3 hata kodu hemen kullanıyor
   (`InvalidCredentials`, `RefreshTokenInvalid` + korumalı endpoint'lerin challenge'ı).
   Hafta 4'te 503 için Shared genişletilmemişti (tek kullanım); burada gerçek çok-tüketicili ihtiyaç.
3. **Bilgi sızdırmama üçlüsü:** e-posta yok / şifre yanlış AYNI `InvalidCredentials`;
   refresh token yok/expired/revoked AYNI `RefreshTokenInvalid`; logout idempotent
   (bulunamayan/başkasının token'ında da 204 — ama başkasının token'ı asla iptal edilmez).
   Timing-attack dokunuşu: kullanıcı yokken de `Verify(DummyHash, ...)` koşulur.
4. **RefreshToken ayrı aggregate** (User'ın çocuğu değil): arama hep token üzerinden.
   Ham değer istemciye, DB'ye SHA-256 hex özeti (unique index). Refresh'te **rotasyon**:
   eski token revoke + yenisi aynı SaveChanges'te. Reuse-detection kaskadı bilinçli erteleme.
5. **`AddJwtBearer` Shared.Infrastructure/Auth'ta, Host çağırır** (AddRedis emsali).
   `JwtOptions` TEK kaynak: Host doğrulama, Identity üretim aynı `IOptions<JwtOptions>`'ı alır —
   imza parametreleri ayrışamaz. `MapInboundClaims=false` (sub ham okunur), ClockSkew 30 sn,
   eksik/kısa anahtar boot'ta patlar. Dev anahtarı appsettings'te (uyarı alanıyla); prod → H12 Key Vault.
   Pipeline: `...UseStatusCodePages → UseAuthentication → UseAuthorization → MapEndpoints`
   (StatusCodePages sayesinde JwtBearer'ın boş 401'i ProblemDetails gövdesi kazanır).
6. **Unique e-posta (FR-1.5) iki katmanlı:** handler ön-kontrolü dostça 409 içindir;
   asıl garanti DB unique index — check-then-insert yarışını `UserRepository.SaveChangesAsync`'in
   23505 → `EmailAlreadyExists` çevirimi kapatır (integration testi iki eşzamanlı scope'la kanıtlar).
   Bu yüzden `IUserRepository.SaveChangesAsync` Result döner.
7. **Şifre politikası (min 8) FluentValidation'da** — Domain ham şifreyi HİÇ görmez
   (hash'i hazır alır), dolayısıyla bu bir istek-şekli kuralıdır, iş kuralı değil.
8. **Cart: DbContext YOK.** Sepet Redis'te tek JSON belge (`cart:{customerId}`), TTL 7 gün
   **yazmada kayar** (okuma TTL'e dokunmaz — pasif GET terk edilmiş sepeti yaşatmaz).
   Boşalan sepet anahtarı silinir; olmayan anahtar = boş sepet (404 değil).
   Mülakat cümlesi: **"her veri aynı dayanıklılığı hak etmez"** (NFR-4.3) —
   Inventory'nin CP disipliniyle bilinçli tezat (NFR-4.2 AP, last-write-wins).
9. **Cart aggregate Entity türevi değil** (EF/event yok; kimliği CustomerId) ama tüm iş
   kuralları yine Domain'de: satır başına maks 10 adet (flash-sale stok gaspı önlemi),
   maks 50 satır, birleştirme tavana tabi, silme açık DELETE (adet 0 kabul edilmez).
   `ICartRepository` Result dönen imzalarla: Redis erişilemezse `Cart.StorageUnavailable`/500 —
   "boş sepet" yalanıyla erişilemezlik maskelenmez (H4 `LockUnavailable` emsali).
10. **Bu hafta Cart, Catalog/Inventory'yi HİÇ aramaz** (FR-4.3): productId yalnız biçimsel
    doğrulanır; varlık/stok/fiyat doğrulaması checkout'la (Hafta 6) gelir. Sepet yanıtlarının
    hepsinde FR-4.4 uyarı alanı: "Sepete eklemek rezervasyon değildir...".
11. **Contracts iki modülde de BOŞ bırakıldı:** Ordering (H6) userId'yi JWT'den alacak
    (NFR-1.2'nin doğal sonucu); `ICartReader` benzeri yüzey onu ilk talep eden checkout ile gelir.
12. **`ClaimsPrincipalExtensions.GetUserId()` Shared/Auth'ta:** iki tüketici ilk günden
    (Cart endpoints + Identity logout). Claim yoksa exception — bu istek hatası değil
    yapılandırma hatasıdır (`MapInboundClaims` unutulmuş demektir).

## Ölçüm sonuçları (6 Temmuz 2026, 12 mantıksal çekirdek)

### K6 login smoke — constant-arrival-rate 200 RPS × 30 sn (NFR-1.4)
| Metrik | İterasyon 100k (varsayılan) | İterasyon 20k (seçilen) |
|---|---|---|
| p95 | **3,84 sn ✗** | **20,9 ms ✓** (NFR-1.1 < 200 ms) |
| Gerçekleşen RPS | ~119 (2.238 iterasyon düştü) | **~198** (0 düşen) |
| Hata oranı | %0 | %0 (6.000 login) |

### K6 cart smoke — 20 VU × 30 sn, VU başına kendi sepeti (NFR-4.1)
| Metrik | Sonuç |
|---|---|
| p95 (JWT doğrulama + Redis dahil) | **2,88 ms ✓** (NFR-4.1 < 50 ms) |
| Throughput | ~7.700 istek/sn (≈5.130 yazma/sn + 2.570 okuma/sn) |
| Hata | 0 / 231.109 istek |

**Haftanın bulgusu — PBKDF2 maliyeti vs login SLA'sı:** varsayılan 100k iterasyonda tek
doğrulama ~150 ms → 200 RPS'te 12 çekirdek doydu (200 × 0,15 = 30 CPU-sn/sn > 12), kuyruk
büyüdü, p95 3,8 sn'ye tırmandı. `IterationCount=20_000` (~30 ms/hash) NFR-1.1'i 10 kat marjla
karşıladı. Trade-off bilinçli: hash gücü 5 kat düştü; V3 format iterasyonu hash'e gömdüğünden
mevcut kayıtlar geçerli kalır ve prod'da (daha güçlü donanım/Argon2) yükseltmek migration
gerektirmez. Ders: **"güvenlik parametresi" de bir performans parametresidir; SLA ile birlikte
ölçülmeden seçilemez.**

### Uçtan uca kanıt (roadmap H5 çıktısı)
```
tokensiz GET /api/cart                      → 401 (ProblemDetails + correlationId)
POST /api/identity/signup                   → 201 (aynı e-posta → 409 EmailAlreadyExists)
POST /api/identity/login                    → 200 (access + refresh)
POST /api/cart/items (Bearer)               → 200 + "warning": "Sepete eklemek rezervasyon değildir..."
redis-cli TTL cart:{userId}                 → 604800 (tam 7 gün)
POST /api/identity/refresh (eski token 2. kez) → 401 (rotasyon kanıtı)
DELETE /api/cart/items/{id} (son satır)     → 200, redis EXISTS → 0 (yokluk = boşluk)
```

## Bilinçli ertelemeler
| Konu | Neden |
|---|---|
| Access token iptali (blacklist) | Logout yalnız refresh'i iptal eder; 15 dk pencere = NFR-1.2 lokal doğrulamanın bedeli |
| Refresh reuse-detection kaskadı | Rotasyon yeterli başlangıç; sertleştirme H11 |
| Identity/Cart Contracts yüzeyi | H6'da Ordering talep edince (boş Contracts = "henüz kimse istemedi"nin kanıtı) |
| Rate limiting (login brute-force) | Roadmap H11 |
| E-posta doğrulama / şifre sıfırlama | Kapsam dışı |
| Checkout'ta sepet yeniden doğrulama (FR-4.3) | H6 — ilk modüller arası senkron çağrı oraya ait |
| Prod signing key yönetimi | H12 (Key Vault) |

## Riskler / notlar
- Sepette read-modify-write last-write-wins'tir: iki eşzamanlı istek satır kaybettirebilir —
  NFR-4.2 bunu açıkça kabul eder; K6 script'i bu yüzden VU başına ayrı sepet kullanır
  (ölçülen şey gecikmedir, AP davranışı değil).
- `MapInboundClaims=false` kaldırılırsa `sub` claim'i map'lenir ve `GetUserId` exception'ı
  yapılandırma hatasını anında görünür kılar (sessiz boş Guid yerine).
- Login smoke tek kullanıcıya 200 RPS okur: Postgres connection pool (varsayılan 100) doymadı;
  çok-kullanıcılı gerçekçi dalga H11 full-checkout senaryosunun işi.
- `dotnet ef migrations add` sonrası Host asla `--no-build` ile koşulmaz (bilinen tuzak, tekrarı yok).
