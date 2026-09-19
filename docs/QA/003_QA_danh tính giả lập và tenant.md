# QA: Danh tính giả lập với tenant context đã xác định

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

**Nguồn đối chiếu**:
[`architecture/003`](../architecture/003_Architect_danh%20tính%20giả%20lập%20và%20tenant.md) ·
[`development/003`](../development/003_Development_danh%20tính%20giả%20lập%20và%20tenant.md) ·
[`summary/003`](../summary/003_PO_danh%20tính%20giả%20lập%20và%20tenant.md) ·
[`spec-summary-vi/003`](../spec-summary-vi/003-stub-identity-tenant-context.json) — đối chiếu chéo cả 4,
kèm xác minh lại với source code thật khi có nghi vấn.

## Luồng happy-case đã rà soát (US1 → US2)

1. Gateway xác định tenant đúng 1 lần (danh tính giả lập), ghi đè `X-Tenant-Id` (không tin giá trị
   client tự gửi), BFF relay nguyên vẹn xuống service; `TenantId` hiện trong log scope ở từng chặng.
2. Truy cập persistence mà chưa có tenant thì thất bại ngay (`MissingTenantContextException`, trước khi
   mở kết nối SQL) — không có tenant mặc định.
3. Thay nguồn phân giải tenant (stub → JWT) chỉ chạm bước phân giải ở gateway.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

Dựa trên [`specs/003-stub-identity-tenant-context/quickstart.md`](../../specs/003-stub-identity-tenant-context/quickstart.md),
nhưng quickstart gốc **không còn chạy đúng như viết** trên code hiện tại (xem [QA_Debt.md](QA_Debt.md)):
từ spec 014/015, gateway local đã cutover sang JWT thật và mọi domain service trả `401` trước khi tới
cổng tenant. Vì vậy phần thủ công dưới đây chỉ gồm những gì đã kiểm chứng được; phần còn lại kiểm bằng
test tự động (chạy được ngay khi Docker Desktop bật).

> Nếu `localhost` không gọi được dù container `healthy`: khởi động lại hẳn Docker Desktop (lỗi forwarding
> IPv6 loopback `::1` của WSL2), không phải lỗi ứng dụng.

### Thủ công — dựng stack và xác nhận hành vi thực tế

```bash
cp .env.example .env   # chỉ cần 1 lần
docker compose -f docker-compose.local.yml up -d --wait products-api
```

Lần đầu (image/SQL Server chưa có cache) có thể mất nhiều phút; chỉ dựng đúng service cần test để nhẹ máy.

| Bước | Cách làm | Kết quả đã quan sát |
|---|---|---|
| Gọi thẳng service, không token, không tenant | Postman folder `Product` → "Thiếu header tenant thì không phục vụ catalog" (hoặc `curl http://localhost:5088/products`) | Quickstart Scenario 3 kỳ vọng `500`; thực tế **`401`** `{"error":"unauthorized"...}` — deny-by-default (014/015) chặn trước, không chạm tới cổng tenant |
| Gọi thẳng, có `X-Tenant-Id` nhưng không token | `curl -H "X-Tenant-Id: contoso" http://localhost:5088/products` | Vẫn `401` — header tenant không thay được token |
| Có token hợp lệ, không có `X-Tenant-Id` | Lấy token (Postman folder "00 - Xác thực & phân quyền" → "01", hoặc `curl` tới `localhost:5205/connect/token` kèm `Host: identity-api:8080`) rồi gọi `curl -H "Authorization: Bearer <token>" http://localhost:5088/products` | **`500`** (cổng tenant, `MissingTenantContextException`) — đúng Scenario 3. *Cập nhật lần 2 (nhánh `claude/spa-identity-server-login-e6100d`): lấy token nay thành công (200); ở lượt đầu không lấy được do identity chưa seed.* Qua gateway có token: `GET /bff/products` → `200` |
| Dọn dẹp | `docker compose -f docker-compose.local.yml down -v` | |

### Thủ công — SC-004 (đổi nguồn phân giải chỉ chạm 1 bước)

Review code: dưới `StubIdentityAuthenticationHandler` (nay được bọc bởi `AddToggleGatedIdentity`, có
JwtBearer thay thế qua toggle `FeatureToggles:IdentityServerAuthCutover`), `TenantHeaderPropagationMiddleware`,
`Tenancy/TenantContextMiddleware`, `TenantPropagationHandler` của BFF chỉ đọc claim/header `tenant_id`,
không tham chiếu handler stub. Kịch bản rollback về stub: `specs/014-identity-server-auth/quickstart.md`
Scenario 7 (chưa kiểm chứng trong lượt QA này).

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

Test có hậu tố `IntegrationTests` dùng Testcontainers → cần Docker Desktop đang chạy.

| Cần xác nhận | Lệnh | Kết quả lượt QA này |
|---|---|---|
| US1/FR-001,002 — gateway ghi đè `X-Tenant-Id`, tenant tới BFF | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter TenantPropagationTests` | 4/4 PASS |
| US1/FR-002 — BFF relay tenant xuống downstream | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter TenantPropagationTests` | 2/2 PASS |
| FR-003/004/005, US2/SC-002 — persistence không tenant thì fail (đổi `products` thành `baskets`/`orders`/`parties` để lặp lại) | `dotnet test services/products/tests/Products.Api.IntegrationTests --filter TenantEnforcementTests` | products 2/2 PASS; 3 service còn lại chưa chạy |
| `TenantContext` + middleware dùng chung | `dotnet test shared/Tenancy.UnitTests` | 28/28 PASS |
| Danh tính giả lập ở gateway | `dotnet test services/gateway/tests/Gateway.Api.UnitTests --filter StubIdentityAuthenticationHandlerTests` | 8/8 PASS |
| **SC-003** — mọi `AddDbContext` đều gated theo tenant | `dotnet test tests/CrossServiceIsolation.Tests --filter TenantGatedConnectionTests` | **6/7 — FAIL** `EveryDbContextRegistration_IsGatedOnAResolvedTenant`: `orders` kỳ vọng 1 call site gated, thực tế 0 |

## Kết luận

**PASS kèm ghi chú** — 4 nguồn mô tả cùng 1 luồng happy-case (resolve 1 lần ở gateway → relay → gate
trước persistence) và các test hành vi (propagation, enforcement, unit) đều PASS. Nhưng **SC-003 đang
FAIL trên `master`** (test tự động của chính spec này đỏ do spec 024 chuyển cổng tenant của Orders), và
quickstart thủ công chỉ tái hiện được khi có token (không token → `401`, có token nhưng không `X-Tenant-Id` → `500`) — xem [QA_Debt.md](QA_Debt.md).
