# Implementation Plan: Mini-Freight Requests (MVP)

**Branch**: `001-mini-freight-requests` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-mini-freight-requests/spec.md`

## Summary

MVP de um marketplace móvel de mini-fretes que conecta clientes e profissionais autônomos.
O cliente cria uma requisição (itens, peso estimado, origem, destino), recebe uma estimativa de
preço por distância rodoviária × peso e o sistema oferta a requisição a um profissional
elegível por vez (janela de 30 s), ordenando por proximidade via geolocalização. Sem aceite em
até 5 min ou 8 profissionais, o cliente pode agendar para uma data; o agendamento é notificado
a todos os profissionais disponíveis nessa data e o primeiro a aceitar fica com a vaga.

**Abordagem técnica**: API em **.NET 9 / ASP.NET Core** organizada como *modular monolith* com
fronteiras de módulo explícitas (Accounts, Requests, Pricing, Matching, Scheduling,
Notifications, Trips) e contratos OpenAPI/eventos versionados, pronta para extração em serviços.
App móvel em **React Native + TypeScript (Expo dev client)** para Android e iOS, com
geolocalização (`expo-location`), mapas (`react-native-maps`), dados assíncronos
(`@tanstack/react-query`) e estilo utilitário (`nativewind`). Estado efêmero de ofertas e os
temporizadores de 30 s ficam em **Redis**; persistência em **PostgreSQL + PostGIS**;
notificações push via **FCM/APNs** (Expo Push como fachada no MVP). Observabilidade com
**OpenTelemetry + Serilog**. Tudo containerizado, *stateless* e com *outbox pattern* para
permitir escala horizontal e migração para mensageria após a PoC.

## Technical Context

**Language/Version**: C# / .NET 9 (API); TypeScript 5.x / React Native 0.76 via Expo SDK 52 (mobile)

**Primary Dependencies**:
- API: ASP.NET Core Web API, EF Core 9 (Npgsql + NetTopologySuite), MediatR (mediação entre
  módulos), FluentValidation, ASP.NET Core Identity + JWT, `Microsoft.AspNetCore.RateLimiting`,
  Polly, Hangfire *ou* Hosted Services + Redis para orquestração de ofertas, StackExchange.Redis,
  Swashbuckle/NSwag (OpenAPI), Serilog, OpenTelemetry SDK.
- Mobile: Expo (dev client), React Navigation, `@tanstack/react-query`, `expo-location`,
  `react-native-maps`, `nativewind` + Tailwind, `zustand` (estado local), `react-hook-form` +
  `zod`, `expo-notifications`, `expo-secure-store` (tokens).

**Storage**: PostgreSQL 16 + PostGIS (dados relacionais + geoconsultas); Redis 7 (estado de
oferta ativa, locks de atribuição, TTL de 30 s, filtros de proximidade em cache).

**Testing**: API — xUnit + FluentAssertions, Testcontainers (Postgres/Redis) para integração e
contrato, WireMock.Net para o provedor de mapas; contratos verificados contra o OpenAPI.
Mobile — Jest + React Native Testing Library (unidade/componente), Maestro (E2E de jornadas).

**Target Platform**: API distribuída **exclusivamente como imagem OCI** (contêiner Linux) — a
mesma imagem roda em dev (Docker Compose), CI, staging e produção (Kubernetes ou container host
gerenciado). App em Android 8+ e iOS 15+.

**Infra local (Docker-first, Constituição §VIII)**: `deploy/docker-compose.yml` sobe todo o
backend com um único `docker compose up` — API (build a partir de `deploy/Dockerfile.api`),
PostgreSQL 16 + PostGIS, Redis 7 e o OpenTelemetry Collector, todos com tags de imagem fixas.
Nenhum passo manual; apenas variáveis de ambiente documentadas. `.devcontainer/` referencia o
mesmo Compose para desenvolvimento dentro do contêiner.

**Project Type**: Mobile + API (app cross-platform consumindo uma API REST).

**Performance Goals**:
- Estimativa de preço exibida em ≤ 5 s p95 (SC-002) — inclui chamada ao provedor de rota.
- API p95 ≤ 300 ms para endpoints de leitura/escrita simples (exclui chamadas externas).
- Atribuição de profissional dentro do limite de busca (5 min / 8 profissionais) em ≥ 80 % dos
  casos com oferta disponível (SC-003).
- Precisão do temporizador de oferta: janela de 30 s respeitada em 100 % dos casos (SC-004),
  tolerância de expiração ≤ 1 s.

**Constraints**:
- Timers e transições de oferta devem sobreviver a reinício/reescala de instância (estado em
  Redis, não em memória de processo).
- Atribuição de uma requisição/agendamento a exatamente um profissional sob concorrência
  (lock otimista + unicidade no banco).
- Operações expostas a cliente e consumidas de fila devem ser idempotentes (chave de
  idempotência).
- Dados de localização retidos por tempo mínimo (última posição + curto histórico operacional).
- MVP monorregião: raio/limite de atuação configurável.

**Scale/Scope**:
- MVP: 1 cidade, ~500 profissionais ativos, ~2.000 requisições/dia, picos ~20 requisições
  simultâneas em busca.
- Escopo: ~14 telas no app; ~7 módulos na API; ~6 entidades principais.
- Preparado para escala: serviços stateless replicáveis, estado externalizado, outbox +
  contrato de eventos para futura mensageria, módulos extraíveis.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Princípio | Como o plano atende | Status |
|---|-----------|---------------------|--------|
| I | Service-Oriented & API-First | Contrato OpenAPI-first versionado (`/v1`); módulos com fronteira explícita comunicando por interfaces/eventos internos; sem acesso cruzado a tabelas de outro módulo; eventos via outbox no formato de barramento. MVP como *modular monolith* **coberto pelo carve-out de MVP/PoC da Constituição §I**; extração para serviços registrada no Complexity Tracking. | ✅ (carve-out §I) |
| II | Security by Default | AuthN JWT (access curto + refresh) em toda rota; AuthZ por papel/propriedade do recurso; TLS obrigatório; segredos via variáveis/secret manager (nunca em repo/log); FluentValidation em toda entrada; rate limiting por IP e por usuário; contato cliente↔profissional só após vínculo (FR-031). | ✅ |
| III | Privacy & Compliance | Minimização (só nome/telefone/e-mail + localização enquanto disponível); endpoints de acesso/correção/exclusão (FR-030); retenção curta de localização; acesso a dados pessoais logado. | ✅ |
| IV | Test-First & Quality Gates | Testes de contrato por endpoint/evento, integração com Testcontainers, E2E das jornadas P1/P2 (Maestro); gates de lint/type/test/scan no CI bloqueando merge; cobertura não regride. | ✅ |
| V | Observability & Operability | OpenTelemetry (traces/metrics/logs) com correlation id propagado do app; Serilog JSON; métricas RED por endpoint e do orquestrador de ofertas; `/health` e `/ready`; SLOs de disponibilidade/latência e alerta de burn. | ✅ |
| VI | Reliability & Resilience | Timeout/retry/backoff + circuit breaker (Polly) nas chamadas ao provedor de mapas e push; idempotência por chave; outbox transacional; timers/estado de oferta em Redis; degradação: sem rota externa cai para distância geodésica com aviso. | ✅ |
| VII | Simplicity, Versioning & Explicit Change | Menor desenho que atende o MVP (monolito modular); SemVer nas APIs e schemas de evento; IaC (Docker Compose dev + módulo Terraform/Bicep esboçado); parâmetros de negócio (preço, 30 s, 5 min/8 prof., 24 h, N/data, raio, validade de localização) configuráveis sem deploy de código. | ✅ |
| VIII | Docker-First Infrastructure | `deploy/Dockerfile.api` multi-stage, usuário não-root, base fixada (`mcr.microsoft.com/dotnet/aspnet:9.0`); `deploy/docker-compose.yml` sobe API + Postgres/PostGIS + Redis + otel-collector com um comando, tags fixas, sem passo manual; a **mesma imagem** em dev/CI/staging/prod, diferenças só por env/secrets em runtime; `.devcontainer/` sobre o mesmo Compose; CI faz build + scan (Trivy) da imagem e publica no merge. Mobile/Expo fora do escopo do princípio (build nativo). | ✅ |

**Resultado do gate**: PASS. O uso de *modular monolith* está autorizado pelo carve-out de
MVP/PoC da Constituição §I; a extração futura para serviços independentes permanece
rastreada no Complexity Tracking. Nenhuma violação.

**Re-check pós-design (Fase 1)**: Após `data-model.md`, `contracts/` e `quickstart.md`, o
desenho continua aderente: contratos OpenAPI `/v1` + envelope de evento versionado
([contracts/events.md](contracts/events.md)), fronteiras de módulo preservadas (nenhum acesso
cruzado a tabelas), outbox transacional e idempotência modelados, endpoints de LGPD e trilha de
auditoria presentes, parâmetros de negócio externalizados em `configuration`. Docker-first
(§VIII, Constituição v1.2.0): imagem OCI única como unidade de build/deploy, Compose de um
comando para o stack local, `.devcontainer/` sobre o mesmo Compose, build+scan da imagem no CI.
Nenhum desvio: o modular monolith está coberto pelo carve-out da Constituição §I. O Complexity
Tracking mantém o registro da extração futura para serviços.

## Project Structure

### Documentation (this feature)

```text
specs/001-mini-freight-requests/
├── plan.md              # Este arquivo
├── research.md          # Fase 0 — decisões técnicas
├── data-model.md        # Fase 1 — entidades, campos, transições
├── quickstart.md        # Fase 1 — guia de validação ponta a ponta
├── contracts/           # Fase 1 — OpenAPI + eventos internos
│   ├── openapi.yaml
│   └── events.md
├── checklists/
│   └── requirements.md  # Checklist de qualidade da spec
└── tasks.md             # Fase 2 (/speckit-tasks — NÃO criado aqui)
```

### Source Code (repository root)

```text
api/                                  # Solução .NET 9 (modular monolith)
├── src/
│   ├── MyFrete.Api/                  # Host ASP.NET Core: composição, middleware, auth, OpenAPI
│   ├── MyFrete.BuildingBlocks/       # Contratos base: Result, erros, idempotência, outbox, telemetria
│   ├── Modules/
│   │   ├── Accounts/                 # Cliente/Profissional, cadastro, auth, verificação (status), LGPD
│   │   ├── Pricing/                  # Regra de precificação configurável, cálculo de estimativa
│   │   ├── Requests/                 # Requisição de transporte, ciclo de vida, itens, endereços
│   │   ├── Matching/                 # Elegibilidade, ordenação por proximidade, orquestração de ofertas 30s
│   │   ├── Scheduling/               # Disponibilidade por data, oferta de agendamento, corrida "primeiro aceita"
│   │   ├── Trips/                    # Conclusão (marcar entrega), confirmação/contestação, verificação 24h
│   │   └── Notifications/            # Fachada de push (Expo/FCM/APNs), templates, entrega
│   └── MyFrete.Migrations/           # Migrações EF Core
└── tests/
    ├── contract/                     # Um projeto por módulo — valida requests/responses vs OpenAPI
    ├── integration/                  # Fluxos entre módulos com Testcontainers (Postgres+Redis)
    └── unit/                         # Regras puras (preço, elegibilidade, ordenação, máquina de estados)

mobile/                               # App Expo (React Native + TypeScript)
├── src/
│   ├── app/                          # Navegação (React Navigation) e telas
│   │   ├── auth/                     # Onboarding, cadastro cliente/profissional, login
│   │   ├── client/                   # Nova requisição, estimativa, acompanhamento, histórico, conclusão
│   │   └── pro/                      # Disponibilidade, oferta recebida (30s), agenda, meus transportes
│   ├── features/                     # Hooks/estado por domínio (react-query + zustand)
│   ├── services/                     # Cliente HTTP gerado do OpenAPI, push, localização, mapas
│   ├── components/                   # UI compartilhada (nativewind)
│   └── lib/                          # Config, i18n (pt-BR), formatação, validação zod
└── tests/
    ├── unit/                         # Jest + RNTL
    └── e2e/                          # Maestro (jornadas P1/P2)

deploy/
├── docker-compose.yml               # `docker compose up` → api + postgres+postgis + redis + otel-collector (tags fixas)
├── docker-compose.override.yml      # Opcional: hot-reload / bind-mount de fonte para dev
├── Dockerfile.api                   # Multi-stage, non-root, base fixada — unidade de build/deploy
├── otel-collector-config.yaml
└── infra/                           # Esboço Terraform/Bicep (rede, DB gerenciado, cache, runtime de contêiner)

.devcontainer/
└── devcontainer.json                # Dev dentro do contêiner, reutilizando deploy/docker-compose.yml

.github/workflows/                   # CI: build+test+scan (api e mobile), lint, migrações, e2e
```

**Structure Decision**: **Mobile + API**. A API é uma solução .NET única com um projeto host
(`MyFrete.Api`) e um projeto por módulo em `api/src/Modules/*`. Cada módulo expõe apenas
interfaces públicas em `BuildingBlocks`/contratos; comunicação entre módulos é por chamada de
interface in-process + eventos de domínio persistidos em outbox (mesma forma que serão
publicados numa fila no futuro). O app móvel é um projeto Expo único cobrindo Android e iOS,
com o cliente HTTP gerado a partir de `contracts/openapi.yaml`.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Princípio I — MVP entregue como *modular monolith* (autorizado pelo carve-out §I); extração para serviços independentemente implantáveis pendente | Equipe pequena e PoC: um único artefato reduz custo de operação, observabilidade e deploy enquanto o domínio ainda está se estabilizando. As fronteiras de módulo, os contratos OpenAPI/evento versionados e o outbox preservam o caminho de extração, **obrigatória antes de escalar além da PoC**. | Serviços separados desde o MVP multiplicariam pipelines, bancos, malha de rede e sobrecarga de tracing sem ganho de escala real nos volumes previstos (~2k req/dia). O risco de acoplamento é mitigado por fronteiras explícitas e proibição de acesso cruzado a dados. |
| Redis como dependência adicional (além do Postgres) | Os temporizadores de oferta (30 s), locks de atribuição sob concorrência e o TTL de estado de busca precisam sobreviver a reescala e não podem viver na memória do processo; fazer isso só no Postgres exigiria polling agressivo e locks de linha de longa duração. | Polling no Postgres a cada segundo para ~20 ofertas simultâneas gera carga e latência desnecessárias e complica a precisão de 1 s exigida pelo SC-004. |
