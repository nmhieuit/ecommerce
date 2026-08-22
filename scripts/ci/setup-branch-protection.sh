#!/usr/bin/env sh
# Applies the branch protection that makes the CI pipeline a merge blocker (FR-004/FR-005).
#
#   scripts/ci/setup-branch-protection.sh <owner/repo> [branch]
#
# Run once by a repository administrator, and again whenever the required check names change.
# Requires the GitHub CLI (`gh auth login`) with admin rights on the repository.
#
# Branch protection is deliberately not managed as infrastructure-as-code: introducing an IaC
# stack to manage one repository's settings would be disproportionate (research.md Decision 5).
# This script is the documented, repeatable form of that one-time action, and it is the only
# reason the five stage names in the Jenkinsfile are load-bearing —
# specs/012-sonarqube-quality-gate/contracts/pipeline-stage-contract.md §1 is the shared list.
#
# `enforce_admins: true` is the setting that removes the override path for every role, including
# repository admins. Without it the pipeline is advisory, not a gate.
set -eu

REPO="${1:?usage: setup-branch-protection.sh <owner/repo> [branch]}"
BRANCH="${2:-master}"

command -v gh >/dev/null 2>&1 || { echo "GitHub CLI (gh) is required." >&2; exit 1; }

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
    "required_approving_review_count": 1
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
