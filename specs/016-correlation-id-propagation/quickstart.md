# Quickstart: Kiểm chứng lan truyền Correlation ID từ Edge đến Frontend

Kiểm chứng tính năng này đầu-cuối theo các acceptance scenario của `spec.md` và test scenario của vé Jira SCRUM-26. Xem [data-model.md](data-model.md) cho hình dạng Correlation ID/Hop lan truyền, và [contracts/correlation-id-header-contract.md](contracts/correlation-id-header-contract.md) / [contracts/event-correlation-field-contract.md](contracts/event-correlation-field-contract.md) / [contracts/spa-correlation-visibility-contract.md](contracts/spa-correlation-visibility-contract.md) cho các hợp đồng đầy đủ.

## Prerequisites

- .NET 10 SDK và Node.js/pnpm (qua corepack) đã cài, khớp `frontend/README.md`.
- Toàn bộ stack local chạy qua `docker-compose.local.yml` (gateway, bff, 4 domain service, `otel-collector`) — xem [005-one-command-local-run](../005-one-command-local-run/).
- Không cần Elastic/Kibana — quickstart này dùng log debug của `otel-collector` (research.md Decision 4).
- Một token hợp lệ (qua client `integration-test-ropc` như [014-identity-server-auth](../014-identity-server-auth/) đã thiết lập).

## Validation Scenarios

### Scenario 1 — Gateway tự sinh correlation ID khi request không mang sẵn (spec US1 AC1, Jira Test Scenario 2)

```bash
curl -i http://localhost:<gateway-port>/bff/products -H "Authorization: Bearer <token>"
```

**Expected**: Phản hồi có header `X-Correlation-Id` với một giá trị GUID dạng `n` (32 ký tự hex, không dấu gạch ngang) mà request không hề gửi lên.

### Scenario 2 — Correlation ID lan truyền xuyên suốt gateway → BFF → domain service, xuất hiện ở log mọi hop (spec US1 AC2/AC3, Jira Test Scenario 1 — nhánh đồng bộ)

```bash
CID="quickstart-$(date +%s)"
curl -i http://localhost:<gateway-port>/bff/products \
  -H "Authorization: Bearer <token>" \
  -H "X-Correlation-Id: $CID"

docker compose logs otel-collector | grep "$CID"
```

**Expected**: `$CID` xuất hiện trong log của **cả gateway, BFF, lẫn products** (không chỉ gateway/BFF) — trước khi sửa (research.md Decision 1), log của `products` sẽ mang một GUID khác do service đó tự sinh khi bị BFF bỏ mất header. Đây là bằng chứng trực tiếp cho bug đã sửa.

### Scenario 3 — Đặt đơn hàng, tra một correlation ID xuyên suốt toàn bộ nhánh đồng bộ (spec US1, Jira Test Scenario 1 — theo đúng luồng đặt hàng)

```bash
CID="order-quickstart-$(date +%s)"
curl -i -X POST http://localhost:<bff-port>/bff/checkout \
  -H "Authorization: Bearer <token>" -H "X-Correlation-Id: $CID"

docker compose logs otel-collector | grep "$CID"
```

**Expected**: `$CID` xuất hiện trong log của bff, baskets, và orders (mọi service tham gia luồng checkout đồng bộ). **Không** xuất hiện trong một consumer bất đồng bộ xử lý `OrderPlaced` — chưa tồn tại trong phạm vi tính năng này (research.md Decision 3; xác minh đầy đủ nhánh này chờ SCRUM-31).

### Scenario 4 — Correlation ID do client cung cấp được giữ nguyên, kể cả khi không phải GUID (spec Edge Cases, research.md Decision 2)

```bash
curl -i http://localhost:<gateway-port>/bff/products \
  -H "Authorization: Bearer <token>" -H "X-Correlation-Id: caller-supplied-correlation-id"
```

**Expected**: Phản hồi echo lại đúng `caller-supplied-correlation-id`, không bị thay bằng GUID mới.

### Scenario 5 — Giá trị chứa ký tự điều khiển hoặc quá dài bị từ chối, gateway sinh giá trị mới (spec Edge Cases/FR-009, research.md Decision 2)

```bash
curl -i http://localhost:<gateway-port>/bff/products \
  -H "Authorization: Bearer <token>" \
  -H $'X-Correlation-Id: bad\r\nvalue'
```

**Expected**: Phản hồi mang một correlation ID mới do gateway sinh (GUID), không phải giá trị chứa `\r\n` đã gửi lên; không có dòng log nào bị "gãy" bởi ký tự điều khiển.

### Scenario 6 — Hai request đồng thời không lẫn correlation ID (spec US3, Jira Test Scenario 3)

```bash
(curl -s http://localhost:<gateway-port>/bff/products -H "Authorization: Bearer <token>" -H "X-Correlation-Id: concurrent-a" &
 curl -s http://localhost:<gateway-port>/bff/products -H "Authorization: Bearer <token>" -H "X-Correlation-Id: concurrent-b" &
 wait)

docker compose logs otel-collector | grep -E "concurrent-a|concurrent-b"
```

**Expected**: Log của `concurrent-a` và `concurrent-b` tách biệt hoàn toàn — không có dòng log nào của service (products) mang ID này lại xuất hiện dưới ID kia. Đây là kịch bản tự động hoá bằng test đồng thời ở `Bff.Api.IntegrationTests` (research.md Decision 7), lệnh curl trên chỉ là kiểm chứng thủ công bổ sung.

### Scenario 7 — SPA đọc được correlation ID qua mã, không chỉ qua DevTools thủ công (spec US2, Jira Test Scenario 3)

**Thủ công (Jira AC3, đã đúng từ trước — research.md Decision 5)**:
1. Mở SPA (`pnpm --filter web dev`), mở tab Network của DevTools.
2. Thực hiện một thao tác gọi API bất kỳ (ví dụ tải trang catalog).
3. Chọn request tới gateway, xem tab Headers → Response Headers.

**Expected**: `X-Correlation-Id` hiển thị đầy đủ, kể cả trước khi sửa CORS.

**Tự động (mới, xác nhận SPA đọc được bằng code)**:

```bash
corepack pnpm --filter web test -- fetcher
```

**Expected**: Test mới trong `frontend/apps/web/tests/shared/fetcher.test.ts` xác nhận `ApiError.correlationId` khớp với header `X-Correlation-Id` do MSW mock trả về, và là `null` khi phản hồi không mang header này.

## Automated Coverage

Các scenario trên là phần thủ công/khám phá bổ sung cho bộ test tự động:

- `Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs` (mở rộng) — thêm test cho ràng buộc hợp lệ mới (Decision 2, Scenario 4/5 ở trên).
- `Bff.Api.IntegrationTests/CorrelationPropagationTests.cs` (mới, mirror `TenantPropagationTests.cs`) — xác nhận outbound call tới domain service mang đúng correlation ID của request đã nhận (Decision 1), cộng biến thể đồng thời (Decision 7, Scenario 6).
- `frontend/apps/web/tests/shared/fetcher.test.ts` (mới) — xác nhận `ApiError.correlationId` (Decision 5, Scenario 7).

Tất cả nằm trong PR gate hiện có từ [013-sonarqube-merge-blocker](../013-sonarqube-merge-blocker/) (build → unit test → integration test → contract test → SonarQube quality gate).
