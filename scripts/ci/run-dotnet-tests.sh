#!/usr/bin/env sh
# Runs one tier of the repository's .NET test suites, collecting Cobertura coverage.
#
#   scripts/ci/run-dotnet-tests.sh unit|integration|contract|performance [extra dotnet test args...]
#
# Projects are discovered on disk rather than listed here, so a new service's test project joins CI
# the moment it is created — the pipeline must not need editing to notice new tests
# (specs/012-sonarqube-quality-gate/contracts/pipeline-stage-contract.md §2).
#
# Tier classification, applied in order:
#   contract     — *ContractTests.csproj                (Pact, spec 011)
#   integration  — *IntegrationTest*.csproj             (Testcontainers, spec 010; needs Docker.
#                                                        The glob also catches
#                                                        shared/IntegrationTestSupport.Tests, which
#                                                        starts real Redis/RabbitMQ containers.)
#   performance  — CriticalPathLoadTests.csproj         (NBomber load test, spec 025; needs the full
#                                                        stack running — see
#                                                        scripts/ci/run-performance-tests.sh. NEVER
#                                                        runs on a PR — see "unit" below.)
#   unit         — every remaining *Tests.csproj        (per-service unit suites, shared/ suites,
#                                                        and the repository convention suites under
#                                                        tests/, which read files off disk)
#
# "unit" runs on every PR without Docker or a live stack (Jenkinsfile "unit tests" stage) — a project
# that needs either MUST be excluded here explicitly, the same way "contract"/"integration" already
# are, or it silently joins "unit" and breaks the PR gate the moment it exists on disk
# (specs/025-load-performance-test-budgets/contracts/performance-pipeline-stage-contract.md §1).
set -eu

TIER="${1:?usage: run-dotnet-tests.sh <unit|integration|contract|performance> [dotnet test args]}"
shift

REPO_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
cd "$REPO_ROOT"

all=$(find . -name '*Tests.csproj' -not -path '*/bin/*' -not -path '*/obj/*' | sed 's|^\./||' | sort)

case "$TIER" in
    contract)
        projects=$(printf '%s\n' "$all" | grep 'ContractTests\.csproj$' || true)
        ;;
    integration)
        projects=$(printf '%s\n' "$all" | grep -v 'ContractTests\.csproj$' | grep 'IntegrationTest' || true)
        ;;
    performance)
        projects=$(printf '%s\n' "$all" | grep 'CriticalPathLoadTests\.csproj$' || true)
        ;;
    unit)
        projects=$(printf '%s\n' "$all" | grep -v 'ContractTests\.csproj$' | grep -v 'IntegrationTest' | grep -v 'CriticalPathLoadTests\.csproj$' || true)
        ;;
    *)
        echo "Unknown tier '${TIER}' (expected unit, integration, contract, or performance)." >&2
        exit 2
        ;;
esac

if [ -z "$projects" ]; then
    echo "No ${TIER} test projects found." >&2
    exit 1
fi

echo "Running ${TIER} test projects:"
printf '%s\n' "$projects" | sed 's/^/  /'

# Every project runs even if an earlier one fails, so one PR shows every broken suite at once
# instead of only the first.
failed=''
printf '%s\n' "$projects" > "${TMPDIR:-/tmp}/ci-projects-$$.txt"
while IFS= read -r project; do
    [ -n "$project" ] || continue
    echo "--- dotnet test ${project}"
    if ! dotnet test "$project" \
        --configuration Release \
        --no-build \
        --logger "trx;LogFileName=$(basename "$project" .csproj).trx" \
        --collect:"XPlat Code Coverage" \
        "$@"; then
        failed="${failed} ${project}"
    fi
done < "${TMPDIR:-/tmp}/ci-projects-$$.txt"
rm -f "${TMPDIR:-/tmp}/ci-projects-$$.txt"

if [ -n "$failed" ]; then
    echo "FAILED ${TIER} test projects:${failed}" >&2
    exit 1
fi
