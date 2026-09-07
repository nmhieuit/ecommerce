#!/usr/bin/env sh
# Builds every service's container image and scans its filesystem for secrets with Trivy, per
# specs/018-cluster-secret-store/contracts/ci-secret-scan-stage-contract.md (ci/image-secret-scan
# stage).
#
#   scripts/ci/run-image-secret-scan.sh
#
# Dockerfiles are discovered on disk rather than listed here, matching
# scripts/ci/run-dotnet-tests.sh's convention: a new service's Dockerfile joins this stage the
# moment it exists, without editing this script or the Jenkinsfile.
#
# Exits non-zero if ANY image has a finding (fail-closed: FR-006/SC-005) — every image is still
# built and scanned even if an earlier one fails, so one run reports every affected service, not
# just the first. Never prints a matched secret value: Trivy's own default output already
# truncates/redacts the matched text, and this script does not echo image contents itself.
#
# The pipeline does not build container images anywhere else today, so this stage builds them
# itself (default `final` target — see each Dockerfile's own header comment for why the build
# context must be the repository root, not the service folder).
set -eu

REPO_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
cd "$REPO_ROOT"

if ! command -v trivy >/dev/null 2>&1; then
    echo "trivy not found on PATH — see docker/ci/jenkins.Dockerfile for how the CI agent installs it." >&2
    exit 2
fi

mkdir -p artifacts/secret-scan

dockerfiles=$(find services -path '*/src/*/Dockerfile' -not -path '*/bin/*' -not -path '*/obj/*' | sort)

if [ -z "$dockerfiles" ]; then
    echo "No service Dockerfiles found under services/*/src/*/Dockerfile." >&2
    exit 1
fi

failed=''
printf '%s\n' "$dockerfiles" > "${TMPDIR:-/tmp}/ci-image-secret-scan-$$.txt"
while IFS= read -r dockerfile; do
    [ -n "$dockerfile" ] || continue

    # services/orders/src/Orders.Api/Dockerfile -> orders
    service=$(printf '%s\n' "$dockerfile" | cut -d/ -f2)
    tag="${service}-api:secret-scan"

    echo "--- docker build -f ${dockerfile} -t ${tag} ."
    if ! docker build -f "$dockerfile" -t "$tag" .; then
        echo "Failed to build ${dockerfile}." >&2
        failed="${failed} ${service}(build)"
        continue
    fi

    echo "--- trivy image --scanners secret ${tag}"
    if ! trivy image \
        --scanners secret \
        --exit-code 1 \
        --format json \
        --output "artifacts/secret-scan/trivy-${service}.json" \
        "$tag"; then
        failed="${failed} ${service}(secret-found)"
    fi
done < "${TMPDIR:-/tmp}/ci-image-secret-scan-$$.txt"
rm -f "${TMPDIR:-/tmp}/ci-image-secret-scan-$$.txt"

if [ -n "$failed" ]; then
    echo "Images with secret-scan findings or build failures:${failed}" >&2
    exit 1
fi

echo "No secrets found in any service image filesystem."
