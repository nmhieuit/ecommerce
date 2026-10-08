---

description: "Danh sách task loại span health khỏi công thức ngân sách lỗi (spec 033)"
---

# Tasks: Loại span health khỏi công thức ngân sách lỗi

**Input**: Tài liệu thiết kế tại `specs/033-exclude-health-spans/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: CÓ. Nguyên tắc III: test viết trước và chạy thấy ĐỎ thật (rule/manifest chưa có điều kiện/khoá) rồi mới sửa. Comment test tiếng Việt theo khuôn hiện có (`Kiểm tra` / `Lý do` / `Task nguồn: spec 033 — FR-xxx`). Dashboard là cấu hình, kiểm theo [quickstart.md](./quickstart.md).

**Organization**: Nhóm theo user story của spec.md. **Thứ tự làm khác thứ tự ưu tiên**: US4 (khoá manifest + test, P2) làm **trước** US1–US3 vì rule và test của US1/US3 đọc `excluded-path-prefixes` từ manifest.

**Không commit** (người dùng chốt): chỉ ghi file. Người dùng tự xem và commit.

**Không suy diễn**: mọi điểm đánh dấu **HỎI NGƯỜI DÙNG** hoặc **DỪNG VÀ HỎI** phải dừng lại, đưa 2–4 phương án kèm hệ quả, không tự chọn.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: chạy song song được (khác file, không phụ thuộc task chưa xong)
- **[Story]**: US1–US5 của spec.md

## Đã chốt ở phiên `/speckit-tasks` (ghi lại để task bên dưới dùng đúng)

- **Rule mới**: tên `health-failure`, tag `health-failure`, chu kỳ 5 phút, file export `docs/kibana-quan-sat-he-thong/alerts/health-failure-rule.ndjson`.
- **Panel mới**: `Health lỗi theo service`, đặt ở section "Tình trạng SLO" của dashboard Xử lý sự cố, cạnh panel Phát hiện nhanh.
- **Đường dẫn tiêm lỗi/độ trễ** (thay `/health/live`): **route nghiệp vụ sẵn có của từng service**, đúng bảng của `scripts/incident-drill.ps1`:
  `Gateway.Api` và `Bff.Api` → `/bff/products`; `Products.Api` → `/products`; `Baskets.Api` → `/baskets/current`; `Orders.Api` → `/orders/{guid}`; `Parties.Api` → `/parties/{guid}`; `Identity.Api` → `/.well-known/openid-configuration` (`{guid}` dùng biến `{{unknownGuid}}` của collection hoặc GUID cố định như folder 25).
- **Assertion "cờ TẮT"** của 25a/27a: sửa theo phản hồi thật đo được ở V5 (T007).
- **Tên file**: `docs/PO/033_PO_loại span health khỏi ngân sách lỗi.md`, `docs/QA/033_QA_loại span health khỏi ngân sách lỗi.md`, `docs/architecture/033_Architect_loại span health khỏi ngân sách lỗi.md`; drawio `docs/diagrams/033-health-exclusion-{component,flow-nghiep-vu,sequence}.drawio`; folder Postman `33 - Health: loại khỏi ngân sách` (nội dung hỏi ở T044); lớp test rule mới `HealthFailureRuleTests`.
- **Giữ nguyên từ spec**: nhận diện theo tiền tố `/health`; loại hẳn khỏi cả 4 ngân sách; health lỗi chỉ tính 5xx (không tính chậm); điều kiện bắn ≥ 50% trong 5 phút; service chỉ có health không hiện dòng; khoá `excluded-path-prefixes: [/health]` trong `error-budget-policy` (không sửa hiến chương); `scripts/incident-drill.ps1` không đổi.

## Path Conventions

- Manifest: `services/<dir>/src/<Name>.Api/service-manifest.yaml`, 7 cặp `parties/Parties`, `products/Products`, `baskets/Baskets`, `orders/Orders`, `identity/Identity`, `gateway/Gateway`, `bff/Bff`.
- Test: `tests/ServiceManifestSloConventionTests/`.
- Export Kibana: `docs/kibana-quan-sat-he-thong/alerts/`, `docs/kibana-quan-sat-he-thong/dashboards/`.
- Kibana `http://localhost:5601`, Elasticsearch `http://localhost:9200` (stack `ecomerce-local` đang chạy; các container API đang mang `Chaos__AllowFaultInjection=true`, riêng `orders-api` thêm `Chaos__AllowLatencyInjection=true`). Lệnh PowerShell 5.1 dùng `curl.exe`; newman: `npx.cmd --yes newman@6.2.2`.
- Id dashboard: Xử lý sự cố `e61fc7f3-17fe-428a-a373-da88af0a4a1e`, Ngân sách tuần `2a607bf4-2449-48a1-a2e8-1336ec35a7b7`.
- Điều kiện loại (D1): `| WHERE NOT (COALESCE(attributes.url.path, "") LIKE "/health*")` (bắt buộc `COALESCE`, research F3).
- **LỆNH-TÌM-A** (tiêm lỗi vào health, dùng ở T002 và T059):
  `grep -rnI -E "health/(live|ready)" --exclude-dir=.git --exclude-dir=node_modules --exclude-dir=.claude --exclude-dir=bin --exclude-dir=obj --exclude-dir=services --exclude-dir=tests . | grep -E "Chaos|chaos|tiêm"`
- **LỆNH-TÌM-B** (mô tả công thức "mọi span", dùng ở T002 và T059):
  `grep -rnI -E "mọi span|tất cả span|every span|không loại health" --exclude-dir=.git --exclude-dir=node_modules --exclude-dir=.claude --exclude-dir=bin --exclude-dir=obj docs specs`

---

## Phase 1: Setup

**Purpose**: Ghi mốc trạng thái hiện có trước khi sửa.

- [X] T001 Chạy `dotnet test tests/ServiceManifestSloConventionTests` và `dotnet test shared/ServiceDefaults.UnitTests`, ghi số test xanh vào ghi chú task này. Nếu đỏ: dừng và báo người dùng. **Kết quả**: XANH 110/110 (`ServiceManifestSloConventionTests`) và 21/21 (`ServiceDefaults.UnitTests`).
- [X] T002 Chạy LỆNH-TÌM-A và LỆNH-TÌM-B, lưu danh sách file vào scratchpad của phiên (không vào repo) làm mốc cho T059; đối chiếu với plan.md mục "Phạm vi sửa tài liệu" và ghi file ngoài danh sách vào ghi chú task. **Kết quả**: LỆNH-TÌM-A: 36 dòng (27 trong Postman, 2 ở `12-ha-tang-dung.md`, và 025 QA, 027 quickstart, 029 spec/tasks, 2 drawio, `ket-qua/`); LỆNH-TÌM-B: 4 dòng (`01-lam-quen…`, `specs/006…`, `specs/027…/research.md`, `specs/030…/spec.md`). Lưu ở scratchpad.
- [X] T003 Lưu bản export hiện tại của hai dashboard (`POST /api/saved_objects/_export`, `includeReferencesDeep`) và của 5 rule (`alerts/*.ndjson` hiện có trong repo) vào scratchpad để đối chiếu/quay lui. Ghi id các rule đang có trên Kibana (`GET /api/alerting/rules/_find`). **Kết quả**: đã lưu 2 export dashboard và 2 file rule vào scratchpad; id rule: 50 `4169d562-…`, 75 `f724cf89-…`, 100 `a3581b2a-…`, frozen `a16eed47-…`, 028 `9b0e2c36-…`.

---

## Phase 2: Foundational (kiểm chứng kỹ thuật, điểm dừng)

**Purpose**: Chứng minh V1, V2, V3, V5 ở [research.md](./research.md) trước khi sửa rule/dashboard. **Chặn US1–US3, US5.** Sai bất kỳ mục nào: **DỪNG VÀ HỎI**. Kết quả (đúng/sai, bằng chứng, thời điểm) ghi ở T008. V4 làm ở Phase US3 (cần rule mới đã có).

- [X] T004 Kiểm chứng V2: tạo vài request nghiệp vụ thật (newman folder Postman `00 - Xác thực & phân quyền (Get Token)` rồi `00 - Smoke Flow`, như 030). Truy vấn `POST /_query` trên `traces-generic.otel-default*` hai bản: không loại và có điều kiện D1. Xác nhận: span health (`/health*`) bị loại; span nghiệp vụ và **span `Client` (không có `attributes.url.path`) được giữ**; tổng = tổng không loại trừ số span health. Ghi số liệu. **Kết quả**: ĐÚNG (xem research.md "Kết quả xác minh"): có `COALESCE` giữ 108 span nghiệp vụ (kể cả 44 span Client), thiếu `COALESCE` mất toàn bộ Client.
- [X] T005 Kiểm chứng V3: tạo index tạm `tmp-033-events` (mapping như `slo-error-budget-events`) với vài sự kiện mẫu, chạy ES|QL của rule `error-budget-frozen` (lấy từ file `alerts/error-budget-rules.ndjson`) với `FROM traces-generic.otel-default*, tmp-033-events METADATA _index` (đổi tên index) có thêm điều kiện D1 ngay sau `FROM`; xác nhận các sự kiện **vẫn được giữ** (cột `exhausted_at` có giá trị). Xoá index tạm ngay sau đó (chỉ index do task này tạo). **Kết quả**: ĐÚNG: sự kiện "cạn" vẫn được giữ khi thêm điều kiện loại; đã xoá index tạm.
- [X] T006 Kiểm chứng V1: tạo dashboard thử tạm `tmp-033-probe` (Dashboards API `PUT /api/dashboards/tmp-033-probe`) với một panel Lens `metric` đếm span có `query` KQL `not attributes.url.path : /health*` và một panel cùng cấu hình không có `query`. Đối chiếu số hiển thị (Chrome MCP hoặc trình duyệt trong app) với `COUNT(*)` ES|QL loại `/health*`; xác nhận KQL giữ span thiếu trường `attributes.url.path` (nếu T004 đã có span Client) và kết hợp được với KQL trong công thức (`count(kql='…')`). Thử cú pháp thay thế nếu `/` cần escape. Xoá dashboard thử khi xong. **Kết quả**: ĐÚNG, lưu ý: KQL phải **không nháy** `not attributes.url.path : /health*` (có nháy không loại gì); đã xoá dashboard thử.
- [X] T007 Kiểm chứng V5 (phần phản hồi khi cờ TẮT): với mỗi route trong bảng "Đường dẫn tiêm lỗi" ở đầu file, gửi **một request không có header tiêm lỗi** (tương đương hành vi cờ TẮT) tới service tương ứng qua `curl.exe` (cổng như `scripts/incident-drill.ps1`: gateway 5300, bff 5301, products 5088, baskets 5188, orders 5041, parties 5204, identity 5205; `{guid}` = GUID cố định) và ghi mã phản hồi (ví dụ 200/401/404). **KHÔNG gửi request mang `X-Chaos-Fault: 5xx`** ở task này (sẽ đốt ngân sách): phần có header kiểm ở T049 sau khi **HỎI NGƯỜI DÙNG**. Bảng kết quả dùng cho T047. **Kết quả**: Tất cả route trả 401 khi không có header (identity 200); bảng ghi ở research.md.
- [X] T008 Ghi kết quả V1–V3, V5 (phần không header) vào mục mới "Kết quả xác minh" ở cuối [research.md](./research.md). **Kết quả**: đã ghi kết quả V1, V2, V3, V5 vào research.md.

**Checkpoint**: điều kiện loại, Lens KQL, rule frozen và phản hồi route thay thế đã được chứng minh. Chưa chứng minh được: dừng, hỏi người dùng.

---

## Phase 3: User Story 4 — Quy tắc loại span khai báo trong manifest và có test canh gác (Priority: P2, làm trước US1–US3)

**Goal**: Cả 7 manifest khai `excluded-path-prefixes: [/health]` giống hệt nhau, test báo đỏ khi lệch.

**Independent Test**: `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~ErrorBudgetPolicyTests` xanh; xoá khoá ở một manifest thì test đỏ đúng service đó.

- [X] T009 [US4] Sửa contract `specs/029-error-budget-weekly/contracts/error-budget-policy-manifest-shape.md`: thêm khoá `excluded-path-prefixes: [/health]` vào hình dạng bắt buộc của `error-budget-policy` (ngang hàng `window`, `timezone`), kèm bất biến "cả 7 manifest giống hệt nhau" và dẫn tới `specs/033-exclude-health-spans/contracts/budget-exclusion-contract.md` (Nguyên tắc II: viết trước khi sửa manifest/test).
- [X] T010 [US4] Sửa `tests/ServiceManifestSloConventionTests/ServiceManifestModel.cs`: thêm vào `ErrorBudgetPolicySection` thuộc tính `ExcludedPathPrefixes` (`List<string>?`, `[YamlMember(Alias = "excluded-path-prefixes")]`) cùng comment tiếng Việt dẫn contract.
- [X] T011 [US4] Thêm vào `tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs` test mới (theo khuôn test hiện có trong file, comment `Kiểm tra` / `Lý do` / `Task nguồn: spec 033 — FR-007`): với mỗi service, `ErrorBudgetPolicy.ExcludedPathPrefixes` bằng đúng `["/health"]`. Chạy `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~ErrorBudgetPolicyTests`: PHẢI ĐỎ (7 manifest chưa có khoá). Ghi số test đỏ vào ghi chú task.
- [X] T012 [P] [US4] Thêm `excluded-path-prefixes: [/health]` (kèm comment một dòng dẫn spec 033) vào khối `error-budget-policy` của `services/parties/src/Parties.Api/service-manifest.yaml`, ngay dưới `timezone`.
- [X] T013 [P] [US4] Như T012 cho `services/products/src/Products.Api/service-manifest.yaml`.
- [X] T014 [P] [US4] Như T012 cho `services/baskets/src/Baskets.Api/service-manifest.yaml`.
- [X] T015 [P] [US4] Như T012 cho `services/orders/src/Orders.Api/service-manifest.yaml`.
- [X] T016 [P] [US4] Như T012 cho `services/identity/src/Identity.Api/service-manifest.yaml`.
- [X] T017 [P] [US4] Như T012 cho `services/gateway/src/Gateway.Api/service-manifest.yaml`.
- [X] T018 [P] [US4] Như T012 cho `services/bff/src/Bff.Api/service-manifest.yaml`.
- [X] T019 [US4] Chạy `dotnet test tests/ServiceManifestSloConventionTests`: XANH toàn bộ (gồm test mới T011). Ghi số test.

**Checkpoint**: manifest và test khoá nhất quán; US1/US3 có thể đọc tiền tố từ manifest.

---

## Phase 4: User Story 1 — Ngân sách chỉ tính request nghiệp vụ (Priority: P1) 🎯 MVP

**Goal**: 4 rule 027 và rule 028 loại span health.

**Independent Test**: Quickstart Kịch bản 1–2. Test đỏ trước, xanh sau; trên stack chỉ có health, không rule nào bắn.

### Tests (viết trước, PHẢI đỏ)

- [X] T020 [US1] Thêm vào `tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs` test (Theory trên `error-budget-50/75/100/frozen`, comment `Task nguồn: spec 033 — FR-001, FR-002`): ES|QL của rule có **đúng một** điều kiện `NOT (COALESCE(attributes.url.path, "") LIKE "<tiền tố>*")` cho mỗi tiền tố của `ExcludedPathPrefixes` đọc từ manifest (qua `ServiceManifestFixture`, không hard-code), và điều kiện đó đứng **trước** mọi `EVAL`/`STATS`. Thêm test: rule `error-budget-frozen` vẫn có nhánh đọc `slo-error-budget-events` (không bị điều kiện loại làm mất sự kiện, contract bất biến 6).
- [X] T021 [US1] Thêm vào `tests/ServiceManifestSloConventionTests/IncidentFastDetectionRuleDefinitionTests.cs` test tương tự cho rule `incident-fast-detection` (điều kiện loại đúng tiền tố manifest, `Task nguồn: spec 033 — FR-002`).
- [X] T022 [US1] Chạy `dotnet test tests/ServiceManifestSloConventionTests --filter "FullyQualifiedName~ErrorBudgetRuleDefinitionTests|FullyQualifiedName~IncidentFastDetectionRuleDefinitionTests"`: PHẢI ĐỎ đúng các test mới (rule chưa có điều kiện loại); các test cũ vẫn xanh. Ghi số test đỏ.

### Sửa rule

- [X] T023 [US1] Sửa 3 rule mốc `error-budget-50`, `error-budget-75`, `error-budget-100` trên Kibana đang chạy (`PUT /api/alerting/rule/<id>`, đọc id ở T003): giữ nguyên mọi tham số, chỉ chèn dòng `| WHERE NOT (COALESCE(attributes.url.path, "") LIKE "/health*")` ngay sau dòng `| WHERE @timestamp >= DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours` (gộp bằng `AND` nếu muốn một `WHERE`, nhưng test T020 đọc điều kiện loại như một biểu thức riêng). Lưu ý đã biết: sửa rule 100 có thể ghi lại sự kiện "cạn" cho alert vừa active (QA_Debt 027/029); ghi những gì quan sát được vào ghi chú task.
- [X] T024 [US1] Sửa rule `error-budget-frozen` theo kết quả V3 (T005): chèn điều kiện D1 ngay sau dòng `FROM traces-generic.otel-default*, slo-error-budget-events METADATA _index`; kiểm tra lại bằng truy vấn tay rằng sự kiện vẫn được giữ.
- [X] T025 [US1] Sửa rule `incident-fast-detection` (cùng cách): chèn điều kiện D1 ngay sau `| WHERE @timestamp > NOW() - 5 minutes`.
- [X] T026 [US1] Export lại hai file `docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson` và `docs/kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson` (cách ở `alerts/README.md`, giữ id rule cũ). Chạy `dotnet test tests/ServiceManifestSloConventionTests`: XANH toàn bộ.
- [X] T027 [US1] **HỎI NGƯỜI DÙNG** trước khi tạo lại 7 container API (Kịch bản 2 cần health check lần đầu chậm); chỉ làm khi được xác nhận rõ. Sau đó chờ ≥ 10 phút (hai chu kỳ rule) và xác nhận: 4 rule 027 và rule 028 không bắn, `slo-error-budget-events` không có sự kiện mới, truy vấn mức tiêu hao loại health không trả dòng nào cho service chỉ có health. Ghi số đo (số này chỉ ghi ở QA_Debt T056, không ghi vào file QA). **Kết quả (2026-10-08)**: tạo lại 7 container lúc 13:24:59Z. Health lần đầu chậm không làm rule nào bắn vì health: `slo-error-budget-events` có 14 sự kiện "cạn" lúc 13:26:17Z nhưng toàn bộ từ span nghiệp vụ (7 ngày chỉ có 13–69 span nghiệp vụ/service, gồm 2×504 và các lần gọi nguội của tôi); sau 13:27 không có thêm sự kiện nào từ health. Mẫu số nghiệp vụ nhỏ làm 5 rule bật (ghi ở QA_Debt 033).

**Checkpoint**: rule 027/028 không còn tính health; test canh gác xanh.

---

## Phase 5: User Story 2 — Hai dashboard không tính health check (Priority: P1)

**Goal**: Các panel ES|QL của dashboard Ngân sách tuần và 6 panel Lens đọc traces của dashboard Xử lý sự cố loại span health.

**Independent Test**: Quickstart Kịch bản 3. Số dashboard khớp truy vấn Elasticsearch loại `/health*`; service chỉ có health không hiện dòng.

- [X] T028 [US2] Sửa dashboard `Ngân sách lỗi tuần — 7 service`: (a) saved search `slo-error-budget-consumption` (`PUT /api/saved_objects/search/…`, cả `searchSourceJSON` lẫn `tabs[0]`); (b) các panel by-value "Hạn mức còn lại", "Error-rate theo ngày", "Latency p95 theo ngày", "Tiêu hao lũy kế" bằng `GET /api/dashboards/2a607bf4-…` → chèn điều kiện D1 vào truy vấn ES|QL (đứng ngay sau `WHERE @timestamp >= … AND …` của tuần) → `PUT` lại, giữ nguyên mọi thuộc tính khác (id panel, `time_range now-30d`, điều khiển `?tuan_chon`, bố cục). Hai saved search cảnh báo/cạn không đổi.
- [X] T029 [US2] Sửa dashboard `Xử lý sự cố — 7 service` (cùng cách GET → sửa → PUT): thêm `query` KQL cấp panel (cú pháp chốt ở T006) cho 6 panel Lens đọc traces: "Bảng SLO — 7 service", "5xx theo phút theo service", "Latency p95 theo phút theo service", "Traffic + 401/403 theo phút theo service", "Phân bố status code theo service", "Top endpoint chậm nhất". Không đổi `dotnet.exceptions`, "Lỗi gọi hạ lưu", "Log lỗi gần nhất", "Phát hiện nhanh".
- [X] T030 [US2] Kiểm Kịch bản 3 của [quickstart.md](./quickstart.md): tạo request nghiệp vụ (T004), mở hai dashboard (Chrome MCP hoặc trình duyệt trong app); so từng (service, ngân sách) của mức tiêu hao với truy vấn Elasticsearch loại `/health*` (sai lệch ≤ 2 điểm %); xác nhận Bảng SLO và biểu đồ theo phút không có span health; service chỉ có health không hiện dòng. Ghi số đo vào ghi chú (chỉ ghi ở QA_Debt). **Kết quả**: dashboard `Ngân sách lỗi tuần` mở được trên Kibana thật, bảng mức tiêu hao và hạn mức còn lại đều 28 dòng (7 service × 4 ngân sách, đều có span nghiệp vụ); `Xử lý sự cố` hiện panel `Health lỗi theo service`. Số 5xx nghiệp vụ theo service đối chiếu bằng ES|QL (xem QA_Debt 033).
- [X] T031 [US2] Export lại `docs/kibana-quan-sat-he-thong/dashboards/ngan-sach-loi-tuan.ndjson` và `docs/kibana-quan-sat-he-thong/dashboards/xu-ly-su-co.ndjson` (`dashboards/README.md`). Kiểm tra từng file parse được, truy vấn ngân sách có điều kiện D1, không có id/tên dashboard cũ, `time_range` panel tuần vẫn `now-30d`.

**Checkpoint**: hai dashboard khớp rule và không tính health.

---

## Phase 6: User Story 3 — Chỉ báo riêng cho health lỗi (Priority: P2)

**Goal**: Rule `health-failure` và panel `Health lỗi theo service`.

**Independent Test**: Quickstart Kịch bản 4. Health trả 5xx ≥ 50% trong 5 phút thì rule bắn đúng service; health chậm hoặc dưới 50% thì không.

- [X] T032 [US3] Tạo `tests/ServiceManifestSloConventionTests/HealthFailureRuleTests.cs` theo khuôn `IncidentFastDetectionRuleDefinitionTests.cs` và [contracts/health-failure-rule-contract.md](./contracts/health-failure-rule-contract.md) (bất biến 1–8): đọc `docs/kibana-quan-sat-he-thong/alerts/health-failure-rule.ndjson`; khoá: loại `.es-query`/`esqlQuery`, `groupBy row`, `timeField @timestamp`, tag `health-failure`, chu kỳ `5m`, cửa sổ 5 phút (`NOW() - 5 minutes`); điều kiện chỉ lấy span **có** tiền tố (`COALESCE(attributes.url.path, "") LIKE "<tiền tố>*"`, tiền tố đọc từ manifest, không đảo thành `NOT`); chỉ tính `attributes.http.response.status_code >= 500`; ngưỡng `>= 50`; chỉ giữ cột `service`; `thresholdComparator ">"`, `threshold [0]`; không có action. Chạy test: PHẢI ĐỎ (file export chưa tồn tại). Ghi số test đỏ.
- [X] T033 [US3] Tạo rule trên Kibana (`POST /api/alerting/rule`): tên `health-failure`, tag `health-failure`, `rule_type_id .es-query`, chu kỳ `5m`, `params`: `searchType esqlQuery`, `timeWindowSize 5`, `timeWindowUnit m`, `groupBy row`, `timeField @timestamp`, `size 0`, `thresholdComparator >`, `threshold [0]`, không `actions`; ES|QL:
  `FROM traces-generic.otel-default*` / `| WHERE @timestamp > NOW() - 5 minutes AND COALESCE(attributes.url.path, "") LIKE "/health*"` / `| EVAL service = resource.attributes.service.name, is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0)` / `| STATS health_spans = COUNT(*), health_5xx = SUM(is_5xx) BY service` / `| EVAL health_5xx_pct = TO_DOUBLE(health_5xx) / health_spans * 100.0` / `| WHERE health_5xx_pct >= 50` / `| KEEP service`. Chạy thử truy vấn qua `POST /_query` trước (đã chạy được ở research F5). Enable rule.
- [X] T034 [US3] Export rule ra `docs/kibana-quan-sat-he-thong/alerts/health-failure-rule.ndjson` (Saved Objects Export theo id rule; cùng cách `alerts/README.md`); chạy `dotnet test tests/ServiceManifestSloConventionTests`: XANH (gồm `HealthFailureRuleTests`).
- [X] T035 [US3] Thêm panel `Health lỗi theo service` vào dashboard Xử lý sự cố (GET → sửa → PUT `e61fc7f3-…`), trong section "Tình trạng SLO", **cạnh** panel Phát hiện nhanh (dịch bố cục các panel khác xuống nếu cần, không đổi nội dung): Discover session ES|QL nhúng sẵn, theo thanh thời gian (không `time_range` riêng): `FROM traces-generic.otel-default*` / `| WHERE COALESCE(attributes.url.path, "") LIKE "/health*"` / `| EVAL service = resource.attributes.service.name, is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0)` / `| STATS health_spans = COUNT(*), health_5xx = SUM(is_5xx) BY service` / `| EVAL health_5xx_pct = ROUND(TO_DOUBLE(health_5xx) / health_spans * 100.0, 1)` / `| SORT health_5xx_pct DESC, service`; cột `service`, `health_spans`, `health_5xx`, `health_5xx_pct`. Chỉ tính 5xx.
- [X] T036 [US3] Export lại `docs/kibana-quan-sat-he-thong/dashboards/xu-ly-su-co.ndjson` (có thêm panel mới); kiểm tra parse được và panel có mặt.
- [X] T037 [US3] Kiểm chứng V4 (Kịch bản 4). **HỎI NGƯỜI DÙNG** trước khi tạm dừng DB của một service (ví dụ `products-db`) và nêu hậu quả (health trả 503, vài 5xx health vào dữ liệu, không vào ngân sách). Khi được xác nhận: dừng DB > 5 phút; xác nhận panel `Health lỗi theo service` hiện service với `health_5xx_pct` cao, rule `health-failure` bắn đúng service đó trong một chu kỳ, không rule ngân sách nào bắn; bật lại DB và xác nhận health về 200. Ghi số đo vào ghi chú (chỉ ghi ở QA_Debt). **Kết quả**: dừng `products-db` 13:35:13Z; `/health/ready` 503; 52/52 span health của `Products.Api` 5xx trong 5 phút, 6 service khác 0%; rule `health-failure` bắn lúc 13:42:32Z (~7 phút) **chỉ** cho `Products.Api`; không có sự kiện "cạn" mới từ health; bật lại DB 13:43:3xZ, health 200 lúc 13:44:29Z.
- [X] T038 [US3] Kiểm tra ngược: với dữ liệu hiện có (không có health 5xx hoặc dưới 50%) rule không bắn; ghi trạng thái rule (`GET /api/alerting/rules/_find`, tag `health-failure`) vào ghi chú task. **Kết quả**: trước khi dừng DB, rule `health-failure` ở trạng thái `ok`, chu kỳ 5m, enabled, 0 alert.

**Checkpoint**: service không sẵn sàng vẫn nhìn thấy qua rule và panel riêng, không làm nhiễu ngân sách.

---

## Phase 7: User Story 5 — Postman tiêm lỗi và tài liệu dùng đường dẫn không phải health (Priority: P3)

**Goal**: 26 request Postman đốt được ngân sách bằng route nghiệp vụ; tài liệu nói đúng công thức loại health.

**Independent Test**: Quickstart Kịch bản 5, 6. Folder 29a/30a bật mốc 50/75/100; folder 25 làm p95 `Orders.Api` tăng; LỆNH-TÌM chỉ còn bản ghi lịch sử.

### Postman

- [X] T039 [US5] **HỎI NGƯỜI DÙNG nội dung folder `33 - Health: loại khỏi ngân sách`** (chưa chốt, không suy diễn): phương án gợi ý (a) request tạo request nghiệp vụ + truy vấn đối chiếu số ngân sách loại health, (b) thêm cả kịch bản health lỗi (cần dừng DB), (c) chỉ truy vấn đối chiếu. Làm T045 theo câu trả lời.
- [X] T040 [US5] Đổi 8 request của folder `25 - Diễn tập chaos: tiêm độ trễ orders-api` (25a: 1, 25b: 7) từ `{{ordersUrl}}/health/live` sang route nghiệp vụ của `Orders.Api` (`{{ordersUrl}}/orders/{{unknownGuid}}` hoặc GUID cố định như request 07 hiện có của folder này), giữ header `X-Chaos-Latency-Ms`; cập nhật mô tả/`raw` URL/`host`/`path`. Chỉnh sửa bằng thao tác văn bản (collection có chỗ định dạng tay, CRLF; không serialize lại cả file): chỉ đổi dòng liên quan.
- [X] T041 [US5] Đổi 3 request của folder `27 - Ngân sách lỗi: tiêm 5xx và cảnh báo (error budget)` (27a: 1, 27b: 2) từ `/health/live` sang `{{ordersUrl}}/orders/{{unknownGuid}}`, giữ header `X-Chaos-Fault`; cập nhật mô tả.
- [X] T042 [US5] Đổi 7 request của `29a - Tiêm 5xx 7 service (chạy theo vòng)` sang route từng service theo bảng "Đường dẫn tiêm lỗi" (gateway `/bff/products`, bff `/bff/products`, products `/products`, baskets `/baskets/current`, orders `/orders/{{unknownGuid}}`, parties `/parties/{{unknownGuid}}`, identity `/.well-known/openid-configuration`), giữ header và test `500`; cập nhật mô tả.
- [X] T043 [US5] Đổi 8 request của `30a - Tạo lỗi + traffic cho Xử lý sự cố (chạy theo vòng)` (7 request 5xx và request độ trễ `Orders.Api`) tương tự T042/T040; các request 401/403/hạ lưu không đổi.
- [X] T044 [US5] Sửa assertion "cờ TẮT" của 25a và 27a theo bảng phản hồi đo ở T007 (kỳ vọng mã thật của route mới khi header bị bỏ qua; giữ ý nghĩa "không phải 500 / không bị trễ"); cập nhật mô tả bước "cờ TẮT" ở cả hai folder.
- [X] T045 [US5] Thêm folder `33 - Health: loại khỏi ngân sách` vào `postman/ecommerce.postman_collection.v2.json` (sau folder 30; chèn bằng thao tác văn bản như 030) theo đáp án T039; mỗi request có mô tả mục đích/input/output/FR như các folder khác. Chạy newman folder đó.
- [X] T046 [US5] Kiểm tra `postman/ecommerce.postman_collection.v2.json` parse JSON được và không dòng nào ngoài các request đã nêu bị đổi (`git diff --numstat`).
- [X] T047 [US5] **HỎI NGƯỜI DÙNG** trước khi chạy newman các folder tiêm lỗi (sẽ đốt ngân sách 5xx/độ trễ thật trên stack đang chạy và có thể bật alert 100, ghi sự kiện "cạn", đóng băng service; cờ đang bật sẵn trên container nên không phải tạo lại). Khi được xác nhận: chạy `29a` và `30a` theo vòng (`-n`) và xác nhận ngân sách 5xx của 7 service tăng, mốc 50/75/100 bật; chạy folder `25` xác nhận p95 `Orders.Api` tăng; xác nhận assertion cờ TẮT mới đúng (có thể cần tạo lại container với cờ tắt: hỏi riêng). Ghi số đo (chỉ ghi ở QA_Debt). `scripts/incident-drill.ps1` không đổi. **Kết quả**: đã được người dùng xác nhận. `29a` ×4 vòng: 28/28 assertion; `30a` ×2: lần 1 có 1 lỗi tạm (504 ở luồng hạ lưu ngay sau khi bật lại DB), lần 2 30/30; 5xx nghiệp vụ mỗi service +8 trong 15 phút, mức tiêu hao error-rate 1594–8889%, mốc 50/75/100 active, thêm 12 sự kiện "cạn"; folder `25`: p95 `Orders.Api` 2,51 s, 9 span >1 s. Assertion cờ TẮT mới đúng: tạo lại `orders-api` với cờ tắt (người dùng xác nhận) thì `25a` 01 = `401` 67 ms, `27a` 01 = `401` 50 ms; đã bật lại cờ theo `.env`. `25b` 08 đỏ sẵn từ trước (thiếu token khi chạy riêng folder, trả 401 thay 404).

### Tài liệu cũ sửa tại chỗ (FR-012)

Với mỗi file: thay phần mô tả công thức "tính mọi span" hoặc hướng dẫn tiêm lỗi vào `/health*` bằng mô tả đúng ("không tính span có đường dẫn bắt đầu bằng `/health`", "tiêm lỗi vào route nghiệp vụ"); giữ nguyên nội dung ngoài đó. Không sửa bản ghi lịch sử (mục cũ `docs/QA/QA_Debt.md`, `docs/dien-tap-chaos-engineering/ket-qua/`, `specs/002-gateway-bff-routing/`).

- [X] T048 [P] [US5] Sửa tài liệu Kibana: `docs/kibana-quan-sat-he-thong/06-dashboard-xu-ly-su-co-va-ngan-sach-tuan.md` (hai dashboard không tính health, panel mới `Health lỗi theo service`), `07-canh-bao-ngan-sach-loi.md` (truy vấn chuẩn có điều kiện loại, khoá manifest), `08-phat-hien-nhanh-va-xu-ly-su-co.md`, `12-ha-tang-dung.md` (nếu nhắc tiêm lỗi vào health), `alerts/README.md` (thêm rule `health-failure`, file export mới), `dashboards/README.md`.
- [X] T049 [P] [US5] Sửa tài liệu 025: `docs/QA/025_QA_diễn tập chaos giết pod tiêm độ trễ.md`, `docs/architecture/025_Architect_*`, `docs/development/025_Development_*`, `docs/diagrams/025-chaos-pod-kill-latency-{component,sequence}.drawio` (mở file trước, chỉ sửa nhãn chữ, giữ bố cục, XML hợp lệ), `specs/025-chaos-pod-kill-latency/{contracts/chaos-latency-injection-contract,data-model,plan,quickstart,research,tasks}.md` nếu nhắc `/health/live` làm đường dẫn tiêm.
- [X] T050 [P] [US5] Sửa tài liệu 027: `docs/QA/027_QA_*`, `docs/architecture/027_Architect_*`, `docs/PO/027_PO_*` (nếu nhắc công thức), drawio `027-error-budget-alerting-{component,sequence}`, `specs/027-error-budget-alerting/{contracts/chaos-fault-injection-contract,data-model,quickstart,research,tasks}.md` (hướng dẫn `X-Chaos-Fault` vào `/health/live` đổi sang route nghiệp vụ; mô tả công thức loại health).
- [X] T051 [P] [US5] Sửa tài liệu 028/029/030: `docs/architecture/028_Architect_*`, `docs/PO/028_PO_*`, drawio `028-incident-oncall-drill-component`, `docs/QA/029_QA_*`, `docs/architecture/029_Architect_*`, `docs/PO/029_PO_*`, drawio `029-error-budget-weekly-{component,sequence}`, `docs/QA/030_QA_*`, `docs/architecture/030_Architect_*`, `docs/PO/030_PO_*`, drawio `030-incident-and-weekly-dashboards-{component,sequence}`, `specs/028-incident-oncall-drill/*`, `specs/029-error-budget-weekly/{contracts,quickstart,research,tasks}.md` và `specs/030-incident-and-weekly-dashboards/{contracts,quickstart,research,tasks}.md` — chỉ sửa chỗ nhắc công thức hoặc tiêm lỗi vào health; contract `specs/028-incident-oncall-drill/contracts/fast-detection-rule-contract.md` và `specs/027-error-budget-alerting/contracts/error-budget-alert-rules-contract.md` thêm bất biến "loại span `/health*`".
- [X] T052 [P] [US5] Sửa các tài liệu troubleshoot nhắc tiêm lỗi vào health hoặc công thức ngân sách (từ spec 031/032): `docs/kibana-quan-sat-he-thong/10-*.md`, `11-*.md`, `17-*.md`, `docs/QA/032_QA_*`, `docs/architecture/031_Architect_*` (chỉ chỗ liên quan; chạy lại test convention của 032 `tests/TroubleshootGuideConventionTests` nếu có sửa các file đó và xác nhận xanh).

### Bộ tài liệu 033 (FR-014)

- [X] T053 [P] [US5] Tạo `docs/PO/033_PO_loại span health khỏi ngân sách lỗi.md` theo khuôn `docs/PO/030_PO_*.md` (ngôn ngữ nghiệp vụ: vì sao health check làm nhiễu ngân sách, ngân sách nay chỉ tính request nghiệp vụ, cảnh báo health lỗi riêng, giới hạn).
- [X] T054 [P] [US5] Tạo `docs/architecture/033_Architect_loại span health khỏi ngân sách lỗi.md` theo khuôn `docs/architecture/030_Architect_*.md`: bằng chứng F1–F10, điều kiện `COALESCE` (F3), khoá manifest, rule `health-failure`, panel, Postman, quyết định D1–D8, kết quả V1–V5; mục `## Sơ đồ` với 3 link (dạng `%20`) tới 3 drawio T056–T058.
- [X] T055 [P] [US5] Tạo `docs/QA/033_QA_loại span health khỏi ngân sách lỗi.md` theo khuôn QA 024–030: **Thủ công trước, Tự động sau**; bảng thủ công điều khiển bằng công tắc `.env`/compose và folder Postman 25/27/29a/30a/33 (không cột số đo thật); bảng Tự động link từng test tới **dòng khai báo** (`#L…`) trong `ErrorBudgetPolicyTests.cs`, `ErrorBudgetRuleDefinitionTests.cs`, `IncidentFastDetectionRuleDefinitionTests.cs`, `HealthFailureRuleTests.cs`; mọi **phát hiện chỉ ghi ở `QA_Debt.md`**, không có khối "Nguồn đối chiếu".
- [X] T056 [P] [US5] Tạo `docs/diagrams/033-health-exclusion-component.drawio`. Trước khi vẽ, mở `docs/diagrams/030-incident-and-weekly-dashboards-component.drawio` để chép bố cục, kiểu ô/màu, nhãn tiếng Việt, dạng `<diagram id=...>`. Nội dung: health check Docker → span health bị loại; request nghiệp vụ → ngân sách (5 rule + hai dashboard); health 5xx → rule `health-failure` + panel; manifest `excluded-path-prefixes` ↔ test.
- [X] T057 [P] [US5] Tạo `docs/diagrams/033-health-exclusion-flow-nghiep-vu.drawio` theo mẫu `030-…-flow-nghiep-vu.drawio` (ngôn ngữ nghiệp vụ, không tên công cụ): container khởi động lại, kiểm tra sức khoẻ chậm lần đầu → không tính vào ngân sách; request thật xấu → ngân sách; service không sẵn sàng → cảnh báo riêng.
- [X] T058 [P] [US5] Tạo `docs/diagrams/033-health-exclusion-sequence.drawio` theo mẫu `030-…-sequence.drawio`: trình tự thật của T027/T030/T037/T047 (tạo lại container, health chậm, rule không bắn, tạo request nghiệp vụ, dashboard khớp, dừng DB → rule `health-failure` bắn).
- [X] T059 [US5] Chạy lại LỆNH-TÌM-A và LỆNH-TÌM-B, so với mốc T002: mọi file còn lại phải thuộc nhóm "không sửa" (mục cũ QA_Debt, `ket-qua/`, `specs/002`), spec 033, hoặc ghi chú lịch sử có chủ đích. Ghi danh sách còn lại vào ghi chú task (SC-006). **Kết quả**: LỆNH-TÌM-A còn 6 file, đều có chủ đích: `docs/QA/025_QA_*` (ghi chú lịch sử số đo 61 ms của `/health/live`), `ket-qua/2026-09-14-inject-latency.md` (bản ghi lịch sử, không sửa), `12-ha-tang-dung.md` (dừng DB rồi gọi `/health/ready`, không phải tiêm lỗi), Postman (ghi chú "spec 033 thay `/health/live`" và request đối chứng `27b` 02), `specs/029` spec/tasks (ghi chú "ban đầu `/health/live`; spec 033 đổi"). LỆNH-TÌM-B còn `01-lam-quen-kibana-discover.md` (bài Discover không nói ngân sách), `specs/006`, `specs/030/spec.md` (không nói công thức), và chính bộ 033; `specs/027/research.md` đã sửa.
- [X] T060 [US5] Thêm mục `## 033 — Loại span health khỏi công thức ngân sách lỗi` vào cuối: `docs/QA/QA_Debt.md` (phát hiện từ T004–T008, T027, T030, T037, T047; số đo thật; cập nhật dòng tiêu đề "(001-030)" → "(001-033)" hoặc số hiện hành; không sửa mục cũ), `docs/architecture/technical-debt.md` (giới hạn: health chậm không báo ở đâu; tiền tố `/health` loại cả endpoint nghiệp vụ trùng tiền tố; `LIKE` phân biệt hoa thường; phụ thuộc `attributes.url.path`; mẫu số nghiệp vụ nhỏ ở tuần đầu vẫn làm phần trăm vọt; đóng ghi chú 030 về health), `docs/PO/functional-debt.md` (cả hai phần của file, link tới file PO T053 dạng `%20`). **Kết quả**: thêm mục 033 vào `QA_Debt.md` (đổi tiêu đề 001-033), `technical-debt.md` (bullet 033, tiêu đề 001-033), `functional-debt.md` (hai phần).

**Checkpoint**: kịch bản tiêm lỗi chạy được; tài liệu nói đúng; bộ tài liệu 033 đủ.

---

## Phase 8: Polish & kiểm tra cuối

- [X] T061 Chạy `dotnet test tests/ServiceManifestSloConventionTests` và `dotnet test shared/ServiceDefaults.UnitTests`, ghi số test xanh (SC-004). Nếu có sửa file troubleshoot ở T052: chạy thêm `dotnet test tests/TroubleshootGuideConventionTests`. **Kết quả**: `ServiceManifestSloConventionTests` 129/129, `ServiceDefaults.UnitTests` 21/21, `TroubleshootGuideConventionTests` 38/38 xanh.
- [X] T062 Kiểm tra link trong các file Markdown mới/sửa (T048–T060): link tương đối trỏ tới file có thật (giải mã `%20`), link `#L…` của QA 033 trỏ đúng dòng khai báo test. Ghi số file/số link đã kiểm; lỗi có sẵn từ trước ngoài phạm vi chỉ ghi nhận, không sửa. **Kết quả**: 37 file Markdown đổi/mới, 419 link kiểm; 0 link hỏng do spec này (1 link cũ `/app/dashboards` trong 030 Architect là đường dẫn Kibana, không phải file); `#L…` của QA 033 lấy từ dòng khai báo thật.
- [X] T063 Kiểm tra file JSON/ndjson đã sửa/tạo parse được từng dòng/từng file: `alerts/error-budget-rules.ndjson`, `alerts/incident-fast-detection-rule.ndjson`, `alerts/health-failure-rule.ndjson`, hai ndjson dashboard, `postman/ecommerce.postman_collection.v2.json`; các drawio mới/sửa XML hợp lệ. **Kết quả**: 5 ndjson (Kibana) parse từng dòng, Postman `json.load` OK, drawio 033 + 025/027/029 `minidom.parse` OK.
- [X] T064 Import hai ndjson dashboard và các ndjson rule vào Kibana sạch (space tạm `tmp033` bằng `POST /api/spaces/space`, như 030; id có thể bị đổi trong space khác nên chỉ kiểm hiển thị và truy vấn), chạy lại Kịch bản 3 liền mạch; xoá space tạm khi xong. Không đốt lại ngân sách nếu import đặt lại trạng thái alert (**HỎI NGƯỜI DÙNG** nếu cần). **Kết quả**: tạo space tạm `tmp033`, import 2 dashboard + 3 file rule (`success: true`, 4+5+5+1+1 object, 0 lỗi), thấy 2 dashboard; đã xoá space.
- [X] T065 Xoá mọi object tạm do spec này tạo (index `tmp-033-events` T005, dashboard `tmp-033-probe` T006, space `tmp033` T064 nếu còn) và xác nhận không còn: liệt kê `GET /api/saved_objects/_find?type=dashboard`, `GET /_cat/indices?h=index`. **Kết quả**: không còn index `tmp-033*`, space `tmp033` đã xoá, không dashboard tạm; Kibana còn đúng 6 rule và 2 dashboard 030.
- [X] T066 **HỎI NGƯỜI DÙNG** có dọn toàn bộ Elastic/đặt lại stack bây giờ không (FR-015). Chỉ thực hiện khi người dùng xác nhận rõ ràng ngay lúc đó và nêu chính xác lệnh/volume sẽ xoá (và hậu quả: mất dữ liệu traces/logs/alert, dashboard/rule phải import lại). **Kết quả**: người dùng chọn xoá. Dừng và xoá `elasticsearch`/`kibana`/`otel-collector`, xoá volume `ecomerce-local_local-es-data`, dựng lại cả ba (healthy); chỉ còn 3 data stream mới (55/387/46 document), Kibana 0 dashboard, 0 rule. Cần import lại ndjson và bật rule theo README khi muốn dùng.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1**: không phụ thuộc.
- **Phase 2**: sau Phase 1. Chặn US1, US2, US3, US5 (cần V1, V2, V3, V5). US4 chỉ cần T001.
- **Phase 3 (US4)**: chỉ cần Phase 1; làm **trước** Phase 4–6 vì rule/test đọc tiền tố từ manifest. T009 → T010/T011 → T012–T018 → T019.
- **Phase 4 (US1)**: cần Phase 2 (V2, V3) và Phase 3. T020–T022 (test đỏ) → T023–T025 (sửa rule) → T026 (export + test xanh) → T027.
- **Phase 5 (US2)**: cần Phase 2 (V1, V2) và Phase 3; độc lập với US1 về file nhưng nên sau T026 để số khớp rule.
- **Phase 6 (US3)**: cần Phase 3; T035/T036 cần T029/T031 (cùng dashboard Xử lý sự cố, làm tuần tự); T037 cần T033–T035.
- **Phase 7 (US5)**: Postman T040–T046 cần T007 (bảng phản hồi) và T039; T047 cần T040–T045 và US1; tài liệu T048–T052 cần giá trị cuối của Phase 3–6; T053–T058 sau khi có kết quả thật; T059–T060 cuối cùng.
- **Phase 8**: sau Phase 3–7.

### Within Each User Story

- Test viết/sửa trước, PHẢI đỏ (T011, T022, T032) trước khi sửa manifest/rule tương ứng.
- Sửa Kibana đang chạy → export file → test xanh.
- Dashboard: GET → sửa → PUT (giữ nguyên mọi thuộc tính khác) → kiểm → export.

### Parallel Opportunities

- T012–T018: 7 manifest, song song hoàn toàn.
- T020 ∥ T021 (hai file test khác nhau).
- T040–T043: bốn folder khác nhau trong cùng một file JSON → **làm tuần tự** (cùng file), không đánh [P].
- T048–T052: khác file, song song. T053–T058: song song.

---

## Parallel Example: User Story 4

```text
Task: "T012 services/parties/src/Parties.Api/service-manifest.yaml"
Task: "T013 services/products/src/Products.Api/service-manifest.yaml"
Task: "T014 services/baskets/src/Baskets.Api/service-manifest.yaml"
Task: "T015 services/orders/src/Orders.Api/service-manifest.yaml"
Task: "T016 services/identity/src/Identity.Api/service-manifest.yaml"
Task: "T017 services/gateway/src/Gateway.Api/service-manifest.yaml"
Task: "T018 services/bff/src/Bff.Api/service-manifest.yaml"
```

## Parallel Example: User Story 5 (tài liệu)

```text
Task: "T048 tài liệu Kibana 06/07/08/12 + READMEs"   Task: "T049 tài liệu 025"
Task: "T050 tài liệu 027"                              Task: "T051 tài liệu 028/029/030"
Task: "T053 PO 033"   Task: "T054 Architect 033"   Task: "T055 QA 033"
Task: "T056 drawio component"   Task: "T057 drawio flow"   Task: "T058 drawio sequence"
```

---

## Implementation Strategy

### MVP First (US4 → US1)

1. Phase 1 → Phase 2 (kiểm chứng) → Phase 3 (manifest + test) → Phase 4 (rule loại health).
2. **DỪNG và kiểm tra**: quickstart Kịch bản 1–2. Từ đây ngân sách 027/028 không còn tính health.

### Incremental Delivery

1. Setup + Phase 2 → biết kỹ thuật nào dùng được; sai thì dừng, hỏi người dùng.
2. US4 → khoá manifest + test.
3. US1 → rule 027/028 loại health (MVP).
4. US2 → hai dashboard khớp rule.
5. US3 → rule và panel `health-failure`.
6. US5 → Postman, tài liệu, bộ tài liệu 033.
7. Polish → kiểm tra cuối, hỏi trước khi dọn Elastic.
