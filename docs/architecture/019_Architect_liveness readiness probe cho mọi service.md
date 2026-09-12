# Kiến trúc: Liveness/Readiness Probe cho mọi service trên Kubernetes

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-28 ("[SECURE-3] Liveness/readiness probes on every service"), đặc tả tại
[`specs/019-liveness-readiness-probes/`](../../specs/019-liveness-readiness-probes/). Ba quyết định
kiến trúc: [`research.md`](../../specs/019-liveness-readiness-probes/research.md). Tính năng đầu tiên
tạo ra thư mục `deploy/ansible/` — trước đó repo hoàn toàn chưa có manifest K8s hay tài nguyên Ansible
nào (kể cả `018`, vốn chỉ có `deploy/k8s/` — manifest `ExternalSecret` khai báo, không phải Deployment).

**Trạng thái xác minh**: 26 task `[X]`, 58/58 test xUnit pass. Có 1 lượt xác thực trên cluster `kind`
thật, nhưng lớp kiểm tra Ansible/`ansible-lint`/`kubeconform` chưa hề chạy trong chính phiên triển
khai tính năng này — trình bày trung thực ở [technical-debt.md](technical-debt.md), không che giấu.

## 1. Kiến trúc tổng thể

```
deploy/ansible/inventories/services.yml (7 service + override riêng nếu có)
deploy/ansible/roles/service_deployment/defaults/main.yml (probe_defaults: db_backed vs stateless)
   │ số liệu tái dùng TỪ healthcheck docker-compose.yml đã kiểm chứng qua vận hành (mục 2.2)
   ▼
deploy/ansible/roles/service_deployment/templates/deployment.yaml.j2 (Jinja2)
   │ livenessProbe/readinessProbe, strategy: RollingUpdate maxUnavailable:0
   ▼
Deployment K8s đã render (YAML thật cho từng service)
   │
   ├─ Lớp kiểm tra 1: tests/DeploymentManifestConventionTests (C#, TỰ render Jinja2, không cần Ansible)
   ├─ Lớp kiểm tra 2: scripts/ci/lint-deployment-manifests.sh (ansible-lint + Ansible thật + kubeconform)
   │   — stage "deployment manifest lint" trong Jenkinsfile, KHÔNG publish check GitHub riêng
   └─ Cluster kind thật — xác thực hành vi ĐỘNG, chạy thủ công/định kỳ, không chặn mọi PR
```

## 2. Mô tả từng thành phần

### 2.1. Ansible là công cụ ĐÃ ĐƯỢC QUYẾT ĐỊNH SẴN, không phải lựa chọn của tính năng này (research.md Quyết định 1)

Trích nguyên văn: *"Constitution cố định 'Platform: containers on Kubernetes, provisioned and
configured through Ansible' như một ràng buộc không thể thương lượng lại theo từng feature — Ansible
là công cụ đã được quyết định sẵn."* Đây là lý do **không có ADR mới** cho quyết định dùng Ansible ở
tài liệu này. Alternative bị loại: Helm chart (constitution nêu đích danh Ansible, thêm Helm là bổ
sung ngăn xếp công nghệ ngoài phạm vi, cần ADR riêng); viết tay 7 manifest YAML riêng (lặp khối probe
7 lần, dễ lệch nhau theo thời gian — đúng vấn đề Principle VII đã cảnh báo cho observability, áp dụng
tương tự cho probe).

### 2.2. Ngưỡng probe tái dùng số liệu đã kiểm chứng, không phải số tự nghĩ (research.md Quyết định 2)

`defaults/main.yml` chia 2 nhóm — comment gốc: *"Nhóm db_backed.readiness tái sử dụng chính xác giá
trị healthcheck dùng chung (`&service-healthcheck`) trong `docker-compose.yml`: interval 5s →
period_seconds, timeout 5s → timeout_seconds, retries 20 → failure_threshold, start_period 10s →
initial_delay_seconds."* Đây chính là khối `x-service-healthcheck` đã đọc ở
[01-tong-quan-kien-truc.md](../onboarding/01-tong-quan-kien-truc.md) — không phải trùng hợp: ngưỡng
"SQL Server cần bao lâu để phục hồi sau restart" đã được kiểm chứng qua vận hành local từ trước, K8s
thừa hưởng đúng số đó.

### 2.3. Lớp kiểm tra 1 — `tests/DeploymentManifestConventionTests` (research.md Quyết định 3)

Đã giải thích chi tiết ở [07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md § 5](../onboarding/07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md#5-testsdeploymentmanifestconventiontests--mọi-service-phải-khai-báo-đúng-livenessreadiness-probe-mới-spec-019)
— không lặp lại. Bộ test C# tự render Jinja2 hoàn toàn, không gọi `ansible-playbook` — lý do đổi hướng
khi implement xem [technical-debt.md](technical-debt.md).

### 2.4. Rollout strategy — `maxUnavailable: 0`

Kubernetes mặc định `maxUnavailable: 25%` — cho phép rút 1 pod cũ TRƯỚC KHI pod mới sẵn sàng.
`deployment.yaml.j2` khai báo `maxUnavailable: 0` tường minh, đã kiểm chứng bằng
`RolloutStrategyTests.cs` và trên cluster `kind` thật.

## 3. Bảng quyết định — probe nào phản ứng với gì

| Tình huống | Liveness | Readiness |
|---|---|---|
| Tiến trình bình thường, dependency (DB) healthy | Pass | Pass |
| Dependency ngoài (DB) tạm gián đoạn | **Không đổi — vẫn Pass** (FR-003, cố ý không phụ thuộc dependency ngoài) | Fail → loại khỏi traffic, KHÔNG restart |
| Tiến trình treo/deadlock thật | Fail liên tiếp vượt ngưỡng → Kubernetes restart pod | (không quan sát được — pod đã restart) |
| Rolling update, pod mới chưa Ready | N/A (pod mới) | Pod mới `0/1`, pod cũ vẫn `1/1 Running` phục vụ |

## 4. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/019-liveness-readiness-probes-component.drawio`](../diagrams/019-liveness-readiness-probes-component.drawio)
- Sơ đồ luồng nghiệp vụ: [`docs/diagrams/019-liveness-readiness-probes-flow-nghiep-vu.drawio`](../diagrams/019-liveness-readiness-probes-flow-nghiep-vu.drawio)
- Sơ đồ trình tự: [`docs/diagrams/019-liveness-readiness-probes-sequence.drawio`](../diagrams/019-liveness-readiness-probes-sequence.drawio)

## 5. Tham khảo thêm

`deploy/ansible/` đối chiếu với `deploy/k8s/` (spec `018`) đã có ở
[11-trien-khai-k8s-va-secret-store.md](../onboarding/11-trien-khai-k8s-va-secret-store.md).

**Giới hạn phạm vi đã biết — phần quan trọng nhất của tài liệu gốc**: xác thực trên cluster `kind`
thật (thành công 1 phần, thất bại 1 phần) và việc `ansible-lint`/`kubeconform` chưa từng chạy thật
trong phiên triển khai này — chi tiết đầy đủ xem [technical-debt.md](technical-debt.md).
