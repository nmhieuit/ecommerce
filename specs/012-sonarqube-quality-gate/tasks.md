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
implementation and testing of each story.

## Status legend (implementation pass, 2026-08-22)

- `[X]` — done, and verified locally where the artifact could be exercised without a server.
- `[ ] ⛔` — **blocked on access to an external system** (the Jenkins controller, the SonarQube
  server, or the GitHub repository's admin settings). Nothing in the repository is missing for these;
  each is an action a Jenkins/SonarQube/GitHub administrator performs, or a validation that requires
  a live pull request running against them. See "Remaining work" at the end of this file.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

This is CI/infrastructure wiring, not an application feature (see plan.md Project Structure) — the
only new repo artifacts are a root `Jenkinsfile`, `sonar-project.properties`, and one ADR. All
`stage` work executes existing test projects under `services/*/tests/` and the existing
`frontend/` Turborepo workspace; Jenkins-server and GitHub-settings tasks are configuration actions
(no repo file), traceable to `quickstart.md`'s Prerequisites and
`contracts/pipeline-stage-contract.md`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the skeleton files the pipeline will be built into

- [X] T001 Create a repository-root `Jenkinsfile` with a declarative pipeline skeleton (`agent`,
      empty `stages {}` block, `options { timeout(...) }`) at `Jenkinsfile`
- [X] T002 [P] Create `sonar-project.properties` at repo root with `sonar.projectKey`,
      `sonar.projectName`, `sonar.sources` (covering `services/` and `frontend/`), and
      `sonar.exclusions` for generated/build output (`**/bin/**`, `**/obj/**`, `**/node_modules/**`,
      `**/dist/**`)
- [ ] ⛔ T003 [P] Verify/install required Jenkins plugins (GitHub Branch Source, Pipeline: SonarQube
      Scanner integration) on the Jenkins controller, per `quickstart.md` Prerequisites
- [X] T004 [P] Verify `coverlet.collector` is referenced by every `*.Api.{UnitTests,IntegrationTests,ContractTests}`
      project (already true per `research.md`) and add `@vitest/coverage-v8` to the frontend
      workspace's root `devDependencies` in `frontend/package.json` if not already present, so
      `--coverage` is available to every Turbo test task

**Checkpoint**: Skeleton files exist; Jenkins has the plugins it needs to run a pipeline against
this repo.

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

## Phase 3: User Story 1 - Quality gate blocks a substandard PR (Priority: P1) 🎯 MVP

**Goal**: A failing SonarQube quality gate (or any earlier stage) blocks merge for every role, with
no override control.

**Independent Test**: Open a PR that drops coverage below threshold (or an earlier-stage failure);
confirm the pipeline runs the full ordered sequence, the responsible stage is identified, and no
role — including repository admins — can merge past it.

### Implementation for User Story 1

- [X] T010 [US1] Add the `SonarQube quality gate` stage to `Jenkinsfile`: run
      `dotnet-sonarscanner begin` before T005's build and `dotnet-sonarscanner end` after T008's
      contract-tests stage, then call `waitForQualityGate()` inside `timeout(time: ..., unit: 'MINUTES')`,
      calling `error(...)` on any non-`OK` status or on timeout so the stage fails closed
      (FR-008); publishes required check `ci/sonarqube-quality-gate` per
      `research.md` Decision 3 and `contracts/pipeline-stage-contract.md` §3
- [ ] ⛔ T011 [US1] Configure the Jenkins↔SonarQube server connection (`Manage Jenkins → System →
      SonarQube servers`) including the webhook back to Jenkins that `waitForQualityGate()`
      listens on, per `quickstart.md` Prerequisites
- [ ] ⛔ T012 [US1] Provision a Jenkins Multibranch Pipeline job for this repository using the GitHub
      Branch Source plugin (credentials with commit-status write access), so PRs are
      auto-discovered and each `Jenkinsfile` stage publishes its named GitHub check, per
      `research.md` Decision 1–2
- [ ] ⛔ T013 [US1] Configure GitHub branch protection on the protected branch: add `ci/build`,
      `ci/unit-tests`, `ci/integration-tests`, `ci/contract-tests`, `ci/sonarqube-quality-gate` as
      required status checks, enable "Require branches to be up to date before merging", and
      enable "Do not allow bypassing the above settings" so admins cannot override either
      (FR-004, FR-005; `research.md` Decision 5)
- [ ] ⛔ T014 [US1] Validate `quickstart.md` Scenarios 1 and 2 end-to-end: full sequence runs in
      order on a passing PR; a coverage-dropping PR and a unit-test-breaking PR are both blocked
      with the correct stage identified and no override path for any role

**Checkpoint**: User Story 1 is fully functional and independently testable — the gate blocks bad
PRs. This is the MVP.

---

## Phase 4: User Story 2 - Passing gate surfaces quality metrics on the PR (Priority: P2)

**Goal**: Coverage, duplication, and code-smell metrics are visible directly on a passing PR.

**Independent Test**: Merge-eligible PR from US1's passing scenario shows coverage %, duplication
%, and new code-smell count on the PR itself, without navigating to the SonarQube server.

### Implementation for User Story 2

- [ ] ⛔ T015 [US2] Enable SonarQube's GitHub PR decoration for this project (native GitHub
      integration or Community Branch Plugin, per SonarQube edition in use) so analysis results
      post automatically to the PR, per `research.md` Decision 6
- [ ] ⛔ T016 [US2] Confirm the Sonar project's quality-gate conditions read the coverage inputs
      wired in T009 (non-zero, correct coverage/duplication numbers appear, not defaults from an
      unconfigured project)
- [ ] ⛔ T017 [US2] Validate `quickstart.md` Scenario 3 end-to-end: metrics visible on a passing PR;
      pushing a new commit updates the displayed metrics to the latest analysis

**Checkpoint**: User Stories 1 AND 2 both work independently — the gate blocks bad PRs and shows
metrics on good ones.

---

## Phase 5: User Story 3 - Coverage gap is fixed and the gate re-evaluates (Priority: P3)

**Goal**: Pushing a fix to a blocked PR automatically re-runs the pipeline and lifts the block, with
no manual intervention.

**Independent Test**: Starting from a PR blocked by a coverage gap, push a commit that restores
coverage; confirm the full sequence re-runs automatically and the merge block lifts once it passes.

### Implementation for User Story 3

- [ ] ⛔ T018 [US3] Confirm the GitHub Branch Source plugin's PR trigger includes the `synchronize`
      event (new commits to an open PR), not only PR-open, so T012's Multibranch job re-triggers
      automatically on every push (FR-007)
- [ ] ⛔ T019 [US3] Validate `quickstart.md` Scenario 4 end-to-end: a PR blocked in US1 becomes
      mergeable after one automatic re-run following a coverage fix, with no manual admin action

**Checkpoint**: All three user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Governance, edge-case validation, and closing the scope boundary noted in `plan.md`

- [X] T020 [P] Author `docs/adr/0012-ci-quality-gate-enforcement.md` recording the Multibranch
      Pipeline + required-status-checks-with-no-bypass approach (`research.md` Decisions 1, 2, 5),
      per the constitution's Governance rule and `plan.md`'s Constitution Check action item
- [ ] ⛔ T021 [P] Validate `quickstart.md` Scenario 5: a temporarily unreachable SonarQube server
      causes `ci/sonarqube-quality-gate` to fail via the `timeout()` from T010, not to pass or be
      skipped (FR-008)
- [ ] ⛔ T022 Run the full `quickstart.md` Success Criteria checklist (SC-001 through SC-005)
      end-to-end and record the results
- [X] T023 Record the constitution's container-image-vulnerability-scan stage as explicit tracked
      follow-up work (new ticket reference alongside `plan.md`'s Complexity Tracking entry), since
      it is out of scope for this feature but required for full Development Workflow compliance

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3-5)**: All depend on Foundational phase completion
  - US1 has no dependency on US2/US3
  - US2 depends on US1's `Jenkinsfile` Sonar stage and Multibranch job existing (T010, T012) to
    have anything to decorate, but does not require US1's branch-protection task (T013)
  - US3 depends on US1's Multibranch job (T012) and branch protection (T013) existing, since it
    validates the *blocked-then-unblocked* behavior those tasks create
- **Polish (Phase 6)**: Depends on US1 (MVP) at minimum; T021/T022/T023 assume all three stories
  are complete for a full validation pass

### User Story Dependencies

- **User Story 1 (P1)**: Start after Foundational (Phase 2) — no dependency on other stories
- **User Story 2 (P2)**: Start after US1's T010/T012 exist — adds visibility on top of US1's gate
- **User Story 3 (P3)**: Start after US1's T012/T013 exist — validates re-trigger behavior of US1's
  wiring

### Within Each User Story

- Configuration tasks (Jenkins/GitHub/SonarQube settings) before their validation task
- Validation task last in each phase, per `quickstart.md`

### Parallel Opportunities

- T002, T003, T004 (Phase 1) can run in parallel — different files/systems, no shared dependency
- T009 (Phase 2) can run in parallel with T005-T008 once `sonar-project.properties` exists from
  T002 — T005-T008 all edit `Jenkinsfile` sequentially and are NOT parallel with each other
- T020 and T021 (Phase 6) can run in parallel — different files/no shared state

---

## Parallel Example: Phase 1 (Setup)

```bash
# Launch Setup's independent tasks together:
Task: "Create sonar-project.properties at repo root with sonar.projectKey, sonar.sources, sonar.exclusions"
Task: "Verify/install required Jenkins plugins per quickstart.md Prerequisites"
Task: "Add @vitest/coverage-v8 to frontend/package.json devDependencies if missing"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Run `quickstart.md` Scenarios 1 and 2 against a real PR
5. At this point the gate is live and blocking — deploy/announce to the team

### Incremental Delivery

1. Complete Setup + Foundational → pipeline runs all four non-Sonar stages in order
2. Add User Story 1 → gate blocks bad PRs → this is the MVP the ticket asks for
3. Add User Story 2 → metrics become visible on the PR → no behavior change to blocking
4. Add User Story 3 → confirm auto re-trigger (mostly validation of existing Multibranch behavior)
5. Polish → ADR, edge-case validation, and the tracked vulnerability-scan follow-up

### Solo/Small-Team Strategy

Given this is infrastructure wiring on one `Jenkinsfile` and one set of GitHub/Jenkins/SonarQube
settings (not parallelizable application code across a team), the realistic path is sequential:
Setup → Foundational → US1 → US2 → US3 → Polish, in that order, by whoever owns CI for this repo.

---

## Notes

- [P] tasks = different files/systems, no dependency ordering required
- [Story] label maps task to specific user story for traceability
- No test-code tasks exist because this feature has no application code — `quickstart.md`
  Scenarios 1–5 are the acceptance tests, referenced directly from each phase's validation task
- Commit after each task or logical group (e.g., after each `Jenkinsfile` stage addition)
- Stop at the Phase 3 checkpoint to validate the MVP independently before continuing

---

## Remaining work (after the 2026-08-22 implementation pass)

Every repository artifact this feature calls for exists and is in place:

| Artifact | Purpose |
|---|---|
| `Jenkinsfile` | Five ordered stages, each publishing its contracted `ci/*` GitHub check |
| `sonar-project.properties` | Single source of truth for scanner settings and coverage inputs |
| `scripts/ci/sonar-begin.sh` | Translates that properties file into SonarScanner-for-.NET arguments |
| `scripts/ci/run-dotnet-tests.sh` | Discovers and runs the unit / integration / contract tiers |
| `scripts/ci/merge-coverage.sh` | Merges per-project Cobertura reports into the one file Sonar reads |
| `scripts/ci/setup-branch-protection.sh` | The one-time `gh api` call that makes the checks blocking |
| `frontend/apps/web/vitest.config.ts`, `frontend/turbo.json` | lcov coverage output, preserved across Turbo cache hits |
| `.config/dotnet-tools.json` | Pins `dotnet-sonarscanner` 11.2.1 and `dotnet-coverage` 18.10.0 |
| `docs/adr/0012-ci-quality-gate-enforcement.md` | The governance record for the enforcement approach |

What is verified: the solution builds in Release; all 12 unit-tier projects (138 tests) pass through
`run-dotnet-tests.sh` and emit Cobertura; `merge-coverage.sh` produces a single populated report;
the frontend suite (49 tests) emits `lcov.info`; `sonar-begin.sh` produces the correct scanner
argument list; the `Jenkinsfile` parses as valid Groovy.

What is not, and cannot be, verified from the repository: everything that needs the Jenkins
controller, the SonarQube server, or the GitHub repository's admin settings. Those are the
⛔ tasks above, and they reduce to four administrator actions plus the validation runs that
follow them:

1. **Jenkins** (T003, T011, T012) — install the GitHub Branch Source, GitHub Checks, and SonarQube
   Scanner plugins; add a SonarQube server connection named `sonarqube` (matching
   `SONARQUBE_SERVER` in the `Jenkinsfile`) with the webhook back to Jenkins that
   `waitForQualityGate()` waits on; create the Multibranch Pipeline job for this repository.
   Confirm its PR trigger includes the `synchronize` event (T018), which is the default.
2. **GitHub** (T013) — run `scripts/ci/setup-branch-protection.sh nmhieuit/ecommerce master`.
3. **SonarQube** (T015, T016) — enable GitHub PR decoration for the `ecommerce` project and confirm
   its quality-gate conditions read the coverage inputs wired in `sonar-project.properties`.
4. **Validation** (T014, T017, T019, T021, T022) — work through `quickstart.md` Scenarios 1-5 and
   the SC-001..SC-005 checklist against real pull requests.

One repository-side item also remains open: ADR-0012's Action Item 4 carries a `SCRUM-TBD`
placeholder for the container image vulnerability scan (T023). Raising that backlog ticket and
replacing the placeholder closes the tracking obligation on `plan.md`'s Complexity Tracking entry.
