# ADR-0008: Feature Toggle System

**Status:** Accepted
**Date:** 2026-08-14
**Deciders:** Platform maintainers

## Context

Principle X requires every non-trivial change to ship behind a toggle with a named owner and a removal date recorded at creation, and requires rollback without a code change or redeploy. This is a fairly narrow requirement (existence, ownership, expiry, fast flip) rather than a request for sophisticated targeting or experimentation.

## Decision

Use **Unleash**, self-hosted.

## Options Considered

### Option A: Unleash (self-hosted)
| Dimension | Assessment |
|---|---|
| Complexity | Medium — one more Postgres-backed service to run |
| Cost | Free (OSS); hosted option available later if desired |
| Scalability | High |
| Team familiarity | Low initially |

**Pros:** Self-hostable, fitting the Ansible/K8s deployment model like everything else; ready-made admin UI lets someone flip a flag without a deploy or a database script — directly serving Principle X's "rollback without a redeploy" goal; official .NET and React SDKs; owner/tag metadata on flags can be extended with a CI check that fails the build if a toggle is past its recorded removal date, automating the "stale toggles are technical debt" rule.
**Cons:** A new stateful service (Postgres-backed) to operate; UI/targeting sophistication is good but not as polished as LaunchDarkly's for advanced scenarios — not needed here.

### Option B: LaunchDarkly
| Dimension | Assessment |
|---|---|
| Complexity | Low (fully managed) |
| Cost | Recurring SaaS cost, scales with usage |
| Scalability | High (vendor-managed) |
| Team familiarity | Medium |

**Pros:** Best-in-class targeting/experimentation UI; zero operational burden.
**Cons:** Recurring external SaaS cost and dependency, inconsistent with the platform's otherwise self-hosted posture; per-seat/MAU pricing is a real, growing line item for a feature the constitution only requires in a narrow form.

### Option C: Flagsmith (self-hosted)
| Dimension | Assessment |
|---|---|
| Complexity | Medium |
| Cost | Free (OSS) |
| Scalability | High |
| Team familiarity | Low |

**Pros:** Similar self-hostable, open-source profile to Unleash.
**Cons:** Smaller community and ecosystem than Unleash; .NET SDK is less mature and battle-tested.

### Option D: Homegrown flags table
| Dimension | Assessment |
|---|---|
| Complexity | Low to start, grows over time |
| Cost | Engineering time only |
| Scalability | High |
| Team familiarity | High |

**Pros:** Zero third-party infrastructure; owner/removal-date are just table columns, trivially enforceable by a CI lint against the SonarQube/Jenkins gate; full control, matching the platform's "everything is C#" philosophy.
**Cons:** No admin UI out of the box — flipping a flag safely without a deploy means building one, which is exactly the tooling Unleash already provides; no percentage-rollout or targeting engine if the platform needs gradual rollout later.

## Trade-off Analysis

LaunchDarkly is rejected mainly for introducing the platform's first recurring external SaaS dependency where a self-hosted option fully covers the actual requirement. Between Unleash and a homegrown table, Unleash wins primarily on the ready-made admin UI — the whole point of Principle X is fast, safe rollback without a deploy, and a real UI for non-engineers to flip a flag serves that better than requiring a database script or an internal tool the team would otherwise have to build.

## Consequences

- Unleash becomes another service with its own availability requirements — a toggle-flip during an incident depends on Unleash being reachable.
- CI must be extended to query Unleash (or a synced export) and fail builds on toggles past their removal date.

## Action Items

1. [ ] Deploy self-hosted Unleash with Postgres backing store
2. [ ] Integrate .NET and React SDKs into `ServiceDefaults` and the frontend shared package respectively
3. [ ] Build the CI check that fails on toggles past their recorded removal date
