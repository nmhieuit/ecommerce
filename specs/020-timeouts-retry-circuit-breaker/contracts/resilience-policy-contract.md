# Hợp đồng: Resilience Policy cho một điểm gọi ra ngoài

Áp dụng cho MỌI điểm gọi ra ngoài trong hệ thống — hiện tại (gateway→BFF, BFF→4 domain service,
6 điểm gọi JwtBearer backchannel) và tương lai (bao gồm cuộc gọi service→broker mà SCRUM-31 sẽ thêm).
Một điểm gọi ra ngoài mới KHÔNG được coi là hoàn chỉnh cho tới khi thỏa mãn hợp đồng này.

## Bắt buộc

1. **Timeout tường minh**: điểm gọi PHẢI khai báo một giá trị timeout hữu hạn — cho cuộc gọi HTTP là
   `AttemptTimeout` và `TotalRequestTimeout`; cho cuộc gọi gửi message tới broker (khi SCRUM-31 hiện
   thực) là timeout tương đương của thao tác publish (không phải timeout xử lý phía consumer). Giá
   trị `Timeout.InfiniteTimeSpan`, giá trị rỗng/null, hoặc việc không cấu hình gì (im lặng dùng mặc
   định ẩn của thư viện) đều VI PHẠM hợp đồng này.
2. **Circuit breaker**: điểm gọi PHẢI có một cơ chế tự động ngừng gọi tới dependency đang liên tục
   lỗi và fail-fast cho tới khi dependency có dấu hiệu phục hồi. Cơ chế cụ thể có thể là Polly
   circuit breaker (`Microsoft.Extensions.Http.Resilience`) hoặc cơ chế tương đương của lớp hạ tầng
   (ví dụ YARP passive health check ở tầng gateway — research.md Decision 3) — miễn là thỏa hai tính
   chất: (a) mở mạch theo một ngưỡng lỗi/tỷ lệ lỗi có thể cấu hình, và (b) tự thử lại (half-open hoặc
   tương đương) sau một khoảng thời gian, không cần can thiệp thủ công.
3. **Retry có điều kiện, không mù**: retry CHỈ được bật cho các thao tác mà việc thực thi lại không
   gây trùng lặp side effect — trong thực tế hiện tại của hệ thống, nghĩa là giới hạn theo HTTP method
   an toàn (`GET`, `HEAD`) trừ khi điểm gọi có cơ chế idempotency key rõ ràng. Một điểm gọi ghi dữ
   liệu (`POST`/`PUT`/`PATCH`/`DELETE` không có idempotency key) PHẢI tắt retry, không phải "retry rồi
   chấp nhận rủi ro trùng lặp".
4. **Không thay đổi hợp đồng phản hồi khi khỏe mạnh**: việc thêm resilience policy KHÔNG được thay
   đổi hành vi/hợp đồng phản hồi của cuộc gọi khi dependney đích đang hoạt động bình thường — chỉ bổ
   sung hành vi bảo vệ khi có sự cố (spec FR-009).

## Khuyến nghị (không bắt buộc nhưng nên có)

- Sự kiện resilience (timeout xảy ra, một lần retry, circuit breaker đổi trạng thái) nên phát ra qua
  cùng cơ chế đo lường/tracing dùng chung của platform (`ServiceDefaults` — research.md Decision 6),
  không log rời rạc theo cách riêng của từng service.
- Ngưỡng cụ thể (thời lượng timeout, số lần retry, ngưỡng mở mạch) nên được đặt tương xứng với SLO
  của dependency đích (constitution Principle VIII) thay vì một giá trị copy y nguyên từ điểm gọi
  khác không cùng đặc tính.

## Ghi chú cho SCRUM-31 (service → broker, chưa hiện thực)

Khi outbox/publisher cho `BasketCheckedOutV1` (hoặc bất kỳ event nào khác) được xây dựng, thao tác
publish tới RabbitMQ PHẢI tuân theo Mục "Bắt buộc" ở trên: một timeout tường minh cho thao tác gửi,
một cơ chế circuit breaker khi broker không khả dụng, và retry có backoff cho lỗi kết nối tạm thời
(publish tới broker về bản chất được coi là idempotent-safe để retry nếu bản thân outbox pattern đã
đảm bảo publish-at-least-once với event id ổn định — xem `eventId` trong
`BasketCheckedOutMapper.ToEvent`, vốn đã được thiết kế sẵn cho việc consumer có thể loại bỏ bản gửi
lại trùng lặp).
