// Checkout smoke (Hafta 6):
//   1) checkout_latency — NFR-5.1: checkout zinciri (sepet + Catalog doğrulama +
//      rezervasyon + sipariş insert) p95 < 500 ms; tek satırlık sepetle ölçülür
//      (NFR tanımı "zincir"dir; çok satırın lineer maliyeti hafta notlarında).
//   2) idempotency_burst — FR-5.4 YÜK ALTINDA: aynı Idempotency-Key ile 5 PARALEL
//      checkout → terminal durumda tam 1×201 + 4×200 ve hepsi AYNI sipariş.
//
// RETRYABLE 409 SÖZLEŞMESİ (Hafta 3-4'ten beri geçerli istemci kontratı):
// tüm VU'lar AYNI sıcak ürünü rezerve eder → OptimisticConcurrency stratejisinde
// Inventory.ConcurrencyConflict tasarlanmış "tekrar dene" yanıtıdır. İstemci AYNI
// Idempotency-Key ile tekrar dener — idempotency tam da bu senaryo için var.
// (RedisLock stratejisiyle koşulursa çakışmalar beklemeye döner, retry azalır.)
//
// Her VU kendi hesabı/sepetiyle çalışır (cart-smoke deseni). Host Development
// modunda olmalı (dev stok endpoint'i kullanılır).
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';
import exec from 'k6/execution';

const BASE = __ENV.BASE_URL || 'http://localhost:49822';
const PASSWORD = 'k6-gizli-sifre-123';
const RUN_ID = Date.now();
const MAX_RETRY = 30;

// 201 (yeni sipariş), 200 (replay) ve 409 (retryable çakışma) tasarlanmış yanıtlardır.
http.setResponseCallback(http.expectedStatuses(200, 201, 409));

const checkouts201 = new Counter('checkouts_201');
const replays200 = new Counter('replays_200');
const retryable409 = new Counter('retryable_409');

export const options = {
  scenarios: {
    checkout_latency: {
      executor: 'constant-vus',
      vus: 20,
      duration: '30s',
      exec: 'checkoutLatency',
    },
    idempotency_burst: {
      executor: 'per-vu-iterations',
      vus: 5,
      iterations: 10,
      startTime: '32s', // gecikme ölçümünü kirletmesin diye ayrık pencere
      exec: 'idempotencyBurst',
    },
  },
  thresholds: {
    'http_req_duration{endpoint:checkout}': ['p(95)<500'], // NFR-5.1
    http_req_failed: ['rate<0.01'],
    checks: ['rate>0.99'],
  },
};

export function setup() {
  // Catalog seed id'leri rastgele, Inventory'ninkiler sabit — kesişim yok:
  // ürün katalogdan seçilir, stoğu dev endpoint'iyle basılır (kod değişikliği sıfır).
  const products = http.get(`${BASE}/api/catalog/products?page=1&pageSize=1`);
  if (products.status !== 200) {
    exec.test.abort(`Katalog okunamadı: HTTP ${products.status}`);
  }
  const productId = JSON.parse(products.body).items[0].id;

  const stock = http.put(
    `${BASE}/api/inventory/dev/stock/${productId}`,
    JSON.stringify({ onHand: 1000000 }),
    { headers: { 'Content-Type': 'application/json' } },
  );
  if (stock.status !== 200) {
    exec.test.abort(`Dev stok basılamadı (Host Development modunda mı?): HTTP ${stock.status}`);
  }

  return { productId };
}

let token = null;

function authenticate(scenario) {
  const email = `k6-checkout-${RUN_ID}-${scenario}-vu${exec.vu.idInTest}@test.local`;
  const headers = { 'Content-Type': 'application/json' };

  http.post(`${BASE}/api/identity/signup`,
    JSON.stringify({ email, password: PASSWORD }), { headers });

  const login = http.post(`${BASE}/api/identity/login`,
    JSON.stringify({ email, password: PASSWORD }), { headers });
  if (login.status !== 200) {
    exec.test.abort(`Login başarısız: HTTP ${login.status}`);
  }

  return JSON.parse(login.body).accessToken;
}

function randomKey() {
  return `k6-${RUN_ID}-${exec.vu.idInTest}-${exec.vu.iterationInScenario}-${Math.random().toString(36).slice(2)}`;
}

function addToCart(data, headers) {
  const res = http.post(`${BASE}/api/cart/items`,
    JSON.stringify({ productId: data.productId, quantity: 1 }), { headers });
  check(res, { 'sepete ekleme 200': (r) => r.status === 200 });
}

function isRetryable(res) {
  if (res.status !== 409) {
    return false;
  }
  const title = JSON.parse(res.body).title;
  return title === 'Inventory.ConcurrencyConflict' || title === 'Inventory.LockTimeout';
}

function checkoutOnce(headers, key) {
  return http.post(`${BASE}/api/ordering/checkout`, null, {
    headers: { ...headers, 'Idempotency-Key': key },
    tags: { endpoint: 'checkout' },
  });
}

/// Retryable 409'da AYNI key ile tekrar dener (idempotency kontratı); terminal yanıtı döner.
function checkoutUntilTerminal(headers, key) {
  for (let attempt = 0; attempt < MAX_RETRY; attempt++) {
    const res = checkoutOnce(headers, key);
    if (!isRetryable(res)) {
      return res;
    }
    retryable409.add(1);
    sleep(0.005 + Math.random() * 0.01); // jitter — thundering herd kırıcı
  }
  return null;
}

export function checkoutLatency(data) {
  if (!token) {
    token = authenticate('lat');
  }
  const headers = { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` };

  addToCart(data, headers);

  const res = checkoutUntilTerminal(headers, randomKey());

  if (res && res.status === 201) {
    checkouts201.add(1);
  }
  check(res, { 'checkout terminal 201': (r) => r !== null && r.status === 201 });
}

export function idempotencyBurst(data) {
  if (!token) {
    token = authenticate('burst');
  }
  const headers = { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` };

  addToCart(data, headers);

  // Çift tık fırtınası: AYNI key ile 5 paralel checkout (FR-5.4).
  const key = randomKey();
  const request = {
    method: 'POST',
    url: `${BASE}/api/ordering/checkout`,
    body: null,
    params: { headers: { ...headers, 'Idempotency-Key': key }, tags: { endpoint: 'checkout' } },
  };
  const initial = http.batch([request, request, request, request, request]);

  // Retryable alanlar aynı key ile terminale kadar devam eder (gerçek istemci davranışı).
  const finals = initial.map((r) => (isRetryable(r) ? checkoutUntilTerminal(headers, key) : r));

  const created = finals.filter((r) => r !== null && r.status === 201);
  const replayed = finals.filter((r) => r !== null && r.status === 200);
  const ids = new Set(finals
    .filter((r) => r !== null && (r.status === 200 || r.status === 201))
    .map((r) => JSON.parse(r.body).id));

  checkouts201.add(created.length);
  replays200.add(replayed.length);

  check(finals, {
    'terminalde tam 1×201 (tek sipariş yaratıldı)': () => created.length === 1,
    'terminalde 4×200 (replay; 400/409 terminal YOK)': () => replayed.length === 4,
    '5 yanıt da aynı siparişi gösteriyor': () => ids.size === 1,
  });
}
