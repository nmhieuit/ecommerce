<#
.SYNOPSIS
    Starts the whole platform locally, and does not return until it is usable.

.DESCRIPTION
    The documented command (005-one-command-local-run, FR-001).

    Everything this script does beyond calling Compose is prerequisite checking, and that is the
    reason it exists: Compose cannot tell you *which* prerequisite is missing. With no daemon it
    prints a socket error; with no .env it would substitute an empty password and the database would
    fail later for a reason that looks unrelated. FR-011 requires the missing thing to be named.

    It deliberately does NOT create .env for you. The setup is exactly two steps — copy the
    template, run this — and a script that silently generates a credentials file teaches a habit
    that is wrong the moment it is not local (spec SC-002).
#>
[CmdletBinding()]
param(
    # Publishes the internal service ports as well, for regenerating the API client or inspecting
    # the broker. See contracts/stack-interface.md.
    [switch]$Debug
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Stop-WithReason {
    param([string]$Reason)
    Write-Host "Cannot start the stack: $Reason" -ForegroundColor Red
    exit 1
}

# --- prerequisites, each failing with one sentence naming what is missing (FR-011) ---------------

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Stop-WithReason "Docker is not installed, or is not on PATH. Install Docker Desktop from https://docs.docker.com/get-docker/."
}

docker info --format '{{.ServerVersion}}' 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Stop-WithReason "the Docker daemon is not responding. Start Docker Desktop and try again."
}

$envFile = Join-Path $repositoryRoot '.env'
if (-not (Test-Path $envFile)) {
    Stop-WithReason "'.env' does not exist. Copy the template first:  cp .env.example .env  (no editing required)."
}

# The floor is what the stack actually needs with headroom, not a guess: one SQL Server, six
# services, a broker, a cache, a collector, and the storefront.
$requiredMemoryGb = 6
$daemonMemoryBytes = [int64](docker info --format '{{.MemTotal}}')
$daemonMemoryGb = [math]::Round($daemonMemoryBytes / 1GB, 1)
if ($daemonMemoryGb -lt $requiredMemoryGb) {
    Stop-WithReason "Docker has ${daemonMemoryGb} GB of memory available but the stack needs ${requiredMemoryGb} GB. Raise it in Docker Desktop > Settings > Resources."
}

# --- start ---------------------------------------------------------------------------------------

Push-Location $repositoryRoot
try {
    $composeArgs = @('compose')
    if ($Debug) { $composeArgs += @('--profile', 'debug') }

    # --build so a source change is running code rather than a stale image (FR-009).
    # --wait so this returns only once every component is healthy, and non-zero if one is not
    #   (FR-002, FR-010).
    $composeArgs += @('up', '--build', '--wait')

    Write-Host "Starting the platform. First run builds images and takes a few minutes." -ForegroundColor Cyan
    & docker @composeArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "The stack did not come up. The component that failed is named above; its logs:" -ForegroundColor Red
        Write-Host "  docker compose logs <component>" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host ""
    Write-Host "The platform is up." -ForegroundColor Green
    Write-Host "  Storefront   http://localhost:4173"
    Write-Host "  Gateway      http://localhost:5300"
    Write-Host ""
    Write-Host "Stop with ./scripts/down.ps1, start over with ./scripts/reset.ps1."
}
finally {
    Pop-Location
}
