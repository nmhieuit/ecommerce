---

description: "Danh sách task chuyển ngân sách lỗi sang tuần lịch giờ Việt Nam, SLO 99%/1% (spec 029)"
---

# Tasks: Ngân sách lỗi theo tuần lịch giờ Việt Nam (thay thế tháng lịch của đặc tả 027)

**Input**: Tài liệu thiết kế tại `specs/029-error-budget-weekly/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: CÓ. Người dùng chốt "sửa + thêm kiểm tra tuần" (research.md Quyết định 6), và Nguyên tắc III bắt buộc Red-Green. Mỗi test PHẢI chạy thấy ĐỎ trước khi sửa manifest/rule tương ứng. Comment test (tiếng Việt) theo khuôn hiện có (`Kiểm tra` / `Lý do` / `Task nguồn`). Test sửa hoặc thêm ghi `Task nguồn: spec 029 (ngân sách lỗi theo tuần lịch) — FR-xxx`.

**Organization**: Nhóm theo user story của spec.md.

**Không commit** (người dùng chốt): chỉ ghi file. Người dùng tự xem và commit.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: chạy song song được (khác file, không phụ thuộc task chưa xong)
- **[Story]**: US1–US4 của spec.md

## Path Conventions

- Manifest: `services/<dir>/src/<Name>.Api/service-manifest.yaml`, với 7 cặp `parties/Parties`, `products/Products`, `baskets/Baskets`, `orders/Orders`, `identity/Identity`, `gateway/Gateway`, `bff/Bff`.
- Convention test: `tests/ServiceManifestSloConventionTests/`.
- Export Kibana: `docs/kibana-quan-sat-he-thong/alerts/`, `docs/kibana-quan-sat-he-thong/dashboards/`.
- Kibana `http://localhost:5601`, Elasticsearch `http://localhost:9200`, dựng bằng `./scripts/local-up.ps1`.
- Lệnh tìm phạm vi (dùng ở T002 và T055), gọi là **LỆNH-TÌM**:
  `grep -rlI -E "calendar-month|ngân sách (lỗi )?tháng|1 month|monthly availability|# monthly|99\.9 ?%|0\.1 ?%|0\.001|err_pct >= 0\.1" --exclude-dir=.git --exclude-dir=node_modules --exclude-dir=.claude --exclude-dir=bin --exclude-dir=obj .`

---

## Phase 1: Setup

**Purpose**: Ghi mốc trạng thái hiện có trước khi sửa.

- [X] T001 Chạy `dotnet test tests/ServiceManifestSloConventionTests` và `dotnet test shared/ServiceDefaults.UnitTests`, xác nhận cả hai xanh và ghi số test vào ghi chú task này. Nếu đỏ: dừng và báo người dùng. 4 test đỏ có sẵn ở `CrossServiceIsolation.Tests`/`Orders.Api.UnitTests` (QA_Debt 027) không thuộc hai dự án này. **Kết quả 2026-10-05: XANH 95/95 và 21/21.**
- [X] T002 Chạy LỆNH-TÌM, lưu danh sách file vào scratchpad của phiên (không vào repo) làm mốc cho T055, đối chiếu với danh sách ở plan.md mục "Phạm vi sửa tài liệu 021/027/028". File nào có trong kết quả mà plan chưa liệt kê: thêm vào ghi chú task này và hỏi người dùng nó thuộc nhóm "sửa" hay "không sửa" trước khi sang Phase 6. **Kết quả 2026-10-05: 62 file** (gồm 9 file của chính spec 029); mọi file khác đều có trong danh sách plan — không có file lạ.

---

## Phase 2: Foundational (kiểm chứng trên Kibana 9.4.4 thật)

**Purpose**: Chứng minh research.md V1, V2 trước khi đổi rule. **Chặn US2 và US3.** US1 (hiến chương + manifest + test) KHÔNG phụ thuộc phase này.

- [X] T003 Dựng stack mới (`./scripts/local-up.ps1`, Elastic đang trống). Tạo index `slo-error-budget-events` theo lệnh `PUT` ở `docs/kibana-quan-sat-he-thong/07-canh-bao-ngan-sach-loi.md`. Import `alerts/error-budget-rules.ndjson`, `alerts/incident-fast-detection-rule.ndjson`, `dashboards/ngan-sach-loi-tuan.ndjson` (bản hiện tại, theo `alerts/README.md`). Enable 5 rule; rule nào kẹt `pending` thì Disable/Enable lại. Xác nhận `GET /api/alerting/_health` có `has_permanent_encryption_key: true`. **Kết quả 2026-10-05 (người dùng chốt dùng stack `ecomerce-local` đang chạy từ repo chính, xoá riêng sự kiện cũ)**: `has_permanent_encryption_key: true`; 5 rule đã bật sẵn; index `slo-error-budget-events` (21 sự kiện chế độ tháng, mapping động `text`) được xoá và tạo lại với mapping `keyword` đúng file `07`, 0 document. Không import lại ndjson vì rule đã có sẵn trên stack.
- [X] T004 Kiểm chứng V1 trong Discover (ES|QL), theo [quickstart.md](./quickstart.md) Kịch bản 0: tính `DATE_TRUNC(1 week, TO_DATETIME("<t>") + 7 hours) - 7 hours` với 4 thời điểm ở research.md Quyết định 9, so với cột "Đầu tuần mong đợi". **Sai bất kỳ hàng nào: DỪNG toàn bộ US2/US3 và hỏi người dùng chọn cách khác. Không tự đổi biểu thức.** **Kết quả: ĐÚNG** (4/4 hàng, chạy qua `POST /_query` — xem research.md "Kết quả xác minh").
- [X] T005 Kiểm chứng V2. Tạo tạm một rule `.es-query` ES|QL với `timeWindowSize: 7`, `timeWindowUnit: d`, truy vấn `FROM traces-generic.otel-default* | WHERE @timestamp >= DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours | STATS c = COUNT(*), first = MIN(@timestamp) BY resource.attributes.service.name`. Xác nhận bằng event log/rule preview của Kibana rằng bộ lọc thời gian của rule là `now-7d` (không ngắn hơn), nên không thể cắt hụt thứ Hai 00:00 giờ Việt Nam. Xong thì xoá rule tạm. Không chứng minh được: **dừng và hỏi người dùng** (spec Assumptions: không tự nới cửa sổ). **Kết quả: ĐÚNG, có giới hạn** (cửa sổ 7d thấy span 03/10, 1d chỉ thấy 05/10; chưa quan sát trọn 7 ngày vì dữ liệu chỉ có từ 03/10). Đã xoá 2 rule tạm.
- [X] T006 Ghi kết quả V1–V2 (đúng/sai, bằng chứng, thời điểm) vào mục mới "Kết quả xác minh (T004–T005)" ở cuối `specs/029-error-budget-weekly/research.md`. V3 sẽ ghi ở T029.

**Checkpoint**: biểu thức đầu tuần và cửa sổ 7 ngày đã được chứng minh trên Kibana thật.

---

## Phase 3: User Story 1 — Chính sách và hiến chương nói cùng chu kỳ tuần và cùng con số (Priority: P1) 🎯 MVP

**Goal**: Hiến chương 2.0.0, 7 manifest và test cùng nói: tuần lịch UTC+7, SLO 99%/1%, tỷ lệ 1%/1%/5%/1%.

**Independent Test**: `dotnet test tests/ServiceManifestSloConventionTests` xanh. Đọc hiến chương và 1 manifest khớp [contracts/constitution-amendment.md](./contracts/constitution-amendment.md) và [contracts/error-budget-policy-manifest-shape.md](./contracts/error-budget-policy-manifest-shape.md) (quickstart Kịch bản 4).

### Tests for User Story 1 ⚠️ (viết trước, chạy thấy ĐỎ)

- [X] T007 [P] [US1] Sửa `tests/ServiceManifestSloConventionTests/PlatformSloDefaults.cs`: cả hai hồ sơ `Availability: "99%"`, `MaxFiveXxRatio: "1%"` (độ trễ giữ nguyên). Sửa XML doc để dẫn hiến chương 2.0.0. Sửa con số trong comment của `tests/ServiceManifestSloConventionTests/SloDefaultComplianceTests.cs` ("availability 99.9% và max-5xx 0.1%" → "99% và 1%").
- [X] T008 [P] [US1] Sửa `tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs`:
  - bảng mong đợi `availability`/`error-rate` → `("http-5xx", "1%")`;
  - test cửa sổ mong đợi `calendar-week`, đổi tên `EveryService_UsesACalendarMonthInVietnamTime` → `EveryService_UsesACalendarWeekInVietnamTime`;
  - comment `Kiểm tra`/`Lý do` theo FR-001/FR-003 của spec 029, `Task nguồn: spec 029`.

  Sửa comment ví dụ `0.1%` trong `tests/ServiceManifestSloConventionTests/ServiceManifestModel.cs` thành `1%`.
- [X] T009 [US1] Chạy `dotnet test tests/ServiceManifestSloConventionTests`. Xác nhận ĐỎ đúng chỗ: `SloDefaultComplianceTests` cho 7 manifest (thiếu justification vì `slos` còn 99.9%/0.1%), các test tỷ lệ và cửa sổ của `ErrorBudgetPolicyTests` cho 7 service. Ghi số test đỏ vào ghi chú task này. **Kết quả 2026-10-05: ĐỎ 21/95** — `EveryService_DeclaresExactlyTheFourBudgets_WithContractValues` ×7, `EveryService_UsesACalendarWeekInVietnamTime` ×7, `EveryService_MatchesPlatformDefault_OrDocumentsAJustifiedAlternative` ×7; 74 test còn lại xanh.

### Implementation for User Story 1

- [X] T010 [US1] Sửa `.specify/memory/constitution.md` đúng [contracts/constitution-amendment.md](./contracts/constitution-amendment.md):
  - 2 dòng Nguyên tắc VIII;
  - dòng phiên bản `2.0.0 | Ratified 2026-08-13 | Last Amended 2026-10-05`;
  - thay khối Sync Impact Report.

  Trước khi ghi báo cáo, chạy `grep -n "99.9\|monthly\|0.1%" .specify/templates/*.md` và tìm file hướng dẫn agent của repo (vd `CLAUDE.md`, `AGENTS.md`, `.github/copilot-instructions.md`), rồi ghi từng file là "✅ không cần đổi" hoặc "✅ đã cập nhật". Không sửa dòng nào khác của hiến chương.
- [X] T011 [P] [US1] Sửa `services/parties/src/Parties.Api/service-manifest.yaml`:
  - `slos.availability: 99%   # weekly`, `slos.error-rate.max-5xx-ratio: 1%`;
  - khối `error-budget-policy`: comment đầu khối theo contract (dẫn hiến chương 2.0.0 và `specs/029-error-budget-weekly`), `window: calendar-week`, `allowed-bad-ratio` của `availability` và `error-rate` → `1%`;
  - không chạm khoá khác.
- [X] T012 [P] [US1] Như T011 cho `services/products/src/Products.Api/service-manifest.yaml`.
- [X] T013 [P] [US1] Như T011 cho `services/baskets/src/Baskets.Api/service-manifest.yaml`.
- [X] T014 [P] [US1] Như T011 cho `services/orders/src/Orders.Api/service-manifest.yaml`.
- [X] T015 [P] [US1] Như T011 cho `services/identity/src/Identity.Api/service-manifest.yaml`.
- [X] T016 [P] [US1] Như T011 cho `services/gateway/src/Gateway.Api/service-manifest.yaml`.
- [X] T017 [P] [US1] Như T011 cho `services/bff/src/Bff.Api/service-manifest.yaml`.
- [X] T018 [US1] Chạy `dotnet test tests/ServiceManifestSloConventionTests` (toàn bộ). Xác nhận XANH hết, ghi số test. **Kết quả 2026-10-05: XANH 95/95.**

**Checkpoint**: Định nghĩa mới đã viết và được test canh. Giao được độc lập (MVP).

---

## Phase 4: User Story 2 — Cảnh báo mốc và dashboard tính theo tuần lịch (Priority: P1)

**Goal**: 3 rule mốc lọc từ đầu tuần UTC+7, tỷ lệ `0.01`, cửa sổ 7 ngày. 3 panel ngân sách "tuần này", `now-7d`, panel text 99%/tuần. Có folder Postman 29 để đốt ngân sách 7 service.

**Independent Test**: test định nghĩa rule xanh; [quickstart.md](./quickstart.md) Kịch bản 1 trên Kibana thật.

### Tests for User Story 2 ⚠️

- [X] T019 [US2] Sửa `tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs`:
  - `AllowedBadRatios` → `availability`/`error-rate` = `0.01m` (p95 `0.05m`, p99 `0.01m` giữ), sửa comment `0.001` → `0.01`;
  - **thêm** `[Theory]` 3 rule mốc `ThresholdRule_StartsAtMondayMidnightVietnamTime`: ES|QL có đúng một dòng khớp `WHERE @timestamp >= DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours` (bỏ qua khác biệt khoảng trắng) và không chứa `1 month` (contract bất biến 11);
  - **thêm** `[Theory]` 3 rule mốc `ThresholdRule_LooksBackSevenDays`: `params.timeWindowSize == 7` và `params.timeWindowUnit == "d"` (bất biến 12, phần rule mốc), đọc từ `ExportedRule.Attributes`;
  - comment theo khuôn, `Task nguồn: spec 029`.
- [X] T020 [US2] Chạy `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~ErrorBudgetRuleDefinitionTests`. Xác nhận ĐỎ đúng: tỷ lệ (3), đầu tuần (3), cửa sổ 7 ngày (3). Ghi số test đỏ. **Kết quả 2026-10-05: ĐỎ 9/23 đúng 9 test trên, 14 xanh.**

### Implementation for User Story 2

- [X] T021 [US2] Sửa truy vấn mức tiêu hao trong `docs/kibana-quan-sat-he-thong/07-canh-bao-ngan-sach-loi.md`: đầu tuần thay cho đầu tháng (research Quyết định 1), `allowed` `0.01, 0.01, 0.05, 0.01`. Chạy truy vấn trong Discover trên stack T003, xác nhận ra 4 hàng/service có traffic và mẫu số chỉ gồm span từ thứ Hai 00:00 giờ Việt Nam. **Kết quả 2026-10-05**: chạy qua `POST /_query` lúc 08:24Z → 28 hàng; span sớm nhất trong mẫu số `2026-10-05T04:03Z` (sau đầu tuần `2026-10-04T17:00Z`). `Parties.Api` đã có 157 lỗi 5xx thật trong tuần (504%).
- [X] T022 [US2] Trên Kibana, sửa 3 rule `error-budget-50/75/100` (`PUT /api/alerting/rule/{id}`, giữ id trong ndjson). Mỗi rule: truy vấn T021 + `| STATS consumed_pct = MAX(consumed_pct) BY service, budget | WHERE consumed_pct >= <mốc> | KEEP service, budget`, `timeWindowSize: 7`, `timeWindowUnit: d`, chu kỳ `5m`, tag và action của rule 100 giữ nguyên. Sửa trước khi có service nào cạn (QA_Debt 027: sửa rule 100 khi đang cạn làm ghi lại sự kiện). **Kết quả**: 3 rule cập nhật lúc 08:25:20Z (`timeWindowSize 7 d`, 5m, rule 100 giữ 1 action). Lần `PUT` này Kibana **giữ** trạng thái alert (`new: 0`); các alert theo tháng không còn khớp thì `recovered` (vd Orders.Api 08:26:36Z).
- [X] T023 [US2] Sửa dashboard `Ngân sách lỗi tuần — 7 service` (id `2a607bf4-2449-48a1-a2e8-1336ec35a7b7`) qua Saved Objects API, đúng [data-model.md](./data-model.md) mục 6 và spec Clarifications phiên `/speckit-tasks`:
  - saved search `slo-error-budget-consumption`: title "Ngân sách lỗi — mức tiêu hao tuần này (7 service × 4 ngân sách)", `description` "% ngân sách tuần (UTC+7) đã tiêu…", truy vấn = T021;
  - tiêu đề panel "Ngân sách lỗi tuần này — mức tiêu hao (%)" và "Ngân sách lỗi tuần này — cảnh báo đang hoạt động"; panel cạn giữ tiêu đề;
  - `time_range` 3 panel `eb027-*` → `now-7d`;
  - panel text "`99.9%`/tháng" → "`99%`/tuần";
  - không đổi id, vị trí, panel khác. **Kết quả**: 08:28Z. Ngoài 3 panel ngân sách + panel text, người dùng chốt sửa thêm **nhãn cột** "Error-rate — Thực tế (ngưỡng ≤ 0.1%, cả 7 service)" → "(ngưỡng < 1%, cả 7 service)" trong panel "Bảng SLO — 7 service" của 021 (ngoại lệ FR-010, ghi ở spec Clarifications). Đối chiếu trước/sau: 12/12 panel giữ `gridData`, đúng 5 panel đổi, 11 tham chiếu giữ nguyên.
- [X] T024 [US2] Thêm folder `29 - Ngân sách lỗi theo tuần: tiêm 5xx 7 service` vào `postman/ecommerce.postman_collection.v2.json` (đặt sau folder 28), gồm hai folder con:
  - (a) 7 request `GET {{gatewayUrl|bffUrl|productsUrl|basketsUrl|ordersUrl|partiesUrl|identityUrl}}<route nghiệp vụ>` (ban đầu `/health/live`; spec 033 đổi vì health không tính vào ngân sách) kèm header `X-Chaos-Fault: 5xx`, test kỳ vọng `500` (cần cờ bật), chạy theo vòng;
  - (b) 3 truy vấn `POST {{elasticsearchUrl}}/_query` kiểu 27c cho cả 7 service: mức tiêu hao tuần này (truy vấn T021), alert đang hoạt động tag `slo-error-budget`, service đang cạn. Chạy một lần, không theo vòng. **Kết quả**: người dùng chốt tên `29a - Tiêm 5xx 7 service (chạy theo vòng)` / `29b - Trạng thái ngân sách tuần (chạy một lần)`. Chèn vào sau folder 28 bằng thao tác văn bản (collection có chỗ định dạng tay, không serialize lại cả file): +417 dòng, không dòng nào bị xoá.

  Mỗi request có mô tả mục đích/input/output/FR như các folder khác (PR #66/#67). **HỎI LẠI người dùng tên hai folder con trước khi tạo** (đề xuất: `29a - Tiêm 5xx 7 service (chạy theo vòng)`, `29b - Trạng thái ngân sách tuần (chạy một lần)`).
- [X] T025 [US2] Export 3 rule mốc (cùng rule frozen và connector, chưa đổi) ra `docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson`, và dashboard ra `docs/kibana-quan-sat-he-thong/dashboards/ngan-sach-loi-tuan.ndjson`, theo lệnh export trong hai README. **Kết quả**: export lúc 08:32Z (rule) và 08:33Z (dashboard).
- [X] T026 [US2] Chạy `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~ErrorBudgetRuleDefinitionTests`. Xác nhận XANH. **Kết quả: XANH 23/23.**
- [X] T027 [US2] Thực hiện quickstart Kịch bản 1: bật `CHAOS_ALLOW_FAULT_INJECTION=true`, tạo lại 7 service, chạy folder 29a bằng newman theo đợt, chạy 29b sau mỗi đợt. Ghi bằng chứng (thời điểm vượt từng mốc của từng service, khớp bảng mức tiêu hao/bảng alert trên dashboard, span 500 trong Discover, sự kiện `exhausted` của 7 service) vào ghi chú task này để đưa vào QA 029 / QA_Debt 029 ở Phase 7. **Kết quả 2026-10-05** (cờ `CHAOS_ALLOW_FAULT_INJECTION` đã bật sẵn trên container của stack — người dùng xác nhận buổi diễn tập 028 lúc 11:29 đã xong; không tạo lại container, không sửa `.env` của repo chính): đợt 1 `-n 16` (08:35:49–08:36:57Z, 112/112 `500`) → mốc 50 cho 6 service lúc 08:41:30Z (4 phút 33 giây); đợt 2 `-n 8` (08:42:10–08:42:21Z, 56/56) → mốc 75 lúc 08:46:30Z chỉ cho Bff.Api, Gateway.Api (77,4%, 80,45%); 4 service dừng ở 74,5–74,7% vì traffic nền làm tăng mẫu số; đợt 3 `-n 10` (08:46:56–08:47:09Z, 70/70) → mốc 100 cho cả 7 service lúc 08:49:03Z (1 phút 54 giây), mốc 75 của 4 service còn lại bắn trễ một lượt lúc 08:51:30Z (nhảy thẳng qua 75 và 100 trong một chu kỳ — giới hạn lưu lượng thấp). 12 sự kiện `exhausted` lúc 08:49:06Z; rule frozen bật cho 6 service lúc 08:51:24Z (Parties.Api từ 08:36:24Z). Truy vấn `29b` = bảng dashboard ở cùng thời điểm. Parties.Api: alert 100 active liên tục từ trước khi sửa rule nên không ghi sự kiện mới sau khi xoá index; người dùng chốt Disable/Enable rule 100 lúc 08:34:02Z → 2 sự kiện 08:34:06Z, đóng băng 08:36:24Z. Sau Disable/Enable, alert 100 cũ (start 04:33:59Z) của Parties.Api vẫn `active` trong bộ lọc 15 phút tới khoảng 08:46 → bảng alert tạm hiện trùng.

**Checkpoint**: Cảnh báo và dashboard cùng tính theo tuần.

---

## Phase 5: User Story 3 — Đóng băng, hồi phục và phát hiện nhanh dùng SLO mới (Priority: P2)

**Goal**: rule `error-budget-frozen` cửa sổ 14 ngày, ngày không đạt khi 5xx `>= 0.01`; `incident-fast-detection` bắn khi 5xx `>= 1%`.

**Independent Test**: test bất biến 12 (frozen) và 13 xanh; quickstart Kịch bản 2 và 3.

**Phụ thuộc**: rule frozen nằm cùng file export với US2 (T025 trước T030).

### Tests for User Story 3 ⚠️

- [X] T028 [US3] Thêm vào `tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs`:
  - `[Fact] FrozenRule_LooksBackFourteenDays`: `timeWindowSize == 14`, `timeWindowUnit == "d"` (bất biến 12, phần frozen);
  - `[Fact] FrozenRule_DailySloThresholdsMatchTheBudgets`: trong ES|QL của `error-budget-frozen`, biểu thức `day_missed_slo` dùng `bad_5xx / spans >= 0.01`, `bad_p95 / spans > 0.05`, `bad_p99 / spans > 0.01`; các hằng số so với `AllowedBadRatios` (`error-rate`, `latency-p95`, `latency-p99`) thay vì chép tay (bất biến 13). **Kết quả: ĐỎ 2/25** — `Expected: 14, Actual: 62`; `Not found: "TO_DOUBLE(bad_5xx) / spans >= 0.01"`.

  Chạy, xác nhận ĐỎ (2 test), ghi lại.

### Implementation for User Story 3

- [X] T029 [US3] Trên Kibana, sửa rule `error-budget-frozen` (`PUT`, giữ id): `0.001` → `0.01` trong `day_missed_slo`, `timeWindowSize: 14`. Ghi kết quả V3 vào research.md mục "Kết quả xác minh": sau khi sửa rule 100 ở T022 có ghi sự kiện "cạn" giả không. Sửa đoạn mô tả rule frozen trong `docs/kibana-quan-sat-he-thong/07-canh-bao-ngan-sach-loi.md` (cửa sổ 14 ngày, ngưỡng 1%, giới hạn đóng băng > 14 ngày). **Kết quả**: rule frozen cập nhật lúc 08:38:22Z. V3 (sửa rule 100 không ghi sự kiện giả): **đúng** — lần `PUT` 08:25Z giữ trạng thái alert, không ghi sự kiện nào; chỉ Disable/Enable mới ghi.
- [X] T030 [US3] Export lại `docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson` (4 rule + connector). Chạy `dotnet test tests/ServiceManifestSloConventionTests`, xác nhận XANH hết. **Kết quả**: export 08:39Z; `ServiceManifestSloConventionTests` **XANH 103/103**.
- [X] T031 [US3] Trên Kibana, sửa rule `incident-fast-detection` (`err_pct >= 0.1` → `err_pct >= 1`, giữ phần độ trễ và quy tắc gateway). Export ra `docs/kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson` theo `alerts/README.md`. Sửa mô tả ngưỡng trong `docs/kibana-quan-sat-he-thong/08-phat-hien-nhanh-va-xu-ly-su-co.md`. **Kết quả**: rule cập nhật và export lúc 08:39Z (`err_pct >= 1`, 5m/5m, 0 action).
- [X] T032 [US3] Thực hiện quickstart Kịch bản 2 (5xx dưới/trên 1% vào `Orders.Api` khi có tải nền) và Kịch bản 3 (event `exhausted` thử cách 2/5/13 ngày, có ngày 5xx 0.5%; event ngoài 14 ngày). Ghi bằng chứng vào ghi chú task này. Xoá event thử và service giả sau khi xong. **Kết quả 2026-10-05** — Kịch bản 2: cửa sổ 08:51:30–08:56:30Z `Orders.Api` 1/359 = 0,28% → lượt 08:56:30Z **không** bắn cho Orders.Api (6 alert của đợt đốt `recovered`); cửa sổ 08:56:30–09:01:30Z 10/366 = 2,73% → alert mới lúc 09:01:30Z. Products.Api giữ active từ 08:41:30Z dù 0 lỗi trong cửa sổ: `kibana.alert.flapping = true` (Kibana trì hoãn recovered cho alert đổi trạng thái nhiều lần). Phụ tác: 300 request thường của 2a kéo ngân sách 5xx Orders.Api xuống dưới 100% → alert 100 `recovered` 08:54:03Z, 2b đẩy lại → sự kiện `exhausted` mới 08:59:06Z (`exhausted_at` của Orders.Api dời sang 08:59). Kịch bản 3 (index tạm `tmp-029-*`, chạy đúng ES|QL của rule frozen đã export + lọc 14 ngày): A cạn 13 ngày trước không traffic → 12 ngày đạt, không đóng băng; B cạn 2 ngày trước → 1 ngày, đóng băng; C cạn 16 ngày trước → không thấy (giới hạn 14 ngày); D 4 ngày mỗi ngày 5xx 0,5% → 4 ngày đạt, không đóng băng; E hôm qua 5xx 2% → đóng băng. Đã xoá index tạm.

**Checkpoint**: Hệ quả và tín hiệu phát hiện nhất quán với SLO mới.

---

## Phase 6: User Story 4 — Tài liệu 021/027/028 nói đúng chu kỳ và con số mới (Priority: P3)

**Goal**: sửa tại chỗ (FR-016) theo research.md Quyết định 10.

**Independent Test**: LỆNH-TÌM ở T055 chỉ còn file thuộc nhóm "không sửa".

Quy tắc chung cho mọi task phase này:
- **Đổi**: tháng lịch → tuần lịch (thứ Hai 00:00 → Chủ nhật 23:59 UTC+7); `99.9%` → `99%`; `0.1%` → `1%` (khi là SLO/ngân sách 5xx); `0.001` → `0.01`; cửa sổ `31 d` → `7 d`, `62 d` → `14 d`, `now-62d` → `now-7d`; ngưỡng phát hiện nhanh `0.1%` → `1%`.
- **Giữ**: con số độ trễ và mốc 50/75/100.
- **Xoá bằng chứng đo cũ** chỉ ở: tài liệu vận hành và research/tasks/quickstart 027/028 (ràng buộc kỹ thuật rút ra từ bằng chứng chuyển thành quyết định, không xoá).
- **Không sửa**: mục cũ QA_Debt, `docs/dien-tap-chaos-engineering/ket-qua/`, `specs/002-gateway-bff-routing/`.

- [X] T033 [P] [US4] `specs/027-error-budget-alerting/`:
  - Sửa `spec.md`, `plan.md`, `data-model.md`, `contracts/*.md` theo quy tắc chung. Clarifications 027 giữ câu trả lời gốc, thêm một dòng "thay bởi spec 029" ngay dưới câu về tháng lịch.
  - Ở `research.md`:
    - xoá mục "Kết quả xác minh (T005–T008)";
    - chuyển Hệ quả 1–3, `COALESCE` tên service, mapping `duration` (ns) thành mục "Ràng buộc kỹ thuật" (không còn số đo/ngày giờ);
    - sửa Quyết định 2/3/4/5.
  - Ở `tasks.md`: xoá các ghi chú "**Kết quả 2026-10-0x…**", sửa mô tả task theo quy tắc chung.
  - Ở `quickstart.md`: sửa theo tuần/1%, xoá đoạn kết quả.
  - `checklists/requirements.md`: sửa nếu nhắc tháng. **Kết quả**: sửa spec/plan/data-model/contracts/quickstart/tasks/research; Clarifications giữ câu gốc + ghi chú "Thay bởi spec 029"; research: mục "Kết quả xác minh" thay bằng "Ràng buộc kỹ thuật" (giữ Ràng buộc 1–3, mapping, `COALESCE`, mapping index sự kiện); tasks: xoá 7 ghi chú kết quả; tham chiếu "Hệ quả 2/3" ở test, contract 027/028, Architect 028 đổi thành "Ràng buộc 2/3".
- [X] T034 [P] [US4] `specs/028-incident-oncall-drill/`:
  - Sửa `spec.md` (FR/Clarifications nhắc "ngân sách tháng" và ngưỡng 0.1%; giữ câu trả lời gốc kèm dòng "thay bởi spec 029"), `research.md` (xoá phần kết quả đo), `tasks.md` (xoá ghi chú kết quả), `quickstart.md`, `contracts/fast-detection-rule-contract.md` (bất biến 3 → `≥ 1%`), `contracts/incident-record-contract.md`, `plan.md`/`data-model.md` nếu nhắc. **Kết quả**: research: 2 mục "Kết quả xác minh" thay bằng "Ràng buộc kỹ thuật và quyết định" (giữ V1/V4/V6, severity Case, `up --no-deps`, newman, khôi phục loại C, 4 quyết định người dùng); tasks: xoá 8 ghi chú kết quả; ngưỡng 1% ở spec/data-model/research/2 contract; Clarifications thêm 2 ghi chú.
- [X] T035 [P] [US4] `specs/021-declare-service-slos/{spec,research,data-model,tasks}.md`, `specs/021-declare-service-slos/contracts/service-manifest-slo-shape.md`, `specs/021-declare-service-slos/contracts/continuous-measurement-contract.md` (nếu nhắc 99.9%/0.1%/tháng), và `docs/spec-summary-vi/021-declare-service-slos.json`: đổi mặc định 99.9%/0.1% → 99%/1% theo hiến chương 2.0.0. Giữ JSON hợp lệ (kiểm bằng `python -m json.tool`). **Kết quả**: 15 chỗ ở 5 file 021 + JSON tóm tắt (hợp lệ). Dòng `**Input**` (trích Jira gốc) giữ nguyên.
- [X] T036 [P] [US4] Tài liệu Kibana:
  - `docs/kibana-quan-sat-he-thong/06-dashboard-xu-ly-su-co-va-ngan-sach-tuan.md`;
  - `07-canh-bao-ngan-sach-loi.md`: phần còn lại sau T021/T029, gồm xoá bảng diễn tập "1 lỗi = 80,39%…" và mọi số đo cũ, tiêu đề nhóm panel "tuần này", cửa sổ rule 7/14 ngày;
  - `08-phat-hien-nhanh-va-xu-ly-su-co.md` (xoá số đo cũ theo 0.1%);
  - `alerts/README.md` (xoá số đo cũ, giữ cách gỡ rule `pending`);
  - `00-tong-quan-lo-trinh.md` (nếu nhắc). **Kết quả**: `07` viết lại (bảng rule 7 ngày, nhóm panel "tuần này" `now-7d`, rule frozen 14 ngày/0.01 + giới hạn 14 ngày, lưu ý Disable/Enable; xoá toàn bộ mục kết quả đo cũ); `08` (ngưỡng 1%, xoá 2 mục kiểm chứng cũ); `alerts/README.md` (link 029, bỏ số đo cũ, giữ cách gỡ `pending`); `06` (nhãn cột 1%); `00`.
- [X] T037 [P] [US4] `docs/PO/027_PO_chính sách ngân sách lỗi và ngưỡng cảnh báo.md`, `docs/QA/027_QA_chính sách ngân sách lỗi và ngưỡng cảnh báo.md`, `docs/architecture/027_Architect_chính sách ngân sách lỗi và ngưỡng cảnh báo.md`: sửa con số và chu kỳ. Với QA 027, cập nhật nhãn dòng test đổi tên (`EveryService_UsesACalendarWeekInVietnamTime`) và số dòng `#L…` sau Phase 3–5. **HỎI LẠI người dùng trước khi đụng cột "Đã quan sát (2026-10-01)" và mục "Kết quả lượt QA"**: đây là số đo thật ở tài liệu QA, mà người dùng chỉ chốt "sửa con số và chu kỳ" cho nhóm PO/QA/Architect. **Kết quả**: người dùng chốt **xoá số đo cũ** → bỏ cột "Đã quan sát" (QA 027, 028) và đoạn "Kết quả lượt QA" (QA 027); link `#L…` QA 027 cập nhật theo dòng khai báo test hiện tại (14 link), đổi tên `EveryService_UsesACalendarWeekInVietnamTime`.
- [X] T038 [P] [US4] Sửa 3 sơ đồ 027 theo chu kỳ tuần và con số mới (mở file trước, chỉ sửa nhãn chữ, giữ bố cục): `docs/diagrams/027-error-budget-alerting-component.drawio`, `docs/diagrams/027-error-budget-alerting-flow-nghiep-vu.drawio`, `docs/diagrams/027-error-budget-alerting-sequence.drawio`. **Kết quả**: trước khi sửa, fast-forward nhánh lên `master` `c19ac28` (PR #69 sửa drawio 027/028; người dùng chốt merge — fast-forward, không tạo commit). Component/flow: 13 nhãn; sequence: 8 nhãn viết lại không còn giờ/số đo cũ. XML hợp lệ.
- [X] T039 [P] [US4] `docs/PO/028_PO_diễn tập sự cố thật và phản ứng trực sự cố.md`, `docs/QA/028_QA_diễn tập sự cố thật và phản ứng on-call.md`, `docs/architecture/028_Architect_diễn tập sự cố thật và phản ứng on-call.md`, `docs/diagrams/028-incident-oncall-drill-component.drawio`, và 2 drawio 028 còn lại nếu nhắc: sửa "ngân sách tháng" → "ngân sách tuần", ngưỡng phát hiện nhanh 0.1% → 1%. Cột số đo thật trong QA 028: hỏi như T037. **Kết quả**: PO/Architect 028 + drawio component 028 (5xx ≥ 1%); QA 028 bỏ cột "Đã quan sát"; flow/sequence 028 không nhắc tháng/0.1%.
- [X] T040 [P] [US4] `docs/QA/021_QA_khai báo và đo SLO theo từng service.md`, `docs/superpowers/plans/2026-09-08-dashboard-slo-van-hanh-hang-ngay.md`, `docs/superpowers/specs/2026-09-08-dashboard-slo-van-hanh-design.md`: đổi 99.9%/0.1%/tháng theo quy tắc chung. **Kết quả**: 14 chỗ ở QA 021 và 2 tài liệu superpowers.
- [X] T041 [P] [US4] `postman/ecommerce.postman_collection.v2.json` folder 27 và 28:
  - truy vấn ES|QL của `27c` bước 02/03/04 dùng đầu tuần, tỷ lệ `0.01`, ngưỡng ngày `0.01`;
  - tên/mô tả "tháng này" → "tuần này";
  - mọi mô tả nhắc 99.9%/0.1%. **Kết quả**: 15 chỗ trong đoạn văn bản của folder 27/28 (truy vấn `27c` bước 02 dùng đầu tuần + `0.01`, tên "tuần này"); JSON hợp lệ; ngoài folder 29 không còn nhắc tháng/0.1%.

  Không đụng folder 29 (T024). Giữ JSON hợp lệ.
- [X] T042 [US4] `docs/PO/functional-debt.md` và `docs/architecture/technical-debt.md`: sửa con số và chu kỳ ở mục 021/027/028. Ở technical-debt, đánh dấu các dòng của 027/028 đã được 029 giải quyết hoặc thay đổi (ví dụ "lưu lượng thấp vượt mốc nhanh": vẫn còn, nay theo tuần). Không đụng `docs/QA/QA_Debt.md` ở task này. **Kết quả**: functional-debt (027, 028) và technical-debt (027, 028) sửa chu kỳ + ghi chú "Từ 029".

**Checkpoint**: Không tài liệu đang dùng nào còn nói tháng/99.9%/0.1%.

---

## Phase 7: Polish — bộ tài liệu 029 và kiểm tra cuối

**Purpose**: FR-017, theo khuôn 027/028.

- [X] T043 [P] Tạo `docs/architecture/029_Architect_ngân sách lỗi theo tuần lịch.md` theo đúng cấu trúc và văn phong của `docs/architecture/027_Architect_chính sách ngân sách lỗi và ngưỡng cảnh báo.md`. Có mục `## Sơ đồ` với 3 link tới file T046–T048 (dạng `%20` như 021/027). **Kết quả**: `docs/architecture/029_Architect_ngân sách lỗi theo tuần lịch.md` (8 mục, mục 7 Sơ đồ 3 link). Kèm sửa mục "Tham khảo thêm" của Architect 027 (bỏ trích số đo đã xoá khỏi file `07`, trỏ QA_Debt + Architect 029).
- [X] T044 [P] Tạo `docs/QA/029_QA_ngân sách lỗi theo tuần lịch.md` theo khuôn QA 024–028 (đối chiếu `docs/QA/027_QA_*.md`, `docs/QA/028_QA_*.md`):
  - phần **Thủ công** đứng trước **Tự động**;
  - phần thủ công điều khiển bằng `CHAOS_ALLOW_FAULT_INJECTION` (`.env`/compose) và folder Postman 29 (29a/29b), bảng cột `Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | Đã quan sát`, lấy từ ghi chú T027/T032;
  - bảng **Tự động** link từng test tới đúng dòng `#L…` (gồm test mới của T019/T028 và test đã sửa của T007/T008);
  - comment trong các test được nhắc phải là tiếng Việt theo spec 029;
  - mọi phát hiện chỉ ghi ở QA_Debt (T049), không ghi trong file QA; không có khối "Nguồn đối chiếu". **Kết quả**: `docs/QA/029_QA_ngân sách lỗi theo tuần lịch.md` — Thủ công trước Tự động, bảng thủ công 9 bước có cột "Đã quan sát (2026-10-05)", bảng Tự động 8 dòng link `#L…` tới dòng khai báo test; không có "Nguồn đối chiếu"; phát hiện ở QA_Debt.
- [X] T045 [P] Tạo `docs/PO/029_PO_ngân sách lỗi theo tuần lịch.md` theo khuôn `docs/PO/027_PO_chính sách ngân sách lỗi và ngưỡng cảnh báo.md` và `docs/PO/028_PO_*.md`: ngôn ngữ nghiệp vụ, vì sao tuần thay tháng, cam kết mới 99%/1%, hệ quả giữ nguyên, giới hạn. **Kết quả**: `docs/PO/029_PO_ngân sách lỗi theo tuần lịch.md`.
- [X] T046 [P] Tạo `docs/diagrams/029-error-budget-weekly-component.drawio`. Trước khi vẽ, mở `docs/diagrams/027-error-budget-alerting-component.drawio` để chép bố cục, kiểu khối/màu, nhãn tiếng Việt, dạng `<diagram id=...>`. Nội dung: hiến chương 2.0.0 → 7 manifest → test quy ước; 4 rule (7/14 ngày, 5 phút) + rule 028 → index sự kiện → dashboard (3 panel tuần). **Kết quả**: sinh theo đúng kiểu ô/màu/cạnh của drawio 027 (11 khối, 11 cạnh), CRLF, XML hợp lệ. Chưa mở xem hình bằng diagrams.net (không gửi nội dung ra dịch vụ ngoài).
- [X] T047 [P] Tạo `docs/diagrams/029-error-budget-weekly-flow-nghiep-vu.drawio` theo mẫu `docs/diagrams/027-error-budget-alerting-flow-nghiep-vu.drawio` (ngôn ngữ nghiệp vụ, không tên công cụ): request xấu tiêu hao ngân sách tuần (thứ Hai 00:00 giờ VN) → mốc 50/75/100 → cạn → dừng merge → 3 ngày đạt SLO (5xx < 1%) → hồi phục; thứ Hai đặt lại ngân sách nhưng không gỡ đóng băng. **Kết quả**: 8 khối, 8 cạnh, XML hợp lệ.
- [X] T048 [P] Tạo `docs/diagrams/029-error-budget-weekly-sequence.drawio` theo mẫu `docs/diagrams/027-error-budget-alerting-sequence.drawio`: trình tự thật của T027 (bật cờ, tạo lại 7 service, newman folder 29a, OTel → Elasticsearch, rule 5 phút bắn mốc, rule 100 ghi sự kiện, rule frozen, người vận hành xem dashboard). **Kết quả**: 5 làn + 9 bước + ghi chú, số liệu thật T027/T032, XML hợp lệ.
- [X] T049 Thêm mục `## 029 — Ngân sách lỗi theo tuần lịch giờ Việt Nam` vào cuối:
  - `docs/QA/QA_Debt.md`: phát hiện từ T004–T006, T027, T029, T032; giới hạn lưu lượng thấp theo tuần; ranh giới thật 12/10 chưa quan sát; cập nhật dòng tiêu đề "(001-028)" → "(001-029)". Không sửa mục cũ.
  - `docs/architecture/technical-debt.md`: giới hạn đóng băng > 14 ngày tự mất; lưu lượng thấp; rule 028 vẫn không có test; ranh giới thật chưa quan sát.
  - `docs/PO/functional-debt.md`: thêm mục 029 vào cả hai phần `## 1. Điều đặc biệt — bằng chứng…` và phần nợ chức năng, link tới file T045 dạng `%20`. **Kết quả**: QA_Debt mục 029 (12 gạch đầu dòng, tiêu đề "(001-029)"); technical-debt mục 029 ở cả 4 phần (phạm vi đổi, bug thật, giới hạn, đính chính); functional-debt mục 029 ở phần 1 và 2.
- [X] T050 Đặt `CHAOS_ALLOW_FAULT_INJECTION=false` (xoá khỏi `.env`) và tạo lại 7 service. Xác nhận folder 27a (cờ tắt) trả `200`. **Kết quả**: cờ có sẵn từ buổi diễn tập 028 trong `.env` của repo chính; người dùng chốt mình tắt → comment dòng đó, tạo lại 7 container từ `docker-compose.local.yml` của repo chính (bỏ override `.incident-drill61005-112954`) → cả 7 in `false`, folder `27a` → `200`.
- [X] T051 Chạy `dotnet test tests/ServiceManifestSloConventionTests` và `dotnet test shared/ServiceDefaults.UnitTests`, ghi số test xanh. **Kết quả: XANH 103/103 và 21/21.**
- [X] T052 Kiểm tra link trong 3 file 029 (T043–T045), mục 029 của 3 file debt, và QA/Architect 027/028 đã sửa: link tương đối trỏ tới file có thật (giải mã `%20`), link `#L…` trỏ đúng dòng khai báo test. **Kết quả**: 40 file Markdown đã sửa/tạo, 282 link tương đối, 1 link `#L` của QA_Debt kiểm dòng; 6 link hỏng **có sẵn từ trước** ở phần không sửa (2 ở mục cũ QA_Debt, 4 ở `docs/superpowers/plans/2026-09-08-…md`) — ngoài phạm vi 029, đề xuất task riêng. Link `#L…` của QA 027 và QA 029 trỏ đúng dòng khai báo test (kiểm khi viết).
- [X] T053 Kiểm tra 4 file JSON/ndjson đã sửa (`postman/ecommerce.postman_collection.v2.json`, `docs/spec-summary-vi/021-declare-service-slos.json`, 2 ndjson rule, 1 ndjson dashboard) parse được từng dòng/từng file. **Kết quả**: 3 ndjson (6/2/8 dòng) và 2 JSON parse được.
- [X] T054 Chạy lại toàn bộ [quickstart.md](./quickstart.md) liền mạch trên stack import từ ndjson đã export (Chuẩn bị → Kịch bản 0–4), xác nhận rule import chạy được với cấu hình mới. **Kết quả (người dùng chốt bản rút gọn — import lại sẽ đặt lại trạng thái alert và ghi lại sự kiện "cạn")**: so 10 object (5 rule: tên, chu kỳ, ES|QL, cửa sổ, tag, số action; 4 saved search + dashboard: mọi thuộc tính) giữa ndjson và Kibana đang chạy → **0 chỗ lệch**. Sau đó ndjson được dùng thật ở T056 (import vào Elastic trống: 5 + 1 + 7 object thành công, 5 rule `ok` ngay lượt đầu, không rule nào kẹt `pending`).
- [X] T055 Chạy LỆNH-TÌM, so với mốc T002. Mọi file còn lại phải thuộc nhóm "không sửa" (mục cũ QA_Debt, `ket-qua/`, `specs/002`), hoặc là phần lịch sử phiên bản của hiến chương, hoặc là spec 029 (nơi mô tả giá trị cũ → mới). Ghi danh sách còn lại vào ghi chú task này (SC-006). **Kết quả**: còn lại hợp lệ: Sync Impact Report của hiến chương; mục cũ QA_Debt; `specs/002`; dòng `**Input**` (trích Jira) của spec 021; câu Clarifications gốc của spec 027/028 (có ghi chú "Thay bởi spec 029"); tài liệu/mục 029 mô tả giá trị cũ → mới (PO 029, technical-debt mục 029, comment test). Phát hiện sót 1 chỗ: mô tả saved search `incident-fast-detection-active-alerts` ghi "5xx ≥ 0.1%" → sửa thành 1% trên Kibana và export lại dashboard.
- [X] T056 **HỎI NGƯỜI DÙNG** có dọn toàn bộ Elastic bây giờ không (FR-018). Chỉ thực hiện khi người dùng xác nhận rõ ràng ngay lúc đó, và nêu chính xác lệnh/volume sẽ xoá trước khi chạy. **Kết quả**: người dùng chốt dọn — xoá volume ES, xác nhận lần hai sau khi được báo sẽ mất 1 Kibana Case "[SEV1] Sự cố diễn tập 20261003-184227 — orders-api" (đã đóng, không có trong ndjson). Từ repo chính: `stop kibana otel-collector elasticsearch` → `rm -f elasticsearch` → `docker volume rm ecomerce-local_local-es-data` → `up -d --wait elasticsearch kibana otel-collector` → tạo index `slo-error-budget-events` (mapping `keyword`) → import 3 ndjson → Enable 5 rule (09:47:27Z) → cả 5 `ok`; traces mới của 7 service về từ 09:45Z.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1**: không phụ thuộc.
- **Phase 2**: sau Phase 1. Chặn US2 và US3, KHÔNG chặn US1.
- **Phase 3 (US1)**: chỉ cần Phase 1.
- **Phase 4 (US2)**: cần Phase 2 (V1, V2 đúng).
- **Phase 5 (US3)**: cần T025 của US2 (cùng file export rule). T031 (rule 028) chỉ cần Phase 2.
- **Phase 6 (US4)**: T033–T042 cần các giá trị cuối của Phase 3–5 để mô tả đúng. Riêng số dòng `#L…` (T037) phải làm sau khi test đã xong. T036 làm sau T021/T029/T031.
- **Phase 7**: sau Phase 3–6.

### Within Each User Story

- Test sửa/thêm trước, PHẢI đỏ (T009, T020, T028) trước khi sửa manifest/rule.
- Truy vấn đã đối chiếu (T021) → rule (T022) → dashboard (T023) → export (T025) → test xanh (T026).

### Parallel Opportunities

- T007 ∥ T008.
- T011–T017: 7 manifest, song song hoàn toàn.
- US1 (Phase 3) song song với Phase 2.
- T033–T041: khác file, song song.
- T043–T048: khác file, song song.

---

## Parallel Example: User Story 1

```text
Task: "T011 Sửa services/parties/src/Parties.Api/service-manifest.yaml"
Task: "T012 Sửa services/products/src/Products.Api/service-manifest.yaml"
Task: "T013 ... baskets"  Task: "T014 ... orders"  Task: "T015 ... identity"
Task: "T016 ... gateway"  Task: "T017 ... bff"
```

## Parallel Example: User Story 4

```text
Task: "T033 specs/027-error-budget-alerting/*"
Task: "T034 specs/028-incident-oncall-drill/*"
Task: "T035 specs/021-declare-service-slos/* + spec-summary-vi"
Task: "T038 3 drawio 027"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 → Phase 3: hiến chương, manifest và test thống nhất.
2. **DỪNG và kiểm tra**: quickstart Kịch bản 4.

### Incremental Delivery

1. Setup + US1 → MVP (định nghĩa mới có test canh).
2. Phase 2 (V1, V2) → sai thì dừng, hỏi người dùng.
3. US2 → cảnh báo + dashboard theo tuần, đốt ngân sách 7 service.
4. US3 → đóng băng + phát hiện nhanh theo SLO mới.
5. US4 → sửa tài liệu 021/027/028.
6. Polish → tài liệu 029, kiểm tra cuối, hỏi trước khi dọn Elastic.

---

## Notes

- Task cần Kibana/Docker thật (T003–T006, T021–T023, T025, T027, T029–T032, T050, T054, T056) chỉ đánh `[X]` khi đã chạy thật và có bằng chứng ghi lại.
- `CHAOS_ALLOW_FAULT_INJECTION` phải về `false` sau diễn tập (T050).
- Ba điểm phải HỎI LẠI người dùng trong lúc triển khai:
  - T024: tên folder con;
  - T037/T039: cột số đo thật trong QA 027/028;
  - T056: dọn Elastic.
- Mọi kết quả kiểm chứng sai ở T004/T005 dẫn tới DỪNG.
