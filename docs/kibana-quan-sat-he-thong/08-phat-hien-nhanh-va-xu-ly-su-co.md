# 08 — Phát hiện nhanh và xử lý sự cố

*(Cần đã làm file 07: file này dùng cùng dữ liệu traces, cùng khoá mã hoá Kibana và cùng dashboard
`Xử lý sự cố — 7 service` (bảng Phát hiện nhanh).)*

Đặc tả: [`specs/028-incident-oncall-drill/`](../../specs/028-incident-oncall-drill/spec.md) (SCRUM-36).
Hợp đồng: [`contracts/fast-detection-rule-contract.md`](../../specs/028-incident-oncall-drill/contracts/fast-detection-rule-contract.md).
Export thật: [`alerts/incident-fast-detection-rule.ndjson`](alerts/incident-fast-detection-rule.ndjson) (rule),
[`dashboards/xu-ly-su-co.ndjson`](dashboards/xu-ly-su-co.ndjson) (dashboard).

Bốn rule ngân sách lỗi của file 07 đo mức tiêu hao **cả tuần lịch** (từ thứ Hai 00:00 giờ Việt Nam). Một sự cố đơn lẻ có thể cần hàng giờ
mới đẩy mức đó qua mốc 50%, nên chúng không dùng để phát hiện sự cố. File này thêm một rule nhìn **5
phút gần nhất**, và mô tả cách xác nhận khôi phục bằng số đo. Quy trình triage (severity, Kibana Case,
bản ghi sự cố) nằm ở
[`docs/dien-tap-chaos-engineering/README.md`](../dien-tap-chaos-engineering/README.md#diễn-tập-sự-cố-on-call-scrum-36).

## Rule `incident-fast-detection`

| Thuộc tính | Giá trị |
|---|---|
| Loại | Elasticsearch query (`.es-query`), ES|QL, tạo một alert cho mỗi hàng (`groupBy: row`) |
| Chu kỳ / cửa sổ | 5 phút / 5 phút |
| Tag | `incident-fast-detection` |
| Action | không có (người dùng chốt: chỉ trong Kibana) |
| Id | `9b0e2c36-678b-4cd7-9de0-7468d623f82d` |

Một service có alert khi, trong 5 phút gần nhất:
- tỷ lệ 5xx ≥ 1% (SLO 5xx dưới 1% — hiến chương 2.0.0, spec 029), **hoặc**
- p95 hoặc p99 vượt ngưỡng `slos.latency` trong manifest của chính nó.

Ngưỡng là `Bff.Api` 300/800 ms và 150/500 ms cho mọi service còn lại.

**Riêng `Gateway.Api` chỉ xét 5xx** (người dùng chốt 2026-10-02). Ngưỡng 150/500 ms của gateway chặt
hơn ngưỡng 300/800 ms của BFF, trong khi mọi request của gateway đều đi qua BFF. Vì vậy ở mức nền
gateway đã vượt độ trễ: đo được p95 245 ms, POST p95 538 ms.

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 5 minutes
| EVAL service = resource.attributes.service.name
| EVAL is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0)
| STATS total = COUNT(*), bad_5xx = SUM(is_5xx), p95_ns = PERCENTILE(duration, 95), p99_ns = PERCENTILE(duration, 99) BY service
| EVAL err_pct = TO_DOUBLE(bad_5xx) / total * 100.0
| EVAL p95_slo_ns = CASE(service == "Bff.Api", 300000000, 150000000)
| EVAL p99_slo_ns = CASE(service == "Bff.Api", 800000000, 500000000)
| EVAL latency_breach = CASE(service == "Gateway.Api", false, p95_ns > p95_slo_ns OR p99_ns > p99_slo_ns)
| WHERE err_pct >= 1 OR latency_breach
| STATS breaching = COUNT(*) BY service
| KEEP service
```

Vì sao có hai lệnh `STATS` và chỉ giữ cột `service`:
- Ràng buộc kỹ thuật trong research của 027: Kibana ghép **mã alert** từ mọi cột kết quả, và chỉ lấy
  cột của lệnh `STATS` cuối cùng.
- Nếu giữ thêm `err_pct` hay `p95`, mã alert sẽ đổi sau mỗi lần chạy. Alert cũ sẽ "recovered" rồi alert
  mới sinh ra mỗi 5 phút, nên thời điểm bắt đầu (`kibana.alert.start`) không còn dùng làm mốc "alert
  bắn" được.

Service không có span nào trong 5 phút thì không có hàng, nên không có alert. Thiếu dữ liệu không bị
hiểu thành sự cố.

**Ngưỡng nằm ở hai nơi**: trong `CASE` ở trên và trong `slos` của manifest. Từ spec 030, test
[`IncidentFastDetectionRuleDefinitionTests`](../../tests/ServiceManifestSloConventionTests/IncidentFastDetectionRuleDefinitionTests.cs) giữ hai
nơi này khớp nhau (đã đóng sai lệch Nguyên tắc III của 028). Sửa manifest thì phải sửa rule này bằng tay, rồi export lại; test báo đỏ nếu quên.

## Bảng trên dashboard Xử lý sự cố

Bảng "Phát hiện nhanh — service vượt SLO trong khoảng thời gian đã chọn" là một Discover session ES|QL (id
`incident-fast-detection-active-alerts`) ở section "Tình trạng SLO" của dashboard `Xử lý sự cố — 7 service`
(từ spec 030; trước đó nằm ngay dưới bảng "Cạn ngân sách" của dashboard SLO cũ). Bảng **đi theo thanh thời gian** như mọi
panel khác của dashboard, thay vì lọc cứng 15 phút gần nhất.

```esql
FROM .alerts-stack.alerts-default
| WHERE kibana.alert.rule.tags == "incident-fast-detection"
| EVAL service = kibana.alert.grouping.service, status = kibana.alert.status
| KEEP service, status, kibana.alert.start
| SORT status, service
```

Cột `status` phân biệt alert đang hoạt động (`active`) với alert đã tắt (`recovered`) trong khoảng thời gian đã chọn; đặt thanh thời gian
ngắn (ví dụ 15 phút) để chỉ thấy alert gần đây. Điều kiện `@timestamp >= NOW() - 15 minutes` của bản cũ đã được bỏ.

## Nhiễu khởi động nguội

`scripts/incident-drill.ps1` tạo lại **cả 7 container** lúc tiêm lỗi, để uptime không lộ service đích.
Mọi service vừa khởi động lại đều chậm một lúc. Số đo thật của `Orders.Api` (không bị tiêm) sau các lần
tạo lại lúc 10:28, 10:38 và 10:44 ngày 2026-10-02:
- p95 vọt lên 0.3–9 s;
- trạng thái này kéo dài tới **5–7 phút** sau mỗi lần tạo lại.

Nghĩa là rule có thể bắn cho service **không** bị tiêm trong tối đa hai lần chạy (10 phút) đầu tiên.
Người dùng chốt giữ cách này. Tiêu chí của quy trình triage:
- alert chỉ có trong **một** lần chạy rule là nhiễu;
- alert còn active qua **≥ 2 lần chạy** là sự cố.

**Giới hạn đã biết** (người dùng chấp nhận 2026-10-02): nhiễu đo được có thể kéo dài tới 2 lần chạy, nên
thỉnh thoảng nhiễu sẽ bị tính là sự cố. Khi đó, bước "xác định nguyên nhân" sẽ cho thấy service tự hết
lỗi mà không cần làm gì. Ghi lại điều này trong bản ghi sự cố.

## Xác nhận khôi phục 15 phút

Mốc "giải quyết" chỉ được ghi khi service đạt SLO **liên tục 15 phút có traffic** **và** rule không còn
alert active cho service đó (spec FR-013). Truy vấn theo từng phút, thay `Orders.Api` và hai ngưỡng
bằng service đang xử lý (`Bff.Api`: 300000000/800000000):

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 30 minutes AND resource.attributes.service.name == "Orders.Api"
| EVAL is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0)
| STATS total = COUNT(*), bad_5xx = SUM(is_5xx), p95_ns = PERCENTILE(duration, 95), p99_ns = PERCENTILE(duration, 99) BY minute = BUCKET(@timestamp, 1 minute)
| EVAL err_pct = ROUND(TO_DOUBLE(bad_5xx) / total * 100.0, 2), p95_ms = ROUND(p95_ns / 1000000.0), p99_ms = ROUND(p99_ns / 1000000.0)
| EVAL dat = total > 0 AND err_pct < 1 AND p95_ns <= 150000000 AND p99_ns <= 500000000
| EVAL phut_gio_vn = DATE_FORMAT("HH:mm", minute + 7 hours)
| KEEP phut_gio_vn, total, err_pct, p95_ms, p99_ms, dat
| SORT phut_gio_vn
```

Cách đọc:
- Tìm 15 dòng liên tiếp (15 phút liền nhau) đều `dat = true`. Phút cuối cùng của chuỗi đó là mốc giải
  quyết.
- Một phút **không có dòng** nghĩa là không có traffic. Phút đó làm đứt chuỗi, không được tính là đạt
  (Edge Case "không traffic" của spec).
- Với gateway, chỉ xét `err_pct`, cùng lý do như rule.

`BUCKET` phải nằm trong `STATS ... BY`. Đặt nó trong `EVAL` thì Elasticsearch báo `cannot use grouping
function [BUCKET(...)] outside of a STATS or LIMIT BY command`.

Bằng chứng kiểm chứng trên dữ liệu thật không ghi ở tài liệu vận hành này; xem `docs/QA/QA_Debt.md` (mục 028, 029)
và tài liệu QA của từng spec.
