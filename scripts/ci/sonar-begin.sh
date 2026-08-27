#!/usr/bin/env sh
# Starts a SonarQube analysis for the whole monorepo.
#
#   scripts/ci/sonar-begin.sh
#
# SonarScanner for .NET takes its settings as `/d:` command-line arguments and — unlike the generic
# CLI scanner — does not read a properties file of its own. Rather than splitting scanner settings
# between a properties file and the Jenkinsfile, this translates the committed
# sonar-scanner.properties into scanner arguments, keeping that file the single source of truth
# (see its header comment). That file is deliberately not named `sonar-project.properties` —
# SonarScanner for .NET 11.x hard-fails post-processing if a file with that reserved name exists
# anywhere under the analyzed root.
#
# Expects SONAR_HOST_URL and SONAR_TOKEN in the environment; Jenkins' `withSonarQubeEnv` block
# supplies both (SONAR_AUTH_TOKEN is accepted as the older name). Pull-request parameters come from
# the multibranch job's CHANGE_* variables so SonarQube attaches its analysis — and its PR
# decoration — to the right GitHub pull request.
set -eu

REPO_ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
cd "$REPO_ROOT"

PROPS="${SONAR_PROPERTIES:-sonar-scanner.properties}"
[ -f "$PROPS" ] || { echo "Missing ${PROPS}" >&2; exit 1; }

set -- begin

while IFS= read -r line || [ -n "$line" ]; do
    case "$line" in
        ''|'#'*|';'*) continue ;;
        *'='*) ;;
        *) continue ;;
    esac
    key=$(printf '%s' "${line%%=*}" | tr -d ' \t')
    value=${line#*=}
    case "$key" in
        sonar.projectKey)     set -- "$@" "/k:${value}" ;;
        sonar.projectName)    set -- "$@" "/n:${value}" ;;
        sonar.projectVersion) set -- "$@" "/v:${value}" ;;
        # Computed by the scanner from the MSBuild project graph and the project base directory;
        # passing them is rejected. Declared in the properties file for documentation only.
        sonar.sources|sonar.tests) ;;
        *) set -- "$@" "/d:${key}=${value}" ;;
    esac
done < "$PROPS"

: "${SONAR_HOST_URL:?SONAR_HOST_URL is not set — run inside a withSonarQubeEnv block}"
set -- "$@" "/d:sonar.host.url=${SONAR_HOST_URL}"

TOKEN="${SONAR_TOKEN:-${SONAR_AUTH_TOKEN:-}}"
[ -n "$TOKEN" ] && set -- "$@" "/d:sonar.token=${TOKEN}"

if [ -n "${CHANGE_ID:-}" ]; then
    set -- "$@" "/d:sonar.pullrequest.key=${CHANGE_ID}"
    [ -n "${CHANGE_BRANCH:-}" ] && set -- "$@" "/d:sonar.pullrequest.branch=${CHANGE_BRANCH}"
    [ -n "${CHANGE_TARGET:-}" ] && set -- "$@" "/d:sonar.pullrequest.base=${CHANGE_TARGET}"
elif [ -n "${BRANCH_NAME:-}" ]; then
    set -- "$@" "/d:sonar.branch.name=${BRANCH_NAME}"
fi

# The token is the only secret in the argument list and Jenkins masks it in the console log.
echo "dotnet sonarscanner begin (project $(grep -m1 '^sonar.projectKey=' "$PROPS" | cut -d= -f2))"
exec dotnet sonarscanner "$@"
