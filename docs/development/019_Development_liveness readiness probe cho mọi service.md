# Bước 019: Thay đổi nghiệp vụ so với bước 018

## Phạm vi

Bước 019 không đổi bất kỳ file nào dưới `services/` hay `shared/` — xác nhận bằng `git show --stat`.
Đây là tính năng đầu tiên tạo ra thư mục `deploy/ansible/`; trước đó repo hoàn toàn chưa có manifest
K8s Deployment hay tài nguyên Ansible nào (kể cả bước 018, vốn chỉ có `deploy/k8s/` — manifest
`ExternalSecret` khai báo, không phải Deployment).

Ranh giới commit được xác định từ lịch sử Git:

- Mốc hoàn tất bước 018: commit `46f0a61`.
- Đặc tả và triển khai bước 019 — mốc hoàn tất, một commit duy nhất (merge qua PR #24): commit
  `00eeba0`.

`tests/DeploymentManifestConventionTests` (project C# tự render Jinja2 để xác nhận probe, KHÔNG gọi
`ansible-playbook`) là nội dung kiểm thử, bị loại theo đúng phạm vi đã áp dụng — chỉ được nhắc tên.

## 1. Template Deployment K8s — `deployment.yaml.j2`

[deploy/ansible/roles/service_deployment/templates/deployment.yaml.j2](../../deploy/ansible/roles/service_deployment/templates/deployment.yaml.j2)

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ service_name }}
spec:
  strategy:
    type: RollingUpdate
    rollingUpdate:
      # 019: Kubernetes mặc định 25% — cho phép rút 1 pod cũ TRƯỚC KHI pod mới sẵn sàng.
      # Khai báo tường minh 0 để không pod nào bị rút khỏi traffic sớm.
      maxUnavailable: 0
      maxSurge: 1
  template:
    spec:
      containers:
        - name: {{ service_name }}
          image: "{{ image }}"
          livenessProbe:
            httpGet:
              path: {{ liveness_path }}
              port: {{ container_port }}
            initialDelaySeconds: {{ liveness_initial_delay_seconds }}
            periodSeconds: {{ liveness_period_seconds }}
            timeoutSeconds: {{ liveness_timeout_seconds }}
            failureThreshold: {{ liveness_failure_threshold }}
          readinessProbe:
            httpGet:
              path: {{ readiness_path }}
              port: {{ container_port }}
            initialDelaySeconds: {{ readiness_initial_delay_seconds }}
            periodSeconds: {{ readiness_period_seconds }}
            timeoutSeconds: {{ readiness_timeout_seconds }}
            failureThreshold: {{ readiness_failure_threshold }}
```

Một template Jinja2 duy nhất cho cả 7 service — lý do bị loại của 2 phương án khác (research.md
Quyết định 1): Helm chart bị loại vì constitution đã nêu đích danh Ansible, thêm Helm là bổ sung
ngăn xếp công nghệ ngoài phạm vi; viết tay 7 manifest YAML riêng bị loại vì lặp khối probe 7 lần, dễ
lệch nhau theo thời gian.

## 2. Ngưỡng probe tái dùng số liệu đã kiểm chứng, không phải số tự nghĩ

[deploy/ansible/roles/service_deployment/defaults/main.yml](../../deploy/ansible/roles/service_deployment/defaults/main.yml)

```yaml
# 019: nhóm db_backed.readiness tái sử dụng CHÍNH XÁC giá trị healthcheck dùng chung
# (&service-healthcheck) trong docker-compose.yml: interval 5s -> period_seconds, timeout 5s ->
# timeout_seconds, retries 20 -> failure_threshold, start_period 10s -> initial_delay_seconds.
# Đây là ngưỡng đã được kiểm chứng qua vận hành local cho việc SQL Server cần thời gian phục hồi
# sau restart, không phải số tự nghĩ ra cho K8s.
probe_defaults:
  db_backed:
    readiness:
      initial_delay_seconds: 10
      period_seconds: 5
      timeout_seconds: 5
      failure_threshold: 20
    # 019: liveness KHÔNG phụ thuộc dependency ngoài (FR-003) — failure_threshold thấp hơn hẳn
    # readiness vì không cần dung sai cho thời gian phục hồi database.
    liveness:
      initial_delay_seconds: 10
      period_seconds: 10
      timeout_seconds: 5
      failure_threshold: 3

  # 019: nhóm cho gateway/bff — không chờ kết nối database nào, nên initial_delay và
  # failure_threshold của readiness ngắn hơn nhiều mà không rủi ro false positive.
  stateless:
    readiness:
      initial_delay_seconds: 2
      period_seconds: 5
      timeout_seconds: 3
      failure_threshold: 3
    liveness:
      initial_delay_seconds: 5
      period_seconds: 10
      timeout_seconds: 5
      failure_threshold: 3
```

## 3. Inventory — 7 service, phân theo `depends_on_database`

[deploy/ansible/inventories/services.yml](../../deploy/ansible/inventories/services.yml)

```yaml
services:
  orders:
    container_port: 8080
    depends_on_database: true

  gateway:
    container_port: 8080
    # 019: gateway/bff là lớp cổng/tổng hợp không trạng thái — readiness "Healthy" ngay khi
    # tiến trình chạy (Gateway.Api/Bff.Api HealthCheckEndpoints.cs không đăng ký check nào).
    depends_on_database: false
```

5 service có database riêng (Parties, Products, Baskets, Orders, Identity) khai báo
`depends_on_database: true`, dùng nhóm ngưỡng `db_backed`; Gateway và BFF khai báo `false`, dùng
nhóm `stateless`.

## 4. CI thêm 1 stage lint, không publish check GitHub riêng

[Jenkinsfile](../../Jenkinsfile)

```groovy
stage('deployment manifest lint') {
    // 019: validate tĩnh deploy/ansible/ (ansible-lint + kubeconform per service). KHÔNG được
    // nối vào registry required-check của pipeline-stage-contract.md — registry đó thuộc về
    // 013-sonarqube-merge-blocker, ngoài phạm vi tính năng này — nhưng lỗi ở đây vẫn làm fail
    // stage và do đó fail cả build, giống mọi bước `sh` khác.
    when { environment name: 'CI_FAST_ITERATION', value: 'false' }
    steps {
        sh 'scripts/ci/lint-deployment-manifests.sh'
    }
}
```

Khác với 2 stage secret-scan của bước 018 (luôn chạy, không bị `CI_FAST_ITERATION` tắt), stage này
BỊ gate bởi `CI_FAST_ITERATION` — chỉ chạy khi cờ đó là `false`.

## Tóm tắt 018 → 019

| Khu vực | Bước 018 | Bước 019 |
|---|---|---|
| `deploy/` | Chỉ `deploy/k8s/` — manifest `ExternalSecret` khai báo | Thêm `deploy/ansible/` — template Deployment thật (Jinja2) |
| Rollout | Chưa có | `maxUnavailable: 0` tường minh, khác mặc định K8s (25%) |
| Probe threshold | Chưa có | Tái dùng số liệu từ `docker-compose.yml` healthcheck đã kiểm chứng |
| CI stage mới | `secret-scan`/`image-secret-scan` (luôn chạy) | `deployment manifest lint` (bị `CI_FAST_ITERATION` gate, không publish check riêng) |
| `services/`/`shared/` code | Có (`RequiredSecretsValidation.cs`...) | Không đổi gì — xác nhận bằng `git show --stat` |

**Kết luận:** bước 019 không thêm nghiệp vụ mua hàng, không đổi bất kỳ code service/shared nào. Nó
tạo lớp triển khai K8s đầu tiên của nền tảng — một template Ansible/Jinja2 duy nhất render Deployment
cho cả 7 service, với ngưỡng probe kế thừa từ số liệu healthcheck Docker Compose đã kiểm chứng qua
vận hành thay vì số tự nghĩ ra, và `maxUnavailable: 0` để rolling update không rút pod cũ khỏi
traffic trước khi pod mới sẵn sàng.

## 5. Shared project trong bước 019

Bước 019 không tạo, không sửa, và không cần bất kỳ `ProjectReference` nào tới
`ServiceDefaults`/`Tenancy`/`EventContracts`/`Identity`. Giống bước 017, đây là bước hoàn toàn không
chạm shared C# project nào — toàn bộ nội dung nằm ở tầng triển khai (Ansible) đứng bên ngoài mã nguồn
của các service.
