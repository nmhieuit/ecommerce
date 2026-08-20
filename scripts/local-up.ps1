<#
.SYNOPSIS
    Starts the stack with every port published, for trying the test cases by hand.

.DESCRIPTION
    The same prerequisite checks as up.ps1, plus one this stack needs and the default one does not:
    it refuses to start while `ecomerce-stack` or `ecomerce` is running. Both hold ports this file
    wants, and Compose's own failure for that is a bind error naming a port number — true, but it
    does not say which of your stacks is holding it.

    Deliberately no warm-up pass, unlike up.ps1. That exists so the first shopper request does not
    pay JIT and EF model-building costs inside the BFF's 3-second budget; here the first request is
    usually a health probe you are making on purpose, and warming would hide exactly the cold-start
    behaviour you might be looking at.

    See docs/local-testing.md for what to do with the stack once it is up.
#>
[CmdletBinding()]
param(
    # Skips the image build. Faster when you are only restarting, and wrong the moment you have
    # edited source since the last build.
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$composeFile = 'docker-compose.local.yml'

function Stop-WithReason {
    param([string]$Reason)
    Write-Host "Cannot start the local stack: $Reason" -ForegroundColor Red
    exit 1
}

# --- prerequisites, each failing with one sentence naming what is missing -------------------------

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

# Four SQL Server instances rather than one, so the floor is higher than up.ps1's 6 GB. Each server
# reserves around 1 GB before it will start at all.
$requiredMemoryGb = 8
$daemonMemoryBytes = [int64](docker info --format '{{.MemTotal}}')
$daemonMemoryGb = [math]::Round($daemonMemoryBytes / 1GB, 1)
if ($daemonMemoryGb -lt $requiredMemoryGb) {
    Stop-WithReason "Docker has ${daemonMemoryGb} GB of memory available but this stack runs four SQL Server instances and needs ${requiredMemoryGb} GB. Raise it in Docker Desktop > Settings > Resources."
}

# The port collision, named rather than left to Compose's bind error.
$conflicting = @(docker ps --filter 'label=com.docker.compose.project=ecomerce-stack' --format '{{.Names}}') +
               @(docker ps --filter 'label=com.docker.compose.project=ecomerce' --format '{{.Names}}')
if ($conflicting.Count -gt 0) {
    Stop-WithReason "another stack is running and holds ports 4173, 5300 and 14330-14333 ($($conflicting -join ', ')). Stop it first:  ./scripts/down.ps1"
}

# --- start ---------------------------------------------------------------------------------------

Push-Location $repositoryRoot
try {
    $composeArgs = @('compose', '-f', $composeFile, 'up')
    if (-not $NoBuild) { $composeArgs += '--build' }
    # --wait returns only once every component is healthy, and non-zero if one is not.
    $composeArgs += @('-d', '--wait')

    Write-Host "Starting the local stack. First run builds images and takes a few minutes." -ForegroundColor Cyan
    & docker @composeArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "The stack did not come up. The component that failed is named above; its logs:" -ForegroundColor Red
        Write-Host "  docker compose -f $composeFile logs <component>" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    Write-Host ""
    Write-Host "The local stack is up, with every port published." -ForegroundColor Green
    Write-Host "  Storefront     http://localhost:4173"
    Write-Host "  Gateway        http://localhost:5300"
    Write-Host "  BFF + OpenAPI  http://localhost:5301/openapi/v1.json"
    Write-Host "  Products       http://localhost:5088/health/ready"
    Write-Host "  Baskets        http://localhost:5188/health/ready"
    Write-Host "  Orders         http://localhost:5041/health/ready"
    Write-Host "  Parties        http://localhost:5204/health/ready"
    Write-Host "  Databases      localhost,14330 parties | 14331 products | 14332 baskets | 14333 orders"
    Write-Host "  RabbitMQ UI    http://localhost:15672  (guest/guest)"
    Write-Host ""
    Write-Host "Scenarios to try: docs/local-testing.md"
    Write-Host "Stop with ./scripts/local-down.ps1."
}
finally {
    Pop-Location
}
