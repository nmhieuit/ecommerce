# Research: SonarQube Quality Gate as a Merge Blocker

## Context established by repo inspection

- No `Jenkinsfile` exists anywhere in the repository today. The constitution and
  `docs/tech-stack-decisions.md` both name Jenkins + SonarQube as the fixed CI/CD and quality-gate
  tooling, and ADRs 0001/0008/0010 refer to "the Jenkins pipeline" as an assumed entity, but none of
  them created it. This feature is therefore the first to stand up an actual CI pipeline, not just
  bolt Sonar onto an existing one.
- No `sonar-project.properties`, no SonarQube server/project reference, and no coverage-aggregation
  step exist yet.
- Backend: `Ecommerce.slnx` (.NET 10) with per-service `*.Api.UnitTests`, `*.Api.IntegrationTests`
  (Testcontainers-based, per `010-testcontainers-integration-tests`), and `*.Api.ContractTests`
  (Pact, per `011-consumer-contract-tests`) for `baskets`, `bff`, `orders`, `products`; `gateway` and
  `parties` currently have unit + integration tests but no contract tests. `coverlet.collector` is
  already a package reference across test projects (confirmed in `Directory.Packages.props` and
  multiple `.csproj` files), so Cobertura/OpenCover-format coverage output is already obtainable via
  `dotnet test --collect:"XPlat Code Coverage"` without adding a new package.
- Frontend: pnpm + Turborepo monorepo under `frontend/`, with a `turbo run test` script already
  defined (Vitest, per the constitution's Principle IX and ADR 0010). Vitest's built-in `--coverage`
  (via `@vitest/coverage-v8`) can emit `lcov`, which SonarQube's JS/TS analyzer consumes natively.
- No `.github/` directory, no GitHub Actions workflows, no branch-protection-as-code
  (Terraform/other IaC) exist. The repository has no remote configured in this environment; the
  user confirmed (via clarification) that GitHub is the PR host for this feature.
- No CODEOWNERS file exists at the repo root (constitution's PR review rule for
  contract/authorization/tenant-path changes is currently unenforced by tooling — out of scope for
  this feature, noted only as a related gap).

## Decisions

### Decision 1: Pipeline orchestrator and trigger model

**Decision**: A repository-root declarative `Jenkinsfile` runs as a Jenkins **Multibranch
Pipeline** job connected to the GitHub repository via the GitHub Branch Source plugin, so PR builds
trigger automatically and Jenkins reports per-stage results back to GitHub via commit statuses /
checks (Decision 2).

**Rationale**: Multibranch + GitHub Branch Source is the standard Jenkins-GitHub integration and
requires no custom webhook receiver code; it auto-discovers PR branches and reports build status
without bespoke scripting, matching Principle I/VI's preference for standard, deny-by-default
platform mechanisms over hand-rolled infrastructure.

**Alternatives considered**: A generic webhook + freestyle job was rejected — it would require
hand-written status-reporting logic that the GitHub Branch Source plugin already provides, adding
unjustified complexity for no behavioral gain.

### Decision 2: How Jenkins reports per-stage pass/fail to the PR (FR-001, FR-002, FR-007)

**Decision**: Each of the five stages (build, unit tests, integration tests, contract tests,
SonarQube quality gate) is a distinct top-level `stage {}` block in the declarative `Jenkinsfile`.
The GitHub Branch Source plugin publishes one GitHub commit status/check per stage automatically
(stage-level status publishing enabled via the pipeline's built-in stage view), keyed by a stable
check name (`ci/build`, `ci/unit-tests`, `ci/integration-tests`, `ci/contract-tests`,
`ci/sonarqube-quality-gate`). `post { always { ... } }` blocks are not needed for status reporting
(the plugin handles it from stage results) but are used to publish test result and coverage report
artifacts for visibility.

**Rationale**: Five independently named checks map directly to FR-001 (visible per-stage sequence)
and FR-002 (identify which stage blocked the PR), and each check re-runs on every push (FR-007)
because Jenkins re-triggers the whole pipeline per commit.

**Alternatives considered**: A single "CI" status aggregating all stages was rejected — it would
satisfy FR-001's ordering but not FR-002's requirement to identify *which* stage failed without
someone opening the Jenkins console.

### Decision 3: Enforcing the SonarQube quality gate as a blocking pipeline step (FR-003, FR-008)

**Decision**: The SonarQube stage runs `dotnet sonarscanner begin` / build / `dotnet sonarscanner
end` (backend) plus the frontend scan inputs in one combined Sonar Scanner invocation against a
single Sonar project, then calls the Jenkins **SonarQube Scanner plugin**'s `waitForQualityGate()`
step inside a `timeout()` block. `waitForQualityGate()` blocks the stage until SonarQube's webhook
reports the gate result and returns a non-`OK` status as a Groovy value the pipeline checks
explicitly to fail the stage (`error(...)`) rather than relying on scanner exit code alone.

**Rationale**: The Sonar Scanner CLI returns success even when the quality gate itself will later
fail (analysis upload succeeding is not the same as the gate passing) — `waitForQualityGate()` is
the documented mechanism for making the *gate result itself*, not just the scan, a blocking pipeline
outcome. Wrapping it in `timeout()` directly satisfies FR-008: an unreachable/slow SonarQube server
causes the timeout to fire, which fails the stage (blocking), rather than the pipeline hanging
forever or silently passing.

**Alternatives considered**: Relying solely on the scanner CLI's own exit code was rejected because
quality gate computation happens asynchronously on the SonarQube server after upload; polling the
Sonar Web API directly in a shell script was rejected as unjustified custom scripting when the
Jenkins plugin step already does this correctly.

### Decision 4: Coverage input to the gate across two stacks (FR-003, FR-006)

**Decision**: Backend `dotnet test` runs collect Cobertura-format coverage per test project;
results are merged with `dotnet-coverage merge` (or `reportgenerator`) into one Cobertura file
consumed via `sonar.cs.cobertura.reportsPaths`. Frontend `turbo run test -- --coverage` emits
`lcov.info` per package, referenced via `sonar.javascript.lcov.reportPaths` /
`sonar.typescript.lcov.reportPaths`. Both feed one Sonar project covering the whole monorepo so a
single quality-gate result (FR-003) reflects both stacks, and the gate's own PR decoration (SonarQq
GitHub integration, see Decision 6) surfaces coverage/duplication/code-smell numbers on the PR
(FR-006) without custom reporting code.

**Rationale**: One combined project keeps FR-003's "single quality-gate status per PR head commit"
literally true; per-service Sonar projects would produce multiple gate results and reintroduce the
ambiguity the ticket is trying to remove.

**Alternatives considered**: Separate Sonar projects per service were rejected for the reason above;
a fully custom PR-comment bot posting metrics was rejected in favor of the built-in Sonar-to-GitHub
PR decoration integration, which needs configuration, not new code.

### Decision 5: GitHub branch protection and the "no override" requirement (FR-004, FR-005)

**Decision**: Branch protection on the protected branch(es) is configured (one-time, via the GitHub
UI or a documented `gh api` invocation run by a repo admin — not new pipeline code) with: all five
Jenkins-reported checks added as **required status checks**, "Require branches to be up to date
before merging" enabled, and **"Do not allow bypassing the above settings"** (the setting that
also blocks administrators) enabled. This is configuration, not application code, and is documented
in `quickstart.md` as a setup step rather than automated by this feature, since introducing new
IaC tooling (e.g., Terraform for GitHub) purely to manage one repository's branch protection would
be disproportionate infrastructure for the problem (Principle I: complexity proportional to the
domain).

**Rationale**: GitHub's own "do not allow bypassing" flag is a native, audited mechanism that
satisfies "no override path" (FR-004) without any custom enforcement code; GitHub's organization
audit log already records any settings change that would re-enable bypass, satisfying FR-005's
audit fallback without bespoke logging.

**Alternatives considered**: A custom GitHub Actions/App-based merge-check bot re-implementing
"required checks" was rejected as duplicating a first-class GitHub feature.

### Decision 6: Metrics visibility on the PR (FR-006)

**Decision**: Use SonarQube's built-in GitHub PR decoration (Community Branch Plugin or SonarQube's
native GitHub integration, depending on the SonarQube edition already licensed) to post the
coverage/duplication/new-code-smell summary as a PR check annotation/comment automatically once
analysis completes.

**Rationale**: This is configuration on the existing SonarQube server pointing at the GitHub App/PAT
for this repo, not new code — it directly satisfies FR-006 without building and maintaining a
custom reporting step.

**Alternatives considered**: A custom Jenkins post-build step calling the GitHub Checks API to post
a formatted metrics comment was considered as a fallback if the installed SonarQube edition lacks
native PR decoration; deferred to the implementation phase to confirm edition/licensing, and noted
as a fallback rather than the primary approach.

## Outstanding items intentionally deferred (not NEEDS CLARIFICATION — out of scope for this ticket)

- The constitution's PR gate also names a **container image vulnerability scan** stage after the
  SonarQube gate. SCRUM-22's own acceptance criteria stop at the SonarQube gate; the vulnerability
  scan is treated as separate, not-yet-scoped follow-up work (tracked as a scope boundary in
  `plan.md`'s Constitution Check), not something this feature silently drops.
- Populating `gateway` and `parties` with contract-test projects is unrelated to wiring the gate
  itself — the pipeline's contract-tests stage runs whatever contract-test projects exist today
  (four services) and will pick up more automatically as they're added by other work.
- Exact SonarQube quality-profile thresholds (coverage %, duplication %) are a SonarQube
  configuration matter for whoever administers the SonarQube server/project, not a pipeline-code
  decision this feature makes.
