# Phase 1 Data Model: Stub Identity with Resolved Tenant Context

Nothing here is persisted by this feature itself. The entities below are either an in-flight, request-scoped value (Tenant Context) or a fixed, hardcoded configuration value (Stub Identity) — see `spec.md` Key Entities.

## Tenant Context (request-scoped, in-flight)

The resolved tenant identifier, carried from the gateway through to whichever service ultimately touches persistence.

| Field | Description | Notes |
|---|---|---|
| TenantId | The resolved tenant's identifier | Phase 1: always the one hardcoded value (spec FR-008). Never absent once resolved — either it's set, or `RequireTenantId()` throws; there is no "empty but present" state. |
| ResolvedAt | Which hop resolved it | Always the gateway (constitution Principle V: resolved once at the edge). Every other hop only reads what the gateway resolved. |

**State machine**: A `TenantContext` has exactly two states for a given request, at any single hop:

1. **Unresolved** — `TenantContextMiddleware` found no (or an empty) `X-Tenant-Id` header. Calling `RequireTenantId()` in this state throws `MissingTenantContextException` (spec FR-004/FR-005; Test Scenario 2).
2. **Resolved** — the header was present and non-empty; `RequireTenantId()` returns it.

There is no third "resolved to a default" state — that state is the one this feature exists to make impossible (spec Edge Cases).

**Validation rules**:
- The gateway's own resolution (Decision 1/2, `research.md`) always produces a non-empty tenant claim for every request in Phase 1 — there is no scenario where the gateway itself forwards a request unresolved. Downstream services can still observe "unresolved" only if a request reaches them by some other path than the gateway (spec Edge Cases: "the gateway was bypassed").
- A tenant identifier a service observes that doesn't match what the gateway would have resolved MUST be treated as unresolved, not as a different valid tenant (spec Edge Cases — propagation corruption).

## Stub Identity (gateway configuration, fixed)

The single fake authenticated identity Phase 1 always resolves, in place of a real identity server.

| Field | Description | Notes |
|---|---|---|
| Scheme | The registered authentication scheme name | e.g. `StubIdentity` — swapped for a real scheme (e.g. `Bearer`/JWT) in Phase 3 without touching anything downstream of the claim (research.md Decision 1). |
| SubjectId | The fake user's identifier | Fixed for Phase 1; not exercised by any acceptance scenario beyond existing, so no further shape is specified here. |
| TenantClaim | The claim carrying the tenant identifier | What `TenantHeaderPropagationMiddleware` reads to produce the `X-Tenant-Id` header (research.md Decision 2). |

**Validation rules**: The stub identity always authenticates successfully and always carries a `TenantClaim` — Phase 1 has no "authentication failed" path (spec Assumptions: no login flow, no credential entry).

## X-Tenant-Id Header Contract

The wire-level contract this feature adds between hops — see `contracts/tenant-id-header.md` for the full contract.

| Field | Description |
|---|---|
| Header name | `X-Tenant-Id` |
| Set by | The gateway only (`TenantHeaderPropagationMiddleware`) and the BFF's outbound `TenantPropagationHandler` (relaying, not resolving) |
| Trusted by | `TenantContextMiddleware` in the BFF and each domain service — read-only, never re-derived |
| Required | On every request past the gateway; a request arriving at a domain service without it (or with an empty value) resolves to the Unresolved state above |

## Relationships

```text
Gateway (resolves)
  └─ StubIdentityAuthenticationHandler → ClaimsPrincipal (TenantClaim)
       └─ TenantHeaderPropagationMiddleware → sets X-Tenant-Id on the request
            └─ YARP forwards the request (headers included, by default) →

BFF (relays)
  └─ TenantContextMiddleware reads X-Tenant-Id → TenantContext (Resolved)
       └─ TenantPropagationHandler (per outbound call) → sets X-Tenant-Id on the downstream request →

Domain service — products | baskets | orders | parties (enforces)
  └─ TenantContextMiddleware reads X-Tenant-Id → TenantContext
       └─ AddDbContext<T>'s (serviceProvider, options) factory calls TenantContext.RequireTenantId()
            ├─ Resolved  → modelBuilder.HasDefaultSchema(tenantId); connection proceeds
            └─ Unresolved → MissingTenantContextException; no connection is ever opened
```

## State Transitions

None beyond the two-state Tenant Context above. Every entity here is either static Phase 1 configuration (Stub Identity) or a value scoped to a single request's lifetime (Tenant Context) — nothing in this feature has a persisted lifecycle of its own.
