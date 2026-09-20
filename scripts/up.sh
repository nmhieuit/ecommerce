#!/usr/bin/env bash
#
# Starts the whole platform locally, and does not return until it is usable.
#
# The documented command (005-one-command-local-run, FR-001). The POSIX twin of up.ps1 — see that
# file for why the prerequisite checks exist rather than calling Compose directly.

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
required_memory_gb=6

stop_with_reason() {
    printf '\033[31mCannot start the stack: %s\033[0m\n' "$1" >&2
    exit 1
}

# --- prerequisites, each failing with one sentence naming what is missing (FR-011) ---------------

command -v docker >/dev/null 2>&1 \
    || stop_with_reason "Docker is not installed, or is not on PATH. Install it from https://docs.docker.com/get-docker/."

docker info --format '{{.ServerVersion}}' >/dev/null 2>&1 \
    || stop_with_reason "the Docker daemon is not responding. Start Docker and try again."

[ -f "$repository_root/.env" ] \
    || stop_with_reason "'.env' does not exist. Copy the template first:  cp .env.example .env  (no editing required)."

daemon_memory_bytes="$(docker info --format '{{.MemTotal}}')"
daemon_memory_gb=$(( daemon_memory_bytes / 1024 / 1024 / 1024 ))
if [ "$daemon_memory_gb" -lt "$required_memory_gb" ]; then
    stop_with_reason "Docker has ${daemon_memory_gb} GB of memory available but the stack needs ${required_memory_gb} GB. Raise it in your Docker settings."
fi

# --- start ---------------------------------------------------------------------------------------

cd "$repository_root"

compose_args=(compose)
if [ "${1:-}" = "--debug" ]; then
    # An override file, not a profile: a profile decides whether a service starts, and cannot add
    # ports to one that is starting anyway. See docker-compose.debug.yml.
    compose_args+=(-f docker-compose.yml -f docker-compose.debug.yml)
fi

# --build so a source change is running code rather than a stale image (FR-009).
# --wait so this returns only once every component is healthy, non-zero if one is not (FR-002).
compose_args+=(up --build --wait)

printf '\033[36mStarting the platform. First run builds images and takes a few minutes.\033[0m\n'

if ! docker "${compose_args[@]}"; then
    printf '\n\033[31mThe stack did not come up. The component that failed is named above; its logs:\033[0m\n' >&2
    printf '\033[31m  docker compose logs <component>\033[0m\n' >&2
    exit 1
fi

# --- warm the request path ----------------------------------------------------------------------
#
# Health checks say a service can reach its database. They do not say the platform can serve a
# request, and on a cold start those are different claims: the first call through any path pays JIT
# compilation, EF model building, and connection-pool creation, which measurably exceeds the BFF's
# 3-second downstream budget. Observed before this existed: the first checkout after a fresh start
# failed with `Downstream call to BasketsApi failed with 504`, from a stack every gate called
# healthy.
#
# The ticket's second acceptance criterion is that opening the SPA gives a working app with no
# further steps, so paying that cost here — once, in the command that claims the platform is up —
# is the difference between "the containers started" and "the platform works".
printf '\033[36mWarming the request path…\033[0m\n'

# Every request behind the gateway needs a token (spec 014), so an anonymous warm-up is refused at
# the edge and warms nothing. Sign in as the dev test user the identity server provisions, using the
# password from .env — the file the stack was configured from — and warm with that. Without the
# password the warm-up is skipped rather than failing: the health gates already passed.
test_user='postman-test@local.test'
test_password="$(grep -E '^TestUserPassword=' "$repository_root/.env" | head -n1 | cut -d= -f2- | tr -d '\r' || true)"
token=''
if [ -n "$test_password" ]; then
    token="$(curl -fsS -m 30 -X POST 'http://localhost:5205/connect/token' \
        -d grant_type=password -d client_id=ecommerce-web-spa-password \
        -d 'scope=openid profile ecommerce-api' -d "username=$test_user" \
        --data-urlencode "password=$test_password" 2>/dev/null \
        | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p' || true)"
fi

if [ -z "$token" ]; then
    printf '  (no TestUserPassword in .env, or sign-in unavailable: skipping the warm-up)\n'
else
    # Two passes: the first pays the cold cost (each service's first token validation fetches the
    # identity server's signing keys on top of the JIT and EF costs), and can itself exceed the BFF's
    # 3-second budget and answer 504. The second finds everything warm.
    for pass in 1 2; do
        for path in /bff/products /bff/basket "/bff/orders/00000000-0000-4000-8000-000000000000"; do
            # Each primes a different service's EF model and connection pool. Failures are ignored:
            # the orders probe is expected to 404, and a warm-up that cannot warm is not a reason to
            # refuse a stack whose health gates all passed.
            curl -fsS -m 30 -o /dev/null -H "Authorization: Bearer $token" "http://localhost:5300${path}" 2>/dev/null || true
        done
    done
fi

printf '\n\033[32mThe platform is up.\033[0m\n'
printf '  Storefront   http://localhost:4173\n'
printf '  Gateway      http://localhost:5300\n'
printf '  Sign in as   postman-test@local.test  (password: TestUserPassword in .env)\n\n'
printf 'Stop with ./scripts/down.sh, start over with ./scripts/reset.sh.\n'
