# Contract: Trường `correlationId` trên Integration Event

Hợp đồng dữ liệu (không phải transport) giữa mọi integration event trong `shared/EventContracts` và consumer tương lai của chúng. Ghi lại hiện trạng và ranh giới phạm vi rõ ràng với SCRUM-31 (research.md Decision 3) — tính năng này **không** thay đổi hợp đồng này, chỉ xác nhận và ghi chép nó.

## Cam kết

| | |
|---|---|
| Áp dụng cho | Mọi integration event hiện có: `BasketCheckedOutV1`, `OrderPlacedV1` (`shared/EventContracts`) |
| Trường | `correlationId` (JSON), `CorrelationId` (C#) — kiểu `string`, **bắt buộc** trên cả schema JSON Schema (`schemas/*.v1.schema.json`) lẫn record C# |
| Nguồn giá trị | Correlation ID của request đồng bộ đã tạo ra sự kiện đó (ví dụ request checkout tạo `BasketCheckedOutV1`), truyền vào tại thời điểm dựng payload — không được resolve lại hay sinh mới ở tầng message |
| Trạng thái hôm nay | Trường đã tồn tại và đã được `BasketCheckedOutProviderPactTests`/`BasketCheckedOutConsumerPactTests`/`SchemaValidationTests` xác nhận đúng hình dạng (từ SCRUM-18, Done) — **trước cả khi tính năng SCRUM-26 này bắt đầu** |

## Producers (khi tồn tại)

| Nguồn | Hành vi |
|---|---|
| `Baskets.Api/Features/Checkout/BasketCheckedOutMapper.ToEvent(...)` | Nhận `correlationId` như một tham số bắt buộc, gắn vào payload — **hàm này đã tồn tại nhưng chưa được gọi bởi bất kỳ nơi nào** (comment trong chính file: "Nothing calls this yet, and that is deliberate... the outbox and the publisher... are SCRUM-31's work") |
| `Orders.Api` → `OrderPlacedV1` | **Chưa tồn tại** — không có mapper, không có publisher nào xây dựng kiểu này trong mã nguồn hôm nay; đây là công việc của SCRUM-31 |

## Consumers (khi tồn tại)

| Hop | Hành vi dự kiến (chưa xây) |
|---|---|
| Consumer bất đồng bộ xử lý `OrderPlacedV1`/`BasketCheckedOutV1` qua RabbitMQ (SCRUM-31) | Đọc `CorrelationId` từ payload, đưa vào `ILogger.BeginScope` giống hệt cơ chế đồng bộ (`CorrelationIdMiddleware`), để log của consumer nối liền với log của request đồng bộ đã tạo ra sự kiện |

## Ranh giới phạm vi với SCRUM-31

| | Trong phạm vi SCRUM-26 (tính năng này) | Trong phạm vi SCRUM-31 |
|---|---|---|
| Trường `correlationId` tồn tại và đúng hình dạng trên event contract | ✅ Đã có sẵn (SCRUM-18), tính năng này chỉ xác nhận | — |
| Outbox pattern (state change và publish không phân kỳ) | Không đổi | ✅ |
| Publisher thật gửi `BasketCheckedOutV1`/`OrderPlacedV1` lên RabbitMQ | Không xây | ✅ |
| Consumer thật xử lý các event này, ghi log có correlation ID | Không xây | ✅ (dùng lại cơ chế `BeginScope` đã có sẵn từ `ServiceDefaults`, không cần cơ chế mới) |
| Jira SCRUM-26 Test Scenario 1 (nhánh async, "OrderPlaced event consumer") | Xác minh được **một phần** (hợp đồng dữ liệu sẵn sàng) | Xác minh **đầy đủ** (transport thật tồn tại) |

## Failure Modes

| Tình huống | Hành vi |
|---|---|
| Một publisher tương lai (SCRUM-31) quên truyền `correlationId` khi dựng payload | Không biên dịch được — `BasketCheckedOutMapper.ToEvent(...)` có `correlationId` là tham số bắt buộc (không có giá trị mặc định); `OrderPlacedV1`'s constructor tương tự sẽ cần cùng kỷ luật khi được viết |
| Schema JSON Schema thiếu trường `correlationId` | Đã được `SchemaValidationTests`/`TolerantReaderTests` (`shared/EventContracts.UnitTests`) chặn từ SCRUM-18 — không đổi bởi tính năng này |

## Stability

Hợp đồng dữ liệu nội bộ giữa các service qua message bất đồng bộ, tuân theo constitution Principle II (contract-first, versioned qua `V1`/`V2` suffix — không sửa `V1` tại chỗ). Không thay đổi bởi tính năng này; sẽ được **thực thi lần đầu ở transport thật** khi SCRUM-31 hoàn thành.
