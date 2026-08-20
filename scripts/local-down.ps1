<#
.SYNOPSIS
    Stops the local test stack. Keeps your data.

.DESCRIPTION
    Removes every container `docker-compose.local.yml` started and releases every port it held,
    while leaving the four database volumes alone so the next start finds your orders and baskets
    where you left them.

    Only ever touches the `ecomerce-local` project — the -f flag is what keeps `ecomerce-stack`
    (docker-compose.yml) and `ecomerce` (docker-compose.deps.yml) out of its reach.
#>
[CmdletBinding()]
param(
    # Discards the four databases and the broker state as well, so the next start behaves like a
    # first run: fresh schema, seeded catalog reapplied, no previous orders.
    [switch]$DiscardData
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$composeFile = 'docker-compose.local.yml'

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "Cannot stop the local stack: Docker is not installed, or is not on PATH." -ForegroundColor Red
    exit 1
}

if ($DiscardData) {
    Write-Host "This discards all local-stack data - orders, baskets, and broker state." -ForegroundColor Yellow
}

Push-Location $repositoryRoot
try {
    $composeArgs = @('compose', '-f', $composeFile, 'down')
    if ($DiscardData) { $composeArgs += '--volumes' }

    & docker @composeArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host ""
    if ($DiscardData) {
        Write-Host "The local stack is stopped and its data discarded." -ForegroundColor Green
        Write-Host "The next ./scripts/local-up.ps1 behaves like a first run, seed catalog included."
    }
    else {
        Write-Host "The local stack is stopped. Your data is kept." -ForegroundColor Green
        Write-Host "Start again with ./scripts/local-up.ps1, or discard the data with ./scripts/local-down.ps1 -DiscardData."
    }
}
finally {
    Pop-Location
}
