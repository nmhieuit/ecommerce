# Phase 1 Data Model: Timeout, retry và circuit breaker cho mọi cuộc gọi ra ngoài

Tính năng này không sở hữu database (xem `plan.md` Technical Context — Storage: N/A). Các "thực
thể" dưới đây là hình dạng cấu hình và bản ghi kiểm kê, không phải bản ghi được lưu trữ lâu dài;
chúng tồn tại trong cấu hình khởi động của service (resilience policy) hoặc trong mã nguồn test
(outbound call inventory).

## Resilience Policy (cấu hình, mỗi điểm gọi ra ngoài)

Bộ tham số mọi điểm gọi ra ngoài phải khai báo tường minh (research.md Decision 2). Mở rộng "Downstream
Service Client" đã có ở `specs/002-gateway-bff-routing/data-model.md` — cùng hình dạng, áp dụng thêm
cho các điểm gọi ngoài 4 typed client gốc.

| Field | Description | Notes |
|---|---|---|
| CallSiteName | Tên định danh điểm gọi (dùng trong log, trong `ResilienceCoverageScanner`) | Ví dụ `IdentityBackchannel`, `bff-cluster` |
| AttemptTimeout | Thời gian tối đa cho một lần thử | Bắt buộc, không được là `Timeout.InfiniteTimeSpan` |
| TotalRequestTimeout | Thời gian tối đa cho toàn bộ chuỗi thử (bao gồm retry) | Bắt buộc; PHẢI ≥ AttemptTimeout |
| RetryEnabled | Điểm gọi này có bật retry hay không | `false` cho gateway→BFF (Decision 3); `true` có điều kiện cho BFF's 4 client (Decision 5) |
| RetryAllowedMethods | Nếu RetryEnabled, tập HTTP method được phép retry | `{GET, HEAD}` cho BFF's typed client; N/A cho các điểm gọi chỉ có một method (ví dụ backchannel luôn GET) |
| RetryMaxAttempts / RetryDelay | Số lần thử lại + độ trễ khởi điểm (backoff) | Kế thừa giá trị đã có ở BFF cho 4 client gốc; giá trị mới cho backchannel dùng cùng cấu hình mặc định của `AddStandardResilienceHandler` trừ khi đo lường thực tế yêu cầu khác |
| CircuitBreakerEnabled | Điểm gọi này có circuit breaker hay không | `true` cho mọi điểm trong bảng kiểm kê bên dưới, kể cả gateway (qua passive health check thay vì Polly) |
| CircuitBreakerMechanism | Cơ chế cụ thể hiện thực circuit breaker | `Polly CircuitBreaker` (BFF, backchannel) hoặc `YARP Passive Health Check` (gateway — Decision 3) |

**Validation rules**: Một điểm gọi ra ngoài có `AttemptTimeout` hoặc `TotalRequestTimeout` không xác
định (hoặc bằng `Timeout.InfiniteTimeSpan`) là lỗi cấu hình, PHẢI bị `ResilienceCoverageTests` bắt được
trước khi tới review, không phải một hành vi runtime âm thầm. `RetryAllowedMethods` khác rỗng chỉ khi
`RetryEnabled = true`; một điểm gọi ghi dữ liệu (route có method không idempotent, ví dụ `POST` không
có idempotency key) KHÔNG được có method đó trong `RetryAllowedMethods` (spec FR-006).

## Outbound Call Inventory Entry (mã nguồn test, danh sách tường minh)

Một dòng trong danh sách kỳ vọng của `ResilienceCoverageScanner` (research.md Decision 7) — tương tự
`Boundary` record của `ContractCoverageScanner`.

| Field | Description | Notes |
|---|---|---|
| Name | Tên điểm gọi, khớp `CallSiteName` ở trên | Dùng trong thông báo lỗi test |
| Caller | Service khởi tạo cuộc gọi | `gateway`, `bff`, `parties`, `products`, `orders`, `baskets` |
| Callee | Đích của cuộc gọi | `bff`, `products`, `baskets`, `orders`, `parties`, `identity-server` |
| ConfigurationFile | File nguồn/cấu hình nơi resilience policy được khai báo | Đường dẫn tương đối gốc repo, ví dụ `services/gateway/src/Gateway.Api/appsettings.json` |
| RequiredMarkers | Danh sách chuỗi PHẢI xuất hiện trong `ConfigurationFile` để coi là "đã bọc" | Ví dụ `["HealthCheck", "Passive"]` cho gateway, `["AddStandardResilienceHandler"]` cho BFF/backchannel |

**Validation rules**: Danh sách này PHẢI liệt kê đủ 3 điểm gọi ra ngoài đang tồn tại trong kiểm kê ở
`research.md` Decision 1 (#1, #2, #3 — #4 không tồn tại nên không có dòng, #5 ngoài phạm vi nên không
có dòng). Thêm một điểm gọi ra ngoài mới vào hệ thống PHẢI đi kèm một dòng mới ở đây trong cùng PR —
đây là cơ chế review bắt buộc thay cho phát hiện tự động (research.md Decision 7).

## Outbound Call Site (khái niệm, không lưu trữ — tham chiếu)

Giữ nguyên như `specs/002-gateway-bff-routing/data-model.md` đã định nghĩa cho "Route Mapping" và
"Downstream Service Client"; tính năng này không định nghĩa lại, chỉ mở rộng phạm vi áp dụng
`Resilience Policy` ở trên sang các điểm gọi #1 và #3 mà 002 chưa bọc đầy đủ, và tinh chỉnh điểm gọi
#2 (đã có từ 002) theo Decision 5.
