# ADR-0010: Frontend Monorepo Tooling

**Status:** Accepted
**Date:** 2026-08-14
**Deciders:** Platform maintainers

## Context

The constitution fixes React + TypeScript strict + Vite + TanStack Query in a single monorepo housing at minimum: the web SPA, the mobile-web SPA, the shared design system (ADR-0009), and the generated API client (ADR-0004). The Development Workflow section mandates trunk-based development with short-lived branches, small PRs, and frequent merges — which depends on CI staying fast as the repo grows.

## Decision

Use **Turborepo**.

## Options Considered

### Option A: Turborepo
| Dimension | Assessment |
|---|---|
| Complexity | Low — thin orchestration layer |
| Cost | Free; optional remote caching |
| Scalability | High |
| Team familiarity | Medium |

**Pros:** Minimal config (`turbo.json` task pipeline) — stays out of the way of Vite, since each package just runs its own `vite build`/`vite dev` and Turborepo orchestrates and caches those scripts; fast incremental build/test caching directly serves the "frequent merges to main" workflow by keeping CI fast; low learning curve on top of plain package-manager workspaces.
**Cons:** No built-in code generators for scaffolding new packages/components (manual or a custom Plop setup); less sophisticated dependency-graph visualization than Nx; "affected" test/lint detection is achievable via git-diff filtering but less polished than Nx's.

### Option B: Nx
| Dimension | Assessment |
|---|---|
| Complexity | Medium-High — executors, generators, project.json |
| Cost | Free core; Nx Cloud for remote caching |
| Scalability | High |
| Team familiarity | Low |

**Pros:** Powerful task-graph and caching; code generators for scaffolding; strong "affected" detection and dependency graph tooling; large plugin ecosystem including a Vite executor.
**Cons:** Heavier conceptual model imposed over the repo — more to learn than the team otherwise needs for a C#-first team with a smaller frontend surface; Nx's Vite-specific opinions occasionally lag the latest Vite ecosystem, adding friction against a Vite-first workflow the constitution already commits to.

### Option C: Plain pnpm workspaces (no orchestration tool)
| Dimension | Assessment |
|---|---|
| Complexity | Lowest |
| Cost | Free |
| Scalability | Degrades as package count grows |
| Team familiarity | High |

**Pros:** Simplest possible setup, nothing extra to learn or maintain.
**Cons:** No build caching — every package rebuilds fully in every CI run; no automatic "only test/build what changed," so CI time grows roughly linearly with package count, directly taxing the trunk-based, frequent-small-PR workflow the constitution mandates.

## Trade-off Analysis

Nx's heavier framework model is judged disproportionate to a monorepo of a handful of packages (two apps, a design system, a generated client) on a team that's C#-first elsewhere. Turborepo delivers the CI-speed benefit that actually matters for trunk-based development without imposing that overhead, at the cost of some scaffolding convenience Nx would provide.

## Consequences

- CI pipeline steps must be defined per package via `turbo.json` pipelines, with cache keys tied to source/lockfile hashes.
- If the frontend package count grows substantially (many more apps or shared libraries) or the team wants generators/stronger dependency graphing, revisit Nx as a documented amendment rather than bolting its concerns onto Turborepo piecemeal.

## Action Items

1. [ ] Initialize Turborepo in the frontend monorepo with pnpm workspaces
2. [ ] Define `turbo.json` pipelines for build/test/lint across web, mobile-web, design-system, and generated-client packages
3. [ ] Wire remote caching into the Jenkins pipeline if CI time warrants it
