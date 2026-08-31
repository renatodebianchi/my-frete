# SLOs & alerts — my-frete API

The API exports OTLP metrics/traces/logs to the collector. Wire these into your metrics backend
(the collector's `exporters` in `deploy/otel-collector-config.yaml` currently just prints).

## SLOs (initial targets)

| SLO | Target | Source |
|-----|--------|--------|
| API availability | 99.5% monthly | `/ready` probe + 5xx rate on `http.server.request.duration` |
| Price estimate latency | p95 ≤ 5 s (SC-002) | `POST /v1/pricing/estimate` duration |
| Simple read/write latency | p95 ≤ 300 ms | `http.server.request.duration` excluding `/pricing/*` |
| Immediate assignment | ≥ 80% within the search limit (SC-003) | `matching_session` duration histogram / `outcome=accepted` ratio |
| Offer window accuracy | 100% ≥ 30 s, 0 late accepts honoured (SC-004) | `offer` `respond_by - sent_at`; `409 matching.offer_closed` count |

## Alerts

- **Error-budget burn** (fast + slow burn) on availability and estimate-latency SLOs.
- `OutboxMessage` rows in `state = failed` > 0 for 5 min → page.
- Any background worker not ticking (no log line for 3× its poll interval).
- Vulnerability scan (Trivy, in CI) — HIGH/CRITICAL blocks the merge, so a live alert here
  means a base-image CVE published after merge: rebuild + redeploy.

## Dashboards

- **RED per endpoint** (rate, errors, duration) from `http.server.*`.
- **Matching funnel**: sessions started → offers sent → accepted / expired / exhausted.
- **Scheduling funnel**: broadcasts → scheduled offers → accepted / filled_by_other / unfulfilled.
- **Route provider**: call rate, error rate, `distance_source = geodesic_fallback` ratio.
