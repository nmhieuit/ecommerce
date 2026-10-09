# Quickstart: Kiểm chứng ngân sách chỉ đếm span Server

**Spec**: [spec.md](./spec.md) | **Contract**: [contracts/server-span-only-contract.md](./contracts/server-span-only-contract.md) | **Research**: [research.md](./research.md)

Điều kiện: stack local đang chạy (Elasticsearch `localhost:9200`, Kibana `localhost:5601`). Các bước chỉ đọc không cần hỏi; bước xoá/Disable/Enable ở mục 6 **phải hỏi người dùng ngay trước khi làm**.

## 1. Test quy ước (đỏ trước, xanh sau)

Trước khi sửa rule, chạy bộ test và ghi số test đỏ (kỳ vọng: các test Server mới đỏ cho cả 5 rule):

```bash
dotnet test tests/ServiceManifestSloConventionTests
```

Sau khi sửa export rule: toàn bộ xanh, gồm test hiện có của 027, 028, 033 (`HealthFailureRuleTests`).

## 2. Số liệu đối chiếu trên dữ liệu thật (V2)

Chạy truy vấn đếm chỉ-Server cho tuần hiện tại (`DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours`) và so từng (service, ngân sách) với rule mốc. Mốc tuần 05–11/10: `Bff.Api` 118 span / 9 lỗi 5xx, tổng 527 span.

```bash
curl -s -H 'Content-Type: application/json' 'localhost:9200/_query?format=txt' -d '{"query":"FROM traces-generic.otel-default* | WHERE kind == \"Server\" AND NOT (COALESCE(attributes.url.path, \"\") LIKE \"/health*\") | STATS n=COUNT(*) BY resource.attributes.service.name"}'
```

## 3. Rule frozen giữ sự kiện (V3)

Chạy ES|QL của `error-budget-frozen` đã sửa và xác nhận 20 sự kiện vẫn nằm trong kết quả trung gian (cột `is_event`), không bị điều kiện Server loại.

## 4. Import và kiểm dashboard (V1, V4)

Import hai file rule và hai file dashboard bằng một lệnh mỗi file (theo `alerts/README.md` và `dashboards/README.md`). Mở:
- `Ngân sách lỗi tuần — 7 service`: mức tiêu hao khớp bước 2 (sai lệch ≤ 2 điểm %).
- `Xử lý sự cố — 7 service`: Bảng SLO, 5xx/p95/traffic, status code, top endpoint chỉ đếm Server; panel `Lỗi gọi hạ lưu` vẫn hiện span Client.

## 5. Kịch bản lỗi qua Gateway → BFF → Products (US1)

Sau khi chốt nội dung folder Postman 34 (hỏi ở tasks), chạy với cờ tiêm lỗi bật và xác nhận: mỗi service chỉ tăng đúng 1 span Server lỗi cho mỗi request lỗi, BFF không tăng gấp đôi.

## 6. Dọn trạng thái sau triển khai (US4) — hỏi lại trước khi làm

Hiện 20 sự kiện cạn trong `slo-error-budget-events`. Liệt kê chúng cho người dùng, chờ xác nhận, rồi xoá sự kiện sinh từ công thức cũ và Disable/Enable `error-budget-100`. Kiểm chứng: không alert ngân sách nào active khi chưa có request mới.

## 7. Tài liệu (SC-006)

Chạy LỆNH-TÌM trong tasks để chắc không còn mô tả công thức "tính mọi span" ngoài các bản ghi lịch sử đã loại trừ.
