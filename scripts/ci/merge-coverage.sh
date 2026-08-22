#!/usr/bin/env sh
# Merges every per-project Cobertura report produced so far into the single file SonarQube reads.
#
#   scripts/ci/merge-coverage.sh
#
# `dotnet test --collect:"XPlat Code Coverage"` writes one report per test project under
# <project>/TestResults/<guid>/coverage.cobertura.xml. SonarQube wants one path
# (sonar.cs.cobertura.reportsPaths), so this collapses them.
#
# Re-run after each test tier: the merge is over whatever exists at the time, so running it again
# after the integration and contract tiers grows the file to cover all three by the time
# `dotnet sonarscanner end` reads it (spec 012-sonarqube-quality-gate, T006/T009).
set -eu

REPO_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
cd "$REPO_ROOT"

OUTPUT="${COVERAGE_REPORT:-artifacts/coverage/backend.cobertura.xml}"
mkdir -p "$(dirname "$OUTPUT")"

# Input paths are expanded here and passed explicitly rather than handed to dotnet-coverage as a
# glob: its own pattern matching silently merges nothing and writes an empty <packages /> report,
# which would present as 0% coverage instead of as an error.
LIST="${TMPDIR:-/tmp}/ci-coverage-inputs-$$.txt"
find . -name 'coverage.cobertura.xml' -path '*/TestResults/*' | sed 's|^\./||' | sort > "$LIST"

count=$(wc -l < "$LIST" | tr -d ' ')
if [ "$count" -eq 0 ]; then
    rm -f "$LIST"
    echo "No Cobertura reports found to merge — did dotnet test run with --collect:\"XPlat Code Coverage\"?" >&2
    exit 1
fi

set --
while IFS= read -r report; do
    [ -n "$report" ] || continue
    set -- "$@" "$report"
done < "$LIST"
rm -f "$LIST"

echo "Merging ${count} Cobertura report(s) into ${OUTPUT}"
dotnet dotnet-coverage merge \
    --output "$OUTPUT" \
    --output-format cobertura \
    "$@"

# An empty merge is the failure mode this script exists to catch: it would hand SonarQube a 0%
# coverage number that looks like a real measurement.
if grep -q '<packages */>' "$OUTPUT"; then
    echo "Merged report at ${OUTPUT} contains no packages — the inputs did not merge." >&2
    exit 1
fi
