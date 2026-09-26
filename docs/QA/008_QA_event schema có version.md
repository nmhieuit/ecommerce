# QA: Event schema có version — OrderPlaced, BasketCheckedOut

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát (US1 → US3)

1. **Hợp đồng ở 1 nơi** (US1): JSON Schema 2020-12 + record C# của `OrderPlaced`/`BasketCheckedOut` nằm trong `shared/EventContracts`,
   service không tự định nghĩa lại.
2. **Phiên bản đã công bố là bất biến** (US2): sửa 1 schema đã công bố (kể cả thêm trường bắt buộc mà không tạo phiên bản mới)
   làm test đóng băng nội dung thất bại — chặn merge.
3. **Bên phát/bên nhận** (US3): event thật serialize ra kiểm chứng hợp lệ với schema; consumer không vỡ khi gặp trường lạ.
4. **Trên hệ thống chạy thật** (từ spec 024): `orders-api` phát `OrderPlacedV1` qua outbox lên RabbitMQ; message thật phải khớp `OrderPlaced.v1.schema.json`.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — bật/tắt bằng cấu hình rồi bấm Postman

Dựng stack `docker-compose.local.yml` (chỉ cần `orders-api` + `rabbitmq`). Postman: import
[`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**;
chạy request `00 - Xác thực & phân quyền → 01 Lấy access token` một lần, rồi folder **`08 - Event schema OrderPlaced (RabbitMQ)`** (bước 01 → 05, mỗi bước cách nhau vài giây).
Folder này tạo queue tạm bind vào exchange `EventContracts:OrderPlacedV1`, đặt 1 đơn, đọc message và kiểm từng ràng buộc của schema v1, rồi xoá queue.

**Công tắc**: `ORDERS_RABBITMQ_CONNECTION` (mẫu ở [`.env.example`](../../.env.example)); đổi xong chạy `docker compose -f docker-compose.local.yml up -d --force-recreate orders-api` và đợi `healthy`.

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-26)** |
|---|---|---|---|---|
| SC-001 — schema ở vị trí dùng chung | (không cần) — mở thư mục `shared/EventContracts/schemas` | (không có) | `OrderPlaced.v1` và `BasketCheckedOut.v1` | Đúng: 2 file (`OrderPlaced.v1.schema.json`, `BasketCheckedOut.v1.schema.json`) |
| SC-001 — service không tự định nghĩa lại | (không cần) — tìm `record OrderPlaced` / `record BasketCheckedOut` trong `services/` | (không có) | Không có định nghĩa trùng (quickstart gốc nói "0 kết quả tham chiếu") | **Lệch quickstart**: 9 file *tham chiếu* 2 event (Orders `OrdersDbContext`, `OrderEndpoints`, Baskets `BasketCheckedOutMapper` + test) vì từ spec 024 Orders/Baskets thật sự phát/tiêu thụ; **không** có định nghĩa lại trong `services/`, `Orders.Api`/`Baskets.Api` dùng `shared/EventContracts` (US1-KB2 đúng) |
| **TẮT** kết nối broker (mặc định của local) | `.env`: không có dòng `ORDERS_RABBITMQ_CONNECTION` (hoặc để rỗng) → tạo lại `orders-api` | `08` bước 01 → 05 | Đơn vẫn đặt được (FR-007 spec 024), không có message | `03` → `201` (cold start 3.8 s); bước 04 **đỏ** "không thấy message … số message trong queue: 0"; bảng `OutboxMessage` có **1 hàng kẹt** chưa giao; các bước 01/02/03/05 xanh (exchange còn từ lần chạy trước nên bind vẫn `201`) |
| **BẬT** kết nối broker | `.env`: `ORDERS_RABBITMQ_CONNECTION=amqp://guest:guest@rabbitmq:5672` → tạo lại `orders-api` | `08` bước 01 → 05 | Message `OrderPlaced` tới queue, hợp lệ với schema v1 | Hàng kẹt của lượt TẮT được giao ngay khi khởi động lại (`OutboxMessage` về 0). Bước 04: **12/14 test xanh** — xanh: có message, loại `urn:message:EventContracts:OrderPlacedV1`, **đúng 7 trường bắt buộc, không thừa**, UUID, `occurredAtUtc` UTC, `tenantId`, `correlationId` = `X-Correlation-Id` đã gửi; **đỏ 2 test**: `total` và `unitPrice` là **chuỗi** (`"12.50"`) trong khi schema v1 quy định `number` — xem QA_Debt |
| SC-002 — thay đổi phá vỡ không kèm phiên bản bị bắt *(ngoại lệ: hợp đồng thư viện, không có runtime để bật/tắt)* | Sửa `shared/EventContracts/schemas/OrderPlaced.v1.schema.json` (thêm thuộc tính bắt buộc `qaExperimentField`), rồi hoàn tác (`git checkout`) | (không có) — chạy `dotnet test shared/EventContracts.UnitTests` | Test đóng băng thất bại | Đúng: **2 test đỏ** — `SchemaImmutabilityTests.OrderPlaced_V1_Schema_Content_Is_Frozen` và `SchemaValidationTests.Serialized_OrderPlacedV1_Validates_Against_Its_Published_Schema`; sau hoàn tác 6/6 PASS, `git status` sạch |
| SC-003 — consumer cũ chịu được trường lạ *(ngoại lệ: chưa có consumer thật, chỉ có test)* | (không có) | (không có) — `--filter TolerantReaderTests` | PASS | 2/2 PASS |
| SC-004 — tìm phiên bản/khung ngưng dùng ở 1 chỗ | (không cần) — mở `shared/EventContracts/README.md` | (không có) | Nêu phiên bản hiện hành, phiên bản cũ, chính sách deprecation | Có bảng phiên bản (cả 2 event: V1, chưa có phiên bản cũ) và mục "Deprecation window". **Nhưng dòng đầu README "Nothing here is referenced by a service yet… no broker exists" đã sai** (xem QA_Debt) |
| `BasketCheckedOut` trên hệ thống thật | — | (không có) | Có luồng phát/nhận | **Không có luồng thật**: `BasketCheckedOutMapper` chỉ dựng payload cho Pact provider test (spec 011), chưa có publisher/consumer wire — không kiểm thủ công được |
| Chặn merge trong CI | (không cần) — đọc `scripts/ci/run-dotnet-tests.sh unit` | (không có) | `EventContracts.UnitTests` nằm trong tầng unit | Theo script: tầng `unit` = mọi `*Tests.csproj` còn lại kể cả `shared/`, nên chạy trong cổng PR. **Chưa chạy Jenkins thật** — chỉ đọc script |

Đã dọn: `.env` về nguyên trạng, `orders-api` tạo lại theo compose mặc định (TẮT), xoá các đơn/outbox do QA tạo, queue tạm đã xoá.

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

Mỗi link mở thẳng đúng dòng khai báo hàm test (comment mỗi hàm đã gắn `Task nguồn: spec 008 ...`, xem lại tại đó nếu cần biết test ứng với US/task nào). Bộ test không cần Docker/DB/broker.

| Cần xác nhận | Test case (bấm để mở) | Lệnh chạy riêng |
|---|---|---|
| US2/FR-003,006/SC-002 — schema đã công bố là bất biến (hash SHA-256 đóng băng) cho cả 2 event | [`SchemaImmutabilityTests.cs:46`](../../shared/EventContracts.UnitTests/SchemaImmutabilityTests.cs#L46) · [`:60`](../../shared/EventContracts.UnitTests/SchemaImmutabilityTests.cs#L60) | `dotnet test shared/EventContracts.UnitTests --filter "FullyQualifiedName~SchemaImmutabilityTests"` |
| US3/FR-009 — event dựng thật + serialize kiểm chứng hợp lệ với schema đã công bố (record và schema không lệch nhau) | [`SchemaValidationTests.cs:49`](../../shared/EventContracts.UnitTests/SchemaValidationTests.cs#L49) · [`:82`](../../shared/EventContracts.UnitTests/SchemaValidationTests.cs#L82) | `dotnet test shared/EventContracts.UnitTests --filter "FullyQualifiedName~SchemaValidationTests"` |
| US3/FR-007/SC-003 — consumer không vỡ khi payload có trường lạ (tolerant reader) | [`TolerantReaderTests.cs:41`](../../shared/EventContracts.UnitTests/TolerantReaderTests.cs#L41) · [`:93`](../../shared/EventContracts.UnitTests/TolerantReaderTests.cs#L93) | `dotnet test shared/EventContracts.UnitTests --filter "FullyQualifiedName~TolerantReaderTests"` |

**Kết quả lượt QA này (2026-09-26)**: `dotnet test shared/EventContracts.UnitTests` **6/6 PASS** (2 immutability + 2 validation + 2 tolerant reader). Bộ test này chỉ kiểm record + schema, **không** kiểm message thật trên RabbitMQ — vì vậy không bắt được lệch kiểu `total`/`unitPrice` ở bảng thủ công phía trên.

## Kết luận

**PASS kèm ghi chú nghiêm trọng.** Hợp đồng ở 1 nơi, bất biến, và test đóng băng chặn đúng (SC-002: thêm trường bắt buộc → 2/6 đỏ, hoàn tác → 6/6); trên hệ thống thật message `OrderPlaced` có đúng 7 trường, đúng loại, mang đúng `tenantId`/`correlationId`. Ghi chú: (1) **message thật vi phạm schema v1** — `total` và `unitPrice` là chuỗi thay vì số — mà 6 test tự động không phát hiện vì chúng dùng serializer khác MassTransit; (2) `shared/EventContracts/README.md` và quickstart SC-001 vẫn nói "chưa service nào tham chiếu / 0 kết quả grep"; (3) đường nâng phiên bản (V2, khung ngưng dùng) chưa từng thực hành, kiểm tra tương thích chỉ là hash thô;
(4) `BasketCheckedOut` chưa có luồng thật; chưa chạy Jenkins. Chi tiết và hướng vá: [QA_Debt.md](QA_Debt.md) mục 008.
