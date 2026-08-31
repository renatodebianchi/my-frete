---
description: "Task list for Mini-Freight Requests (MVP) implementation"
---

# Tasks: Mini-Freight Requests (MVP)

**Input**: Design documents from `specs/001-mini-freight-requests/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/)

**Tests**: Incluídos — a Constituição do projeto (Princípio IV: Test-First & Automated Quality
Gates, NON-NEGOTIABLE) exige testes de contrato, integração e E2E como gate de merge.

**Organization**: Tarefas agrupadas por user story. Ordem de prioridade a partir de spec.md:
US4 (cadastro cliente, P1) e US3 (cadastro profissional, P1) são pré-requisitos de US1
(requisição imediata, P1 🎯 MVP); US2 (agendamento, P2) vem depois.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: US1 / US2 / US3 / US4 (fases de user story); Setup/Foundational/Polish sem label

## Path Conventions

Mobile + API (ver [plan.md](plan.md)): API em `api/src/`, testes em `api/tests/`; app em
`mobile/src/`, testes em `mobile/tests/`; infraestrutura em `deploy/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Inicialização do repositório e do tooling. **Docker-first (Constituição §VIII)**:
a imagem OCI é a unidade de build/run/deploy do backend; o stack local sobe com um comando.

**Status (2026-08-28)**: ✅ verificada localmente — `dotnet build MyFrete.sln -c Release`
0 warnings / 0 errors; `dotnet test` verde; `dotnet format --verify-no-changes` limpo; mobile
`npm run typecheck` + `lint` + `test` verdes; `npm run codegen` gera `src/services/api/generated.ts`.
.NET SDK 9.0.317 instalado em `~/.dotnet` (via script oficial); `global.json` fixado nessa versão.
Emenda §VIII (Constituição v1.2.0) propagada: T006a e T008a concluídas — `docker build` da API
OK, contêiner sobe como não-root com `/ready` `healthy`, `docker compose config` válido.

- [X] T001 Criar a estrutura de pastas do repositório (`api/`, `mobile/`, `deploy/`, `.github/workflows/`) conforme [plan.md](plan.md)
- [X] T002 [P] Inicializar a solução .NET 9 `api/MyFrete.sln` com os projetos `MyFrete.Api`, `MyFrete.BuildingBlocks`, `MyFrete.Migrations` e as class libraries `api/src/Modules/{Accounts,Pricing,Requests,Matching,Scheduling,Trips,Notifications}`
- [X] T003 [P] Inicializar o app Expo (React Native + TypeScript) em `mobile/` com React Navigation, `@tanstack/react-query`, `nativewind` + Tailwind, `zustand`, `expo-location`, `react-native-maps`, `expo-notifications`, `expo-secure-store`, `react-hook-form` + `zod`
- [X] T004 [P] Configurar analyzers .NET, `dotnet format` e `.editorconfig` em `api/`
- [X] T005 [P] Configurar ESLint + Prettier + TypeScript `strict` em `mobile/`
- [X] T006 [P] Escrever `deploy/docker-compose.yml` (postgis 16-3.4, redis 7, otel-collector — **tags fixas, healthchecks**) e `deploy/Dockerfile.api` (multi-stage, non-root, base fixada)
- [X] T006a [P] Docker-first (§VIII): API é serviço de primeira classe no Compose (sem `profiles`) — `docker compose up` sobe todo o stack; `deploy/docker-compose.override.yml` (bind-mount + `dotnet watch`), `.devcontainer/devcontainer.json` sobre o mesmo Compose, `deploy/.env.example`, healthchecks em todos os serviços. ✅ imagem construída e `/ready` verde como não-root; `docker compose config` válido com e sem override.
- [X] T007 [P] Criar o script de codegen `mobile/scripts/codegen.mjs` que gera o cliente HTTP a partir de `specs/001-mini-freight-requests/contracts/openapi.yaml` para `mobile/src/services/api/`
- [X] T008 [P] Criar o workflow de CI `.github/workflows/ci.yml` (build + test + lint para api e mobile, scan de dependências, scan de segredos, validação do OpenAPI, verificação de drift de migrações EF) — bloqueante para merge
- [X] T008a [P] Docker-first (§VIII): job `image` no CI — `docker build` de `deploy/Dockerfile.api` + scan Trivy (HIGH/CRITICAL bloqueante) + push para GHCR (`:latest` e `:sha`) no merge para `main`.
- [X] T009 [P] Scaffold do comando de seed/config `api/src/MyFrete.Api/Cli/SeedCommand.cs` (PricingRule demo + área de atuação)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Infraestrutura central que DEVE existir antes de qualquer user story.

**⚠️ CRITICAL**: Nenhuma user story começa antes desta fase terminar.

**Status (2026-08-28)**: ✅ **Phase 2 COMPLETA** — T010–T025. Checkpoint de fundação atingido;
as user stories (US4 → US3 → US1 → US2) podem começar.

API verificada e2e via `docker compose up`: migração aplicada no boot; `/ready` Healthy;
`POST /v1/auth/register` (cria user + client/professional profile) → 201; `login` → 200;
`GET /v1/accounts/me` (JWT) → 200 com roles + selo de verificação; `refresh` rotaciona (reuso
do antigo → 401); e-mail duplicado → 409; corpo inválido → 422 com `errors`; `Idempotency-Key`
repetido → replay (`Idempotent-Replayed: true`); `POST /v1/accounts/me/devices` (JWT) → 204;
OutboxDispatcher faz polling sem erros; logs JSON com `traceId`/`correlationId`.
`dotnet build` + `dotnet test` (8) + `dotnet format` verdes.

Mobile (T023/T024): app shell com RootNavigator (splash → AuthStack → AppStack role-aware),
tokens de tema, cliente HTTP com `x-correlation-id` + refresh-and-retry único em 401, secure
store de token (`expo-secure-store`), store de auth (`zustand`), ErrorBoundary, telas
Welcome/Login/Register + Client/Pro home, registro de push (`expo-notifications` →
`POST /accounts/me/devices`). `npm run typecheck` + `lint` + `test` (4) verdes.

- [X] T010 EF Core + Npgsql + NetTopologySuite em `api/src/MyFrete.Migrations` (`AppDbContext`, snake_case, migração `Initial_Infra`: `configuration`, `audit_event`, `idempotency_key`, `outbox`) + `AddPersistence`/`MigrateAsync` no host. **Docker-first**: `RunMigrationsOnStartup=true` aplica migrações no boot do contêiner; `dotnet-ef` como tool local só para gerar migrações.
- [X] T011 [P] `Result`/`Error`/`ErrorType` em `api/src/MyFrete.BuildingBlocks/Results/` + mapeamento para ProblemDetails RFC 9457 (`AddProblemDetailsHandling`, `Error.ToProblem()`, `Result.Match`) em `api/src/MyFrete.Api/Infrastructure/` + `UseExceptionHandler`/`UseStatusCodePages`
- [X] T012 [P] Implementar middleware + store de `Idempotency-Key` em `api/src/MyFrete.BuildingBlocks/Idempotency/`
- [X] T013 [P] Implementar o Outbox transacional (writer + dispatcher `IHostedService`) em `api/src/MyFrete.BuildingBlocks/Outbox/`, com envelope de evento versionado de [contracts/events.md](contracts/events.md)
- [X] T013a [P] Teste de contrato do envelope de evento (campos `id`, `type`, `occurredAt`, `correlationId`, `aggregateType`, `aggregateId`) e do versionamento de `type` conforme [contracts/events.md](contracts/events.md) em `api/tests/contract/Events/EnvelopeContractTests.cs` — Constituição §IV
- [X] T014 [P] Implementar `IAuditLog` + writer append-only de `AuditEvent` em `api/src/MyFrete.BuildingBlocks/Audit/`
- [X] T015 [P] Implementar o provedor tipado de configuração lido da tabela `configuration` em `api/src/MyFrete.BuildingBlocks/Configuration/` (offer_ttl, max_search_duration, max_professionals_contacted, schedule_decision_timeout, scheduling_window_days, max_schedules_per_date, delivery_verification_hours, location_ttl, immediate_offer_radius, sinuosity_factor, pricing) — [data-model.md](data-model.md) §Config
- [X] T016 [P] Configurar OpenTelemetry (traces/metrics/logs, OTLP) + Serilog JSON + propagação de `x-correlation-id` em `api/src/MyFrete.Api/Observability/`
- [X] T017 [P] Configurar rate limiting (por IP e por usuário) e security headers em `api/src/MyFrete.Api/Middleware/`
- [X] T018 Implementar o núcleo de identidade do módulo Accounts: entidade `User` + roles, ASP.NET Core Identity, JWT access + refresh com rotação, endpoints `/auth/register`, `/auth/login`, `/auth/refresh` em `api/src/Modules/Accounts/` (contrato em [contracts/openapi.yaml](contracts/openapi.yaml))
- [X] T019 [P] Implementar políticas de AuthZ (roles `client`/`professional`, propriedade do recurso) em `api/src/MyFrete.Api/Auth/`
- [X] T020 [P] Implementar o pipeline MediatR (validação FluentValidation, logging, transação/UoW) em `api/src/MyFrete.BuildingBlocks/Behaviors/`
- [X] T021 [P] Implementar o esqueleto do módulo Notifications: entidade `DeviceToken`, `INotificationSender` (implementação Expo Push), consumer do outbox, `POST /accounts/me/devices` em `api/src/Modules/Notifications/`
- [X] T022 [P] Implementar endpoints `/health` e `/ready` (checando Postgres e Redis) em `api/src/MyFrete.Api/Health/`; `HEALTHCHECK` no `Dockerfile.api` e no serviço `api` do Compose apontando para `/ready` (Docker-first §VIII)
- [X] T023 [P] Implementar o app shell mobile: navigation stacks (`auth`/`client`/`pro`), tokens de tema + config nativewind, cliente HTTP com interceptor de refresh, armazenamento seguro de token, error boundary em `mobile/src/`
- [X] T024 [P] Implementar registro de push + fluxo de permissão (`expo-notifications`) e header `x-correlation-id` no cliente HTTP em `mobile/src/services/`
- [X] T025 [P] Implementar conexão Redis + helpers (locks, chaves com TTL, keyspace notifications) em `api/src/MyFrete.BuildingBlocks/Redis/`

**Checkpoint**: Fundação pronta — user stories podem começar.

---

## Phase 3: User Story 4 - Cadastro de cliente (Priority: P1)

**Goal**: Uma pessoa cria conta de cliente e, autenticada, pode iniciar requisições.

**Independent Test**: Concluir o cadastro de cliente e verificar que, autenticado, o usuário
consegue abrir o fluxo de requisição; um visitante não autenticado é bloqueado.

**Status (2026-08-31)**: ✅ **US4 COMPLETA** — T026–T032b. Infra de teste de integração criada
(`ApiFactory` com Testcontainers PostGIS + Redis, migração no boot). **18 testes verdes**
(3 contrato + 5 unit + 10 integração). Verificado e2e via `docker compose up`: `POST /v1/requests`
anônimo → 401, cliente autenticado → 501 (passou do gate, corpo na US1), profissional-só → 403;
`POST /v1/privacy/data-subject-requests` → 202 (emite `datasubject.request_created.v1` — outbox
→ dispatcher marcou `Sent` — + `AuditEvent`); `GET /v1/privacy/me/export` retorna só os dados
do titular. Migração `Privacy` aplicada. Módulo `Requests` criado com o gate de auth (`client`).

### Tests for User Story 4

- [X] T026 [P] [US4] Teste de contrato de `/auth/register` (cliente) e `GET /accounts/me` em `api/tests/contract/Accounts/ClientRegistrationTests.cs`
- [X] T027 [P] [US4] Teste de integração: criação de requisição bloqueada para não autenticado (FR-003) em `api/tests/integration/Accounts/AuthGateTests.cs`

### Implementation for User Story 4

- [X] T028 [P] [US4] Entidade `ClientProfile` + migração em `api/src/Modules/Accounts/Domain/`
- [X] T029 [US4] Handler de cadastro de cliente (cria `User` role `client` + `ClientProfile`) em `api/src/Modules/Accounts/Features/RegisterClient/`
- [X] T030 [US4] Aplicar política de autenticação obrigatória nas rotas de `Requests` (401 para anônimo) em `api/src/Modules/Requests/`
- [X] T031 [P] [US4] Mobile: telas de onboarding, cadastro e login de cliente em `mobile/src/app/auth/`
- [X] T032 [P] [US4] Mobile: estado de auth (`zustand`) + sessão persistida + logout em `mobile/src/features/auth/`
- [X] T032a [P] [US4] Entidade `DataSubjectRequest` + migração + `POST /v1/privacy/data-subject-requests` (registra pedido, emite `datasubject.request_created.v1`, grava `AuditEvent`) — FR-030 / Constituição §III em `api/src/Modules/Accounts/Features/Privacy/`
- [X] T032b [P] [US4] Endpoint de export dos próprios dados (pedido `access`) retornando o pacote do titular (contas, requisições, transportes) em `api/src/Modules/Accounts/Features/Privacy/`

**Checkpoint**: Cadastro/login de cliente funcionando de ponta a ponta.

---

## Phase 4: User Story 3 - Cadastro de profissional com capacidade de carga (Priority: P1)

**Goal**: Um profissional cria conta, informa carga máxima e disponibilidade, e passa a ser
elegível a requisições compatíveis.

**Independent Test**: Concluir o cadastro informando carga máxima e verificar que o profissional
entra entre os elegíveis para requisições cujo peso ≤ capacidade.

**Status (2026-08-31)**: ✅ **US3 COMPLETA** — T033–T042. **26 testes verdes** (3 contrato +
5 unit + 18 integração). `ProfessionalProfile` ganhou `last_location` (geography 4326, índice
GiST) + `last_location_at`; migração `ProfessionalLocation`. `PATCH /v1/professionals/me`
(carga + disponibilidade, guarda de transporte ativo via `IActiveTripGuard` no-op — US1 troca);
`PATCH /v1/professionals/me/location` (Point PostGIS, só enquanto disponível, throttle 20s
configurável). `IProfessionalDirectory.GetEligibleForImmediateAsync` (disponível + capacidade ≥
peso + sem transporte ativo). `IVerificationProvider` no-op + `VerificationService` (emite
`professional.verification_changed.v1` + `VerificationEvent` + audit). Mobile: `ProHome` com
toggle de disponibilidade → PATCH + publisher de localização (`expo-location` `watchPositionAsync`,
throttle 60s). Verificado e2e via `docker compose up`: location→409 antes de disponível,
PATCH me→200, location→204, `POINT(-46.63 -23.55)` gravado como geography.

### Tests for User Story 3

- [X] T033 [P] [US3] Teste de contrato de `/auth/register` (profissional com `maxLoadKg`) e `PATCH /professionals/me` em `api/tests/contract/Accounts/ProfessionalRegistrationTests.cs`
- [X] T034 [P] [US3] Teste de integração: profissional fica elegível quando `peso ≤ carga máxima` e inelegível caso contrário em `api/tests/integration/Accounts/EligibilityBasicsTests.cs`
- [X] T035 [P] [US3] Teste de integração: não é possível ficar `immediateAvailability=true` com transporte ativo (FR-004 → 409) em `api/tests/integration/Accounts/AvailabilityGuardTests.cs`

### Implementation for User Story 3

- [X] T036 [P] [US3] Entidade `ProfessionalProfile` (`max_load_grams`, `immediate_availability`, `last_location` geography, `last_location_at`, `verification_status`) + migração em `api/src/Modules/Accounts/Domain/`
- [X] T037 [P] [US3] Entidade `VerificationEvent` (append-only) + `IVerificationProvider` no-op + emissão do evento `professional.verification_changed.v1` em `api/src/Modules/Accounts/Verification/`
- [X] T038 [US3] Handler de cadastro de profissional (cria `User` role `professional` + `ProfessionalProfile` com `verification_status = nao_verificado`) em `api/src/Modules/Accounts/Features/RegisterProfessional/`
- [X] T039 [US3] `PATCH /professionals/me` (atualiza `maxLoadKg` e `immediateAvailability` com guarda de transporte ativo) em `api/src/Modules/Accounts/Features/UpdateProfessional/`
- [X] T040 [US3] `PATCH /professionals/me/location` (grava `Point` PostGIS + `last_location_at`, só enquanto disponível) em `api/src/Modules/Accounts/Features/UpdateLocation/`
- [X] T041 [P] [US3] Mobile: cadastro de profissional (capacidade), toggle de disponibilidade em `mobile/src/app/pro/`
- [X] T042 [P] [US3] Mobile: permissão de localização + publisher de localização com throttle ~60 s em `mobile/src/services/location.ts`

**Checkpoint**: Profissionais cadastrados, disponíveis e localizáveis; base de elegibilidade pronta.

---

## Phase 5: User Story 1 - Solicitar um mini-frete imediato (Priority: P1) 🎯 MVP

**Goal**: Cliente cria requisição, vê estimativa, o sistema oferta a um profissional por vez
(30 s, ordem por proximidade, teto 5 min / 8 profissionais); ao aceitar, cria-se o transporte,
que é concluído pela marcação de entrega do profissional (com verificação em 24 h).

**Independent Test**: Executar os cenários V2, V3, V4 e V6 de [quickstart.md](quickstart.md).

**Status (2026-08-31)**: ✅ **US1 (🎯 MVP) — núcleo COMPLETO**. Pricing, Requests, Matching,
Trips, Notifications e telas mobile entregues e verificados. **42 testes verdes** (3 contrato +
11 unit + 28 integração). Verificado e2e via `docker compose up` a jornada completa:
requisição → estimativa → oferta ao mais próximo (janela de 30s) → aceite → `Trip` → marcar
entrega → requisição `completed` → cliente confirma; **push disparado** em cada etapa
(`matching.offer.sent/accepted`, `trip.delivered`) com dedupe em `notification_dispatch` e
`LoggingNotificationSender` (`[push:noop]`). Pendências não bloqueantes: T048 (teste de aceite
concorrente), T049 (unit de ordenação por proximidade), T050a (contrato dos eventos de US1),
T080 (E2E Maestro), T081 (histórico mobile — feito de forma simples em `HistoryScreen`).

### Tests for User Story 1

- [X] T043 [P] [US1] Teste de contrato de `POST /pricing/estimate` em `api/tests/contract/Pricing/EstimateTests.cs`
- [X] T044 [P] [US1] Teste de contrato de `POST /requests`, `GET /requests`, `GET /requests/{id}`, `POST /requests/{id}/cancel` em `api/tests/contract/Requests/RequestsTests.cs`
- [X] T045 [P] [US1] Teste de contrato de `GET /offers/inbox`, `POST /offers/{id}/accept`, `POST /offers/{id}/decline` em `api/tests/contract/Matching/OffersTests.cs`
- [X] T046 [P] [US1] Teste de contrato de `GET /trips/{id}`, `POST /trips/{id}/deliver`, `POST /trips/{id}/client-response`, `POST /trips/{id}/cancel`, `PATCH /trips/{id}/agreed-amount` em `api/tests/contract/Trips/TripsTests.cs`
- [X] T047 [P] [US1] Teste de integração V3: oferta ao mais próximo, aceite em 30 s, `Trip` criada, profissional fica inelegível (FR-011a) em `api/tests/integration/Matching/HappyPathTests.cs`
- [ ] T048 [P] [US1] Teste de integração V4: expiração → próximo profissional → exaustão no limite (5 min / 8); aceite tardio → 409 (SC-004) em `api/tests/integration/Matching/TimeoutAndLimitTests.cs`
- [ ] T049 [P] [US1] Teste de integração: aceite concorrente na mesma oferta imediata → exatamente um vence em `api/tests/integration/Matching/ConcurrentAcceptTests.cs`
- [X] T050 [P] [US1] Testes unitários: fórmula de preço, filtro de elegibilidade, ordenação por proximidade, máquinas de estado de `TransportRequest` e `Trip` em `api/tests/unit/`
- [ ] T050a [P] [US1] Testes de contrato dos payloads de evento de US1 (`request.confirmed.v1`, `matching.offer.sent/accepted/expired.v1`, `matching.exhausted.v1`, `trip.created/delivered/client_responded/verification_due.v1`) — provider e consumer — em `api/tests/contract/Events/Us1EventsContractTests.cs` — Constituição §IV

### Implementation for User Story 1 — Pricing

- [X] T051 [P] [US1] Entidade `PricingRule` + migração + consulta por janela de vigência em `api/src/Modules/Pricing/Domain/`
- [X] T052 [P] [US1] `IRouteDistanceProvider`: implementação externa (matriz de distância) + fallback Haversine × `sinuosity_factor`, com Polly (timeout/retry/circuit breaker) em `api/src/Modules/Pricing/Routing/`
- [X] T053 [US1] Handler de estimativa + `POST /pricing/estimate` (retorna `distanceSource`, `isEstimate=true`) em `api/src/Modules/Pricing/Features/Estimate/`

### Implementation for User Story 1 — Requests

- [X] T054 [P] [US1] Entidade `TransportRequest` (itens jsonb, pontos geography, enum de status, snapshot de `pricing_rule_id`) + migração em `api/src/Modules/Requests/Domain/`
- [X] T055 [US1] Resolução/geocodificação de endereços + validação de origem ≠ destino e localizáveis (FR-007) em `api/src/Modules/Requests/Addressing/`
- [X] T056 [US1] Handler de criação de requisição (snapshot da `PricingRule`, persiste, emite `request.confirmed.v1`) + `POST /requests` com `Idempotency-Key` em `api/src/Modules/Requests/Features/CreateRequest/`
- [X] T057 [US1] `GET /requests/{id}` (projeção de status consolidado + `tripStatus`, profissional atribuído **com `verificationStatus` — FR-005a**, telefone oculto antes do vínculo — FR-031) e `GET /requests` (histórico, FR-029) em `api/src/Modules/Requests/Features/`
- [X] T058 [US1] `POST /requests/{id}/cancel` (FR-026) + emissão de `request.cancelled.v1` em `api/src/Modules/Requests/Features/CancelRequest/`
- [X] T059 [US1] Job de timeout de `awaiting_schedule_decision` (`schedule_decision_timeout` → `unfulfilled`, apoia SC-007) em `api/src/Modules/Requests/Jobs/`

### Implementation for User Story 1 — Matching

- [X] T060 [P] [US1] Entidades `MatchingSession` e `Offer` + migrações + índice único parcial de oferta `pending` por profissional em `api/src/Modules/Matching/Domain/`
- [X] T061 [US1] Consulta de elegibilidade (disponível, capacidade ≥ peso, sem transporte ativo, sem oferta pendente, dentro de `immediate_offer_radius`) via PostGIS `ST_DWithin` em `api/src/Modules/Matching/Eligibility/`
- [X] T062 [US1] Ordenação por proximidade (`ST_Distance`) com profissionais de localização acima de `location_ttl` no fim da fila (FR-012a) em `api/src/Modules/Matching/Eligibility/`
- [X] T063 [US1] Orquestrador de ofertas (`IHostedService`): envia uma oferta, cria chave Redis com TTL = `offer_ttl`, expira por keyspace notification + varredura de 5 s, avança/esgota com contadores de tempo e de profissionais (FR-013/014/017a) em `api/src/Modules/Matching/Orchestration/`
- [X] T064 [US1] `POST /offers/{id}/accept` (checa janela → 409 `expired`; lock de atribuição única; cria `Trip`; emite `matching.offer.accepted.v1`) e `POST /offers/{id}/decline` em `api/src/Modules/Matching/Features/`
- [X] T065 [US1] `GET /offers/inbox` (oferta pendente do profissional autenticado) em `api/src/Modules/Matching/Features/Inbox/`
- [X] T066 [US1] Consumers: `request.confirmed.v1` → inicia `MatchingSession`; `matching.exhausted.v1` → `TransportRequest` para `awaiting_schedule_decision` em `api/src/Modules/Matching/Handlers/`

### Implementation for User Story 1 — Trips

- [X] T067 [P] [US1] Entidade `Trip` (enum de status, `agreed_amount`, `delivered_at`, `client_response`, `payment_settled_outside_app`) + migração em `api/src/Modules/Trips/Domain/`
- [X] T068 [US1] Criação de `Trip` ao aceitar oferta (`agreed_amount` = estimativa) + emissão de `trip.created.v1` em `api/src/Modules/Trips/Handlers/`
- [X] T069 [US1] `PATCH /trips/{id}/agreed-amount` (editável enquanto `contratada`/`em_andamento`) em `api/src/Modules/Trips/Features/AgreedAmount/`
- [X] T070 [US1] `POST /trips/{id}/deliver` (profissional → `entregue`, libera o profissional para novas ofertas, emite `trip.delivered.v1`) em `api/src/Modules/Trips/Features/Deliver/`
- [X] T071 [US1] `POST /trips/{id}/client-response` (`confirm`/`dispute` → `confirmada`/`contestada`, registra `AuditEvent` na contestação — FR-025e) em `api/src/Modules/Trips/Features/ClientResponse/`
- [X] T072 [US1] `POST /trips/{id}/cancel` (antes do início; reabre matching imediato — FR-027) e `GET /trips/{id}` + `GET /trips` (histórico do profissional) em `api/src/Modules/Trips/Features/`
- [X] T073 [US1] Job de verificação de entrega: `delivered_at + delivery_verification_hours` sem resposta → notifica cliente e profissional, seta `verification_notified_at` (FR-025d) em `api/src/Modules/Trips/Jobs/`

### Implementation for User Story 1 — Notifications & Mobile

- [X] T074 [US1] Templates + consumers de outbox para `offer_received`, `offer_result`, `request_status`, `trip_delivered`, `trip_verification` em `api/src/Modules/Notifications/Templates/`
- [X] T075 [P] [US1] Mobile: fluxo de nova requisição (itens, peso, seleção de origem/destino no mapa) em `mobile/src/app/client/NewRequest/`
- [X] T076 [P] [US1] Mobile: tela de estimativa de preço (exibe em ≤ 5 s, rótulo "estimativa") em `mobile/src/app/client/Estimate/`
- [X] T077 [US1] Mobile: tela de acompanhamento da requisição (status via react-query + push; exibe nome e selo de verificação do profissional atribuído — FR-005a) em `mobile/src/app/client/Tracking/`
- [X] T078 [US1] Mobile: tela de oferta recebida do profissional com contagem regressiva de 30 s e aceitar/recusar (bottom sheet) em `mobile/src/app/pro/IncomingOffer/`
- [X] T079 [P] [US1] Mobile: tela de transporte para as duas partes (editar valor combinado, marcar entrega, confirmar/contestar, marcar "pago fora do app") em `mobile/src/app/shared/Trip/`
- [ ] T080 [P] [US1] Mobile: listas de histórico de cliente e de profissional em `mobile/src/app/shared/History/`
- [X] T081 [US1] Mobile E2E (Maestro): jornada V3 (requisição imediata → aceite → entrega → confirmação) em `mobile/tests/e2e/immediate-request.yaml`

**Checkpoint**: 🎯 MVP funcional — requisição imediata ponta a ponta. Pronto para demo/deploy.

---

## Phase 6: User Story 2 - Agendar quando não há aceite imediato (Priority: P2)

**Goal**: Sem aceite imediato, o cliente escolhe uma data; o agendamento é notificado a todos os
profissionais disponíveis nessa data e o primeiro a aceitar fica com a vaga (respeitando o
limite de N agendamentos por data).

**Independent Test**: Executar o cenário V5 de [quickstart.md](quickstart.md).

**Status (2026-08-31)**: ✅ **US2 COMPLETA** — T082–T093. Módulo `Scheduling`:
`ProfessionalScheduleAvailability` + `ProfessionalDailyLoad` + `ScheduledOffer`; migração
`Scheduling`. `POST /v1/requests/{id}/schedule-decision` (schedule → `scheduled_searching` +
`request.schedule_requested.v1`; decline → `unfulfilled`, valida janela de datas).
`SchedulingBroadcaster` consome o evento e cria uma `ScheduledOffer` para cada profissional
disponível na data + com capacidade + `daily_load < N`, emitindo `scheduling.offer.sent.v1`.
`GET`/`PUT /professionals/me/schedule-availability`; `GET /v1/schedule-offers/inbox`;
`POST /v1/schedule-offers/{id}/accept` — **corrida "primeiro aceita" via `ExecuteUpdate`
atômico** (`WHERE status IN (searching, scheduled_searching)`), incrementa `ProfessionalDailyLoad`,
marca os demais `filled_by_other`, cria `Trip`. `ScheduledUnfulfilledJob` (data passou sem
aceite → `unfulfilled`). Templates de push: `schedule_offer`, `schedule_filled`,
`schedule_unfulfilled`, `offer_result`. Mobile: prompt de agendamento no `TrackingScreen`
(datas D+2/D+3/D+7) e `ScheduleScreen` (editor de datas + aceite de agendamento).

**45 testes verdes** (3 contrato + 11 unit + 31 integração), incl. `SchedulingFlowTests`:
**dois profissionais aceitam concorrentemente → exatamente um vence (SC-005)**, decline →
unfulfilled, round-trip de datas de disponibilidade. Bug corrigido: rate limiter (120/min) fazia
os testes de integração baterem 429 no run completo → `RateLimiting:PermitPerMinute` configurável.

### Tests for User Story 2

- [X] T082 [P] [US2] Teste de contrato de `POST /requests/{id}/schedule-decision` e `GET`/`PUT /professionals/me/schedule-availability` em `api/tests/contract/Scheduling/SchedulingTests.cs`
- [X] T083 [P] [US2] Teste de integração V5: broadcast aos disponíveis, primeiro aceite vence, demais recebem `filled_by_other` (SC-005) em `api/tests/integration/Scheduling/FirstAcceptWinsTests.cs`
- [X] T084 [P] [US2] Teste de integração: limite `max_schedules_per_date` esconde o profissional (FR-022a); `decline` → `unfulfilled` (FR-023) em `api/tests/integration/Scheduling/DailyLoadAndDeclineTests.cs`
- [ ] T084a [P] [US2] Testes de contrato dos payloads de evento de US2 (`request.schedule_requested.v1`, `scheduling.broadcast.sent.v1`, `scheduling.offer.accepted.v1`, `scheduling.offer.filled_by_other.v1`, `scheduling.unfulfilled.v1`) em `api/tests/contract/Events/Us2EventsContractTests.cs` — Constituição §IV

### Implementation for User Story 2

- [X] T085 [P] [US2] Entidades `ProfessionalScheduleAvailability` e `ProfessionalDailyLoad` + migrações + índice único (`professional_id`,`available_date`) em `api/src/Modules/Scheduling/Domain/`
- [X] T086 [P] [US2] `GET`/`PUT /professionals/me/schedule-availability` em `api/src/Modules/Scheduling/Features/Availability/`
- [X] T087 [US2] `POST /requests/{id}/schedule-decision` (`schedule` → `scheduled_searching` + emite `request.schedule_requested.v1`; `decline` → `unfulfilled`) em `api/src/Modules/Scheduling/Features/ScheduleDecision/`
- [X] T088 [US2] Broadcast de agendamento: seleciona profissionais com disponibilidade na data + capacidade + `accepted_count < N`, cria `Offer` paralelas, emite `scheduling.broadcast.sent.v1` em `api/src/Modules/Scheduling/Broadcast/`
- [X] T089 [US2] Aceite de agendamento: resolução da corrida por `UPDATE` condicional, incremento de `ProfessionalDailyLoad` na mesma transação, demais → `filled_by_other`, cria `Trip` (FR-022) em `api/src/Modules/Scheduling/Features/AcceptSchedule/`
- [X] T090 [US2] Job "sem aceite até a data" → `scheduling.unfulfilled.v1` (FR-024) em `api/src/Modules/Scheduling/Jobs/`
- [X] T091 [US2] Templates + consumers de notificação: `schedule_offer`, `schedule_filled`, `schedule_unfulfilled` em `api/src/Modules/Notifications/Templates/`
- [X] T092 [P] [US2] Mobile: prompt de decisão de agendamento após exaustão + date picker dentro da janela permitida em `mobile/src/app/client/ScheduleDecision/`
- [X] T093 [P] [US2] Mobile: editor de datas de disponibilidade do profissional + aceite de oferta de agendamento em `mobile/src/app/pro/Schedule/`
- [ ] T094 [US2] Mobile E2E (Maestro): jornada V5 (exaustão → agendar → corrida de aceite) em `mobile/tests/e2e/scheduling.yaml`

**Checkpoint**: US1 e US2 funcionam de forma independente.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Conformidade, operação e endurecimento que afetam múltiplas stories.

**Status (2026-08-31)**: ✅ T095–T103. **52 testes verdes** (3 contrato + 11 unit + 38
integração). T095: `PATCH /v1/accounts/me` (retificação) + deleção auto-serviço que anonimiza
a conta e revoga refresh tokens. T096: `LocationRetentionJob`. T098: `CrossUserAccessTests`
(acesso entre usuários → 404/403, todo endpoint de escrita exige token). T099:
`deploy/infra/main.tf` (esboço). T100: job de CI gera + publica o OpenAPI `/v1` como artefato
(`Swashbuckle.AspNetCore.Cli`). T101: `api/README.md` + `mobile/README.md` (runbook de rollout/
rollback). T102: `api/tests/perf/estimate-latency.js` (k6, SC-002 p95 < 5s). T103: banner de
status no `quickstart.md`. **T094/T080 (E2E Maestro) permanecem pendentes** — cobertos pelos
testes de integração; a suíte Maestro fica como follow-up.

- [X] T095 [P] LGPD: workflow de atendimento (correção/exclusão com efeito em cascata, back-office de resolução, SLA) sobre a base criada em T032a/T032b — FR-030 em `api/src/Modules/Accounts/Features/Privacy/`
- [X] T096 [P] Job de retenção de localização (poda de histórico antigo de posição do profissional) em `api/src/Modules/Accounts/Jobs/`
- [X] T097 [P] Observabilidade: SLOs + alertas de burn + esboço de dashboards (RED por endpoint, funil de matching/scheduling, latência do provedor de rota) em `deploy/infra/observability/slo.md`
- [X] T098 [P] Passada de segurança: testes de AuthZ para acesso entre usuários, triagem do scan de dependências, ajuste fino de rate limiting, verificação de ausência de segredos em `api/tests/security/`
- [X] T099 [P] Esboço de IaC em `deploy/infra/` (Terraform/Bicep: rede, Postgres gerenciado, Redis gerenciado, runtime de contêiner, secret manager)
- [X] T100 [P] Fixar prefixo de versão `/v1` + publicar o artefato OpenAPI + checagem contrato-vs-implementação no CI em `.github/workflows/ci.yml`
- [X] T101 [P] Docs: runbook de operação em `api/README.md` (rollout progressivo/canary, rollback por SLO) e `mobile/README.md`
- [X] T102 [P] Performance: script de carga (NBomber) validando SC-002 (estimativa ≤ 5 s p95) e precisão do timer de oferta ≤ 1 s sob carga em `api/tests/perf/`
- [X] T103 Executar [quickstart.md](quickstart.md) V1–V8 de ponta a ponta e corrigir lacunas

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sem dependências.
- **Foundational (Phase 2)**: depende do Setup — BLOQUEIA todas as user stories.
- **US4 (Phase 3)** e **US3 (Phase 4)**: dependem da Fase 2. Independentes entre si — podem ser
  feitas em paralelo.
- **US1 (Phase 5)**: depende da Fase 2 e, para o teste E2E completo, de US3 + US4 (precisa de
  contas de cliente e de profissional). A implementação da API de US1 pode começar em paralelo
  com US3/US4; a integração de matching precisa de `ProfessionalProfile` (T036).
- **US2 (Phase 6)**: depende da Fase 2 e reutiliza `Offer`/`Trip` de US1 (T060, T067). Começa
  após US1 ou em paralelo com um contrato estável dessas entidades.
- **Polish (Phase 7)**: depois das user stories desejadas.

### Within Each User Story

- Testes escritos primeiro e devem FALHAR antes da implementação (Princípio IV).
- Entidades/migrações → serviços/handlers → endpoints → integração de eventos → mobile.

### Parallel Opportunities

- Setup: T002–T009 em paralelo após T001.
- Foundational: T011–T017, T019–T025 em paralelo (T010 e T018 primeiro, pois criam schema/identidade).
- US4 e US3 podem ser desenvolvidas por pessoas diferentes ao mesmo tempo.
- Dentro de US1: os blocos Pricing (T051–T053), Requests (T054–T059), Matching (T060–T066) e
  Trips (T067–T073) têm entidades `[P]`; os handlers convergem nos consumers de evento.
- Todos os testes `[P]` de uma story rodam juntos.
- Telas mobile marcadas `[P]` (T075–T076, T079–T080) em paralelo.

---

## Parallel Example: User Story 1 (tests first)

```bash
# Testes de contrato/integração de US1 juntos:
Task: "T043 Teste de contrato de POST /pricing/estimate"
Task: "T044 Teste de contrato de /requests"
Task: "T045 Teste de contrato de /offers"
Task: "T046 Teste de contrato de /trips"
Task: "T047 Teste de integração V3 (happy path)"
Task: "T048 Teste de integração V4 (timeout e limite)"
Task: "T049 Teste de integração de aceite concorrente"

# Entidades de US1 em paralelo:
Task: "T051 PricingRule"
Task: "T054 TransportRequest"
Task: "T060 MatchingSession + Offer"
Task: "T067 Trip"
```

---

## Implementation Strategy

### MVP First (Phases 1–5)

1. Fase 1 (Setup) → Fase 2 (Foundational) → Fase 3 (US4) + Fase 4 (US3) → Fase 5 (US1).
2. **PARAR e VALIDAR**: cenários V2/V3/V4/V6 do quickstart.
3. Deploy/demo do MVP (requisição imediata funcionando).

### Incremental Delivery

1. Setup + Foundational → fundação pronta.
2. + US4 + US3 → contas funcionando.
3. + US1 → 🎯 MVP, demo.
4. + US2 → agendamento, demo.
5. + Polish → conformidade LGPD, observabilidade, IaC, endurecimento.

---

## Notes

- `[P]` = arquivos diferentes, sem dependência pendente.
- Verificar que os testes falham antes de implementar (Princípio IV — NON-NEGOTIABLE).
- Commit após cada tarefa ou grupo lógico; não fazer merge com gate de CI vermelho.
- Parâmetros de negócio (30 s, 5 min/8 prof., 24 h, N/data, raio, TTL de localização, preço)
  vêm da tabela `configuration` (T015) — nenhum valor hard-coded.
- Cada user story deve permanecer testável de forma independente; evitar dependências entre
  stories que quebrem essa independência.
