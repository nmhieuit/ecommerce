#!/usr/bin/env bash
#
# Runs the Phase 1 order demo end to end, and fails loudly if any part of it does not hold.
#
# The one command the demo costs (006-e2e-order-demo, FR-007c). The POSIX twin of demo.ps1 — see
# that file for why the prerequisite checks exist rather than calling the tools directly. The
# command's contract is specs/006-e2e-order-demo/contracts/demo-interface.md.

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

storefront_url='http://localhost:4173'
gateway_url='http://localhost:5300'
orders_url='http://localhost:5041'
baskets_url='http://localhost:5188'

# The stub identity the gateway stamps onto every request (gateway appsettings.json, StubIdentity).
# The demo supplies these itself on the calls it makes straight to a service, because a
# host-originated call has no gateway in front of it to resolve a tenant.
tenant_id='contoso'
subject_id='phase1-stub-user'

web_app_dir="$repository_root/frontend/apps/web"
playwright_cli="$web_app_dir/node_modules/@playwright/test/cli.js"
artifacts_dir="$repository_root/artifacts/demo"
verification_file="$artifacts_dir/verification.txt"
reference_file="$artifacts_dir/last-reference.txt"
total_file="$artifacts_dir/last-total.txt"

skip_start=0
if [ "${1:-}" = "--skip-start" ]; then
    skip_start=1
fi

stop_with_reason() {
    printf '\033[31mCannot run the demo: %s\033[0m\n' "$1" >&2
    exit 1
}

endpoint_answers() {
    curl -fsS -m "${2:-5}" -o /dev/null "$1" 2>/dev/null
}

# --- prerequisites, each failing with one sentence naming what is missing -------------------------

command -v docker >/dev/null 2>&1 \
    || stop_with_reason "Docker is not installed, or is not on PATH. Install it from https://docs.docker.com/get-docker/."

docker info --format '{{.ServerVersion}}' >/dev/null 2>&1 \
    || stop_with_reason "the Docker daemon is not responding. Start Docker and try again."

[ -f "$repository_root/.env" ] \
    || stop_with_reason "'.env' does not exist. Copy the template first:  cp .env.example .env  (no editing required)."

command -v node >/dev/null 2>&1 \
    || stop_with_reason "Node is not installed, or is not on PATH. The demo drives the storefront with Playwright, which Node runs."

# Playwright is invoked through Node against its own CLI rather than through pnpm. pnpm is what
# installs these dependencies, but once they are installed it is not needed to run them — and one
# fewer thing required on PATH is one fewer reason the demo refuses to run on a machine that could
# have run it.
[ -f "$playwright_cli" ] \
    || stop_with_reason "the storefront's dependencies are not installed. Install them once:  pnpm install  (from the frontend/ directory)"

# Playwright's browsers live outside the repository, so a fresh clone with node_modules installed
# still has none. Checked here because Playwright's own failure — "Executable doesn't exist" with a
# long path — reads as a broken install rather than a one-time setup step nobody ran.
browsers_root="${PLAYWRIGHT_BROWSERS_PATH:-}"
if [ -z "$browsers_root" ]; then
    case "$(uname -s)" in
        Darwin) browsers_root="$HOME/Library/Caches/ms-playwright" ;;
        # Git Bash, MSYS, and Cygwin run this script on Windows, where Playwright caches under
        # LOCALAPPDATA rather than ~/.cache. Without this branch the check looks in a directory that
        # is empty on every Windows machine and refuses a correctly installed setup.
        MINGW*|MSYS*|CYGWIN*)
                browsers_root="$(cygpath -u "${LOCALAPPDATA:-$HOME}")/ms-playwright" ;;
        *)      browsers_root="$HOME/.cache/ms-playwright" ;;
    esac
fi
if ! compgen -G "$browsers_root/chromium*" >/dev/null 2>&1; then
    stop_with_reason "Playwright's chromium is not installed. Install it once:  pnpm --filter @ecommerce/web exec playwright install chromium"
fi

# --- start, or verify what is already running ------------------------------------------------------

cd "$repository_root"

if [ "$skip_start" -eq 1 ]; then
    # Demo mode's observable signature is the orders service answering on its own port. The default
    # stack publishes only the storefront and the gateway, so a stack brought up with up.sh fails
    # this check — which is the point, because the verification step and the clean-basket step both
    # call services directly.
    endpoint_answers "$orders_url/health/ready" \
        || stop_with_reason "the stack is not running in demo mode, so nothing is answering on $orders_url. Drop --skip-start, or start it with:  docker compose -f docker-compose.yml -f docker-compose.demo.yml up --wait"

    printf '\033[36mUsing the stack that is already up.\033[0m\n'
else
    # Layered over the default stack rather than replacing it: demo mode publishes the two services
    # the demo speaks to and makes the collector print spans, and changes nothing else. See
    # docker-compose.demo.yml.
    #
    # --build so a source change is running code rather than a stale image.
    # --wait so this returns only once every component is healthy, and non-zero if one is not, which
    #   is FR-009 — the demo must not begin against a partially available stack.
    printf '\033[36mStarting the platform in demo mode. First run builds images and takes a few minutes.\033[0m\n'

    if ! docker compose -f docker-compose.yml -f docker-compose.demo.yml up --build --wait; then
        printf '\n\033[31mThe stack did not come up, so the demo did not run. The component that failed is named above; its logs:\033[0m\n' >&2
        printf '\033[31m  docker compose logs <component>\033[0m\n' >&2
        exit 1
    fi

    # The same cold-start warm-up up.sh performs, and for the same measured reason: health checks
    # say a service can reach its database, not that it can serve a request inside the BFF's
    # downstream budget. Skipping it here would make the first demo after a cold start fail on a 504
    # from a stack every gate called healthy.
    printf '\033[36mWarming the request path…\033[0m\n'
    for path in /bff/products /bff/basket "/bff/orders/00000000-0000-4000-8000-000000000000"; do
        endpoint_answers "${gateway_url}${path}" 30 || true
    done
fi

# Fail before the flow rather than during it, so "the storefront is not being served" never arrives
# disguised as a failed assertion about a product list.
endpoint_answers "$storefront_url" \
    || stop_with_reason "the storefront is not answering on $storefront_url, so there is nothing to demonstrate."

endpoint_answers "$baskets_url/health/ready" \
    || stop_with_reason "the baskets service is not answering on $baskets_url, so the demo cannot start from a clean basket."

# --- remember what the last run produced, before this run overwrites it ---------------------------
#
# Read now rather than after, so the comparison at the end is against a genuinely earlier run
# (FR-007: a repeat run must place a new order, not re-report the previous one).
previous_reference=''
if [ -f "$reference_file" ]; then
    previous_reference="$(tr -d '[:space:]' < "$reference_file")"
fi

# --- clean basket ---------------------------------------------------------------------------------
#
# Straight to the baskets service, not through /bff/checkout. Checking out to empty a basket would
# place a real order every time the demo started with something left in it, and the demo would then
# have to account for orders nobody asked for (research.md Decision 7).
#
# 409 means it was already empty, which is success: the service is reporting there was nothing to
# clear, not refusing to clear it.
printf '\033[36mClearing the basket…\033[0m\n'
clear_status="$(curl -s -o /dev/null -w '%{http_code}' -m 10 -X POST \
    -H "X-Tenant-Id: ${tenant_id}" \
    -H "X-Subject-Id: ${subject_id}" \
    "$baskets_url/baskets/current/clear" || echo '000')"

case "$clear_status" in
    2*|409) ;;
    *) stop_with_reason "the basket could not be cleared (HTTP ${clear_status}), so the demo would not start from a known state." ;;
esac

# --- run the flow -----------------------------------------------------------------------------------

printf '\033[36mRunning the demo flow…\033[0m\n\n'

flow_exit=0
( cd "$web_app_dir" && node "$playwright_cli" test --config playwright.demo.config.ts ) || flow_exit=$?

if [ "$flow_exit" -ne 0 ]; then
    printf '\n\033[31mThe demo flow did not complete. The failing step and its assertion are named above.\033[0m\n' >&2
    printf '\033[31m  Video and trace:  artifacts/demo/\033[0m\n' >&2
    exit "$flow_exit"
fi

# --- report -----------------------------------------------------------------------------------------

[ -f "$reference_file" ] \
    || stop_with_reason "the flow reported success but wrote no order reference to $reference_file, so there is nothing to verify."

reference="$(tr -d '[:space:]' < "$reference_file")"
total="$(tr -d '[:space:]' < "$total_file")"

# --- verify, straight against the orders service (FR-012, SC-004) ---------------------------------
#
# Two calls, both to the service that owns the record rather than to the database behind it or the
# BFF in front of it. The story asks to query the orders service directly, and the constitution
# forbids reaching into another component's store; this is the one reading that satisfies both.

# Without a tenant first. Not error handling — the enforcement gate demonstrated (FR-006, User Story
# 2 scenario 2). A call from this machine has no gateway in front of it to resolve a tenant, so the
# service must refuse rather than answer from some default.
untenanted_status="$(curl -s -o /dev/null -w '%{http_code}' -m 10 "$orders_url/orders/$reference" || echo '000')"

case "$untenanted_status" in
    2*) stop_with_reason "the orders service answered a request that resolved no tenant (HTTP ${untenanted_status}). That is a tenant-isolation failure, not a demo failure." ;;
esac

# Then with the tenant the gateway would have stamped.
order_json="$(curl -fsS -m 10 -H "X-Tenant-Id: ${tenant_id}" "$orders_url/orders/$reference" || echo '')"

[ -n "$order_json" ] \
    || stop_with_reason "the orders service did not return order $reference even with a tenant resolved."

stored_tenant="$(printf '%s' "$order_json" | sed -n 's/.*"tenantId":"\([^"]*\)".*/\1/p')"

[ -n "$stored_tenant" ] \
    || stop_with_reason "order $reference came back without a tenant. FR-005 requires every order to carry the tenant it was placed for."

[ "$stored_tenant" = "$tenant_id" ] \
    || stop_with_reason "order $reference is attributed to '${stored_tenant}' but the placing request resolved '${tenant_id}'. That is a cross-tenant attribution fault."

# --- compose the report ---------------------------------------------------------------------------
#
# Shape fixed by specs/006-e2e-order-demo/data-model.md. It is a contract because User Story 2
# scenario 3 requires somebody who did not build this to read it and see what was proved.
mkdir -p "$artifacts_dir"
{
    printf 'ORDER PLACED\n'
    printf '  reference : %s\n' "$reference"
    printf '  total     : %s\n' "$total"
    printf '  tenant    : %s\n\n' "$stored_tenant"
    printf 'TENANT ATTRIBUTION\n'
    printf '  resolved tenant for the placing request : %s\n' "$tenant_id"
    printf '  tenant stored on the order              : %s\n' "$stored_tenant"
    printf '  match                                   : YES\n\n'
    printf 'WITHOUT A TENANT\n'
    printf '  GET /orders/%s  (no X-Tenant-Id)  ->  %s, no order returned\n' "$reference" "$untenanted_status"
    printf '  the orders service refuses to answer when no tenant was resolved\n\n'
} > "$verification_file"

printf '\n\033[32m================================================================\033[0m\n'
cat "$verification_file"

# --- repeatability (FR-007, SC-003) -----------------------------------------------------------------
#
# The claim this story makes is that the demo is repeatable rather than a one-off. Two runs reporting
# the same order would mean the second re-read the first's result instead of placing anything, and
# that would pass every other check here.
if [ -n "$previous_reference" ]; then
    if [ "$reference" = "$previous_reference" ]; then
        stop_with_reason "this run reported the same order as the previous one ($reference). A repeat run must place a new order."
    fi

    printf 'REPEATABLE\n'
    printf '  previous run : %s\n' "$previous_reference"
    printf '  this run     : %s\n' "$reference"
    printf '  distinct     : YES\n\n'
fi

printf '\033[32m================================================================\033[0m\n\n'
printf '\033[32mThe demo completed.\033[0m\n'
printf '  Recording    artifacts/demo/\n'
printf '  Run it again with:  ./scripts/demo.sh --skip-start\n'
