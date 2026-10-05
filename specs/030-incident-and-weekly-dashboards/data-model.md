# Data Model: Hai dashboard Xử lý sự cố và Ngân sách lỗi tuần

**Spec**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

Không có bảng cơ sở dữ liệu mới. "Mô hình" ở đây là các saved object Kibana và các nguồn dữ liệu chúng đọc.

## Nguồn dữ liệu (đọc, không sửa)

| Nguồn | Dùng cho | Trường chính |
|---|---|---|
| `traces-generic.otel-default*` | bảng SLO, biểu đồ theo phút/ngày, ngân sách, hạ lưu | `@timestamp`, `resource.attributes.service.name`, `kind` (Server/Client), `duration` (ns), `attributes.http.response.status_code`, `attributes.server.address`, `attributes.correlation.id`, `trace_id`, `status.code` |
| `logs-generic.otel-default*` | log lỗi gần nhất | `@timestamp`, `severity_number` (≥ 17 = Error trở lên), `severity_text`, `body.text`/`message`, `resource.attributes.service.name`, `trace_id`, correlation id (tên trường theo kết quả V4) |
| `metrics-generic.otel-default*` | `dotnet.exceptions` | như dashboard cũ |
| `.alerts-stack.alerts-default` | Phát hiện nhanh; cảnh báo mốc; cạn ngân sách | `kibana.alert.rule.tags`, `kibana.alert.rule.name`, `kibana.alert.status`, `kibana.alert.grouping.*`, `kibana.alert.start`, `@timestamp` |

## Saved object

### Dashboard (2)

| Thuộc tính | Xử lý sự cố | Ngân sách tuần |
|---|---|---|
| Tên | `Xử lý sự cố — 7 service` | `Ngân sách lỗi tuần — 7 service` |
| Id | UUID mới cố định (ghi ở contract khi tạo) | UUID mới cố định (ghi ở contract khi tạo) |
| Thời gian | mặc định `now-1h` → `now`, lưu cùng dashboard (`timeRestore`), tự làm mới 1 phút | không phụ thuộc thanh thời gian; tuần chọn bằng điều khiển |
| Điều khiển | không | 1 điều khiển ES|QL tĩnh `?tuan_chon` (4 giá trị tương đối: Tuần này / Tuần trước / 2 tuần trước / 3 tuần trước) |
| Link | 1 ô Markdown link → Ngân sách tuần | 1 ô Markdown link → Xử lý sự cố |
| Section | có, luôn mở | có, luôn mở |

### Panel (quan hệ với dashboard cũ)

Chi tiết từng panel và bất biến: [contracts/dashboards-contract.md](./contracts/dashboards-contract.md).

| Panel | Loại | Nguồn gốc |
|---|---|---|
| Bảng SLO — 7 service (+ ô Markdown ngưỡng) | Lens + Markdown | chép từ panel 5–6 cũ |
| `dotnet.exceptions` theo service | Lens | chép từ panel 9 |
| Phân bố status code theo service | Lens | chép từ panel 10 |
| Top endpoint chậm nhất | Lens | chép từ panel 11 |
| Phát hiện nhanh | Discover session ES|QL | sửa từ `incident-fast-detection-active-alerts` (bỏ lọc 15 phút, thêm trạng thái) |
| 5xx theo phút theo service; p95 theo phút theo service | Lens | mới |
| Traffic + 401/403 theo phút theo service | Lens | mới, thay panel 12 "Tổng 401 + 403" |
| Lỗi gọi hạ lưu | Discover session ES|QL | mới |
| Log lỗi gần nhất | Discover session cổ điển + data view logs | mới |
| Mức tiêu hao tuần (4 ngân sách × 7 service) | Discover session ES|QL | sửa từ `slo-error-budget-consumption` (`?tuan_chon`) |
| Hạn mức còn lại | Discover session ES|QL | mới |
| Cảnh báo mốc; Cạn ngân sách | Discover session ES|QL | sửa từ `slo-error-budget-active-alerts`, `slo-error-budget-frozen` (bỏ lọc 15 phút; trạng thái hiện tại, ghi rõ trên dashboard) |
| Error-rate theo ngày; p95 theo ngày | Lens ES|QL | thay panel 7–8 (`?tuan_chon`) |
| Tiêu hao lũy kế theo ngày | Lens ES|QL | mới (`?tuan_chon`) |

### Index-pattern (data view)

| Tên | Trạng thái |
|---|---|
| `traces-generic.otel-default*`, `metrics-generic.otel-default*` | có sẵn; export cùng dashboard dùng chúng |
| `logs-generic.otel-default*` | **mới**; trường `trace_id` có định dạng URL trỏ Discover (data view traces) lọc theo trace_id |

## Thực thể dẫn xuất (không lưu)

- **Mức tiêu hao tuần**: `consumed_pct = bad / total / allowed × 100` theo (service, ngân sách), `allowed` = 1% / 1% / 5% / 1%.
- **Hạn mức còn lại**: `remaining_pct = 100 − consumed_pct`; `remaining_requests = allowed × total − bad`.
- **Tiêu hao lũy kế**: `consumed_pct` tính tại cuối mỗi ngày `as_of` ∈ [0, 6] từ thứ Hai 00:00 UTC+7 của tuần chọn.
- **Cặp gọi hạ lưu**: (service gọi, đích suy ra từ `server.address`) cùng số span, span lỗi, span chậm, % xấu, p95.
- **Tuần lịch giờ VN**: `[week_start, week_start + 7 ngày)`, `week_start = DATE_TRUNC(1 week, t + 7 giờ) − 7 giờ`.

## Chuyển trạng thái

Không có. Cảnh báo mốc/cạn là trạng thái do rule 027 quản lý; spec này chỉ hiển thị.
