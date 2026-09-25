# QA: Secrets qua Cluster Secret Store, loại bỏ cấu hình hardcode

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. 5 service có database (Baskets/Orders/Parties/Products/Identity) gọi `builder.AddRequiredSecretsValidation(RequiredSecret.ConnectionString("<Tên>Db"))` —
   `IValidateOptions` + `.ValidateOnStart()`, chạy TRƯỚC `app.Run()`.
2. `RequiredSecret.ConnectionString(name).Resolve` gọi `HasCredential(...)`: coi là thiếu trừ khi chuỗi có `Password=`/`Pwd=`/`Integrated Security=true`/
   `Trusted_Connection=true` — không chỉ kiểm tra rỗng.
3. `appsettings.json` (Production) cố tình giữ connection string chỉ có host/database; `appsettings.Development.json` không còn credential, chỉ còn hướng dẫn `dotnet user-secrets`.
4. `deploy/k8s/<service>/external-secret.yaml` khai `secretKey` khớp dạng biến môi trường của `RequiredSecret.Name`.
5. CI thêm 2 stage `ci/secret-scan` (gitleaks, toàn lịch sử git) và `ci/image-secret-scan` (Trivy, filesystem image) — không bị `CI_FAST_ITERATION` bỏ qua.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Tự động — chạy thẳng bộ test đã có

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-007/US2 — validate thuần, 8 method (thành công/thiếu/trắng/thông báo an toàn/không secret nào/tên khoá/host-only bị coi thiếu/có credential) | [`RequiredSecretsValidationTests.cs:29`](../../shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs#L29) · [`:47`](../../shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs#L47) · [`:69`](../../shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs#L69) · [`:91`](../../shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs#L91) · [`:123`](../../shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs#L123) · [`:144`](../../shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs#L144) · [`:168`](../../shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs#L168) · [`:192`](../../shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs#L192) | `dotnet test shared/ServiceDefaults.UnitTests --filter FullyQualifiedName~RequiredSecretsValidationTests` |
| FR-007/US2 — host ASP.NET Core thật dừng khởi động khi thiếu credential | [`RequiredSecretsFailFastTests.cs:32`](../../services/orders/tests/Orders.Api.IntegrationTests/RequiredSecretsFailFastTests.cs#L32) — `HostFailsToStart_WhenOrdersDbConnectionStringHasNoCredential` | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter FullyQualifiedName~RequiredSecretsFailFastTests` |

**Kết quả lượt QA này (2026-09-24)**: `RequiredSecretsValidationTests` **13/13**, `RequiredSecretsFailFastTests` **1/1** — không đổi sau khi dịch comment.

### Thủ công — chạy đúng như spec mô tả

| Bước (quickstart) | Cách làm | Kỳ vọng theo tài liệu | **Đã quan sát** |
|---|---|---|---|
| Kịch bản 1 — không hardcode trong `appsettings*.json` | Đọc 5 file `appsettings.Development.json` | Chỉ còn placeholder `Password=<…>` | Đúng: chỉ còn hướng dẫn `dotnet user-secrets` |
| Kịch bản 1 — gitleaks toàn lịch sử (SC-001) | `docker run zricethezav/gitleaks detect --config .gitleaks.toml --baseline-path .gitleaks-baseline.json --log-opts="--all" --redact` | 0 phát hiện mới | **36 phát hiện mới (34 fingerprint)** ngoài baseline 9 mục — xem QA_Debt |
| Kịch bản 2b — image sạch (SC-002) | `docker run aquasec/trivy image --scanners secret ecomerce-local-orders-api` | 0 secret | **0 secret** |
| `external-secret.yaml` | Đọc `apiVersion` của 5 file | `external-secrets.io/v1` (bản vá `v1beta1` → `v1`) | Đúng, còn nguyên trên `master` |
| Kịch bản 2c/3 — K8s + Vault + ESO, rotate không redeploy (FR-003/SC-004) | (không dựng lại) | — | Dùng bằng chứng của phiên triển khai gốc ở `technical-debt.md` mục 018 (kind + Vault dev + ESO: `SecretSynced`, rotate cập nhật ngay) |

Máy QA không cài sẵn `gitleaks`/`trivy` nên chạy qua Docker (`MSYS_NO_PATHCONV=1` khi dùng Git Bash).

## Kết luận

**PASS kèm ghi chú nghiêm trọng.** 4 nguồn nhất quán; fail-fast (FR-007, US2) xác nhận ở cả unit (13/13) và host thật (1/1), `appsettings*.json` sạch credential, `external-secret.yaml`
giữ đúng `apiVersion`, Trivy xác nhận image sạch (SC-002). Ghi chú: (1) **cổng `ci/secret-scan` sẽ ĐỎ ngay nếu chạy thật** — 36 phát hiện gitleaks mới không nằm trong baseline (chủ yếu false
positive từ tài liệu/test trích dẫn mẫu credential + 7 JWT test-fixture trong `pacts/`) chưa từng được review; cộng với CI đã ngừng (QA 013) thành "khoá kép". Chi tiết:
[QA_Debt.md](QA_Debt.md) mục 018.
