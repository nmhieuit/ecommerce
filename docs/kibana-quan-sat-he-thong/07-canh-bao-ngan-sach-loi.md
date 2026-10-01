# 07 — Cảnh báo ngân sách lỗi (error budget)

*(Cần đã làm file 06 — file này thêm cảnh báo tự động lên đúng dashboard SLO hằng ngày của file 06.)*

Đặc tả: [`specs/027-error-budget-alerting/`](../../specs/027-error-budget-alerting/spec.md) (SCRUM-35).
Hợp đồng: [`contracts/error-budget-alert-rules-contract.md`](../../specs/027-error-budget-alerting/contracts/error-budget-alert-rules-contract.md).
Export thật: [`alerts/error-budget-rules.ndjson`](alerts/error-budget-rules.ndjson) (4 rule + connector),
[`dashboards/slo-van-hanh-hang-ngay.ndjson`](dashboards/slo-van-hanh-hang-ngay.ndjson) (dashboard).

Chính sách (con số, hệ quả, điều kiện hồi phục) nằm trong khối `error-budget-policy` của từng
`service-manifest.yaml`. File này chỉ nói cách Kibana đo và cảnh báo theo chính sách đó.

## Điều kiện trước: khoá mã hoá Kibana

Kibana Alerting không cho tạo hay chạy rule nếu thiếu `xpack.encryptedSavedObjects.encryptionKey`.
Khoá lấy từ `KIBANA_ENCRYPTION_KEY` trong `.env` (Vùng 2 của `.env.example`), compose truyền vào qua
`XPACK_ENCRYPTEDSAVEDOBJECTS_ENCRYPTIONKEY`. Kiểm tra:

```powershell
Invoke-RestMethod http://localhost:5601/api/alerting/_health | Select-Object has_permanent_encryption_key
```

## Truy vấn chuẩn: mức tiêu hao 4 ngân sách × 7 service

Mọi rule mốc và bảng "mức tiêu hao" trên dashboard dùng **cùng một truy vấn** dưới đây, chỉ khác dòng
`WHERE consumed_pct >= N` cuối cùng. `tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs`
đọc rule đã export và dựa vào đúng 3 hình dạng sau — sửa truy vấn thì giữ nguyên chúng:

- `p95_ns = CASE(service == "<Tên>.Api", <ns>, ..., <ns mặc định>)` và `p99_ns = CASE(...)`: ngưỡng độ
  trễ theo service, bằng `slos.latency` của manifest × 1 000 000 (`duration` tính bằng nanosecond).
- `allowed = CASE(budget == "availability", 0.001, ...)`: tỷ lệ request xấu cho phép, không có nhánh
  mặc định.
- `WHERE consumed_pct >= N`: đúng một lần, `N` là mốc của rule.

Phần chung (dùng nguyên văn cho bảng mức tiêu hao trên dashboard, thêm `| KEEP service, budget, consumed_pct`):

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp >= DATE_TRUNC(1 month, NOW() + 7 hours) - 7 hours
| EVAL service = resource.attributes.service.name
| EVAL p95_ns = CASE(service == "Bff.Api", 300000000, 150000000)
| EVAL p99_ns = CASE(service == "Bff.Api", 800000000, 500000000)
| EVAL is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0)
| EVAL over_p95 = CASE(duration > p95_ns, 1, 0), over_p99 = CASE(duration > p99_ns, 1, 0)
| STATS total = COUNT(*), bad_5xx = SUM(is_5xx), bad_p95 = SUM(over_p95), bad_p99 = SUM(over_p99) BY service
| EVAL budget = ["availability", "error-rate", "latency-p95", "latency-p99"]
| MV_EXPAND budget
| EVAL bad = CASE(budget == "latency-p95", bad_p95, budget == "latency-p99", bad_p99, bad_5xx)
| EVAL allowed = CASE(budget == "availability", 0.001, budget == "error-rate", 0.001, budget == "latency-p95", 0.05, budget == "latency-p99", 0.01)
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
  một alert — đã gặp thật lúc dựng (research.md "Hệ quả 3").
- `KEEP service, budget`: Kibana ghép mã alert từ giá trị mọi cột kết quả. Giữ cột `consumed_pct` thì
  mã alert đổi mỗi lần chạy, alert "recovered" rồi mọc lại mỗi 5 phút thay vì giữ active liên tục
  (research.md "Hệ quả 2"). Mức tiêu hao (%) vì vậy hiện ở bảng riêng trên dashboard, không trong alert.
- Một service không có span nào từ đầu tháng thì không có hàng nào → không có alert ("không có dữ
  liệu" khác "0%", FR-012).

## Ba rule mốc

| Rule | Id saved object | Loại | Chu kỳ | Cửa sổ rule | Alert theo |
|---|---|---|---|---|---|
| `error-budget-50` | `4169d562-aca7-47af-8da1-63511967b07f` | Elasticsearch query (ES\|QL), `groupBy: row` | 5 phút | 31 ngày | (service, budget) |
| `error-budget-75` | `f724cf89-13ac-4f9e-af09-f89e5e436917` | như trên | 5 phút | 31 ngày | (service, budget) |
| `error-budget-100` | `a3581b2a-3eda-4a2f-8f4d-a9af04c8fac3` | như trên | 5 phút | 31 ngày | (service, budget) |

Tạo bằng Kibana Alerting API (`POST /api/alerting/rule`, `rule_type_id: .es-query`,
`consumer: stackAlerts`, `params.searchType: esqlQuery`, `threshold: [0]`, `thresholdComparator: ">"`,
`excludeHitsFromPreviousRun: false`), tag `slo-error-budget`. Cửa sổ rule 31 ngày là bắt buộc: rule tự lọc
`@timestamp` theo cửa sổ của nó (research.md "Hệ quả 1"); dòng `WHERE` đầu truy vấn cắt lại đúng từ
00:00 ngày 1 giờ Việt Nam.

## Nhóm panel "Ngân sách lỗi tháng này" trên dashboard

Hai panel đặt trên cùng dashboard `SLO vận hành hằng ngày — 7 service`, mỗi panel là một **Discover
session dạng ES|QL** (saved object type `search`) gắn theo tham chiếu, khoảng thời gian riêng
`now-62d → now` (không theo time range 24 giờ của dashboard):

| Panel | Discover session id | Truy vấn | Cột |
|---|---|---|---|
| Mức tiêu hao (%) | `slo-error-budget-consumption` | phần chung ở trên + `EVAL moc = CASE(consumed_pct >= 100, "100% - CẠN", >= 75 "75%", >= 50 "50%", "dưới 50%")`, sắp giảm dần theo `consumed_pct` | `service`, `budget`, `consumed_pct`, `moc` |
| Cảnh báo đang hoạt động | `slo-error-budget-active-alerts` | `FROM .alerts-stack.alerts-default`, lọc tag `slo-error-budget` + `status == "active"`, lấy `kibana.alert.grouping.service/budget` | `service`, `budget`, `moc`, `kibana.alert.start` |

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

Id `a16eed47-01ed-40ce-a8be-c264bde1b771`, cùng loại với rule mốc, chu kỳ 5 phút, **cửa sổ rule 62 ngày**
(lần cạn có thể ở tháng trước), alert theo `service`:

```esql
FROM traces-generic.otel-default*, slo-error-budget-events METADATA _index
| EVAL is_event = CASE(_index LIKE "*slo-error-budget-events*", 1, 0)
| EVAL is_span = 1 - is_event
| EVAL service = COALESCE(resource.attributes.service.name, service)
| EVAL day = DATE_TRUNC(1 day, @timestamp + 7 hours)
| EVAL p95_ns = CASE(service == "Bff.Api", 300000000, 150000000)
| EVAL p99_ns = CASE(service == "Bff.Api", 800000000, 500000000)
| EVAL is_5xx = CASE(is_span == 1 AND attributes.http.response.status_code >= 500, 1, 0)
| EVAL over_p95 = CASE(is_span == 1 AND duration > p95_ns, 1, 0), over_p99 = CASE(is_span == 1 AND duration > p99_ns, 1, 0)
| EVAL ev_ts = CASE(is_event == 1, @timestamp, NULL)
| STATS spans = SUM(is_span), bad_5xx = SUM(is_5xx), bad_p95 = SUM(over_p95), bad_p99 = SUM(over_p99), day_exhausted_at = MAX(ev_ts) BY service, day
| EVAL day_missed_slo = CASE(spans > 0 AND (TO_DOUBLE(bad_5xx) / spans >= 0.001 OR TO_DOUBLE(bad_p95) / spans > 0.05 OR TO_DOUBLE(bad_p99) / spans > 0.01), 1, 0)
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
   4 tỷ lệ vượt ngưỡng: 5xx `>= 0.1%` (đạt cả "5xx dưới 0.1%" lẫn "khả dụng ≥ 99.9%"), vượt p95 `> 5%`,
   vượt p99 `> 1%`. Ngày không có span thì không có hàng → tự nhiên tính là đạt.
2. Theo service: `exhausted_at` = lần cạn gần nhất, `last_bad_day` = ngày không đạt gần nhất (kể cả hôm
   nay — hôm nay đã xấu thì chắc chắn chưa hồi phục).
3. `anchor_day` = ngày muộn hơn giữa ngày cạn và ngày xấu gần nhất; số ngày trọn vẹn đạt SLO =
   `hôm nay − anchor_day − 1`. Đóng băng khi `< 3`. Sự kiện chỉ ghi lúc alert 100 chuyển sang active nên
   việc ngân sách đặt lại đầu tháng không xoá `exhausted_at` (FR-010).

### Panel thứ 3

Discover session `slo-error-budget-frozen` ("Cạn ngân sách — ưu tiên độ tin cậy"): alert active của rule
`error-budget-frozen` cập nhật trong 15 phút gần nhất, cột `service`, `kibana.alert.start`; đặt ngay dưới 2
bảng đầu, trải hết chiều ngang.

### Lưu ý vận hành: sửa rule `error-budget-100` là ghi lại sự kiện "cạn"

Cập nhật rule (kể cả chỉ đổi action) làm Kibana đặt lại trạng thái alert của rule đó: mọi alert đang ở
100% thành "mới" và action ghi thêm một sự kiện `exhausted` với `@timestamp` hiện tại → `exhausted_at` của
các service đó dời về sau, chuỗi 3 ngày hồi phục tính lại từ hôm sửa. Đã gặp thật lúc gắn action
(07:05:07Z, 13 sự kiện cho 6 service). Tránh sửa rule 100 khi có service đang cạn, hoặc chấp nhận hệ quả này.

## Kết quả xác minh trên Kibana 9.4.4 thật

### Diễn tập làm cạn ngân sách 5xx của `Orders.Api` (quickstart.md Kịch bản 1 + 3, 2026-10-01)

Bật `CHAOS_ALLOW_FAULT_INJECTION=true` trong `.env`, tạo lại riêng `orders-api`. Tháng mới bắt đầu nên
`Orders.Api` mới có khoảng 1 200 span, ngân sách 5xx (0.1%) chỉ chứa khoảng 1,2 lỗi. Vì vậy một lỗi đã
vượt cả mốc 50 và 75, lỗi thứ hai vượt 100 — không tách riêng được mốc 50 và 75 ở lưu lượng này (giới
hạn "lưu lượng thấp" của spec).

| Bước (UTC) | Hành động / quan sát | Mức tiêu hao 5xx |
|---|---|---|
| 06:50:40 | `X-Chaos-Fault: 5xx` → `500`; không header → `200`; giá trị `500` → `200` | 1 lỗi / 1 244 span = 80,39% |
| 06:50:40 | Span `GET /health/live` với `status_code = 500` có trong `traces-generic.otel-default*` | |
| 06:55:00 | Alert `error-budget-75` cho `Orders.Api,availability` và `Orders.Api,error-rate` bật (4 phút 20 giây) | |
| 06:55:39 | Alert `error-budget-50` cho hai cặp trên bật (4 phút 59 giây) | |
| 06:55:44 | Gửi lỗi thứ hai | 2 / 1 253 = 159,62% |
| 07:00:00 | Alert `error-budget-100` cho hai cặp trên bật (4 phút 16 giây); alert 75 vẫn active, `@timestamp` cập nhật 07:00 | |
| sau diễn tập | Đặt lại `false`, tạo lại `orders-api`: header `5xx` → `200` | |

Bảng "cảnh báo đang hoạt động" trên dashboard trả đủ 6 alert 5xx của `Orders.Api` (đối chiếu bằng chính
truy vấn của panel), bảng mức tiêu hao 28 hàng, bảng SLO của file 06 vẫn render — 5 bất biến của hợp
đồng 021 không bị ảnh hưởng.

### Giới hạn đã gặp: Kibana mất trạng thái alert khi máy quá tải

Hai lần (06:23 và 06:46), event loop Kibana bị chặn 25–37 giây; task rule thất bại với `409` khi lưu
trạng thái, lượt sau coi mọi alert là mới và để lại bản cũ ở `active` mãi mãi ("mồ côi"). Không phải lỗi
truy vấn — khi máy không quá tải, rule giữ trạng thái đúng (06:34: rule 75 `active: 7, new: 2`). Bảng
alert trên dashboard vì vậy chỉ lấy alert có `@timestamp` trong 15 phút gần nhất (người dùng chốt); cái
giá: nếu Kibana ngừng chạy rule quá 15 phút thì bảng trống.

### Kiểm chứng trạng thái đóng băng (T041, 2026-10-01)

- (a) Sự kiện thật: 13 document `exhausted` cho 6 service (Orders.Api do diễn tập 5xx; Bff.Api, Gateway.Api
  vì 5xx thật lúc quá tải; Identity/Parties/Products vì độ trễ), `service`/`budget` điền đúng từ mustache.
  Lượt `error-budget-frozen` lúc 07:09:01Z bật đúng 6 alert cho 6 service đó; `Baskets.Api` (chưa từng
  cạn) không bị đóng băng.
- (b)/(c) Phép đếm hồi phục, dùng document thử (đã xoá ngay sau đó, index về 13 document thật) và chạy
  chính truy vấn của rule:

| Trường hợp | `exhausted_at` | `last_bad_day` | Ngày trọn vẹn đạt SLO | Đóng băng? |
|---|---|---|---|---|
| `Tmp027.Api` (không có traffic) cạn 26/9 | 2026-09-26 | không có | 4 | Không — ngày không traffic tính là đạt |
| `Baskets.Api` cạn 26/9, có ngày xấu 29/9 | 2026-09-26 | 2026-09-29 | 1 | Có — ngày xấu làm đếm lại |
| `Tmp027.Api` cạn hôm qua 30/9 | 2026-09-30 | không có | 0 | Có |

Chưa kiểm chứng được trong một phiên (để mở ở tài liệu QA): hồi phục thật sau 3 ngày và giữ đóng băng qua
ranh giới tháng trên dữ liệu chạy liên tục.
