# Kiến trúc: Kiểm thử tải/hiệu năng luồng nghiệp vụ trọng yếu đối chiếu ngân sách hiệu năng của hiến chương

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-32 ("[RESILIENCE-4] Load/performance test against constitution budgets"),
đặc tả tại [`specs/025-load-performance-test-budgets/`](../../specs/025-load-performance-test-budgets/)
— **lưu ý đánh số**: thư mục spec này tên là `025-...` (trùng số với `025-chaos-pod-kill-latency`/
SCRUM-34, vì cả hai đều được tạo cùng đợt trước khi merge); tài liệu hoá dùng số **026** cho SCRUM-32
vì SCRUM-34 merge vào `master` trước (PR #36 so với PR #38). 6 quyết định kiến trúc ở
[`research.md`](../../specs/025-load-performance-test-budgets/research.md).

**Trạng thái xác minh**: 18/18 task hoàn thành `[X]`. Đã chạy thật trên stack đầy đủ
(`docker-compose.yml` + `docker-compose.demo.yml`, 14 image build thành công) — cơ chế đo lường/ghi
báo cáo/cổng chặn tự động hoạt động **đúng thiết kế**, nhưng lần chạy thật **FAIL đúng như thiết kế**
vì `GET /bff/products` qua gateway trả về `401` — một khoảng trống xác thực của TOÀN NỀN TẢNG (BFF
luôn đòi JWT thật, không có cách nào lấy token thật từ 1 client HTTP ngoài hôm nay), không phải lỗi
của tính năng này. Chi tiết đầy đủ: xem [technical-debt.md](technical-debt.md).

## 1. Kiến trúc tổng thể

```
tests/CriticalPathLoadTests (NBomber 4.1.2, ghim cứng)
   │ GatewayClient — gọi qua gateway, KHÔNG tự đính bearer token
   ▼
Gateway → BFF: GET /bff/products → POST /bff/basket/items → POST /bff/checkout → GET /bff/orders/{id}
   │ (đúng 4 bước của luồng browse→basket→checkout→order, thứ tự thật)
   ▼
CriticalPathStepBudget — đọc ngưỡng p95/p99 từ service-manifest.yaml của bff (ServiceManifestFixture,
                          tái dùng nguyên trạng từ 021 — KHÔNG định nghĩa lại con số ở đâu khác)
   ▼
LoadTestReportWriter (ghi artifacts/performance/, không ghi đè) → BudgetAssertions (fail nếu vượt ngân sách)
```

Lớp `internal-service-api` (4 service phía sau BFF) không bị lái tải trực tiếp — tận dụng lại dashboard
đo liên tục của 021 (đọc cùng dữ liệu OTel mà traffic NBomber tạo ra) làm bước xác nhận bổ sung trong
`quickstart.md`, không xây đường đo song song.

## 2. Quyết định kỹ thuật đáng chú ý (research.md)

| # | Quyết định |
|---|---|
| 0 | Nguồn chân lý ngân sách: đọc lại `service-manifest.yaml` qua `ServiceManifestFixture` (021) — không hard-code số ở dự án kiểm thử tải, tránh drift giữa 2 nơi |
| 1 | Công cụ tạo tải: NBomber, ghim đúng **4.1.2** (không dùng 5.x/6.x — xem phát hiện license ở [technical-debt.md](technical-debt.md)) |
| 2 | Chỉ lái tải qua 4 route BFF (giống shopper thật gọi qua gateway) — không lái tải trực tiếp vào 4 service nội bộ |
| 3 | Tier CI "performance" riêng, loại trừ khỏi tier "unit" hiện có; chạy từ `Jenkinsfile.performance` theo lịch, tách biệt hoàn toàn PR gate hiện có (specs/013) |
| 4 | Mô phỏng hồi quy hiệu năng bằng `Task.Delay` tạm thời thủ công (như 021 đã làm) — không xây cơ chế fault-injection thường trực mới cho tính năng này |
| 5 | Bổ sung 2 mục `endpoints` còn thiếu (`POST /bff/basket/items`, `POST /bff/checkout`) vào manifest `bff` — khoảng trống tài liệu thật do khảo sát phát hiện, không phải giả định |

## 3. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/026-load-performance-test-budgets-component.drawio`](../diagrams/026-load-performance-test-budgets-component.drawio)
- Sơ đồ trình tự (4 bước qua gateway → đối chiếu ngân sách → ghi báo cáo → fail/pass, gồm nhánh 401
  thật): [`docs/diagrams/026-load-performance-test-budgets-sequence.drawio`](../diagrams/026-load-performance-test-budgets-sequence.drawio)
- Sơ đồ luồng nghiệp vụ đơn giản hoá (đi kèm tài liệu PO):
  [`docs/diagrams/026-load-performance-test-budgets-flow-nghiep-vu.drawio`](../diagrams/026-load-performance-test-budgets-flow-nghiep-vu.drawio)

Phát hiện license NBomber, khoảng trống xác thực 401 toàn nền tảng, và 2 bug thật không liên quan phát
hiện tình cờ (Dockerfile thiếu `COPY`, race condition `identity-api`): xem
[technical-debt.md](technical-debt.md).
