# 08 — Phát hiện nhanh và xử lý sự cố

*(Cần đã làm file 07: file này dùng cùng dữ liệu traces, cùng khoá mã hoá Kibana và cùng dashboard SLO
hằng ngày.)*

Đặc tả: [`specs/028-incident-oncall-drill/`](../../specs/028-incident-oncall-drill/spec.md) (SCRUM-36).
Hợp đồng: [`contracts/fast-detection-rule-contract.md`](../../specs/028-incident-oncall-drill/contracts/fast-detection-rule-contract.md).
Export thật: [`alerts/incident-fast-detection-rule.ndjson`](alerts/incident-fast-detection-rule.ndjson) (rule),
[`dashboards/slo-van-hanh-hang-ngay.ndjson`](dashboards/slo-van-hanh-hang-ngay.ndjson) (dashboard).

Bốn rule ngân sách lỗi của file 07 đo mức tiêu hao **cả tháng**. Một sự cố đơn lẻ có thể cần hàng giờ
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
- tỷ lệ 5xx ≥ 0.1%, **hoặc**
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
| WHERE err_pct >= 0.1 OR latency_breach
| STATS breaching = COUNT(*) BY service
| KEEP service
```

Vì sao có hai lệnh `STATS` và chỉ giữ cột `service`:
- Hệ quả 2 và 3 trong research của 027: Kibana ghép **mã alert** từ mọi cột kết quả, và chỉ lấy cột
  của lệnh `STATS` cuối cùng.
- Nếu giữ thêm `err_pct` hay `p95`, mã alert sẽ đổi sau mỗi lần chạy. Alert cũ sẽ "recovered" rồi alert
  mới sinh ra mỗi 5 phút, nên thời điểm bắt đầu (`kibana.alert.start`) không còn dùng làm mốc "alert
  bắn" được.

Service không có span nào trong 5 phút thì không có hàng, nên không có alert. Thiếu dữ liệu không bị
hiểu thành sự cố.

**Ngưỡng nằm ở hai nơi**: trong `CASE` ở trên và trong `slos` của manifest. Không có test nào giữ hai
nơi này khớp nhau (sai lệch Nguyên tắc III, xem `specs/028-incident-oncall-drill/plan.md`). Sửa
manifest thì phải sửa rule này bằng tay, rồi export lại.

### Đã kiểm chứng ở mức nền (2026-10-02)

Chạy truy vấn trong Discover (ES|QL) khi tải nền `./scripts/incident-drill.ps1 -Load` đang chạy và
không có sự cố: **0 hàng**. Số đo của hai cửa sổ 5 phút liên tiếp: 0 lỗi 5xx ở cả 7 service; p95 lớn
nhất 133 ms và p99 lớn nhất 208 ms, đều ở gateway, nơi không xét độ trễ.

Mức nền chỉ khoẻ với tải nhẹ (nghỉ 1000 ms giữa request, token lấy mỗi 30 phút). Với tải nặng hơn
(nghỉ 200 ms, lấy token mỗi vòng), nhiều service đã vượt SLO mà không cần sự cố nào. Số đo đầy đủ ở
`specs/028-incident-oncall-drill/research.md`, mục "Kết quả xác minh".

## Bảng trên dashboard SLO hằng ngày

Bảng "Phát hiện nhanh — vượt SLO trong 5 phút gần nhất" là một Discover session ES|QL (id
`incident-fast-detection-active-alerts`). Nó nằm thành một hàng rộng toàn trang, ngay dưới bảng "Cạn
ngân sách" của file 07. Các panel bên dưới chỉ bị đẩy xuống, kích thước và nội dung giữ nguyên. Cách
dựng giống bảng cảnh báo của 027 (Saved Objects API).

```esql
FROM .alerts-stack.alerts-default
| WHERE kibana.alert.rule.tags == "incident-fast-detection" AND kibana.alert.status == "active" AND @timestamp >= NOW() - 15 minutes
| EVAL service = kibana.alert.grouping.service
| KEEP service, kibana.alert.start
| SORT service
```

`@timestamp >= NOW() - 15 minutes` bỏ các alert "mồ côi" (Kibana mất trạng thái lúc quá tải), cùng lý
do như bảng cảnh báo của 027.

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
| EVAL dat = total > 0 AND err_pct < 0.1 AND p95_ns <= 150000000 AND p99_ns <= 500000000
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

### Đã kiểm chứng trên dữ liệu thật (T025/T030, 2026-10-02)

Chế độ không mù: tiêm loại A (đích kết nối sai) vào `orders-api`.

| Mốc | Thời điểm (+07:00) |
|---|---|
| Tiêm lỗi (`injected-at.txt`) | 11:00:25 |
| Alert `Orders.Api` bắn (`kibana.alert.start`) | 11:02:00 — lần chạy đầu, cùng lúc cả 7 service do khởi động nguội |
| Khôi phục: bỏ cờ khỏi `.env`, chạy lại compose | ~11:27 |
| Alert `Orders.Api` recovered (`kibana.alert.end`) | 11:32:03 |
| Chuỗi đạt SLO đầu tiên | 11:46–11:59 (14 phút); đứt ở 12:00 vì p95 167 ms |
| **Giải quyết** (phút cuối của 15 phút liên tục `dat = true`) | **12:15** (chuỗi 12:01–12:15, tiếp tục đạt tới hết dữ liệu 12:29) |

Từ lúc khôi phục tới lúc giải quyết mất khoảng 48 phút. Nguyên nhân là môi trường local chậm từng đợt:
p95 của `Orders.Api` vượt 150 ms ở các phút 11:31–11:34, 11:41, 11:43–11:45, 12:00, dù không còn lỗi
5xx nào. Đây chính là lý do không ghi "giải quyết" chỉ vì service "trông có vẻ ổn".
