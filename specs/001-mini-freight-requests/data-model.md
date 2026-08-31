# Phase 1 Data Model: Mini-Freight Requests (MVP)

Persistência em PostgreSQL + PostGIS. Convenções: PK `id` (uuid v7), `created_at`/`updated_at`
(timestamptz), soft-delete apenas onde LGPD exigir rastro; caso contrário exclusão real.
Todo valor monetário em `numeric(12,2)` + `currency` (`BRL` no MVP). Distâncias em metros,
pesos em gramas (inteiro) para evitar float.

## Módulo: Accounts

### User
| Campo | Tipo | Regras |
|-------|------|--------|
| id | uuid | PK |
| name | text | obrigatório, 2–120 chars |
| email | citext | único, formato válido |
| phone | text | obrigatório, E.164 |
| password_hash | text | via ASP.NET Identity |
| roles | text[] | subconjunto de {`client`,`professional`} não vazio |
| status | text | `active` \| `suspended` \| `deletion_requested` |
| created_at / updated_at | timestamptz | |

### ClientProfile
| Campo | Tipo | Regras |
|-------|------|--------|
| user_id | uuid | PK/FK → User (role `client`) |

### ProfessionalProfile
| Campo | Tipo | Regras |
|-------|------|--------|
| user_id | uuid | PK/FK → User (role `professional`) |
| max_load_grams | int | > 0 |
| immediate_availability | bool | default false |
| last_location | geography(Point,4326) | nullable |
| last_location_at | timestamptz | nullable; "recente" se `now - last_location_at ≤ location_ttl` |
| verification_status | text | `nao_verificado` (default) \| `em_analise` \| `verificado` \| `rejeitado` |

**Transições de disponibilidade**: `immediate_availability` só pode ser `true` se o profissional
não tiver `Trip` ativa. Ao aceitar uma oferta imediata → torna-se indisponível até
conclusão/cancelamento.

### ProfessionalScheduleAvailability
| Campo | Tipo | Regras |
|-------|------|--------|
| id | uuid | PK |
| professional_id | uuid | FK → ProfessionalProfile |
| available_date | date | ≥ hoje |
| — | | único (`professional_id`,`available_date`) |

### ProfessionalDailyLoad  *(agregado derivado, mantido transacionalmente)*
| Campo | Tipo | Regras |
|-------|------|--------|
| professional_id | uuid | FK |
| load_date | date | |
| accepted_count | int | ≤ `max_schedules_per_date` (config, default 1) |
| — | | PK (`professional_id`,`load_date`) |

### VerificationEvent  *(append-only)*
| Campo | Tipo | Regras |
|-------|------|--------|
| id | uuid | PK |
| professional_id | uuid | FK |
| from_status / to_status | text | |
| reason | text | nullable |
| actor | text | `system` \| `operator:<id>` |
| occurred_at | timestamptz | |

### DeviceToken
| Campo | Tipo | Regras |
|-------|------|--------|
| id | uuid | PK |
| user_id | uuid | FK → User |
| platform | text | `ios` \| `android` |
| token | text | único |
| last_seen_at | timestamptz | |

### DataSubjectRequest  *(LGPD — FR-030)*
| Campo | Tipo | Regras |
|-------|------|--------|
| id | uuid | PK |
| user_id | uuid | FK |
| kind | text | `access` \| `rectification` \| `deletion` |
| status | text | `open` \| `fulfilled` \| `rejected` |
| requested_at / resolved_at | timestamptz | |

## Módulo: Pricing

### PricingRule
| Campo | Tipo | Regras |
|-------|------|--------|
| id | uuid | PK |
| base_fare | numeric(12,2) | ≥ 0 |
| per_km | numeric(12,2) | ≥ 0 |
| per_kg | numeric(12,2) | ≥ 0 |
| min_price | numeric(12,2) | ≥ 0 |
| effective_from | timestamptz | |
| effective_to | timestamptz | nullable; vigente = janela cobre `now` |

**Cálculo (FR-008/009)**: `preço = max(min_price, base_fare + per_km * dist_km + per_kg *
weight_kg)`. `distance_source` registrado na requisição (`routed` \| `geodesic_fallback`).

## Módulo: Requests

### TransportRequest
| Campo | Tipo | Regras |
|-------|------|--------|
| id | uuid | PK |
| client_id | uuid | FK → ClientProfile |
| items | jsonb | lista de `{description, qty}`, ≥ 1 item |
| estimated_weight_grams | int | > 0 |
| origin_address | text | obrigatório |
| origin_point | geography(Point,4326) | resolvido na criação |
| destination_address | text | obrigatório |
| destination_point | geography(Point,4326) | resolvido; ≠ origem |
| distance_meters | int | ≥ 0 |
| distance_source | text | `routed` \| `geodesic_fallback` |
| estimated_price | numeric(12,2) | da PricingRule vigente |
| pricing_rule_id | uuid | FK (snapshot da regra usada) |
| kind | text | `immediate` \| `scheduled` |
| scheduled_date | date | obrigatório se `kind=scheduled`; entre amanhã e `now + scheduling_window_days` |
| status | text | ver máquina de estados |
| assigned_professional_id | uuid | nullable, FK |
| created_at / updated_at | timestamptz | |

**Máquina de estados** (`status`):

```
draft ──confirm(immediate)──▶ searching
searching ──professional aceita──▶ hired
searching ──esgotou/limite (FR-017a)──▶ awaiting_schedule_decision
awaiting_schedule_decision ──cliente escolhe data──▶ scheduled_searching
awaiting_schedule_decision ──cliente recusa──▶ unfulfilled
scheduled_searching ──profissional aceita (1º)──▶ scheduled
scheduled_searching ──ninguém até a data──▶ unfulfilled
hired | scheduled ──início──▶ (Trip: em_andamento)
hired | scheduled ──Trip.status ∈ {entregue, confirmada, contestada}──▶ completed
(qualquer ativo) ──cliente/profissional cancela──▶ cancelled
```

Regras:
- `awaiting_schedule_decision` expira em `schedule_decision_timeout` (config, default 10 min) →
  `unfulfilled` (suporta SC-007 medindo decisão explícita vs. abandono).
- `completed` é um estado **consolidado** da requisição, derivado de `Trip`. Após `hired` /
  `scheduled` a requisição não tem lógica própria — o ciclo de vida segue em `Trip`
  (contratada → em_andamento → entregue → confirmada | contestada). `completed` é atingido
  quando o `Trip` chega a `entregue` (a contestação posterior não reverte o `completed`; ela
  apenas marca o `Trip` como `contestada` para acompanhamento).
- Índice: `(status)` parcial para `searching`/`scheduled_searching`; GiST em `origin_point`.

### Mapeamento de estados (spec ↔ modelo)

| Spec (Key Entities) | `TransportRequest.status` | `Trip.status` |
|---------------------|---------------------------|---------------|
| rascunho            | draft                     | —             |
| buscando            | searching / scheduled_searching | —       |
| aguardando decisão  | awaiting_schedule_decision | —            |
| agendada            | scheduled                 | —             |
| contratada          | hired / scheduled         | contratada / em_andamento |
| concluída           | completed                 | entregue / confirmada |
| contestada          | completed                 | contestada    |
| não atendida        | unfulfilled               | —             |
| cancelada           | cancelled                 | cancelada     |

## Módulo: Matching

### MatchingSession
| Campo | Tipo | Regras |
|-------|------|--------|
| id | uuid | PK |
| request_id | uuid | FK único → TransportRequest |
| mode | text | `immediate` \| `scheduled` |
| state | text | `searching` \| `offer_pending` \| `accepted` \| `exhausted` \| `timed_out` |
| started_at | timestamptz | |
| deadline_at | timestamptz | `started_at + max_search_duration` (config, default 5 min) |
| contacted_count | int | encerra em `max_professionals_contacted` (config, default 8) |
| current_offer_id | uuid | nullable → Offer |

### Offer
| Campo | Tipo | Regras |
|-------|------|--------|
| id | uuid | PK |
| session_id | uuid | FK → MatchingSession |
| request_id | uuid | FK (desnormalizado p/ consulta) |
| professional_id | uuid | FK |
| type | text | `immediate` \| `scheduled` |
| sent_at | timestamptz | |
| respond_by | timestamptz | `sent_at + 30s` (config `offer_ttl`) para `immediate` |
| outcome | text | `pending` \| `accepted` \| `declined` \| `expired` \| `filled_by_other` |
| responded_at | timestamptz | nullable |

Regras:
- **Imediata**: no máximo 1 `Offer` com `outcome=pending` por `professional_id` (FR-011b) e por
  `session_id` (FR-013). Aceite válido só se `now ≤ respond_by` (FR-015); senão → `expired`.
- **Agendamento**: várias `Offer` `pending` simultâneas (uma por profissional notificado);
  primeiro `accepted` transaciona o request para `scheduled`, demais → `filled_by_other`
  (FR-022).
- Espelho de estado ativo em Redis: chave `offer:{id}` com TTL = `offer_ttl` (só imediata).

## Módulo: Trips

### Trip
| Campo | Tipo | Regras |
|-------|------|--------|
| id | uuid | PK |
| request_id | uuid | FK único |
| client_id / professional_id | uuid | FK |
| status | text | `contratada` \| `em_andamento` \| `entregue` \| `confirmada` \| `contestada` \| `cancelada` |
| agreed_amount | numeric(12,2) | default = `estimated_price`; editável enquanto `status ∈ {contratada, em_andamento}` |
| delivered_at | timestamptz | setado na marcação de entrega pelo profissional |
| client_response | text | nullable `confirmada` \| `contestada` |
| client_responded_at | timestamptz | nullable |
| verification_notified_at | timestamptz | setado pelo job 24 h se sem resposta |
| payment_settled_outside_app | bool | default false |
| settled_at | timestamptz | nullable |

**Transições**:
```
contratada ─▶ em_andamento ─(profissional marca entrega)▶ entregue
entregue ─(cliente confirma)▶ confirmada
entregue ─(cliente contesta)▶ contestada
entregue ─(24h sem resposta: job)▶ mantém 'entregue' + verification_notified_at; notifica ambos
contratada|em_andamento ─(cancelamento)▶ cancelada  (reabre matching imediato / reoferta agendamento)
```
- `entregue` já libera o profissional (`immediate_availability` volta a poder ser `true`;
  `Trip` deixa de contar como ativa) — FR-011a/025b.
- Contestação registra `AuditEvent` (FR-025e).

## Módulo: Notifications

### NotificationOutbox  *(outbox transacional — Princípio VI)*
| Campo | Tipo | Regras |
|-------|------|--------|
| id | uuid | PK |
| dedupe_key | text | único (`{event_type}:{recipient_id}:{aggregate_id}`) |
| recipient_user_id | uuid | FK |
| type | text | `offer_received` \| `offer_result` \| `request_status` \| `schedule_offer` \| `trip_delivered` \| `trip_verification` \| ... |
| payload | jsonb | dados para render do push |
| state | text | `pending` \| `sent` \| `failed` |
| attempts | int | retry com backoff |
| created_at / sent_at | timestamptz | |

## Cross-cutting

### AuditEvent  *(append-only — FR-032, Constituição §Security)*
| Campo | Tipo | Regras |
|-------|------|--------|
| id | uuid | PK |
| actor | text | `user:<id>` \| `system` \| `operator:<id>` |
| action | text | `offer.sent` \| `offer.accepted` \| `offer.declined` \| `offer.expired` \| `request.assigned` \| `request.cancelled` \| `trip.delivered` \| `trip.disputed` \| `verification.changed` \| `datasubject.*` |
| aggregate_type / aggregate_id | text / uuid | |
| metadata | jsonb | sem dados sensíveis desnecessários |
| occurred_at | timestamptz | |

### IdempotencyKey
| Campo | Tipo | Regras |
|-------|------|--------|
| key | text | PK (header `Idempotency-Key`) |
| request_hash | text | corpo + rota |
| response_snapshot | jsonb | |
| created_at | timestamptz | TTL 24 h |

### Config (`AppSettings` / tabela `configuration`)
Parâmetros ajustáveis sem deploy (FR-009, FR-014, FR-017a, FR-019, FR-022a, FR-025d, FR-012a):
`offer_ttl_seconds=30`, `max_search_duration_seconds=300`, `max_professionals_contacted=8`,
`schedule_decision_timeout_seconds=600`, `scheduling_window_days=30`,
`max_schedules_per_date=1`, `delivery_verification_hours=24`, `location_ttl_seconds=300`,
`immediate_offer_radius_meters=15000`, `sinuosity_factor=1.3`, pricing (`base_fare`, `per_km`,
`per_kg`, `min_price`).

## Índices principais
- `professional_profile` GiST(`last_location`); parcial `WHERE immediate_availability`.
- `transport_request` parcial `WHERE status IN ('searching','scheduled_searching')`.
- `offer` único parcial `WHERE outcome='pending'` em (`professional_id`) para imediatas.
- `professional_schedule_availability` único (`professional_id`,`available_date`).
- `notification_outbox` (`state`,`created_at`); `audit_event` (`aggregate_type`,`aggregate_id`).
