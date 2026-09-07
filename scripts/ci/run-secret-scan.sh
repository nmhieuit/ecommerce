#!/usr/bin/env sh
# Runs gitleaks across the repository's full git history, per
# specs/018-cluster-secret-store/contracts/ci-secret-scan-stage-contract.md (ci/secret-scan stage).
#
#   scripts/ci/run-secret-scan.sh
#
# Exits non-zero on any NEW finding (fail-closed: FR-006/SC-005) and never prints a matched secret
# value — gitleaks' own JSON/SARIF report is redacted (--redact), and only the summary table
# (rule, commit, file, line — no matched string) reaches the Jenkins console log.
#
# --baseline-path .gitleaks-baseline.json (research.md Decision 6): full-history scanning finds
# gitleaks' own custom connection-string-password rule matching commits made before this feature
# existed (the appsettings.Development.json password removed by tasks.md T008-T012 is still, and
# will always be, in those old commits — rewriting published git history to erase it was rejected
# in spec.md's Assumptions as its own high-risk action). The baseline is exactly those 9
# already-known, already-remediated findings, captured once; this scan blocks on anything gitleaks
# finds that is NOT already in that baseline, so the gate is real for every commit from here
# forward without requiring a history rewrite for what came before.
set -eu

REPO_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
cd "$REPO_ROOT"

if ! command -v gitleaks >/dev/null 2>&1; then
    echo "gitleaks not found on PATH — see docker/ci/jenkins.Dockerfile for how the CI agent installs it." >&2
    exit 2
fi

mkdir -p artifacts/secret-scan

echo "Scanning full git history with gitleaks (config: .gitleaks.toml, baseline: .gitleaks-baseline.json)..."
gitleaks detect \
    --source "$REPO_ROOT" \
    --config "$REPO_ROOT/.gitleaks.toml" \
    --baseline-path "$REPO_ROOT/.gitleaks-baseline.json" \
    --log-opts="--all" \
    --redact \
    --report-format json \
    --report-path artifacts/secret-scan/gitleaks-report.json \
    --exit-code 1
