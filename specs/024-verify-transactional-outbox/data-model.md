# Data Model: Xác minh outbox pattern giao dịch trên dịch vụ phát sự kiện đơn hàng

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-11

Không có entity nghiệp vụ mới. Toàn bộ bảng dưới đây do `MassTransit.EntityFrameworkCore` định nghĩa
sẵn (đăng ký qua `modelBuilder.AddTransactionalOutboxEntities()`), không phải entity tự thiết kế cho
tính năng này — xem research.md Quyết định 2.

## `OutboxMessage` (mới — CSDL `orders`, trong `OrdersDbContext`)

Một hàng cho mỗi message chờ gửi, được ghi trong **cùng transaction EF Core** với việc lưu `Order`.

| Trường (khái niệm) | Vai trò |
|---|---|
| `SequenceNumber` | Thứ tự gửi trong cùng 1 outbox (đảm bảo gửi đúng thứ tự các message của cùng 1 request). |
| `EnqueueTime` / `SentTime` | Thời điểm ghi vào outbox / thời điểm đã gửi thành công — `SentTime` rỗng nghĩa là "chưa gửi", chính là điều kiện outbox delivery service quét để phục hồi sau crash (spec FR-003/FR-004). |
| Payload (`OrderPlacedV1` tuần tự hoá) | Nội dung sự kiện sẽ publish — đúng hợp đồng đã có, không đổi. |

**Ràng buộc từ spec**: một hàng `OutboxMessage` cho `OrderPlacedV1` của 1 đơn hàng chỉ tồn tại nếu
transaction tạo `Order` đó đã commit thành công (FR-001); nếu transaction rollback, hàng này (và hàng
`Order` tương ứng) không tồn tại — cả hai cùng biến mất hoặc cùng không tồn tại (FR-002).

## `OutboxState` (mới — CSDL `orders`, trong `OrdersDbContext`)

Theo dõi trạng thái gửi ở cấp "lô" (outbox instance) cho outbox delivery service — dùng nội bộ bởi
thư viện để biết đã gửi tới đâu, không phải dữ liệu nghiệp vụ.

## `InboxState` (mới — CSDL `orders`, trong `OrdersDbContext`, dùng bởi consumer xác minh)

Một hàng cho mỗi `(MessageId, ConsumerId)` đã được consumer xác minh xử lý — khoá theo `MessageId`
(chính là `OrderPlacedV1.EventId`), dùng row-level lock để chặn xử lý trùng.

**Ràng buộc từ spec (FR-005)**: nhận `OrderPlacedV1` lần đầu (một `EventId` chưa có trong `InboxState`)
→ consumer xử lý và ghi 1 hàng `InboxState`. Nhận lại đúng `EventId` đó lần thứ hai → MassTransit chặn
consumer chạy lại (row đã tồn tại) → không có tác dụng phụ thêm — đây là cơ chế thật được test xác
minh, không phải logic dedupe tự viết (research.md Quyết định 3).

## `OrderPlacedV1` (đã có, KHÔNG đổi)

Xem `shared/EventContracts/OrderPlacedV1.cs`. Publish nguyên trạng: `EventId`, `OccurredAtUtc`,
`OrderId`, `TenantId`, `CorrelationId`, `Total`, `Lines` — dựng từ `Order` vừa lưu (`Id`, `PlacedAtUtc`,
`Total`, `TenantId`) cộng `Lines` lấy từ `PlaceOrderRequest.Items` trong cùng request (bản thân `Order`
không lưu line item — xem `Order.cs`), và `CorrelationId` đọc từ
`context.Items[CorrelationIdMiddleware.HeaderName]` của request hiện tại.

## Bản ghi "đã xử lý" của consumer xác minh (mới — chỉ dùng trong test)

Một bảng tối giản, riêng cho `Orders.Api.IntegrationTests` (không phải một phần của `OrdersDbContext`
sản xuất), ghi lại mỗi lần `OrderPlacedVerificationConsumer` thực sự chạy logic của nó (không tính các
lần bị `InboxState` chặn) — dùng để assert "chỉ chạy đúng 1 lần cho 1 `EventId`, dù nhận message 2
lần" (FR-005/SC-003).
