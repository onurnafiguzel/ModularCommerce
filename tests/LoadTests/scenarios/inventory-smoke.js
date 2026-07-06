// Rezervasyon gecikme smoke testi (NFR-3.2): p95 < 150 ms — kilit bekleme dahil.
// Bol stoklu ürünü (2222..., OnHand 1M) hedefler: çakışma/tükenme gürültüsü olmadan
// saf yazma yolu gecikmesi ölçülür. RedisLock stratejisiyle koşulması anlamlıdır
// (kilit edinme maliyeti ölçüme dahil olur).
import http from 'k6/http';
import { check } from 'k6';
import { Counter } from 'k6/metrics';

const BASE = __ENV.BASE_URL || 'http://localhost:49822';
const PRODUCT = '22222222-2222-2222-2222-222222222222'; // seed: OnHand 1.000.000

// 409, tasarlanmış iş yanıtıdır (ConcurrencyConflict/LockTimeout — "tekrar deneyin"),
// transport hatası değil: http_req_failed yalnızca gerçek hataları (5xx vb.) saysın.
// Tek sıcak satıra 50 VU sürekli yük = bilinçli contention benchmark'ı; başarı oranı
// stratejilerin etkin yazma kapasitesini gösterir (sonuç counter'larından okunur).
http.setResponseCallback(http.expectedStatuses(201, 409));

const reservations201 = new Counter('reservations_201');
const retryable409 = new Counter('retryable_409');

export const options = {
  scenarios: {
    steady: {
      executor: 'constant-vus',
      vus: 50,
      duration: '30s',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<150'], // NFR-3.2 — kilit/çakışma bekleme dahil
    http_req_failed: ['rate<0.01'],   // gerçek hatalar (5xx)
  },
};

export function setup() {
  const res = http.put(
    `${BASE}/api/inventory/dev/stock/${PRODUCT}`,
    JSON.stringify({ onHand: 1000000 }),
    { headers: { 'Content-Type': 'application/json' } },
  );
  if (res.status !== 200) {
    throw new Error(`Stok reset başarısız: HTTP ${res.status}`);
  }
}

export default function () {
  const res = http.post(
    `${BASE}/api/inventory/reservations`,
    JSON.stringify({ productId: PRODUCT, quantity: 1 }),
    { headers: { 'Content-Type': 'application/json' } },
  );

  if (res.status === 201) {
    reservations201.add(1);
  } else if (res.status === 409) {
    retryable409.add(1);
  }

  check(res, { '201 veya 409 (tasarlanmış yanıtlar)': (r) => r.status === 201 || r.status === 409 });
}
