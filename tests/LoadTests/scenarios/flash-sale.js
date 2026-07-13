// Flash sale senaryosu (Hafta 11 — uçtan uca sertleştirme kanıtı):
//   Düşük stoklu SICAK bir ürüne rampalı (ramping-arrival-rate) checkout yükü. Farklı
//   kullanıcılar yarışır; asıl kanıt: SIFIR OVERSELL (commit edilen adet başlangıç stoğunu
//   ASLA aşmaz — onHand negatife düşmez) ve checkout p95 < 500 ms (NFR-5.1) yük altında.
//   Yanıt karışımı raporlanır: 201 (satış) + 409 InsufficientStock (stok bitti, doğru) +
//   429 (rate limit, korunma çalışıyor) + retryable-409 (çakışma, aynı key tekrar).
//
// RATE LIMITING ETKİLEŞİMİ (kritik):
//   "auth" policy login/signup'ı IP bazlı sıkı sınırlar (brute-force koruması). Yük üreteci
//   TEK IP'den yüzlerce signup yaptığından, auth limiti bu senaryonun kendi kullanıcı
//   üretimini de boğar. Bu senaryo checkout OVERSELL'ini ölçer, auth throttle'ını DEĞİL —
//   bu yüzden Host'u auth limiti GEVŞETİLMİŞ çalıştırın:
//     RateLimiting__Auth__PermitLimit=100000  RateLimiting__Auth__WindowSeconds=1
//   ("checkout" policy default kalır: burst-absorbing, oversell testinin konusu). Auth
//   throttle'ının kendisi ayrıca kanıtlanır (README rate-limit demo: /login'e ard arda istek → 429).
//   PSP gecikmesini de sıfırlayın: Payment__Psp__LatencyMs=0. Host Development modunda olmalı.
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';
import exec from 'k6/execution';

const BASE = __ENV.BASE_URL || 'http://localhost:49822';
const PASSWORD = 'k6-gizli-sifre-123';
const RUN_ID = Date.now();
const HOT_STOCK = Number(__ENV.HOT_STOCK || 50); // sıcak ürünün başlangıç stoğu
const MAX_RETRY = 30;

// 201 (satış), 200 (replay), 409 (çakışma/stok-bitti) ve 429 (rate limit) tasarlanmış yanıtlardır.
http.setResponseCallback(http.expectedStatuses(200, 201, 409, 429));

const checkouts201 = new Counter('checkouts_201');
const soldout409 = new Counter('soldout_409');
const retryable409 = new Counter('retryable_409');
const ratelimited429 = new Counter('ratelimited_429');

export const options = {
  scenarios: {
    flash_sale: {
      executor: 'ramping-arrival-rate',
      startRate: 20,
      timeUnit: '1s',
      preAllocatedVUs: 200,
      maxVUs: Number(__ENV.MAX_VUS || 600),
      stages: [
        { target: 100, duration: '10s' }, // ısınma
        { target: 500, duration: '20s' }, // ani dalga (flash)
        { target: 500, duration: '10s' }, // tepe yük
        { target: 0, duration: '5s' },    // iniş
      ],
      exec: 'flashSale',
    },
  },
  thresholds: {
    'http_req_duration{endpoint:checkout}': ['p(95)<500'], // NFR-5.1
    checks: ['rate>0.99'],
    // http_req_failed dahil edilmez: 409/429 response-callback ile "expected" sayılır.
  },
};

export function setup() {
  const products = http.get(`${BASE}/api/catalog/products?page=1&pageSize=1`);
  if (products.status !== 200) {
    exec.test.abort(`Katalog okunamadı: HTTP ${products.status}`);
  }
  const productId = JSON.parse(products.body).items[0].id;

  // Sıcak ürünü düşük, deterministik stoğa reset et (rezervasyonlar dahil temizlenir).
  const stock = http.put(
    `${BASE}/api/inventory/dev/stock/${productId}`,
    JSON.stringify({ onHand: HOT_STOCK }),
    { headers: { 'Content-Type': 'application/json' } },
  );
  if (stock.status !== 200) {
    exec.test.abort(`Dev stok basılamadı (Host Development modunda mı?): HTTP ${stock.status}`);
  }

  return { productId, initialStock: HOT_STOCK };
}

let token = null;

function authenticate() {
  // VU başına bir kez: her VU ayrı kullanıcı (flash-sale'de farklı alıcılar yarışır).
  const email = `k6-flash-${RUN_ID}-vu${exec.vu.idInTest}@test.local`;
  const headers = { 'Content-Type': 'application/json' };

  http.post(`${BASE}/api/identity/signup`, JSON.stringify({ email, password: PASSWORD }), { headers });

  const login = http.post(`${BASE}/api/identity/login`, JSON.stringify({ email, password: PASSWORD }), { headers });
  if (login.status !== 200) {
    exec.test.abort(`Login başarısız (auth limiti gevşetildi mi?): HTTP ${login.status}`);
  }
  return JSON.parse(login.body).accessToken;
}

function randomKey() {
  return `flash-${RUN_ID}-${exec.vu.idInTest}-${exec.vu.iterationInScenario}-${Math.random().toString(36).slice(2)}`;
}

function addToCart(productId, headers) {
  http.post(`${BASE}/api/cart/items`, JSON.stringify({ productId, quantity: 1 }), { headers });
}

function checkoutOnce(headers, key) {
  return http.post(`${BASE}/api/ordering/checkout`, null, {
    headers: { ...headers, 'Idempotency-Key': key },
    tags: { endpoint: 'checkout' },
  });
}

// Terminal yanıta kadar aynı key ile devam (gerçek istemci kontratı):
//   retryable-409 → hemen tekrar; 429 → Retry-After kadar bekle sonra tekrar.
function checkoutUntilTerminal(headers, key) {
  for (let attempt = 0; attempt < MAX_RETRY; attempt++) {
    const res = checkoutOnce(headers, key);

    if (res.status === 429) {
      ratelimited429.add(1);
      const retryAfter = Number(res.headers['Retry-After'] || '1');
      sleep(retryAfter + Math.random() * 0.2);
      continue;
    }

    if (res.status === 409) {
      const title = JSON.parse(res.body).title;
      if (title === 'Inventory.ConcurrencyConflict' || title === 'Inventory.LockTimeout'
        || title === 'Payment.InFlight' || title === 'Payment.PspUnavailable') {
        retryable409.add(1);
        sleep(0.005 + Math.random() * 0.01); // jitter — thundering herd kırıcı
        continue;
      }
      // Inventory.InsufficientStock (ve diğer terminal 409'lar): stok bitti — flash-sale'de beklenir.
      return res;
    }

    return res; // 201 / 200 / terminal
  }
  return null;
}

export function flashSale(data) {
  if (!token) {
    token = authenticate();
  }
  const headers = { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` };

  addToCart(data.productId, headers);

  const res = checkoutUntilTerminal(headers, randomKey());

  if (res && res.status === 201) {
    checkouts201.add(1);
  } else if (res && res.status === 409 && JSON.parse(res.body).title === 'Inventory.InsufficientStock') {
    soldout409.add(1);
  }

  // Her yanıt ya satıştır ya da tasarlanmış bir ret (stok bitti / hız sınırı) olmalı — hata değil.
  check(res, {
    'yanıt tasarlanmış (201 satış | 409 stok-bitti)': (r) =>
      r !== null && (r.status === 201
        || (r.status === 409 && JSON.parse(r.body).title === 'Inventory.InsufficientStock')),
  });
}

export function teardown(data) {
  // OVERSELL KANITI: commit edilen adet başlangıç stoğunu aşamaz → onHand negatife DÜŞMEZ,
  // available >= 0. Sweeper/in-flight için kısa bekleme sonrası okunur.
  sleep(2);
  const res = http.get(`${BASE}/api/inventory/stock/${data.productId}`);
  const s = res.json();
  const sold = data.initialStock - s.onHand;
  const oversell = s.onHand < 0 ? -s.onHand : 0;

  console.log(
    `FLASH SALE SONUÇ: başlangıç=${data.initialStock} onHand=${s.onHand} reserved=${s.reserved} ` +
    `available=${s.available} SATILAN=${sold} OVERSELL=${oversell}`,
  );

  if (oversell > 0 || s.available < 0) {
    exec.test.abort(`OVERSELL TESPİT EDİLDİ: onHand=${s.onHand} available=${s.available}`);
  }
}
