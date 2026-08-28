<!--
Sync Impact Report
Version change: 1.1.0 → 1.2.0
Rationale: MINOR — added Principle VIII (Docker-First Infrastructure): the OCI container image
is the single unit of build, run, and deploy across every environment; local development runs
the full backend stack through Compose. Extended the CI gate to build/scan/publish the image.
No principle removed or redefined.
Modified principles:
  - (added) VIII. Docker-First Infrastructure
  - Development Workflow & Quality Gates — CI gate now builds, scans and publishes the image
Added sections: none (Core Principles list extended with VIII)
Removed sections: none
Templates reviewed for consistency:
  - .specify/templates/plan-template.md ✅ (Constitution Check gate references this file)
  - .specify/templates/spec-template.md ✅ (no constitution-specific placeholders)
  - .specify/templates/tasks-template.md ✅ (setup/infra tasks align)
  - .specify/templates/checklist-template.md ✅
Propagation applied in the same change:
  - specs/001-mini-freight-requests/plan.md — Constitution Check row VIII, Technical Context,
    Structure Decision
  - specs/001-mini-freight-requests/research.md — decision 13 (Docker-first)
  - specs/001-mini-freight-requests/tasks.md — Phase 1/2 tasks reframed Docker-first (T006a,
    T010, T022 notes)
  - specs/001-mini-freight-requests/quickstart.md — Compose-first run instructions
  - specs/001-mini-freight-requests/spec.md — Assumptions line on containerized infrastructure
Follow-up TODOs: none
-->

# my-frete Constitution

my-frete is a freight/logistics marketplace platform (web + mobile) connecting shippers and
carriers. This constitution defines the non-negotiable engineering principles that govern how
the platform is designed, built, operated, and evolved. It is modeled on the operational
patterns of large-scale, safety- and trust-critical marketplaces (e.g. ride-hailing platforms):
secure by default, horizontally scalable, and observable end to end.

## Core Principles

### I. Service-Oriented & API-First

Functionality MUST be delivered as independently deployable services with explicit,
versioned contracts. Every capability is exposed through a documented API (OpenAPI/AsyncAPI)
before any client consumes it; the contract is the source of truth and is designed first.
Services MUST NOT share databases or reach into another service's storage — all cross-service
access goes through published APIs or events. Each service owns its data, its schema, and its
release cadence.

During the MVP / proof-of-concept phase, a service MAY be delivered as a module of a modular
monolith provided that: (a) it has an explicit code boundary and never accesses another
module's data directly; (b) it exposes a versioned OpenAPI/event contract; (c) it publishes
events through a transactional outbox in the same format it would use on a message bus.
Extraction into an independently deployable service is mandatory before scaling beyond the PoC
and MUST be recorded in the plan's Complexity Tracking.

Rationale: Independent contracts and data ownership let teams scale delivery in parallel and
allow the platform to grow service-by-service without coordinated big-bang releases. The
carve-out avoids paying full multi-service operational cost while the domain is still
stabilizing, without losing the extraction path.

### II. Security by Default (NON-NEGOTIABLE)

Security is a precondition for merge, not a follow-up. Every service MUST:
- Authenticate every request (short-lived tokens / OIDC) and enforce least-privilege
  authorization on every endpoint and message consumer — no implicit trust between services.
- Encrypt all data in transit (TLS 1.2+) and all sensitive data at rest.
- Store secrets only in a managed secret store; secrets MUST NOT appear in code, config files,
  logs, or CI output.
- Validate and sanitize all external input; defend against the current OWASP Top 10.
- Apply rate limiting and abuse protection on all public-facing and authenticated endpoints.

Any change that weakens an existing control MUST be called out explicitly in review and
approved by a security reviewer.

Rationale: The platform holds identity, location, payment, and shipment data; a single weak
endpoint compromises user trust and legal standing.

### III. Privacy & Regulatory Compliance

Personal data (identity, location, contact, payment, shipment history) MUST be collected only
for a declared purpose, minimized to what that purpose requires, and retained only as long as
needed. The system MUST support data-subject rights (access, correction, deletion, export) as
required by LGPD and GDPR. Access to production personal data MUST be logged and restricted to
a justified need. Data residency and cross-border transfer constraints MUST be respected.

Rationale: Compliance is a legal obligation and a trust differentiator; retrofitting privacy is
far more expensive than designing for it.

### IV. Test-First & Automated Quality Gates (NON-NEGOTIABLE)

Tests are written before or alongside the implementation and MUST fail before the code that
satisfies them exists. The following gates MUST pass in CI before merge:
- Unit tests for business logic.
- Contract tests for every published API and event schema (provider and consumer).
- Integration tests for inter-service communication and shared schemas.
- End-to-end tests for critical user journeys (request a freight, accept a load, track,
  complete, pay).
No merge to the main branch with failing or skipped required tests. Coverage MUST NOT decrease
on a change without explicit written justification in the PR.

Rationale: A marketplace with money and physical goods in motion cannot rely on manual
verification; automated gates are the only scalable safety net.

### V. Observability & Operability

Every service MUST emit: structured JSON logs with a correlation/trace ID, RED/USE metrics
(rate, errors, duration; utilization, saturation, errors), and distributed traces spanning
service-to-service calls. Every user-facing capability MUST have defined SLOs (availability and
latency) with alerting on error-budget burn. Health and readiness endpoints are mandatory.
A change is not "done" until its telemetry is in place and dashboards/alerts are updated.

Rationale: At scale, problems are found through telemetry, not by reading code; unobservable
services cannot be operated safely.

### VI. Reliability & Resilience

Services MUST degrade gracefully. Required patterns for all synchronous cross-service calls:
timeouts, retries with backoff and jitter, circuit breakers, and bulkheads. State-changing
operations exposed to clients or consumed from queues MUST be idempotent (idempotency keys or
natural dedup). The platform MUST scale horizontally (stateless services, externalized
session/state) and survive the loss of any single instance or availability zone without data
loss. Capacity and load assumptions MUST be documented and load-tested for critical paths.

Rationale: Freight operations run 24/7; partial failures are constant at scale and must never
cascade into full outages.

### VII. Simplicity, Versioning & Explicit Change

Start with the simplest design that meets the requirement (YAGNI); added complexity MUST be
justified against a concrete need in the plan's Complexity Tracking. All public APIs, event
schemas, and released artifacts use Semantic Versioning (MAJOR.MINOR.PATCH). Breaking changes
require a new MAJOR version, a documented migration path, and a deprecation window during which
the previous version keeps working. Infrastructure MUST be defined as code and applied through
the same review and CI process as application code.

Rationale: Predictable, reversible change lets a growing platform move fast without breaking
its integrators and operators.

### VIII. Docker-First Infrastructure

The OCI container image is the single unit of build, run, and deploy for every backend service
and job. Concretely:
- Each service/job MUST ship a `Dockerfile` (multi-stage, non-root runtime user, pinned base
  image) that is the authoritative way to build and run it. "Works on my machine" outside a
  container is not a supported path.
- The full local backend stack (services, database, cache, message/telemetry infra) MUST be
  startable with a single `docker compose up` from a checked-in Compose file, and MUST reach
  a healthy state using only documented environment variables and no manual steps.
- Local, CI, staging, and production MUST run the same image; environment differences are
  expressed only through configuration and secrets injected at runtime, never through separate
  build paths or code branches.
- All infra dependencies (DB engine + extensions, cache, brokers, collectors) MUST be pinned to
  explicit image tags (no `latest`) and declared in the Compose file / IaC, not installed ad hoc.
- CI MUST build the image, run vulnerability scanning on it (Principle II gate applies), and —
  on merge — publish it to the registry as the deployable artifact.
- Developer tools that cannot be containerized (native mobile build tooling / Expo, IDEs) are
  out of scope for this principle; it governs backend and infrastructure only.

Rationale: A single, reproducible artifact from laptop to production removes environment drift
as a class of incident, makes rollbacks trivial (redeploy a previous image), and keeps the
modular-monolith extraction path (Principle I) cheap because each module already runs the same
way it will run as a standalone service.

## Security & Privacy Requirements

- **Identity & access**: Central identity provider; MFA for all human production access;
  service-to-service auth via mTLS or signed short-lived tokens.
- **Data classification**: Every data store and stream is classified (public / internal /
  personal / sensitive-personal / payment). Controls scale with classification.
- **Payment data**: Card data handling is delegated to a PCI-DSS compliant provider; the
  platform MUST NOT store raw PAN/CVV.
- **Vulnerability management**: Automated dependency and container scanning in CI; known
  critical/high vulnerabilities block release. Security patches follow a defined SLA.
- **Auditability**: Security-relevant events (auth, permission changes, data export, admin
  actions) are logged to an append-only audit trail.
- **Incident response**: A documented runbook exists for security and privacy incidents,
  including breach-notification timelines required by LGPD/GDPR.
- **Least privilege by default**: New services and roles start with no access and are granted
  the minimum required, reviewed periodically.

## Development Workflow & Quality Gates

- **Branching & review**: Trunk-based development with short-lived branches. Every change lands
  via pull request with at least one qualified reviewer approval; changes touching security,
  auth, payments, or personal-data handling require a second reviewer from the relevant owning
  area.
- **CI is mandatory and blocking**: lint, type checks, unit/contract/integration/e2e tests,
  dependency + container vulnerability scan, secret scan, IaC validation, and a successful
  build of the service container image MUST all pass before merge. On merge to main, CI
  publishes the scanned image to the registry as the deployable artifact.
- **CD**: Automated deployment to staging on merge; production deployment is automated behind
  progressive rollout (canary / blue-green) with automated rollback on SLO breach.
- **Definition of Done**: contract published/updated, tests passing, telemetry + dashboards +
  alerts in place, docs/runbook updated, security and privacy impact considered.
- **Plan-time gate**: Every implementation plan MUST include a Constitution Check confirming
  compliance with these principles; violations MUST be resolved or explicitly justified in
  Complexity Tracking before implementation starts.

## Governance

This constitution supersedes other engineering practices where they conflict. All pull
requests and design reviews MUST verify compliance with these principles; reviewers are
expected to block non-compliant changes.

**Amendment procedure**: Proposed amendments are raised as a pull request modifying this file,
including a Sync Impact Report and rationale. Amendments require approval from the engineering
lead plus one additional maintainer. On merge, dependent templates and runbooks MUST be
reviewed for consistency in the same or an immediately following change.

**Versioning policy**: This constitution is versioned with Semantic Versioning:
- **MAJOR**: backward-incompatible governance changes, or removal/redefinition of a principle.
- **MINOR**: a new principle or section, or materially expanded guidance.
- **PATCH**: clarifications and wording fixes with no change in obligations.

**Compliance review**: Compliance is reviewed continuously at PR time and audited at least
quarterly. Recurring violations trigger a retrospective and, if needed, an amendment to close
the gap. Complexity introduced against Principle VII MUST be revisited at each such review and
removed when no longer justified.

**Version**: 1.2.0 | **Ratified**: 2026-08-28 | **Last Amended**: 2026-08-28
