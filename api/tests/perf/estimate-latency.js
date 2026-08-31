// SC-002: the price estimate is shown to the client within 5 s (p95).
// Run against a seeded stack:  k6 run api/tests/perf/estimate-latency.js
import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE = __ENV.API_BASE_URL || 'http://localhost:8080/v1';

export const options = {
  scenarios: {
    steady: { executor: 'constant-vus', vus: 20, duration: '2m' },
  },
  thresholds: {
    // SC-002 — the full estimate round trip (includes the route provider / fallback).
    'http_req_duration{name:estimate}': ['p(95)<5000'],
    // Constitution perf goal for simple reads/writes.
    'http_req_duration{name:login}': ['p(95)<300'],
    http_req_failed: ['rate<0.01'],
  },
};

export function setup() {
  const email = `perf-${Date.now()}@example.com`;
  const reg = http.post(`${BASE}/auth/register`, JSON.stringify({
    name: 'Perf', email, phone: '+5511900000000', password: 's3nhaForte1', roles: ['client'],
  }), { headers: { 'content-type': 'application/json' } });
  return { token: reg.json('accessToken') };
}

export default function (data) {
  const headers = { 'content-type': 'application/json', authorization: `Bearer ${data.token}` };

  const login = http.post(`${BASE}/auth/login`,
    JSON.stringify({ email: 'perf@example.com', password: 'x' }),
    { headers, tags: { name: 'login' } });
  check(login, { 'login responded': (r) => r.status === 200 || r.status === 401 });

  const estimate = http.post(`${BASE}/pricing/estimate`, JSON.stringify({
    origin: { text: 'Paulista', point: { lat: -23.5613, lng: -46.656 } },
    destination: { text: 'Ibirapuera', point: { lat: -23.5874, lng: -46.6576 } },
    estimatedWeightKg: 30,
  }), { headers, tags: { name: 'estimate' } });
  check(estimate, { 'estimate 200': (r) => r.status === 200 });

  sleep(1);
}
