# ADR-0004: OpenAPI-to-TypeScript Client Codegen

**Status:** Accepted
**Date:** 2026-08-14
**Deciders:** Platform maintainers

## Context

Principle II requires generated, never hand-written, API clients. Principle IX requires server state to be managed with TanStack Query, and calls "duplicated UI primitives or hand-written API calls across apps" a violation.

## Decision

Use **Orval**.

## Options Considered

### Option A: Orval
| Dimension | Assessment |
|---|---|
| Complexity | Low-Medium |
| Cost | Free (OSS) |
| Scalability | N/A |
| Team familiarity | Medium |

**Pros:** The only option here that generates TanStack Query hooks (`useQuery`/`useMutation`) directly from the OpenAPI spec — zero hand-written glue between the generated client and the mandated data-fetching layer, which is exactly what Principle IX is trying to eliminate.
**Cons:** Smaller maintainer community than openapi-typescript; opinionated output tightly coupled to TanStack Query (acceptable since that coupling is already the constitutional choice); complex/polymorphic OpenAPI schemas can need extra config.

### Option B: openapi-typescript (+ openapi-fetch)
| Dimension | Assessment |
|---|---|
| Complexity | Low |
| Cost | Free (OSS) |
| Scalability | N/A |
| Team familiarity | Medium |

**Pros:** Extremely popular, minimal-magic, fully-typed output; very well maintained.
**Cons:** Produces types + a thin fetch wrapper only — no TanStack Query hooks, so every call site needs a hand-written `useQuery` wrapper, reintroducing the exact hand-written-API-call pattern Principle IX prohibits.

### Option C: Kiota
| Dimension | Assessment |
|---|---|
| Complexity | Medium |
| Cost | Free (Microsoft) |
| Scalability | N/A |
| Team familiarity | Low |

**Pros:** Microsoft-maintained, multi-language (could generate both the TS client and any future non-BFF C# client from one spec); strong long-term support signal.
**Cons:** No TanStack Query awareness; generated builder-pattern client style is more verbose and less idiomatic in React than Orval's hooks; limited adoption in the React ecosystem.

### Option D: NSwag
| Dimension | Assessment |
|---|---|
| Complexity | Medium |
| Cost | Free (OSS) |
| Scalability | N/A |
| Team familiarity | Medium |

**Pros:** Mature, historically popular in .NET shops for full-stack codegen.
**Cons:** TS output idioms lag behind React-focused tools; no native TanStack Query hook generation; project momentum has slowed relative to Orval.

## Trade-off Analysis

The decisive factor is Principle IX's TanStack Query mandate — Orval is the only candidate that closes the gap between "generated client" and "TanStack Query hook" without hand-written wrapper code at every call site, which is precisely the class of duplication the constitution flags as a violation.

## Consequences

- Frontend data-fetching code is generated on every OpenAPI spec change — CI must regenerate and fail the build on drift between the spec and the checked-in generated output.
- Both web and mobile-web apps consume the exact same generated hooks package, enforcing the "one generated API client" rule structurally, not by convention.

## Action Items

1. [ ] Add Orval to the frontend monorepo, configured against the BFF's OpenAPI output
2. [ ] Add a CI check that fails if generated output is stale relative to the OpenAPI spec
