# Data Model: Secrets qua Cluster Secret Store, loại bỏ cấu hình hardcode

**Feature**: [spec.md](./spec.md) | **Ngày**: 2026-09-06

Feature này không đưa thêm bảng/entity nghiệp vụ nào vào bất kỳ database service nào — không có migration EF Core mới. Các "entity" dưới đây là đối tượng cấu hình/khai báo (configuration & declarative artifacts), tồn tại dưới dạng manifest YAML, cấu hình `IOptions`, và kết quả quét CI — không phải bản ghi trong một database runtime.

## 1. RequiredSecret (đối tượng cấu hình trong `shared/ServiceDefaults`)

Đại diện cho một secret mà một service khai báo là bắt buộc để khởi động thành công.

| Field | Kiểu | Mô tả | Validation |
|---|---|---|---|
| `Name` | string | Tên định danh secret (vd: `ConnectionStrings:Default`, `Jwt:SigningKey`) — trùng với key trong `IConfiguration` | Không rỗng; phải khớp một key thực tồn tại trong configuration schema của service |
| `OwningService` | string | Service tiêu thụ secret (parties, products, baskets, orders, identity, gateway, bff) | Một trong các service đã liệt kê ở Technical Context |
| `Source` | enum | `EnvironmentVariable` \| `MountedFile` — cơ chế service nhận giá trị tại runtime | Bắt buộc chọn một trong hai (xem research.md #2) |
| `IsSensitive` | bool | Đánh dấu giá trị không bao giờ được ghi ra log/trace | Luôn `true` cho mọi `RequiredSecret` trong feature này |

**Validation rule (FR-007)**: Khi service khởi động, mọi `RequiredSecret` của service đó PHẢI được resolve thành công (giá trị không null/rỗng, đúng định dạng mong đợi nếu có ràng buộc định dạng — vd: connection string hợp lệ). Nếu không, service dừng khởi động (fail-fast) và log lỗi có cấu trúc chứa `Name` và `OwningService`, không chứa giá trị secret.

**State transitions**: Không có — đây là kiểm tra một lần tại thời điểm khởi động (startup-time validation), không phải entity có vòng đời runtime.

## 2. ExternalSecretBinding (manifest khai báo dưới `deploy/k8s/<service>/`)

Đại diện cho ánh xạ giữa một secret trong Vault và một Kubernetes `Secret` object mà service tiêu thụ, được ESO đồng bộ (theo ADR-0007). Đây là artifact khai báo (YAML), không phải dữ liệu runtime của ứng dụng — repo chỉ chứa cấu trúc/tên, không chứa giá trị thật.

| Field | Kiểu | Mô tả |
|---|---|---|
| `serviceName` | string | Service sở hữu binding (khớp `RequiredSecret.OwningService`) |
| `vaultPath` | string | Đường dẫn tới secret trong Vault (placeholder trong repo, giá trị thật thuộc phạm vi hạ tầng — xem research.md #1) |
| `k8sSecretName` | string | Tên `Secret` object K8s được ESO tạo ra, mà manifest triển khai của service tham chiếu qua `envFrom.secretRef` |
| `refreshInterval` | duration | Chu kỳ ESO đồng bộ lại từ Vault (liên quan SC-004 — service phải nhận giá trị mới trong tối đa 5 phút) |
| `keys[]` | list<string> | Danh sách key trong K8s Secret, mỗi key khớp một `RequiredSecret.Name` của service đó |

**Quan hệ**: một `ExternalSecretBinding` → nhiều `RequiredSecret` (1-n, qua `keys[]` ↔ `RequiredSecret.Name`); một `RequiredSecret` được resolve bởi đúng một `ExternalSecretBinding` của service sở hữu nó.

## 3. SecretScanFinding (kết quả tạm thời trong lúc chạy CI, không lưu trữ lâu dài trong repo)

Đại diện cho một phát hiện của gitleaks (`ci/secret-scan`) hoặc Trivy secret-scan (`ci/image-secret-scan`) khi quét git history hoặc image filesystem.

| Field | Kiểu | Mô tả |
|---|---|---|
| `source` | enum | `GitHistory` \| `ContainerImage` |
| `location` | string | Commit SHA + đường dẫn file (GitHistory) hoặc layer + đường dẫn file (ContainerImage) |
| `ruleMatched` | string | Tên rule/pattern đã khớp (vd: `generic-api-key`, `connection-string`) |
| `outcome` | enum | `Blocked` — mọi finding đều chặn merge/build (fail-closed, FR-006); không có trạng thái "waived"/"ignored" per-finding trong pipeline, chỉ có allowlist cấu hình sẵn trong `.gitleaks.toml`/Trivy ignore-file cho false-positive đã xác nhận trước |

**Validation rule (FR-006, SC-005)**: bất kỳ `SecretScanFinding` nào xuất hiện đều khiến stage CI tương ứng thất bại; không có cơ chế bỏ qua tại thời điểm chạy (per-PR override) — chỉ có thể loại trừ trước qua cấu hình allowlist đã được review, áp dụng cho toàn repo chứ không phải riêng một PR.
