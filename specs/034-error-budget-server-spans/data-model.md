# Data Model: Ngân sách lỗi chỉ đếm span Server

**Spec**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

Không có bảng cơ sở dữ liệu, index hay khoá manifest mới. "Mô hình" là các định nghĩa rule/panel Kibana và nguồn dữ liệu chúng đọc.

## Nguồn dữ liệu (đọc, không sửa)

| Nguồn | Trường dùng | Ghi chú |
|---|---|---|
| `traces-generic.otel-default*` | `@timestamp`, `kind`, `resource.attributes.service.name`, `attributes.url.path`, `attributes.http.response.status_code`, `duration` | `kind` ∈ {`Server`, `Client`, `Producer`}, luôn có giá trị (F1). Chỉ `Server` được tính. |
| `slo-error-budget-events` | `@timestamp`, `service`, `budget`, `event` | **Không có cột `kind`** (F4); rule frozen phải giữ sự kiện bằng `_index`. |

## Loại span và việc có được tính hay không

| `kind` | Ý nghĩa | Tính vào ngân sách | Panel vẫn dùng |
|---|---|---|---|
| `Server` | request mà chính service nhận | **Có** (trừ đường dẫn `/health*`, 033) | tất cả panel ngân sách/SLO |
| `Client` | lời gọi đi ra hạ lưu | Không | `Lỗi gọi hạ lưu — cặp service gọi → đích` |
| `Producer` | publish thông điệp (hiện chỉ `Orders.Api`) | Không | — |
| loại khác | — | Không | — |

## Định nghĩa rule (file export `docs/kibana-quan-sat-he-thong/alerts/*.ndjson`)

| Rule | Thay đổi | Giữ nguyên |
|---|---|---|
| `error-budget-50/75/100` | thêm `WHERE kind == "Server"` | loại `/health*`, tỷ lệ, ngưỡng độ trễ, mốc, cửa sổ 7 ngày, chu kỳ 5 phút, cột `service, budget` |
| `error-budget-frozen` | thêm `WHERE (kind == "Server" OR _index LIKE "*slo-error-budget-events*")` | loại `/health*`, cửa sổ 14 ngày, ngày đạt SLO, cột `service` |
| `incident-fast-detection` | thêm `WHERE kind == "Server"` | loại `/health*`, cửa sổ 5 phút, ngưỡng 5xx 1%, ngưỡng p95/p99, cột `service` |
| `health-failure` | không đổi | — |

## Định nghĩa panel (file export `docs/kibana-quan-sat-he-thong/dashboards/*.ndjson`)

| Dashboard | Đổi | Giữ nguyên |
|---|---|---|
| `Ngân sách lỗi tuần — 7 service` | saved search `slo-error-budget-consumption`; "Hạn mức còn lại"; 3 vis (error-rate/p95/tiêu hao lũy kế) | 2 panel cảnh báo/cạn, markdown |
| `Xử lý sự cố — 7 service` | 6 Lens: Bảng SLO, 5xx/phút, p95/phút, Traffic + 401/403, Phân bố status code, Top endpoint chậm | Lỗi gọi hạ lưu (Client), Phát hiện nhanh, Health lỗi theo service, `dotnet.exceptions`, Log lỗi, markdown |

## Trạng thái cần dọn (FR-008)

| Đối tượng | Hiện trạng (2026-10-09) | Hành động sau triển khai (hỏi lại trước khi làm) |
|---|---|---|
| `slo-error-budget-events` | 20 tài liệu sự kiện cạn từ bài thử 29a/30a | xoá sự kiện sinh từ công thức cũ |
| Rule `error-budget-100` | có thể đang active theo công thức cũ | Disable rồi Enable |

## Quan hệ

Rule và panel cùng đọc `traces-generic.otel-default*` với cùng điều kiện (Server + loại `/health*`); rule frozen đọc thêm `slo-error-budget-events`. Không có khoá ngoại hay trạng thái mới.
