# Contract: Bộ rule ngân sách lỗi theo tuần, rule phát hiện nhanh và các panel ngân sách

**Feature**: [../spec.md](../spec.md) | **Thay thế**: `specs/027-error-budget-alerting/contracts/error-budget-alert-rules-contract.md`, ngưỡng 5xx trong `specs/028-incident-oncall-drill/contracts/fast-detection-rule-contract.md` (cả hai sẽ được sửa tại chỗ cho khớp contract này)

**Nguồn export**:
- `docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson`;
- `docs/kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson`;
- `docs/kibana-quan-sat-he-thong/dashboards/ngan-sach-loi-tuan.ndjson`.

**Người tiêu thụ**:
- `tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests`: bất biến 1–4, 1b, 11–13, 15;
- `tests/ServiceManifestSloConventionTests/FrozenPanelStatusTests`: bất biến 16;
- [`../quickstart.md`](../quickstart.md): các bất biến còn lại, kiểm trên Kibana thật.

## Bộ rule

| Rule | Chu kỳ | Cửa sổ rule | Alert theo | Đổi so với trước |
|---|---|---|---|---|
| `error-budget-50` / `-75` / `-100` | 5 phút | `7 d` | (service, budget) | cửa sổ `31 d` → `7 d`; ranh giới tháng → tuần; tỷ lệ 5xx/khả dụng `0.001` → `0.01` |
| `error-budget-frozen` | 5 phút | `14 d` | service | cửa sổ `62 d` → `14 d`; ngày không đạt 5xx `>= 0.001` → `>= 0.01`. *Thay bởi nhánh fix/frozen-panel-status (2026-10-09)*: hồi phục theo mức tiêu hao tuần (dưới 75%), không còn đếm ngày đạt SLO; thêm action ghi sự kiện `recovered` |
| `incident-fast-detection` (028) | 5 phút | 5 phút (giữ) | service | `err_pct >= 0.1` → `err_pct >= 1` |

Rule `error-budget-100` giữ action Index → `slo-error-budget-events` (khi đổi trạng thái sang active). Từ nhánh fix/frozen-panel-status, rule `error-budget-frozen` có action Index → cùng connector, nhóm `recovered` (khi alert đổi sang recovered), ghi `{"service": "{{alert.id}}", "event": "recovered"}`. Mọi rule ngân sách mang tag `slo-error-budget`.

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
| 13 | Mức tiêu hao cao nhất của rule frozen là `GREATEST(TO_DOUBLE(bad_5xx) / week_spans / 0.01, TO_DOUBLE(bad_p95) / week_spans / 0.05, TO_DOUBLE(bad_p99) / week_spans / 0.01)`, chỉ tính span Server từ `week_start = DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours`; ba tỷ lệ bằng tỷ lệ ngân sách ở bất biến 4. Rule giữ service khi `WHERE week_spans < <min-requests-to-recover> OR max_consumed_pct >= <recovered-below-consumption>` (hai số lấy từ manifest, hiện `1` và `75`), và chỉ xét service có `exhausted_at > recovered_at`. *(Sửa bởi nhánh fix/frozen-panel-status, 2026-10-09; trước đây là điều kiện ngày không đạt SLO.)* | FR-005, FR-008 |
| 5 | Biểu thức ở bất biến 11 cho ra thứ Hai 00:00 giờ Việt Nam với cả 4 thời điểm giả định ở research.md Quyết định 9. | FR-001; research V1 |
| 6 | Alert bắn trong ≤ 1 chu kỳ (5 phút) kể từ khi mức tiêu hao thật vượt mốc, và giữ active chừng nào còn vượt. | FR-007, SC-002 |
| 7 | Service không có span trong tuần không sinh alert. Thiếu dữ liệu không bật hay tắt alert. | FR-013, SC-008 |
| 8 | Dashboard: 3 panel ngân sách mang tiêu đề "tuần này", `time_range = now-7d`; truy vấn mức tiêu hao dùng cùng biểu thức (bất biến 11) và cùng tỷ lệ (bất biến 4) với rule mốc; panel text ghi "`99%`/tuần". Id, vị trí và các panel khác không đổi. | FR-010, SC-004 |
| 9 | `error-budget-frozen` giữ active qua thứ Hai 00:00 giờ Việt Nam (tuần mới chưa có request Server thì vẫn đóng băng), được gỡ (recovered) khi tuần đã có ≥ 1 request và mức tiêu hao cao nhất dưới 75%; đã recovered thì không đóng băng lại cho tới sự kiện `exhausted` mới. *(Sửa bởi nhánh fix/frozen-panel-status, 2026-10-09; trước đây là "đủ 3 ngày đạt SLO".)* | FR-005 |
| 10 | 5 bất biến của `specs/021-declare-service-slos/contracts/continuous-measurement-contract.md` vẫn thỏa (với con số SLO mới). | FR-015 |
| 15 | Rule frozen có đúng một action `.index`, nhóm `recovered`, document `{"service": "{{alert.id}}", "event": "recovered"}`. Mã alert của rule frozen là tên service nên `{{alert.id}}` ra tên service (đã kiểm chứng trên Kibana 9.4.4, xem QA_Debt). | FR-005 (nhánh fix/frozen-panel-status) |
| 16 | Panel "Cạn ngân sách — ưu tiên độ tin cậy (trạng thái hiện tại)" (`slo-error-budget-frozen`) có cột `service, status, consumed_max_pct, frozen_since, recovered_at`; `status = CASE(frozen_since IS NOT NULL AND consumed_max_pct >= 100, "active", frozen_since IS NOT NULL, "recovering", "recovered")`; mức tiêu hao cùng công thức và cùng tập span (Server, không `/health*`, từ đầu tuần hiện tại) với rule frozen. | FR-005 (nhánh fix/frozen-panel-status) |
| 14 | `incident-fast-detection` bắn khi 5xx trong 5 phút ≥ 1%, không bắn vì 5xx khi dưới 1%. Ngưỡng độ trễ và quy tắc "gateway chỉ xét 5xx" giữ nguyên. Không có test tự động; kiểm bằng quickstart. | FR-009, SC-005 |
