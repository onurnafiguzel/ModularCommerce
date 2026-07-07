// Sepet smoke testi (NFR-4.1: okuma/yazma p95 < 50 ms — auth middleware
// maliyeti DAHİL, dürüst ölçüm). Her VU KENDİ hesabı ve KENDİ sepetiyle çalışır:
// tek sepette 20 VU'nun read-modify-write yarışı (last-write-wins, NFR-4.2)
// kayıp güncellemeyi 404'e çevirirdi — ölçülen şey gecikme, AP davranışı değil.
// İterasyon = POST /items + GET + DELETE: sepet küçük kalır, satır tavanlarına
// (10 adet/satır, 50 satır) çarpılmaz; productId'ler doğrulanmaz (FR-4.3 Hafta 6).
import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';
import exec from 'k6/execution';

const BASE = __ENV.BASE_URL || 'http://localhost:49822';
const PASSWORD = 'k6-gizli-sifre-123';
const RUN_ID = Date.now(); // init-context: her koşuda benzersiz e-postalar

// Signup 201 tasarlanmış yanıttır (VU başına bir kez); gerisi 200 bekler.
http.setResponseCallback(http.expectedStatuses(200, 201));

const cartWrites = new Counter('cart_writes');
const cartReads = new Counter('cart_reads');

export const options = {
  scenarios: {
    steady: {
      executor: 'constant-vus',
      vus: 20,
      duration: '30s',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<50'], // NFR-4.1 — JWT doğrulama + Redis dahil
    http_req_failed: ['rate<0.01'],
  },
};

let token = null;

function authenticate() {
  const email = `k6-cart-${RUN_ID}-vu${exec.vu.idInTest}@test.local`;
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

function randomGuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}

export default function () {
  if (!token) {
    token = authenticate(); // VU başına bir kez; sayısı ihmal edilebilir (20/binlerce)
  }

  const headers = {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`,
  };
  const productId = randomGuid();

  const add = http.post(`${BASE}/api/cart/items`,
    JSON.stringify({ productId, quantity: 1 }), { headers });
  cartWrites.add(1);

  const get = http.get(`${BASE}/api/cart`, { headers });
  cartReads.add(1);

  const del = http.del(`${BASE}/api/cart/items/${productId}`, null, { headers });
  cartWrites.add(1);

  check(add, { 'POST /items 200': (r) => r.status === 200 });
  check(get, { 'GET /cart 200': (r) => r.status === 200 });
  check(del, { 'DELETE /items 200': (r) => r.status === 200 });
}
