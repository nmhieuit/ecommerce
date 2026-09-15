# Chủ động "phá" hệ thống trong tầm kiểm soát — để biết chắc lưới an toàn có thật hay chỉ trên giấy

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành — 16/16 hạng mục công việc, xác minh bằng cách thực sự chạy 4 lần trên hạ
tầng thật. Kết quả trung thực: cả 3 lần ghi nhận đều kết luận "phát hiện sai lệch", không phải "đạt" —
xem [functional-debt.md](functional-debt.md), đây là đúng giá trị của việc chủ động thử, không phải
thất bại của tính năng.*

## Vấn đề trước đây

Nhiều năng lực "tự phục hồi khi có sự cố" (khi 1 bộ phận gặp sự cố, hệ thống tự bảo vệ và tự phục hồi;
khi có dấu hiệu quá tải, có nơi hiển thị rõ ràng) đã được xây dựng ở các tính năng trước. Nhưng chưa
ai từng **chủ động** gây ra 1 sự cố thật để xem những năng lực đó có thực sự hoạt động hay không — mọi
thứ vẫn chỉ là điều đã được cấu hình đúng trên giấy, chưa được chứng minh bằng thực nghiệm.

## Giải pháp: chủ động gây sự cố có kiểm soát, trong môi trường dành riêng cho việc đó

- **Chủ động xoá 1 bộ phận đang chạy** (giống như rút phích cắm) trong lúc hệ thống có 1 lượng người
  dùng giả lập, rồi quan sát: hệ thống có tự khởi động lại bộ phận đó không, và các bộ phận khác có tự
  bảo vệ mình khi bộ phận kia chưa sẵn sàng trở lại hay không.
- **Chủ động làm chậm 1 bộ phận** một cách có kiểm soát, dừng được ngay lập tức khi cần, để xem công cụ
  giám sát có kịp hiển thị việc "ngân sách hiệu năng đang bị tiêu hao" gần với thời gian thực hay
  không.
- **Mọi lần thử đều được ghi lại thành 1 kết luận rõ ràng** — hoặc xác nhận mọi thứ hoạt động đúng, hoặc
  trở thành 1 việc cần xử lý — không để buổi thử trôi qua rồi bị quên.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Đội chủ động xoá 1 bộ phận đang chạy** trong lúc có traffic giả lập — quan sát hệ thống có tự khởi
   động lại bộ phận thay thế hay không, và các bộ phận gọi tới nó có phản ứng ra sao trong lúc chờ.
2. **Đội chủ động làm chậm 1 bộ phận khác**, xem công cụ giám sát có hiển thị kịp thời việc "sắp vượt
   ngưỡng cam kết" hay không — rồi dừng lại, xác nhận mọi thứ trở về bình thường không cần can thiệp gì
   thêm.
3. **Mỗi lần thử đều được ghi thành 1 bản kết luận** — nêu rõ đã thử gì, quan sát được gì, và kết luận
   cuối cùng là "hoạt động đúng như kỳ vọng" hay "phát hiện chỗ chưa đúng, cần xử lý".
4. **Có thể tra lại lịch sử mọi lần đã thử trước đó**, không chỉ dựa vào trí nhớ của người thực hiện.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/025-chaos-pod-kill-latency-flow-nghiep-vu.drawio`](../diagrams/025-chaos-pod-kill-latency-flow-nghiep-vu.drawio))*

## Lợi ích kinh doanh

- **Biết chắc, không phải tin chắc** — lưới an toàn đã xây từ trước được chứng minh bằng sự cố thật,
  không chỉ bằng cấu hình đúng trên giấy.
- **Phát hiện sớm những chỗ chưa đúng như kỳ vọng**, trước khi 1 sự cố thật ngoài kế hoạch xảy ra với
  khách hàng thật.
- **Có thói quen và quy trình lặp lại được** cho việc kiểm chứng định kỳ, không phải 1 lần làm rồi thôi.

Kết quả thật (bao gồm phát hiện quan trọng: 1 lưới an toàn chưa từng kích hoạt qua 4 lần thử) và giới
hạn hiện tại: xem [functional-debt.md](functional-debt.md). Chi tiết kỹ thuật đầy đủ dành cho đội kỹ
thuật, xem
[`docs/architecture/025_Architect_diễn tập chaos engineering giết pod tiêm độ trễ.md`](../architecture/025_Architect_diễn%20tập%20chaos%20engineering%20giết%20pod%20tiêm%20độ%20trễ.md).
