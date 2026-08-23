# Feature Specification: SonarQube Quality Gate as a Merge Blocker

**Feature Branch**: `012-sonarqube-quality-gate`

**Created**: 2026-08-22 (backend-selection scope merged in 2026-08-23)

**Status**: Draft

**Input**: User description: "SCRUM-22: [CONTRACT-2] Wire SonarQube quality gate as a merge blocker — As DevOps, I want the SonarQube quality gate wired into the build and enforced as a merge blocker so that coverage and code-quality standards are machine-enforced, not reviewed by eye (Development Workflow & Quality Gates). Acceptance Criteria: pipeline runs build → unit tests → integration tests → contract tests → SonarQube gate in sequence; a failing gate blocks merge with no override path; a passing gate surfaces coverage/duplication/code-smell metrics on the PR. (Source: https://nmhieuit.atlassian.net/browse/SCRUM-22)" — combined with a follow-up: "Decide between SonarQube (self-hosted) and SonarCloud (SaaS) as the analysis backend for this pipeline, and connect the chosen option to https://github.com/nmhieuit/ecommerce so every PR reports a real quality-gate check, closing tasks T011-T013."

**Note**: This spec supersedes the separate `013-sonarqube-backend-selection` feature. The pipeline
mechanics (which stages run, what blocks a merge) and the analysis-backend decision (which product
those stages report to, and how it's connected to GitHub) are one feature, not two — the gate has no
meaning without a real backend behind it, and the backend has no purpose without the gate. The
`013-sonarqube-backend-selection/` directory is retired; its content lives here now.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A real analysis backend is chosen, provisioned, and connected (Priority: P1)

As DevOps, I want a single, justified decision on the quality gate's analysis backend — self-hosted
SonarQube or SonarCloud — and a real, provisioned instance of it connected to
`github.com/nmhieuit/ecommerce`, so that the pipeline has something real to report to instead of a
placeholder connection nobody has set up.

**Why this priority**: Every other story in this feature depends on this existing first — a gate
that reports to nothing cannot block anything.

**Independent Test**: Read the decision record and confirm it names exactly one backend with
rationale; separately, trigger a manual analysis run (or a real PR build) and confirm the backend
receives it, computes a quality gate result, and reports it back to Jenkins without a connection or
authentication error.

**Acceptance Scenarios**:

1. **Given** the two candidate backends, **When** the decision is made, **Then** it is recorded in
   writing with rationale covering cost, hosting/maintenance burden, and GitHub PR-decoration
   support for both options — not just the chosen one.
2. **Given** the decision is recorded, **When** anyone reads the CI enforcement ADR, **Then** they
   can tell which backend the pipeline targets, and why, without asking the person who set it up.
3. **Given** the chosen backend, **When** setup is complete, **Then** a Sonar project exists that is
   bound to the `nmhieuit/ecommerce` GitHub repository and reachable from the Jenkins agent that runs
   the pipeline, with a working webhook/callback so the pipeline's quality-gate wait step receives a
   real result rather than a connection timeout.
4. **Given** setup requires credentials or access a given work session does not have (no `gh` CLI
   auth, no cluster access, no existing server admin account), **When** that gap is hit, **Then** the
   gap is reported explicitly and a numbered, copy-pasteable manual setup guide is produced instead
   of the step being silently marked done.

---

### User Story 2 - Quality gate blocks a substandard PR (Priority: P1)

As a developer opening a pull request, I want the pipeline to automatically run the full quality
sequence against the connected backend and block my merge if code quality or coverage regresses, so
that quality standards are enforced consistently without relying on a reviewer noticing by eye.

**Why this priority**: This is the core value of the feature — an unenforced gate is not a gate.
Without automatic blocking, the feature delivers no value over the status quo of manual review.

**Independent Test**: Open a PR that intentionally drops test coverage below the configured
threshold (or introduces a blocking code smell). Confirm the pipeline runs build → unit tests →
integration tests → contract tests → SonarQube analysis in that order, the SonarQube stage fails,
and the PR's merge control is blocked with a visible reason, for every role including repository
admins.

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

### User Story 3 - Passing gate surfaces quality metrics on the PR (Priority: P2)

As a developer or reviewer, I want to see coverage, duplication, and code-smell metrics directly on
the PR once the quality gate passes, so that I can assess code health without leaving the PR or
manually opening the SonarQube dashboard.

**Why this priority**: Visibility is what makes the gate trustworthy and actionable day-to-day, but
the feature still delivers its primary value (blocking) without it — this is a fast-follow, not a
prerequisite.

**Independent Test**: Merge a PR that passes the quality gate and confirm the PR view shows
coverage percentage, duplication percentage, and new code-smell count via GitHub PR decoration.

**Acceptance Scenarios**:

1. **Given** the SonarQube quality gate passes, **When** a reviewer views the PR status, **Then**
   coverage, duplication, and code-smell counts for the changed code are visible directly on the PR.
2. **Given** the quality gate has run at least once for a PR, **When** new commits are pushed,
   **Then** the displayed metrics update to reflect the latest analysis rather than a stale run.

---

### User Story 4 - Coverage gap is fixed and the gate re-evaluates (Priority: P3)

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
  attempt MUST be logged with actor, PR, and justification and surfaced as an audit event.
- What happens if the Jenkins agent cannot route to the self-hosted SonarQube server (wrong
  network/namespace, firewall, DNS)? The gate MUST fail closed rather than skip or pass, and the
  manual setup guide must call out the specific network path (or Kubernetes Service/Ingress)
  required.
- What happens if no SonarQube server exists yet at all (the case at the time of this spec, since
  none has been provisioned)? Standing up the server itself — its Kubernetes deployment, database,
  and Ansible playbook entry — is in scope as the first provisioning step, following the platform's
  existing "containers on Kubernetes via Ansible" pattern rather than an ad hoc install.
- What happens on a PR that touches only documentation or non-code files? The full sequence still
  runs; a reasonable default is that SonarQube analyzes whatever changed and the gate evaluates
  normally (no bypass path for "trivial" changes, consistent with "no override path").
- What happens when a PR has zero new lines of coverable code (e.g., a pure config change)? The
  gate MUST still evaluate using SonarQube's standard new-code quality gate conditions rather than
  being skipped.
- How are quality gate thresholds (coverage %, duplication %, blocker/critical issue counts)
  configured and changed over time? They are centrally defined quality-profile/gate settings, not
  per-PR or per-developer configurable.
- What happens if a given work session lacks the credentials to complete a step (no `gh` CLI auth,
  no Kubernetes cluster access, no existing SonarQube admin account)? That step is not attempted
  silently — it is reported as blocked, with the manual steps a human needs to run instead.

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
- **FR-005**: The analysis backend MUST be a self-hosted SonarQube server, not SonarCloud. This
  decision MUST be recorded in writing, covering cost, hosting/maintenance burden, and GitHub
  PR-decoration support for both options, with SonarCloud's rejection reason stated explicitly
  (recurring SaaS cost for a private repository, and source metadata leaving the internal network,
  against a platform whose stated pattern is everything self-hosted on Kubernetes via Ansible).
- **FR-006**: The chosen backend MUST have a Sonar project provisioned and bound to the
  `nmhieuit/ecommerce` GitHub repository, reachable from wherever the pipeline executes.
- **FR-007**: The connection between the backend and Jenkins MUST supply a working webhook/callback
  so that the pipeline's quality-gate wait step receives a real quality-gate result rather than
  timing out.
- **FR-008**: Merging MUST be governed by GitHub branch protection on protected branches, with the
  five stages (`ci/build`, `ci/unit-tests`, `ci/integration-tests`, `ci/contract-tests`,
  `ci/sonarqube-quality-gate`) configured as required status checks, and with "Do not allow
  bypassing the above settings" enabled so that repository administrators cannot merge past a
  failing or pending check either. If GitHub ever exposes a residual override path outside this
  setting, every such override attempt MUST be captured in an audit log recording actor, PR,
  timestamp, and stated justification.
- **FR-009**: When the quality gate passes, the system MUST display coverage percentage, code
  duplication percentage, and new code-smell count for the analyzed changes on the PR itself
  (without requiring navigation to a separate SonarQube dashboard) via GitHub PR decoration.
  SonarQube's official PR-decoration feature requires a Developer Edition (or higher) license;
  Community Edition (the no-cost tier) does not include it. The decision record MUST state which
  licensing tier is used and, if staying on Community Edition, MUST name the accepted alternative
  (the community-maintained Branch Plugin, or accepting that metrics are visible on the server but
  not decorated onto the PR) rather than silently assuming decoration works.
- **FR-010**: The system MUST re-run the full stage sequence automatically whenever new commits are
  pushed to a PR under evaluation, and MUST update the PR's merge-block status to reflect the latest
  run rather than a prior one.
- **FR-011**: The system MUST treat an inability to reach or complete SonarQube analysis (timeout,
  service error, unreachable server) as a failing/blocking result, not as a skipped or passing check.
- **FR-012**: The quality gate's thresholds and rules MUST be defined in a single centrally
  maintained SonarQube quality profile/gate rather than configured per repository, per PR, or per
  developer.
- **FR-013**: The system MUST retain a record of each PR's stage-by-stage pipeline results and
  quality-gate outcome for at least as long as the PR remains open, for audit and troubleshooting.
- **FR-014**: The backend decision and its connection to GitHub MUST be reflected in
  `docs/adr/0012-ci-quality-gate-enforcement.md` (by amendment) per the constitution's Governance
  rule that architecturally significant decisions are recorded as ADRs.
- **FR-015**: Wherever a given work session has sufficient credentials or authenticated CLI/cluster
  access to perform a setup step directly, it MUST be performed and the resulting change shown.
  Wherever it does not, the step MUST be reported as blocked with an explicit, numbered,
  copy-pasteable manual procedure instead of being assumed complete.
- **FR-016**: This feature's own `tasks.md` (the tasks tracking Jenkins↔SonarQube connection,
  Multibranch job provisioning, and branch protection) MUST accurately reflect real-world state —
  checked off only with evidence, or explicitly marked blocked with the specific external dependency
  named — never left inconsistent with what was actually verified.

### Key Entities

- **Pipeline Run**: One execution of the build → unit tests → integration tests → contract tests →
  SonarQube stage sequence for a specific PR commit; has a status and a list of per-stage results.
- **Quality Gate Result**: The SonarQube outcome for a given analysis (pass/fail), carrying the
  coverage, duplication, and new-issue metrics evaluated against the quality profile.
- **Merge Block**: The state attached to a PR that prevents merging while its latest Pipeline Run's
  Quality Gate Result (or an earlier stage) is failing; cleared automatically once a passing run
  completes for the current head commit. Physically implemented as GitHub branch protection's
  required-status-checks mechanism.
- **Override Attempt** (audit-only): A record of any attempt to merge a PR while blocked, capturing
  actor, PR, timestamp, and justification, used only if the hosting platform cannot fully prevent
  the action.
- **Backend Decision Record**: The written comparison and choice between self-hosted SonarQube and
  SonarCloud (FR-005, FR-014).
- **Sonar Project**: The provisioned analysis project on the self-hosted SonarQube server, bound to
  `nmhieuit/ecommerce` (FR-006).
- **Jenkins↔Backend Connection**: The server connection and webhook configured in Jenkins so the
  quality-gate wait step resolves against the real backend (FR-007).

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
- **SC-006**: A reader of the ADR can state which analysis backend was chosen and why, without
  asking anyone, within one minute of reading it.
- **SC-007**: A real pull request opened against `nmhieuit/ecommerce` shows all five required checks
  running, including a SonarQube result that is not a connection error or timeout.
- **SC-008**: Every setup step a given work session could not complete for lack of credentials/
  access is accompanied by a manual procedure specific and complete enough that a human with the
  right access can follow it without further research.
- **SC-009**: This feature's `tasks.md` entries for backend connection, pipeline provisioning, and
  branch protection accurately reflect real-world state after implementation, with no task marked
  done that was not actually verified.

## Assumptions

- Pull requests are hosted and merged on GitHub, using branch protection with required status
  checks and the administrator-bypass setting disabled, per FR-008.
- Unit tests, integration tests (Testcontainers-based, per prior work), and contract tests already
  exist and run in CI for the services in scope; this feature wires SonarQube in as the next stage
  and enforces its result, rather than authoring new unit/integration/contract test suites.
  [009-retrofit-tdd-basket-order] and [010-testcontainers-integration-tests] establish those test
  layers; [011-consumer-contract-tests] establishes the contract-test layer this feature's sequence
  depends on.
- `nmhieuit/ecommerce` is treated as a private repository by default (typical for a company
  codebase); this was part of why self-hosted SonarQube was chosen over SonarCloud (no recurring
  paid-tier SaaS cost for private-repo analysis), and is recorded as such in the decision record.
- The backend decision (self-hosted vs. SaaS) was raised as a clarification during specification
  rather than defaulted silently, because it carries real ongoing cost and operational-ownership
  consequences; the requesting user chose self-hosted SonarQube.
- No self-hosted SonarQube server exists yet for this repository at the time of this spec;
  provisioning one (as a Kubernetes workload with its own database, following the platform's
  existing Ansible-provisioned pattern) is in scope, not assumed to already exist.
- SonarQube Community Edition (no license cost) is assumed, consistent with the cost rationale for
  choosing self-hosted over SonarCloud; since Community Edition has no official PR decoration, the
  default is the community-maintained Branch Plugin (widely used for exactly this gap) rather than
  a paid Developer Edition license. This trade-off — unofficial plugin, reinstalled on every
  SonarQube upgrade — is recorded in the ADR rather than hidden, and can be revisited later.
- This feature builds directly on already-existing repository artifacts rather than starting from
  scratch: `Jenkinsfile`, `sonar-project.properties`, and `scripts/ci/*.sh` (`sonar-begin.sh`,
  `merge-coverage.sh`, `run-dotnet-tests.sh`, `setup-branch-protection.sh`) already implement the
  five-stage pipeline and read `SONAR_HOST_URL` / `SONAR_TOKEN` from the environment in a
  backend-agnostic way; this feature extends and completes that wiring (provisioning the actual
  server, the Jenkins connection, the Multibranch job, and applying branch protection) rather than
  replacing it.
- "No override path" means no UI/API control to merge past a failing gate for any role; it does not
  mean the gate's thresholds themselves are immutable — threshold changes go through the normal
  quality-profile configuration process, not a per-PR override.
- Coverage/duplication/code-smell thresholds themselves (exact percentages) are configuration
  values owned by the platform's SonarQube quality profile and are not fixed by this specification.
- Actually provisioning a server, installing the PR-decoration plugin, or changing live GitHub
  repository settings requires credentials/cluster access a given work session may or may not have;
  per FR-015, this feature's output either performs those actions when possible or hands off a
  precise manual procedure — it does not claim success it cannot verify.
