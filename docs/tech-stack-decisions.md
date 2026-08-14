# Technical Stack Decisions

**Input for:** `/engineering:system-design`
**Status:** Accepted — see individual ADRs in [`docs/adr/`](adr/) for full context, options, and trade-off analysis
**Scope:** Product/tool selections within the categories fixed by [constitution.md](../.specify/memory/constitution.md). The constitution's own "Technology and Infrastructure Constraints" (C#/.NET 10, EF Core + SQL Server + Redis, RabbitMQ + MassTransit, gateway→BFF edge, React/TS/Vite/TanStack Query, Kubernetes via Ansible, Jenkins + SonarQube) are **not** re-decided here — they're inputs, not outputs, of this document.

## Decision Table

| # | Decision Area | Selected | One-Line Rationale | ADR |
|---|---|---|---|---|
| 1 | Identity provider | **Duende IdentityServer** | Only option that deploys like every other service and consumes the shared `ServiceDefaults` telemetry component — Keycloak's Java runtime can't | [ADR-0001](adr/0001-identity-provider.md) |
| 2 | API gateway | **YARP** | Runs as an ASP.NET Core app; token validation and telemetry match every service behind it; more actively maintained than Ocelot | [ADR-0002](adr/0002-api-gateway.md) |
| 3 | BFF implementation | **Minimal APIs** | Native OpenAPI generation, low ceremony fits "aggregate, don't implement business logic"; GraphQL rejected as conflicting with Principle II | [ADR-0003](adr/0003-bff-implementation-pattern.md) |
| 4 | OpenAPI→TS client codegen | **Orval** | Only tool that generates TanStack Query hooks directly — closes the Principle IX gap with zero hand-written wrapper code | [ADR-0004](adr/0004-openapi-client-codegen.md) |
| 5 | Event contract format | **JSON Schema** (shared contracts package, no registry service) | Matches MassTransit's native JSON serialization; tolerant-reader behavior comes free from `System.Text.Json`; avoids a new stateful registry | [ADR-0005](adr/0005-event-contract-format.md) |
| 6 | Contract testing | **Pact** + self-hosted Pact Broker | "Fail the producer's build" is Pact's exact workflow, including cross-service dependency tracking that would otherwise be built by hand | [ADR-0006](adr/0006-contract-testing-tool.md) |
| 7 | Secrets delivery | **External Secrets Operator + self-hosted Vault** | Vault's dynamic credentials/audit trail, consumed via plain K8s Secrets so no service needs native Vault integration | [ADR-0007](adr/0007-secrets-delivery.md) |
| 8 | Feature toggles | **Unleash** (self-hosted) | Self-hosted like the rest of the platform; ready-made admin UI serves Principle X's "rollback without a redeploy" better than a homegrown table | [ADR-0008](adr/0008-feature-toggle-system.md) |
| 9 | Design-system foundation | **Radix UI + Tailwind CSS**, docs via Storybook | Compile-time styling fits the CWV/bundle budgets; Radix's built-in accessibility reduces WCAG 2.2 AA testing burden | [ADR-0009](adr/0009-design-system-foundation.md) |
| 10 | Frontend monorepo tooling | **Turborepo** | Low-config incremental caching keeps CI fast for trunk-based, frequent-small-PR development, without Nx's heavier framework model | [ADR-0010](adr/0010-frontend-monorepo-tooling.md) |

## Full Stack Manifest (fixed + selected)

| Layer | Technology |
|---|---|
| Backend language/runtime | C# / .NET 10 (fixed) |
| Persistence | EF Core over SQL Server, database/schema-per-tenant; Redis for basket + caching (fixed) |
| Messaging | RabbitMQ via MassTransit, outbox + retry/DLQ (fixed) |
| Event contract format | JSON Schema, shared contracts package |
| Identity provider | Duende IdentityServer |
| API gateway | YARP |
| BFF | ASP.NET Core Minimal APIs |
| Frontend framework | React + TypeScript strict + Vite (fixed) |
| Server state | TanStack Query (fixed) |
| Generated API client | Orval (OpenAPI → TS + TanStack Query hooks) |
| Design system | Radix UI + Tailwind CSS, documented in Storybook |
| Frontend monorepo tooling | Turborepo + pnpm workspaces |
| Contract testing | Pact + self-hosted Pact Broker |
| Secrets | External Secrets Operator + self-hosted HashiCorp Vault |
| Feature toggles | Unleash (self-hosted) |
| Platform | Kubernetes, provisioned via Ansible (fixed) |
| CI/CD & quality gate | Jenkins + SonarQube (fixed) |
| Observability | OpenTelemetry → Elastic stack via shared `ServiceDefaults` (fixed) |

## New Infrastructure Introduced

These are the stateful services this stack selection adds beyond the constitution's fixed list — each is a real operational commitment, not a config choice:

- **Duende IdentityServer** — runs as just another service (no new *class* of infra, but licensing cost)
- **HashiCorp Vault** (+ External Secrets Operator) — new HA/backup/DR surface
- **Pact Broker** — lightweight, but new
- **Unleash** — Postgres-backed, new

## Open Follow-Ups for System Design

- Tenant → identity-client mapping model (ADR-0001) needs to be designed before `/engineering:system-design` can finalize the auth flow
- Vault dynamic-credential policies per dependency type (SQL Server, RabbitMQ) are not yet defined
- GraphQL was explicitly rejected for the BFF (ADR-0003) on constitutional grounds — if per-client-shape aggregation pain shows up during system design, that's a signal to raise a constitutional amendment discussion, not to route around it
