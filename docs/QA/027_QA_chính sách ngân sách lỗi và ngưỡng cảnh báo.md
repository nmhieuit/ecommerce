# QA: Chính sách ngân sách lỗi (error budget) và ngưỡng cảnh báo gắn với SLO từng service

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. **US1 — chính sách bằng con số**: khối `error-budget-policy` ngay sau `slos` trong cả 7 `service-manifest.yaml` — 4 ngân sách (khả dụng 0.1%, 5xx 0.1%, vượt p95 5%, vượt p99 1%), tháng lịch UTC+7, mốc 50/75/100, "cạn" = bất kỳ ngân sách nào 100%, hệ quả (dừng merge tính năng mới) và hồi phục (3 ngày đạt SLO, ngày không traffic tính là đạt, đặt lại đầu tháng không gỡ đóng băng).
2. **US2 — cảnh báo theo mốc**: 3 rule Kibana ES|QL `error-budget-50/75/100` mỗi 5 phút, alert theo (service, ngân sách), hiện ở 2 bảng trên cùng dashboard SLO hằng ngày; công cụ diễn tập `ChaosFaultInjectionMiddleware` (ServiceDefaults, cờ `Chaos:AllowFaultInjection` + header `X-Chaos-Fault: 5xx`).
3. **US3 — trạng thái cạn**: rule 100 ghi sự kiện vào `slo-error-budget-events` khi alert chuyển sang active; rule `error-budget-frozen` suy ra service đang "cạn — ưu tiên độ tin cậy", hiện ở bảng thứ 3.
4. Hạ tầng: Kibana cần `KIBANA_ENCRYPTION_KEY` (`.env` Vùng 2) mới chạy được rule.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — đổi cờ tiêm lỗi rồi bấm Postman; xem kết quả trên dashboard SLO hằng ngày

Dựng stack: `./scripts/local-up.ps1` (cần `KIBANA_ENCRYPTION_KEY` trong `.env`). Tạo index `slo-error-budget-events`, import [`alerts/error-budget-rules.ndjson`](../kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson) và [`dashboards/slo-van-hanh-hang-ngay.ndjson`](../kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson), Enable 4 rule tag `slo-error-budget` ([`alerts/README.md`](../kibana-quan-sat-he-thong/alerts/README.md)).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và [`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), environment **Ecommerce - Local**; folder **`27 - Ngân sách lỗi: tiêm 5xx và cảnh báo (error budget)`** — không cần token.

**Công tắc cấu hình**: `CHAOS_ALLOW_FAULT_INJECTION` trong `.env` (xem `.env.example`; cả 2 file compose, mặc định `false` = tắt), rồi `docker compose -f docker-compose.local.yml up -d --force-recreate --no-deps orders-api` và chờ healthy. **Khôi phục mặc định**: xoá dòng đó khỏi `.env` và tạo lại `orders-api`.

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-10-01)** |
|---|---|---|---|---|
| Kibana chạy được rule | `KIBANA_ENCRYPTION_KEY` có trong `.env` (mặc định của `.env.example`) | `27c` bước 01 | `has_permanent_encryption_key: true` | `true` |
| Bất biến 2 — cờ TẮT bỏ qua header | Mặc định (container in `false`) | `27a` bước 01 | `200` | `200` |
| Bất biến 3, 4 — cờ BẬT | `CHAOS_ALLOW_FAULT_INJECTION=true` rồi tạo lại `orders-api` | `27b` bước 01, 02, 03 | `5xx` → `500`; không header / giá trị `500` → `200` | `500` / `200` / `200` |
| Bất biến 5 — 500 đi qua OTel | (như trên) | `27b` bước 04 | Có span 5xx `Orders.Api` trong 10 phút | Có (span `GET /health/live`, `status_code = 500`) |
| US2 — mốc 50/75/100 bật trong ≤ 5 phút | (như trên), gửi lặp `27b` bước 01 | `27c` bước 02, 03 | Alert mỗi mốc đã vượt cho `availability`, `error-rate` | Lỗi 1 → 80,39%: mốc 75 sau 4 phút 20 giây, mốc 50 sau 4 phút 59 giây; lỗi 2 → 159,62%: mốc 100 sau 4 phút 16 giây |
| FR-007 — alert giữ active | (như trên) | `27c` bước 03 lần nữa sau 5 phút | Alert 75 vẫn active, `start` không đổi | Có (`start` 06:55:00, cập nhật 07:00:00) |
| US3 — trạng thái cạn | (như trên) | `27c` bước 04 | `Orders.Api` có trong danh sách cạn | Có (07:09:01, cùng 5 service cạn vì độ trễ/5xx thật) |
| Kịch bản Jira 3 — nhìn thấy trên dashboard | (không đổi) | (không có) — mở dashboard SLO hằng ngày | 3 bảng trên cùng có dữ liệu | Mức tiêu hao 28 hàng, alert active, bảng cạn; bảng SLO của 021 vẫn hiện |
| Dọn dẹp | Xoá `CHAOS_ALLOW_FAULT_INJECTION` khỏi `.env`, tạo lại `orders-api` | `27a` bước 01 | `200` | Container in `false`, `200` |

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001 — có khối ngay sau `slos` (7 service) | [`ErrorBudgetPolicyTests.cs:42`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L42) — `EveryService_DeclaresThePolicy_RightAfterSlos` | `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~ErrorBudgetPolicyTests` |
| FR-002 — đúng 4 ngân sách, đúng tỷ lệ | [`:70`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L70) — `EveryService_DeclaresExactlyTheFourBudgets_WithContractValues` | (lệnh như trên) |
| FR-003 — tháng lịch UTC+7 | [`:102`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L102) — `EveryService_UsesACalendarMonthInVietnamTime` | (lệnh như trên) |
| FR-004/006 — mốc và "cạn" bằng con số | [`:125`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L125) — `EveryService_DefinesThresholdsAndExhaustionNumerically` | (lệnh như trên) |
| FR-009 — ai dừng, dừng cái gì | [`:148`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L148) — `EveryService_NamesWhoStopsWhatWhenExhausted` | (lệnh như trên) |
| FR-010 — hồi phục 3 ngày | [`:175`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L175) — `EveryService_DefinesRecoveryAsThreeDaysMeetingSlo` | (lệnh như trên) |
| FR-013 — không khai báo lại ngưỡng độ trễ | [`:203`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L203) — `EveryService_PolicyCarriesNoKeysBeyondTheContract` | (lệnh như trên) |
| FR-006 — 3 rule mốc, 5 phút, tag | [`ErrorBudgetRuleDefinitionTests.cs:48`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L48) — `ThresholdRule_IsExported_EveryFiveMinutes_WithTheTag` · [`:68`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L68) — `ThresholdRule_FiltersOnItsOwnThreshold` | `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~ErrorBudgetRuleDefinitionTests` |
| FR-013 — ngưỡng độ trễ trong rule khớp manifest | [`:90`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L90) — `ThresholdRule_LatencyThresholdsMatchEveryManifest` | (lệnh như trên) |
| FR-002 — tỷ lệ cho phép trong rule | [`:118`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L118) — `ThresholdRule_AllowedRatiosMatchTheContract` | (lệnh như trên) |
| FR-007 — mã alert ổn định | [`:142`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L142) — `ThresholdRule_ReturnsOnlyTheAlertIdentityColumns` | (lệnh như trên) |
| FR-010/011 — rule đóng băng và sự kiện cạn | [`:158`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L158) — `FrozenRule_IsExported_EveryFiveMinutes_KeepingOnlyTheService` · [`:182`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L182) — `ExhaustionRule_WritesAnEventOnlyWhenAnAlertBecomesActive` | (lệnh như trên) |
| Bất biến 2 — cờ tắt bỏ qua header | [`ChaosFaultInjectionMiddlewareTests.cs:29`](../../shared/ServiceDefaults.UnitTests/ChaosFaultInjectionMiddlewareTests.cs#L29) — `InvokeAsync_InjectionDisabled_IgnoresHeaderEntirely` | `dotnet test shared/ServiceDefaults.UnitTests --filter FullyQualifiedName~ChaosFaultInjection` |
| Bất biến 3 — cờ bật + `5xx` → 500, không gọi `next` | [`:51`](../../shared/ServiceDefaults.UnitTests/ChaosFaultInjectionMiddlewareTests.cs#L51) — `InvokeAsync_EnabledWithFaultHeader_Returns500WithoutCallingNext` | (lệnh như trên) |
| Bất biến 4 — không header / giá trị khác → đi tiếp | [`:73`](../../shared/ServiceDefaults.UnitTests/ChaosFaultInjectionMiddlewareTests.cs#L73) — `InvokeAsync_EnabledWithoutHeader_CallsNext` · [`:97`](../../shared/ServiceDefaults.UnitTests/ChaosFaultInjectionMiddlewareTests.cs#L97) — `InvokeAsync_EnabledWithOtherHeaderValue_CallsNext` | (lệnh như trên) |
| Bất biến 6 / FR-015 — không đổi response | [`:116`](../../shared/ServiceDefaults.UnitTests/ChaosFaultInjectionMiddlewareTests.cs#L116) — `InvokeAsync_NoInjection_LeavesDownstreamResponseUntouched` | (lệnh như trên) |

**Kết quả lượt QA này (2026-10-01)**: `ServiceManifestSloConventionTests` 95/95 xanh (49 chính sách + 17 rule + 29 của 021), `ServiceDefaults.UnitTests` 21/21 xanh; thử phá ngưỡng p95 Bff.Api trong file export (300 → 350 ms) làm 3 test rule đỏ đúng, hoàn tác lại xanh. Folder Postman 27: `27a` 1/1, `27b` 5/5, `27c` 5/5 assertion đạt.

## Kết luận

**PASS kèm ghi chú.** Chính sách có trong cả 7 manifest và được test canh hình dạng lẫn con số; ba mốc cảnh báo bật đúng trong vòng một chu kỳ 5 phút trên Kibana thật, giữ active liên tục, hiện ngay trên dashboard người vận hành mở mỗi ngày; trạng thái cạn được suy ra tự động và phép đếm hồi phục đúng ở cả 3 trường hợp thử. Ghi chú:
(1) Kibana mất trạng thái alert khi máy quá tải (2 lần, task rule `409`) để lại alert "mồ côi" — bảng dashboard lọc 15 phút để che;
(2) sửa rule `error-budget-100` ghi lại sự kiện "cạn" cho mọi service đang 100%, dời mốc hồi phục;
(3) lưu lượng thấp làm 1 lỗi đã vượt cả mốc 50 và 75 — không tách riêng được hai mốc này ở môi trường local;
(4) hồi phục thật sau 3 ngày và giữ đóng băng qua ranh giới tháng chưa quan sát được trong một phiên;
(5) 4 test đỏ có sẵn ngoài phạm vi 027.
Chi tiết: [QA_Debt.md](QA_Debt.md) mục 027.
