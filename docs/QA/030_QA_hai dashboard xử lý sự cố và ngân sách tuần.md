# QA: Tách dashboard SLO thành "Xử lý sự cố" và "Ngân sách lỗi tuần"

> **Cập nhật spec 033**: công thức ngân sách loại span có đường dẫn bắt đầu bằng `/health`; việc tiêm lỗi/độ trễ nay đi vào route nghiệp vụ. Xem [033 QA](033_QA_loại%20span%20health%20khỏi%20ngân%20sách%20lỗi.md).

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. **US1 — Xử lý sự cố đi theo thanh thời gian**: dashboard `Xử lý sự cố — 7 service` (mặc định 1 giờ, tự làm mới 1 phút): Bảng SLO + ngưỡng,
   Phát hiện nhanh (có cột trạng thái), 5xx/p95/traffic + 401/403 theo phút, `dotnet.exceptions`, status code, top endpoint chậm, lỗi gọi hạ lưu, log lỗi (Error trở lên, link `trace_id` mở Discover).
2. **US2 — Ngân sách tuần cố định tuần lịch**: dashboard `Ngân sách lỗi tuần — 7 service`: điều khiển Tuần (`Tuần này`/`Tuần trước`/`2 tuần trước`/`3 tuần trước`), mức tiêu hao,
   hạn mức còn lại, cảnh báo mốc + cạn (trạng thái hiện tại), error-rate/p95 theo ngày, tiêu hao lũy kế; không đổi theo thanh thời gian.
3. **US3 — bỏ dashboard cũ, link hai chiều, test canh gác rule 028**: hai file ndjson độc lập; ô Markdown link qua lại; không còn dashboard `e2e06ff5-…`; test `IncidentFastDetectionRuleDefinitionTests`.
4. **US4 — tài liệu**: PO/QA/Architect 030, mục 030 trong 3 file debt, 3 sơ đồ drawio, folder Postman 30.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — đổi cờ tiêm lỗi rồi chạy folder Postman 30; xem kết quả trên hai dashboard

Dựng stack: `./scripts/local-up.ps1` (cần `KIBANA_ENCRYPTION_KEY` trong `.env`). Tạo index `slo-error-budget-events`
(lệnh `PUT` ở [`07-canh-bao-ngan-sach-loi.md`](../kibana-quan-sat-he-thong/07-canh-bao-ngan-sach-loi.md)), import
[`alerts/error-budget-rules.ndjson`](../kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson),
[`alerts/incident-fast-detection-rule.ndjson`](../kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson),
rồi hai dashboard [`dashboards/xu-ly-su-co.ndjson`](../kibana-quan-sat-he-thong/dashboards/xu-ly-su-co.ndjson) và
[`dashboards/ngan-sach-loi-tuan.ndjson`](../kibana-quan-sat-he-thong/dashboards/ngan-sach-loi-tuan.ndjson)
(lệnh ở [`dashboards/README.md`](../kibana-quan-sat-he-thong/dashboards/README.md)), Enable 5 rule
([`alerts/README.md`](../kibana-quan-sat-he-thong/alerts/README.md)).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json)
và [`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), environment
**Ecommerce - Local**; folder **`30 - Hai dashboard: tạo lỗi và đối chiếu số liệu`**.
`30a` chạy theo vòng (Runner hoặc `npx.cmd --yes newman@6.2.2 run ... --folder "30a - Tạo lỗi + traffic cho Xử lý sự cố (chạy theo vòng)" -n <số vòng>`),
`30b` chạy một lần.

**Công tắc cấu hình**: `CHAOS_ALLOW_FAULT_INJECTION` (bước 5xx) và `CHAOS_ALLOW_LATENCY_INJECTION` (bước độ trễ) trong `.env` (xem `.env.example`; cả 2 file
compose, mặc định `false` = tắt), rồi tạo lại 7 container API (`docker compose -f docker-compose.local.yml up -d --force-recreate --no-deps gateway-api bff-api products-api baskets-api orders-api parties-api identity-api`)
và chờ healthy. **Khôi phục mặc định**: xoá hai dòng đó khỏi `.env` và tạo lại 7 container; folder 27a xác nhận header bị bỏ qua.

| Bước | Cấu hình cần chỉnh | Request / thao tác | Kỳ vọng theo tài liệu |
|---|---|---|---|
| Hai dashboard import được vào Kibana sạch (SC-006) | (không đổi) | Import hai file ndjson theo thứ tự bất kỳ | Cả hai dashboard mở được; dashboard cũ `SLO vận hành hằng ngày` không tồn tại |
| Xử lý sự cố: mặc định và theo thanh thời gian (FR-002, FR-004) | (không đổi) | Mở `Xử lý sự cố — 7 service`; đổi thanh thời gian giữa 15 phút, 1 giờ, 24 giờ | Mặc định 1 giờ, tự làm mới 1 phút; mọi panel đổi theo; không panel nào giữ cửa sổ cố định |
| Tạo traffic, 401/403 và lời gọi hạ lưu | (không đổi) | `30a` bước 08–13 (theo vòng) | Panel Traffic + 401/403 có chuỗi 401/403; panel Lỗi gọi hạ lưu có cặp (gọi → đích) |
| 5xx theo phút và Phát hiện nhanh (US1, FR-007) | `CHAOS_ALLOW_FAULT_INJECTION=true` + 7 container | `30a` bước 01–07 theo vòng | Service bị lỗi hiện ở Bảng SLO, ở biểu đồ 5xx theo phút, và panel Phát hiện nhanh hiện alert với cột `status` (`active`/`recovered`) |
| p95 theo phút | `CHAOS_ALLOW_LATENCY_INJECTION=true` + 7 container | `30a` bước 14 | Latency p95 theo phút của `Orders.Api` tăng; Bảng SLO vượt ngưỡng |
| Log lỗi, trace_id và correlation id (FR-006, FR-019) | Tạm dừng `products-db`, gọi `GET /health/ready` của `Products.Api`, bật lại `products-db` | (không có) — `docker compose stop products-db` rồi gọi health | Panel Log lỗi gần nhất có log Error kèm `trace_id` và `attributes.CorrelationId`; bấm `trace_id` mở Discover lọc đúng trace |
| Ngân sách tuần không đổi theo thanh thời gian (SC-003) | (không đổi) | Mở `Ngân sách lỗi tuần — 7 service`; đổi thanh thời gian 5 phút / 24 giờ | Số mức tiêu hao và hạn mức còn lại không đổi |
| Chọn tuần và panel trạng thái hiện tại (FR-009) | (không đổi) | Chọn `Tuần trước` | Mức tiêu hao và hạn mức còn lại đổi theo tuần chọn (trống nếu tuần chưa có dữ liệu); hai panel cảnh báo/cạn không đổi và ghi "(trạng thái hiện tại)" |
| Đối chiếu số với Elasticsearch (SC-003) | (không đổi) | `30b` bước 01–03 | Mức tiêu hao và hạn mức còn lại khớp dashboard cùng thời điểm; `remaining_requests = FLOOR(tỷ lệ × tổng − xấu)` (âm nếu vượt); lũy kế không giảm theo ngày |
| Link hai chiều (FR-011) | (không đổi) | Bấm ô Markdown đầu trang ở mỗi dashboard | Chuyển sang dashboard còn lại |
| Dọn dẹp | Xoá hai cờ khỏi `.env`, tạo lại 7 container; bật lại `products-db` | `27a` bước 01 | `200` |

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-015 — rule 028: loại, chu kỳ 5m, cửa sổ 5m, tag, `groupBy` | [`IncidentFastDetectionRuleDefinitionTests.cs:29`](../../tests/ServiceManifestSloConventionTests/IncidentFastDetectionRuleDefinitionTests.cs#L29) — `Rule_IsExported_EveryFiveMinutes_WithTheTag` | `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~IncidentFastDetectionRuleDefinitionTests` |
| FR-015 — rule 028 bắn khi `> 0` service vi phạm | [`:56`](../../tests/ServiceManifestSloConventionTests/IncidentFastDetectionRuleDefinitionTests.cs#L56) — `Rule_AlertsWhenAnyServiceBreaches` | (lệnh như trên) |
| FR-015 — rule 028 chỉ nhìn 5 phút gần nhất | [`:72`](../../tests/ServiceManifestSloConventionTests/IncidentFastDetectionRuleDefinitionTests.cs#L72) — `Rule_LooksAtTheLastFiveMinutesOnly` | (lệnh như trên) |
| FR-015 — ngưỡng 5xx = SLO manifest (1%) | [`:88`](../../tests/ServiceManifestSloConventionTests/IncidentFastDetectionRuleDefinitionTests.cs#L88) — `Rule_FiveXxThresholdMatchesEveryManifest` | (lệnh như trên) |
| FR-015 — ngưỡng p95/p99 theo từng service = manifest | [`:119`](../../tests/ServiceManifestSloConventionTests/IncidentFastDetectionRuleDefinitionTests.cs#L119) — `Rule_LatencyThresholdsMatchEveryManifest` | (lệnh như trên) |
| FR-015 — gateway chỉ xét 5xx | [`:144`](../../tests/ServiceManifestSloConventionTests/IncidentFastDetectionRuleDefinitionTests.cs#L144) — `Rule_GatewayIsJudgedOnFiveXxOnly` | (lệnh như trên) |
| FR-015 — kết quả chỉ giữ cột `service` | [`:160`](../../tests/ServiceManifestSloConventionTests/IncidentFastDetectionRuleDefinitionTests.cs#L160) — `Rule_ReturnsOnlyTheAlertIdentityColumn` | (lệnh như trên) |
| FR-015 — test 027 vẫn khớp chu kỳ tuần | [`ErrorBudgetRuleDefinitionTests.cs:145`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L145) — `ThresholdRule_StartsAtMondayMidnightVietnamTime` · [`:167`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L167) — `ThresholdRule_LooksBackSevenDays` | `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~ErrorBudgetRuleDefinitionTests` |

**Kết quả lượt QA này (2026-10-05)**: `ServiceManifestSloConventionTests` 110/110 xanh (7 test mới của 030 chạy thấy đỏ từng cái khi đổi một giá trị trong file rule rồi hoàn tác);
`ErrorBudgetRuleDefinitionTests` 25/25. Folder Postman 30: `30a` và `30b` chạy được (`30b` 6/6 assertion). Bảng thủ công: không có test tự động cho dashboard (cấu hình Kibana); số đo và phát hiện nằm ở QA_Debt.

## Kết luận

**PASS kèm ghi chú.** Hai dashboard chạy trên Kibana thật, panel đi theo đúng cửa sổ thời gian đã chốt, số ngân sách khớp truy vấn
Elasticsearch, test canh gác rule 028 xanh và từng đỏ. Ghi chú:
(1) Kibana APM không đọc dữ liệu trace OTel thô nên link trace mở Discover;
(2) Discover ES|QL bị thanh thời gian cắt thêm nên dashboard tuần đặt `time_range` riêng 30 ngày (chỉ xem lại tối đa 3 tuần trước);
(3) link hai chiều là ô Markdown theo id cố định;
(4) panel log lỗi hiển thị nội dung log có thể chứa dữ liệu nhạy cảm;
(5) chưa quan sát ranh giới thứ Hai 00:00 thật trên dashboard tuần.
Chi tiết: [QA_Debt.md](QA_Debt.md) mục 030.
