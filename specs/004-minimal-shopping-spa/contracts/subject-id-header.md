# Contract: `X-Subject-Id` request header

**Feature**: 004-minimal-shopping-spa · **Status**: Design-time contract · **Date**: 2026-08-16

Companion to [`specs/003-stub-identity-tenant-context/contracts/tenant-id-header.md`](../../003-stub-identity-tenant-context/contracts/tenant-id-header.md).
Same mechanism, same guarantees, different claim — deliberately, so that Phase 3's real identity
server changes the resolution source and nothing else (research Decision 6).

## What it carries

The identifier of the **caller** — the shopper — for the current request. In Phase 1 that is the
fixed stub subject `phase1-stub-user`, issued by
`Gateway.Api.Identity.StubIdentityAuthenticationHandler` as a `ClaimTypes.NameIdentifier` claim
alongside the tenant claim it already issues.

## Why it exists

Spec FR-006 requires the shopper's basket to be resolved from the shopper's identity, "rather than
from an identifier the browser supplies or remembers". Today the gateway authenticates a principal
carrying a subject claim, but nothing carries that subject past the gateway — only the tenant
travels. The baskets service therefore has no way to know whose basket it is holding.

## Rules

| Rule | Detail |
|------|--------|
| **Resolved once** | Only the gateway sets it, from the authenticated principal's `ClaimTypes.NameIdentifier` claim. No hop below the gateway resolves, derives, or defaults it. |
| **Never trusted inbound** | Any value already on the request is overwritten. When no subject is resolved, the header is **removed**, not passed through — a caller-supplied subject is exactly the impersonation route this closes. |
| **Written onto the request** | Set on the request (not the response), so YARP's default forwarding carries it to the BFF, and the BFF's `TenantPropagationHandler` relays it onward to domain services. |
| **Read-only below the edge** | The BFF and every domain service read it into a request-scoped `CallerContext` via the shared `Tenancy` library's existing `AddTenancy()` / `UseTenancy()` calls. |
| **Absence is not an error at the middleware** | A missing header leaves the context Unresolved, exactly as tenancy does. Health probes legitimately arrive with no caller. The failure happens where it matters — at the routes that need a caller. |
| **Required by the caller-scoped routes** | `/baskets/current`, `/baskets/current/items`, `/baskets/current/clear`, and `/orders` require a resolved subject and fail loudly without one. |
| **Logged at every hop** | Pushed into the same logging scope the tenant uses, so a single request is traceable by shopper as well as by tenant (constitution Principle VII). |

## Header name

```
X-Subject-Id: phase1-stub-user
```

## Phase 3 replacement

When SCRUM-23 stands up the real identity server, the gateway's `AddScheme<...>` registration
becomes `AddJwtBearer(...)` against it. Provided the issued token carries the subject in
`ClaimTypes.NameIdentifier` (or a claim mapped to it), **nothing else in this contract changes** —
not the header, not the propagation, not the reading, not the routes that require it. That property
is the reason this is a header stamped from a claim rather than anything cleverer.

## Relationship to the tenant header

Both are stamped by the gateway, both ride the same path, both are read by the same library, and
both are removed rather than forwarded when unresolved. They answer different questions:
`X-Tenant-Id` decides *which data store* a request may reach; `X-Subject-Id` decides *whose rows*
inside it. A request needs both to touch a basket.
