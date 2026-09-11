# Kiến trúc: Timeout, retry và circuit breaker cho mọi cuộc gọi ra ngoài (outbound call)

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-30 ("[RESILIENCE-4] Timeouts, retry, and circuit-breaker on every outbound
call"), đặc tả tại
[`specs/020-timeouts-retry-circuit-breaker/`](../../specs/020-timeouts-retry-circuit-breaker/). 7
quyết định kiến trúc ở
[`research.md`](../../specs/020-timeouts-retry-circuit-breaker/research.md).

**Trạng thái xác minh**: 27 task, 26 đánh dấu `[X]` hoàn thành, 1 (`T025`) hoàn thành **một phần** —
5/6 bước `quickstart.md` đã chạy PASS thật, bước 6 (quan sát sự kiện Polly qua OTel Collector/Elastic)
**chưa thực hiện được** trong phiên triển khai (không có OTel Collector chạy sẵn trong sandbox lúc
đó) — xem mục 5. Có 2 phát hiện thật ngoài dự kiến ban đầu khi triển khai (mục 3).

## 1. Phạm vi thật — kiểm kê trước, không thiết kế theo suy đoán

Acceptance criteria gốc của Jira liệt kê 3 loại cuộc gọi (BFF→service, service→service,
service→broker) như thể cả 3 đều đang tồn tại. Trước khi thiết kế, `research.md` Decision 1 kiểm kê
lại **toàn bộ mã nguồn thật** — chỉ 3/5 điểm dưới đây thực sự tồn tại:

| # | Điểm gọi | Trạng thái TRƯỚC | Trạng thái SAU |
|---|---|---|---|
| 1 | Gateway → BFF (YARP forward) | Có timeout (`ActivityTimeout`), không có circuit breaker | + passive health check = circuit breaker (mục 2) |
| 2 | BFF → Products/Baskets/Orders/Parties (4 typed client) | Đã đủ timeout/retry/circuit breaker từ spec 002 | Giữ nguyên, chỉ thu hẹp retry theo method (mục 2) |
| 3 | Gateway + BFF + 5 service → Identity server (JwtBearer backchannel, OIDC discovery/JWKS) | Timeout ẩn (mặc định framework 60s), không retry/circuit breaker | + `HttpClient` tường minh có resilience đầy đủ (mục 2) |
| 4 | Service → service đồng bộ (vd orders gọi thẳng products) | **Không tồn tại** | Không đổi — không có gì để bọc |
| 5 | Service → broker (RabbitMQ/MassTransit publish) | **Không tồn tại** — `BasketCheckedOutMapper.cs` chỉ dựng payload, chưa ai gọi nó | Không đổi — thuộc SCRUM-31, xác nhận lại lúc hoàn tất (mục 5) |

Thiết kế máy móc theo đúng acceptance criteria gốc (dựng cả hạ tầng messaging chỉ để có 1 cuộc gọi
bọc resilience) sẽ vượt xa 8 story point của SCRUM-30 và trùng phạm vi SCRUM-31 — quyết định phạm vi
này là điều kiện tiên quyết của toàn bộ tính năng.

## 2. 3 thay đổi thật đã triển khai

### 2.1. Gateway → BFF: circuit breaker qua passive health check, KHÔNG thêm retry (Decision 3)

Bật `HealthCheck:Passive` của YARP trên cluster `bff-cluster` — KHÔNG thêm retry ở tầng reverse-proxy,
vì gateway forward nguyên văn mọi method (kể cả `POST /basket/items`, `POST /checkout`) mà không biết
ngữ nghĩa idempotency của route — 1 retry mù ở tầng transport-agnostic này có nguy cơ nhân đôi side
effect (đúng thứ FR-006 cấm).

**Bug thật tìm được khi xác thực thủ công** (`T016`, chạy gateway thật + `curl` lặp lại, đọc log debug
YARP): `AvailableDestinationsPolicy` mặc định của YARP là `HealthyOrPanic` — tự động coi MỌI
destination là "available" khi không còn destination nào khỏe mạnh. Với cluster chỉ có 1 destination
(`bff`), circuit breaker "mở" nhưng gateway **vẫn tiếp tục thử kết nối thật** (vẫn `502`, không
fail-fast). Phải đặt tường minh `AvailableDestinationsPolicy: "HealthyAndUnknown"` mới có hành vi
fail-fast thật — đã đo được `503` trong ~20ms sau khi sửa, có test riêng
(`BffCluster_UsesAvailableDestinationsPolicyThatActuallyExcludesUnhealthyDestinations`) và marker
`"AvailableDestinationsPolicy"` được thêm vào scanner rà soát (mục 4).

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
giỏ hàng trùng hoặc **đơn hàng trùng**. Hệ thống không có cơ chế idempotency-key nào (đã rà soát toàn
bộ `services/*`, xác nhận không có) — xây 1 cơ chế như vậy là 1 hạng mục lớn hơn nhiều, ngoài phạm vi
8 story point của tính năng này. Giải pháp đã chọn: thu hẹp `resilience.Retry.ShouldHandle` để chỉ
retry khi method là `GET`/`HEAD`. `POST` vẫn giữ nguyên timeout và circuit breaker, chỉ mất khả năng
tự động retry.

**Phát hiện kỹ thuật khi implement** (`T021`): kế hoạch ban đầu định đọc method từ
`args.Outcome.Result?.RequestMessage`, nhưng 1 lỗi transport (connection refused — không có
`HttpResponseMessage` nào) khiến `Outcome.Result` là `null`. Phải đọc method gốc từ
`args.Context.GetRequestMessage()` (`Polly.HttpResilienceContextExtensions`) thay vì từ `Outcome`.

## 3. Rà soát lặp lại được — `ResilienceCoverageTests` (Decision 7)

Theo đúng khuôn mẫu `ContractCoverageTests` đã có: 1 scanner đọc file cấu hình dưới dạng text, xác
nhận từng marker bắt buộc (`ActivityTimeout`, `AddStandardResilienceHandler`, `HealthCheck`,
`Passive`, `AvailableDestinationsPolicy`, `SafeToRetryMethods`) có mặt đúng chỗ — bắt lỗi khi ai đó vô
tình bỏ marker, kể cả với điểm gọi mới thêm sau này (spec FR-007). Danh sách `ExpectedCallSites` được
populate dần theo từng user story, không viết sẵn toàn bộ từ đầu.

## 4. Kết quả xác thực — có nhiễu môi trường, đã tách bạch rõ ràng

- `Gateway.Api.IntegrationTests` có 12/34 test FAIL **cả trước lẫn sau** thay đổi của tính năng này —
  đã xác nhận bằng `git stash -u` chạy lại đúng bộ test đó trên `master` gốc, cùng lỗi
  `Expected: NotFound/BadGateway, Actual: Unauthorized` xảy ra y hệt. Nghi ngờ liên quan cách
  `TestJwtBearer`/symmetric-key test token được cấp trong sandbox — **không phải hồi quy do tính năng
  này**, ngoài phạm vi SCRUM-30 để sửa.
- `Parties.Api.UnitTests`/`Products.Api.UnitTests` chỉ có 1 test và nó FAIL vì thiếu secret cục bộ
  (`ConnectionStrings:PartiesDb`/`ProductsDb`) — vấn đề môi trường từ `018-cluster-secret-store`, xác
  nhận không liên quan Identity/ServiceDefaults của tính năng này.
- Đã grep xác nhận lại giả định Decision 1 #5 vẫn đúng tại thời điểm hoàn tất: không có
  `AddMassTransit|IPublishEndpoint|IBus` nào trong `services/` — SCRUM-31 xác nhận chưa bắt đầu.

## 5. Giới hạn phạm vi đã biết

- **Bước 6 của `quickstart.md` (quan sát sự kiện Polly qua OTel Collector/Elastic thật) CHƯA thực
  hiện được trong chính phiên triển khai này** — không có OTel Collector chạy sẵn trong sandbox. Đã
  xác nhận ở mức cấu hình: `AddSource("Polly")`/`AddMeter("Polly")` có trong
  `ServiceDefaultsExtensions.cs`, build sạch — nhưng **chưa có bằng chứng runtime thật qua Elastic**
  cho chính tính năng này (khác với dữ liệu Elastic thật đã xác nhận ở
  [017](017_Architect_phát%20telemetry%20OTel%20qua%20ServiceDefaults%20tới%20Elastic.md), vốn không
  đặc thù cho sự kiện Polly).
- **Chỉ phủ 3/5 loại điểm gọi ở mục 1** — service→service đồng bộ không tồn tại nên không có gì để
  bọc; service→broker (SCRUM-31) vẫn hoàn toàn ngoài phạm vi, kể cả sau tính năng này.
- **Không xây cơ chế idempotency-key** — mục 2.3 chỉ thu hẹp retry theo method như 1 biện pháp giảm
  thiểu trong phạm vi, không giải quyết triệt để rủi ro trùng lặp nếu 1 client tự ý gửi lại `POST` thủ
  công (ngoài phạm vi retry tự động của resilience handler).
- **`docs/architecture/010_Architect_hạ tầng kiểm thử container thật.md`** từng ghi nhận việc "chịu
  lỗi broker toàn diện (retry, circuit breaker)" là phần còn treo của SCRUM-30 — nay đã có Amendment
  làm rõ: tính năng này chỉ đóng phần HTTP outbound, phần broker vẫn treo, chờ SCRUM-31.

## 6. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/020-timeouts-retry-circuit-breaker-component.drawio`](../diagrams/020-timeouts-retry-circuit-breaker-component.drawio)
- Sơ đồ luồng nghiệp vụ: [`docs/diagrams/020-timeouts-retry-circuit-breaker-flow-nghiep-vu.drawio`](../diagrams/020-timeouts-retry-circuit-breaker-flow-nghiep-vu.drawio)
- Sơ đồ trình tự: [`docs/diagrams/020-timeouts-retry-circuit-breaker-sequence.drawio`](../diagrams/020-timeouts-retry-circuit-breaker-sequence.drawio)
