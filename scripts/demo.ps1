<#
.SYNOPSIS
    Runs the Phase 1 order demo end to end, and fails loudly if any part of it does not hold.

.DESCRIPTION
    The one command the demo costs (006-e2e-order-demo, FR-007c). Its contract -- what it does, in
    what order, and what it leaves behind -- is specs/006-e2e-order-demo/contracts/demo-interface.md.

    Like up.ps1, most of what this does beyond calling other tools is prerequisite checking, for the
    same reason: the underlying tools cannot tell you *which* prerequisite is missing. Playwright
    against a stopped stack reports a navigation timeout, which reads like a broken test rather than
    a stack nobody started.

    It is deliberately NOT wired into any pipeline, blocking or scheduled. Gating the build belongs
    to the story that owns the build gate (SCRUM-22); this command is run by a person who wants to
    watch an order get placed (spec Clarifications, research.md Decision 12).

    ASCII only, deliberately. Windows PowerShell 5.1 reads a .ps1 without a byte-order mark as ANSI,
    and no script in this repository carries one -- so a stray em dash here becomes three mangled
    bytes and, inside a double-quoted string, can turn the next token into a parameter. That is not
    hypothetical: it is how the first version of this file failed to parse.
#>
[CmdletBinding()]
param(
    # Skips bringing the stack up, for a repeat run against one that is already warm. The stack must
    # already be in demo mode; this script checks rather than assuming.
    [switch]$SkipStart
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

$storefrontUrl = 'http://localhost:4173'
$gatewayUrl = 'http://localhost:5300'
$ordersUrl = 'http://localhost:5041'
$basketsUrl = 'http://localhost:5188'

# The stub identity the gateway stamps onto every request (gateway appsettings.json, StubIdentity).
# The demo supplies these itself on the calls it makes straight to a service, because a
# host-originated call has no gateway in front of it to resolve a tenant.
$tenantId = 'contoso'
$subjectId = 'phase1-stub-user'

$webAppDir = Join-Path $repositoryRoot 'frontend/apps/web'
$playwrightCli = Join-Path $webAppDir 'node_modules/@playwright/test/cli.js'
$artifactsDir = Join-Path $repositoryRoot 'artifacts/demo'
$verificationFile = Join-Path $artifactsDir 'verification.txt'
$referenceFile = Join-Path $artifactsDir 'last-reference.txt'
$totalFile = Join-Path $artifactsDir 'last-total.txt'
$hopsFile = Join-Path $artifactsDir 'hops.txt'

# Every component the checkout path is narrated as touching. The demo asserts each one actually
# served traffic during the run (FR-011a).
$expectedHops = @('Gateway.Api', 'Bff.Api', 'Products.Api', 'Baskets.Api', 'Orders.Api')

function Stop-WithReason {
    param([string]$Reason)
    Write-Host "Cannot run the demo: $Reason" -ForegroundColor Red
    exit 1
}

# Runs a native executable without letting anything it writes to stderr become a terminating error.
#
# Windows PowerShell 5.1 wraps each stderr line from a native command in an ErrorRecord, and under
# $ErrorActionPreference = 'Stop' that ends the script -- even when the command goes on to exit 0.
# Node does exactly this: it warned "'NO_COLOR' is ignored due to 'FORCE_COLOR'" and killed a demo
# run that was otherwise succeeding. Exit codes are the signal here; stderr is just output.
# The exit code is handed back through a script-scoped variable rather than returned, because a
# function that returns it would also return everything the command printed: `& $Command` writes its
# output to the pipeline, so `$code = Invoke-Native {...}` collects the whole transcript and the exit
# code as one array. Comparing that array to 0 reported a failure on a run that had passed, and then
# exited 0 anyway. Streaming the output and keeping the code separate avoids both.
$script:NativeExitCode = 0

function Invoke-Native {
    param([scriptblock]$Command)

    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Command
        $script:NativeExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }
}

function Test-Endpoint {
    param([string]$Uri, [int]$TimeoutSec = 5)
    try {
        Invoke-WebRequest -Uri $Uri -TimeoutSec $TimeoutSec -UseBasicParsing | Out-Null
        return $true
    }
    catch {
        return $false
    }
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

if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Stop-WithReason "Node is not installed, or is not on PATH. The demo drives the storefront with Playwright, which Node runs."
}

# Playwright is invoked through Node against its own CLI rather than through pnpm. pnpm is what
# installs these dependencies, but once they are installed it is not needed to run them -- and one
# fewer thing required on PATH is one fewer reason the demo refuses to run on a machine that could
# have run it.
if (-not (Test-Path $playwrightCli)) {
    Stop-WithReason "the storefront's dependencies are not installed. Install them once:  pnpm install  (from the frontend/ directory)"
}

# Playwright's browsers live outside the repository, so a fresh clone with node_modules installed
# still has none. Checked here because Playwright's own failure -- "Executable doesn't exist" with a
# long path -- reads as a broken install rather than a one-time setup step nobody ran.
$browsersRoot = $env:PLAYWRIGHT_BROWSERS_PATH
if ([string]::IsNullOrWhiteSpace($browsersRoot)) {
    $browsersRoot = Join-Path $env:LOCALAPPDATA 'ms-playwright'
}
$chromiumInstalled = (Test-Path $browsersRoot) -and
    @(Get-ChildItem -Path $browsersRoot -Filter 'chromium*' -Directory -ErrorAction SilentlyContinue).Count -gt 0
if (-not $chromiumInstalled) {
    Stop-WithReason "Playwright's chromium is not installed. Install it once:  pnpm --filter @ecommerce/web exec playwright install chromium"
}

# --- start, or verify what is already running ------------------------------------------------------

Push-Location $repositoryRoot
try {
    if ($SkipStart) {
        # Demo mode's observable signature is the orders service answering on its own port. The
        # default stack publishes only the storefront and the gateway, so a stack that was brought
        # up with up.ps1 fails this check -- which is the point, because the verification step and
        # the clean-basket step both call services directly.
        if (-not (Test-Endpoint -Uri "$ordersUrl/health/ready")) {
            # Three different faults look identical from out here: the service was never published,
            # the service died, or the service is up and unwell. Asking Compose and Docker which it
            # is turns "nothing is answering" into a diagnosis, which is what FR-016 asks for.
            # Reporting "not in demo mode" for a crashed service sends the reader to the wrong fix.
            #
            # `ps --services` lists only what is running; `-a` includes stopped containers. The
            # difference is what separates "this service died" from "this service was never here".
            Invoke-Native { & docker compose -f docker-compose.yml -f docker-compose.demo.yml ps --services } 2>$null |
                Out-String -OutVariable composeRunning | Out-Null
            Invoke-Native { & docker compose -f docker-compose.yml -f docker-compose.demo.yml ps -a --services } 2>$null |
                Out-String -OutVariable composeDefined | Out-Null

            $runningServices = @($composeRunning -split "`r?`n" | ForEach-Object { $_.Trim() })
            $definedServices = @($composeDefined -split "`r?`n" | ForEach-Object { $_.Trim() })

            if ($runningServices -contains 'orders-api') {
                Invoke-Native { & docker ps --filter publish=5041 -q } 2>$null |
                    Out-String -OutVariable publishers | Out-Null

                if ([string]::IsNullOrWhiteSpace($publishers)) {
                    Stop-WithReason "the stack is up but not in demo mode - the orders service is running and its port is not published. Drop -SkipStart, or start it with:  docker compose -f docker-compose.yml -f docker-compose.demo.yml up --wait"
                }

                Stop-WithReason "the orders service is running and its port is published, but it is not answering on $ordersUrl. Check it with:  docker compose logs orders-api"
            }

            if ($definedServices -contains 'orders-api') {
                Stop-WithReason "the orders service belongs to this stack but is not running, so the demo cannot read back the order it places. Start it with:  docker compose -f docker-compose.yml -f docker-compose.demo.yml up --wait orders-api"
            }

            Stop-WithReason "the stack is not running in demo mode, so nothing is answering on $ordersUrl. Drop -SkipStart, or start it with:  docker compose -f docker-compose.yml -f docker-compose.demo.yml up --wait"
        }

        Write-Host "Using the stack that is already up." -ForegroundColor Cyan
    }
    else {
        # Layered over the default stack rather than replacing it: demo mode publishes the two
        # services the demo speaks to and makes the collector print spans, and changes nothing else.
        # See docker-compose.demo.yml.
        #
        # --build so a source change is running code rather than a stale image.
        # --wait so this returns only once every component is healthy, and non-zero if one is not,
        #   which is FR-009 -- the demo must not begin against a partially available stack.
        Write-Host "Starting the platform in demo mode. First run builds images and takes a few minutes." -ForegroundColor Cyan

        # Compose reports build and health progress on stderr, which 5.1 would otherwise treat as
        # a failure. Same reason as Invoke-Native's, same fix.
        Invoke-Native {
            & docker compose -f docker-compose.yml -f docker-compose.demo.yml up --build --wait
        }
        $composeExitCode = $script:NativeExitCode

        if ($composeExitCode -ne 0) {
            Write-Host ""
            Write-Host "The stack did not come up, so the demo did not run. The component that failed is named above; its logs:" -ForegroundColor Red
            Write-Host "  docker compose logs <component>" -ForegroundColor Red
            exit $composeExitCode
        }

        # The same cold-start warm-up up.ps1 performs, and for the same measured reason: health
        # checks say a service can reach its database, not that it can serve a request inside the
        # BFF's downstream budget. Skipping it here would make the first demo after a cold start
        # fail on a 504 from a stack every gate called healthy.
        Write-Host "Warming the request path..." -ForegroundColor Cyan
        foreach ($path in @('/bff/products', '/bff/basket', '/bff/orders/00000000-0000-4000-8000-000000000000')) {
            Test-Endpoint -Uri "$gatewayUrl$path" -TimeoutSec 30 | Out-Null
        }
    }

    # Fail before the flow rather than during it, so "the storefront is not being served" never
    # arrives disguised as a failed assertion about a product list.
    if (-not (Test-Endpoint -Uri $storefrontUrl)) {
        Stop-WithReason "the storefront is not answering on $storefrontUrl, so there is nothing to demonstrate."
    }
    if (-not (Test-Endpoint -Uri "$basketsUrl/health/ready")) {
        Stop-WithReason "the baskets service is not answering on $basketsUrl, so the demo cannot start from a clean basket."
    }

    # --- remember what the last run produced, before this run overwrites it ------------------------
    #
    # Read now rather than after, so the comparison at the end is against a genuinely earlier run
    # (FR-007: a repeat run must place a new order, not re-report the previous one).
    $previousReference = ''
    if (Test-Path $referenceFile) {
        $previousReference = (Get-Content $referenceFile -Raw).Trim()
    }

    # --- mark the evidence window ------------------------------------------------------------------
    #
    # Everything the collector logs from here on belongs to this run. Taken before the basket is
    # cleared so the clearing call itself is inside the window - it is traffic this run caused.
    $evidenceSince = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss')

    # --- clean basket ------------------------------------------------------------------------------
    #
    # Straight to the baskets service, not through /bff/checkout. Checking out to empty a basket
    # would place a real order every time the demo started with something left in it, and the demo
    # would then have to account for orders nobody asked for (research.md Decision 7).
    #
    # 409 means it was already empty, which is success: the service is reporting there was nothing
    # to clear, not refusing to clear it.
    Write-Host "Clearing the basket..." -ForegroundColor Cyan
    try {
        Invoke-WebRequest -Uri "$basketsUrl/baskets/current/clear" -Method Post -TimeoutSec 10 `
            -Headers @{ 'X-Tenant-Id' = $tenantId; 'X-Subject-Id' = $subjectId } `
            -UseBasicParsing | Out-Null
    }
    catch {
        $status = 0
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
        }

        if ($status -ne 409) {
            Stop-WithReason "the basket could not be cleared (HTTP $status), so the demo would not start from a known state."
        }
    }

    # --- run the flow ------------------------------------------------------------------------------

    Write-Host "Running the demo flow..." -ForegroundColor Cyan
    Write-Host ""

    Push-Location $webAppDir
    try {
        Invoke-Native { & node $playwrightCli test --config playwright.demo.config.ts }
        $flowExitCode = $script:NativeExitCode
    }
    finally {
        Pop-Location
    }

    if ($flowExitCode -ne 0) {
        Write-Host ""
        Write-Host "The demo flow did not complete. The failing step and its assertion are named above." -ForegroundColor Red
        Write-Host "  Video and trace:  artifacts/demo/" -ForegroundColor Red
        exit $flowExitCode
    }

    # --- report ------------------------------------------------------------------------------------

    if (-not (Test-Path $referenceFile)) {
        Stop-WithReason "the flow reported success but wrote no order reference to $referenceFile, so there is nothing to verify."
    }

    $reference = (Get-Content $referenceFile -Raw).Trim()
    $total = (Get-Content $totalFile -Raw).Trim()

    # --- verify, straight against the orders service (FR-012, SC-004) --------------------------
    #
    # Two calls, both to the service that owns the record rather than to the database behind it or
    # the BFF in front of it. The story asks to query the orders service directly, and the
    # constitution forbids reaching into another component's store; this is the one reading that
    # satisfies both.

    # Without a tenant first. Not error handling -- the enforcement gate demonstrated (FR-006, User
    # Story 2 scenario 2). A call from this machine has no gateway in front of it to resolve a
    # tenant, so the service must refuse rather than answer from some default.
    $untenantedStatus = 0
    try {
        $untenanted = Invoke-WebRequest -Uri "$ordersUrl/orders/$reference" -TimeoutSec 10 -UseBasicParsing
        $untenantedStatus = [int]$untenanted.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $untenantedStatus = [int]$_.Exception.Response.StatusCode
        }
    }

    if ($untenantedStatus -ge 200 -and $untenantedStatus -lt 300) {
        Stop-WithReason "the orders service answered a request that resolved no tenant (HTTP $untenantedStatus). That is a tenant-isolation failure, not a demo failure."
    }

    # Then with the tenant the gateway would have stamped.
    try {
        $order = Invoke-RestMethod -Uri "$ordersUrl/orders/$reference" -TimeoutSec 10 `
            -Headers @{ 'X-Tenant-Id' = $tenantId }
    }
    catch {
        Stop-WithReason "the orders service did not return order $reference even with a tenant resolved."
    }

    $storedTenant = $order.tenantId

    if ([string]::IsNullOrWhiteSpace($storedTenant)) {
        Stop-WithReason "order $reference came back without a tenant. FR-005 requires every order to carry the tenant it was placed for."
    }

    if ($storedTenant -ne $tenantId) {
        Stop-WithReason "order $reference is attributed to '$storedTenant' but the placing request resolved '$tenantId'. That is a cross-tenant attribution fault."
    }

    # --- compose the report --------------------------------------------------------------------
    #
    # Shape fixed by specs/006-e2e-order-demo/data-model.md. It is a contract because User Story 2
    # scenario 3 requires somebody who did not build this to read it and see what was proved.
    New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null
    @(
        "ORDER PLACED"
        "  reference : $reference"
        "  total     : $total"
        "  tenant    : $storedTenant"
        ""
        "TENANT ATTRIBUTION"
        "  resolved tenant for the placing request : $tenantId"
        "  tenant stored on the order              : $storedTenant"
        "  match                                   : YES"
        ""
        "WITHOUT A TENANT"
        "  GET /orders/$reference  (no X-Tenant-Id)  ->  $untenantedStatus, no order returned"
        "  the orders service refuses to answer when no tenant was resolved"
        ""
    ) | Set-Content -Path $verificationFile -Encoding utf8

    # --- hop evidence (FR-011a, FR-011b, SC-006a) ----------------------------------------------
    #
    # Which components actually served this run, read from the spans they exported. The parsing is
    # not incidental - it is what makes the assertion mean anything (006 research.md, follow-up
    # measurement T005):
    #
    #   * anchored on `ResourceTraces`, because a bare `service.name=` also appears on `ResourceLog`
    #     lines from the logs pipeline, and in the collector's own resource block on every line;
    #   * every span whose url.path starts with /health is dropped, because Docker health-checks each
    #     service every five seconds. Measured on this stack: Parties.Api emitted 461 spans in a
    #     demo window and all 461 were health checks. Counting those, a component that served
    #     nothing looks busy - evidence worse than none, because it looks like evidence.
    Write-Host "Collecting hop evidence..." -ForegroundColor Cyan

    Invoke-Native {
        & docker compose -f docker-compose.yml -f docker-compose.demo.yml logs --since $evidenceSince otel-collector
    } 2>$null | Out-String -OutVariable collectorLog | Out-Null

    $spansByService = @{}
    $currentService = $null

    foreach ($line in ($collectorLog -split "`r?`n")) {
        $resource = [regex]::Match($line, 'ResourceTraces #\d+ service\.name=([A-Za-z0-9._-]+)')
        if ($resource.Success) {
            $currentService = $resource.Groups[1].Value
            continue
        }

        $urlPath = [regex]::Match($line, 'url\.path=(\S+)')
        if ($urlPath.Success -and $currentService -and -not $urlPath.Groups[1].Value.StartsWith('/health')) {
            if (-not $spansByService.ContainsKey($currentService)) {
                $spansByService[$currentService] = 0
            }
            $spansByService[$currentService]++
        }
    }

    ($spansByService.Keys | Sort-Object | ForEach-Object { "$_`t$($spansByService[$_])" }) |
        Set-Content -Path $hopsFile -Encoding utf8

    $missingHops = @($expectedHops | Where-Object { -not $spansByService.ContainsKey($_) })

    if ($missingHops.Count -gt 0) {
        Write-Host ""
        Write-Host "Hop evidence is incomplete. These components served no non-health traffic during the run: $($missingHops -join ', ')" -ForegroundColor Red
        Write-Host "  Collected evidence: $hopsFile" -ForegroundColor Red
        Write-Host "  A demo that cannot show every named hop served it has not demonstrated the path (FR-011a)." -ForegroundColor Red
        exit 1
    }

    $hopLines = @('HOPS THAT SERVED THIS RUN')
    foreach ($service in ($spansByService.Keys | Sort-Object)) {
        $hopLines += ("  {0,-14} spans: {1}" -f $service, $spansByService[$service])
    }
    $hopLines += ''
    $hopLines | Add-Content -Path $verificationFile -Encoding utf8

    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Green
    Get-Content $verificationFile | Write-Host

    # --- repeatability (FR-007, SC-003) ------------------------------------------------------------
    #
    # The claim this story makes is that the demo is repeatable rather than a one-off. Two runs
    # reporting the same order would mean the second re-read the first's result instead of placing
    # anything, and that would pass every other check here.
    if ($previousReference) {
        if ($reference -eq $previousReference) {
            Stop-WithReason "this run reported the same order as the previous one ($reference). A repeat run must place a new order."
        }

        Write-Host "REPEATABLE"
        Write-Host "  previous run : $previousReference"
        Write-Host "  this run     : $reference"
        Write-Host "  distinct     : YES"
        Write-Host ""
    }

    Write-Host "================================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "The demo completed." -ForegroundColor Green
    Write-Host "  Recording    artifacts/demo/"
    Write-Host "  Run it again with:  ./scripts/demo.ps1 -SkipStart"
}
finally {
    Pop-Location
}
