# Kiến trúc: Timeout, retry và circuit breaker cho mọi cuộc gọi ra ngoài (outbound call)

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-30 ("[RESILIENCE-4] Timeouts, retry, and circuit-breaker on every outbound
call"), đặc tả tại
[`specs/020-timeouts-retry-circuit-breaker/`](../../specs/020-timeouts-retry-circuit-breaker/). 7
quyết định kiến trúc ở
[`research.md`](../../specs/020-timeouts-retry-circuit-breaker/research.md).

**Trạng thái xác minh**: 27 task, 26 đánh dấu `[X]` hoàn thành, 1 (`T025`) hoàn thành một phần — 5/6
bước `quickstart.md` đã chạy PASS thật, bước 6 (quan sát sự kiện Polly qua OTel Collector/Elastic)
chưa thực hiện được trong phiên triển khai (không có OTel Collector chạy sẵn trong sandbox lúc đó) —
xem [technical-debt.md](technical-debt.md). Có 2 bug thật tìm được khi triển khai, cũng ở đó.

## 1. Phạm vi thật — kiểm kê trước, không thiết kế theo suy đoán

Acceptance criteria gốc của Jira liệt kê 3 loại cuộc gọi (BFF→service, service→service,
service→broker) như thể cả 3 đều đang tồn tại. Trước khi thiết kế, `research.md` Decision 1 kiểm kê
lại **toàn bộ mã nguồn thật** — chỉ 3/5 điểm dưới đây thực sự tồn tại:

| # | Điểm gọi | Trạng thái TRƯỚC | Trạng thái SAU |
|---|---|---|---|
| 1 | Gateway → BFF (YARP forward) | Có timeout (`ActivityTimeout`), không có circuit breaker | + passive health check = circuit breaker (mục 2.1) |
| 2 | BFF → Products/Baskets/Orders/Parties (4 typed client) | Đã đủ timeout/retry/circuit breaker từ spec 002 | Giữ nguyên, chỉ thu hẹp retry theo method (mục 2.3) |
| 3 | Gateway + BFF + 5 service → Identity server (JwtBearer backchannel, OIDC discovery/JWKS) | Timeout ẩn (mặc định framework 60s), không retry/circuit breaker | + `HttpClient` tường minh có resilience đầy đủ (mục 2.2) |
| 4 | Service → service đồng bộ (vd orders gọi thẳng products) | **Không tồn tại** | Không đổi — không có gì để bọc |
| 5 | Service → broker (RabbitMQ/MassTransit publish) | **Không tồn tại** | Không đổi trong tính năng này. **Amendment (2026-09-12)**: [024-verify-transactional-outbox](../../specs/024-verify-transactional-outbox/) (SCRUM-31) sau đó đã hiện thực publish thật cho `OrderPlaced` — call-site này NAY ĐÃ TỒN TẠI nhưng chưa được rà soát resilience/circuit-breaker theo đúng khuôn mẫu tính năng này, xem [technical-debt.md](technical-debt.md) |

Thiết kế máy móc theo đúng acceptance criteria gốc (dựng cả hạ tầng messaging chỉ để có 1 cuộc gọi
bọc resilience) sẽ vượt xa 8 story point của SCRUM-30 và trùng phạm vi SCRUM-31 — quyết định phạm vi
này là điều kiện tiên quyết của toàn bộ tính năng.

## 2. 3 thay đổi thật đã triển khai

### 2.1. Gateway → BFF: circuit breaker qua passive health check, KHÔNG thêm retry (Decision 3)

Bật `HealthCheck:Passive` của YARP trên cluster `bff-cluster` — KHÔNG thêm retry ở tầng reverse-proxy,
vì gateway forward nguyên văn mọi method (kể cả `POST /basket/items`, `POST /checkout`) mà không biết
ngữ nghĩa idempotency của route — 1 retry mù ở tầng transport-agnostic này có nguy cơ nhân đôi side
effect (đúng thứ FR-006 cấm). Bug thật tìm được khi xác thực (`AvailableDestinationsPolicy` của YARP)
— xem [technical-debt.md](technical-debt.md).

### 2.2. JwtBearer backchannel: từ mặc định ẩn sang `HttpClient` tường minh (Decision 4)

`JwtBearerOptions.Backchannel` mặc định trống → framework tự tạo 1 `HttpClient` với
`BackchannelTimeout` = 60 giây — 1 giá trị **ẩn**, không phải khai báo tường minh trong cấu hình của
service, không thỏa yêu cầu "explicit" của spec FR-001. Đã thay bằng 1 `HttpClient` đặt tên
`"IdentityBackchannel"` qua `IHttpClientFactory`, gắn `AddStandardResilienceHandler()` riêng (timeout
5s/15s — phù hợp tần suất gọi thấp của OIDC discovery/JWKS, tách biệt khỏi ngân sách nghiệp vụ của
mục 2.1/2.3). Sửa đúng 1 lần ở `shared/Identity/IdentityValidationExtensions.cs` (bao phủ BFF + 4
domain service) và 1 lần ở gateway — không lặp lại cấu hình ở 6 nơi gọi.

### 2.3. BFF → 4 downstream service: retry chỉ cho method an toàn (Decision 5)

**Đây là 1 bug tiềm ẩn thật đã tồn tại từ trước, không phải tính năng mới.** Cấu hình retry hiện có
(từ spec 002) áp dụng cho **mọi** HTTP method — nghĩa là `POST /basket/items` hay `POST /checkout` có
thể bị gửi lại sau khi bản gốc đã tới server thành công nhưng phản hồi bị mất trên đường về, tạo dòng
giỏ hàng trùng hoặc **đơn hàng trùng**. Hệ thống không có cơ chế idempotency-key nào — xây 1 cơ chế
như vậy là 1 hạng mục lớn hơn nhiều, ngoài phạm vi 8 story point của tính năng này. Giải pháp đã chọn:
thu hẹp `resilience.Retry.ShouldHandle` để chỉ retry khi method là `GET`/`HEAD`. `POST` vẫn giữ nguyên
timeout và circuit breaker, chỉ mất khả năng tự động retry. Phát hiện kỹ thuật khi implement (đọc
method từ `Outcome` vs `GetRequestMessage()`) — xem [technical-debt.md](technical-debt.md).

## 3. Rà soát lặp lại được — `ResilienceCoverageTests` (Decision 7)

Theo đúng khuôn mẫu `ContractCoverageTests` đã có: 1 scanner đọc file cấu hình dưới dạng text, xác
nhận từng marker bắt buộc (`ActivityTimeout`, `AddStandardResilienceHandler`, `HealthCheck`,
`Passive`, `AvailableDestinationsPolicy`, `SafeToRetryMethods`) có mặt đúng chỗ — bắt lỗi khi ai đó vô
tình bỏ marker, kể cả với điểm gọi mới thêm sau này (spec FR-007). Danh sách `ExpectedCallSites` được
populate dần theo từng user story, không viết sẵn toàn bộ từ đầu.

## 4. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/020-timeouts-retry-circuit-breaker-component.drawio`](../diagrams/020-timeouts-retry-circuit-breaker-component.drawio)
- Sơ đồ luồng nghiệp vụ: [`docs/diagrams/020-timeouts-retry-circuit-breaker-flow-nghiep-vu.drawio`](../diagrams/020-timeouts-retry-circuit-breaker-flow-nghiep-vu.drawio)
- Sơ đồ trình tự: [`docs/diagrams/020-timeouts-retry-circuit-breaker-sequence.drawio`](../diagrams/020-timeouts-retry-circuit-breaker-sequence.drawio)

2 bug thật tìm được khi triển khai, kết quả xác thực có nhiễu môi trường đã tách bạch, và giới hạn
phạm vi đã biết (bước 6 quickstart chưa chạy, chỉ phủ 3/5 điểm gọi, chưa có idempotency-key): xem
[technical-debt.md](technical-debt.md).
