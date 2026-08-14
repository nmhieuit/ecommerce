# ADR-0002: API Gateway Implementation

**Status:** Accepted
**Date:** 2026-08-14
**Deciders:** Platform maintainers

## Context

The constitution fixes the edge chain as load balancer → API gateway → BFF, and requires the gateway to independently validate tokens (Principle VI — "the gateway is not a trust boundary services may rely on"). The platform is self-hosted on K8s via Ansible, entirely C#/.NET elsewhere in the stack.

## Decision

Use **YARP** (Yet Another Reverse Proxy), hosted as an ASP.NET Core application.

## Options Considered

### Option A: YARP
| Dimension | Assessment |
|---|---|
| Complexity | Medium — code-first, less "batteries included" than Kong |
| Cost | Free, Microsoft-maintained |
| Scalability | High — stateless ASP.NET Core |
| Team familiarity | High |

**Pros:** Runs as a normal ASP.NET Core app — same container/pipeline/`ServiceDefaults` telemetry as everything else; token validation is standard ASP.NET Core auth middleware, so gateway-level and service-level validation use identical code patterns; actively maintained by Microsoft and used in production inside Azure itself; deny-by-default routing policy can be expressed in the same C# policy model services use.
**Cons:** No plugin marketplace — rate limiting, request transforms, etc. are hand-built as YARP middleware rather than configured from a catalog; less "gateway feature checklist" out of the box than Kong.

### Option B: Ocelot
| Dimension | Assessment |
|---|---|
| Complexity | Low — config-driven |
| Cost | Free |
| Scalability | Medium-High |
| Team familiarity | High |

**Pros:** .NET-native like YARP; config-file-driven routing is quick to stand up; historically the default choice for .NET API gateways.
**Cons:** Maintenance cadence has slowed relative to YARP, which now has Microsoft's backing and is the more actively evolving option; fewer advanced load-balancing/health-check features than YARP.

### Option C: Kong
| Dimension | Assessment |
|---|---|
| Complexity | Medium — DB or DB-less mode, plugin config |
| Cost | Free (OSS) / commercial tier available |
| Scalability | High |
| Team familiarity | Low |

**Pros:** Large plugin ecosystem (rate limiting, auth, transforms) available via configuration rather than code; proven at scale across many stacks.
**Cons:** Lua/OpenResty-based — the same "foreign runtime breaks `ServiceDefaults` observability" problem as Keycloak in ADR-0001; adds an admin API and (in DB mode) a datastore dependency not otherwise in this footprint; team has no existing Kong expertise.

## Trade-off Analysis

Same reasoning as the identity server decision: staying inside the C#/.NET fleet keeps the gateway observable through the mandated shared telemetry component and keeps token-validation code consistent between the gateway and every service behind it. YARP is chosen over Ocelot specifically for its more active maintenance trajectory and Microsoft backing.

## Consequences

- Gateway-level features not built into YARP (advanced rate limiting, request/response transforms beyond basic ones) are the team's own middleware to write and test.
- The gateway is a first-class deployable service in the same CI/CD pipeline, not a separately-operated appliance.

## Action Items

1. [ ] Scaffold the YARP gateway project with `ServiceDefaults` wired in
2. [ ] Implement JWT bearer validation matching the token issued by ADR-0001's identity server
3. [ ] Define YARP route/cluster config for the BFF
