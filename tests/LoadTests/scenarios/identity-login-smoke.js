// Login smoke testi (NFR-1.1: p95 < 200 ms, hash doğrulama maliyeti DAHİL;
// NFR-1.4: 200 RPS sürdürülebilir login dalgası).
// constant-arrival-rate: k6 hedef RPS'i VU sayısından bağımsız sabitler —
// yanıtlar yavaşlarsa VU eklenir, yük düşmez (gerçek kampanya öncesi dalga).
import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';

const BASE = __ENV.BASE_URL || 'http://localhost:49822';
const PASSWORD = 'k6-gizli-sifre-123';

const logins200 = new Counter('logins_200');

export const options = {
  scenarios: {
    login_wave: {
      executor: 'constant-arrival-rate',
      rate: 200,            // NFR-1.4
      timeUnit: '1s',
      duration: '30s',
      preAllocatedVUs: 100,
      maxVUs: 400,
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<200'], // NFR-1.1 — PBKDF2 doğrulaması dahil
    http_req_failed: ['rate<0.01'],
  },
};

// Seeder yazılmadı (bilinçli): k6 setup'taki signup zaten seeder'dır,
// koda test kimlik bilgisi gömülmez.
export function setup() {
  const email = `k6-login-${Date.now()}@test.local`;

  const res = http.post(
    `${BASE}/api/identity/signup`,
    JSON.stringify({ email, password: PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } },
  );
  if (res.status !== 201) {
    throw new Error(`Signup başarısız: HTTP ${res.status}`);
  }

  return { email };
}

export default function (data) {
  const res = http.post(
    `${BASE}/api/identity/login`,
    JSON.stringify({ email: data.email, password: PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } },
  );

  if (res.status === 200) {
    logins200.add(1);
  }

  check(res, { 'login 200': (r) => r.status === 200 });
}
