#!/usr/bin/env sh
# Static validation for the Kubernetes Deployment manifests rendered by deploy/ansible/ (019-
# liveness-readiness-probes research.md Decision 3). Two checks, in order:
#
#   1. ansible-lint over the role — catches Ansible-level mistakes (bad module args, deprecated
#      syntax) that a plain YAML render would not surface.
#   2. Render each service in inventories/services.yml with `ansible-playbook --check --diff`
#      (no real cluster contacted) and validate the rendered Deployment against the Kubernetes
#      OpenAPI schema with kubeconform.
#
# Requires ansible-core, ansible-lint, and kubeconform on PATH — install via
# deploy/ansible/README.md. Ansible does not run on Windows; this script is meant for the Linux CI
# agents that run the rest of scripts/ci/*.sh, and for local use from WSL/macOS/Linux.
set -eu

REPO_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
cd "$REPO_ROOT/deploy/ansible"

echo "--- ansible-lint"
ansible-lint roles/service_deployment deploy.yml

echo "--- render + kubeconform per service"
rm -rf .rendered
mkdir -p .rendered

services=$(python3 -c "
import yaml
with open('inventories/services.yml') as f:
    doc = yaml.safe_load(f)
print('\n'.join(doc['services'].keys()))
")

failed=''
for service in $services; do
    echo "  rendering ${service}"
    if ! ansible-playbook deploy.yml --check --diff --limit "$service" >/dev/null; then
        failed="${failed} ${service}(render)"
        continue
    fi

    manifest=".rendered/${service}.deployment.yaml"
    if ! kubeconform -strict -summary "$manifest"; then
        failed="${failed} ${service}(kubeconform)"
    fi
done

if [ -n "$failed" ]; then
    echo "FAILED:${failed}" >&2
    exit 1
fi

echo "All service manifests rendered and passed kubeconform."
