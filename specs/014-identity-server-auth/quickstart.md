# Quickstart: Kiểm chứng máy chủ định danh thay thế xác thực giả lập

Kiểm chứng tính năng này đầu-cuối theo các acceptance scenario của `spec.md` và test scenario của vé Jira SCRUM-23. Xem [data-model.md](data-model.md) cho trạng thái ba-giá-trị của Token, và [contracts/identity-token-claims-contract.md](contracts/identity-token-claims-contract.md) / [contracts/service-authentication-contract.md](contracts/service-authentication-contract.md) cho các hợp đồng đầy đủ.

## Prerequisites

- .NET 10 SDK đã cài.
- Docker sẵn sàng (service `identity` mới cần container SQL Server riêng, giống các domain service khác — xem `docker-compose.deps.yml`).
- [003-stub-identity-tenant-context](../003-stub-identity-tenant-context/) đã triển khai và đang chạy: gateway, BFF, và cả 4 domain service, với cơ chế lan truyền tenant/subject header hoạt động.
- Toggle Unleash `identity-server-auth-cutover` (research.md Decision 7) đã bật trong môi trường kiểm thử.

## Setup

1. Khởi động database dependency của service `identity` và chạy service này cùng 4 domain service, BFF, và gateway hiện có, theo đúng các bước Setup của [003-stub-identity-tenant-context/quickstart.md](../003-stub-identity-tenant-context/quickstart.md), cộng thêm `identity-api`/`identity-db`.
2. Đăng ký sẵn một `Client Application` (data-model.md) cho ứng dụng web SPA, và ít nhất một `Identity User` thử nghiệm đã gán `TenantId` hợp lệ (ví dụ trùng với tenant Phase 1 đã dùng trong `StubIdentity:TenantId`, để so sánh hành vi trước/sau).

## Validation Scenarios

### Scenario 1 — Đăng nhập phát hành token hợp lệ kèm claim tenant (spec Test Scenario 1, US1)

```bash
curl -i -X POST http://localhost:<identity-port>/connect/token \
  -d grant_type=authorization_code -d code=<auth-code-from-login> -d client_id=<client-id>
```

**Expected**: `200 OK`, phản hồi chứa một `access_token` (JWT). Giải mã token (ví dụ bằng `jwt.io` cục bộ hoặc `dotnet user-jwts`/thư viện JWT bất kỳ) cho thấy claim `sub` và `tenant_id` đều có mặt và không rỗng (data-model.md — Token, trạng thái Valid).

### Scenario 2 — Request hợp lệ đi qua toàn bộ đường đi, tenant vẫn lan truyền đúng như trước (US1 Acceptance Scenario 2)

```bash
curl -i http://localhost:<gateway-port>/bff/products -H "Authorization: Bearer <access_token>"
```

**Expected**: `200 OK`, và log có cấu trúc ở gateway/BFF/`Products.Api` cho thấy cùng một `TenantId` ở mọi hop (giống hệt [003-stub-identity-tenant-context/quickstart.md](../003-stub-identity-tenant-context/quickstart.md) Scenario 1) — chỉ khác nguồn của giá trị đó giờ là claim `tenant_id` trong token thật, không còn là giá trị gán cứng.

### Scenario 3 — Token giả mạo gửi thẳng tới một domain service, bỏ qua gateway, bị chặn độc lập (spec Test Scenario 2, US2)

```bash
curl -i http://localhost:<products-port>/products -H "Authorization: Bearer <token-with-tampered-signature>"
```

**Expected**: `401 Unauthorized` từ chính `Products.Api` — không phụ thuộc gateway đã chặn từ trước, vì request này chưa từng đi qua gateway (spec US2 Acceptance Scenario 3; `contracts/identity-token-claims-contract.md` Failure Modes).

### Scenario 4 — Token hết hạn bị từ chối rõ ràng, không âm thầm (spec Test Scenario 3, US3)

```bash
curl -i http://localhost:<gateway-port>/bff/products -H "Authorization: Bearer <expired-token>"
```

**Expected**: `401 Unauthorized` với một thông điệp lỗi rõ ràng ("unauthorized"/"token expired"), không phải một lỗi chung chung hay một phản hồi im lặng khác thường (spec US3 Acceptance Scenario 2).

### Scenario 5 — Request không có token bị chặn ở mọi endpoint nghiệp vụ, trừ health probe (spec FR-011)

```bash
curl -i http://localhost:<products-port>/products
curl -i http://localhost:<products-port>/health/live
```

**Expected**: Lệnh đầu tiên trả `401 Unauthorized` (`FallbackPolicy = RequireAuthenticatedUser()` — `contracts/service-authentication-contract.md`); lệnh thứ hai trả `200 OK` như bình thường (`[AllowAnonymous]` tường minh trên health probe).

### Scenario 6 — Chỉ nguồn xác định tenant thay đổi, cơ chế lan truyền giữ nguyên (spec FR-008, SC-004)

Kiểm tra bằng code review, không phải runtime call: xác nhận `TenantHeaderPropagationMiddleware`, `SubjectHeaderPropagationMiddleware`, và toàn bộ `shared/Tenancy` không có commit nào sửa đổi trong tính năng này ngoài việc gateway đổi đúng một dòng đăng ký scheme (`AddScheme<StubIdentity...>` → `AddJwtBearer(...)`, bọc trong toggle — research.md Decision 2/7). Đây là điều `research.md` Decision 3 dự đoán trước khi triển khai, không phải một khẳng định suông sau khi code đã viết xong.

### Scenario 7 — Rollback không cần redeploy (constitution Principle X)

```bash
# Gạt toggle Unleash `identity-server-auth-cutover` về OFF, không redeploy
curl -i http://localhost:<gateway-port>/bff/products
```

**Expected**: Gateway quay lại xác thực bằng `StubIdentityAuthenticationHandler` (Phase 1 stub), request vẫn thành công như trước tính năng này — xác nhận rollback tức thời khả dụng (research.md Decision 7).

## Automated Coverage

Các scenario trên là phần thủ công/khám phá bổ sung cho bộ test tự động mà tính năng này thêm vào: `Identity.Api.IntegrationTests` (Testcontainers SQL Server thật cho service `identity`), `Identity.UnitTests` cho thư viện chia sẻ mới, integration test mở rộng ở gateway/BFF/4 domain service xác nhận từ chối token giả mạo/hết hạn/vắng mặt, và `tests/CrossServiceIsolation.Tests/AuthenticatedByDefaultScannerTests.cs` xác nhận mọi service đã đăng ký `AddIdentityValidation()` — tất cả đều nằm trong PR gate cùng bộ test hiện có từ [003-stub-identity-tenant-context](../003-stub-identity-tenant-context/).
