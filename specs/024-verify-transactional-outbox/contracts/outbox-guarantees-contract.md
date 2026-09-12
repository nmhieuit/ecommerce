# Hợp đồng: Đảm bảo hành vi của outbox pattern trên order publisher

**Feature**: [../spec.md](../spec.md) | **Date**: 2026-09-11

Không có hợp đồng HTTP/event mới (`OrderPlacedV1` giữ nguyên — xem `data-model.md`). Tài liệu này ghi
lại **bất biến hành vi** mà việc hiện thực outbox trên `orders` phải luôn thoả, để bộ test tích hợp
(`services/orders/tests/Orders.Api.IntegrationTests/OrderPlacedOutbox*.cs`,
`OrderPlacedIdempotentConsumerTests.cs`) có một danh sách ngắn gọn, ổn định để đối chiếu — cùng khuôn
mẫu `contracts/continuous-measurement-contract.md` của 021.

## Bất biến 1 — Ghi nguyên tử (spec FR-001, US1)

Với mọi request `POST /orders` mà transaction CSDL **commit thành công**: tồn tại đúng 1 hàng `Order`
và đúng 1 hàng `OutboxMessage` mang payload `OrderPlacedV1` tương ứng, cả hai được ghi trong cùng 1
transaction. Không có trạng thái trung gian nào (đơn hàng tồn tại nhưng thiếu outbox record, hoặc
ngược lại) từng hiện diện trong CSDL tại bất kỳ thời điểm nào sau khi transaction đó kết thúc.

**Kiểm chứng**: `OrderPlacedOutboxAtomicityTests` — tạo đơn qua HTTP thật, đọc trực tiếp CSDL (không
qua API) ngay sau đó, xác nhận cả hai hàng cùng tồn tại.

## Bất biến 2 — Rollback-safety (spec FR-002, US1 Edge Case)

Nếu transaction tạo đơn hàng bị rollback (lỗi domain, lỗi ràng buộc CSDL, hay bất kỳ lý do gì): không
có hàng `Order` VÀ không có hàng `OutboxMessage` nào cho request đó tồn tại sau khi request kết thúc.

**Kiểm chứng**: `OrderPlacedOutboxAtomicityTests` — buộc một lỗi trong quá trình tạo đơn (ví dụ dòng
hàng không hợp lệ sau khi đã bắt đầu transaction, hoặc lỗi ràng buộc CSDL chèn có chủ đích), xác nhận
không có hàng nào ở cả 2 bảng.

## Bất biến 3 — Phục hồi sau crash, at-least-once (spec FR-003/FR-004, US2)

Nếu một `OutboxMessage` đã được commit (Bất biến 1 đã thoả) nhưng tiến trình `orders` dừng đột ngột
trước khi outbox delivery service gửi nó: lần khởi động tiếp theo của `orders` (bất kể sau bao lâu, bất
kể khởi động lại bao nhiêu lần) PHẢI cuối cùng gửi được message đó ra RabbitMQ, không cần thao tác thủ
công nào ngoài việc tiến trình được khởi động lại.

**Kiểm chứng**: `OrderPlacedOutboxCrashRecoveryTests` — kịch bản 2-host của research.md Quyết định 4.

## Bất biến 4 — Idempotent consumer (spec FR-005, US3)

Nếu cùng một `OrderPlacedV1` (cùng `EventId`) được một consumer nhận từ RabbitMQ hai lần (redelivery
tự nhiên hoặc phát lại thủ công): logic nghiệp vụ của consumer đó chỉ thực sự chạy đúng 1 lần; lần nhận
thứ hai trở đi không tạo thêm bất kỳ tác dụng phụ nào.

**Kiểm chứng**: `OrderPlacedIdempotentConsumerTests` — publish/redeliver cùng 1 message 2 lần vào
consumer xác minh (dùng `InboxState` thật — data-model.md), assert bảng "đã xử lý" chỉ có đúng 1 hàng
cho `EventId` đó.

## Ngoài phạm vi (deliberately not covered)

- Không có bất biến nào về `BasketCheckedOutV1` hay việc tạo đơn qua message thay vì HTTP — nằm ngoài
  phạm vi tính năng này (research.md Quyết định 1; `plan.md` Complexity Tracking).
- Không có bất biến nào về thứ tự tuyệt đối giữa nhiều `OrderPlacedV1` khác nhau — chỉ đảm bảo mỗi
  message riêng lẻ cuối cùng được gửi/xử lý đúng 1 lần theo nghĩa nghiệp vụ.
