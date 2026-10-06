<#
.SYNOPSIS
    Diễn tập sự cố thật: tiêm lỗi vào hệ thống Docker Compose chỉ bằng cấu hình sai hoặc công cụ Docker bên ngoài
    (spec 028-incident-oncall-drill, SCRUM-36; mở rộng 8 nhóm lỗi ở spec 031-error-group-catalog).

.DESCRIPTION
    Hai dạng tiêm:
      (a) có chủ đích — chọn nhóm/loại và đích, một lệnh:  -Inject -Type <A-I> | -Group <1-8> [-Target <đích>] [-DurationSeconds <n>]
      (b) bất ngờ không báo trước — bốc thăm mù và niêm phong:  -Start

    -Start   Bốc thăm nhóm lỗi (đều 1/8), loại, đích, tham số và thời điểm tiêm (0–30 phút), niêm phong
             lựa chọn vào .incident-drill/<runId>/sealed.json, chỉ in runId và mã băm SHA-256, rồi
             giao việc tiêm cho một tiến trình nền ẩn và trả về ngay.
    -Inject  Tiêm ngay một loại lỗi có chủ đích (không phải bài mù); in rõ đã tiêm gì. -DurationSeconds đặt thì
             hết thời lượng script tự khôi phục; không đặt thì giữ lỗi tới khi -Restore.
    -Restore -RunId <id>  Khôi phục một lần chạy (mọi loại A–I): hoàn tác thao tác tiêm rồi chờ đích khoẻ (tối đa
             10 phút). Lần chạy còn đang chờ tiêm thì chỉ huỷ.
    -Hint    -RunId <id> -Level 1|2|3  Gợi ý theo mức cho một lần chạy: 1 = triệu chứng, 2 = tên nhóm, 3 = đáp án.
             Mỗi lần mở được ghi vào hint-log.json; không ghi vào Kibana Case, không đổi mốc thời gian.
    -Load    Tải nền cho cả 7 service: lấy token (folder Postman 00) MỘT lần mỗi 30 phút, xuất
             environment có token, rồi chạy folder 26 + 28 bằng newman với environment đó trong
             30 phút; lặp lại tới khi Ctrl+C. Lấy token mỗi vòng làm identity luôn vượt SLO p95
             (POST /connect/token p50 ~310 ms) — người dùng chốt 30 phút (T018, 2026-10-02).
    -Reveal  Kiểm mã băm, in lựa chọn, thời điểm tiêm thực tế, trạng thái và nhật ký gợi ý, xoá file compose
             override tạm. Chỉ chạy SAU khi sự cố đã được giải quyết (đạt SLO liên tục 15 phút).

    Chín loại lỗi (danh mục: scripts/incident-drill/catalog.json, tám nhóm):
      A  đích kết nối sai        — DB trỏ tới incident-missing-db; BFF/gateway trỏ tới incident-missing-host
      B  cạn pool                — MaxConnectionsPerServer=1, chỉ cho gateway
      C  5xx của 027             — gửi request mang X-Chaos-Fault: 5xx vào service đích, tỷ lệ 5–50%
      D  độ trễ của 025          — gửi request mang X-Chaos-Latency-Ms: 2000 thẳng vào orders-api, tỷ lệ 5–50%
      E  cơ sở dữ liệu dừng      — dừng container của một trong 5 DB
      F  địa chỉ định danh sai   — Identity__Authority sai cho một service dùng nó
      G  thiếu tài nguyên        — giới hạn CPU 0,1 và bộ nhớ 256 MB cho một service
      H  mạng đứt                — tách một service khỏi network chung
      I  container chết          — buộc dừng một service (không tự sống lại)

    Mọi lệnh tiêm yêu cầu CHAOS_ALLOW_FAULT_INJECTION=true trong .env; loại D còn yêu cầu
    CHAOS_ALLOW_LATENCY_INJECTION=true và token do -Load sinh ra.

    Script KHÔNG sửa file nào đã commit: cấu hình sai chỉ nằm trong
    .incident-drill/<runId>/docker-compose.incident.yml, và mật khẩu không bao giờ được ghi ra — file
    override để Compose tự nội suy ${MSSQL_SA_PASSWORD} từ .env lúc chạy.

    Gỡ lỗi: ./scripts/incident-drill.ps1 -Restore -RunId <id>. Loại A, B, F cũng gỡ được bằng chạy lại stack
    không kèm override: docker compose -f docker-compose.local.yml up -d --build --wait

    Hợp đồng: specs/028-incident-oncall-drill/contracts/incident-drill-script-contract.md và
    specs/031-error-group-catalog/contracts/.

.EXAMPLE
    # Tải nền cho buổi diễn tập (chạy trong một terminal riêng, Ctrl+C để dừng).
    ./scripts/incident-drill.ps1 -Load

.EXAMPLE
    # Dạng (b): bài mù.
    ./scripts/incident-drill.ps1 -Start
    ./scripts/incident-drill.ps1 -Hint -RunId 20261001-210000 -Level 1
    ./scripts/incident-drill.ps1 -Restore -RunId 20261001-210000
    ./scripts/incident-drill.ps1 -Reveal -RunId 20261001-210000

.EXAMPLE
    # Dạng (a): lỗi có chủ đích, tự gỡ sau 10 phút.
    ./scripts/incident-drill.ps1 -Inject -Type E -Target orders-db -DurationSeconds 600

.EXAMPLE
    # Chế độ xác minh KHÔNG MÙ của 028 (QA) — không dùng cho buổi diễn tập.
    ./scripts/incident-drill.ps1 -Start -Service orders-api -FaultType B -DelaySeconds 0
#>
[CmdletBinding()]
param(
    [switch]$Start,
    [switch]$Reveal,
    [string]$RunId,

    # 031 — dạng (a): lỗi có chủ đích theo nhóm; khôi phục chung; gợi ý theo mức cho dạng (b).
    [switch]$Inject,
    [switch]$Restore,
    [switch]$Hint,
    [ValidateSet('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I')]
    [string]$Type,
    [ValidateRange(1, 8)]
    [int]$Group,
    [string]$Target,
    [ValidateRange(1, 86400)]
    [int]$DurationSeconds,
    [ValidateRange(1, 3)]
    [int]$Level,

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

# --- danh mục nhóm lỗi + bộ chuyển đổi Compose (spec 031) --------------------------------------
# Danh mục (scripts/incident-drill/catalog.json) chỉ mô tả LOẠI lỗi; mọi lệnh docker/compose nằm ở các hàm
# Invoke-Compose* bên dưới — bộ chuyển đổi Compose. Thêm bộ chuyển đổi CD/Kubernetes sau này = thêm tập hàm cùng
# giao diện, không đụng danh mục.
$catalogPath = Join-Path $PSScriptRoot 'incident-drill/catalog.json'
$supportedRestoreKinds = @('recreate-without-override', 'stop-sending', 'resume-component', 'reattach-network', 'recreate-target')
$wrongHost = 'http://incident-missing-host:8080'
$healthWaitSeconds = 600
$ordersDirectPort = 5041
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$script:Catalog = $null

function Import-FaultCatalog {
    if (-not (Test-Path $catalogPath)) { Stop-WithReason "không có danh mục $catalogPath." }
    $text = [System.IO.File]::ReadAllText($catalogPath, $utf8NoBom)
    $catalog = $text | ConvertFrom-Json
    if ($catalog.version -ne 1) { Stop-WithReason "danh mục có version lạ ($($catalog.version))." }
    $groups = @($catalog.groups)
    $types = @($catalog.types)
    if ($groups.Count -ne 8) { Stop-WithReason "danh mục phải có đúng 8 nhóm (có $($groups.Count))." }
    if ($types.Count -ne 9) { Stop-WithReason "danh mục phải có đúng 9 loại A–I (có $($types.Count))." }
    if ($text -match 'docker' -or $text.Contains('${')) { Stop-WithReason "danh mục không được chứa lệnh docker hay biến compose." }
    $letterPattern = '(?<![\p{L}\d])[A-I](?![\p{L}\d])'
    foreach ($type in $types) {
        if ($supportedRestoreKinds -notcontains $type.restoreKind) { Stop-WithReason "loại $($type.code) có restoreKind '$($type.restoreKind)' mà bộ chuyển đổi Compose không hỗ trợ." }
        if (-not ($groups | Where-Object { $_.id -eq $type.groupId })) { Stop-WithReason "loại $($type.code) tham chiếu nhóm không tồn tại." }
        if (@($type.targets).Count -eq 0) { Stop-WithReason "loại $($type.code) không có đích áp dụng." }
        foreach ($level in '1', '2') {
            $hintText = [string]$type.hints.$level
            if (-not $hintText) { continue }
            foreach ($targetName in @($type.targets)) {
                if ($hintText.Contains($targetName)) { Stop-WithReason "gợi ý mức $level của loại $($type.code) lộ tên đích '$targetName'." }
            }
            if ($hintText -cmatch $letterPattern) { Stop-WithReason "gợi ý mức $level của loại $($type.code) lộ mã loại." }
        }
    }
    return $catalog
}

function Get-FaultType {
    param([string]$Code)
    return @($script:Catalog.types) | Where-Object { $_.code -eq $Code } | Select-Object -First 1
}

# DB -> service chủ (orders-db -> orders-api); service app giữ nguyên.
function Get-OwnerService {
    param([string]$TargetName)
    if ($TargetName -like '*-db') { return ($TargetName -replace '-db$', '-api') }
    return $TargetName
}

function Get-SealedTarget {
    param($Sealed)
    if ($Sealed.PSObject.Properties['target'] -and $Sealed.target) { return [string]$Sealed.target }
    return [string]$Sealed.service
}

function Test-FaultApplicable {
    param([string]$Code, [string]$TargetName)
    $type = Get-FaultType -Code $Code
    return [bool]($type -and (@($type.targets) -contains $TargetName))
}

function Get-ContainerName {
    param([string]$Name)
    return "$composeProject-$Name-1"
}

# Chạy docker và gom đầu ra; mã thoát nằm ở $script:LastDockerExit. Docker ghi tiến độ ra stderr, nên với
# ErrorActionPreference = Stop PowerShell 5.1 sẽ coi đó là lỗi.
function Invoke-Docker {
    param([string[]]$Arguments)
    $ErrorActionPreference = 'Continue'
    $output = & docker @Arguments 2>&1 | ForEach-Object { "$_" }
    $script:LastDockerExit = $LASTEXITCODE
    return $output
}

function Write-JsonFile {
    param($Object, [string]$Path)
    [System.IO.File]::WriteAllText($Path, (ConvertTo-Json -InputObject $Object -Depth 6), $utf8NoBom)
}

function Read-JsonFile {
    param([string]$Path)
    return ([System.IO.File]::ReadAllText($Path, $utf8NoBom) | ConvertFrom-Json)
}

function Get-RunState {
    param([string]$Id)
    $path = Join-Path (Join-Path $drillRoot $Id) 'state.json'
    if (-not (Test-Path $path)) { return $null }
    return (Read-JsonFile -Path $path)
}

function Set-RunState {
    param([string]$Id, [hashtable]$Changes)
    $merged = [ordered]@{ status = $null; injectorPid = $null; injectedAt = $null; restoredAt = $null; failure = $null }
    $current = Get-RunState -Id $Id
    if ($current) {
        foreach ($key in @($merged.Keys)) { if ($current.PSObject.Properties[$key]) { $merged[$key] = $current.$key } }
    }
    foreach ($key in $Changes.Keys) { $merged[$key] = $Changes[$key] }
    Write-JsonFile -Object ([pscustomobject]$merged) -Path (Join-Path (Join-Path $drillRoot $Id) 'state.json')
}

# Bất biến 24: còn lần chạy chưa khôi phục thì không bắt đầu lần mới. Lần chạy 028 cũ không có state.json bị bỏ qua.
function Assert-NoOpenRun {
    if (-not (Test-Path $drillRoot)) { return }
    foreach ($directory in Get-ChildItem $drillRoot -Directory) {
        $statePath = Join-Path $directory.FullName 'state.json'
        if (-not (Test-Path $statePath)) { continue }
        $state = Read-JsonFile -Path $statePath
        if (@('pending', 'injected', 'failed') -contains $state.status) {
            Stop-WithReason "còn lần chạy chưa khôi phục: $($directory.Name) (trạng thái $($state.status)). Chạy ./scripts/incident-drill.ps1 -Restore -RunId $($directory.Name) trước."
        }
    }
}

function Test-EnvFlag {
    param([string]$Name)
    $envFile = Join-Path $repositoryRoot '.env'
    if (-not (Test-Path $envFile)) { return $false }
    $line = Get-Content $envFile | Where-Object { $_ -match "^\s*$Name\s*=" } | Select-Object -Last 1
    if (-not $line) { return $false }
    return (($line -split '=', 2)[1].Trim().Trim('"').Trim("'") -eq 'true')
}

function Test-FaultInjectionAllowed { return (Test-EnvFlag -Name 'CHAOS_ALLOW_FAULT_INJECTION') }
function Test-LatencyInjectionAllowed { return (Test-EnvFlag -Name 'CHAOS_ALLOW_LATENCY_INJECTION') }

# Loại D gửi request có token và X-Tenant-Id thẳng vào orders-api (V9: header không đi xuyên gateway/BFF).
# Token do -Load sinh ra.
function Get-LoadEnvironmentPath { return (Join-Path (Join-Path $drillRoot 'load') 'environment-with-token.json') }

function Get-LoadCredentials {
    $path = Get-LoadEnvironmentPath
    if (-not (Test-Path $path)) { return $null }
    $values = (Read-JsonFile -Path $path).values
    $token = ($values | Where-Object { $_.key -eq 'accessToken' } | Select-Object -First 1).value
    $tenant = ($values | Where-Object { $_.key -eq 'tenantId' } | Select-Object -First 1).value
    if (-not $token -or -not $tenant) { return $null }
    return @{ Token = $token; Tenant = $tenant }
}

function Test-ServiceReady {
    param([string]$Name)
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:$($services[$Name].Port)/health/ready" -TimeoutSec 5
        return ($response.StatusCode -eq 200)
    }
    catch { return $false }
}

# Chờ container khoẻ. Với service app còn chờ /health/ready 200: health của chính container vẫn "healthy" khi DB
# dừng và /health/ready đã trả 503 (V1, 2026-10-05), nên chỉ health của docker là chưa đủ.
function Wait-ContainerReady {
    param([string]$Name)
    $container = Get-ContainerName -Name $Name
    $deadline = (Get-Date).AddSeconds($healthWaitSeconds)
    while ((Get-Date) -lt $deadline) {
        $health = Invoke-Docker @('inspect', '-f', '{{.State.Health.Status}}', $container) | Select-Object -First 1
        $ready = ($script:LastDockerExit -eq 0 -and $health -eq 'healthy')
        if ($ready -and $services.Contains($Name)) { $ready = Test-ServiceReady -Name $Name }
        if ($ready) { return $true }
        Start-Sleep -Seconds 5
    }
    return $false
}

function Invoke-ComposeUpService {
    param([string]$Name, [string]$OverridePath, [string]$LogPath)
    $arguments = @('compose', '-p', $composeProject, '-f', $composeFile)
    if ($OverridePath) { $arguments += @('-f', $OverridePath) }
    $arguments += @('up', '-d', '--force-recreate', '--no-deps', $Name)
    $output = Invoke-Docker $arguments
    if ($LogPath) { Add-Content -Path $LogPath -Value ($output | Out-String) -Encoding UTF8 }
    if ($script:LastDockerExit -ne 0) { throw "docker compose up $Name thất bại (mã $script:LastDockerExit)" }
}

# Tạo lại cả 7 liền nhau để uptime trong `docker ps` không lộ service đích (bất biến 5 của 028). Mỗi service một
# lệnh riêng: một lệnh gộp sẽ bắt BFF/gateway chờ service đích healthy theo depends_on (T016 của 028).
function Invoke-RecreateAllServices {
    param([string]$OverridePath, [string]$LogPath)
    foreach ($name in @($services.Keys)) { Invoke-ComposeUpService -Name $name -OverridePath $OverridePath -LogPath $LogPath }
}

function Invoke-DockerOrThrow {
    param([string[]]$Arguments, [string]$LogPath)
    $output = Invoke-Docker $Arguments
    if ($LogPath) { Add-Content -Path $LogPath -Value ($output | Out-String) -Encoding UTF8 }
    if ($script:LastDockerExit -ne 0) { throw "docker $($Arguments -join ' ') thất bại (mã $script:LastDockerExit): $($output | Out-String)" }
}

# Bộ chuyển đổi Compose — tiêm. Loại tạo lại (A, B, C, D, F) tạo lại cả 7 container; E, G, H, I chỉ chạm đích.
function Invoke-ComposeInject {
    param($Sealed, [string]$RunDirectory, [string]$LogPath)
    $name = Get-SealedTarget -Sealed $Sealed
    $container = Get-ContainerName -Name $name
    switch ($Sealed.faultType) {
        { @('A', 'B', 'C', 'D', 'F') -contains $_ } {
            $override = Join-Path $RunDirectory 'docker-compose.incident.yml'
            Write-OverrideFile -Sealed $Sealed -Path $override
            Invoke-RecreateAllServices -OverridePath $override -LogPath $LogPath
        }
        'E' { Invoke-DockerOrThrow -Arguments @('stop', $container) -LogPath $LogPath }
        'G' {
            $type = Get-FaultType -Code 'G'
            $memory = "$($type.parameters.memoryLimitMb)m"
            Invoke-DockerOrThrow -Arguments @('update', '--cpus', "$($type.parameters.cpuLimit)", '--memory', $memory, '--memory-swap', $memory, $container) -LogPath $LogPath
        }
        'H' { Invoke-DockerOrThrow -Arguments @('network', 'disconnect', "${composeProject}_backbone", $container) -LogPath $LogPath }
        'I' { Invoke-DockerOrThrow -Arguments @('kill', $container) -LogPath $LogPath }
    }
}

# Bộ chuyển đổi Compose — khôi phục theo restoreKind của loại (danh mục). Trả $true khi đích đã khoẻ.
function Invoke-ComposeRestore {
    param($Sealed, [string]$LogPath)
    $type = Get-FaultType -Code $Sealed.faultType
    $name = Get-SealedTarget -Sealed $Sealed
    $container = Get-ContainerName -Name $name
    switch ($type.restoreKind) {
        'recreate-without-override' {
            Invoke-RecreateAllServices -LogPath $LogPath
            return (Wait-ContainerReady -Name $Sealed.service)
        }
        'stop-sending' { return $true }
        'resume-component' {
            Invoke-DockerOrThrow -Arguments @('start', $container) -LogPath $LogPath
            if (-not (Wait-ContainerReady -Name $name)) { return $false }
            return (Wait-ContainerReady -Name $Sealed.service)
        }
        'reattach-network' {
            # Truyền cả hai alias gốc (V5): tên service và tên container.
            Invoke-DockerOrThrow -Arguments @('network', 'connect', '--alias', $name, '--alias', $container, "${composeProject}_backbone", $container) -LogPath $LogPath
            return (Wait-ContainerReady -Name $name)
        }
        'recreate-target' {
            Invoke-ComposeUpService -Name $name -LogPath $LogPath
            return (Wait-ContainerReady -Name $name)
        }
    }
    return $false
}

# Khôi phục một lần chạy (dùng chung cho -Restore và tự gỡ sau -DurationSeconds).
function Complete-Restore {
    param([string]$Id)
    $runDirectory = Join-Path $drillRoot $Id
    $log = Join-Path $runDirectory 'injector.log'
    $sealed = Read-JsonFile -Path (Join-Path $runDirectory 'sealed.json')
    New-Item -ItemType File -Force -Path (Join-Path $runDirectory 'stop.flag') | Out-Null
    try {
        $ok = Invoke-ComposeRestore -Sealed $sealed -LogPath $log
    }
    catch {
        Add-Content -Path $log -Encoding UTF8 -Value "LOI khoi phuc: $($_.Exception.Message)"
        Set-RunState -Id $Id -Changes @{ status = 'failed'; failure = "khôi phục lỗi: $($_.Exception.Message)" }
        return $false
    }
    if (-not $ok) {
        Set-RunState -Id $Id -Changes @{ status = 'failed'; failure = "đích chưa khoẻ sau $healthWaitSeconds giây" }
        return $false
    }
    $now = Format-Vietnam (Get-VietnamNow)
    Set-Content -Path (Join-Path $runDirectory 'restored-at.txt') -Value $now -Encoding UTF8
    Set-RunState -Id $Id -Changes @{ status = 'restored'; restoredAt = $now; failure = $null }
    return $true
}

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
        'D' {
            # Không override: hai cờ Chaos__AllowFaultInjection/AllowLatencyInjection lấy từ .env (đã kiểm là true).
        }
        'F' {
            # Nhóm 5: địa chỉ máy chủ định danh sai cho đúng một service trong 6 service dùng nó.
            $environment['Identity__Authority'] = $wrongHost
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

function Add-InjectorLog {
    param([string]$Log, [string]$Message)
    Add-Content -Path $Log -Encoding UTF8 -Value $Message
}

# Ngủ tới hạn nhưng thoát sớm khi -Restore tạo stop.flag. Trả $true nếu đã ngủ đủ.
function Wait-UntilOrStopped {
    param([DateTime]$Until, [string]$StopFlag)
    while ((Get-Date) -lt $Until) {
        if (Test-Path $StopFlag) { return $false }
        $remaining = ($Until - (Get-Date)).TotalMilliseconds
        Start-Sleep -Milliseconds ([int][math]::Max(50, [math]::Min(2000, $remaining)))
    }
    return $true
}

# Vòng gửi header cho loại C (X-Chaos-Fault: 5xx, request đồng bộ) và loại D (X-Chaos-Latency-Ms, request bất
# đồng bộ vì mỗi request kéo dài ≥ 2 s — gửi tuần tự chỉ đạt ≈ 0,5 request/giây, V7). Dừng khi: stop.flag xuất
# hiện, hết hạn -DurationSeconds, hoặc container đích đã được tạo lại (bất biến 8 của 028).
function Invoke-HeaderLoop {
    param($Sealed, [string]$RunDirectory, [double]$Baseline, $Deadline)
    $log = Join-Path $RunDirectory 'injector.log'
    $stopFlag = Join-Path $RunDirectory 'stop.flag'
    $target = Get-SealedTarget -Sealed $Sealed
    $info = $services[$target]
    $injectedId = Get-ContainerId -Name $target
    $ratio = $Sealed.errorRatePct / 100.0
    # Tốc độ gửi để header chiếm r trên tổng span: r/(1−r) × tốc độ nền. Không đặt mức sàn: mức sàn 1 request/giây
    # làm parties (nền ≈ 0,1 span/s) ra 75,7% thay vì 40% (T018 của 028). Chỉ khi chưa đo được tốc độ nền mới dùng 0,2.
    $perSecond = $ratio / (1.0 - $ratio) * $Baseline
    if ($perSecond -le 0) { $perSecond = 0.2 }
    $intervalMs = [int][math]::Max(10, 1000.0 / $perSecond)
    Add-InjectorLog -Log $log -Message "loai $($Sealed.faultType): baseline=$Baseline span/s, gui $perSecond req/s (moi $intervalMs ms)"

    $isLatency = ($Sealed.faultType -eq 'D')
    $client = $null
    $pending = New-Object System.Collections.ArrayList
    $credentials = $null
    $credentialsAt = [DateTime]::MinValue
    if ($isLatency) {
        Add-Type -AssemblyName System.Net.Http
        $client = New-Object System.Net.Http.HttpClient
        $client.Timeout = [TimeSpan]::FromSeconds(15)
    }

    $lastCheck = [DateTime]::MinValue
    $reason = 'dung'
    while ($true) {
        if (Test-Path $stopFlag) { $reason = 'stop.flag'; break }
        if ($Deadline -and (Get-Date) -ge $Deadline) { $reason = 'het thoi luong'; break }
        if (([DateTime]::UtcNow - $lastCheck).TotalSeconds -ge 5) {
            $currentId = Get-ContainerId -Name $target
            if (-not $currentId -or $currentId -ne $injectedId) { $reason = 'container dich da duoc tao lai'; break }
            $lastCheck = [DateTime]::UtcNow
        }
        $route = $info.Route.Replace('{guid}', [guid]::NewGuid().ToString())
        if ($isLatency) {
            # -Load làm mới token mỗi 30 phút: đọc lại file mỗi 60 giây.
            if (([DateTime]::UtcNow - $credentialsAt).TotalSeconds -ge 60) {
                $fresh = Get-LoadCredentials
                if ($fresh) { $credentials = $fresh }
                $credentialsAt = [DateTime]::UtcNow
            }
            if ($credentials) {
                try {
                    $request = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Get, "http://localhost:$ordersDirectPort/orders/$([guid]::NewGuid())")
                    [void]$request.Headers.TryAddWithoutValidation('Authorization', "Bearer $($credentials.Token)")
                    [void]$request.Headers.TryAddWithoutValidation('X-Tenant-Id', [string]$credentials.Tenant)
                    [void]$request.Headers.TryAddWithoutValidation('X-Chaos-Latency-Ms', [string]$Sealed.latencyMs)
                    [void]$pending.Add($client.SendAsync($request))
                }
                catch { }
            }
            # Dọn các request đã xong để danh sách không phình ra.
            for ($i = $pending.Count - 1; $i -ge 0; $i--) { if ($pending[$i].IsCompleted) { $pending.RemoveAt($i) } }
        }
        else {
            try {
                Invoke-WebRequest -UseBasicParsing -Uri "http://localhost:$($info.Port)$route" -Headers @{ 'X-Chaos-Fault' = '5xx' } -TimeoutSec 5 | Out-Null
            }
            catch { }
        }
        # Ngủ từng đoạn ≤ 5 s để không lỡ lần kiểm container/stop.flag khi khoảng cách giữa hai request dài.
        $remaining = $intervalMs
        while ($remaining -gt 0) {
            $step = [math]::Min($remaining, 2000)
            Start-Sleep -Milliseconds $step
            $remaining -= $step
            if ($remaining -gt 0 -and (Test-Path $stopFlag)) { break }
        }
    }
    if ($client) { $client.Dispose() }
    Add-InjectorLog -Log $log -Message "loai $($Sealed.faultType): dung gui header ($reason) luc $(Format-Vietnam (Get-VietnamNow))"
}

function Invoke-InjectorProcess {
    param([string]$Id)
    $runDirectory = Join-Path $drillRoot $Id
    $log = Join-Path $runDirectory 'injector.log'
    $stopFlag = Join-Path $runDirectory 'stop.flag'
    $sealed = Read-JsonFile -Path (Join-Path $runDirectory 'sealed.json')
    $script:Catalog = Import-FaultCatalog

    try {
        # -Restore khi còn pending tạo stop.flag: thoát mà không tiêm gì.
        if ($sealed.delaySeconds -gt 0) {
            if (-not (Wait-UntilOrStopped -Until (Get-Date).AddSeconds($sealed.delaySeconds) -StopFlag $stopFlag)) { return }
        }
        if (Test-Path $stopFlag) { return }

        $baseline = 0.0
        if (@('C', 'D') -contains $sealed.faultType) {
            $baseline = Get-BaselineSpansPerSecond -OtelName $services[(Get-SealedTarget -Sealed $sealed)].OtelName
        }

        Invoke-ComposeInject -Sealed $sealed -RunDirectory $runDirectory -LogPath $log

        $injectedAt = Format-Vietnam (Get-VietnamNow)
        Set-Content -Path (Join-Path $runDirectory 'injected-at.txt') -Value $injectedAt -Encoding UTF8
        Set-RunState -Id $Id -Changes @{ status = 'injected'; injectedAt = $injectedAt; injectorPid = $PID }

        $deadline = $null
        if ($sealed.PSObject.Properties['durationSeconds'] -and $sealed.durationSeconds) { $deadline = (Get-Date).AddSeconds([int]$sealed.durationSeconds) }

        if (@('C', 'D') -contains $sealed.faultType) {
            Invoke-HeaderLoop -Sealed $sealed -RunDirectory $runDirectory -Baseline $baseline -Deadline $deadline
        }
        elseif ($deadline) {
            Wait-UntilOrStopped -Until $deadline -StopFlag $stopFlag | Out-Null
        }

        # Hết thời lượng (không phải do -Restore): tự khôi phục.
        if ($deadline -and -not (Test-Path $stopFlag)) {
            Add-InjectorLog -Log $log -Message "het thoi luong $($sealed.durationSeconds) giay — tu khoi phuc luc $(Format-Vietnam (Get-VietnamNow))"
            Complete-Restore -Id $Id | Out-Null
        }
    }
    catch {
        Add-InjectorLog -Log $log -Message "LOI: $($_.Exception.Message)"
        Set-RunState -Id $Id -Changes @{ status = 'failed'; failure = $_.Exception.Message }
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

# Tạo thư mục lần chạy, niêm phong lựa chọn (SHA-256), ghi state.json (pending) và khởi tiến trình nền tiêm lỗi.
# Dùng chung cho -Start (mù và không mù cũ) và -Inject (dạng a).
function New-DrillRun {
    param($Type, [string]$TargetName, [int]$Delay, [string]$Mode, $Duration)
    $now = Get-VietnamNow
    $id = $now.ToString('yyyyMMdd-HHmmss')
    $runDirectory = Join-Path $drillRoot $id
    New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

    $ownerService = Get-OwnerService -TargetName $TargetName
    $faultDetail = [ordered]@{}
    if ($Type.code -eq 'A' -and $TargetName -eq 'bff-api') { $faultDetail.downstream = $bffDownstreams | Get-Random }
    $errorRatePct = $null
    $latencyMs = $null
    if (@('C', 'D') -contains $Type.code) { $errorRatePct = Get-Random -Minimum 5 -Maximum 51 }
    if ($Type.code -eq 'D') { $latencyMs = [int]$Type.parameters.latencyMs }

    $sealed = [ordered]@{
        runId           = $id
        blind           = ($Mode -eq 'blind')
        mode            = $Mode
        group           = [int]$Type.groupId
        service         = $ownerService
        target          = $TargetName
        faultType       = $Type.code
        faultDetail     = $faultDetail
        delaySeconds    = $Delay
        durationSeconds = $Duration
        errorRatePct    = $errorRatePct
        latencyMs       = $latencyMs
        plannedInjectAt = Format-Vietnam ($now.AddSeconds($Delay))
    }
    $sealedPath = Join-Path $runDirectory 'sealed.json'
    Write-JsonFile -Object $sealed -Path $sealedPath
    $hash = (Get-FileHash -Algorithm SHA256 $sealedPath).Hash
    Set-Content -Path (Join-Path $runDirectory 'hash.txt') -Value $hash -Encoding UTF8
    Set-RunState -Id $id -Changes @{ status = 'pending' }

    $process = Start-Process -WindowStyle Hidden -FilePath 'powershell.exe' -PassThru -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-RunInjector', '-RunId', $id
    )
    Set-RunState -Id $id -Changes @{ injectorPid = $process.Id }
    return [pscustomobject]@{ Id = $id; Hash = $hash; Sealed = $sealed }
}

# Bốc thăm mù (bất biến 33): nhóm đều 1/8 → loại đều trong nhóm → đích đều trong các đích áp dụng được.
# Loại có cờ chưa bật (hoặc loại D khi chưa có token của -Load) bị loại khỏi vòng bốc, để không tiêm hỏng
# giữa chừng; điều này không lộ lựa chọn.
function Select-BlindFault {
    $latencyOk = ((Test-LatencyInjectionAllowed) -and [bool](Get-LoadCredentials))
    $eligibleGroups = @($script:Catalog.groups | Where-Object {
            $groupId = $_.id
            @($script:Catalog.types | Where-Object { $_.groupId -eq $groupId -and ((@($_.requiresFlags) -notcontains 'LATENCY_INJECTION') -or $latencyOk) }).Count -gt 0
        })
    if ($eligibleGroups.Count -eq 0) { Stop-WithReason 'không có nhóm lỗi nào bốc được.' }
    $group = $eligibleGroups | Get-Random
    $candidates = @($script:Catalog.types | Where-Object { $_.groupId -eq $group.id -and ((@($_.requiresFlags) -notcontains 'LATENCY_INJECTION') -or $latencyOk) })
    $type = $candidates | Get-Random
    $targetName = @($type.targets) | Get-Random
    return @{ Type = $type; Target = $targetName }
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
    [System.IO.File]::ReadAllText($sealedPath, $utf8NoBom) | Write-Host
    $injectedAt = Join-Path $runDirectory 'injected-at.txt'
    if (Test-Path $injectedAt) {
        Write-Host "Thời điểm tiêm thực tế: $((Get-Content $injectedAt -Raw).Trim())" -ForegroundColor Cyan
    }
    else {
        Write-Host "Chưa có thời điểm tiêm thực tế (tiến trình nền chưa tiêm, hoặc lỗi — xem injector.log)." -ForegroundColor Yellow
    }

    # Bất biến 34: trạng thái và nhật ký gợi ý (chỉ có ở lần chạy từ spec 031; lần chạy 028 cũ không có).
    $state = Get-RunState -Id $RunId
    if ($state) {
        Write-Host "Trạng thái: $($state.status); tiêm lúc $($state.injectedAt); khôi phục lúc $($state.restoredAt)" -ForegroundColor Cyan
    }
    $hintLogPath = Join-Path $runDirectory 'hint-log.json'
    if (Test-Path $hintLogPath) {
        $hintLog = @(Read-JsonFile -Path $hintLogPath)
        Write-Host "Gợi ý đã mở: $($hintLog.Count) lần" -ForegroundColor Cyan
        foreach ($entry in $hintLog) { Write-Host "  $($entry.at)  mức $($entry.level)" }
    }
    else {
        Write-Host "Gợi ý đã mở: 0 lần" -ForegroundColor Cyan
    }

    $override = Join-Path $runDirectory 'docker-compose.incident.yml'
    if (Test-Path $override) { Remove-Item $override }
    exit 0
}

# --- -Hint --------------------------------------------------------------------------------------

if ($Hint) {
    if (-not $RunId) { Stop-WithReason "thiếu -RunId." }
    if ($Level -lt 1 -or $Level -gt 3) { Stop-WithReason "thiếu -Level (1, 2 hoặc 3)." }
    $script:Catalog = Import-FaultCatalog
    $runDirectory = Join-Path $drillRoot $RunId
    $sealedPath = Join-Path $runDirectory 'sealed.json'
    if (-not (Test-Path $sealedPath)) { Stop-WithReason "không có $sealedPath." }
    $sealed = Read-JsonFile -Path $sealedPath
    $typeEntry = Get-FaultType -Code $sealed.faultType
    $hintText = [string]$typeEntry.hints.([string]$Level)
    if (-not $hintText) { Stop-WithReason "danh mục chưa có gợi ý mức $Level cho loại này." }

    if ($Level -eq 3) {
        # Mức 3 là đáp án: kiểm băm như -Reveal, rồi điền dấu giữ chỗ từ sealed.json.
        $expected = (Get-Content (Join-Path $runDirectory 'hash.txt') -Raw).Trim()
        $actual = (Get-FileHash -Algorithm SHA256 $sealedPath).Hash
        if ($actual -ne $expected) { Stop-WithReason "mã băm không khớp — sealed.json đã bị sửa sau khi niêm phong." }
        $parameters = ($typeEntry.parameters | ConvertTo-Json -Compress)
        $hintText = $hintText.Replace('{service}', [string]$sealed.service).Replace('{target}', (Get-SealedTarget -Sealed $sealed)).Replace('{parameters}', $parameters).Replace('{errorRatePct}', [string]$sealed.errorRatePct)
    }

    # Ghi nhật ký (kể cả khi bỏ cách mức); KHÔNG ghi vào Kibana Case, không sửa sealed.json hay mốc thời gian.
    $hintLogPath = Join-Path $runDirectory 'hint-log.json'
    $entries = @()
    if (Test-Path $hintLogPath) { $entries = @(Read-JsonFile -Path $hintLogPath) }
    $entries += [pscustomobject]@{ at = (Format-Vietnam (Get-VietnamNow)); level = $Level }
    Write-JsonFile -Object @($entries) -Path $hintLogPath

    Write-Host "Gợi ý mức ${Level}:" -ForegroundColor Cyan
    Write-Host $hintText
    exit 0
}

# --- -Restore -----------------------------------------------------------------------------------

if ($Restore) {
    if (-not $RunId) { Stop-WithReason "thiếu -RunId." }
    $script:Catalog = Import-FaultCatalog
    $runDirectory = Join-Path $drillRoot $RunId
    $sealedPath = Join-Path $runDirectory 'sealed.json'
    if (-not (Test-Path $sealedPath)) { Stop-WithReason "không có $sealedPath." }
    $state = Get-RunState -Id $RunId
    if (-not $state) {
        # Lần chạy 028 cũ không có state.json: suy ra từ injected-at.txt.
        $legacyStatus = 'pending'
        if (Test-Path (Join-Path $runDirectory 'injected-at.txt')) { $legacyStatus = 'injected' }
        Set-RunState -Id $RunId -Changes @{ status = $legacyStatus }
        $state = Get-RunState -Id $RunId
    }
    if ($state.status -eq 'restored') { Write-Host "Lần chạy $RunId đã khôi phục từ trước." -ForegroundColor Green; exit 0 }

    # stop.flag làm tiến trình nền đang chờ (hoặc đang gửi header) dừng mà không phải kill giữa lúc tiêm.
    New-Item -ItemType File -Force -Path (Join-Path $runDirectory 'stop.flag') | Out-Null
    if ($state.status -eq 'pending') {
        $waited = 0
        while ($waited -lt $healthWaitSeconds) {
            $state = Get-RunState -Id $RunId
            $injector = $null
            if ($state.injectorPid) { $injector = Get-Process -Id $state.injectorPid -ErrorAction SilentlyContinue }
            if ($state.status -ne 'pending' -or -not $injector) { break }
            Start-Sleep -Seconds 3
            $waited += 3
        }
        $state = Get-RunState -Id $RunId
        if ($state.status -eq 'pending') {
            $now = Format-Vietnam (Get-VietnamNow)
            Set-RunState -Id $RunId -Changes @{ status = 'restored'; restoredAt = $now }
            Set-Content -Path (Join-Path $runDirectory 'restored-at.txt') -Value $now -Encoding UTF8
            Write-Host "Chưa tiêm gì — đã huỷ lần chạy $RunId." -ForegroundColor Green
            exit 0
        }
    }

    Write-Host "Đang khôi phục $RunId (chờ đích khoẻ tối đa $healthWaitSeconds giây)..." -ForegroundColor Cyan
    if (Complete-Restore -Id $RunId) {
        Write-Host "Đã khôi phục $RunId; đích đã khoẻ." -ForegroundColor Green
        exit 0
    }
    $failed = Get-RunState -Id $RunId
    Write-Host "Khôi phục thất bại: $($failed.failure)" -ForegroundColor Red
    exit 1
}

# --- -Inject (dạng a: lỗi có chủ đích theo nhóm) ----------------------------------------------

if ($Inject) {
    $script:Catalog = Import-FaultCatalog
    if (-not (Test-FaultInjectionAllowed)) {
        Stop-WithReason "CHAOS_ALLOW_FAULT_INJECTION không phải 'true' trong .env. Chỉ bật cờ này trong lúc diễn tập."
    }
    Assert-NoOpenRun

    if ($Type) {
        $faultTypeEntry = Get-FaultType -Code $Type
        if ($Group -and $faultTypeEntry.groupId -ne $Group) { Stop-WithReason "loại $Type không thuộc nhóm $Group." }
    }
    elseif ($Group) {
        $inGroup = @($script:Catalog.types | Where-Object { $_.groupId -eq $Group })
        if ($inGroup.Count -eq 0) { Stop-WithReason "nhóm $Group không có loại nào." }
        $faultTypeEntry = $inGroup | Get-Random
    }
    else { Stop-WithReason "chỉ định -Type (A–I) hoặc -Group (1–8)." }

    if (@($faultTypeEntry.requiresFlags) -contains 'LATENCY_INJECTION' -and -not (Test-LatencyInjectionAllowed)) {
        Stop-WithReason "loại $($faultTypeEntry.code) cần CHAOS_ALLOW_LATENCY_INJECTION=true trong .env (cờ này đang thiếu)."
    }
    if ($faultTypeEntry.code -eq 'D' -and -not (Get-LoadCredentials)) {
        Stop-WithReason "loại D cần token do -Load sinh ra ($(Get-LoadEnvironmentPath)); chạy ./scripts/incident-drill.ps1 -Load trước."
    }

    if ($Target) {
        if (-not (Test-FaultApplicable -Code $faultTypeEntry.code -TargetName $Target)) {
            Stop-WithReason "loại $($faultTypeEntry.code) không áp dụng được cho '$Target' (đích hợp lệ: $(@($faultTypeEntry.targets) -join ', '))."
        }
        $chosenTarget = $Target
    }
    else { $chosenTarget = @($faultTypeEntry.targets) | Get-Random }

    $duration = $null
    if ($DurationSeconds -gt 0) { $duration = $DurationSeconds }
    $run = New-DrillRun -Type $faultTypeEntry -TargetName $chosenTarget -Delay 0 -Mode 'scripted' -Duration $duration

    $groupName = ($script:Catalog.groups | Where-Object { $_.id -eq $faultTypeEntry.groupId }).name
    Write-Host "TIÊM CÓ CHỦ ĐÍCH (dạng a) — không phải bài mù." -ForegroundColor Yellow
    Write-Host "runId:  $($run.Id)"
    Write-Host "nhóm:   $($faultTypeEntry.groupId) — $groupName"
    Write-Host "loại:   $($faultTypeEntry.code) — $($faultTypeEntry.name)"
    Write-Host "đích:   $chosenTarget"
    Write-Host "tham số: $(($faultTypeEntry.parameters | ConvertTo-Json -Compress))"
    if ($duration) { Write-Host "tự khôi phục sau: $duration giây" } else { Write-Host "không tự khôi phục — chạy: ./scripts/incident-drill.ps1 -Restore -RunId $($run.Id)" }
    Write-Host "Tiêm đang chạy ở tiến trình nền; xem .incident-drill/$($run.Id)/injector.log và state.json."
    exit 0
}

# --- -Start -------------------------------------------------------------------------------------

if (-not $Start) {
    Write-Host "Dùng -Start, -Inject -Type <A-I>, -Hint/-Restore/-Reveal -RunId <id>, hoặc -Load. Xem: Get-Help $PSCommandPath -Full"
    exit 1
}

$script:Catalog = Import-FaultCatalog

# Bất biến 1/20: cờ phải bật trong .env, nếu không thì không ghi gì, không gọi docker.
if (-not (Test-FaultInjectionAllowed)) {
    Stop-WithReason "CHAOS_ALLOW_FAULT_INJECTION không phải 'true' trong .env. Chỉ bật cờ này trong lúc diễn tập."
}
Assert-NoOpenRun

$blind = -not ($Service -or $FaultType -or $DelaySeconds -ge 0)
if ($blind) {
    # Bốc thăm mù mới: cả 8 nhóm (bất biến 33).
    $pick = Select-BlindFault
    $faultTypeEntry = $pick.Type
    $chosenTarget = $pick.Target
    $mode = 'blind'
}
else {
    # Chế độ không mù cũ của 028 (bất biến 11): chỉ định sẵn service và/hoặc loại A/B/C.
    if (-not $Service) { $Service = @($services.Keys) | Get-Random }
    $applicable = Get-ApplicableFaultTypes -Name $Service
    if ($FaultType) {
        if ($applicable -notcontains $FaultType) { Stop-WithReason "loại $FaultType không áp dụng được cho $Service." }
    }
    else {
        $FaultType = $applicable | Get-Random
    }
    $faultTypeEntry = Get-FaultType -Code $FaultType
    $chosenTarget = $Service
    $mode = 'legacy-nonblind'
}
if ($DelaySeconds -lt 0) { $DelaySeconds = Get-Random -Minimum 0 -Maximum 1801 }

$run = New-DrillRun -Type $faultTypeEntry -TargetName $chosenTarget -Delay $DelaySeconds -Mode $mode -Duration $null

# Bất biến 2: chỉ runId và mã băm — không service, loại, tham số hay thời điểm.
if (-not $blind) {
    Write-Host "CHẾ ĐỘ KHÔNG MÙ — không dùng cho buổi diễn tập." -ForegroundColor Yellow
}
Write-Host "runId:  $($run.Id)"
Write-Host "SHA256: $($run.Hash)"
Write-Host "Dán mã băm vào Kibana Case khi mở Case. Sau khi sự cố đã giải quyết: ./scripts/incident-drill.ps1 -Reveal -RunId $($run.Id)"
