# Contract: Bất biến của cơ chế đo lường liên tục (dashboard SLO)

**Feature**: [../spec.md](../spec.md)

Đây là hợp đồng đầu ra cho User Story 3 — danh sách bất biến ngắn gọn, ổn định mà dashboard
`SLO vận hành hằng ngày — 7 service` (Kibana, saved object id `e2e06ff5-9cdf-4bea-acc8-5fd60ce26170`,
định nghĩa đầy đủ tại [`docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md`](../../../docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md),
export tại [`docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson`](../../../docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson))
PHẢI luôn thỏa để được xem là cơ chế đo lường liên tục hợp lệ cho tính năng này. File này không lặp
lại nhật ký xây dựng chi tiết — chỉ nêu bất biến để `quickstart.md` và mọi lần sửa dashboard sau này
đối chiếu.

## Bất biến mà mọi lần đo/hiển thị PHẢI thỏa (đúng theo Functional Requirements)

| # | Bất biến | Nguồn |
|---|---|---|
| 1 | Với mỗi trong 7 service, dashboard hiển thị giá trị đo được thật (không phải giá trị khai báo lặp lại) cho cả 3 chỉ số: error-rate, latency p95, latency p99 — tính từ dữ liệu traces OTel trong cửa sổ thời gian gần đây (mặc định 24 giờ). | FR-004 |
| 2 | Mỗi chỉ số đo được có thể đối chiếu trực tiếp với ngưỡng đã khai báo tương ứng của cùng service (cột "Ngưỡng" cạnh cột "Thực tế", hoặc tương đương). | FR-005, entity **Giá trị đo được liên tục** |
| 3 | Khi một service không có span nào trong cửa sổ đang xét, dashboard thể hiện rõ "không có dữ liệu" — KHÔNG hiển thị `0%` lỗi hay bất kỳ giá trị nào ngụ ý service đạt ngân sách hoàn hảo. | FR-006 |
| 4 | Khi hiệu năng thực tế của một service thay đổi (ví dụ một endpoint bị làm chậm), giá trị đo được của service đó phản ánh thay đổi trong cùng cửa sổ thời gian đang xét mà không cần thao tác thu thập lại số liệu thủ công. | FR-007 |
| 5 | Availability được suy ra xấp xỉ từ `100% − error-rate` (chưa có synthetic uptime check riêng — xem Assumptions của spec.md) — đây là một xấp xỉ đã biết, không phải một thiếu sót cần sửa trong phạm vi tính năng này. | Assumptions (spec.md) |

## Ngoài phạm vi hợp đồng này

- Cảnh báo chủ động (alert rule) khi vượt ngân sách — thuộc SCRUM-35, không phải bất biến của
  dashboard này.
- Đo Availability bằng synthetic uptime check thật — chưa có hạ tầng, ngoài phạm vi spec.md.

## Người tiêu thụ hợp đồng này

- `quickstart.md` — Bước 3 và Bước 4 xác thực trực tiếp bất biến 1–4 trên dashboard đang chạy thật.
- Bất kỳ ai sửa Lens Formula hoặc cấu trúc dashboard sau này — đối chiếu lại 5 bất biến trên trước khi
  coi thay đổi là an toàn, thay vì chỉ dựa vào việc dashboard "trông vẫn ổn".
