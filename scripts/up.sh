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
    compose_args+=(--profile debug)
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

printf '\n\033[32mThe platform is up.\033[0m\n'
printf '  Storefront   http://localhost:4173\n'
printf '  Gateway      http://localhost:5300\n\n'
printf 'Stop with ./scripts/down.sh, start over with ./scripts/reset.sh.\n'
