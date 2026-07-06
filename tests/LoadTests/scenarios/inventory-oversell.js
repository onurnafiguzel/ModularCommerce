// Oversell senaryosu (NFR-3.1): 10 stokluk ürüne 1000 eşzamanlı istek.
// Üç stratejiyle ayrı ayrı koşulur (Host restart + Inventory__ReservationStrategy env).
// İstemci davranışı gerçek istemciyi taklit eder:
//   - 409 Inventory.ConcurrencyConflict → RETRY ("tekrar deneyin" — optimistic'in sözleşmesi)
//   - 409 Inventory.LockTimeout        → RETRY (yoğunluk — redis lock'ın sözleşmesi)
//   - 409 Inventory.InsufficientStock  → TERMİNAL (stok bitti, dur)
// STRICT=1 ile koşulursa (Optimistic/RedisLock) tam-10 threshold'u uygulanır.
import http from 'k6/http';
import { Counter } from 'k6/metrics';

const BASE = __ENV.BASE_URL || 'http://localhost:49822';
const PRODUCT = '11111111-1111-1111-1111-111111111111'; // seed: OnHand 10
const MAX_RETRY = 20;

const successfulReservations = new Counter('successful_reservations');
const concurrencyConflicts = new Counter('concurrency_conflicts');
const lockTimeouts = new Counter('lock_timeouts');
const soldOutRejections = new Counter('sold_out_rejections');

export const options = {
  scenarios: {
    burst: {
      executor: 'per-vu-iterations',
      vus: 1000,
      iterations: 1,
      maxDuration: '2m',
    },
  },
  thresholds: __ENV.STRICT
    ? { successful_reservations: ['count<=10'] }
    : {},
};

export function setup() {
  // Deterministik başlangıç: dev-only reset endpoint'i (yalnız Development).
  const res = http.put(
    `${BASE}/api/inventory/dev/stock/${PRODUCT}`,
    JSON.stringify({ onHand: 10 }),
    { headers: { 'Content-Type': 'application/json' } },
  );
  if (res.status !== 200) {
    throw new Error(`Stok reset başarısız: HTTP ${res.status} — Host Development modunda mı?`);
  }
}

export default function () {
  for (let attempt = 0; attempt < MAX_RETRY; attempt++) {
    const res = http.post(
      `${BASE}/api/inventory/reservations`,
      JSON.stringify({ productId: PRODUCT, quantity: 1 }),
      { headers: { 'Content-Type': 'application/json' } },
    );

    if (res.status === 201) {
      successfulReservations.add(1);
      return;
    }

    if (res.status === 409) {
      const title = res.json('title');
      if (title === 'Inventory.ConcurrencyConflict') {
        concurrencyConflicts.add(1);
        continue; // tekrar deneyin
      }
      if (title === 'Inventory.LockTimeout') {
        lockTimeouts.add(1);
        continue; // yoğunluk — tekrar deneyin
      }
      if (title === 'Inventory.InsufficientStock') {
        soldOutRejections.add(1);
        return; // terminal
      }
    }

    return; // beklenmeyen durum — sayaçlara girmez, http_req_failed yakalar
  }
}

export function teardown() {
  const res = http.get(`${BASE}/api/inventory/stock/${PRODUCT}`);
  const s = res.json();
  console.log(
    `SONUÇ: onHand=${s.onHand} reserved=${s.reserved} available=${s.available} ` +
    `OVERSELL=${s.reserved - s.onHand > 0 ? s.reserved - s.onHand : 0}`,
  );
}
