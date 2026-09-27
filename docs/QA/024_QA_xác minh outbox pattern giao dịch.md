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

### Thủ công — bật/tắt broker RabbitMQ rồi bấm Postman

Công tắc cấu hình: biến `ORDERS_RABBITMQ_CONNECTION` trong `.env` (xem `.env.example`), rồi `docker compose -f docker-compose.local.yml up -d --force-recreate --no-deps orders-api`:
BẬT = `amqp://guest:guest@rabbitmq:5672` (mặc định của `docker-compose.yml`); TẮT = để trống (mặc định của `docker-compose.local.yml` khi không khai báo).
Dựng stack: `docker compose -f docker-compose.local.yml up -d --wait gateway-api bff-api orders-api rabbitmq` (kéo theo identity-api và DB), với công tắc **BẬT**.
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**; chạy `00 - Xác thực & phân quyền (Get Token) → 01`, rồi lần lượt 3 folder con của
**`24 - Outbox giao dịch (broker sập / kill orders-api)`** trong Runner: `24a` → `docker compose -f docker-compose.local.yml stop rabbitmq` → `24b` → `docker compose -f docker-compose.local.yml start rabbitmq` → `24c`.

**Công tắc** (hạ tầng, không sửa mã): `stop rabbitmq` / `start rabbitmq`; `docker kill ecomerce-local-orders-api-1` + `start orders-api`; `ORDERS_RABBITMQ_CONNECTION` (`.env`).

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-27)** |
|---|---|---|---|---|
| Chuẩn bị — queue bền hứng bản sao `OrderPlacedV1`, đặt 1 đơn làm ấm | Công tắc BẬT, broker chạy | `24a` bước 01 → 04 | Queue + binding tạo được; đơn làm ấm được giao ngay | `24a` 4/4 xanh (`201`, `201`, đơn `201` ~25–90 ms, đọc queue ≥ 1 message) |
| FR-007/US1 — đặt đơn khi broker không tới được | `stop rabbitmq` | `24b` bước 01 (`POST /orders` trực tiếp) | `201` nhanh, không phụ thuộc broker | `201` trong **68–84 ms**; bảng outbox `OutboxMessage` có **1** dòng chờ (`select count(*)` qua `sqlcmd`) |
| FR-003/FR-004/US2-KB1 — broker sống lại, message tự được giao | `start rabbitmq` | `24c` bước 01, 02 (chờ ~30 giây rồi đọc queue) | Message đã commit tự được gửi, đúng 1 lần | Đúng **1** message cho `orderId` đó (không mất, không trùng); `correlationId` khớp `X-Correlation-Id`; tổng `59.25`, 2 dòng — **5/5 assertion xanh** |
| Kịch bản 1 quickstart — sập giữa commit và gửi | `stop rabbitmq` → `24b` → `docker kill ecomerce-local-orders-api-1` → `start orders-api` và `start rabbitmq` | `24c` | Message đã commit vẫn được giao sau khi tiến trình chết | Hàng outbox còn nguyên sau kill (`pending = 1`); sau khi khởi động lại `24c` **5/5 xanh** — đúng 1 message, đúng `orderId` |
| Công tắc TẮT — outbox tích luỹ, không giao *(ngoại lệ: sửa `.env` rồi tạo lại service)* | `ORDERS_RABBITMQ_CONNECTION=` (rỗng) trong `.env` rồi `up -d --force-recreate --no-deps orders-api`; 2 lần `POST /orders` | (bước 24b tương đương) | `201` bình thường, message chưa giao | 2 `POST` đều `201` (1.19 s lần đầu, 48 ms lần sau); sau 8 giây bảng outbox còn **2** dòng, log `orders-api` đầy thông báo `Connection Failed` (60 dòng) — không lỗi nào ra tới client |
| Công tắc BẬT lại — bản ghi kẹt được giao | Khôi phục `.env`, tạo lại `orders-api` | (không có) | Outbox về 0 | Bảng outbox về **0** ngay chu kỳ quét đầu; exchange `EventContracts:OrderPlacedV1` `publish_in` tăng tương ứng |
| US1-KB3 — nhiều đơn đồng thời *(ngoại lệ: tải song song)* | Queue bền `24a`; `seq 1 20 \| xargs -P 20 -I{} curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5041/orders -H "Authorization: Bearer <token>" -H "X-Tenant-Id: contoso" -H "X-Subject-Id: phase1-stub-user" -H "X-Correlation-Id: qa024-burst-{}" -H "content-type: application/json" -d '{"items":[{"productId":"9f8d6b1e-0001-4000-8000-000000000001","quantity":1,"unitPrice":12.5}]}'` | (không có) | Mỗi đơn đúng 1 outbox, không thiếu/trùng | 20 × `201`; sau ~6 giây outbox `0`; đọc queue: **20 message, 20 `messageId` khác nhau, 20 `orderId` khác nhau**, `correlationId` `qa024-burst-N` khớp 20/20 |
| Mutation trên mã (thêm `SaveChangesAsync()` sau `Orders.Add`; comment `UseBusOutbox()`) *(ngoại lệ: sửa mã production)* | — | — | Test đỏ | **Không chạy lại ở lượt này** (sửa mã production bị chặn bởi quyền của phiên). Kết quả lượt trước còn nguyên (xem QA_Debt): mutation tách transaction → 4/4 vẫn xanh; comment `UseBusOutbox()` → chỉ 2 đỏ |
| Dọn dẹp | Xoá queue `qa024-orderplaced` (bước `24c/03`); xoá đơn thử; khôi phục `.env` | (không có) | Không dữ liệu dư | `Orders` về 1 dòng gốc, outbox `0`, `orders-api` chạy đúng cấu hình `.env` ban đầu |

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001/US1-KB1 — đơn hàng và outbox cùng tồn tại sau `POST /orders` | [`OrderPlacedOutboxAtomicityTests.cs:41`](../../services/orders/tests/Orders.Api.IntegrationTests/OrderPlacedOutboxAtomicityTests.cs#L41) — `PlaceOrder_WritesTheOrder_AndTheOutboxRecord_InTheSameTransaction` | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter FullyQualifiedName~OrderPlacedOutboxAtomicityTests` |
| FR-002/US1-KB2 — rollback thì không có hàng outbox | [`:90`](../../services/orders/tests/Orders.Api.IntegrationTests/OrderPlacedOutboxAtomicityTests.cs#L90) — `PlaceOrder_WhenTheTransactionRollsBack_WritesNeitherTheOrderNorTheOutboxRecord` | (lệnh như trên) |
| FR-003/FR-004/US2-KB1 — commit rồi sập, khởi động lại tự gửi | [`OrderPlacedOutboxCrashRecoveryTests.cs:43`](../../services/orders/tests/Orders.Api.IntegrationTests/OrderPlacedOutboxCrashRecoveryTests.cs#L43) — `OutboxMessage_CommittedButUnsentWhenTheProcessStops_StillGetsPublished_AfterRestart` | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter FullyQualifiedName~OrderPlacedOutboxCrashRecoveryTests` |
| FR-005/US3-KB1 — consumer nhận trùng chỉ xử lý 1 lần | [`OrderPlacedIdempotentConsumerTests.cs:42`](../../services/orders/tests/Orders.Api.IntegrationTests/OrderPlacedIdempotentConsumerTests.cs#L42) — `Consumer_ProcessesTheSameRedeliveredMessage_ExactlyOnce` | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter FullyQualifiedName~OrderPlacedIdempotentConsumerTests` |

**Kết quả lượt QA này (2026-09-27)**: 4/4 test outbox xanh (~30 giây, dựng SQL Server + RabbitMQ Testcontainers). US1-KB3, US2-KB2/KB3, US3-KB2/KB3 không có test tự động (xem QA_Debt).

## Kết luận

**PASS kèm ghi chú nghiêm trọng.** Hành vi outbox đúng như thiết kế và được chứng minh sống bằng công tắc broker: đặt đơn khi broker sập vẫn `201` trong ~80 ms, message nằm lại trong outbox rồi tự được giao khi broker sống lại (kể cả sau `docker kill orders-api`), 20 đơn đồng thời không thiếu/trùng message, correlation ID đi hết vào payload. 4/4 test tự động xanh.
Ghi chú: (1) test không phát hiện việc `POST /orders` tách order và outbox ra 2 transaction, và test rollback rỗng nghĩa nếu bus outbox bị tắt (lượt trước, chưa chạy lại); (2) test crash-recovery không giữ cấu hình chu kỳ quét; 5 kịch bản chấp nhận (US1-KB3, US2-KB2/KB3, US3-KB2/KB3) chưa có test tự động;
(3) khi công tắc TẮT (mặc định của `docker-compose.local.yml`) hoặc manifest K8s không có cấu hình RabbitMQ, outbox không bao giờ giao được và chỉ có log báo (im lặng với client); (4) `total` trong payload là chuỗi (xem QA 008); (5) `data-model.md`/`quickstart.md` mô tả sai cột outbox. Chi tiết và hướng vá: [QA_Debt.md](QA_Debt.md) mục 024.
