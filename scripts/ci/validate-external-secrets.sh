#!/usr/bin/env sh
# Validates deploy/k8s/*/external-secret.yaml against
# specs/018-cluster-secret-store/contracts/external-secret-manifest-contract.md:
#
#   1. refreshInterval must be <=5m (SC-004 — rotate in Vault without a redeploy).
#   2. Every data[].secretKey must correspond to a RequiredSecret actually declared in that
#      service's Program.cs (secretKey "ConnectionStrings__<X>" <-> code's
#      RequiredSecret.ConnectionString("<X>"), the env-var/config-key translation documented in
#      the contract).
#
#   scripts/ci/validate-external-secrets.sh
#
# Local/manual validation script (tasks.md T033) — not yet wired into the Jenkinsfile as a
# required check: these manifests are declarative contracts for infrastructure that does not exist
# in this repository yet (research.md Decision 1), so failing the PR gate on them would block
# unrelated work on a check nothing can act on until Vault/ESO are provisioned. Run this by hand,
# or from a future CI stage once that infrastructure lands.
set -eu

REPO_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
cd "$REPO_ROOT"

manifests=$(find deploy/k8s -mindepth 2 -name 'external-secret.yaml' | sort)

if [ -z "$manifests" ]; then
    echo "No deploy/k8s/*/external-secret.yaml manifests found." >&2
    exit 1
fi

failed=''

printf '%s\n' "$manifests" > "${TMPDIR:-/tmp}/ci-validate-ext-secrets-$$.txt"
while IFS= read -r manifest; do
    [ -n "$manifest" ] || continue

    # deploy/k8s/orders/external-secret.yaml -> orders
    service=$(printf '%s\n' "$manifest" | cut -d/ -f3)
    echo "--- ${manifest} (service: ${service})"

    refresh=$(grep -m1 'refreshInterval:' "$manifest" | sed 's/.*refreshInterval:[[:space:]]*//' | tr -d '\r')
    number=$(printf '%s\n' "$refresh" | sed -nE 's/^([0-9]+)(s|m)$/\1/p')
    unit=$(printf '%s\n' "$refresh" | sed -nE 's/^([0-9]+)(s|m)$/\2/p')
    if [ -z "$number" ] || [ -z "$unit" ]; then
        echo "    refreshInterval='${refresh}' is not a recognized <N>s / <N>m duration." >&2
        failed="${failed} ${service}(refreshInterval=${refresh})"
    else
        seconds=$number
        [ "$unit" = "m" ] && seconds=$((number * 60))
        if [ "$seconds" -le 300 ]; then
            echo "    refreshInterval=${refresh} (${seconds}s): OK (<=5m)"
        else
            echo "    refreshInterval=${refresh} (${seconds}s) exceeds the 5-minute SC-004 budget." >&2
            failed="${failed} ${service}(refreshInterval=${refresh})"
        fi
    fi

    program_cs=$(find "services/${service}/src" -name 'Program.cs' | head -n1)
    if [ -z "$program_cs" ]; then
        echo "    could not find services/${service}/src/*/Program.cs to cross-check secretKey." >&2
        failed="${failed} ${service}(no-program-cs)"
        continue
    fi

    for secret_key in $(grep -oE 'secretKey:[[:space:]]*[A-Za-z0-9_]+' "$manifest" | sed 's/.*:[[:space:]]*//'); do
        # ConnectionStrings__OrdersDb -> RequiredSecret.ConnectionString("OrdersDb")
        case "$secret_key" in
            ConnectionStrings__*)
                db_name=${secret_key#ConnectionStrings__}
                if grep -q "RequiredSecret.ConnectionString(\"${db_name}\")" "$program_cs"; then
                    echo "    ${secret_key}: matches RequiredSecret.ConnectionString(\"${db_name}\") in ${program_cs}"
                else
                    echo "    ${secret_key}: no matching RequiredSecret.ConnectionString(\"${db_name}\") found in ${program_cs}" >&2
                    failed="${failed} ${service}(${secret_key}-not-in-code)"
                fi
                ;;
            *)
                echo "    ${secret_key}: not a ConnectionStrings__* key — no automated cross-check for this shape yet." >&2
                ;;
        esac
    done
done < "${TMPDIR:-/tmp}/ci-validate-ext-secrets-$$.txt"
rm -f "${TMPDIR:-/tmp}/ci-validate-ext-secrets-$$.txt"

if [ -n "$failed" ]; then
    echo "Validation failures:${failed}" >&2
    exit 1
fi

echo "All deploy/k8s/*/external-secret.yaml manifests are valid."
