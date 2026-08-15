# Specification Quality Checklist: Scaffold Parties/Products/Baskets/Orders Service Shells

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-14
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- Source ticket (Jira SCRUM-11) already had well-defined acceptance criteria and test scenarios, which resolved cleanly into user stories and functional requirements without needing any [NEEDS CLARIFICATION] markers.
- All items passed on the first validation pass — no iteration was required.

## Post-implementation re-verification (T051)

Re-checked after all six phases. **No specification defect was found** — every item above still
holds, and nothing in spec.md needed changing. What follows records what implementation revealed,
per T051's "update if anything changed during implementation".

### Success criteria, as measured

| Criterion | Status | Evidence |
|---|---|---|
| SC-001 — fresh clone to a running, healthy service in under 5 minutes | **MET** | Measured bring-up per service: parties 30.8s, products 41.0s, baskets 140.9s, orders 41.7s (T050 walkthrough). Restore + build adds roughly another minute on a warm NuGet cache. |
| SC-002 — all four report healthy independently, 100% of the time | **MET** | Each service was exercised with **only its own** database container running; all four returned `200` from `/health/ready` with `"self-database": "Healthy"`. |
| SC-003 — zero cross-service data access, verified by a repeatable check | **MET** | `tests/CrossServiceIsolation.Tests` (6 tests). Mutation-verified: injecting a real `parties → orders` connection string turned the suite red. |
| SC-004 — all code for one capability findable in one place | **MET** | `tests/StructureConventionTests` (9 tests). Mutation-verified: creating a real `Parties.Api/Controllers/` folder turned the suite red. |

### Two defects the walkthrough exposed, both fixed

Neither was a specification problem; both were implementation gaps that only running the quickstart
end-to-end could surface.

1. **The service databases were never created.** A fresh SQL Server container ships with only
   `master`/`tempdb`/`model`/`msdb`, so every service authenticated successfully and then failed
   readiness with error 4060, "Cannot open database". SC-001 and SC-002 were *not* met before this
   was fixed. Resolved by adding a per-service `*-db-init` service to `docker-compose.deps.yml`
   that waits for its database to pass a healthcheck and then creates the empty database.
   `quickstart.md` step 1 now names that init service.
2. **Readiness took 11 seconds to fail.** SqlClient's idle-connection-resiliency retry
   (`ConnectRetryCount=1`, `ConnectRetryInterval=10`) is redundant behind a Kubernetes probe that
   retries on its own schedule. Resolved with `Connect Timeout=3;ConnectRetryCount=0` in the
   development connection strings; the failure path now completes in ~3.2s.

### Open observation — SLO measurement is declared, not yet verified

The health-endpoint SLO (p95 ≤ 150 ms) is now *declared* in each service's `service-manifest.yaml`
(T046–T049). It is **not** verified by the T050 walkthrough, and should not be read as such: warm
`/health/ready` medians were 46–139 ms, but every 15-sample run also contained one outlier between
684 ms and 3.6 s. With n=15 the "p95" statistic degenerates to the maximum, so those figures are not
a meaningful p95, and a Docker Desktop SQL Server on a developer laptop is not a representative
environment. Constitution Principle VIII specifies SLOs are measured continuously from production
telemetry, which is the correct instrument here. Worth revisiting once that telemetry exists.
