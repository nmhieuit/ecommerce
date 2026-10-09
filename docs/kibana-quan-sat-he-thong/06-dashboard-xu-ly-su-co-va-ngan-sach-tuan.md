# 06 — Hai dashboard SLO: Xử lý sự cố và Ngân sách lỗi tuần

*(Cần đã làm file 00-05 — file này mô tả 2 dashboard thật đo đúng các chỉ số SLO đã khai báo trong
`service-manifest.yaml`, khác Panel ở file 05 vốn chỉ nhằm dạy cách dùng Lens.)*

Từ spec 030, dashboard `SLO vận hành hằng ngày — 7 service` (id `e2e06ff5-…`, trộn ba loại cửa sổ thời gian trên cùng một màn
hình) đã **được tách và bỏ**. Hai dashboard thay thế:

| Dashboard | Dùng khi | Thời gian | Id |
|---|---|---|---|
| **`Xử lý sự cố — 7 service`** | Một service đang lỗi hoặc chậm, cần thu hẹp từ "service nào" xuống "lỗi gì, endpoint nào, hạ lưu nào, trace nào" | Mọi panel theo **thanh thời gian**; mặc định 1 giờ gần nhất, tự làm mới 1 phút | `e61fc7f3-17fe-428a-a373-da88af0a4a1e` |
| **`Ngân sách lỗi tuần — 7 service`** | Biết hạn mức SLO của tuần còn lại bao nhiêu | **Cố định tuần lịch giờ Việt Nam** (thứ Hai 00:00 → Chủ nhật 23:59, UTC+7), chọn tuần bằng điều khiển; không đổi theo thanh thời gian | `2a607bf4-2449-48a1-a2e8-1336ec35a7b7` |

Mã nguồn export thật (mỗi dashboard một file): [`dashboards/xu-ly-su-co.ndjson`](dashboards/xu-ly-su-co.ndjson),
[`dashboards/ngan-sach-loi-tuan.ndjson`](dashboards/ngan-sach-loi-tuan.ndjson); cách import/export ở
[`dashboards/README.md`](dashboards/README.md). Hợp đồng bất biến: [`specs/030-incident-and-weekly-dashboards/contracts/dashboards-contract.md`](../../specs/030-incident-and-weekly-dashboards/contracts/dashboards-contract.md).
Mỗi dashboard có một ô Markdown ở đầu trang với link sang dashboard kia (theo id cố định ở bảng trên).

Hợp đồng đo lường liên tục của spec [`021-declare-service-slos`](../../specs/021-declare-service-slos/spec.md) (User Story 3) nay được thực hiện bởi
**hai dashboard này** (Bảng SLO ở `Xử lý sự cố`; ngân sách tuần ở `Ngân sách lỗi tuần`); hợp đồng ở
[`continuous-measurement-contract.md`](../../specs/021-declare-service-slos/contracts/continuous-measurement-contract.md), đã xác thực trên dữ liệu thật tại
[`tasks.md`](../../specs/021-declare-service-slos/tasks.md) T011–T014 (khi còn là một dashboard).

## Dashboard `Xử lý sự cố — 7 service` (mọi panel theo thanh thời gian)

Section **Tình trạng SLO**:
- **Bảng SLO — 7 service** và ô Markdown **Ngưỡng SLO** (Error-rate, p95, p99 thực tế so với ngưỡng đã khai báo; xem mục "Quyết định đã khoá lúc build" bên dưới).
- **Phát hiện nhanh — service vượt SLO trong khoảng thời gian đã chọn**: alert của rule `incident-fast-detection` (spec 028) trong khoảng thời gian đã chọn; cột `status` phân biệt `active` với `recovered`.
- **Health lỗi theo service** (spec 033): alert của rule `health-failure` (từ 50% span health của service trả 5xx trong 5 phút), nằm cạnh bảng Phát hiện nhanh. Health là tín hiệu "service có sẵn sàng không", tách khỏi ngân sách.

> **Spec 033**: các panel đọc traces của dashboard này (và mọi panel ES|QL của dashboard Ngân sách lỗi tuần) **không tính span có đường dẫn bắt đầu bằng `/health`** (health check của Docker). Ngân sách chỉ đo request nghiệp vụ. Chi tiết: [`specs/033-exclude-health-spans/`](../../specs/033-exclude-health-spans/spec.md).
>
> **Spec 034**: các panel ngân sách/SLO (Bảng SLO, 5xx/p95/traffic theo phút, phân bố status code, top endpoint chậm nhất và mọi panel ES|QL của dashboard Ngân sách lỗi tuần) **chỉ đếm span `kind = Server`** (request mà chính service nhận), không đếm span Client/Producer. Panel `Lỗi gọi hạ lưu` vẫn đọc span Client. Chi tiết: [`specs/034-error-budget-server-spans/`](../../specs/034-error-budget-server-spans/spec.md).

Section **Đào sâu lỗi**:
- **5xx theo phút theo service**, **Latency p95 theo phút theo service**, **Traffic + 401/403 theo phút theo service** (thay panel "Tổng 401 + 403" cũ).
- **Xu hướng dotnet.exceptions theo service**, **Phân bố status code theo service**, **Top endpoint chậm nhất**.
- **Lỗi gọi hạ lưu — cặp service gọi → đích**: span Client tổng hợp theo cặp, "xấu" = lỗi (`status.code = Error` hoặc 5xx) hoặc chậm (vượt ngưỡng p95 của service đích: 150ms, `Bff.Api` 300ms), 20 dòng, không có link trace (bảng tổng hợp). Đích suy ra từ `attributes.server.address` (`<tên>-api` → `<Tên>.Api`).
- **Log lỗi gần nhất (Error trở lên)**: log mức Error/Fatal (`severity_number >= 17`), 50 dòng, cột thời gian, service, nội dung, `trace_id`, `attributes.CorrelationId`. Bấm `trace_id` mở Discover lọc đúng trace đó (Kibana APM **không** đọc được dữ liệu trace OTel thô của dự án nên không dùng link APM).

Cách tạo dữ liệu để các panel có số: folder Postman `30a` (5xx cần cờ `CHAOS_ALLOW_FAULT_INJECTION`, độ trễ cần `CHAOS_ALLOW_LATENCY_INJECTION`).

## Dashboard `Ngân sách lỗi tuần — 7 service` (cố định tuần lịch giờ Việt Nam)

Điều khiển **Tuần**: `Tuần này` (mặc định) / `Tuần trước` / `2 tuần trước` / `3 tuần trước` (biến ES|QL `?tuan_chon`). Mỗi panel có khoảng
thời gian riêng 30 ngày nên **không** đổi theo thanh thời gian (Kibana tự cắt mọi truy vấn ES|QL theo thanh thời gian nếu panel không đặt khoảng riêng).

Section **Ngân sách tuần**:
- **Mức tiêu hao ngân sách (%) — tuần đã chọn**: 7 service × 4 ngân sách (khả dụng, 5xx, p95, p99); công thức và tỷ lệ cho phép 1% / 1% / 5% / 1% trùng rule mốc 027 (xem [`07-canh-bao-ngan-sach-loi.md`](07-canh-bao-ngan-sach-loi.md)).
- **Hạn mức còn lại — tuần đã chọn**: `remaining_pct = 100 − consumed_pct` và `remaining_requests = FLOOR(tỷ lệ cho phép × tổng request tuần tới giờ − số request xấu đã có)` (âm = đã vượt).
- **Cảnh báo mốc đang hoạt động (trạng thái hiện tại)** và **Cạn ngân sách — ưu tiên độ tin cậy (trạng thái hiện tại)**: alert của tuần lịch hiện tại, **không** đổi theo tuần đã chọn.

Section **Xu hướng trong tuần**: **Error-rate theo ngày trong tuần**, **Latency p95 theo ngày trong tuần**, **Tiêu hao lũy kế theo ngày từ thứ Hai** (mức cao nhất trong 4 ngân sách, mỗi service một đường, chỉ ngày đã tới).

Truy vấn đối chiếu số dashboard với Elasticsearch: folder Postman `30b`. Tuần chưa có dữ liệu (ví dụ ngay sau khi dọn Elastic) hiển thị "No results found", không phải số sai.

## Quyết định đã khoá lúc build: cách hiển thị cột "Ngưỡng"

Đã chọn **phương án (c)** trong 3 phương án đề xuất ở thiết kế gốc: "chấp nhận ghi ngưỡng trực tiếp
trong tên cột (vd 'Error-rate — ngưỡng 1%') nếu ngưỡng giống nhau cho phần lớn service, chỉ ghi chú
riêng ngoại lệ của `Bff.Api` bằng chữ bên cạnh bảng." Lý do: Lens Table không có cách hiển nhiên để
chèn giá trị tĩnh khác nhau theo từng dòng trong cùng 1 cột tính từ aggregation — ghi ngay trong tên
cột là cách rẻ nhất, không cần runtime field/`esql`.

Tên cột (Name) thật đã đặt ở Task 2 Bước 2 mục 4-6:

| Cột | Name (label) thật |
|---|---|
| Error-rate | `Error-rate — Thực tế (ngưỡng < 1%, cả 7 service)` (spec 029: SLO 5xx dưới 1%) |
| Latency p95 | `Latency p95 (ms) — Thực tế (ngưỡng 500ms; riêng Bff.Api 700ms, Gateway.Api 800ms)` |
| Latency p99 | `Latency p99 (ms) — Thực tế (ngưỡng 700ms; riêng Bff.Api 1000ms, Gateway.Api 1100ms)` |

Khác nhỏ so với mô tả gốc: ngoại lệ `Bff.Api` được ghi thẳng trong cùng tên cột (trong ngoặc), không
tách thành 1 dòng chữ riêng "bên cạnh bảng" — gộp lại cho gọn, không ảnh hưởng nội dung.

## Đã xác nhận thật lúc build (khép lại các mục "chưa chốt" của thiết kế gốc)

- **Cú pháp Lens Formula cho Error-rate**: `count(kql='attributes.http.response.status_code >= 500') / count()`
  — đối chiếu với service mẫu `Orders.Api` (Task 2 Bước 4): truy vấn REST API thô trên Elasticsearch
  (`now-24h`) cho `total.count = 7687`, `err.count = 1` → Error-rate tự tính = `1 / 7687 = 0.01%`.
  Giá trị Kibana hiển thị cùng lúc: `0.01%`. **Khớp tuyệt đối, chênh lệch 0 điểm %.**
  Latency cũng đã đối chiếu (Task 2 Bước 5): p95 tự tính `27.14ms` → làm tròn `27ms`, Kibana hiển thị
  `27ms` (khớp tuyệt đối); p99 tự tính `389.49ms` → làm tròn `389ms`, Kibana hiển thị `390ms` (lệch
  1ms trên nền ~390ms, ~0.25%, do time window `now-24h` trượt vài giây giữa 2 lần query trên hệ thống
  demo sinh traffic liên tục — không phải lỗi formula). Chart A (Task 3 Bước 6) cũng đối chiếu khớp
  theo ngày cho `Orders.Api`: 2026-09-06 tooltip `0.55%` vs tính thô `0.5519%`; 2026-09-08 tooltip
  `0.01%` vs tính thô `0.0121%` — cả 2 điểm đều khớp sau làm tròn 2 chữ số thập phân.
- **Lens Formula xử lý mẫu số 0**: đã kiểm tra bằng cách đặt time range Absolute **Sep 7–8, 2020**
  (chắc chắn không có dữ liệu). Kết quả: Lens hiển thị **"No results found"** cho toàn bảng — **không
  phải** `NaN`, **không phải** `0%` gây hiểu lầm. Cơ chế: Rows dùng "Top values" (terms aggregation)
  trên `resource.attributes.service.name`, nên 1 service chỉ xuất hiện thành 1 row khi có ≥1 document
  khớp trong range — mẫu số `count()` không bao giờ thực sự bằng 0 đối với bất kỳ row nào đang hiển
  thị; service không có traffic thì biến mất khỏi bảng thay vì hiện dòng lỗi. Kết luận: **giữ nguyên
  formula gốc**, không cần bọc `ifelse(count() == 0, null, ...)`.
- **Tên/vị trí "Customize time range" trên Kibana 9.4.4**: tên brief giả định (`Customize time range`)
  **không đúng**. Vị trí/tên thật: hover vào panel → toolbar góc trên panel hiện ra (bút chì = Edit,
  **bánh răng = Settings**, icon tròn = Inspect, `⋮` = More) → click **icon bánh răng "Settings"** →
  flyout "Settings" mở ra, bên trong có toggle tên **"Apply custom time range"**. Bật toggle này hiện
  thêm ô "Time range" với time-picker con (Quick select / Absolute / Relative). Flyout "Settings" này
  cũng là nơi đặt "Show title", "Title", "Description", "Show panel border" — đặt tên panel và đặt time
  range riêng làm chung trong 1 bước.
- **Cách Collapsible section lưu trong saved object**: số panel cấp cao nhất thật sau khi build Tầng 2
  là **8** (4 panel Tầng 1+1.5 cũ + 4 panel Tầng 2 mới), **không phải `5`** như Interfaces dự kiến ban
  đầu — vì `panelsJSON` chỉ chứa các panel thật, Collapsible section **không** được biểu diễn như 1
  phần tử trong `panelsJSON`. Kibana lưu section ở 1 thuộc tính cấp cao nhất riêng của dashboard, tên
  `sections` (mảng, song song với `panelsJSON`, không lồng bên trong):
  ```json
  [
    {
      "collapsed": true,
      "title": "Đào sâu khi có báo động",
      "gridData": { "y": 30, "i": "a84380a8-997c-4271-83d4-e63c72b1d4b3" }
    }
  ]
  ```
  Không có `panelIds` hay `sectionId` tường minh nối panel với section — quan hệ panel↔section hoàn
  toàn ngầm định qua toạ độ `gridData.y` (panel có `y` từ giá trị `y` của section trở lên, tới trước
  section kế tiếp hoặc hết dashboard, được xem là "trong" section đó).

  **Phát hiện quan trọng nhất — giới hạn thật của Kibana 9.4.4, không giảm nhẹ**: trạng thái
  `collapsed: true` được lưu đúng và phản ánh đúng ở header trong chế độ View (class CSS
  `kbnGridSectionHeader--collapsed`, `aria-expanded="false"`) — **nhưng các panel bên trong section
  KHÔNG bị ẩn khỏi màn hình**. Kiểm tra DOM trực tiếp (`display: block`, `visibility: visible`, chiều
  cao đầy đủ `412px` cho cả 4 panel) sau khi Save, Exit edit, và **reload thật bằng
  `location.reload()`** (loại trừ khả năng SPA giữ state cũ) cho kết quả nhất quán qua 3 lần thử (2
  lần collapse/expand trong edit mode + 1 lần reload thật). Đây là hành vi thật của bản Kibana đang
  chạy, không phải thao tác sai — **đừng phụ thuộc vào Collapsible section để "giấu bớt" nội dung khi
  trình bày dashboard cho người khác xem**; section chỉ hữu ích như 1 nhãn phân nhóm trực quan (đổi
  chevron, đổi ARIA), chưa dùng được như cơ chế ẩn/hiện nội dung thật sự trên bản Kibana này.

## Cách tự đối chiếu lại (đúng thói quen file 01-04, không tin số Lens hiển thị mà không tự kiểm tra)

Đối chiếu Error-rate (Task 2 Bước 4) — service mẫu `Orders.Api`, khoảng `now-24h`:

```powershell
# tổng số span
Invoke-RestMethod -Uri "$EsBase/traces-generic.otel-default*/_count" -Method Post -ContentType "application/json" -Body '{
  "query": { "bool": { "must": [
    { "term": { "resource.attributes.service.name": "Orders.Api" } },
    { "range": { "@timestamp": { "gte": "now-24h" } } }
  ] } }
}'
# → count = 7687

# số span lỗi 5xx
Invoke-RestMethod -Uri "$EsBase/traces-generic.otel-default*/_count" -Method Post -ContentType "application/json" -Body '{
  "query": { "bool": { "must": [
    { "term": { "resource.attributes.service.name": "Orders.Api" } },
    { "range": { "@timestamp": { "gte": "now-24h" } } },
    { "range": { "attributes.http.response.status_code": { "gte": 500 } } }
  ] } }
}'
# → count = 1
```

Kết quả thật: `1 / 7687 = 0.01%`, Kibana (cùng khoảng, refresh ngay sau khi chạy 2 lệnh trên) hiển thị
`Orders.Api → Error-rate = 0.01%` — **khớp, chênh lệch 0 điểm %**.

Đối chiếu Latency p95/p99 (Task 2 Bước 5) — cùng service, cùng khoảng, dùng `percentiles` aggregation
thô qua `_search` trên field `duration`:

```powershell
$latBody = @{
  size = 0
  query = @{ bool = @{ must = @(
    @{ term = @{ "resource.attributes.service.name" = $svc } }
    @{ range = @{ "@timestamp" = @{ gte = "now-24h" } } }
  ) } }
  aggs = @{ p = @{ percentiles = @{ field = "duration"; percents = @(95, 99) } } }
} | ConvertTo-Json -Depth 10
$lat = Invoke-RestMethod -Uri "$EsBase/traces-generic.otel-default*/_search" -Method Post -Body $latBody -ContentType "application/json"
$p95ms = $lat.aggregations.p.values.'95.0' / 1000000
$p99ms = $lat.aggregations.p.values.'99.0' / 1000000
Write-Output "p95 = $p95ms ms, p99 = $p99ms ms"
```

Kết quả thật chạy lệnh trên (`$svc = "Orders.Api"`):

```
p95 = 27.1438818702788 ms  → làm tròn 27 ms
p99 = 389.487010515074 ms  → làm tròn 389 ms
```

Kibana hiển thị (refresh ngay sau khi chạy aggregation trên): `Latency p95 (ms) = 27`,
`Latency p99 (ms) = 390`. Kết quả: p95 **khớp tuyệt đối** (27 = 27); p99 **khớp gần đúng**, lệch 1ms
(389 vs 390, ~0.25% trên nền ~390ms) — do time window `now-24h` trượt vài giây giữa 2 lệnh
PowerShell và lúc Kibana tự refresh trên hệ thống demo sinh traffic liên tục, cộng sai số xấp xỉ vốn
có của thuật toán percentile (t-digest); không phải lỗi formula.

## Giới hạn đã biết

- Availability đo xấp xỉ (`100% − Error-rate`), không phải uptime thật — service sập hẳn (0 traces)
  sẽ biến mất khỏi bảng thay vì hiện cảnh báo (xem `SCRUM-29`/`SCRUM-30`).
- Cảnh báo ngân sách lỗi do 4 rule của spec 027 (chu kỳ tuần theo spec 029) đảm nhiệm, không phải dashboard — xem
  [`07-canh-bao-ngan-sach-loi.md`](07-canh-bao-ngan-sach-loi.md); phát hiện nhanh do rule `incident-fast-detection` — xem [`08-phat-hien-nhanh-va-xu-ly-su-co.md`](08-phat-hien-nhanh-va-xu-ly-su-co.md).
- Dashboard `Ngân sách lỗi tuần` chỉ xem lại được tối đa 3 tuần trước (khoảng thời gian riêng 30 ngày của mỗi panel).
- Hai panel cảnh báo/cạn ngân sách luôn là trạng thái hiện tại (rule chỉ giữ alert của tuần hiện tại), không có lịch sử từng tuần.
- Collapsible section lưu đúng trạng thái collapsed/expanded nhưng **không thực sự ẩn nội dung panel trong chế độ View** trên Kibana 9.4.4
  (xem mục "Cách Collapsible section lưu trong saved object" ở trên) — hai dashboard dùng section luôn ở trạng thái mở, chỉ như nhãn phân nhóm.
- Dashboard dựng bằng **Dashboards REST API của Kibana 9.4.4** (`PUT /api/dashboards/{id}`, cho phép đặt id), rồi export bằng Saved Objects Export API.
