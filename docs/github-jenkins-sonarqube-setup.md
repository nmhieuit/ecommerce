# Connecting GitHub, Jenkins, and SonarQube for the Quality Gate

**Date**: 2026-08-23
**Feature**: [specs/012-sonarqube-quality-gate](../specs/012-sonarqube-quality-gate/spec.md)
**Repository**: `github.com/nmhieuit/ecommerce` (private, default branch `master`)

This is the record of what an automated session did toward wiring GitHub → Jenkins → SonarQube for
this repository, and the exact remaining steps for a human to finish it. It exists because the
session found the premise it was asked to act on — "SonarQube running in local Jenkins on Docker
Desktop" — did not match reality, and because finishing the wiring requires pasting real secrets
(a GitHub token, a SonarQube token) into forms, which an automated session must never do (even when
asked), per this environment's operating rules and the feature's own FR-015.

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

## What requires you, specifically, to do it

Every step below involves typing a real secret into a form (a SonarQube token, a GitHub token) or an
account first-login. That is deliberately outside what this session will do automatically —
entering API keys/tokens/passwords into any field is off-limits for an automated session
regardless of instructions, so these are handed off as exact procedures instead.

### 1. Unlock Jenkins and finish its setup wizard

Jenkins is up and its three required plugins (`github-branch-source`, `github-checks`, `sonar`) are
already installed and confirmed loaded, as of this session. Its initial admin password (retrieved
via `docker exec`, not typed anywhere by the automated session):

```
91bbce79541a493f8b5a7f769fabcddc
```

If this Jenkins container is ever recreated, get a fresh one the same way:

```bash
docker exec ecomerce-ci-jenkins-1 cat /var/jenkins_home/secrets/initialAdminPassword
```

1. Open `http://localhost:8080`.
2. Paste the password above.
3. On the plugin-selection screen, choose **"Select plugins to install"** and select none (or
   "Install suggested plugins" is also fine — the three required ones are already present either
   way; this just avoids re-downloading a large default set).
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

### 4. Connect Jenkins to GitHub and create the pipeline job

1. On GitHub: create a Personal Access Token (classic or fine-grained) scoped to `nmhieuit/ecommerce`
   with repository read access and commit-status/checks write access —
   `github.com/settings/tokens` (classic) or `github.com/settings/personal-access-tokens/new`
   (fine-grained, scope it to just this repository).
2. In Jenkins: **Manage Jenkins → Credentials → Add Credentials** → Kind "Username with password"
   (username: your GitHub username; password: the token) or "GitHub App" if you provisioned one
   instead. ID: `github-credentials`.
3. **New Item → Multibranch Pipeline**, name it (e.g. `ecommerce`). Branch Sources → GitHub:
   - Credentials: the one from step 2 above.
   - Repository HTTPS URL: `https://github.com/nmhieuit/ecommerce`.
   - Behaviors: leave "Discover pull requests from origin" and the default branch discovery — this
     is what makes `synchronize` (new commits to an open PR) re-trigger the job automatically.
4. Save. Jenkins will scan the repository and should discover the `master` branch (and any open
   PRs), picking up the root `Jenkinsfile` automatically.

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
  FR-004/FR-005 require).

### 6. PR decoration (metrics visible on the PR itself)

SonarQube Community Edition has no official GitHub PR decoration. Either:
- Install the community-maintained [SonarQube Community Branch Plugin](https://github.com/mc1arke/sonarqube-community-branch-plugin)
  (reinstalled on every SonarQube upgrade — noted as a trade-off in the ADR-0012 amendment), and
  connect it to the GitHub token from step 4.1, **or**
- Upgrade to SonarQube Developer Edition, which includes PR decoration natively.

Either way, this is a licensing/installation decision for whoever owns the SonarQube instance, not
something to default silently — see the ADR-0012 amendment.

## Verifying it worked

Push a trivial commit or open a PR against `master` and confirm, in order: `ci/build` →
`ci/unit-tests` → `ci/integration-tests` → `ci/contract-tests` → `ci/sonarqube-quality-gate` all
report on the commit, and the last one reflects a real SonarQube analysis (not a connection
error/timeout). This is `quickstart.md` Scenario 1 in
[specs/012-sonarqube-quality-gate/quickstart.md](../specs/012-sonarqube-quality-gate/quickstart.md).

## Important caveat: this is a development instance, not production

Everything above stands up Jenkins and SonarQube **locally in Docker Desktop** to unblock wiring and
validate the connection end-to-end. The feature spec's Edge Cases call for the production backend to
be a Kubernetes workload with its own database, provisioned via this platform's existing
Ansible-based pattern — not a laptop's Docker Desktop. Standing up that production instance and
repointing the production Jenkins controller at it is tracked as `T028` in
[`tasks.md`](../specs/012-sonarqube-quality-gate/tasks.md) and remains separate follow-up work.
