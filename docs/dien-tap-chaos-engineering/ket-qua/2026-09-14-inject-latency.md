# Bản ghi kết quả bài tập chaos

- **ngay_chay**: 2026-09-14
- **kich_ban**: inject-latency
- **nguoi_thuc_hien**: Claude Sonnet 5 (phiên `/speckit-implement`, dưới sự giám sát của nmhieuit)
- **quan_sat**:
  Chạy trên stack `ecomerce-local` (docker-compose, auth/DB thật) với `Chaos:AllowLatencyInjection=true`
  đặt riêng cho container `orders-api` (biến môi trường, không sửa `appsettings.json` mặc định —
  đúng Bất biến 1 của hợp đồng). Không có cluster Kubernetes khả dụng với đủ hạ tầng (xem bản ghi
  `2026-09-12-kill-pod.md`), nên bước tiêm độ trễ cũng chạy trên container thật thay vì pod k8s —
  middleware/hành vi kiểm chứng ở đây độc lập với runtime k8s hay container (Bất biến của hợp đồng
  không phụ thuộc nền tảng triển khai), nên kết quả có giá trị cho phần này.

  **Kiểm chứng middleware trực tiếp**: `curl -H "X-Chaos-Latency-Ms: 2000" .../health/live` trả lời
  sau ≈2.1s — khớp đúng [contracts/chaos-latency-injection-contract.md](../../specs/025-chaos-pod-kill-latency/contracts/chaos-latency-injection-contract.md).

  **Lần thử 1** (tải tiêm độ trễ trực tiếp vào Orders.Api, 15 request đồng thời, ~35s, header
  `X-Chaos-Latency-Ms: 2000`): tải nền qua BFF (`GET /bff/orders/{id}`, ~2 request/giây) không bị ảnh
  hưởng — toàn bộ 80/80 request `200`/`404` bình thường, không có timeout nào. **Nguyên nhân**:
  `await Task.Delay(...)` không chặn thread — nhiều request đang "chờ" đồng thời không tiêu tốn năng
  lực xử lý thật của Kestrel/threadpool, nên 15 request đồng thời không đủ để ảnh hưởng tới các
  request khác. Đây là một phát hiện thật của bài tập, không phải lỗi thao tác.

  **Lần thử 2** (tăng tải tiêm lên 80 request đồng thời liên tục ~25s+, header
  `X-Chaos-Latency-Ms: 3000`): tải nền qua BFF (90 request tổng) ghi nhận 7 request `504 Gateway
  Timeout` + 2 lần `curl` không kết nối được (`code=000`) rải rác trong ~2 phút quan sát — không tập
  trung thành một cửa sổ liên tục. Log BFF xác nhận Polly's retry (`OrdersApi-standard//Standard-Retry`)
  engage thật với `AttemptTimeout=1s` liên tục bị vượt (`The operation didn't complete within the
  allowed timeout of '00:00:01'`, thời gian thực thi quan sát được 1.0s-2.0s — khớp đúng độ trễ tiêm
  2-3s bị cắt ở AttemptTimeout), retry tới hết số lần cấu hình rồi `The operation was canceled.`
  (TotalRequestTimeout) → BFF trả `504` cho caller của chính nó. Khớp đúng mô tả quickstart.md Bước 2.

  **Circuit breaker KHÔNG mở mạch** ở cả hai lần thử (không tìm thấy `BrokenCircuitException` nào
  trong log BFF dù lần 2 có 7 lỗi thật) — 7 lỗi rải rác trong ~2 phút không đủ mật độ trong bất kỳ cửa
  sổ `CircuitBreakerSamplingDuration` nào để vượt ngưỡng tỷ lệ lỗi tối thiểu đã cấu hình cho
  `OrdersApiClient`. Acceptance Criteria gốc của SCRUM-34 ("circuit breaker trips per its configured
  threshold") không được xác nhận trong lần chạy này.

  **Dashboard SLO (Kibana, Bước 3 quickstart.md)**: KHÔNG kiểm chứng — theo thỏa thuận với người giám
  sát tại thời điểm chọn hướng đi (bỏ qua phần xác minh dashboard Elastic), dù Elasticsearch/Kibana có
  chạy healthy trong stack `ecomerce-local` này.
- **ket_luan**: sai_lech
- **jira_ticket**: (chưa mở — xem ghi chú bên dưới)

## Ghi chú sai lệch

1. **Circuit breaker của `OrdersApiClient` không trip** dù có tải tiêm độ trễ đủ mạnh để gây ra 7 lỗi
   thật (504/timeout) qua BFF. Vì lỗi rải rác theo thời gian thay vì tập trung, ngưỡng lấy mẫu của
   circuit breaker không bị vượt. Cần một lần chạy khác với tải tiêm ĐỒNG BỘ và TẬP TRUNG hơn (ví dụ
   một công cụ load-test thật thay vì vòng lặp `curl` thủ công trong bash) để xác nhận circuit breaker
   thực sự trip theo đúng Acceptance Criteria của SCRUM-34 trước khi coi US2 đã xác nhận đầy đủ.
2. **Không kiểm chứng dashboard SLO Kibana** (SC-003 của spec.md) — bỏ qua theo thỏa thuận với người
   giám sát, không phải giới hạn kỹ thuật (Elastic thực tế đang chạy sẵn trong môi trường này).
3. Tiêm độ trễ chạy trên container Docker thật (không phải pod k8s) do cùng giới hạn hạ tầng đã ghi ở
   bản ghi `2026-09-12-kill-pod.md` — bản thân middleware độc lập với runtime nên kết quả về middleware
   không bị ảnh hưởng, nhưng phần "circuit breaker trip theo ngưỡng cấu hình" nên được chạy lại trên
   một cluster Kubernetes thật với một công cụ tạo tải chuyên dụng.
