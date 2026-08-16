# Contract: `X-Tenant-Id` Header

This feature's only externally-observable interface: an internal, service-to-service header contract, not a client-facing API (there are no new HTTP routes in this feature). Every hop from the gateway onward participates in this contract.

## Header

| | |
|---|---|
| Name | `X-Tenant-Id` |
| Direction | Request only (unlike `X-Correlation-Id`, this is never echoed on the response — a tenant identifier is not diagnostic information a client needs back) |
| Value | The resolved tenant's identifier, as a non-empty string. Phase 1: always the single hardcoded value (spec FR-008). |
| Cardinality | Exactly one value per request, or absent |

## Producers

| Hop | Behavior |
|---|---|
| Gateway (`TenantHeaderPropagationMiddleware`) | **Resolves and sets.** Always overwrites any inbound value with the tenant claim from the authenticated (stub) identity — a client-supplied `X-Tenant-Id` is never trusted, per constitution Principle V ("never inferred from user-supplied request bodies or query parameters"; a header a caller controls is exactly that risk). |
| BFF (`TenantPropagationHandler`, on each outbound downstream call) | **Relays, does not resolve.** Copies the value already present in the BFF's own inbound `TenantContext`. If the BFF's own `TenantContext` is Unresolved (spec Edge Cases — gateway bypassed), no header is set on the outbound call, and the downstream service observes Unresolved too — the failure propagates, it is not masked. |

## Consumers

| Hop | Behavior |
|---|---|
| BFF, all four domain services (`TenantContextMiddleware`) | Reads the header into a request-scoped `TenantContext`. Empty or missing → Unresolved (data-model.md). Never falls back to a default. |
| Each domain service's `AddDbContext<T>` registration | Calls `TenantContext.RequireTenantId()`. Resolved → proceeds with that tenant's schema. Unresolved → throws `MissingTenantContextException` before any connection is opened (spec FR-004/FR-005). |

## Failure Modes

| Scenario | Behavior |
|---|---|
| Header absent at a domain service (gateway/BFF bypassed) | `TenantContext` is Unresolved; any persistence attempt throws `MissingTenantContextException` (spec Test Scenario 2). |
| Header present but empty string | Treated identically to absent — Unresolved (data-model.md validation rules). |
| Header value doesn't match what the gateway would have resolved (corrupted/overwritten mid-flight) | Treated as Unresolved, not as a different valid tenant (spec Edge Cases) — this feature has no mechanism to validate a tenant id's authenticity past the gateway in Phase 1, so an unverifiable value is handled as no value. |

## Stability

This is an internal contract between services in the same deployment, not a published client-facing contract under constitution Principle II's versioning rules (no external consumer, no breaking-change deprecation window applies). It changes when Phase 3 (SCRUM-23) replaces the stub identity with real token claims — expected to be additive (a real, verifiable tenant claim replaces a hardcoded one) rather than a change to this header's name, direction, or consumers.
