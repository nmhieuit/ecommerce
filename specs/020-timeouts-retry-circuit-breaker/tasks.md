---

description: "Task list template for feature implementation"
---

# Tasks: Timeout, retry và circuit breaker cho mọi cuộc gọi ra ngoài (outbound call)

**Input**: Design documents from `specs/020-timeouts-retry-circuit-breaker/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Constitution Principle III (Test-First, NON-NEGOTIABLE) áp dụng cho tính năng này — các task test dưới đây là BẮT BUỘC, không tùy chọn, và PHẢI được viết trước, xác nhận FAIL, rồi mới triển khai.

**Organization**: Task được nhóm theo user story trong spec.md để mỗi story có thể triển khai và kiểm thử độc lập. 5 khoảng hở đã xác định ở `plan.md` Summary được phân bổ vào story tương ứng: khoảng hở 1 (gateway CB) → US2, khoảng hở 2 (JwtBearer backchannel) → chủ yếu US1 (timeout là thuộc tính nền tảng của cùng một lần đăng ký HttpClient), khoảng hở 3 (retry theo method) → US3, khoảng hở 4 (observability) và phần khung của khoảng hở 5 (scanner) → Foundational vì cả 3 story đều cần.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Có thể chạy song song (khác file, không phụ thuộc task chưa hoàn thành)
- **[Story]**: User story mà task thuộc về (US1, US2, US3)
- Mỗi task đều có đường dẫn file cụ thể

## Path Conventions

Monorepo backend nhiều service hiện có (xem plan.md § Project Structure) — không có service runtime
mới:

- Cấu hình dùng chung xác thực: `shared/Identity/`
- Cấu hình dùng chung observability: `shared/ServiceDefaults/`
- Gateway: `services/gateway/src/Gateway.Api/`, test tại `services/gateway/tests/`
- BFF: `services/bff/src/Bff.Api/DownstreamClients/`, test tại `services/bff/tests/`
- Test quét mới: `tests/ResilienceCoverageTests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Chuẩn bị package reference và khung dự án test cần thiết cho toàn bộ tính năng

- [X] T001 [P] Thêm `<PackageReference Include="Microsoft.Extensions.Http.Resilience" />` vào
      `services/gateway/src/Gateway.Api/Gateway.Api.csproj` (phiên bản đã pin sẵn ở
      `Directory.Packages.props`, không cần khai báo version ở đây)
- [X] T002 [P] Thêm `<PackageReference Include="Microsoft.Extensions.Http.Resilience" />` vào
      `shared/Identity/Identity.csproj`
- [X] T003 [P] Tạo dự án xUnit `tests/ResilienceCoverageTests/ResilienceCoverageTests.csproj` theo
      đúng khuôn mẫu `tests/ContractCoverageTests/ContractCoverageTests.csproj`, đăng ký project này
      trong `Ecommerce.slnx`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Hạ tầng dùng chung mà CẢ 3 user story đều cần trước khi có thể triển khai hoặc kiểm thử
đầy đủ

**⚠️ CRITICAL**: Không user story nào được coi là hoàn tất trước khi phase này xong, dù có thể bắt
đầu code song song

- [X] T004 [P] Cài đặt khung `tests/ResilienceCoverageTests/ResilienceCoverageScanner.cs` theo đúng
      khuôn mẫu `tests/ContractCoverageTests/ContractCoverageScanner.cs`: record `OutboundCallSite`
      (Name, Caller, Callee, ConfigurationFile, RequiredMarkers), record `CoverageViolation`, record
      `CoverageScanResult`, hàm `LocateRepositoryRoot()`, và hàm `Scan(repositoryRoot, expected)` đọc
      file dưới dạng text và xác nhận từng marker trong `RequiredMarkers` xuất hiện trong
      `ConfigurationFile` — `ExpectedCallSites` khởi tạo RỖNG (populate dần theo từng story ở dưới)
      — phụ thuộc T003
- [X] T005 [P] Viết `tests/ResilienceCoverageTests/ResilienceCoverageTests.cs`:
      `Scan_ReportsNoViolations_ForCurrentInventory` chạy `ResilienceCoverageScanner.Scan` với
      `ExpectedCallSites` thật (rỗng ở bước này nên PASS vô nghĩa — sẽ có ý nghĩa khi US1-3 populate
      danh sách) và `Scan_DetectsViolation_WhenMarkerMissing` dùng một `OutboundCallSite` giả lập trỏ
      tới file không có marker, xác nhận scanner tự nó phát hiện đúng gap (test cho chính scanner,
      theo khuôn mẫu `ContractCoverageTests` đã có) — phụ thuộc T004
- [X] T006 [P] Thêm `.AddSource("Polly")` vào khối `WithTracing` và `.AddMeter("Polly")` vào khối
      `WithMetrics` trong `AddServiceDefaults()`,
      `shared/ServiceDefaults/ServiceDefaultsExtensions.cs` (research.md Quyết định 6, spec FR-008) —
      cạnh `AddHttpClientInstrumentation()` đã có

**Checkpoint**: Nền tảng sẵn sàng — scanner có khung (danh sách rỗng), observability dùng chung đã
lên OTel cho mọi service. Có thể bắt đầu triển khai từng user story.

---

## Phase 3: User Story 1 - Mọi cuộc gọi ra ngoài đều có timeout tường minh (Priority: P1) 🎯 MVP

**Goal**: Cả 6 điểm gọi JwtBearer backchannel (gateway + 5 service khác) có timeout tường minh thay
vì dựa vào giá trị mặc định ẩn của framework; kiểm kê xác nhận không còn điểm gọi nào (trong số đã
biết) thiếu timeout.

**Independent Test**: Chạy `tests/ResilienceCoverageTests` với danh sách đã bao gồm cả 3 loại điểm
gọi ở research.md Decision 1 (#1, #2, #3) và xác nhận không còn violation về marker liên quan tới
timeout; đọc trực tiếp `shared/Identity/IdentityValidationExtensions.cs` và
`ToggleGatedAuthenticationExtensions.cs`, xác nhận `Backchannel` không còn là giá trị mặc định.

### Tests for User Story 1 ⚠️

> **Viết các test này TRƯỚC, xác nhận chúng FAIL trước khi triển khai (Backchannel hiện vẫn là mặc
> định của framework)**

- [X] T007 [P] [US1] Bổ sung 3 dòng vào `ExpectedCallSites` của
      `tests/ResilienceCoverageTests/ResilienceCoverageScanner.cs` (T004): `bff-cluster`
      (gateway→bff, marker `"ActivityTimeout"`), `IdentityBackchannel (gateway)` (marker
      `"AddStandardResilienceHandler"` trong `ToggleGatedAuthenticationExtensions.cs`),
      `IdentityBackchannel (shared)` (marker `"AddStandardResilienceHandler"` trong
      `IdentityValidationExtensions.cs`) — chạy `dotnet test tests/ResilienceCoverageTests`, xác nhận
      2 dòng backchannel FAIL (marker chưa tồn tại), dòng `bff-cluster` PASS (đã có sẵn từ 002) —
      phụ thuộc T004
- [X] T008 [P] [US1] Viết `services/bff/tests/Bff.Api.UnitTests/IdentityBackchannelResilienceTests.cs`
      và `services/gateway/tests/Gateway.Api.UnitTests/IdentityBackchannelResilienceTests.cs` — kiểm
      tra bằng cách build `IServiceCollection` qua `AddIdentityValidation`/`AddToggleGatedIdentity`
      rồi so sánh `IOptionsMonitor<HttpClientFactoryOptions>.Get("IdentityBackchannel")` với một tên
      client chưa từng đăng ký (không dùng `options.Backchannel != null` — property này khác `null`
      ngay cả ở hành vi mặc định của framework, nên không phân biệt được "đã cấu hình" hay chưa) — đã
      xác nhận FAIL trước khi có T009/T010

### Implementation for User Story 1

- [X] T009 [US1] Trong `shared/Identity/`: tạo `IdentityBackchannelResilience.cs` với phương thức dùng
      chung `AddIdentityBackchannelResilience(this IServiceCollection, string authenticationScheme)`
      — đăng ký `services.AddHttpClient("IdentityBackchannel").AddStandardResilienceHandler(...)` với
      timeout phù hợp tần suất gọi thấp của OIDC discovery/JWKS (5 s/15 s, tách biệt khỏi ngân sách
      nghiệp vụ ở `DownstreamClientRegistrationExtensions.cs`), rồi gán `jwtOptions.Backchannel` bằng
      client này qua `services.AddOptions<JwtBearerOptions>(scheme).Configure<IHttpClientFactory>(...)`
      — gọi từ `IdentityValidationExtensions.AddIdentityValidation` (thay vì lặp lại logic, để tham số
      timeout chỉ tồn tại ở một chỗ cho cả 2 điểm gọi T009/T010) — phụ thuộc T002, T007, T008
- [X] T010 [US1] Gọi `services.AddIdentityBackchannelResilience(JwtBearerDefaults.AuthenticationScheme)`
      trong `services/gateway/src/Gateway.Api/Identity/ToggleGatedAuthenticationExtensions.cs` (tái sử
      dụng helper dùng chung ở T009 thay vì lặp lại cấu hình) — phụ thuộc T001, T007, T008
- [X] T011 [US1] Chạy lại T007, T008, xác nhận PASS (Green) — phụ thuộc T009, T010
- [X] T012 [US1] Chạy lại toàn bộ suite hiện có của gateway và của 5 service dùng
      `AddIdentityValidation`/`AddToggleGatedIdentity` — phụ thuộc T011.
      **Kết quả thực tế**: `Gateway.Api.IntegrationTests` có 12/34 test FAIL cả TRƯỚC lẫn SAU thay đổi
      của US1 (đã xác nhận bằng `git stash -u` để chạy lại đúng 4 test của `UnmatchedRouteTests` trên
      mã nguồn `master` gốc — cùng lỗi `Expected: NotFound/BadGateway, Actual: Unauthorized` xảy ra y
      hệt, không liên quan tới thay đổi `Backchannel`) — đây là lỗi có sẵn của môi trường/phiên này
      (nghi ngờ liên quan tới cách `TestJwtBearer`/symmetric-key test token được cấp trong sandbox),
      KHÔNG PHẢI hồi quy do US1 gây ra, và nằm ngoài phạm vi SCRUM-30 để sửa. `Bff.Api.IntegrationTests`
      và build toàn solution xác nhận không có lỗi biên dịch/hồi quy mới do đổi `Backchannel`.

**Checkpoint**: Tại đây, User Story 1 hoạt động độc lập và kiểm thử được — timeout tường minh đã phủ
đủ cả 3 loại điểm gọi đang tồn tại. Đây là MVP.

---

## Phase 4: User Story 2 - Circuit breaker chặn cuộc gọi dồn ứ khi dependency chậm hoặc gián đoạn (Priority: P1)

**Goal**: Gateway tự động ngừng forward request mới tới BFF khi BFF liên tục lỗi/timeout, fail-fast
thay vì chờ hết `ActivityTimeout` mỗi lần; JwtBearer backchannel (đã có HttpClient resilience từ US1)
cũng có circuit breaker hoạt động đúng.

**Independent Test**: Giả lập BFF ngừng phản hồi, dồn dập gửi request qua gateway, xác nhận sau
ngưỡng lỗi đã cấu hình, request tiếp theo thất bại nhanh thay vì chờ hết `ActivityTimeout` (tương tự
`DownstreamUnavailableTests` đã có ở BFF cho chặng BFF→service, áp dụng cho chặng gateway→BFF).

### Tests for User Story 2 ⚠️

> **Viết các test này TRƯỚC, xác nhận chúng FAIL trước khi triển khai (chưa bật passive health check)**

- [X] T013 [P] [US2] Viết
      `services/gateway/tests/Gateway.Api.UnitTests/PassiveHealthCheckConfigurationTests.cs`
      (theo đúng khuôn mẫu đọc file JSON của `ForwardingTimeoutBudgetTests.cs`): xác nhận
      `ReverseProxy:Clusters:bff-cluster` khai báo `HealthCheck:Passive:Enabled = true` với một
      `Policy` và ngưỡng hợp lệ (không rỗng, không vô hiệu hoá) — đã xác nhận FAIL trước T016, PASS
      sau (3/3)
- [X] T014 [P] [US2] Viết
      `services/gateway/tests/Gateway.Api.IntegrationTests/PassiveHealthCheckCircuitBreakerTests.cs`:
      `AfterRepeatedFailures_TheCircuitOpens_AndSubsequentRequestsFailFast_WithoutAttemptingAConnection`
      — dựng gateway với BFF không thể tới được, ép `FeatureToggles` về false (môi trường
      `appsettings.Development.json` mặc định bật cutover cho hand-testing — xem ghi chú), giảm
      `ReverseProxy:TransportFailureRateHealthPolicy:MinimalTotalCountThreshold` xuống 2 qua cấu hình
      test, gửi đủ request lỗi rồi xác nhận request tiếp theo trả `503 ServiceUnavailable` (không
      còn destination khỏe mạnh) thay vì `502 BadGateway` — đã xác nhận FAIL trước T016
- [X] T015 [P] [US2] Cập nhật `ExpectedCallSites` của `ResilienceCoverageScanner.cs`: dòng
      `bff-cluster` bổ sung marker `"HealthCheck"` và `"Passive"` (ngoài `"ActivityTimeout"` đã có từ
      T007) — đã xác nhận PASS ngay (T016 làm trước khi kịp chạy lại ở trạng thái đỏ do cả 2 việc
      thực hiện gần nhau; hành vi đỏ→xanh đã được chứng minh qua T013)

### Implementation for User Story 2

- [X] T016 [US2] Trong `services/gateway/src/Gateway.Api/appsettings.json`: thêm khối
      `HealthCheck.Passive` (Enabled, Policy="TransportFailureRate", ReactivationPeriod) VÀ
      `HealthCheck.AvailableDestinationsPolicy="HealthyAndUnknown"` cho cluster `bff-cluster`
      (research.md Quyết định 3); trong `Program.cs`, đổi `app.MapReverseProxy()` thành overload có
      cấu hình pipeline, gọi `proxyPipeline.UsePassiveHealthChecks()` +
      `proxyPipeline.UseLoadBalancing()` (middleware bắt buộc để passive health check thực sự chạy —
      `MapReverseProxy()` không tham số không tự thêm), và
      `services.Configure<TransportFailureRateHealthPolicyOptions>(...)` để test override được
      `MinimalTotalCountThreshold` — phụ thuộc T001, T013, T014, T015.
      **Phát hiện quan trọng qua xác thực thủ công** (chạy gateway thật + `curl` lặp lại, xem log
      debug của YARP): `AvailableDestinationsPolicy` mặc định của YARP là `HealthyOrPanic`, tự động
      coi MỌI destination là "available" khi không còn destination nào khỏe mạnh — với cluster chỉ có
      1 destination (`bff`), điều này khiến circuit breaker "mở" nhưng gateway vẫn tiếp tục thử kết
      nối thật (vẫn 502, không fail-fast). Phải đặt tường minh `AvailableDestinationsPolicy:
      "HealthyAndUnknown"` để có hành vi fail-fast thật (503, ~20ms) đúng như spec yêu cầu — đã thêm
      test riêng (`BffCluster_UsesAvailableDestinationsPolicyThatActuallyExcludesUnhealthyDestinations`)
      và marker `"AvailableDestinationsPolicy"` vào `ResilienceCoverageScanner`.
- [X] T017 [US2] Chạy lại T013 (4/4), T014 (1/1), T015/ResilienceCoverageTests (5/5) — tất cả PASS —
      phụ thuộc T016
- [X] T018 [US2] Chạy lại `ForwardingTimeoutBudgetTests` (đã có từ 002), xác nhận PASS (2/2) — thay
      đổi ở T016 không phá vỡ ràng buộc `ActivityTimeout ≥` tổng ngân sách BFF — phụ thuộc T017

**Checkpoint**: User Story 1 VÀ 2 đều hoạt động độc lập — timeout tường minh và circuit breaker đã
phủ đủ cả 3 loại điểm gọi đang tồn tại (gateway→BFF, BFF→4 service đã có sẵn từ 002, JwtBearer
backchannel).

---

## Phase 5: User Story 3 - Retry có backoff cho lỗi tạm thời (Priority: P2)

**Goal**: `POST /basket/items` và `POST /checkout` không còn bị retry mù khi downstream lỗi tạm thời
(khoảng hở 3, tương ứng trực tiếp spec FR-006/Edge Case 1); các route đọc dữ liệu (`GET`) của cả 4
downstream client vẫn giữ nguyên khả năng tự động retry với backoff.

**Independent Test**: Giả lập downstream lỗi tạm thời 1-2 lần rồi phục hồi; gửi `POST /basket/items`
qua BFF và xác nhận downstream CHỈ nhận đúng 1 request (không retry); gửi `GET` tương tự và xác nhận
downstream nhận nhiều request (có retry, đúng số `MaxRetryAttempts` đã cấu hình).

### Tests for User Story 3 ⚠️

> **Viết các test này TRƯỚC, xác nhận chúng FAIL trước khi triển khai (retry hiện tại áp dụng cho mọi
> method)**

- [X] T019 [P] [US3] Viết `services/bff/tests/Bff.Api.UnitTests/RetryMethodPolicyTests.cs` — thay vì
      đi qua toàn bộ route HTTP của BFF (cả `/basket/items` lẫn `/checkout` đều cần một cuộc gọi GET
      thành công ở bước trước đó mới tới được cuộc gọi ghi dữ liệu, làm test phức tạp không cần
      thiết), test dựng `IServiceCollection` qua `AddDownstreamClients` thật, thay primary handler
      của `BasketsApi` bằng một handler đếm số lần gọi rồi luôn ném `HttpRequestException`, và gọi
      trực tiếp `BasketsApiClient.GetCurrentBasketAsync` (GET) so với `BasketsApiClient.AddItemAsync`
      (POST) — cùng client, cùng pipeline, chỉ khác method. Đã xác nhận: GET PASS ngay (3 lần gọi,
      đã retry đúng `MaxRetryAttempts`), POST FAIL trước T021 (3 lần gọi thay vì kỳ vọng 1)
- [X] T020 [P] [US3] Cập nhật `ExpectedCallSites` của `ResilienceCoverageScanner.cs`: 2 dòng
      `BasketsApi`, `OrdersApi` bổ sung marker `"SafeToRetryMethods"` — đã xác nhận FAIL trước T021

### Implementation for User Story 3

- [X] T021 [US3] Trong `services/bff/src/Bff.Api/DownstreamClients/DownstreamClientRegistrationExtensions.cs`:
      sửa `resilience.Retry.ShouldHandle` để chỉ coi là "nên retry" khi
      `HttpClientResiliencePredicates.IsTransient(args.Outcome)` ĐÚNG **và** method của request đang
      thử nằm trong `SafeToRetryMethods` (`GET`, `HEAD`). Method đọc qua
      `args.Context.GetRequestMessage()?.Method` (từ `Polly.HttpResilienceContextExtensions`) chứ
      KHÔNG phải `args.Outcome.Result?.RequestMessage` như dự kiến ban đầu ở research.md — một lỗi
      transport (connection refused, không có `HttpResponseMessage` nào) khiến `Outcome.Result` là
      `null`, nên phải đọc request gốc từ `ResilienceContext` thay vì từ outcome. Áp dụng đồng nhất
      cho cả 4 client — phụ thuộc T009
- [X] T022 [US3] Chạy lại T019 (2/2 PASS), T020/ResilienceCoverageTests (5/5 PASS) — phụ thuộc T021
- [X] T023 [US3] Chạy lại `DownstreamUnavailableTests` hiện có của BFF (US2/002) để xác nhận thu hẹp
      retry ở T021 không phá vỡ hành vi circuit breaker đã kiểm chứng — phụ thuộc T022

**Checkpoint**: Cả 3 user story đều hoạt động độc lập. Toàn bộ 5 khoảng hở ở `plan.md` Summary đã
được khép kín.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Xác thực toàn diện cuối cùng, không thuộc riêng một user story nào

- [X] T024 [P] Cập nhật `specs/020-timeouts-retry-circuit-breaker/contracts/outbound-call-inventory-contract.md`
      với tên marker chính xác đã chốt ở T009, T010, T016, T021 — phụ thuộc T011, T017, T022. Cũng
      cập nhật `quickstart.md` Bước 2 và Bước 4 để khớp tên file/filter test thật (khác kế hoạch ban
      đầu: `RetryMethodPolicyTests` nằm ở `Bff.Api.UnitTests` không phải `IntegrationTests`; circuit
      breaker động của gateway là file mới `PassiveHealthCheckCircuitBreakerTests.cs`, không phải mở
      rộng `DownstreamUnavailableTests.cs`)
- [~] T025 [P] Thực hiện [quickstart.md](./quickstart.md) Bước 1–6 — phụ thuộc T012, T018, T023.
      Bước 1-5 đã chạy PASS thật (ResilienceCoverageTests 5/5, RetryMethodPolicyTests 2/2,
      DownstreamUnavailableTests của BFF 8/8, PassiveHealthCheckConfigurationTests 4/4 +
      PassiveHealthCheckCircuitBreakerTests 1/1, test timeout hiện có 1/1). Bước 6 (quan sát sự kiện
      Polly qua OTel Collector/Elastic thật) KHÔNG thực hiện được trong phiên này — không có OTel
      Collector chạy sẵn trong sandbox; đã xác nhận ở mức cấu hình (build sạch, `AddSource("Polly")`/
      `AddMeter("Polly")` có trong `ServiceDefaultsExtensions.cs`), chưa có bằng chứng runtime thật
      qua Elastic.
- [X] T026 Build sạch toàn `Ecommerce.slnx` (0 lỗi/cảnh báo), cộng
      `shared/Identity.UnitTests` (15/15), `shared/ServiceDefaults.UnitTests` (13/13),
      `Orders.Api.UnitTests` (14/14), `Baskets.Api.UnitTests` (14/14) — phụ thuộc T025.
      **Phát hiện không liên quan tới tính năng**: `Parties.Api.UnitTests`/`Products.Api.UnitTests`
      chỉ có đúng 1 test (`HealthCheckTests.HealthLive_ReturnsOk`) và nó FAIL vì thiếu secret cục bộ
      `ConnectionStrings:PartiesDb`/`ProductsDb` trong sandbox này
      (`OptionsValidationException` từ `018-cluster-secret-store`) — không liên quan tới
      Identity/ServiceDefaults đã sửa, xác nhận bằng cách loại trừ đúng test đó thì phần còn lại của
      từng service PASS sạch. Ngoài phạm vi SCRUM-30 để sửa (thiếu cấu hình môi trường cục bộ, không
      phải lỗi mã nguồn).
- [X] T027 [P] Rà soát lại spec.md Assumptions và research.md Decision 1: grep xác nhận
      `BasketCheckedOutMapper.ToEvent` chỉ được gọi từ
      `Baskets.Api.ContractTests/BasketCheckedOutProviderPactTests.cs` (test hợp đồng đã có), không
      có nơi nào trong mã nguồn production gọi nó; grep `AddMassTransit|IPublishEndpoint|IBus` trên
      toàn `services/` không có kết quả nào — SCRUM-31 xác nhận chưa bắt đầu, "service → broker" vẫn
      đúng là ngoài phạm vi tại thời điểm tính năng này hoàn tất.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Không phụ thuộc — bắt đầu ngay
- **Foundational (Phase 2)**: Phụ thuộc hoàn tất Setup — CHẶN việc coi bất kỳ user story nào là hoàn
  tất (scanner cần có khung trước khi mỗi story populate danh sách của mình)
- **User Stories (Phase 3-5)**: Đều phụ thuộc hoàn tất Foundational
  - US1 (P1), US2 (P1), US3 (P2) chạm vào các file gần như tách biệt hoàn toàn (US1:
    `shared/Identity` + gateway Identity; US2: gateway `appsettings.json` + `Program.cs`; US3: BFF
    `DownstreamClientRegistrationExtensions.cs`) — độc lập thật sự, có thể triển khai song song bởi
    nhiều người, chỉ giao nhau ở `ResilienceCoverageScanner.cs` (mỗi story thêm dòng riêng, xung đột
    merge nhỏ nếu làm đồng thời)
  - Thứ tự ưu tiên đề xuất theo P1 → P1 → P2: US1 trước (nền tảng, chạm nhiều service nhất), US2 kế
    tiếp (cùng P1, độc lập với US1), US3 sau cùng (P2, sửa file đã ổn định lâu nhất — ít rủi ro xung
    đột khi US1 cũng có thể chạm `DownstreamClientRegistrationExtensions.cs`... thực tế US1 KHÔNG
    chạm file này, chỉ US3 chạm — xác nhận không xung đột)
- **Polish (Phase 6)**: Phụ thuộc cả 3 user story hoàn tất

### Within Each User Story

- Test viết trước, xác nhận FAIL trước khi triển khai (Constitution Principle III, NON-NEGOTIABLE)
- Cấu hình/registration trước khi chạy lại test xác nhận Green
- Story hoàn tất (test chuyển Green, không hồi quy) trước khi coi là sẵn sàng tích hợp

### Parallel Opportunities

- T001, T002, T003 (Setup) chạy song song — khác file
- T004, T006 (Foundational) chạy song song — khác file/mối quan tâm; T005 phụ thuộc T004
- T007, T008 (test US1) chạy song song — khác file
- T013, T014, T015 (test US2) chạy song song — khác file
- T019, T020 (test US3) chạy song song — khác file
- US1, US2, US3 có thể triển khai song song bởi 3 người khác nhau sau khi Phase 2 xong (xem ghi chú
  Dependencies ở trên)

---

## Parallel Example: User Story 1

```bash
# Chạy song song các test của User Story 1 (khác file):
Task: "Bổ sung ExpectedCallSites trong tests/ResilienceCoverageTests/ResilienceCoverageScanner.cs"
Task: "Viết IdentityBackchannelResilienceTests.cs kiểm tra Backchannel không còn mặc định"

# Sau khi cả hai FAIL đúng như kỳ vọng, triển khai song song 2 điểm cấu hình dùng chung (khác file):
Task: "Đăng ký IdentityBackchannel resilience trong shared/Identity/IdentityValidationExtensions.cs"
Task: "Đăng ký IdentityBackchannel resilience trong ToggleGatedAuthenticationExtensions.cs của gateway"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Hoàn tất Phase 1: Setup
2. Hoàn tất Phase 2: Foundational (CHẶN mọi story)
3. Hoàn tất Phase 3: User Story 1 — timeout tường minh phủ đủ mọi điểm gọi đang tồn tại
4. **DỪNG và XÁC THỰC**: chạy T007, T008, T012 độc lập, xác nhận Green và không hồi quy xác thực
5. Đây là MVP có thể trình diễn — khoảng hở nghiêm trọng nhất (timeout ẩn ở backchannel, ảnh hưởng
   toàn bộ 6 service) đã được khép kín, dù circuit breaker gateway (US2) và an toàn retry (US3) chưa
   xong

### Incremental Delivery

1. Setup + Foundational → nền tảng sẵn sàng (scanner có khung, observability lên OTel)
2. + User Story 1 → kiểm thử độc lập → MVP (timeout tường minh khắp nơi)
3. + User Story 2 → kiểm thử độc lập (gateway fail-fast khi BFF gặp sự cố)
4. + User Story 3 → kiểm thử độc lập (retry an toàn, không nhân đôi giỏ hàng/đơn hàng)
5. Mỗi story bổ sung giá trị mà không phá vỡ story trước — xác nhận bằng cách chạy lại toàn bộ test
   của story trước đó ở mỗi checkpoint (T012, T018, T023)

---

## Notes

- [P] = khác file, không phụ thuộc task chưa hoàn thành
- Nhãn [Story] gắn task với đúng user story để truy vết
- Test PHẢI được viết trước và xác nhận FAIL trước khi triển khai (Constitution Principle III,
  NON-NEGOTIABLE — không tùy chọn cho tính năng này)
- "service → broker" (research.md Decision 1, #5) không có task nào trong file này — ngoài phạm vi,
  chỉ có T027 xác nhận giả định đó vẫn đúng tại thời điểm hoàn tất
- Commit sau mỗi task hoặc mỗi nhóm task liên quan
- Dừng ở mỗi checkpoint để xác thực từng story độc lập trước khi tiếp tục
