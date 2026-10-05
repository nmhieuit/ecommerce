# Contract: Rule phát hiện nhanh `incident-fast-detection` và panel dashboard

**Feature**: [../spec.md](../spec.md) (FR-005, FR-006) | **Nguồn sự thật**:
`docs/kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson` (export Saved Objects).

## Bất biến

| # | Bất biến |
|---|---|
| 1 | Đúng một rule, loại `.es-query`, `searchType: esqlQuery`, `groupBy: row`, `schedule.interval = 5m`, `timeWindowSize = 5`, `timeWindowUnit = m`, tag `incident-fast-detection`. |
| 2 | Nguồn dữ liệu là `traces-generic.otel-default*` (giống dashboard 021 và rule 027); service lấy từ `resource.attributes.service.name`. |
| 3 | Một hàng (một alert) cho mỗi service mà, trong 5 phút gần nhất, `5xx / tổng ≥ 1%` HOẶC p95 `duration` > ngưỡng p95 HOẶC p99 `duration` > ngưỡng p99. Riêng `Gateway.Api` chỉ xét `5xx / tổng ≥ 1%` (người dùng chốt 2026-10-02). |
| 4 | Ngưỡng theo service khớp `slos` trong `service-manifest.yaml`: `Bff.Api` 300/800 ms, sáu service còn lại 150/500 ms (`duration` là ns, nhân 1 000 000). |
| 5 | Kết quả cuối chỉ có cột `service`, sinh từ lệnh `STATS ... BY service` cuối cùng (Ràng buộc 2–3 của research 027), nên alert không bị tạo lại mỗi lần chạy. |
| 6 | Service không có span nào trong 5 phút → không có hàng → không có alert. |
| 7 | Rule không có action/connector; 4 rule `slo-error-budget` của 027 và file `error-budget-rules.ndjson` không bị sửa. |
| 8 | Dashboard `SLO vận hành hằng ngày — 7 service` có thêm bảng "Phát hiện nhanh — vượt SLO trong 5 phút gần nhất", đọc alert active có tag `incident-fast-detection` từ `.alerts-stack.alerts-default`, hiện service và thời điểm bắt đầu. Các panel hiện có (021, 027) giữ nguyên. |
