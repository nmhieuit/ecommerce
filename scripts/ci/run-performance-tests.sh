#!/usr/bin/env sh
# Brings up the demo (production-like) stack and runs the "performance" tier — the load test on the
# critical browse->basket->checkout->order path, spec 025-load-performance-test-budgets.
#
# Deliberately NOT called from Jenkinsfile (the PR gate) or from scripts/ci/run-dotnet-tests.sh's
# other tiers: this is the scheduled, non-PR-blocking pipeline described in
# specs/025-load-performance-test-budgets/contracts/performance-pipeline-stage-contract.md — see
# Jenkinsfile.performance.
set -eu

REPO_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
cd "$REPO_ROOT"

# --build so a source change is running code rather than a stale image; --wait so this returns only
# once every component is healthy (same choice scripts/demo.sh makes for the same stack).
echo "Starting the platform in demo mode (production-like, per constitution's Performance gate)..."
docker compose -f docker-compose.yml -f docker-compose.demo.yml up --build --wait

echo "Building the solution..."
dotnet restore "$REPO_ROOT/Ecommerce.slnx"
dotnet build "$REPO_ROOT/Ecommerce.slnx" --configuration Release --no-restore

echo "Running the performance tier..."
"$REPO_ROOT/scripts/ci/run-dotnet-tests.sh" performance "$@"
