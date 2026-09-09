# Quickstart: Xác thực khai báo và đo lường liên tục SLO

**Feature**: [spec.md](./spec.md) | Tham chiếu: [data-model.md](./data-model.md), [contracts/](./contracts/)

Hướng dẫn này xác thực end-to-end đúng 3 kịch bản kiểm thử của Jira SCRUM-29: (1) manifest khai báo
SLO đầy đủ, không placeholder; (2) đối chiếu SLO khai báo với dashboard đo thật; (3) làm chậm một
endpoint và xác nhận ngân sách bị tiêu hao thể hiện rõ trên dashboard. Bước 1–2 là test tĩnh, chặn
PR. Bước 3–5 là kịch bản chạy tay/định kỳ trên dữ liệu thật — không phải test chặn PR (xem
research.md Quyết định 3, cùng logic 019 đã dùng cho smoke test động).

## Điều kiện tiên quyết

- .NET 10 SDK đã cài (để chạy dự án test mới).
- Elastic stack (Elasticsearch + Kibana) đang chạy và đã nhận dữ liệu OTel thật từ ít nhất một service
  — theo đúng thiết lập của 017-otel-servicedefaults-elastic. Không cần thiết lập gì thêm cho bước
  này; tái sử dụng môi trường quan sát đã có.
- Đã import dashboard `SLO vận hành hằng ngày — 7 service` vào Kibana từ
  `docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson` (Stack Management → Saved
  Objects → Import). Nếu đã import từ trước, bỏ qua bước này.

## Bước 1 — Chạy test tĩnh: khai báo SLO đầy đủ, không placeholder (Test Scenario 1)

```bash
dotnet test tests/ServiceManifestSloConventionTests
```

**Kỳ vọng**: toàn bộ test pass. Test này đọc cả 7 `service-manifest.yaml` và assert 6 bất biến tại
[contracts/service-manifest-slo-shape.md](./contracts/service-manifest-slo-shape.md).

## Bước 2 — Xác nhận cơ chế bảo vệ hoạt động thật (không chỉ pass vì tình cờ đúng)

Tạm sửa một giá trị trong `services/orders/src/Orders.Api/service-manifest.yaml` (ví dụ đổi
`latency.p95: 150ms` thành `latency.p95: 50ms`, không kèm `slos.justification`), chạy lại lệnh ở
Bước 1.

**Kỳ vọng**: test FAIL, chỉ đích danh `orders` và lý do (lệch mặc định `internal-service-api`, không
có `slos.justification`). Hoàn tác thay đổi (`git checkout -- services/orders/src/Orders.Api/service-manifest.yaml`)
và chạy lại Bước 1 để xác nhận test pass trở lại.

## Bước 3 — Đối chiếu SLO khai báo với dashboard đo thật (Test Scenario 2)

Mở dashboard `SLO vận hành hằng ngày — 7 service` trong Kibana, cửa sổ thời gian **Last 24 hours**.
Với một service bất kỳ (ví dụ `Orders.Api`), ghi lại giá trị "Thực tế" của error-rate, latency p95,
latency p99 hiển thị trên Tầng 1.

**Kỳ vọng**: mỗi giá trị "Thực tế" có một cột/ghi chú "Ngưỡng" đi kèm, khớp đúng giá trị khai báo
trong `services/orders/src/Orders.Api/service-manifest.yaml` — xác nhận bất biến 1–2 tại
[contracts/continuous-measurement-contract.md](./contracts/continuous-measurement-contract.md).

## Bước 4 — Làm chậm một endpoint, xác nhận ngân sách bị tiêu hao thể hiện rõ (Test Scenario 3)

Gây độ trễ có chủ đích cho một endpoint của service đang theo dõi (ví dụ thêm một `Task.Delay` tạm
thời, hoặc tạo tải giả lập nhiều request chậm) trong một khoảng thời gian đủ để có traffic mới trong
cửa sổ đo.

**Kỳ vọng**: reload dashboard sau vài phút — giá trị "Thực tế" của latency p95/p99 cho service đó
tăng lên rõ rệt, tiến gần hoặc vượt cột "Ngưỡng"; Chart B (Tầng 1.5, "Latency p95 theo ngày") thể
hiện điểm dữ liệu của ngày hôm đó cao hơn các ngày trước — xác nhận bất biến 4. Hoàn tác thay đổi làm
chậm sau khi xác nhận xong.

## Bước 5 — Xác nhận "không có dữ liệu" không hiển thị nhầm thành "0% lỗi" (Edge Case, FR-006)

Chọn một khoảng thời gian chắc chắn không có traffic (ví dụ đặt time range Absolute về một ngày trong
quá khứ trước khi hệ thống demo bắt đầu sinh dữ liệu).

**Kỳ vọng**: dashboard hiển thị "No results found" (hoặc trạng thái tương đương thể hiện rõ "không có
dữ liệu") cho các service không có traffic trong khoảng đó — KHÔNG hiển thị `0%` lỗi hay bất kỳ giá
trị nào ngụ ý service đạt ngân sách hoàn hảo. Xác nhận bất biến 3 tại
[contracts/continuous-measurement-contract.md](./contracts/continuous-measurement-contract.md).
