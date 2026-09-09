# Data Model: Khai báo và đo lường liên tục SLO theo từng service trong service manifest

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-09

Tính năng này không xử lý dữ liệu nghiệp vụ hay entity miền (domain entity) nào. "Dữ liệu" liên quan
là (a) schema khai báo SLO trong `service-manifest.yaml`, (b) hồ sơ mặc định theo phân loại nền tảng
dùng để so sánh, và (c) hình chiếu của dữ liệu telemetry thật thành giá trị đo được liên tục — tài
liệu tại đây mô tả cấu trúc của cả ba, đóng vai trò tương đương data-model cho một tính năng hạ tầng
quan sát/quy ước.

## Entity: Khai báo ngân sách SLO của service (`slos:` block trong `service-manifest.yaml`)

Đại diện cho khối `slos:` trong manifest mô tả của mỗi service — nguồn dữ liệu duy nhất mà
`tests/ServiceManifestSloConventionTests` đọc và assert.

| Field | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `availability` | string (phần trăm, ví dụ `99.9%`) | Có | Mục tiêu độ khả dụng hàng tháng. |
| `error-rate.max-5xx-ratio` | string (phần trăm) | Có | Ngưỡng tối đa tỷ lệ phản hồi 5xx trên tổng request. |
| `latency.p95` | string (đơn vị ms, ví dụ `150ms`) | Có | Ngưỡng độ trễ p95. |
| `latency.p99` | string (đơn vị ms) | Có | Ngưỡng độ trễ p99. |
| `justification` (`slos.justification`) | string | Chỉ bắt buộc khi một trong bốn giá trị trên khác hồ sơ mặc định tương ứng với `service.classification` của service | Lý do vì sao service này cần một ngân sách khác mặc định — phải là văn bản có ý nghĩa, không phải chuỗi rỗng hay placeholder (FR-003). |

Ba trường liên quan ngoài `slos:` mà việc so sánh với mặc định phụ thuộc vào:

| Field | Kiểu | Vị trí | Mô tả |
|---|---|---|---|
| `service.name` | string | `service.name` | Tên service, dùng để đối chiếu với danh sách 7 service đang tồn tại (SC-001). |
| `service.classification` | enum (`client-facing-bff`, `internal-service-api`) | `service.classification` | Quyết định hồ sơ mặc định nào áp dụng cho service này (xem entity **Hồ sơ mặc định nền tảng**). |

**Validation rules** (bắt nguồn từ Functional Requirements của spec.md):

- Cả 4 giá trị SLO PHẢI hiện diện và không rỗng/không phải placeholder (FR-001).
- Nếu cả 4 giá trị khớp đúng hồ sơ mặc định của đúng `service.classification`, `slos.justification`
  KHÔNG bắt buộc phải có (FR-002).
- Nếu bất kỳ giá trị nào khác hồ sơ mặc định, `slos.justification` PHẢI hiện diện và không rỗng
  (FR-003).
- `service.classification` PHẢI là một trong hai giá trị đã biết (`client-facing-bff`,
  `internal-service-api`) — một giá trị lạ không thể đối chiếu với hồ sơ mặc định nào, tự nó là một
  lỗi cấu hình cần phát hiện.

## Entity: Hồ sơ mặc định nền tảng (Platform SLO Default Profile)

Hai bộ giá trị chuẩn, lấy trực tiếp từ constitution Principle VIII — không được định nghĩa lại trong
tính năng này, chỉ được mã hoá lại (mirror) trong `PlatformSloDefaults.cs` của dự án test mới, có
comment trỏ ngược về constitution để không trôi dạt so với nguồn gốc.

| Phân loại (`classification`) | `availability` | `error-rate.max-5xx-ratio` | `latency.p95` | `latency.p99` |
|---|---|---|---|---|
| `client-facing-bff` | 99.9% | 0.1% | 300ms | 800ms |
| `internal-service-api` | 99.9% | 0.1% | 150ms | 500ms |

## Entity: Giá trị đo được liên tục (hình chiếu trên dashboard Kibana, US3)

Đại diện cho giá trị thực tế mà dashboard `SLO vận hành hằng ngày — 7 service` tính từ dữ liệu traces
OTel thật, đối chiếu trực tiếp với **Khai báo ngân sách SLO** ở trên. Đây không phải dữ liệu do tính
năng này tạo ra — nó là một hình chiếu (view) trên dữ liệu telemetry đã tồn tại từ
017-otel-servicedefaults-elastic.

| Field | Kiểu | Mô tả |
|---|---|---|
| `service_name` | string | Tên service — PHẢI khớp `service.name` trong manifest tương ứng để đối chiếu được. |
| `measured.error_rate` | phần trăm, tính trong cửa sổ trượt (mặc định 24 giờ gần nhất) | `count(status_code ≥ 500) / count(*)` trên toàn bộ span của service trong cửa sổ. |
| `measured.latency_p95` / `measured.latency_p99` | ms | Percentile 95/99 của `duration` trên toàn bộ span của service trong cùng cửa sổ. |
| `measured.availability_approx` | phần trăm | `100% − measured.error_rate` (xấp xỉ, vì chưa có synthetic uptime check riêng — xem Assumptions của spec.md). |
| `data_state` | enum (`has-data`, `no-data`) | PHẢI là `no-data` khi không có span nào của service trong cửa sổ đang xét — KHÔNG được suy ra `measured.error_rate = 0%` từ tình trạng này (FR-006). |

**Validation rules**:

- Với mọi `service_name` xuất hiện trên dashboard, phải tồn tại đúng một **Khai báo ngân sách SLO**
  cùng tên để đối chiếu (SC-003).
- Khi `data_state = no-data`, dashboard PHẢI thể hiện trạng thái này khác biệt rõ ràng với
  `measured.error_rate = 0%` (FR-006, đã xác minh cơ chế tại `docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md`
  mục "Lens Formula xử lý mẫu số 0").

**State transitions**: không áp dụng — đây là một hình chiếu tức thời/theo cửa sổ trượt được tính lại
liên tục, không phải một entity có vòng đời trạng thái.
