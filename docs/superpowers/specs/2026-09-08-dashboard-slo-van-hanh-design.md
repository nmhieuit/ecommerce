# Thiết kế: Dashboard SLO vận hành hằng ngày

**Ngày:** 2026-09-08
**Trạng thái:** Đã được duyệt qua brainstorm, sẵn sàng chuyển sang kế hoạch triển khai

## Bối cảnh

Dự án đã có `docs/kibana-quan-sat-he-thong/05-dashboard-va-visualize.md` — hướng dẫn dựng 1 dashboard
gồm 4 panel, nhưng viết ra với mục đích **dạy cách dùng Lens**, nối tiếp 4 tình huống điều tra thủ công
ở file 02. Dashboard đó chưa từng thực sự được lưu lại (đã xác nhận qua
`GET /api/saved_objects/_find`: 0 dashboard, chỉ có 2 Lens visualization rời rạc không liên quan).

`service-manifest.yaml` của cả 7 service đã khai báo sẵn SLO mục tiêu thật theo constitution
Principle VIII (availability 99.9%/tháng, error-rate ≤0.1% 5xx, latency p95/p99 — riêng `Bff.Api` có
ngân sách latency nới hơn: p95 300ms/p99 800ms so với 150ms/500ms của 6 service còn lại) — nhưng
**chưa từng được đo tự động ở đâu** (0 alert rule trong Kibana, `docs/roadmap.md` xếp việc đo SLO thật
vào `SCRUM-29`/`SCRUM-35` ở Giai đoạn 4-5, chưa triển khai).

## Mục tiêu

Dựng **1 dashboard tách biệt**, phục vụ đúng 1 người (solo-operator đang thực hành vai SRE theo
`docs/roadmap.md`) mở xem **mỗi ngày**, lấy đúng 3 chỉ số SLO đã khai báo làm trục chính — lần đầu tiên
Principle VIII được đo thật trong dự án, đặt nền cho việc xây error-budget policy chính thức sau này.

**Không thuộc phạm vi**: sửa lại hay xoá `05-dashboard-va-visualize.md` (giữ nguyên vai trò tài liệu học
Lens); dựng alert rule tự động (thuộc `SCRUM-35`, chưa tới lượt); đo Availability bằng synthetic uptime
check thật (chưa có hạ tầng, xem phần Giới hạn).

## Cấu trúc tổng thể — 3 tầng

```
Tầng 1   — Bảng SLO: 7 service × 3 chỉ số (Error-rate, Latency p95, Latency p99),
           mỗi chỉ số 2 cột Thực tế | Ngưỡng cam kết. Cửa sổ: Last 24 hours (cấp dashboard).
Tầng 1.5 — 2 trend chart: Error-rate theo ngày, Latency p95 theo ngày (7 ngày gần nhất, tách theo
           service).
Tầng 2   — 4 panel đào sâu (dựng mới theo đúng cấu hình đã có ở file 05), gói trong 1 Collapsible
           section thu gọn mặc định, chỉ mở khi Tầng 1 báo có vấn đề.
```

Lý do 3 tầng: khớp thói quen dùng thật — liếc Tầng 1 vài giây biết có vấn đề không, nhìn Tầng 1.5 biết
đang tệ dần hay ổn định, chỉ mở Tầng 2 khi cần điều tra sâu.

## Tầng 1 — Bảng SLO

- **Data view**: Traces
- **Rows**: Top values trên `resource.attributes.service.name` (7 service)
- **3 chỉ số**, mỗi chỉ số gồm cột "Thực tế" (tính từ dữ liệu) và cột "Ngưỡng" (số tĩnh, chép từ
  `service-manifest.yaml`, không tính từ dữ liệu):

  | Chỉ số | Thực tế | Ngưỡng |
  |---|---|---|
  | Error-rate (5xx) | % span có `attributes.http.response.status_code >= 500` trên tổng span mỗi service | `0.1%` (cả 7 service) |
  | Latency p95 | Percentile 95 của `duration` | `150ms` (6 service) / `300ms` (`Bff.Api`) |
  | Latency p99 | Percentile 99 của `duration` | `500ms` (6 service) / `800ms` (`Bff.Api`) |

- **Availability xấp xỉ** (đã thống nhất: dùng `100% − Error-rate`, không đo uptime thật vì chưa có
  synthetic check): không tách thành cột riêng vì trùng thông tin với Error-rate — chỉ ghi 1 dòng chú
  thích dưới bảng, vd *"Availability xấp xỉ hôm nay: 99.98% — ngưỡng 99.9%"*.
- **Cửa sổ thời gian**: đặt ở time picker cấp dashboard = **Last 24 hours**, áp dụng chung toàn Tầng 1.

## Tầng 1.5 — 2 trend chart

- **Chart A — Error-rate theo ngày**: Line chart, Data view Traces, Horizontal axis = Date histogram
  `@timestamp` (interval 1 ngày), Vertical axis = công thức Error-rate (như Tầng 1, tính theo từng
  ngày), Breakdown = Top values `resource.attributes.service.name`.
- **Chart B — Latency p95 theo ngày**: cấu trúc giống Chart A, Vertical axis đổi thành Percentile 95
  của `duration`.
- **Vì sao đúng 2 chart này**: Error-rate là chỉ số hành động được nhiều nhất; Latency chọn p95 thay vì
  p99 vì p99 dễ nhiễu ở lưu lượng thấp hiện tại (1 request chậm bất thường làm p99 nhảy vọt vô nghĩa),
  p95 cho đường xu hướng ổn định hơn cho mục đích theo dõi hàng ngày; p99 vẫn xem được ở Tầng 1 dạng
  điểm-tại-thời-điểm. Availability không cần trend riêng vì tính từ cùng dữ liệu Error-rate.
- **Cần verify lúc build, chưa hứa trước**: cửa sổ 7 ngày của tầng này khác cửa sổ 24h cấp dashboard —
  cần dùng tính năng "Customize time range" theo từng panel của Kibana Lens; chưa tự tay xác nhận tên/
  vị trí chính xác của tính năng này trong phiên bản Kibana đang chạy.

## Tầng 2 — 4 panel đào sâu

Dựng mới đúng theo cấu hình đã có sẵn, đã verify từng bước ở
`docs/kibana-quan-sat-he-thong/05-dashboard-va-visualize.md` (không lặp lại chi tiết ở đây):

1. Line chart — xu hướng `dotnet.exceptions` (Counter rate) theo service
2. Bar/Stacked — phân bố status code theo service
3. Table — top endpoint chậm nhất theo `duration`
4. Metric — đếm tổng 401+403

Khác biệt duy nhất so với file 05: dùng chung cửa sổ Last 24 hours cấp dashboard thay vì "Last 15
minutes" mặc định lúc demo.

**Đề xuất, cần verify lúc build**: gói 4 panel vào 1 **Collapsible section** (đã thấy thật trong menu
"Add panel" → "Collapsible section"), đặt tên "Đào sâu khi có báo động", thu gọn mặc định. Chưa tự tay
xác nhận hành vi collapse/expand hoạt động đúng ra sao ở phiên bản Kibana này.

**Cập nhật sau khi build**: đã verify — Collapsible section KHÔNG thực sự ẩn nội dung panel bên
trong ở chế độ View (chỉ đổi trạng thái header), xem chi tiết và bằng chứng ở
[`docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md`](../../kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md).

## Trường hợp biên & rủi ro đã biết

- **Cách hiển thị cột "Ngưỡng" — chưa chốt cơ chế**: Lens Table vốn dựng từ aggregation trên dữ liệu
  thật, không có cách hiển nhiên để chèn 1 giá trị tĩnh (số ngưỡng chép tay từ `service-manifest.yaml`)
  khác nhau theo từng dòng ngay trong cùng bảng. Cần chọn 1 trong các hướng sau lúc build, chưa chốt ở
  bước thiết kế này: (a) dùng `esql`/runtime field gán ngưỡng theo `service.name` bằng biểu thức
  `CASE`, (b) tách riêng 1 panel dạng chữ (Markdown) liệt kê ngưỡng cạnh bảng thay vì gộp chung 1 bảng,
  (c) chấp nhận ghi ngưỡng trực tiếp trong tên cột (vd "Error-rate — ngưỡng 0.1%") nếu ngưỡng giống
  nhau cho phần lớn service, chỉ ghi chú riêng ngoại lệ của `Bff.Api` bằng chữ bên cạnh bảng.
- **Chia cho 0**: nếu 1 service không có span nào trong 24h qua, công thức `count(5xx)/count(total)`
  có thể ra lỗi/NaN thay vì "0%" — cần kiểm tra Lens Formula xử lý mẫu số 0 thế nào lúc build; nếu
  không tự xử lý, cần hiển thị rõ "Không có dữ liệu" thay vì để hiểu lầm thành "0% lỗi = tốt".
- **Service down hoàn toàn**: bảng Tầng 1 chỉ tính trên request THẬT SỰ đã tới nơi — 1 service sập hẳn
  (không phát cả metrics lẫn traces) sẽ khiến dòng của nó biến mất khỏi bảng thay vì hiện cảnh báo. Đây
  là giới hạn cố hữu của cách đo "availability xấp xỉ" đã thống nhất, không phải lỗi cần sửa ở thiết kế
  này — ghi nhận làm tiền đề cho synthetic uptime check thật ở giai đoạn sau (`SCRUM-29`/`SCRUM-30`).

## Cách xác nhận đúng trước khi tin dùng hàng ngày

Áp dụng đúng thói quen đối chiếu đã xây dựng xuyên suốt file 01-04 — không tin 1 con số Lens hiển thị
mà không tự kiểm tra lại ít nhất 1 lần:

1. Với 1 service mẫu, lấy số Error-rate Tầng 1 hiển thị, tự tính lại bằng 2 lệnh `_count` riêng biệt
   (tổng số span, số span ≥500) qua `Invoke-RestMethod` như đã học ở file 02 — 2 cách phải ra cùng 1
   tỉ lệ.
2. Với Latency p95/p99, đối chiếu bằng Discover: sort `duration` giảm dần trên đúng service đó, tự ước
   lượng vị trí percentile bằng mắt trên tổng số document đang lọc.
3. Chỉ coi bảng đáng tin cho cả 7 service sau khi đối chiếu khớp ở 1-2 service mẫu.

## Việc cần làm ở bước triển khai (không thuộc phạm vi thiết kế này)

**Đã hoàn tất** — xem kết quả thật ở [`docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md`](../../kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md).

- [x] Xác nhận cú pháp Lens Formula thật cho Error-rate (tỉ lệ 2 count có điều kiện KQL)
- [x] Xác nhận tính năng "Customize time range" theo panel còn đúng tên/vị trí ở phiên bản Kibana này
- [x] Xác nhận hành vi Collapsible section
- [x] Xác nhận cách Lens Formula xử lý mẫu số 0
- [x] Đặt tên và lưu dashboard, đặt tên gợi nhớ (vd `SLO vận hành hằng ngày — 7 service`)
