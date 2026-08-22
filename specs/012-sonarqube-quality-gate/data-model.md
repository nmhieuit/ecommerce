# Data Model: SonarQube Quality Gate as a Merge Blocker

No application database or schema is introduced by this feature (spec Assumption: this wires an
existing/standard CI toolchain, it does not add domain persistence). The entities named in
`spec.md`'s Key Entities section are conceptual — each maps to a concrete record already owned by
an existing system, not a new table this feature creates or migrates.

## Entities

### Pipeline Run

- **What it represents**: One execution of the build → unit tests → integration tests → contract
  tests → SonarQube stage sequence for a specific PR commit (spec Key Entities).
- **Physical home**: A Jenkins Multibranch Pipeline build (`Jenkinsfile` run). Its per-stage results
  are the build's stage view; no separate storage is created.
- **Key attributes**: commit SHA, branch/PR number, per-stage status (pending/success/failure),
  overall status, start/end timestamps, links to test result and coverage artifacts.
- **Relationships**: One Pipeline Run produces exactly one Quality Gate Result (if it reaches the
  SonarQube stage) and contributes to the current Merge Block state of its PR.

### Quality Gate Result

- **What it represents**: The SonarQube outcome (pass/fail) for a given analysis, with coverage,
  duplication, and new-issue metrics evaluated against the configured quality profile (spec Key
  Entities; FR-003, FR-006).
- **Physical home**: A SonarQube analysis/quality-gate record on the SonarQube server, referenced
  by the Sonar project + analysis ID. Retrieved by the pipeline via the Jenkins SonarQube Scanner
  plugin's `waitForQualityGate()` step (research.md Decision 3) and by reviewers via SonarQube's
  GitHub PR decoration (research.md Decision 6).
- **Key attributes**: gate status (OK/ERROR), coverage %, duplicated lines %, new blocker/critical
  issue counts, analysis ID, project key.
- **Relationships**: Belongs to exactly one Pipeline Run; its status is the deciding factor (along
  with earlier stage results) for that PR's Merge Block state.

### Merge Block

- **What it represents**: The state attached to a PR that prevents merging while its latest
  Pipeline Run has a failing stage (including a failing Quality Gate Result), and that clears once a
  passing run completes for the current head commit (spec Key Entities; FR-004, FR-007).
- **Physical home**: GitHub branch protection's required-status-checks mechanism, evaluated per PR
  head commit from the five checks Jenkins reports (research.md Decisions 2 and 5). There is no
  separate "Merge Block" record to persist — it is derived state GitHub computes from check results.
- **Key attributes**: derived, not stored: is-mergeable (boolean), list of required checks and each
  one's latest state.
- **Relationships**: Computed from the most recent Pipeline Run's per-stage / Quality Gate Result
  for the PR's current head commit.

### Override Attempt (audit-only)

- **What it represents**: A record of any attempt to merge a PR while blocked (spec Key Entities;
  FR-005), retained only as a fallback since GitHub's "do not allow bypassing" setting is expected
  to prevent the action outright rather than merely log it.
- **Physical home**: GitHub's organization/repository audit log (for changes to branch protection
  settings that could reintroduce a bypass) and GitHub's own merge-attempt rejection response (for
  an attempted merge while a required check is failing/pending). No custom audit table is created by
  this feature.
- **Key attributes**: actor, PR, timestamp, action attempted, outcome (rejected vs. settings
  changed).
- **Relationships**: References a PR and, where applicable, the Merge Block state active at the time
  of the attempt.

## Retention (FR-010)

Pipeline Run and Quality Gate Result data already persist for the retention period of the Jenkins
build history and SonarQube's own analysis history respectively — both exceed "at least as long as
the PR remains open" by default. No new retention mechanism is required; only build/analysis
history retention settings need to not be shorter than that on both servers (an operational
configuration check during implementation, not new code).
