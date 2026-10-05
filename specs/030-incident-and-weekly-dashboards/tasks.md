---

description: "Danh sách task tách dashboard SLO thành Xử lý sự cố và Ngân sách lỗi tuần (spec 030)"
---

# Tasks: Tách dashboard SLO thành "Xử lý sự cố" và "Ngân sách lỗi tuần"

**Input**: Tài liệu thiết kế tại `specs/030-incident-and-weekly-dashboards/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: CÓ, nhưng chỉ ở hai chỗ có hành vi kiểm được bằng mã: test canh gác rule `incident-fast-detection` (FR-015) và, **nếu V4 báo thiếu**, test ghi log trong `ServiceDefaults` (FR-019). Nguyên tắc III: mỗi test PHẢI chạy thấy ĐỎ trước khi xanh. Dashboard là cấu hình, kiểm bằng [quickstart.md](./quickstart.md) (người dùng chốt không thêm test đọc file dashboard). Comment test tiếng Việt theo khuôn hiện có (`Kiểm tra` / `Lý do` / `Task nguồn: spec 030 … — FR-xxx`).

**Organization**: Nhóm theo user story của spec.md.

**Không commit** (người dùng chốt): chỉ ghi file. Người dùng tự xem và commit.

**Không suy diễn**: mọi điểm đánh dấu **HỎI NGƯỜI DÙNG** hoặc **DỪNG VÀ HỎI** phải dừng lại, đưa 2–4 phương án kèm hệ quả, không tự chọn.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: chạy song song được (khác file, không phụ thuộc task chưa xong)
- **[Story]**: US1–US4 của spec.md

## Đã chốt ở phiên `/speckit-tasks` (ghi lại để task bên dưới dùng đúng)

- **Tên file ndjson**: `docs/kibana-quan-sat-he-thong/dashboards/xu-ly-su-co.ndjson`, `docs/kibana-quan-sat-he-thong/dashboards/ngan-sach-loi-tuan.ndjson`.
- **Folder Postman**: folder cha `30 - Hai dashboard: tạo lỗi và đối chiếu số liệu` gồm hai folder con `30a - Tạo lỗi + traffic cho Xử lý sự cố (chạy theo vòng)` và `30b - Đối chiếu ngân sách tuần (chạy một lần)` (người dùng chọn phương án "30 + hai con 30a/30b"; các tên này là tên ví dụ nêu trong phương án đó, người dùng chọn nguyên phương án).
- **File hướng dẫn Kibana**: viết lại `06-dashboard-slo-van-hanh-hang-ngay.md` cho hai dashboard và **đổi tên** thành `06-dashboard-xu-ly-su-co-va-ngan-sach-tuan.md`; sửa mọi link trỏ tới tên cũ.
- **3 drawio**: `docs/diagrams/030-incident-and-weekly-dashboards-component.drawio`, `…-flow-nghiep-vu.drawio`, `…-sequence.drawio`.
- **File PO/QA/Architect**: `docs/PO/030_PO_hai dashboard xử lý sự cố và ngân sách tuần.md`, `docs/QA/030_QA_hai dashboard xử lý sự cố và ngân sách tuần.md`, `docs/architecture/030_Architect_hai dashboard xử lý sự cố và ngân sách tuần.md`.
- **`remaining_requests`**: hiển thị số âm khi vượt; làm tròn xuống số nguyên (`FLOOR`).
- **Không thêm** test đọc file dashboard.
- **Id hai dashboard**: UUID mới tạo một lần (T008), ghi vào [contracts/dashboards-contract.md](./contracts/dashboards-contract.md).
- **Tiêu đề panel và section**: tôi đề xuất, người dùng duyệt ở T017 và T029 trước khi tạo.

## Path Conventions

- Export Kibana: `docs/kibana-quan-sat-he-thong/dashboards/`, `docs/kibana-quan-sat-he-thong/alerts/`.
- Kibana `http://localhost:5601`, Elasticsearch `http://localhost:9200` (stack `ecomerce-local` đang chạy). Lệnh PowerShell 5.1 dùng `curl.exe` (xem `dashboards/README.md`).
- Test: `tests/ServiceManifestSloConventionTests/`, `shared/ServiceDefaults.UnitTests/`.
- Lệnh tìm tham chiếu dashboard cũ, gọi là **LỆNH-TÌM** (dùng ở T002 và T062):
  `grep -rlI -E "e2e06ff5|slo-van-hanh-hang-ngay|SLO vận hành hằng ngày" --exclude-dir=.git --exclude-dir=node_modules --exclude-dir=.claude --exclude-dir=bin --exclude-dir=obj .`

---

## Phase 1: Setup

**Purpose**: Ghi mốc trạng thái trước khi sửa.

- [X] T001 Chạy `dotnet test tests/ServiceManifestSloConventionTests` và `dotnet test shared/ServiceDefaults.UnitTests`, xác nhận cả hai xanh, ghi số test vào ghi chú task này. Nếu đỏ: dừng và báo người dùng. **Kết quả**: XANH 103/103 (`ServiceManifestSloConventionTests`) và 21/21 (`ServiceDefaults.UnitTests`).
- [X] T002 Chạy LỆNH-TÌM, lưu danh sách file vào scratchpad của phiên (không vào repo) làm mốc cho T062. Đối chiếu với danh sách ở plan.md mục "Phạm vi sửa tài liệu trỏ tới dashboard cũ"; file nào có trong kết quả mà plan chưa nêu thì ghi vào ghi chú task. **Kết quả**: 55 file (gồm 6 file của spec 030); khớp danh sách plan, không có file ngoài danh sách. Lưu ở scratchpad.
- [X] T003 Lưu bản export dashboard cũ hiện đang chạy trên Kibana (cách như `dashboards/README.md`) vào scratchpad của phiên (không vào repo) để đối chiếu lúc chép panel và để quay lui nếu cần. Ghi `id` các Lens/Discover session của panel 5–6, 9–11 để chép đúng. **Kết quả**: đã export dashboard cũ vào scratchpad (7 object) để chép panel; panel cũ đều là Lens by-value, không có object Lens riêng.

---

## Phase 2: Foundational (kiểm chứng kỹ thuật trên Kibana 9.4.4 thật, điểm dừng)

**Purpose**: Chứng minh V1–V8 ở [research.md](./research.md) trước khi dựng dashboard. **Chặn US1–US3.** Sai bất kỳ mục nào: **DỪNG VÀ HỎI**, không tự đổi cách làm. Kết quả (đúng/sai, bằng chứng, thời điểm) ghi ở T012.

- [X] T004 Kiểm chứng V4 (log trong request có correlation id và trace id không). Gây một log mức Error trong một request thật của một service (chọn cách gây, ví dụ endpoint ném ngoại lệ chưa bắt, ở môi trường local; chế độ tiêm 5xx của 027 trả 500 từ middleware nên có thể không sinh log Error). Truy vấn: `FROM logs-generic.otel-default* | WHERE severity_number >= 17 | SORT @timestamp DESC | LIMIT 5` và xem `trace_id` cùng trường correlation id (có thể là `attributes.CorrelationId`). Ghi tên trường thật tìm được. Nếu không gây được log Error trong request: **DỪNG VÀ HỎI**. **Kết quả**: ĐÚNG, không cần sửa code: 11/11 log Error trong request có `trace_id` và `attributes.CorrelationId` (xem research.md "Kết quả xác minh").
- [X] T005 Kiểm chứng V1 và V2 (bộ chọn tuần). Trên Kibana tạo **dashboard thử tạm** (id/tên có tiền tố `tmp-030`, xoá sau T012) với điều khiển ES|QL kiểu "giá trị từ truy vấn" cho biến `?week_start` bằng truy vấn danh sách tuần ở research.md D3; thêm một Discover session ES|QL và một Lens ES|QL dùng `TO_DATETIME(?week_start)`. Xác nhận hiển thị đúng khi đổi tuần, rồi export dashboard thử và import vào Kibana khác/đã xoá object để chứng minh điều khiển và panel biến import lại được. Sai: **DỪNG VÀ HỎI**. **Kết quả**: V1, V2 ĐÚNG (điều khiển ES|QL + Lens ES|QL + Discover ES|QL dùng biến). Dựng bằng Dashboards REST API 9.4.4 thay vì giao diện.
- [X] T006 Kiểm chứng V3 (thanh thời gian). Trên dashboard thử tạm: Discover session ES|QL `FROM traces-generic.otel-default* | STATS c=COUNT(*) BY resource.attributes.service.name` (không điều kiện thời gian) đổi kết quả khi đổi thanh thời gian giữa 15 phút và 24 giờ; làm tương tự cho truy vấn trên `.alerts-stack.alerts-default` (như panel Phát hiện nhanh). Nếu không đổi, thử biến `?_tstart`/`?_tend`; không được thì **DỪNG VÀ HỎI**. **Kết quả**: V3 ĐÚNG kèm hệ quả: Discover ES|QL bị thanh thời gian cắt; panel cần `time_range` riêng để thoát (dùng cho dashboard tuần).
- [X] T007 Kiểm chứng V5, V7, V8 trên dashboard thử tạm: (V5) tạo index-pattern `logs-generic.otel-default*` với định dạng URL cho `trace_id` trỏ `/app/apm/link-to/trace/{{value}}`, thêm Discover session cổ điển trên nó (lọc `severity_number >= 17`, 50 dòng) và bấm link: phải mở **trace thật** từ dữ liệu `generic.otel` (cần có log Error có trace_id từ T004; nếu chưa có, dùng một `trace_id` span thật để thử link); export/import giữ nguyên định dạng URL. (V7) thêm panel Links trỏ sang dashboard thử thứ hai; export/import giữ đúng link. (V8) thêm collapsible section để mở; export/import giữ section. Mỗi mục sai: **DỪNG VÀ HỎI**. **Kết quả**: V5 KHÔNG ĐẠT (APM không đọc dữ liệu OTel thô) → người dùng chốt link Discover theo trace_id, đã kiểm chứng link hoạt động; V7: panel Links hoạt động nhưng tạo vòng tham chiếu → người dùng chốt link Markdown theo id cố định; V8 ĐÚNG.
- [X] T008 Tạo hai UUID mới (một lần) cho hai dashboard thật; điền vào bảng "Id" của [contracts/dashboards-contract.md](./contracts/dashboards-contract.md) cùng tên file ndjson đã chốt (`xu-ly-su-co.ndjson`, `ngan-sach-loi-tuan.ndjson`). **Kết quả**: `e61fc7f3-17fe-428a-a373-da88af0a4a1e` (Xử lý sự cố), `2a607bf4-2449-48a1-a2e8-1336ec35a7b7` (Ngân sách tuần); đã ghi vào contract.
- [X] T009 Kiểm chứng V6 (span Client). Tạo lưu lượng service → service thật (chạy folder Postman đặt hàng end-to-end có sẵn, hoặc luồng qua gateway/BFF có xác thực của dự án). Truy vấn `FROM traces-generic.otel-default* | WHERE kind == "Client" | STATS c=COUNT(*) BY resource.attributes.service.name, attributes.server.address | LIMIT 50`. Ghi bảng ánh xạ `server.address` → tên service đích (ví dụ host trong Compose) vào ghi chú task và vào research.md. Không có span Client sau khi gọi luồng hợp lệ: **DỪNG VÀ HỎI**. **Kết quả**: V6 ĐÚNG: có span Client, `server.address` = `identity-api`/`bff-api`/`products-api`/`baskets-api`; ánh xạ đích `<tên>-api` → `<Tên>.Api` bằng biểu thức, không cần bảng CASE.
- [X] T010 **Chỉ nếu T004 báo thiếu correlation id/trace id ở log**: viết test trong `shared/ServiceDefaults.UnitTests/` (tên file đề xuất `LogCorrelationTests.cs`, theo khuôn `ChaosFaultInjectionMiddlewareTests.cs`) khẳng định một log ghi trong request mang correlation id; chạy thấy ĐỎ, ghi số test đỏ. Comment tiếng Việt, `Task nguồn: spec 030 (log mang correlation id) — FR-019`. Nếu T004 báo đủ: ghi "không cần sửa code" và đánh dấu xong. **Kết quả**: bỏ qua — V4 báo log đã đủ correlation id.
- [X] T011 **Chỉ nếu T010 đã đỏ**: sửa `shared/ServiceDefaults/ServiceDefaultsExtensions.cs` (và file liên quan trong `shared/ServiceDefaults/`) để log trong request mang correlation id tới Elasticsearch; chạy test thấy XANH; tạo lại 7 service từ compose và lặp T004 để xác nhận log Elasticsearch có trường. Không đổi phản hồi của endpoint nào (FR-017). **Kết quả**: bỏ qua — V4 báo log đã đủ correlation id, không sửa `ServiceDefaults`.
- [X] T012 Ghi kết quả V1–V8 và bảng ánh xạ T009 vào mục mới "Kết quả xác minh" ở cuối [research.md](./research.md). Xoá dashboard thử `tmp-030` và saved object thử khỏi Kibana (chỉ object do T005–T007 tạo, đã ghi id). **Kết quả**: đã ghi kết quả V1–V8 vào research.md; xoá toàn bộ dashboard/links/space thử.

**Checkpoint**: bộ chọn tuần, Lens ES|QL, thanh thời gian, link APM, links, section, log và span Client đã được chứng minh trên Kibana thật. Chưa chứng minh được: dừng, hỏi người dùng.

---

## Phase 3: User Story 1 — Dashboard Xử lý sự cố đi theo thanh thời gian (Priority: P1) 🎯 MVP

**Goal**: Dashboard `Xử lý sự cố — 7 service`, mọi panel theo thanh thời gian, mặc định 1 giờ, tự làm mới 1 phút.

**Independent Test**: Quickstart Kịch bản 2. Đổi thanh thời gian giữa 15 phút, 1 giờ, 24 giờ; mọi panel đổi theo; tiêm lỗi vào một service thì thấy ở bảng SLO, Phát hiện nhanh, 5xx theo phút, log lỗi kèm trace id/correlation id.

- [X] T013 [P] [US1] Thêm vào `postman/ecommerce.postman_collection.v2.json` (đặt sau folder 29) folder cha `30 - Hai dashboard: tạo lỗi và đối chiếu số liệu` và folder con `30a - Tạo lỗi + traffic cho Xử lý sự cố (chạy theo vòng)`: request tạo 5xx cho 7 service (header `X-Chaos-Fault: 5xx`, như folder 29a), request 401/403 và request chậm, và lời gọi service → service để có span Client (dùng luồng đã chạy ở T009). Mỗi request có mô tả mục đích/input/output/FR như các folder khác (PR #66/#67). Chèn bằng thao tác văn bản (collection có chỗ định dạng tay, không serialize lại cả file): chỉ thêm dòng, không xoá dòng nào. Nội dung chi tiết (số lần, ngưỡng chậm) không có trong mô tả người dùng: **HỎI NGƯỜI DÙNG** các request cụ thể trước khi viết. **Kết quả**: folder cha `30 - Hai dashboard: tạo lỗi và đối chiếu số liệu` + `30a` (14 request: 5xx ×7, 401, token không scope + 403, token + 2 request hạ lưu, độ trễ) chèn bằng thao tác văn bản (+632 dòng, không xoá dòng nào); newman 4 vòng: 401, 403, hạ lưu, độ trễ chạy đúng; 5xx đỏ vì cờ tiêm lỗi đang TẮT (đúng như mô tả folder).
- [X] T014 [P] [US1] Tạo index-pattern logs `logs-generic.otel-default*` trên Kibana (nếu T007 đã tạo bản thử thì tạo lại bản thật), `trace_id` định dạng URL trỏ `/app/apm/link-to/trace/{{value}}`, đúng tên trường tìm được ở T004. **Kết quả**: index-pattern `logs-generic-otel-default`, `trace_id` định dạng URL mở Discover (data view traces) lọc theo trace_id; đã bấm thử: mở đúng 12 span của trace.
- [X] T015 [US1] Tạo (Saved Objects API, như 027/028) Discover session ES|QL **Lỗi gọi hạ lưu** theo research.md D8 và contract bất biến 10: `kind == "Client"`, đích suy ra từ `attributes.server.address` bằng `CASE` theo bảng ánh xạ T009, "xấu" = `status.code == "Error"` hoặc `attributes.http.response.status_code >= 500` hoặc `duration` vượt p95 của đích (150 ms, `Bff.Api` 300 ms), `LIMIT 20`, sắp theo % xấu giảm dần, không có `trace_id`. Chạy thử qua `POST /_query` trên dữ liệu T009 và ghi kết quả vào ghi chú task. **Kết quả**: Discover session ES|QL by-value; `q_ds` chạy thật trả 9 cặp (ví dụ `Gateway.Api → Bff.Api`, 55,56% xấu). Hiển thị mọi cặp xếp theo % xấu giảm dần (không lọc riêng cặp xấu).
- [X] T016 [US1] Tạo Discover session cổ điển **Log lỗi gần nhất** theo research.md D6 và contract bất biến 11: index-pattern T014, lọc `severity_number >= 17`, sắp theo `@timestamp` giảm dần, 50 dòng, cột thời gian, service, message, `trace_id`, correlation id. **Kết quả**: Discover session cổ điển by-value: `severity_number >= 17`, 50 dòng, cột thời gian/service/body.text/trace_id/attributes.CorrelationId.
- [X] T017 [US1] **HỎI NGƯỜI DÙNG duyệt tiêu đề** trước khi tạo panel/section còn lại. Đề xuất (người dùng duyệt hoặc sửa): **Kết quả**: người dùng duyệt đúng như đề xuất.
  - section "Tình trạng SLO": "Bảng SLO — 7 service", "Ngưỡng SLO" (Markdown), "Phát hiện nhanh — service vượt SLO trong khoảng thời gian đã chọn";
  - section "Đào sâu lỗi": "5xx theo phút theo service", "Latency p95 theo phút theo service", "Traffic + 401/403 theo phút theo service", "Xu hướng dotnet.exceptions theo service", "Phân bố status code theo service", "Top endpoint chậm nhất", "Lỗi gọi hạ lưu — cặp service gọi → đích", "Log lỗi gần nhất (Error trở lên)".
- [X] T018 [US1] Sửa saved search `incident-fast-detection-active-alerts` (hoặc tạo bản mới nếu người dùng muốn id mới ở T017) theo contract bất biến 7 và 9: bỏ `AND @timestamp >= NOW() - 15 minutes`, thêm cột `kibana.alert.status`, giữ `KEEP` gồm `service`, `kibana.alert.status`, `kibana.alert.start`, sắp theo trạng thái rồi service. Cập nhật mô tả (không còn "15 phút"). **Kết quả**: saved search `incident-fast-detection-active-alerts` sửa: bỏ lọc 15 phút, thêm cột `status`.
- [X] T019 [P] [US1] Tạo Lens trên index-pattern traces có sẵn: **5xx theo phút theo service** (date histogram theo phút, tách theo `resource.attributes.service.name`, KQL `attributes.http.response.status_code >= 500`) và **Latency p95 theo phút theo service** (`percentile 95` của `duration`, đổi sang ms nếu panel cũ làm vậy). **Kết quả**: Lens (Dashboards API) trên index-pattern traces: 5xx theo phút và p95 theo phút theo service.
- [X] T020 [P] [US1] Tạo Lens **Traffic + 401/403 theo phút theo service**: số request theo phút theo service và số 401/403 cùng biểu đồ (hai chuỗi hoặc hai lớp). Thay thế panel "Tổng 401 + 403" (FR-003). **Kết quả**: Lens hai lớp: traffic theo service (line) + 401/403 theo service (bar_stacked).
- [X] T021 [US1] Dựng dashboard `Xử lý sự cố — 7 service` với id ở T008: chép Lens bảng SLO, Markdown ngưỡng, `dotnet.exceptions`, phân bố status code, top endpoint chậm nhất từ dashboard cũ (đối chiếu T003, không chép panel "Tổng 401 + 403", panel 7–8 và 3 panel ngân sách); thêm các panel T015, T016, T018, T019, T020; xếp bằng collapsible section luôn mở; đặt thời gian lưu cùng dashboard `now-1h` → `now` và tự làm mới 1 phút (FR-004). Chưa thêm link (T045). **Kết quả**: dashboard `e61fc7f3-…` dựng bằng Dashboards API: 2 section luôn mở, `now-1h`, tự làm mới 1 phút, 11 panel.
- [X] T022 [US1] Kiểm Kịch bản 2 của [quickstart.md](./quickstart.md) trên Kibana: đổi thanh thời gian, mọi panel đổi; chạy folder Postman `30a` (newman qua `npx.cmd`, như 028/029) và xác nhận service lỗi hiện ở các panel, log lỗi hiện kèm trace id/correlation id và link mở trace thật. Ghi số đo thật vào ghi chú task (số đo này chỉ ghi ở QA_Debt, không ghi vào file QA). **Kết quả**: đã kiểm: thanh thời gian đổi thì panel đổi (đối chiếu với ES: 35 log Error ở cả 5 phút và 1 giờ, 154 ở 24 giờ); folder 30a tạo được dữ liệu cho hạ lưu/401/403; link trace mở Discover đúng. Số đo ghi ở QA_Debt (T057).
- [X] T023 [US1] Export dashboard `Xử lý sự cố — 7 service` bằng Saved Objects Export API (`includeReferencesDeep`, như `dashboards/README.md`) ra `docs/kibana-quan-sat-he-thong/dashboards/xu-ly-su-co.ndjson`. Kiểm tra file: parse từng dòng, không chứa `now-7d`, `NOW() - 15 minutes`, `DATE_TRUNC(1 week`, hay id/tên dashboard cũ (contract bất biến 7). **Kết quả**: export `xu-ly-su-co.ndjson` (5 object: dashboard, 1 saved search, 3 index-pattern); không có `NOW() - 15 minutes`, `DATE_TRUNC(1 week`, id/tên dashboard cũ; `now-7d` chỉ ở mẫu URL link trace (đã ghi trong contract).

**Checkpoint**: dashboard Xử lý sự cố chạy, export được, kiểm tay xong. Có thể giao tách riêng (MVP).

---

## Phase 4: User Story 2 — Dashboard Ngân sách lỗi tuần cố định tuần lịch (Priority: P1)

**Goal**: Dashboard `Ngân sách lỗi tuần — 7 service` với bộ chọn tuần, mức tiêu hao, hạn mức còn lại, cảnh báo/cạn, error-rate/p95 theo ngày, lũy kế.

**Independent Test**: Quickstart Kịch bản 3. Số liệu không đổi theo thanh thời gian; chọn tuần khác thì panel tính từ traces đổi, panel cảnh báo/cạn không đổi; số khớp truy vấn ES|QL chạy tay.

- [X] T024 [P] [US2] Thêm folder con `30b - Đối chiếu ngân sách tuần (chạy một lần)` vào folder 30 trong `postman/ecommerce.postman_collection.v2.json`: truy vấn `POST {{elasticsearchUrl}}/_query` (kiểu 29b) cho mức tiêu hao, hạn mức còn lại (`remaining_pct`, `remaining_requests`) và tiêu hao lũy kế theo ngày, có tham số `week_start`. Mỗi request có mô tả mục đích/input/output/FR. Chèn bằng thao tác văn bản như T013. **Kết quả**: folder `30b` (3 truy vấn có tham số `tuan_chon`) chèn bằng thao tác văn bản (+142 dòng); newman 3 request, 6/6 assertion xanh.
- [X] T025 [US2] **HỎI NGƯỜI DÙNG duyệt tiêu đề** panel/section dashboard tuần trước khi tạo. Đề xuất (người dùng duyệt hoặc sửa): điều khiển "Tuần (thứ Hai 00:00 → Chủ nhật 23:59, UTC+7)"; section "Ngân sách tuần": "Mức tiêu hao ngân sách (%) — tuần đã chọn", "Hạn mức còn lại — tuần đã chọn", "Cảnh báo mốc đang hoạt động (trạng thái hiện tại)", "Cạn ngân sách — ưu tiên độ tin cậy (trạng thái hiện tại)"; section "Xu hướng trong tuần": "Error-rate theo ngày trong tuần", "Latency p95 theo ngày trong tuần", "Tiêu hao lũy kế theo ngày từ thứ Hai". **Kết quả**: người dùng duyệt đúng như đề xuất.
- [X] T026 [US2] Tạo điều khiển ES|QL biến `?week_start` (truy vấn danh sách tuần ở research.md D3, mặc định tuần mới nhất) theo cách đã chứng minh ở T005. **Kết quả**: điều khiển ES|QL **tĩnh** `STATIC_VALUES` biến `?tuan_chon` (Tuần này / Tuần trước / 2 tuần trước / 3 tuần trước, mặc định Tuần này) thay cho danh sách theo dữ liệu, vì điều khiển lưu sẵn giá trị mặc định cố định; người dùng đồng ý.
- [X] T027 [US2] Sửa saved search `slo-error-budget-consumption`: thay `WHERE @timestamp >= DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours` bằng `@timestamp >= TO_DATETIME(?week_start) AND @timestamp < TO_DATETIME(?week_start) + 7 days`, giữ nguyên các `allowed` 0.01/0.01/0.05/0.01 và ngưỡng độ trễ. Cập nhật tiêu đề/mô tả (không còn "tuần này"). Chạy thử `POST /_query` với `params` `week_start` = tuần hiện tại và so với truy vấn gốc: kết quả giống nhau từng (service, ngân sách). **Kết quả**: `slo-error-budget-consumption` dùng `?tuan_chon`; so với truy vấn gốc: 28/28 dòng, chênh lệch ≤ 2 điểm % (dữ liệu tăng giữa hai lần chạy).
- [X] T028 [US2] Tạo Discover session ES|QL **Hạn mức còn lại**: dùng cùng truy vấn T027, thêm `remaining_pct = ROUND(100.0 - consumed_pct, 2)` và `remaining_requests = FLOOR(allowed * total - bad)` (số âm giữ nguyên khi vượt, người dùng chốt), cột `service`, `budget`, `remaining_pct`, `remaining_requests`. Chạy thử qua `POST /_query` và kiểm bằng tay trên một service có lỗi. **Kết quả**: Discover session by-value; kiểm tay Products.Api: tổng 1844 span, 12 lỗi 5xx → `remaining_pct` 34,92 và `remaining_requests` 6, khớp công thức.
- [X] T029 [US2] Sửa hai saved search `slo-error-budget-active-alerts` và `slo-error-budget-frozen`: bỏ `AND @timestamp >= NOW() - 15 minutes` (trạng thái hiện tại), giữ lọc `kibana.alert.status == "active"`; thêm dòng chú trong mô tả/tiêu đề rằng đây là trạng thái hiện tại không phụ thuộc tuần chọn (FR-009). **Không** sửa truy vấn rule; tiêu đề đã duyệt ở T025. **Kết quả**: hai saved search cảnh báo/cạn: lọc từ đầu tuần hiện tại (không còn 15 phút), tiêu đề ghi "(trạng thái hiện tại)".
- [X] T030 [P] [US2] Tạo Lens ES|QL **Error-rate theo ngày trong tuần** và **Latency p95 theo ngày trong tuần**: nhóm theo `DATE_TRUNC(1 day, @timestamp + 7 hours) - 7 hours` trong tuần `?week_start`, tách theo service. Thay panel 7–8 cũ. **Kết quả**: Lens ES|QL error-rate theo ngày và p95 theo ngày (xy, `breakdown_by` service).
- [X] T031 [P] [US2] Tạo Lens ES|QL **Tiêu hao lũy kế theo ngày từ thứ Hai**: dùng kỹ thuật research.md D4 / F8 (`as_of = [0,1,2,3,4,5,6]`, `MV_EXPAND`, `WHERE DATE_DIFF("day", TO_DATETIME(?week_start), @timestamp) <= as_of`), hiển thị `consumed_pct` theo `as_of` cho mỗi service (một ngân sách chọn được hoặc một chuỗi mỗi ngân sách; **HỎI NGƯỜI DÙNG** nếu không rõ chọn hiển thị ngân sách nào: chưa có trong mô tả). **Kết quả**: Lens ES|QL lũy kế: mức cao nhất trong 4 ngân sách, mỗi service một đường (người dùng chốt), chỉ ngày đã tới.
- [X] T032 [US2] Dựng dashboard `Ngân sách lỗi tuần — 7 service` với id ở T008: điều khiển T026, các panel T027–T031, collapsible section luôn mở, ghi rõ trên dashboard (Markdown hoặc tiêu đề) hai panel cảnh báo/cạn là trạng thái hiện tại. Không phụ thuộc thanh thời gian (không panel nào dùng thanh thời gian để lọc ngân sách). Chưa thêm link (T045). **Kết quả**: dashboard `2a607bf4-…`: điều khiển + 2 section luôn mở + 7 panel, mọi panel `time_range` `now-30d` (người dùng chốt 30 ngày).
- [X] T033 [US2] Kiểm Kịch bản 3 của [quickstart.md](./quickstart.md): đổi thanh thời gian không đổi số; chọn tuần khác; đối chiếu số dashboard với truy vấn chạy tay và với folder Postman `30b`; kiểm `remaining_pct`, `remaining_requests` và lũy kế không giảm theo ngày; kiểm tuần chưa có dữ liệu hiển thị trống. Ghi số đo thật vào ghi chú task (chỉ ghi ở QA_Debt, không ghi vào file QA). **Kết quả**: đã kiểm: thanh thời gian 5 phút/30 ngày không đổi số (28/28/5/6 dòng); chọn "Tuần trước" → hai panel tính từ traces trống, hai panel cảnh báo/cạn giữ nguyên; công thức khớp; import vào space sạch tạm cho 28 dòng. Số đo ghi ở QA_Debt (T057).
- [X] T034 [US2] Export dashboard `Ngân sách lỗi tuần — 7 service` ra `docs/kibana-quan-sat-he-thong/dashboards/ngan-sach-loi-tuan.ndjson`. Kiểm tra: parse từng dòng, có điều khiển `?week_start`, truy vấn tính từ traces dùng `?week_start`, không chứa `NOW() - 15 minutes` hay id/tên dashboard cũ. **Kết quả**: export `ngan-sach-loi-tuan.ndjson` (4 object: dashboard + 3 saved search); không có `NOW() - 15 minutes` hay id/tên cũ; có điều khiển `tuan_chon`.

**Checkpoint**: dashboard Ngân sách tuần chạy, export được, số khớp rule/truy vấn tay.

---

## Phase 5: User Story 3 — Bỏ dashboard cũ, link qua lại, test canh gác rule 028 (Priority: P2)

**Goal**: Dashboard cũ biến mất; hai dashboard link qua lại; test canh gác `incident-fast-detection`; test 027 xanh.

**Independent Test**: Quickstart Kịch bản 1, 4, 5. `dashboards/` chỉ còn hai file mới; LỆNH-TÌM không còn tham chiếu ngoài danh sách "không sửa"; test 028 từng đỏ rồi xanh.

### Tests (viết trước, PHẢI đỏ)

- [X] T035 [US3] Viết `tests/ServiceManifestSloConventionTests/IncidentFastDetectionRuleDefinitionTests.cs` theo khuôn `ErrorBudgetRuleDefinitionTests.cs` và [contracts/incident-fast-detection-rule-contract.md](./contracts/incident-fast-detection-rule-contract.md) (7 bất biến: tag/chu kỳ/loại/`groupBy`/`timeField`, cửa sổ 5 phút, ngưỡng 5xx 1% khớp `PlatformSloDefaults`/manifest, p95/p99 khớp mọi manifest và ngoại lệ BFF, Gateway chỉ xét 5xx, chỉ trả cột `service`, ngưỡng `> 0`). Đọc ngưỡng từ manifest qua `ServiceManifestFixture`, không hard-code. Comment tiếng Việt (`Kiểm tra` / `Lý do` / `Task nguồn: spec 030 — FR-015`). **Kết quả**: `IncidentFastDetectionRuleDefinitionTests.cs` 7 test (tái dùng `ErrorBudgetRuleDefinitionTests.ParseCase`).
- [X] T036 [US3] Chạy thấy ĐỎ từng test: với mỗi bất biến, tạm đổi một giá trị trong `docs/kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson` (ví dụ `err_pct >= 1` thành `>= 0.1`, đổi chu kỳ `5m`, bỏ nhánh Gateway), chạy `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~IncidentFastDetectionRuleDefinitionTests`, xác nhận đúng test tương ứng ĐỎ, rồi **hoàn tác** (xác nhận `git diff` file ndjson rule rỗng). Ghi số test đỏ theo từng bất biến vào ghi chú task. Không sửa rule thật. **Kết quả**: ĐỎ đúng 1 test cho mỗi biến đổi (7/7: chu kỳ, `>=`, cửa sổ 15 phút, ngưỡng 0.1, p95 100ms, Gateway true, thêm cột); đã hoàn tác, `git diff` file rule rỗng.
- [X] T037 [US3] Chạy `dotnet test tests/ServiceManifestSloConventionTests` (cả `IncidentFastDetectionRuleDefinitionTests` lẫn `ErrorBudgetRuleDefinitionTests`): XANH. Ghi số test. **Kết quả**: XANH 110/110 (`ServiceManifestSloConventionTests`), 25/25 `ErrorBudgetRuleDefinitionTests`.

### Bỏ dashboard cũ và link qua lại

- [X] T038 [US3] Thêm panel Links vào dashboard Xử lý sự cố trỏ tới dashboard Ngân sách tuần (theo id T008) và ngược lại (cách đã chứng minh ở T007), đặt ở vị trí dễ thấy. Export lại **cả hai** file ndjson (T023, T034). **Kết quả**: KHÁC kế hoạch (người dùng chốt): link giữa hai dashboard là ô Markdown theo id cố định thay cho panel Links (panel Links tạo vòng tham chiếu, mỗi file export chứa cả hai dashboard; link ngoài tương đối bị Kibana vô hiệu). Hai file độc lập, đã export lại.
- [X] T039 [US3] Kiểm Kịch bản 4: import hai file vào Kibana sạch (cách dưới `dashboards/README.md` mới), bấm link hai chiều. Nếu cần Kibana sạch riêng, **HỎI NGƯỜI DÙNG** trước khi xoá object trên Kibana đang chạy; không tự xoá. **Kết quả**: import hai file vào space Kibana tạm `tmp030` (sạch): thành công 4 + 5 object, dashboard hiển thị đủ panel và dữ liệu; đã xoá space. Trong space khác id được đổi nên link Markdown chỉ đúng id trên Kibana mặc định/sạch.
- [X] T040 [US3] Xoá `docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson` khỏi repo (`git rm` không cần commit: chỉ xoá file). Xác nhận `dashboards/` có đúng 2 file ndjson mới và `README.md`. **Kết quả**: đã xoá file; `dashboards/` còn 2 ndjson + README.
- [X] T041 [US3] Viết lại `docs/kibana-quan-sat-he-thong/dashboards/README.md`: mô tả hai file, cách import (lệnh `curl.exe` cho từng file, `overwrite=true`), cách export (`includeReferencesDeep` theo id mới), danh sách id. Bỏ nội dung nói về dashboard cũ. **Kết quả**: README viết lại (bảng id, import/export từng file, lưu ý Links/`time_range`/`?tuan_chon`/log lỗi).
- [X] T042 [US3] **HỎI NGƯỜI DÙNG** có xoá dashboard cũ (id `e2e06ff5-9cdf-4bea-acc8-5fd60ce26170`) khỏi Kibana đang chạy bây giờ không (FR-001, SC-005). Chỉ xoá khi người dùng xác nhận rõ ràng ngay lúc đó và nêu đúng id sẽ xoá; không xoá 3 saved search/Lens dùng chung nếu hai dashboard mới còn tham chiếu. **Kết quả**: người dùng xác nhận xoá; đã xoá đúng dashboard `e2e06ff5-…` bằng API (204), không còn object Lens mồ côi.

**Checkpoint**: test canh gác rule 028 đã từng đỏ rồi xanh; dashboard cũ không còn trong repo; link hai chiều chạy.

---

## Phase 6: User Story 4 — Tài liệu trỏ đúng hai dashboard mới và bộ tài liệu 030 (Priority: P3)

**Goal**: Mọi tài liệu trỏ tới dashboard cũ nói đúng hai dashboard mới; có đủ bộ tài liệu 030 theo khuôn 027/028/029.

**Independent Test**: Quickstart Kịch bản 5, 6. LỆNH-TÌM chỉ còn danh sách "không sửa"; có đủ file ở FR-016.

### Sửa tài liệu cũ tại chỗ (FR-014)

Với mỗi file: thay tham chiếu dashboard cũ (id/tên) bằng dashboard mới tương ứng (dashboard Xử lý sự cố cho bảng SLO/đào sâu/Phát hiện nhanh; dashboard Ngân sách tuần cho 3 panel ngân sách). Giữ nguyên nội dung ngoài tham chiếu. Không sửa bản ghi lịch sử (mục cũ `docs/QA/QA_Debt.md`, `docs/dien-tap-chaos-engineering/ket-qua/`, `specs/002-gateway-bff-routing/`).

- [X] T043 [P] [US4] Viết lại `docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md` cho hai dashboard và **đổi tên** thành `06-dashboard-xu-ly-su-co-va-ngan-sach-tuan.md` (dùng `git mv` rồi sửa). Nội dung: vai trò từng dashboard, danh sách panel, bộ chọn tuần, link hai chiều, giới hạn đã biết (collapsible section; panel cảnh báo/cạn là trạng thái hiện tại). Sửa mọi link trỏ tới tên cũ trên toàn repo (`grep -rn "06-dashboard-slo-van-hanh-hang-ngay"`). **Kết quả**: viết lại và `git mv` thành `06-dashboard-xu-ly-su-co-va-ngan-sach-tuan.md`; giữ phần kiểm chứng Lens/section gốc làm ghi chú; mọi link trỏ tên cũ đã đổi.
- [X] T044 [P] [US4] Sửa `docs/kibana-quan-sat-he-thong/00-tong-quan-lo-trinh.md`, `07-canh-bao-ngan-sach-loi.md`, `08-phat-hien-nhanh-va-xu-ly-su-co.md`, `alerts/README.md` (nếu nhắc): trỏ dashboard mới, bỏ lọc "15 phút" khỏi mô tả panel Phát hiện nhanh, mô tả đúng nhóm panel ngân sách trên dashboard Ngân sách tuần. **Kết quả**: `00`, `07` (viết lại mục panel ngân sách, bảng 4 panel, `?tuan_chon`, `now-30d`), `08` (truy vấn mới của panel Phát hiện nhanh, test 030), `alerts/README.md` không nhắc dashboard cũ.
- [X] T045 [P] [US4] Sửa tài liệu 021: `specs/021-declare-service-slos/{plan,research,data-model,quickstart,tasks}.md`, `specs/021-declare-service-slos/contracts/continuous-measurement-contract.md`, `docs/QA/021_QA_khai báo và đo SLO theo từng service.md`, `docs/architecture/021_Architect_khai báo và đo SLO theo từng service.md`, `docs/development/021_Development_khai báo và đo SLO theo từng service.md`, `docs/diagrams/021-declare-service-slos-component.drawio`, `docs/diagrams/021-declare-service-slos-sequence.drawio` (mở file trước, chỉ sửa nhãn chữ, giữ bố cục; XML hợp lệ). **Kết quả**: sửa 021 (spec/plan/research/data-model/quickstart/tasks, contract, QA/Architect/Development, 2 drawio) bằng quét tự động rồi sửa tay các chỗ nghĩa lệch ("8 panel", "Last 24 hours").
- [X] T046 [P] [US4] Sửa tài liệu 025/026: `specs/025-chaos-pod-kill-latency/{quickstart,research,tasks}.md`, `specs/026-load-performance-test-budgets/quickstart.md`, `docs/architecture/025_Architect_diễn tập chaos engineering giết pod tiêm độ trễ.md`, `docs/diagrams/025-chaos-pod-kill-latency-component.drawio`, `docs/dien-tap-chaos-engineering/README.md`. **Không** sửa `docs/dien-tap-chaos-engineering/ket-qua/`. **Kết quả**: sửa 025/026 (quickstart/research/tasks, Architect 025, drawio 025, README diễn tập); không sửa `ket-qua/`.
- [X] T047 [P] [US4] Sửa tài liệu 027: `specs/027-error-budget-alerting/{plan,quickstart,research,tasks}.md`, `docs/QA/027_QA_chính sách ngân sách lỗi và ngưỡng cảnh báo.md`, `docs/architecture/027_Architect_chính sách ngân sách lỗi và ngưỡng cảnh báo.md`, `docs/diagrams/027-error-budget-alerting-component.drawio`. **Kết quả**: sửa 027 (plan/quickstart/research/spec/tasks/checklist, contract, QA, Architect, drawio 027 component + sequence): dashboard Ngân sách lỗi tuần, `ngan-sach-loi-tuan.ndjson`.
- [X] T048 [P] [US4] Sửa tài liệu 028: `specs/028-incident-oncall-drill/{plan,quickstart,research,tasks}.md`, `specs/028-incident-oncall-drill/contracts/fast-detection-rule-contract.md`, `docs/QA/028_QA_diễn tập sự cố thật và phản ứng on-call.md`, `docs/diagrams/028-incident-oncall-drill-component.drawio`. Ghi trong contract `fast-detection-rule-contract.md` rằng spec 030 đã thêm test canh gác (đóng sai lệch Nguyên tắc III) và link tới `IncidentFastDetectionRuleDefinitionTests.cs`. **Kết quả**: sửa 028 (plan/quickstart/research/spec/tasks, contract, QA, Architect, drawio): dashboard Xử lý sự cố, bảng Phát hiện nhanh mới; contract ghi test 030.
- [X] T049 [P] [US4] Sửa tài liệu 029: `specs/029-error-budget-weekly/{plan,quickstart,research,tasks}.md`, `specs/029-error-budget-weekly/contracts/error-budget-alert-rules-contract.md`, `docs/QA/029_QA_ngân sách lỗi theo tuần lịch.md`, `docs/diagrams/029-error-budget-weekly-component.drawio`: chỉ đổi tham chiếu dashboard cũ sang hai dashboard mới (ba panel ngân sách nay ở dashboard Ngân sách tuần); giữ nguyên mọi số/chu kỳ của 029. **Kết quả**: sửa tham chiếu dashboard trong 029 (spec/plan/research/quickstart/tasks, contract, QA, Architect, drawio component + sequence) sang dashboard Ngân sách lỗi tuần; giữ nguyên số/chu kỳ 029.
- [X] T050 [P] [US4] Sửa `docs/superpowers/plans/2026-09-08-dashboard-slo-van-hanh-hang-ngay.md` và `docs/superpowers/specs/2026-09-08-dashboard-slo-van-hanh-design.md`: thêm ghi chú đầu file "đã được tách thành hai dashboard bởi spec 030", trỏ link; không viết lại nội dung thiết kế gốc. Sửa mô tả trong `postman/ecommerce.postman_collection.v2.json` nếu nhắc dashboard cũ (chỉ đổi chữ, không serialize lại file). **Kết quả**: `docs/superpowers/` chỉ thêm ghi chú đầu file và đổi link `06-…md` (nội dung gốc giữ nguyên); mô tả Postman folder 21/27/28/29 sửa (id/tên/`SLO hằng ngày`); bước 21/01 `GET /api/saved_objects/dashboard/e61fc7f3-…` chạy xanh qua newman.

### Bộ tài liệu 030 (FR-016)

- [X] T051 [P] [US4] Tạo `docs/PO/030_PO_hai dashboard xử lý sự cố và ngân sách tuần.md` theo khuôn `docs/PO/027_PO_chính sách ngân sách lỗi và ngưỡng cảnh báo.md`, `docs/PO/028_PO_*.md`, `docs/PO/029_PO_ngân sách lỗi theo tuần lịch.md`: ngôn ngữ nghiệp vụ, vì sao tách (dashboard cũ trộn ba loại cửa sổ), hai dashboard làm được gì, giới hạn. **Kết quả**: `docs/PO/030_PO_hai dashboard xử lý sự cố và ngân sách tuần.md`.
- [X] T052 [P] [US4] Tạo `docs/architecture/030_Architect_hai dashboard xử lý sự cố và ngân sách tuần.md` theo khuôn `docs/architecture/029_Architect_ngân sách lỗi theo tuần lịch.md`: gồm bộ chọn tuần, nguồn dữ liệu, quyết định D1–D12, kết quả V1–V8 (tóm tắt), mục `## Sơ đồ` với 3 link tới 3 drawio (dạng `%20`). **Kết quả**: `docs/architecture/030_Architect_hai dashboard xử lý sự cố và ngân sách tuần.md` (10 mục, mục 9 Sơ đồ 3 link `%20`).
- [X] T053 [P] [US4] Tạo `docs/QA/030_QA_hai dashboard xử lý sự cố và ngân sách tuần.md` theo khuôn QA 024–029 (đối chiếu `docs/QA/029_QA_*.md`): **Kết quả**: `docs/QA/030_QA_hai dashboard xử lý sự cố và ngân sách tuần.md`: Thủ công trước Tự động, bảng thủ công 11 bước (công tắc compose/`.env` + folder 30a/30b), bảng Tự động 8 dòng link `#L…`; không cột số đo, không "Nguồn đối chiếu"; phát hiện ở QA_Debt.
  - mục **Thủ công trước, Tự động sau**; bảng thủ công điều khiển bằng công tắc compose/`.env` và folder Postman `30a`/`30b`, mỗi bước ghi công tắc/lệnh, cách kiểm, kết quả mong đợi (không ghi cột số đo thật);
  - bảng **Tự động** link từng test tới **dòng khai báo** (`#L…`) trong `IncidentFastDetectionRuleDefinitionTests.cs` (và `ErrorBudgetRuleDefinitionTests.cs`, và test `ServiceDefaults` nếu T010 có);
  - viết lại comment test theo đúng spec (tiếng Việt); mọi **phát hiện chỉ ghi ở `QA_Debt.md`** (T057), không ghi trong file QA; không có khối "Nguồn đối chiếu".
- [X] T054 [P] [US4] Tạo `docs/diagrams/030-incident-and-weekly-dashboards-component.drawio`. Trước khi vẽ, mở `docs/diagrams/029-error-budget-weekly-component.drawio` để chép bố cục, kiểu khối/màu, nhãn tiếng Việt, dạng `<diagram id=...>`. Nội dung: nguồn dữ liệu (traces, logs, metrics, alerts) → hai dashboard → người trực / SRE; link hai chiều; điều khiển tuần. **Kết quả**: `docs/diagrams/030-incident-and-weekly-dashboards-component.drawio` (10 khối, 10 cạnh) theo kiểu ô/màu của drawio 029; XML hợp lệ, CRLF.
- [X] T055 [P] [US4] Tạo `docs/diagrams/030-incident-and-weekly-dashboards-flow-nghiep-vu.drawio` theo mẫu `029-error-budget-weekly-flow-nghiep-vu.drawio` (ngôn ngữ nghiệp vụ, không tên công cụ): sự cố xảy ra → mở Xử lý sự cố → thu hẹp từ service tới trace → xem ngân sách tuần còn lại → quyết định (dừng merge / tiếp tục). **Kết quả**: `…-flow-nghiep-vu.drawio` (9 khối, 8 cạnh), ngôn ngữ nghiệp vụ; XML hợp lệ.
- [X] T056 [P] [US4] Tạo `docs/diagrams/030-incident-and-weekly-dashboards-sequence.drawio` theo mẫu `029-error-budget-weekly-sequence.drawio`: trình tự thật của T022/T033 (chạy folder 30a, OTel → Elasticsearch, người dùng mở dashboard, đổi thanh thời gian, chọn tuần, bấm link trace). **Kết quả**: `…-sequence.drawio` (5 làn + 8 bước), số liệu thật T022/T033; XML hợp lệ.
- [X] T057 [US4] Thêm mục `## 030 — Hai dashboard Xử lý sự cố và Ngân sách lỗi tuần` vào cuối: **Kết quả**: QA_Debt mục 030 (tiêu đề "(001-030)"), technical-debt mục 030 ở cả 4 phần (phạm vi đổi, bug thật, giới hạn, đính chính; đánh dấu rule 028 đã có test), functional-debt mục 030 ở phần 1 và 2.
  - `docs/QA/QA_Debt.md`: phát hiện từ T004–T012, T022, T033; số đo thật của T022/T033; ghi nhận panel log hiển thị `message` có thể chứa dữ liệu nhạy cảm (Nguyên tắc VI); cập nhật dòng tiêu đề "(001-029)" → "(001-030)". Không sửa mục cũ.
  - `docs/architecture/technical-debt.md`: giới hạn đã biết (panel cảnh báo/cạn chỉ là trạng thái hiện tại; tuần đầu sau dọn Elastic thiếu dữ liệu; phụ thuộc điều khiển ES|QL/Lens ES|QL trên 9.4.4; `server.address` → tên service là bảng ánh xạ tay); đánh dấu rule 028 đã có test (đóng sai lệch III).
  - `docs/PO/functional-debt.md`: thêm mục 030 ở cả hai phần của file (theo cách mục 029), link tới file PO T051 dạng `%20`.

**Checkpoint**: tài liệu nói đúng hai dashboard mới; bộ tài liệu 030 đủ.

---

## Phase 7: Polish & kiểm tra cuối

- [X] T058 Chạy `dotnet test tests/ServiceManifestSloConventionTests` và `dotnet test shared/ServiceDefaults.UnitTests`, ghi số test xanh (SC-007). Nếu T010/T011 có chạy: lặp thêm test `shared/` liên quan. **Kết quả**: XANH 110/110 (`ServiceManifestSloConventionTests`) và 21/21 (`ServiceDefaults.UnitTests`); T010–T011 bỏ qua nên không có test `shared/` mới.
- [X] T059 Kiểm tra link trong các file Markdown mới/sửa (T041, T043–T053, T057): link tương đối trỏ tới file có thật (giải mã `%20`), link `#L…` trỏ đúng dòng khai báo test, link tới file 06 mới. Ghi số file và số link đã kiểm; lỗi có sẵn từ trước ngoài phạm vi chỉ ghi nhận, không sửa. **Kết quả**: 63 file Markdown mới/sửa, 536 link tương đối (43 link `#L…`): 0 link hỏng do spec này; 1 link hỏng có sẵn từ trước ở `QA_Debt.md` dòng 506 (mục cũ, không sửa) và 1 dương tính giả (mã trong backtick).
- [X] T060 Kiểm tra file JSON/ndjson đã sửa/tạo parse được từng dòng/từng file: `xu-ly-su-co.ndjson`, `ngan-sach-loi-tuan.ndjson`, `postman/ecommerce.postman_collection.v2.json`, 3 drawio (XML hợp lệ). **Kết quả**: 2 ndjson (6 và 5 dòng), collection Postman và 84 file drawio parse được.
- [X] T061 Import hai ndjson vào Kibana sạch (hoặc so từng object với Kibana đang chạy nếu người dùng không cho xoá, như 029 T054) và chạy lại Kịch bản 2–4 của [quickstart.md](./quickstart.md) liền mạch. Không đốt lại ngân sách nếu việc import đặt lại trạng thái alert (**HỎI NGƯỜI DÙNG** nếu cần). **Kết quả**: đã chạy: import 2 ndjson vào space sạch tạm (5 + 4 object, hiển thị đủ), export lại từ Kibana đang chạy trùng byte-by-byte với file trong repo, và sau khi dọn Elastic (T063) import vào Kibana mới hoàn toàn (thành công 5+1 rule, 5 và 4 object dashboard; 5 rule Enable được). Không đốt lại ngân sách.
- [X] T062 Chạy LỆNH-TÌM, so với mốc T002. Mọi file còn lại phải thuộc nhóm "không sửa" (mục cũ `QA_Debt`, `ket-qua/`, `specs/002`), hoặc là spec 030 (nơi mô tả dashboard cũ) và các chỗ ghi chú lịch sử có chủ đích. Ghi danh sách còn lại vào ghi chú task (SC-005). **Kết quả**: còn lại hợp lệ: file của spec 030 (mô tả dashboard cũ), mục lịch sử `QA_Debt.md`, `docs/dien-tap-chaos-engineering/ket-qua/`, 2 file `docs/superpowers/` (có ghi chú đầu file), ghi chú "nay đã tách" có chủ đích ở `docs/development/021_*`, `specs/021/tasks.md`, 06 mới, `dashboards/README.md`, PO/QA/Architect 030 và drawio flow 030, và tên file lịch sử trong `specs/029/tasks.md` T040.
- [X] T063 **HỎI NGƯỜI DÙNG** có dọn toàn bộ Elastic bây giờ không (FR-018). Chỉ thực hiện khi người dùng xác nhận rõ ràng ngay lúc đó và nêu chính xác lệnh/volume sẽ xoá (và hậu quả: mất dashboard/rule/Case đang có trên Kibana nếu chưa export) trước khi chạy. **Kết quả**: người dùng xác nhận "Dọn ngay": xoá container elasticsearch/kibana/otel-collector và volume `ecomerce-local_local-es-data`, dựng lại, tạo index `slo-error-budget-events` (mapping ở file 07), import 4 file ndjson, Enable 5 rule. Dữ liệu cũ (traces/logs/metrics từ 05/10, alert, sự kiện cạn) đã mất; 7 service không bị đụng, đã gửi telemetry mới.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1**: không phụ thuộc.
- **Phase 2**: sau Phase 1. Chặn US1, US2, US3 (cần V1–V8). T010–T011 chỉ khi T004 báo thiếu.
- **Phase 3 (US1)**: cần Phase 2 (V3, V4, V5, V6, V8).
- **Phase 4 (US2)**: cần Phase 2 (V1, V2, V3, V8). Độc lập với US1 (khác dashboard), làm song song được.
- **Phase 5 (US3)**: T035–T037 (test) chỉ cần Phase 1, làm song song với Phase 2–4. T038 cần T023 và T034 (hai dashboard đã có). T040–T042 sau T038, T039.
- **Phase 6 (US4)**: T043–T050 cần id/tên cuối của hai dashboard (T008, T023, T034). T053 cần test đã xong (T035–T037, đúng số dòng `#L…`). T057 cần số đo thật T022/T033/T012.
- **Phase 7**: sau Phase 3–6.

### Within Each User Story

- Test trước, PHẢI đỏ (T036, và T010 nếu có) trước khi xanh.
- Panel con (saved search/Lens) → dashboard → kiểm tay → export.
- Export lại cả hai file sau khi thêm link (T038).

### Parallel Opportunities

- T013 ∥ T014 (khác file/Kibana object); T019 ∥ T020; T024 song song với phần US1.
- US1 (Phase 3) ∥ US2 (Phase 4) sau Phase 2.
- T035–T037 (test rule 028) ∥ Phase 2–4.
- T043–T050: khác file, song song. T051–T056: song song.

---

## Parallel Example: User Story 1

```text
Task: "T013 Folder Postman 30a trong postman/ecommerce.postman_collection.v2.json"
Task: "T014 Index-pattern logs trên Kibana"
Task: "T019 Lens 5xx + p95 theo phút theo service"
Task: "T020 Lens Traffic + 401/403 theo phút theo service"
```

## Parallel Example: User Story 4

```text
Task: "T045 Tài liệu 021"      Task: "T046 Tài liệu 025/026"
Task: "T047 Tài liệu 027"      Task: "T048 Tài liệu 028"
Task: "T051 PO 030"            Task: "T052 Architect 030"
Task: "T054 drawio component"  Task: "T055 drawio flow"  Task: "T056 drawio sequence"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 → Phase 2 (kiểm chứng) → Phase 3: dashboard Xử lý sự cố chạy được và export.
2. **DỪNG và kiểm tra**: quickstart Kịch bản 2.

### Incremental Delivery

1. Setup + Phase 2 → biết kỹ thuật nào dùng được; sai thì dừng, hỏi người dùng.
2. US1 → dashboard Xử lý sự cố (MVP).
3. US2 → dashboard Ngân sách tuần.
4. US3 → test canh gác rule 028, link qua lại, bỏ dashboard cũ.
5. US4 → sửa tài liệu và bộ tài liệu 030.
6. Polish → kiểm tra cuối, hỏi trước khi xoá dashboard cũ trên Kibana và trước khi dọn Elastic.
