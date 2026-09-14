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

  **Lần thử 3 (bổ sung sau, trên cluster Kubernetes THẬT)**: vượt qua được giới hạn image-loading đã
  ghi ở bản ghi `2026-09-12-kill-pod.md` bằng kỹ thuật `dotnet publish` + `kubectl cp` +
  `kubectl exec` (xem `2026-09-14-kill-pod.md` để biết chi tiết kỹ thuật). Orders.Api chạy như một
  Pod Kubernetes thật (`orders-788d88d7cb-pmf88`) với `Chaos__AllowLatencyInjection=true`, BFF
  (container Docker, auth JWT thật) gọi vào qua `kubectl port-forward` + `host.docker.internal`.
  Xác nhận middleware hoạt động đúng trên chính pod k8s: `curl -H "X-Chaos-Latency-Ms: 2000"` trả lời
  sau ≈2.35s. Chạy tải nền qua BFF (`GET /bff/orders/{id}`, 100 request, ~0.3s/vòng) đồng thời với
  luồng tiêm độ trễ tập trung (100 request đồng thời liên tục ~30s, header
  `X-Chaos-Latency-Ms: 3000`, gửi trực tiếp vào pod qua port-forward): chỉ 1/100 request qua BFF lỗi
  (`504`) — YẾU HƠN lần thử 2 trên container Docker (7/90 lỗi) dù mức tải tiêm cao hơn (100 so với
  80 request đồng thời). Log BFF vẫn xác nhận Polly retry (`OrdersApi-standard//Standard-Retry`)
  engage thật với `AttemptTimeout=1s` bị vượt liên tục ở các request lỗi.

  **Circuit breaker KHÔNG mở mạch ở cả BA lần thử** (không tìm thấy `BrokenCircuitException` nào
  trong log BFF ở bất kỳ lần nào, kể cả trên k8s thật) — tổng số lỗi thật quan sát được (7 + 1 = 8
  qua 3 lần chạy độc lập, gồm cả trên container Docker và trên pod Kubernetes thật) luôn rải rác theo
  thời gian, không đủ mật độ trong cửa sổ `CircuitBreakerSamplingDuration` để vượt ngưỡng tỷ lệ lỗi
  tối thiểu đã cấu hình cho `OrdersApiClient`. Đây là một phát hiện nhất quán, được xác nhận độc lập
  trên cả hai nền tảng (Docker container và Kubernetes pod thật) — không phải do hạn chế của một môi
  trường cụ thể. Acceptance Criteria gốc của SCRUM-34 ("circuit breaker trips per its configured
  threshold") vẫn chưa được xác nhận sau 3 lần thử; nguyên nhân nhiều khả năng là công cụ tạo tải
  (`curl` trong vòng lặp bash) không tạo đủ mật độ lỗi ĐỒNG THỜI trong một cửa sổ ngắn — cần công cụ
  load-test chuyên dụng (ví dụ k6, Bombardier) để xác nhận dứt điểm.

  **Dashboard SLO (Kibana, Bước 3 quickstart.md)**: KHÔNG kiểm chứng ở các lần thử trên — theo thỏa
  thuận với người giám sát tại thời điểm chọn hướng đi (bỏ qua phần xác minh dashboard Elastic), dù
  Elasticsearch/Kibana có chạy healthy trong stack `ecomerce-local` này.

  **Lần thử 4 — T015, một lượt liền mạch Bước 1→4 + Dọn dẹp, dùng công cụ load-test thật
  (`autocannon`)**: chạy NGAY SAU Bước 1 (kill-pod) trong cùng phiên không gián đoạn, trên cùng Pod
  Kubernetes thật (`orders-598b9cfb5-nfzc8`, tái khởi động tiến trình `dotnet` với
  `Chaos__AllowLatencyInjection=true` lúc `11:40:36` UTC). Cài `autocannon` (Node.js, `npm install -g
  autocannon`) — công cụ load-test thật, không phải vòng lặp `curl` trong bash. Chạy
  `autocannon -c 50 -d 25 -H "X-Chaos-Latency-Ms: 3000" http://localhost:15041/orders/...` (50 kết
  nối ĐỒNG THỜI thật, 25 giây liên tục, gửi trực tiếp qua `kubectl port-forward` vào pod) trong lúc
  tải nền qua BFF (`GET /bff/orders/{id}`, 120 request, ~0.3s/vòng) vẫn chạy song song. Kết quả
  autocannon: 401 request thật, độ trễ trung vị 3,084ms/p97.5 3,941ms (khớp đúng header `3000` +
  overhead port-forward) — xác nhận middleware xử lý đúng dưới tải đồng thời thật. Tải nền qua BFF:
  118/120 `404` bình thường, chỉ 2/120 `504` — **circuit breaker VẪN KHÔNG mở mạch** dù đây là lần
  thử với tải tập trung ĐỒNG THỜI THẬT SỰ (khác hẳn `curl` tuần tự trong vòng lặp bash của 3 lần thử
  trước). Đây là bằng chứng mạnh nhất tới nay cho thấy nguyên nhân không trip KHÔNG phải do công cụ
  tải yếu, mà do bản chất `await Task.Delay(...)` không chặn thread/connection pool của Kestrel —
  50 request "đang chờ" đồng thời không tạo đủ áp lực lên chính request path mà BFF sử dụng (BFF gọi
  `/orders/{id}` cho MỘT order cụ thể mỗi lần, không cạnh tranh trực tiếp với 50 kết nối của
  autocannon ở cùng tài nguyên hệ thống — CPU/thread pool của Orders.Api thừa sức phục vụ cả hai
  đồng thời).

  **Dashboard SLO (Kibana, Bước 3) — XÁC NHẬN THẬT lần đầu tiên**: mở dashboard
  `SLO vận hành hằng ngày — 7 service` [import bằng
  `curl -X POST http://localhost:5601/api/saved_objects/_import?overwrite=true`], time range bao
  trùm cửa sổ chạy autocannon (`18:30`–`18:45` giờ địa phương ≈ UTC+7, tức `11:30`–`11:45` UTC). Bảng
  SLO hiển thị **Orders.Api: Error-rate 0.00%, Latency p95 = 3,080ms, p99 = 3,343ms** — vượt xa ngưỡng
  khai báo (p95 ≤150ms/p99 ≤500ms, theo đúng chú thích ngay trên bảng). Biểu đồ "Latency p95 theo
  ngày" cho thấy đường Orders.Api (đỏ) tăng vọt từ nền ~50-500ms lên ~2,900-3,000ms đúng vào ngày chạy
  bài tập — không có trường hợp nào việc tiêu hao chỉ được phát hiện sau khi bài tập kết thúc (khớp
  SC-003 của spec.md). Bảng "Top endpoint chậm nhất" xác nhận `/orders/{orderId:guid}` trung bình
  2.37s — đúng route bị tiêm độ trễ. **Đây là kiểm chứng THẬT đầu tiên của Bước 3 sau 4 lần thử.**
- **ket_luan**: sai_lech
- **jira_ticket**: (chưa mở — xem ghi chú bên dưới)

## Ghi chú sai lệch

1. **Circuit breaker của `OrdersApiClient` không trip** ở CẢ BỐN lần thử độc lập (container Docker
   x2, pod Kubernetes thật x2 — lần cuối dùng `autocannon` với 50 kết nối đồng thời thật, 25 giây
   liên tục), dù mỗi lần đều gây ra lỗi thật (504/timeout) qua BFF. **Đã loại trừ nguyên nhân "công cụ
   tải yếu"** — lần thử 4 dùng công cụ load-test chuyên dụng và kết quả không đổi. Nguyên nhân thực sự
   nhiều khả năng là bản chất bất đồng bộ của việc tiêm độ trễ (`await Task.Delay`) không tạo áp lực
   thật lên tài nguyên xử lý (thread pool/connection pool) mà BFF cạnh tranh — nghĩa là chính cơ chế
   tiêm lỗi của tính năng 025 (thiết kế không chặn, đúng chủ đích để không làm sập thật hạ tầng khi
   diễn tập) có thể không đủ để tái hiện kiểu suy giảm hiệu năng mà circuit breaker được thiết kế để
   phát hiện (nghẽn tài nguyên thật). Đây là một phát hiện đáng cân nhắc đưa vào bug/thảo luận ticket
   riêng — không phải lỗi của việc chạy bài tập.
2. ~~Không kiểm chứng dashboard SLO Kibana~~ — **ĐÃ XÁC NHẬN ở lần thử 4** (T015): bảng SLO thật hiển
   thị Orders.Api p95=3,080ms/p99=3,343ms, biểu đồ theo ngày phản ánh đúng thời điểm chạy bài tập.
   Không còn là sai lệch.
3. Lần thử 3 chạy Orders.Api như Pod Kubernetes thật nhưng KHÔNG dùng image build tùy chỉnh (do giới
   hạn image-loading của cluster — xem `2026-09-14-kill-pod.md`) — dùng kỹ thuật `kubectl cp` +
   `kubectl exec` để đưa binary đã publish vào Pod chạy image công khai. Bản thân middleware và hành
   vi resilience quan sát được không bị ảnh hưởng bởi kỹ thuật này (không có sai lệch tương đương
   T001–T003), nhưng vẫn nên chạy lại với image thật nếu cần đo hiệu năng chính xác.
