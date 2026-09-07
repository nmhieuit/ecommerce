# Data Model: Liveness/readiness probe cho mọi service trên Kubernetes

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-06

Tính năng này không xử lý dữ liệu nghiệp vụ hay entity miền (domain entity) nào. "Dữ liệu" duy nhất
liên quan là cấu hình tham số hóa cho template Deployment dùng chung — tài liệu tại đây mô tả schema
của cấu hình đó, đóng vai trò tương đương data-model cho một tính năng hạ tầng.

## Entity: Service Deployment Profile

Đại diện cho tập tham số mà một service cung cấp cho template Deployment dùng chung
(`deploy/ansible/roles/service_deployment/templates/deployment.yaml.j2`) để render ra manifest
triển khai của riêng nó, bao gồm khối probe.

| Field | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `service_name` | string | Có | Tên service, khớp với tên dùng trong `docker-compose.yml` và tên thư mục dưới `services/` (ví dụ `orders`, `gateway`, `bff`). |
| `container_port` | integer | Có | Cổng HTTP nội bộ mà container lắng nghe — nơi cả hai probe sẽ gọi tới. |
| `depends_on_database` | boolean | Có | `true` với service sở hữu cơ sở dữ liệu riêng (parties, products, baskets, orders, identity); `false` với service không trạng thái không sở hữu dữ liệu (gateway, bff). Quyết định readiness probe có phản ánh kết nối cơ sở dữ liệu hay không — bản thân hành vi này đã tồn tại ở tầng endpoint, trường này chỉ chọn đúng nhóm giá trị thời gian mặc định (Quyết định 2, research.md). |
| `liveness_path` | string | Không (mặc định `/health/live`) | Đường dẫn liveness probe. Mặc định dùng chung cho mọi service vì hợp đồng endpoint đã cố định (specs/001). |
| `readiness_path` | string | Không (mặc định `/health/ready`) | Đường dẫn readiness probe. Mặc định dùng chung, cùng lý do trên. |
| `probe_timing_overrides` | object (tùy chọn) | Không | Cho phép một service ghi đè `initial_delay_seconds`/`period_seconds`/`timeout_seconds`/`failure_threshold` khi nhóm mặc định theo `depends_on_database` không phù hợp (ví dụ một service có thời gian khởi động đặc biệt lâu). Việc dùng field này phải có lý do ghi trong `deploy/ansible/inventories/services.yml`. |

**Validation rules** (bắt nguồn từ Functional Requirements của spec.md):

- `liveness_path` và `readiness_path` PHẢI khác nhau (FR-001, FR-002 — hai probe riêng biệt).
- Khi `depends_on_database = false`, `probe_timing_overrides` không được đặt ngưỡng thời gian dài
  hơn nhóm mặc định của service có cơ sở dữ liệu — nếu cần dài hơn, đó là dấu hiệu service này
  thực chất nên được đánh dấu `depends_on_database = true` hoặc có dependency bắt buộc khác cần mô
  hình hóa rõ ràng (FR-004).
- Mọi `service_name` xuất hiện trong `deploy/ansible/inventories/services.yml` PHẢI khớp với đúng
  7 service đang tồn tại (parties, products, baskets, orders, identity, gateway, bff) — không thiếu,
  không thừa (FR-001, spec SC-001).

## Entity: Probe Declaration (hình chiếu trong manifest đã render)

Đại diện cho khối `livenessProbe`/`readinessProbe` thực tế xuất hiện trong manifest Kubernetes sau
khi render — đây là thứ mà `tests/DeploymentManifestConventionTests` đọc và assert.

| Field | Kiểu | Mô tả |
|---|---|---|
| `probe_type` | enum (`liveness`, `readiness`) | Loại probe. |
| `http_get.path` | string | Đường dẫn HTTP được gọi — PHẢI khớp `liveness_path`/`readiness_path` tương ứng của service. |
| `http_get.port` | integer | PHẢI khớp `container_port` của service. |
| `initial_delay_seconds` | integer | Thời gian chờ trước lần gọi probe đầu tiên sau khi container khởi động. |
| `period_seconds` | integer | Chu kỳ gọi probe. |
| `timeout_seconds` | integer | Thời gian chờ tối đa cho một lần gọi probe trước khi tính là fail. |
| `failure_threshold` | integer | Số lần fail liên tiếp trước khi Kubernetes coi probe là thất bại (loại traffic với readiness, khởi động lại pod với liveness). |
| `depends_on_external_tag` | boolean (chỉ áp dụng cho liveness) | PHẢI luôn là `false` — ràng buộc bất biến từ FR-003: liveness không được phụ thuộc dependency ngoài. |

**State transitions** (mức pod, do Kubernetes quản lý — tài liệu hóa để test/quickstart tham chiếu,
không phải state do tính năng này tự cài đặt):

`Pod khởi động` → `readiness = NotReady` (chưa nhận traffic, spec US2) → `readiness = Ready` (bắt
đầu nhận traffic) → (nếu liveness fail liên tiếp ≥ `failure_threshold`) → `Pod bị khởi động lại`
→ quay lại `readiness = NotReady` cho instance mới.
