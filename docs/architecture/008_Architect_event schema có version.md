# Kiến trúc: Event schema có version — OrderPlaced, BasketCheckedOut

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-18 ("[CONTRACT-2] Versioned event schemas"), đặc tả tại
[`specs/008-versioned-event-schemas/`](../../specs/008-versioned-event-schemas/). Quyết định kiến
trúc gốc: [ADR-0005](../adr/0005-event-contract-format.md) (chọn JSON Schema trong một package chia
sẻ, không phải registry service riêng). Đóng lại 2 action item còn mở của ADR-0005.

**Trạng thái xác minh**: `tasks.md` ghi 21 task hoàn thành qua 3 user story, mỗi story có checkpoint
riêng — T020 xác nhận `dotnet test shared/EventContracts.UnitTests --filter
"FullyQualifiedName~SchemaValidationTests|FullyQualifiedName~TolerantReaderTests"` pass toàn bộ; T021
chạy lại toàn bộ suite mới, xác nhận không có hiệu ứng chéo giữa 3 story.

## 1. Phạm vi — chỉ tầng hợp đồng, KHÔNG có wiring broker

`research.md` Decision 1 giới hạn phạm vi tường minh: **chỉ tầng hợp đồng, không đấu nối broker.**
`tasks.md` ghi rõ: "No existing service (`Orders.Api`, `Baskets.Api`, `Bff.Api`) is touched by any
task — this feature is scoped entirely to `shared/EventContracts` and its test project." Đây là một
thư viện hợp đồng thuần tuý, chưa có publisher/consumer thật nào trong hệ thống đang chạy — nhất quán
với ADR-0011 (checkout vẫn đồng bộ vì chưa có hạ tầng messaging) và
[005-one-command-local-run](../../specs/005-one-command-local-run/) (RabbitMQ chạy sẵn nhưng chưa ai
kết nối).

## 2. Quyết định kỹ thuật đáng chú ý (research.md)

| # | Quyết định |
|---|---|
| 1 | Giới hạn phạm vi ở tầng hợp đồng; không đấu nối broker |
| 2 | JSON Schema 2020-12, viết tay, có C# record phản chiếu đúng schema |
| 3 | Các phiên bản schema đã công bố là BẤT BIẾN, thực thi bằng một test "đóng băng nội dung" |
| 4 | Không có test riêng cho quy ước đặt tên |

Quyết định 3 là cơ chế thực thi cốt lõi của FR-003/FR-006: một test so sánh nội dung schema đã công
bố với phiên bản đang có trong mã nguồn — nếu ai đó sửa trực tiếp một phiên bản đã công bố (thay vì
tạo phiên bản mới), test đó thất bại ngay, chặn merge.

## 3. Cấu trúc

- `shared/EventContracts/` — package mới, chứa JSON Schema + C# record mirror cho `OrderPlaced` và
  `BasketCheckedOut`, mỗi event có version tường minh.
- `shared/EventContracts.UnitTests/` — `SchemaImmutabilityTests` (US2, chặn breaking change không
  version), `SchemaValidationTests` + `TolerantReaderTests` (US3, xác nhận consumer cũ đọc được event
  mới có field lạ mà không sập).

## 4. Giới hạn phạm vi đã biết

- **Chưa có publisher/consumer thật nào** — đây là hợp đồng đứng riêng, chờ tính năng đấu nối
  messaging thật (ngoài phạm vi feature này) tới dùng.
- Chỉ đúng hai event (`OrderPlaced`, `BasketCheckedOut`) nằm trong phạm vi — event khác trong tương
  lai cần lặp lại đúng khuôn mẫu này riêng, không tự động áp dụng.
- Khoảng thời gian deprecation cụ thể (bao nhiêu ngày/chu kỳ release) là một chi tiết chính sách/tài
  liệu, không phải một con số cố định trong đặc tả — chỉ yêu cầu nó tồn tại, được ghi lại, và được
  tôn trọng.

## 5. Sơ đồ

Cấu trúc hợp đồng và luồng producer/consumer tương lai:
[`docs/diagrams/008-versioned-event-schemas-component.drawio`](../diagrams/008-versioned-event-schemas-component.drawio).
