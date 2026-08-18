#!/usr/bin/env bash
#
# Stops the stack and throws the data away.
#
# The next start behaves like a first run: fresh databases, the seeded catalog reapplied by the
# migrators, and no previous orders (FR-008).
#
# Separate from down.sh deliberately. Stopping for the day and starting over are different
# intentions, and conflating them is how somebody loses an afternoon's test orders by closing their
# laptop.

set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

if ! command -v docker >/dev/null 2>&1; then
    printf '\033[31mCannot reset the stack: Docker is not installed, or is not on PATH.\033[0m\n' >&2
    exit 1
fi

printf '\033[33mThis discards all local stack data — orders, baskets, and broker state.\033[0m\n'

# --volumes is the whole difference from down.sh.
docker compose down --volumes

printf '\n\033[32mThe stack is stopped and its data discarded.\033[0m\n'
printf 'The next ./scripts/up.sh behaves like a first run, seed catalog included.\n'
