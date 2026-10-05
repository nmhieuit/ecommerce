# Contract: Hai dashboard Xử lý sự cố và Ngân sách lỗi tuần

**Spec**: [../spec.md](../spec.md) | **Research**: [../research.md](../research.md)

Hợp đồng bất biến cho hai file ndjson được export. Mọi thay đổi sau này phải giữ các bất biến dưới đây hoặc sửa contract này trước.

## Bất biến chung

1. **Mỗi dashboard một file ndjson** trong `docs/kibana-quan-sat-he-thong/dashboards/`, export bằng Saved Objects Export API với `includeReferencesDeep` (cách như `dashboards/README.md`). File ghi dashboard, mọi panel, saved search, index-pattern, điều khiển và link nó tham chiếu.
2. Import vào Kibana sạch bằng một lệnh import mỗi file, **không thao tác tay thêm** (SC-006).
3. Id hai dashboard là UUID **mới, cố định**, ghi vào bảng dưới (tạo ở T008). Link qua lại tham chiếu theo các id này.
4. Dashboard cũ `e2e06ff5-9cdf-4bea-acc8-5fd60ce26170` và file `slo-van-hanh-hang-ngay.ndjson` **không tồn tại** trong repo sau spec này (FR-001).
5. Không panel nào ở **cả hai** dashboard nhúng sẵn điều kiện thời gian cứng sai chủ đích (xem bất biến riêng).

| Dashboard | Id | File |
|---|---|---|
| Xử lý sự cố — 7 service | `e61fc7f3-17fe-428a-a373-da88af0a4a1e` | `xu-ly-su-co.ndjson` |
| Ngân sách lỗi tuần — 7 service | `2a607bf4-2449-48a1-a2e8-1336ec35a7b7` | `ngan-sach-loi-tuan.ndjson` |

## Dashboard Xử lý sự cố

6. Thời gian lưu cùng dashboard: `now-1h` → `now`; tự làm mới 1 phút (FR-004).
7. **Không panel nào** chứa `now-7d`, `NOW() - 15 minutes`, `DATE_TRUNC(1 week`, hay đầu tuần/tháng trong truy vấn hoặc cấu hình thời gian riêng (FR-002). Mọi panel đi theo thanh thời gian. Ngoại lệ duy nhất: mẫu URL định dạng của trường `trace_id` (index-pattern logs) mở Discover với khoảng `now-7d` để tìm trace; đó không phải cấu hình thời gian của panel.
8. Có đủ panel (FR-003): Bảng SLO + Markdown ngưỡng; `dotnet.exceptions`; phân bố status code; top endpoint chậm nhất; Phát hiện nhanh; 5xx theo phút theo service; p95 theo phút theo service; traffic + 401/403 theo phút theo service; lỗi gọi hạ lưu; log lỗi gần nhất. **Không** có panel "Tổng 401 + 403".
9. Phát hiện nhanh có cột trạng thái alert (`kibana.alert.status`) để phân biệt đang hoạt động với đã tắt (FR-007).
10. Lỗi gọi hạ lưu: `kind == "Client"`, bảng tổng hợp theo cặp (gọi → đích), `LIMIT 20`, span "xấu" = lỗi hoặc vượt ngưỡng p95 của service đích, **không** có cột/link trace (FR-005).
11. Log lỗi gần nhất: bộ lọc `severity_number >= 17`, 50 dòng, cột thời gian, service, message, trace id, correlation id; `trace_id` là link mở Discover (data view traces) lọc `trace_id` (FR-006); KHÔNG dùng link APM (V5 không đạt).
12. Có 1 ô Markdown ở đầu trang chứa link `/app/dashboards#/view/<id Ngân sách tuần>` (FR-011); không dùng panel Links.

## Dashboard Ngân sách lỗi tuần

13. Có điều khiển ES|QL kiểu tĩnh, biến `?tuan_chon`, giá trị `Tuần này` (mặc định) / `Tuần trước` / `2 tuần trước` / `3 tuần trước`. Mọi truy vấn tính từ traces tính `week_start` bằng `CASE(?tuan_chon == …)` từ `DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours` và lọc `@timestamp >= week_start AND @timestamp < week_start + 7 days`; mọi panel của dashboard có `time_range` riêng `now-30d` → `now` nên **không** phụ thuộc thanh thời gian (FR-008, FR-009).
14. Có đủ panel (FR-008): mức tiêu hao tuần (4 × 7); hạn mức còn lại (`remaining_pct`, `remaining_requests`); cảnh báo mốc; cạn ngân sách; error-rate theo ngày; p95 theo ngày; tiêu hao lũy kế theo ngày.
15. Công thức mức tiêu hao **khớp** rule mốc 027: cùng tỷ lệ cho phép 0.01/0.01/0.05/0.01, cùng ngưỡng độ trễ (p95 150 ms, p99 500 ms; `Bff.Api` 300/800 ms). Hai panel cảnh báo mốc và cạn là **trạng thái hiện tại** và ghi rõ điều đó trên dashboard (FR-009).
16. `remaining_requests = allowed × total − bad` theo tổng request tới hiện tại (FR-010).
17. Có 1 ô Markdown ở đầu trang chứa link `/app/dashboards#/view/<id Xử lý sự cố>` (FR-011); không dùng panel Links.

## Kiểm chứng

Theo [../quickstart.md](../quickstart.md) Kịch bản 1–6. Các bất biến 7, 13, 15 có thể kiểm bằng cách đọc file ndjson; 9–12, 14, 16–17 cần mở dashboard thật.
