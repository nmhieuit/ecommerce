# Quickstart: Xác thực Timeout, Retry và Circuit Breaker

**Feature**: [spec.md](./spec.md) | Tham chiếu: [data-model.md](./data-model.md), [contracts/](./contracts/)

Hướng dẫn này xác thực rằng 5 khoảng hở nêu ở `plan.md` Summary đã được khép kín, ứng với 3 test
scenario gốc của SCRUM-30 và acceptance scenario của spec.md's User Story 1-3.

## Điều kiện tiên quyết

- .NET 10 SDK đã cài; solution build được (`dotnet build Ecommerce.slnx`).
- Các dependency chạy cục bộ theo cách hiện có của repo (SQL Server, Redis — xem README gốc); tính
  năng này không thêm dependency hạ tầng mới.
- Bộ test tĩnh dưới đây PASS trước khi thử kịch bản động — đây là điều kiện cần, không phải tuỳ chọn.

## Bước 1 — Test scenario gốc của Jira: rà soát điểm đăng ký client (tĩnh)

```bash
dotnet test tests/ResilienceCoverageTests
```

**Kỳ vọng**: PASS. Test này đọc trực tiếp danh sách ở
[contracts/outbound-call-inventory-contract.md](./contracts/outbound-call-inventory-contract.md) và
xác nhận mỗi `ConfigurationFile` chứa đủ `RequiredMarkers` — tương đương thao tác "grep for outbound
HTTP client registrations" trong Jira Test Scenario 1, nhưng lặp lại được trong CI thay vì làm tay.

## Bước 2 — Xác nhận retry không mù (User Story 3, Acceptance Scenario 3 / Edge Case 1)

```bash
dotnet test services/bff/tests/Bff.Api.UnitTests --filter FullyQualifiedName~RetryMethodPolicyTests
```

**Kỳ vọng**: PASS (2/2) — test dựng `BasketsApiClient` thật qua `AddDownstreamClients`, thay primary
handler bằng một handler đếm số lần gọi rồi luôn báo lỗi tạm thời, và gọi trực tiếp
`AddItemAsync` (POST — kỳ vọng đúng 1 lần gọi, không retry) so với `GetCurrentBasketAsync` (GET —
kỳ vọng đúng 3 lần gọi, đã retry theo `MaxRetryAttempts`). Cùng client, cùng pipeline, chỉ khác
method — cách này tránh phải dựng cả chuỗi route HTTP (add-item cần gọi products trước khi tới
baskets) mà vẫn chứng minh đúng hành vi.

## Bước 3 — Jira Test Scenario 2: kill service, hammer BFF, xác nhận circuit breaker (BFF→service)

Kịch bản này đã có sẵn từ 002-gateway-bff-routing dưới dạng test tự động
(`services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs`); chạy lại để xác nhận
chưa bị hỏng bởi thay đổi ở Bước 2:

```bash
dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter FullyQualifiedName~DownstreamUnavailable
```

**Kỳ vọng**: PASS — sau ngưỡng lỗi liên tiếp đã cấu hình, các request tiếp theo tới downstream đó
nhận lỗi 502 gần như ngay lập tức (không chờ hết `TotalRequestTimeout`), khớp SC-003 của spec.md.

## Bước 4 — Circuit breaker mới ở gateway (gateway→BFF)

```bash
dotnet test services/gateway/tests/Gateway.Api.UnitTests --filter FullyQualifiedName~PassiveHealthCheck
dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter FullyQualifiedName~PassiveHealthCheckCircuitBreakerTests
```

**Kỳ vọng**: `PassiveHealthCheckConfigurationTests` PASS tĩnh (4/4 — xác nhận `appsettings.json` khai
báo `HealthCheck:Passive` VÀ `HealthCheck:AvailableDestinationsPolicy: "HealthyAndUnknown"` hợp lệ
cho `bff-cluster`). `PassiveHealthCheckCircuitBreakerTests` (file mới, không phải mở rộng
`DownstreamUnavailableTests` như dự tính ban đầu) PASS (1/1) — xác nhận động: sau 2 request lỗi liên
tiếp tới BFF không thể tới được, request thứ 3 nhận `503 ServiceUnavailable` trong vài chục mili giây
thay vì `502 BadGateway` sau ~2-4 giây thử kết nối thật — khớp User Story 2 Acceptance Scenario 2.
**Phát hiện quan trọng khi xác thực thủ công**: `AvailableDestinationsPolicy` mặc định của YARP
(`HealthyOrPanic`) KHÔNG fail-fast cho cluster 1 destination — phải đặt tường minh
`"HealthyAndUnknown"` mới có hành vi đúng (xem `tasks.md` T016).

## Bước 5 — Jira Test Scenario 3: độ trễ nhân tạo, xác nhận timeout của caller kích hoạt

```bash
dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter FullyQualifiedName~Timeout
```

**Kỳ vọng**: PASS — downstream giả lập trễ vượt `AttemptTimeout`/`TotalRequestTimeout` khiến caller
nhận lỗi 504 đúng lúc timeout hết hạn, không treo tới khi downstream tự phản hồi (spec User Story 1,
Acceptance Scenario 3).

## Bước 6 — Quan sát được (FR-008): sự kiện resilience lên OTel

```bash
dotnet run --project services/bff/src/Bff.Api
# ở terminal khác, dồn dập gọi một endpoint qua downstream đã tắt để kích hoạt retry + circuit breaker
```

**Kỳ vọng**: Trong log có cấu trúc (stdout hoặc Elastic nếu OTel Collector đang chạy cục bộ), xuất
hiện các entry gắn nguồn `"Polly"` cho từng lần thử, từng lần retry, và lần circuit breaker đổi trạng
thái — không chỉ một dòng log lỗi cuối cùng từ `DownstreamExceptionHandler`. Nếu OTel Collector/Elastic
không chạy cục bộ, xác nhận qua `dotnet-counters monitor --process-id <pid> Polly` thay thế.

## Dọn dẹp

Không có tài nguyên hạ tầng nào được tạo riêng cho tính năng này (không container, không cluster) —
dừng các tiến trình `dotnet run`/`dotnet test` đã khởi chạy ở trên là đủ.
