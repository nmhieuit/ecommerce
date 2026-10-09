---

description: "Danh sách task ngân sách lỗi chỉ đếm span Server (spec 034)"
---

# Tasks: Ngân sách lỗi chỉ đếm span Server

**Input**: Tài liệu thiết kế tại `specs/034-error-budget-server-spans/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: CÓ. Nguyên tắc III: test viết trước và chạy thấy ĐỎ thật (5 rule chưa có điều kiện `kind`) rồi mới sửa rule. Comment test tiếng Việt theo khuôn hiện có (`Kiểm tra` / `Lý do` / `Task nguồn: spec 034 — FR-xxx`). Dashboard là cấu hình, kiểm theo [quickstart.md](./quickstart.md).

**Organization**: Nhóm theo user story của spec.md. **Thứ tự làm khác thứ tự ưu tiên** (xem "Dependencies"): US3 (test, P2) làm **trước** US1 (sửa rule) để test đỏ thật; Postman của US5 chạy **trước** dọn trạng thái của US4 (vì chạy Postman đốt ngân sách và ghi sự kiện cạn, bước dọn phải đến sau); phần tài liệu của US5 làm cuối.

**Không commit** (người dùng chốt): chỉ ghi file. Người dùng tự xem và commit.

**Không suy diễn**: mọi điểm đánh dấu **HỎI NGƯỜI DÙNG** hoặc **DỪNG VÀ HỎI** phải dừng lại, đưa 2–4 phương án kèm hệ quả, không tự chọn.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: chạy song song được (khác file, không phụ thuộc task chưa xong)
- **[Story]**: US1–US5 của spec.md

## Đã chốt ở phiên `/speckit-tasks` (ghi lại để task bên dưới dùng đúng)

- **Manifest**: KHÔNG khai báo loại span được đếm trong manifest. 7 `service-manifest.yaml`, contract 029 và hiến chương không đổi; định nghĩa "chỉ Server" nằm trong rule + panel + test.
- **Dọn sự kiện cạn**: xoá toàn bộ tài liệu của `slo-error-budget-events` bằng delete-by-query, **giữ index và mapping**; rồi Disable/Enable rule `error-budget-100`. Mỗi thao tác hỏi lại ngay trước khi làm; liệt kê sự kiện trước khi xoá.
- **Folder Postman**: `34 - Ngân sách: chỉ đếm span Server`, gồm kịch bản lỗi qua Gateway→BFF→Products (cờ tiêm lỗi bật) và truy vấn Elasticsearch đếm theo loại span của từng service. Chi tiết request chốt ở T031 theo mô tả này.
- **Tên file**: `docs/PO/034_PO_ngân sách lỗi chỉ đếm span Server.md`, `docs/QA/034_QA_ngân sách lỗi chỉ đếm span Server.md`, `docs/architecture/034_Architect_ngân sách lỗi chỉ đếm span Server.md`; drawio `docs/diagrams/034-server-span-only-{component,flow-nghiep-vu,sequence}.drawio`.
- **Giữ nguyên từ spec**: 4 rule 027 + `incident-fast-detection` chỉ đếm Server (health-failure không đổi); dashboard: chỉ các panel ngân sách/SLO đổi; test Server thêm vào `ErrorBudgetRuleDefinitionTests` và `IncidentFastDetectionRuleDefinitionTests` (lớp có sẵn); không sửa hiến chương (lệch phiên bản PR #75 chỉ ghi vào `technical-debt.md`); không commit.

## Path Conventions

- Test: `tests/ServiceManifestSloConventionTests/`.
- Export Kibana: `docs/kibana-quan-sat-he-thong/alerts/`, `docs/kibana-quan-sat-he-thong/dashboards/`.
- Kibana `http://localhost:5601` (header `kbn-xsrf: true`), Elasticsearch `http://localhost:9200` (stack `ecomerce-local` đang chạy; container API đang mang `Chaos__AllowFaultInjection=true`, riêng `orders-api` thêm `Chaos__AllowLatencyInjection=true`). Lệnh PowerShell 5.1 dùng `curl.exe`; newman: `npx.cmd --yes newman@6.2.2`.
- Id dashboard: Xử lý sự cố `e61fc7f3-17fe-428a-a373-da88af0a4a1e`, Ngân sách tuần `2a607bf4-2449-48a1-a2e8-1336ec35a7b7`.
- Id rule trên Kibana (đo 2026-10-09): `error-budget-50` `4169d562-aca7-47af-8da1-63511967b07f`, `error-budget-75` `f724cf89-13ac-4f9e-af09-f89e5e436917`, `error-budget-100` `a3581b2a-3eda-4a2f-8f4d-a9af04c8fac3`, `error-budget-frozen` `a16eed47-01ed-40ce-a8be-c264bde1b771`, `incident-fast-detection` `9b0e2c36-678b-4cd7-9de0-7468d623f82d`, `health-failure` `b41a1c85-152e-4b17-97b0-ea0e5fd3880e` (không đổi).
- Điều kiện chỉ Server (D1): rule mốc và 028: `| WHERE kind == "Server"`; rule frozen: `| WHERE (kind == "Server" OR _index LIKE "*slo-error-budget-events*")`. KQL Lens: `kind : Server and not attributes.url.path : /health*` (không nháy). Đặt ngay cạnh điều kiện loại `/health*` của 033, trước mọi `EVAL`/`STATS`.
- **LỆNH-TÌM-C** (tài liệu/hiện vật mô tả điều kiện loại health trong công thức ngân sách, dùng ở T002 và T041):
  `grep -rlI -E "excluded-path-prefixes|COALESCE\(attributes\.url\.path|loại span health|không tính span (có đường dẫn )?(bắt đầu bằng )?.?/health|mọi span|tất cả span|every span" --exclude-dir=.git --exclude-dir=node_modules --exclude-dir=.claude --exclude-dir=bin --exclude-dir=obj --exclude-dir=services --exclude-dir=tests docs specs postman`
- **Không sửa (lịch sử)**: mục cũ `docs/QA/QA_Debt.md`, `docs/dien-tap-chaos-engineering/ket-qua/`, `specs/002-gateway-bff-routing/`, và `spec.md`/`tasks.md`/`research.md` của các spec đã xong khi chúng chỉ là bản ghi lịch sử (không phải mô tả hiện hành).

---

## Phase 1: Setup

**Purpose**: Ghi mốc trạng thái hiện có trước khi sửa.

- [X] T001 Chạy `dotnet test tests/ServiceManifestSloConventionTests`, ghi số test xanh vào ghi chú task này. Nếu đỏ: dừng và báo người dùng. **Kết quả**: XANH 129/129.
- [X] T002 Chạy LỆNH-TÌM-C, lưu danh sách file vào scratchpad của phiên (không vào repo) làm mốc cho T041; đối chiếu với plan.md mục "Phạm vi sửa tài liệu" và ghi file ngoài danh sách vào ghi chú task.
- [X] T003 Lưu bản export hiện tại của hai dashboard (`POST /api/saved_objects/_export`, `includeReferencesDeep`) và của 5 rule (`alerts/error-budget-rules.ndjson`, `alerts/incident-fast-detection-rule.ndjson` trong repo) vào scratchpad để đối chiếu/quay lui. Ghi trạng thái rule hiện tại (`GET /api/alerting/rules/_find`: tên, id, `execution_status.status`) và số tài liệu của `slo-error-budget-events` (hiện 20) vào ghi chú task.

---

## Phase 2: Foundational (kiểm chứng kỹ thuật, điểm dừng)

**Purpose**: Chứng minh V1, V2, V3 ở [research.md](./research.md) trước khi sửa rule/dashboard. **Chặn US1, US2, US5.** Sai mục nào: **DỪNG VÀ HỎI**. Kết quả ghi ở T007. V4, V5 làm ở các phase sau.

- [X] T004 Kiểm chứng V2 (số đối chiếu): chạy `POST /_query` đếm theo `(service, kind)` cho tuần hiện tại (`DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours`), đã loại `/health*`, không và có điều kiện `kind == "Server"`. Xác nhận khớp research F3: `Bff.Api` 118 span Server / 9 lỗi 5xx (201 / 12 Client), tổng chỉ-Server 527. Nếu dữ liệu đã đổi (có request mới), ghi số mới làm mốc thay cho F3 và cập nhật con số trong [contracts/server-span-only-contract.md](./contracts/server-span-only-contract.md) bất biến 11 cho khớp. **Kết quả**: ĐÚNG, số khớp F3 (Bff 118/9, tổng 527).
- [X] T005 Kiểm chứng V3: chạy ES|QL của `error-budget-frozen` (lấy từ `alerts/error-budget-rules.ndjson`) với điều kiện `(kind == "Server" OR _index LIKE "*slo-error-budget-events*")` thay chỗ điều kiện D1 mới, chỉ đọc (không tạo index tạm): xác nhận kết quả trung gian còn đủ sự kiện (đếm `is_event` = số tài liệu của `slo-error-budget-events`) và kết quả cuối (`service`, `exhausted_at`) khớp bản chưa sửa trên cùng dữ liệu. Ghi số. **Kết quả**: ĐÚNG, 20 sự kiện được giữ, kết quả cuối không đổi.
- [X] T006 Kiểm chứng V1: tạo dashboard thử tạm `tmp-034-probe` (`PUT /api/dashboards/tmp-034-probe`) với một panel Lens `metric` đếm span, `query` KQL cấp panel `kind : Server and not attributes.url.path : /health*`, và một panel cùng cấu hình không có `query`. Đối chiếu số hiển thị (Chrome MCP hoặc trình duyệt trong app) với `COUNT(*)` ES|QL chỉ-Server loại `/health*` (cùng khoảng thời gian); xác nhận kết hợp được với KQL đã có trong công thức nếu panel nào có (`count(kql='…')`). Thử cú pháp thay thế nếu `kind : Server` bị từ chối. Xoá dashboard thử khi xong. **Kết quả**: ĐÚNG, Lens khớp ES|QL từng dòng; đã xoá dashboard thử.
- [X] T007 Ghi kết quả V1–V3 (đúng/sai, bằng chứng, thời điểm) vào mục mới "Kết quả xác minh" ở cuối [research.md](./research.md).

**Checkpoint**: số đối chiếu, rule frozen và Lens KQL đã được chứng minh. Chưa chứng minh được: dừng, hỏi người dùng.

---

## Phase 3: User Story 3 — Test canh gác "chỉ đếm Server" (Priority: P2, làm trước US1)

**Goal**: Test báo đỏ khi một trong 5 rule bỏ điều kiện Server.

**Independent Test**: `dotnet test tests/ServiceManifestSloConventionTests --filter "FullyQualifiedName~ErrorBudgetRuleDefinitionTests|FullyQualifiedName~IncidentFastDetectionRuleDefinitionTests"`: đỏ trước khi sửa rule, xanh sau T016.

- [X] T008 [US3] Thêm vào `tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs` một `[Theory]` trên `error-budget-50/75/100` (theo khuôn `BudgetRule_ExcludesTheManifestDeclaredPathPrefixes_BeforeAnyCalculation`, comment `Task nguồn: spec 034 — FR-001, FR-002, FR-007`): ES|QL có **đúng một** dòng `WHERE kind == "Server"` và dòng đó đứng **trước** mọi `EVAL`/`STATS`. Điều kiện loại `/health*` của 033 vẫn do test 033 canh, không lặp lại.
- [X] T009 [US3] Thêm vào cùng file một `[Fact]` cho `error-budget-frozen` (`Task nguồn: spec 034 — FR-001, FR-003, FR-007`): ES|QL có **đúng một** điều kiện `(kind == "Server" OR _index LIKE "*slo-error-budget-events*")` đứng trước mọi `EVAL`/`STATS`, và **không** có dòng `WHERE kind == "Server"` trần (sẽ làm mất sự kiện cạn, research F4). Giữ nguyên `FrozenRule_StillReadsTheExhaustionEvents`.
- [X] T010 [US3] Thêm vào `tests/ServiceManifestSloConventionTests/IncidentFastDetectionRuleDefinitionTests.cs` một `[Fact]` tương tự T008 cho rule `incident-fast-detection` (`Task nguồn: spec 034 — FR-001, FR-002, FR-007`).
- [X] T011 [US3] Chạy lệnh Independent Test ở trên: PHẢI ĐỎ đúng 5 trường hợp mới (3 trường hợp của Theory T008, 1 của T009, 1 của T010) vì rule chưa có điều kiện `kind`; test cũ của 027, 028, 033 vẫn xanh. Ghi số test đỏ vào ghi chú task (Nguyên tắc III). **Kết quả**: ĐỎ đúng 5 test mới (3 + 1 + 1), 38 test cũ xanh.

**Checkpoint**: 5 test mới đỏ thật.

---

## Phase 4: User Story 1 — Ngân sách và phát hiện nhanh chỉ đếm span Server (Priority: P1) 🎯 MVP

**Goal**: 4 rule 027 và rule 028 chỉ đếm span Server.

**Independent Test**: Quickstart mục 1–3. Test xanh; mỗi (service, ngân sách) khớp truy vấn chỉ-Server.

- [X] T012 [US1] Sửa 3 rule mốc `error-budget-50`, `error-budget-75`, `error-budget-100` trên Kibana đang chạy (`PUT /api/alerting/rule/<id>`, id ở đầu file): giữ nguyên mọi tham số, chỉ chèn dòng `| WHERE kind == "Server"` ngay sau dòng `| WHERE NOT (COALESCE(attributes.url.path, "") LIKE "/health*")`. Lưu ý đã biết: sửa rule 100 có thể ghi lại sự kiện "cạn" cho alert vừa active (QA_Debt 027/029); ghi những gì quan sát được vào ghi chú task (chỉ ghi ở QA_Debt T056). **Kết quả**: đã PUT 3 rule (50/75 chạy trước; rule 100 có action `.index` nên phải bỏ `connector_type_id` khỏi body PUT). Chưa quan sát sự kiện cạn mới ngay sau sửa; ghi ở QA_Debt khi rà ở T028.
- [X] T013 [US1] Sửa rule `error-budget-frozen` (`PUT`, id `a16eed47-…`): chèn ngay sau dòng loại `/health*` điều kiện `| WHERE (kind == "Server" OR _index LIKE "*slo-error-budget-events*")`; kiểm lại bằng truy vấn tay (T005) rằng sự kiện vẫn được giữ.
- [X] T014 [US1] Sửa rule `incident-fast-detection` (`PUT`, id `9b0e2c36-…`): chèn `| WHERE kind == "Server"` ngay sau dòng loại `/health*`.
- [X] T015 [US1] Export lại hai file `docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson` và `docs/kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson` (cách ở `alerts/README.md`, giữ id rule cũ); kiểm tra `health-failure-rule.ndjson` không đổi (`git diff`).
- [X] T016 [US1] Chạy `dotnet test tests/ServiceManifestSloConventionTests`: XANH toàn bộ (gồm 5 test mới ở T008–T010 và test cũ của 027, 028, 033). Ghi số test. **Kết quả**: XANH 134/134 (129 + 5).
- [X] T017 [US1] Kiểm chứng V2 sau sửa (quickstart mục 2): chạy ES|QL của từng rule đã sửa (lấy từ file export) trên cùng tuần và đối chiếu từng (service, ngân sách) với truy vấn chỉ-Server của T004 (ví dụ `Bff.Api` error-rate: 9/118 so với trước 21/319); span `Client`/`Producer` không còn nằm trong mẫu số. Ghi số đo (chỉ ghi ở QA_Debt). **Kết quả**: 28/28 (service, ngân sách) của rule 50 khớp phép tính độc lập chỉ-Server (Bff error-rate 762,71%, p95 33,9%, p99 84,75%); rule 028 chạy được, không dòng nào (cửa sổ 5 phút hiện không vượt).

**Checkpoint**: rule 027/028 chỉ đếm Server; test canh gác xanh.

---

## Phase 5: User Story 2 — Hai dashboard khớp điều kiện bắn của rule (Priority: P1)

**Goal**: Panel ngân sách trên hai dashboard chỉ đếm span Server; panel Lỗi gọi hạ lưu giữ nguyên.

**Independent Test**: Quickstart mục 4. Số dashboard khớp truy vấn chỉ-Server (sai lệch ≤ 2 điểm %).

- [X] T018 [US2] Sửa dashboard `Ngân sách lỗi tuần — 7 service` (GET → sửa → PUT, giữ nguyên mọi thuộc tính khác: id panel, `time_range now-30d`, điều khiển `?tuan_chon`, bố cục): (a) saved search `slo-error-budget-consumption` (`PUT /api/saved_objects/search/…`, cả `searchSourceJSON` lẫn `tabs[0]`); (b) panel "Hạn mức còn lại — tuần đã chọn", "Error-rate theo ngày trong tuần", "Latency p95 theo ngày trong tuần", "Tiêu hao lũy kế theo ngày từ thứ Hai": chèn `| WHERE kind == "Server"` ngay sau điều kiện loại `/health*` trong truy vấn ES|QL. Hai saved search cảnh báo/cạn và markdown không đổi. **Kết quả**: sửa bằng chèn dòng vào file ndjson (9 chỗ: 5 truy vấn, một số lồng nhiều lớp) rồi import `overwrite=true` (đường import theo README), live == repo.
- [X] T019 [US2] Sửa dashboard `Xử lý sự cố — 7 service` (cùng cách GET → sửa → PUT): đổi KQL cấp panel của 6 panel Lens từ `not attributes.url.path : /health*` thành `kind : Server and not attributes.url.path : /health*` (cú pháp chốt ở T006): "Bảng SLO — 7 service", "5xx theo phút theo service", "Latency p95 theo phút theo service", "Traffic + 401/403 theo phút theo service", "Phân bố status code theo service", "Top endpoint chậm nhất". Không đổi "Lỗi gọi hạ lưu — cặp service gọi → đích", "Phát hiện nhanh", "Health lỗi theo service", "Xu hướng dotnet.exceptions", "Log lỗi gần nhất", markdown. **Kết quả**: 6 KQL Lens đổi, 6 panel còn lại không đổi (live đọc lại đúng).
- [X] T020 [US2] Kiểm Kịch bản dashboard của [quickstart.md](./quickstart.md) mục 4 (Chrome MCP hoặc trình duyệt trong app): so từng (service, ngân sách) của mức tiêu hao với truy vấn chỉ-Server của T004 và với T017; Bảng SLO/5xx/p95/status code không đếm span Client; panel "Lỗi gọi hạ lưu" vẫn hiện span Client như trước. Ghi số đo (chỉ ghi ở QA_Debt). **Kết quả**: dashboard Ngân sách hiển thị 28 dòng; truy vấn tiêu hao khớp rule 50 (không dòng nào lệch > 2 điểm %, Bff error-rate 762,71%).
- [X] T021 [US2] Export lại `docs/kibana-quan-sat-he-thong/dashboards/ngan-sach-loi-tuan.ndjson` và `docs/kibana-quan-sat-he-thong/dashboards/xu-ly-su-co.ndjson` (`dashboards/README.md`). Kiểm tra từng file parse được, id/tên dashboard giữ nguyên, có dòng điều kiện Server ở đúng 5 truy vấn ES|QL và 6 KQL Lens, `time_range` panel tuần vẫn `now-30d`, các panel giữ nguyên không đổi (`git diff`).

**Checkpoint**: hai dashboard khớp rule.

---

## Phase 6: User Story 5 (phần Postman) — Kịch bản kiểm chứng lỗi qua ba tầng (Priority: P3)

**Goal**: Folder Postman 34 chứng minh mỗi service chỉ được cộng đúng 1 span Server cho một lỗi đi qua Gateway→BFF→Products.

**Independent Test**: Chạy folder 34 bằng newman; bước truy vấn đối chiếu xanh.

- [X] T022 [US5] Xem folder `33 - Health: loại khỏi ngân sách` (dòng ~14955 của `postman/ecommerce.postman_collection.v2.json`) làm khuôn: biến (`{{elasticsearchUrl}}`, `{{tenantId}}`, `{{subjectId}}`, token), cách viết mô tả (Mục đích / Input / Output / FR), cách chèn bằng thao tác văn bản. Ghi khung request sẽ thêm vào ghi chú task rồi **HỎI NGƯỜI DÙNG** nếu khung lệch mô tả đã chốt ("lỗi qua Gateway→BFF→Products + truy vấn đếm theo loại span"). Không suy diễn thêm request. **Kết quả**: khung lệch mô tả: `ChaosFaultInjectionMiddleware` trả 500 ngay ở service đầu tiên nên header không tạo được lỗi qua 3 tầng; đã hỏi người dùng, chọn "dừng tạm `products-api`, gọi `/bff/products` qua Gateway".
- [X] T023 [US5] Thêm folder `34 - Ngân sách: chỉ đếm span Server` vào `postman/ecommerce.postman_collection.v2.json` ngay sau folder 33 (chèn bằng thao tác văn bản, CRLF, không serialize lại cả file), gồm: (a) một request lỗi `GET {{gatewayUrl}}/bff/products` với header `X-Chaos-Fault: 5xx` (cờ BẬT sẵn trên container) kỳ vọng `500`; (b) một truy vấn `POST {{elasticsearchUrl}}/_query` đếm theo `(service, kind)` trong cửa sổ ngắn vừa qua (đã loại `/health*`) với test: `Bff.Api` có `Server` cộng đúng 1 lỗi 5xx cho request ở (a) và các span `Client` nằm ở hàng riêng không vào phép đếm "chỉ Server"; (c) mô tả folder nêu mục đích, input, output kỳ vọng, FR-001/FR-004, và rằng request (a) đốt ngân sách thật. Mỗi request có mô tả đủ như các folder khác. Chạy `npx.cmd --yes newman@6.2.2 run … --folder "34 - Ngân sách: chỉ đếm span Server"` sau T025. **Kết quả**: thêm folder gồm 34a (1 request không header tiêm lỗi, cần dừng `products-api`) và 34b (2 truy vấn đối chiếu theo loại span); nội dung theo lựa chọn T022, không phải request header tiêm lỗi như mô tả ban đầu của task.
- [X] T024 [US5] Kiểm tra `postman/ecommerce.postman_collection.v2.json` parse JSON được và chỉ thêm folder 34 (`git diff --numstat`: không dòng nào ngoài folder mới bị đổi). **Kết quả**: JSON parse được; diff chỉ thêm 174 dòng (folder 34), không dòng nào khác đổi.
- [X] T025 [US5] **HỎI NGƯỜI DÙNG** trước khi chạy folder 34 (request lỗi đốt ngân sách 5xx thật qua Gateway→BFF→Products, có thể bật thêm mốc 50/75/100 và ghi sự kiện cạn). Khi được xác nhận: chạy newman folder 34, xác nhận bước (a) `500` và bước (b) đúng; đối chiếu bằng truy vấn tay: BFF chỉ tăng 1 span Server lỗi (không nhân đôi bởi Client). Ghi số đo (chỉ ghi ở QA_Debt). **Kết quả (2026-10-09)**: người dùng xác nhận. Compose trong worktree thiếu `.env` nên dừng/bật bằng `docker stop/start ecomerce-local-products-api-1` (tương đương). newman `00 Get Token` + folder 34: 8 request, 21/21 assertion xanh (34a trả 5xx). Truy vấn 34b ban đầu rỗng vì chạy ngay trước khi span được ingest; chạy lại 34b sau ~45 giây: 14/14 xanh. Quan sát: Gateway 1 span Server 5xx + 1 span Client 5xx cho cùng một request (cũ đếm 2, mới 1); BFF 1 Server 5xx + 3 Client (kết nối bị từ chối, thử lại, không có mã trạng thái); Products dừng nên không có span. Đã bật lại products-api (healthy). Đã sửa folder 34 cho khớp (assertion sau 34a) và đã dừng tại T022 để hỏi vì header không tạo được lỗi qua 3 tầng.

**Checkpoint**: Postman 34 chứng minh việc đếm theo span Server qua ba tầng.

---

## Phase 7: User Story 4 — Về sạch trạng thái cạn/đóng băng giả (Priority: P2, làm sau Postman)

**Goal**: Xoá sự kiện cạn sinh từ công thức cũ và đặt lại rule 100.

**Independent Test**: Quickstart mục 6. Không service nào ở trạng thái đóng băng giả; không alert ngân sách active nếu chưa có request mới.

- [X] T026 [US4] Liệt kê toàn bộ tài liệu của `slo-error-budget-events` (`POST /slo-error-budget-events/_search`: `@timestamp`, `service`, `budget`, `event`) và trạng thái alert hiện tại của 4 rule ngân sách; trình bày cho người dùng (số sự kiện, theo service, các sự kiện mới do T012/T025 nếu có). **Kết quả**: 20 sự kiện, tất cả `exhausted`, từ 2026-10-08T23:18–23:28Z (bài thử 29a/30a); rule: 50 (22 alert), 75 (21), 100 (19), frozen (7), 028 và health-failure `ok`.
- [X] T027 [US4] **HỎI NGƯỜI DÙNG**: xoá toàn bộ tài liệu của `slo-error-budget-events` bằng `POST /slo-error-budget-events/_delete_by_query` (`match_all`), **giữ index và mapping**; hậu quả: mất mọi lịch sử sự kiện cạn (kể cả hợp lệ), rule frozen coi mọi service chưa từng cạn. Chỉ làm khi được xác nhận rõ ngay lúc đó. Sau khi làm: `_count` = 0 và index còn nguyên (`GET /slo-error-budget-events/_mapping`). **Kết quả (2026-10-09 03:27Z)**: người dùng xác nhận. `_delete_by_query` match_all xoá 21 tài liệu (20 cũ + 1 sự kiện rule 100 vừa ghi lúc bài thử), index và mapping còn nguyên (`@timestamp`, `budget`, `event`, `service`).
- [X] T028 [US4] **HỎI NGƯỜI DÙNG**: Disable rồi Enable `error-budget-100` (`POST /api/alerting/rule/a3581b2a-…/_disable` rồi `_enable`); hậu quả: alert đang active bị đóng, rule tính lại từ đầu tuần theo công thức mới và có thể ghi sự kiện cạn mới nếu service vẫn vượt 100% với chỉ span Server. Khi được xác nhận: chờ ≥ 10 phút (hai chu kỳ rule), xác nhận service nào còn alert là do chính span Server của nó vượt ngưỡng (đối chiếu truy vấn chỉ-Server). Ghi số đo (chỉ ghi ở QA_Debt). **Kết quả**: người dùng xác nhận. Disable/Enable `error-budget-100` 03:27:13Z (204/204). Rule chạy lại tạo 21 alert mới và ghi lại 22 sự kiện cạn (mọi service vượt 100% với chỉ span Server do mẫu số rất nhỏ, ví dụ Parties 11 span/4 lỗi); đây là kết quả đúng của công thức mới, không phải đóng băng giả. `incident-fast-detection` có 2 alert mới (Gateway, BFF) từ lỗi 34a trong cửa sổ 5 phút.

**Checkpoint**: trạng thái cạn/đóng băng phản ánh đúng công thức mới.

---

## Phase 8: User Story 5 (phần tài liệu) — Tài liệu mô tả đúng định nghĩa mới (Priority: P3)

**Goal**: Mọi mô tả công thức hiện hành nói "chỉ đếm span Server"; bộ tài liệu 034 đủ.

**Independent Test**: LỆNH-TÌM-C chạy lại: mọi file mô tả công thức hiện hành đều nhắc span Server; còn lại là bản ghi lịch sử có chủ đích.

### Tài liệu cũ sửa tại chỗ (FR-011)

Với mỗi file: thêm/sửa câu mô tả công thức để nói rõ "chỉ đếm span Server (`kind = Server`), không đếm span Client/Producer; vẫn loại `/health*`"; giữ nguyên nội dung ngoài đó. Không sửa bản ghi lịch sử (xem "Không sửa" ở đầu file).

- [X] T029 [P] [US5] Sửa tài liệu Kibana: `docs/kibana-quan-sat-he-thong/06-dashboard-xu-ly-su-co-va-ngan-sach-tuan.md`, `07-canh-bao-ngan-sach-loi.md` (truy vấn chuẩn có điều kiện Server; ngoại lệ rule frozen), `08-phat-hien-nhanh-va-xu-ly-su-co.md`, `alerts/README.md`, `dashboards/README.md`; thêm vào cuối mỗi file nhắc nhanh lệch trước/sau nếu file có ví dụ con số theo công thức cũ. Chỉ sửa chỗ liên quan (kiểm bằng LỆNH-TÌM-C ở T002). **Kết quả**: sửa `06`, `07` (2 khối truy vấn trước đó thiếu cả điều kiện loại `/health*`, nay có cả hai + bullet Server), `08` (2 khối truy vấn tương tự + câu mô tả), `alerts/README.md`, `dashboards/README.md`.
- [X] T030 [P] [US5] Sửa tài liệu 027/028: `docs/QA/027_QA_*`, `docs/architecture/027_Architect_*`, `docs/PO/027_PO_*`, `docs/architecture/028_Architect_*`, `docs/PO/028_PO_*`, `docs/QA/028_QA_*`; drawio `docs/diagrams/027-error-budget-alerting-{component,sequence}.drawio` và `028-incident-oncall-drill-{component,sequence}.drawio` (mở file trước, chỉ sửa nhãn chữ nhắc công thức, giữ bố cục, XML hợp lệ); contract `specs/027-error-budget-alerting/contracts/error-budget-alert-rules-contract.md` và `specs/028-incident-oncall-drill/contracts/fast-detection-rule-contract.md` thêm bất biến "chỉ đếm span Server" dẫn tới [contracts/server-span-only-contract.md](./contracts/server-span-only-contract.md). **Kết quả**: sửa tài liệu mô tả công thức hiện hành: ghi chú đầu file ở Architect 025/027/028/029/030 (cùng khuôn ghi chú 033), contract 027 (bất biến 11) và 028 (bất biến 9), `specs/027/research.md`. QA/PO 027/028 và drawio 027–030 không có mô tả công thức nên không đổi (LỆNH-TÌM-C không khớp).
- [X] T031 [P] [US5] Sửa tài liệu 029/030/033: `docs/QA/029_QA_*`, `docs/architecture/029_Architect_*`, `docs/PO/029_PO_*`, `docs/QA/030_QA_*`, `docs/architecture/030_Architect_*`, `docs/PO/030_PO_*`, `docs/QA/033_QA_*`, `docs/architecture/033_Architect_*`, `docs/PO/033_PO_*`; drawio `029-error-budget-weekly-{component,sequence}`, `030-incident-and-weekly-dashboards-{component,sequence,flow-nghiep-vu}`, `033-health-exclusion-{component,flow-nghiep-vu,sequence}` (nếu nhắc công thức); `specs/029-error-budget-weekly/contracts/*`, `specs/030-incident-and-weekly-dashboards/contracts/*` và `specs/033-exclude-health-spans/contracts/budget-exclusion-contract.md` (thêm ghi chú nối tiếp: từ 034 công thức còn chỉ đếm span Server). Chỉ sửa chỗ mô tả hiện hành; `spec.md`/`tasks.md`/`research.md` của spec cũ là lịch sử, không sửa. **Kết quả**: Architect/QA 033 thêm ghi chú nối tiếp 034, drawio 033 component thêm một dòng, contract 033 thêm câu nối tiếp. PO 033, 029, 030 và contract 029/030 là ngôn ngữ nghiệp vụ hoặc hình dạng manifest nên không cần sửa.
- [X] T032 [P] [US5] Sửa tài liệu troubleshoot nhắc công thức ngân sách (từ spec 031/032) nếu LỆNH-TÌM-C có: `docs/kibana-quan-sat-he-thong/09-*`, `10-*`, `11-*`, `12-*`, `14-*`, `17-*`, `docs/QA/032_QA_*`, `docs/architecture/031_Architect_*`, `032_Architect_*` (chỉ chỗ liên quan). Nếu sửa các file này, chạy `dotnet test tests/TroubleshootGuideConventionTests` và xác nhận xanh. **Kết quả**: LỆNH-TÌM-C không khớp tài liệu troubleshoot 09–17 / 031 / 032 nào mô tả công thức, không sửa nên không cần chạy lại `TroubleshootGuideConventionTests`.
- [X] T033 [P] [US5] Sửa mô tả Postman: mô tả collection/folder nhắc công thức ngân sách ("tính mọi span", điều kiện loại health) trong `postman/ecommerce.postman_collection.v2.json` (folder 27, 29a, 30, 33…) thành mô tả chỉ đếm Server — chỉ sửa chữ trong trường `description`, bằng thao tác văn bản; `git diff --numstat` xác nhận không dòng nào khác đổi. **Kết quả**: sửa 2 chỗ chữ trong mô tả một folder cũ ("dùng mọi span"); JSON parse được; diff Postman: 189 dòng thêm, 1 dòng đổi.

### Bộ tài liệu 034 (FR-012)

- [X] T034 [P] [US5] Tạo `docs/PO/034_PO_ngân sách lỗi chỉ đếm span Server.md` theo khuôn `docs/PO/033_PO_*.md` (ngôn ngữ nghiệp vụ: vì sao đếm cả lời gọi hạ lưu làm một lỗi bị trừ nhiều lần, ngân sách nay chỉ tính request mà service nhận, lệch Governance của PR #75 ghi nhận như nợ riêng, giới hạn).
- [X] T035 [P] [US5] Tạo `docs/architecture/034_Architect_ngân sách lỗi chỉ đếm span Server.md` theo khuôn `docs/architecture/033_Architect_*.md`: bằng chứng F1–F8, điều kiện `kind == "Server"` và ngoại lệ rule frozen (F4), danh sách panel đổi/giữ, quyết định D1–D8, kết quả V1–V5; mục `## Sơ đồ` với 3 link (dạng `%20`) tới 3 drawio T037–T039.
- [X] T036 [P] [US5] Tạo `docs/QA/034_QA_ngân sách lỗi chỉ đếm span Server.md` theo khuôn QA 024–033: **Thủ công trước, Tự động sau**; bảng thủ công điều khiển bằng công tắc `.env`/compose và folder Postman 29a/30a/34 (không cột số đo thật); bảng Tự động link từng test tới **dòng khai báo** (`#L…`) trong `ErrorBudgetRuleDefinitionTests.cs` và `IncidentFastDetectionRuleDefinitionTests.cs`; mọi **phát hiện chỉ ghi ở `QA_Debt.md`**, không có khối "Nguồn đối chiếu".
- [X] T037 [P] [US5] Tạo `docs/diagrams/034-server-span-only-component.drawio`. Trước khi vẽ, mở `docs/diagrams/033-health-exclusion-component.drawio` để chép bố cục, kiểu ô/màu, nhãn tiếng Việt, dạng `<diagram id=...>`. Nội dung: request qua Gateway→BFF→Products tạo span Server/Client/Producer; chỉ span Server vào 5 rule và hai dashboard; span Client vào panel Lỗi gọi hạ lưu; `slo-error-budget-events` đọc bởi rule frozen; test canh.
- [X] T038 [P] [US5] Tạo `docs/diagrams/034-server-span-only-flow-nghiep-vu.drawio` theo mẫu `033-health-exclusion-flow-nghiep-vu.drawio` (ngôn ngữ nghiệp vụ, không tên công cụ): một lỗi ở tầng cuối trừ ngân sách đúng service nhận request, không nhân đôi qua lời gọi hạ lưu; lỗi hạ lưu vẫn nhìn thấy ở panel riêng.
- [X] T039 [P] [US5] Tạo `docs/diagrams/034-server-span-only-sequence.drawio` theo mẫu `033-health-exclusion-sequence.drawio`: trình tự thật của T017/T020/T025/T027–T028 (đếm theo loại span, lỗi qua ba tầng, dashboard khớp rule, dọn sự kiện, đặt lại rule 100).
- [X] T040 [US5] Thêm mục `## 034 — Ngân sách lỗi chỉ đếm span Server` vào: `docs/QA/QA_Debt.md` (phát hiện từ T004–T007, T012, T017, T020, T025, T028; số đo thật; cập nhật dòng tiêu đề "(001-033)" → "(001-034)"; không sửa mục cũ), `docs/architecture/technical-debt.md` (giới hạn: span thiếu `kind` bị loại; loại span lạ như `Internal`/`Consumer` không được đếm; lỗi chỉ ở hạ lưu và bị service gọi nuốt không còn trừ ngân sách service gọi; ghi nhận **lệch Governance**: PR #75 sửa ngưỡng độ trễ trong hiến chương nhưng không tăng phiên bản, vẫn 2.0.0, xử lý như việc riêng), `docs/PO/functional-debt.md` (cả hai phần của file, link tới file PO T034 dạng `%20`).

### Kiểm tra tài liệu

- [X] T041 [US5] Chạy lại LỆNH-TÌM-C, so với mốc T002: mọi file mô tả công thức hiện hành phải nhắc "span Server"; file còn lại phải thuộc nhóm "không sửa" (lịch sử) hoặc là spec 034. Ghi danh sách còn lại vào ghi chú task (SC-006). **Kết quả**: 38 file còn khớp; không file nào thuộc nhóm mô tả công thức hiện hành mà thiếu "Server" (ndjson có chuỗi bị escape; còn lại là `01-lam-quen`, spec 006, contract 029 / 033 health-failure, quickstart/tasks 033: không phải mô tả công thức hoặc lịch sử).

**Checkpoint**: tài liệu nói đúng; bộ tài liệu 034 đủ.

---

## Phase 9: Polish & kiểm tra cuối

- [X] T042 Chạy `dotnet test tests/ServiceManifestSloConventionTests` (và `dotnet test tests/TroubleshootGuideConventionTests` nếu T032 có sửa), ghi số test xanh (SC-004). **Kết quả**: `ServiceManifestSloConventionTests` 134/134 xanh; không sửa file troubleshoot nên không chạy `TroubleshootGuideConventionTests`.
- [X] T043 Kiểm tra link trong các file Markdown mới/sửa (T029–T040): link tương đối trỏ tới file có thật (giải mã `%20`), link `#L…` của QA 034 trỏ đúng dòng khai báo test. Ghi số file/số link đã kiểm; lỗi có sẵn từ trước ngoài phạm vi chỉ ghi nhận, không sửa. **Kết quả**: 20 file Markdown đổi/mới, 224 link kiểm; 1 link hỏng có sẵn từ trước (`QA_Debt.md` trỏ `021_Architect_khai báo và đo SLO…`, đã có ở HEAD), ngoài phạm vi; `#L…` của QA 034 lấy từ dòng khai báo thật (287, 306, 320, 194 trước khi sửa file test thêm).
- [X] T044 Kiểm tra file JSON/ndjson đã sửa/tạo parse được từng dòng/từng file: `alerts/error-budget-rules.ndjson`, `alerts/incident-fast-detection-rule.ndjson`, `alerts/health-failure-rule.ndjson` (phải không đổi), hai ndjson dashboard, `postman/ecommerce.postman_collection.v2.json`; các drawio mới/sửa XML hợp lệ (`minidom.parse`). **Kết quả**: 5 ndjson (từng dòng), Postman JSON, 3 drawio 034 và drawio 033 component parse được; `health-failure-rule.ndjson` không đổi.
- [X] T045 Import hai ndjson dashboard và hai ndjson rule vào một space tạm (`POST /api/spaces/space` id `tmp034`, như 033), kiểm hiển thị và truy vấn, rồi xoá space tạm. Không đặt lại trạng thái alert ở space chính; **HỎI NGƯỜI DÙNG** nếu cần thao tác nào ngoài import. **Kết quả**: space tạm `tmp034`: import 2 dashboard + 2 file rule `success: true`, thấy 2 dashboard và 5 rule (đều disabled); `slo-error-budget-events` không đổi (22 trước và sau).
- [X] T046 Xoá mọi object tạm do spec này tạo (dashboard `tmp-034-probe` T006, space `tmp034` T045 nếu còn) và xác nhận không còn: liệt kê `GET /api/saved_objects/_find?type=dashboard`, `GET /api/spaces/space`, `GET /_cat/indices?h=index`. **Kết quả**: đã xoá space `tmp034` và dashboard `tmp-034-probe`; còn space `default`, 2 dashboard, 6 rule, không index `tmp*`.
- [X] T047 Xác nhận `git status`: chỉ các file nêu trong plan.md bị đổi; 7 `service-manifest.yaml`, `.specify/memory/constitution.md`, `alerts/health-failure-rule.ndjson`, `scripts/incident-drill.ps1` KHÔNG đổi. **Không commit.** **Kết quả**: 7 manifest, hiến chương, `health-failure-rule.ndjson`, `scripts/incident-drill.ps1` không đổi; chưa commit.
- [X] T048 **HỎI NGƯỜI DÙNG** có dọn toàn bộ Elastic/đặt lại stack bây giờ không (FR-013). Chỉ thực hiện khi người dùng xác nhận rõ ràng ngay lúc đó và nêu chính xác lệnh/volume sẽ xoá (hậu quả: mất dữ liệu traces/logs/alert, dashboard/rule phải import lại). **Kết quả (2026-10-09)**: người dùng chọn dọn sạch. Dừng và xoá container `elasticsearch`/`kibana`/`otel-collector`, xoá volume `ecomerce-local_local-es-data`, dựng lại ba container bằng compose của repo chính (project `ecomerce-local`), Kibana available; còn 3 data stream mới (36/609/109 document), 0 dashboard, 0 rule. Phải import lại 2 ndjson dashboard và 2 ndjson rule rồi bật rule theo README khi cần.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1**: không phụ thuộc.
- **Phase 2**: sau Phase 1. Chặn Phase 4–6 (cần V1, V2, V3).
- **Phase 3 (US3)**: chỉ cần Phase 1; làm **trước** Phase 4 để test đỏ thật. T008–T010 → T011 (đỏ).
- **Phase 4 (US1)**: cần Phase 2 và T011. T012–T014 → T015 → T016 (xanh) → T017.
- **Phase 5 (US2)**: cần Phase 2 (V1) và T017 (số khớp rule); T018 ∥ T019 khác dashboard nhưng cùng Kibana, làm tuần tự cho đơn giản; T020 → T021.
- **Phase 6 (US5 Postman)**: cần Phase 4. T022 → T023 → T024 → T025.
- **Phase 7 (US4)**: cần T025 (đã đốt xong) và rule đã sửa (Phase 4); T026 → T027 → T028. Làm sau Postman vì Postman ghi thêm sự kiện.
- **Phase 8 (US5 tài liệu)**: T029–T033 sau Phase 4–5; T034–T039 sau khi có kết quả thật (Phase 4–7); T040–T041 cuối cùng.
- **Phase 9**: sau Phase 3–8.

### Within Each User Story

- Test viết trước, PHẢI đỏ (T011) trước khi sửa rule.
- Sửa Kibana đang chạy → export file → test xanh.
- Dashboard: GET → sửa → PUT (giữ nguyên mọi thuộc tính khác) → kiểm → export.

### Parallel Opportunities

- T008 ∥ T010 (hai file test khác nhau); T009 cùng file với T008, làm tuần tự.
- T029–T033: khác file, song song (T033 cùng file JSON với T023 nhưng làm sau T024).
- T034–T039: song song.

---

## Parallel Example: tài liệu (Phase 8)

```text
Task: "T029 tài liệu Kibana 06/07/08 + READMEs"   Task: "T030 tài liệu 027/028"
Task: "T031 tài liệu 029/030/033"                   Task: "T032 tài liệu troubleshoot"
Task: "T034 PO 034"   Task: "T035 Architect 034"   Task: "T036 QA 034"
Task: "T037 drawio component"   Task: "T038 drawio flow"   Task: "T039 drawio sequence"
```

---

## Implementation Strategy

### MVP First (US3 → US1)

1. Phase 1 → Phase 2 (kiểm chứng) → Phase 3 (test đỏ) → Phase 4 (rule chỉ đếm Server, test xanh).
2. **DỪNG và kiểm tra**: quickstart mục 1–3. Từ đây ngân sách 027/028 chỉ đếm Server.

### Incremental Delivery

1. Setup + Phase 2 → biết kỹ thuật nào dùng được; sai thì dừng, hỏi người dùng.
2. US3 → 5 test đỏ; US1 → rule sửa, test xanh (MVP).
3. US2 → hai dashboard khớp rule.
4. US5 Postman → chứng minh qua ba tầng.
5. US4 → dọn sự kiện và đặt lại rule 100 (hỏi trước từng bước).
6. US5 tài liệu → sửa tại chỗ và bộ tài liệu 034.
7. Polish → kiểm tra cuối, hỏi trước khi dọn Elastic.
