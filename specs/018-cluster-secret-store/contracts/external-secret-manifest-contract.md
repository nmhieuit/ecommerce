# Hợp đồng: Manifest `Secret` / `ExternalSecret` khai báo cho từng service

**Feature**: [../spec.md](../spec.md) | Liên quan: FR-002, FR-003, SC-003, SC-004

Hợp đồng này quy định cấu trúc manifest khai báo đặt dưới `deploy/k8s/<service>/`. Đây là artifact **khai báo/documentation-as-code** chuẩn bị cho hạ tầng Vault/ESO (ADR-0007) — repo không tự triển khai Vault/ESO trong phạm vi feature này (xem [../research.md](../research.md) mục 1); manifest ở đây là hợp đồng để đội hạ tầng áp dụng khi Vault/ESO được provision.

## `external-secret.yaml` — cấu trúc bắt buộc

Mỗi service PHẢI có đúng một `ExternalSecret` khai báo, với các trường tối thiểu sau (dùng API của External Secrets Operator):

```yaml
apiVersion: external-secrets.io/v1
kind: ExternalSecret
metadata:
  name: <service>-secrets            # convention: "<tên-service>-secrets"
  namespace: <service-namespace>
spec:
  refreshInterval: 5m                 # PHẢI <= 5 phút, khớp SC-004 (rotate không cần redeploy)
  secretStoreRef:
    name: vault-backend                # tên SecretStore/ClusterSecretStore dùng chung, do hạ tầng cấu hình
    kind: ClusterSecretStore
  target:
    name: <service>-secrets            # tên K8s Secret object được tạo ra — service tham chiếu tên này
    creationPolicy: Owner
  data:
    - secretKey: <RequiredSecret.Name, dạng biến môi trường: ":" -> "__">
      remoteRef:
        key: <vaultPath của service>   # placeholder trong repo, giá trị thật do đội hạ tầng điền khi provision
        property: <tên field trong Vault>
```

## `secret.example.yaml` — tài liệu tham khảo (không áp dụng vào cluster)

File này **không được apply vào bất kỳ cluster thật nào** — mục đích duy nhất là minh hoạ hình dạng của `Secret` object mà ESO sẽ tạo ra, để review viên có thể xác nhận tên key khớp với `service-configuration-contract.md` mà không cần quyền truy cập Vault. File PHẢI có comment đầu file nêu rõ đây là ví dụ, và PHẢI dùng giá trị placeholder rõ ràng vô hại (vd: `"REPLACE_ME"`), không dùng giá trị trông giống secret thật (tránh false-positive khi gitleaks/Trivy quét chính file này — xem allowlist trong `.gitleaks.toml`).

## Ràng buộc đặt tên (contract, không phải gợi ý)

| Thành phần | Convention |
|---|---|
| Tên `ExternalSecret` | `<service>-secrets` |
| Tên K8s `Secret` đích | `<service>-secrets` (trùng tên `ExternalSecret.spec.target.name`) |
| `secretKey` trong `data[]` | Dạng **biến môi trường** .NET của `RequiredSecret.Name` — thay `:` bằng `__` (double underscore), vd: `RequiredSecret.Name = "ConnectionStrings:OrdersDb"` (dạng configuration-key, dùng trong code/thông báo lỗi của `RequiredSecretsValidator`) ↔ `secretKey = "ConnectionStrings__OrdersDb"` (dạng env var, vì `:` không hợp lệ trong tên biến môi trường POSIX — K8s `envFrom.secretRef` nạp thẳng tên key của `Secret` object làm tên biến môi trường trong container). Hai dạng này tương đương nhau qua cơ chế dịch chuẩn của .NET Configuration (env var provider tự động đổi `__` thành `:`) — không phải một chuỗi khớp literal. |

Đổi bất kỳ tên nào ở trên PHẢI cập nhật đồng thời: `RequiredSecret` liên quan trong `shared/ServiceDefaults`, manifest triển khai của service (`envFrom.secretRef.name`), và `external-secret.yaml` — trong cùng một PR (nguyên tắc "tên là hợp đồng" kế thừa từ ADR-0012 §Consequences).

## Kiểm chứng hợp đồng

Vì Vault/ESO thật chưa được provision (xem research.md #1), kiểm chứng trong phạm vi feature này giới hạn ở:
- YAML lint/schema validation cho `external-secret.yaml` chạy trong CI (kubeconform hoặc tương đương, dùng CRD schema của ESO).
- Đối chiếu thủ công/tự động giữa `secretKey` trong manifest và `RequiredSecret.Name` trong code (có thể là một test đơn giản đọc cả hai nguồn và so khớp — xem tasks.md).
- Independent Test của User Story 2 trong spec (triển khai vào "môi trường cluster thử nghiệm") có thể thực hiện bằng một cluster tối thiểu cục bộ (kind/k3d) với ESO trỏ vào một Vault dev-mode tạm thời, KHÔNG phải Vault production của ADR-0007 — đủ để xác nhận manifest đúng cấu trúc và service khởi động được từ K8s Secret, không xác nhận toàn bộ thuộc tính HA/audit của Vault production.
