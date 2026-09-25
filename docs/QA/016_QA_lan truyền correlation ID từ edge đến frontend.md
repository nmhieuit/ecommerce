# QA: Lan truyền Correlation ID từ Edge đến Frontend

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

Spec không xây cơ chế mới — vá 3 khoảng trống trong `CorrelationIdMiddleware` (từ spec 001): giá trị client tự gửi chưa được validate,
hop BFF → domain service làm rớt correlation ID, SPA không đọc được header bằng mã.

## Luồng happy-case đã rà soát

1. **Gateway sinh/giữ correlation ID có validate**: `CorrelationIdMiddleware.ResolveCorrelationId` giữ giá trị client gửi nếu hợp lệ (chuỗi mờ bất
   kỳ, không ép GUID), sinh `Guid` mới nếu thiếu/không hợp lệ; chỉ loại ký tự điều khiển (kể cả CRLF) và độ dài > 128.
2. **Gateway → BFF**: YARP tự copy header của request đã bị middleware ghi ngược — đã đúng từ spec 002.
3. **BFF → domain service**: `TenantPropagationHandler.SendAsync` đọc `HttpContext.Items[X-Correlation-Id]` tươi mỗi lần gọi (không capture ở constructor) và relay.
4. **Async**: `OrderEndpoints.cs:101` gán `httpContext.Items[…]` vào `OrderPlacedV1.CorrelationId` khi publish qua outbox (spec 024).
5. **CORS + SPA**: `Gateway.Api/Program.cs` `.WithExposedHeaders(X-Correlation-Id)`; `ApiError.correlationId` đọc từ `response.headers`.
6. **Không lẫn khi đồng thời**: rủi ro chỉ ở handler pool của `IHttpClientFactory` — có test tải đồng thời thật (10 request song song).

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001/FR-002 — gateway tự sinh/giữ correlation ID | [`CorrelationIdPropagationTests.cs:36`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs#L36) · [`:71`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs#L71) (2 test này của spec 002) | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter FullyQualifiedName~CorrelationIdPropagationTests` |
| FR-009 — loại ký tự điều khiển/độ dài quá mức | [`:108`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs#L108) · [`:145`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs#L145) (2 test này do spec 016 thêm) | (lệnh như trên) |
| FR-003 — BFF → domain service relay đúng ID | [`Bff.Api.IntegrationTests/CorrelationPropagationTests.cs:43`](../../services/bff/tests/Bff.Api.IntegrationTests/CorrelationPropagationTests.cs#L43) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter FullyQualifiedName~CorrelationPropagationTests` |
| FR-007/US3 — 10 request đồng thời không lẫn ID | [`:80`](../../services/bff/tests/Bff.Api.IntegrationTests/CorrelationPropagationTests.cs#L80) | (lệnh như trên) |
| FR-004 — nhánh async mang correlation ID | không có test tự động (xem QA_Debt); xác nhận tĩnh ở `services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs:101` | — |
| FR-006/US2 — SPA đọc correlation ID bằng mã | [`frontend/apps/web/tests/shared/fetcher.test.ts:28`](../../frontend/apps/web/tests/shared/fetcher.test.ts#L28) · [`:55`](../../frontend/apps/web/tests/shared/fetcher.test.ts#L55) | `cd frontend && corepack pnpm --filter web test -- fetcher` (chạy từ `frontend/`, xem QA_Debt) |

**Kết quả lượt QA này (2026-09-23)**: Gateway **4/4**, BFF **2/2**, frontend **5/5** (gồm 3 test kế thừa từ spec 004 cùng file) — chạy lại sau khi sửa/dịch comment, không đổi hành vi.
Đã tự đọc mã thật của `CorrelationIdMiddleware`, `TenantPropagationHandler`, `Gateway.Api/Program.cs`, `fetcher.ts`, `OrderEndpoints.cs` — khớp `development/016`.

### Thủ công — Scenario 7 (SPA hiển thị correlation ID)

Xác nhận qua đọc mã (`WithExposedHeaders` + `ApiError.correlationId`) và test tự động ở trên (phần "đọc được bằng mã" chặt hơn việc nhìn DevTools, vốn hiển
thị mọi header thô không phụ thuộc CORS).

## Kết luận

**PASS kèm ghi chú.** 4 nguồn nhất quán với nhau và với mã thật; toàn bộ nhánh đồng bộ (US1 AC1/AC2, FR-001…003, FR-007, FR-009) tự chạy lại có Docker — 11 test xanh ở
3 project; nhánh CORS/SPA (US2) xác nhận bằng cả đọc mã lẫn test. Ghi chú: (1) 2 test kiểm tra hợp lệ ở file gateway bị gắn nhầm "Task nguồn: spec 002" thay vì spec 016 —
đã tự sửa comment, không đổi hành vi; (2) nhánh async (FR-004, US1 AC3) chỉ xác nhận tĩnh, chưa test nào so khớp `OrderPlacedV1.CorrelationId` dù hạ tầng consumer
thật đã có từ spec 024; (3) ghi chú công cụ `pnpm`. Chi tiết: [QA_Debt.md](QA_Debt.md) mục 016.
