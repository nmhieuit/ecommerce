# 07 — Cảnh báo ngân sách lỗi (error budget)

*(Cần đã làm file 06 — file này thêm cảnh báo tự động lên đúng dashboard Ngân sách lỗi tuần của file 06.)*

Đặc tả: [`specs/027-error-budget-alerting/`](../../specs/027-error-budget-alerting/spec.md) (SCRUM-35), chu kỳ tuần lịch
và SLO 99%/1% theo [`specs/029-error-budget-weekly/`](../../specs/029-error-budget-weekly/spec.md).
Hợp đồng: [`contracts/error-budget-alert-rules-contract.md`](../../specs/029-error-budget-weekly/contracts/error-budget-alert-rules-contract.md).
Export thật: [`alerts/error-budget-rules.ndjson`](alerts/error-budget-rules.ndjson) (4 rule + connector),
[`dashboards/ngan-sach-loi-tuan.ndjson`](dashboards/ngan-sach-loi-tuan.ndjson) (dashboard).

Chính sách (con số, hệ quả, điều kiện hồi phục) nằm trong khối `error-budget-policy` của từng
`service-manifest.yaml`. File này chỉ nói cách Kibana đo và cảnh báo theo chính sách đó.

Chu kỳ ngân sách là **tuần lịch giờ Việt Nam**: từ thứ Hai 00:00 tới Chủ nhật 23:59 (UTC+7), đặt lại vào
thứ Hai 00:00 mỗi tuần. Tỷ lệ request xấu cho phép: khả dụng 1%, 5xx 1%, vượt p95 5%, vượt p99 1%.

## Điều kiện trước: khoá mã hoá Kibana

Kibana Alerting không cho tạo hay chạy rule nếu thiếu `xpack.encryptedSavedObjects.encryptionKey`.
Khoá lấy từ `KIBANA_ENCRYPTION_KEY` trong `.env` (Vùng 2 của `.env.example`), compose truyền vào qua
`XPACK_ENCRYPTEDSAVEDOBJECTS_ENCRYPTIONKEY`. Kiểm tra:

```powershell
Invoke-RestMethod http://localhost:5601/api/alerting/_health | Select-Object has_permanent_encryption_key
```

## Truy vấn chuẩn: mức tiêu hao 4 ngân sách × 7 service

Mọi rule mốc và bảng "mức tiêu hao" trên dashboard dùng **cùng một truy vấn** (bảng dashboard chỉ thay `NOW()` bằng đầu tuần theo `?tuan_chon`) dưới đây, chỉ khác dòng
`WHERE consumed_pct >= N` cuối cùng. `tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs`
đọc rule đã export và dựa vào đúng 3 hình dạng sau — sửa truy vấn thì giữ nguyên chúng:

- `p95_ns = CASE(service == "<Tên>.Api", <ns>, ..., <ns mặc định>)` và `p99_ns = CASE(...)`: ngưỡng độ
  trễ theo service, bằng `slos.latency` của manifest × 1 000 000 (`duration` tính bằng nanosecond).
- `allowed = CASE(budget == "availability", 0.01, ...)`: tỷ lệ request xấu cho phép, không có nhánh
  mặc định.
- `WHERE consumed_pct >= N`: đúng một lần, `N` là mốc của rule.
- `WHERE NOT (COALESCE(attributes.url.path, "") LIKE "<tiền tố>*")`: đúng một lần cho mỗi tiền tố trong `error-budget-policy.excluded-path-prefixes` của manifest (hiện `/health`), đứng **trước** lệnh `EVAL` đầu tiên (spec 033). `COALESCE` là bắt buộc: thiếu nó, `NOT (null LIKE …)` loại luôn span không có đường dẫn. `LIKE` phân biệt hoa thường. Rule `error-budget-frozen` vẫn đọc cả index `slo-error-budget-events`.
- `WHERE kind == "Server"`: đúng một lần, ngay sau điều kiện loại `/health*` và trước lệnh `EVAL` đầu tiên (spec 034): chỉ span Server (request mà chính service nhận) vào mẫu số và tập span xấu; span Client/Producer không tính. Riêng `error-budget-frozen` dùng `WHERE (kind == "Server" OR _index LIKE "*slo-error-budget-events*")` vì sự kiện cạn không có cột `kind` (thiếu vế `_index` thì rule mất toàn bộ sự kiện). Test: `BudgetRule_CountsOnlyServerSpans_BeforeAnyCalculation` và `FrozenRule_CountsOnlyServerSpans_ButKeepsTheExhaustionEvents`.
- `WHERE @timestamp >= DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours`: đúng một lần — đầu tuần giờ Việt
  Nam. ES|QL làm tròn tuần bắt đầu từ thứ Hai; biểu thức đã kiểm chứng với 4 thời điểm quanh ranh giới
  thứ Hai 00:00 giờ Việt Nam (`specs/029-error-budget-weekly/research.md` V1).

Phần chung (dùng nguyên văn cho bảng mức tiêu hao trên dashboard, thêm `| KEEP service, budget, consumed_pct`):

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp >= DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours
| WHERE NOT (COALESCE(attributes.url.path, "") LIKE "/health*")
| WHERE kind == "Server"
| EVAL service = resource.attributes.service.name
| EVAL p95_ns = CASE(service == "Bff.Api", 700000000, service == "Gateway.Api", 800000000, 500000000)
| EVAL p99_ns = CASE(service == "Bff.Api", 1000000000, service == "Gateway.Api", 1100000000, 700000000)
| EVAL is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0)
| EVAL over_p95 = CASE(duration > p95_ns, 1, 0), over_p99 = CASE(duration > p99_ns, 1, 0)
| STATS total = COUNT(*), bad_5xx = SUM(is_5xx), bad_p95 = SUM(over_p95), bad_p99 = SUM(over_p99) BY service
| EVAL budget = ["availability", "error-rate", "latency-p95", "latency-p99"]
| MV_EXPAND budget
| EVAL bad = CASE(budget == "latency-p95", bad_p95, budget == "latency-p99", bad_p99, bad_5xx)
| EVAL allowed = CASE(budget == "availability", 0.01, budget == "error-rate", 0.01, budget == "latency-p95", 0.05, budget == "latency-p99", 0.01)
| EVAL consumed_pct = ROUND(TO_DOUBLE(bad) / total / allowed * 100.0, 2)
```

Ba rule mốc nối thêm đúng 3 dòng (N = 50, 75, 100):

```esql
| STATS consumed_pct = MAX(consumed_pct) BY service, budget
| WHERE consumed_pct >= N
| KEEP service, budget
```

Vì sao có 3 dòng này, và vì sao không được bỏ:

- `STATS ... BY service, budget`: Kibana chỉ dùng cột của lệnh `STATS` **cuối cùng** làm mã alert.
  Thiếu dòng này thì `budget` (sinh bởi `MV_EXPAND`) bị bỏ, hai ngân sách của cùng service gộp thành
  một alert (ràng buộc kỹ thuật kế thừa từ 027).
- `KEEP service, budget`: Kibana ghép mã alert từ giá trị mọi cột kết quả. Giữ cột `consumed_pct` thì
  mã alert đổi mỗi lần chạy, alert "recovered" rồi mọc lại mỗi 5 phút thay vì giữ active liên tục
  (ràng buộc kỹ thuật kế thừa từ 027). Mức tiêu hao (%) vì vậy hiện ở bảng riêng trên dashboard, không trong alert.
- Một service không có span nào từ đầu tuần thì không có hàng nào → không có alert ("không có dữ
  liệu" khác "0%", FR-012).

## Ba rule mốc

| Rule | Id saved object | Loại | Chu kỳ | Cửa sổ rule | Alert theo |
|---|---|---|---|---|---|
| `error-budget-50` | `4169d562-aca7-47af-8da1-63511967b07f` | Elasticsearch query (ES\|QL), `groupBy: row` | 5 phút | 7 ngày | (service, budget) |
| `error-budget-75` | `f724cf89-13ac-4f9e-af09-f89e5e436917` | như trên | 5 phút | 7 ngày | (service, budget) |
| `error-budget-100` | `a3581b2a-3eda-4a2f-8f4d-a9af04c8fac3` | như trên | 5 phút | 7 ngày | (service, budget) |

Tạo bằng Kibana Alerting API (`POST /api/alerting/rule`, `rule_type_id: .es-query`,
`consumer: stackAlerts`, `params.searchType: esqlQuery`, `threshold: [0]`, `thresholdComparator: ">"`,
`excludeHitsFromPreviousRun: false`), tag `slo-error-budget`. Cửa sổ rule 7 ngày là bắt buộc: rule tự lọc
`@timestamp` theo cửa sổ của nó trước khi chạy ES|QL; một tuần lịch dài tối đa 6 ngày 23 giờ 59 phút tính
từ thứ Hai 00:00, nên 7 ngày đủ phủ, còn dòng `WHERE` đầu truy vấn cắt lại đúng từ thứ Hai 00:00 giờ Việt
Nam. Cửa sổ ngắn hơn 7 ngày sẽ âm thầm cắt hụt request đầu tuần.

## Panel ngân sách trên dashboard `Ngân sách lỗi tuần — 7 service`

Từ spec 030 các panel ngân sách nằm ở dashboard riêng `Ngân sách lỗi tuần — 7 service` (cố định tuần lịch giờ Việt Nam, không phụ thuộc thanh
thời gian; xem [06](06-dashboard-xu-ly-su-co-va-ngan-sach-tuan.md)). Mỗi panel Discover session dạng ES|QL có khoảng thời gian riêng `now-30d → now` để không bị thanh thời gian cắt thêm.
Điều khiển `Tuần` (biến `?tuan_chon`: `Tuần này` / `Tuần trước` / `2 tuần trước` / `3 tuần trước`) chọn tuần cho hai panel tính từ traces; truy vấn dashboard tính
`week_start = CASE(?tuan_chon == ..., t0, t0 - 7 days, ...)` với `t0 = DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours` thay cho đầu tuần hiện tại.

| Panel | Discover session id | Truy vấn | Cột |
|---|---|---|---|
| Mức tiêu hao ngân sách (%) — tuần đã chọn | `slo-error-budget-consumption` | phần chung ở trên (đầu tuần theo `?tuan_chon`) + `EVAL moc = CASE(consumed_pct >= 100, "100% - CẠN", >= 75 "75%", >= 50 "50%", "dưới 50%")`, sắp giảm dần theo `consumed_pct` | `service`, `budget`, `consumed_pct`, `moc` |
| Hạn mức còn lại — tuần đã chọn | (nhúng trong dashboard) | phần chung + `remaining_pct = 100 - consumed_pct`, `remaining_requests = FLOOR(allowed * total - bad)` | `service`, `budget`, `remaining_pct`, `remaining_requests` |
| Cảnh báo mốc đang hoạt động (trạng thái hiện tại) | `slo-error-budget-active-alerts` | `FROM .alerts-stack.alerts-default`, lọc tag `slo-error-budget` + `status == "active"` + `@timestamp >= đầu tuần hiện tại`, lấy `kibana.alert.grouping.service/budget` | `service`, `budget`, `moc`, `kibana.alert.start` |

Vì sao Discover session thay vì Lens tô màu: lúc dựng, thao tác UI Lens qua trình duyệt tự động hoá
không ổn định; người dùng chốt dựng bằng Saved Objects API với Discover session (cấu trúc JSON đơn giản,
lặp lại được). Mốc hiển thị bằng cột chữ `moc` thay vì tô màu.

## Trạng thái "cạn ngân sách — ưu tiên độ tin cậy"

### Index sự kiện `slo-error-budget-events`

Tạo một lần (Dev Tools hoặc `curl`) trước khi import rule — connector Index không tự tạo mapping đúng:

```http
PUT slo-error-budget-events
{"mappings":{"properties":{"@timestamp":{"type":"date"},"service":{"type":"keyword"},"budget":{"type":"keyword"},"event":{"type":"keyword"}}}}
```

### Connector và action

- Connector `slo-error-budget-events (Index)` (id `709d5d97-0731-4d70-b244-fd8cf5f4195d`, loại `.index`):
  `config.index = slo-error-budget-events`, `refresh: true`, `executionTimeField: @timestamp` (Kibana tự
  ghi thời điểm chạy action vào `@timestamp`).
- Action trên rule `error-budget-100`: nhóm `query matched`, `frequency.summary: false`,
  `notify_when: onActionGroupChange` — chỉ ghi khi một alert (service, budget) **chuyển sang** active,
  document `{"service": "{{context.grouping.service}}", "budget": "{{context.grouping.budget}}", "event": "exhausted"}`.
- Action trên rule `error-budget-frozen` (nhánh fix/frozen-panel-status): nhóm `recovered`, `frequency.summary: false`,
  `notify_when: onActionGroupChange` — chỉ ghi khi alert đóng băng của một service **chuyển sang** recovered (rule
  gỡ đóng băng), document `{"service": "{{alert.id}}", "event": "recovered"}`. Mã alert của rule frozen chính là
  tên service (truy vấn chỉ `KEEP service`), nên `{{alert.id}}` ra `Baskets.Api`, ...

### Trạng thái đóng băng: active → recovering → recovered

Quy tắc ở khối `error-budget-policy.recovery` của manifest (nhánh fix/frozen-panel-status, thay quy tắc "3 ngày liên
tiếp đạt SLO" của spec 027 FR-010 / 029 FR-005). Sau một lần cạn (sự kiện `exhausted`), xét **mức tiêu hao cao nhất
trong 4 ngân sách của tuần lịch hiện tại** (cùng công thức với bảng mức tiêu hao):

```text
            sự kiện "exhausted" (rule error-budget-100 chạm 100%)
                                  │
                                  ▼
   ┌──────────────── active ─────────────────┐   mức tiêu hao >= 100%           ĐÓNG BĂNG
   │                    │  ▲                  │
   │      < 100%        │  │  >= 100%         │
   │                    ▼  │                  │
   │              recovering                  │   75% <= mức tiêu hao < 100%,    ĐÓNG BĂNG
   │                    │                     │   hoặc tuần chưa có request Server
   │   < 75% và tuần    │                     │   (thứ Hai 00:00 KHÔNG tự gỡ)
   │   có >= 1 request  ▼                     │
   └──────────────► recovered ────────────────┘   hết đóng băng; giữ nguyên dù lên lại 75–99%,
                                                  chỉ quay lại active khi có sự kiện "exhausted" mới
```

| Khoá manifest | Giá trị | Nghĩa |
|---|---|---|
| `recovered-below-consumption` | `75%` | recovered khi mức tiêu hao cao nhất dưới 75% |
| `min-requests-to-recover` | `1` | tuần phải có ít nhất 1 request Server mới xét được; chưa có thì vẫn recovering |
| `recovering-keeps-freeze` | `true` | recovering vẫn dừng merge tính năng mới |
| `recovered-stays-until-exhausted` | `true` | đã recovered thì giữ tới lần cạn kế tiếp |
| `budget-reset-clears-freeze` | `false` | ngân sách đặt lại thứ Hai không gỡ đóng băng |

### Rule `error-budget-frozen`

Id `a16eed47-01ed-40ce-a8be-c264bde1b771`, cùng loại với rule mốc, chu kỳ 5 phút, **cửa sổ rule 14 ngày**
(lần cạn có thể ở tuần trước), alert theo `service`. Alert **active** = service đang đóng băng (active hoặc
recovering); alert **recovered** = rule gỡ đóng băng.

```esql
FROM traces-generic.otel-default*, slo-error-budget-events METADATA _index
| WHERE NOT (COALESCE(attributes.url.path, "") LIKE "/health*")
| WHERE (kind == "Server" OR _index LIKE "*slo-error-budget-events*")
| EVAL is_event = CASE(_index LIKE "*slo-error-budget-events*", 1, 0)
| EVAL service = COALESCE(resource.attributes.service.name, service)
| EVAL week_start = DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours
| EVAL in_week = CASE(is_event == 0 AND @timestamp >= week_start, 1, 0)
| EVAL p95_ns = CASE(service == "Bff.Api", 700000000, service == "Gateway.Api", 800000000, 500000000)
| EVAL p99_ns = CASE(service == "Bff.Api", 1000000000, service == "Gateway.Api", 1100000000, 700000000)
| EVAL is_5xx = CASE(in_week == 1 AND attributes.http.response.status_code >= 500, 1, 0)
| EVAL over_p95 = CASE(in_week == 1 AND duration > p95_ns, 1, 0), over_p99 = CASE(in_week == 1 AND duration > p99_ns, 1, 0)
| EVAL exhausted_ts = CASE(is_event == 1 AND event == "exhausted", @timestamp, NULL), recovered_ts = CASE(is_event == 1 AND event == "recovered", @timestamp, NULL)
| STATS week_spans = SUM(in_week), bad_5xx = SUM(is_5xx), bad_p95 = SUM(over_p95), bad_p99 = SUM(over_p99), exhausted_at = MAX(exhausted_ts), recovered_at = MAX(recovered_ts) BY service
| WHERE exhausted_at IS NOT NULL AND (recovered_at IS NULL OR exhausted_at > recovered_at)
| EVAL max_consumed_pct = CASE(week_spans > 0, GREATEST(TO_DOUBLE(bad_5xx) / week_spans / 0.01, TO_DOUBLE(bad_p95) / week_spans / 0.05, TO_DOUBLE(bad_p99) / week_spans / 0.01) * 100.0, NULL)
| WHERE week_spans < 1 OR max_consumed_pct >= 75
| KEEP service
```

Đọc truy vấn theo 3 bước:

1. Theo service: `week_spans` và 3 số span xấu chỉ tính span Server **từ thứ Hai 00:00 giờ Việt Nam của tuần
   hiện tại** (`in_week`); `exhausted_at` / `recovered_at` = sự kiện "cạn" / "hồi phục" gần nhất trong cửa sổ 14 ngày.
2. Chỉ xét service có lần cạn mới hơn lần hồi phục gần nhất (`exhausted_at > recovered_at`) — đây là chỗ giữ
   `recovered` cho tới lần cạn kế tiếp: service đã recovered thì không còn hàng nào dù mức tiêu hao lên lại 75–99%.
3. `max_consumed_pct` = `GREATEST` của 3 tỷ lệ xấu chia tỷ lệ cho phép (5xx 0.01 — gộp cả khả dụng và error-rate —,
   p95 0.05, p99 0.01). Giữ đóng băng khi tuần chưa có request (`week_spans < 1`, `min-requests-to-recover`) hoặc
   `max_consumed_pct >= 75` (`recovered-below-consumption`). Hàng biến mất → alert recovered → action ghi sự kiện
   `recovered`. Sự kiện chỉ ghi lúc alert đổi trạng thái nên việc ngân sách đặt lại vào thứ Hai không xoá `exhausted_at`.

Test canh gác (`ErrorBudgetRuleDefinitionTests`): `FrozenRule_UnfreezesBelowTheManifestRecoveryThreshold` (75 và 1
lấy từ manifest), `FrozenRule_ConsumptionUsesTheBudgetRatios`, `FrozenRule_StaysRecoveredUntilTheNextExhaustion`,
`FrozenRule_WritesARecoveredEventWhenTheFreezeLifts`.

**Giới hạn đã biết — cửa sổ 14 ngày**: rule không có `WHERE @timestamp` cho sự kiện, chỉ thấy sự kiện "cạn" và
"hồi phục" trong 14 ngày gần nhất. Một service đóng băng liên tục hơn 14 ngày (không có sự kiện "cạn" mới) có thể mất
trạng thái đóng băng khi sự kiện "cạn" trôi ra ngoài cửa sổ (xem `docs/architecture/technical-debt.md`).

### Panel thứ 3

Discover session `slo-error-budget-frozen` ("Cạn ngân sách — ưu tiên độ tin cậy (trạng thái hiện tại)"): mỗi service
có alert của rule `error-budget-frozen` cập nhật từ đầu tuần hiện tại (trạng thái hiện tại, không đổi theo tuần đã
chọn), kèm trạng thái; hiển thị cạnh bảng cảnh báo mốc trên dashboard `Ngân sách lỗi tuần — 7 service`.

```esql
FROM .alerts-stack.alerts-default, traces-generic.otel-default* METADATA _index
| EVAL t0 = DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours
| EVAL is_alert = CASE(_index LIKE "*alerts-stack*", 1, 0)
| WHERE @timestamp >= t0
| WHERE (is_alert == 1 AND kibana.alert.rule.name == "error-budget-frozen") OR (is_alert == 0 AND kind == "Server" AND NOT (COALESCE(attributes.url.path, "") LIKE "/health*"))
| EVAL service = COALESCE(kibana.alert.grouping.service, resource.attributes.service.name)
| EVAL p95_ns = CASE(service == "Bff.Api", 700000000, service == "Gateway.Api", 800000000, 500000000)
| EVAL p99_ns = CASE(service == "Bff.Api", 1000000000, service == "Gateway.Api", 1100000000, 700000000)
| EVAL is_span = 1 - is_alert
| EVAL is_5xx = CASE(is_span == 1 AND attributes.http.response.status_code >= 500, 1, 0)
| EVAL over_p95 = CASE(is_span == 1 AND duration > p95_ns, 1, 0), over_p99 = CASE(is_span == 1 AND duration > p99_ns, 1, 0)
| EVAL active_start = CASE(is_alert == 1 AND kibana.alert.status == "active", kibana.alert.start, NULL), recovered_end = CASE(is_alert == 1 AND kibana.alert.status == "recovered", kibana.alert.end, NULL)
| STATS alerts = SUM(is_alert), spans = SUM(is_span), bad_5xx = SUM(is_5xx), bad_p95 = SUM(over_p95), bad_p99 = SUM(over_p99), frozen_since = MAX(active_start), recovered_at = MAX(recovered_end) BY service
| WHERE alerts > 0
| EVAL consumed_max_pct = CASE(spans > 0, ROUND(GREATEST(TO_DOUBLE(bad_5xx) / spans / 0.01, TO_DOUBLE(bad_p95) / spans / 0.05, TO_DOUBLE(bad_p99) / spans / 0.01) * 100.0, 2), NULL)
| EVAL status = CASE(frozen_since IS NOT NULL AND consumed_max_pct >= 100, "active", frozen_since IS NOT NULL, "recovering", "recovered")
| KEEP service, status, consumed_max_pct, frozen_since, recovered_at
| SORT status, service
```

| Cột | Nghĩa |
|---|---|
| `service` | service có alert đóng băng (active hoặc đã recovered) trong tuần hiện tại |
| `status` | `active` (alert đóng băng active, mức tiêu hao ≥ 100%), `recovering` (alert active, dưới 100% hoặc tuần chưa có request), `recovered` (alert đã recovered — hết đóng băng) |
| `consumed_max_pct` | mức tiêu hao cao nhất trong 4 ngân sách của tuần hiện tại (%), trống khi tuần chưa có request |
| `frozen_since` | `kibana.alert.start` của alert đóng băng đang active |
| `recovered_at` | `kibana.alert.end` của lần gỡ đóng băng gần nhất trong tuần |

Đóng băng (dừng merge tính năng mới) ở `active` **và** `recovering`. Test canh gác: `FrozenPanelStatusTests`.
`recovered` trên panel lấy từ trạng thái alert, không tự tính lại ngưỡng 75% — panel và rule không thể lệch nhau.

### Lưu ý vận hành: đặt lại trạng thái rule `error-budget-100` là ghi lại sự kiện "cạn"

Đặt lại trạng thái alert của rule 100 (Disable rồi Enable; có lúc cả khi cập nhật rule) làm mọi alert đang ở
100% thành "mới", và action ghi thêm một sự kiện `exhausted` với `@timestamp` hiện tại → `exhausted_at` của
các service đó dời về sau, service đã `recovered` mà vẫn ở 100% bị đóng băng lại. Tránh làm vậy khi có service đang cạn,
trừ khi cố ý muốn ghi lại sự kiện. Ngược lại, xoá index sự kiện trong lúc alert 100 vẫn active liên tục thì
không có sự kiện mới nào được ghi: service đã cạn nhưng không bị đóng băng cho tới khi alert đó đổi trạng
thái — khi đó Disable rồi Enable rule 100 để ghi lại sự kiện.

## Kết quả kiểm chứng

Bằng chứng kiểm chứng trên Kibana thật (thời điểm vượt mốc, sự kiện cạn, các giới hạn đã gặp) không ghi
ở tài liệu vận hành này; xem `docs/QA/QA_Debt.md` (mục 027, 029) và tài liệu QA của từng spec.
