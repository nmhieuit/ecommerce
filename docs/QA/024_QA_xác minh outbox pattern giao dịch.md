# QA: Xác minh outbox pattern giao dịch trên dịch vụ phát sự kiện đơn hàng

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. `POST /orders` (Orders.Api) gọi `Order.PlaceFrom(...)` rồi `IPublishEndpoint.Publish(OrderPlacedV1)` **trước** `SaveChangesAsync()` — nhờ MassTransit Bus Outbox (`UseBusOutbox()`) lệnh publish chỉ xếp 1 hàng `OutboxMessage`, được commit cùng transaction với hàng `Order`.
2. `OrdersDbContext.AddTransactionalOutboxEntities()` + migration `AddTransactionalOutbox` (additive) tạo `OutboxMessage`/`OutboxState`/`InboxState`; MassTransit ghim `8.5.4` (v9 đã tính phí).
3. Hosted outbox delivery service quét mỗi `Outbox:QueryDelaySeconds` (mặc định 1 s), gửi lên RabbitMQ, rồi xoá bản ghi — kể cả ở lần quét đầu tiên sau khi tiến trình khởi động lại (đường phục hồi sau crash).
4. `OrderPlacedV1.CorrelationId` lấy từ `HttpContext.Items["X-Correlation-Id"]`; `ServiceDefaults` thêm `AddSource/AddMeter("MassTransit")`; `/health/ready` cố ý chỉ xét database, không xét bus.
5. Consumer xác minh idempotency (`OrderPlacedVerificationConsumer`) chỉ tồn tại trong test, dùng Consumer Outbox/`InboxState` thật khoá theo `MessageId`; không có consumer nghiệp vụ thật.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001/US1-KB1 — đơn hàng và outbox cùng tồn tại sau `POST /orders` | [`OrderPlacedOutboxAtomicityTests.cs:41`](../../services/orders/tests/Orders.Api.IntegrationTests/OrderPlacedOutboxAtomicityTests.cs#L41) — `PlaceOrder_WritesTheOrder_AndTheOutboxRecord_InTheSameTransaction` | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter FullyQualifiedName~OrderPlacedOutboxAtomicityTests` |
| FR-002/US1-KB2 — rollback thì không có hàng outbox | [`:90`](../../services/orders/tests/Orders.Api.IntegrationTests/OrderPlacedOutboxAtomicityTests.cs#L90) — `PlaceOrder_WhenTheTransactionRollsBack_WritesNeitherTheOrderNorTheOutboxRecord` | (lệnh như trên) |
| FR-003/FR-004/US2-KB1 — commit rồi sập, khởi động lại tự gửi | [`OrderPlacedOutboxCrashRecoveryTests.cs:43`](../../services/orders/tests/Orders.Api.IntegrationTests/OrderPlacedOutboxCrashRecoveryTests.cs#L43) — `OutboxMessage_CommittedButUnsentWhenTheProcessStops_StillGetsPublished_AfterRestart` | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter FullyQualifiedName~OrderPlacedOutboxCrashRecoveryTests` |
| FR-005/US3-KB1 — consumer nhận trùng chỉ xử lý 1 lần | [`OrderPlacedIdempotentConsumerTests.cs:42`](../../services/orders/tests/Orders.Api.IntegrationTests/OrderPlacedIdempotentConsumerTests.cs#L42) — `Consumer_ProcessesTheSameRedeliveredMessage_ExactlyOnce` | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter FullyQualifiedName~OrderPlacedIdempotentConsumerTests` |

**Kết quả lượt QA này (2026-09-24)**: 4/4 test outbox xanh (~2.6 phút, dựng SQL Server + RabbitMQ Testcontainers); toàn `Orders.Api.IntegrationTests` **29/29** (khớp tài liệu); `dotnet build Ecommerce.slnx` 0 lỗi — không đổi sau khi dịch comment. US1-KB3, US2-KB2/KB3, US3-KB2/KB3 không có test tự động (xem QA_Debt).

### Thủ công — trên stack Docker `docker-compose.local.yml`

| Bước | Cách làm | Kỳ vọng theo tài liệu | **Đã quan sát** |
|---|---|---|---|
| Đặt đơn khi broker không tới được | `POST :5041/orders` (token thật + `X-Tenant-Id` + `X-Subject-Id`) | `201` nhanh, không phụ thuộc broker (FR-007) | `201` trong 0.33 s; outbox tích luỹ, không giao được vì `orders-api` của compose local **thiếu cấu hình RabbitMQ** (411 log `Connection Failed`, xem QA_Debt) |
| Thêm `ConnectionStrings__RabbitMq` (file override tạm) rồi tạo lại `orders-api` | `docker compose -f docker-compose.local.yml -f <override> up -d --no-deps --force-recreate orders-api` | Bản ghi kẹt tự được giao | Cả 2 message kẹt (trong đó 1 cái kẹt 7+ giờ) được giao ngay: exchange `EventContracts:OrderPlacedV1` `publish_in: 2`, bảng outbox về 0 |
| Kịch bản 1 quickstart — sập giữa commit và gửi | Dừng RabbitMQ → `POST /orders` → `docker kill orders-api` → bật lại RabbitMQ + `orders-api` | Message đã commit tự được gửi, không cần thao tác khác | `POST` `201` trong 0.91 s; hàng outbox còn nguyên sau kill; sau khởi động lại `publish_in: 1`, bảng outbox về 0 |
| US1-KB3 — nhiều đơn đồng thời | 20 `POST` song song, mỗi cái 1 `X-Correlation-Id`; đọc message từ queue tạm bind vào exchange | Mỗi đơn đúng 1 outbox, không thiếu/trùng | 20 `201`; `Orders` 3 → 23; outbox về 0 sau ~4 s; 20 message với 20 `messageId`/`orderId`/`eventId` khác nhau; `correlationId` trong payload khớp `X-Correlation-Id` 20/20 |
| Mutation: thêm `SaveChangesAsync()` sau `Orders.Add` (order commit riêng) | Sửa `OrderEndpoints.cs`, chạy 4 test, `git checkout --` | Test đỏ | **4/4 vẫn xanh** — xem QA_Debt |
| Mutation: comment `UseBusOutbox()` | Sửa `Program.cs`, chạy 4 test, hoàn tác | Cả 4 đỏ | 2 đỏ (atomicity, crash-recovery); **rollback và idempotency vẫn xanh** |
| Mutation: bỏ Host B / gán cứng `QueryDelay = 1s` (crash test) | Sửa test / `Program.cs`, hoàn tác | Test đỏ | Bỏ Host B → đỏ sau ~48 s; gán cứng `QueryDelay` → **vẫn xanh** |
| Mutation: bỏ `UseEntityFrameworkOutbox` khỏi consumer host | Sửa file Support, hoàn tác | Idempotency đỏ | Đỏ: `Expected: 1, Actual: 2` |

Đã dọn dữ liệu QA (22 đơn thêm, queue tạm `qa024`) và tạo lại `orders-api` đúng theo `docker-compose.local.yml` của repo (không override).

## Kết luận

**PASS kèm ghi chú nghiêm trọng.** Hành vi outbox đúng như thiết kế và đã được chứng minh sống: ghi đơn + outbox, sống sót qua `docker kill` + broker sập, 20 đơn đồng thời không thiếu/trùng message, correlation ID đi hết vào payload. Ghi chú: (1) test không phát hiện việc `POST /orders` tách order và outbox ra 2 transaction (mutation → 4/4 vẫn xanh), và test rollback rỗng nghĩa nếu bus outbox bị tắt;
(2) test crash-recovery không giữ cấu hình chu kỳ quét và không khẳng định "chưa gửi" trước khi khởi động lại; 5 kịch bản chấp nhận (US1-KB3, US2-KB2/KB3, US3-KB2/KB3) chưa có test tự động; (3) `orders-api` của `docker-compose.local.yml` và manifest K8s không có cấu hình RabbitMQ nên outbox không bao giờ giao được (im lặng — chỉ có log);
(4) `data-model.md`/`quickstart.md` mô tả sai cột outbox (`SentTime` không phải cờ "đã gửi") và 3 kịch bản thủ công không tái hiện nguyên văn được. Chi tiết và hướng vá: [QA_Debt.md](QA_Debt.md) mục 024.
