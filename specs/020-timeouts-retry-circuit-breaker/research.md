# Phase 0 Research: Timeout, retry và circuit breaker cho mọi cuộc gọi ra ngoài

Không còn `[NEEDS CLARIFICATION]` nào trong Technical Context của `plan.md` — mọi quyết định dưới
đây được rút ra trực tiếp từ việc rà soát mã nguồn hiện có (dẫn chiếu file cụ thể) và từ constitution
Principle VIII, không phải giả định.

## Decision 1: Phạm vi thực tế — kiểm kê mọi điểm gọi ra ngoài hiện có trong hệ thống

**Decision**: Trước khi thiết kế bất kỳ thay đổi nào, kiểm kê toàn bộ điểm gọi ra ngoài đang tồn tại
trong mã nguồn (không phải suy đoán từ acceptance criteria của Jira), rồi chỉ thiết kế cho những
điểm thực sự tồn tại.

| # | Điểm gọi | Trạng thái hiện tại | Nguồn |
|---|---|---|---|
| 1 | Gateway → BFF (YARP forward) | Timeout ✅ (`ActivityTimeout: 00:00:10`, kiểm chứng bởi `ForwardingTimeoutBudgetTests`). Retry: không có (chủ ý, xem Decision 3). Circuit breaker: ❌ chưa cấu hình. | `services/gateway/src/Gateway.Api/appsettings.json` |
| 2 | BFF → Products/Baskets/Orders/Parties (4 typed client) | Timeout + Retry + Circuit breaker ✅ đầy đủ qua `AddStandardResilienceHandler()`. | `services/bff/src/Bff.Api/DownstreamClients/DownstreamClientRegistrationExtensions.cs` |
| 3 | Gateway + BFF + parties + products + orders + baskets (6 service) → Identity server, JwtBearer backchannel (OIDC discovery + JWKS) | Timeout: dựa vào `BackchannelTimeout` mặc định của framework (bị chặn nhưng không khai báo tường minh). Retry/circuit breaker: ❌ không có. | `shared/Identity/IdentityValidationExtensions.cs`, `services/gateway/src/Gateway.Api/Identity/ToggleGatedAuthenticationExtensions.cs` |
| 4 | Service → service (đồng bộ, ví dụ orders gọi thẳng products) | Không tồn tại — không có cuộc gọi HTTP đồng bộ nào giữa hai domain service. | Rà soát toàn bộ `services/*/src` |
| 5 | Service → broker (RabbitMQ/MassTransit publish) | Không tồn tại. `BasketCheckedOutMapper.cs` chỉ dựng payload, chưa có nơi nào gọi nó; comment trong chính file đó ghi rõ "the outbox and the publisher... are SCRUM-31's work". | `services/baskets/src/Baskets.Api/Features/Checkout/BasketCheckedOutMapper.cs` |

**Rationale**: Acceptance criteria gốc của SCRUM-30 liệt kê ba loại cuộc gọi (BFF→service,
service→service, service→broker) như thể cả ba đều đang tồn tại và cần bọc resilience. Kiểm kê thực
tế cho thấy chỉ có #1–#3 tồn tại; #4 chưa từng tồn tại; #5 bị hoãn có chủ đích sang SCRUM-31. Thiết
kế theo acceptance criteria gốc một cách máy móc sẽ dẫn tới việc xây dựng cả hạ tầng messaging (outbox,
MassTransit, publisher) chỉ để có một cuộc gọi mà bọc resilience — vượt xa 8 story point của SCRUM-30
và trùng phạm vi với SCRUM-31.

**Alternatives considered**:
- *Xây luôn message publisher + outbox trong tính năng này để có đủ ba loại cuộc gọi*: bị loại vì
  trùng phạm vi SCRUM-31 (5 story point riêng), vi phạm ranh giới trách nhiệm giữa hai ticket, và
  không có trong spec's Assumptions (đã ghi rõ thư viện/hạ tầng cụ thể là chi tiết triển khai nằm
  ngoài phạm vi quyết định của đặc tả).
- *Bỏ qua hoàn toàn "service → broker", không nhắc tới*: bị loại vì spec Edge Case 5 yêu cầu chính
  sách phải "vẫn áp dụng" khi cuộc gọi đó xuất hiện — cần để lại một hợp đồng SCRUM-31 dùng được
  ngay, không phải im lặng bỏ qua.

## Decision 2: Thư viện hiện thực — tái sử dụng `Microsoft.Extensions.Http.Resilience` đã có

**Decision**: Dùng đúng gói `Microsoft.Extensions.Http.Resilience` (đã tồn tại trong
`Bff.Api.csproj`) cho mọi HttpClient cần bọc thêm — không đưa gói/thư viện mới nào khác vào.

**Rationale**: Đây là gói cụ thể hiện thực `Microsoft.Extensions.Resilience` mà constitution
Principle VIII nêu tên, và đã được 002-gateway-bff-routing chọn với lý do y hệt (research.md Decision
3 của 002: "a direct, named requirement, not a choice among alternatives"). Dùng lại đúng gói giữ mọi
HttpClient trong hệ thống nhất quán một cơ chế cấu hình, một bộ khái niệm (`AttemptTimeout`,
`TotalRequestTimeout`, `Retry`, `CircuitBreaker`).

**Alternatives considered**:
- *Polly trực tiếp (không qua `Microsoft.Extensions.Http.Resilience`)*: bị loại vì đây là API cấp
  thấp hơn mà chính gói kia bọc lại; dùng trực tiếp sẽ tạo hai cách cấu hình khác nhau trong cùng
  một hệ thống.

## Decision 3: Gateway → BFF — timeout + passive health check, KHÔNG thêm retry

**Decision**: Bật YARP passive health check (`HealthCheck:Passive`) trên cluster `bff-cluster` làm
cơ chế circuit-breaker của gateway. KHÔNG thêm retry ở tầng reverse-proxy.

**Rationale**: Gateway forward nguyên văn mọi method (kể cả `POST /basket/items`,
`POST /checkout`) mà không biết ngữ nghĩa idempotency của route cụ thể — một retry mù ở tầng
transport-agnostic này có nguy cơ nhân đôi chính xác loại side-effect mà spec FR-006 cấm. Quyết định
này nhất quán với Decision 5 (retry chỉ ở tầng biết rõ route). YARP hỗ trợ sẵn passive health check
qua cấu hình (đánh dấu destination "unhealthy" theo tỷ lệ lỗi, tạm ngừng route tới nó, tự phục hồi
sau `ReactivationPeriod`) — đúng ngữ nghĩa "mở mạch, fail fast" của circuit breaker mà không cần thêm
thư viện.

**Alternatives considered**:
- *Thêm Polly resilience handler bọc quanh `HttpForwarder` của YARP*: khả thi về kỹ thuật nhưng phức
  tạp hơn nhiều so với cấu hình passive health check sẵn có của YARP cho đúng nhu cầu (circuit
  breaker, không retry) — vi phạm nguyên tắc "phức tạp phải tương xứng với nhu cầu" ngụ ý ở
  constitution Principle I.
- *Retry chỉ cho GET ở tầng gateway*: bị loại vì gateway không có route table phân loại theo
  business semantics — nó forward theo path catch-all; phân loại "route nào an toàn để retry" đúng
  chỗ hơn ở tầng BFF (nơi đã biết rõ từng downstream call), không phải lặp lại logic đó ở gateway.

## Decision 4: JwtBearer backchannel — HttpClient tường minh với resilience, sửa 1 lần ở 2 nơi dùng chung

**Decision**: Thay vì để `JwtBearerOptions.Backchannel` trống (framework tự tạo `HttpClient` mặc
định với `BackchannelTimeout` = 60 giây), gán một `HttpClient` được tạo qua `IHttpClientFactory` với
`AddStandardResilienceHandler()` riêng (tên `"IdentityBackchannel"`), cấu hình trong
`AddJwtBearer(...)` bằng `services.AddOptions<JwtBearerOptions>(...).Configure<IHttpClientFactory>(...)`
— mẫu hình được ASP.NET Core hỗ trợ chính thức để tiêm một service đã đăng ký (ở đây là
`IHttpClientFactory`) vào bước cấu hình options. Sửa đúng 1 lần ở
`shared/Identity/IdentityValidationExtensions.cs` (bao phủ BFF + 4 domain service) và 1 lần ở
`services/gateway/src/Gateway.Api/Identity/ToggleGatedAuthenticationExtensions.cs` (gateway) — không
sửa lặp lại ở 6 nơi gọi.

**Rationale**: Constitution yêu cầu timeout tường minh cho "mọi cuộc gọi ra ngoài", và việc lấy
JWKS/discovery document từ identity server là một cuộc gọi ra ngoài thật sự (mỗi service độc lập xác
thực token — Principle VI), dù tần suất thấp hơn nhiều so với cuộc gọi nghiệp vụ. `BackchannelTimeout`
mặc định của framework là một giá trị ẩn (implicit), không phải một khai báo tường minh trong cấu
hình của service — không thỏa "explicit" mà cả spec FR-001 lẫn constitution đều nhấn mạnh.

**Alternatives considered**:
- *Chỉ đặt `jwtOptions.BackchannelTimeout = TimeSpan.FromSeconds(N)`, không thêm retry/circuit
  breaker*: thỏa yêu cầu timeout nhưng không thỏa yêu cầu retry/circuit-breaker của Principle VIII;
  bị loại vì `Microsoft.Extensions.Http.Resilience` áp dụng được ở đây với chi phí thấp (chỉ thêm một
  named `HttpClient`), không có lý do kỹ thuật để bỏ qua.
- *Viết một `IConfigurationManager` tùy chỉnh*: quá phức tạp so với nhu cầu — `Backchannel` là điểm
  mở rộng chính thức, không cần thay cả cơ chế quản lý cấu hình OIDC.

## Decision 5: BFF's typed client retry — chỉ retry HTTP method an toàn (GET/HEAD)

**Decision**: Sửa `AddStandardResilienceHandler(resilience => ...)` trong
`DownstreamClientRegistrationExtensions.cs` để `resilience.Retry.ShouldHandle` chỉ coi một lỗi là
"nên retry" khi `HttpMethod` của request là `GET` hoặc `HEAD`, ngoài các điều kiện transient mặc định
(`HttpClientResiliencePredicates.IsTransient`) đã có. `POST /basket/items` và `POST /checkout` — hai
route ghi dữ liệu duy nhất hiện có — tiếp tục có timeout và circuit breaker như cũ, chỉ mất khả năng
tự động retry.

**Rationale**: Đây là khoảng hở đúng nghĩa "bug tiềm ẩn", không phải một tính năng mới: cấu hình hiện
tại đã retry mù mọi method, nghĩa là một request thêm giỏ hàng hoặc checkout có thể được gửi lại sau
khi bản gốc đã tới server thành công nhưng phản hồi bị mất — tạo dòng giỏ hàng trùng hoặc (nghiêm
trọng hơn) đơn hàng trùng. Không có cơ chế idempotency key nào tồn tại trong hệ thống (đã rà soát
toàn bộ `services/*` — không có), nên "chỉ retry method an toàn" là cách khép kín khoảng hở này trong
phạm vi 8 story point, thay vì xây một cơ chế idempotency-key mới xuyên suốt baskets + orders (một
hạng mục lớn hơn nhiều, không được đặt ra trong spec).

**Alternatives considered**:
- *Xây dựng idempotency key (client sinh key, service lưu và trả lại kết quả cũ nếu trùng key)*: giải
  quyết triệt để hơn nhưng đòi hỏi thay đổi data model của cả baskets và orders (migration, cột mới,
  logic kiểm tra trùng) — vượt phạm vi một feature về "timeout/retry/circuit breaker thuần cấu hình";
  ghi nhận như một cải tiến tương lai, không phải một khoảng hở SCRUM-30 phải đóng.
- *Tắt retry hoàn toàn cho cả 4 client (kể cả GET)*: bị loại vì làm giảm giá trị User Story 3 của
  spec (tự phục hồi khỏi lỗi tạm thời) một cách không cần thiết — các route đọc dữ liệu (listing sản
  phẩm, xem giỏ hàng, xem đơn hàng) an toàn để retry.

## Decision 6: Quan sát được sự kiện resilience — thêm nguồn đo lường "Polly" vào ServiceDefaults

**Decision**: Thêm `.AddSource("Polly")` (tracing) và `.AddMeter("Polly")` (metrics) vào
`AddServiceDefaults()` trong `shared/ServiceDefaults/ServiceDefaultsExtensions.cs`, cạnh
`AddHttpClientInstrumentation()` đã có.

**Rationale**: `Microsoft.Extensions.Http.Resilience` (Polly v8) tự phát ra activity/metric riêng cho
từng lần thử, từng lần retry, và từng lần đổi trạng thái circuit breaker qua nguồn tên `"Polly"` —
nhưng `ServiceDefaultsExtensions` hiện chỉ đăng ký `AddAspNetCoreInstrumentation`,
`AddHttpClientInstrumentation`, `AddRuntimeInstrumentation`. Thiếu dòng này nghĩa là timeout/retry/mở
mạch xảy ra nhưng không ai nhìn thấy được trong Elastic — đúng khoảng hở FR-008 mô tả ("phân biệt
được giữa dependency thực sự gặp sự cố và lỗi tạm thời tự phục hồi"). Sửa một lần trong
`ServiceDefaults` áp dụng cho mọi service dùng `AddServiceDefaults()` — không cấu hình riêng lẻ,
đúng tinh thần Principle VII ("observability MUST NOT be configured per service by hand").

**Alternatives considered**:
- *Log thủ công tại `DownstreamCall.ExecuteAsync`*: đã có phần xử lý lỗi ở đó
  (`DownstreamExceptionHandler`), nhưng chỉ bắt được thất bại cuối cùng sau khi resilience pipeline
  đã hết cách — không thấy được các lần retry trung gian hay các lần circuit breaker chuyển trạng
  thái mà không dẫn tới lỗi cuối cùng (ví dụ mở rồi tự đóng lại). Đăng ký nguồn đo lường của chính
  Polly nắm được toàn bộ vòng đời, không chỉ kết quả cuối.

## Decision 7: Rà soát lặp lại được — scanner filesystem-only, danh sách tường minh

**Decision**: Dự án `tests/ResilienceCoverageTests` theo đúng khuôn mẫu `ContractCoverageScanner`
(`tests/ContractCoverageTests/ContractCoverageScanner.cs`): một danh sách viết tay (`record`) liệt kê
từng điểm gọi ra ngoài kỳ vọng, file nguồn/cấu hình nơi resilience của nó được khai báo, và một chuỗi
đánh dấu (marker) cần tìm thấy trong file đó (ví dụ `"AddStandardResilienceHandler"`,
`"HealthCheck:Passive"`, `"AddSource(\"Polly\")"`). Test đọc file dưới dạng văn bản/JSON như
`ForwardingTimeoutBudgetTests` đã làm, không compile ngược lại các project đang bị kiểm tra.

**Rationale**: Danh sách tường minh (không tự khám phá qua reflection hay glob `AddHttpClient(`)
là chủ đích, giống lý do `ContractCoverageScanner` đã ghi lại: một scanner tự khám phá sẽ báo "đủ
coverage" ngay khi một điểm gọi bị xoá nhầm hoặc đổi tên, vì nó không còn thấy điểm đó để kiểm tra
nữa. Thêm một điểm gọi mới bắt buộc phải sửa danh sách này — một quyết định có thể review được trong
PR — thay vì một hệ quả im lặng. Đây chính là cơ chế khép kín Edge Case cuối của spec ("một điểm gọi
ra ngoài mới được thêm vào sau này... không được gắn chính sách resilience").

**Alternatives considered**:
- *Roslyn/reflection quét toàn bộ lệnh gọi `AddHttpClient` trong solution*: mạnh hơn về lý thuyết
  (tự động phát hiện điểm gọi mới) nhưng phức tạp hơn nhiều so với mọi convention test khác trong
  repo, và có nguy cơ false-negative với các cách gọi gián tiếp (qua helper dùng chung) — chính cách
  hệ thống này đang dùng nhiều nhất. Danh sách tường minh đơn giản hơn, nhất quán với
  `ContractCoverageTests` đã được review và chấp nhận trước đó, và lỗi (quên cập nhật danh sách) hiện
  ra ngay ở review PR chứ không phải một lỗ hổng thầm lặng.
