# Connecting GitHub, Jenkins, and SonarQube for the Quality Gate

**Status: 🟢 fully wired, verified, and enforcing** — see
[specs/013-sonarqube-merge-blocker/tasks.md](../specs/013-sonarqube-merge-blocker/tasks.md) for the
complete verification trail (every claim below is backed by a real PR, a real build log, or a real
GitHub API response — no simulated/stubbed evidence).

**Originally written**: 2026-08-23 · **Last verified**: 2026-09-01
**Feature**: [specs/012-sonarqube-quality-gate](../specs/012-sonarqube-quality-gate/spec.md),
[specs/013-sonarqube-merge-blocker](../specs/013-sonarqube-merge-blocker/spec.md)
**Repository**: `github.com/nmhieuit/ecommerce` (public, default branch `master`)

This document originally recorded a session that stood up Jenkins and SonarQube locally but stopped
short of finishing the wiring, because the remaining steps required pasting real secrets into forms
— something an automated session must never do. Everything below that section has since been
completed by hand and independently verified end-to-end. The historical sections are kept as-is;
skip to **"Current status"** for what's actually true today.

## Current status (2026-09-01)

Everything the original wiring targeted is live and has been proven against the real GitHub API and
real Jenkins builds, not just configured and assumed to work:

- **Jenkins ↔ GitHub, live**: the `ecommerce` Multibranch Pipeline job discovers branches and pull
  requests automatically via periodic branch indexing — no manual "Scan Now" is ever needed. Pushing
  a commit to an open PR auto-triggers a fresh build; opening a PR from a branch already known to
  Jenkins converts the branch job into a `PR-<n>` job automatically. Verified across dozens of real
  builds in specs 013 T014/T015, including on same-repo PRs
  [#6](https://github.com/nmhieuit/ecommerce/pull/6) and
  [#9](https://github.com/nmhieuit/ecommerce/pull/9).
- **Branch protection, live and unbypassable**: `master` requires all five checks
  (`ci/build`, `ci/unit-tests`, `ci/integration-tests`, `ci/contract-tests`,
  `ci/sonarqube-quality-gate`) with bypass disabled for every role, including the repo owner.
  Verified on [PR #3](https://github.com/nmhieuit/ecommerce/pull/3): a genuinely failing check
  disabled the merge button with no override path visible anywhere in the UI, logged in while
  authenticated as the repo owner.
- **The gate genuinely blocks and unblocks merges**: a passing PR shows
  "Ready to merge" the moment all five checks are green
  ([PR #2](https://github.com/nmhieuit/ecommerce/pull/2)); a PR that was blocked and then fixed
  auto-reruns the full pipeline and auto-unblocks the instant the real SonarQube gate passes, with
  zero manual intervention beyond pushing the fix commit
  ([PR #6](https://github.com/nmhieuit/ecommerce/pull/6),
  [PR #9](https://github.com/nmhieuit/ecommerce/pull/9)).
- **Fails closed, not open**: pointing the pipeline's SonarQube URL at an unreachable address makes
  `ci/sonarqube-quality-gate` fail after exactly the configured timeout — never silently pass or get
  skipped.
- **PR decoration, live and current**: SonarQube's Community Branch Plugin posts a real comment
  (Quality Gate badge, Issues, Measures, coverage/duplication estimates, a link back into
  SonarQube) on every analyzed PR, using the same PAT already configured for `githubNotify` — no
  extra permissions needed. The comment is replaced (not left stale) on every new commit; confirmed
  the coverage estimate actually changes between builds on
  [PR #9](https://github.com/nmhieuit/ecommerce/pull/9) (79.30% → 79.40%).
- **Audit trail, confirmed sufficient without new code**: see
  [contracts/pipeline-stage-contract.md §4](../specs/013-sonarqube-merge-blocker/contracts/pipeline-stage-contract.md)
  for how GitHub's own security log plus per-commit check-status history answer "who changed branch
  protection, and what did the quality gate say at the time of every merge" — no custom audit
  endpoint was built or is needed.

### Gotchas found getting here that aren't obvious from the steps below

The manual setup steps in this document get Jenkins and SonarQube *talking*; they do not by
themselves make Testcontainers-based tests or PR decoration actually work. Six additional fixes
were required, none of them optional:

1. **SonarQube Community Branch Plugin needs a Java agent flag on two components, not just the
   plugin jar installed.** Without `SONAR_WEB_JAVAADDITIONALOPTS` and `SONAR_CE_JAVAADDITIONALOPTS`
   (see `docker-compose.ci.yml`), SonarQube aborts startup entirely with "Fail to load plugin
   Community Branch Plugin ... Please check the Java Agent has been correctly set" — first for the
   `web` component, then again for `ce` (Compute Engine, which actually computes gate results) once
   the first is fixed. This is easy to set once and then forget it needs to survive
   `docker compose down`/`up` — it must live in the compose file, not be applied by hand.
2. **A bare `jenkins/jenkins:lts-jdk17` image has none of the tooling the `Jenkinsfile` needs.** No
   .NET SDK, no Node/pnpm, no Docker CLI, no git-across-uids config. `docker/ci/jenkins.Dockerfile`
   builds all of that in; `docker-compose.ci.yml`'s `jenkins` service must `build:` from it, not pull
   the bare image.
3. **corepack's pinned pnpm version must be cached outside `/var/jenkins_home`.** That path is a
   named volume; anything corepack caches there at image-build time (as `root`) is invisible to the
   `jenkins` user at runtime (different `HOME`, and the volume shadows it anyway). Without
   `ENV COREPACK_HOME=/opt/corepack-cache` pointed outside the volume, every `pnpm` invocation
   silently fetches latest instead of the version pinned in `frontend/package.json`.
4. **Testcontainers-based tests (contract and integration tiers) need a real Docker daemon.** The
   Jenkins container needs `/var/run/docker.sock` mounted (docker-outside-of-docker) and the
   `jenkins` user added to the `root` group (the socket is `root:root`, mode 660) — otherwise every
   such test fails with `DockerUnavailableException`. This is a genuine privilege grant (root-
   equivalent control of the Docker host); it should not be applied without deliberately deciding to
   accept that trade-off for a local CI box.
5. **Testcontainers' Ryuk reaper cannot complete its handshake in this setup.** Ryuk needs a reverse
   TCP connection back to the test process, which only works when the test process and the Docker
   daemon share a network namespace. Here they only share the *daemon* (via the mounted socket); Ryuk
   ends up a sibling container on a different network path and every test using Testcontainers fails
   with `ResourceReaperException: Initialization has been cancelled`. Testcontainers' own docs
   recommend disabling it in exactly this sibling-container CI shape:
   `TESTCONTAINERS_RYUK_DISABLED=true`. The trade-off: spun-up containers are no longer
   auto-cleaned on a crashed test run and can accumulate — worth an occasional
   `docker ps --filter ancestor=<image>` sweep.
6. **Testcontainers must be told the real Docker host, not `localhost`.** With the daemon reached
   over a mounted socket, published container ports live on the Docker Desktop VM's network stack,
   not inside the Jenkins container's own loopback — so the default `localhost:<port>` addressing
   Testcontainers-dotnet uses to reach spun-up dependencies (and PactNet's provider-state callbacks)
   just times out. `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal` (Docker Desktop's standing
   DNS name for that VM, reachable from every container on it) fixes this.

All six are already applied in this repository's `docker-compose.ci.yml` and
`docker/ci/jenkins.Dockerfile` — nothing further to do for the currently-running instance. They're
listed here because they're exactly the kind of thing that gets silently lost the next time this
Jenkins container is recreated from a config that's missing one of them, and the failure mode each
time is a confusing, seemingly-unrelated test failure rather than an obvious "config missing" error.

## What was found before any changes were made

- `docker ps -a`, `docker images`, and port probes on 8080/8081/9000/9090 showed **no Jenkins or
  SonarQube container, image, or listening service anywhere on this machine**. Only the
  application's own stack (gateway, bff, orders, baskets, products, sqlserver, redis, rabbitmq,
  otel-collector) existed, all stopped.
- No `docker-compose` file or setup script for Jenkins/SonarQube existed in the repository.
- The repository-root `Jenkinsfile` and `sonar-project.properties` already existed from a prior
  implementation pass — the pipeline code was ready, it had simply never been pointed at a live
  Jenkins or SonarQube.
- `gh` (GitHub CLI) is not installed in this environment, so `scripts/ci/setup-branch-protection.sh`
  could not be run here.
- Direct inspection of `github.com/nmhieuit/ecommerce/settings/branches` and `.../settings/hooks`
  (via an already-authenticated browser session) confirmed: **zero branch protection rules** and
  **zero webhooks** currently configured on the repository.

## What this session did automatically

These steps involved no secrets and no account creation, so they were performed directly.

1. **Added `docker-compose.ci.yml`** at the repository root — a Jenkins LTS (`jenkins/jenkins:lts-jdk17`)
   and SonarQube Community Edition (`sonarqube:community`) container, on a shared `ci-backbone`
   network, each with a named volume for persistence and a health check.
   ```bash
   docker compose -f docker-compose.ci.yml up -d
   ```
2. **Installed Jenkins' three required plugins** — GitHub Branch Source, GitHub Checks, SonarQube
   Scanner — via `jenkins-plugin-cli` inside the container (a credential-free, non-interactive
   install path built into the official Jenkins image), then restarted Jenkins so they load.
   Confirmed present after restart: `github-branch-source.jpi`, `github-checks.jpi`, `sonar.jpi` in
   `/var/jenkins_home/plugins/`. SonarQube confirmed `"status":"UP"` (version `26.8.0.126808`) via
   `curl http://localhost:9000/api/system/status`; Jenkins' login page confirmed reachable
   (HTTP 200) at `http://localhost:8080/login`.
3. **Wrote the missing backend-decision record** required by FR-005/FR-009/FR-014: an "Amendment
   (2026-08-23)" section in
   [`docs/adr/0012-ci-quality-gate-enforcement.md`](adr/0012-ci-quality-gate-enforcement.md)
   naming self-hosted SonarQube Community Edition over SonarCloud, with the cost/hosting/
   PR-decoration comparison the spec requires.
4. **Regenerated [`specs/012-sonarqube-quality-gate/tasks.md`](../specs/012-sonarqube-quality-gate/tasks.md)**
   to match the current `spec.md` (which now has four user stories, the backend-selection decision
   having been folded in as User Story 1) and to record today's real status honestly, per the
   feature's own FR-016.

## Runbook: recreating this from scratch

Every step below has already been done for the currently-running Jenkins/SonarQube instance. Keep
this section as a runbook for the day someone needs to rebuild it (a wiped volume, a new machine) —
each step involves either pasting a real secret into a form (which stays a manual, human action by
design) or a decision only a human should make, so this was never meant to be automated end-to-end.

### 1. Unlock Jenkins and finish its setup wizard

Get the container's initial admin password fresh each time it's recreated (never typed anywhere by
an automated session):

```bash
docker exec ecomerce-ci-jenkins-1 cat /var/jenkins_home/secrets/initialAdminPassword
```

1. Open `http://localhost:8080`.
2. Paste the password above.
3. On the plugin-selection screen, choose **"Select plugins to install"** and select none (or
   "Install suggested plugins" is also fine — the three required ones — GitHub Branch Source, GitHub
   Checks, SonarQube Scanner — are already present either way; this just avoids re-downloading a
   large default set).
4. Create your admin user when prompted (this is a local, single-user Jenkins — pick anything).

### 2. First login to SonarQube and generate a token

1. Open `http://localhost:9000`.
2. Log in with the default `admin` / `admin` — SonarQube forces an immediate password change; set a
   new one.
3. **Administration → Projects → Management → Create Project** (manual), project key
   `nmhieuit_ecommerce` (matching `sonar.projectKey` in `sonar-project.properties` — check that file
   if you change this), display name `ecommerce`.
4. **My Account → Security → Generate Token**, type "Project Analysis Token", scoped to the project
   above. Copy it now — SonarQube shows it exactly once.

### 3. Connect Jenkins to SonarQube

1. In Jenkins: **Manage Jenkins → Credentials → System → Global credentials → Add Credentials**.
   Kind: "Secret text". Secret: the token from step 2.4. ID: `sonarqube-token` (or update
   `scripts/ci/sonar-begin.sh` / the `Jenkinsfile` if you name it differently).
2. **Manage Jenkins → System → SonarQube servers** → Add SonarQube:
   - Name: `sonarqube` — **must match exactly**; `Jenkinsfile`'s `SONARQUBE_SERVER` environment
     variable reads this name.
   - Server URL: `http://sonarqube:9000` (the container-to-container address on the
     `ci-backbone` network — not `localhost`, since Jenkins reaches SonarQube as a sibling
     container, not via the host).
   - Server authentication token: select the `sonarqube-token` credential from step 1.
3. Still in SonarQube, add the webhook Jenkins listens on:
   **Administration → Configuration → Webhooks → Create**, URL
   `http://jenkins:8080/sonarqube-webhook/` (same container-network addressing).
4. Set `SONAR_WEB_JAVAADDITIONALOPTS` and `SONAR_CE_JAVAADDITIONALOPTS` on the `sonarqube` service in
   `docker-compose.ci.yml` (already done in this repo — see gotcha #1 above). Without this, SonarQube
   won't start at all once the Community Branch Plugin is installed.

### 4. Connect Jenkins to GitHub and create the pipeline job

1. On GitHub: create a Personal Access Token (classic or fine-grained) scoped to `nmhieuit/ecommerce`
   with repository read access and commit-status write access —
   `github.com/settings/tokens` (classic) or `github.com/settings/personal-access-tokens/new`
   (fine-grained, scope it to just this repository). Note: this PAT can post commit statuses
   (`githubNotify`/`POST /statuses/:sha`) but **cannot** create/close/merge pull requests or read
   branch protection settings via the API — those need a real user session (browser) or a
   differently-scoped token. `publishChecks` (the Checks API) also rejects PATs outright with 403;
   only a GitHub App installation token can create check runs, which is why the Jenkinsfile uses
   `githubNotify` (Status API) instead.
2. In Jenkins: **Manage Jenkins → Credentials → Add Credentials** → Kind "Username with password"
   (username: your GitHub username; password: the token) or "GitHub App" if you provisioned one
   instead. ID: `github-credentials`.
3. **New Item → Multibranch Pipeline**, name it (e.g. `ecommerce`). Branch Sources → GitHub:
   - Credentials: the one from step 2 above.
   - Repository HTTPS URL: `https://github.com/nmhieuit/ecommerce`.
   - Behaviors: leave "Discover pull requests from origin" and the default branch discovery — this
     is what makes `synchronize` (new commits to an open PR) re-trigger the job automatically.
     Set the PR discovery strategy to "The current pull request revision" (`strategyId=2`), not
     merge-ref — GitHub computes required-check status against the PR's actual HEAD commit, so
     building the merge-ref commit instead leaves checks pending forever.
4. Save. Jenkins will scan the repository and should discover the `master` branch (and any open
   PRs), picking up the root `Jenkinsfile` automatically. A `PeriodicFolderTrigger` (5-minute
   interval) re-scans without any manual "Scan Now" — this is what makes new commits and new PRs
   show up on their own.
5. Rebuild the Jenkins image from `docker/ci/jenkins.Dockerfile` (not the bare upstream image — see
   gotcha #2), and apply gotchas #3–6 (corepack cache path, Docker socket mount + root group,
   Ryuk disabled, Testcontainers host override) in `docker-compose.ci.yml` before expecting any
   Testcontainers-based test tier to pass.

### 5. Turn the checks into a real merge blocker on GitHub

Once one full pipeline run has completed against a real commit (so the five checks
`ci/build`, `ci/unit-tests`, `ci/integration-tests`, `ci/contract-tests`,
`ci/sonarqube-quality-gate` have reported at least once — GitHub can only require checks it has
seen), run, as a repository admin with `gh` installed and authenticated:

```bash
gh auth login
scripts/ci/setup-branch-protection.sh nmhieuit/ecommerce master
```

Or configure the same by hand at `github.com/nmhieuit/ecommerce/settings/branches` → **Add classic
branch protection rule** on `master`:
- Require status checks to pass: add all five names above.
- Require branches to be up to date before merging: on.
- Do not allow bypassing the above settings: on (this is what removes the admin override path
  FR-004/FR-005 require — confirmed working on PR #3, see "Current status" above).

### 6. PR decoration (metrics visible on the PR itself)

SonarQube Community Edition has no official GitHub PR decoration. This repository uses the
community-maintained
[SonarQube Community Branch Plugin](https://github.com/mc1arke/sonarqube-community-branch-plugin)
(reinstalled on every SonarQube upgrade — noted as a trade-off in the ADR-0012 amendment), connected
to the same GitHub PAT from step 4.1 — no extra token scope needed (see "Current status" above for
proof it posts and updates real decoration comments). The alternative, upgrading to SonarQube
Developer Edition for native PR decoration, remains a licensing/installation decision for whoever
owns the SonarQube instance — see the ADR-0012 amendment.

## Verifying it worked

Push a trivial commit or open a PR against `master` and confirm, in order: `ci/build` →
`ci/unit-tests` → `ci/integration-tests` → `ci/contract-tests` → `ci/sonarqube-quality-gate` all
report on the commit, and the last one reflects a real SonarQube analysis (not a connection
error/timeout), followed by a SonarQube decoration comment on the PR. This is `quickstart.md`
Scenario 1 in
[specs/012-sonarqube-quality-gate/quickstart.md](../specs/012-sonarqube-quality-gate/quickstart.md),
and has been re-verified end-to-end multiple times — most recently and most thoroughly in
[specs/013-sonarqube-merge-blocker/tasks.md](../specs/013-sonarqube-merge-blocker/tasks.md) T010–T015,
against real PRs #2, #3, #5, #6, #7, #8, and #9.

## Important caveat: this is a development instance, not production

Everything above stands up Jenkins and SonarQube **locally in Docker Desktop** to unblock wiring and
validate the connection end-to-end. The feature spec's Edge Cases call for the production backend to
be a Kubernetes workload with its own database, provisioned via this platform's existing
Ansible-based pattern — not a laptop's Docker Desktop. Standing up that production instance and
repointing the production Jenkins controller at it is tracked as `T028` in
[`tasks.md`](../specs/012-sonarqube-quality-gate/tasks.md) and remains separate follow-up work.
