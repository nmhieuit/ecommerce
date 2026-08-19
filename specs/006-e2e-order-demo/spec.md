# Feature Specification: End-to-End Order Demo — Phase 1 Exit Proof

**Feature Branch**: `006-e2e-order-demo`

**Created**: 2026-08-19

**Status**: Draft

**Input**: User description: "https://nmhieuit.atlassian.net/browse/SCRUM-16 — [WALK-1] Demo: place one order end to end. As the Product Owner, I want to watch one order get placed end to end on the deployed skeleton so that Phase 1 has an unambiguous, demoable definition of done."

## Clarifications

### Session 2026-08-19

- Q: When the demo checks that an order belongs to the right tenant, should the order itself carry a tenant identifier that can be read back from the orders service, or should the check instead confirm the record sits in the correct tenant's separate store? (FR-005) → A: Persist a tenant identifier on the order and include it in the order read response (Option A), so the demo shows it directly. The tenant-scoped store remains the enforcement boundary; the stored identifier is evidence, not enforcement.
- Q: What does "from a clean state" mean when the demo is re-run to prove it is repeatable — wiping all data and starting the platform fresh, or just clearing the shopper's basket and running again? (FR-007, FR-017, SC-003) → A: Routine repeat runs start from a **clean basket** only (Option C); prior order records may accumulate. The procedure is additionally validated once from a **cold start** (no stored data at all) to prove first-time setup and catalogue seeding.
- Q: Should the demo recording be committed into the repository as a file anyone can open, or generated on demand by re-running the automated demo, with only the written walkthrough committed? (FR-013, FR-014) → A: Commit the written walkthrough plus lightweight stills of each key step (Option C). The full video is produced by the automated run and attached to the Jira story (SCRUM-16) rather than committed to the repository.
- Q: When the demo narrates the request path — storefront → gateway → BFF → services — does the run need to produce actual per-hop evidence that a request traversed each component, or is a documented, accurate description of the path enough? (FR-011) → A: Documented description plus evidence that each named component actually served traffic during the run (Option B). Following a single request across hops by a shared identifier is deferred to Phase 3 (SCRUM-25, SCRUM-26).
- Q: Once the demo is automated, should it run on every change as a check that blocks merging when it fails, or only when someone chooses to run it? (FR-007) → A: On-demand only in this story (Option A) — one documented command runs it locally. Wiring it into the build pipeline as a gate stays with the Phase 2 story that owns the build gate (SCRUM-22).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Watch one order placed end to end, live (Priority: P1)

The Product Owner starts the platform, begins from a clean basket, opens the storefront, browses
the product list, adds a product to the basket, checks out, and sees an order confirmation carrying
an order reference and a total. Nothing in that sequence requires a developer to intervene: no
manual data seeding, no restarting a service that came up in the wrong order, no console command
between steps. When the confirmation appears, an order record exists in the orders service and can
be read back.

**Why this priority**: This is the story. Phase 1 claims a walking skeleton exists; the only
unambiguous evidence is a person watching the flow complete without help. Every other story here
strengthens that evidence but is worthless without it.

**Independent Test**: Start the stack, empty the basket, follow the documented demo sequence in a
browser, and confirm the confirmation screen appears and the order is readable from the orders
service. Delivers the full Phase 1 claim on its own.

**Acceptance Scenarios**:

1. **Given** the platform is running and the basket is empty, **When** the Product Owner follows the
   documented demo sequence through browse → basket → checkout, **Then** a confirmation showing an
   order reference and total is displayed, and an order record with that reference is persisted in
   the orders service.
2. **Given** the demo has just completed, **When** the persisted order is read back by its
   reference, **Then** its total matches the total shown on the confirmation screen.
3. **Given** the demo is being performed, **When** each step is taken, **Then** no step requires a
   command, restart, data fix, or configuration change outside the documented sequence.
4. **Given** the demo has completed once, **When** it is performed a second time from a clean
   basket, **Then** it completes identically and produces a second, distinct order record.

---

### User Story 2 - Confirm the order belongs to the right tenant (Priority: P2)

After the order is placed, the Product Owner (or a reviewer) can establish, by reading the
persisted order through a documented step, that the order is attributed to the tenant that was
resolved for the request that placed it — not to a default, a blank, or another tenant's data.

**Why this priority**: Tenant isolation is a security boundary, and Phase 1 deliberately resolved
real tenant context rather than deferring it. A demo that proves the flow works but leaves tenant
attribution unobservable proves the cheaper half of the claim. It ranks below P1 because the flow
existing at all is the precondition for checking who it belongs to.

**Independent Test**: Place one order, then follow the documented verification step and confirm the
result names the same tenant that the request resolved. Testable without any of the recording or
artifact work in US3.

**Acceptance Scenarios**:

1. **Given** an order placed under a resolved tenant context, **When** the persisted order is
   inspected through the documented verification step, **Then** the tenant it is attributed to is
   present, non-empty, and equal to the tenant resolved for the placing request.
2. **Given** an attempt to place an order reaches the orders service with no tenant resolved,
   **When** the request is processed, **Then** it fails loudly and no order record is created.
3. **Given** the verification step is followed by someone who did not build the system, **When**
   they run it, **Then** the tenant attribution is legible from the output without reading source
   code.

---

### User Story 3 - Leave behind a reference artifact for Phase 1 exit (Priority: P3)

The demo run produces durable evidence that outlives the meeting: a written walkthrough naming each
hop the request takes (storefront → gateway → BFF → services), stills captured at each key step,
and a statement of which Phase 1 exit criteria the run demonstrates. The walkthrough and stills are
committed and discoverable from the repository documentation, so a reviewer joining later can see
what "Phase 1 done" meant without asking anyone to re-run it. The full video the run produces is
attached to the Jira story rather than committed.

**Why this priority**: The artifact is what converts a one-time observation into a reusable
definition of done, but it is a record of the first two stories rather than a capability of its
own. If it slipped, Phase 1 would still be provably complete — just harder to re-litigate later.

**Independent Test**: Perform the demo, then confirm the written walkthrough and its stills are
committed, are reachable from the repository documentation, and identify the exit criteria they
evidence.

**Acceptance Scenarios**:

1. **Given** a completed demo run, **When** the committed walkthrough and stills are opened,
   **Then** together they show the full browse → basket → checkout → confirmation flow and name
   each hop the request traverses.
2. **Given** a reviewer who was not present at the demo, **When** they open the repository
   documentation, **Then** they can reach the walkthrough and its stills without being told where
   to look, and can tell from it where the full video recording lives.
3. **Given** the written walkthrough, **When** it is read, **Then** it states explicitly which
   Phase 1 exit criteria the run satisfies and which remain out of scope for Phase 1.

---

### Edge Cases

- What happens when the demo is run against a stack that is up but not yet ready (a dependency
  still starting)? The demo sequence must not begin until the platform reports itself usable, so a
  premature start surfaces as "not ready yet" rather than a failed step mid-flow.
- What happens when a previous run left items in the basket? The documented sequence must begin
  from a known basket state so the second run is not skewed by the first.
- What happens when the checkout action is triggered twice in quick succession? Exactly one order
  is created; the demo must not produce two records from one intended purchase.
- What happens when checkout is attempted with an empty basket? The flow is refused with a
  comprehensible message and no order record is created.
- What happens when a downstream service is unavailable at checkout? The failure is visible and
  comprehensible to the person running the demo, and no partial or orphaned order record remains.
- What happens when the demo is run on a machine that has never run it before? The documented
  sequence names every prerequisite, so a first-time runner reaches the confirmation screen without
  outside help.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The platform MUST support completing the browse → add to basket → checkout →
  confirmation flow through the storefront with no intervention outside the documented demo
  sequence.
- **FR-002**: The confirmation shown at the end of the flow MUST display an order reference and an
  order total.
- **FR-003**: Completing the flow MUST result in exactly one persisted order record in the orders
  service per intended purchase, retrievable by the reference shown on the confirmation.
- **FR-004**: The persisted order's total MUST equal the total displayed on the confirmation.
- **FR-005**: The persisted order MUST carry the identifier of the tenant resolved for the request
  that placed it, stored alongside the order rather than inferred at read time.
- **FR-005a**: Reading an order back from the orders service MUST return that stored tenant
  identifier, so tenant attribution is visible without inspecting the database or the source code.
- **FR-005b**: Storing the tenant identifier on the order MUST NOT weaken or replace the existing
  tenant-scoped store separation — that separation remains the enforcement boundary, and the stored
  identifier is evidence of correct attribution.
- **FR-006**: An order placement request that reaches the orders service without a resolved tenant
  context MUST fail and MUST NOT create an order record.
- **FR-007**: The demo MUST be repeatable: running the documented sequence twice from a clean
  basket MUST produce the same observable outcome both times and MUST create two distinct order
  records. Order records left by earlier runs MUST NOT change the outcome.
- **FR-007a**: The documented procedure MUST also be validated once from a cold start — no stored
  data at all — reaching the confirmation screen without a manual seeding or repair step.
- **FR-007b**: The documented procedure MUST state which starting state each step assumes, and
  MUST name how the clean-basket state is reached so the runner is not left to guess.
- **FR-007c**: The automated demo MUST be runnable by a single documented command, so re-running it
  costs one action rather than a retraced sequence of clicks.
- **FR-007d**: This feature MUST NOT make the automated demo a merge-blocking or scheduled pipeline
  check; when and how it gates the build belongs to the story that owns the build gate.
- **FR-008**: The system MUST make the product catalogue available at demo time without a manual
  seeding step by the person running the demo.
- **FR-009**: The platform MUST signal when it is ready for the demo to begin, so the sequence is
  not started against a partially available stack.
- **FR-010**: The demo sequence MUST be documented as an ordered, step-by-step procedure that names
  its prerequisites, the starting state, each action, and the expected observation at each step.
- **FR-011**: The documentation MUST name each hop a request traverses on the checkout path
  (storefront → gateway → BFF → downstream services) so the narration during the demo is factual
  rather than improvised.
- **FR-011a**: The demo run MUST produce evidence that every component named in FR-011 actually
  served traffic during that run, so the narration is checkable rather than asserted.
- **FR-011b**: That evidence MUST NOT require following one request across components by a shared
  identifier — end-to-end request correlation is out of scope here and belongs to the observability
  work of a later phase.
- **FR-012**: A verification step MUST be documented that reads the placed order back from the
  orders service and shows its reference, total, and tenant attribution.
- **FR-013**: The automated demo run MUST produce a video recording of the full flow that can be
  replayed without re-running the platform. The video MUST NOT be committed to the repository; it
  is attached to the Jira story (SCRUM-16).
- **FR-013a**: The demo run MUST capture a still image at each key step of the flow, and those
  stills MUST be committed alongside the written walkthrough so the committed evidence stands on
  its own without the video.
- **FR-014**: The written walkthrough and its stills MUST be discoverable from the repository’s
  existing documentation entry points, and the walkthrough MUST state where the full video
  recording is held.
- **FR-015**: The written walkthrough MUST state which Phase 1 exit criteria the run evidences and
  which concerns are explicitly deferred to later phases.
- **FR-016**: Failures encountered during the demo (unavailable dependency, refused checkout) MUST
  surface a comprehensible message to the person running the demo rather than a blank or silent
  failure.
- **FR-017**: The demo MUST leave no partial state that changes the outcome of the next run — a
  subsequent run from a clean basket MUST behave identically.

### Key Entities

- **Order record**: The persisted result of one completed checkout. Carries a reference by which it
  can be read back, when it was placed, what it came to, and the identifier of the tenant it
  belongs to — the last being new to this feature.
- **Tenant context**: The tenant resolved for a request at the edge and carried through every hop;
  the value the persisted order's attribution is checked against.
- **Demo procedure**: The documented, ordered sequence of prerequisites, actions, and expected
  observations that constitutes "the demo" — the thing that is repeated, not re-invented.
- **Reference artifact**: The evidence retained for Phase 1 exit — a committed written walkthrough
  and per-step stills, plus a video recording held outside the repository — including the mapping
  from observed steps to exit criteria.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A person following the documented procedure completes browse → basket → checkout →
  confirmation in under 5 minutes from a ready platform, without assistance.
- **SC-002**: The demo completes with zero manual interventions outside the documented steps.
- **SC-003**: Two consecutive runs from a clean basket both succeed, producing two distinct order
  records — the demo is repeatable, not a one-off.
- **SC-003a**: One run performed from a cold start reaches the confirmation screen with no manual
  seeding or repair step.
- **SC-004**: For every order placed during the demo, the tenant attribution read back is non-empty
  and matches the tenant resolved for the placing request — 100% of runs, zero exceptions.
- **SC-005**: A reviewer who was not present at the demo can, using only the repository
  documentation, follow the flow step by step from the committed walkthrough and stills, find where
  the video is held, and reproduce the same flow on their own machine.
- **SC-006**: The written walkthrough accounts for 100% of the Phase 1 exit criteria, each marked
  as evidenced by the run or explicitly deferred.
- **SC-006a**: Every component named in the narrated request path shows activity for the demo run —
  no named hop is left unverifiable.
- **SC-007**: A double-triggered checkout produces exactly one order record.
- **SC-008**: Every failure path exercised during the demo (empty basket, unavailable dependency)
  produces a message the runner can act on, and leaves no order record behind.

## Assumptions

- The demo environment for Phase 1 is the local one-command stack delivered by
  `005-one-command-local-run`. Demonstrating on Kubernetes is out of scope here; the Jira story
  permits either, and local is what currently exists.
- Phase 1 remains single-tenant in practice: one resolved tenant, one fake user. Proving isolation
  by demonstrating two tenants side by side is out of scope for this story.
- The storefront delivered by `004-minimal-shopping-spa` is the demo surface; no new user-facing
  screens or visual polish are in scope.
- Making the demo repeatable means the sequence itself is automated end to end where it can be, so
  "run it again" is one action rather than a person retracing clicks. Both the video and the stills
  are captured by that automated run rather than assembled by hand.
- Committing large binaries is avoided deliberately: this repository has no binary-asset convention,
  and a video re-recorded on each change would accumulate in history forever. Stills are small
  enough to be an exception.
- Reading the order back "directly from the orders service" means through the orders service's own
  read capability, not by querying the database behind it — the constitution forbids reaching
  another component's store, and the demo should model the rule it teaches.
- Order line items are not persisted in Phase 1 (per `004`); verifying the order means verifying its
  reference, total, and tenant attribution, not a line-by-line comparison against the basket.
- Nothing in this story adds messaging, events, or an outbox — those arrive with `SCRUM-18` and are
  explicitly deferred.
- Existing prerequisites (a container runtime and the documented environment file) remain the
  runner's responsibility; the demo procedure names them rather than installing them.
- Automating the demo does not put it in the build pipeline: this story delivers a demo runnable by
  one documented command. Whether it becomes a merge gate is decided by the story that owns the
  build gate (`SCRUM-22`, Phase 2).
