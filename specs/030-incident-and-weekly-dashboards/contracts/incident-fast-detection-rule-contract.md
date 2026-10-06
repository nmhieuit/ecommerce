# Contract: Test canh gác rule `incident-fast-detection` (đóng sai lệch Nguyên tắc III của 028)

**Spec**: [../spec.md](../spec.md) (FR-015) | **Research**: [../research.md](../research.md) D11

Rule `incident-fast-detection` được export ở `docs/kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson` (id `9b0e2c36-678b-4cd7-9de0-7468d623f82d`). 028 không có test cho nó; spec 029 giữ nguyên sai lệch đó. Test mới đọc đúng file này, theo khuôn `tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs`, và đặt trong `IncidentFastDetectionRuleDefinitionTests.cs`.

Rule **không đổi** trong spec này; test khoá giá trị hiện có (tính đến 029). Test phải chạy thấy ĐỎ trước khi xanh: viết test, chạy với một giá trị cố ý sai trong bản sao để thấy đỏ rồi trả về (hoặc viết test trước khi đọc đúng file); ghi số test đỏ vào tasks.md.

## Bất biến

| # | Bất biến | Nguồn đối chiếu |
|---|---|---|
| 1 | Rule được export, là loại `.es-query` (ES|QL), có tag `incident-fast-detection`, chu kỳ `5m`, cửa sổ `5 m`, `groupBy: row`, `timeField: @timestamp` | file ndjson |
| 2 | Truy vấn chỉ nhìn 5 phút gần nhất (`NOW() - 5 minutes`) | file ndjson |
| 3 | Ngưỡng 5xx là `err_pct >= 1`, bằng SLO 5xx mặc định của hiến chương/manifest (5xx dưới 1%) | `PlatformSloDefaults`, 7 manifest |
| 4 | Ngưỡng p95/p99 theo từng service khớp manifest: p95 500 ms / p99 700 ms, `Bff.Api` 700/1000 ms, `Gateway.Api` 800/1100 ms | 7 manifest (`slos`) |
| 5 | Gateway được xét độ trễ như mọi service: `latency_breach` không có nhánh riêng cho `Gateway.Api` (từ 2026-10-06) | file ndjson |
| 6 | Kết quả chỉ giữ cột `service` (kết quả rule chỉ giữ cột định danh alert) | file ndjson |
| 7 | Ngưỡng cảnh báo `> 0` trên số service vi phạm (`thresholdComparator: >`, `threshold: [0]`) | file ndjson |

## Quy ước test

- Comment tiếng Việt theo khuôn hiện có: `Kiểm tra` / `Lý do` / `Task nguồn: spec 030 (test canh gác rule 028) — FR-015`.
- Không đổi `ErrorBudgetRuleDefinitionTests`; chạy lại để xác nhận xanh sau khi thêm test mới (SC-007).
- Bất biến 3 và 4 đọc giá trị từ manifest qua `ServiceManifestFixture`, không hard-code, để một manifest đổi ngưỡng làm test đỏ đúng chỗ.
