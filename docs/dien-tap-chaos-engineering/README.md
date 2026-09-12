# Diễn tập chaos engineering

Runbook cho [SCRUM-34](https://nmhieuit.atlassian.net/browse/SCRUM-34) — chủ động giết một pod hoặc
tiêm độ trễ vào hệ thống đang chạy để kiểm chứng bằng thực nghiệm rằng các lưới an toàn resilience
(020) và ngân sách SLO (021) hoạt động đúng, không chỉ đúng trên giấy.

Đây là thư mục tài liệu vận hành sống — tích luỹ dần theo mỗi lần bài tập được chạy — khác với
`specs/025-chaos-pod-kill-latency/`, nơi giữ hồ sơ thiết kế một lần (spec/plan/tasks) của tính năng
đã xây dựng ra công cụ tiêm lỗi dùng ở đây.

## Cách chạy một bài tập

Làm theo từng bước tại
[specs/025-chaos-pod-kill-latency/quickstart.md](../../specs/025-chaos-pod-kill-latency/quickstart.md)
— không lặp lại nội dung ở đây để tránh hai nơi lệch nhau theo thời gian. Tóm tắt hai kịch bản:

- **kill-pod**: xóa một pod đang chạy của `baskets` trong lúc có tải nhẹ, quan sát Kubernetes tái
  lập lịch và circuit breaker/retry của BFF engage.
- **inject-latency**: bật `Chaos:AllowLatencyInjection=true` trên môi trường diễn tập, gửi header
  `X-Chaos-Latency-Ms` tới Orders.Api, quan sát dashboard SLO (021) thể hiện ngân sách bị tiêu hao.

Sau khi chạy, điền [mau-ket-qua.md](./mau-ket-qua.md) thành một file mới trong
[ket-qua/](./ket-qua/) và thêm vào mục "Lịch sử chạy" dưới đây (mới nhất trước).

## Lịch sử chạy

_Chưa có bài tập nào được ghi nhận._
