# Kiến trúc: Secrets qua Cluster Secret Store, loại bỏ cấu hình hardcode

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-27 ("[SECURE-3] Secrets via cluster secret store, remove hardcoded config"),
đặc tả tại [`specs/018-cluster-secret-store/`](../../specs/018-cluster-secret-store/). Bảy quyết định
kiến trúc (2 phát sinh trong lúc implement/xác thực): [`research.md`](../../specs/018-cluster-secret-store/research.md).
Hiện thực hoá nửa "application-side" của [ADR-0007](../adr/0007-secrets-delivery.md) (chọn Vault +
External Secrets Operator) — ADR đó đã được cập nhật 1 mục "Amendment" ghi rõ ranh giới đã làm/chưa
làm, xem [technical-debt.md](technical-debt.md).

**Trạng thái xác minh**: 37 task `[X]` trong `tasks.md` — nhiều nhất trong 4 tính năng của đợt này.
Có 1 lượt xác thực end-to-end **trên cluster K8s thật** (tạm thời, không phải hạ tầng production) —
bằng chứng mạnh nhất trong cả 4 tài liệu Architect mới này, xem [technical-debt.md](technical-debt.md).

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
          ▼ (đã xác thực end-to-end trên cluster thử nghiệm)
       External Secrets Operator + HashiCorp Vault — CHƯA tồn tại cho môi trường production thật
```

Song song, độc lập với runtime: `ci/secret-scan` (gitleaks, toàn bộ lịch sử git) +
`ci/image-secret-scan` (Trivy, filesystem image đã build) — 2 stage CI luôn chạy, không bị
`CI_FAST_ITERATION` bỏ qua (xem [07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md](../onboarding/07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md)).

## 2. Mô tả từng thành phần

### 2.1. `RequiredSecretsValidation.cs` — không chỉ check rỗng

`appsettings.json` (Production, không phải `.Development`) **cố tình vẫn giữ** 1 connection string
hợp lệ chỉ có host/database, không credential (đúng `contracts/service-configuration-contract.md`
rule 2, để service biết kết nối đâu ngay cả khi chưa có secret). Fail-fast thực thi bằng
`HasCredential(...)`: resolve về `null` (coi là thiếu) trừ khi chuỗi thật sự có `Password=`/`Pwd=`/
`Integrated Security=true`/`Trusted_Connection=true` (chi tiết phát hiện dẫn tới quyết định này — xem
[technical-debt.md](technical-debt.md)).

### 2.2. Cách service đọc secret tại runtime (research.md Decision 2)

Dùng cơ chế cấu hình chuẩn của .NET — biến môi trường ánh xạ từ K8s `Secret` (đồng bộ bởi ESO từ
Vault) qua `envFrom.secretRef`. **Không cần SDK/thư viện client Vault trong mã ứng dụng** — `IConfiguration`
tự nạp biến môi trường, không thêm dependency mới. Khớp đúng lý do ESO được chọn ở ADR-0007: "pods
don't need Vault client/sidecar integration".

### 2.3. `deploy/k8s/<service>/external-secret.yaml` — hợp đồng, không phải cấu hình sống

Đã giải thích chi tiết ở [11-trien-khai-k8s-va-secret-store.md](../onboarding/11-trien-khai-k8s-va-secret-store.md)
mục 1.3 — không lặp lại. Naming contract (`secretKey` PHẢI khớp `RequiredSecret.Name` dạng biến môi
trường) đã được xác nhận thật trên cluster, không chỉ lý thuyết.

### 2.4. Công cụ secret-scanning (research.md Decision 4, 6)

`gitleaks` (git history) — vì được nêu đích danh trong Jira ticket gốc, ưu tiên làm baseline thay vì
TruffleHog. `Trivy` (image filesystem) — tái dùng công cụ đã được `ADR-0012` Action Item 4 định hướng
sẵn cho image scanning (CVE), tránh thêm 1 binary CI mới chỉ để scan secret. Ruleset tuỳ biến cần
thiết để rule có tác dụng thật với connection string — xem [technical-debt.md](technical-debt.md).

### 2.5. Baseline, không phải allowlist (research.md Decision 6)

Quét toàn bộ lịch sử (`--log-opts="--all"`, đúng FR-004) phát hiện lại 9 commit lịch sử từng hardcode
mật khẩu dev — chúng vẫn tồn tại nguyên vẹn trong lịch sử git dù working tree đã sạch (viết lại lịch
sử git bị coi là hành động rủi ro cao, ngoài phạm vi). Giải pháp: `.gitleaks-baseline.json` chốt đúng
9 fingerprint (commit SHA + file + dòng) đã biết, review 1 lần. Khác biệt cốt lõi với allowlist bằng
regex: baseline chỉ loại trừ ĐÚNG 9 vị trí cụ thể đó — 1 commit tương lai vô tình dùng lại y hệt giá
trị cũ ở 1 dòng KHÁC vẫn bị chặn.

## 3. Bảng quyết định — thiếu secret thì service làm gì

| Tình huống | Kết quả |
|---|---|
| Secret có, đúng định dạng (chứa credential thật) | `ValidateOptionsResult.Success` — `app.Run()` chạy bình thường |
| Secret thiếu hoàn toàn (biến môi trường không tồn tại) | `ValidateOptionsResult.Fail(...)` — host ném exception, `app.Run()` KHÔNG BAO GIỜ được gọi |
| Connection string có giá trị nhưng không có credential (chỉ host/database, đúng base `appsettings.json`) | Coi như THIẾU (nhờ `HasCredential`) — cùng nhánh fail ở trên, không phải nhánh "coi là hợp lệ" |
| Secret rotate trong Vault, không redeploy | Pod đang chạy vẫn dùng giá trị cũ tới lần đọc file tiếp theo (mount volume) — không tự động hot-reload vào tiến trình .NET đang chạy, chỉ ESO đồng bộ lại `Secret` object |

## 4. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/018-cluster-secret-store-component.drawio`](../diagrams/018-cluster-secret-store-component.drawio)
- Sơ đồ luồng nghiệp vụ: [`docs/diagrams/018-cluster-secret-store-flow-nghiep-vu.drawio`](../diagrams/018-cluster-secret-store-flow-nghiep-vu.drawio)
- Sơ đồ trình tự: [`docs/diagrams/018-cluster-secret-store-sequence.drawio`](../diagrams/018-cluster-secret-store-sequence.drawio)

## 5. Tham khảo thêm

Chi tiết `deploy/k8s/`, so sánh với `deploy/ansible/` (spec `019`), và toàn văn trích ADR-0007 Amendment
đã có ở [11-trien-khai-k8s-va-secret-store.md](../onboarding/11-trien-khai-k8s-va-secret-store.md).

**Giới hạn phạm vi đã biết — phần quan trọng nhất của tài liệu gốc**: cluster K8s dùng để xác thực
mục 4 (ở technical-debt.md) là TẠM THỜI, không phải hạ tầng production — chi tiết đầy đủ xem
[technical-debt.md](technical-debt.md).
