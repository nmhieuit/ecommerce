# Contract: Chỉ báo health lỗi (rule cảnh báo và panel)

**Spec**: [../spec.md](../spec.md) (FR-008, FR-009) | **Research**: [../research.md](../research.md) D5

Rule mới nằm trong một file export riêng trong `docs/kibana-quan-sat-he-thong/alerts/` (tên file, tên rule, tag, chu kỳ chạy: **người dùng chưa chốt, hỏi ở `/speckit-tasks`**). Test mới đọc đúng file này theo khuôn `IncidentFastDetectionRuleDefinitionTests`.

## Bất biến của rule

| # | Bất biến | Nguồn đối chiếu |
|---|---|---|
| 1 | Rule được export, loại `.es-query` ES|QL (`searchType: esqlQuery`), `groupBy: row`, `timeField: @timestamp`, có tag riêng (không dùng `slo-error-budget`) | file ndjson |
| 2 | Cửa sổ rule 5 phút (`timeWindowSize 5`, `m`) và truy vấn chỉ nhìn 5 phút gần nhất (`NOW() - 5 minutes`) | file ndjson |
| 3 | Chỉ tính span **có** tiền tố health: `COALESCE(attributes.url.path, "") LIKE "<tiền tố>*"` cho đúng tiền tố của manifest (không đảo thành `NOT`) | file ndjson, 7 manifest |
| 4 | Chỉ tính **5xx** (`attributes.http.response.status_code >= 500`), không tính độ trễ | file ndjson |
| 5 | Điều kiện bắn: `health_5xx / health_spans × 100 >= 50` theo service | file ndjson |
| 6 | Kết quả chỉ giữ cột `service` | file ndjson |
| 7 | `thresholdComparator: >` và `threshold: [0]` | file ndjson |
| 8 | Rule không có action/connector và không đọc/ghi `slo-error-budget-events` | file ndjson |

## Bất biến của panel

9. Panel trên dashboard `Xử lý sự cố — 7 service`: Discover session ES|QL theo thanh thời gian (không `time_range` riêng), mỗi service một dòng với `health_spans`, `health_5xx`, `health_5xx_pct`, sắp theo `health_5xx_pct` giảm dần, cùng tiền tố với rule; chỉ tính 5xx.
10. Rule và panel không làm đổi bất kỳ ngân sách nào (rule không ghi vào index sự kiện, panel không dùng công thức ngân sách).

## Quy ước test

- Lớp mới (tên đề xuất `HealthFailureRuleDefinitionTests`, hỏi ở tasks), comment tiếng Việt theo khuôn: `Kiểm tra` / `Lý do` / `Task nguồn: spec 033 — FR-010`.
- Test viết trước khi tạo rule/export; chạy thấy ĐỎ (file chưa có) rồi mới tạo.
- Bất biến 3 đọc tiền tố từ manifest qua `ServiceManifestFixture`, không hard-code.
