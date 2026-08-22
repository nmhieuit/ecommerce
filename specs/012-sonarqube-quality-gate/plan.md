# Implementation Plan: SonarQube Quality Gate as a Merge Blocker

**Branch**: `012-sonarqube-quality-gate` | **Date**: 2026-08-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/012-sonarqube-quality-gate/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Stand up the repository's first Jenkins CI pipeline (none exists today) as a declarative
`Jenkinsfile` running five ordered, independently-reported stages — build, unit tests, integration
tests, contract tests, SonarQube quality gate — against a Jenkins Multibranch Pipeline job wired to
GitHub. Each stage publishes its own required GitHub status check; branch protection requires all
five with admin bypass disabled, so a failing SonarQube gate (or any earlier stage) blocks merge
for every role with no override control (FR-004/FR-005). The SonarQube stage uses the Jenkins
SonarQube Scanner plugin's `waitForQualityGate()` inside a `timeout()` so the *gate result* itself,
not just the scan, blocks the pipeline, and an unreachable SonarQube server fails closed (FR-008).
Coverage feeding the gate comes from existing `coverlet`-based `dotnet test` output (backend) and
Vitest `--coverage` lcov output (frontend), merged into one Sonar project so the PR shows a single
gate result with coverage/duplication/code-smell metrics via SonarQube's native GitHub PR
decoration (FR-006), requiring no custom reporting code.

## Technical Context

**Language/Version**: Groovy (Jenkins declarative `Jenkinsfile`); orchestrates existing C#/.NET 10
(`Ecommerce.slnx`) and TypeScript (pnpm/Turborepo `frontend/`) toolchains — no new application
language introduced.

**Primary Dependencies**: Jenkins plugins — GitHub Branch Source, SonarQube Scanner, Pipeline
(declarative). SonarScanner for .NET (`dotnet-sonarscanner`) and SonarQube's JS/TS analyzer (server
side, no new frontend package beyond `@vitest/coverage-v8` for lcov output). Existing
`coverlet.collector` (already referenced repo-wide) for backend coverage — no new backend package.

**Storage**: N/A — no application data added. Coverage/analysis artifacts are transient CI build
outputs (Cobertura XML, lcov.info) consumed by the Sonar scanner, not persisted by this feature.

**Testing**: The feature's own correctness is validated by exercising the pipeline itself against
real PRs (see `quickstart.md`), not by a new unit-test suite — there is no application code to unit
test. Existing `*.Api.UnitTests` / `*.Api.IntegrationTests` / `*.Api.ContractTests` (.NET, xUnit) and
frontend Vitest suites are the tests the new pipeline stages execute.

**Target Platform**: Jenkins controller/agents (with Docker available for Testcontainers-based
integration tests, per `010-testcontainers-integration-tests`) and GitHub.com as the PR host
(per user clarification during `/speckit-specify`).

**Project Type**: CI/CD infrastructure wiring inside the existing monorepo — no new runnable
service, no frontend feature, no API surface change.

**Performance Goals**: Not a runtime-performance feature; existing budgets in Constitution
Principle VIII are unaffected. Pipeline wall-clock time is a secondary concern deferred to
implementation (e.g., parallelizing independent stages) and is not a stated acceptance criterion.

**Constraints**: Must not introduce new IaC tooling solely for one repository's branch protection
(research.md Decision 5); must reuse existing coverage tooling rather than adding new packages
(research.md Decision 4); quality gate must fail closed on SonarQube unavailability (FR-008).

**Scale/Scope**: Six existing services (`baskets`, `bff`, `gateway`, `orders`, `parties`,
`products`) plus the frontend monorepo, all analyzed under one Sonar project; scope is limited to
the five stages named in SCRUM-22 (build → unit → integration → contract → SonarQube) — the
constitution's additional container-vulnerability-scan stage is explicitly out of scope here (see
Complexity Tracking).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **Principle III (Test-First, NON-NEGOTIABLE)** — "The SonarQube quality gate is the coverage
  authority and MUST pass before merge." This feature exists to satisfy that clause literally.
  **PASS.**
- **Principle VI (Secure by Default)** — deny-by-default extends naturally to merge control: no
  override path for any role (FR-004/FR-005) mirrors "an endpoint without an authorization decision
  MUST fail the build." **PASS.**
- **Development Workflow and Quality Gates** — constitution mandates "build → unit tests →
  integration tests (Testcontainers) → contract tests → SonarQube quality gate → container image
  vulnerability scan... no exceptions, overrides, or waivers." This feature implements the first
  five stages exactly as ordered; the sixth (vulnerability scan) is **not** implemented here.
  **CONDITIONAL PASS — see Complexity Tracking** for why this partial scope is a deliberate,
  time-bounded boundary rather than a silent gap.
- **Governance** ("architecturally significant decisions MUST be recorded as ADRs") — standing up
  the first Jenkins pipeline and choosing the GitHub-branch-protection enforcement mechanism is
  architecturally significant. **ACTION**: an ADR should be authored during implementation
  documenting the Multibranch Pipeline + required-status-checks approach (research.md Decisions
  1, 2, 5); tracked as an implementation task, not a plan-time blocker.
- No other principle (Service Autonomy, Contract-First, Event-Driven, Tenant Isolation, Observable
  by Default, Performance Budgets, Frontend Discipline, Toggle-Gated Delivery) is implicated by a
  CI-wiring change with no application code. **N/A for all.**

*Re-checked after Phase 1 design (data-model.md, contracts/, quickstart.md): no new violations
introduced — the design stayed within GitHub/Jenkins/SonarQube native mechanisms throughout, adding
no custom services, data stores, or bespoke enforcement code.* **PASS.**

## Project Structure

### Documentation (this feature)

```text
specs/012-sonarqube-quality-gate/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   └── pipeline-stage-contract.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
Jenkinsfile                    # NEW — declarative pipeline, 5 stages (research.md Decisions 1-3)
sonar-project.properties       # NEW — one Sonar project covering backend + frontend (Decision 4)

services/
├── baskets/tests/{Baskets.Api.UnitTests,Baskets.Api.IntegrationTests,Baskets.Api.ContractTests}/
├── bff/tests/{Bff.Api.UnitTests,Bff.Api.IntegrationTests,Bff.Api.ContractTests}/
├── gateway/tests/{Gateway.Api.UnitTests,Gateway.Api.IntegrationTests}/
├── orders/tests/{Orders.Api.UnitTests,Orders.Api.IntegrationTests,Orders.Api.ContractTests}/
├── parties/tests/{Parties.Api.UnitTests,Parties.Api.IntegrationTests}/
└── products/tests/{Products.Api.UnitTests,Products.Api.IntegrationTests,Products.Api.ContractTests}/
    # EXISTING test projects — the Jenkinsfile's unit/integration/contract stages run these via
    # `dotnet test` with coverage collection; no new test projects are created by this feature.

frontend/
└── (existing pnpm/Turborepo workspace — `turbo run test -- --coverage` feeds the frontend
   coverage input to the Sonar project; no structural change to frontend/)

docs/adr/
└── 0012-ci-quality-gate-enforcement.md   # NEW — ADR for the pipeline/branch-protection approach
                                             (Constitution Check "Governance" action item)
```

**Structure Decision**: This is CI/infrastructure wiring, not a new application component — the only
new source artifacts are a repo-root `Jenkinsfile` and Sonar scanner configuration, plus one new ADR
recording the enforcement approach. All test execution runs against the existing per-service test
projects and the existing frontend Turborepo workspace; no `src/`, `backend/`, or `frontend/src/`
changes are introduced. GitHub branch protection configuration itself is not a repository artifact
(see research.md Decision 5) and is recorded procedurally in `quickstart.md` instead.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|---------------------------------------|
| Constitution's PR gate names build → unit → integration → contract → SonarQube → **container image vulnerability scan**; this feature stops before the vulnerability scan. | SCRUM-22's own acceptance criteria and test scenarios scope the gate to build→unit→integration→contract→SonarQube only; bundling an unscoped vulnerability-scan stage into this ticket would expand its boundary beyond what was requested and risks shipping an under-specified scan step (tooling, severity thresholds, and image registry integration are undefined). | Deferring the vulnerability scan to a separate, explicitly-scoped follow-up ticket was preferred over guessing its requirements here. This is a time-bounded deviation: the vulnerability-scan stage MUST be added as a follow-up before this feature can be considered a complete implementation of the constitution's Development Workflow section — tracked as an open item in this plan, not closed silently. |

### Follow-up tracking (added during implementation, T023)

The deferred container image vulnerability scan is recorded as **Action Item 4 in
[ADR-0012](../../docs/adr/0012-ci-quality-gate-enforcement.md)**, which states plainly that this
pipeline implements five of the constitution's six mandated gates. That ADR action item is the
durable tracking record; it carries a `SCRUM-TBD` placeholder because the backlog ticket itself has
not been raised yet. Raising that ticket and replacing the placeholder with its key is the one
remaining step to close this deviation's tracking obligation.
