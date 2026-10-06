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
- `WHERE @timestamp >= DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours`: đúng một lần — đầu tuần giờ Việt
  Nam. ES|QL làm tròn tuần bắt đầu từ thứ Hai; biểu thức đã kiểm chứng với 4 thời điểm quanh ranh giới
  thứ Hai 00:00 giờ Việt Nam (`specs/029-error-budget-weekly/research.md` V1).

Phần chung (dùng nguyên văn cho bảng mức tiêu hao trên dashboard, thêm `| KEEP service, budget, consumed_pct`):

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp >= DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours
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

### Rule `error-budget-frozen`

Id `a16eed47-01ed-40ce-a8be-c264bde1b771`, cùng loại với rule mốc, chu kỳ 5 phút, **cửa sổ rule 14 ngày**
(lần cạn có thể ở tuần trước), alert theo `service`:

```esql
FROM traces-generic.otel-default*, slo-error-budget-events METADATA _index
| EVAL is_event = CASE(_index LIKE "*slo-error-budget-events*", 1, 0)
| EVAL is_span = 1 - is_event
| EVAL service = COALESCE(resource.attributes.service.name, service)
| EVAL day = DATE_TRUNC(1 day, @timestamp + 7 hours)
| EVAL p95_ns = CASE(service == "Bff.Api", 700000000, service == "Gateway.Api", 800000000, 500000000)
| EVAL p99_ns = CASE(service == "Bff.Api", 1000000000, service == "Gateway.Api", 1100000000, 700000000)
| EVAL is_5xx = CASE(is_span == 1 AND attributes.http.response.status_code >= 500, 1, 0)
| EVAL over_p95 = CASE(is_span == 1 AND duration > p95_ns, 1, 0), over_p99 = CASE(is_span == 1 AND duration > p99_ns, 1, 0)
| EVAL ev_ts = CASE(is_event == 1, @timestamp, NULL)
| STATS spans = SUM(is_span), bad_5xx = SUM(is_5xx), bad_p95 = SUM(over_p95), bad_p99 = SUM(over_p99), day_exhausted_at = MAX(ev_ts) BY service, day
| EVAL day_missed_slo = CASE(spans > 0 AND (TO_DOUBLE(bad_5xx) / spans >= 0.01 OR TO_DOUBLE(bad_p95) / spans > 0.05 OR TO_DOUBLE(bad_p99) / spans > 0.01), 1, 0)
| EVAL bad_day = CASE(day_missed_slo == 1, day, NULL)
| STATS exhausted_at = MAX(day_exhausted_at), last_bad_day = MAX(bad_day) BY service
| WHERE exhausted_at IS NOT NULL
| EVAL exhausted_day = DATE_TRUNC(1 day, exhausted_at + 7 hours)
| EVAL anchor_day = CASE(last_bad_day IS NULL OR exhausted_day > last_bad_day, exhausted_day, last_bad_day)
| EVAL full_days_meeting_slo = DATE_DIFF("day", anchor_day, DATE_TRUNC(1 day, NOW() + 7 hours)) - 1
| WHERE full_days_meeting_slo < 3
| KEEP service
```

Đọc truy vấn theo 3 bước:

1. Gộp traces và sự kiện theo (service, ngày giờ Việt Nam). Ngày "không đạt" = có traffic và một trong
   4 tỷ lệ vượt ngưỡng: 5xx `>= 1%` (đạt cả "5xx dưới 1%" lẫn "khả dụng ≥ 99%"), vượt p95 `> 5%`,
   vượt p99 `> 1%`. Ngày không có span thì không có hàng → tự nhiên tính là đạt.
2. Theo service: `exhausted_at` = lần cạn gần nhất, `last_bad_day` = ngày không đạt gần nhất (kể cả hôm
   nay — hôm nay đã xấu thì chắc chắn chưa hồi phục).
3. `anchor_day` = ngày muộn hơn giữa ngày cạn và ngày xấu gần nhất; số ngày trọn vẹn đạt SLO =
   `hôm nay − anchor_day − 1`. Đóng băng khi `< 3`. Sự kiện chỉ ghi lúc alert 100 chuyển sang active nên
   việc ngân sách đặt lại vào thứ Hai không xoá `exhausted_at`.

**Giới hạn đã biết — cửa sổ 14 ngày**: rule không có `WHERE @timestamp`, chỉ thấy sự kiện "cạn" và ngày xấu
trong 14 ngày gần nhất. Một service cạn rồi tiếp tục không đạt SLO liên tục hơn 14 ngày có thể mất trạng
thái đóng băng khi sự kiện "cạn" trôi ra ngoài cửa sổ (xem `docs/architecture/technical-debt.md` mục 029).

### Panel thứ 3

Discover session `slo-error-budget-frozen` ("Cạn ngân sách — ưu tiên độ tin cậy"): alert active của rule
`error-budget-frozen` cập nhật từ đầu tuần hiện tại (trạng thái hiện tại, không đổi theo tuần đã chọn), cột `service`,
`kibana.alert.start`; hiển thị cạnh bảng cảnh báo mốc trên dashboard `Ngân sách lỗi tuần — 7 service`.

### Lưu ý vận hành: đặt lại trạng thái rule `error-budget-100` là ghi lại sự kiện "cạn"

Đặt lại trạng thái alert của rule 100 (Disable rồi Enable; có lúc cả khi cập nhật rule) làm mọi alert đang ở
100% thành "mới", và action ghi thêm một sự kiện `exhausted` với `@timestamp` hiện tại → `exhausted_at` của
các service đó dời về sau, chuỗi 3 ngày hồi phục tính lại từ lúc đó. Tránh làm vậy khi có service đang cạn,
trừ khi cố ý muốn ghi lại sự kiện. Ngược lại, xoá index sự kiện trong lúc alert 100 vẫn active liên tục thì
không có sự kiện mới nào được ghi: service đã cạn nhưng không bị đóng băng cho tới khi alert đó đổi trạng
thái — khi đó Disable rồi Enable rule 100 để ghi lại sự kiện.

## Kết quả kiểm chứng

Bằng chứng kiểm chứng trên Kibana thật (thời điểm vượt mốc, sự kiện cạn, các giới hạn đã gặp) không ghi
ở tài liệu vận hành này; xem `docs/QA/QA_Debt.md` (mục 027, 029) và tài liệu QA của từng spec.
