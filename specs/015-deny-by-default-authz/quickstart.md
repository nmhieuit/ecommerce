# Quickstart: Kiểm chứng phân quyền từ chối theo mặc định

Kiểm chứng tính năng này đầu-cuối theo các acceptance scenario của `spec.md` và test scenario của vé Jira SCRUM-24. Xem [data-model.md](data-model.md) cho hình dạng Chính sách phân quyền/Endpoint Authorization Declaration, và [contracts/authorization-policy-contract.md](contracts/authorization-policy-contract.md) / [contracts/client-server-validation-parity-contract.md](contracts/client-server-validation-parity-contract.md) cho các hợp đồng đầy đủ.

## Prerequisites

- .NET 10 SDK đã cài.
- [014-identity-server-auth](../014-identity-server-auth/) đã triển khai và đang chạy: gateway, BFF, 4 domain service, và service `identity` đều đã tự xác thực token độc lập.
- Toggle Unleash mới của tính năng này (research.md Decision 5 — ví dụ `authz-require-api-scope`) đã bật trong môi trường kiểm thử.
- Một token hợp lệ, đầy đủ scope `ecommerce-api` (lấy qua client `integration-test-ropc` như 014 đã thiết lập), và (cho Scenario 2) khả năng lấy/tạo một token hợp lệ nhưng KHÔNG mang scope đó (ví dụ token chỉ xin `openid profile`, không xin `ecommerce-api`, qua cùng client với tham số `scope` khác).

## Validation Scenarios

### Scenario 1 — Mọi endpoint/handler có chính sách phân quyền tường minh (spec US1, Test Scenario 1)

```bash
dotnet test tests/CrossServiceIsolation.Tests --filter AuthorizationPolicyDeclaredScannerTests
```

**Expected**: Toàn bộ test pass — mọi `Map(Get|Post|Put|Delete|Patch)` trong `bff`, `baskets`, `orders`, `parties`, `products` có đúng một trong `.RequireAuthorization(...)`/`.AllowAnonymous()`; quét `IConsumer<` trên toàn `services/` không tìm thấy handler nào thiếu khai báo nguồn tin cậy (rỗng hôm nay — research.md Decision 4).

### Scenario 2 — Token hợp lệ nhưng thiếu claim `scope=ecommerce-api` bị từ chối 403 (spec US1, Test Scenario 2)

```bash
curl -i http://localhost:<baskets-port>/baskets/current -H "Authorization: Bearer <token-without-ecommerce-api-scope>"
```

**Expected**: `403 Forbidden`, thân JSON rõ ràng (research.md Decision 6) — không phải `200 OK`, không phải một thân rỗng mặc định.

### Scenario 3 — Endpoint mới không khai báo phân quyền bị chặn merge (spec US2, Test Scenario 1)

Kiểm tra bằng review mã, không phải runtime call: thêm tạm một route `app.MapGet("/temp-test", ...)` vào một `*Endpoints.cs` bất kỳ mà không chain `.RequireAuthorization(...)`/`.AllowAnonymous()`, chạy lại Scenario 1.

**Expected**: `AuthorizationPolicyDeclaredScannerTests` thất bại, nêu rõ route/file thiếu khai báo — xác nhận cổng build chặn đúng trường hợp này trước khi hoàn tác thay đổi thử nghiệm.

### Scenario 4 — Máy chủ tự từ chối dữ liệu không hợp lệ khi bỏ qua SPA (spec US3, Test Scenario 3)

```bash
# Giỏ hàng rỗng, gọi thẳng checkout của BFF, bỏ qua nút "Check out" đã bị vô hiệu hóa ở SPA
curl -i -X POST http://localhost:<bff-port>/bff/checkout -H "Authorization: Bearer <token>"

# Số lượng không hợp lệ, gọi thẳng baskets, bỏ qua việc SPA luôn gửi quantity: 1
curl -i -X POST http://localhost:<baskets-port>/baskets/current/items \
  -H "Authorization: Bearer <token>" -H "Content-Type: application/json" \
  -d '{"productId":"<product-id>","quantity":0,"unitPrice":10}'
```

**Expected**: Cả hai lệnh trả về lỗi 4xx (`409` cho giỏ hàng rỗng, `400` cho số lượng < 1) — không có lệnh nào lọt qua như một yêu cầu hợp lệ, đúng theo `contracts/client-server-validation-parity-contract.md`.

### Scenario 5 — Toggle tắt: hành vi rơi về đúng như 014 (constitution Principle X)

```bash
# Gạt toggle authz-require-api-scope về false, không cần redeploy
curl -i http://localhost:<baskets-port>/baskets/current -H "Authorization: Bearer <token-without-ecommerce-api-scope>"
```

**Expected**: `200 OK` — vì khi toggle tắt, chính sách chỉ còn yêu cầu "đã xác thực" (đúng hành vi trước tính năng này); rollback có hiệu lực ngay lập tức, không cần build/deploy lại.

## Automated Coverage

Các scenario trên là phần thủ công/khám phá bổ sung cho bộ test tự động: `tests/CrossServiceIsolation.Tests/AuthorizationPolicyDeclaredScannerTests.cs` (mới), `AuthorizationPolicyTests.cs` (mới, mỗi service — mirror `IndependentTokenValidationTests.cs`), test bổ sung xác nhận validation parity ở `Baskets.Api.IntegrationTests`/`Bff.Api.IntegrationTests`/`Orders.Api.IntegrationTests`, và cập nhật `IntegrationTestSupport.TestJwtBearer` để phát hành token có/không có claim `scope=ecommerce-api` theo yêu cầu của từng test — tất cả nằm trong PR gate hiện có từ [013-sonarqube-merge-blocker](../013-sonarqube-merge-blocker/).
