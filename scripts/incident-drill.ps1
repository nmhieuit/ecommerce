<#
.SYNOPSIS
    Diễn tập sự cố thật: tiêm một hỏng hóc bí mật vào 1 trong 7 service, chỉ bằng cấu hình sai
    (spec 028-incident-oncall-drill, SCRUM-36).

.DESCRIPTION
    -Start   Bốc thăm service, loại hỏng hóc, tham số và thời điểm tiêm (0–30 phút), niêm phong
             lựa chọn vào .incident-drill/<runId>/sealed.json, chỉ in runId và mã băm SHA-256, rồi
             giao việc tiêm cho một tiến trình nền ẩn và trả về ngay.
    -Load    Tải nền cho cả 7 service: lấy token (folder Postman 00) MỘT lần mỗi 30 phút, xuất
             environment có token, rồi chạy folder 26 + 28 bằng newman với environment đó trong
             30 phút; lặp lại tới khi Ctrl+C. Lấy token mỗi vòng làm identity luôn vượt SLO p95
             (POST /connect/token p50 ~310 ms) — người dùng chốt 30 phút (T018, 2026-10-02).
    -Reveal  Kiểm mã băm, in lựa chọn và thời điểm tiêm thực tế, xoá file compose override tạm.
             Chỉ chạy SAU khi sự cố đã được giải quyết (đạt SLO liên tục 15 phút).

    Ba loại hỏng hóc (research.md Quyết định 3, đã duyệt):
      A  đích kết nối sai   — DB trỏ tới incident-missing-db; BFF/gateway trỏ tới incident-missing-host
      B  cạn pool           — MaxConnectionsPerServer=1, chỉ cho gateway
      C  5xx của 027        — gửi request mang X-Chaos-Fault: 5xx vào service đích, tỷ lệ 5–50%

    Script KHÔNG sửa file nào đã commit: cấu hình sai chỉ nằm trong
    .incident-drill/<runId>/docker-compose.incident.yml, và mật khẩu không bao giờ được ghi ra — file
    override để Compose tự nội suy ${MSSQL_SA_PASSWORD} từ .env lúc chạy.

    Gỡ lỗi = chạy lại stack bình thường, không kèm override:
        docker compose -f docker-compose.local.yml up -d --build --wait

    Hợp đồng: specs/028-incident-oncall-drill/contracts/incident-drill-script-contract.md.

.EXAMPLE
    # Tải nền cho buổi diễn tập (chạy trong một terminal riêng, Ctrl+C để dừng).
    ./scripts/incident-drill.ps1 -Load

.EXAMPLE
    ./scripts/incident-drill.ps1 -Start
    ./scripts/incident-drill.ps1 -Reveal -RunId 20261001-210000

.EXAMPLE
    # Chế độ xác minh KHÔNG MÙ (QA, kiểm chứng từng loại) — không dùng cho buổi diễn tập.
    ./scripts/incident-drill.ps1 -Start -Service orders-api -FaultType B -DelaySeconds 0
#>
[CmdletBinding()]
param(
    [switch]$Start,
    [switch]$Reveal,
    [string]$RunId,

    # Chế độ không mù: chỉ định sẵn thay cho bốc thăm (bất biến 11).
    [ValidateSet('gateway-api', 'bff-api', 'products-api', 'baskets-api', 'orders-api', 'parties-api', 'identity-api')]
    [string]$Service,
    [ValidateSet('A', 'B', 'C')]
    [string]$FaultType,
    [ValidateRange(0, 1800)]
    [int]$DelaySeconds = -1,

    # Tải nền: số tiến trình newman song song và khoảng nghỉ giữa các request (newman bắt buộc > 0).
    [switch]$Load,
    [ValidateRange(1, 16)]
    [int]$Parallel = 1,
    [ValidateRange(1, 10000)]
    [int]$DelayMs = 200,

    # Nội bộ: tiến trình nền do -Start khởi chạy. Người vận hành không gọi trực tiếp.
    [switch]$RunInjector
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$drillRoot = Join-Path $repositoryRoot '.incident-drill'
$composeFile = Join-Path $repositoryRoot 'docker-compose.local.yml'
$composeProject = 'ecomerce-local'

# Mỗi service: tên OTel (resource.attributes.service.name), cổng publish trên máy, route nhận header
# của loại C (đã duyệt), và khoá connection string nếu có DB.
$services = [ordered]@{
    'gateway-api'  = @{ OtelName = 'Gateway.Api';  Port = 5300; Route = '/bff/products';                      Db = $null }
    'bff-api'      = @{ OtelName = 'Bff.Api';      Port = 5301; Route = '/bff/products';                      Db = $null }
    'products-api' = @{ OtelName = 'Products.Api'; Port = 5088; Route = '/products';                          Db = 'PRODUCTS' }
    'baskets-api'  = @{ OtelName = 'Baskets.Api';  Port = 5188; Route = '/baskets/current';                   Db = 'BASKETS' }
    'orders-api'   = @{ OtelName = 'Orders.Api';   Port = 5041; Route = '/orders/{guid}';                     Db = 'ORDERS' }
    'parties-api'  = @{ OtelName = 'Parties.Api';  Port = 5204; Route = '/parties/{guid}';                    Db = 'PARTIES' }
    'identity-api' = @{ OtelName = 'Identity.Api'; Port = 5205; Route = '/.well-known/openid-configuration'; Db = 'IDENTITY' }
}
$bffDownstreams = @('ProductsApi', 'BasketsApi', 'OrdersApi', 'PartiesApi')

function Stop-WithReason {
    param([string]$Reason)
    Write-Host "Không chạy được diễn tập: $Reason" -ForegroundColor Red
    exit 1
}

function Get-VietnamNow {
    [System.TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow, 'SE Asia Standard Time')
}

function Format-Vietnam {
    param([DateTime]$Value)
    $Value.ToString('yyyy-MM-ddTHH:mm:ss') + '+07:00'
}

function Get-ApplicableFaultTypes {
    param([string]$Name)
    # Loại B chỉ áp dụng cho gateway (người dùng chốt 2026-10-02): BFF không có tham số pool HTTP;
    # với 5 service có DB, Max Pool Size=1 dưới tải nền không gây 5xx hay trễ (T017: orders 0 lỗi,
    # p95 25 ms), còn MaxConnectionsPerServer=1 ở gateway cho 45% span 5xx.
    if ($Name -eq 'gateway-api') { return @('A', 'B', 'C') }
    return @('A', 'C')
}

function Get-ContainerId {
    param([string]$Name)
    $ErrorActionPreference = 'Continue'
    $id = docker inspect -f '{{.Id}}' "$composeProject-$Name-1" 2>$null
    $exitCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($exitCode -ne 0) { return $null }
    return $id
}

# Connection string đúng như docker-compose.local.yml dựng — viết dưới dạng biểu thức nội suy của
# Compose, để mật khẩu chỉ được đọc từ .env lúc chạy, không bao giờ nằm trong file override.
function Get-DbConnectionExpression {
    param([string]$Prefix, [string]$ServerHost)
    $lower = $Prefix.ToLowerInvariant()
    if (-not $ServerHost) { $ServerHost = "`${${Prefix}_DB_HOST:-$lower-db}" }
    return "Server=$ServerHost;Database=`${${Prefix}_DB_NAME:-$lower};User Id=`${SQLSERVER_USER:-sa};Password=`${MSSQL_SA_PASSWORD};`${SQLSERVER_CONNECTION_OPTIONS:-TrustServerCertificate=true};`${SQLSERVER_FAIL_FAST_OPTIONS:-Connect Timeout=3;ConnectRetryCount=0}"
}

function Get-OverrideEnvironment {
    param($Sealed)
    $info = $services[$Sealed.service]
    $environment = [ordered]@{}
    $dbKey = $null
    if ($info.Db) {
        $dbKey = 'ConnectionStrings__' + (Get-Culture).TextInfo.ToTitleCase($info.Db.ToLowerInvariant()) + 'Db'
    }

    switch ($Sealed.faultType) {
        'A' {
            if ($info.Db) {
                $environment[$dbKey] = Get-DbConnectionExpression -Prefix $info.Db -ServerHost 'incident-missing-db'
            }
            elseif ($Sealed.service -eq 'bff-api') {
                $environment["Services__$($Sealed.faultDetail.downstream)__BaseUrl"] = 'http://incident-missing-host:8080'
            }
            else {
                $environment['ReverseProxy__Clusters__bff-cluster__Destinations__bff__Address'] = 'http://incident-missing-host:8080'
            }
        }
        'B' {
            # Chỉ gateway (Get-ApplicableFaultTypes).
            $environment['ReverseProxy__Clusters__bff-cluster__HttpClient__MaxConnectionsPerServer'] = '1'
        }
        'C' {
            # Không override: cờ Chaos__AllowFaultInjection lấy từ .env (đã kiểm là true ở -Start).
        }
    }
    return $environment
}

function Write-OverrideFile {
    param($Sealed, [string]$Path)
    $lines = @('# Sinh bởi scripts/incident-drill.ps1 — spec 028. Không commit. -Reveal xoá file này.', 'services:')
    foreach ($name in $services.Keys) {
        $lines += "  ${name}:"
        $environment = @{}
        if ($name -eq $Sealed.service) { $environment = Get-OverrideEnvironment -Sealed $Sealed }
        if ($environment.Count -eq 0) {
            # Khối rỗng cho mọi service khác: chúng được tạo lại với đúng cấu hình gốc.
            $lines += '    environment: {}'
            continue
        }
        $lines += '    environment:'
        foreach ($key in $environment.Keys) {
            $value = $environment[$key].Replace("'", "''")
            $lines += "      ${key}: '$value'"
        }
    }
    # UTF-8 không BOM: Set-Content -Encoding UTF8 của PowerShell 5.1 ghi BOM vào đầu file YAML.
    [System.IO.File]::WriteAllLines($Path, [string[]]$lines, (New-Object System.Text.UTF8Encoding($false)))
}

function Test-FaultInjectionAllowed {
    $envFile = Join-Path $repositoryRoot '.env'
    if (-not (Test-Path $envFile)) { return $false }
    $line = Get-Content $envFile | Where-Object { $_ -match '^\s*CHAOS_ALLOW_FAULT_INJECTION\s*=' } | Select-Object -Last 1
    if (-not $line) { return $false }
    return (($line -split '=', 2)[1].Trim().Trim('"').Trim("'") -eq 'true')
}

# Số span của service trong 5 phút gần nhất → tốc độ nền (span/giây), dùng để tính tốc độ gửi header.
function Get-BaselineSpansPerSecond {
    param([string]$OtelName)
    $query = "FROM traces-generic.otel-default* | WHERE @timestamp > NOW() - 5 minutes AND resource.attributes.service.name == `"$OtelName`" | STATS c = COUNT(*)"
    $body = @{ query = $query } | ConvertTo-Json
    try {
        $result = Invoke-RestMethod -Method Post -Uri 'http://localhost:9200/_query' -ContentType 'application/json' -Body $body
        return [double]$result.values[0][0] / 300.0
    }
    catch { return 0.0 }
}

function Invoke-InjectorProcess {
    param([string]$Id)
    $runDirectory = Join-Path $drillRoot $Id
    $log = Join-Path $runDirectory 'injector.log'
    $sealed = Get-Content (Join-Path $runDirectory 'sealed.json') -Raw | ConvertFrom-Json

    try {
        if ($sealed.delaySeconds -gt 0) { Start-Sleep -Seconds $sealed.delaySeconds }

        $baseline = 0.0
        if ($sealed.faultType -eq 'C') { $baseline = Get-BaselineSpansPerSecond -OtelName $services[$sealed.service].OtelName }

        $override = Join-Path $runDirectory 'docker-compose.incident.yml'
        Write-OverrideFile -Sealed $sealed -Path $override

        # Tạo lại cả 7 liền nhau để uptime trong `docker ps` không lộ service đích (bất biến 5).
        # Mỗi service một lệnh riêng: một lệnh gộp sẽ bắt BFF/gateway chờ service đích healthy theo
        # depends_on — service đích không bao giờ healthy nên BFF/gateway kẹt ở "Created" và Compose in
        # đích danh service hỏng (phát hiện khi chạy T016, 2026-10-01).
        foreach ($name in @($services.Keys)) {
            # Compose ghi tiến độ ra stderr; với ErrorActionPreference = Stop, PowerShell 5.1 coi đó là lỗi.
            $ErrorActionPreference = 'Continue'
            $output = & docker compose -p $composeProject -f $composeFile -f $override up -d --force-recreate --no-deps $name 2>&1 | ForEach-Object { "$_" }
            $exitCode = $LASTEXITCODE
            $ErrorActionPreference = 'Stop'
            Add-Content -Path $log -Value ($output | Out-String) -Encoding UTF8
            if ($exitCode -ne 0) { throw "docker compose up $name thất bại (mã $exitCode)" }
        }

        Set-Content -Path (Join-Path $runDirectory 'injected-at.txt') -Value (Format-Vietnam (Get-VietnamNow)) -Encoding UTF8

        if ($sealed.faultType -eq 'C') {
            $info = $services[$sealed.service]
            $injectedId = Get-ContainerId -Name $sealed.service
            $ratio = $sealed.errorRatePct / 100.0
            # Tốc độ gửi để header chiếm r trên tổng span: r/(1−r) × tốc độ nền. Không đặt mức sàn: mức sàn
            # 1 request/giây ban đầu làm parties (nền ~0.1 span/s) ra 75.7% thay vì 40% (T018, 2026-10-02).
            # Chỉ khi chưa đo được tốc độ nền (không có span) mới dùng 0.2 request/giây.
            $perSecond = $ratio / (1.0 - $ratio) * $baseline
            if ($perSecond -le 0) { $perSecond = 0.2 }
            $intervalMs = [int][math]::Max(10, 1000.0 / $perSecond)
            Add-Content -Path $log -Encoding UTF8 -Value "loai C: baseline=$baseline span/s, gui $perSecond req/s (moi $intervalMs ms)"

            # Dừng khi người vận hành đã tạo lại container đích (build lại từ master) — bất biến 8.
            # Kiểm container mỗi ~5 giây (không theo số request), để tốc độ gửi thấp vẫn dừng kịp.
            $lastCheck = [DateTime]::MinValue
            while ($true) {
                if (([DateTime]::UtcNow - $lastCheck).TotalSeconds -ge 5) {
                    $currentId = Get-ContainerId -Name $sealed.service
                    if (-not $currentId -or $currentId -ne $injectedId) { break }
                    $lastCheck = [DateTime]::UtcNow
                }
                $route = $info.Route.Replace('{guid}', [guid]::NewGuid().ToString())
                try {
                    Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:$($info.Port)$route" -Headers @{ 'X-Chaos-Fault' = '5xx' } -TimeoutSec 5 | Out-Null
                }
                catch { }
                # Ngủ từng đoạn ≤ 5 s để không lỡ lần kiểm container khi khoảng cách giữa hai request dài.
                $remaining = $intervalMs
                while ($remaining -gt 0) {
                    $step = [math]::Min($remaining, 5000)
                    Start-Sleep -Milliseconds $step
                    $remaining -= $step
                    if ($remaining -gt 0) {
                        $currentId = Get-ContainerId -Name $sealed.service
                        # Ép vòng ngoài kiểm lại ngay, để không gửi thêm request nào sau khi đã đổi container.
                        if (-not $currentId -or $currentId -ne $injectedId) { $lastCheck = [DateTime]::MinValue; break }
                    }
                }
            }
            Add-Content -Path $log -Encoding UTF8 -Value "loai C: container dich da duoc tao lai — dung gui header luc $(Format-Vietnam (Get-VietnamNow))"
        }
    }
    catch {
        Add-Content -Path $log -Encoding UTF8 -Value "LOI: $($_.Exception.Message)"
        exit 1
    }
}

function Invoke-BackgroundLoad {
    $collection = Join-Path $repositoryRoot 'postman/ecommerce.postman_collection.v2.json'
    $environment = Join-Path $repositoryRoot 'postman/local.postman_environment.v2.json'
    $loadDirectory = Join-Path $drillRoot 'load'
    New-Item -ItemType Directory -Force -Path $loadDirectory | Out-Null
    $tokenEnvironment = Join-Path $loadDirectory 'environment-with-token.json'
    $tokenFolder = '00 - Xác thực & phân quyền (Get Token)'
    $loadFolders = @('26 - Ngân sách hiệu năng luồng trọng yếu (browse → giỏ → checkout → đơn)',
                     '28 - Diễn tập sự cố: tải nền bổ sung (parties, identity)')
    $refreshMinutes = 30

    Write-Host "Tải nền: $Parallel tiến trình newman, nghỉ $DelayMs ms giữa request, token làm mới mỗi $refreshMinutes phút. Ctrl+C để dừng." -ForegroundColor Cyan
    $runners = @()
    try {
        while ($true) {
            $ErrorActionPreference = 'Continue'
            # npx.cmd, không phải npx: npx.ps1 của PowerShell báo "could not determine executable to run".
            & npx.cmd --yes newman@6.2.2 run $collection -e $environment --folder $tokenFolder --export-environment $tokenEnvironment --reporters cli *> (Join-Path $loadDirectory 'token.log')
            $ErrorActionPreference = 'Stop'
            if (-not (Test-Path $tokenEnvironment)) { Stop-WithReason "không lấy được token — xem $loadDirectory	oken.log." }
            Write-Host "$(Format-Vietnam (Get-VietnamNow))  đã lấy token mới"

            $runners = @()
            for ($k = 1; $k -le $Parallel; $k++) {
                $arguments = @('--yes', 'newman@6.2.2', 'run', "`"$collection`"", '-e', "`"$tokenEnvironment`"")
                foreach ($folder in $loadFolders) { $arguments += @('--folder', "`"$folder`"") }
                $arguments += @('-n', '1000000', '--delay-request', "$DelayMs", '--reporters', 'cli')
                $runners += Start-Process -FilePath 'npx.cmd' -ArgumentList $arguments -WindowStyle Hidden -PassThru `
                    -RedirectStandardOutput (Join-Path $loadDirectory "runner$k.log") -RedirectStandardError (Join-Path $loadDirectory "runner$k.err.log")
            }
            # Lấy token lại khi hết 30 phút, HOẶC ngay khi runner gặp 401: tạo lại identity (lúc tiêm lỗi)
            # làm token cũ mất hiệu lực, và 401 chặn ở gateway khiến service phía sau mất traffic — lỗi
            # tiêm vào có thể không lộ ra (T018, người dùng chốt 2026-10-02). Folder 26/28 không có
            # request nào chờ 401, nên mọi 401 trong log runner đều là token hỏng.
            # Tạo lại identity (mỗi lần tiêm đều tạo lại cả 7) đổi khoá ký: token cũ vẫn qua gateway (còn
            # giữ khoá cũ) nhưng bị service hạ lưu vừa tạo lại từ chối → BFF trả 502/504, không phải 401
            # (T018, 2026-10-02). Nên cũng lấy token lại khi Id container identity-api đổi.
            $identityId = Get-ContainerId -Name 'identity-api'
            $deadline = (Get-Date).AddMinutes($refreshMinutes)
            $reason = "hết $refreshMinutes phút"
            while ((Get-Date) -lt $deadline) {
                Start-Sleep -Seconds 15
                $unauthorized = 0
                for ($k = 1; $k -le $Parallel; $k++) {
                    $runnerLog = Join-Path $loadDirectory "runner$k.log"
                    if (Test-Path $runnerLog) {
                        $unauthorized += @(Select-String -Path $runnerLog -Pattern '401 Unauthorized' -SimpleMatch).Count
                    }
                }
                if ($unauthorized -gt 0) { $reason = 'runner gặp 401'; break }
                $currentIdentityId = Get-ContainerId -Name 'identity-api'
                if ($currentIdentityId -and $currentIdentityId -ne $identityId) {
                    for ($w = 0; $w -lt 60; $w++) {
                        $ErrorActionPreference = 'Continue'
                        $health = docker inspect -f '{{.State.Health.Status}}' "$composeProject-identity-api-1" 2>$null
                        $ErrorActionPreference = 'Stop'
                        if ($health -eq 'healthy') { break }
                        Start-Sleep -Seconds 5
                    }
                    $reason = 'identity-api được tạo lại'
                    break
                }
            }
            $runners | ForEach-Object { Stop-RunnerTree -Process $_ }
            Write-Host "$(Format-Vietnam (Get-VietnamNow))  lấy token lại ($reason)"
        }
    }
    finally {
        $runners | ForEach-Object { Stop-RunnerTree -Process $_ }
    }
}

# npx.cmd → cmd → node: dừng cả cây tiến trình, nếu không node vẫn chạy tiếp sau khi npx dừng.
function Stop-RunnerTree {
    param($Process)
    if (-not $Process -or $Process.HasExited) { return }
    # taskkill báo lỗi ra stderr khi một tiến trình con đã tự thoát; với ErrorActionPreference = Stop,
    # PowerShell 5.1 coi đó là lỗi chặn và làm -Load thoát giữa buổi (gặp thật 2026-10-02 11:00).
    $ErrorActionPreference = 'Continue'
    & taskkill.exe /PID $Process.Id /T /F *> $null
    $ErrorActionPreference = 'Stop'
}

# --- tải nền ------------------------------------------------------------------------------------

if ($Load) {
    Invoke-BackgroundLoad
    exit 0
}

# --- tiến trình nền -----------------------------------------------------------------------------

if ($RunInjector) {
    if (-not $RunId) { exit 1 }
    Invoke-InjectorProcess -Id $RunId
    exit 0
}

# --- -Reveal ------------------------------------------------------------------------------------

if ($Reveal) {
    if (-not $RunId) { Stop-WithReason "thiếu -RunId." }
    $runDirectory = Join-Path $drillRoot $RunId
    $sealedPath = Join-Path $runDirectory 'sealed.json'
    if (-not (Test-Path $sealedPath)) { Stop-WithReason "không có $sealedPath." }

    $expected = (Get-Content (Join-Path $runDirectory 'hash.txt') -Raw).Trim()
    $actual = (Get-FileHash -Algorithm SHA256 $sealedPath).Hash
    if ($actual -ne $expected) {
        Stop-WithReason "mã băm không khớp — sealed.json đã bị sửa sau khi niêm phong (ghi lúc -Start: $expected, tính lại: $actual)."
    }

    Write-Host "Mã băm khớp: $actual" -ForegroundColor Green
    Write-Host "Lựa chọn đã niêm phong:" -ForegroundColor Cyan
    Get-Content $sealedPath -Raw | Write-Host
    $injectedAt = Join-Path $runDirectory 'injected-at.txt'
    if (Test-Path $injectedAt) {
        Write-Host "Thời điểm tiêm thực tế: $((Get-Content $injectedAt -Raw).Trim())" -ForegroundColor Cyan
    }
    else {
        Write-Host "Chưa có thời điểm tiêm thực tế (tiến trình nền chưa tiêm, hoặc lỗi — xem injector.log)." -ForegroundColor Yellow
    }

    $override = Join-Path $runDirectory 'docker-compose.incident.yml'
    if (Test-Path $override) { Remove-Item $override }
    exit 0
}

# --- -Start -------------------------------------------------------------------------------------

if (-not $Start) {
    Write-Host "Dùng -Start hoặc -Reveal -RunId <id>. Xem: Get-Help $PSCommandPath -Full"
    exit 1
}

# Bất biến 1: cờ phải bật trong .env, nếu không thì không ghi gì, không gọi docker.
if (-not (Test-FaultInjectionAllowed)) {
    Stop-WithReason "CHAOS_ALLOW_FAULT_INJECTION không phải 'true' trong .env. Chỉ bật cờ này trong lúc diễn tập."
}

$blind = -not ($Service -or $FaultType -or $DelaySeconds -ge 0)

if (-not $Service) { $Service = @($services.Keys) | Get-Random }
$applicable = Get-ApplicableFaultTypes -Name $Service
if ($FaultType) {
    if ($applicable -notcontains $FaultType) { Stop-WithReason "loại $FaultType không áp dụng được cho $Service." }
}
else {
    $FaultType = $applicable | Get-Random
}
if ($DelaySeconds -lt 0) { $DelaySeconds = Get-Random -Minimum 0 -Maximum 1801 }

$faultDetail = [ordered]@{}
if ($FaultType -eq 'A' -and $Service -eq 'bff-api') { $faultDetail.downstream = $bffDownstreams | Get-Random }
$errorRatePct = $null
if ($FaultType -eq 'C') { $errorRatePct = Get-Random -Minimum 5 -Maximum 51 }

$now = Get-VietnamNow
$id = $now.ToString('yyyyMMdd-HHmmss')
$runDirectory = Join-Path $drillRoot $id
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

$sealed = [ordered]@{
    runId           = $id
    blind           = $blind
    service         = $Service
    faultType       = $FaultType
    faultDetail     = $faultDetail
    delaySeconds    = $DelaySeconds
    errorRatePct    = $errorRatePct
    plannedInjectAt = Format-Vietnam ($now.AddSeconds($DelaySeconds))
}
$sealedPath = Join-Path $runDirectory 'sealed.json'
$sealed | ConvertTo-Json -Depth 5 | Set-Content -Path $sealedPath -Encoding UTF8
$hash = (Get-FileHash -Algorithm SHA256 $sealedPath).Hash
Set-Content -Path (Join-Path $runDirectory 'hash.txt') -Value $hash -Encoding UTF8

Start-Process -WindowStyle Hidden -FilePath 'powershell.exe' -ArgumentList @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-RunInjector', '-RunId', $id
) | Out-Null

# Bất biến 2: chỉ runId và mã băm — không service, loại, tham số hay thời điểm.
if (-not $blind) {
    Write-Host "CHẾ ĐỘ KHÔNG MÙ — không dùng cho buổi diễn tập." -ForegroundColor Yellow
}
Write-Host "runId:  $id"
Write-Host "SHA256: $hash"
Write-Host "Dán mã băm vào Kibana Case khi mở Case. Sau khi sự cố đã giải quyết: ./scripts/incident-drill.ps1 -Reveal -RunId $id"
