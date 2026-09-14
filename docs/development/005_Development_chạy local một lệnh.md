# Bước 005: Thay đổi nghiệp vụ so với bước 004

## Phạm vi

Tài liệu này chỉ mô tả phần code được tạo hoặc thay đổi bởi bước 005 so với bước 004. Phạm vi Git được xác định như sau:

- Mốc cuối phần triển khai nghiệp vụ của 004: commit `c99783c`.
- Commit tạo đặc tả 005: `b1081b3`.
- Commit thêm Docker Compose và script khởi động: `36bafd2`.
- Commit bổ sung CORS và request warming: `fd8ed2d`.
- Commit thêm `down` và `reset`: `e6ad6cf`.
- Commit hoàn thiện local stack: `5578fff`.

Bước 005 không bổ sung rule nghiệp vụ mới cho Products, Baskets, Orders hoặc Checkout. Nó đóng gói các service của bước 004 thành một local stack có thể khởi động bằng một lệnh, có migration riêng, có health gate, có CORS và có quy trình dừng hoặc xóa dữ liệu rõ ràng.

Nội dung kiểm thử, test convention và frontend được bỏ qua.

## 1. Một lệnh khởi động toàn bộ nền tảng

### PowerShell

[scripts/up.ps1](../../scripts/up.ps1)

```powershell
# 005: tham số này cho phép mở thêm port nội bộ khi debug.
[CmdletBinding()]
param(
    [switch]$PublishInternalPorts
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
```

### Kiểm tra Docker

[scripts/up.ps1](../../scripts/up.ps1)

```powershell
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    # 005: báo rõ Docker chưa được cài hoặc không có trên PATH.
    Stop-WithReason "Docker is not installed, or is not on PATH..."
}

docker info --format '{{.ServerVersion}}' 2>&1 | Out-Null

if ($LASTEXITCODE -ne 0) {
    # 005: Docker CLI có thể tồn tại nhưng daemon chưa chạy.
    Stop-WithReason "the Docker daemon is not responding..."
}
```

### Kiểm tra file môi trường

[scripts/up.ps1](../../scripts/up.ps1)

```powershell
$envFile = Join-Path $repositoryRoot '.env'

if (-not (Test-Path $envFile)) {
    # 005: không tự tạo credentials file.
    Stop-WithReason "'.env' does not exist. Copy the template first..."
}
```

005 yêu cầu người dùng tạo `.env` từ template trước khi chạy stack. Script không tự sinh giá trị bí mật.

### Kiểm tra bộ nhớ Docker

[scripts/up.ps1](../../scripts/up.ps1)

```powershell
# 005: ngưỡng tối thiểu cho local stack.
$requiredMemoryGb = 6
$daemonMemoryBytes = [int64](docker info --format '{{.MemTotal}}')
$daemonMemoryGb = [math]::Round(
    $daemonMemoryBytes / 1GB,
    1)

if ($daemonMemoryGb -lt $requiredMemoryGb) {
    # 005: dừng sớm và nêu rõ nguyên nhân.
    Stop-WithReason "Docker has ${daemonMemoryGb} GB of memory available but the stack needs ${requiredMemoryGb} GB."
}
```

Mức 6 GB dành cho toàn bộ stack local gồm SQL Server, domain services, BFF, Gateway và các dependency được Compose khai báo.

### Build và chờ stack sẵn sàng

[scripts/up.ps1](../../scripts/up.ps1)

```powershell
$composeArgs = @('compose')

if ($PublishInternalPorts) {
    # 005: file override chỉ thêm port debug, không thay đổi service topology.
    $composeArgs += @(
        '-f',
        'docker-compose.yml',
        '-f',
        'docker-compose.debug.yml')
}

# 005: build code mới và chờ health gate.
$composeArgs += @('up', '--build', '--wait')

& docker @composeArgs
```

- `--build` bảo đảm image được build lại từ source hiện tại.
- `--wait` khiến lệnh chỉ hoàn tất sau khi các container cần thiết đạt điều kiện health.
- Nếu Compose thất bại, script hiển thị hướng dẫn xem log của component lỗi.

### Phiên bản POSIX

[scripts/up.sh](../../scripts/up.sh)

```bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
required_memory_gb=6

# 005: các prerequisite được kiểm tra trước khi gọi Compose.
command -v docker >/dev/null 2>&1 \
    || stop_with_reason "Docker is not installed, or is not on PATH."

docker info --format '{{.ServerVersion}}' >/dev/null 2>&1 \
    || stop_with_reason "the Docker daemon is not responding."
```

## 2. Request warming sau khi stack healthy

Health check chỉ chứng minh container hoặc dependency đáp ứng probe. Bước 005 còn gọi trước các đường dẫn chính để làm nóng đường đi thực tế qua Gateway và BFF.

[scripts/up.ps1](../../scripts/up.ps1)

```powershell
Write-Host "Warming the request path..."

foreach ($path in @(
    '/bff/products',
    '/bff/basket',
    '/bff/orders/00000000-0000-4000-8000-000000000000')) {
    try {
        # 005: làm nóng các path chính qua Gateway.
        Invoke-WebRequest `
            -Uri "http://localhost:5300$path" `
            -TimeoutSec 30 `
            -UseBasicParsing |
            Out-Null
    }
    catch {
        # 005: warming thất bại không làm stack bị coi là không khởi động được.
    }
}
```

[scripts/up.sh](../../scripts/up.sh)

```bash
# 005: gọi cùng các path trong phiên bản POSIX.
for path in \
    /bff/products \
    /bff/basket \
    "/bff/orders/00000000-0000-4000-8000-000000000000"; do
    curl -fsS -m 30 \
        -o /dev/null \
        "http://localhost:5300${path}" \
        2>/dev/null || true
done
```

Các request này nhằm khởi tạo sớm các thành phần có thể tốn thời gian ở request đầu tiên, như EF model, connection pool và downstream path. Warming không thay đổi dữ liệu nghiệp vụ; kết quả 404 của order ID giả được chấp nhận.

## 3. Compose gom các service của bước 004

[docker-compose.yml](../../docker-compose.yml)

```yaml
# 005: tên project riêng để không xung đột với compose stack khác.
name: ecomerce-stack

networks:
  # 005: các container giao tiếp trên một network nội bộ.
  backbone:
    driver: bridge

volumes:
  # 005: volume còn lại sau down, bị xóa bởi reset.
  sqlserver-data:
  rabbitmq-data:
  elasticsearch-data:
```

005 đưa các thành phần cần cho local stack vào một Compose project. Các service domain của bước 004 vẫn giữ database riêng về mặt logic, dù local stack dùng một SQL Server container.

### Dependency

[docker-compose.yml](../../docker-compose.yml)

```yaml
services:
  # 005: database dùng chung ở môi trường local.
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest

  # 005: dependency có sẵn cho các story cần dùng sau này.
  redis:
    image: redis:7-alpine

  rabbitmq:
    image: rabbitmq:3-management-alpine

  # 005: nền tảng lưu và xem telemetry local.
  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:...

  kibana:
    image: docker.elastic.co/kibana/kibana:...
```

Redis và RabbitMQ được chạy trong local stack theo topology được khai báo, nhưng bước 005 không thêm logic basket Redis hoặc logic messaging vào các domain service.

## 4. Migration chạy bằng container riêng

Bước 005 tách việc apply migration khỏi container chạy API. Điều này là thay đổi vận hành của code 004, không phải thay đổi domain model.

[docker-compose.yml](../../docker-compose.yml)

```yaml
services:
  products-migrate:
    # 005: thử lại nếu migration gặp lỗi tạm thời.
    restart: on-failure:3

    build:
      context: .
      dockerfile: services/products/src/Products.Api/Dockerfile
      # 005: chạy stage migration, không chạy API.
      target: migrator

    environment:
      # 005: migrator chỉ nhận connection string của Products.
      ConnectionStrings__ProductsDb: Server=sqlserver;Database=products;...

    depends_on:
      sqlserver:
        condition: service_healthy
```

Các migrator tương ứng:

- `products-migrate`.
- `baskets-migrate`.
- `orders-migrate`.
- `parties-migrate`.
- `identity-migrate`.

[docker-compose.yml](../../docker-compose.yml)

```yaml
services:
  products-api:
    # 005: API chỉ chạy sau khi migration hoàn tất thành công.
    depends_on:
      products-migrate:
        condition: service_completed_successfully
```

Cùng quy tắc được áp dụng cho Baskets, Orders, Parties và Identity. Nhờ đó, API bước 004 không phải tự chạy migration khi startup.

## 5. Dockerfile hỗ trợ hai loại image

[services/products/src/Products.Api/Dockerfile](../../services/products/src/Products.Api/Dockerfile)

```dockerfile
# 005: stage build dùng để biên dịch source.
FROM ... AS build

# 005: stage migrator dùng output để apply EF migration.
FROM ... AS migrator

# 005: stage final chỉ chạy Products API.
FROM ... AS final
```

Các Dockerfile tương ứng của Baskets, Orders, Parties và Gateway được chỉnh để Compose có thể chọn stage `migrator` hoặc `final`.

[services/baskets/src/Baskets.Api/Dockerfile](../../services/baskets/src/Baskets.Api/Dockerfile)

```dockerfile
# 005: cùng một image definition phục vụ migration và API runtime.
FROM ... AS build
FROM ... AS migrator
FROM ... AS final
```

[services/orders/src/Orders.Api/Dockerfile](../../services/orders/src/Orders.Api/Dockerfile)

```dockerfile
# 005: Orders API được build thành stage final;
# migration được chạy từ stage migrator trước đó.
FROM ... AS migrator
FROM ... AS final
```

## 6. Gateway cho phép storefront gọi qua origin khác

Bước 004 có storefront gọi Gateway từ origin khác. Bước 005 đưa storefront vào Compose và bổ sung CORS để trình duyệt cho phép request.

[services/gateway/src/Gateway.Api/Program.cs](../../services/gateway/src/Gateway.Api/Program.cs)

```csharp
// 005: origin được lấy từ cấu hình, không hard-code trong code nghiệp vụ.
builder.Services.AddCors(options =>
    options.AddPolicy(
        StorefrontCorsPolicy,
        policy => policy
            .WithOrigins(
                builder.Configuration
                    .GetSection("Cors:AllowedOrigins")
                    .Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));
```

[services/gateway/src/Gateway.Api/Program.cs](../../services/gateway/src/Gateway.Api/Program.cs)

```csharp
// 005: CORS chạy trước authentication và reverse proxy,
// để Gateway tự trả lời preflight OPTIONS.
app.UseCors(StorefrontCorsPolicy);

app.UseAuthentication();
app.UseMiddleware<TenantHeaderPropagationMiddleware>();
app.UseMiddleware<SubjectHeaderPropagationMiddleware>();
app.MapReverseProxy();
```

[services/gateway/src/Gateway.Api/appsettings.Development.json](../../services/gateway/src/Gateway.Api/appsettings.Development.json)

```json
{
  "Cors": {
    // 005: origin của frontend khi chạy development.
    "AllowedOrigins": [
      "http://localhost:5173",

      // 005: origin của storefront trong Compose.
      "http://localhost:4173"
    ]
  }
}
```

[docker-compose.yml](../../docker-compose.yml)

```yaml
services:
  gateway-api:
    environment:
      # 005: Compose chạy Production nên truyền origin qua environment.
      Cors__AllowedOrigins__0: http://localhost:4173
```

005 không thay đổi quy tắc tenant hoặc subject của bước 004. Nó chỉ bảo đảm browser có thể gọi Gateway từ origin của storefront trong local stack.

## 7. Chỉ publish các entry point cần thiết

[docker-compose.yml](../../docker-compose.yml)

```yaml
services:
  gateway-api:
    # 005: Gateway là entry point HTTP của backend.
    ports:
      - "5300:8080"

  storefront:
    # 005: storefront là entry point của browser.
    ports:
      - "4173:80"
```

Các domain service không publish port trực tiếp ra host. Luồng truy cập local được đóng gói thành:

```text
Browser
  -> localhost:4173 storefront
  -> localhost:5300 Gateway
  -> bff-api:8080
  -> products-api / baskets-api / orders-api
```

Điều này giữ nguyên nguyên tắc của bước 004: browser chỉ đi qua Gateway và BFF.

## 8. Dừng stack nhưng giữ dữ liệu

[scripts/down.ps1](../../scripts/down.ps1)

```powershell
# 005: xóa container và giải phóng port.
# Volume vẫn còn nên database và basket/order local được giữ lại.
& docker compose down
```

[scripts/down.sh](../../scripts/down.sh)

```bash
# 005: hành vi POSIX tương đương.
docker compose down
```

Sau `down`, lần `up` tiếp theo tiếp tục dùng dữ liệu cũ.

## 9. Reset stack và xóa dữ liệu

[scripts/reset.ps1](../../scripts/reset.ps1)

```powershell
# 005: --volumes là khác biệt chính so với down.ps1.
& docker compose down --volumes
```

[scripts/reset.sh](../../scripts/reset.sh)

```bash
# 005: xóa container và toàn bộ volume của local stack.
docker compose down --volumes
```

Sau `reset`, lần `up` tiếp theo tạo trạng thái giống lần đầu:

- Database được tạo lại.
- Catalog của bước 004 được seed lại bởi migration.
- Basket và order cũ không còn.
- Broker state bị xóa.
- Telemetry state trong volume bị xóa.

## 10. Debug compose override

[docker-compose.debug.yml](../../docker-compose.debug.yml)

```yaml
# 005: file override dùng để publish thêm port nội bộ khi debug.
services:
  products-api:
    ports:
      - "..."

  baskets-api:
    ports:
      - "..."
```

[scripts/up.ps1](../../scripts/up.ps1)

```powershell
# 005: bật override debug bằng tham số riêng.
./scripts/up.ps1 -PublishInternalPorts
```

[scripts/up.sh](../../scripts/up.sh)

```bash
# 005: bật override debug trên POSIX.
./scripts/up.sh --debug
```

Override này không tạo thêm business flow. Nó chỉ mở thêm điểm quan sát khi phát triển local.

## Tóm tắt 004 → 005

| Khu vực | Bước 004 | Bước 005 |
|---|---|---|
| Nghiệp vụ | Basket, Order, Checkout | Không thêm rule nghiệp vụ mới |
| Khởi động | Các thành phần được chạy riêng | Một lệnh `up` cho toàn stack |
| Migration | Chưa được đóng gói thành flow local thống nhất | Container `*-migrate` riêng |
| Gateway | Có routing và subject propagation | Thêm CORS cho storefront |
| Sẵn sàng | Health của container/service | Health gate rồi warming request path |
| Dừng | Chưa có workflow thống nhất | `down` giữ volume và dữ liệu |
| Làm mới | Chưa có workflow thống nhất | `reset` xóa volume và dữ liệu |
| Dockerfile | Build service runtime | Hỗ trợ stage `migrator` và `final` |
| Network | Chưa đóng gói thành local entry point | Chỉ publish storefront và Gateway |

**Kết luận:** bước 004 tạo ra khả năng mua hàng; bước 005 làm cho khả năng đó chạy được nhất quán trong môi trường local. 005 chủ yếu là thay đổi triển khai và vận hành, không thay đổi logic nghiệp vụ của Basket, Order hoặc Checkout.

## 11. Shared project trong bước 005

Theo các commit của bước 005 (`b1081b3` đến `5578fff`), không có shared project mới dưới `shared` và không có thay đổi mới về cách các service sử dụng `ServiceDefaults` hoặc `Tenancy`.

[shared/ServiceDefaults/ServiceDefaults.csproj](../../shared/ServiceDefaults/ServiceDefaults.csproj)

```xml
<!-- 005: vẫn là shared project hạ tầng được các service kế thừa từ các bước trước. -->
<Project Sdk="Microsoft.NET.Sdk" />
```

[shared/Tenancy/Tenancy.csproj](../../shared/Tenancy/Tenancy.csproj)

```xml
<!-- 005: vẫn là shared project tenant/subject của bước 003 và 004. -->
<Project Sdk="Microsoft.NET.Sdk" />
```

[services/gateway/src/Gateway.Api/Dockerfile](../../services/gateway/src/Gateway.Api/Dockerfile)

```dockerfile
# 005: Dockerfile đóng gói Gateway cùng các project mà Gateway đã tham chiếu.
# Bước 005 thay đổi cách build/chạy container, không thay đổi API của shared project.
```

[services/bff/src/Bff.Api/Dockerfile](../../services/bff/src/Bff.Api/Dockerfile)

```dockerfile
# 005: BFF được đóng gói vào local stack; ServiceDefaults và Tenancy vẫn là dependency của BFF.
```

Kết luận cho shared layer ở bước 005: tăng trưởng nằm ở cách build và khởi động các service đã sử dụng shared project, không nằm ở việc tạo hoặc mở rộng shared project.
