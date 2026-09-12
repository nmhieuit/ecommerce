# Hợp đồng: Cơ chế tiêm độ trễ chaos (Chaos Latency Injection)

Áp dụng cho `ChaosLatencyInjectionMiddleware` trong Orders.Api (research.md Quyết định 1;
data-model.md mục 1). Đây là hợp đồng an toàn — vi phạm bất kỳ bất biến nào dưới đây biến một công
cụ diễn tập có kiểm soát thành một rủi ro vận hành thật.

## Bất biến bắt buộc

1. **Mặc định tắt hoàn toàn**: khi `Chaos:AllowLatencyInjection` vắng mặt hoặc `false` (giá trị mặc
   định), middleware KHÔNG được đọc, không được xử lý header `X-Chaos-Latency-Ms` dưới bất kỳ hình
   thức nào — hành vi observable phải giống hệt như middleware không tồn tại. Cấu hình production đã
   commit trong repo KHÔNG BAO GIỜ được đặt giá trị này là `true`.
2. **Không có header = không có độ trễ**: khi `AllowLatencyInjection=true` nhưng request không mang
   header `X-Chaos-Latency-Ms`, request đi qua middleware mà không có bất kỳ độ trễ nhân tạo nào.
3. **Giá trị không hợp lệ = không có độ trễ**: header có mặt nhưng không parse được thành số nguyên
   không âm (ví dụ chuỗi rỗng, chữ, số âm) được coi như vắng mặt (Bất biến 2) — KHÔNG được ném lỗi,
   KHÔNG được làm request thất bại.
4. **Trần an toàn**: giá trị hợp lệ vượt quá `MaxInjectedLatencyMs = 30000` (30 giây) PHẢI bị kẹp
   (clamp) về đúng 30000 — không bao giờ trì hoãn lâu hơn trần này, bất kể giá trị header yêu cầu bao
   nhiêu.
5. **Áp dụng trước xác thực**: middleware PHẢI đăng ký trước `app.UseIdentityValidation()` trong
   `Program.cs` (data-model.md mục 1) — độ trễ mô phỏng đúng độ trễ hạ tầng mà một caller (BFF) chịu,
   không phụ thuộc request có vượt qua được xác thực/authorization hay không.
6. **Không đổi hợp đồng phản hồi**: middleware KHÔNG được thay đổi status code, header, hay nội dung
   response của bất kỳ route nào — chỉ trì hoãn thời điểm response được gửi đi.

## Không thuộc phạm vi hợp đồng này

- Không có yêu cầu về việc `AllowLatencyInjection` phải là `true` ở bất kỳ môi trường nào — việc bật
  nó ở một môi trường diễn tập cụ thể là một quyết định vận hành ngoài phạm vi mã nguồn (spec FR-006).
- Không áp dụng cho service nào khác ngoài Orders.Api — User Story 1 (kill-pod trên `baskets`) không
  cần và không có cơ chế tương đương.

## Quy tắc thay đổi

- Đổi `MaxInjectedLatencyMs` PHẢI cập nhật đồng thời hợp đồng này và test tương ứng
  (`ChaosLatencyInjectionMiddlewareTests`) trong cùng pull request.
- Mở rộng cơ chế này sang một service khác (nếu một chaos exercise tương lai cần) PHẢI thêm một mục
  hợp đồng mới ở đây, không âm thầm tái sử dụng header `X-Chaos-Latency-Ms` với ngữ nghĩa khác.
