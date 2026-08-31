# Feature Specification: Mini-Freight Requests (MVP)

**Feature Branch**: `001-mini-freight-requests`

**Created**: 2026-08-28

**Status**: Draft

**Input**: User description: "uma nova aplicação (inicialmente um MVP) de um aplicativo móvel de requisição de mini-fretes, o aplicativo deve permitir que um profissional cadastre-se para oferecer os serviços e indicar a carga máxima que pode transportar. Os usuários que vão consumir os serviços também devem se cadastrar e requisitar um transporte indicando os itens que comporão o transporte, um peso estimado e a origem e destino do transporte. O aplicativo deve apresentar uma estimativa de preço baseado na distância e peso e direcionar notificações para os profissionais cadastrados (1 por vez com um tempo de espera de 30 segundos para confirmar). Se nenhum profissional aceitar imediatamente, o aplicativo deve perguntar ao usuário se ele deseja agendar para um dia específico, a agenda é então enviada por notificação a todos os profissionais com disponibilidade da data solicitada e o primeiro a aceitar recebe o agendamento."

## Clarifications

### Session 2026-08-28

- Q: Qual é o limite para a busca imediata antes de o app oferecer o agendamento? → A: Encerra após o que ocorrer primeiro — 5 minutos totais ou 8 profissionais contatados.
- Q: Profissional com transporte ativo pode receber novas ofertas imediatas? → A: Não — fica inelegível para novas ofertas imediatas até concluir ou cancelar o transporte ativo.
- Q: Como um transporte passa a ser "concluído"? → A: O profissional marca como entregue (isso conclui o transporte); o cliente é notificado e pode contestar. Se o cliente não confirmar nem contestar em 24h, o app notifica ambos para verificar se o processo foi concluído.
- Q: Idade máxima da localização do profissional para entrar na ordenação por proximidade? → A: 5 minutos (configurável); acima disso, vai para o fim da fila.
- Q: Profissional pode aceitar mais de um agendamento para a mesma data? → A: Sim, até um limite configurável de N agendamentos por data (padrão N=1); atingido o limite naquela data, ele deixa de ser notificado por novos agendamentos dessa data.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Solicitar um mini-frete imediato (Priority: P1)

Um cliente cadastrado abre o aplicativo, informa os itens a transportar, um peso estimado, o
endereço de origem e o endereço de destino. O aplicativo mostra uma estimativa de preço
calculada a partir da distância e do peso. Ao confirmar, o pedido é oferecido a um profissional
elegível por vez; cada profissional tem 30 segundos para aceitar antes de o pedido passar ao
próximo. Quando um profissional aceita, o cliente é notificado com a identificação do
profissional e o transporte é considerado contratado.

**Why this priority**: É o núcleo do produto e o menor recorte que entrega valor real — sem ele
não há marketplace. Sozinho já constitui um MVP utilizável.

**Independent Test**: Com pelo menos um cliente e um profissional cadastrados, criar uma
requisição imediata e verificar que a estimativa aparece, que a oferta chega a um profissional
de cada vez, que a janela de 30 segundos é respeitada e que a aceitação vincula o profissional
ao transporte.

**Acceptance Scenarios**:

1. **Given** um cliente cadastrado e um profissional disponível cuja carga máxima é maior ou
   igual ao peso estimado, **When** o cliente confirma a requisição, **Then** o aplicativo
   exibe uma estimativa de preço e envia a oferta a esse profissional.
2. **Given** uma oferta enviada a um profissional, **When** ele aceita dentro de 30 segundos,
   **Then** o transporte é atribuído a ele e o cliente recebe uma notificação de confirmação.
3. **Given** uma oferta enviada a um profissional, **When** ele não responde em 30 segundos ou
   recusa, **Then** a oferta é encaminhada ao próximo profissional elegível.
4. **Given** uma requisição cujo peso estimado excede a carga máxima de um profissional,
   **When** o sistema seleciona candidatos, **Then** esse profissional não recebe a oferta.

---

### User Story 2 - Agendar quando não há aceite imediato (Priority: P2)

Se, após percorrer todos os profissionais elegíveis, nenhum aceitar a requisição imediata, o
aplicativo pergunta ao cliente se deseja agendar o transporte para um dia específico. O cliente
escolhe a data. O agendamento é então notificado a todos os profissionais que declararam
disponibilidade naquela data e cujo limite de carga comporta o peso; o primeiro a aceitar fica
com o agendamento e os demais são informados de que a vaga foi preenchida.

**Why this priority**: Recupera demanda que de outra forma seria perdida, mas depende do fluxo
imediato (P1) já existir.

**Independent Test**: Forçar um cenário sem aceite imediato, confirmar que o cliente recebe a
oferta de agendamento, escolher uma data, e verificar que apenas profissionais disponíveis
naquela data são notificados e que o primeiro aceite encerra a disputa.

**Acceptance Scenarios**:

1. **Given** uma requisição imediata que percorreu todos os profissionais elegíveis sem
   aceite, ou que atingiu o limite de 5 minutos ou 8 profissionais contatados, **When** a
   busca imediata termina, **Then** o aplicativo pergunta ao cliente se deseja agendar para um
   dia específico.
2. **Given** o cliente escolhe uma data de agendamento, **When** o agendamento é criado,
   **Then** todos os profissionais com disponibilidade nessa data e carga suficiente recebem a
   notificação simultaneamente.
3. **Given** um agendamento notificado a vários profissionais, **When** o primeiro deles
   aceita, **Then** o agendamento é atribuído a ele e os demais recebem aviso de que a vaga foi
   preenchida.
4. **Given** o cliente opta por não agendar, **When** ele recusa a oferta de agendamento,
   **Then** a requisição é encerrada como não atendida.
5. **Given** um profissional que já atingiu o limite de N agendamentos aceitos para uma data,
   **When** um novo agendamento é criado para essa mesma data, **Then** ele não é notificado
   nem pode aceitá-lo.

---

### User Story 3 - Cadastro de profissional com capacidade de carga (Priority: P1)

Um profissional baixa o aplicativo, cria uma conta, informa seus dados de contato e a carga
máxima (em kg) que consegue transportar. Após o cadastro, ele passa a poder receber ofertas de
transporte compatíveis com sua capacidade.

**Why this priority**: Sem oferta cadastrada não há a quem enviar requisições; é pré-requisito
do fluxo P1.

**Independent Test**: Concluir o cadastro de um profissional informando a carga máxima e
verificar que ele passa a ser considerado elegível para requisições cujo peso estimado é menor
ou igual à sua capacidade.

**Acceptance Scenarios**:

1. **Given** um profissional novo, **When** ele conclui o cadastro informando a carga máxima,
   **Then** a conta é criada e fica apta a receber ofertas.
2. **Given** um profissional cadastrado, **When** uma requisição com peso menor ou igual à sua
   carga máxima é criada, **Then** ele é incluído entre os candidatos elegíveis.

---

### User Story 4 - Cadastro de cliente (Priority: P1)

Um cliente cria uma conta com dados de contato antes de poder solicitar transportes.

**Why this priority**: Pré-requisito para qualquer requisição; parte mínima do MVP.

**Independent Test**: Criar uma conta de cliente e verificar que, autenticado, ele consegue
iniciar uma requisição de transporte.

**Acceptance Scenarios**:

1. **Given** um visitante, **When** ele conclui o cadastro de cliente, **Then** a conta é
   criada e ele pode iniciar requisições.
2. **Given** um visitante não cadastrado, **When** ele tenta criar uma requisição, **Then** o
   aplicativo exige o cadastro/login antes de prosseguir.

---

### Edge Cases

- Não há nenhum profissional cadastrado ou disponível no momento da requisição imediata → o
  aplicativo vai direto para a oferta de agendamento.
- O peso estimado excede a carga máxima de **todos** os profissionais → a requisição não pode
  ser atendida; o cliente é informado.
- O cliente fecha o aplicativo enquanto a requisição está sendo oferecida → a busca continua e
  o cliente é notificado do resultado.
- Profissional com transporte ativo recebe uma segunda requisição imediata → não é incluído
  entre os elegíveis enquanto o transporte atual não é concluído ou cancelado.
- Um profissional aceita a oferta imediata a partir do dispositivo, mas a janela de 30 segundos
  já expirou (aceite tardio) → o aceite é rejeitado e ele é informado.
- Dois profissionais aceitam o mesmo agendamento quase simultaneamente → apenas o primeiro
  registrado fica com a vaga; o outro recebe aviso.
- Origem e destino iguais, ou endereço não localizável → a requisição não avança e o cliente é
  solicitado a corrigir.
- Data de agendamento no passado ou fora da janela permitida → rejeitada na seleção da data.
- Profissional sem conexão durante a janela de oferta → tratado como não resposta e a oferta
  passa ao próximo.
- Cliente cancela a requisição durante a busca → a busca é interrompida.
- Profissional disponível que negou a permissão de localização → é chamado apenas ao final da
  ordem, após todos os profissionais com localização conhecida.
- Profissional cancela depois de aceitar → a requisição volta a ser oferecida (imediata) ou o
  agendamento é reaberto aos demais disponíveis.
- Profissional marca entrega mas o cliente contesta → requisição fica "contestada", evento
  registrado em auditoria; o profissional já foi liberado para novas ofertas.
- Cliente não confirma nem contesta em 24h → ambos recebem notificação de verificação; a
  requisição permanece "concluída".

## Requirements *(mandatory)*

### Functional Requirements

**Cadastro e contas**

- **FR-001**: O sistema MUST permitir que uma pessoa se cadastre como cliente informando nome,
  telefone e e-mail, e autentique-se para usar o aplicativo.
- **FR-002**: O sistema MUST permitir que uma pessoa se cadastre como profissional informando
  nome, telefone, e-mail e a carga máxima transportável em quilogramas.
- **FR-003**: O sistema MUST impedir a criação de requisições por usuários não autenticados.
- **FR-004**: O profissional MUST poder atualizar sua carga máxima e seu estado de
  disponibilidade (disponível / indisponível) para ofertas imediatas.
- **FR-005**: No MVP o profissional é ativado com dados auto-declarados, sem verificação prévia
  de documentos. O sistema MUST manter um campo de status de verificação em cada profissional
  (ex.: `nao_verificado`, `em_analise`, `verificado`, `rejeitado`) e registrar os eventos de
  mudança desse status, de modo que uma etapa de verificação manual ou automática possa ser
  introduzida no futuro sem alterar o modelo de dados nem o fluxo de matching.
- **FR-005a**: O status de verificação MUST ser visível ao cliente na confirmação da oferta
  (ex.: selo "profissional verificado" quando aplicável); no MVP todos os profissionais ativos
  aparecem como não verificados.

**Requisição de transporte**

- **FR-006**: O cliente MUST poder criar uma requisição informando a lista de itens, um peso
  total estimado (kg), um endereço de origem e um endereço de destino.
- **FR-007**: O sistema MUST validar que origem e destino são endereços distintos e
  localizáveis antes de prosseguir.
- **FR-008**: O sistema MUST calcular e exibir uma estimativa de preço antes da confirmação,
  derivada da distância entre origem e destino e do peso estimado.
- **FR-009**: A fórmula de preço MUST ser configurável (tarifa base, valor por unidade de
  distância, valor por unidade de peso) sem alteração de código.
- **FR-010**: O sistema MUST apresentar a estimativa como um valor previsto, deixando claro ao
  cliente que é uma estimativa.

**Matching imediato**

- **FR-011**: Ao confirmar uma requisição imediata, o sistema MUST montar a lista de
  profissionais elegíveis — disponíveis, com carga máxima maior ou igual ao peso estimado e
  **sem transporte ativo** (nenhuma requisição aceita ainda não concluída ou cancelada).
- **FR-011a**: Um profissional com transporte ativo MUST voltar a ser elegível para ofertas
  imediatas assim que esse transporte for concluído ou cancelado.
- **FR-011b**: O sistema MUST garantir que um profissional tenha no máximo uma oferta imediata
  pendente por vez; enquanto uma oferta a ele estiver na janela de 30 segundos, ele não recebe
  outra oferta imediata.
- **FR-012**: O sistema MUST ordenar os profissionais elegíveis pela menor distância entre a
  localização atual do profissional e o endereço de origem da requisição, chamando primeiro o
  mais próximo.
- **FR-012a**: O profissional MUST compartilhar sua localização atual enquanto estiver
  disponível para ofertas imediatas. Considera-se "recente" a localização atualizada há no
  máximo 5 minutos (valor configurável); um profissional disponível cuja última localização é
  mais antiga que esse limite, ou desconhecida, MUST ser colocado ao final da ordem de chamada.
- **FR-012b**: O sistema MAY limitar as ofertas imediatas a profissionais dentro de um raio
  máximo configurável da origem; fora desse raio, a requisição segue para o fluxo de
  agendamento.
- **FR-013**: O sistema MUST oferecer a requisição a um profissional por vez.
- **FR-014**: Cada profissional MUST ter exatamente 30 segundos para aceitar; expirada a
  janela sem aceite, ou havendo recusa, a oferta MUST passar ao próximo elegível.
- **FR-015**: O sistema MUST rejeitar aceites recebidos após o fim da janela de 30 segundos.
- **FR-016**: Quando um profissional aceita dentro da janela, o sistema MUST atribuir a
  requisição a ele, encerrar a busca e notificar o cliente com a identificação do profissional.
- **FR-017**: O sistema MUST notificar o cliente sobre o andamento (buscando, contratado, sem
  aceite).
- **FR-017a**: A busca imediata MUST ser encerrada quando ocorrer o primeiro dos seguintes
  eventos: 5 minutos decorridos desde a confirmação da requisição, ou 8 profissionais já
  contatados sem aceite. Atingido o limite, a requisição segue para o fluxo de agendamento
  (FR-018). Ambos os valores (tempo total e nº de profissionais) MUST ser configuráveis.

**Fluxo de agendamento**

- **FR-018**: Se todos os profissionais elegíveis forem percorridos sem aceite, o sistema MUST
  perguntar ao cliente se deseja agendar o transporte para um dia específico.
- **FR-019**: O cliente MUST poder escolher uma data de agendamento dentro de uma janela
  futura permitida; datas passadas MUST ser rejeitadas.
- **FR-020**: O profissional MUST poder declarar as datas em que tem disponibilidade para
  agendamentos.
- **FR-021**: Ao criar um agendamento, o sistema MUST notificar simultaneamente todos os
  profissionais com disponibilidade na data escolhida e carga máxima suficiente.
- **FR-022**: O primeiro profissional a aceitar o agendamento MUST recebê-lo; os demais MUST
  ser notificados de que a vaga foi preenchida.
- **FR-022a**: Um profissional MUST poder aceitar até N agendamentos para a mesma data, sendo N
  um limite configurável com valor padrão 1. Ao atingir N agendamentos aceitos em uma data, o
  profissional MUST deixar de ser notificado (FR-021) e de poder aceitar novos agendamentos
  nessa data.
- **FR-023**: Se o cliente recusar a oferta de agendamento, a requisição MUST ser encerrada
  como não atendida.
- **FR-024**: Se nenhum profissional aceitar o agendamento até a data, o sistema MUST notificar
  o cliente de que não foi possível atender.

**Pagamento**

- **FR-025**: No MVP o aplicativo NÃO processa pagamento. O sistema MUST registrar em cada
  transporte o valor combinado (padrão: a estimativa exibida; editável pelas partes antes da
  conclusão) e permitir que cliente e profissional marquem o pagamento como "acertado fora do
  app". A liquidação financeira ocorre diretamente entre as partes.
- **FR-025a**: O sistema MUST guardar o valor combinado no histórico de cada requisição e de
  cada profissional para fins de acompanhamento, sem movimentar dinheiro.

**Conclusão do transporte**

- **FR-025b**: O profissional MUST poder marcar o transporte como entregue. Essa ação conclui o
  transporte (o transporte é a fase de execução da requisição já contratada), reflete-se no
  status consolidado da requisição como "concluída" e libera o profissional para novas ofertas
  (FR-011a). Ver o mapeamento técnico de estados em `data-model.md`.
- **FR-025c**: Ao marcar-se como entregue, o sistema MUST notificar o cliente, que MUST poder
  confirmar o recebimento ou contestar a conclusão.
- **FR-025d**: Se o cliente não confirmar nem contestar em até 24 horas após a marcação de
  entrega, o sistema MUST notificar cliente e profissional pedindo que verifiquem se o
  transporte foi concluído. O prazo de 24 horas MUST ser configurável.
- **FR-025e**: Uma contestação do cliente MUST ser registrada na trilha de auditoria e sinalizada
  para acompanhamento (resolução da disputa em si está fora do escopo do MVP).

**Cancelamento e notificações**

- **FR-026**: O cliente MUST poder cancelar a requisição enquanto ela estiver em busca ou antes
  do início do transporte agendado.
- **FR-027**: Um profissional que aceitou MUST poder cancelar antes do início; nesse caso a
  requisição imediata volta a ser oferecida e o agendamento é reaberto aos demais disponíveis.
- **FR-028**: O sistema MUST entregar as ofertas e atualizações de status por notificação ao
  dispositivo do usuário.
- **FR-029**: O sistema MUST manter um histórico das requisições de cada cliente e dos
  transportes de cada profissional, com data, trajeto, peso, preço estimado e status final.

**Privacidade e conformidade** (deriva da Constituição do projeto)

- **FR-030**: O sistema MUST coletar apenas os dados pessoais necessários ao serviço e permitir
  ao usuário solicitar acesso, correção e exclusão dos seus dados.
- **FR-031**: Contato entre cliente e profissional MUST ser possível apenas após um vínculo
  (aceite) existir entre eles.
- **FR-032**: O sistema MUST registrar em trilha de auditoria os eventos relevantes de
  atribuição, aceite, recusa e cancelamento.

### Key Entities *(include if feature involves data)*

- **Cliente**: pessoa que solicita transportes. Atributos: nome, telefone, e-mail, histórico
  de requisições.
- **Profissional**: pessoa que executa transportes. Atributos: nome, telefone, e-mail, carga
  máxima (kg), estado de disponibilidade para ofertas imediatas, localização atual e momento da
  última atualização de localização (considerada válida por até 5 minutos), datas de
  disponibilidade para agendamento, status de
  verificação (`nao_verificado` / `em_analise` / `verificado` / `rejeitado`), histórico de
  transportes.
- **Requisição de Transporte**: pedido criado pelo cliente. Atributos: itens, peso estimado
  (kg), endereço de origem, endereço de destino, distância calculada, preço estimado, tipo
  (imediata ou agendada), data de agendamento (quando aplicável), status consolidado (rascunho,
  buscando, aguardando decisão, agendada, contratada, concluída, contestada, não atendida,
  cancelada — ver mapeamento técnico em `data-model.md`), momento da marcação de entrega,
  momento e tipo da resposta do cliente (confirmada / contestada), cliente associado,
  profissional associado (quando houver), valor combinado e indicador de pagamento acertado
  fora do app.
- **Oferta**: proposta de uma requisição a um profissional específico. Atributos: requisição,
  profissional, tipo (imediata ou agendamento), momento de envio, prazo de resposta, resultado
  (aceita, recusada, expirada, preenchida por outro).
- **Regra de Precificação**: parâmetros configuráveis usados na estimativa. Atributos: tarifa
  base, valor por unidade de distância, valor por unidade de peso, vigência.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Um cliente consegue criar uma requisição de transporte — dos itens à estimativa
  de preço exibida — em menos de 3 minutos.
- **SC-002**: A estimativa de preço é exibida ao cliente em até 5 segundos após a informação de
  origem, destino e peso.
- **SC-003**: Em pelo menos 80% das requisições imediatas feitas quando há ao menos um
  profissional elegível disponível, um profissional é atribuído dentro do limite da busca
  imediata (5 minutos / 8 profissionais).
- **SC-004**: 100% das ofertas imediatas respeitam a janela de 30 segundos: nenhum profissional
  recebe menos de 30 segundos para responder e nenhum aceite após o prazo é aceito.
- **SC-005**: Quando um agendamento é aceito por mais de um profissional, exatamente um fica
  com a vaga em 100% dos casos.
- **SC-006**: Um profissional conclui o cadastro, incluindo a carga máxima, em menos de 3
  minutos.
- **SC-007**: Pelo menos 70% das requisições sem aceite imediato resultam em uma decisão
  explícita do cliente sobre agendar (sim ou não), em vez de serem abandonadas.
- **SC-008**: O cliente recebe uma notificação de resultado (contratado, agendado ou não
  atendido) para 100% das requisições confirmadas.
- **SC-009**: 100% dos transportes marcados como entregues pelo profissional geram notificação
  ao cliente; e 100% dos que ficam sem confirmação nem contestação por 24h geram a notificação
  de verificação a ambas as partes.

## Assumptions

- O aplicativo é móvel e cada usuário opera em um único dispositivo com conexão à internet e
  permissão de notificações; a entrega de notificações depende dessa permissão.
- O MVP opera em uma única região/cidade de atuação definida na configuração; expansão
  multirregião está fora do escopo desta fase.
- "Mini-frete" refere-se a cargas pequenas transportáveis por um único profissional em um único
  veículo; não há divisão de carga entre profissionais nem múltiplas paradas — apenas uma
  origem e um destino por requisição.
- A distância usada na estimativa é a distância de deslocamento por vias entre origem e
  destino, obtida de um serviço de mapas; o peso é o valor estimado informado pelo cliente
  (sem pesagem física no MVP).
- A estimativa de preço é aproximada e não constitui cobrança automática. O app não processa
  pagamento no MVP: registra o valor combinado (padrão = estimativa) e a marcação de "pago fora
  do app"; a liquidação é feita diretamente entre cliente e profissional.
- Autenticação segue padrão de mercado para apps móveis (por exemplo, e-mail/senha ou provedor
  de identidade); o método exato é decisão de implementação, respeitando a Constituição.
- A disponibilidade do profissional para ofertas imediatas é um estado que ele controla
  manualmente no app. Enquanto disponível, o app coleta a localização atual do profissional
  (com sua permissão) para ordenar as ofertas por proximidade da origem.
- No MVP o profissional é ativado com dados auto-declarados; o modelo já contempla um status de
  verificação para permitir, no futuro, verificação manual ou automática (documentos/CNH) sem
  redesenho do fluxo.
- Avaliações/reputação, chat interno rico, rastreamento em tempo real do veículo e emissão de
  documentos fiscais estão fora do escopo do MVP.
- A "janela futura permitida" para agendamento é assumida como até 30 dias a partir da data
  atual, ajustável por configuração.
- Cada requisição tem no máximo um profissional atribuído por vez.
- A infraestrutura do backend é *Docker-first* (Constituição §VIII): o serviço e suas
  dependências (banco, cache, telemetria) rodam como contêineres a partir de uma única imagem/
  Compose versionados, iguais em desenvolvimento, homologação e produção. O build nativo do app
  móvel não é containerizado.
