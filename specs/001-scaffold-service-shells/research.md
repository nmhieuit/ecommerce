# Phase 0 Research: Scaffold Parties/Products/Baskets/Orders Service Shells

Three items in the Technical Context weren't already settled by the constitution or the existing ADRs (docs/adr/0001–0010 decided the BFF, gateway, and cross-cutting infra, but not the microservices' own internal framework choices). Each is resolved below.

## Decision 1: API framework style for the microservices themselves

**Decision**: ASP.NET Core Minimal APIs (same choice as ADR-0003 made for the BFF, extended here to the microservices).

**Rationale**: ADR-0003 already reasoned through Minimal APIs vs. Controllers vs. GraphQL for the BFF and picked Minimal APIs for low ceremony and native OpenAPI generation. These four service shells expose nothing but a health check today, and the constitution's Principle II contract-first discipline means any future domain endpoint needs the same clean OpenAPI output. Using a different style per service (or per service vs. BFF) would mean two conventions to teach and maintain for no benefit — consistency with the BFF's already-decided pattern is the deciding factor, not a fresh evaluation.

**Alternatives considered**: Controllers (MVC) — rejected for the same reason ADR-0003 rejected it for the BFF (more ceremony than a thin surface needs); introducing it only for services while the BFF uses Minimal APIs would be an unjustified inconsistency.

## Decision 2: .NET unit test framework

**Decision**: xUnit.

**Rationale**: The constitution mandates unit tests, integration tests via Testcontainers, and contract tests (Principle III) but doesn't name a specific .NET test framework — only Vitest/Testing Library are named, and those are for the frontend. xUnit is the de facto standard for new .NET projects (Microsoft's own templates default to it), has first-class Testcontainers-for-.NET integration (fixture/collection support designed around xUnit), and is what ADR-0006 already implicitly assumes when describing the Pact .NET SDK's typical test-runner integration.

**Alternatives considered**: NUnit — comparable maturity, no material advantage here and less common in current .NET Testcontainers examples/documentation. MSTest — Microsoft's older framework, weaker Testcontainers ecosystem alignment than xUnit today.

## Decision 3: Health-check implementation approach

**Decision**: ASP.NET Core's built-in `Microsoft.Extensions.Diagnostics.HealthChecks` middleware, exposing two distinct endpoints per service: `/health/live` (process liveness only) and `/health/ready` (includes a check that the service's own database is reachable).

**Rationale**: Constitution Principle VII requires "liveness and readiness probes" as two distinct signals, and spec.md's FR-003 and Edge Cases explicitly require "ready" to reflect actual database connectivity, not just that the process is up. The built-in health-checks middleware supports exactly this split (tagged health checks, `AddDbContextCheck<T>()` for the readiness probe) without pulling in a third-party package, keeping the scaffold as thin as the feature's scope demands.

**Alternatives considered**: A single combined `/health` endpoint — rejected because it can't express "process is up but database isn't reachable yet" as a distinct state, which the spec's edge cases explicitly require Kubernetes to be able to detect and act on differently (liveness failure restarts the pod; readiness failure just stops routing traffic to it).

## Summary

All three unknowns resolve to: reuse the BFF's already-decided API style (Minimal APIs), adopt the .NET ecosystem's default test framework (xUnit) which the platform's own contract-testing ADR already assumes, and use the framework-native health-check middleware that directly supports the liveness/readiness split the constitution requires. No new third-party dependency is introduced beyond what the constitution and existing ADRs already imply.
