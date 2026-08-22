# Contract: CI Pipeline Stages ↔ GitHub Branch Protection ↔ SonarQube

This feature's "external interface" is not an HTTP API — it is the set of names and paths that
Jenkins, GitHub branch protection, and SonarQube must agree on for the gate to work. Any future
change to stage names, or any new service onboarding to the pipeline, MUST preserve this contract
or update it (and the branch protection configuration) in the same change.

## 1. Required status check names (Jenkins → GitHub)

GitHub branch protection's "required status checks" list references these exact check names,
published by the `Jenkinsfile`'s five top-level stages (research.md Decision 2):

| Stage | Required check name | Fails the PR when |
|---|---|---|
| Build | `ci/build` | Solution/monorepo build fails |
| Unit tests | `ci/unit-tests` | Any `*.Api.UnitTests` project (or `turbo run test` unit suite) fails |
| Integration tests | `ci/integration-tests` | Any `*.Api.IntegrationTests` project fails (Testcontainers) |
| Contract tests | `ci/contract-tests` | Any `*.Api.ContractTests` project fails (Pact) |
| Quality gate | `ci/sonarqube-quality-gate` | SonarQube quality gate result is not `OK`, or the gate wait times out (FR-008) |

**Consumers of this table**: whoever configures GitHub branch protection (research.md Decision 5)
must list all five names above as required checks with "do not allow bypassing" enabled. Renaming a
stage in the `Jenkinsfile` without updating branch protection silently removes that stage from
enforcement — this is the failure mode this contract exists to prevent.

## 2. Coverage report contract (per-service test project → SonarQube)

For a backend test project to be included in the coverage number the quality gate evaluates, it
MUST:

- Be named `<Service>.Api.{UnitTests,IntegrationTests,ContractTests}` (existing convention, already
  followed by `baskets`, `bff`, `orders`, `parties`, `products`, `gateway`).
- Reference `coverlet.collector` (already a package reference repo-wide) and run under
  `dotnet test --collect:"XPlat Code Coverage"`, producing a Cobertura XML file consumed after
  merge via `sonar.cs.cobertura.reportsPaths` (research.md Decision 4).

For a frontend package to be included, it MUST expose a `test` Turbo task that supports
`--coverage` and emit `lcov.info`, consumed via `sonar.javascript.lcov.reportPaths` /
`sonar.typescript.lcov.reportPaths`.

A service or package that does not meet this contract is simply absent from the coverage
computation — it does not fail the build, but its code is not protected by the gate, which is
itself an onboarding gap worth flagging (not something this feature needs to force).

## 3. Quality gate wait contract (Jenkins ↔ SonarQube)

- Sonar analysis for a PR MUST be tagged with the PR's branch/commit so SonarQube's own PR
  decoration (research.md Decision 6) attaches to the correct GitHub PR.
- The pipeline MUST call `waitForQualityGate()` (or equivalent) after submitting analysis and MUST
  treat any non-`OK` status, and any timeout waiting for the SonarQube webhook, as a stage failure
  (FR-008) — never as success-by-default or skipped.

## 4. Audit fallback contract (GitHub)

No custom audit endpoint is introduced. The audit fallback (FR-005) is GitHub's own
organization/repository audit log for branch-protection setting changes, and GitHub's native
rejection of a merge attempt against a failing/pending required check. This feature's only
obligation to that contract is ensuring branch protection is actually configured as described in
section 1 — GitHub owns the rest.
