# ADR-0012: CI Quality Gate Enforcement

**Status:** Accepted
**Date:** 2026-08-22
**Deciders:** Platform maintainers

## Context

The constitution's Development Workflow section requires every pull request to pass build → unit
tests → integration tests (Testcontainers) → contract tests → SonarQube quality gate → container
image vulnerability scan, "no exceptions, overrides, or waivers", and Principle III names the
SonarQube quality gate as the coverage authority that must pass before merge.

Until this decision, none of that existed as running software. ADRs 0001, 0008, and 0010 refer to
"the Jenkins pipeline" as an assumed entity, but no `Jenkinsfile`, no SonarQube configuration, and
no branch protection had ever been created — the gate was a written rule with nothing enforcing it.
This ADR covers standing up the first actual CI pipeline for the repository and, more consequentially,
choosing the mechanism by which its results become a *merge blocker* rather than advice.

Two things needed deciding: how the pipeline runs and reports, and what makes a failing result
actually stop a merge for everyone, including the people who administer the repository.

## Decision

**A repository-root declarative `Jenkinsfile` runs as a Jenkins Multibranch Pipeline job, publishing
one named GitHub check per stage; GitHub branch protection lists those check names as required
status checks with `enforce_admins` enabled.**

Concretely:

- Five stages publish the checks `ci/build`, `ci/unit-tests`, `ci/integration-tests`,
  `ci/contract-tests`, and `ci/sonarqube-quality-gate`. The names are published explicitly via the
  GitHub Checks plugin rather than inherited from Jenkins' stage names, because branch protection
  matches on the exact string.
- The SonarQube stage calls `waitForQualityGate()` inside a `timeout()` and turns any non-`OK`
  status — and the timeout itself — into a stage failure. The scanner CLI exits successfully once
  analysis is *uploaded*; the gate is computed asynchronously afterwards, so the scanner's exit code
  alone would let a failing gate through.
- Branch protection is applied by a one-time administrator action, scripted as
  `scripts/ci/setup-branch-protection.sh`, not managed as infrastructure-as-code.

## Options Considered

### Option A: Multibranch Pipeline + required status checks with admin bypass disabled (chosen)

| Dimension | Assessment |
|---|---|
| Complexity | Low — one `Jenkinsfile`, one job, one settings change |
| New code to maintain | None beyond the pipeline itself |
| Enforcement strength | Absolute — GitHub disables the merge control for every role |
| Auditability | GitHub's own organization audit log records any change to the setting |

**Pros:** Uses first-class, audited platform features end to end; the GitHub Branch Source plugin
auto-discovers pull-request branches and re-triggers on every push with no webhook code of our own;
`enforce_admins` is the one switch that literally removes the override path the constitution
forbids. **Cons:** The enforcement lives in repository settings rather than in the repository, so
it is invisible to `git log` — a reviewer cannot see that it is still in place by reading a diff.

### Option B: Pipeline + branch protection managed as infrastructure-as-code (Terraform GitHub provider)

| Dimension | Assessment |
|---|---|
| Complexity | Medium-High — a new IaC stack, state backend, and apply pipeline |
| New code to maintain | A Terraform configuration and its credentials |
| Enforcement strength | Identical to Option A (it configures the same setting) |
| Auditability | Better — settings changes appear as reviewable diffs |

**Pros:** Closes exactly the weakness of Option A: protection settings become a reviewable, version
controlled artifact, and drift is detectable by a plan run. **Cons:** Introduces a whole IaC
toolchain, its state storage, and its credential handling to manage the settings of a single
repository — disproportionate infrastructure for the problem (Principle I), and the new stack would
itself need a pipeline and an owner.

### Option C: A custom merge-check bot (GitHub App or Actions workflow) enforcing the rule in code

| Dimension | Assessment |
|---|---|
| Complexity | High — a service to write, host, secure, and monitor |
| New code to maintain | A bot plus its credentials and availability story |
| Enforcement strength | Weaker — a bot that is down either blocks everything or nothing |
| Auditability | Only what the bot itself chooses to log |

**Pros:** Enforcement logic is visible in the repository, and arbitrary rules are expressible.
**Cons:** Re-implements required status checks, a feature GitHub already provides correctly; adds a
component whose own outage becomes a merge outage; and its bypass path is whatever its code and
token permissions happen to allow, which is a weaker guarantee than a platform setting.

## Trade-off Analysis

The decisive question is not which option enforces the rule — all three configure or replicate the
same block — but what each costs to keep trustworthy. Option C loses outright: writing a service to
duplicate an existing platform feature adds an outage mode and a subtler bypass surface than the
one it replaces.

Between A and B, the real trade is Option A's invisibility against Option B's overhead. Option B's
advantage is genuine: a settings change that quietly re-enables bypass is the one failure this
feature cannot detect from inside the repository. It is accepted because GitHub's organization
audit log already records that change (spec FR-005 asks for audit as the fallback, not for
prevention by a second mechanism), and because an IaC stack introduced for one repository's
settings would need more governance than it provides. If protection settings are ever needed across
many repositories, this decision should be revisited as an amendment rather than by hand-managing
each one.

A smaller but sharp trade-off sits inside Option A: SonarScanner for .NET takes settings as `/d:`
command-line arguments and, unlike the generic CLI scanner, does not read `sonar-project.properties`.
Splitting scanner settings between a properties file and the `Jenkinsfile` would leave two places to
change one thing, so `scripts/ci/sonar-begin.sh` translates the committed properties file into
scanner arguments and the properties file stays the single source of truth.

## Consequences

- **Stage names are a contract, not a label.** Renaming a stage's check in the `Jenkinsfile` without
  updating branch protection silently removes that stage from enforcement — a failure that looks
  like a green PR. The names are listed in
  `specs/012-sonarqube-quality-gate/contracts/pipeline-stage-contract.md` §1 and must change in both
  places in the same pull request.
- **The Jenkins agent gains hard requirements**: the .NET 10 SDK, Node 22 with corepack, and a
  reachable Docker daemon, because the integration tier runs Testcontainers (spec 010).
- **The quality gate fails closed.** An unreachable or slow SonarQube server times out and fails the
  check rather than passing or skipping it, so SonarQube downtime blocks merges. That is the
  intended direction of failure, and it makes SonarQube availability a merge-path dependency.
- **Test discovery is on-disk, not enumerated.** `scripts/ci/run-dotnet-tests.sh` classifies every
  `*Tests.csproj` into a tier by name, so a new service's suites join CI without editing the
  pipeline. A test project named outside the convention runs in the unit tier by default.
- **Branch protection is not visible in the repository.** Confirming the gate is still in force means
  reading the GitHub settings or the audit log, not the diff.

## Action Items

1. [ ] Run `scripts/ci/setup-branch-protection.sh <owner/repo> <branch>` as a repository administrator
       and confirm all five checks are required with `enforce_admins` enabled
2. [ ] Create the Jenkins Multibranch Pipeline job and the SonarQube server connection (with the
       webhook back to Jenkins that `waitForQualityGate()` depends on), per
       `specs/012-sonarqube-quality-gate/quickstart.md` Prerequisites
3. [ ] Validate the five scenarios in `specs/012-sonarqube-quality-gate/quickstart.md` against real
       pull requests, including the fail-closed behaviour when SonarQube is unreachable
4. [ ] SCRUM-TBD: add the constitution's container image vulnerability scan as a sixth stage and a
       sixth required check; until then this pipeline implements five of the six mandated gates
       (see `specs/012-sonarqube-quality-gate/plan.md` Complexity Tracking)

## Amendment (2026-08-23): Analysis backend decision (FR-005, FR-009, FR-014)

`spec.md` was extended to fold in a previously separate decision — which analysis backend the
`ci/sonarqube-quality-gate` check above actually reports to — because a gate wired to nothing is not
yet enforcing anything. This section is that decision record.

**Decision: self-hosted SonarQube (Community Edition), not SonarCloud.**

| Dimension | Self-hosted SonarQube | SonarCloud (SaaS) |
|---|---|---|
| Recurring cost | None (Community Edition) beyond hosting compute | Paid tier required for a **private** repository |
| Hosting/maintenance burden | Platform owns upgrades, backups, and uptime — consistent with every other component in this repo, which runs self-hosted on Kubernetes via Ansible | None — vendor-operated |
| Source metadata exposure | Stays inside the internal network | Source metadata leaves the internal network to a third party |
| GitHub PR decoration | **Not included** in Community Edition; requires the community-maintained Branch Plugin (unofficial, reinstalled on every SonarQube upgrade) or a paid Developer Edition license | Included natively at every tier |

**Rationale**: `nmhieuit/ecommerce` is a private repository, so SonarCloud's no-cost tier does not
apply — the recurring SaaS cost was the deciding factor against it, on a platform whose stated
pattern (constitution, ADRs 0001/0008/0010) is self-hosting everything. The trade-off accepted in
exchange is licensing: Community Edition has no official PR decoration, so this pipeline either
runs the community Branch Plugin or accepts that metrics live on the SonarQube server without being
decorated onto the PR until that plugin is installed and verified (FR-009). This is recorded here
rather than assumed silently, per FR-009.

**Provisioning status as of 2026-08-23**: no self-hosted SonarQube instance existed anywhere for
this repository before this date. A local instance (SonarQube Community Edition + Jenkins LTS) was
stood up in Docker Desktop via `docker-compose.ci.yml` at the repository root, to unblock wiring the
Jenkins↔SonarQube connection and the GitHub credential/webhook configuration described in
`quickstart.md`. This is a **development instance**, not the production deployment implied by the
spec's Edge Cases (a Kubernetes workload with its own database, provisioned via the platform's
existing Ansible pattern) — standing up that production instance remains a separate follow-up
tracked against FR-006. The local instance is sufficient to validate the Jenkins↔SonarQube↔GitHub
wiring end-to-end; it is not itself the "provisioned instance" FR-006 describes for production use.
