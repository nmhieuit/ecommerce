# QA: Ngân sách lỗi chỉ đếm span Server

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.
Spec: [`specs/034-error-budget-server-spans/`](../../specs/034-error-budget-server-spans/spec.md).*

## Luồng happy-case đã rà soát

1. **US1 — ngân sách và phát hiện nhanh chỉ đếm span Server**: `error-budget-50/75/100` và `incident-fast-detection` có `WHERE kind == "Server"`; `error-budget-frozen` có `WHERE (kind == "Server" OR _index LIKE "*slo-error-budget-events*")`.
2. **US2 — hai dashboard khớp rule**: 5 truy vấn ES|QL của `Ngân sách lỗi tuần` và 6 panel Lens ngân sách/SLO của `Xử lý sự cố` chỉ đếm Server; panel `Lỗi gọi hạ lưu` vẫn đọc span Client.
3. **US3 — test canh gác**: 5 test mới (3 + 1 + 1) cho 5 rule, chạy đỏ trước khi sửa rule.
4. **US4 — dọn trạng thái**: xoá sự kiện cạn trong `slo-error-budget-events` (giữ index) và Disable/Enable `error-budget-100`.
5. **US5 — Postman và tài liệu**: folder `34 - Ngân sách: chỉ đếm span Server`, tài liệu 027–033 sửa tại chỗ, bộ tài liệu 034.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — dừng một service hạ lưu, chạy folder Postman 34 và xem hai dashboard

Dựng stack, import rule và dashboard như [QA 033](033_QA_loại%20span%20health%20khỏi%20ngân%20sách%20lỗi.md) (6 rule, Enable hết).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json); lấy token ở folder `00 - Xác thực & phân quyền (Get Token)`; folder **`34 - Ngân sách: chỉ đếm span Server`**.

**Công tắc**: dừng/bật `products-api` (`docker compose -f docker-compose.local.yml stop products-api` / `start products-api`). **Khôi phục mặc định**: bật lại `products-api`; nếu muốn về sạch trạng thái cạn, xoá sự kiện trong `slo-error-budget-events` và Disable/Enable `error-budget-100`.

| Bước | Cấu hình cần chỉnh | Request / thao tác | Kỳ vọng theo tài liệu |
|---|---|---|---|
| Lỗi hạ lưu qua 3 tầng (FR-001) | Dừng `products-api` | `34a` bước 01 (sau khi lấy token) | Mã 5xx; Gateway có 1 span Server 5xx và 1 span Client 5xx; BFF có 1 span Server 5xx và các span Client không có mã trạng thái |
| Đối chiếu theo loại span (FR-001, FR-004) | Bật lại `products-api` | `34b` bước 01–02, chạy ngay sau `34a` (trong 15 phút) | `tong_moi_span = span_duoc_tinh + span_bi_loai` ở mọi service; Gateway và BFF có `loi_duoc_tinh` ≥ 1; span Client nằm ở `span_bi_loai` |
| Dashboard khớp rule (FR-004, FR-005) | (không đổi) | Mở `Ngân sách lỗi tuần` và `Xử lý sự cố` | Mức tiêu hao khớp truy vấn chỉ-Server (sai lệch ≤ 2 điểm %); Bảng SLO không đếm Client; `Lỗi gọi hạ lưu` vẫn hiện span Client |
| Rule phát hiện nhanh chỉ đếm Server (FR-002) | (không đổi) | Xem `incident-fast-detection` sau `34a` | Rule bắn từ span Server 5xx; span Client/Producer không làm bắn |
| Dọn trạng thái (FR-008) | (hỏi trước khi làm) | Xoá sự kiện, Disable/Enable `error-budget-100` | Index giữ nguyên mapping; rule 100 tạo lại alert theo công thức mới |

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001/FR-002/FR-007 — 3 rule mốc có đúng một dòng `WHERE kind == "Server"` trước mọi `EVAL` | [`ErrorBudgetRuleDefinitionTests.cs:306`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L306) — `BudgetRule_CountsOnlyServerSpans_BeforeAnyCalculation` | `dotnet test tests/ServiceManifestSloConventionTests --filter "FullyQualifiedName~ErrorBudgetRuleDefinitionTests"` |
| FR-001/FR-003/FR-007 — rule frozen chỉ-Server nhưng giữ sự kiện cạn | [`ErrorBudgetRuleDefinitionTests.cs:320`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L320) — `FrozenRule_CountsOnlyServerSpans_ButKeepsTheExhaustionEvents` | `dotnet test tests/ServiceManifestSloConventionTests --filter "FullyQualifiedName~ErrorBudgetRuleDefinitionTests"` |
| FR-003 — rule frozen vẫn đọc index sự kiện cạn (033) | [`ErrorBudgetRuleDefinitionTests.cs:287`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L287) — `FrozenRule_StillReadsTheExhaustionEvents` | `dotnet test tests/ServiceManifestSloConventionTests --filter "FullyQualifiedName~ErrorBudgetRuleDefinitionTests"` |
| FR-001/FR-002/FR-007 — rule 028 chỉ đếm Server | [`IncidentFastDetectionRuleDefinitionTests.cs:194`](../../tests/ServiceManifestSloConventionTests/IncidentFastDetectionRuleDefinitionTests.cs#L194) — `Rule_CountsOnlyServerSpans_BeforeAnyCalculation` | `dotnet test tests/ServiceManifestSloConventionTests --filter "FullyQualifiedName~IncidentFastDetectionRuleDefinitionTests"` |

**Kết quả lượt QA này**: xem [QA_Debt.md](QA_Debt.md) mục 034 (số test, số đo thật và phát hiện). Dashboard là cấu hình Kibana nên không có test tự động; chỉ 5 rule được canh bằng test.

## Kết luận

**PASS kèm ghi chú** (chi tiết và số đo ở [QA_Debt.md](QA_Debt.md) mục 034). Điều kiện chỉ-Server có trong 5 rule và các panel ngân sách; có test canh; Postman 34 chứng minh việc đếm theo loại span; lệch Governance của PR #75 để việc riêng.
