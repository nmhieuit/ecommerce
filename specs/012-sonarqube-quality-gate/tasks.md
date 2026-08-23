---

description: "Task list template for feature implementation"
---

# Tasks: SonarQube Quality Gate as a Merge Blocker

**Input**: Design documents from `/specs/012-sonarqube-quality-gate/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/pipeline-stage-contract.md](./contracts/pipeline-stage-contract.md), [quickstart.md](./quickstart.md)

**Tests**: Not requested in the feature specification. This feature has no application code to unit
test — "tests" here means validating the pipeline itself against the scenarios in `quickstart.md`,
which appear as explicit validation tasks within each user story instead of a separate test phase.

**Organization**: Tasks are grouped by user story (from `spec.md`) to enable independent
implementation and testing of each story. `spec.md` was amended 2026-08-23 to fold in the
previously separate backend-selection decision as a new User Story 1 — every other story's numbering
shifted down by one from the prior version of this file.

## Status legend (implementation pass, 2026-08-23)

- `[X]` — done, and verified locally where the artifact could be exercised without a server.
- `[ ] ⛔` — **blocked on an action only a human can perform**: typing a real secret (GitHub PAT,
  SonarQube token) into a credential field, or a GitHub organization/account-level decision. Per
  FR-015, an automated session must never perform these even when asked, and must hand off an exact
  procedure instead of claiming completion it cannot verify (FR-016).
- `[x] (local)` — done against the **local development** Jenkins/SonarQube instance stood up in
  Docker Desktop today, not against a production instance. See "Remaining work" for what still
  separates local wiring from the production posture the spec describes.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

## Path Conventions

This is CI/infrastructure wiring, not an application feature (see plan.md Project Structure) — the
only new repo artifacts are a root `Jenkinsfile`, `sonar-project.properties`,
`docker-compose.ci.yml` (new, this pass), and ADR-0012. All `stage` work executes existing test
projects under `services/*/tests/` and the existing `frontend/` Turborepo workspace; Jenkins-server
and GitHub-settings tasks are configuration actions (no repo file), traceable to `quickstart.md`'s
Prerequisites and `contracts/pipeline-stage-contract.md`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the skeleton files the pipeline will be built into

- [X] T001 Create a repository-root `Jenkinsfile` with a declarative pipeline skeleton (`agent`,
      empty `stages {}` block, `options { timeout(...) }`) at `Jenkinsfile`
- [X] T002 [P] Create `sonar-project.properties` at repo root with `sonar.projectKey`,
      `sonar.projectName`, `sonar.sources` (covering `services/` and `frontend/`), and
      `sonar.exclusions` for generated/build output (`**/bin/**`, `**/obj/**`, `**/node_modules/**`,
      `**/dist/**`)
- [x] (local) T003 [P] Install the required Jenkins plugins (GitHub Branch Source, GitHub Checks,
      SonarQube Scanner) on the local Jenkins controller via `jenkins-plugin-cli` inside the
      container (`docker-compose.ci.yml`), per `quickstart.md` Prerequisites — credential-free, so
      done directly rather than left as a manual step. A production Jenkins controller needs the
      same three plugins installed the same way (or via its own admin UI).
- [X] T004 [P] Verify `coverlet.collector` is referenced by every `*.Api.{UnitTests,IntegrationTests,ContractTests}`
      project (already true per `research.md`) and add `@vitest/coverage-v8` to the frontend
      workspace's root `devDependencies` in `frontend/package.json` if not already present, so
      `--coverage` is available to every Turbo test task

**Checkpoint**: Skeleton files exist; a local Jenkins has the plugins it needs to run a pipeline
against this repo.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The four non-Sonar stages, in order, with coverage collection wired — every user story
depends on this running end-to-end before the SonarQube stage can mean anything

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T005 Add the `build` stage to `Jenkinsfile`: `dotnet build Ecommerce.slnx` and
      `pnpm --dir frontend install --frozen-lockfile && pnpm --dir frontend build`; stage name
      must publish as required check `ci/build` per `contracts/pipeline-stage-contract.md` §1
- [X] T006 Add the `unit tests` stage to `Jenkinsfile`: run every `*.Api.UnitTests` project via
      `dotnet test --collect:"XPlat Code Coverage"`, run `pnpm --dir frontend turbo run test -- --coverage`
      for frontend unit suites, then merge the per-project Cobertura outputs into one file (e.g.
      via `dotnet-coverage merge`) at a fixed path referenced later by T002's
      `sonar-project.properties`; publishes required check `ci/unit-tests`
- [X] T007 Add the `integration tests` stage to `Jenkinsfile`: run every `*.Api.IntegrationTests`
      project (Testcontainers — requires Docker on the Jenkins agent, per `010-testcontainers-integration-tests`);
      publishes required check `ci/integration-tests`
- [X] T008 Add the `contract tests` stage to `Jenkinsfile`: run every `*.Api.ContractTests` project
      (`baskets`, `bff`, `orders`, `products`, per `011-consumer-contract-tests`); publishes
      required check `ci/contract-tests`
- [X] T009 [P] Set `sonar.cs.cobertura.reportsPaths` in `sonar-project.properties` to the merged
      Cobertura path produced by T006, and `sonar.javascript.lcov.reportPaths` /
      `sonar.typescript.lcov.reportPaths` to the frontend `lcov.info` glob, per
      `research.md` Decision 4 and `contracts/pipeline-stage-contract.md` §2

**Checkpoint**: Pushing a commit runs build → unit → integration → contract in order, each
reporting its own GitHub check, with merged coverage output ready for the Sonar scan.

---

## Phase 3: User Story 1 - A real analysis backend is chosen, provisioned, and connected (Priority: P1) 🎯

**Goal**: A single, justified backend decision exists in writing, and a real instance of that
backend is reachable from wherever the pipeline runs, with a working webhook back to Jenkins —
so the gate has something real to report to.

**Independent Test**: Read the decision record and confirm it names exactly one backend with
rationale; separately, trigger a manual analysis run and confirm the backend receives it, computes
a quality gate result, and reports it back to Jenkins without a connection or authentication error.

### Implementation for User Story 1

- [X] T010 [US1] Record the backend decision (self-hosted SonarQube Community Edition vs.
      SonarCloud) in writing, covering cost, hosting/maintenance burden, and GitHub PR-decoration
      support for both — added as the "Amendment (2026-08-23)" section of
      `docs/adr/0012-ci-quality-gate-enforcement.md` (FR-005, FR-009, FR-014)
- [x] (local) T011 [US1] Provision a SonarQube Community Edition instance reachable from the
      pipeline: added `docker-compose.ci.yml` at the repo root (Jenkins LTS + SonarQube Community,
      shared `ci-backbone` network, named volumes for both, health checks), started via
      `docker compose -f docker-compose.ci.yml up -d`, confirmed both containers report healthy.
      This satisfies "reachable from the Jenkins agent" for **local development only** — see
      "Remaining work" for the production instance FR-006 ultimately requires.
- [ ] ⛔ T012 [US1] Log into the local SonarQube instance (`http://localhost:9000`, default
      `admin`/`admin`, forced password change on first login — a human must set this, not an
      automated session), create a project bound to `nmhieuit/ecommerce`, and generate a project
      analysis token for Jenkins to authenticate with. See the generated setup guide for exact
      screens.
- [ ] ⛔ T013 [US1] Configure the Jenkins↔SonarQube server connection (`Manage Jenkins → System →
      SonarQube servers`, name must match `SONARQUBE_SERVER = 'sonarqube'` in `Jenkinsfile`) using
      the token from T012, plus the webhook back to Jenkins
      (`http://<jenkins-host>:8080/sonarqube-webhook/`) that `waitForQualityGate()` listens on —
      requires pasting a real secret into Jenkins' credential store, so it is a human action, not
      an automated one (FR-015)
- [ ] ⛔ T014 [US1] Generate a GitHub Personal Access Token (or GitHub App) with commit-status/checks
      write access for `nmhieuit/ecommerce`, add it as Jenkins credentials, and provision a Jenkins
      Multibranch Pipeline job using the GitHub Branch Source plugin so PRs are auto-discovered and
      each `Jenkinsfile` stage publishes its named GitHub check (research.md Decision 1–2) —
      requires pasting a real secret, so it is a human action
- [ ] ⛔ T015 [US1] Validate: trigger a manual build (or open a real PR) against the local Jenkins
      job and confirm the `sonarqube quality gate` stage reaches SonarQube and gets back a real
      `OK`/`ERROR` result rather than a connection or authentication error (spec.md User Story 1
      Acceptance Scenario 3)

**Checkpoint**: A real backend exists, is documented, and Jenkins can reach it — the gate has
something real to report to. T012–T014 are the credential-entry steps a human on this machine must
complete; the setup guide this pass produced gives exact screens and values for each.

---

## Phase 4: User Story 2 - Quality gate blocks a substandard PR (Priority: P1)

**Goal**: A failing SonarQube quality gate (or any earlier stage) blocks merge for every role, with
no override control.

**Independent Test**: Open a PR that drops coverage below threshold (or an earlier-stage failure);
confirm the pipeline runs the full ordered sequence, the responsible stage is identified, and no
role — including repository admins — can merge past it.

### Implementation for User Story 2

- [X] T016 [US2] Add the `SonarQube quality gate` stage to `Jenkinsfile`: run
      `dotnet-sonarscanner begin` before T005's build and `dotnet-sonarscanner end` after T008's
      contract-tests stage, then call `waitForQualityGate()` inside `timeout(time: ..., unit: 'MINUTES')`,
      calling `error(...)` on any non-`OK` status or on timeout so the stage fails closed
      (FR-011); publishes required check `ci/sonarqube-quality-gate` per
      `research.md` Decision 3 and `contracts/pipeline-stage-contract.md` §3
- [ ] ⛔ T017 [US2] Confirmed via browser inspection (2026-08-23): `nmhieuit/ecommerce` currently
      has **no branch protection rules and no webhooks configured** (checked
      `github.com/nmhieuit/ecommerce/settings/branches` and `.../settings/hooks` directly). Once
      T013/T014 exist, run `scripts/ci/setup-branch-protection.sh nmhieuit/ecommerce master` as a
      repository admin to add `ci/build`, `ci/unit-tests`, `ci/integration-tests`,
      `ci/contract-tests`, `ci/sonarqube-quality-gate` as required status checks, enable "Require
      branches to be up to date before merging", and enable "Do not allow bypassing the above
      settings" — requires an authenticated `gh` CLI session (not present in this environment), so
      it is a human action (FR-008)
- [ ] ⛔ T018 [US2] Validate `quickstart.md` Scenarios 1 and 2 end-to-end: full sequence runs in
      order on a passing PR; a coverage-dropping PR and a unit-test-breaking PR are both blocked
      with the correct stage identified and no override path for any role

**Checkpoint**: User Story 2 is fully functional and independently testable once T017–T018 close —
the gate blocks bad PRs.

---

## Phase 5: User Story 3 - Passing gate surfaces quality metrics on the PR (Priority: P2)

**Goal**: Coverage, duplication, and code-smell metrics are visible directly on a passing PR.

**Independent Test**: Merge-eligible PR from US2's passing scenario shows coverage %, duplication
%, and new code-smell count on the PR itself, without navigating to the SonarQube server.

### Implementation for User Story 3

- [ ] ⛔ T019 [US3] Install and configure the community-maintained GitHub Branch Plugin on the
      SonarQube instance (Community Edition has no official PR decoration — ADR-0012 amendment),
      or confirm a Developer Edition license is in use instead, then connect it to the GitHub PAT/
      App from T014 so analysis results post automatically to the PR (research.md Decision 6)
- [ ] ⛔ T020 [US3] Confirm the Sonar project's quality-gate conditions read the coverage inputs
      wired in T009 (non-zero, correct coverage/duplication numbers appear, not defaults from an
      unconfigured project)
- [ ] ⛔ T021 [US3] Validate `quickstart.md` Scenario 3 end-to-end: metrics visible on a passing PR;
      pushing a new commit updates the displayed metrics to the latest analysis

**Checkpoint**: User Stories 1, 2, and 3 all work independently — the gate blocks bad PRs and shows
metrics on good ones.

---

## Phase 6: User Story 4 - Coverage gap is fixed and the gate re-evaluates (Priority: P3)

**Goal**: Pushing a fix to a blocked PR automatically re-runs the pipeline and lifts the block, with
no manual intervention.

**Independent Test**: Starting from a PR blocked by a coverage gap, push a commit that restores
coverage; confirm the full sequence re-runs automatically and the merge block lifts once it passes.

### Implementation for User Story 4

- [ ] ⛔ T022 [US4] Confirm the GitHub Branch Source plugin's PR trigger includes the `synchronize`
      event (new commits to an open PR), not only PR-open, so T014's Multibranch job re-triggers
      automatically on every push (FR-010) — this is the plugin's default, but must be confirmed
      against the actual job once T014 exists
- [ ] ⛔ T023 [US4] Validate `quickstart.md` Scenario 4 end-to-end: a PR blocked in US2 becomes
      mergeable after one automatic re-run following a coverage fix, with no manual admin action

**Checkpoint**: All four user stories are independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Governance, edge-case validation, and closing the scope boundary noted in `plan.md`

- [X] T024 [P] Author `docs/adr/0012-ci-quality-gate-enforcement.md` recording the Multibranch
      Pipeline + required-status-checks-with-no-bypass approach (`research.md` Decisions 1, 2, 5),
      per the constitution's Governance rule and `plan.md`'s Constitution Check action item
- [ ] ⛔ T025 [P] Validate `quickstart.md` Scenario 5: a temporarily unreachable SonarQube server
      causes `ci/sonarqube-quality-gate` to fail via the `timeout()` from T016, not to pass or be
      skipped (FR-011)
- [ ] ⛔ T026 Run the full `quickstart.md` Success Criteria checklist (SC-001 through SC-009)
      end-to-end and record the results
- [X] T027 Record the constitution's container-image-vulnerability-scan stage as explicit tracked
      follow-up work (new ticket reference alongside `plan.md`'s Complexity Tracking entry), since
      it is out of scope for this feature but required for full Development Workflow compliance
- [ ] ⛔ T028 [P] Provision the **production** self-hosted SonarQube instance as a Kubernetes
      workload with its own database via the platform's existing Ansible-provisioned pattern (spec
      Edge Cases), replacing the local Docker Desktop instance from T011 as the backend the
      production Jenkins controller points at — the local instance exists to unblock wiring and
      validation, not to serve as the permanent backend

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3-6)**: All depend on Foundational phase completion
  - US1 has no dependency on US2/US3/US4, and everything else depends on it — a gate with no
    backend behind it cannot block anything
  - US2 depends on US1's SonarQube connection (T013) and Multibranch job (T014) existing
  - US3 depends on US2's Jenkinsfile Sonar stage (T016) and Multibranch job (T014) existing to
    have anything to decorate, but does not require US2's branch-protection task (T017)
  - US4 depends on US2's Multibranch job (T014) and branch protection (T017) existing, since it
    validates the *blocked-then-unblocked* behavior those tasks create
- **Polish (Phase 7)**: Depends on US2 (MVP) at minimum; T025/T026 assume all four stories are
  complete for a full validation pass; T028 can start any time after T011 lands

### User Story Dependencies

- **User Story 1 (P1)**: Start after Foundational (Phase 2) — no dependency on other stories; every
  other story depends on it
- **User Story 2 (P1)**: Start after US1's T013/T014 exist — this is the core enforcement value
- **User Story 3 (P2)**: Start after US2's T016/T014 exist — adds visibility on top of the gate
- **User Story 4 (P3)**: Start after US2's T014/T017 exist — validates re-trigger behavior

### Within Each User Story

- Configuration tasks (Jenkins/GitHub/SonarQube settings) before their validation task
- Validation task last in each phase, per `quickstart.md`

### Parallel Opportunities

- T002, T003, T004 (Phase 1) can run in parallel — different files/systems, no shared dependency
- T009 (Phase 2) can run in parallel with T005-T008 once `sonar-project.properties` exists from
  T002 — T005-T008 all edit `Jenkinsfile` sequentially and are NOT parallel with each other
- T024 and T025 (Phase 7) can run in parallel — different files/no shared state
- T028 can run in parallel with US2/US3/US4 once T011 exists — it replaces the backend, it doesn't
  block wiring to it

---

## Parallel Example: Phase 1 (Setup)

```bash
# Launch Setup's independent tasks together:
Task: "Create sonar-project.properties at repo root with sonar.projectKey, sonar.sources, sonar.exclusions"
Task: "Install required Jenkins plugins via jenkins-plugin-cli"
Task: "Add @vitest/coverage-v8 to frontend/package.json devDependencies if missing"
```

---

## Implementation Strategy

### MVP First (User Story 1 + User Story 2)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1 — a real backend must exist before anything can block on it
4. Complete Phase 4: User Story 2
5. **STOP and VALIDATE**: Run `quickstart.md` Scenarios 1 and 2 against a real PR
6. At this point the gate is live and blocking — deploy/announce to the team

### Incremental Delivery

1. Complete Setup + Foundational → pipeline runs all four non-Sonar stages in order
2. Add User Story 1 → a real, documented backend exists and is reachable
3. Add User Story 2 → gate blocks bad PRs → this is the MVP the ticket asks for
4. Add User Story 3 → metrics become visible on the PR → no behavior change to blocking
5. Add User Story 4 → confirm auto re-trigger (mostly validation of existing Multibranch behavior)
6. Polish → production backend provisioning, edge-case validation, and the tracked
   vulnerability-scan follow-up

### Solo/Small-Team Strategy

Given this is infrastructure wiring on one `Jenkinsfile` and one set of GitHub/Jenkins/SonarQube
settings (not parallelizable application code across a team), the realistic path is sequential:
Setup → Foundational → US1 → US2 → US3 → US4 → Polish, in that order, by whoever owns CI for this
repo.

---

## Notes

- [P] tasks = different files/systems, no dependency ordering required
- [Story] label maps task to specific user story for traceability
- No test-code tasks exist because this feature has no application code — `quickstart.md`
  Scenarios 1–5 are the acceptance tests, referenced directly from each phase's validation task
- Commit after each task or logical group (e.g., after each `Jenkinsfile` stage addition)
- Stop at the Phase 4 checkpoint to validate the MVP independently before continuing

---

## Remaining work (after the 2026-08-23 implementation pass)

Every repository artifact this feature calls for exists and is in place:

| Artifact | Purpose |
|---|---|
| `Jenkinsfile` | Five ordered stages, each publishing its contracted `ci/*` GitHub check |
| `sonar-project.properties` | Single source of truth for scanner settings and coverage inputs |
| `docker-compose.ci.yml` | **New this pass** — local Jenkins + SonarQube for development wiring |
| `scripts/ci/sonar-begin.sh` | Translates that properties file into SonarScanner-for-.NET arguments |
| `scripts/ci/run-dotnet-tests.sh` | Discovers and runs the unit / integration / contract tiers |
| `scripts/ci/merge-coverage.sh` | Merges per-project Cobertura reports into the one file Sonar reads |
| `scripts/ci/setup-branch-protection.sh` | The one-time `gh api` call that makes the checks blocking |
| `frontend/apps/web/vitest.config.ts`, `frontend/turbo.json` | lcov coverage output, preserved across Turbo cache hits |
| `.config/dotnet-tools.json` | Pins `dotnet-sonarscanner` 11.2.1 and `dotnet-coverage` 18.10.0 |
| `docs/adr/0012-ci-quality-gate-enforcement.md` | Governance record, now including the backend decision |

What changed this pass: found that **no Jenkins or SonarQube instance existed anywhere** (Docker
Desktop had no such containers, images, or listening ports, contrary to the assumption that one was
already running) and no `gh` CLI/auth was available in this environment. Stood up a local instance
of both in Docker Desktop via `docker-compose.ci.yml`, installed Jenkins' three required plugins
credential-free via `jenkins-plugin-cli`, wrote the missing backend-decision record into ADR-0012,
and confirmed via direct inspection of `github.com/nmhieuit/ecommerce/settings` that the repository
is private, its default branch is `master`, and it currently has zero branch protection rules and
zero webhooks configured.

What is not, and cannot be, automated from here — every one of these requires typing a real secret
into a form, which FR-015 and this session's operating rules both treat as a human action, never an
automated one:

1. **SonarQube** (T012) — first-login password change and project/token creation on
   `http://localhost:9000`.
2. **Jenkins↔SonarQube** (T013) — pasting that SonarQube token into
   `Manage Jenkins → System → SonarQube servers`, plus the webhook URL.
3. **Jenkins↔GitHub** (T014) — generating a GitHub PAT/App and pasting it into a new Jenkins
   credential, then creating the Multibranch Pipeline job.
4. **GitHub branch protection** (T017) — either running `scripts/ci/setup-branch-protection.sh`
   with an authenticated `gh` session, or configuring the same five required checks by hand at
   `github.com/nmhieuit/ecommerce/settings/branches`.
5. **PR decoration licensing** (T019) — installing the Branch Plugin or confirming a Developer
   Edition license.
6. **Validation** (T015, T018, T021, T022, T023, T025, T026) — work through `quickstart.md`
   Scenarios 1-5 and the SC-001..SC-009 checklist against real pull requests once 1-4 are done.
7. **Production backend** (T028) — the Kubernetes/Ansible-provisioned SonarQube instance the spec's
   Edge Cases describe; today's local Docker Desktop instance is a development stand-in, not that.

A companion document, `docs/github-jenkins-sonarqube-setup.md`, gives the exact screens, field
values, and commands for steps 1–5 above, generated from this session's inspection of the live
GitHub repository and the freshly started local Jenkins/SonarQube containers.

One repository-side item also remains open: ADR-0012's Action Item 4 carries a `SCRUM-TBD`
placeholder for the container image vulnerability scan (T027). Raising that backlog ticket and
replacing the placeholder closes the tracking obligation on `plan.md`'s Complexity Tracking entry.
