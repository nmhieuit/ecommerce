# Quickstart: Xác thực Liveness/Readiness Probe

**Feature**: [spec.md](./spec.md) | Tham chiếu: [data-model.md](./data-model.md), [contracts/](./contracts/)

Hướng dẫn này xác thực end-to-end rằng manifest được sinh ra thỏa các bất biến ở
[contracts/probe-manifest-shape.md](./contracts/probe-manifest-shape.md) và các hành vi động ở
User Story 2, 3 của spec.md. Đây là kịch bản chạy tay/định kỳ — không phải test chặn PR (xem
research.md Quyết định 3).

## Điều kiện tiên quyết

- Đã cài `ansible-core` ≥ 2.16 và collection `kubernetes.core`.
- Đã cài `kind` (Kubernetes in Docker) và `kubectl` để dựng cluster thử nghiệm cục bộ.
- Đã build image của service muốn kiểm tra (dùng lại pipeline build image hiện có của repo).
- Bộ test tĩnh `tests/DeploymentManifestConventionTests` đã pass (`dotnet test tests/DeploymentManifestConventionTests`) — đây là điều kiện cần trước khi thử trên cluster thật.

## Bước 1 — Dựng cluster thử nghiệm

```bash
kind create cluster --name probe-quickstart
```

## Bước 2 — Render và áp dụng manifest cho một service

Ví dụ với `orders` (service có cơ sở dữ liệu, đại diện nhóm `depends_on_database: true`):

```bash
ansible-playbook deploy/ansible/deploy.yml \
  --limit orders \
  -e kubeconfig=$(kind get kubeconfig-path --name probe-quickstart 2>/dev/null || echo ~/.kube/config)
```

**Kỳ vọng**: lệnh chạy thành công, không có task nào fail; xem lại manifest thực tế đã áp dụng bằng
`kubectl get deployment orders -o yaml` và đối chiếu thủ công với 6 bất biến trong
[contracts/probe-manifest-shape.md](./contracts/probe-manifest-shape.md).

## Bước 3 — Xác nhận US2: pod chưa sẵn sàng không nhận traffic

```bash
kubectl rollout restart deployment/orders
kubectl get pods -l app=orders -w
```

**Kỳ vọng**: pod mới hiện `READY 0/1` trong lúc container đang chờ kết nối cơ sở dữ liệu; trong
cùng lúc, gửi request qua Service (`kubectl port-forward svc/orders 8080:8080` rồi `curl`) chỉ chạm
vào các pod cũ đã `READY 1/1`, không bao giờ chạm pod mới cho tới khi pod đó chuyển `READY 1/1`.

## Bước 4 — Xác nhận US3: pod treo bị tự khởi động lại

Giả lập treo bằng cách chặn endpoint kiểm tra tiến trình sống của một pod đang chạy (ví dụ dùng
`kubectl exec` vào container để chiếm dụng luồng xử lý HTTP, hoặc tạm thời áp một `NetworkPolicy`
chặn cổng container — chọn cách phù hợp với image hiện có của service):

```bash
kubectl get pods -l app=orders -w
```

**Kỳ vọng**: sau đúng số lần fail liên tiếp bằng `failureThreshold` đã cấu hình cho `livenessProbe`
(xem giá trị trong `deploy/ansible/roles/service_deployment/defaults/main.yml`), cột `RESTARTS` của
pod đó tăng thêm 1 và pod được Kubernetes khởi động lại — không cần thao tác thủ công nào khác.

## Bước 5 — Xác nhận sự cố dependency ngoài không kích hoạt liveness sai (Edge Case)

Tạm dừng cơ sở dữ liệu mà `orders` phụ thuộc (ví dụ scale deployment SQL Server test về 0) trong lúc
`orders` đang chạy khỏe mạnh:

**Kỳ vọng**: `READY` của pod `orders` chuyển về `0/1` (readiness fail, đúng SC-004), nhưng cột
`RESTARTS` KHÔNG tăng (liveness vẫn pass) — đúng bất biến #4 trong
[contracts/probe-manifest-shape.md](./contracts/probe-manifest-shape.md). Khôi phục cơ sở dữ liệu
và xác nhận `READY` quay lại `1/1` mà không cần restart pod.

## Dọn dẹp

```bash
kind delete cluster --name probe-quickstart
```
