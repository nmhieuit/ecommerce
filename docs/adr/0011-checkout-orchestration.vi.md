# ADR-0011: Điều phối Checkout Giai đoạn 1 (Phase 1)

*(Bản dịch tiếng Việt của [`0011-checkout-orchestration.md`](0011-checkout-orchestration.md) — bản
gốc tiếng Anh vẫn được giữ nguyên, không sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn đúng.)*

**Trạng thái:** Đã chấp thuận | **Ngày:** 2026-08-16 | **Người quyết định:** Platform maintainers
**Thay thế:** không gì cả. **Bị thay thế bởi:** chưa có gì — dự kiến sẽ được thay thế khi SCRUM-18 và
SCRUM-31 hoàn thành.

## Bối cảnh

[SCRUM-14](https://nmhieuit.atlassian.net/browse/SCRUM-14) khép lại "walking skeleton": 1 người mua
biến giỏ hàng của họ thành 1 đơn hàng và được hiển thị 1 xác nhận. Do đó checkout trải dài qua 2
service — 1 đơn hàng phải được tạo trong `orders`, và giỏ hàng phải được làm rỗng trong `baskets`.

Principle IV của constitution nói rõ về hình dạng này: "Workflow đa-service PHẢI được mô hình hoá
thành saga với bù trừ (compensation) tường minh, không bao giờ dùng transaction phân tán." Nó cũng
yêu cầu mẫu hình transactional outbox cho mọi publisher.

Repository chưa có bất kỳ cơ chế nào mà quy tắc đó giả định phải có sẵn. Không có tham chiếu package
MassTransit, không có container RabbitMQ trong `docker-compose.deps.yml`, không có bảng outbox trong
bất kỳ migration nào, và không có schema event nào ở bất cứ đâu. Roadmap đặt schema event ở
[SCRUM-18](https://nmhieuit.atlassian.net/browse/SCRUM-18) (Giai đoạn 2) và verify outbox ở
[SCRUM-31](https://nmhieuit.atlassian.net/browse/SCRUM-31) (Giai đoạn 4).

## Quyết định

Checkout là 1 quy trình **đồng bộ 2 bước, do BFF điều phối**: đọc giỏ hàng của người gọi, tạo 1 đơn
hàng cho các dòng hàng của nó, rồi làm rỗng giỏ hàng. Đơn hàng được tạo **trước khi** giỏ hàng được
làm rỗng.

Đây là 1 **sai lệch có ghi nhận, có giới hạn thời gian, so với Principle IV**, được khép lại bởi
SCRUM-18 và SCRUM-31.

## Các phương án đã cân nhắc

### Phương án A: Điều phối đồng bộ bởi BFF, tạo đơn trước *(đã chọn)*

| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Thấp — 3 client đã có kiểu tường minh (typed client), không hạ tầng mới |
| Chi phí | Miễn phí |
| Khả năng mở rộng | Đủ dùng cho 1 tenant và 1 người mua |
| Độ quen thuộc của đội | Cao |

**Ưu điểm:** Ra mắt được walking skeleton ngay, đây chính là toàn bộ trọng tâm của Giai đoạn 1. Dùng
pipeline chịu lỗi (resilience pipeline) mà BFF đã áp dụng sẵn cho mọi lời gọi ra ngoài, nên không đưa
thêm chờ đợi vô hạn nào. Thứ tự tạo-đơn-trước nghĩa là 1 lỗi giữa 2 bước để lại cho người mua **1 đơn
hàng thật họ có thể được cho xem** — có thể khôi phục — thay vì 1 giỏ hàng rỗng và không có gì để cho
xem.

**Nhược điểm:** Không có bù trừ. Nếu bước làm rỗng thất bại sau khi đơn hàng đã được tạo, giỏ hàng vẫn
giữ nguyên các mục của nó và 1 lượt checkout thứ 2 sẽ tạo ra 1 đơn hàng thứ 2. BFF nắm giữ 1 workflow,
điều này nằm không thoải mái bên cạnh nhiệm vụ "chỉ tổng hợp" của nó.

### Phương án B: Dựng RabbitMQ + MassTransit và publish `BasketCheckedOut`

| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Cao — broker mới, package mới, bảng outbox, consumer, idempotency |
| Chi phí | Miễn phí (OSS), nhưng bề mặt vận hành thật sự |
| Khả năng mở rộng | Cao |
| Độ quen thuộc của đội | Thấp |

**Ưu điểm:** Đúng theo hiến pháp (constitution). Bù trừ và idempotency trở thành thuộc tính của thiết
kế thay vì thứ phải xin lỗi vì thiếu.

**Nhược điểm:** Nhân kích thước của tính năng này lên nhiều lần, và trùng lặp công việc mà SCRUM-18 và
SCRUM-31 vốn tồn tại để làm đúng cách. Đích đến đúng, nhưng sai giai đoạn.

### Phương án C: Làm rỗng giỏ hàng trước, rồi mới tạo đơn hàng

**Ưu điểm:** Đối xứng với Phương án A; không thêm cơ chế nào.

**Nhược điểm:** Kiểu lỗi tệ hơn hẳn. 1 lỗi giữa 2 bước làm mất giỏ hàng của người mua *và* để họ không
có đơn hàng nào — kết quả duy nhất không thể khôi phục được từ phía người mua.

## Phân tích đánh đổi

Yếu tố quyết định là hạ tầng của Phương án B đã được giao cho 2 story sau này với tiêu chí chấp nhận
riêng của chúng. Xây 1 phiên bản nửa vời ở đây sẽ hoặc bị gỡ bỏ khi 2 story đó ra mắt, hoặc âm thầm
trở thành phiên bản được ra mắt thật — cả 2 đều không phải kết quả tốt.

Giữa A và C, tranh luận hoàn toàn xoay quanh việc người mua có thể khôi phục từ lỗi nào. 1 đơn hàng đã
tồn tại là thứ có thể được cho xem, báo giá, và được hỗ trợ. 1 giỏ hàng bị làm rỗng cho 1 đơn hàng
chưa bao giờ được tạo là vô hình và không thể khôi phục đối với họ.

## Hệ quả

- **Rủi ro còn lại, được chấp nhận:** 1 lượt làm rỗng thất bại sau khi đơn hàng đã được tạo để lại 1
  giỏ hàng không rỗng. Do đó đảm bảo "không có đơn hàng thứ 2" của FR-016 dựa vào 2 lớp bảo vệ khác,
  không phải bù trừ:
  1. storefront vô hiệu hoá nút checkout trong lúc 1 lượt checkout đang diễn ra, nên cú click thứ 2
     không bao giờ trở thành request thứ 2;
  2. service baskets trả về `409` cho 1 lượt làm rỗng trên giỏ hàng đã rỗng sẵn, nên 1 lượt checkout
     lặp lại bị từ chối.
- Lỗi được ghi log, không im lặng. `CheckoutEndpoints.LogBasketNotCleared` ghi lại mã định danh đơn
  hàng bất cứ khi nào giỏ hàng báo không có gì để làm rỗng, nên khoảng trống này grep được trên
  production thay vì vô hình.
- BFF không thực hiện phép tính nào trong luồng này. Các dòng hàng của giỏ hàng tới từ `baskets` và
  tổng tiền đơn hàng được tính bởi `orders`, nên "chỉ tổng hợp" vẫn đúng cho phần tiền dù workflow thì
  không.

## Việc cần làm

1. [x] SCRUM-18: định nghĩa schema event `BasketCheckedOut` / `OrderPlaced` tại vị trí contract dùng
   chung
2. [~] SCRUM-31 (024-verify-transactional-outbox): đã hiện thực outbox pattern giao dịch cho `orders`
   publish `OrderPlaced` — ghi nguyên tử, phục hồi sau crash, consumer idempotent, đều xác minh bằng
   test tích hợp thật (Testcontainers). **Chưa** thay điều phối checkout này bằng saga đầy đủ, **chưa**
   publish/consume `BasketCheckedOut` — quyết định phạm vi có chủ ý (xem
   `specs/024-verify-transactional-outbox/research.md` Quyết định 1): `OrderPlacedV1` không mang định
   danh khách hàng/giỏ hàng nên không đủ để lái nghiệp vụ "xoá giỏ hàng" mà không đổi hợp đồng sự kiện
   (`OrderPlacedV2`) — vượt phạm vi 3 tiêu chí chấp nhận thật sự của Jira SCRUM-31. Điều phối 2 bước
   đồng bộ ở ADR này vẫn còn nguyên.
3. [ ] Việc còn lại để đóng hẳn ADR này (chưa có story riêng): thay bước "tạo đơn" của BFF bằng việc
   `orders` consume `BasketCheckedOut`, rồi mới đánh dấu ADR này đã bị thay thế và gỡ sai lệch này khỏi
   mục Complexity Tracking của `specs/004-minimal-shopping-spa/plan.md`
