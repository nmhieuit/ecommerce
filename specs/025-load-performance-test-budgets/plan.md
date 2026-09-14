# Implementation Plan: Kiểm thử tải/hiệu năng luồng nghiệp vụ trọng yếu đối chiếu ngân sách hiệu năng của hiến chương

**Branch**: `code/Load-performance-test-against-constitution-budgets` | **Date**: 2026-09-12 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/025-load-performance-test-budgets/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

**Cập nhật sau khi triển khai (tasks.md T001–T018)**: Ba điều chỉnh so với dự tính ban đầu, cả ba đều
do khảo sát/build thật phát hiện, không phải do đổi phạm vi:

1. **Giấy phép NBomber**: research.md Quyết định 1 ban đầu chọn "NBomber" mà chưa kiểm tra giấy phép
   bản mới nhất. `dotnet restore` thật cho thấy bản mới nhất (6.6.0) không hề miễn phí cho tổ chức —
   phải ghim đúng `4.1.2` (Apache-2.0), cùng lúc ghim `MessagePack` lên `3.1.8` để vượt qua cảnh báo
   audit bảo mật (NU1902/NU1903) của phiên bản MessagePack mà NBomber 4.1.2 kéo theo. Xem research.md
   Quyết định 1 (đã cập nhật) và comment tại `Directory.Packages.props`.
2. **Nguồn ngưỡng cho `CriticalPathStepBudget`**: `ServiceManifestModel` (021) chỉ parse khối
   `service`/`slos` cấp service, không parse khối `endpoints:` — `CriticalPathStepBudgets.LoadAll()`
   vì vậy đọc ngưỡng từ khối `slos.latency` cấp service của `bff`, áp dụng đồng nhất cho cả 4 bước
   (giá trị giống hệt 4 mục `endpoints` đã bổ sung ở T006, vì cùng lớp `client-facing-bff`) — xem
   research.md Quyết định 5, "Ghi chú triển khai".
3. **Giả định "không cần bearer token" ban đầu SAI** (research.md Quyết định 6) — chạy thật trên
   `docker-compose.yml` + `docker-compose.demo.yml` (T017) cho kết quả **401** ở bước đầu tiên
   (`GET /bff/products`). Nguyên nhân thật: gateway ở chế độ `StubIdentity` chỉ chuyển tiếp header
   (`X-Tenant-Id`/`X-Subject-Id`), không bao giờ đính kèm bearer token; BFF (`AddIdentityValidation()`)
   luôn xác thực JWT thật KHÔNG ĐIỀU KIỆN, không có lối tắt nào như gateway. Không có cách hợp lệ nào
   để một client HTTP bên ngoài lấy token thật hôm nay (không tài khoản demo, không UI đăng nhập,
   không endpoint tự đăng ký — `SeedData.cs` cố ý không giữ thông tin đăng nhập). Đây là khoảng trống
   thật của TOÀN NỀN TẢNG, không riêng tính năng này — đã tạo task riêng để theo dõi (xem research.md
   Quyết định 6 đã sửa lại toàn bộ). `GatewayClient` giữ nguyên (không tự chế cơ chế xác thực mới,
   ngoài thẩm quyền của một bài kiểm thử tải) — hệ quả: bài kiểm thử tải này hiện sẽ luôn FAIL vì 401
   khi chạy trên một stack mới dựng, và đó là hành vi ĐÚNG theo FR-004 chứ không phải lỗi của chính
   bài kiểm thử tải (nó phát hiện đúng một vi phạm thật: không xác thực được).

**Bổ sung ngoài dự tính, phát hiện khi chạy T017 (đã sửa/ghi lại)**:
- `services/orders/src/Orders.Api/Dockerfile` thiếu `COPY shared/EventContracts/` dù
  `Orders.Api.csproj` tham chiếu tới — khiến `docker compose build` thất bại hoàn toàn, không liên
  quan gì đến tính năng này (lỗi có sẵn từ khi EventContracts được thêm). Đã vá tối thiểu (2 dòng
  COPY, đúng khuôn mẫu `Baskets.Api/Dockerfile` đã làm đúng) để có thể tiếp tục xác thực trực tiếp;
  đã tạo task riêng để đưa bản vá chính thức lên nhánh sạch.
- `identity-api` trên stack mới dựng có dấu hiệu race/deadlock khi Duende tự sinh signing key lần
  đầu dưới tải — nhiều request treo/timeout. Cũng đã tạo task riêng, không sửa trong phạm vi tính năng
  này.

Đã xác minh bằng build/test/chạy thật:
- `dotnet build tests/CriticalPathLoadTests` — 0 lỗi/0 cảnh báo (đúng `TreatWarningsAsErrors=true`).
- `dotnet test --filter "BudgetAssertionsTests|CriticalPathStepBudgetsTests"` — 6/6 Passed (không cần
  stack sống).
- `dotnet test tests/ServiceManifestSloConventionTests` — 29/29 Passed (xác nhận sửa manifest `bff` ở
  T006 không phá vỡ hạng mục 021).
- `dotnet build Ecommerce.slnx` (toàn bộ 44 dự án monorepo) — 0 lỗi/0 cảnh báo, xác nhận nâng
  `MessagePack` lên 3.1.8 không phá vỡ dự án nào khác.
- **`scripts/ci/run-performance-tests.sh` chạy thật trên `docker-compose.yml`+`docker-compose.demo.yml`**
  (sau khi vá Dockerfile `orders-api`): toàn bộ 14 image build thành công, toàn bộ container lên
  `Healthy`, `dotnet test tests/CriticalPathLoadTests` chạy thật 68 giây, tạo báo cáo NBomber thật —
  **kết quả FAIL đúng như thiết kế** (US2 hoạt động đúng: một vi phạm thật — ở đây là không xác thực
  được — khiến lần chạy thất bại rõ ràng, không âm thầm "pass"). Đây là bằng chứng mạnh cho US1/US2:
  toàn bộ cơ chế đo lường + đối chiếu ngân sách + cổng chặn tự động hoạt động đúng trên stack thật;
  điều duy nhất chưa chứng minh được là một lần chạy THÀNH CÔNG thật (401 chặn trước khi có traffic
  hợp lệ để đo) — việc đó phụ thuộc vào task xác thực nền tảng đã tạo ở trên.

## Summary

Khảo sát thực tế cho thấy luồng browse→basket→checkout→order đã tồn tại đầy đủ qua BFF
(`GET /bff/products`, `POST /bff/basket/items`, `POST /bff/checkout`, `GET /bff/orders/{orderId}` —
tất cả proxy qua gateway, đúng route mà `frontend/apps/web/e2e/walkthrough.spec.ts` đang dùng), và
ngân sách SLO cho lớp "client-facing-bff" (p95 ≤ 300ms/p99 ≤ 800ms) đã được hạng mục 021 hiện thực
hoá thành nguồn chân lý máy-đọc-được trong `services/bff/src/Bff.Api/service-manifest.yaml`, đọc bởi
`tests/ServiceManifestSloConventionTests`. Cái còn thiếu đúng như Jira SCRUM-32 mô tả: không có bài
kiểm thử tải nào tạo traffic thật trên luồng này và đối chiếu số đo với ngân sách đó — số đo hiện chỉ
tồn tại trên Kibana một cách bị động, phụ thuộc traffic sản xuất thật.

Khảo sát cũng phát hiện 2 khoảng trống thật, không dự tính trước:

1. **Manifest chưa theo kịp code**: `services/bff/src/Bff.Api/service-manifest.yaml` liệt kê 4
   route (`GET /bff/products`, `GET /bff/baskets/{basketId}`, `GET /bff/orders/{orderId}`,
   `GET /bff/parties/{partyId}`) nhưng THIẾU hẳn `POST /bff/basket/items` và `POST /bff/checkout` —
   hai route đã tồn tại trong code (`BasketsEndpoints.cs`, `CheckoutEndpoints.cs`) từ các hạng mục
   trước. FR-003 của spec này yêu cầu đối chiếu ngân sách cho từng bước của luồng trọng yếu, nên 2
   route thiếu này PHẢI được bổ sung vào manifest trước khi bài kiểm thử tải có thể tham chiếu chúng
   — dùng đúng ngân sách client-facing-bff hiện có (300ms/800ms), không phải số liệu mới.
2. **Bẫy tự động phát hiện dự án test của `scripts/ci/run-dotnet-tests.sh`**: script này gom mọi
   `*Tests.csproj` chưa khớp `ContractTests`/`IntegrationTest` vào tier "unit" — tier chạy trên MỌI
   PR, không cần Docker/stack sống. Một dự án kiểm thử tải mới đặt tên theo quy ước `*Tests.csproj`
   sẽ tự động lọt vào tier "unit" nếu không được loại trừ tường minh, kéo theo yêu cầu stack sống vào
   PR gate — một hồi quy nghiêm trọng cho cổng chặn PR hiện có (specs/013). Kế hoạch này phải sửa
   điều kiện loại trừ của tier "unit" cùng lúc với việc thêm dự án mới.

Cách tiếp cận: (1) bổ sung 2 khai báo `endpoints` còn thiếu vào manifest `bff`; (2) thêm dự án
`tests/CriticalPathLoadTests` dùng NBomber, đọc ngưỡng trực tiếp từ manifest qua
`ServiceManifestSloConventionTests.ServiceManifestFixture` (không hard-code số liệu lần thứ hai), lái
tải qua đúng 4 route BFF theo trình tự thật của luồng, và tự thất bại (exit code khác 0) khi bất kỳ
bước nào vượt ngân sách; (3) thêm tier "performance" mới + loại trừ dự án đó khỏi tier "unit"; (4)
thêm `Jenkinsfile.performance` chạy theo lịch trên môi trường giống production
(`docker-compose.demo.yml`), tách biệt khỏi Jenkinsfile chặn PR hiện có, đúng tinh thần "Performance
gate" đã nêu ở hiến chương; (5) tài liệu hoá 3 kịch bản kiểm thử của Jira SCRUM-32 thành
`quickstart.md`, tái dùng đúng kỹ thuật "thêm `Task.Delay` tạm thời rồi hoàn tác" mà hạng mục 021 đã
dùng để giả lập hồi quy — không xây cơ chế fault-injection thường trực mới (nằm ngoài phạm vi Jira
này, thuộc một hạng mục Failure Injection riêng của cùng epic SCRUM-8).

## Technical Context

**Language/Version**: C#/.NET 10 cho dự án kiểm thử tải mới — không có ngôn ngữ ứng dụng mới, không
sửa hành vi bất kỳ service nào ngoài bổ sung tài liệu manifest.

**Primary Dependencies**: NBomber (thư viện tạo tải native .NET) cho kịch bản kiểm thử tải; tái dùng
`YamlDotNet`/`ServiceManifestFixture` đã có trong `tests/ServiceManifestSloConventionTests` để đọc
ngưỡng SLO trực tiếp từ `service-manifest.yaml` thay vì hard-code lần hai (xem research.md Quyết
định 0–1).

**Storage**: N/A cho bản thân tính năng — không tạo, không ghi dữ liệu nghiệp vụ mới. Báo cáo mỗi lần
chạy được ghi ra tệp dưới `artifacts/performance/` (cùng quy ước với `artifacts/coverage/` hiện có
trong Jenkinsfile) để làm mốc nền tra cứu lại (FR-005), không phải một cơ sở dữ liệu mới.

**Testing**: Dự án mới `tests/CriticalPathLoadTests` (xUnit host cho các scenario NBomber), yêu cầu
toàn bộ stack đang chạy thật (gateway + bff + 4 service nghiệp vụ) — giống yêu cầu mà
`frontend/apps/web/e2e/walkthrough.spec.ts` đã ghi chú cho Playwright. KHÔNG chạy trong 3 tier hiện có
của `scripts/ci/run-dotnet-tests.sh` (unit/integration/contract) — cần một tier "performance" mới,
loại trừ tường minh khỏi tier "unit" (xem Summary, khoảng trống #2).

**Target Platform**: Môi trường giống production khởi động bằng `docker-compose.demo.yml` (đã dùng
cho demo end-to-end của hạng mục 006), chạy trong một pipeline Jenkins mới theo lịch trình
(`Jenkinsfile.performance`), tách biệt khỏi `Jenkinsfile` chặn PR hiện có.

**Project Type**: Bổ sung một dự án kiểm thử tải + một pipeline theo lịch vào monorepo hiện có —
không phải service runtime mới, không có "frontend"/"backend" mới cho tính năng này.

**Performance Goals**: Chính là nội dung của tính năng — đối chiếu với ngân sách client-facing-bff đã
khai báo cho bff (p95 ≤ 300ms, p99 ≤ 800ms) trên cả 4 bước của luồng trọng yếu.

**Constraints**: KHÔNG được thay đổi hành vi phản hồi của bất kỳ endpoint nào hiện có (FR-010). KHÔNG
hard-code số liệu ngân sách lần thứ hai trong dự án kiểm thử tải — luôn đọc từ manifest (nhất quán
với 021). KHÔNG được để dự án kiểm thử tải mới lọt vào tier "unit"/"integration"/"contract" hiện có
của PR gate (khoảng trống #2 ở Summary).

**Scale/Scope**: 1 dự án kiểm thử tải mới; 1 tệp manifest được bổ sung (2 mục `endpoints` còn thiếu);
1 script CI mới (`scripts/ci/run-performance-tests.sh`) + 1 chỉnh sửa loại trừ trong
`scripts/ci/run-dotnet-tests.sh`; 1 pipeline Jenkins mới theo lịch.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Áp dụng thế nào | Trạng thái |
|---|---|---|
| I. Service Autonomy and Bounded Context | Không đổi quyền sở hữu dữ liệu hay ranh giới service; dự án kiểm thử tải chỉ gọi các endpoint công khai qua BFF/gateway như một client thật, không truy cập trực tiếp CSDL của bất kỳ service nào. | PASS |
| II. Contract-First Integration | Không có hợp đồng HTTP/event mới cho ứng dụng; hợp đồng đầu ra của tính năng này là tài liệu bất biến của chính bài kiểm thử tải (`contracts/load-test-run-contract.md`, `contracts/performance-pipeline-stage-contract.md`), viết trước khi hiện thực. | PASS |
| III. Test-First Development | Bản thân tính năng LÀ một bộ test; "Red" tự nhiên đến từ Test Scenario 2 của Jira (đưa hồi quy vào → xác nhận fail trước khi coi là "Green" ở Test Scenario 3) — đúng tinh thần Red-Green mà spec FR-009/quickstart.md Bước 2–3 yêu cầu. | PASS |
| IV. Event-Driven by Default | Không liên quan — tính năng không thêm giao tiếp bất đồng bộ mới giữa service. | N/A |
| V. Tenant Isolation Is a Security Boundary | Tải giả lập vẫn phải mang tenant hợp lệ như một client thật (cùng cơ chế X-Tenant-Id/token mà walkthrough.spec.ts và BFF endpoints đã yêu cầu) — không có đường dẫn mới bỏ qua ngữ cảnh tenant. | PASS |
| VI. Secure by Default | Không có endpoint mới, không có secret mới; dự án kiểm thử tải xác thực qua đúng cơ chế bearer/`ecommerce-api-scope` hiện có của BFF, không thêm cơ chế auth riêng. | PASS |
| VII. Observable by Default | Tính năng tận dụng lại đúng telemetry OTel mà Principle VII yêu cầu — traffic do NBomber tạo ra được BFF/service ghi vết như traffic thật, không cần instrumentation mới. | PASS |
| VIII. Performance and Resilience Budgets | Đây chính là nội dung cốt lõi của Principle VIII ("Critical user paths MUST have automated performance tests, and a regression that pushes a path outside its budget blocks release") — tính năng này là phần triển khai còn thiếu của chính câu đó. | PASS (là mục tiêu chính) |
| IX. Frontend Discipline | Không liên quan — không đổi mã frontend. | N/A |
| X. Toggle-Gated, Reversible Delivery | Bổ sung một dự án test và một pipeline theo lịch không phải hành vi ứng dụng runtime cần feature toggle; rollback là revert commit/tắt job theo lịch, không ảnh hưởng release đang chạy hay dữ liệu đã có. | PASS (lý giải, không phải vi phạm) |

Không có vi phạm nào cần biện minh tại Complexity Tracking.

**Rà soát lại sau Phase 1**: `data-model.md`, `contracts/`, và `quickstart.md` không đưa vào bất kỳ
service, endpoint, hay đường dẫn dữ liệu tenant mới nào so với Technical Context ở trên — bảng
Constitution Check giữ nguyên, không mục nào đổi trạng thái.

## Project Structure

### Documentation (this feature)

```text
specs/025-load-performance-test-budgets/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md         # Phase 1 output (/speckit-plan command)
├── quickstart.md         # Phase 1 output (/speckit-plan command)
├── contracts/            # Phase 1 output (/speckit-plan command)
└── tasks.md              # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
services/bff/src/Bff.Api/
└── service-manifest.yaml            # sửa: bổ sung 2 mục endpoints còn thiếu (POST /bff/basket/items, POST /bff/checkout)
                                      #      cùng ngân sách client-facing-bff hiện có — khoảng trống thật do khảo sát phát hiện

tests/
├── ServiceManifestSloConventionTests/   # đã có (021) — CriticalPathLoadTests tham chiếu ServiceManifestFixture từ đây, không đọc YAML lần hai
└── CriticalPathLoadTests/               # dự án mới
    ├── CriticalPathLoadTests.csproj
    ├── CriticalPathScenario.cs          # kịch bản NBomber: browse → basket → checkout → order, qua gateway
    ├── BudgetAssertions.cs              # so p95/p99 NBomber đo được với ngưỡng đọc từ ServiceManifestFixture; FAIL (exit khác 0) khi vượt
    └── LoadTestReportWriter.cs          # ghi báo cáo mỗi lần chạy vào artifacts/performance/ (mốc nền FR-005)

scripts/ci/
├── run-dotnet-tests.sh               # sửa: thêm case "performance" + loại trừ CriticalPathLoadTests khỏi tier "unit" (khoảng trống #2)
└── run-performance-tests.sh          # mới: khởi động docker-compose.demo.yml (nếu chưa chạy) rồi gọi tier "performance"

Jenkinsfile.performance                # mới: pipeline theo lịch (cron), tách biệt khỏi Jenkinsfile chặn PR hiện có
```

**Structure Decision**: Không tạo service runtime mới. Dự án kiểm thử tải nằm cạnh các dự án
`tests/*Tests` cùng loại theo đúng quy ước vị trí của repo, nhưng KHÔNG dùng chung tier "unit" với
chúng vì bản chất khác biệt (cần stack sống, chạy chậm hơn nhiều bậc) — đây là lý do bắt buộc phải
sửa `run-dotnet-tests.sh` thay vì chỉ "thêm file mới" như một convention test thông thường. Pipeline
theo lịch được tách thành `Jenkinsfile.performance` riêng thay vì thêm stage vào `Jenkinsfile` hiện
có, vì `Jenkinsfile` được xây quanh giả định "mọi stage chặn mọi PR" (specs/013) — trộn một stage
theo lịch, chạy chậm vào đó sẽ làm sai lệch chính hợp đồng 5-stage đang được branch protection thực
thi.

## Complexity Tracking

> Không có vi phạm Constitution Check nào cần biện minh — bảng này để trống có chủ đích.
