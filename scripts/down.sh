#!/usr/bin/env bash
#
# Stops the stack. Keeps your data.
#
# Removes every container this stack started and releases every port it held (FR-006), while
# leaving the volumes alone so the next start finds your orders and basket where you left them
# (FR-007). To discard data as well, use reset.sh — that is a separate command on purpose.

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

if ! command -v docker >/dev/null 2>&1; then
    printf '\033[31mCannot stop the stack: Docker is not installed, or is not on PATH.\033[0m\n' >&2
    exit 1
fi

docker compose down

printf '\n\033[32mThe stack is stopped. Your data is kept.\033[0m\n'
printf 'Start again with ./scripts/up.sh, or discard the data with ./scripts/reset.sh.\n'
