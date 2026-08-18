<#
.SYNOPSIS
    Stops the stack and throws the data away.

.DESCRIPTION
    The next start behaves like a first run: fresh databases, the seeded catalog reapplied by the
    migrators, and no previous orders (FR-008).

    Separate from down.ps1 deliberately. Stopping for the day and starting over are different
    intentions, and conflating them is how somebody loses an afternoon's test orders by closing
    their laptop.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "Cannot reset the stack: Docker is not installed, or is not on PATH." -ForegroundColor Red
    exit 1
}

Write-Host "This discards all local stack data - orders, baskets, and broker state." -ForegroundColor Yellow

Push-Location $repositoryRoot
try {
    # --volumes is the whole difference from down.ps1.
    & docker compose down --volumes
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host ""
    Write-Host "The stack is stopped and its data discarded." -ForegroundColor Green
    Write-Host "The next ./scripts/up.ps1 behaves like a first run, seed catalog included."
}
finally {
    Pop-Location
}
