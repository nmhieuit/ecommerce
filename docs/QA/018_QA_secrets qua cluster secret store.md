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

### Thủ công — đặt công tắc `.env` để "làm mất" secret rồi bấm Postman

Dựng stack: `docker compose -f docker-compose.local.yml up -d --wait products-api baskets-api orders-api parties-api identity-api` (kéo theo DB).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**, chạy folder
**`18 - Secrets bắt buộc (fail-fast khi thiếu)`** (5 request `health/ready`, không cần token).

**Công tắc** (mới, mẫu ở [`.env.example`](../../.env.example); mặc định không khai báo = chuỗi có `Password=${MSSQL_SA_PASSWORD}`): `PRODUCTS_DB_CONNECTION`, `BASKETS_DB_CONNECTION`, `ORDERS_DB_CONNECTION`, `PARTIES_DB_CONNECTION`, `IDENTITY_DB_CONNECTION`. Thêm dòng vào `.env`, rồi
`docker compose -f docker-compose.local.yml up -d --force-recreate <service>-api` (đợi ~40 giây). **Không đổi `MSSQL_SA_PASSWORD`** (làm hỏng chính container SQL Server).

| Bước | Cấu hình cần chỉnh (`.env`) | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-27)** |
|---|---|---|---|---|
| BẬT — cả 5 service khởi động với secret hợp lệ | (không khai báo công tắc) | `18` bước 01 → 05 | `health/ready` `200` cả 5 | Đúng, **5/5 xanh** |
| Vì sao mặc định luôn health dù `.env` không có dòng `*_DB_CONNECTION` nào *(ngoại lệ: đọc cấu hình, không có request)* | `grep DB_CONNECTION docker-compose.local.yml docker-compose.yml` | (không có) | Không phải bug | Cả 2 file dùng cú pháp `${ORDERS_DB_CONNECTION:-Server=orders-db;...;Password=${MSSQL_SA_PASSWORD};...}` (tương tự 4 service kia) — dấu `:-` khiến Compose dùng default **ngay trong file YAML** khi biến không được `.env` khai; default đó luôn có `Password=`. `.env` không cần chứa connection string nào để mọi thứ health — đó là baseline đúng thiết kế, không phải lỗi |
| **TẮT** secret của `orders` — service từ chối khởi động (FR-007, US2) | `ORDERS_DB_CONNECTION=Server=orders-db;Database=orders;TrustServerCertificate=true` | `18` bước 03 (lỗi kết nối) + bước còn lại | Service thoát, thông báo nêu đúng khoá thiếu; các service khác vẫn chạy | `orders-api` **exit 139**, log `OptionsValidationException: Missing required secret(s): ConnectionStrings:OrdersDb. Each must be supplied at runtime from the cluster secret store…`; gọi thẳng `:5041/health/live` → không kết nối được (`curl` mã `000`, container đã thoát nên không có gì lắng nghe cổng); qua gateway `GET /bff/orders/<id>` → `504` `ProblemDetails` (`downstream-timeout`, `OrdersApi`) — chưa từng "health/ready trả dữ liệu"; 4 service còn lại `200` |
| **TẮT** secret của `baskets` — lặp lại để xác nhận không riêng `orders` | `BASKETS_DB_CONNECTION=Server=baskets-db;Database=baskets;TrustServerCertificate=true` | (gọi `health/live` của baskets, hoặc qua gateway) | Cùng hành vi | `baskets-api` **exit 139**, cùng thông báo (`ConnectionStrings:BasketsDb`); gọi thẳng `:5188/health/live` → không kết nối được (`curl` mã `000`); `GET gateway /bff/basket` → `504` sau ~3 giây (timeout của BFF, không phải "ready trả dữ liệu") |
| **Đặt biến RỖNG (`BASKETS_DB_CONNECTION=`) — KHÔNG mô phỏng được thiếu secret (phát hiện lượt này)** | `BASKETS_DB_CONNECTION=` (dòng có `=` nhưng không có giá trị) | (không có) | Nhầm tưởng sẽ fail-fast giống dòng trên | **Không fail-fast**: `baskets-api` khởi động lại bình thường, `healthy` trong vài giây — Compose áp dụng default (`:-` coi biến rỗng như biến chưa khai), tức lại có `Password=${MSSQL_SA_PASSWORD}`. Phải dùng đúng cú pháp ở dòng trên (giá trị KHÔNG rỗng, KHÔNG có `Password=`/`Pwd=`/`Integrated Security=true`/`Trusted_Connection=true`) mới tạo ra lỗi thật |
| `Password=` rỗng | `BASKETS_DB_CONNECTION=Server=baskets-db;Database=baskets;User Id=sa;Password=;TrustServerCertificate=true` | (gọi `health/ready` của baskets) | Bị coi là thiếu → fail-fast | **Không fail-fast**: service khởi động, `health/live` `200`, `health/ready` `503` (`Login failed for user 'sa'`) — validator chỉ kiểm sự có mặt của `Password=`, không kiểm giá trị (xem QA_Debt) |
| `Integrated Security=true` (không Password) | `PARTIES_DB_CONNECTION=Server=parties-db;Database=parties;Integrated Security=true;TrustServerCertificate=true` | (gọi `health/ready` của parties) | Được chấp nhận (theo `HasCredential`) | Khởi động, `health/ready` `503` (`Cannot generate SSPI context`) — hợp lệ với validator nhưng vô nghĩa trên container Linux |
| Cả 5 service mất secret | Đặt cả 5 công tắc thành chuỗi host-only | `18` bước 01 → 05 | Cả 5 từ chối | Chỉ `identity-api` chạy và thoát (log nêu `ConnectionStrings:IdentityDb`); 4 service còn lại ở trạng thái `created` vì `depends_on: identity-api healthy` nên không có thông điệp riêng — 5/5 request lỗi kết nối |
| Khôi phục | Xoá các dòng công tắc khỏi `.env`, tạo lại 5 service | `18` bước 01 → 05 | `200` cả 5 | 5/5 xanh, `.env` về nguyên trạng |
| Kịch bản 1 — không hardcode trong `appsettings*.json` *(ngoại lệ: đọc file)* | (không có) — đọc 5 file `appsettings.Development.json` | (không có) | Chỉ còn placeholder | Đúng: chỉ còn hướng dẫn `dotnet user-secrets` |
| Kịch bản 1 — gitleaks toàn lịch sử (SC-001) *(ngoại lệ: công cụ quét)* | `docker run zricethezav/gitleaks detect --config .gitleaks.toml --baseline-path .gitleaks-baseline.json --log-opts="--all" --redact` (`MSYS_NO_PATHCONV=1`) | (không có) | 0 phát hiện mới | **38 phát hiện mới** trên 171 commit (36 ở lượt trước) ngoài baseline 9 mục — xem QA_Debt |
| Kịch bản 2b — image sạch (SC-002) *(ngoại lệ: công cụ quét)* | `docker run aquasec/trivy image --scanners secret ecomerce-local-orders-api` | (không có) | 0 secret | **0 secret** |
| `external-secret.yaml` *(ngoại lệ: đọc file)* | (không có) — đọc `apiVersion` của 5 file | (không có) | `external-secrets.io/v1` | Đúng |
| Kịch bản 2c/3 — K8s + Vault + ESO, rotate không redeploy (FR-003/SC-004) | (không dựng lại) | (không có) | — | Dùng bằng chứng của phiên triển khai gốc ở `technical-debt.md` mục 018 |

### Tự động — chạy thẳng bộ test đã có

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-007/US2 — validate thuần, 8 method (thành công/thiếu/trắng/thông báo an toàn/không secret nào/tên khoá/host-only bị coi thiếu/có credential) | [`RequiredSecretsValidationTests.cs:29`](../../shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs#L29) · [`:47`](../../shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs#L47) · [`:69`](../../shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs#L69) · [`:91`](../../shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs#L91) · [`:123`](../../shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs#L123) · [`:144`](../../shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs#L144) · [`:168`](../../shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs#L168) · [`:192`](../../shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs#L192) | `dotnet test shared/ServiceDefaults.UnitTests --filter FullyQualifiedName~RequiredSecretsValidationTests` |
| FR-007/US2 — host ASP.NET Core thật dừng khởi động khi thiếu credential | [`RequiredSecretsFailFastTests.cs:32`](../../services/orders/tests/Orders.Api.IntegrationTests/RequiredSecretsFailFastTests.cs#L32) — `HostFailsToStart_WhenOrdersDbConnectionStringHasNoCredential` | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter FullyQualifiedName~RequiredSecretsFailFastTests` |

**Kết quả lượt QA này (2026-09-27)**: `RequiredSecretsValidationTests` **13/13**, `RequiredSecretsFailFastTests` **1/1** — không đổi.

## Kết luận

**PASS kèm ghi chú nghiêm trọng.** 4 nguồn nhất quán; fail-fast (FR-007, US2) xác nhận ở unit (13/13), host thật (1/1) và trên container thật qua công tắc `.env` — đã rà soát lại và tái hiện đúng cho cả `orders` lẫn `baskets`: thiếu secret → service **exit 139** ngay khi khởi động (trước khi mở cổng HTTP), không phải "vẫn health nhưng trả lỗi" — gọi thẳng vào cổng của nó nhận `curl` mã `000` (không ai lắng nghe), gọi qua gateway nhận `504 ProblemDetails` đúng ngân sách timeout, không bao giờ có chuyện "health/ready trả dữ liệu" như khi thiếu secret thật.
Đồng thời làm rõ nghi vấn "vì sao `.env` không có `*_DB_CONNECTION` mà mọi thứ vẫn health": đó là baseline đúng thiết kế — cả 2 file compose dùng `${VAR:-default}` với default nhúng sẵn `Password=${MSSQL_SA_PASSWORD}` ngay trong YAML, không đọc từ `.env`; và phát hiện thêm 1 cái bẫy khi tự tay thử mô phỏng: **đặt biến thành chuỗi RỖNG (`BASKETS_DB_CONNECTION=`) không mô phỏng được gì** — cú pháp `:-` của Compose coi biến rỗng y hệt biến chưa khai nên vẫn rơi về default có mật khẩu; phải đặt 1 chuỗi KHÔNG RỖNG và không có `Password=`/`Pwd=`/`Integrated Security=true`/`Trusted_Connection=true` thì mới tạo ra lỗi thật (đã cập nhật `.env.example` để nêu rõ điều này).
`appsettings*.json` sạch credential, `external-secret.yaml` giữ đúng `apiVersion`, Trivy xác nhận image sạch (SC-002). Ghi chú: (1) **cổng `ci/secret-scan` sẽ ĐỎ ngay nếu chạy thật** — 38 phát hiện gitleaks mới không nằm trong baseline (chủ yếu false positive từ tài liệu/test trích dẫn mẫu credential + JWT test-fixture) chưa từng được review;
(2) `Password=` rỗng (bên trong chuỗi kết nối, khác với biến `.env` rỗng) và `Integrated Security=true` vượt qua fail-fast và chỉ lộ ra ở readiness `503`; (3) 1 service thất bại kéo theo dependents ở trạng thái `created`. Chi tiết: [QA_Debt.md](QA_Debt.md) mục 018.
