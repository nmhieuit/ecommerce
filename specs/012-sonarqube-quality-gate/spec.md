# Feature Specification: SonarQube Quality Gate as a Merge Blocker

**Feature Branch**: `012-sonarqube-quality-gate`

**Created**: 2026-08-22

**Status**: Draft

**Input**: User description: "SCRUM-22: [CONTRACT-2] Wire SonarQube quality gate as a merge blocker — As DevOps, I want the SonarQube quality gate wired into the build and enforced as a merge blocker so that coverage and code-quality standards are machine-enforced, not reviewed by eye (Development Workflow & Quality Gates). Acceptance Criteria: pipeline runs build → unit tests → integration tests → contract tests → SonarQube gate in sequence; a failing gate blocks merge with no override path; a passing gate surfaces coverage/duplication/code-smell metrics on the PR. (Source: https://nmhieuit.atlassian.net/browse/SCRUM-22)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Quality gate blocks a substandard PR (Priority: P1)

As a developer opening a pull request, I want the pipeline to automatically run the full quality
sequence and block my merge if code quality or coverage regresses, so that quality standards are
enforced consistently without relying on a reviewer noticing by eye.

**Why this priority**: This is the core value of the feature — an unenforced gate is not a gate.
Without automatic blocking, the feature delivers no value over the status quo of manual review.

**Independent Test**: Open a PR that intentionally drops test coverage below the configured
threshold (or introduces a blocking code smell). Confirm the pipeline runs build → unit tests →
integration tests → contract tests → SonarQube analysis in that order, the SonarQube stage fails,
and the PR's merge control is blocked with a visible reason.

**Acceptance Scenarios**:

1. **Given** a PR is opened against a protected branch, **When** the pipeline runs, **Then** it
   executes build → unit tests → integration tests → contract tests → SonarQube quality gate in
   that sequence, and each stage's pass/fail status is visible on the PR.
2. **Given** the SonarQube quality gate fails (coverage below threshold, new blocker/critical
   issues, or excessive duplication), **When** someone attempts to merge the PR, **Then** the merge
   is blocked and no merge override control is available to any role.
3. **Given** an earlier stage (build, unit tests, integration tests, or contract tests) fails,
   **When** the pipeline continues, **Then** the SonarQube stage does not run and the PR is blocked
   at the failed stage with that stage identified as the cause.

---

### User Story 2 - Passing gate surfaces quality metrics on the PR (Priority: P2)

As a developer or reviewer, I want to see coverage, duplication, and code-smell metrics directly on
the PR once the quality gate passes, so that I can assess code health without leaving the PR or
manually opening the SonarQube dashboard.

**Why this priority**: Visibility is what makes the gate trustworthy and actionable day-to-day, but
the feature still delivers its primary value (blocking) without it — this is a fast-follow, not a
prerequisite.

**Independent Test**: Merge a PR that passes the quality gate and confirm the PR view shows
coverage percentage, duplication percentage, and new code-smell count, each linking to the full
SonarQube analysis.

**Acceptance Scenarios**:

1. **Given** the SonarQube quality gate passes, **When** a reviewer views the PR status, **Then**
   coverage, duplication, and code-smell counts for the changed code are visible directly on the PR.
2. **Given** the quality gate has run at least once for a PR, **When** new commits are pushed,
   **Then** the displayed metrics update to reflect the latest analysis rather than a stale run.

---

### User Story 3 - Coverage gap is fixed and the gate re-evaluates (Priority: P3)

As a developer whose PR was blocked, I want to push a fix and have the gate automatically
re-evaluate, so that I can unblock my own PR without manual intervention from anyone else.

**Why this priority**: Self-service recovery is expected pipeline behavior and mostly falls out of
correctly wiring the gate to the PR's head commit; it is called out separately because it is an
explicit acceptance/test scenario in the source request.

**Independent Test**: Starting from a PR blocked by a coverage gap, push a commit that restores
coverage above the threshold and confirm the pipeline re-runs automatically and the merge block is
lifted without any manual override.

**Acceptance Scenarios**:

1. **Given** a PR is blocked by a failed quality gate, **When** the developer pushes a commit that
   resolves the underlying issue, **Then** the full pipeline sequence re-runs automatically and the
   PR's merge status updates once the new analysis completes.
2. **Given** the re-run passes the quality gate, **When** the developer checks the PR, **Then** the
   merge block is lifted with no manual approval step required beyond the existing review policy.

### Edge Cases

- What happens when someone with elevated repository/admin permissions attempts to merge past a
  failing gate (e.g., an "admin merge" or force-merge)? The system MUST prevent this the same as
  for any other role; if the platform's tooling cannot technically prevent an admin override, the
  attempt MUST be logged with actor, PR, and justification and surfaced as an audit event, per the
  Test Scenarios in the source ticket.
- What happens when the SonarQube analysis service itself is unreachable or times out? The gate
  MUST fail closed (treated as a blocking failure, not silently skipped), and the PR MUST show that
  the gate could not be evaluated as the reason merge is blocked.
- What happens on a PR that touches only documentation or non-code files? The full sequence still
  runs; a reasonable default is that SonarQube analyzes whatever changed and the gate evaluates
  normally (no bypass path for "trivial" changes, consistent with "no override path").
- What happens when a PR has zero new lines of coverable code (e.g., a pure config change)? The
  gate MUST still evaluate using SonarQube's standard new-code quality gate conditions rather than
  being skipped.
- How are quality gate thresholds (coverage %, duplication %, blocker/critical issue counts)
  configured and changed over time? They are centrally defined quality-profile/gate settings, not
  per-PR or per-developer configurable.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The pipeline MUST execute, for every pull request against a protected branch, the
  stages build, unit tests, integration tests, contract tests, and SonarQube quality gate, strictly
  in that order.
- **FR-002**: The pipeline MUST stop before the SonarQube stage if any earlier stage (build, unit
  tests, integration tests, contract tests) fails, and MUST report the failing stage as the block
  reason on the PR.
- **FR-003**: The system MUST report a single quality-gate status (pass/fail) per PR head commit,
  derived from the current SonarQube quality gate configuration (coverage threshold, duplication
  threshold, new blocker/critical issue rules).
- **FR-004**: The system MUST block merging of any PR whose quality-gate status is failing,
  regardless of the merging user's role or permission level, and MUST NOT provide any override,
  bypass, or "merge anyway" control for a failing gate.
- **FR-005**: Merging MUST be governed by GitHub branch protection on protected branches, with the
  SonarQube quality gate (and the preceding build/unit/integration/contract stages) configured as
  required status checks, and with "Do not allow bypassing the above settings" enabled so that
  repository administrators cannot merge past a failing or pending check either. If GitHub ever
  exposes a residual override path outside this setting, every such override attempt MUST be
  captured in an audit log recording actor, PR, timestamp, and stated justification.
- **FR-006**: When the quality gate passes, the system MUST display coverage percentage, code
  duplication percentage, and new code-smell count for the analyzed changes on the PR itself
  (without requiring navigation to a separate SonarQube dashboard to see the summary).
- **FR-007**: The system MUST re-run the full stage sequence automatically whenever new commits are
  pushed to a PR under evaluation, and MUST update the PR's merge-block status to reflect the latest
  run rather than a prior one.
- **FR-008**: The system MUST treat an inability to reach or complete SonarQube analysis (timeout,
  service error) as a failing/blocking result, not as a skipped or passing check.
- **FR-009**: The quality gate's thresholds and rules MUST be defined in a single centrally
  maintained SonarQube quality profile/gate rather than configured per repository, per PR, or per
  developer.
- **FR-010**: The system MUST retain a record of each PR's stage-by-stage pipeline results and
  quality-gate outcome for at least as long as the PR remains open, for audit and troubleshooting.

### Key Entities

- **Pipeline Run**: One execution of the build → unit tests → integration tests → contract tests →
  SonarQube stage sequence for a specific PR commit; has a status and a list of per-stage results.
- **Quality Gate Result**: The SonarQube outcome for a given analysis (pass/fail), carrying the
  coverage, duplication, and new-issue metrics evaluated against the quality profile.
- **Merge Block**: The state attached to a PR that prevents merging while its latest Pipeline Run's
  Quality Gate Result (or an earlier stage) is failing; cleared automatically once a passing run
  completes for the current head commit.
- **Override Attempt** (audit-only): A record of any attempt to merge a PR while blocked, capturing
  actor, PR, timestamp, and justification, used only if the hosting platform cannot fully prevent
  the action.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of PRs opened against a protected branch show a single pass/fail quality-gate
  status derived from the full build → unit → integration → contract → SonarQube sequence before
  merge is permitted.
- **SC-002**: 0 PRs with a failing quality gate are merged into a protected branch, across all
  roles, over any 90-day audit window.
- **SC-003**: Reviewers can see coverage, duplication, and code-smell counts directly on a passing
  PR without leaving the PR view, for 100% of merged PRs.
- **SC-004**: A PR blocked solely by a coverage/quality regression becomes mergeable within one
  pipeline run (no manual intervention) after the underlying code issue is fixed and pushed.
- **SC-005**: Any bypass or override attempt on a blocked PR is either technically impossible or
  produces an audit record within the same pipeline run, 100% of the time.

## Assumptions

- Pull requests are hosted and merged on GitHub, using branch protection with required status
  checks and the administrator-bypass setting disabled, per FR-005.
- Unit tests, integration tests (Testcontainers-based, per prior work), and contract tests already
  exist and run in CI for the services in scope; this feature wires SonarQube in as the next stage
  and enforces its result, rather than authoring new unit/integration/contract test suites.
  [009-retrofit-tdd-basket-order] and [010-testcontainers-integration-tests] establish those test
  layers; [011-consumer-contract-tests] establishes the contract-test layer this feature's sequence
  depends on.
- SonarQube (server or SonarCloud) is available as an analysis target; provisioning a new SonarQube
  instance is out of scope for this feature, which focuses on wiring the pipeline and gate
  enforcement.
- "No override path" means no UI/API control to merge past a failing gate for any role; it does not
  mean the gate's thresholds themselves are immutable — threshold changes go through the normal
  quality-profile configuration process, not a per-PR override.
- Coverage/duplication/code-smell thresholds themselves (exact percentages) are configuration
  values owned by the platform's SonarQube quality profile and are not fixed by this specification.
