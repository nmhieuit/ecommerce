# ADR-0006: Công cụ kiểm thử hợp đồng do bên tiêu thụ dẫn dắt (Consumer-Driven Contract Testing)

*(Bản dịch tiếng Việt của [`0006-contract-testing-tool.md`](0006-contract-testing-tool.md) — bản gốc
tiếng Anh vẫn được giữ nguyên, không sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn đúng.)*

**Trạng thái:** Đã chấp thuận | **Ngày:** 2026-08-14 | **Người quyết định:** Platform maintainers

## Bối cảnh

Principle III yêu cầu kiểm thử hợp đồng do bên tiêu thụ dẫn dắt (consumer-driven contract test) cho
mọi ranh giới HTTP và event, và rằng "phá vỡ 1 hợp đồng đã publish PHẢI làm fail build của bên sản
xuất (producer)" — nghĩa là việc verify hợp đồng phải chạy ngay trong pipeline CI của chính bên sản
xuất, biết được mọi bên tiêu thụ đang phụ thuộc vào nó.

## Quyết định

Dùng **Pact**, với 1 Pact Broker tự host, và tính năng message-pact của Pact (qua 1 adapter mỏng cho
MassTransit) cho các ranh giới event.

## Các phương án đã cân nhắc

### Phương án A: Pact
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Trung bình — cần chạy Broker, message-pact cần 1 adapter |
| Chi phí | Miễn phí (OSS); Broker tự host là 1 service phụ nhỏ |
| Khả năng mở rộng | Cao |
| Độ quen thuộc của đội | Thấp lúc đầu, cộng đồng hỗ trợ mạnh |

**Ưu điểm:** "Build của bên sản xuất fail khi hợp đồng bị vỡ" chính là workflow cốt lõi của Pact
(`can-i-deploy`), gồm cả việc tự động theo dõi bên tiêu thụ nào phụ thuộc vào bên sản xuất nào — tự
xây bằng tay đồ thị phụ thuộc đó sẽ là 1 công việc lớn hơn nhiều so với việc dùng Pact; SDK .NET
trưởng thành; hỗ trợ HTTP-pact là tính năng hạng nhất.
**Nhược điểm:** Hỗ trợ message/event-pact chưa trưởng thành bằng HTTP-pact và sẽ cần 1 adapter riêng
cho MassTransit; Pact Broker là thêm 1 service có trạng thái cần vận hành (nhẹ hơn 1 schema registry,
nhưng vẫn là hạ tầng).

### Phương án B: Bộ khung kiểm thử hợp đồng tự xây (custom)
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Cao để xây, thấp khi đã xây xong |
| Chi phí | Thời gian kỹ thuật thay vì hạ tầng mới |
| Khả năng mở rộng | N/A |
| Độ quen thuộc của đội | Cao (XUnit thuần + validate bằng JSON Schema) |

**Ưu điểm:** Không hạ tầng mới nào; tái dùng đúng JSON Schema đã chọn ở ADR-0005 cho cả hợp đồng HTTP
lẫn event, nên chỉ có 1 định dạng và 1 cách validate duy nhất ở mọi nơi; không cần học thêm DSL mới
ngoài những gì đội đã dùng.
**Nhược điểm:** Cam kết "build của bên sản xuất fail khi làm vỡ 1 bên tiêu thụ" đòi hỏi tự xây và duy
trì bằng tay 1 bản đồ phụ thuộc liên-service — đúng chính phần khó nhất mà Pact đã giải sẵn; không có
sẵn dashboard cho biết ai phụ thuộc vào cái gì; nhiều công sức kỹ thuật ban đầu hơn, và câu chuyện
onboarding cho nhân sự mới kém chuẩn ngành hơn.

## Phân tích đánh đổi

Yêu cầu ở đây không chỉ là "validate 1 schema" — mà là "biết được bên tiêu thụ nào đang tồn tại và
làm fail đúng build khi 1 trong số đó sẽ bị vỡ", đây là 1 bài toán theo dõi phụ thuộc phân tán
(distributed dependency-tracking). Pact giải trực tiếp bài toán đó; 1 bộ khung tự xây sẽ phải phát
minh lại nó. Chi phí vận hành của Broker được đánh giá nhỏ hơn chi phí kỹ thuật để tự xây việc theo
dõi bên tiêu thụ từ đầu.

## Hệ quả

- 1 Pact Broker phải được triển khai và giữ luôn sẵn sàng — CI phụ thuộc vào việc kết nối được tới nó
  trong lúc build PR.
- Kiểm thử hợp đồng ở ranh giới event mang rủi ro tích hợp (adapter MassTransit) nên được thí điểm
  trên 1 cặp service trước khi triển khai toàn nền tảng.

## Việc cần làm

1. [ ] Dựng 1 Pact Broker tự host
2. [ ] Thí điểm HTTP-pact trên 1 ranh giới BFF↔service trước khi triển khai cho cả 6 service
3. [ ] Xây và verify adapter message-pact MassTransit trên 1 ranh giới event
