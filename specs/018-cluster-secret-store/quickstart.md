# Quickstart: Xác thực tính năng "Secrets qua Cluster Secret Store"

**Feature**: [spec.md](./spec.md) | Tham chiếu: [data-model.md](./data-model.md), [contracts/](./contracts/)

Tài liệu này mô tả các kịch bản chạy được (runnable) để chứng minh feature hoạt động đúng end-to-end, ứng với từng User Story trong spec. Không lặp lại chi tiết hợp đồng — xem `contracts/` cho cấu trúc chính xác.

## Điều kiện tiên quyết

- .NET 10 SDK, Docker Desktop (đã dùng cho `docker-compose*.yml` hiện có của repo).
- gitleaks CLI và Trivy CLI cài trên máy chạy thử (hoặc Jenkins agent khi chạy qua pipeline thật).
- (Cho kịch bản 3) một cluster K8s tối thiểu cục bộ (kind hoặc k3d) + External Secrets Operator cài qua Helm + một Vault dev-mode tạm thời (`vault server -dev`) — đây KHÔNG phải Vault production của ADR-0007, chỉ đủ để xác nhận manifest hoạt động đúng cấu trúc (xem giới hạn tại `contracts/external-secret-manifest-contract.md`).

## Kịch bản 1 — Xác thực User Story 1: không còn secret hardcode (P1)

```bash
# Quét toàn bộ lịch sử git — kỳ vọng: 0 phát hiện MỚI (SC-001; baseline loại trừ 9 phát hiện lịch
# sử đã biết từ trước feature này — research.md Decision 6, .gitleaks-baseline.json)
scripts/ci/run-secret-scan.sh
# tương đương chạy trực tiếp:
gitleaks detect --source . --config .gitleaks.toml --baseline-path .gitleaks-baseline.json --log-opts="--all" --redact

# Kiểm tra thủ công không còn credential trong file appsettings đã commit
git grep -nE "Password=[^<$]|ApiKey|Secret\"?\s*:" -- "services/**/appsettings*.json"
```

**Kết quả mong đợi**: `scripts/ci/run-secret-scan.sh` thoát mã 0, log `no leaks found` (đã xác thực thực nghiệm khi implement — xem tasks.md T016/T018); lệnh `git grep` không còn khớp giá trị credential thật nào (chỉ còn placeholder dạng `Password=<...>` trong hướng dẫn `dotnet user-secrets`, bị pattern trên loại trừ có chủ đích bằng `[^<$]`).

## Kịch bản 2 — Xác thực User Story 2: fail-fast khi thiếu secret & image sạch (P1)

```bash
# 2a. Fail-fast: chạy một service mà KHÔNG set biến môi trường bắt buộc
docker compose -f docker-compose.deps.yml up -d orders-db
dotnet run --project services/orders/src/Orders.Api --no-launch-profile
# (không set ConnectionStrings__Default__Password)
```

**Kết quả mong đợi**: tiến trình dừng ngay lập tức với thông điệp lỗi có cấu trúc nêu rõ `RequiredSecret.Name` còn thiếu (vd: `ConnectionStrings__Default`), không phải một exception mơ hồ khi gọi database lần đầu (FR-007).

```bash
# 2b. Image sạch: build image rồi quét filesystem
docker build -t orders-api:local -f services/orders/src/Orders.Api/Dockerfile .
trivy image --scanners secret orders-api:local
```

**Kết quả mong đợi**: Trivy báo `0` secret tìm thấy (SC-002).

```bash
# 2c. (Nếu có cluster thử nghiệm theo điều kiện tiên quyết) triển khai với secret injected
kubectl apply -f deploy/k8s/orders/external-secret.yaml
kubectl rollout status deployment/orders-api
```

**Kết quả mong đợi**: pod khởi động thành công, biến môi trường trong container đến từ K8s `Secret` (`kubectl exec ... -- env | grep ConnectionStrings`) chứ không phải từ image hay file đã commit (SC-003).

## Kịch bản 3 — Xác thực User Story 3: rotate secret không cần redeploy (P2)

```bash
# Trong cluster thử nghiệm đã có ở Kịch bản 2c:
vault kv put secret/orders connectionstring="Server=orders-db;...;Password=NewRotatedValue!"

# Không redeploy — chỉ chờ ESO đồng bộ theo refreshInterval (tối đa 5 phút, xem SC-004)
kubectl get externalsecret orders-secrets -w
```

**Kết quả mong đợi**: trong vòng tối đa 5 phút, `kubectl get secret orders-secrets -o jsonpath='{.data.ConnectionStrings__Default}' | base64 -d` phản ánh giá trị mới; pod đang chạy không bị restart do triển khai lại (chỉ restart nếu chính service tự chọn reload-on-change, tuỳ quyết định implement ở tasks.md), và không có request nào tới `orders-api` thất bại do gián đoạn trong quá trình rotate (SC-004).

## Kịch bản 4 — Xác thực CI gate (FR-006, SC-005)

Theo đúng phương pháp ADR-0012 đã dùng để validate 5 check gốc:

1. Mở một PR "sạch" (không chứa secret) → xác nhận `ci/secret-scan` và `ci/image-secret-scan` đều hiện `Success` trên PR.
2. Mở một PR thử cố tình chèn một chuỗi trông giống API key vào một file bất kỳ → xác nhận cả hai check hiện `Failure` với lý do cụ thể (rule đã khớp, vị trí phát hiện), nút merge của PR bị khoá, và **đóng/không merge PR thử này**.

Xem `contracts/ci-secret-scan-stage-contract.md` cho tên check chính xác và quy tắc fail-closed.
