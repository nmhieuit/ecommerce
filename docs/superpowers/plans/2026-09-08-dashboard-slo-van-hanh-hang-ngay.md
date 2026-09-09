# Kế hoạch triển khai: Dashboard SLO vận hành hằng ngày

> **Dành cho người/agent thực thi:** BẮT BUỘC DÙNG KÈM: superpowers:subagent-driven-development
> (khuyến nghị) hoặc superpowers:executing-plans để thực thi kế hoạch này từng task một. Các bước
> dùng cú pháp checkbox (`- [ ]`) để theo dõi tiến độ.

**Mục tiêu:** Dựng và lưu thật 1 dashboard Kibana tên `SLO vận hành hằng ngày — 7 service`, đo đúng
3 chỉ số SLO (Error-rate, Latency p95, Latency p99) đã khai báo trong `service-manifest.yaml` của 7
service, theo đúng cấu trúc 3 tầng đã chốt ở
[`docs/superpowers/specs/2026-09-08-dashboard-slo-van-hanh-design.md`](../specs/2026-09-08-dashboard-slo-van-hanh-design.md).

**Kiến trúc:** Dashboard dựng thủ công trên Kibana UI (Lens), không có code ứng dụng nào thay đổi.
Mỗi task build xong 1 phần dashboard, tự đối chiếu số Lens hiển thị với truy vấn Elasticsearch REST
API thô (đóng vai "bài test" cho hạ tầng không-phải-code này), rồi export toàn bộ saved objects liên
quan ra 1 file NDJSON đưa vào git — đây là "mã nguồn" thật sự được version-control của dashboard, vì
bản thân trạng thái Kibana không nằm trong git. Task cuối viết tài liệu hands-on file `06` nối tiếp
đúng văn phong đã có ở `docs/kibana-quan-sat-he-thong/00`-`05`.

**Công nghệ chính:** Kibana Lens (Kibana 9.4.4), Elasticsearch REST API, PowerShell
`Invoke-RestMethod`/`Invoke-WebRequest` (môi trường Windows), Kibana Saved Objects Export/Import API.

## Ràng buộc chung (áp dụng cho mọi task bên dưới)

- **Ngưỡng SLO thật** (từ `service-manifest.yaml`, constitution Principle VIII):
  - Availability: `99.9%`/tháng (áp dụng cả 7 service)
  - Error-rate: `≤ 0.1%` mã 5xx (áp dụng cả 7 service)
  - Latency: `p95 150ms` / `p99 500ms` cho 6 service (`Baskets.Api`, `Gateway.Api`, `Identity.Api`,
    `Orders.Api`, `Parties.Api`, `Products.Api`)
  - Latency riêng `Bff.Api`: `p95 300ms` / `p99 800ms`
- **Availability xấp xỉ = `100% − Error-rate`** — không đo uptime thật (chưa có synthetic check).
- **Ngoài phạm vi** (không làm trong kế hoạch này): không sửa/xoá
  `docs/kibana-quan-sat-he-thong/05-dashboard-va-visualize.md`; không dựng alert rule tự động
  (`SCRUM-35`); không đo Availability bằng synthetic uptime check thật (`SCRUM-29`/`SCRUM-30`).
- **Cửa sổ thời gian**: Tầng 1 và Tầng 2 dùng time picker cấp dashboard = `Last 24 hours`. Tầng 1.5
  dùng `Last 7 days` đặt riêng theo panel (Customize time range).
- **Endpoint hạ tầng** (docker-compose.local.yml): Kibana `http://localhost:5601`, Elasticsearch
  `http://localhost:9200`. Cả 2 chạy không bật xác thực (`xpack.security.enabled: "false"`) — mọi
  lệnh gọi API dưới đây không cần token, nhưng lệnh POST/PUT/DELETE tới Kibana API vẫn bắt buộc
  header `kbn-xsrf: true` (yêu cầu chống XSRF của Kibana, độc lập với xác thực).
- **Data view dùng**: `Traces` (đã tồn tại sẵn từ `docs/kibana-quan-sat-he-thong/01-lam-quen-kibana-discover.md`,
  index pattern `traces-generic.otel-default*`).
- **Tên dashboard cố định xuyên suốt kế hoạch**: `SLO vận hành hằng ngày — 7 service`.
- **Quyết định khoá lại cách hiển thị cột "Ngưỡng"** (thiết kế gốc để ngỏ 3 phương án — kế hoạch này
  chọn dứt khoát phương án (c) trong spec: ghi thẳng số ngưỡng vào **tên cột**, vì ngưỡng Error-rate
  giống nhau ở cả 7 service và ngưỡng Latency chỉ lệch đúng 1 ngoại lệ (`Bff.Api`) — không cần
  ESQL/runtime field phức tạp, không cần bảng Markdown tách riêng). Xem chi tiết ở Task 2.
- Mọi lệnh PowerShell dưới đây giả định chạy từ thư mục gốc repo
  (`C:\Users\ngantran\source\repos\ecomerce`).

---

## Cấu trúc file

| File | Vai trò |
|---|---|
| `docs/kibana-quan-sat-he-thong/dashboards/README.md` | Hướng dẫn import file NDJSON vào 1 Kibana khác (script + API) |
| `docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson` | Export saved objects thật của dashboard — cập nhật đè sau mỗi task, đây là "mã nguồn" version-control được của dashboard |
| `docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md` | Tài liệu hands-on nối tiếp file 00-05, viết SAU khi đã build+verify xong, ghi lại đúng những gì đã làm thật (đúng văn phong "đã tự kiểm chứng" của các file trước) |
| `docs/README.md` | Sửa 1 dòng: đếm file từ `00→05` thành `00→06` |
| `docs/superpowers/specs/2026-09-08-dashboard-slo-van-hanh-design.md` | Đánh dấu hoàn tất checklist "Việc cần làm ở bước triển khai" ở cuối file |

---

### Task 1: Chuẩn bị môi trường, xác nhận điểm khởi đầu, dựng khung export

**Files:**
- Create: `docs/kibana-quan-sat-he-thong/dashboards/README.md`
- Test: không có file test riêng — "test" của task này là script PowerShell chạy trực tiếp, kết quả
  đối chiếu bằng mắt với kỳ vọng ghi trong bước

**Interfaces:**
- Produces: biến quy ước dùng lại ở mọi task sau —
  `$KibanaBase = "http://localhost:5601"`, `$EsBase = "http://localhost:9200"`,
  `$KbnHeaders = @{ "kbn-xsrf" = "true" }` — và đường dẫn cố định
  `docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson` (chưa tạo ở task này, tạo
  lần đầu ở Task 2).

- [ ] **Bước 1: Xác nhận stack đang chạy (bài kiểm tra "thất bại" — baseline chưa có dashboard nào)**

Chạy trong PowerShell:

```powershell
$KibanaBase = "http://localhost:5601"
$EsBase = "http://localhost:9200"
$KbnHeaders = @{ "kbn-xsrf" = "true" }

Invoke-RestMethod -Uri "$EsBase/_cluster/health"
Invoke-RestMethod -Uri "$KibanaBase/api/status" | Select-Object -ExpandProperty status | Select-Object -ExpandProperty overall
Invoke-RestMethod -Uri "$KibanaBase/api/saved_objects/_find?type=dashboard"
```

- [ ] **Bước 2: Xác nhận kết quả đúng kỳ vọng (bài kiểm tra "baseline" phải đúng như vậy để task sau có ý nghĩa)**

Kỳ vọng:
- `_cluster/health` trả `"status": "green"` hoặc `"yellow"` (không phải `"red"`).
- `api/status` overall trả `"level": "available"`.
- `saved_objects/_find?type=dashboard` trả `"total": 0` — đúng như thiết kế đã ghi nhận ("Kibana của
  bạn hiện đang trống"). Nếu `total` khác 0, dừng lại — nghĩa là đã có ai/cái gì tạo dashboard trước,
  cần xác nhận thủ công đó có phải dashboard này không trước khi tiếp tục (không tự ý ghi đè).

- [ ] **Bước 3: Viết `docs/kibana-quan-sat-he-thong/dashboards/README.md`**

```markdown
# Export saved objects của dashboard SLO vận hành hằng ngày

`slo-van-hanh-hang-ngay.ndjson` là bản export thật (Kibana Saved Objects Export API) của dashboard
`SLO vận hành hằng ngày — 7 service` và mọi Lens visualization nó dùng. Đây là "mã nguồn" duy nhất
của dashboard được version-control — bản thân trạng thái Kibana không nằm trong git, nên file này là
nguồn để dựng lại dashboard trên 1 Kibana khác (máy mới, môi trường CI, đồng nghiệp khác) mà không
phải click lại từ đầu theo `docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md`.

## Cách import lại

```powershell
$KibanaBase = "http://localhost:5601"
$form = @{
  file = Get-Item "docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson"
}
Invoke-RestMethod -Uri "$KibanaBase/api/saved_objects/_import?overwrite=true" `
  -Method Post -Headers @{ "kbn-xsrf" = "true" } -Form $form
```

Hoặc qua UI: menu ☰ → **Stack Management** → **Saved Objects** → **Import** → chọn file này → tick
**Automatically overwrite conflicts** nếu đang cập nhật bản cũ.

## Cách export lại (sau khi sửa dashboard trên UI)

```powershell
$KibanaBase = "http://localhost:5601"
$body = @{ type = @("dashboard","lens","index-pattern") } | ConvertTo-Json
Invoke-WebRequest -Uri "$KibanaBase/api/saved_objects/_export" -Method Post `
  -Headers @{ "kbn-xsrf" = "true" } -ContentType "application/json" -Body $body `
  -OutFile "docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson"
```
```

- [ ] **Bước 4: Commit**

```bash
git add docs/kibana-quan-sat-he-thong/dashboards/README.md
git commit -m "docs: chuẩn bị khung export cho dashboard SLO vận hành hằng ngày"
```

---

### Task 2: Tầng 1 — Bảng SLO (7 service × 3 chỉ số) + ghi chú Availability

**Files:**
- Create (qua Kibana UI, không phải file code): dashboard `SLO vận hành hằng ngày — 7 service` +
  1 Lens visualization kiểu Table + 1 panel Markdown
- Create: `docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson` (lần export đầu
  tiên — ghi đè ở các task sau)

**Interfaces:**
- Consumes: `$KibanaBase`, `$EsBase`, `$KbnHeaders` (Task 1), data view `Traces`.
- Produces: dashboard đã lưu, tổng số panel cấp cao nhất = `2` (1 Table + 1 Markdown) — số này Task 3
  sẽ dùng làm điểm xuất phát.

- [ ] **Bước 1: Bài kiểm tra "thất bại" — xác nhận dashboard tên này chưa tồn tại**

```powershell
Invoke-RestMethod -Uri "$KibanaBase/api/saved_objects/_find?type=dashboard&search_fields=title&search=SLO*"
```

Kỳ vọng: `"total": 0`.

- [ ] **Bước 2: Dựng bảng SLO trên Kibana UI**

1. Menu ☰ → **Dashboards** → **Create a dashboard**.
2. **Create visualization** → Lens mở ra. **Data view** → **Traces**. Đổi chart type → **Table**.
3. **Rows**: bấm ô nhóm hàng → **Top values** → field `resource.attributes.service.name` → Number of
   values = `7`.
4. **Cột 1 — Error-rate**: bấm **Add or drag-and-drop a field** → chọn **Formula** → nhập:

   ```
   count(kql='attributes.http.response.status_code >= 500') / count()
   ```

   Value format: **Percent**, 2 chữ số thập phân. Đổi tên cột (Label) thành:
   `Error-rate — Thực tế (ngưỡng ≤ 0.1%, cả 7 service)`.

5. **Cột 2 — Latency p95**: thêm cột **Formula** mới:

   ```
   percentile(duration, percentile=95) / 1000000
   ```

   (chia `1000000` vì `duration` lưu ở đơn vị nanosecond — đã xác nhận ở
   `docs/kibana-quan-sat-he-thong/02-elasticsearch-rest-api.md`). Value format: **Number**, 0 chữ số
   thập phân. Đổi tên cột thành:
   `Latency p95 (ms) — Thực tế (ngưỡng 150ms; riêng Bff.Api 300ms)`.

6. **Cột 3 — Latency p99**: lặp lại bước 5 với `percentile(duration, percentile=99) / 1000000`. Đổi
   tên cột thành: `Latency p99 (ms) — Thực tế (ngưỡng 500ms; riêng Bff.Api 800ms)`.

   **Vì sao ghi ngưỡng thẳng vào tên cột thay vì tách cột riêng**: đã chốt ở phần Ràng buộc chung —
   phương án (c) trong thiết kế gốc, vì ngưỡng Error-rate giống nhau ở cả 7 service, ngưỡng Latency
   chỉ có đúng 1 ngoại lệ (`Bff.Api`) đủ ngắn để ghi gọn trong tên cột, không cần ESQL/runtime field.

7. **Kiểm tra hành vi chia cho 0** (rủi ro đã ghi trong thiết kế): thu hẹp time picker xuống 1
   khoảng chắc chắn không có traffic (vd `Last 1 minute` lúc không có request nào chạy) và quan sát
   cột Error-rate.
   - Nếu Lens hiển thị `-` hoặc ô trống: đạt yêu cầu, không cần sửa gì thêm — giữ nguyên formula.
   - Nếu hiển thị `NaN` hoặc gây hiểu lầm thành `0%`: sửa formula Error-rate thành
     `ifelse(count() == 0, null, count(kql='attributes.http.response.status_code >= 500') / count())`
     rồi lặp lại kiểm tra này tới khi ra `-`/trống.
   - Đặt lại time picker về `Last 24 hours` sau khi kiểm tra xong.
8. **Save and return**.
9. **Add panel** → **Text** (Markdown), nội dung:

   ```markdown
   **Availability xấp xỉ hôm nay**: `100% − Error-rate` ở bảng trên (không đo uptime thật — chưa có
   synthetic check, xem `SCRUM-29`/`SCRUM-30`).

   **Ngoại lệ ngưỡng Latency**: `Bff.Api` có ngân sách nới hơn 6 service còn lại vì mỗi lần đọc của
   BFF gọi xuống ít nhất 1 service khác — ngưỡng riêng: p95 `300ms`, p99 `800ms` (so với `150ms`/
   `500ms` của 6 service kia).

   **Giới hạn đã biết**: bảng này chỉ tính trên request THẬT SỰ đã tới nơi — 1 service sập hẳn (không
   phát traces) sẽ biến mất khỏi bảng thay vì hiện cảnh báo, không phải lỗi cần sửa ở dashboard này.
   ```

10. Đặt time picker cấp dashboard = **Last 24 hours**.
11. **Save**, đặt tên `SLO vận hành hằng ngày — 7 service`.

- [ ] **Bước 3: Bài kiểm tra "đã có dashboard" (test pass ở mức tồn tại)**

```powershell
Invoke-RestMethod -Uri "$KibanaBase/api/saved_objects/_find?type=dashboard&search_fields=title&search=SLO*"
```

Kỳ vọng: `"total": 1`, `saved_objects[0].attributes.title` = `SLO vận hành hằng ngày — 7 service`.
Lưu lại id: `$dashId = (Invoke-RestMethod -Uri "$KibanaBase/api/saved_objects/_find?type=dashboard&search_fields=title&search=SLO*").saved_objects[0].id`

- [ ] **Bước 4: Bài kiểm tra đúng-số — đối chiếu Error-rate với truy vấn thô (đúng phương pháp ở mục "Cách xác nhận đúng" của thiết kế)**

Chọn 1 service mẫu, ví dụ `Orders.Api`:

```powershell
$svc = "Orders.Api"
$totalBody = @{ query = @{ bool = @{ must = @(
  @{ term = @{ "resource.attributes.service.name" = $svc } }
  @{ range = @{ "@timestamp" = @{ gte = "now-24h" } } }
) } } } | ConvertTo-Json -Depth 10
$total = Invoke-RestMethod -Uri "$EsBase/traces-generic.otel-default*/_count" -Method Post -Body $totalBody -ContentType "application/json"

$errBody = @{ query = @{ bool = @{ must = @(
  @{ term = @{ "resource.attributes.service.name" = $svc } }
  @{ range = @{ "@timestamp" = @{ gte = "now-24h" } } }
  @{ range = @{ "attributes.http.response.status_code" = @{ gte = 500 } } }
) } } } | ConvertTo-Json -Depth 10
$err = Invoke-RestMethod -Uri "$EsBase/traces-generic.otel-default*/_count" -Method Post -Body $errBody -ContentType "application/json"

Write-Output ("Error-rate tự tính: {0:P2}" -f ($err.count / $total.count))
```

Kỳ vọng: con số `Error-rate tự tính` khớp (chênh lệch làm tròn không quá 0.01 điểm %) với ô
`Orders.Api` ở cột Error-rate trên bảng Kibana (cùng khoảng `Last 24 hours`). Nếu `$total.count` = 0,
service đó không có traffic trong 24h — bỏ qua, chọn service khác đang có traffic để đối chiếu.

- [ ] **Bước 5: Đối chiếu Latency p95/p99 bằng aggregation thô**

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

Kỳ vọng: `$p95ms`/`$p99ms` khớp (chênh lệch nhỏ, do làm tròn) với 2 cột Latency của `Orders.Api` trên
bảng Kibana.

- [ ] **Bước 6: Export saved objects lần đầu**

```powershell
$body = @{ type = @("dashboard","lens","index-pattern") } | ConvertTo-Json
Invoke-WebRequest -Uri "$KibanaBase/api/saved_objects/_export" -Method Post `
  -Headers $KbnHeaders -ContentType "application/json" -Body $body `
  -OutFile "docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson"
```

- [ ] **Bước 7: Commit**

```bash
git add docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson
git commit -m "feat(kibana): dung Tang 1 bang SLO cho dashboard SLO van hanh hang ngay"
```

---

### Task 3: Tầng 1.5 — 2 trend chart (Error-rate & Latency p95 theo ngày, 7 ngày)

**Files:**
- Modify (qua Kibana UI): dashboard `SLO vận hành hằng ngày — 7 service` (thêm 2 panel)
- Modify: `docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson` (ghi đè)

**Interfaces:**
- Consumes: dashboard id `$dashId` từ Task 2, biến `$KibanaBase`/`$EsBase`/`$KbnHeaders` (Task 1).
- Produces: tổng số panel cấp cao nhất = `4` (2 từ Task 2 + 2 chart mới) — Task 4 dùng số này làm
  điểm xuất phát.

- [ ] **Bước 1: Bài kiểm tra "thất bại" — đếm panel hiện có, phải đúng 2 (từ Task 2)**

```powershell
$dashId = (Invoke-RestMethod -Uri "$KibanaBase/api/saved_objects/_find?type=dashboard&search_fields=title&search=SLO*").saved_objects[0].id
$dashObj = Invoke-RestMethod -Uri "$KibanaBase/api/saved_objects/dashboard/$dashId"
$panels = $dashObj.attributes.panelsJSON | ConvertFrom-Json
$panels.Count
```

Kỳ vọng: `2`.

- [ ] **Bước 2: Dựng Chart A — Error-rate theo ngày**

1. Mở dashboard `SLO vận hành hằng ngày — 7 service` ở chế độ **Edit**.
2. **Add panel** → **Visualization**, Data view **Traces**, chart type **Line**.
3. **Horizontal axis** → **Date histogram** → field `@timestamp`, interval **Daily**.
4. **Vertical axis** → **Formula** →
   `count(kql='attributes.http.response.status_code >= 500') / count()`, format **Percent**.
5. **Breakdown** → **Top values** → `resource.attributes.service.name` (7 service).
6. Đặt tên panel: `Error-rate theo ngày (7 ngày gần nhất)`.
7. Bấm menu (⋮) trên panel vừa tạo (hoặc icon 3 chấm ở góc panel) → tìm mục **Customize time
   range** (đây là tính năng thiết kế gốc ghi rõ "chưa tự tay xác nhận tên/vị trí chính xác" — nếu
   không thấy đúng tên "Customize time range", tìm trong menu **Panel settings** hoặc biểu tượng
   đồng hồ ở panel header của phiên bản Kibana 9.4.4 đang chạy — ghi lại tên/vị trí thật tìm được để
   đưa vào tài liệu ở Task 5). Đặt = **Last 7 days**.
8. **Save and return**.

- [ ] **Bước 3: Dựng Chart B — Latency p95 theo ngày**

Lặp lại Bước 2 với 2 khác biệt: Vertical axis đổi công thức thành
`percentile(duration, percentile=95) / 1000000` (format **Number**), đặt tên panel
`Latency p95 theo ngày (7 ngày gần nhất)`. Cũng đặt Customize time range = **Last 7 days**.

- [ ] **Bước 4: Save dashboard**

**Save** (không phải "Save and return").

- [ ] **Bước 5: Bài kiểm tra "đã pass" — đếm lại panel, phải đúng 4**

```powershell
$dashObj = Invoke-RestMethod -Uri "$KibanaBase/api/saved_objects/dashboard/$dashId"
$panels = $dashObj.attributes.panelsJSON | ConvertFrom-Json
$panels.Count
```

Kỳ vọng: `4`.

- [ ] **Bước 6: Đối chiếu Chart A với aggregation thô theo ngày**

```powershell
$dailyBody = @{
  size = 0
  query = @{ bool = @{ must = @(
    @{ term = @{ "resource.attributes.service.name" = $svc } }
    @{ range = @{ "@timestamp" = @{ gte = "now-7d/d" } } }
  ) } }
  aggs = @{
    by_day = @{
      date_histogram = @{ field = "@timestamp"; calendar_interval = "1d" }
      aggs = @{
        errors = @{ filter = @{ range = @{ "attributes.http.response.status_code" = @{ gte = 500 } } } }
      }
    }
  }
} | ConvertTo-Json -Depth 10
$daily = Invoke-RestMethod -Uri "$EsBase/traces-generic.otel-default*/_search" -Method Post -Body $dailyBody -ContentType "application/json"
$daily.aggregations.by_day.buckets | ForEach-Object {
  [PSCustomObject]@{ ngay = $_.key_as_string; tong = $_.doc_count; loi = $_.errors.doc_count; ty_le = if ($_.doc_count -gt 0) { $_.errors.doc_count / $_.doc_count } else { $null } }
}
```

Kỳ vọng: mỗi ngày trong bảng kết quả PowerShell khớp với điểm dữ liệu tương ứng trên Chart A của
`Orders.Api` (nhìn bằng mắt trên đường trend, không cần khớp tuyệt đối từng chữ số).

- [ ] **Bước 7: Export lại saved objects (ghi đè)**

```powershell
$body = @{ type = @("dashboard","lens","index-pattern") } | ConvertTo-Json
Invoke-WebRequest -Uri "$KibanaBase/api/saved_objects/_export" -Method Post `
  -Headers $KbnHeaders -ContentType "application/json" -Body $body `
  -OutFile "docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson"
```

- [ ] **Bước 8: Commit**

```bash
git add docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson
git commit -m "feat(kibana): them Tang 1.5 trend chart Error-rate va Latency p95 theo ngay"
```

---

### Task 4: Tầng 2 — 4 panel đào sâu trong Collapsible section

**Files:**
- Modify (qua Kibana UI): dashboard `SLO vận hành hằng ngày — 7 service` (thêm 1 Collapsible
  section chứa 4 panel)
- Modify: `docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson` (ghi đè)

**Interfaces:**
- Consumes: dashboard id `$dashId`, đúng cấu hình 4 panel đã verify ở
  `docs/kibana-quan-sat-he-thong/05-dashboard-va-visualize.md` Bước 2-5 (Line chart
  `dotnet.exceptions` Counter rate; Bar/Stacked status code; Table top endpoint chậm nhất; Metric
  đếm 401+403), chỉ đổi time picker sang `Last 24 hours` cấp dashboard thay vì `Last 15 minutes`.
- Produces: tổng số panel cấp cao nhất dự kiến = `5` (4 từ Task 3 + 1 group Collapsible section) —
  **cần xác nhận thật ở Bước 5** vì cách Kibana lưu Collapsible section trong `panelsJSON` chưa được
  thiết kế gốc xác nhận trực tiếp.

- [ ] **Bước 1: Bài kiểm tra "thất bại" — đếm panel hiện có, phải đúng 4 (từ Task 3)**

```powershell
$dashObj = Invoke-RestMethod -Uri "$KibanaBase/api/saved_objects/dashboard/$dashId"
$panels = $dashObj.attributes.panelsJSON | ConvertFrom-Json
$panels.Count
```

Kỳ vọng: `4`.

- [ ] **Bước 2: Tạo Collapsible section**

1. Mở dashboard ở chế độ **Edit**.
2. **Add panel** → tìm mục **Collapsible section** (đã xác nhận có thật trong menu theo thiết kế
   gốc). Đặt tên: `Đào sâu khi có báo động`. Để mặc định ở trạng thái thu gọn (collapsed) — không
   bấm mở rộng sau khi tạo.

- [ ] **Bước 3: Dựng 4 panel bên trong section, đúng cấu hình đã verify ở file 05**

Kéo/thêm lần lượt 4 panel vào bên trong Collapsible section vừa tạo, cấu hình y hệt
`docs/kibana-quan-sat-he-thong/05-dashboard-va-visualize.md`:

1. **Line chart** — Data view Metrics, Vertical axis `metrics.dotnet.exceptions` hàm **Counter
   rate**, Horizontal axis Date histogram `@timestamp`, Breakdown Top values
   `resource.attributes.service.name`.
2. **Bar/Stacked** — Data view Traces, Horizontal axis Top values
   `resource.attributes.service.name`, Vertical axis **Count**, Breakdown Top values
   `attributes.http.response.status_code`.
3. **Table** — Data view Traces, nhóm theo `attributes.http.route` (Top values), giá trị **Average**
   trên `duration`, sắp xếp giảm dần.
4. **Metric** — Data view Traces, giá trị **Count**, KQL filter
   `attributes.http.response.status_code >= 401 and attributes.http.response.status_code <= 403`.

Khác biệt duy nhất so với file 05: không tự đặt time range riêng cho panel nào — để mặc định kế thừa
`Last 24 hours` cấp dashboard.

- [ ] **Bước 4: Save dashboard, kiểm tra hành vi collapse/expand**

**Save**. Sau khi lưu, reload lại trang dashboard (F5) và quan sát: section **Đào sâu khi có báo
động** phải hiện ở trạng thái thu gọn mặc định. Bấm vào để mở rộng, xác nhận cả 4 panel hiện đúng dữ
liệu (không trống/lỗi). Thu gọn lại trước khi tiếp tục.

- [ ] **Bước 5: Bài kiểm tra "đã pass" — đếm panel, xác nhận thật cách Kibana biểu diễn Collapsible section**

```powershell
$dashObj = Invoke-RestMethod -Uri "$KibanaBase/api/saved_objects/dashboard/$dashId"
$panels = $dashObj.attributes.panelsJSON | ConvertFrom-Json
$panels.Count
$panels | Select-Object -ExpandProperty type -ErrorAction SilentlyContinue
```

Kỳ vọng chính: tổng panel cấp cao nhất = `5` (đúng dự kiến ở Interfaces). **Nếu ra số khác** (ví dụ
Kibana lưu 4 panel con trực tiếp ở cấp cao nhất kèm 1 thuộc tính group riêng, cho ra tổng `9`, hoặc
Collapsible section không xuất hiện trong `panelsJSON` mà ở 1 cấu trúc khác) — đó là kết quả thật,
không phải lỗi cần sửa: ghi lại đúng con số và cấu trúc JSON thật quan sát được, đưa vào tài liệu ở
Task 5 (đây chính là mục "chưa tự tay xác nhận hành vi Collapsible section" mà thiết kế gốc để ngỏ).

- [ ] **Bước 6: Export lại saved objects (ghi đè)**

```powershell
$body = @{ type = @("dashboard","lens","index-pattern") } | ConvertTo-Json
Invoke-WebRequest -Uri "$KibanaBase/api/saved_objects/_export" -Method Post `
  -Headers $KbnHeaders -ContentType "application/json" -Body $body `
  -OutFile "docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson"
```

- [ ] **Bước 7: Commit**

```bash
git add docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson
git commit -m "feat(kibana): them Tang 2 collapsible section 4 panel dao sau"
```

---

### Task 5: Viết tài liệu file 06, cập nhật docs/README.md, đóng checklist thiết kế gốc

**Files:**
- Create: `docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md`
- Modify: `docs/README.md` (dòng đếm file Kibana)
- Modify: `docs/superpowers/specs/2026-09-08-dashboard-slo-van-hanh-design.md` (đánh dấu checklist)

**Interfaces:**
- Consumes: mọi kết quả xác nhận thật đã ghi lại ở Task 2-4 (con số Error-rate/Latency đối chiếu
  được ở Bước 4-5/Task 2 và Bước 6/Task 3, tên/vị trí thật của "Customize time range" ghi ở Bước
  2/Task 3, cấu trúc `panelsJSON` thật của Collapsible section ghi ở Bước 5/Task 4, cách formula xử
  lý mẫu số 0 ghi ở Bước 2.7/Task 2).

- [ ] **Bước 1: Bài kiểm tra "thất bại" — xác nhận file 06 chưa tồn tại**

```powershell
Test-Path "docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md"
```

Kỳ vọng: `False`.

- [ ] **Bước 2: Viết `docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md`**

Viết theo đúng văn phong file 00-05 (xưng "bạn", phân biệt rõ "đã tự kiểm chứng" khỏi "khuyến nghị",
trích số liệu thật). Nội dung tối thiểu bắt buộc có:

```markdown
# 06 — Dashboard SLO vận hành hằng ngày

*(Cần đã làm file 00-05 — file này dựng 1 dashboard thật đo đúng 3 chỉ số SLO đã khai báo trong
`service-manifest.yaml`, khác Panel ở file 05 vốn chỉ nhằm dạy cách dùng Lens.)*

Thiết kế đầy đủ: [`docs/superpowers/specs/2026-09-08-dashboard-slo-van-hanh-design.md`](../superpowers/specs/2026-09-08-dashboard-slo-van-hanh-design.md).
Mã nguồn export thật: [`dashboards/slo-van-hanh-hang-ngay.ndjson`](dashboards/slo-van-hanh-hang-ngay.ndjson).

## Quyết định đã khoá lúc build: cách hiển thị cột "Ngưỡng"

[điền lại nguyên văn quyết định phương án (c) đã ghi ở Ràng buộc chung của kế hoạch triển khai, kèm
tên cột thật đã đặt ở Task 2 Bước 2 mục 4-6]

## Đã xác nhận thật lúc build (khép lại các mục "chưa chốt" của thiết kế gốc)

- **Cú pháp Lens Formula cho Error-rate**: `count(kql='attributes.http.response.status_code >= 500') / count()`
  — [ghi lại kết quả thật Task 2 Bước 4: có khớp truy vấn REST API thô không, chênh lệch bao nhiêu]
- **Lens Formula xử lý mẫu số 0**: [điền kết quả thật quan sát ở Task 2 Bước 7 — hiển thị `-`/trống
  hay cần `ifelse`]
- **Tên/vị trí "Customize time range" trên Kibana 9.4.4**: [điền kết quả thật quan sát ở Task 3 Bước
  2]
- **Cách Collapsible section lưu trong `panelsJSON`**: [điền kết quả thật quan sát ở Task 4 Bước 5 —
  số panel cấp cao nhất thật, có đúng dự kiến `5` không]

## Cách tự đối chiếu lại (đúng thói quen file 01-04, không tin số Lens hiển thị mà không tự kiểm tra)

[dẫn lại 2 lệnh PowerShell đối chiếu Error-rate và Latency ở Task 2 Bước 4-5, ghi rõ kết quả thật đã
chạy — không chỉ chép lệnh mà không có số liệu]

## Giới hạn đã biết

- Availability đo xấp xỉ (`100% − Error-rate`), không phải uptime thật — service sập hẳn (0 traces)
  sẽ biến mất khỏi bảng thay vì hiện cảnh báo (xem `SCRUM-29`/`SCRUM-30`).
- Không có alert rule tự động đi kèm dashboard này (thuộc `SCRUM-35`, giai đoạn sau).
```

(Thay mọi `[điền ...]` bằng số liệu/kết quả THẬT đã quan sát khi thực thi Task 2-4 — không được để
lại dấu ngoặc vuông nào trong file thật.)

- [ ] **Bước 3: Cập nhật `docs/README.md`**

Sửa dòng (hiện ở khoảng dòng 49):

```
→ [`docs/kibana-quan-sat-he-thong/`](kibana-quan-sat-he-thong/) — 6 file hands-on, đọc theo số
`00`→`05`, mọi lệnh/field/thao tác đều lấy trực tiếp từ dữ liệu thật và chính giao diện Kibana đang
chạy trên máy bạn, không phải ví dụ Kibana chung chung.
```

thành:

```
→ [`docs/kibana-quan-sat-he-thong/`](kibana-quan-sat-he-thong/) — 7 file hands-on, đọc theo số
`00`→`06`, mọi lệnh/field/thao tác đều lấy trực tiếp từ dữ liệu thật và chính giao diện Kibana đang
chạy trên máy bạn, không phải ví dụ Kibana chung chung.
```

- [ ] **Bước 4: Đánh dấu checklist trong thiết kế gốc**

Trong `docs/superpowers/specs/2026-09-08-dashboard-slo-van-hanh-design.md`, mục cuối "Việc cần làm ở
bước triển khai" — đổi cả 5 dòng `- [ ]` thành `- [x]`, thêm 1 dòng ngay dưới tiêu đề mục đó:

```markdown
**Đã hoàn tất** — xem kết quả thật ở [`docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md`](../../kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md).
```

- [ ] **Bước 5: Bài kiểm tra "đã pass" — xác nhận cả 3 file đã đúng trạng thái mong muốn**

```powershell
Test-Path "docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md"
Select-String -Path "docs/README.md" -Pattern "00.→.06"
Select-String -Path "docs/superpowers/specs/2026-09-08-dashboard-slo-van-hanh-design.md" -Pattern "\[x\]"
```

Kỳ vọng: dòng 1 trả `True`; dòng 2 tìm thấy khớp; dòng 3 trả về đúng 5 dòng đã tick.

- [ ] **Bước 6: Commit**

```bash
git add docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md docs/README.md docs/superpowers/specs/2026-09-08-dashboard-slo-van-hanh-design.md
git commit -m "docs: hoan tat tai lieu va checklist dashboard SLO van hanh hang ngay"
```

---

## Tự rà soát (đã chạy khi viết kế hoạch này)

**Phủ hết thiết kế gốc**: Tầng 1 bảng SLO → Task 2; Tầng 1.5 trend chart → Task 3; Tầng 2 collapsible
→ Task 4; cả 3 trường hợp biên (cách hiển ngưỡng, chia cho 0, service down hoàn toàn) → Task 2 Bước
2 và ghi trong panel Markdown; mục "Cách xác nhận đúng" → Task 2 Bước 4-5, Task 3 Bước 6; checklist
"Việc cần làm ở bước triển khai" → đóng hết ở Task 5. Không có mục nào trong thiết kế gốc bị bỏ sót.

**Không có placeholder**: mọi bước có lệnh PowerShell/cấu hình UI cụ thể, không có "TBD"/"xử lý phù
hợp". 2 điểm thiết kế gốc chủ động để ngỏ ("Customize time range" tên/vị trí thật, hành vi
Collapsible section) được giữ nguyên dạng **bước xác minh có kỳ vọng cụ thể + hướng xử lý nếu lệch**,
không phải để trống chờ điền sau.

**Nhất quán tên/biến**: `$KibanaBase`, `$EsBase`, `$KbnHeaders`, `$dashId` dùng xuyên suốt Task 1-4
đúng như khai báo ở mục Interfaces của từng task; tên dashboard `SLO vận hành hằng ngày — 7 service`
và đường dẫn `docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson` giữ cố định từ
Task 1 tới Task 5.
