# Tổng quan dự án — Nền tảng thương mại điện tử (Ecommerce Platform)

*Đối tượng đọc: quản lý, người phụ trách nghiệp vụ, hoặc bất kỳ ai không đọc code. Không yêu cầu kiến thức kỹ thuật.*

*Tài liệu này là bản tổng quan theo góc nhìn nghiệp vụ. Muốn xem chi tiết từng chức năng, xem thư mục [`docs/chuc-nang-nghiep-vu/`](chuc-nang-nghiep-vu/). Muốn xem kiến trúc kỹ thuật dành cho đội phát triển, xem [`docs/thiet-ke-he-thong.md`](thiet-ke-he-thong.md). Muốn xem tiến độ theo từng đợt phát triển (roadmap), xem [`docs/roadmap.md`](roadmap.md) và [`docs/tai-lieu-tong-quan-danh-cho-quan-ly.md`](tai-lieu-tong-quan-danh-cho-quan-ly.md) (tài liệu cũ, theo dõi theo từng tính năng SCRUM).*

---

## 1. Dự án này là gì

Đây là một **dự án luyện tập cá nhân (solo)** nhằm thực hành quy trình phát triển phần mềm bài bản (SDLC) cho một hệ thống thương mại điện tử, **không phải một hệ thống đang phục vụ khách hàng thật**. Nguồn: [`docs/roadmap.md`](roadmap.md).

Về mặt nghiệp vụ, hệ thống mô phỏng một cửa hàng trực tuyến rất đơn giản, cho phép người mua:

1. Xem danh sách sản phẩm.
2. Thêm sản phẩm vào giỏ hàng.
3. Xác nhận đặt hàng.

Đây là 3 bước tối thiểu của một luồng mua hàng, được gọi là "walking skeleton" (bộ khung tối giản chạy được đầu-cuối) trong roadmap — xem bằng chứng thực tế đã chạy tại [`docs/demo-phase-1.md`](demo-phase-1.md).

## 2. Hệ thống gồm những phần nào (nhìn theo nghiệp vụ, không phải theo code)

| Phần nghiệp vụ | Trả lời câu hỏi | Tài liệu chi tiết |
|---|---|---|
| **Sản phẩm** | Cửa hàng đang bán gì? | [`chuc-nang-nghiep-vu/san-pham.md`](chuc-nang-nghiep-vu/san-pham.md) |
| **Giỏ hàng** | Khách đang định mua gì, trước khi xác nhận? | [`chuc-nang-nghiep-vu/gio-hang.md`](chuc-nang-nghiep-vu/gio-hang.md) |
| **Đặt hàng** | Khách đã xác nhận mua — đơn hàng chính thức là gì? | [`chuc-nang-nghiep-vu/dat-hang.md`](chuc-nang-nghiep-vu/dat-hang.md) |
| **Khách hàng / đối tác** | Ai là người đang giao dịch? | [`chuc-nang-nghiep-vu/khach-hang.md`](chuc-nang-nghiep-vu/khach-hang.md) |

Mỗi phần nghiệp vụ trên được xây dựng thành **một dịch vụ (service) độc lập, tự quản lý dữ liệu riêng của mình** — không dịch vụ nào được phép đọc thẳng vào cơ sở dữ liệu của dịch vụ khác. Đây là một nguyên tắc thiết kế cố định của dự án (xem [`docs/thiet-ke-he-thong.md`](thiet-ke-he-thong.md) để hiểu vì sao). Ngoài 4 phần nghiệp vụ trên, hệ thống còn có 2 lớp kỹ thuật thuần tuý không mang nghiệp vụ riêng: một **cổng vào (Gateway)** và một **lớp tổng hợp cho giao diện web (BFF)** — hai lớp này không lưu dữ liệu, chỉ điều phối request.

## 3. Trạng thái thật hiện tại (quan trọng)

Ba điều quan trọng cần biết trước khi đọc thêm bất kỳ tài liệu nào khác của dự án:

1. **Chỉ 4 dịch vụ nghiệp vụ đã được xây**: Sản phẩm, Giỏ hàng, Đặt hàng, Khách hàng. Roadmap có nhắc tới 2 dịch vụ tương lai là "Vận chuyển" (Logistics) và "Hoá đơn" (Invoices) — **hai dịch vụ này chưa tồn tại**, chỉ mới là kế hoạch.
2. **Đăng nhập hiện là giả lập**: hệ thống chưa có màn hình đăng ký/đăng nhập thật. Mọi request đang được gán cứng cho một "khách hàng giả lập" duy nhất. Vì vậy hiện tại **không thể demo với nhiều tài khoản khách hàng khác nhau**.
3. **Các dịch vụ nghiệp vụ chưa "nói chuyện" với nhau qua sự kiện (event)** — mọi thứ hiện tại đang gọi trực tiếp, đồng bộ (xem [`chuc-nang-nghiep-vu/dat-hang.md`](chuc-nang-nghiep-vu/dat-hang.md) mục rủi ro). Cơ chế hàng đợi sự kiện (RabbitMQ) đã có sẵn trong hạ tầng chạy thử nhưng **chưa có dịch vụ nào thực sự gửi/nhận qua đó**.

Muốn biết chi tiết "cái gì xong thật, cái gì mới viết code nhưng chưa bật" theo từng đợt phát triển, đọc [`docs/tai-lieu-tong-quan-danh-cho-quan-ly.md`](tai-lieu-tong-quan-danh-cho-quan-ly.md) — tài liệu đó theo dõi theo lịch sử từng tính năng (SCRUM-XX), còn tài liệu này chỉ mô tả bức tranh nghiệp vụ hiện tại.

## 4. Đọc tiếp ở đâu

- Muốn hiểu **một chức năng cụ thể làm được gì, giới hạn gì** → vào [`docs/chuc-nang-nghiep-vu/`](chuc-nang-nghiep-vu/), chọn đúng file.
- Muốn hiểu **kiến trúc kỹ thuật, các thành phần bên trong/bên ngoài repo, và luồng xử lý đặt hàng đầu-cuối** → [`docs/thiet-ke-he-thong.md`](thiet-ke-he-thong.md).
- Muốn hiểu **tiến độ, cái gì đã hoàn thành thật theo từng giai đoạn** → [`docs/roadmap.md`](roadmap.md) và [`docs/tai-lieu-tong-quan-danh-cho-quan-ly.md`](tai-lieu-tong-quan-danh-cho-quan-ly.md).
