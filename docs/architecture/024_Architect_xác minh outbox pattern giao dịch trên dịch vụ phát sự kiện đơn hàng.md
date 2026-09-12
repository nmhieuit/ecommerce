# Kiến trúc: Xác minh outbox pattern giao dịch trên dịch vụ phát sự kiện đơn hàng

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-31 ("[RESILIENCE-4] Verify transactional outbox on the order publisher"),
đặc tả tại [`specs/024-verify-transactional-outbox/`](../../specs/024-verify-transactional-outbox/). 5
quyết định kiến trúc (cộng 1 quyết định "Xác nhận hiện trạng") ở
[`research.md`](../../specs/024-verify-transactional-outbox/research.md). Đóng 1 phần Action Item #2
của [ADR-0011](../adr/0011-checkout-orchestration.md) — chỉ vế `OrderPlaced`, ADR đó đã tự cập nhật
mục "Action Items" phản ánh đúng phạm vi này.

**Trạng thái xác minh**: 23/23 task hoàn thành `[X]`. 29/29 test tích hợp PASS thật qua Testcontainers
(SQL Server + RabbitMQ). Cả 3 kịch bản Jira đã xác thực bằng test tự động chạy thật, cộng chạy
`orders-api` thật (`dotnet run`, ngoài `WebApplicationFactory`) kết nối RabbitMQ thật — log xác nhận
`Bus started: rabbitmq://localhost/`. 1 phát hiện thật quan trọng khi triển khai (license MassTransit
v9) chỉ lộ ra ở bước chạy thật này, không bộ test tích hợp nào bắt được — xem
[technical-debt.md](technical-debt.md).

## 1. Kiểm kê hiện trạng trước khi thiết kế (Quyết định 0)

Trước khi code, `research.md` xác nhận trực tiếp bằng cách đọc mã nguồn: không có hạ tầng nhắn tin
thật nào (grep `MassTransit|AddEntityFrameworkOutbox|IPublishEndpoint|IBus` chỉ khớp comment nói "chưa
tồn tại"); `POST /orders` không publish gì sau khi lưu; `OrderPlacedV1` chỉ là hợp đồng, chưa từng
được publish; container RabbitMQ đã có sẵn trong `docker-compose.yml` nhưng comment ghi rõ "nothing
connects to this"; [ADR-0011](../adr/0011-checkout-orchestration.md) đã ghi nhận đây là 1 sai lệch có
ghi nhận, có giới hạn thời gian, so với Principle IV.

## 2. Phạm vi — chỉ đóng outbox của `OrderPlaced`, KHÔNG dựng saga checkout đầy đủ (Quyết định 1)

Tính năng này chỉ hiện thực đúng 3 tiêu chí chấp nhận của Jira (ghi nguyên tử, phục hồi khi crash,
consumer idempotent) cho việc `orders` publish `OrderPlacedV1`. **KHÔNG** chuyển bước "tạo đơn" của
BFF sang tiêu thụ `BasketCheckedOutV1`, **KHÔNG** dùng bước "xoá giỏ hàng" làm consumer thật —
`OrderPlacedV1` không mang `CustomerRef`/`BasketId`, nên dùng nó để lái nghiệp vụ xoá giỏ hàng đòi hỏi
1 phiên bản hợp đồng mới (`OrderPlacedV2`, theo Principle II) và đổi UX xác nhận đơn từ đồng bộ sang
bất đồng bộ — vượt xa 3 tiêu chí chấp nhận thật sự của Jira. [ADR-0011](../adr/0011-checkout-orchestration.md)
giữ nguyên, không đóng — chỉ cập nhật 1 ghi chú.

## 3. Kiến trúc tổng thể

```
POST /orders → Order.PlaceFrom(...) + publish OrderPlacedV1 qua IPublishEndpoint
                    │ (cùng transaction EF Core nhờ MassTransit Bus Outbox — UseBusOutbox())
                    ▼
OrdersDbContext: bảng OutboxMessage/OutboxState/InboxState (MassTransit.EntityFrameworkCore)
                    │
                    ▼ outbox delivery service (hosted, quét theo chu kỳ QueryDelay)
              RabbitMQ (thật, docker-compose.deps.yml — dùng chung, không per-service)
                    │
                    ▼ receive endpoint dùng InboxState khoá theo MessageId (chặn xử lý trùng)
      Consumer xác minh (chỉ tồn tại trong test tích hợp — KHÔNG phải nghiệp vụ thật)
```

## 4. Quyết định kỹ thuật đáng chú ý

| # | Quyết định |
|---|---|
| 2 | `MassTransit` + `MassTransit.EntityFrameworkCore` (Bus Outbox có sẵn của thư viện), không tự viết bảng outbox/`BackgroundService` quét tay — pin dòng 8.x (xem phát hiện license ở [technical-debt.md](technical-debt.md)) |
| 3 | Consumer xác minh idempotency dùng đúng Consumer Outbox/Inbox thật của MassTransit (`InboxState`, khoá theo `MessageId`), chạy trong bộ test tích hợp — không phải logic dedupe tự viết, không phải consumer nghiệp vụ thật |
| 4 | Mô phỏng crash tiến trình bằng 2 `WebApplicationFactory` trỏ cùng 1 cặp Testcontainers: Host A commit rồi `Dispose()` ngay (mô phỏng sập trước khi gửi), Host B khởi động mới quét thấy bản ghi outbox chưa gửi và tự gửi |
| 5 | RabbitMQ dùng chung (khác per-service như `*-db`) thêm vào `docker-compose.deps.yml` — nhất quán với comment sẵn có "the story that first needs one finds it present" |

## 5. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/024-verify-transactional-outbox-component.drawio`](../diagrams/024-verify-transactional-outbox-component.drawio)
- Sơ đồ trình tự (2-host mô phỏng crash giữa commit và publish → khởi động lại → outbox relay tự gửi):
  [`docs/diagrams/024-verify-transactional-outbox-sequence.drawio`](../diagrams/024-verify-transactional-outbox-sequence.drawio)
- Sơ đồ luồng nghiệp vụ đơn giản hoá (đi kèm tài liệu PO):
  [`docs/diagrams/024-verify-transactional-outbox-flow-nghiep-vu.drawio`](../diagrams/024-verify-transactional-outbox-flow-nghiep-vu.drawio)

Phát hiện thật quan trọng khi triển khai (license MassTransit v9) và giới hạn phạm vi đã biết (chỉ
`OrderPlaced`, ADR-0011 vẫn để ngỏ): xem [technical-debt.md](technical-debt.md).
