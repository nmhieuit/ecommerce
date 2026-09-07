# Kiến trúc: Secrets qua Cluster Secret Store, loại bỏ cấu hình hardcode

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-27 ("[SECURE-3] Secrets via cluster secret store, remove hardcoded config"),
đặc tả tại [`specs/018-cluster-secret-store/`](../../specs/018-cluster-secret-store/). Bảy quyết định
kiến trúc (2 phát sinh trong lúc implement/xác thực): [`research.md`](../../specs/018-cluster-secret-store/research.md).
Hiện thực hoá nửa "application-side" của [ADR-0007](../adr/0007-secrets-delivery.md) (chọn Vault +
External Secrets Operator) — ADR đó đã được cập nhật 1 mục "Amendment" ghi rõ ranh giới đã làm/chưa
làm, xem mục 5.

**Trạng thái xác minh**: 37 task `[X]` trong `tasks.md` — nhiều nhất trong 4 tính năng của đợt này.
Có 1 lượt xác thực end-to-end **trên cluster K8s thật** (tạm thời, không phải hạ tầng production) —
xem mục 4, đây là bằng chứng mạnh nhất trong cả 4 tài liệu Architect mới này.

## 1. Kiến trúc tổng thể

```
Orders.Api (đại diện 5 service có DB)
   │ RequiredSecret.ConnectionString("OrdersDb") — HasCredential(...) kiểm tra THẬT có
   │ Password=/Pwd=/Trusted_Connection=..., không chỉ check rỗng
   ▼
shared/ServiceDefaults/RequiredSecretsValidation.cs
   │ RequiredSecretsValidator.ValidateOnStart() — chạy TRƯỚC app.Run(), fail-fast
   ▼
IConfiguration (biến môi trường ConnectionStrings__OrdersDb — docker-compose/K8s)
   │
   ├── appsettings.Development.json — KHÔNG còn giá trị thật (chỉ hướng dẫn dotnet user-secrets)
   │
   └── deploy/k8s/orders/external-secret.yaml — hợp đồng ExternalSecret (secretKey PHẢI khớp tên)
          │
          ▼ (đã xác thực end-to-end trên cluster thử nghiệm — mục 4)
       External Secrets Operator + HashiCorp Vault — CHƯA tồn tại cho môi trường production thật
```

Song song, độc lập với runtime: `ci/secret-scan` (gitleaks, toàn bộ lịch sử git) +
`ci/image-secret-scan` (Trivy, filesystem image đã build) — 2 stage CI luôn chạy, không bị
`CI_FAST_ITERATION` bỏ qua (xem [07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md](../onboarding/07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md)).

## 2. Mô tả từng thành phần

### 2.1. `RequiredSecretsValidation.cs` — không chỉ check rỗng (phát hiện thật khi implement T006/T019)

Bản đầu tiên chỉ check "non-blank" — nhưng `appsettings.json` (Production, không phải `.Development`)
**cố tình vẫn giữ** 1 connection string hợp lệ chỉ có host/database, không credential (đúng
`contracts/service-configuration-contract.md` rule 2, để service biết kết nối đâu ngay cả khi chưa có
secret). Hệ quả: nếu chỉ check "non-blank", fail-fast **không bao giờ kích hoạt** khi cluster quên
inject credential thật, vì giá trị base đã sẵn không-rỗng. Vá bằng `HasCredential(...)`: resolve về
`null` (coi là thiếu) trừ khi chuỗi thật sự có `Password=`/`Pwd=`/`Integrated Security=true`/
`Trusted_Connection=true` — viết test RED trước (`ConnectionString_TreatsAHostOnlyValueWithNoCredential_AsMissing`),
xác nhận FAIL, rồi sửa GREEN (13/13 pass).

### 2.2. Cách service đọc secret tại runtime (research.md Decision 2)

Dùng cơ chế cấu hình chuẩn của .NET — biến môi trường ánh xạ từ K8s `Secret` (đồng bộ bởi ESO từ
Vault) qua `envFrom.secretRef`. **Không cần SDK/thư viện client Vault trong mã ứng dụng** — `IConfiguration`
tự nạp biến môi trường, không thêm dependency mới. Khớp đúng lý do ESO được chọn ở ADR-0007: "pods
don't need Vault client/sidecar integration".

### 2.3. `deploy/k8s/<service>/external-secret.yaml` — hợp đồng, không phải cấu hình sống

Đã giải thích chi tiết ở [11-trien-khai-k8s-va-secret-store.md](../onboarding/11-trien-khai-k8s-va-secret-store.md)
mục 1.3 — không lặp lại. Điểm bổ sung riêng cho tài liệu này: naming contract
(`secretKey` PHẢI khớp `RequiredSecret.Name` dạng biến môi trường) đã được **xác nhận thật trên
cluster** (mục 4), không chỉ lý thuyết.

### 2.4. Công cụ secret-scanning (research.md Decision 4, 6)

`gitleaks` (git history) — vì được nêu đích danh trong Jira ticket gốc, ưu tiên làm baseline thay vì
TruffleHog. `Trivy` (image filesystem) — tái dùng công cụ đã được `ADR-0012` Action Item 4 định hướng
sẵn cho image scanning (CVE), tránh thêm 1 binary CI mới chỉ để scan secret.

**Phát hiện thật khi implement (T016, research.md Decision 6)**: ruleset MẶC ĐỊNH của gitleaks
KHÔNG có rule khớp `Password=<chuỗi entropy thấp>` kiểu connection string ADO.NET — nó tối ưu cho
API key/token entropy cao. Chạy thử với ruleset mặc định trả về "no leaks found" **kể cả trước khi**
dọn sạch mật khẩu dev cũ khỏi working tree — nghĩa là SC-001 ("0 findings") sẽ "đạt" 1 cách giả tạo
nếu không sửa. Đã thêm rule tuỳ biến `connection-string-password` vào `.gitleaks.toml` để gate có tác
dụng thật.

### 2.5. Baseline, không phải allowlist (research.md Decision 6)

Sau khi rule tuỳ biến có tác dụng, quét toàn bộ lịch sử (`--log-opts="--all"`, đúng FR-004) tất yếu
phát hiện lại chính 9 commit lịch sử từng hardcode mật khẩu dev — chúng vẫn tồn tại nguyên vẹn trong
lịch sử git dù working tree đã sạch (viết lại lịch sử git bị coi là hành động rủi ro cao, ngoài phạm
vi, per spec.md Assumptions). Giải pháp: `.gitleaks-baseline.json` chốt đúng 9 fingerprint (commit SHA
+ file + dòng) đã biết, review 1 lần. Khác biệt cốt lõi với allowlist bằng regex: baseline chỉ loại
trừ ĐÚNG 9 vị trí cụ thể đó — 1 commit tương lai vô tình dùng lại y hệt giá trị cũ ở 1 dòng KHÁC vẫn
bị chặn; allowlist bằng regex sẽ làm suy yếu rule cho mọi phát hiện thật kế tiếp.

## 3. Bảng quyết định — thiếu secret thì service làm gì

| Tình huống | Kết quả |
|---|---|
| Secret có, đúng định dạng (chứa credential thật) | `ValidateOptionsResult.Success` — `app.Run()` chạy bình thường |
| Secret thiếu hoàn toàn (biến môi trường không tồn tại) | `ValidateOptionsResult.Fail(...)` — host ném exception, `app.Run()` KHÔNG BAO GIỜ được gọi |
| Connection string có giá trị nhưng không có credential (chỉ host/database, đúng base `appsettings.json`) | Coi như THIẾU (nhờ `HasCredential`) — cùng nhánh fail ở trên, không phải nhánh "coi là hợp lệ" |
| Secret rotate trong Vault, không redeploy | Pod đang chạy vẫn dùng giá trị cũ tới lần đọc file tiếp theo (mount volume) — không tự động hot-reload vào tiến trình .NET đang chạy, chỉ ESO đồng bộ lại `Secret` object |

## 4. Xác thực end-to-end thật trên cluster K8s thử nghiệm (research.md Decision 7, T032/T034)

Đây là bằng chứng mạnh nhất trong 4 tính năng của đợt này — không phải mô phỏng:

1. Cài Vault (dev-mode) + External Secrets Operator qua Helm vào cluster Docker Desktop Kubernetes.
2. Tạo `ClusterSecretStore` trỏ vào Vault, seed `secret/orders` → `connectionstring`.
3. **Phát hiện thật lúc apply**: `deploy/k8s/orders/external-secret.yaml` dùng
   `apiVersion: external-secrets.io/v1beta1` (contract ban đầu) → API server trả lỗi "no matches for
   kind ClusterSecretStore in version v1beta1" — CRD của ESO ở phiên bản chart hiện tại chỉ **serve**
   `v1`, dù schema vẫn định nghĩa cả hai cho tương thích. **Đã sửa** `apiVersion` sang `v1` ở cả 5
   manifest + contract document — sửa tại nguồn, không chỉ bản test cục bộ.
4. Apply lại file đã sửa → `ExternalSecret` báo `SecretSynced`/`Ready=True`.
5. Đọc `Secret` K8s tạo ra: `ConnectionStrings__OrdersDb` khớp **chính xác** giá trị đã seed trong
   Vault.
6. Chạy 1 pod thật với `envFrom.secretRef.name: orders-secrets` → biến môi trường xuất hiện đúng giá
   trị trong container.
7. Rotate secret trong Vault (`vault kv put`, version 2), ép ESO đồng bộ lại (annotation
   `force-sync`, KHÔNG `kubectl apply`/redeploy) → `Secret` K8s cập nhật giá trị mới ngay,
   `ExternalSecret` vẫn `SecretSynced` — xác nhận trực tiếp FR-003/SC-004.

**Giới hạn môi trường test đã gặp (không phải lỗi thiết kế)**: cluster Docker Desktop K8s 2-node
(`desktop-control-plane`/`desktop-worker`) không chia sẻ image store với `docker build` cục bộ —
image `orders-api:secret-scan` không pull được vào pod thật (`ErrImageNeverPull`). Thay bằng 1 pod
`busybox` public image + `envFrom` cùng `Secret` để xác thực trực tiếp biến môi trường đến đúng
container — kết hợp bằng chứng đã có (WebApplicationFactory integration test xác nhận .NET parse đúng
`ConnectionStrings__OrdersDb`; Trivy xác nhận image `orders-api` sạch), tổng thể vẫn là chứng minh
end-to-end đầy đủ, đáng tin cậy cho FR-002/FR-003/SC-003/SC-004.

## 5. Giới hạn phạm vi đã biết — trung thực, đây là phần quan trọng nhất

**Cluster K8s thử nghiệm ở mục 4 là TẠM THỜI, dùng để xác thực, không phải hạ tầng production.**
[ADR-0007 Amendment (2026-09-06)](../adr/0007-secrets-delivery.md) ghi rõ: 3 Action Item gốc của ADR
đó (deploy Vault HA thật, cài ESO vào cluster thật, định nghĩa dynamic-credential policy) **vẫn còn
nguyên `[ ]` chưa làm** — không có Vault, không có ESO nào đang chạy cho môi trường vận hành thật của
nền tảng này. Mã ứng dụng giờ phụ thuộc đúng hình dạng `Secret` object mà Action Item 2 mô tả, nhưng
chưa có gì THẬT SỰ tạo ra 1 `Secret` object đó ngoài `kubectl apply` thủ công vào cluster thử nghiệm
tạm thời nói trên.

## 6. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/018-cluster-secret-store-component.drawio`](../diagrams/018-cluster-secret-store-component.drawio)
- Sơ đồ luồng nghiệp vụ: [`docs/diagrams/018-cluster-secret-store-flow-nghiep-vu.drawio`](../diagrams/018-cluster-secret-store-flow-nghiep-vu.drawio)
- Sơ đồ trình tự: [`docs/diagrams/018-cluster-secret-store-sequence.drawio`](../diagrams/018-cluster-secret-store-sequence.drawio)

## 7. Tham khảo thêm

Chi tiết `deploy/k8s/`, so sánh với `deploy/ansible/` (spec `019`), và toàn văn trích ADR-0007 Amendment
đã có ở [11-trien-khai-k8s-va-secret-store.md](../onboarding/11-trien-khai-k8s-va-secret-store.md) —
không lặp lại ở đây, trừ chi tiết xác thực cluster thật ở mục 4 (chưa từng được ghi ở onboarding).
