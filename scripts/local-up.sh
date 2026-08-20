#!/usr/bin/env bash
#
# Starts the stack with every port published, for trying the test cases by hand.
#
# The POSIX twin of local-up.ps1 — see that file for why the prerequisite checks exist, and why
# there is deliberately no warm-up pass here. Scenarios: docs/local-testing.md.
#
# Usage:
#   ./scripts/local-up.sh              build images, then start
#   ./scripts/local-up.sh --no-build   skip the build (wrong if you have edited source)

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
compose_file="docker-compose.local.yml"
# Four SQL Server instances rather than one, so the floor is higher than up.sh's 6 GB.
required_memory_gb=8

stop_with_reason() {
    printf '\033[31mCannot start the local stack: %s\033[0m\n' "$1" >&2
    exit 1
}

# --- prerequisites, each failing with one sentence naming what is missing -------------------------

command -v docker >/dev/null 2>&1 \
    || stop_with_reason "Docker is not installed, or is not on PATH. Install it from https://docs.docker.com/get-docker/."

docker info --format '{{.ServerVersion}}' >/dev/null 2>&1 \
    || stop_with_reason "the Docker daemon is not responding. Start Docker and try again."

[ -f "$repository_root/.env" ] \
    || stop_with_reason "'.env' does not exist. Copy the template first:  cp .env.example .env  (no editing required)."

daemon_memory_bytes="$(docker info --format '{{.MemTotal}}')"
daemon_memory_gb=$(( daemon_memory_bytes / 1024 / 1024 / 1024 ))
if [ "$daemon_memory_gb" -lt "$required_memory_gb" ]; then
    stop_with_reason "Docker has ${daemon_memory_gb} GB of memory available but this stack runs four SQL Server instances and needs ${required_memory_gb} GB. Raise it in your Docker settings."
fi

# The port collision, named rather than left to Compose's bind error.
conflicting="$(
    docker ps --filter 'label=com.docker.compose.project=ecomerce-stack' --format '{{.Names}}'
    docker ps --filter 'label=com.docker.compose.project=ecomerce' --format '{{.Names}}'
)"
if [ -n "$conflicting" ]; then
    stop_with_reason "another stack is running and holds ports 4173, 5300 and 14330-14333 ($(echo "$conflicting" | tr '\n' ' ')). Stop it first:  ./scripts/down.sh"
fi

# --- start ---------------------------------------------------------------------------------------

cd "$repository_root"

compose_args=(compose -f "$compose_file" up)
if [ "${1:-}" != "--no-build" ]; then
    compose_args+=(--build)
fi
# --wait returns only once every component is healthy, non-zero if one is not.
compose_args+=(-d --wait)

printf '\033[36mStarting the local stack. First run builds images and takes a few minutes.\033[0m\n'

if ! docker "${compose_args[@]}"; then
    printf '\n\033[31mThe stack did not come up. The component that failed is named above; its logs:\033[0m\n' >&2
    printf '\033[31m  docker compose -f %s logs <component>\033[0m\n' "$compose_file" >&2
    exit 1
fi

printf '\n\033[32mThe local stack is up, with every port published.\033[0m\n'
printf '  Storefront     http://localhost:4173\n'
printf '  Gateway        http://localhost:5300\n'
printf '  BFF + OpenAPI  http://localhost:5301/openapi/v1.json\n'
printf '  Products       http://localhost:5088/health/ready\n'
printf '  Baskets        http://localhost:5188/health/ready\n'
printf '  Orders         http://localhost:5041/health/ready\n'
printf '  Parties        http://localhost:5204/health/ready\n'
printf '  Databases      localhost,14330 parties | 14331 products | 14332 baskets | 14333 orders\n'
printf '  RabbitMQ UI    http://localhost:15672  (guest/guest)\n\n'
printf 'Scenarios to try: docs/local-testing.md\n'
printf 'Stop with ./scripts/local-down.sh.\n'
