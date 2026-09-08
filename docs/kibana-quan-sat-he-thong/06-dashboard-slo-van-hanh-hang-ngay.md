# 06 — Dashboard SLO vận hành hằng ngày

*(Cần đã làm file 00-05 — file này dựng 1 dashboard thật đo đúng 3 chỉ số SLO đã khai báo trong
`service-manifest.yaml`, khác Panel ở file 05 vốn chỉ nhằm dạy cách dùng Lens.)*

Thiết kế đầy đủ: [`docs/superpowers/specs/2026-09-08-dashboard-slo-van-hanh-design.md`](../superpowers/specs/2026-09-08-dashboard-slo-van-hanh-design.md).
Mã nguồn export thật: [`dashboards/slo-van-hanh-hang-ngay.ndjson`](dashboards/slo-van-hanh-hang-ngay.ndjson).

Dashboard thật: **`SLO vận hành hằng ngày — 7 service`** (id `e2e06ff5-9cdf-4bea-acc8-5fd60ce26170`),
đã dựng đủ 3 tầng đúng thiết kế gốc, tổng cộng **8 panel cấp cao nhất** (không phải `5` như dự kiến ban
đầu — xem mục "Đã xác nhận thật lúc build" bên dưới).

## Quyết định đã khoá lúc build: cách hiển thị cột "Ngưỡng"

Đã chọn **phương án (c)** trong 3 phương án đề xuất ở thiết kế gốc: "chấp nhận ghi ngưỡng trực tiếp
trong tên cột (vd 'Error-rate — ngưỡng 0.1%') nếu ngưỡng giống nhau cho phần lớn service, chỉ ghi chú
riêng ngoại lệ của `Bff.Api` bằng chữ bên cạnh bảng." Lý do: Lens Table không có cách hiển nhiên để
chèn giá trị tĩnh khác nhau theo từng dòng trong cùng 1 cột tính từ aggregation — ghi ngay trong tên
cột là cách rẻ nhất, không cần runtime field/`esql`.

Tên cột (Name) thật đã đặt ở Task 2 Bước 2 mục 4-6:

| Cột | Name (label) thật |
|---|---|
| Error-rate | `Error-rate — Thực tế (ngưỡng ≤ 0.1%, cả 7 service)` |
| Latency p95 | `Latency p95 (ms) — Thực tế (ngưỡng 150ms; riêng Bff.Api 300ms)` |
| Latency p99 | `Latency p99 (ms) — Thực tế (ngưỡng 500ms; riêng Bff.Api 800ms)` |

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
- Không có alert rule tự động đi kèm dashboard này (thuộc `SCRUM-35`, giai đoạn sau).
- Collapsible section ở Tầng 2 lưu đúng trạng thái collapsed/expanded nhưng **không thực sự ẩn nội
  dung panel trong chế độ View** trên Kibana 9.4.4 (xem chi tiết ở mục "Đã xác nhận thật lúc build" —
  không dùng section này như cơ chế ẩn/hiện nội dung khi trình bày cho người khác).
