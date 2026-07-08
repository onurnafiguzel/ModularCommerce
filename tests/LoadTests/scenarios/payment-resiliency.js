// Payment resiliency koşuları (Hafta 7). İki mod, Host'un PSP env ayarına göre
// AYRI koşulur (strateji-karşılaştırma reçetesindeki gibi Host restart gerekir):
//
//   -e MODE=breaker  → Host: Payment__Psp__FailureRate=0.5 ile başlatılmalı.
//     Kanıt §10.3: %50 transient hata altında circuit breaker'ın AÇILIP KAPANDIĞI
//     Host'un Serilog çıktısında görülür (Polly telemetrisi: "Circuit breaker
//     state changed"). K6 tarafı 409 Payment.PspUnavailable sayacını raporlar —
//     breaker açıkken istekler PSP'ye gitmeden hızlı reddedilir.
//
//   -e MODE=declined → Host: Payment__Psp__DeclineRate=1 ile başlatılmalı.
//     Kanıt "ödeme reddi → sızıntı yok": her checkout 409 Payment.Declined döner,
//     sipariş YAZILMAZ ve koşu sonunda sıcak ürünün reserved sayacı 0'dır.
//
// Host Development modunda olmalı (dev stok endpoint'i kullanılır).
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';
import exec from 'k6/execution';

const BASE = __ENV.BASE_URL || 'http://localhost:49822';
const MODE = __ENV.MODE || 'breaker';
const PASSWORD = 'k6-gizli-sifre-123';
const RUN_ID = Date.now();

// 4xx/5xx'in tamamı bu koşuda "tasarlanmış" kabul edilir: declined 409, breaker 409,
// timeout 409 — hepsi resiliency davranışının gözlem konusu.
http.setResponseCallback(http.expectedStatuses(200, 201, 409));

const checkouts201 = new Counter('checkouts_201');
const declined409 = new Counter('declined_409');
const pspUnavailable409 = new Counter('psp_unavailable_409');
const timeout409 = new Counter('timeout_409');
const inventoryRetry409 = new Counter('inventory_retryable_409');
const inflight409 = new Counter('payment_inflight_409');

export const options = MODE === 'breaker'
  ? {
      scenarios: {
        breaker_load: {
          executor: 'constant-vus',
          vus: 20,
          duration: '60s',
          exec: 'breakerLoad',
        },
      },
      // Breaker koşusunda eşik yok: amaç open/close döngüsünün gözlemi (§10.3).
    }
  : {
      scenarios: {
        declined_leak: {
          executor: 'per-vu-iterations',
          vus: 5,
          iterations: 8, // sepet satır tavanına (10) çarpmadan
          exec: 'declinedLeak',
        },
      },
      thresholds: {
        checks: ['rate>0.99'],
      },
    };

export function setup() {
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
  const email = `k6-resiliency-${RUN_ID}-${scenario}-vu${exec.vu.idInTest}@test.local`;
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
  return `k6r-${RUN_ID}-${exec.vu.idInTest}-${exec.vu.iterationInScenario}-${Math.random().toString(36).slice(2)}`;
}

function addToCart(data, headers) {
  const res = http.post(`${BASE}/api/cart/items`,
    JSON.stringify({ productId: data.productId, quantity: 1 }), { headers });
  check(res, { 'sepete ekleme 200': (r) => r.status === 200 });
}

function classify(res) {
  if (res.status === 201) {
    checkouts201.add(1);
    return;
  }
  if (res.status !== 409) {
    return;
  }
  const title = JSON.parse(res.body).title;
  if (title === 'Payment.Declined') declined409.add(1);
  else if (title === 'Payment.PspUnavailable') pspUnavailable409.add(1);
  else if (title === 'Payment.Timeout') timeout409.add(1);
  else if (title === 'Payment.InFlight') inflight409.add(1);
  else inventoryRetry409.add(1); // Inventory.ConcurrencyConflict/LockTimeout
}

// MODE=breaker: sürekli yük; retryable'da AYNI key ile devam (istemci sözleşmesi),
// terminalde (201 / Declined / Timeout) iterasyon biter.
export function breakerLoad(data) {
  if (!token) {
    token = authenticate('breaker');
  }
  const headers = { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` };

  addToCart(data, headers);

  const key = randomKey();
  for (let attempt = 0; attempt < 30; attempt++) {
    const res = http.post(`${BASE}/api/ordering/checkout`, null, {
      headers: { ...headers, 'Idempotency-Key': key },
      tags: { endpoint: 'checkout' },
    });
    classify(res);

    if (res.status !== 409) {
      return;
    }
    const title = JSON.parse(res.body).title;
    if (title === 'Payment.Declined' || title === 'Payment.Timeout') {
      return; // terminal — yeni iterasyon yeni key alacak
    }
    sleep(0.02 + Math.random() * 0.03);
  }
}

// MODE=declined: her checkout'un TERMİNALİ Payment.Declined olmalı (sıcak üründe
// rezervasyon adımı retryable Inventory-409 üretebilir — istemci sözleşmesi gereği
// aynı key ile terminale taşınır); sızıntı kanıtı teardown'da.
export function declinedLeak(data) {
  if (!token) {
    token = authenticate('declined');
  }
  const headers = { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` };

  addToCart(data, headers);

  const key = randomKey();
  let res = null;
  for (let attempt = 0; attempt < 30; attempt++) {
    res = http.post(`${BASE}/api/ordering/checkout`, null, {
      headers: { ...headers, 'Idempotency-Key': key },
      tags: { endpoint: 'checkout' },
    });
    classify(res);

    const retryable = res.status === 409
      && ['Inventory.ConcurrencyConflict', 'Inventory.LockTimeout',
          'Payment.InFlight', 'Payment.PspUnavailable']
        .includes(JSON.parse(res.body).title);
    if (!retryable) {
      break;
    }
    sleep(0.005 + Math.random() * 0.01);
  }

  check(res, {
    'checkout terminali 409 Payment.Declined': (r) =>
      r !== null && r.status === 409 && JSON.parse(r.body).title === 'Payment.Declined',
  });
}

export function teardown(data) {
  if (MODE !== 'declined') {
    return;
  }

  // SIZINTI KANITI: tüm checkout'lar reddedildi → rezervasyonların TAMAMI release
  // edilmiş olmalı; sıcak üründe reserved 0'dır.
  const stock = http.get(`${BASE}/api/inventory/stock/${data.productId}`);
  const body = JSON.parse(stock.body);
  check(stock, {
    'ödeme reddi sonrası reserved=0 (sızıntı yok)': () => body.reserved === 0,
  });
  console.log(`SIZINTI KONTROLÜ: reserved=${body.reserved} onHand=${body.onHand}`);
}
