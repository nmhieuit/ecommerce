# Contract: Bộ rule ngân sách lỗi theo tuần, rule phát hiện nhanh và các panel ngân sách

**Feature**: [../spec.md](../spec.md) | **Thay thế**: `specs/027-error-budget-alerting/contracts/error-budget-alert-rules-contract.md`, ngưỡng 5xx trong `specs/028-incident-oncall-drill/contracts/fast-detection-rule-contract.md` (cả hai sẽ được sửa tại chỗ cho khớp contract này)

**Nguồn export**:
- `docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson`;
- `docs/kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson`;
- `docs/kibana-quan-sat-he-thong/dashboards/ngan-sach-loi-tuan.ndjson`.

**Người tiêu thụ**:
- `tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests`: bất biến 1–4, 1b, 11–13;
- [`../quickstart.md`](../quickstart.md): các bất biến còn lại, kiểm trên Kibana thật.

## Bộ rule

| Rule | Chu kỳ | Cửa sổ rule | Alert theo | Đổi so với trước |
|---|---|---|---|---|
| `error-budget-50` / `-75` / `-100` | 5 phút | `7 d` | (service, budget) | cửa sổ `31 d` → `7 d`; ranh giới tháng → tuần; tỷ lệ 5xx/khả dụng `0.001` → `0.01` |
| `error-budget-frozen` | 5 phút | `14 d` | service | cửa sổ `62 d` → `14 d`; ngày không đạt 5xx `>= 0.001` → `>= 0.01` |
| `incident-fast-detection` (028) | 5 phút | 5 phút (giữ) | service | `err_pct >= 0.1` → `err_pct >= 1` |

Rule `error-budget-100` giữ action Index → `slo-error-budget-events` (khi đổi trạng thái sang active). Mọi rule ngân sách mang tag `slo-error-budget`.

## Bất biến

| # | Bất biến | Nguồn |
|---|---|---|
| 1 | File export có đúng 4 rule ngân sách, mỗi rule chu kỳ `5m`, tag `slo-error-budget`. | FR-007 |
| 1b | Kết quả ES\|QL chỉ giữ cột định danh alert: `KEEP service, budget` (rule mốc), `KEEP service` (frozen). Cột định danh phải sinh từ lệnh `STATS` cuối cùng. | FR-007; ràng buộc kế thừa từ 027 |
| 2 | Ba rule mốc so sánh `consumed_pct` với đúng `50`, `75`, `100`. | FR-005 |
| 3 | Ngưỡng độ trễ theo từng service trong ES\|QL khớp `slos.latency.p95/p99` của manifest (đổi sang ns). | FR-015 |
| 4 | `allowed = CASE(...)` của rule mốc: `availability` → `0.01`, `error-rate` → `0.01`, `latency-p95` → `0.05`, `latency-p99` → `0.01`, không có nhánh mặc định. | FR-003 |
| 11 | Mỗi rule mốc có đúng dòng `WHERE @timestamp >= DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours`, và không còn `1 month`. | FR-001, FR-006 |
| 12 | Rule mốc có `timeWindowSize = 7`, `timeWindowUnit = d`. Rule frozen có `timeWindowSize = 14`, `timeWindowUnit = d`. | FR-006, FR-008 |
| 13 | Điều kiện ngày không đạt SLO của rule frozen dùng `0.01` cho 5xx, `0.05` cho p95, `0.01` cho p99. Ba con số này bằng tỷ lệ ngân sách ở bất biến 4. | FR-008 |
| 5 | Biểu thức ở bất biến 11 cho ra thứ Hai 00:00 giờ Việt Nam với cả 4 thời điểm giả định ở research.md Quyết định 9. | FR-001; research V1 |
| 6 | Alert bắn trong ≤ 1 chu kỳ (5 phút) kể từ khi mức tiêu hao thật vượt mốc, và giữ active chừng nào còn vượt. | FR-007, SC-002 |
| 7 | Service không có span trong tuần không sinh alert. Thiếu dữ liệu không bật hay tắt alert. | FR-013, SC-008 |
| 8 | Dashboard: 3 panel ngân sách mang tiêu đề "tuần này", `time_range = now-7d`; truy vấn mức tiêu hao dùng cùng biểu thức (bất biến 11) và cùng tỷ lệ (bất biến 4) với rule mốc; panel text ghi "`99%`/tuần". Id, vị trí và các panel khác không đổi. | FR-010, SC-004 |
| 9 | `error-budget-frozen` giữ active qua thứ Hai 00:00 giờ Việt Nam cho tới khi đủ 3 ngày đạt SLO, và được gỡ trong tuần nếu đủ 3 ngày dù ngân sách tuần vẫn 100%. | FR-005 |
| 10 | 5 bất biến của `specs/021-declare-service-slos/contracts/continuous-measurement-contract.md` vẫn thỏa (với con số SLO mới). | FR-015 |
| 14 | `incident-fast-detection` bắn khi 5xx trong 5 phút ≥ 1%, không bắn vì 5xx khi dưới 1%. Ngưỡng độ trễ và quy tắc "gateway chỉ xét 5xx" giữ nguyên. Không có test tự động; kiểm bằng quickstart. | FR-009, SC-005 |
