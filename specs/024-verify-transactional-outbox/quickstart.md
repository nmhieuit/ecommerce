# Quickstart: Xác minh outbox pattern giao dịch trên dịch vụ phát sự kiện đơn hàng

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-11

Hướng dẫn này lặp lại đúng 3 kịch bản kiểm thử của Jira SCRUM-31, dùng làm cách xác thực tính năng đã
hoạt động đúng — cả tự động (CI) lẫn thủ công (local).

## Chạy tự động (CI / local)

Toàn bộ 3 kịch bản đã có bản tự động hoá trong `services/orders/tests/Orders.Api.IntegrationTests`
(xem `contracts/outbox-guarantees-contract.md` để biết bất biến nào ứng với test nào):

```bash
dotnet test services/orders/tests/Orders.Api.IntegrationTests
```

Test dùng Testcontainers thật (SQL Server + RabbitMQ) — không cần cài đặt gì thêm ngoài Docker đang
chạy; không cần `docker compose up` thủ công trước khi chạy `dotnet test`.

## Xác thực thủ công (đối chiếu đúng 3 kịch bản của Jira)

**Chuẩn bị**: khởi động RabbitMQ + CSDL orders cục bộ.

```bash
docker compose -f docker-compose.deps.yml up --wait orders-db-init rabbitmq
```

Chạy `orders-api` cục bộ (kết nối tới 2 dependency trên) theo cách đã có của repo (xem
`docs/architecture/005_Architect_chạy local một lệnh.md`).

### Kịch bản 1 — Kill tiến trình giữa lúc publish

1. Gửi `POST /orders` với ít nhất 1 dòng hàng hợp lệ — xác nhận `201` trả về ngay.
2. **Ngay lập tức** dừng tiến trình `orders-api` (`Ctrl+C`/`docker stop`) — cố gắng dừng trước khi log
   "message delivered" của outbox delivery service xuất hiện (chu kỳ quét mặc định vài giây, đủ để can
   thiệp kịp).
3. Kiểm tra CSDL `orders`: hàng `Order` và hàng `OutboxMessage` (`SentTime` còn rỗng) cùng tồn tại.
4. Khởi động lại `orders-api`.
5. Quan sát RabbitMQ management UI (`http://localhost:15672`, `guest`/`guest`, dưới profile `debug`)
   hoặc log của `orders-api`: message `OrderPlaced` cho đơn hàng ở bước 1 được gửi ra, không cần thao
   tác nào khác ngoài việc khởi động lại.

### Kịch bản 2 — Phát lại (replay) cùng một sự kiện hai lần

1. Với một `OrderPlacedV1` đã publish thành công (ví dụ từ Kịch bản 1), lấy `EventId`/message id của
   nó từ RabbitMQ management UI (tab "Get messages" trên queue của consumer xác minh, hoặc từ log).
2. Republish thủ công cùng payload đó (cùng `MessageId`) vào đúng queue.
3. Xác nhận trong CSDL/log của consumer xác minh: không có tác dụng phụ mới nào được tạo thêm ở lần
   xử lý thứ hai — bảng "đã xử lý" chỉ có đúng 1 hàng cho `EventId` đó (Bất biến 4).

### Kịch bản 3 — Rollback không phát sự kiện

1. Gửi `POST /orders` với payload cố ý gây lỗi sau khi transaction đã bắt đầu nhưng trước khi commit
   (ví dụ một ràng buộc CSDL bị vi phạm có chủ đích trong môi trường test) — xác nhận response lỗi
   (không phải `201`).
2. Kiểm tra CSDL `orders`: không có hàng `Order` nào và không có hàng `OutboxMessage` nào được tạo cho
   yêu cầu đó (Bất biến 2).

## Tiêu chí hoàn thành

Cả 3 kịch bản trên khớp đúng: (a) bộ test tự động trong CI xanh, VÀ (b) lặp lại được thủ công theo các
bước trên với kết quả giống hệt — đúng tinh thần `spec.md` SC-005 ("chạy tự động, lặp lại được... cho
ra kết quả xác định").
