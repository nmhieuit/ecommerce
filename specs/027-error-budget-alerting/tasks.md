---

description: "Danh sách task triển khai chính sách ngân sách lỗi và ngưỡng cảnh báo (SCRUM-35)"
---

# Tasks: Chính sách ngân sách lỗi (error budget) và ngưỡng cảnh báo gắn với SLO từng service

**Input**: Tài liệu thiết kế tại `specs/027-error-budget-alerting/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: CÓ — người dùng chốt "viết test trước" (research.md Quyết định 8) và constitution Principle
III bắt buộc Red-Green. Mỗi test PHẢI được chạy thấy đỏ trước khi viết phần hiện thực tương ứng.

**Organization**: Nhóm theo user story của spec.md để mỗi story hiện thực và kiểm thử độc lập được.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Chạy song song được (khác file, không phụ thuộc task chưa xong)
- **[Story]**: User story mà task phục vụ (US1, US2, US3)
- Mỗi task ghi rõ đường dẫn file

## Path Conventions

- Manifest: `services/<dir>/src/<Name>.Api/service-manifest.yaml` — 7 cặp `<dir>`/`<Name>`:
  `parties/Parties`, `products/Products`, `baskets/Baskets`, `orders/Orders`, `identity/Identity`,
  `gateway/Gateway`, `bff/Bff`.
- Middleware dùng chung: `shared/ServiceDefaults/`; test: `shared/ServiceDefaults.UnitTests/`.
- Convention test: `tests/ServiceManifestSloConventionTests/`.
- Tài liệu và export Kibana: `docs/kibana-quan-sat-he-thong/`.
- Kibana local: `http://localhost:5601`, dựng bằng `./scripts/local-up.ps1`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Ghi mốc trạng thái xanh hiện có và tạo chỗ chứa export rule.

- [X] T001 Chạy `dotnet test tests/ServiceManifestSloConventionTests` và `dotnet test shared/ServiceDefaults.UnitTests`, xác nhận cả hai xanh trước khi sửa gì; nếu đỏ thì dừng và báo người dùng (không phải lỗi của tính năng này).
- [X] T002 [P] Tạo `docs/kibana-quan-sat-he-thong/alerts/README.md` theo đúng khuôn của `docs/kibana-quan-sat-he-thong/dashboards/README.md`: mục đích của `error-budget-rules.ndjson`, lệnh import (`curl.exe ... /api/saved_objects/_import?overwrite=true`), lệnh export theo id rule + connector, và ghi chú bắt buộc "Kibana nhập rule ở trạng thái disabled — phải Enable lại cả 4 rule có tag `slo-error-budget` sau mỗi lần import" (research.md Quyết định 9).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Bật được Kibana Alerting và kiểm chứng 5 điểm V1–V5 của research.md trên Kibana 9.4.4 thật.

**⚠️ CRITICAL**: Phase này chặn US2 và US3. US1 (chỉ sửa manifest + test) KHÔNG phụ thuộc phase này, có thể làm ngay sau Phase 1.

- [X] T003 [P] Sửa `.env.example`: thêm `KIBANA_ENCRYPTION_KEY` vào **Vùng 2 (biến bảo mật)** với một giá trị local dài ≥ 32 ký tự và chú thích "khoá mã hoá saved objects của Kibana, bắt buộc để chạy alert rule — SCRUM-35"; thêm `CHAOS_ALLOW_FAULT_INJECTION` vào **Vùng 1** (dạng dòng comment `# CHAOS_ALLOW_FAULT_INJECTION=true`, đúng khuôn `CHAOS_ALLOW_LATENCY_INJECTION` của 025 — để trống = compose mặc định `false`) với chú thích "chỉ đặt true trong lúc diễn tập làm cạn ngân sách lỗi (specs/027 quickstart), không bao giờ true ở production".
- [X] T004 Sửa service `kibana` trong `docker-compose.yml` và `docker-compose.local.yml`: thêm biến môi trường `XPACK_ENCRYPTEDSAVEDOBJECTS_ENCRYPTIONKEY: ${KIBANA_ENCRYPTION_KEY:?copy .env.example to .env before starting}` — cùng cú pháp `:?` mà `MSSQL_SA_PASSWORD` đang dùng trong `docker-compose.yml`.
- [X] T005 Dựng stack (`./scripts/local-up.ps1`), gọi `GET http://localhost:5601/api/alerting/_health` và xác nhận `has_permanent_encryption_key: true` (research.md V4). Nếu biến môi trường không được ánh xạ: chuyển sang mount một `kibana.yml` tối thiểu như phương án dự phòng của V4, sửa lại T004, rồi kiểm tra lại.
- [X] T006 Trong Kibana Discover (chế độ ES|QL), kiểm chứng V3: biểu thức `DATE_TRUNC(1 month, NOW() + 7 hours) - 7 hours` trả đúng 00:00 ngày 1 tháng hiện tại giờ Việt Nam; và V2: một truy vấn `FROM traces-generic.otel-default*, <index thử>` với hai tầng `STATS` (theo service + ngày, rồi theo service) chạy được. Index thử tạo tạm bằng Dev Tools, xoá sau khi xong.
- [X] T007 Tạo tạm một rule "Elasticsearch query" dạng ES|QL trả về nhiều hàng (vd `STATS c = COUNT(*) BY resource.attributes.service.name`), chu kỳ 1 phút, và kiểm chứng V1: Kibana tạo **một alert riêng cho mỗi hàng/nhóm** (xem Stack Management → Rules → rule → tab Alerts).
- [X] T008 Với alert của rule tạm ở T007, kiểm chứng V5: tạo data view trên `.alerts-stack.alerts-default` (cho phép index ẩn) và hiển thị được các alert active trong một panel Lens/ES|QL trên một dashboard nháp. Xong thì xoá rule tạm, dashboard nháp và data view thử.
- [X] T009 Ghi kết quả V1–V5 (đúng/sai, bằng chứng, phương án dự phòng đã dùng nếu có) vào một mục mới "Kết quả xác minh (T005–T008)" ở cuối `specs/027-error-budget-alerting/research.md`. **Nếu V1 hoặc V2 sai: DỪNG toàn bộ US2/US3 và hỏi người dùng chọn lại cơ chế — không tự đổi phương án** (plan.md "Điểm dừng bắt buộc").

**Checkpoint**: Kibana Alerting chạy được; cơ chế ES|QL rule đã được chứng minh — US2/US3 có thể bắt đầu.

---

## Phase 3: User Story 1 - Chính sách ngân sách lỗi được định nghĩa bằng con số và ghi trong manifest (Priority: P1) 🎯 MVP

**Goal**: Cả 7 `service-manifest.yaml` có khối `error-budget-policy` đúng [contracts/error-budget-policy-manifest-shape.md](./contracts/error-budget-policy-manifest-shape.md).

**Independent Test**: `dotnet test tests/ServiceManifestSloConventionTests` xanh; đọc từng manifest trả lời được "ai dừng / dừng cái gì / khi nào tiếp tục" (quickstart.md Kịch bản 2).

### Tests for User Story 1 ⚠️

> **Viết test trước, chạy thấy ĐỎ rồi mới sửa manifest.**

- [X] T010 [US1] Mở rộng `tests/ServiceManifestSloConventionTests/ServiceManifestModel.cs`: thêm thuộc tính `ErrorBudgetPolicy` (alias `error-budget-policy`) vào `ServiceManifestDocument` và các lớp con ánh xạ đúng mọi khoá của contract (`window`, `timezone`, `budgets` với 4 khoá `availability`/`error-rate`/`latency-p95`/`latency-p99` mỗi khoá có `bad-request`/`allowed-bad-ratio`, `alert-thresholds`, `exhausted-when`, `on-exhausted.who/stops/does`, `recovery.consecutive-days-meeting-slo/no-traffic-day-counts-as-met/budget-reset-clears-freeze`), mỗi thuộc tính dùng `[YamlMember(Alias = ...)]` như các lớp sẵn có.
- [X] T011 [US1] Tạo `tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs`: các `[Theory]` với 7 `[InlineData]` service giống `SloDeclarationTests.cs`, mỗi test một bất biến 1–7 của contract (có khối; đúng 4 ngân sách với đúng `bad-request` và `allowed-bad-ratio`; `window`/`timezone`; mốc `[50%, 75%, 100%]` và `exhausted-when`; `on-exhausted` không rỗng; `recovery` đúng 3/true/false; khối không chứa ngưỡng độ trễ). Comment tiếng Việt theo cùng mẫu `/// Kiểm tra / Lý do / Task nguồn: spec 027 ...` của `SloDeclarationTests.cs`.
- [X] T012 [US1] Chạy `dotnet test tests/ServiceManifestSloConventionTests --filter ErrorBudgetPolicyTests`, xác nhận ĐỎ cho cả 7 service (chưa manifest nào có khối), ghi số test đỏ vào ghi chú của task này. **Kết quả 2026-10-01: ĐỎ 49/49 (7 bất biến × 7 service), mỗi service báo "has no error-budget-policy block".** T020: XANH 78/78 (29 test 021 + 49 test mới); `CriticalPathStepBudgetsTests` (cũng đọc manifest) vẫn XANH 2/2.

### Implementation for User Story 1

- [X] T013 [P] [US1] Thêm khối `error-budget-policy` (đúng nguyên văn hình dạng trong contract, kèm comment dẫn SCRUM-35 / Principle VIII) ngay sau khối `slos` trong `services/parties/src/Parties.Api/service-manifest.yaml`.
- [X] T014 [P] [US1] Như T013 cho `services/products/src/Products.Api/service-manifest.yaml`.
- [X] T015 [P] [US1] Như T013 cho `services/baskets/src/Baskets.Api/service-manifest.yaml`.
- [X] T016 [P] [US1] Như T013 cho `services/orders/src/Orders.Api/service-manifest.yaml`.
- [X] T017 [P] [US1] Như T013 cho `services/identity/src/Identity.Api/service-manifest.yaml`.
- [X] T018 [P] [US1] Như T013 cho `services/gateway/src/Gateway.Api/service-manifest.yaml`.
- [X] T019 [P] [US1] Như T013 cho `services/bff/src/Bff.Api/service-manifest.yaml`.
- [X] T020 [US1] Chạy `dotnet test tests/ServiceManifestSloConventionTests` (toàn bộ, gồm test của 021), xác nhận XANH hết.

**Checkpoint**: Chính sách đã viết và được bảo vệ khỏi trôi dạt — giao được độc lập (MVP).

---

## Phase 4: User Story 2 - Cảnh báo tự động khi ngân sách vượt mốc, hiển thị ở nơi người vận hành nhìn mỗi ngày (Priority: P1)

**Goal**: 3 rule `error-budget-50/75/100` chạy mỗi 5 phút, alert theo (service, ngân sách), hiện trên dashboard SLO hằng ngày; có công cụ tiêm 5xx để chứng minh.

**Independent Test**: quickstart.md Kịch bản 1 (phần mốc 50/75/100) và Kịch bản 3.

### Tests for User Story 2 ⚠️

- [X] T021 [P] [US2] Tạo `shared/ServiceDefaults.UnitTests/ChaosFaultInjectionMiddlewareTests.cs` phủ bất biến 2, 3, 4, 6 của [contracts/chaos-fault-injection-contract.md](./contracts/chaos-fault-injection-contract.md) bằng `DefaultHttpContext` (cấu hình tắt + có header → `next` được gọi, response không đổi; bật + `X-Chaos-Fault: 5xx` → 500 và `next` KHÔNG được gọi; bật + thiếu header hoặc giá trị khác → `next` được gọi). Nếu project test thiếu kiểu ASP.NET Core, thêm `FrameworkReference Microsoft.AspNetCore.App` vào `shared/ServiceDefaults.UnitTests/ServiceDefaults.UnitTests.csproj`.
- [X] T022 [P] [US2] Tạo `tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs` đọc `docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson` (tìm từ repo root như `ServiceManifestFixture`) và kiểm tra bất biến 1–4 của [contracts/error-budget-alert-rules-contract.md](./contracts/error-budget-alert-rules-contract.md) **cho 3 rule mốc**: có đủ `error-budget-50`, `-75`, `-100`; chu kỳ `5m`; tag `slo-error-budget`; ngưỡng so sánh đúng `50`/`75`/`100`; ngưỡng độ trễ (ns) theo từng service trong ES|QL khớp `slos.latency.p95/p99` của manifest; tỷ lệ cho phép `0.001`/`0.001`/`0.05`/`0.01`.
- [X] T023 [US2] Chạy `dotnet test shared/ServiceDefaults.UnitTests` và `dotnet test tests/ServiceManifestSloConventionTests --filter ErrorBudgetRuleDefinitionTests`, xác nhận ĐỎ (middleware chưa có, file ndjson chưa có). **Kết quả 2026-10-01: ServiceDefaults.UnitTests không biên dịch (CS0246 `ChaosFaultOptions`); ErrorBudgetRuleDefinitionTests ĐỎ 12/12 "error-budget-rules.ndjson does not exist".**

### Implementation for User Story 2 — tiêm lỗi 5xx

- [X] T024 [US2] Tạo `shared/ServiceDefaults/ChaosFaultOptions.cs` (section `Chaos`, `bool AllowFaultInjection` mặc định `false`, XML doc dẫn contract bất biến 1) theo khuôn `services/orders/src/Orders.Api/Features/Chaos/ChaosOptions.cs`.
- [X] T025 [US2] Tạo `shared/ServiceDefaults/ChaosFaultInjectionMiddleware.cs` (header `X-Chaos-Fault`, giá trị kích hoạt `5xx`, trả `StatusCodes.Status500InternalServerError` và không gọi `next`) theo khuôn `ChaosLatencyInjectionMiddleware.cs` của 025.
- [X] T026 [US2] Sửa `shared/ServiceDefaults/ServiceDefaultsExtensions.cs`: đăng ký `Configure<ChaosFaultOptions>(...GetSection("Chaos"))` trong `AddServiceDefaults` và `app.UseMiddleware<ChaosFaultInjectionMiddleware>()` ngay sau `CorrelationIdMiddleware` trong `UseServiceDefaults`.
- [X] T027 [US2] Thêm `Chaos__AllowFaultInjection: ${CHAOS_ALLOW_FAULT_INJECTION:-false}` vào biến môi trường của 7 service API (`products-api`, `baskets-api`, `orders-api`, `parties-api`, `identity-api`, `bff-api`, `gateway-api`) trong `docker-compose.yml` và `docker-compose.local.yml`.
- [X] T028 [US2] Chạy `dotnet test shared/ServiceDefaults.UnitTests`, xác nhận XANH; chạy `dotnet build Ecommerce.slnx` xác nhận 7 service build sạch (analyzer không báo lỗi). **Kết quả 2026-10-01: ServiceDefaults.UnitTests XANH 21/21 (13 cũ + 8 mới); `dotnet build Ecommerce.slnx` 0 lỗi 0 cảnh báo; ContainerConventionTests 9/9, StructureConventionTests 9/9, DeploymentManifestConventionTests 58/58 XANH. Có 4 test đỏ KHÔNG do 027 — đỏ y hệt trên HEAD `d43ee17` sạch (đã chạy lại trong worktree tạm): CrossServiceIsolation.Tests 3 (`TenantGatedConnectionTests` orders; 2 test `AuthorizationPolicyDeclaredScannerTests` vì `OrderPlacedVerificationConsumer` trong Orders.Api.IntegrationTests) và Orders.Api.UnitTests `HealthLive_ReturnsOk` (thiếu secret `ConnectionStrings:OrdersDb`).**

### Implementation for User Story 2 — rule và dashboard

- [X] T029 [US2] Tạo `docs/kibana-quan-sat-he-thong/07-canh-bao-ngan-sach-loi.md` (cùng văn phong file `06`): ghi truy vấn ES|QL tính `consumed_pct` của 4 ngân sách × 7 service theo research.md Quyết định 2 (ranh giới tháng đã xác minh ở T006, ngưỡng độ trễ CASE theo service lấy từ manifest, `duration` tính bằng ns). Chạy truy vấn trong Discover và đối chiếu `Orders.Api` với số đếm thô qua `_search`/`_count` như file `06` đã làm; ghi kết quả đối chiếu vào file.
- [X] T030 [US2] Trên Kibana UI tạo 3 rule "Elasticsearch query" (ES|QL) `error-budget-50`, `error-budget-75`, `error-budget-100`: truy vấn T029 + `WHERE consumed_pct >= <mốc>`, chu kỳ 5 phút, alert theo hàng (service, budget) như đã xác minh ở T007, tag `slo-error-budget`. Ghi cấu hình từng rule vào `docs/kibana-quan-sat-he-thong/07-canh-bao-ngan-sach-loi.md`.
- [X] T031 [US2] Sửa dashboard `SLO vận hành hằng ngày — 7 service` (id `e2e06ff5-9cdf-4bea-acc8-5fd60ce26170`): thêm nhóm panel "Ngân sách lỗi tháng này" ở **trên cùng** gồm (1) bảng mức tiêu hao 7 service × 4 ngân sách tô màu theo mốc 50/75/100, không theo time range của dashboard; (2) bảng alert đang active lọc tag `slo-error-budget` với cột service / ngân sách / mốc / mức tiêu hao — nguồn dữ liệu theo kết quả V5 ở T008. Ghi cách dựng vào file `07`. **Điều chỉnh (người dùng chốt 2026-10-01)**: dựng bằng 2 Discover session ES|QL qua Saved Objects API (thao tác UI Lens qua trình duyệt tự động hoá không ổn định); mốc hiển thị bằng cột chữ `moc` thay vì tô màu; panel dùng khoảng thời gian riêng `now-62d`.
- [X] T032 [US2] Export 3 rule ra `docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson` và export lại dashboard ra `docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson` theo đúng lệnh export trong hai README.
- [X] T033 [US2] Chạy `dotnet test tests/ServiceManifestSloConventionTests --filter ErrorBudgetRuleDefinitionTests`, xác nhận XANH.
- [X] T034 [US2] Thực hiện [quickstart.md](./quickstart.md) Chuẩn bị + Kịch bản 1 (phần mốc 50/75/100 của `Orders.Api`, gồm kiểm tra span 500 trong Discover) + Kịch bản 3, rồi kiểm tra lại 5 bất biến của `specs/021-declare-service-slos/contracts/continuous-measurement-contract.md` trên dashboard đã sửa. Ghi bằng chứng (thời điểm vượt mốc, thời điểm alert bật, ảnh/giá trị panel) vào file `07`. Xong thì đặt lại `CHAOS_ALLOW_FAULT_INJECTION=false`.

**Checkpoint**: Vượt mốc là có cảnh báo hiện ngay trên dashboard người vận hành mở mỗi ngày.

---

## Phase 5: User Story 3 - Hệ quả khi ngân sách cạn: ưu tiên độ tin cậy cho tới khi hồi phục (Priority: P2)

**Goal**: Trạng thái "cạn ngân sách — ưu tiên độ tin cậy" được suy ra tự động (research.md Quyết định 4) và hiện trên dashboard.

**Independent Test**: quickstart.md Kịch bản 1 (phần event + bảng "cạn") và các kiểm tra logic hồi phục ở T041.

### Tests for User Story 3 ⚠️

- [X] T035 [US3] Mở rộng `tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs`: thêm kiểm tra rule `error-budget-frozen` có mặt, chu kỳ `5m`, tag `slo-error-budget`; rule `error-budget-100` có action Index connector ghi vào `slo-error-budget-events` với tần suất "khi đổi trạng thái"; file export có connector Index đó. Chạy và xác nhận ĐỎ.

### Implementation for User Story 3

- [X] T036 [US3] Tạo index `slo-error-budget-events` với mapping `@timestamp: date`, `service: keyword`, `budget: keyword`, `event: keyword` qua Dev Tools; ghi nguyên văn lệnh `PUT` vào `docs/kibana-quan-sat-he-thong/07-canh-bao-ngan-sach-loi.md` và thêm bước tạo index này vào mục "Chuẩn bị" của `specs/027-error-budget-alerting/quickstart.md`.
- [X] T037 [US3] Tạo connector Index trỏ tới `slo-error-budget-events` và thêm action vào rule `error-budget-100` (theo từng alert, khi đổi trạng thái sang active) ghi `{ @timestamp, service, budget, event: "exhausted" }` từ context của alert. Ghi cấu hình vào file `07`.
- [X] T038 [US3] Tạo rule `error-budget-frozen` (ES|QL, 5 phút, tag `slo-error-budget`, alert theo service) đúng logic research.md Quyết định 4: `exhausted_at` từ `slo-error-budget-events`; `last_bad_day` = ngày UTC+7 gần nhất có traffic mà không đạt đủ 4 tỷ lệ cho phép; ngày không traffic tính là đạt; `frozen` khi số ngày trọn vẹn sau `MAX(ngày(exhausted_at), last_bad_day)` tới hết hôm qua < 3. Ghi truy vấn và giải thích vào file `07`.
- [X] T039 [US3] Thêm panel thứ 3 "Cạn ngân sách — ưu tiên độ tin cậy" (service đang có alert active của `error-budget-frozen`) vào nhóm "Ngân sách lỗi tháng này" của dashboard.
- [X] T040 [US3] Export lại `docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson` (4 rule + connector) và `docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson`; chạy `dotnet test tests/ServiceManifestSloConventionTests`, xác nhận XANH hết.
- [X] T041 [US3] Kiểm chứng logic đóng băng/hồi phục: (a) sau Kịch bản 1 của quickstart, `slo-error-budget-events` có event `exhausted` cho `Orders.Api` và bảng "cạn" hiện `Orders.Api`; (b) ghi thủ công một event thử cho một service khác với `@timestamp` cách đây 5 ngày, xác nhận service đó KHÔNG bị đóng băng nếu 3 ngày gần nhất đều đạt SLO; (c) đổi `@timestamp` event thử thành hôm qua, xác nhận service đó bị đóng băng. Xoá event thử sau khi xong; ghi bằng chứng vào file `07`.

**Checkpoint**: Vi phạm kéo dài có hệ quả thật, nhìn thấy được, với điều kiện thoát rõ ràng.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Tài liệu kiến trúc/QA/PO/sơ đồ theo nếp của các feature trước và đồng bộ các tham chiếu cũ.

- [X] T042 [P] Tạo `docs/architecture/027_Architect_chính sách ngân sách lỗi và ngưỡng cảnh báo.md` theo đúng cấu trúc và văn phong của `docs/architecture/021_Architect_khai báo và đo SLO theo từng service.md`.
- [X] T043 [P] Tạo `docs/QA/027_QA_chính sách ngân sách lỗi và ngưỡng cảnh báo.md` theo nếp tài liệu QA từ 024 trở đi: phần Thủ công đứng trước Tự động; phần thủ công điều khiển bằng toggle `.env`/compose (`CHAOS_ALLOW_FAULT_INJECTION`) và folder Postman; bảng "Tự động" link từng test (`ErrorBudgetPolicyTests`, `ErrorBudgetRuleDefinitionTests`, `ChaosFaultInjectionMiddlewareTests`) tới đúng dòng; giữ ngắn gọn, không có khối "Nguồn đối chiếu", mọi phát hiện chuyển sang `docs/QA/QA_Debt.md`.
- [X] T044 [P] Thêm folder "027 — Tiêm lỗi 5xx (error budget)" vào `postman/ecommerce.postman_collection.v2.json` với request gửi header `X-Chaos-Fault: 5xx` tới health endpoint của từng service, dùng biến môi trường trong `postman/local.postman_environment.v2.json`.
- [X] T045 Cập nhật `docs/architecture/technical-debt.md` (mục 021 dòng "Không có alert rule tự động ... thuộc SCRUM-35": đánh dấu đã giải quyết bởi 027, ghi giới hạn mới nếu có — vd lưu lượng thấp làm vượt mốc nhanh, ngày không traffic tính là đạt) và `docs/QA/QA_Debt.md` (các phát hiện từ T034/T041).
- [X] T046 [P] Cập nhật tham chiếu "alert rule thuộc SCRUM-35, ngoài phạm vi" thành trỏ tới 027 trong `docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md` và `specs/021-declare-service-slos/contracts/continuous-measurement-contract.md`; thêm mục file `07` vào `docs/kibana-quan-sat-he-thong/00-tong-quan-lo-trinh.md`.
- [X] T047 Chạy một lượt cuối `dotnet test tests/ServiceManifestSloConventionTests` và `dotnet test shared/ServiceDefaults.UnitTests`, rồi thực hiện toàn bộ [quickstart.md](./quickstart.md) liền mạch trên stack dựng mới (import từ ndjson, enable rule). Mục "Kiểm tra theo thời gian" (hồi phục thật sau 3 ngày, giữ đóng băng qua ranh giới tháng) ghi là còn mở trong tài liệu QA, không đánh dấu xong. **Kết quả 2026-10-01: ServiceManifestSloConventionTests 95/95, ServiceDefaults.UnitTests 21/21 xanh; import lại `alerts/error-budget-rules.ndjson` (5 object) và dashboard (6 object) thành công, 4 rule disabled sau import đúng như tài liệu; 2/4 rule kẹt `pending` sau khi Enable, gỡ bằng Disable → Enable (đã ghi vào README/quickstart/QA_Debt); cả 4 rule chạy lại thành công. Không dựng stack hoàn toàn mới (stack đang chạy, dữ liệu giữ nguyên); mục "theo thời gian" để mở trong QA 027.**

**Bổ sung tài liệu theo khuôn spec 020–026 (yêu cầu ngày 2026-10-01)** — sơ đồ, PO, rà soát QA/Architect:

- [X] T048 [P] Tạo `docs/diagrams/027-error-budget-alerting-component.drawio` — sơ đồ thành phần. Trước khi vẽ, mở `docs/diagrams/021-declare-service-slos-component.drawio` và `docs/diagrams/025-chaos-pod-kill-latency-component.drawio` để chép đúng bố cục, kiểu khối/màu, ngôn ngữ nhãn (tiếng Việt) và dạng thuộc tính `<diagram id="error-budget-alerting-component" name="Kiến trúc thành phần — …">`. Nội dung: 7 `service-manifest.yaml` (khối `error-budget-policy` cạnh `slos`); `ChaosFaultInjectionMiddleware` trong `shared/ServiceDefaults` (cờ `Chaos:AllowFaultInjection`, header `X-Chaos-Fault: 5xx`); OTel Collector → Elasticsearch `traces-generic.otel-default*`; 4 rule Kibana ES|QL `error-budget-50/75/100` và `error-budget-frozen` (chu kỳ 5 phút, tag `slo-error-budget`); connector Index → index `slo-error-budget-events`; alerts-as-data `.alerts-stack.alerts-default`; 3 Discover session (`slo-error-budget-consumption`, `slo-error-budget-active-alerts`, `slo-error-budget-frozen`) trên dashboard "SLO vận hành hằng ngày — 7 service"; 2 bộ test canh gác (`tests/ServiceManifestSloConventionTests`: `ErrorBudgetPolicyTests`, `ErrorBudgetRuleDefinitionTests`; `shared/ServiceDefaults.UnitTests`: `ChaosFaultInjectionMiddlewareTests`). Nguồn sự thật: `specs/027-error-budget-alerting/plan.md`, `research.md`, `docs/kibana-quan-sat-he-thong/07-canh-bao-ngan-sach-loi.md`.
- [X] T049 [P] Tạo `docs/diagrams/027-error-budget-alerting-flow-nghiep-vu.drawio` — luồng nghiệp vụ, theo mẫu `docs/diagrams/021-declare-service-slos-flow-nghiep-vu.drawio` và `docs/diagrams/025-chaos-pod-kill-latency-flow-nghiep-vu.drawio` (ngôn ngữ nghiệp vụ, không tên công cụ kỹ thuật): request xấu tiêu hao ngân sách tháng (tháng lịch giờ Việt Nam) → vượt mốc 50% / 75% / 100% → cảnh báo hiện trên dashboard người vận hành mở mỗi ngày → "cạn" khi bất kỳ ngân sách nào đạt 100% → dừng merge tính năng mới vào service đó, chỉ làm việc nâng độ tin cậy → đạt SLO 3 ngày liên tục (ngày không có traffic tính là đạt; ngày có traffic không đạt làm đếm lại) → hồi phục; nhánh riêng thể hiện "sang tháng mới ngân sách đặt lại nhưng KHÔNG gỡ trạng thái cạn". Nguồn: `spec.md` User Story 1–3, `data-model.md` mục 5.
- [X] T050 [P] Tạo `docs/diagrams/027-error-budget-alerting-sequence.drawio` — sơ đồ trình tự thật của diễn tập, theo mẫu `docs/diagrams/021-declare-service-slos-sequence.drawio` và `docs/diagrams/025-chaos-pod-kill-latency-sequence.drawio`: người vận hành bật `CHAOS_ALLOW_FAULT_INJECTION=true` (`.env`) và tạo lại `orders-api` → gửi `X-Chaos-Fault: 5xx` → `orders-api` trả `500`, không chạy tiếp pipeline → span qua OTel Collector vào Elasticsearch → rule chạy mỗi 5 phút, tính mức tiêu hao từ đầu tháng → alert (service, ngân sách) chuyển active → riêng mốc 100: action Index ghi sự kiện `exhausted` vào `slo-error-budget-events` → rule `error-budget-frozen` đọc sự kiện + traces theo ngày → panel "Cạn ngân sách — ưu tiên độ tin cậy" trên dashboard. Ghi số đo thật làm nhãn phụ (mốc 75 sau 4 phút 20 giây, mốc 100 sau 4 phút 16 giây — `docs/kibana-quan-sat-he-thong/07-canh-bao-ngan-sach-loi.md`).
- [X] T051 Tạo tài liệu PO `docs/PO/027_PO_<tên hướng người dùng>.md` — **HỎI LẠI NGƯỜI DÙNG tên file (phần sau `027_PO_`) trước khi tạo, đề xuất 2–3 phương án theo kiểu tên của 021/025**. Theo đúng khuôn `docs/PO/021_PO_biết ngay service nào đang lố ngân sách hiệu năng đã cam kết.md` và `docs/PO/025_PO_diễn tập chaos engineering giết pod tiêm độ trễ.md` (~45–55 dòng, đối tượng PO/stakeholder, không tên công cụ kỹ thuật): tiêu đề hướng giá trị → `## Vấn đề trước đây` → `## Giải pháp: …` → `## Trải nghiệm thực tế diễn ra như thế nào` → `## Lợi ích kinh doanh`. Không chép mục "Điều đặc biệt"/"Giới hạn hiện tại" vào file này — các mục đó thuộc T052.
- [X] T052 Cập nhật `docs/PO/functional-debt.md`: thêm mục 027 (link tới file PO của T051, cùng dạng link mã hoá `%20` như mục 021) vào **cả hai** phần `## 1. Điều đặc biệt — bằng chứng đã kiểm chứng thật / lỗi thật bắt được lúc kiểm chứng` (diễn tập làm cạn ngân sách thật, cảnh báo bật trong ≤ 5 phút, trạng thái cạn tự suy ra) và `## 2. Giới hạn hiện tại` (ví dụ: "dừng merge" chỉ là cam kết quy trình, không có cơ chế chặn tự động; hồi phục thật sau 3 ngày chưa quan sát trên dữ liệu chạy liên tục; môi trường local lưu lượng thấp làm vượt mốc rất nhanh) — bằng ngôn ngữ nghiệp vụ, đối chiếu `docs/QA/QA_Debt.md` mục 027 và `docs/architecture/technical-debt.md` mục 027. Số đếm "toàn bộ 24 tính năng" ở tiêu đề: nếu không còn đúng thì **hỏi lại người dùng con số trước khi sửa**. Phụ thuộc T051.
- [X] T053 [P] Rà soát `docs/QA/027_QA_chính sách ngân sách lỗi và ngưỡng cảnh báo.md` theo khuôn QA 024–026 (đối chiếu `docs/QA/025_QA_diễn tập chaos giết pod tiêm độ trễ.md`, `docs/QA/026_QA_kiểm thử tải hiệu năng luồng trọng yếu.md`): Thủ công đứng trước Tự động; bảng thủ công có cột `Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | Đã quan sát`, nêu cả trạng thái BẬT/TẮT của `CHAOS_ALLOW_FAULT_INJECTION` và cách khôi phục mặc định, trỏ đúng folder Postman `27 - Ngân sách lỗi: tiêm 5xx và cảnh báo (error budget)`; bảng Tự động 3 cột link từng test tới đúng dòng khai báo; không có khối "Nguồn đối chiếu"; không có phát hiện dài trong file (mọi phát hiện chỉ ở `docs/QA/QA_Debt.md` mục 027). Chạy lại `grep -n "public void\|public async Task"` trên `tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs`, `tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs`, `shared/ServiceDefaults.UnitTests/ChaosFaultInjectionMiddlewareTests.cs` và sửa mọi `#L…` lệch. Chỉ sửa khi có sai lệch thật; nếu đã đúng thì ghi "không cần sửa" vào ghi chú của task. **Kết quả 2026-10-01: không cần sửa — 19/19 link `#L…` trỏ đúng dòng khai báo test, không có "Nguồn đối chiếu", Thủ công trước Tự động, bảng 5 cột đúng khuôn, phát hiện chỉ ở QA_Debt.**
- [X] T054 Bổ sung vào `docs/architecture/027_Architect_chính sách ngân sách lỗi và ngưỡng cảnh báo.md` mục `## Sơ đồ` liệt kê 3 link tới file của T048–T050 (cùng dạng với mục `## 4. Sơ đồ` của `docs/architecture/021_Architect_khai báo và đo SLO theo từng service.md`: "Sơ đồ thành phần / Sơ đồ luồng nghiệp vụ / Sơ đồ trình tự"), đánh lại số các mục sau nếu cần; rồi đối chiếu từng giới hạn/phát hiện nêu trong file với `docs/architecture/technical-debt.md` mục 027 (cả phần 2 và phần 3) — chỗ nào lệch thì sửa cho khớp và ghi lại đã sửa gì. Phụ thuộc T048–T050. **Kết quả 2026-10-01: thêm `## 6. Sơ đồ` (3 link), "Tham khảo thêm" thành mục 7. Lệch phát hiện khi đối chiếu: giới hạn "2/4 rule kẹt `pending` sau import" (T047) chỉ có ở QA_Debt/README/quickstart — đã bổ sung vào Architect mục 5 và `technical-debt.md` phần 3 mục 027.**
- [X] T055 Kiểm tra cuối cho phần tài liệu: (a) mọi link tương đối trong file PO (T051), `docs/PO/functional-debt.md` mục 027, file QA 027, file Architect 027 và `docs/architecture/technical-debt.md` mục 027 trỏ tới file/dòng có thật (giải mã `%20`/ký tự tiếng Việt rồi kiểm tra tồn tại; với link `#L…` kiểm tra dòng đó là khai báo test); (b) 3 file `.drawio` của T048–T050 parse được bằng XML parser (`python -c "import xml.etree.ElementTree as E; E.parse(...)"`) và mỗi file có đúng 1 phần tử `<diagram>`. Ghi kết quả vào ghi chú của task này. Phụ thuộc T048–T054. **Kết quả 2026-10-01: 46 link kiểm tra, 0 lỗi (PO 027: 4, functional-debt mục 027: 3, QA 027: 25 — gồm 19 link `#L…` đều là dòng khai báo test, Architect 027: 11, technical-debt mục 027: 3); 3 file `.drawio` parse XML được, mỗi file đúng 1 `<diagram>`, không cạnh nào trỏ tới khối không tồn tại.**

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: không phụ thuộc.
- **Phase 2 (Foundational)**: sau Phase 1; **chặn US2 và US3**, KHÔNG chặn US1.
- **Phase 3 (US1)**: chỉ cần Phase 1.
- **Phase 4 (US2)**: cần Phase 2 (T009 kết luận V1/V2 đúng).
- **Phase 5 (US3)**: cần Phase 4 (rule `error-budget-100` và nhóm panel đã tồn tại).
- **Phase 6 (Polish)**: sau khi các story mong muốn đã xong. Nhóm bổ sung tài liệu T048–T055 chỉ dựa trên nội dung đã có của T001–T047, không cần stack chạy.

### User Story Dependencies

- **US1 (P1)**: độc lập hoàn toàn.
- **US2 (P1)**: độc lập với US1 về mã (rule đọc ngưỡng từ `slos` vốn đã có ở 021).
- **US3 (P2)**: phụ thuộc US2 (gắn action vào rule `error-budget-100`, panel thêm vào nhóm của US2).

### Within Each User Story

- Test viết trước và PHẢI đỏ trước khi hiện thực (T012, T023, T035).
- Middleware (T024→T026) trước wiring compose (T027).
- Truy vấn đã đối chiếu (T029) trước rule (T030) trước dashboard (T031) trước export (T032).

### Parallel Opportunities

- T002 song song với T003.
- US1 (T010–T020) chạy song song với cả Phase 2 và Phase 4.
- T013–T019: 7 manifest khác file, song song hoàn toàn.
- T021 và T022: khác dự án test, song song.
- T042, T043, T044, T046: khác file, song song.
- T048, T049, T050 (3 sơ đồ) và T053 (rà soát QA): khác file, không phụ thuộc nhau, song song; T051 cũng song song với chúng (chỉ chờ người dùng chốt tên file).
- T052 sau T051; T054 sau T048–T050; T055 sau cùng (sau T048–T054).

---

## Parallel Example: User Story 1

```text
Task: "T013 Thêm error-budget-policy vào services/parties/src/Parties.Api/service-manifest.yaml"
Task: "T014 Thêm error-budget-policy vào services/products/src/Products.Api/service-manifest.yaml"
Task: "T015 ... baskets"   Task: "T016 ... orders"   Task: "T017 ... identity"
Task: "T018 ... gateway"   Task: "T019 ... bff"
```

## Parallel Example: User Story 2

```text
Task: "T021 ChaosFaultInjectionMiddlewareTests trong shared/ServiceDefaults.UnitTests"
Task: "T022 ErrorBudgetRuleDefinitionTests trong tests/ServiceManifestSloConventionTests"
```

## Parallel Example: Bổ sung tài liệu (Phase 6)

```text
Task: "T048 docs/diagrams/027-error-budget-alerting-component.drawio"
Task: "T049 docs/diagrams/027-error-budget-alerting-flow-nghiep-vu.drawio"
Task: "T050 docs/diagrams/027-error-budget-alerting-sequence.drawio"
Task: "T053 Rà soát docs/QA/027_QA_chính sách ngân sách lỗi và ngưỡng cảnh báo.md"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 → Phase 3 (US1): chính sách đã viết, có test bảo vệ — đáp ứng tiêu chí chấp nhận 1 và 2 của Jira.
2. **DỪNG và kiểm tra**: quickstart.md Kịch bản 2.

### Incremental Delivery

1. Setup + US1 → MVP (chính sách bằng văn bản).
2. Phase 2 (xác minh V1–V5) → nếu V1/V2 sai, dừng hỏi người dùng.
3. US2 → cảnh báo trên dashboard (tiêu chí chấp nhận 3 + kịch bản kiểm thử 1, 3 của Jira).
4. US3 → trạng thái đóng băng tự động.
5. Polish → tài liệu Architect/QA, đồng bộ tham chiếu.

---

## Notes

- [P] = khác file, không phụ thuộc task chưa hoàn thành.
- Task cần Kibana/Docker thật (T005–T009, T029–T034, T036–T041, T047) chỉ đánh `[X]` khi đã chạy thật và có bằng chứng ghi lại — không đánh dấu dựa trên suy luận.
- `CHAOS_ALLOW_FAULT_INJECTION` phải về `false` sau mỗi lần diễn tập.
- Commit sau mỗi task hoặc nhóm task hợp lý, theo Conventional Commits.
