# Docker Compose'dan Kubernetes'e: ModularCommerce'i `kind` ile Yerelde Ayağa Kaldırmak

*Bir `.yml` dosyasının CI'da ne işe yaradığını, bir `.yaml` dosyasının cluster'da neye dönüştüğünü ve "pod" dediğimiz şeyin aslında "container" ile aynı şey olmadığını, gerçek bir repo üzerinden, adım adım.*

---

## Neden bu makale

`docker-compose.yml` ile bir süre sonra rahatlıyorsunuz: `docker compose up -d`, üç servis ayağa kalkıyor, `depends_on` + `healthcheck` sırayı hallediyor, iş bitiyor. Ama bu, **tek makine, tek Docker daemon'u** varsayımıyla çalışır. Gerçek üretimde soru değişir: "API'nin 3 kopyası varsa istek hangisine gider?", "bir kopya çökerse ne olur?", "veritabanı migration'ını 3 kopya aynı anda çalıştırırsa ne olur?" Bunlara docker-compose'un cevabı yok — çünkü bu sorular onun problem alanı değil, **orkestrasyon**un problem alanı.

ModularCommerce'i Kubernetes'e taşırken ortaya çıkan gerçek dosyalar üzerinden — uydurma örnekler değil — bu geçişi anlatıyorum. İki klasör var: `.github/workflows/` (CI/CD — kod nasıl imaja, imaj nasıl cluster'a gidiyor) ve `k8s/` (cluster'da ne çalışacağının tarifi). `kind` ile bunları kendi makinenizde, gerçek bir bulut hesabı açmadan deneyebilirsiniz.

---

## Bölüm 1 — `.github/workflows/`: kod cluster'a nasıl ulaşır

### `ci.yml` — kapı bekçisi

```yaml
on:
  push:
    branches: [ "main" ]
  pull_request:
    branches: [ "main" ]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --verbosity normal
```

Tek işi var: her `main`'e push'ta ve her PR'da **derle + test et**. Deploy etmiyor, imaj basmıyor — sadece "bu kod bozuk mu?" sorusuna cevap veriyor. `ModularCommerce.sln`'de `TreatWarningsAsErrors=true` olduğu için burada bir uyarı bile build'i kırar; yani bu adım aslında Docker imajını inşa etmeden önceki son güvenlik ağı.

### `release.yml` — imajdan cluster'a

Bu dosya iki işten oluşuyor, sırayla:

**`build-and-push`** — Dockerfile'ı derler, imajı GitHub Container Registry'ye (`ghcr.io`) basar:
```yaml
- uses: docker/build-push-action@v7
  with:
    context: .
    file: src/Bootstrapper/ModularCommerce.Host/Dockerfile
    push: true
    tags: ghcr.io/onurnafiguzel/modularcommerce:${{ github.sha }}
```
Etiket (`tag`) commit SHA'sı — yani her commit kendi benzersiz imajını üretir, "latest" gibi belirsiz bir etiket yok. Bu, hangi cluster'ın hangi kod sürümünü çalıştırdığını her zaman geriye izlenebilir kılıyor.

**`deploy`** — Azure'a giriş yapar, AKS (Azure Kubernetes Service) credential'larını alır, sonra ilginç bir numara yapar:
```yaml
- run: |
    sed -i "s|IMAGE_TAG|${{ github.sha }}|g" k8s/deployment.yaml
    sed -i "s|IMAGE_TAG|${{ github.sha }}|g" k8s/migration-job.yaml
- run: |
    kubectl apply -f k8s/
    kubectl rollout status deployment/modularcommerce-api --timeout=5m
```
`k8s/deployment.yaml` ve `k8s/migration-job.yaml` içinde imaj adı sabit bir SHA değil, **`IMAGE_TAG` adında bir yer tutucu** olarak duruyor. CI, deploy anında bunu gerçek commit SHA'sıyla değiştiriyor (`sed`), sonra `kubectl apply -f k8s/` ile klasördeki **her** manifesti cluster'a gönderiyor. `kubectl rollout status` ise "deploy bitti" demeden önce yeni pod'ların gerçekten Ready olmasını bekliyor — bu adım başarısız olursa CI kırmızı yanar, yani bozuk bir imaj sessizce prod'da kalmaz.

Yerelde `kind` ile çalışırken bu iki adımı **elle** taklit edeceğiz: `sed` yerine dosyayı siz düzenleyeceksiniz, GHCR yerine `kind load docker-image` kullanacaksınız (aşağıda).

---

## Bölüm 2 — `k8s/`: cluster'da ne çalışacak

Sekiz dosya, üç kategoriye ayrılıyor.

### Kategori A — uygulamanın kendisi

| Dosya | Kind | Amaç |
|---|---|---|
| `configmap.yaml` | ConfigMap | Gizli olmayan config: `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_URLS=http://+:8080` |
| `secret.yaml` | Secret | Bağlantı dizeleri (`ConnectionStrings__Database` vb.) — Base64 ile saklanır (şifreleme değil, ama düz metin appsettings'ten ayrı bir kaynak) |
| `deployment.yaml` | Deployment | API'nin **3 replika**sını tanımlar, her birine `configmap`+`secret`'ı `envFrom` ile enjekte eder, liveness/readiness probe'larını bağlar |
| `service.yaml` | Service | 3 replikayı tek bir ağ adının (`modularcommerce-api`) arkasında toplar, isteği aralarında dağıtır |
| `migration-job.yaml` | Job | Tek seferlik: veritabanı migration'ını **replikalardan bağımsız, bir kez** çalıştırır |

### Kategori B — altyapı bağımlılıkları

| Dosya | Kind | Amaç |
|---|---|---|
| `postgres.yaml` | StatefulSet + Service | Kalıcı, kimliği sabit bir Postgres pod'u + disk (PVC) |
| `redis.yaml` | Deployment + Service | Cache/lock için Redis |
| `rabbitmq.yaml` | Deployment + Service | Olay yayını için RabbitMQ (+ management UI) |

Bu ayrımın kendisi önemli bir ders: **Deployment** değiştirilebilir, kimliksiz kopyalar içindir (API, Redis, RabbitMQ — hangi pod'un hangisi olduğu önemsiz, sadece "3 tane çalışsın" önemli). **StatefulSet** ise Postgres gibi *kimliği ve diski olan* iş yükleri içindir — pod'un adı sabittir (`postgres-0`), yeniden başlasa bile **aynı** disk parçasına (PVC) bağlanır. docker-compose'daki `volumes: pgdata:/var/lib/postgresql/data` satırının k8s karşılığı, StatefulSet'in `volumeClaimTemplates`'i:

```yaml
volumeClaimTemplates:
  - metadata:
      name: postgres-data
    spec:
      accessModes: ["ReadWriteOnce"]
      resources:
        requests:
          storage: 1Gi
```

### Migration Job — neden ayrı bir dosya

`docker-compose`'da migration sorunu yoktu çünkü tek bir `api` container'ı vardı. K8s'te `deployment.yaml` **3 replika** istiyor — üçü de aynı anda ayağa kalkarsa, üçü de aynı anda `MigrateAndSeedHostedService`'i tetikleyip aynı veritabanı şemasına yarışarak migration atmaya çalışır. Çözüm, migration'ı **Deployment'tan tamamen çıkarıp** ayrı bir `Job` yapmak:

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: modularcommerce-migrate
spec:
  backoffLimit: 3
  ttlSecondsAfterFinished: 300
  template:
    spec:
      restartPolicy: Never
      containers:
        - name: migrate
          image: ghcr.io/onurnafiguzel/modularcommerce:IMAGE_TAG
          env:
            - name: RUN_MIGRATIONS
              value: "true"
          envFrom:
            - configMapRef: { name: modularcommerce-config }
            - secretRef: { name: modularcommerce-secrets }
```

`Job`, `Deployment`'tan farklı bir birincil kavram: "bu pod'u sürekli ayakta tut" değil, **"bu pod'u bir kere çalıştır, işi bitince kapat"** demek. `restartPolicy: Never` + `backoffLimit: 3` = "3 deneme hakkı ver, hepsi başarısız olursa pes et". `ttlSecondsAfterFinished: 300` = "bittikten 5 dakika sonra kendini temizle, cluster'da çöp bırakma". Aynı imaj, `RUN_MIGRATIONS=true` env'iyle çalıştığında `Program.cs`'teki dallanma migration'ı çalıştırıp süreci kapatıyor; `Deployment`'taki normal pod'larda bu env yok, dolayısıyla normal HTTP sunucusu olarak ayağa kalkıyorlar. **Aynı imaj, iki farklı çalışma modu** — bu, k8s'te çok sık görülen bir desendir.

---

## Bölüm 3 — Pod ve Container: aynı şey değil

Docker'la çalışırken "container" tek birimdir. K8s'te değil — **Pod**, bir veya daha fazla container'ı saran, k8s'in yönettiği en küçük birimdir.

```
┌─────────────────────────────┐
│           Pod                │   ← k8s bunu bir bütün olarak zamanlar,
│  ┌────────────────────────┐  │      izler, yeniden başlatır
│  │   Container: api        │  │
│  │   (ModularCommerce.dll) │  │
│  └────────────────────────┘  │
│  Paylaşılan: ağ (IP), disk   │
└─────────────────────────────┘
```

ModularCommerce'te her pod'da **tek** container var (`api`, `postgres`, `redis`, `rabbitmq`) — bu en yaygın durum. Ama bir pod birden fazla container da taşıyabilir (örn. bir "sidecar" — logları toplayan ayrı bir yardımcı container); o zaman aynı pod içindeki container'lar ağı ve `localhost`'u paylaşır. Bizim senaryomuzda bunu görmüyoruz ama ismin neden "pod" (container **çekirdeği/grubu**) olduğunu bu açıklıyor.

**Deployment → ReplicaSet → Pod → Container** zinciri:

```
Deployment (modularcommerce-api, replicas: 3)
  │
  ▼
ReplicaSet  ──"3 pod ayakta olsun" kuralını uygular
  │
  ├── Pod #1  →  Container: api  (image: ...IMAGE_TAG)
  ├── Pod #2  →  Container: api  (image: ...IMAGE_TAG)
  └── Pod #3  →  Container: api  (image: ...IMAGE_TAG)
```

Siz doğrudan Pod yönetmiyorsunuz — `Deployment`'a "3 kopya istiyorum" diyorsunuz, o arka planda bir `ReplicaSet` oluşturuyor, `ReplicaSet` de bu sayıyı **sürekli** doğru tutuyor. Bir pod çökerse (`kubectl delete pod ...` ile siz silseniz bile) ReplicaSet bunu **saniyeler içinde** fark edip yenisini başlatır — docker-compose'da `docker compose up`'ı elle tekrar çalıştırmanız gerekirken, burada bu kendiliğinden olur. Buna **self-healing** deniyor ve orkestrasyonun asıl satın aldığınız şeylerden biri budur.

`Service` ise bu 3 pod'un önüne konan sabit bir kapı:

```
                    Service: modularcommerce-api
                    (sabit DNS adı + port 80)
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
           Pod #1          Pod #2          Pod #3
        (IP değişken)   (IP değişken)   (IP değişken)
```

Pod'ların IP'leri her yeniden başlatmada değişir — bu yüzden hiçbir zaman bir pod'un IP'sine doğrudan güvenmezsiniz. `Service`, `selector: app: modularcommerce-api` etiketine sahip **her** pod'u otomatik bulur ve trafiği aralarında dağıtır; siz hep aynı sabit adı (`modularcommerce-api`) kullanırsınız. `secret.yaml`'daki `Host=postgres` de aynı mantık — `postgres` bir pod IP'si değil, Postgres StatefulSet'inin önündeki Service'in adı.

---

## Bölüm 4 — `kind` ile yerelde deneme

`kind` (Kubernetes IN Docker), gerçek bir k8s cluster'ının düğümlerini (node) birer Docker container'ı olarak ayağa kaldırır. Yani AKS/EKS gibi bir bulut hesabı açmadan, kendi makinenizde gerçek bir `kubectl apply` deneyimi yaşarsınız.

```bash
kind create cluster --name modularcommerce
kubectl cluster-info --context kind-modularcommerce
```

### Tuzak: yerel imaj kind'in içinden görünmez

Bu, k8s'e yeni başlayanların en çok takıldığı nokta: `docker build -t modularcommerce:local .` ile imajı kendi Docker daemon'ınızda oluşturursunuz, ama **`kind`'in node container'ları kendi ayrı containerd önbelleğini kullanır** — sizin yerel Docker imaj listenizi görmez. `kubectl apply` sonrası pod'lar `ImagePullBackOff` hatasıyla takılı kalır çünkü `ghcr.io/onurnafiguzel/modularcommerce:...` imajını registry'den çekmeye çalışır ve bulamaz (private ise erişemez, public ise sizin yerel değişikliklerinizi yansıtmaz).

İki çözüm var:

1. **Yerel imajı doğrudan cluster'a yükle** (registry'ye hiç gitmeden):
   ```bash
   docker build -t modularcommerce:local -f src/Bootstrapper/ModularCommerce.Host/Dockerfile .
   kind load docker-image modularcommerce:local --name modularcommerce
   ```
   Sonra `k8s/deployment.yaml` ve `k8s/migration-job.yaml`'daki `image:` satırını geçici olarak `modularcommerce:local` yapıp `imagePullPolicy: IfNotPresent` eklemeniz gerekir (yoksa k8s yine de registry'den çekmeye çalışır).
2. **CI'daki gibi gerçek bir registry kullan** — `IMAGE_TAG` yerine commit SHA'sını `sed` ile basıp GHCR'den çekmesine izin ver (internet bağlantısı ve imajın public/erişilebilir olması gerekir).

Yerelde pratik/öğrenme amaçlıysanız 1. yöntem daha hızlı ve ücretsizdir — tam olarak CI/CD'nin yaptığı "imajı bas, sonra cluster'a bildir" akışının yerel simülasyonu.

### Sırayla apply etmek

`kubectl apply -f k8s/` klasördeki her dosyayı gönderir ama **sıra önemli** — API, Postgres/Redis/RabbitMQ'ya bağlanamadan `Running` olmaz (readinessProbe bunu zaten engeller ama gereksiz yere `CrashLoopBackOff` görmemek için mantıklı sıra):

```bash
# 1) Altyapı önce
kubectl apply -f k8s/postgres.yaml -f k8s/redis.yaml -f k8s/rabbitmq.yaml

# 2) Config + Secret
kubectl apply -f k8s/configmap.yaml -f k8s/secret.yaml

# 3) Migration — bitmesini bekle
kubectl apply -f k8s/migration-job.yaml
kubectl wait --for=condition=complete job/modularcommerce-migrate --timeout=120s

# 4) Uygulama
kubectl apply -f k8s/deployment.yaml -f k8s/service.yaml
```

### Multi-pod'u gözünüzle görmek

```bash
kubectl get pods
```
```
NAME                                   READY   STATUS    RESTARTS   AGE
modularcommerce-api-7d9f8c6b5d-abcde   1/1     Running   0          40s
modularcommerce-api-7d9f8c6b5d-fghij   1/1     Running   0          40s
modularcommerce-api-7d9f8c6b5d-klmno   1/1     Running   0          40s
postgres-0                             1/1     Running   0          2m
redis-...                              1/1     Running   0          2m
rabbitmq-...                           1/1     Running   0          2m
```
Üç `modularcommerce-api-*` satırı — aynı `Deployment`'tan doğmuş 3 ayrı pod, her biri kendi rastgele son ekiyle. `postgres-0` ise StatefulSet'e özgü **sıralı, sabit** isimlendirme (`<name>-<ordinal>`) — bu isim yeniden başlasa bile değişmez, bu yüzden "state"ful.

Self-healing'i canlı görmek için:
```bash
kubectl delete pod modularcommerce-api-7d9f8c6b5d-abcde
kubectl get pods -w    # birkaç saniye içinde yeni bir pod doğduğunu izleyin
```

Ve Service'in gerçekten trafiği 3 pod'a dağıttığını görmek için (cluster içinden bir test pod'u açıp):
```bash
kubectl run curltest --rm -it --image=curlimages/curl -- sh
# içeride:
curl http://modularcommerce-api/
```
Bunu birkaç kez tekrarlayın — `Service`, `kube-proxy` üzerinden isteği pod'lar arasında döndürür; docker-compose'da tek `api` container'ı varken bu kavram hiç yoktu.

---

## Bölüm 5 — Bunu yerelde pratik etmenin faydası

- **Gerçek hataları ucuza yaşarsınız.** `ImagePullBackOff`, yanlış `selector` label'ı, eksik `envFrom`, `readinessProbe` hiç geçmeyen bir pod — bunların hepsini AKS faturası ödemeden, `kind delete cluster` ile sıfırlayarak deneyebilirsiniz.
- **CI/CD'nin neden var olduğunu anlarsınız.** `release.yml`'deki `sed` + `kubectl apply` + `rollout status` üçlüsü, siz elle yaptığınız "imajı yükle, dosyayı düzenle, apply et, bekle" adımlarının otomasyonundan başka bir şey değil. Elle bir kere yapan, otomasyonun neyi gizlediğini gerçekten kavrar.
- **"Neden Job, neden Deployment değil" sorusunun cevabı somutlaşır.** Migration'ı yanlışlıkla `Deployment` yapıp 3 replikanın veritabanına yarışarak migrate etmeye çalıştığını görmek (isterseniz kasıtlı olarak deneyip loglardan `duplicate column`/lock hatalarını görebilirsiniz), bu ayrımı ezbere değil, deneyerek öğretir.
- **AKS'ye geçiş bir "öğrenme sıçraması" değil, bir "hedef değişikliği" olur.** `k8s/` klasöründeki dosyalar zaten cluster-agnostic — aynı YAML'lar `kind`'de de, AKS'de de çalışır. Yerelde doğrulanmış bir manifest seti, prod'a giderken sürpriz çıkarmaz.
- **Kaynak sınırlarını, health check'leri, self-healing'i izole bir ortamda kırıp düzeltme özgürlüğü verir.** Bir pod'u silmek, bir Service'i yanlış selector ile bozmak, bir Secret'ı eksik bırakmak — prod'da yapamayacağınız deneyleri `kind`'de saniyeler içinde yapıp geri alabilirsiniz.

---

## Kapanış

`docker-compose.yml` "bu servisler birlikte çalışsın" der. Kubernetes manifestleri ise "bu servisler **kaç kopya**, **hangi kimlikle**, **kim çökünce ne olacak**, **migration gibi tek seferlik işler nasıl izole edilecek** sorularına da cevap verir. `kind`, bu sorulara verdiğiniz cevapları gerçek bir bulut faturası ödemeden test etmenizi sağlayan en ucuz yol — ve ModularCommerce'in `k8s/` klasörü, bu sorulara verilmiş somut, çalışan cevapların toplamı.
