# Contract: Ngân sách lỗi chỉ đếm span Server

**Spec**: [../spec.md](../spec.md) | **Research**: [../research.md](../research.md) | **Nối tiếp**: `specs/033-exclude-health-spans/contracts/budget-exclusion-contract.md` (loại `/health*`, vẫn giữ nguyên)

Hợp đồng bất biến cho rule và dashboard. **Viết trước** khi sửa rule, dashboard và test (Nguyên tắc II).

## Điều kiện chỉ đếm Server trong ES|QL (người tiêu thụ: `ErrorBudgetRuleDefinitionTests`, `IncidentFastDetectionRuleDefinitionTests`)

1. `error-budget-50`, `error-budget-75`, `error-budget-100` và `incident-fast-detection` có **đúng một** dòng `| WHERE kind == "Server"` ngay sau `FROM` (cùng vùng với điều kiện loại `/health*`), đứng trước mọi `EVAL`/`STATS`.
2. `error-budget-frozen` có **đúng một** điều kiện `WHERE (kind == "Server" OR _index LIKE "*slo-error-budget-events*")` ngay sau `FROM`. Điều kiện này vẫn giữ mọi sự kiện cạn (sự kiện không có cột `kind`); thiếu vế `_index` làm rule mất toàn bộ sự kiện và không bao giờ bắn.
3. Rule frozen vẫn có `slo-error-budget-events` trong `FROM` (test 033 `FrozenRule_StillReadsTheExhaustionEvents` giữ xanh).
4. Điều kiện loại `/health*` của 033 giữ nguyên trong cả 5 rule. Mọi quy tắc khác (tỷ lệ cho phép 1%/1%/5%/1%, ngưỡng độ trễ PR #75, mốc 50/75/100, cửa sổ 7/14 ngày và 5 phút, chu kỳ 5 phút, cột kết quả) KHÔNG đổi.
5. Span không phải `Server` (`Client`, `Producer`, loại khác) không vào mẫu số lẫn tập span xấu của bất kỳ ngân sách nào. Ngày chỉ có span không-Server tính là không có traffic và tính là đạt (027/029).
6. Rule `health-failure` KHÔNG đổi.

## Dashboard (kiểm bằng quickstart, không test tự động)

7. `Ngân sách lỗi tuần — 7 service`: mọi truy vấn ES|QL tính từ traces (saved search `slo-error-budget-consumption`, "Hạn mức còn lại", error-rate theo ngày, p95 theo ngày, tiêu hao lũy kế) có cùng điều kiện bất biến 1; hai panel cảnh báo/cạn không đổi.
8. `Xử lý sự cố — 7 service`: 6 panel Lens (Bảng SLO, 5xx/phút, p95/phút, Traffic + 401/403, Phân bố status code, Top endpoint chậm nhất) có KQL cấp panel `kind : Server and not attributes.url.path : /health*`.
9. Giữ nguyên trên `Xử lý sự cố`: `Lỗi gọi hạ lưu — cặp service gọi → đích` (vẫn đọc span Client), Phát hiện nhanh, Health lỗi theo service, `dotnet.exceptions`, Log lỗi, markdown.
10. Mọi `time_range` riêng của panel và thanh thời gian không đổi; id cố định và cấu trúc hai dashboard không đổi (030).

## Số liệu đối chiếu (tuần 05–11/10, UTC+7, đã loại `/health*`)

11. Sau khi sửa, `Bff.Api` có 118 span / 9 lỗi 5xx được tính (trước: 319 / 21); tổng 7 service 527 span (trước: 869). Dùng làm mốc cho V2.

## Kiểm chứng

Theo [../quickstart.md](../quickstart.md). Bất biến 1–6 đọc được từ file ndjson và được test tự động canh; 7–11 cần mở dashboard thật.
