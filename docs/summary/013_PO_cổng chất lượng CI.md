# Cổng chất lượng tự động — Không có mã kém chất lượng nào lọt vào hệ thống chính

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành và đang hoạt động thật (không phải bản demo/thử nghiệm) trên kho mã nguồn
của dự án.*

## Vấn đề trước đây

Trước tính năng này, việc kiểm tra chất lượng một thay đổi mã nguồn trước khi đưa vào hệ thống chính
phụ thuộc vào việc **con người tự nhớ để làm**: tự chạy kiểm tra, tự đọc kết quả, tự quyết định có
đủ tốt để đưa vào hay không. Cách làm này có ba điểm yếu:

- **Dễ quên hoặc bỏ qua khi vội** — nhất là khi có áp lực thời gian, bước kiểm tra thủ công là bước
  đầu tiên bị "linh động" bỏ qua.
- **Không nhất quán giữa người này với người khác** — mỗi người có thể có tiêu chuẩn khác nhau về
  "đủ tốt".
- **Không có cách nào đảm bảo tuyệt đối** — kể cả người có quyền cao nhất trong hệ thống vẫn có thể
  đưa một thay đổi chưa đạt vào, dù cố ý hay vô ý.

## Giải pháp: một người gác cổng không bao giờ mệt, không bao giờ quên, và không ai "xin qua" được

Giờ đây, **mọi thay đổi mã nguồn muốn được đưa vào hệ thống chính đều phải tự động đi qua một chuỗi
kiểm tra**, không cần ai phải nhớ để bấm nút, và — quan trọng nhất — **không có ai có thể yêu cầu bỏ
qua bước kiểm tra này, kể cả người quản trị cao nhất**. Đây là điểm khác biệt cốt lõi so với việc chỉ
"khuyến nghị nên kiểm tra": đây là một **quy tắc cứng, được máy móc thực thi**, không phải một chính
sách dựa vào ý thức tự giác.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Một người trong đội đề xuất một thay đổi** (mở một "yêu cầu thay đổi" trên hệ thống quản lý mã
   nguồn).
2. **Hệ thống tự động kiểm tra ngay lập tức**, không cần ai bấm nút gì cả — gồm nhiều vòng kiểm tra
   liên tiếp, trong đó có một vòng chuyên về **chất lượng mã nguồn** (đo mức độ được kiểm thử, mức
   trùng lặp mã, và các vấn đề mới phát sinh).
3. **Nếu đạt tất cả**: thay đổi đó được phép đưa vào hệ thống chính.
4. **Nếu không đạt**: việc đưa vào bị chặn ngay lập tức, và **lý do bị chặn được hiển thị rõ ràng
   ngay tại chỗ** — người đề xuất không cần đi tìm ở bất kỳ công cụ nào khác để biết vì sao.
5. **Người đề xuất sửa lại vấn đề được chỉ ra và gửi lại.**
6. **Hệ thống tự động kiểm tra lại từ đầu** — vẫn không cần ai nhắc hay bấm nút "kiểm tra lại" thủ
   công. Nếu lần này đạt, việc chặn được gỡ bỏ ngay trong đúng một lượt kiểm tra tiếp theo.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/013-quality-gate-flow-nghiep-vu.drawio`](../diagrams/013-quality-gate-flow-nghiep-vu.drawio))*

## Điều đặc biệt: không có "đường vòng" cho bất kỳ ai

Đây là điểm đã được **kiểm chứng thật**, không chỉ là thiết kế trên giấy: đội đã thử nghiệm bằng cách
cố tình tạo ra một thay đổi không đạt chuẩn, sau đó cố gắng đưa nó vào hệ thống chính **bằng chính
tài khoản có quyền cao nhất** (chủ sở hữu kho mã nguồn). Kết quả: nút "đưa vào hệ thống chính" bị vô
hiệu hoá hoàn toàn, và **không hề có bất kỳ tuỳ chọn "cứ làm bất chấp" nào xuất hiện ở đâu cả** —
không phải bị ẩn, mà thực sự không tồn tại trong luồng thao tác.

## Lợi ích kinh doanh

- **Giảm rủi ro**: mã chất lượng thấp — thiếu kiểm thử, trùng lặp, có vấn đề tiềm ẩn — không còn cơ
  hội lọt vào hệ thống chính, kể cả trong tình huống vội vàng hay do sơ suất.
- **Giảm thời gian review thủ công**: người review không còn phải tự tay dò từng chỉ số chất lượng —
  hệ thống đã tự làm việc đó và trình bày kết quả sẵn.
- **Minh bạch cho mọi người**: bất kỳ ai xem một yêu cầu thay đổi đều thấy ngay tình trạng chất lượng
  của nó, không cần hỏi ai hay đăng nhập vào một hệ thống đo lường riêng.
- **Vòng lặp sửa lỗi nhanh**: từ lúc phát hiện vấn đề tới lúc được duyệt lại là hoàn toàn tự động —
  không có bước chờ đợi hay xin duyệt lại thủ công nào chen giữa.

## Giới hạn hiện tại — trung thực cần biết

- **Đây là môi trường thử nghiệm cục bộ**, không phải hạ tầng vận hành chính thức (production) trên
  máy chủ chuyên dụng. Việc cơ chế này *hoạt động đúng* đã được kiểm chứng đầy đủ; việc *dựng nó trên
  hạ tầng vận hành chính thức lâu dài* là một công việc riêng, chưa nằm trong phạm vi đã hoàn thành.
- Một số điều chỉnh kỹ thuật đã được xác minh hoạt động tốt trong quá trình thử nghiệm nhưng **hiện
  chưa được đưa chính thức vào nhánh mã nguồn chính** — chi tiết dành cho đội kỹ thuật, xem
  [`docs/architecture/013_Architect_cổng chất lượng CI.md`](../architecture/013_Architect_cổng%20chất%20lượng%20CI.md).
  Điều này không ảnh hưởng tới việc cơ chế chặn merge đã hoạt động thật trên các thay đổi thật.
