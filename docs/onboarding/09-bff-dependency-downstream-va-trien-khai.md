# 09 — BFF: có phụ thuộc downstream không, và cơ chế gọi khi triển khai

> Đọc [01](01-tong-quan-kien-truc.md) và [02](02-orders-service-va-cac-api-endpoint.md) trước. Tài liệu này trả lời trực tiếp: BFF có phụ thuộc (dependency) vào 4 service downstream (products/baskets/orders/parties) không, và cơ chế gọi hoạt động thế nào khi chạy Docker Desktop (thật, đã triển khai) so với Kubernetes (chưa tồn tại trong repo — xem cảnh báo ở Phần 4).

## Phần 1 — "Phụ thuộc" ở 3 mức khác nhau, không phải 1 câu trả lời có/không

### 1.1 Lúc biên dịch (compile-time): KHÔNG phụ thuộc

[`services/bff/src/Bff.Api/Bff.Api.csproj`](../../services/bff/src/Bff.Api/Bff.Api.csproj) chỉ có 3 `ProjectReference`, cả 3 đều vào `shared/`:
```xml
<ProjectReference Include="..\..\..\..\shared\ServiceDefaults\ServiceDefaults.csproj" />
<ProjectReference Include="..\..\..\..\shared\Tenancy\Tenancy.csproj" />
<ProjectReference Include="..\..\..\..\shared\Identity\Identity.csproj" />
```
**Không có** `ProjectReference` nào tới `Baskets.Api.csproj`/`Orders.Api.csproj`/`Parties.Api.csproj`/`Products.Api.csproj`. BFF không thể "nhìn thấy" 1 dòng code C# nào của 4 service kia lúc biên dịch — build BFF thành công hay thất bại hoàn toàn độc lập với 4 service đó có tồn tại hay không trên đĩa.

### 1.2 Lúc khởi động (startup): phụ thuộc do ĐIỀU PHỐI HẠ TẦNG áp đặt, không phải code C# tự làm

Đây là điểm dễ nhầm lẫn nhất — cần tách rõ 2 lớp khác nhau:

- **Code C# của BFF** (`DownstreamServiceClientOptions.cs`) chỉ validate rằng cấu hình `Services:{ServiceName}:BaseUrl` là 1 URI hợp lệ (`.ValidateOnStart()`), **không hề gọi thử** downstream để xác nhận nó đang sống. BFF khởi động thành công ngay cả khi cả 4 service kia đang tắt — nó chỉ thất bại khi có 1 request THẬT cần gọi tới chúng.
- **Docker Compose** (không phải code) mới là nơi áp đặt thứ tự khởi động — cả 2 file [`docker-compose.local.yml`](../../docker-compose.local.yml) và [`docker-compose.yml`](../../docker-compose.yml) đều khai báo:
  ```yaml
  bff-api:
    depends_on:
      products-api: { condition: service_healthy }
      baskets-api:  { condition: service_healthy }
      orders-api:   { condition: service_healthy }
      parties-api:  { condition: service_healthy }
      identity-api: { condition: service_healthy }
  ```
  Comment ngay bên cạnh trong `docker-compose.yml` giải thích lý do: *"BFF không sở hữu database nào nên không giữ connection string nào — nó chờ đủ 4 service nó tổng hợp: 1 BFF khởi động trước chúng sẽ trả 502 cho request đầu tiên."* Đây là quyết định điều phối ở tầng hạ tầng (Compose), KHÔNG phải giới hạn kỹ thuật của code C# — bản thân BFF hoàn toàn có khả năng chạy độc lập rồi trả lỗi rõ ràng (xem 1.3) khi 1 downstream chưa sẵn sàng.

### 1.3 Lúc xử lý request (runtime): CÓ phụ thuộc, đúng 1-hoặc-nhiều tuỳ route

Mỗi route BFF (đã liệt kê ở [02](02-orders-service-va-cac-api-endpoint.md#5-bff--cùng-pattern-khác-vai-trò)) gọi đúng 1 downstream — TRỪ route `/bff/checkout` ([`CheckoutEndpoints.cs`](../../services/bff/src/Bff.Api/Features/Checkout/CheckoutEndpoints.cs), đã xem ở [04](04-giai-doan-2-spa-va-demo-end-to-end.md#route-trải-dài-nhiều-service-đầu-tiên-checkout-spec-004)) — route DUY NHẤT phụ thuộc **2 downstream cùng lúc** (`baskets` rồi `orders`) trong 1 request.

## Phần 2 — Cơ chế gọi trong code: `DownstreamClients/`

### Đăng ký: 1 hàm dùng chung cho cả 4 client, không ai "quên" cấu hình

[`DownstreamClientRegistrationExtensions.cs`](../../services/bff/src/Bff.Api/DownstreamClients/DownstreamClientRegistrationExtensions.cs):
```csharp
services.AddDownstreamClient<ProductsApiClient>(ProductsApiClient.ServiceName, configuration);
services.AddDownstreamClient<BasketsApiClient>(BasketsApiClient.ServiceName, configuration);
services.AddDownstreamClient<OrdersApiClient>(OrdersApiClient.ServiceName, configuration);
services.AddDownstreamClient<PartiesApiClient>(PartiesApiClient.ServiceName, configuration);
```
Cả 4 gọi qua đúng 1 hàm private `AddDownstreamClient<TClient>(...)` — comment đầu file nói rõ lý do: *"để không ai trong 4 client này âm thầm thiếu timeout hay resilience pipeline."* Địa chỉ mỗi client lấy từ mục cấu hình `Services:{ServiceName}:BaseUrl` (ví dụ `Services:ProductsApi:BaseUrl`) — đọc ở [Phần 3](#phần-3--cơ-chế-gọi-downstream-khi-chạy-trên-docker-desktop-thật-đã-triển-khai) giá trị thật của nó ở từng môi trường.

### Timeout/retry: con số cụ thể, không suy đoán

```csharp
private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(1);       // 1 lần gọi tối đa 1s
private static readonly TimeSpan TotalRequestTimeout = TimeSpan.FromSeconds(3);  // cả request (kể cả retry) tối đa 3s
private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);
private const int MaxRetryAttempts = 2;                                          // tối đa 2 lần retry
```
Dùng gói `Microsoft.Extensions.Http.Resilience` (`.AddStandardResilienceHandler(...)`) — timeout + retry + circuit breaker có sẵn của .NET, không phải code tự viết. Chi tiết đáng chú ý: `RetryDelay = 200ms` thay vì mặc định 2s của thư viện — comment giải thích: *"độ trễ retry mặc định của handler chuẩn là 2s, không vừa trong ngân sách tổng 3s: riêng độ trễ của lần retry đầu đã ăn hết ngân sách, khiến 1 downstream từ chối kết nối NGAY LẬP TỨC vẫn bị báo cáo như timeout (504) thay vì unavailable (502)"* — ở 200ms, cả 3 lần thử đều xong trong ngân sách, nên lỗi nhanh vẫn được báo là lỗi nhanh.

Ràng buộc liên service đáng nhớ: comment trong code nói `TotalRequestTimeout` (3s) này phải luôn **≤** ngân sách forward của gateway — quan hệ đó được chính 1 unit test (`Gateway.Api.UnitTests/ForwardingTimeoutBudgetTests`) canh giữ, không chỉ dựa vào comment.

### Lan truyền tenant/subject/token: 1 handler chèn vào MỌI request outbound

[`TenantPropagationHandler.cs`](../../services/bff/src/Bff.Api/DownstreamClients/TenantPropagationHandler.cs) — 1 `DelegatingHandler` gắn vào pipeline của cả 4 client, chèn lại 3 header trước khi gửi:
```csharp
Relay(request, TenantContextMiddleware.HeaderName, requestServices?.GetService<TenantContext>()?.TenantId);
Relay(request, CallerContextMiddleware.HeaderName, requestServices?.GetService<CallerContext>()?.SubjectId);
Relay(request, "Authorization", httpContext?.Request.Headers.Authorization.ToString());
```
Comment giải thích lý do phải làm thủ công: *"1 `HttpClient` thường không tự forward gì cả. YARP tự copy header ở chặng gateway → BFF, khiến người ta dễ tưởng cả chuỗi đều tự động — thiếu handler này thì chặng BFF → domain service sẽ âm thầm làm rơi mất tenant."* Kể từ tính năng 014, cùng handler này cũng lan truyền lại **nguyên vẹn** header `Authorization` — vì mỗi domain service tự xác thực token độc lập (đã giải thích ở [01](01-tong-quan-kien-truc.md#6-vì-sao-mỗi-service-tự-xác-thực-không-tin-gateway)), thiếu bước này thì mọi request downstream thật sẽ bị chính domain service đó từ chối vì không có token.

Chi tiết kỹ thuật tinh tế: tenant được đọc qua `IHttpContextAccessor` thay vì inject thẳng `TenantContext` — vì `IHttpClientFactory` dựng và **tái sử dụng** message handler qua nhiều request khác nhau; nếu inject 1 service scoped trực tiếp, nó sẽ bị "chốt cứng" vào tenant của request đầu tiên tạo ra handler đó, và MỌI request sau sẽ vô tình mang tenant của người khác — comment gọi thẳng đây là rủi ro "truy cập dữ liệu chéo tenant" nếu làm sai.

### Khi downstream lỗi: 1 chỗ xử lý duy nhất, không route nào tự viết try/catch riêng

[`DownstreamCall.cs`](../../services/bff/src/Bff.Api/DownstreamClients/DownstreamCall.cs) là điểm MỌI lời gọi downstream đều phải đi qua — bọc lỗi resilience-pipeline/transport thành `DownstreamServiceException` có tên rõ dependency nào lỗi. [`DownstreamExceptionHandler.cs`](../../services/bff/src/Bff.Api/ErrorHandling/DownstreamExceptionHandler.cs) (đăng ký ở `Program.cs`, đã xem ở [01](01-tong-quan-kien-truc.md)) bắt riêng loại exception đó, phân biệt rõ 2 trường hợp:
```csharp
var status = timedOut ? HttpStatusCode.GatewayTimeout : HttpStatusCode.BadGateway;   // 504 vs 502
```
`502` (Bad Gateway) khi không kết nối được hoặc circuit breaker đang mở; `504` (Gateway Timeout) khi hết ngân sách thời gian. Nội dung trả về client CHỈ nêu **tên logic** của service (`"ProductsApi"`), không bao giờ lộ `BaseUrl` thật — comment ghi rõ: *"toàn bộ exception vào log, nơi topology không phải rủi ro rò rỉ; chỉ tên logic ra tới caller."* Cơ chế này đã được kiểm chứng bằng test thật (`DownstreamUnavailableTests.cs`, đã phân tích ở [08](08-chien-luoc-test-unit-integration-va-playwright.md#kỹ-thuật-đáng-chú-ý-giả-lập-lỗi-mạng-không-cần-dừng-container-thật)).

## Phần 3 — Cơ chế gọi downstream khi chạy trên Docker Desktop (thật, đã triển khai)

Đây là phần thú vị nhất: `BaseUrl` của 4 downstream client **khác nhau giữa 2 file cấu hình**, và lý do khác nhau đó chính là chìa khoá hiểu cơ chế:

[`appsettings.json`](../../services/bff/src/Bff.Api/appsettings.json) (mặc định gốc):
```json
"Services": {
  "ProductsApi": { "BaseUrl": "http://products-api:8080" },
  "BasketsApi":  { "BaseUrl": "http://baskets-api:8080" },
  "OrdersApi":   { "BaseUrl": "http://orders-api:8080" },
  "PartiesApi":  { "BaseUrl": "http://parties-api:8080" }
}
```
[`appsettings.Development.json`](../../services/bff/src/Bff.Api/appsettings.Development.json) (ghi đè khi `ASPNETCORE_ENVIRONMENT=Development`):
```json
"Services": {
  "ProductsApi": { "BaseUrl": "http://localhost:5088" },
  "BasketsApi":  { "BaseUrl": "http://localhost:5188" },
  "OrdersApi":   { "BaseUrl": "http://localhost:5041" },
  "PartiesApi":  { "BaseUrl": "http://localhost:5204" }
}
```
`products-api`/`baskets-api`/... KHÔNG phải tên miền thật — đó là **tên service trong Docker Compose**, được Docker tự cấp DNS nội bộ trong mạng ảo `backbone` (đã thấy khai báo `networks: [backbone]` cho mọi service ở [01](01-tong-quan-kien-truc.md)). Container này gọi `http://products-api:8080` thành công vì Docker Engine tự phân giải tên đó tới đúng IP nội bộ của container `products-api` — cơ chế giống hệt cách `identity-api` được giải thích ở [06](06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md), áp dụng y hệt cho cả 4 downstream của BFF.

**Vì sao 2 file khác nhau, và vì sao `docker-compose.local.yml` phải GHI ĐÈ THÊM 1 LẦN NỮA** ([đã đọc ở 01](01-tong-quan-kien-truc.md)):
```yaml
bff-api:
  environment:
    ASPNETCORE_ENVIRONMENT: Development
    Services__ProductsApi__BaseUrl: http://products-api:8080   # ghi đè LẠI giá trị compose hostname
    ...
```
Chuỗi suy luận đầy đủ, xác minh từng bước:
1. `docker-compose.local.yml` set `ASPNETCORE_ENVIRONMENT: Development` cho container `bff-api` — cần thiết vì Development là điều kiện duy nhất `Program.cs` (dòng `if (app.Environment.IsDevelopment()) { app.MapOpenApi()... }`) publish tài liệu OpenAPI mà pipeline Orval codegen của frontend cần.
2. Nhưng `ASPNETCORE_ENVIRONMENT=Development` cũng khiến ASP.NET Core tự nạp `appsettings.Development.json` **đè lên** `appsettings.json` — tức là 4 `BaseUrl` sẽ trở thành `http://localhost:5088`... **BÊN TRONG CONTAINER, `localhost` là chính container đó**, không phải service khác — 4 client sẽ gọi vào chính mình và luôn thất bại.
3. Để sửa, `docker-compose.local.yml` set biến môi trường `Services__ProductsApi__BaseUrl` (cú pháp `__` là quy ước ASP.NET Core đọc biến môi trường thành cấu trúc JSON phân cấp) — biến môi trường có **độ ưu tiên cao nhất** trong chuỗi cấu hình ASP.NET Core, ghi đè lên cả `appsettings.Development.json`, khôi phục lại đúng hostname Compose.

`docker-compose.yml` (bản gần production, đã đọc ở trên) thì **không** cần bước 3 này — vì nó không set `ASPNETCORE_ENVIRONMENT: Development`, nên không bao giờ nạp `appsettings.Development.json`, nên 4 `BaseUrl` gốc trong `appsettings.json` (đã đúng sẵn là hostname Compose) không hề bị ghi đè sai.

## Phần 4 — Kubernetes trong tương lai: PHÂN TÍCH, không phải mô tả cấu hình có sẵn

**Đã kiểm tra trực tiếp: repo này hiện KHÔNG CÓ bất kỳ file K8s/Helm/manifest nào** (không thư mục `k8s/`, không file `*.yaml` kiểu Deployment/Service nào trong toàn repo). Mọi nội dung dưới đây là **phân tích mã nguồn hiện tại** để trả lời "có sẵn sàng cho K8s không" — không phải mô tả cấu hình đã tồn tại.

**Điểm thuận lợi đã thấy trong code (Phần 2-3):** toàn bộ cơ chế xác định địa chỉ downstream đã **tách khỏi code**, đi qua đúng 1 kênh — cấu hình đọc bằng key `Services:{ServiceName}:BaseUrl`, nạp bằng biến môi trường theo quy ước `Services__{ServiceName}__BaseUrl`. Đây chính xác là cơ chế K8s cũng dùng để cấp phát địa chỉ (biến môi trường bơm từ `ConfigMap`/`Deployment.spec.containers[].env`, trỏ tới DNS nội bộ cluster của 1 `Service` K8s) — **không có dòng code C# nào cần sửa** để chuyển sang K8s, chỉ cần thay giá trị biến môi trường lúc triển khai. Ví dụ MINH HOẠ (không phải giá trị thật, vì chưa có manifest nào): `Services__ProductsApi__BaseUrl=http://products-api.default.svc.cluster.local:8080` hoặc ngắn gọn `http://products-api:8080` nếu cùng namespace — tên `products-api` ở đây là tên 1 K8s `Service` GIẢ ĐỊNH, không phải trích dẫn từ file thật nào trong repo.

**Điểm cần xem lại — sự khác biệt giữa cách Compose và K8s xử lý "chờ downstream sẵn sàng":** Phần 1.2 đã chỉ ra `depends_on: condition: service_healthy` của Docker Compose là cơ chế **hạ tầng**, không phải code, đảm bảo BFF chỉ khởi động sau khi cả 4 downstream đã healthy. Kubernetes **không có khái niệm `depends_on` tương đương** cho `Deployment` — các Pod khởi động độc lập, không đảm bảo thứ tự. Đây KHÔNG phải vấn đề với code BFF hiện tại — vì (Phần 1.2) chính code BFF vốn đã KHÔNG cần downstream sẵn sàng lúc khởi động (chỉ validate cấu hình, không ping thử), và cơ chế resilience/circuit-breaker (Phần 2) vốn được thiết kế để xử lý downstream tạm thời chưa sẵn sàng ở TỪNG REQUEST — đây chính xác là mô hình K8s khuyến khích (đừng dựa vào thứ tự khởi động, hãy chịu lỗi mỗi request). Nói cách khác: quyết định thiết kế "BFF không tự kiểm tra downstream lúc start" tưởng như chỉ để đơn giản hoá code, hoá ra lại khớp tự nhiên với mô hình K8s hơn là với Docker Compose.

**Về nhiều bản sao (multiple replicas):** không tìm thấy bằng chứng nào trong code đã đọc (Phần 2) giả định BFF chỉ có 1 instance — không có state cục bộ (BFF không sở hữu database, đã xác nhận ở [01](01-tong-quan-kien-truc.md)), mỗi request tự đọc lại tenant/token từ chính request đó (Phần 2, `TenantPropagationHandler`) chứ không giữ trạng thái giữa các request. Đây là quan sát dựa trên những gì đã đọc trong tài liệu này, không phải đã rà soát toàn bộ codebase BFF — nếu cần khẳng định chắc chắn 100% an toàn khi chạy nhiều replica, nên rà thêm các phần chưa đọc ở đây (`Features/*/Endpoints.cs` còn lại) trước khi kết luận.

## Đi đâu tiếp theo

- [01-tong-quan-kien-truc.md](01-tong-quan-kien-truc.md) — luồng request tổng thể qua gateway/BFF.
- [06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md](06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md) — cơ chế DNS nội bộ Docker Compose áp dụng y hệt cho `identity-api`.
- [08-chien-luoc-test-unit-integration-va-playwright.md](08-chien-luoc-test-unit-integration-va-playwright.md) — cách `DownstreamUnavailableTests.cs` kiểm chứng đúng cơ chế lỗi 502/504 mô tả ở Phần 2.
