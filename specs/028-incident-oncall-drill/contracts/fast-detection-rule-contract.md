# Contract: Rule phát hiện nhanh `incident-fast-detection` và panel dashboard

**Feature**: [../spec.md](../spec.md) (FR-005, FR-006) | **Nguồn sự thật**:
`docs/kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson` (export Saved Objects).

## Bất biến

| # | Bất biến |
|---|---|
| 1 | Đúng một rule, loại `.es-query`, `searchType: esqlQuery`, `groupBy: row`, `schedule.interval = 5m`, `timeWindowSize = 5`, `timeWindowUnit = m`, tag `incident-fast-detection`. |
| 2 | Nguồn dữ liệu là `traces-generic.otel-default*` (giống dashboard 021 và rule 027); service lấy từ `resource.attributes.service.name`. |
| 3 | Một hàng (một alert) cho mỗi service mà, trong 5 phút gần nhất, `5xx / tổng ≥ 1%` HOẶC p95 `duration` > ngưỡng p95 HOẶC p99 `duration` > ngưỡng p99. `Gateway.Api` được xét giống mọi service (2026-10-06; trước đó chỉ xét 5xx). |
| 4 | Ngưỡng theo service khớp `slos` trong `service-manifest.yaml`: `Bff.Api` 700/1000 ms, `Gateway.Api` 800/1100 ms, năm service còn lại 500/700 ms (`duration` là ns, nhân 1 000 000). |
| 5 | Kết quả cuối chỉ có cột `service`, sinh từ lệnh `STATS ... BY service` cuối cùng (Ràng buộc 2–3 của research 027), nên alert không bị tạo lại mỗi lần chạy. |
| 6 | Service không có span nào trong 5 phút → không có hàng → không có alert. |
| 7 | Rule không có action/connector; 4 rule `slo-error-budget` của 027 và file `error-budget-rules.ndjson` không bị sửa. |
| 8 | Dashboard `Xử lý sự cố — 7 service` có thêm bảng "Phát hiện nhanh — service vượt SLO trong khoảng thời gian đã chọn", đọc alert active có tag `incident-fast-detection` từ `.alerts-stack.alerts-default`, hiện service, trạng thái alert và thời điểm bắt đầu. Từ spec 030 bảng đi theo thanh thời gian (không còn lọc 15 phút) và có test canh gác rule `IncidentFastDetectionRuleDefinitionTests`. |
| 9 | Từ spec 034, ES|QL của rule chỉ đếm span `kind = Server`: đúng một dòng `WHERE kind == "Server"` ngay sau điều kiện loại `/health*` (spec 033) và trước mọi `EVAL`. Span Client/Producer không làm rule bắn. Test: `Rule_CountsOnlyServerSpans_BeforeAnyCalculation`. Chi tiết: `specs/034-error-budget-server-spans/contracts/server-span-only-contract.md`. |
