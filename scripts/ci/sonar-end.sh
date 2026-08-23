#!/usr/bin/env sh
# Ends a SonarQube analysis started by scripts/ci/sonar-begin.sh.
#
#   scripts/ci/sonar-end.sh
#
# SonarScanner for .NET requires credentials to be passed identically to both `begin` and `end` —
# passing a token to one and not the other fails with "Credentials must be passed in both begin
# and end steps or not at all". `begin` passes /d:sonar.token explicitly (it needs the full
# settings translation anyway); this mirrors just the token half for `end`.
set -eu

TOKEN="${SONAR_TOKEN:-${SONAR_AUTH_TOKEN:-}}"

if [ -n "$TOKEN" ]; then
    exec dotnet sonarscanner end "/d:sonar.token=${TOKEN}"
else
    exec dotnet sonarscanner end
fi
