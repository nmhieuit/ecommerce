# Kiến trúc: Diễn tập chaos engineering — giết một pod / tiêm độ trễ để kiểm chứng resilience

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-34 ("[RESILIENCE-4] Chaos exercise: kill a pod / inject latency"), đặc tả
tại [`specs/025-chaos-pod-kill-latency/`](../../specs/025-chaos-pod-kill-latency/). 4 quyết định kiến
trúc ở [`research.md`](../../specs/025-chaos-pod-kill-latency/research.md).

**Trạng thái xác minh**: 16/16 task hoàn thành `[X]`. Đã chạy 4 lần độc lập trên hạ tầng thật (container
Docker, rồi Pod Kubernetes thật, kể cả với công cụ load-test thật `autocannon`) — nhưng cả 3 bản ghi
kết quả tại [`docs/dien-tap-chaos-engineering/ket-qua/`](../dien-tap-chaos-engineering/ket-qua/) đều
kết luận **`sai_lệch`**, không phải `đạt`: circuit breaker chưa từng trip ở bất kỳ lần thử nào trong
cả 4 lần. Đây không phải việc "chưa làm xong" — cơ chế quan sát/ghi nhận hoạt động đúng thiết kế, kết
luận sai lệch chính là điều User Story 3 được xây ra để phát hiện. Phân tích nguyên nhân và câu hỏi mở
về Acceptance Criteria gốc: xem [technical-debt.md](technical-debt.md).

## 1. Ba user story cần 3 mức độ can thiệp khác nhau

| User Story | Cần mã ứng dụng mới? | Cơ chế |
|---|---|---|
| US1 — kill-pod (`baskets`) | Không | Thuần vận hành: `kubectl delete pod`, quan sát qua telemetry "Polly" (020) đã có sẵn |
| US2 — inject-latency (`orders`) | Có | Middleware mới, 2 lớp gate, tái dùng dashboard SLO (021) để quan sát tiêu hao ngân sách |
| US3 — bản ghi kết quả | Không (tài liệu) | Thư mục `docs/dien-tap-chaos-engineering/` — mẫu + README + `ket-qua/`, không tích hợp Jira tự động |

Research.md Quyết định 0 xác nhận US1 không cần sửa `services/baskets` hay BFF: `deployment.yaml.j2`
không khai báo `replicas`, nên Kubernetes mặc định 1 replica — "tái lập lịch" ở Acceptance Criteria 1
là **cold-start lại từ đầu**, không phải failover sang pod dự phòng đang chạy sẵn; đây đúng là điều
US1 muốn quan sát, không phải thiếu sót cần "sửa" bằng cách thêm replica (sẽ đổi phạm vi từ "diễn tập
chaos" sang "xây high-availability").

## 2. Cơ chế tiêm độ trễ — 2 lớp gate, dừng không cần redeploy (Quyết định 1)

[`services/orders/src/Orders.Api/Features/Chaos/ChaosOptions.cs`](../../services/orders/src/Orders.Api/Features/Chaos/ChaosOptions.cs):
cờ `AllowLatencyInjection` (mặc định `false`, section `Chaos` riêng — KHÔNG dùng khối `FeatureToggles`
có sẵn, vì đây là công cụ vận hành thường trực không có ngày gỡ, không phải 1 rollout) và hằng số
`MaxInjectedLatencyMs = 30_000` chặn 1 sai sót thao tác (gõ nhầm số 0) biến bài tập có kiểm soát thành
treo vô hạn.

[`ChaosLatencyInjectionMiddleware.cs`](../../services/orders/src/Orders.Api/Features/Chaos/ChaosLatencyInjectionMiddleware.cs)
đọc header `X-Chaos-Latency-Ms` của chính request, chỉ trì hoãn khi CẢ HAI điều kiện đúng
(`AllowLatencyInjection=true` VÀ header hợp lệ), luôn gọi `next(context)` sau đó — không đổi
status/header/body response ở bất kỳ nhánh nào. Tách 2 lớp gate (cấu hình môi trường vs header
per-request) nghĩa là bắt đầu/dừng tiêm giữa chừng chỉ là gửi/không gửi header — không cần restart pod
(constitution Principle X). `IChaosDelay`/`SystemChaosDelay` là seam nhỏ để unit test đo được khoảng
trễ được yêu cầu mà không thực sự chờ khi chạy test.

## 3. Quyết định kỹ thuật đáng chú ý (research.md)

| # | Quyết định |
|---|---|
| 0 | US1 không cần mã ứng dụng mới — chỉ dùng nguyên trạng resilience (020)/telemetry (017) |
| 1 | Middleware mới, tối thiểu, 2 lớp gate — không dùng Toxiproxy/công cụ fault-injection chuyên dụng (thêm hạ tầng mới chỉ cho 1 kịch bản), không sửa mã nguồn tạm thời mỗi lần chạy (như 021 từng làm — không phù hợp bài tập lặp lại định kỳ có ghi nhận) |
| 2 | Quan sát tiêu hao SLO tái dùng nguyên trạng dashboard Kibana "SLO vận hành hằng ngày" (021) — không xây dashboard/panel riêng cho chaos |
| 3 | Bản ghi kết quả là tài liệu markdown theo mẫu tại `docs/` (không phải `specs/025.../`, vì đây là artifact vận hành sống tích luỹ qua nhiều lần chạy) — không tích hợp lập trình với Jira |
| 4 | Unit test thuần trong `Orders.Api.UnitTests` đã có, không tạo dự án test mới — middleware không phụ thuộc ngoài (không DB, không broker) |

## 4. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/025-chaos-pod-kill-latency-component.drawio`](../diagrams/025-chaos-pod-kill-latency-component.drawio)
- Sơ đồ trình tự (2 lớp gate tiêm độ trễ + kịch bản kill-pod, gồm nhánh circuit breaker không trip):
  [`docs/diagrams/025-chaos-pod-kill-latency-sequence.drawio`](../diagrams/025-chaos-pod-kill-latency-sequence.drawio)
- Sơ đồ luồng nghiệp vụ đơn giản hoá (đi kèm tài liệu PO):
  [`docs/diagrams/025-chaos-pod-kill-latency-flow-nghiep-vu.drawio`](../diagrams/025-chaos-pod-kill-latency-flow-nghiep-vu.drawio)

Phát hiện quan trọng nhất (circuit breaker chưa từng trip qua 4 lần thử độc lập) và giới hạn môi
trường thực thi (image-loading của cluster test, recovery-time lẫn thao tác thủ công): xem
[technical-debt.md](technical-debt.md).
