# Triển khai Kubernetes qua Ansible

Nguồn: [specs/019-liveness-readiness-probes](../../specs/019-liveness-readiness-probes/) — bổ sung
`livenessProbe`/`readinessProbe` cho manifest triển khai của mọi service, wiring vào hai endpoint
sức khỏe đã có sẵn từ 001-scaffold-service-shells.

## Điều kiện tiên quyết

Ansible **không chạy trực tiếp trên Windows** (giới hạn đã biết của công cụ — cần Linux/macOS hoặc
WSL làm control node). Chạy các lệnh dưới đây từ WSL, một máy Linux CI agent, hoặc macOS:

```bash
python3 -m venv .venv && source .venv/bin/activate
pip install ansible-core ansible-lint
ansible-galaxy collection install kubernetes.core
```

Ngoài ra cần `kubeconform` (hoặc `kubeval`) để xác thực schema tĩnh của manifest đã render — tải
binary phù hợp hệ điều hành từ trang release của dự án.

## Cấu trúc

- `inventories/services.yml` — biến cấu hình cho từng service (schema:
  [contracts/service-deployment-vars.md](../../specs/019-liveness-readiness-probes/contracts/service-deployment-vars.md)).
- `roles/service_deployment/` — role dùng chung, render một template Deployment duy nhất cho mọi
  service (không viết tay khối probe lặp lại 7 lần).
- `deploy.yml` — playbook gốc, lặp qua từng entry trong inventory và include role trên.

## Chạy cục bộ

```bash
ansible-playbook deploy.yml --check --diff -e kubeconfig=~/.kube/config
```

Xem [quickstart.md](../../specs/019-liveness-readiness-probes/quickstart.md) để có kịch bản xác
thực đầy đủ trên cluster `kind` cục bộ, bao gồm cả các hành vi động (loại trừ traffic, tự khởi động
lại) mà bước `--check` ở trên không thể chứng minh.

## Kiểm thử

Bộ test quy ước (`tests/DeploymentManifestConventionTests`) đọc trực tiếp nội dung
`deployment.yaml.j2` dưới dạng text và nội suy biến đơn giản — **không cần cài Ansible** để chạy
`dotnet test`, theo đúng khuôn mẫu `tests/ContainerConventionTests` đã đọc Dockerfile mà không cần
Docker. Ansible chỉ thực sự cần thiết khi áp dụng manifest vào một cluster thật (`ansible-playbook`)
hoặc khi chạy `ansible-lint`/`kubeconform` trong CI.
