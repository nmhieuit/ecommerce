#!/usr/bin/env sh
# Applies the branch protection that makes the CI pipeline a merge blocker (FR-004/FR-005).
#
#   scripts/ci/setup-branch-protection.sh <owner/repo> [branch]
#
# Run once by a repository administrator, and again whenever the required check names change.
# Requires the GitHub CLI (`gh auth login`) with admin rights on the repository.
#
# PLAN REQUIREMENT — verified against nmhieuit/ecommerce on 2026-08-23: branch protection is not
# available on a **private** repository on a free GitHub plan. The API answers every call against
# it with HTTP 403 "Upgrade to GitHub Pro or make this repository public to enable this feature",
# and repository rulesets (the newer mechanism) answer 403 as well. Authentication is not the
# issue: an admin token gets the same response. Either make the repository public, or move the
# account to GitHub Pro, before this script can do anything. The preflight below says so plainly
# rather than letting a raw 403 read as a broken token.
#
# Branch protection is deliberately not managed as infrastructure-as-code: introducing an IaC
# stack to manage one repository's settings would be disproportionate (research.md Decision 5).
# This script is the documented, repeatable form of that one-time action, and it is the only
# reason the five stage names in the Jenkinsfile are load-bearing —
# specs/013-sonarqube-merge-blocker/contracts/pipeline-stage-contract.md §1 is the shared list.
#
# `enforce_admins: true` is the setting that removes the override path for every role, including
# repository admins. Without it the pipeline is advisory, not a gate.
#
# `required_approving_review_count` is 0, not 1, and that is deliberate. nmhieuit/ecommerce has
# exactly one collaborator, and GitHub does not let anyone approve their own pull request — so
# requiring one approval would make every PR permanently unmergeable regardless of whether CI
# passed. That is an accidental lockout, not a quality gate, and it is not what FR-008 asks for:
# what has to block merges here is the five required status checks. Raise this to 1 when a second
# reviewer actually exists.
set -eu

REPO="${1:?usage: setup-branch-protection.sh <owner/repo> [branch]}"
BRANCH="${2:-master}"

command -v gh >/dev/null 2>&1 || { echo "GitHub CLI (gh) is required." >&2; exit 1; }

# Preflight: distinguish "plan does not allow this" from "your token is wrong". Both surface as
# 403, and only one of them is fixable by re-authenticating.
if ! probe="$(gh api "repos/${REPO}/branches/${BRANCH}/protection" 2>&1)"; then
    case "$probe" in
        *"Upgrade to GitHub Pro"*|*"upgrade to GitHub Pro"*)
            echo "Branch protection is unavailable on ${REPO}." >&2
            echo >&2
            echo "GitHub does not offer protected branches on a private repository on the free" >&2
            echo "plan. Pick one of:" >&2
            echo "  1. Make ${REPO} public   — branch protection becomes available at no cost." >&2
            echo "  2. Upgrade to GitHub Pro — keeps the repository private." >&2
            echo >&2
            echo "Without one of these the pipeline can report checks, but nothing enforces them," >&2
            echo "which does not satisfy FR-008 (no override path for any role)." >&2
            exit 2
            ;;
        *"Branch not protected"*)
            : # Expected on a first run — nothing to report yet.
            ;;
        *)
            echo "Could not read current protection state for ${REPO}@${BRANCH}:" >&2
            echo "${probe}" >&2
            exit 1
            ;;
    esac
fi

echo "Applying branch protection to ${REPO}@${BRANCH}"

gh api \
    --method PUT \
    -H "Accept: application/vnd.github+json" \
    "repos/${REPO}/branches/${BRANCH}/protection" \
    --input - <<'JSON'
{
  "required_status_checks": {
    "strict": true,
    "contexts": [
      "ci/build",
      "ci/unit-tests",
      "ci/integration-tests",
      "ci/contract-tests",
      "ci/sonarqube-quality-gate"
    ]
  },
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "dismiss_stale_reviews": true,
    "require_code_owner_reviews": false,
    "required_approving_review_count": 0
  },
  "restrictions": null,
  "allow_force_pushes": false,
  "allow_deletions": false,
  "required_conversation_resolution": true
}
JSON

echo
echo "Applied. Verify the result:"
echo "  gh api repos/${REPO}/branches/${BRANCH}/protection | jq '.required_status_checks.contexts, .enforce_admins'"
echo
echo "Expected: all five ci/* checks listed, and enforce_admins.enabled = true."
