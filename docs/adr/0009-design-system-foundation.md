# ADR-0009: Frontend Design-System Foundation

**Status:** Accepted
**Date:** 2026-08-14
**Deciders:** Platform maintainers

## Context

Principle IX requires one shared, versioned design-system package consumed by both apps, components accessible to WCAG 2.2 AA, and Principle VIII sets strict Core Web Vitals (LCP ≤ 2.5s, INP ≤ 200ms, CLS ≤ 0.1) and a per-route JS bundle budget. The platform serves two tenants, implying the design system needs to support divergent branding/theming, not just one visual identity.

## Decision

Use **Radix UI primitives + Tailwind CSS** as the design-system foundation, documented and previewed with **Storybook**.

## Options Considered

### Option A: Radix UI + Tailwind CSS
| Dimension | Assessment |
|---|---|
| Complexity | Medium — team builds/owns the styled component layer |
| Cost | Free (OSS) |
| Scalability | N/A |
| Team familiarity | Medium |

**Pros:** Radix primitives are unstyled but fully accessible by default (keyboard nav, ARIA roles, focus management built and tested upstream) — directly serves "Components MUST be accessible" with the least in-house accessibility engineering; Tailwind is compile-time with near-zero runtime style-injection cost, aligning with the strict bundle-budget and CWV targets; component *code* lives inside the design-system package rather than an opaque dependency, so per-tenant theming (color tokens, branding) is fully owned by the team.
**Cons:** More assembly work up front — the team builds the actual visual component layer (Button, Modal, etc.) on top of the primitives rather than getting them pre-styled.

### Option B: MUI (Material UI)
| Dimension | Assessment |
|---|---|
| Complexity | Low to start |
| Cost | Free core, paid tiers for advanced components |
| Scalability | N/A |
| Team familiarity | High |

**Pros:** Comprehensive, pre-styled components out of the box; mature theme-provider system supports per-tenant theme objects natively; fast initial velocity.
**Cons:** Runtime CSS-in-JS engine carries real bundle-size and runtime style-injection cost, working against the JS-bundle-budget-per-route and CWV targets in Principle VIII; Material Design visual language requires deep override work to avoid "looking like a Material app," fighting a distinctive multi-tenant commerce brand.

### Option C: Chakra UI
| Dimension | Assessment |
|---|---|
| Complexity | Low-Medium |
| Cost | Free (OSS) |
| Scalability | N/A |
| Team familiarity | Low |

**Pros:** Good accessibility defaults; simpler API surface than MUI; decent theming.
**Cons:** Similar runtime-styling-engine cost concerns to MUI (improved in v3 but still heavier than compile-time Tailwind); smaller ecosystem.

## Trade-off Analysis

The deciding factors are Principle VIII's hard performance budgets and the two-tenant branding requirement — both push toward a compile-time, fully-owned styling approach over a pre-styled runtime component kit. Radix's accessibility guarantees reduce the team's own a11y testing burden more than any of the alternatives, which matters given WCAG 2.2 AA is a hard requirement, not a target.

## Consequences

- The team owns and must maintain the full visual component layer, not just consume a vendor's components — more design-system engineering investment up front.
- Storybook is added to the monorepo for documentation/preview, with the `storybook-addon-a11y` plugin used to catch WCAG violations directly in CI/PR review.

## Action Items

1. [ ] Scaffold the design-system package with Radix primitives + Tailwind
2. [ ] Add Storybook with the accessibility addon, wired into PR checks
3. [ ] Define the per-tenant theming/token mechanism
