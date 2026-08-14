<!--
Sync Impact Report
==================
Version change: (uninitialized template) → 1.0.0
Bump rationale: Initial ratification. First concrete constitution replacing the unfilled
Spec Kit template; all placeholder tokens resolved.

Principles defined (template slots → concrete principles):
- [PRINCIPLE_1_NAME] → I. Service Autonomy and Bounded Context
- [PRINCIPLE_2_NAME] → II. Contract-First Integration
- [PRINCIPLE_3_NAME] → III. Test-First Development (NON-NEGOTIABLE)
- [PRINCIPLE_4_NAME] → IV. Event-Driven by Default
- [PRINCIPLE_5_NAME] → V. Tenant Isolation Is a Security Boundary
- (added) VI. Secure by Default
- (added) VII. Observable by Default
- (added) VIII. Performance and Resilience Budgets
- (added) IX. Frontend Discipline
- (added) X. Toggle-Gated, Reversible Delivery

Sections added:
- [SECTION_2_NAME] → Technology and Infrastructure Constraints
- [SECTION_3_NAME] → Development Workflow and Quality Gates
- Governance (amendment procedure, semantic versioning policy, compliance review, deviations)

Sections removed: none (template contained no prior content).

Deferred placeholders / follow-up TODOs: none. All tokens resolved.
-->

# Commerce Platform Constitution

The Commerce Platform is a multi-tenant e-commerce system built as C# .NET 10 microservices
(parties, products, baskets, orders, logistics, invoices) behind an API gateway and BFF layer,
with React SPA web and mobile-web clients serving two tenants, deployed to Kubernetes.

This document defines the non-negotiable rules that apply to every feature, in every service,
for the lifetime of the platform. Feature behaviour, schemas, endpoint definitions, and
acceptance criteria are out of scope here; they belong in per-feature specifications under
`specs/[feature]/`.

## Core Principles

### I. Service Autonomy and Bounded Context

Each microservice MUST own its data exclusively. No service may read or write another service's
database, schema, or tables; cross-service data access happens only through a published API or a
published integration event. Each service MUST be independently deployable and independently
versioned.

Internal architecture is chosen per service, not mandated globally. The default is vertical-slice
organisation. Escalation to layered Clean Architecture with DDD aggregates and CQRS is permitted
only for services that own genuine business invariants, and the `plan.md` for any such feature
MUST justify the added complexity. Unjustified architectural ceremony is a violation.

**Rationale**: A shared database is the fastest way for a microservice system to decay into a
distributed monolith that can no longer be deployed or reasoned about independently. Letting each
service choose its own internal structure — while requiring justification for the expensive option
— keeps complexity proportional to the domain rather than spread by imitation.

### II. Contract-First Integration

API and event contracts MUST be written and reviewed before implementation. HTTP APIs are defined
by OpenAPI; asynchronous integration events are defined by versioned schemas in a shared contracts
location. Client code MUST be generated from contracts and MUST NOT be hand-written.

Breaking changes to any published contract require a new explicit version plus a documented
deprecation window, and the previous version MUST keep working for the duration of that window.
Consumers MUST tolerate unknown fields.

**Rationale**: With a gateway, a BFF, six services, and two client applications, contracts are the
integration surface of the entire platform. Agreeing on them before code — and versioning them
deliberately — is far cheaper than renegotiating them after the first breaking change ships.

### III. Test-First Development (NON-NEGOTIABLE)

Red-Green-Refactor is mandatory. No implementation code is merged without a preceding failing test
that it makes pass.

Every service MUST carry:

- Unit tests for domain and application logic.
- Integration tests exercising real dependencies via Testcontainers (SQL Server, Redis, RabbitMQ).
  In-memory database providers and hand-rolled fakes for infrastructure are NOT acceptable
  substitutes.
- Consumer-driven contract tests for every HTTP and event boundary it participates in. Breaking a
  published contract MUST fail the producer's build.

Frontend logic and components MUST be tested with Vitest and Testing Library, asserting behaviour
through accessible roles rather than implementation detail. The SonarQube quality gate is the
coverage authority and MUST pass before merge.

**Rationale**: Tests written after the fact validate what the code does rather than what it should
do. Requiring real infrastructure in integration tests prevents the common failure where a suite
passes against in-memory substitutes that behave nothing like production.

### IV. Event-Driven by Default

Inter-service communication defaults to asynchronous integration events over RabbitMQ via
MassTransit. Synchronous REST/HTTP calls between services are permitted only when the caller
genuinely cannot proceed without an immediate answer, and MUST be justified in the plan.

Every publisher MUST use the transactional outbox pattern so that state change and event
publication cannot diverge. Every consumer MUST be idempotent and MUST handle out-of-order and
duplicate delivery. Multi-service workflows MUST be modelled as sagas with explicit compensation,
never as distributed transactions.

**Rationale**: Synchronous chains couple availability — one slow service degrades every caller. The
outbox, idempotency, and saga rules exist because the data-consistency bugs they prevent are close
to impossible to diagnose once they reach production.

### V. Tenant Isolation Is a Security Boundary

The platform serves multiple tenants from a shared deployment. Tenant identity MUST be resolved
once at the edge (host or verified token claim), propagated explicitly through
gateway → BFF → services → events, and never inferred from user-supplied request bodies or query
parameters.

Each tenant's data MUST reside in a separate database or schema per service, with the connection
resolved per request from the tenant context. Code paths that can reach persistence without a
resolved tenant context MUST NOT exist. Any cross-tenant data exposure is a Severity-1 security
defect, not a bug.

**Rationale**: With physically separated per-tenant data, a tenant-resolution failure is not a
display glitch — it is a data breach. Treating isolation as a security boundary rather than a
feature makes it reviewable and makes the failure mode unambiguous.

### VI. Secure by Default

Authentication is issued centrally by the identity server. Tokens MUST be validated at the gateway
AND independently at each service; the gateway is not a trust boundary services may rely on.

Authorization is deny-by-default: every endpoint, message handler, and BFF route declares its
required policy explicitly, and an endpoint without an authorization decision MUST fail the build
or the review. All input crossing a trust boundary MUST be validated server-side; client-side
validation is UX only. Secrets MUST NOT appear in source, configuration files, container images, or
logs — they are injected at runtime from the cluster secret store. OWASP Top 10 mitigations apply to
every externally reachable surface. PII MUST be encrypted in transit and at rest and MUST NOT be
written to logs or traces.

**Rationale**: Deny-by-default is the only posture that fails safe when a developer forgets.
Independent validation at each service keeps the system secure even when a service is later exposed
inside the mesh or the gateway is misconfigured.

### VII. Observable by Default

Every service MUST emit OpenTelemetry traces, metrics, and structured logs to the Elastic stack
through a shared ServiceDefaults component; observability MUST NOT be configured per service by
hand. A correlation ID MUST be generated at the edge and propagated across every synchronous call
and every message, including through the frontend.

Logs MUST be structured, never interpolated strings, and MUST carry service, tenant, and
correlation identifiers. Every service MUST expose liveness and readiness probes. A feature is not
complete until it is debuggable in production from telemetry alone.

**Rationale**: In a distributed system, a failure that cannot be traced across service boundaries
cannot be diagnosed. A shared defaults component prevents the instrumentation from drifting into
several incompatible configurations.

### VIII. Performance and Resilience Budgets

Performance is a stated budget, not an aspiration. Every service MUST declare its SLOs — latency,
error rate, and availability — in its service manifest, and those SLOs MUST be measured
continuously from the telemetry required by Principle VII.

Unless a service documents a justified alternative, the platform defaults are:

- Client-facing BFF read: p95 ≤ 300 ms, p99 ≤ 800 ms.
- Internal service API: p95 ≤ 150 ms, p99 ≤ 500 ms.
- Integration events processed within 5 s of publication at p95.
- 99.9% monthly availability.
- 5xx responses below 0.1% of requests.

Client applications MUST meet Core Web Vitals at p75 on mobile-web (LCP ≤ 2.5 s, INP ≤ 200 ms,
CLS ≤ 0.1) and MUST declare and enforce a JavaScript bundle budget per route entry point.

Resilience is part of the budget. Every outbound HTTP or messaging call MUST declare an explicit
timeout and MUST be wrapped in retry and circuit-breaker policies via
`Microsoft.Extensions.Resilience`; unbounded waits MUST NOT exist anywhere in the system. Every
collection endpoint MUST paginate, every query MUST be bounded, and N+1 access patterns are defects
rather than optimisation opportunities. Critical user paths MUST have automated performance tests,
and a regression that pushes a path outside its budget blocks release. Sustained SLO breach consumes
the error budget: when a service's error budget is exhausted, reliability work takes priority over
new feature work in that service until it recovers.

**Rationale**: A performance rule with no numbers cannot fail a review. Named budgets make
regressions objective, and the error-budget clause gives a breached SLO an actual consequence
instead of a red dashboard nobody owns.

### IX. Frontend Discipline

Web and mobile-web are React + TypeScript SPAs built with Vite, living in a single monorepo.
TypeScript runs in strict mode; `any` requires a written justification.

Both applications MUST consume one shared, versioned design-system package and one generated API
client — duplicated UI primitives or hand-written API calls across apps are a violation. Server
state MUST be managed with TanStack Query, and server data MUST NOT be copied into a global client
store. Global client state is reserved for genuine cross-cutting UI concerns and MUST stay minimal.
Components MUST be accessible (WCAG 2.2 AA: keyboard operable, correct roles and labels, visible
focus). Frontends talk to the BFF only, never directly to microservices.

**Rationale**: Two applications across two tenants is exactly the shape that produces divergent
copies of the same button and the same API call. One design system and one generated client is the
structural fix. Keeping server data out of global stores eliminates the most common class of React
state bug.

### X. Toggle-Gated, Reversible Delivery

Every non-trivial change MUST ship behind a feature toggle. Each toggle MUST have a named owner and
a removal date recorded at creation; stale toggles are technical debt and MUST be removed once the
change is fully rolled out.

Rolling back any release MUST NOT require a code change or a redeploy. Database migrations MUST be
backward compatible with the previously deployed version so that deploy and rollback are safe while
both versions run — expand/contract, never destructive in a single step.

**Rationale**: The ability to disable a change without a deployment is what keeps incidents short.
That ability is worthless if the schema cannot roll back with the code, which is why migration
compatibility belongs in the same principle.

## Technology and Infrastructure Constraints

The following stack decisions and standing infrastructure rules are fixed platform-wide and MUST NOT
be re-litigated per feature. Changing any of them is a constitutional amendment.

- **Backend**: C# / .NET 10. Nullable reference types enabled; warnings treated as errors.
- **Persistence**: EF Core over SQL Server, database-per-service and schema-or-database-per-tenant.
  Redis backs the basket store and distributed caching.
- **Messaging**: RabbitMQ via MassTransit, with outbox and retry/dead-letter policies configured
  centrally.
- **Edge**: load balancer → API gateway → BFF. The BFF is the only backend surface the clients may
  call, and it aggregates rather than implements business logic.
- **Identity and access**: central identity server issuing tokens; centrally defined authorization
  policies.
- **Frontend**: React, TypeScript strict, Vite, TanStack Query, shared design system in a monorepo.
- **Platform**: containers on Kubernetes, provisioned and configured through Ansible. Configuration
  comes from environment and cluster secrets and MUST NOT be baked into images. Services MUST be
  stateless so any instance can serve any request, and MUST be horizontally scalable to meet the
  budgets in Principle VIII.
- **CI/CD and quality**: Jenkins pipelines with SonarQube as the quality gate.
- **Local development**: every service MUST be runnable locally with its real dependencies via
  containers by a single command.

## Development Workflow and Quality Gates

- **Branching**: trunk-based development — short-lived branches, small PRs, frequent merges to main.
- **Commits**: Conventional Commits are mandatory and drive semantic versioning and changelog
  generation.
- **Style**: code style is machine-enforced and never debated in review. `.editorconfig` plus Roslyn
  analyzers for C#, ESLint plus Prettier for TypeScript. Analyzer and lint violations fail the build.
- **PR gate**: build → unit tests → integration tests (Testcontainers) → contract tests → SonarQube
  quality gate → container image vulnerability scan. A red gate blocks merge. There are no
  exceptions, overrides, or "fix it in the next PR" waivers.
- **Performance gate**: performance tests for critical paths run on a scheduled pipeline against a
  production-like environment, and a budget regression blocks the release even when the PR gate is
  green.
- **Review**: every PR requires at least one approving review. Changes to a published contract, an
  authorization policy, or a tenant-resolution path require review from the owning service's
  maintainer.
- **Decisions**: architecturally significant decisions MUST be recorded as ADRs in the repository.
- **Compliance**: reviewers MUST verify constitutional compliance, not only correctness.

## Governance

- This constitution supersedes all other development practices, style guides, and team habits. Where
  any other document conflicts with it, this document wins.
- Amendments require a pull request that states the rationale and the migration impact on existing
  services, plus approval from the platform maintainers.
- Versioning is semantic: MAJOR for removing or redefining a principle in a backward-incompatible
  way, MINOR for adding a principle or materially expanding guidance, PATCH for clarifications and
  wording.
- Compliance is reviewed at every PR and audited at each quarterly release checkpoint.
- Any deviation MUST be documented in the feature's `plan.md` with the justification and the simpler
  alternative that was rejected, and MUST be time-bounded. Undocumented deviations are defects.
- Runtime development guidance for agents lives in the repository's agent guidance file; it
  elaborates on this constitution and MUST NOT contradict it.

**Version**: 1.0.0 | **Ratified**: 2026-08-13 | **Last Amended**: 2026-08-13
