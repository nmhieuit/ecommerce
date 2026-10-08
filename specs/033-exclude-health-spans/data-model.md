# Data Model: Loại span health khỏi công thức ngân sách lỗi

**Spec**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

Không có bảng cơ sở dữ liệu hay index mới. "Mô hình" gồm cấu hình manifest, định nghĩa rule/panel Kibana và nguồn dữ liệu chúng đọc.

## Nguồn dữ liệu (đọc, không sửa)

| Nguồn | Trường dùng | Ghi chú |
|---|---|---|
| `traces-generic.otel-default*` | `@timestamp`, `kind`, `resource.attributes.service.name`, `attributes.url.path`, `attributes.http.response.status_code`, `duration` | `attributes.url.path` có ở span Server, **không có** ở span Client (F2/F3: bắt buộc `COALESCE`). |
| `slo-error-budget-events` | `@timestamp`, `service`, `budget`, `event` | Rule frozen đọc cùng traces; sự kiện không có `url.path` nên không bị điều kiện loại cắt. |

## Cấu hình manifest

Khối `error-budget-policy` của mỗi `service-manifest.yaml` (7 file) thêm một khoá:

| Khoá | Kiểu | Giá trị bắt buộc | Quan hệ |
|---|---|---|---|
| `excluded-path-prefixes` | danh sách chuỗi | `[/health]` (giống hệt ở cả 7 manifest) | Nguồn duy nhất của tiền tố loại; rule và panel PHẢI dùng đúng giá trị này; test đối chiếu |

Mô hình test (`ServiceManifestModel.cs`): `ErrorBudgetPolicySection.ExcludedPathPrefixes` (`List<string>?`, alias `excluded-path-prefixes`).

## Định nghĩa rule (file export `docs/kibana-quan-sat-he-thong/alerts/*.ndjson`)

| Rule | Thay đổi | Giữ nguyên |
|---|---|---|
| `error-budget-50/75/100` | thêm điều kiện loại tiền tố | tỷ lệ cho phép, ngưỡng độ trễ, mốc, cửa sổ 7 ngày, chu kỳ 5 phút, cột `service, budget` |
| `error-budget-frozen` | thêm điều kiện loại tiền tố (chỉ ảnh hưởng span) | cửa sổ 14 ngày, ngày đạt SLO, cột `service` |
| `incident-fast-detection` | thêm điều kiện loại tiền tố | cửa sổ 5 phút, ngưỡng 5xx 1%, ngưỡng p95/p99, cột `service` |
| **Rule health lỗi (mới)** | tạo mới | không tính ngân sách |

### Rule health lỗi (mới)

| Thuộc tính | Giá trị |
|---|---|
| Loại | `.es-query` ES|QL, `groupBy: row`, `timeField: @timestamp` |
| Cửa sổ | 5 phút (`NOW() - 5 minutes`) |
| Điều kiện | span có đường dẫn bắt đầu bằng tiền tố (từ manifest); `health_5xx / health_spans ≥ 50%` theo service |
| Kết quả | chỉ cột `service` |
| So sánh cảnh báo | `> 0` dòng |
| Chưa chốt | tên, tag, chu kỳ chạy, tên file export (hỏi ở `/speckit-tasks`) |

## Panel dashboard

| Dashboard | Panel | Thay đổi |
|---|---|---|
| Ngân sách lỗi tuần | mức tiêu hao (`slo-error-budget-consumption`), hạn mức còn lại, error-rate/p95 theo ngày, tiêu hao lũy kế | thêm điều kiện loại vào truy vấn ES|QL (D3) |
| Ngân sách lỗi tuần | cảnh báo mốc, cạn ngân sách | không đổi (đọc alert) |
| Xử lý sự cố | Bảng SLO, 5xx/p95/traffic+401/403 theo phút, phân bố status code, top endpoint chậm | thêm `query` KQL loại health (Lens, D2) |
| Xử lý sự cố | `dotnet.exceptions`, lỗi gọi hạ lưu, log lỗi, Phát hiện nhanh | không đổi |
| Xử lý sự cố | **Chỉ báo health lỗi (mới)** | Discover session ES|QL, theo thanh thời gian: `service`, `health_spans`, `health_5xx`, `health_5xx_pct` |

## Thực thể dẫn xuất (không lưu)

- **Span health**: `COALESCE(attributes.url.path, "") LIKE "<prefix>*"` với mỗi tiền tố trong `excluded-path-prefixes`.
- **Request nghiệp vụ**: mọi span còn lại; là tập duy nhất đi vào mẫu số và span xấu của 4 ngân sách.
- **Tỷ lệ health 5xx**: `health_5xx / health_spans × 100` theo service trong khoảng đã chọn (panel) hoặc 5 phút (rule).

## Chuyển trạng thái

Không có. Rule health lỗi không lưu trạng thái ngoài alert của Kibana.
