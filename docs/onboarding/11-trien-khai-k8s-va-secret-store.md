# 11 — Triển khai K8s và cluster secret store (thật, mới có)

> Đọc [01-tong-quan-kien-truc.md](01-tong-quan-kien-truc.md) và [09-bff-dependency-downstream-va-trien-khai.md § Phần 4](09-bff-dependency-downstream-va-trien-khai.md#phần-4--kubernetes-đã-có-thật-chuyển-sang-tài-liệu-riêng) trước. Tài liệu 09 lúc viết lần đầu chỉ có thể PHÂN TÍCH khả năng sẵn sàng cho K8s vì chưa có file K8s nào trong repo — 2 spec `018-cluster-secret-store` và `019-liveness-readiness-probes` sau đó đã merge, tạo ra code K8s THẬT. Tài liệu này thay thế phần phân tích cũ bằng nội dung thật.

## 0. Câu hỏi quan trọng nhất trước tiên: "thật" tới mức nào?

Trả lời trực tiếp bằng chính lời của dự án — đọc nguyên văn phần "Amendment" cuối [`docs/adr/0007-secrets-delivery.md`](../../docs/adr/0007-secrets-delivery.md) (ADR gốc quyết định dùng Vault + External Secrets Operator, viết TRƯỚC khi có code):
> *"`specs/018-cluster-secret-store` implemented the half of this decision that does not require the infrastructure above to exist yet... This action items list above is unchanged and still fully open — no Vault instance and no ESO installation exist anywhere for this platform as of this amendment. The application code now depends on exactly the shape of `Secret` object Action Item 2 describes, but nothing yet produces one outside of manual `kubectl apply` against a placeholder-example."*

Nói ngắn gọn: **phần ứng dụng (code .NET) đã sẵn sàng cho K8s thật; hạ tầng K8s/Vault/ESO thật thì CHƯA — không có cluster nào, không có Vault nào đang chạy.** Mọi thứ dưới `deploy/` là **hợp đồng/template đã review được**, chưa phải cấu hình đang vận hành. Đây là điểm dễ hiểu lầm nhất nếu chỉ lướt qua tên thư mục `deploy/k8s/`.

## 1. Bỏ hardcode secret (spec 018-cluster-secret-store)

### 1.1 Trước/sau: xoá mật khẩu khỏi file commit

`git diff` thật trên `services/orders/src/Orders.Api/appsettings.Development.json`:
```diff
-  "ConnectionStrings": {
-    "OrdersDb": "Server=localhost,14333;...;Password=Change_Me_Local_Dev_Only!;..."
-  },
+  "//ConnectionStrings": "...Running `dotnet run` directly, set your own local secret once:
+    dotnet user-secrets set \"ConnectionStrings:OrdersDb\" \"...Password=<your local
+    MSSQL_SA_PASSWORD>...\" --project services/orders/src/Orders.Api. Without it, the service
+    fails fast at startup (RequiredSecretsValidation.cs) instead of starting with no database.",
```
Trước đây, `Password=Change_Me_Local_Dev_Only!` nằm thẳng trong file **đã commit** (dù chỉ là mật khẩu local, đây vẫn là điều constitution Principle VI cấm). Giờ file commit không còn giá trị thật nào — thay bằng hướng dẫn dùng [`dotnet user-secrets`](https://learn.microsoft.com/aspnet/core/security/app-secrets) (cơ chế Secret Manager có sẵn của .NET, lưu secret NGOÀI thư mục repo, trong `%APPDATA%`/`~/.microsoft/usersecrets/`, không bao giờ bị `git add` nhầm).

### 1.2 `RequiredSecretsValidation.cs` — thất bại ngay lúc khởi động, không chờ tới request đầu tiên

[`shared/ServiceDefaults/RequiredSecretsValidation.cs`](../../shared/ServiceDefaults/RequiredSecretsValidation.cs):
```csharp
public static RequiredSecret ConnectionString(string name) =>
    new($"ConnectionStrings:{name}", configuration =>
    {
        var value = configuration.GetConnectionString(name);
        return HasCredential(value) ? value : null;   // chỉ "có giá trị" nếu THẬT SỰ có Password=/Pwd=/...
    });
```
Chi tiết dễ bỏ sót: **không chỉ kiểm tra rỗng hay không.** Comment giải thích lý do — `appsettings.json` gốc (không phải bản `.Development.json`) vẫn cố tình giữ 1 connection string có host/database nhưng **không có credential** (để service biết kết nối tới đâu ngay cả khi chưa inject secret) — nên 1 kiểm tra "không rỗng" đơn thuần sẽ luôn pass dù chưa có mật khẩu thật nào. `HasCredential(...)` kiểm tra cụ thể có `Password=`/`Pwd=`/`Integrated Security=true`/`Trusted_Connection=true` hay không.

[`ServiceDefaultsExtensions.AddRequiredSecretsValidation(...)`](../../shared/ServiceDefaults/ServiceDefaultsExtensions.cs) đăng ký `.ValidateOnStart()` — dùng ở mỗi service, ví dụ [`services/orders/src/Orders.Api/Program.cs`](../../services/orders/src/Orders.Api/Program.cs):
```csharp
builder.AddServiceDefaults();
builder.AddRequiredSecretsValidation(RequiredSecret.ConnectionString("OrdersDb"));
```
Hệ quả thực dụng: nếu cluster (hoặc dev machine) quên inject `ConnectionStrings__OrdersDb` thật, `orders-api` **không khởi động được** — dừng ngay ở `ValidateOnStart()`, không bao giờ tới `app.Run()`, không bao giờ accept 1 request nào rồi mới lộ ra lỗi kết nối DB. Đây là ví dụ cụ thể của nguyên tắc "fail fast" đã thấy ở [09](09-bff-dependency-downstream-va-trien-khai.md) cho `DownstreamServiceClientOptions` — cùng triết lý, áp dụng cho secret thay vì URL downstream.

### 1.3 `deploy/k8s/` — hợp đồng `ExternalSecret`, chưa phải cấu hình sống

[`deploy/k8s/README.md`](../../deploy/k8s/README.md) tự mô tả rất rõ ràng — trích nguyên văn:
> *"This directory holds **declarative manifests only**... It does **not** provision any infrastructure."*

Cấu trúc: mỗi trong 5 service có DB (`baskets`/`orders`/`parties`/`products`/`identity` — KHÔNG có `gateway`/`bff`, khớp đúng việc 2 service đó không sở hữu database, đã xác nhận ở [01](01-tong-quan-kien-truc.md)) có 2 file:

[`deploy/k8s/orders/external-secret.yaml`](../../deploy/k8s/orders/external-secret.yaml) (rút gọn):
```yaml
apiVersion: external-secrets.io/v1
kind: ExternalSecret
metadata:
  name: orders-secrets
spec:
  refreshInterval: 5m          # rotate trong Vault, không cần redeploy — SC-004
  secretStoreRef: { name: vault-backend, kind: ClusterSecretStore }
  data:
    - secretKey: ConnectionStrings__OrdersDb   # PHẢI khớp đúng RequiredSecret.Name dạng biến môi trường
      remoteRef: { key: secret/orders, property: connectionstring }
```
[`deploy/k8s/orders/secret.example.yaml`](../../deploy/k8s/orders/secret.example.yaml) — comment đầu file nói rõ: *"EXAMPLE ONLY — never apply this file to any cluster."* Chỉ để reviewer thấy hình dạng `Secret` object cuối cùng sẽ ra sao, dùng giá trị giả `REPLACE_ME` (cố tình không giống chuỗi kết nối thật, để không bị chính `gitleaks` ở mục 1.4 báo nhầm là rò rỉ secret thật).

Đường nối 2 chiều tường minh: `secretKey: ConnectionStrings__OrdersDb` trong YAML này phải khớp CHÍNH XÁC với `RequiredSecret.ConnectionString("OrdersDb")` trong `Program.cs` (mục 1.2) — cùng tên, chỉ khác định dạng (`ConnectionStrings:OrdersDb` trong C#/JSON vs `ConnectionStrings__OrdersDb` dạng biến môi trường K8s dùng). `deploy/k8s/README.md` gọi đây là "naming contract, do not diverge without updating both sides".

### 1.4 CI thực thi: gitleaks + Trivy — và lời sửa cho 1 suy đoán sai trước đó

2 stage mới trong `Jenkinsfile` (đã thấy tổng quan ở [07](07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md)):

- **`ci/secret-scan`** ([`scripts/ci/run-secret-scan.sh`](../../scripts/ci/run-secret-scan.sh)) — chạy `gitleaks` quét **toàn bộ lịch sử git** (`--log-opts="--all"`), không chỉ commit mới nhất. Chi tiết hay: dùng `--baseline-path .gitleaks-baseline.json` — vì chính mật khẩu `Change_Me_Local_Dev_Only!` vừa xoá ở mục 1.1 **vẫn còn tồn tại vĩnh viễn** trong các commit LỊCH SỬ trước khi được xoá (viết lại lịch sử git để xoá dấu vết đã bị từ chối, coi là hành động rủi ro cao không cần thiết). Baseline ghi lại đúng 9 phát hiện "đã biết, đã khắc phục" đó 1 lần — scan từ nay về sau chỉ chặn phát hiện MỚI, không nằm trong baseline.
- **`ci/image-secret-scan`** ([`scripts/ci/run-image-secret-scan.sh`](../../scripts/ci/run-image-secret-scan.sh)) — dùng `Trivy` quét bên trong **image Docker đã build**, phát hiện secret vô tình bị bake vào layer image (khác `gitleaks` chỉ quét source/git history).

**Sửa lại 1 điều tôi từng suy đoán sai** ở [06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md](06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md): trước đây, khi thấy nhánh git `fix/sonarqube-community-plugin-agent`, tôi *đoán* nó liên quan tới việc SonarQube Community Edition thiếu plugin phân tích .NET chính chủ. Đọc trực tiếp [`docker/ci/jenkins.Dockerfile`](../../docker/ci/jenkins.Dockerfile) (chính là nội dung PR đó) cho thấy **sự thật khác hẳn**: nhánh này dựng lại 1 Jenkins image từng bị mất (`ecomerce-ci-jenkins:local`, trước đó chỉ tồn tại dưới dạng image build tay, không ai lưu lại cách build), và **tiện thể** cài luôn `gitleaks`+`Trivy` làm 2 công cụ cho 2 stage secret-scan của spec 018 này — không liên quan gì tới plugin SonarQube cả. Bài học giữ lại: suy đoán từ tên nhánh, dù hợp lý về mặt logic, vẫn có thể sai hẳn — luôn ưu tiên đọc nội dung PR thật khi có thể.

## 2. Liveness/readiness probe thật trên K8s (spec 019-liveness-readiness-probes)

### 2.1 `deploy/ansible/` — Ansible render ra Deployment K8s thật từ 1 template

Cấu trúc: [`deploy/ansible/deploy.yml`](../../deploy/ansible/deploy.yml) (playbook) + [`inventories/services.yml`](../../deploy/ansible/inventories/services.yml) (danh sách service + override riêng) + `roles/service_deployment/` (role dùng chung, gồm `defaults/main.yml`, `tasks/main.yml`, và [`templates/deployment.yaml.j2`](../../deploy/ansible/roles/service_deployment/templates/deployment.yaml.j2) — template Jinja2 thật):
```yaml
livenessProbe:
  httpGet: { path: {{ liveness_path }}, port: {{ container_port }} }
  initialDelaySeconds: {{ liveness_initial_delay_seconds }}
  periodSeconds: {{ liveness_period_seconds }}
  failureThreshold: {{ liveness_failure_threshold }}
readinessProbe:
  httpGet: { path: {{ readiness_path }}, port: {{ container_port }} }
  ...
```
`liveness_path`/`readiness_path` map thẳng vào `/health/live`/`/health/ready` — 2 route health-check đã thấy xuyên suốt từ [02](02-orders-service-va-cac-api-endpoint.md).

### 2.2 Ngưỡng probe: tái dùng đúng số đã kiểm chứng ở Docker Compose, không phải số tự nghĩ

[`roles/service_deployment/defaults/main.yml`](../../deploy/ansible/roles/service_deployment/defaults/main.yml) chia 2 nhóm mặc định, `db_backed` (baskets/orders/parties/products/identity) và `stateless` (gateway/bff) — comment gốc nói thẳng nguồn số liệu:
> *"Nhóm db_backed.readiness tái sử dụng chính xác giá trị healthcheck dùng chung (`&service-healthcheck`) trong `docker-compose.yml`: interval 5s → period_seconds, timeout 5s → timeout_seconds, retries 20 → failure_threshold, start_period 10s → initial_delay_seconds."*

Đây chính là khối YAML `x-service-healthcheck` đã đọc ở [01-tong-quan-kien-truc.md](01-tong-quan-kien-truc.md) — không phải trùng hợp, mà là quyết định có chủ đích: ngưỡng "SQL Server cần bao lâu để phục hồi sau restart" đã được kiểm chứng qua vận hành local với Docker Compose, nên K8s thừa hưởng đúng số đó thay vì đoán lại từ đầu.

### 2.3 `maxUnavailable: 0` — không pod nào biến mất trước khi pod mới sẵn sàng

[`RolloutStrategyTests.cs`](../../tests/DeploymentManifestConventionTests/RolloutStrategyTests.cs):
```csharp
Assert.Equal("RollingUpdate", strategy!.Type);
Assert.Equal(0, strategy.RollingUpdate!.MaxUnavailable);
```
Comment gốc: *"Kubernetes mặc định cho phép `maxUnavailable: 25%` — để 1 pod biến mất TRƯỚC KHI pod thay thế nó sẵn sàng, đúng khoảng trống tính năng này tồn tại để đóng lại."* `maxUnavailable: 0` buộc K8s phải chờ pod mới **pass readiness** trước khi rút pod cũ khỏi load balancer — nếu không có dòng này, 1 lần deploy bình thường có thể gây gián đoạn thật cho người dùng đang có request dở dang.

### 2.4 Scanner C# (`tests/DeploymentManifestConventionTests`) — đã giải thích chi tiết ở tài liệu 07

Không lặp lại cơ chế ở đây — xem [07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md § 5](07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md#5-testsdeploymentmanifestconventiontests--mọi-service-phải-khai-báo-đúng-livenessreadiness-probe-mới-spec-019). Tóm tắt 1 câu: dự án này tự render Jinja2 bằng C# (không cần cài Ansible) để kiểm quy ước; stage CI `deployment manifest lint` (mục dưới) kiểm lại 1 lần nữa bằng Ansible + kubeconform thật.

### 2.5 CI thực thi: `ansible-lint` + `kubeconform`, không publish GitHub check

[`scripts/ci/lint-deployment-manifests.sh`](../../scripts/ci/lint-deployment-manifests.sh) — 2 bước: `ansible-lint` trên chính role Ansible (bắt lỗi cú pháp/quy ước Ansible), rồi render từng service bằng `ansible-playbook --check --diff` (KHÔNG chạm cluster nào — chỉ mô phỏng) và validate kết quả bằng `kubeconform` (so khớp schema OpenAPI thật của Kubernetes). Đã nêu ở [07](07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md): stage này **không publish status check GitHub** như 7 stage kia — 1 thất bại vẫn làm đỏ cả pipeline Jenkins, nhưng không hiện thành 1 dòng check riêng trên PR.

## 3. Vậy có thể `kubectl apply` lên 1 cluster thật ngay bây giờ không?

**Không — và đây không phải suy đoán, mà trích thẳng ADR-0007's Action Items, vẫn còn nguyên trạng thái `[ ]` chưa làm ở thời điểm viết tài liệu này:**
1. Chưa có Vault nào được deploy (HA, backup/DR).
2. Chưa có ESO (External Secrets Operator) nào được cài.
3. Chưa có `ClusterSecretStore` nào nối 2 thứ trên với `deploy/k8s/*/external-secret.yaml`.

Những gì THẬT SỰ đã sẵn sàng: (a) code .NET của mọi service đã đọc secret đúng theo quy ước sẽ khớp với `ExternalSecret` khi nó tồn tại (mục 1.2-1.3); (b) template Deployment đã có probe + rollout strategy đúng (mục 2); (c) cả 2 mảng đều có test/lint tự động giữ đúng quy ước (mục 1.4, 2.4-2.5). Việc còn thiếu là **hạ tầng vận hành thật** (Vault, ESO, 1 cluster K8s thật) — đây là công việc hạ tầng, không phải công việc code, và [`deploy/ansible/README.md`](../../deploy/ansible/README.md)/`deploy/k8s/README.md` là 2 tài liệu nên đọc tiếp nếu bạn (hay đội hạ tầng) chuẩn bị làm việc đó.

## Đi đâu tiếp theo

- [09-bff-dependency-downstream-va-trien-khai.md](09-bff-dependency-downstream-va-trien-khai.md) — vì sao cơ chế đọc địa chỉ downstream qua biến môi trường vốn đã "sẵn sàng cho K8s" từ trước khi K8s thật xuất hiện.
- [07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md](07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md) — toàn bộ 9 stage Jenkinsfile hiện tại, gồm 3 stage nhắc ở tài liệu này.
- [`docs/adr/0007-secrets-delivery.md`](../../docs/adr/0007-secrets-delivery.md) — toàn văn quyết định Vault+ESO và phần Amendment đã trích ở Mục 0.
