<#
.SYNOPSIS
    Stops the stack. Keeps your data.

.DESCRIPTION
    Removes every container this stack started and releases every port it held (FR-006), while
    leaving the volumes alone so the next start finds your orders and basket where you left them
    (FR-007).

    To discard data as well, use reset.ps1 — that is a separate command on purpose.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "Cannot stop the stack: Docker is not installed, or is not on PATH." -ForegroundColor Red
    exit 1
}

Push-Location $repositoryRoot
try {
    & docker compose down
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host ""
    Write-Host "The stack is stopped. Your data is kept." -ForegroundColor Green
    Write-Host "Start again with ./scripts/up.ps1, or discard the data with ./scripts/reset.ps1."
}
finally {
    Pop-Location
}
