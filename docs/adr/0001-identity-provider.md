# ADR-0001: Identity Provider Product

**Status:** Accepted
**Date:** 2026-08-14
**Deciders:** Platform maintainers

## Context

The constitution (Principle VI, Technology Constraints) fixes the *pattern* — a central identity server issues tokens, validated independently at the gateway and at every service — but not the product. Constraints that bear on this choice:

- Principle VII requires every service to emit telemetry through a shared C# `ServiceDefaults` component, "not configured per service by hand."
- Principle V requires tenant identity to be resolved at the edge and carried as an explicit claim through every hop.
- The platform is self-hosted on Kubernetes provisioned by Ansible — not committed to any single cloud provider.
- Every other component in the fleet is a C#/.NET container deployed identically through the same Jenkins pipeline.

## Decision

Use **Duende IdentityServer**, deployed as an ASP.NET Core service in the same container/pipeline/Ansible model as every other service.

## Options Considered

### Option A: Duende IdentityServer
| Dimension | Assessment |
|---|---|
| Complexity | Medium — code-first, but multi-tenancy (client/tenant mapping, custom claims) must be built |
| Cost | Commercial license, revenue-tier pricing |
| Scalability | High — stateless ASP.NET Core, scales like every other service |
| Team familiarity | High — same language, same deployment model as the rest of the platform |

**Pros:** Deploys identically to every other service (same Jenkins pipeline, same Ansible playbooks, same K8s manifests); consumes the shared `ServiceDefaults` telemetry component natively, so it doesn't become the one system with hand-rolled observability; tenant claim issuance (`IProfileService`) and per-tenant client config are ordinary C# extensibility points the team already knows; no new runtime (JVM, etc.) enters the fleet.
**Cons:** Commercial licensing cost that scales with company revenue; no built-in "realm" concept for tenant isolation — multi-tenancy must be modeled explicitly in code/config rather than configured out of the box.

### Option B: Keycloak
| Dimension | Assessment |
|---|---|
| Complexity | Medium — mature admin UI, but Java/JVM operational surface |
| Cost | Free/open-source |
| Scalability | High — proven at large scale |
| Team familiarity | Low — Java-based, outside the platform's C#-only skillset |

**Pros:** Free; battle-tested at scale; realm-per-tenant is a first-class, admin-UI-configurable multi-tenancy model that maps directly onto Principle V; broad protocol support (OIDC, SAML) if a tenant ever needs enterprise SSO.
**Cons:** The only non-.NET runtime in the fleet — it cannot consume the shared C# `ServiceDefaults` component, so it needs bespoke observability wiring, in tension with Principle VII's "not configured per service by hand"; introduces JVM operational knowledge (heap tuning, GC, upgrade cadence) the team doesn't otherwise need; custom claim logic beyond what protocol mappers support requires a Java SPI plugin.

### Option C: Azure Entra External ID / Auth0 / Okta (managed CIAM)
| Dimension | Assessment |
|---|---|
| Complexity | Low — mostly configuration |
| Cost | Recurring SaaS/MAU-based cost |
| Scalability | High (vendor-managed) |
| Team familiarity | Medium |

**Pros:** Least operational burden — no identity infrastructure to run at all; vendor handles uptime/patching/scaling.
**Cons:** Introduces an external dependency and recurring cost the rest of this self-hosted, Ansible/K8s-provisioned platform doesn't otherwise have; Azure Entra specifically ties the platform to a cloud provider it isn't otherwise committed to; per-tenant custom claim and branding flexibility is more constrained than a self-hosted, code-first option.

## Trade-off Analysis

The deciding factor is operational consistency, not feature checklists: every other piece of this platform — services, gateway, BFF — is a C# container observed through one shared telemetry component and shipped through one pipeline. Keycloak's Java runtime silently breaks that consistency at the one layer (identity) that every request passes through. Duende's licensing cost is real, but it is the only option that keeps identity inside the platform's existing skillset, deployment model, and observability guarantee.

## Consequences

- Identity server code is owned and maintained by the platform team, not a vendor — upgrades, CVEs, and OIDC spec compliance are the team's responsibility.
- Multi-tenancy (client-per-tenant, tenant claim issuance) must be explicitly designed and reviewed — there's no realm abstraction to lean on.
- A commercial license must be budgeted and tracked; revisit if licensing cost becomes disproportionate to platform revenue.

## Action Items

1. [ ] Procure Duende IdentityServer license appropriate to current revenue tier
2. [X] Design the tenant → client/claim mapping model and get it reviewed as a tenant-resolution-path change (constitution requires owning-service-maintainer review for this) — done in [014-identity-server-auth/data-model.md](../../specs/014-identity-server-auth/data-model.md): `TenantClaimsProfileService` issues `tenant_id` from `ApplicationUser.TenantId` (one tenant per Identity User), and `Config.cs` maps each client to its `ecommerce-api` scope
3. [ ] Wire `ServiceDefaults` telemetry into the identity server project
