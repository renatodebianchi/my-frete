# Phase 0 Research: Mini-Freight Requests (MVP)

Todas as incógnitas do Technical Context e as pendências marcadas na spec/clarify foram
resolvidas abaixo. Formato: Decisão / Justificativa / Alternativas consideradas.

## 1. Provedor de distância e rota (estimativa de preço)

- **Decisão**: Usar uma API de matriz de distância rodoviária externa por trás de uma
  interface `IRouteDistanceProvider`. MVP: **Google Distance Matrix API** (ou Mapbox Matrix como
  equivalente). Fallback determinístico: distância geodésica (Haversine) × fator de sinuosidade
  configurável (padrão 1,3), sinalizando ao cliente que a estimativa é aproximada.
- **Justificativa**: FR-008 exige distância "por vias"; um provedor gerenciado entrega isso sem
  operar roteamento próprio. A interface isola o fornecedor (troca sem mudança de domínio) e o
  fallback cobre indisponibilidade (Princípio VI — degradação graciosa).
- **Alternativas**: OSRM/Valhalla self-hosted (mais infra e dados OSM para manter — adiado para
  pós-PoC quando o volume justificar); apenas geodésica (imprecisa em áreas urbanas, rejeitada
  como padrão).

## 2. Entrega de notificações push

- **Decisão**: `INotificationSender` com implementação **Expo Push Service** no MVP, que
  encapsula FCM (Android) e APNs (iOS). Tokens de dispositivo por usuário armazenados no módulo
  Notifications. Migração futura para FCM/APNs diretos sem mudar o contrato.
- **Justificativa**: O app é Expo; o Expo Push remove a necessidade de configurar credenciais
  APNs/FCM no MVP e dá recibo de entrega. Mensagens são idempotentes por chave (evento + destinatário).
- **Alternativas**: FCM/APNs diretos (mais setup, adiado); SMS/e-mail como canal primário
  (custo e latência piores para ofertas de 30 s — reservado como canal secundário futuro).

## 3. Orquestração da oferta imediata (1 por vez, janela de 30 s, teto 5 min / 8 profissionais)

- **Decisão**: Uma `MatchingSession` por requisição, persistida no Postgres (estado durável) com
  um **registro de oferta ativa em Redis** contendo `requestId`, `professionalId`,
  `expiresAt` e um lock. Um worker (Hosted Service) processa expirações via Redis keyspace
  notifications + varredura de segurança a cada 5 s. Transições: `Searching → OfferPending →
  (Accepted | NextProfessional | Exhausted | TimedOut)`. Contadores de tempo total e de
  profissionais contatados na sessão encerram a busca (FR-017a).
- **Justificativa**: Estado durável no Postgres garante recuperação após crash; Redis dá
  precisão de expiração (~1 s, SC-004) e lock barato de atribuição única. Worker stateless e
  idempotente permite múltiplas réplicas (Princípio VI).
- **Alternativas**: Timer em memória (perde estado na reescala — rejeitado); só polling Postgres
  (carga/latência — ver Complexity Tracking); Durable Task Framework / Temporal (poder de
  orquestração além do necessário para o MVP — reavaliar pós-PoC).

## 4. Corrida "primeiro a aceitar" no agendamento

- **Decisão**: Aceite do agendamento resolvido por **UPDATE condicional** (`WHERE status =
  'Open'`) em transação; vencedor é a primeira linha afetada. Índice único parcial garante um
  profissional por agendamento. Limite de N agendamentos por data (FR-022a) verificado na mesma
  transação contra um agregado `ProfessionalDailyLoad`.
- **Justificativa**: Sem necessidade de lock distribuído; o banco já serializa. Determinístico e
  testável (SC-005).
- **Alternativas**: Lock distribuído em Redis (desnecessário para operação de baixa frequência);
  fila com consumidor único (adiciona latência à corrida).

## 5. Ordenação por proximidade e frescor de localização

- **Decisão**: Localização do profissional (`Point` PostGIS) atualizada pelo app enquanto
  "disponível", via `PATCH /v1/professionals/me/location` (throttle ~60 s no cliente).
  Elegibilidade + ordenação: `ST_DWithin` para o raio configurável e `ST_Distance` para ordenar;
  profissionais com localização > 5 min (config) vão para o fim (FR-012a). Sem transporte ativo
  e sem oferta pendente entram no conjunto (FR-011/011b).
- **Justificativa**: PostGIS resolve geoconsulta e ordenação em uma query indexada (GiST). O
  throttle equilibra precisão × bateria/dados.
- **Alternativas**: Geohash + cálculo em app (reimplementa o que o PostGIS já faz);
  streaming contínuo de GPS (custo de bateria injustificado no MVP).

## 6. Autenticação e identidade

- **Decisão**: ASP.NET Core Identity + JWT (access ~15 min, refresh ~30 dias com rotação),
  `expo-secure-store` no app. Um usuário pode ter papel `client`, `professional` ou ambos.
  Estrutura preparada para trocar por um provedor OIDC externo (o app já usa fluxo de token).
- **Justificativa**: Padrão de mercado para app móvel (Assumptions da spec); Identity cobre
  hashing, lockout e reset. Refresh com rotação limita janela de comprometimento (Princípio II).
- **Alternativas**: OIDC gerenciado (Auth0/Entra) desde já (custo e dependência externa no MVP —
  adiado, caminho preservado); sessão por cookie (inadequado para app nativo).

## 7. Verificação de profissional (preparar sem implementar)

- **Decisão**: Campo `VerificationStatus` (`NaoVerificado | EmAnalise | Verificado | Rejeitado`)
  + tabela `VerificationEvent` (append-only) no módulo Accounts. MVP ativa como `NaoVerificado`.
  Um `IVerificationProvider` no-op deixa o ponto de extensão pronto para verificação
  manual (painel de operador) ou automática (CPF/CNH) depois.
- **Justificativa**: Atende FR-005/005a sem trabalho de verificação real; sem mudança de modelo
  ou de fluxo de matching no futuro (Princípio VII).
- **Alternativas**: Sem campo algum (violaria FR-005 e forçaria migração depois);
  integração de verificação já no MVP (fora do escopo acordado no clarify).

## 8. Pagamento (fora do app no MVP)

- **Decisão**: `Trip.AgreedAmount` (padrão = estimativa, editável antes da conclusão) +
  `PaymentSettledOutsideApp` (bool) + `SettledAt`. Nenhuma integração de gateway. Interface
  `IPaymentGateway` **não** é criada no MVP para evitar abstração especulativa; será introduzida
  quando o pagamento in-app entrar no escopo.
- **Justificativa**: FR-025/025a pedem registro de valor e marcação, não movimentação. YAGNI
  (Princípio VII).
- **Alternativas**: Abstração de gateway desde já (especulativa); registrar só a estimativa sem
  valor combinado editável (não atende FR-025).

## 9. Conclusão do transporte e verificação em 24 h

- **Decisão**: `Trip` com estados `Contratada → EmAndamento → Entregue(por prof.) →
  (Confirmada | Contestada)`; marcação de entrega libera o profissional (FR-011a/025b). Um job
  agendado (Hosted Service varrendo `DeliveredAt + 24h` sem resposta do cliente) emite a
  notificação de verificação a ambos (FR-025d). Janela de 24 h configurável.
- **Justificativa**: Decisão do clarify (2026-08-28). Job idempotente e stateless.
- **Alternativas**: Dupla confirmação obrigatória (bloquearia liberação do profissional —
  rejeitado no clarify); auto-confirmar em silêncio (perde o sinal de verificação pedido).

## 10. Estilo/UX do app

- **Decisão**: `nativewind` (Tailwind para RN) + tokens de design; componentes acessíveis com
  `react-native` core + `@gorhom/bottom-sheet` para os fluxos de oferta/estimativa;
  `react-native-maps` para seleção de origem/destino e visualização; `react-query` para
  estado servidor com cache e revalidação; feedback otimista nas ações de aceitar/recusar.
- **Justificativa**: "melhores bibliotecas JS/CSS" do pedido → Tailwind é o padrão utilitário
  mais difundido; react-query reduz boilerplate e dá a sensação de rapidez (cache + background
  refetch). Tudo compatível com Expo managed + dev client.
- **Alternativas**: Styled-components (mais runtime, menos consistência de tokens); Redux
  Toolkit Query (equivalente, mas time já mais familiar com react-query); UI kits pesados
  (Tamagui/Gluestack) — reavaliar se o design system crescer.

## 11. Observabilidade

- **Decisão**: OpenTelemetry SDK (.NET) exportando OTLP para um `otel-collector` (dev em
  compose; cloud → backend gerenciado). Serilog como sink de logs estruturados JSON com
  `TraceId`/`SpanId`. App envia um header `x-correlation-id` propagado em todos os spans.
  Métricas: RED por endpoint, duração da sessão de matching, taxa de aceite, expirações,
  latência do provedor de rota. SLOs iniciais: disponibilidade 99,5 %; p95 de estimativa ≤ 5 s.
- **Justificativa**: Princípio V; OTel é neutro de fornecedor e evita lock-in.
- **Alternativas**: APM proprietário direto (lock-in); só logs (sem tracing entre módulos/worker).

## 12. IaC e ambientes

- **Decisão**: `deploy/docker-compose.yml` para dev (api, postgis, redis, otel-collector).
  `deploy/infra/` com esboço Terraform (rede, Postgres gerenciado, Redis gerenciado, runtime de
  contêiner, secret manager). Deploy progressivo (canary) e rollback automático por SLO ficam
  descritos no README de operação; automação completa é pós-PoC.
- **Justificativa**: Princípio VII (IaC) sem sobre-engenharia de plataforma no MVP.
- **Alternativas**: Kubernetes manifests completos + GitOps desde já (sobrecarga para PoC);
  deploy manual sem IaC (viola Princípio VII).

## 13. Docker-first para a infraestrutura (Constituição §VIII)

- **Decisão**: A imagem OCI é a única unidade de build/run/deploy do backend.
  `deploy/Dockerfile.api` (multi-stage: `sdk:9.0` → `aspnet:9.0`, usuário não-root, base com
  tag fixa) é a forma autoritativa de construir e rodar a API. `deploy/docker-compose.yml` sobe
  o stack completo (`api`, `postgres` = `postgis/postgis:16-3.4`, `redis` = `redis:7-alpine`,
  `otel-collector` = tag fixa) com `docker compose up`, healthchecks e só variáveis de ambiente
  documentadas. `.devcontainer/devcontainer.json` usa o mesmo Compose. A mesma imagem roda em
  dev, CI, staging e produção; diferenças só por config/secret injetados em runtime.
- **Justificativa**: Elimina drift de ambiente como classe de incidente, torna rollback trivial
  (redeploy de imagem anterior) e mantém barato o caminho de extração do monolito modular
  (§I) — cada módulo já roda do mesmo jeito que rodará como serviço isolado.
- **Alternativas**: setup local nativo (SDK + Postgres/Redis instalados na máquina) — rejeitado
  por drift e passos manuais; imagens sem pin (`latest`) — rejeitado por builds não
  reproduzíveis; Nix/devbox — poder além do necessário para o MVP, reavaliar depois.
- **Exceção**: o build nativo do app mobile (Expo/EAS, toolchain Android/iOS) fica fora deste
  princípio; `mobile/` não é containerizado.

## Itens que permanecem fora do escopo do MVP (confirmados)

Avaliações/reputação, chat interno rico, rastreamento em tempo real do veículo pelo cliente,
emissão de documentos fiscais, pagamento in-app, verificação de documentos, multi-região,
divisão de carga e múltiplas paradas.
