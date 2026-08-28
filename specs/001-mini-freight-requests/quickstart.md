# Quickstart & Validation Guide: Mini-Freight Requests (MVP)

Guia para subir o ambiente e validar as jornadas P1/P2 ponta a ponta. Detalhes de modelo e
contrato estão em [data-model.md](data-model.md) e [contracts/openapi.yaml](contracts/openapi.yaml).

## Pré-requisitos

- **Docker** (Desktop ou Engine + Compose v2) — obrigatório: o backend é *Docker-first*
  (Constituição §VIII). .NET SDK 9 só é necessário para editar/depurar a API fora do contêiner.
- Node 20+, Expo CLI (`npm i -g expo`) e um emulador Android e/ou simulador iOS — apenas para o
  app móvel (não containerizado).
- Chave do provedor de rota (Google Distance Matrix ou Mapbox) em `deploy/.env` como
  `ROUTE_PROVIDER_API_KEY`; sem ela a API usa o fallback geodésico.

## Subir o backend (Docker-first — um comando)

```bash
cd deploy
cp .env.example .env          # ajuste ROUTE_PROVIDER_API_KEY e segredos se quiser
docker compose up             # api + postgres+postgis + redis + otel-collector (tags fixas)
```

- A API fica em `http://localhost:8080` (Swagger em `/swagger`); espere `/ready` responder `200`.
- As migrações do banco são aplicadas automaticamente na subida do contêiner da API.
- A mesma imagem (`deploy/Dockerfile.api`) roda em CI, staging e produção.

Semear dados de exemplo (regra de preço + cidade de atuação):

```bash
docker compose run --rm api seed --demo
```

Desenvolvimento com hot-reload (bind-mount da fonte via override):

```bash
docker compose -f docker-compose.yml -f docker-compose.override.yml up api
```

## Subir o app

```bash
cd mobile
npm install
npm run codegen          # gera o cliente HTTP a partir de contracts/openapi.yaml
npx expo start           # abrir no emulador Android / simulador iOS
```

Configurar `mobile/.env`: `EXPO_PUBLIC_API_BASE_URL=http://10.0.2.2:8080/v1` (Android emu) ou
`http://localhost:8080/v1` (iOS sim) — apontando para a API que sobe via Compose.

## Rodar os testes

```bash
# API
cd api
dotnet test                     # unit + contract + integration (Testcontainers sobe Postgres/Redis)

# Mobile
cd ../mobile
npm test                        # Jest + RNTL
npm run e2e                     # Maestro (precisa de emulador aberto e API no ar)
```

Gates que o CI aplica (bloqueiam merge): `dotnet format --verify-no-changes`, `dotnet test`
com cobertura não decrescente, `npm run lint`, `npm test`, scan de dependências e de segredos,
validação do OpenAPI, `dotnet ef migrations` sem drift.

---

## Cenários de validação

### V1 — Cadastro (US3/US4, FR-001..FR-004)

1. No app, criar conta de **cliente** (nome, telefone, e-mail, senha).
2. Criar conta de **profissional** informando carga máxima 200 kg.
   **Esperado**: login automático; `GET /v1/accounts/me` retorna os papéis corretos e
   `verificationStatus = nao_verificado`.

### V2 — Estimativa de preço (US1, FR-008/009/010)

1. Como cliente, informar origem, destino e peso 50 kg; tocar "Ver estimativa".
   **Esperado**: valor exibido em ≤ 5 s (SC-002), com rótulo "estimativa";
   `distanceSource = routed` (ou `geodesic_fallback` sem chave de API).
2. Alterar a regra de preço via seed/config e repetir.
   **Esperado**: o valor muda sem reiniciar a API (FR-009).

### V3 — Requisição imediata com aceite (US1, FR-011..FR-017)

1. Profissional A (250 kg, disponível, localização a 2 km da origem) e Profissional B (250 kg,
   disponível, a 8 km).
2. Cliente confirma a requisição (peso 50 kg).
   **Esperado**: `TransportRequest.status = searching`; **A** recebe push `offer_received` com
   `respondBy` ≈ agora + 30 s; **B** não recebe ainda.
3. **A** aceita via `POST /v1/offers/{id}/accept` dentro de 30 s.
   **Esperado**: `Trip` criada (`status = contratada`); requisição → `hired`; cliente recebe
   push com nome de **A**; **A** fica `immediateAvailability` inelegível para novas ofertas
   (FR-011a) — validar tentando criar outra requisição: **A** não é ofertado.

### V4 — Passagem ao próximo e limite da busca (FR-014/015/017a)

1. Repetir V3 mas **A** não responde.
   **Esperado**: após 30 s, `matching.offer.expired`; **B** recebe a oferta.
2. Nenhum aceita; encadear até 8 profissionais ou 5 min (usar config reduzida no teste, ex.
   `max_professionals_contacted=2`).
   **Esperado**: `matching.exhausted`; requisição → `awaiting_schedule_decision`; cliente
   recebe a pergunta de agendamento.
3. Enviar aceite de **A** após o `respondBy`.
   **Esperado**: `409` (janela expirada) — nenhum vínculo criado (SC-004).

### V5 — Agendamento e corrida "primeiro aceita" (US2, FR-018..FR-024, SC-005)

1. Do estado `awaiting_schedule_decision`, cliente escolhe uma data D+3 via
   `POST /v1/requests/{id}/schedule-decision {decision: schedule, scheduledDate}`.
2. Profissionais C e D declararam disponibilidade em D+3 e têm carga suficiente.
   **Esperado**: ambos recebem push `schedule_offer` simultâneo; requisição →
   `scheduled_searching`.
3. C e D chamam `accept` quase ao mesmo tempo (script paralelo).
   **Esperado**: exatamente um recebe `Trip` (`200`), o outro recebe `409 filled_by_other` e um
   push de "vaga preenchida"; requisição → `scheduled`.
4. Com `max_schedules_per_date=1`, criar outra requisição agendada para D+3.
   **Esperado**: o profissional já alocado nesse dia **não** é notificado (FR-022a).
5. Cliente responde `decline` a uma oferta de agendamento.
   **Esperado**: requisição → `unfulfilled` (FR-023).

### V6 — Conclusão, contestação e verificação 24 h (FR-025b..e, SC-009)

1. A partir de uma `Trip` `contratada`: profissional chama `POST /v1/trips/{id}/deliver`.
   **Esperado**: `Trip.status = entregue`, `deliveredAt` setado; cliente recebe push
   `trip_delivered`; profissional volta a poder ficar disponível.
2. Cliente chama `client-response {response: confirm}`.
   **Esperado**: `Trip.status = confirmada`.
3. Repetir com `dispute`.
   **Esperado**: `Trip.status = contestada`; `audit_event` `trip.disputed` registrado.
4. Sem resposta do cliente: rodar o job com janela reduzida (`delivery_verification_hours` em
   segundos no teste).
   **Esperado**: push `trip_verification` para cliente **e** profissional; `Trip` permanece
   `entregue` com `verification_notified_at`.

### V7 — Privacidade (FR-030/031)

1. `POST /v1/privacy/data-subject-requests {kind: access}` autenticado.
   **Esperado**: `202`; registro criado e `audit_event` `datasubject.request_created`.
2. Antes de um vínculo, `GET /v1/requests/{id}` como cliente **não** expõe telefone de
   profissional; após o aceite, `Trip.counterparty.phone` fica visível para as duas partes.

### V8 — Observabilidade (Constituição §V)

1. Fazer uma requisição com header `x-correlation-id: test-123`.
   **Esperado**: no otel-collector, um trace único cobrindo API → Matching worker → Notifications
   com o mesmo `correlationId`; logs JSON do Serilog carregam `TraceId`.
2. `GET /health` e `GET /ready` retornam `200` com dependências (Postgres, Redis) checadas.

## Critérios de aceite do plano (rastreabilidade)

| Cenário | Requisitos | Success Criteria |
|---------|------------|------------------|
| V1 | FR-001..005a | SC-006 |
| V2 | FR-006..010 | SC-001, SC-002 |
| V3 | FR-011..017, FR-011a/b | SC-003 |
| V4 | FR-012a, FR-014/015, FR-017a | SC-004 |
| V5 | FR-018..024, FR-022a | SC-005, SC-007 |
| V6 | FR-025b..e | SC-008, SC-009 |
| V7 | FR-030/031 | — |
| V8 | FR-032 + Constituição §V/VI | — |
