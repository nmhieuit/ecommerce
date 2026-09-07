# Research: Liveness/readiness probe cho mọi service trên Kubernetes

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-06

## Quyết định 1: Cơ chế tác giả & áp dụng manifest

**Decision**: Dùng một role Ansible dùng chung, render một template Kubernetes Deployment duy nhất
(Jinja2) áp dụng cho cả 7 service, tham số hóa theo từng service (tên, cổng, có sở hữu cơ sở dữ
liệu hay không, override thời gian probe nếu cần). Áp dụng manifest đã render vào cluster qua
module `kubernetes.core.k8s` của Ansible.

**Rationale**:
- Constitution cố định "Platform: containers on Kubernetes, provisioned and configured through
  Ansible" như một ràng buộc không thể thương lượng lại theo từng feature — Ansible là công cụ đã
  được quyết định sẵn, không phải lựa chọn của tính năng này.
- Dùng một template dùng chung thay vì viết tay 7 manifest riêng biệt nhất quán với cách constitution
  đã xử lý các mối quan tâm cấu hình lặp lại khác trong hệ thống (`ServiceDefaults` cho
  observability, thư viện `Tenancy` cho tenant resolution) — Principle VII còn nói thẳng
  "observability MUST NOT be configured per service by hand", và cùng logic đó áp dụng hợp lý cho
  cấu hình probe.
- Chưa có thư mục Ansible hay manifest K8s nào tồn tại trong repository, và không có tài liệu nào
  (ADR, ADR-0007, tech-stack-decisions.md) chỉ ra một repository hạ tầng tách biệt — nên đặt trong
  cùng monorepo là lựa chọn ít giả định nhất.

**Alternatives considered**:
- **Helm chart**: Bị loại vì constitution nêu đích danh Ansible là công cụ cấp phát/cấu hình nền
  tảng; đưa Helm vào sẽ là một bổ sung ngăn xếp công nghệ ngoài phạm vi đặc tả này và cần ADR riêng.
- **Viết tay YAML riêng cho từng service**: Bị loại vì lặp lại khối probe 7 lần, dễ lệch nhau theo
  thời gian (đúng vấn đề mà Principle VII đã cảnh báo với observability) và không có điểm tập trung
  duy nhất để sửa ngưỡng thời gian probe khi cần điều chỉnh chung.

## Quyết định 2: Giá trị mặc định cho tham số thời gian probe

**Decision**: Suy ra giá trị mặc định cho `initialDelaySeconds`, `periodSeconds`, `timeoutSeconds`,
`failureThreshold` của từng probe Kubernetes từ chính cấu hình `healthcheck` trong `docker-compose.yml`
đang dùng cho môi trường local — cụ thể là block `service-healthcheck` dùng chung
(`interval: 5s, timeout: 5s, retries: 20, start_period: 10s`) cho các service có cơ sở dữ liệu
riêng, chuyển đổi trực tiếp sang `periodSeconds`/`timeoutSeconds`/`failureThreshold`/`initialDelaySeconds`
tương ứng. Với gateway và BFF (không sở hữu cơ sở dữ liệu, readiness luôn "Healthy" ngay khi tiến
trình chạy), dùng `initialDelaySeconds` ngắn hơn vì không phải chờ kết nối dependency.

**Rationale**: `docker-compose.yml` đã ghi lại tường minh (bằng comment) lý do các ngưỡng này được
chọn — ví dụ SQL Server cần thời gian phục hồi sau restart, tránh lỗi "database already exists" khi
retry quá sớm. Tái sử dụng các ngưỡng đã được kiểm chứng qua vận hành local thay vì tự đặt ngưỡng
mới giữ hành vi giữa môi trường local và cluster nhất quán, đúng tinh thần FR-007 ("đủ dung sai,
không kích hoạt sai").

**Alternatives considered**:
- **Dùng giá trị mặc định chung chung theo ví dụ phổ biến của Kubernetes** (ví dụ
  `initialDelaySeconds: 3`): Bị loại vì SQL Server đã được ghi nhận rõ ràng trong repo là cần thời
  gian khởi động lâu hơn mức mặc định thông thường; áp một ngưỡng chung sẽ tái tạo lại chính sự cố
  "mười lần restart/stop thất bại" mà comment trong `docker-compose.yml` đã mô tả, chỉ là ở tầng
  cluster thay vì local.

## Quyết định 3: Cách xác thực & kiểm thử

**Decision**: Thêm dự án xUnit mới `tests/DeploymentManifestConventionTests`, theo đúng khuôn mẫu
convention-test đã có (`tests/ContainerConventionTests/DockerfileSharedProjectTests.cs`) — render
template `deployment.yaml.j2` với biến của từng service (không cần Ansible runtime, chỉ cần một
Jinja2/YAML renderer nhẹ hoặc gọi `ansible-playbook --check` cục bộ) rồi assert: cả hai probe đều
tồn tại, đúng đường dẫn (`/health/live`, `/health/ready`), liveness probe không gắn tag phụ thuộc
dependency ngoài, và tham số thời gian nằm trong khoảng đã tài liệu hóa. Bổ sung `ansible-lint` và
`kubeconform`/`kubeval` vào pipeline CI để xác thực cú pháp/schema tĩnh của manifest đã render. Một
kịch bản smoke test trên cluster `kind` cục bộ (chạy thủ công hoặc theo lịch, không chặn mọi PR)
xác nhận các hành vi động (loại trừ traffic khi chưa sẵn sàng, tự khởi động lại khi treo) mà test
tĩnh không thể khẳng định.

**Rationale**: Constitution Principle III (Test-First, NON-NEGOTIABLE) yêu cầu có test thất bại
trước khi có implementation; test đọc-và-assert-cấu-trúc là cách khả thi để kiểm thử manifest mà
không cần một cluster K8s thật chạy trong mọi lần build — đúng mẫu hình repo đã áp dụng cho Dockerfile.
Đưa smoke test động vào một pipeline định kỳ thay vì mọi PR nhất quán với cách constitution đã xử lý
performance test (Principle VIII: "performance tests for critical paths run on a scheduled
pipeline"), tránh chi phí/độ không ổn định của việc dựng cluster thật cho mọi lần build.

**Alternatives considered**:
- **Chỉ xác thực thủ công, không có test tự động**: Bị loại vì vi phạm trực tiếp Principle III
  (NON-NEGOTIABLE).
- **Bắt buộc mọi PR phải deploy lên cluster thật để test hành vi động**: Bị loại vì chi phí/độ trễ/
  độ không ổn định của CI không tương xứng với một mối quan tâm chủ yếu mang tính cấu trúc (đúng
  cấu hình probe), và không nhất quán với cách constitution đã tách performance/resilience test
  động sang pipeline theo lịch riêng.

## Tổng kết: NEEDS CLARIFICATION đã được giải quyết

Không có mục nào trong Technical Context còn để "NEEDS CLARIFICATION" — cả ba quyết định trên đều
dựa trên ràng buộc đã cố định sẵn trong constitution (Ansible, Kubernetes) hoặc trên cấu hình/khuôn
mẫu đã tồn tại và được kiểm chứng trong chính repository (docker-compose healthcheck, convention
test pattern).
