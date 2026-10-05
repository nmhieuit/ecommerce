# Contract: Bộ rule cảnh báo ngân sách lỗi và phần hiển thị trên dashboard

**Feature**: [../spec.md](../spec.md) | **Nguồn export**: `docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson`

**Người tiêu thụ**: `tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests` (bất
biến 1–4, đọc file export), [`../quickstart.md`](../quickstart.md) (bất biến 5–10, trên Kibana thật).

## Bộ rule

| Rule | Loại | Chu kỳ | Alert theo | Action |
|---|---|---|---|---|
| `error-budget-50` | Elasticsearch query (ES|QL) | 5 phút | (service, budget) | — |
| `error-budget-75` | Elasticsearch query (ES|QL) | 5 phút | (service, budget) | — |
| `error-budget-100` | Elasticsearch query (ES|QL) | 5 phút | (service, budget) | Index → `slo-error-budget-events`, khi đổi trạng thái sang active |
| `error-budget-frozen` | Elasticsearch query (ES|QL) | 5 phút | service | — |

Mọi rule mang tag `slo-error-budget`.

## Bất biến

| # | Bất biến | Nguồn |
|---|---|---|
| 1 | File export có đúng 4 rule trên, mỗi rule chu kỳ `5m`, tag `slo-error-budget`. | FR-006, research Q3 |
| 1b | Kết quả ES|QL của mọi rule chỉ có cột định danh alert (`KEEP service, budget` cho rule mốc, `KEEP service` cho `error-budget-frozen`) — không có cột số thay đổi theo thời gian, để mã alert ổn định qua các lần chạy. | FR-007, research "Ràng buộc 2" |
| 2 | Ba rule mốc so sánh `consumed_pct` với đúng `50`, `75`, `100`. | FR-006 |
| 3 | Ngưỡng độ trễ theo từng service trong ES|QL khớp `slos.latency.p95/p99` của manifest tương ứng (đổi sang nanosecond). | FR-013 |
| 4 | Tỷ lệ cho phép trong ES|QL là `0.01`, `0.01`, `0.05`, `0.01` cho `availability`, `error-rate`, `latency-p95`, `latency-p99`. | FR-002 |
| 5 | Cửa sổ tính bắt đầu đúng thứ Hai 00:00 của tuần hiện tại giờ Việt Nam. | FR-003 |
| 6 | Alert kích hoạt trong vòng 1 chu kỳ (≤ 5 phút) kể từ khi mức tiêu hao thật vượt mốc, và giữ active chừng nào còn vượt. | FR-006, FR-007, SC-002 |
| 7 | Service không có span nào trong tuần không sinh alert nào; thiếu dữ liệu không bật hay tắt alert. | FR-012, SC-005 |
| 8 | Dashboard Ngân sách lỗi tuần có nhóm panel "Ngân sách lỗi tuần này" ở trên cùng: bảng mức tiêu hao 7 × 4 (%), bảng alert đang active (service, ngân sách, mốc) đặt cạnh bảng mức tiêu hao, bảng service đang "cạn — ưu tiên độ tin cậy". | FR-008, FR-011, SC-003 |
| 9 | `error-budget-frozen` giữ active sau khi sang tuần mới cho tới khi đủ 3 ngày đạt SLO; được gỡ trong tuần nếu đủ 3 ngày đạt SLO dù ngân sách tuần vẫn 100%. | FR-010 |
| 10 | 5 bất biến của `specs/021-declare-service-slos/contracts/continuous-measurement-contract.md` vẫn thỏa sau khi sửa dashboard. | FR-015 (không phá cái đã có) |
