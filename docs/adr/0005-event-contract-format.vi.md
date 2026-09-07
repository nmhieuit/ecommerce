# ADR-0005: Định dạng hợp đồng Event/Tích hợp (Event/Integration Contract Format)

*(Bản dịch tiếng Việt của [`0005-event-contract-format.md`](0005-event-contract-format.md) — bản gốc
tiếng Anh vẫn được giữ nguyên, không sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn đúng.)*

**Trạng thái:** Đã chấp thuận | **Ngày:** 2026-08-14 | **Người quyết định:** Platform maintainers

## Bối cảnh

Messaging đã cố định là RabbitMQ qua MassTransit. Principle II yêu cầu schema có version, đặt trong 1
vị trí contract dùng chung; thay đổi phá vỡ tương thích (breaking change) phải mang 1 version tường
minh mới kèm 1 khoảng thời gian deprecation; consumer phải chịu được các field lạ (unknown fields).
Principle IV yêu cầu consumer idempotent, chịu được thứ tự message tới không đúng thứ tự.

## Quyết định

Dùng **JSON Schema**, với các hợp đồng event có version trong 1 package contract dùng chung (không
phải 1 service schema-registry riêng).

## Các phương án đã cân nhắc

### Phương án A: JSON Schema (package contract dùng chung)
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Thấp |
| Chi phí | Không — không cần hạ tầng mới |
| Khả năng mở rộng | N/A (đây là định dạng schema, không phải mối lo runtime) |
| Độ quen thuộc của đội | Cao |

**Ưu điểm:** Serialization mặc định của MassTransit vốn là JSON qua `System.Text.Json`, nên không cần
thêm 1 tầng serialization nào; `System.Text.Json` mặc định bỏ qua các field lạ, tự động đáp ứng yêu cầu
"tolerant reader" (đọc khoan dung) của Principle II mà không cần đội tự xây; đọc được bằng mắt thường
trong management UI và log của RabbitMQ, trực tiếp phục vụ mục tiêu "debug được ngay trên production"
của Principle VII; việc quản lý version dựa vào tên type tường minh (`OrderPlacedV2`) và semver trên
package contract dùng chung, được kiểm tra bằng contract test do bên tiêu thụ dẫn dắt (ADR-0006) thay
vì 1 registry lúc chạy.
**Nhược điểm:** Không có kiểm tra tương thích do registry thực thi tại thời điểm publish — độ an toàn
khi tiến hoá schema dựa vào contract test bắt được lỗi vỡ hợp đồng trong CI, thay vì broker từ chối
publish nếu không tương thích.

### Phương án B: Avro + Schema Registry
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Cao — thêm 1 service có trạng thái mới cần vận hành |
| Chi phí | Chi phí vận hành cho 1 service HA mới |
| Khả năng mở rộng | Cao |
| Độ quen thuộc của đội | Thấp |

**Ưu điểm:** Registry thực thi tương thích xuôi/ngược tập trung ngay tại thời điểm publish, không chỉ
lúc test; mã hoá nhị phân gọn.
**Nhược điểm:** Cần triển khai và vận hành hẳn 1 service có trạng thái mới với các mối lo HA/backup
riêng, cho 1 nền tảng mà lưu lượng event không cần tới độ gọn nhị phân; payload nhị phân khó xem hơn
nhiều trong UI của RabbitMQ hay log có cấu trúc, đi ngược Principle VII; tích hợp Avro của MassTransit
không "hạng nhất" bằng hỗ trợ JSON gốc của nó.

### Phương án C: Protobuf
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Cao |
| Chi phí | Chi phí vận hành tương tự Avro |
| Khả năng mở rộng | Cao |
| Độ quen thuộc của đội | Thấp |

**Ưu điểm:** Tiến hoá schema mạnh nhờ số hiệu field (field number); gọn.
**Nhược điểm:** Cùng nhược điểm khó debug như Avro; không có gRPC ở bất kỳ đâu khác trong stack này để
biện minh cho việc dùng IDL; đội sẽ phải duy trì 2 "ngôn ngữ" hợp đồng khác nhau (OpenAPI/JSON Schema
cho HTTP, Protobuf IDL cho event) thay vì 1 họ JSON Schema nhất quán cho cả 2 loại hợp đồng.

## Phân tích đánh đổi

Tương thích do registry thực thi của Avro và Protobuf là 1 lưới an toàn thật sự, nhưng phải trả giá
bằng 1 service có trạng thái mới và sự thụt lùi về khả năng debug, cho 1 nền tảng chưa có lưu lượng
event đủ lớn để cần mã hoá nhị phân. JSON Schema, kết hợp với contract test đã bắt buộc sẵn theo
Principle III, đạt được độ an toàn tương đương mà không cần hạ tầng mới và không hy sinh khả năng đọc
log/UI.

## Hệ quả

- Độ an toàn trước việc vỡ hợp đồng phụ thuộc vào việc contract test trong CI thực sự chạy trên mọi
  thay đổi — không có lớp chặn ở tầng runtime như 1 registry sẽ cung cấp.
- OpenAPI (HTTP) và hợp đồng event đều nằm trong cùng họ JSON Schema, nên công cụ (validator, trình
  sinh tài liệu) dùng chung được cho cả 2 loại hợp đồng.

## Việc cần làm

1. [ ] Tạo vị trí package/repo contract dùng chung cho schema event
2. [ ] Thiết lập quy ước đặt version `TypeNameVN` và ghi lại chính sách khoảng thời gian deprecation
