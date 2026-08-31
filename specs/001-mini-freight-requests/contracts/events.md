# Internal Domain Events & Contracts (MVP)

No MVP os eventos são despachados **in-process** (MediatR notifications) e persistidos em
`notification_outbox` / `audit_event`. O envelope segue o formato abaixo para que a migração a
um barramento (ex.: RabbitMQ/SNS) após a PoC seja só uma troca de transporte — os payloads não
mudam. Versionamento por `type` sufixado (`v1`).

## Envelope

```json
{
  "id": "uuid",
  "type": "matching.offer.sent.v1",
  "occurredAt": "2026-08-28T12:00:00Z",
  "correlationId": "uuid",
  "aggregateType": "TransportRequest",
  "aggregateId": "uuid",
  "data": { }
}
```

## Eventos por módulo

### Requests
| type | Quando | data (resumo) | Consumidores |
|------|--------|---------------|--------------|
| `request.confirmed.v1` | Cliente confirma requisição imediata | requestId, clientId, origin, destination, weightG, estimate | Matching |
| `request.status_changed.v1` | Qualquer transição de status | requestId, from, to | Notifications |
| `request.schedule_requested.v1` | Cliente escolhe data (FR-018/019) | requestId, scheduledDate, weightG | Scheduling |
| `request.cancelled.v1` | Cancelamento pelo cliente (FR-026) | requestId, by | Matching, Scheduling, Trips, Notifications |

### Matching
| type | Quando | data | Consumidores |
|------|--------|------|--------------|
| `matching.session.started.v1` | Sessão criada | sessionId, requestId, mode | Notifications |
| `matching.offer.sent.v1` | Oferta enviada a 1 profissional (FR-013) | offerId, requestId, professionalId, respondBy | Notifications (`offer_received`), Audit |
| `matching.offer.accepted.v1` | Aceite válido dentro da janela (FR-016) | offerId, requestId, professionalId | Requests, Trips, Notifications, Audit |
| `matching.offer.declined.v1` | Recusa explícita | offerId, professionalId | Audit |
| `matching.offer.expired.v1` | Janela de 30 s expirou (FR-014/015) | offerId, professionalId | Audit |
| `matching.exhausted.v1` | Todos elegíveis OU limite 5 min / 8 prof. (FR-017a) | requestId, reason | Requests (→ `awaiting_schedule_decision`), Notifications |

### Scheduling
| type | Quando | data | Consumidores |
|------|--------|------|--------------|
| `scheduling.broadcast.sent.v1` | Agendamento notificado aos disponíveis (FR-021) | requestId, scheduledDate, professionalIds[] | Notifications |
| `scheduling.offer.accepted.v1` | Primeiro profissional aceita (FR-022) | requestId, professionalId, scheduledDate | Requests, Trips, Notifications, Audit |
| `scheduling.offer.filled_by_other.v1` | Demais profissionais | requestId, professionalId | Notifications |
| `scheduling.unfulfilled.v1` | Ninguém aceitou até a data (FR-024) OU cliente recusou (FR-023) | requestId, reason | Requests, Notifications |

### Trips
| type | Quando | data | Consumidores |
|------|--------|------|--------------|
| `trip.created.v1` | Vínculo firmado (imediato ou agendado) | tripId, requestId, clientId, professionalId, agreedAmount | Notifications |
| `trip.delivered.v1` | Profissional marca entrega (FR-025b) | tripId, deliveredAt | Notifications (`trip_delivered`), Accounts (libera profissional), Audit |
| `trip.client_responded.v1` | Cliente confirma/contesta (FR-025c) | tripId, response | Notifications, Audit |
| `trip.verification_due.v1` | Job 24 h sem resposta do cliente (FR-025d) | tripId, clientId, professionalId | Notifications (ambos) |
| `trip.cancelled.v1` | Cancelamento antes do início (FR-027) | tripId, by | Matching (reabre) / Scheduling (reoferta), Notifications, Audit |

### Accounts
| type | Quando | data | Consumidores |
|------|--------|------|--------------|
| `professional.verification_changed.v1` | Mudança de `verification_status` (FR-005) | professionalId, from, to, actor | Audit |
| `datasubject.request_created.v1` | Solicitação LGPD (FR-030) | userId, kind | Audit, back-office |

## Regras de idempotência / entrega
- Todo consumidor é idempotente por `envelope.id`.
- `notification_outbox.dedupe_key = "{type}:{recipientUserId}:{aggregateId}"`.
- Retentativa com backoff exponencial (máx. 5); após isso `state=failed` e alerta.
- `correlationId` origina no header `x-correlation-id` do app e é propagado a todos os spans e
  eventos (Constituição §Observability).
