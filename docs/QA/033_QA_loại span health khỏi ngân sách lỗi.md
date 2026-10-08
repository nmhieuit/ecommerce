# QA: Loại span health khỏi công thức ngân sách lỗi

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.
Spec: [`specs/033-exclude-health-spans/`](../../specs/033-exclude-health-spans/spec.md).*

## Luồng happy-case đã rà soát

1. **US1 — ngân sách chỉ tính request nghiệp vụ**: 4 rule 027 (`error-budget-50/75/100`, `error-budget-frozen`) và rule 028 loại span có đường dẫn bắt đầu bằng `/health` (điều kiện `NOT (COALESCE(attributes.url.path, "") LIKE "/health*")`).
2. **US2 — hai dashboard khớp rule**: các panel ES|QL của `Ngân sách lỗi tuần` và 6 panel Lens của `Xử lý sự cố` loại cùng tập span.
3. **US3 — health lỗi có chỉ báo riêng**: rule `health-failure` (từ 50% span health của một service trả 5xx trong 5 phút) và panel `Health lỗi theo service`.
4. **US4 — manifest khai báo**: `excluded-path-prefixes: [/health]` trong `error-budget-policy` của cả 7 manifest, test đối chiếu manifest ↔ rule.
5. **US5 — Postman và tài liệu**: 26 request tiêm lỗi/độ trễ đổi sang route nghiệp vụ, folder 33, tài liệu 025–030 sửa tại chỗ, bộ tài liệu 033.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — đổi cờ tiêm lỗi, chạy folder Postman 25/27/29a/30a/33 và xem hai dashboard

Dựng stack, import rule và dashboard như [QA 030](030_QA_hai%20dashboard%20xử%20lý%20sự%20cố%20và%20ngân%20sách%20tuần.md), thêm
[`alerts/health-failure-rule.ndjson`](../kibana-quan-sat-he-thong/alerts/health-failure-rule.ndjson) (6 rule, Enable hết).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json); folder **`33 - Health: loại khỏi ngân sách`**.

**Công tắc cấu hình**: `CHAOS_ALLOW_FAULT_INJECTION` và `CHAOS_ALLOW_LATENCY_INJECTION` trong `.env` (mặc định `false`), rồi tạo lại 7 container API
(`docker compose -f docker-compose.local.yml up -d --force-recreate --no-deps gateway-api bff-api products-api baskets-api orders-api parties-api identity-api`).
**Khôi phục mặc định**: xoá hai dòng khỏi `.env`, tạo lại 7 container; bật lại `products-db` nếu đã dừng.

| Bước | Cấu hình cần chỉnh | Request / thao tác | Kỳ vọng theo tài liệu |
|---|---|---|---|
| Health không tính vào ngân sách (FR-001) | (không đổi) | `33b` bước 01 | Mỗi service có `span_health` lớn và `span_nghiep_vu` nhỏ; mức tiêu hao trên dashboard tuần tính trên `span_nghiep_vu` |
| Health chậm lúc khởi động nguội không làm bắn rule (FR-002) | Tạo lại 7 container | Chờ hai chu kỳ rule (10 phút) | Không rule ngân sách hay rule 028 bắn **vì span health**; nếu có bắn thì do request nghiệp vụ (xem `33b` bước 01) |
| Tạo request nghiệp vụ | (không đổi) | `33a` bước 01–03 | `401`/`401`/`200`; span nghiệp vụ xuất hiện ở dashboard |
| Tiêm 5xx vào route nghiệp vụ (FR-009) | `CHAOS_ALLOW_FAULT_INJECTION=true` + 7 container | `29a` hoặc `30a` bước 01–07 theo vòng | `500` ở cả 7 service; mốc 50/75/100 bật theo mức tiêu hao; Bảng SLO và 5xx theo phút hiện service lỗi |
| Tiêm độ trễ vào route nghiệp vụ (FR-009) | `CHAOS_ALLOW_LATENCY_INJECTION=true` + 7 container | Folder `25` | p95 `Orders.Api` tăng; request cờ TẮT (`25a` bước 01) không trễ |
| Cờ TẮT bỏ qua header (FR-009) | (cờ tắt) | `27a` bước 01 | Không bị ép `500` (route nghiệp vụ cần token nên `401`) |
| Health lỗi có chỉ báo riêng (FR-006, FR-007) | Tạm dừng DB của một service (`docker compose stop products-db`), chờ > 5 phút | Gọi `GET /health/ready` của `Products.Api`; xem panel `Health lỗi theo service`, `33b` bước 02 | `503`; panel hiện `Products.Api` với `health_5xx_pct` ≥ 50; rule `health-failure` bắn **chỉ** cho service đó; không rule ngân sách nào bắn vì health. Bật lại DB: health `200` |
| Đối chiếu số dashboard (FR-008) | (không đổi) | `30b` bước 01–03 và `33b` | Số khớp dashboard cùng thời điểm; service chỉ có health không hiện dòng ngân sách |
| Dọn dẹp | Xoá hai cờ khỏi `.env`, tạo lại 7 container; bật lại DB | `27a` bước 01 | Header bị bỏ qua |

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-007 — cả 7 manifest khai `excluded-path-prefixes: [/health]` | [`ErrorBudgetPolicyTests.cs:129`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L129) — `EveryService_ExcludesTheHealthPathPrefixFromTheBudget` | `dotnet test tests/ServiceManifestSloConventionTests --filter "FullyQualifiedName~ErrorBudgetPolicyTests"` |
| FR-001/FR-002 — 4 rule 027 có đúng một điều kiện loại, trước mọi `EVAL` | [`ErrorBudgetRuleDefinitionTests.cs:273`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L273) — `BudgetRule_ExcludesTheManifestDeclaredPathPrefixes_BeforeAnyCalculation` | `dotnet test tests/ServiceManifestSloConventionTests --filter "FullyQualifiedName~ErrorBudgetRuleDefinitionTests"` |
| FR-003 — rule frozen vẫn đọc index sự kiện "cạn" | [`ErrorBudgetRuleDefinitionTests.cs:287`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L287) — `FrozenRule_StillReadsTheExhaustionEvents` | `dotnet test tests/ServiceManifestSloConventionTests --filter "FullyQualifiedName~ErrorBudgetRuleDefinitionTests"` |
| FR-002 — rule 028 có điều kiện loại | [`IncidentFastDetectionRuleDefinitionTests.cs:181`](../../tests/ServiceManifestSloConventionTests/IncidentFastDetectionRuleDefinitionTests.cs#L181) — `Rule_ExcludesTheManifestDeclaredPathPrefixes_BeforeAnyCalculation` | `dotnet test tests/ServiceManifestSloConventionTests --filter "FullyQualifiedName~IncidentFastDetectionRuleDefinitionTests"` |
| FR-006 — rule `health-failure`: loại, chu kỳ 5m, tag riêng | [`HealthFailureRuleTests.cs:25`](../../tests/ServiceManifestSloConventionTests/HealthFailureRuleTests.cs#L25) — `Rule_IsExported_EveryFiveMinutes_WithItsOwnTag` | `dotnet test tests/ServiceManifestSloConventionTests --filter "FullyQualifiedName~HealthFailureRuleTests"` |
| FR-006 — bắn khi `> 0` service, không có action | [`HealthFailureRuleTests.cs:54`](../../tests/ServiceManifestSloConventionTests/HealthFailureRuleTests.cs#L54) — `Rule_AlertsWhenAnyServiceBreaches_AndHasNoActions` | `dotnet test tests/ServiceManifestSloConventionTests --filter "FullyQualifiedName~HealthFailureRuleTests"` |
| FR-006 — chỉ nhìn 5 phút gần nhất | [`HealthFailureRuleTests.cs:74`](../../tests/ServiceManifestSloConventionTests/HealthFailureRuleTests.cs#L74) — `Rule_LooksAtTheLastFiveMinutesOnly` | `dotnet test tests/ServiceManifestSloConventionTests --filter "FullyQualifiedName~HealthFailureRuleTests"` |
| FR-006 — chỉ đếm tiền tố health của manifest (không đảo) | [`HealthFailureRuleTests.cs:91`](../../tests/ServiceManifestSloConventionTests/HealthFailureRuleTests.cs#L91) — `Rule_CountsOnlyTheManifestDeclaredHealthPrefixes` | `dotnet test tests/ServiceManifestSloConventionTests --filter "FullyQualifiedName~HealthFailureRuleTests"` |
| FR-006 — chỉ đếm 5xx, không đếm chậm | [`HealthFailureRuleTests.cs:115`](../../tests/ServiceManifestSloConventionTests/HealthFailureRuleTests.cs#L115) — `Rule_CountsOnlyFiveXx_NotSlowness` | `dotnet test tests/ServiceManifestSloConventionTests --filter "FullyQualifiedName~HealthFailureRuleTests"` |
| FR-006 — ngưỡng 50% và chỉ giữ cột `service` | [`HealthFailureRuleTests.cs:135`](../../tests/ServiceManifestSloConventionTests/HealthFailureRuleTests.cs#L135) — `Rule_FiresAtFiftyPercent_AndKeepsOnlyTheServiceColumn` | `dotnet test tests/ServiceManifestSloConventionTests --filter "FullyQualifiedName~HealthFailureRuleTests"` |

**Kết quả lượt QA này**: xem [QA_Debt.md](QA_Debt.md) mục 033 (số test, số đo thật và phát hiện). Bảng thủ công: dashboard là cấu hình Kibana nên không có test tự động; chỉ rule và manifest được canh bằng test.

## Kết luận

**PASS kèm ghi chú** (chi tiết và số đo ở [QA_Debt.md](QA_Debt.md) mục 033). Điều kiện loại span health có trong 5 rule và các panel; manifest khai báo và có test canh;
rule `health-failure` và panel mới hoạt động; Postman tiêm lỗi vào route nghiệp vụ.
