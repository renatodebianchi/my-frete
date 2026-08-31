# my-frete API

.NET 9 modular monolith for the mini-freight MVP. Modules under `src/Modules/*` talk only
through published interfaces and versioned events (`specs/001-mini-freight-requests/contracts/`);
none reads another module's tables. Docker-first (Constitution §VIII): the OCI image is the unit
of build/run/deploy.

## Run locally

```bash
cd ../deploy
cp .env.example .env          # set ROUTE_PROVIDER_API_KEY / JWT_SIGNING_KEY if you want
docker compose up             # api + postgres+postgis + redis + otel-collector
docker compose run --rm api seed --demo   # demo pricing rule + config defaults
```

- API: `http://localhost:8080` — Swagger at `/swagger`, health at `/health` and `/ready`.
- Migrations are applied automatically on container start (`RunMigrationsOnStartup=true`).
- Hot-reload from source: `docker compose -f docker-compose.yml -f docker-compose.override.yml up api`.

To edit/debug outside a container you need **.NET SDK 9** and `dotnet tool restore`
(`dotnet-ef` is a local tool used only to author migrations).

## Tests

```bash
dotnet test MyFrete.sln            # unit + contract + integration (Testcontainers spins up PG/Redis)
dotnet format MyFrete.sln --verify-no-changes
```

Integration tests boot the real API against throwaway PostGIS/Redis containers, apply
migrations, and override two settings: `AppConfig:CacheSeconds=0` (no config cache) and
`RateLimiting:PermitPerMinute` (raised so polling loops don't hit 429).

## Modules

| Module | Owns | Key endpoints |
|--------|------|---------------|
| Accounts | users, roles, client/professional profiles, verification, LGPD | `/v1/auth/*`, `/v1/accounts/me`, `/v1/professionals/me*`, `/v1/privacy/*` |
| Pricing | `PricingRule`, route distance | `POST /v1/pricing/estimate` |
| Requests | `TransportRequest` lifecycle | `/v1/requests*` |
| Matching | `MatchingSession` + `Offer`, 30s offer orchestrator | `/v1/offers/*` |
| Scheduling | availability, daily load, `ScheduledOffer` | `/v1/professionals/me/schedule-availability`, `/v1/schedule-offers/*` |
| Trips | `Trip` lifecycle, 24h verification job | `/v1/trips/*` |
| Notifications | device tokens, push fan-out (Expo Push / no-op fallback) | `POST /v1/accounts/me/devices` |

## Operations

- **Config**: business parameters (`offer_ttl_seconds`, `max_professionals_contacted`,
  `max_schedules_per_date`, `delivery_verification_hours`, pricing, …) live in the
  `configuration` table and are hot-reloadable (30s cache).
- **Background workers** (all idempotent, safe on multiple replicas): `OutboxDispatcher`,
  `OfferOrchestrator`, `ScheduleDecisionTimeoutJob`, `DeliveryVerificationJob`,
  `ScheduledUnfulfilledJob`, `LocationRetentionJob`.
- **Deploy**: CI builds and scans the image, then publishes it to GHCR on merge to `main`.
  Roll out with a canary and roll back by redeploying the previous image tag. Watch the
  `/ready` probe and the matching-session-duration / offer-acceptance-rate metrics for SLO burn.
- **Observability**: OTLP to the collector; Serilog JSON logs carry `TraceId` and
  `CorrelationId` (propagated from the app's `x-correlation-id` header).
- **Incident/LGPD**: security-relevant actions are in `audit_event`; a deletion request
  anonymises the account immediately and revokes refresh tokens.
