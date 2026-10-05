# Quickstart: Kiểm chứng hai dashboard Xử lý sự cố và Ngân sách lỗi tuần

**Spec**: [spec.md](./spec.md) | **Contract**: [contracts/dashboards-contract.md](./contracts/dashboards-contract.md) | **Research**: [research.md](./research.md)

Hướng dẫn chạy thật để chứng minh tính năng. Không chứa mã triển khai. Giá trị cụ thể (id, tên file) điền khi tạo dashboard.

## Điều kiện

- Docker Compose `ecomerce-local` đang chạy: Elasticsearch `localhost:9200`, Kibana `localhost:5601` (9.4.4), 7 service và OTel collector.
- Có dữ liệu traces trong tuần hiện tại (đã có từ các lần chạy trước).
- Rule 027/028 đã import (có sẵn trên stack hiện tại).
- `dotnet`, `npx.cmd` (newman). Lệnh PowerShell 5.1: dùng `curl.exe` như `dashboards/README.md`.

## Kịch bản 0 — Kiểm chứng kỹ thuật (làm trước, điểm dừng)

Chạy V1–V8 ở [research.md](./research.md). Ghi kết quả (đúng/sai, bằng chứng, thời điểm) vào mục "Kết quả xác minh" ở cuối research.md. **Sai bất kỳ mục nào: dừng và hỏi người dùng.**

- **V4 (log có correlation id không)**: gây một log mức Error trong một request thật của một service; truy vấn Elasticsearch:
  `FROM logs-generic.otel-default* | WHERE severity_number >= 17 | SORT @timestamp DESC | LIMIT 5`, xem `trace_id` và trường correlation id. Thiếu thì sửa `ServiceDefaults` (test viết trước) rồi tạo lại 7 service và chạy lại V4.
- **V1/V2 (bộ chọn tuần)**: dựng điều khiển `?tuan_chon` (4 giá trị tương đối, research D3 cập nhật), thử một Lens ES|QL và một Discover session dùng biến, export rồi import vào Kibana sạch.

## Kịch bản 1 — Test canh gác rule 028 (US3)

1. Viết `IncidentFastDetectionRuleDefinitionTests` (contract [incident-fast-detection-rule-contract.md](./contracts/incident-fast-detection-rule-contract.md)).
2. `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~IncidentFastDetectionRuleDefinitionTests` — thấy ĐỎ khi chưa khớp (hoặc với giá trị cố ý sai).
3. Cho khớp file hiện có, chạy lại thấy XANH; chạy `--filter FullyQualifiedName~ErrorBudgetRuleDefinitionTests` thấy XANH.

## Kịch bản 2 — Dashboard Xử lý sự cố đi theo thanh thời gian (US1)

1. Import file ndjson Xử lý sự cố vào Kibana sạch (hoặc cập nhật bản đang chạy bằng `overwrite=true`).
2. Mở dashboard: khoảng mặc định 1 giờ, tự làm mới 1 phút.
3. Đổi thanh thời gian giữa 15 phút, 1 giờ, 24 giờ: mọi panel đổi theo; không panel nào giữ cửa sổ cố định.
4. Tạo lưu lượng + lỗi (folder Postman 30: 5xx, 401/403, lời gọi hạ lưu). Xác nhận: service lỗi hiện ở bảng SLO, ở Phát hiện nhanh (cột trạng thái), ở biểu đồ 5xx theo phút; panel traffic có 401/403; panel hạ lưu có cặp lỗi/chậm; panel log lỗi hiện log Error kèm trace id và correlation id; bấm `trace_id` mở Discover lọc đúng trace đó.
5. Xác nhận **không còn** panel "Tổng 401 + 403".
6. Đọc file ndjson: không panel nào có `time_range` riêng hay truy vấn chứa `NOW() - 15 minutes`, `DATE_TRUNC(1 week`; `now-7d` chỉ được xuất hiện trong mẫu URL link trace của index-pattern logs.

## Kịch bản 3 — Dashboard Ngân sách tuần cố định tuần lịch (US2)

1. Import file ndjson Ngân sách tuần.
2. Đổi thanh thời gian bất kỳ: số liệu ngân sách không đổi (SC-003).
3. Tuần mặc định là `Tuần này`; chọn `Tuần trước`/`2 tuần trước`/`3 tuần trước` trong điều khiển: các panel tính từ traces đổi theo; hai panel cảnh báo/cạn không đổi và ghi rõ "trạng thái hiện tại".
4. Đối chiếu mức tiêu hao với truy vấn ES|QL chạy tay qua `POST /_query` (cùng tham số `tuan_chon`) và với folder Postman 30 (truy vấn đọc ngân sách tuần): khớp từng (service, ngân sách).
5. Kiểm `remaining_pct = 100 − consumed_pct` và `remaining_requests = allowed × total − bad` trên một service có lỗi.
6. Xem tiêu hao lũy kế theo ngày: không giảm theo ngày, ngày chưa tới của tuần hiện tại không có điểm.
7. Khi chưa có dữ liệu cho tuần: panel hiển thị trống/"không có dữ liệu", không số sai.

## Kịch bản 4 — Link qua lại (US3)

Từ Xử lý sự cố bấm link sang Ngân sách tuần và ngược lại; cả hai mở đúng dashboard sau khi import vào Kibana sạch.

## Kịch bản 5 — Bỏ dashboard cũ và dọn tham chiếu (US3)

1. `dashboards/` chỉ có đúng 2 file ndjson mới; không còn `slo-van-hanh-hang-ngay.ndjson`.
2. Tìm trên repo `e2e06ff5|slo-van-hanh-hang-ngay|SLO vận hành hằng ngày`: chỉ còn trong danh sách "không sửa" (SC-005).
3. Dashboard cũ trên Kibana đang chạy: **chỉ xoá sau khi người dùng xác nhận**.

## Kịch bản 6 — Tài liệu đi kèm (US4)

Mở từng file ở FR-016: PO, QA (bảng Thủ công trước Tự động sau), Architect, mục 030 trong 3 file debt, 3 drawio, folder Postman 30. Phát hiện chỉ ở `QA_Debt.md`.
