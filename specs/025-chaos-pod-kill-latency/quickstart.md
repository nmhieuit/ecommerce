# Quickstart: Diễn tập chaos engineering — tiêm độ trễ

**Feature**: [spec.md](./spec.md) | Tham chiếu: [data-model.md](./data-model.md), [contracts/](./contracts/)

> **Cập nhật (spec 031, 2026-10-06)**: kịch bản giết pod trên Kubernetes đã gỡ. Việc luyện "service chết"
> nay chạy bằng nhóm 8 của [spec 031](../031-error-group-catalog/quickstart.md) trên Docker Compose.

Hướng dẫn này lặp lại các kịch bản kiểm thử còn lại của Jira SCRUM-34 (tiêm độ trễ, quan sát dashboard,
ghi nhận kết quả). Đây là một bài tập chạy tay/định kỳ trên stack Docker Compose local — không phải
test chặn PR (cùng logic 019/021 đã dùng cho hành vi động).

## Điều kiện tiên quyết

- Stack Docker Compose local đang chạy khoẻ: `docker compose -f docker-compose.local.yml up -d --wait`
  — KHÔNG chạy bài tập này nhắm vào production (spec FR-006).
- `CHAOS_ALLOW_LATENCY_INJECTION=true` đã được đặt trong `.env` CHỈ cho phiên diễn tập này (compose
  truyền thành `Chaos__AllowLatencyInjection` cho `orders-api`; tạo lại container `orders-api` để nhận cờ)
  — xem [contracts/chaos-latency-injection-contract.md](./contracts/chaos-latency-injection-contract.md)
  Bất biến 1.
- Một nguồn tải tổng hợp nhẹ liên tục gọi qua BFF vào orders service trong lúc diễn tập (bất kỳ công
  cụ nào — ví dụ `./scripts/incident-drill.ps1 -Load` hoặc một vòng lặp `curl` thủ công; spec.md
  Assumptions không ràng buộc công cụ cụ thể).
- Elastic stack (Elasticsearch + Kibana) đang chạy, đã nhận dữ liệu OTel từ BFF/orders theo đúng
  017-otel-servicedefaults-elastic; dashboard `Xử lý sự cố — 7 service` đã import
  (021-declare-service-slos).
- `dotnet test services/orders/tests/Orders.Api.UnitTests --filter FullyQualifiedName~ChaosLatencyInjection`
  đã pass trước khi diễn tập.

## Bước 1 — Jira Test Scenario 2: tiêm 2 giây độ trễ vào orders service, xác nhận circuit breaker theo ngưỡng

```bash
curl -i -H "X-Chaos-Latency-Ms: 2000" http://localhost:5041/orders/<id>
```

Lặp lại liên tục (hoặc qua nguồn tải nền, nếu nó hỗ trợ gắn thêm header) để mô phỏng nhiều request
liên tiếp đều chậm 2 giây. Header không đi xuyên gateway/BFF tới orders, nên phải gửi thẳng tới
`orders-api` (cổng 5041 trên máy chủ).

**Kỳ vọng**: mỗi request phản hồi sau ≈2s (cộng độ trễ xử lý bình thường) — khớp
[contracts/chaos-latency-injection-contract.md](./contracts/chaos-latency-injection-contract.md).
Vì `AttemptTimeout=1s` của `OrdersApiClient` (BFF) nhỏ hơn 2s, caller (BFF) timeout ở lần thử đầu
trước khi orders kịp trả lời — nếu đủ số lần thử timeout liên tiếp trong cửa sổ 10s, circuit breaker
của `OrdersApiClient` mở mạch, đúng tinh thần Acceptance Criteria của SCRUM-34 ("circuit breaker
trips per its configured threshold"). Ngừng gửi header (hoặc gửi `X-Chaos-Latency-Ms: 0`) để dừng
tiêm ngay lập tức — không cần restart container nào (Bất biến 5 của hợp đồng tiêm độ trễ).

Cách khác, có tự gỡ và ghi nhận: `./scripts/incident-drill.ps1 -Inject -Type D -Target orders-api
-DurationSeconds 300` (spec 031).

## Bước 2 — Jira Test Scenario 3: quan sát ngân sách SLO bị tiêu hao trên dashboard trong lúc diễn tập

Mở dashboard `Xử lý sự cố — 7 service` trong Kibana (đã import từ
`docs/kibana-quan-sat-he-thong/dashboards/xu-ly-su-co.ndjson`), đặt time range **Last 15
minutes**, theo dõi **trong lúc** đang thực hiện Bước 1 (không phải sau khi đã dừng).

**Kỳ vọng**: giá trị "Thực tế" của latency p95/p99 cho `Orders.Api` tăng lên gần thời gian thực,
vượt ngưỡng khai báo (internal-service-api: p95 ≤150ms/p99 ≤500ms — xem
`services/orders/src/Orders.Api/service-manifest.yaml`), phản ánh đúng SC-003 của spec.md ("không có
trường hợp nào việc tiêu hao chỉ được phát hiện sau khi bài tập đã kết thúc"). Dừng tiêm (Bước 1) và
xác nhận giá trị "Thực tế" quay về dưới ngưỡng trong vài phút tiếp theo.

## Bước 3 — Ghi nhận kết quả (User Story 3)

Sao chép `docs/dien-tap-chaos-engineering/mau-ket-qua.md` thành
`docs/dien-tap-chaos-engineering/ket-qua/<hom-nay>-inject-latency.md`, điền đủ các trường bắt buộc tại
[contracts/exercise-outcome-writeup-contract.md](./contracts/exercise-outcome-writeup-contract.md)
dựa trên quan sát ở Bước 1–2. Nếu bất kỳ kỳ vọng nào ở Bước 1–2 KHÔNG khớp thực tế, đặt
`ket_luan: sai_lech`, mở một bug ticket mô tả sai lệch, và dán liên kết vào trường `jira_ticket`.
Cập nhật `docs/dien-tap-chaos-engineering/README.md` để liệt kê (các) bản ghi vừa tạo.

## Dọn dẹp

- Xác nhận không còn request nào đang mang header `X-Chaos-Latency-Ms` (Bước 1 đã dừng).
- Đặt lại `CHAOS_ALLOW_LATENCY_INJECTION=false` trong `.env` sau khi hoàn tất, rồi tạo lại container
  `orders-api` (hoặc chạy lại stack) để cờ có hiệu lực.
