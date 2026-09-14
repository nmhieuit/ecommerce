# Quickstart: Diễn tập chaos engineering — giết pod / tiêm độ trễ

**Feature**: [spec.md](./spec.md) | Tham chiếu: [data-model.md](./data-model.md), [contracts/](./contracts/)

Hướng dẫn này lặp lại đúng 3 kịch bản kiểm thử của Jira SCRUM-34. Đây là một bài tập chạy tay/định
kỳ trên hạ tầng thật — không phải test chặn PR (cùng logic 019/021 đã dùng cho hành vi động).

## Điều kiện tiên quyết

- Một cluster Kubernetes đã triển khai bằng `deploy/ansible/deploy.yml` (cluster thử nghiệm `kind`
  cục bộ theo đúng cách `specs/019-liveness-readiness-probes/quickstart.md` Bước 1–2 đã dùng, hoặc
  cluster diễn tập thật) — KHÔNG chạy bài tập này nhắm vào production (spec FR-006).
- `Chaos:AllowLatencyInjection=true` đã được đặt cho Orders.Api CHỈ trên cluster diễn tập này (biến
  môi trường `Chaos__AllowLatencyInjection=true` khi render/áp dụng Deployment, hoặc trong
  `appsettings.Development.json` nếu chạy cục bộ) — xem
  [contracts/chaos-latency-injection-contract.md](./contracts/chaos-latency-injection-contract.md)
  Bất biến 1.
- Một nguồn tải tổng hợp nhẹ liên tục gọi qua BFF vào basket service và orders service trong lúc
  diễn tập (bất kỳ công cụ nào — kể cả một vòng lặp `curl` thủ công là đủ; spec.md Assumptions không
  ràng buộc công cụ cụ thể).
- Elastic stack (Elasticsearch + Kibana) đang chạy, đã nhận dữ liệu OTel từ BFF/orders theo đúng
  017-otel-servicedefaults-elastic; dashboard `SLO vận hành hằng ngày — 7 service` đã import
  (021-declare-service-slos).
- `dotnet test services/orders/tests/Orders.Api.UnitTests --filter FullyQualifiedName~ChaosLatencyInjection`
  đã pass trước khi diễn tập trên hạ tầng thật.

## Bước 1 — Jira Test Scenario 1: kill pod của basket service, quan sát circuit breaker ở BFF

```bash
kubectl get pods -l app=baskets -w &
kubectl delete pod -l app=baskets --field-selector=status.phase=Running
```

**Kỳ vọng** (spec User Story 1 / Acceptance Scenario 1–3):
- Kubernetes tái lập lịch một pod `baskets` mới; cột `READY`/`RESTARTS` cho thấy pod cũ mất đi và
  pod mới chuyển `0/1` → `1/1`. Vì hiện tại `baskets` chạy 1 replica (research.md Quyết định 0), cửa
  sổ gián đoạn thật sự tồn tại — đây là điều cần quan sát và ghi lại, không phải lỗi cần sửa trong
  phạm vi tính năng này.
- Trong lúc pod mới chưa `READY`, các request tải nền qua BFF tới `/basket/...` thất bại nhanh
  (không treo quá `TotalRequestTimeout=3s` mỗi lần thử) và, nếu tỷ lệ lỗi vượt ngưỡng trong cửa sổ
  `CircuitBreakerSamplingDuration=10s`, circuit breaker của `BasketsApiClient` mở mạch.
- Quan sát bằng chứng circuit breaker/retry engage: xem log có cấu trúc của BFF (nguồn `"Polly"`),
  hoặc trên Kibana Data View **Traces** (`traces-generic.otel-default*`) lọc
  `resource.attributes.service.name : "Bff.Api"` trong khung giờ vừa chạy — tìm span/scope gắn
  `"Polly"` ứng với các lần thử/retry tới `baskets`. Nếu chạy cục bộ không có Elastic, dùng
  `dotnet-counters monitor --process-id <pid-cua-Bff.Api> Polly` (cùng kỹ thuật
  `specs/020-timeouts-retry-circuit-breaker/quickstart.md` Bước 6 đã xác nhận).
- Sau khi pod mới `READY`, tỷ lệ lỗi của tải nền giảm về bình thường và circuit breaker đóng mạch trở
  lại mà không cần can thiệp thủ công.

## Bước 2 — Jira Test Scenario 2: tiêm 2 giây độ trễ vào orders service, xác nhận circuit breaker theo ngưỡng

```bash
curl -i -H "X-Chaos-Latency-Ms: 2000" http://<orders-service>/orders/<id>
```

Lặp lại liên tục (hoặc qua nguồn tải nền, nếu nó hỗ trợ gắn thêm header) để mô phỏng nhiều request
liên tiếp đều chậm 2 giây.

**Kỳ vọng**: mỗi request phản hồi sau ≈2s (cộng độ trễ xử lý bình thường) — khớp
[contracts/chaos-latency-injection-contract.md](./contracts/chaos-latency-injection-contract.md).
Vì `AttemptTimeout=1s` của `OrdersApiClient` (BFF) nhỏ hơn 2s, caller (BFF) timeout ở lần thử đầu
trước khi orders kịp trả lời — nếu đủ số lần thử timeout liên tiếp trong cửa sổ 10s, circuit breaker
của `OrdersApiClient` mở mạch, đúng tinh thần Acceptance Criteria của SCRUM-34 ("circuit breaker
trips per its configured threshold"). Ngừng gửi header (hoặc gửi `X-Chaos-Latency-Ms: 0`) để dừng
tiêm ngay lập tức — không cần restart pod nào (Bất biến 5 của hợp đồng tiêm độ trễ).

## Bước 3 — Jira Test Scenario 3: quan sát ngân sách SLO bị tiêu hao trên dashboard trong lúc diễn tập

Mở dashboard `SLO vận hành hằng ngày — 7 service` trong Kibana (đã import từ
`docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson`), đặt time range **Last 15
minutes**, theo dõi **trong lúc** đang thực hiện Bước 2 (không phải sau khi đã dừng).

**Kỳ vọng**: giá trị "Thực tế" của latency p95/p99 cho `Orders.Api` tăng lên gần thời gian thực,
vượt ngưỡng khai báo (internal-service-api: p95 ≤150ms/p99 ≤500ms — xem
`services/orders/src/Orders.Api/service-manifest.yaml`), phản ánh đúng SC-003 của spec.md ("không có
trường hợp nào việc tiêu hao chỉ được phát hiện sau khi bài tập đã kết thúc"). Dừng tiêm (Bước 2) và
xác nhận giá trị "Thực tế" quay về dưới ngưỡng trong vài phút tiếp theo.

## Bước 4 — Ghi nhận kết quả (User Story 3)

Sao chép `docs/dien-tap-chaos-engineering/mau-ket-qua.md` thành
`docs/dien-tap-chaos-engineering/ket-qua/<hom-nay>-kill-pod.md` và
`...-inject-latency.md`, điền đủ các trường bắt buộc tại
[contracts/exercise-outcome-writeup-contract.md](./contracts/exercise-outcome-writeup-contract.md)
dựa trên quan sát ở Bước 1–3. Nếu bất kỳ kỳ vọng nào ở Bước 1–3 KHÔNG khớp thực tế, đặt
`ket_luan: sai_lech`, mở một bug ticket mô tả sai lệch, và dán liên kết vào trường `jira_ticket`.
Cập nhật `docs/dien-tap-chaos-engineering/README.md` để liệt kê (các) bản ghi vừa tạo.

## Dọn dẹp

- Xác nhận không còn request nào đang mang header `X-Chaos-Latency-Ms` (Bước 2 đã dừng).
- Nếu `Chaos:AllowLatencyInjection=true` được đặt riêng cho phiên diễn tập này (không phải cấu hình
  thường trực của cluster diễn tập), đặt lại `false` sau khi hoàn tất.
