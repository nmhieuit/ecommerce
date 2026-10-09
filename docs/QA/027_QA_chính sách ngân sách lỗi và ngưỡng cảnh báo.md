# QA: Chính sách ngân sách lỗi (error budget) và ngưỡng cảnh báo gắn với SLO từng service

> **Cập nhật spec 033**: công thức ngân sách loại span có đường dẫn bắt đầu bằng `/health`; việc tiêm lỗi/độ trễ nay đi vào route nghiệp vụ. Xem [033 QA](033_QA_loại%20span%20health%20khỏi%20ngân%20sách%20lỗi.md).

> **Cập nhật nhánh fix/frozen-panel-status (2026-10-09)**: hồi phục không còn đếm "3 ngày đạt SLO" mà theo mức tiêu hao tuần — `active` (≥ 100%) / `recovering` (75–99% hoặc tuần chưa có request; vẫn đóng băng) / `recovered` (dưới 75%, giữ tới lần cạn kế tiếp); bảng thứ 3 có cột `status`. Bằng chứng: [QA_Debt.md](QA_Debt.md) mục "fix/frozen-panel-status".

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. **US1 — chính sách bằng con số**: khối `error-budget-policy` ngay sau `slos` trong cả 7 `service-manifest.yaml` — 4 ngân sách (khả dụng 1%, 5xx 1%, vượt p95 5%, vượt p99 1%), tuần lịch UTC+7, mốc 50/75/100, "cạn" = bất kỳ ngân sách nào 100%, hệ quả (dừng merge tính năng mới) và hồi phục (mức tiêu hao cao nhất của tuần dưới 75% với ít nhất 1 request; `recovering` vẫn đóng băng; `recovered` giữ tới lần cạn kế tiếp; đặt lại đầu tuần không gỡ đóng băng).
2. **US2 — cảnh báo theo mốc**: 3 rule Kibana ES|QL `error-budget-50/75/100` mỗi 5 phút, alert theo (service, ngân sách), hiện ở 2 bảng trên cùng dashboard Ngân sách lỗi tuần; công cụ diễn tập `ChaosFaultInjectionMiddleware` (ServiceDefaults, cờ `Chaos:AllowFaultInjection` + header `X-Chaos-Fault: 5xx`).
3. **US3 — trạng thái cạn**: rule 100 ghi sự kiện vào `slo-error-budget-events` khi alert chuyển sang active; rule `error-budget-frozen` suy ra service đang "cạn — ưu tiên độ tin cậy" và ghi sự kiện `recovered` khi gỡ đóng băng; bảng thứ 3 hiện từng service với `status` active/recovering/recovered.
4. Hạ tầng: Kibana cần `KIBANA_ENCRYPTION_KEY` (`.env` Vùng 2) mới chạy được rule.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — đổi cờ tiêm lỗi rồi bấm Postman; xem kết quả trên dashboard Ngân sách lỗi tuần

Dựng stack: `./scripts/local-up.ps1` (cần `KIBANA_ENCRYPTION_KEY` trong `.env`). Tạo index `slo-error-budget-events`, import [`alerts/error-budget-rules.ndjson`](../kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson) và [`dashboards/ngan-sach-loi-tuan.ndjson`](../kibana-quan-sat-he-thong/dashboards/ngan-sach-loi-tuan.ndjson), Enable 4 rule tag `slo-error-budget` ([`alerts/README.md`](../kibana-quan-sat-he-thong/alerts/README.md)).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và [`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), environment **Ecommerce - Local**; folder **`27 - Ngân sách lỗi: tiêm 5xx và cảnh báo (error budget)`** — không cần token.

**Công tắc cấu hình**: `CHAOS_ALLOW_FAULT_INJECTION` trong `.env` (xem `.env.example`; cả 2 file compose, mặc định `false` = tắt), rồi `docker compose -f docker-compose.local.yml up -d --force-recreate --no-deps orders-api` và chờ healthy. **Khôi phục mặc định**: xoá dòng đó khỏi `.env` và tạo lại `orders-api`.

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu |
|---|---|---|---|
| Kibana chạy được rule | `KIBANA_ENCRYPTION_KEY` có trong `.env` (mặc định của `.env.example`) | `27c` bước 01 | `has_permanent_encryption_key: true` |
| Bất biến 2 — cờ TẮT bỏ qua header | Mặc định (container in `false`) | `27a` bước 01 | `200` |
| Bất biến 3, 4 — cờ BẬT | `CHAOS_ALLOW_FAULT_INJECTION=true` rồi tạo lại `orders-api` | `27b` bước 01, 02, 03 | `5xx` → `500`; không header / giá trị `500` → `200` |
| Bất biến 5 — 500 đi qua OTel | (như trên) | `27b` bước 04 | Có span 5xx `Orders.Api` trong 10 phút |
| US2 — mốc 50/75/100 bật trong ≤ 5 phút | (như trên), gửi lặp `27b` bước 01 | `27c` bước 02, 03 | Alert mỗi mốc đã vượt cho `availability`, `error-rate` |
| FR-007 — alert giữ active | (như trên) | `27c` bước 03 lần nữa sau 5 phút | Alert 75 vẫn active, `start` không đổi |
| US3 — trạng thái cạn | (như trên) | `27c` bước 04 | `Orders.Api` có trong danh sách cạn với `status = active` (cùng truy vấn bảng thứ 3 của dashboard) |
| Kịch bản Jira 3 — nhìn thấy trên dashboard | (không đổi) | (không có) — mở dashboard Ngân sách lỗi tuần | 3 bảng trên cùng có dữ liệu |
| Dọn dẹp | Xoá `CHAOS_ALLOW_FAULT_INJECTION` khỏi `.env`, tạo lại `orders-api` | `27a` bước 01 | `200` |

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001 — có khối ngay sau `slos` (7 service) | [`ErrorBudgetPolicyTests.cs:43`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L43) — `EveryService_DeclaresThePolicy_RightAfterSlos` | `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~ErrorBudgetPolicyTests` |
| FR-002 — đúng 4 ngân sách, đúng tỷ lệ | [`:73`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L73) — `EveryService_DeclaresExactlyTheFourBudgets_WithContractValues` | (lệnh như trên) |
| FR-003 — tuần lịch UTC+7 | [`:105`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L105) — `EveryService_UsesACalendarWeekInVietnamTime` | (lệnh như trên) |
| FR-004/006 — mốc và "cạn" bằng con số | [`:128`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L128) — `EveryService_DefinesThresholdsAndExhaustionNumerically` | (lệnh như trên) |
| FR-009 — ai dừng, dừng cái gì | [`:151`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L151) — `EveryService_NamesWhoStopsWhatWhenExhausted` | (lệnh như trên) |
| FR-010 — hồi phục theo mức tiêu hao tuần | [`:204`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L204) — `EveryService_DefinesRecoveryByWeeklyConsumption` | (lệnh như trên) |
| FR-013 — không khai báo lại ngưỡng độ trễ | [`:206`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L206) — `EveryService_PolicyCarriesNoKeysBeyondTheContract` | (lệnh như trên) |
| FR-006 — 3 rule mốc, 5 phút, tag | [`ErrorBudgetRuleDefinitionTests.cs:49`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L49) — `ThresholdRule_IsExported_EveryFiveMinutes_WithTheTag` · [`:69`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L69) — `ThresholdRule_FiltersOnItsOwnThreshold` | `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~ErrorBudgetRuleDefinitionTests` |
| FR-013 — ngưỡng độ trễ trong rule khớp manifest | [`:91`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L91) — `ThresholdRule_LatencyThresholdsMatchEveryManifest` | (lệnh như trên) |
| FR-002 — tỷ lệ cho phép trong rule | [`:120`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L120) — `ThresholdRule_AllowedRatiosMatchTheContract` | (lệnh như trên) |
| FR-007 — mã alert ổn định | [`:187`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L187) — `ThresholdRule_ReturnsOnlyTheAlertIdentityColumns` | (lệnh như trên) |
| FR-010/011 — rule đóng băng và sự kiện cạn | [`:203`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L203) — `FrozenRule_IsExported_EveryFiveMinutes_KeepingOnlyTheService` · [`:486`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L486) — `ExhaustionRule_WritesAnEventOnlyWhenAnAlertBecomesActive` | (lệnh như trên) |
| FR-010 — rule gỡ đóng băng dưới 75%, giữ `recovered`, ghi sự kiện `recovered` | [`:243`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L243) — `FrozenRule_UnfreezesBelowTheManifestRecoveryThreshold` · [`:275`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L275) — `FrozenRule_ConsumptionUsesTheBudgetRatios` · [`:297`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L297) — `FrozenRule_StaysRecoveredUntilTheNextExhaustion` · [`:314`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L314) — `FrozenRule_WritesARecoveredEventWhenTheFreezeLifts` | (lệnh như trên) |
| FR-010/011 — bảng thứ 3 có `status` active/recovering/recovered | [`FrozenPanelStatusTests.cs:23`](../../tests/ServiceManifestSloConventionTests/FrozenPanelStatusTests.cs#L23) — `Panel_ShowsTheStatusColumns` · [`:37`](../../tests/ServiceManifestSloConventionTests/FrozenPanelStatusTests.cs#L37) — `Panel_DerivesTheStatusFromTheAlertAndTheConsumption` · [`:54`](../../tests/ServiceManifestSloConventionTests/FrozenPanelStatusTests.cs#L54) — `Panel_ConsumptionUsesTheBudgetRatios` · [`:71`](../../tests/ServiceManifestSloConventionTests/FrozenPanelStatusTests.cs#L71) — `Panel_ReadsTheCurrentWeekOfServerSpansAndFrozenAlerts` | `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~FrozenPanelStatusTests` |
| Bất biến 2 — cờ tắt bỏ qua header | [`ChaosFaultInjectionMiddlewareTests.cs:29`](../../shared/ServiceDefaults.UnitTests/ChaosFaultInjectionMiddlewareTests.cs#L29) — `InvokeAsync_InjectionDisabled_IgnoresHeaderEntirely` | `dotnet test shared/ServiceDefaults.UnitTests --filter FullyQualifiedName~ChaosFaultInjection` |
| Bất biến 3 — cờ bật + `5xx` → 500, không gọi `next` | [`:51`](../../shared/ServiceDefaults.UnitTests/ChaosFaultInjectionMiddlewareTests.cs#L51) — `InvokeAsync_EnabledWithFaultHeader_Returns500WithoutCallingNext` | (lệnh như trên) |
| Bất biến 4 — không header / giá trị khác → đi tiếp | [`:73`](../../shared/ServiceDefaults.UnitTests/ChaosFaultInjectionMiddlewareTests.cs#L73) — `InvokeAsync_EnabledWithoutHeader_CallsNext` · [`:97`](../../shared/ServiceDefaults.UnitTests/ChaosFaultInjectionMiddlewareTests.cs#L97) — `InvokeAsync_EnabledWithOtherHeaderValue_CallsNext` | (lệnh như trên) |
| Bất biến 6 / FR-015 — không đổi response | [`:116`](../../shared/ServiceDefaults.UnitTests/ChaosFaultInjectionMiddlewareTests.cs#L116) — `InvokeAsync_NoInjection_LeavesDownstreamResponseUntouched` | (lệnh như trên) |


## Kết luận

**PASS kèm ghi chú.** Chính sách có trong cả 7 manifest và được test canh hình dạng lẫn con số; ba mốc cảnh báo bật đúng trong vòng một chu kỳ 5 phút trên Kibana thật, giữ active liên tục, hiện ngay trên dashboard người vận hành mở mỗi ngày; trạng thái cạn được suy ra tự động và phép đếm hồi phục đúng ở cả 3 trường hợp thử. Ghi chú:
(1) Kibana mất trạng thái alert khi máy quá tải (2 lần, task rule `409`) để lại alert "mồ côi" — bảng dashboard lọc 15 phút để che;
(2) sửa rule `error-budget-100` ghi lại sự kiện "cạn" cho mọi service đang 100%, dời mốc hồi phục;
(3) lưu lượng thấp làm 1 lỗi đã vượt cả mốc 50 và 75 — không tách riêng được hai mốc này ở môi trường local;
(4) hồi phục thật và giữ đóng băng qua ranh giới tuần chưa quan sát được trong một phiên (quy tắc hồi phục đổi sang mức tiêu hao tuần ở nhánh fix/frozen-panel-status; lần gỡ đóng băng thật đầu tiên đo ở QA_Debt);
(5) 4 test đỏ có sẵn ngoài phạm vi 027.
Chi tiết: [QA_Debt.md](QA_Debt.md) mục 027.
