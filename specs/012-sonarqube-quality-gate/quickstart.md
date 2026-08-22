# Quickstart: Validating the SonarQube Quality Gate

This validates the feature end-to-end against the acceptance scenarios in `spec.md`. It assumes the
implementation phase has produced: a repository-root `Jenkinsfile`, a `sonar-project.properties` (or
equivalent scanner config), a Jenkins Multibranch Pipeline job pointed at this repo, a reachable
SonarQube server/project, and GitHub branch protection configured per
`contracts/pipeline-stage-contract.md` section 1.

## Prerequisites

- Jenkins Multibranch Pipeline job created for this repository (GitHub Branch Source plugin
  configured with a GitHub App or PAT that can post commit statuses).
- SonarQube Scanner and SonarQube Quality Gates Jenkins plugins installed on the Jenkins controller;
  a SonarQube server connection configured in Jenkins (`Manage Jenkins → System → SonarQube servers`)
  with a webhook back to Jenkins for `waitForQualityGate()`.
- GitHub branch protection on the protected branch requiring all five checks from
  `contracts/pipeline-stage-contract.md` §1, with "do not allow bypassing the above settings"
  enabled.
- Local: `dotnet` 10 SDK and `pnpm` (via `corepack`) available, matching the existing repo tooling.

## Scenario 1 — Full sequence runs in order (User Story 1, Acceptance Scenario 1)

1. Open a PR with a trivial, passing change.
2. In the PR's checks tab, confirm all five checks appear and transition
   `pending → success` in this order: `ci/build`, `ci/unit-tests`, `ci/integration-tests`,
   `ci/contract-tests`, `ci/sonarqube-quality-gate`.
3. **Expected**: merge becomes available only after `ci/sonarqube-quality-gate` succeeds.

## Scenario 2 — Failing gate blocks merge with no override (User Story 1, Acceptance Scenarios 2–3)

1. Open a PR that removes test coverage for a previously-covered code path (or introduces a
   deliberate code smell/duplication above threshold), per the ticket's Test Scenario 1.
2. Confirm the pipeline still runs build → unit → integration → contract, all passing, and
   `ci/sonarqube-quality-gate` reports failure.
3. **Expected**: the PR's merge button is disabled/blocked; no role (including repo admins) has a
   "merge anyway" control, per the ticket's Test Scenario 2 and FR-004/FR-005.
4. Separately, open a PR with an intentionally broken unit test.
5. **Expected**: `ci/unit-tests` fails, `ci/integration-tests`/`ci/contract-tests`/
   `ci/sonarqube-quality-gate` do not run, and the PR is blocked citing `ci/unit-tests` as the cause
   (FR-002).

## Scenario 3 — Passing gate surfaces metrics on the PR (User Story 2)

1. Merge-eligible PR from Scenario 1: open its checks/PR page.
2. **Expected**: coverage %, duplication %, and new code-smell count are visible directly on the PR
   (via SonarQube's GitHub PR decoration, research.md Decision 6), without navigating to the
   SonarQube server.
3. Push an additional commit to the same PR.
4. **Expected**: the displayed metrics update to the new commit's analysis, not the prior one
   (FR-006, FR-007).

## Scenario 4 — Fix and re-evaluate (User Story 3, ticket Test Scenario 3)

1. Starting from the blocked PR in Scenario 2, push a commit that restores coverage above
   threshold.
2. **Expected**: the full five-stage sequence re-runs automatically; once
   `ci/sonarqube-quality-gate` succeeds, the merge block lifts with no manual admin action beyond
   normal PR review (FR-007, SC-004).

## Scenario 5 — SonarQube unreachable fails closed (Edge case, FR-008)

1. Temporarily point the pipeline's Sonar host URL at an unreachable address (test-only; revert
   after), or simulate by stopping the SonarQube server in a non-production environment.
2. Open/update a PR and let the pipeline reach the quality-gate stage.
3. **Expected**: `waitForQualityGate()` times out and `ci/sonarqube-quality-gate` reports failure
   (not success, not skipped) once the configured `timeout()` elapses.

## Success criteria checklist

- [ ] SC-001: every PR shows one pass/fail gate status derived from all five stages.
- [ ] SC-002: no PR merges to a protected branch with a failing gate, across roles.
- [ ] SC-003: coverage/duplication/code-smell numbers visible on every merged PR without leaving it.
- [ ] SC-004: a PR blocked only by a quality regression becomes mergeable after one pipeline re-run
      post-fix, no manual intervention.
- [ ] SC-005: any bypass attempt is either impossible (button disabled) or produces a GitHub audit
      log entry.
