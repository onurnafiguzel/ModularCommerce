# K8s Hazırlığı — Docker Notları

> Amaç: ModularCommerce'i Kubernetes'e taşımadan önce Docker tarafını production kalitesine çekmek.
> Bu dosya geriye dönük hatırlama için — ne yapıldı, neden, ne kaldı.

## Denetim (kod değişikliği öncesi)

Repo taranıp bulunanlar: **Dockerfile ve `.dockerignore` yoktu**, `docker-compose.yml` sadece
Postgres/Redis/RabbitMQ'yu ayağa kaldırıyordu (hepsinde healthcheck var), uygulamanın kendisi için
servis yoktu, RabbitMQ volumesuzdu. Ayrıca: EF Core migration'lar (`MigrateAndSeedHostedService`)
sadece `ASPNETCORE_ENVIRONMENT=Development` iken çalışıyor — production'da migration yolu yok.
Secret'lar (`appsettings.json`: JWT signing key, DB şifresi; `docker-compose.yml`: Postgres şifresi;
RabbitMQ `guest:guest`) repoya düz metin gömülü.

## Kapsam kararı

- **Dockerfile + `.dockerignore`: kullanıcı kendi yazıyor/yazacak** (öğrenme amaçlı).
- **`docker-compose.yml`: Claude yaptı** — aşağıdaki değişiklikler.
- **Migration akışı ve secret dışsallaştırma: bilinçli ertelendi**, bu tur kapsamı dışında.

## `docker-compose.yml` değişiklikleri

Yeni `api` servisi eklendi:
```yaml
api:
  build:
    context: .
    dockerfile: src/Bootstrapper/ModularCommerce.Host/Dockerfile
  ports:
    - "8080:8080"
  environment:
    ASPNETCORE_ENVIRONMENT: Development
    ASPNETCORE_URLS: http://+:8080
    ConnectionStrings__Database: Host=postgres;Port=5432;Database=modular_commerce;Username=postgres;Password=postgres
    ConnectionStrings__Redis: redis:6379
    ConnectionStrings__RabbitMq: amqp://guest:guest@rabbitmq:5672
  depends_on:
    postgres:
      condition: service_healthy
    redis:
      condition: service_healthy
    rabbitmq:
      condition: service_healthy
```
- Connection string anahtar adları (`Database`/`Redis`/`RabbitMq`) `appsettings.json` ile birebir —
  sadece host adları container-network isimlerine (`postgres`/`redis`/`rabbitmq`) çevrildi.
- `api` servisine önce curl tabanlı bir `/health/live` healthcheck eklendi, sonra **kaldırıldı**
  (chiseled image kullanılırsa curl/shell olmayabilir → k8s'e geçince zaten yerini
  `livenessProbe`/`readinessProbe` alacak, compose'daki healthcheck sadece yerel dev için gereksiz risk).
- `rabbitmq` servisine named volume eklendi: `rabbitmq_data:/var/lib/rabbitmq` (+ top-level `volumes`).
  Önceden restart'ta queue/mesaj kaybı riski vardı.
- `postgres`/`redis`'in mevcut tanımlarına dokunulmadı.

## Dockerfile (multi-stage, kullanıcı yazdı + Claude ile birlikte tamamlandı)

Konum: `src/Bootstrapper/ModularCommerce.Host/dockerfile` (dosya adı **küçük harf** —
compose'da `Dockerfile` büyük harfle referans veriliyor; Windows'ta sorun yok, case-sensitive bir
Linux CI'da build kırılabilir. **Çözülmedi, açık madde.**)

- **Stage 1 (SDK, `AS build`):** önce sadece `.csproj` + `Directory.Packages.props` +
  `Directory.Build.props` kopyalanıp `dotnet restore` çalıştırılıyor, sonra kalan kaynak kopyalanıp
  `dotnet publish --no-restore` — restore adımı layer cache'te kalsın diye.
  - **Sorun:** Host projesi 9 modülün 5 katmanına (40+ csproj) referans veriyor; sadece Host'un
    csproj'unu kopyalamak restore'u kırar (proje referansları bulunamaz).
  - **Çözüm:** `# syntax=docker/dockerfile:1` + `COPY --parents src/**/*.csproj ... ./` — BuildKit'in
    `--parents` flag'i, glob ile eşleşen dosyaları klasör yapısını koruyarak kopyalar.
- **Stage 2 (`aspnet:10.0` runtime):** `COPY --from=build /app/publish .`, `USER $APP_UID`
  (image'a gömülü hazır non-root kullanıcı, yeni kullanıcı yaratmaya gerek yok), `EXPOSE 8080`,
  `ENTRYPOINT ["dotnet", "ModularCommerce.Host.dll"]` — **exec form** (shell form değil, SIGTERM'in
  doğrudan `dotnet` process'ine ulaşması için — k8s pod terminate ederken graceful shutdown şart).

## Doğrulama durumu

- `docker compose config` ile YAML geçerliliği doğrulandı.
- **`docker build` / `docker compose up` ile gerçek build denenmedi** — Dockerfile üretim sırasında
  bilerek atlandı, artık dosya var, test edilebilir.

## Sıradaki adım

1. **`.dockerignore` yaz** (kullanıcı) — yoksa build context `bin/`+`obj/` (~460 MB) dahil tüm repo.
2. **Gerçek build dene:** `docker compose build api` (veya `docker build -f src/Bootstrapper/ModularCommerce.Host/dockerfile .`)
   — Dockerfile'ın gerçekten derlendiğini doğrula, image boyutunu ölç.
3. **`docker compose up`** ile tüm stack'i ayağa kaldırıp `api`'nin postgres/redis/rabbitmq
   healthy olana kadar beklediğini, sonra `/health/ready`'nin gerçekten 200 döndüğünü canlı doğrula.
4. Bunlar yeşil olunca, ertelenen iki kritik madde sırada: **production migration akışı** (ayrı
   job/init-container, `MigrateAndSeedHostedService`'in dev-only gate'ine güvenmeden) ve
   **secret dışsallaştırma** (appsettings + compose'daki düz metin credential'lar → env/secret).
