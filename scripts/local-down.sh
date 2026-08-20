#!/usr/bin/env bash
#
# Stops the local test stack. Keeps your data.
#
# Removes every container docker-compose.local.yml started and releases every port it held, while
# leaving the four database volumes alone so the next start finds your orders and baskets where you
# left them. Only ever touches the `ecomerce-local` project — the -f flag is what keeps
# `ecomerce-stack` and `ecomerce` out of its reach.
#
# Usage:
#   ./scripts/local-down.sh                  stop, keep data
#   ./scripts/local-down.sh --discard-data    stop and throw the databases away

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
compose_file="docker-compose.local.yml"
cd "$repository_root"

if ! command -v docker >/dev/null 2>&1; then
    printf '\033[31mCannot stop the local stack: Docker is not installed, or is not on PATH.\033[0m\n' >&2
    exit 1
fi

compose_args=(compose -f "$compose_file" down)
discard_data=false
if [ "${1:-}" = "--discard-data" ]; then
    discard_data=true
    compose_args+=(--volumes)
    printf '\033[33mThis discards all local-stack data - orders, baskets, and broker state.\033[0m\n'
fi

docker "${compose_args[@]}"

printf '\n'
if [ "$discard_data" = true ]; then
    printf '\033[32mThe local stack is stopped and its data discarded.\033[0m\n'
    printf 'The next ./scripts/local-up.sh behaves like a first run, seed catalog included.\n'
else
    printf '\033[32mThe local stack is stopped. Your data is kept.\033[0m\n'
    printf 'Start again with ./scripts/local-up.sh, or discard the data with ./scripts/local-down.sh --discard-data.\n'
fi
