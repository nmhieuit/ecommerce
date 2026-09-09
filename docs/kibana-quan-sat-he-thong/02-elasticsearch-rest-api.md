# 02 — Elasticsearch REST API

*(Cần đã làm [01-lam-quen-kibana-discover.md](01-lam-quen-kibana-discover.md) — file này bỏ qua giao
diện Kibana, gọi thẳng cái Kibana vẫn đang gọi ngầm bên dưới.)*

Discover ở file 01 không phải phép màu — nó chỉ là giao diện gửi HTTP request tới Elasticsearch rồi vẽ
lại kết quả. File này dạy bạn tự gửi đúng những request đó bằng `curl`, để hiểu thứ đang chạy phía sau
và tự truy vấn được khi không có Kibana trong tay (đúng cách `specs/017-otel-servicedefaults-elastic/
quickstart.md` Scenario 3 đã làm thật).

## Bước 1 — Liệt kê toàn bộ index/data stream thật đang có

```bash
curl -s "http://localhost:9200/_cat/indices?v"
```

**Bạn sẽ thấy** 1 bảng, mỗi dòng là 1 *backing index* thật đứng sau 1 data stream, tên dạng
`.ds-traces-generic.otel-default-2026.09.06-000001`. Chú ý cột `docs.count` — đây là số document thật,
không phải ước lượng.

**Ngữ cảnh cần hiểu**: khi bạn gõ `traces-generic.otel-default*` làm index pattern ở Kibana (file 01
Bước 2), dấu `*` chính là để khớp vào tên backing index dài này — data stream chỉ là 1 cái tên "ổn định"
trỏ vào 1 chuỗi backing index thật sẽ đổi theo thời gian (rotate theo ngày/kích thước).

## Bước 2 — Lấy 1 document thật, đọc cấu trúc JSON gốc

```bash
curl -s "http://localhost:9200/traces-generic.otel-default*/_search?size=1&sort=@timestamp:desc"
```

**Bạn sẽ thấy** 1 khối JSON với `hits.hits[0]._source` chứa đúng các field bạn đã thấy dưới dạng cột ở
Discover — `resource.attributes.service.name`, `attributes.http.response.status_code`,
`attributes.correlation.id`... Đây chính là "sự thật" mà Discover chỉ đang hiển thị lại đẹp hơn.

**Điểm cần để ý**: field lồng nhau kiểu `attributes.http.response.status_code` không phải 1 object con
tên `"attributes.http.response"` — nó là `attributes` → `http` → `response` → `status_code`, 4 tầng
lồng nhau thật sự trong JSON. Query DSL bên dưới dùng đúng chuỗi có dấu chấm này làm tên field
("dotted path"), không phải cú pháp object.

## Bước 3 — Query DSL: lọc chính xác 1 điều kiện

```bash
curl -s "http://localhost:9200/traces-generic.otel-default*/_search" \
  -H 'Content-Type: application/json' \
  -d '{
    "query": {
      "term": { "resource.attributes.service.name": "Gateway.Api" }
    }
  }'
```

**Bạn sẽ thấy** `hits.total.value` là số span của riêng `Gateway.Api`. `term` tìm khớp chính xác (không
phân tích/tokenize) — dùng đúng cho field kiểu keyword như tên service, không dùng cho tìm chuỗi văn
bản tự do.

## Bước 4 — Query DSL: kết hợp nhiều điều kiện (`bool`/`must`)

```bash
curl -s "http://localhost:9200/traces-generic.otel-default*/_search" \
  -H 'Content-Type: application/json' \
  -d '{
    "query": {
      "bool": {
        "must": [
          { "term": { "resource.attributes.service.name": "Gateway.Api" } },
          { "term": { "attributes.http.response.status_code": 401 } }
        ]
      }
    },
    "size": 3
  }'
```

**Bạn sẽ thấy** tối đa 3 document (`size: 3`), mỗi cái là 1 lần `Gateway.Api` trả 401 thật — đây chính
là dữ liệu file 04 sẽ khai thác kỹ hơn.

## Bước 5 — Aggregation: đếm theo nhóm thay vì liệt kê từng dòng

Đây là câu query thật `specs/017-otel-servicedefaults-elastic/quickstart.md` Scenario 3 đã dùng để xác
nhận cả 7 service đều phát metrics:

```bash
curl -s "http://localhost:9200/metrics-generic.otel-default*/_search?size=0" \
  -H 'Content-Type: application/json' \
  -d '{"aggs":{"by_service":{"terms":{"field":"resource.attributes.service.name"}}}}'
```

**Bạn sẽ thấy** trong `aggregations.by_service.buckets` — 1 mảng 7 phần tử, mỗi phần tử có `key` (tên
service) và `doc_count` (số metric document của service đó). `size=0` nghĩa là không cần trả về
document thô, chỉ cần kết quả tổng hợp — nhanh hơn hẳn khi dữ liệu lớn.

Chạy lại đúng câu trên nhưng đổi index sang `traces-generic.otel-default*` — bạn sẽ thấy cùng 7 service
đó xuất hiện lại, lần này đếm theo số **span**, không phải metric.

## Bước 6 — `_count`: chỉ cần số lượng, không cần dữ liệu

Khi chỉ cần biết "có bao nhiêu", không cần tải cả document:

```bash
curl -s "http://localhost:9200/traces-generic.otel-default*/_count" \
  -H 'Content-Type: application/json' \
  -d '{"query":{"term":{"attributes.http.response.status_code":403}}}'
```

**Bạn sẽ thấy** 1 số nguyên trong field `count` — đây chính là số lần request thật trong hệ thống của
bạn đã bị từ chối 403 (thiếu scope). File 04 sẽ dùng đúng số này để đối chiếu sau khi bạn tự tạo thêm
1 request 403 mới qua Postman.

## Vì sao cần đúng 3 Data View riêng biệt — không gộp chung

Tới đây bạn đã tự tay truy vấn cả `traces-generic.otel-default*` (Bước 1-4, 6) lẫn
`metrics-generic.otel-default*` (Bước 5) qua REST API thật — đủ dữ liệu để trả lời câu hỏi "vì sao
không gộp 3 data stream lại thành 1" bằng bằng chứng, không phải lý thuyết chung chung.

### Nguồn gốc: 3 pipeline tách biệt ngay từ collector

`docker/otel-collector-config.yaml`, mục `service.pipelines`, định nghĩa **3 pipeline riêng**
(`traces`, `metrics`, `logs`) — cùng nhận dữ liệu qua 1 receiver `otlp`, nhưng mỗi pipeline có
`processors`/`exporters` xử lý và ghi ra Elasticsearch độc lập. 3 data stream bạn đã tạo Data View
(file 01) không phải 3 cách nhìn khác nhau của cùng 1 dữ liệu — chúng là 3 luồng dữ liệu tách biệt từ
gốc, mang cấu trúc field hoàn toàn khác nhau (bạn đã thấy điều này khi so `attributes.correlation.id`
của traces với `metrics.<tên metric>` của metrics ở Bước 5).

### Traces — đơn vị đo là 1 request, luôn tồn tại bất kể code có làm gì

`shared/ServiceDefaults/CorrelationIdMiddleware.cs` gắn `correlation.id` vào `Activity.Current` —
`Activity` này được tạo tự động bởi auto-instrumentation của ASP.NET Core cho **mọi** request HTTP,
không phụ thuộc code ứng dụng có gọi gì hay không. Đây là lý do (đã kiểm chứng thực nghiệm ở
`01-lam-quen-kibana-discover.md` bài tập 2 và `03-observability-traces-metrics-logs.md`) 1 request bị
chặn ngay ở tầng xác thực vẫn luôn có trace, dù có thể không có log nào.

**Trả lời câu hỏi**: "1 request cụ thể (của 1 user, tại 1 thời điểm) đi qua bao nhiêu service, chậm ở
đâu, lỗi xảy ra ở hop nào" — đúng ví dụ checkout đa-service ở file 03, hoặc điều tra 1 request 401/403
cụ thể ở file 04. **Không dùng metrics được** cho câu hỏi này vì metrics không gắn với 1 request nào
(xem phần dưới).

### Logs — chỉ tồn tại khi code chủ động nói ra điều gì đó

Cùng middleware trên gọi `_logger.BeginScope(...)` — nhưng `BeginScope` tự nó **không tạo ra 1 dòng
log nào**, nó chỉ gắn `CorrelationId` vào ngữ cảnh cho các lệnh `ILogger.LogXxx(...)` chạy *bên trong*
scope đó. Nếu không có dòng code nào gọi log trong lúc xử lý request, sẽ không có document log nào —
dù trace của request đó vẫn luôn tồn tại. Đây chính là lý do đa số trace không có log tương ứng mà bạn
đã tự kiểm chứng ở file 01.

**Trả lời câu hỏi**: "vì sao request này lỗi, chi tiết cụ thể là gì" — traces chỉ cho biết **có** lỗi
(`attributes.http.response.status_code`), logs cho biết **tại sao** bằng nội dung có ý nghĩa nghiệp vụ
mà cấu trúc trace không mang được — ví dụ thật `attributes.error.type : "Tenancy.MissingTenantContextException"`
bạn đã thấy ở bài tập 1 phía trên, hoặc `body.text` (message đã render sẵn) ở file 03. **Không dùng
traces được** cho câu hỏi "tại sao", vì span chỉ có method/route/status code, không có message lỗi.

### Metrics — không gắn với 1 request nào, chỉ đo xu hướng tích luỹ theo thời gian

Đã kiểm chứng trực tiếp bằng query trên toàn bộ index (không phải 1 mẫu): **0 document metrics nào**
mang field `attributes.correlation.id` hay `attributes.CorrelationId`. Đây không phải ngẫu nhiên — về
bản chất, 1 metric (counter/gauge) là 1 con số **tích luỹ** theo `resource.attributes.service.name` và
các nhãn (dimension) của riêng nó, không phải 1 sự kiện gắn với 1 lần gọi HTTP cụ thể.

Ví dụ thật: metric `dotnet.exceptions` (tìm được qua field `attributes.error.type`) đếm dồn số lượng
exception theo từng loại, theo từng service — ví dụ `Orders.Api` đã tích luỹ 163 lần
`SocketException` tính tới hiện tại. Con số này cho biết **xu hướng**: `Orders.Api` có đang throw
`SocketException` nhiều bất thường không so với trước — nhưng **không cho biết** đúng request nào đã
gây ra bất kỳ lần nào trong số 163 lần đó.

**Trả lời câu hỏi**: "hệ thống có đang khoẻ không, xu hướng lỗi/tải có tăng theo thời gian không, service
nào đáng lo nhất" — câu hỏi tổng quan theo thời gian, không phải điều tra 1 sự cố cụ thể. **Không dùng
traces/logs được** cho câu hỏi này theo cách hiệu quả — phải lọc và đếm tay từng document, trong khi
metrics đã tổng hợp sẵn con số đó liên tục.

### Bảng tổng hợp — câu hỏi thật, chọn đúng data view

| Câu hỏi thật | Dùng Data View nào | Vì sao KHÔNG dùng cái còn lại |
|---|---|---|
| "Request của user lúc 10:09 bị lỗi gì, ở service nào?" | **Traces** + **Logs**, cùng `correlation.id`/`CorrelationId` | Metrics không có correlation-id — không tra được theo 1 request |
| "`Orders.Api` có đang throw nhiều exception hơn trước không?" | **Metrics** (`dotnet.exceptions` theo thời gian) | Traces/Logs phải lọc và đếm tay từng document — không tối ưu cho xu hướng dài hạn; Metrics đã tổng hợp sẵn |
| "Vì sao request 403 này bị chặn, lý do cụ thể là gì?" | **Traces** (status code, route) rồi **Logs** (nếu có) cho chi tiết | Metrics không gắn 1 request; bản thân Traces không mang message lỗi |
| "1 lần checkout chạm bao nhiêu service, chậm ở đâu?" | **Traces** theo `correlation.id` | Logs có thể thiếu ở 1 vài hop (không phải service nào cũng log); Metrics không theo từng request |

**Kết luận**: 3 data stream không phải 3 bản sao của cùng 1 dữ liệu — chúng có đơn vị đo khác nhau
(1 request / 1 sự kiện log / 1 con số tích luỹ theo thời gian), sinh ra từ 3 cơ chế khác nhau trong
code (auto-instrumentation / lệnh `ILogger` tường minh / counter-gauge tích luỹ). Chọn sai Data View
cho 1 câu hỏi không phải "chậm hơn" — mà là **không trả lời được**, vì dữ liệu cần thiết không tồn tại
ở đó.

## Field cốt lõi cho từng pipeline — chọn đúng, không phải chọn hết

Mỗi index thật ở đây có hàng chục tới hàng trăm field (đã kiểm tra trực tiếp qua `_mapping`: traces có
~90 field, logs có ~70 field riêng trong `attributes.*`, metrics khai báo 34 tên metric thật). Phần lớn
là "nhiễu" — field ECS cho Kubernetes/cloud (`kubernetes.pod.name`, `cloud.service.name`...) không bao
giờ được điền vì stack này chạy Docker Compose cục bộ, không phải K8s. Phần dưới đây là tập con đã được
lọc bằng bằng chứng thật: đã tồn tại trong `_mapping`, đã kiểm tra có mặt trên document mẫu thật, và (với
phần lớn) đã chứng minh hữu ích qua chính các bước ở file 00-04.

### Traces — 6 field cốt lõi

| Field | Trả lời câu hỏi | Ví dụ thật |
|---|---|---|
| `resource.attributes.service.name` | **Ai** xử lý | `Baskets.Api` |
| `name` | **Làm gì** | `GET /health/ready` |
| `attributes.http.route` | **Endpoint** nào (route đã match, không phải URL thô) | `/baskets/current/clear` |
| `attributes.http.response.status_code` | **Kết quả** | `500` |
| `duration` | **Chậm hay nhanh** — đơn vị nanosecond | `11570000` (~11.57 ms) |
| `attributes.correlation.id` **và** `trace_id` | **Nối các span cùng 1 chuỗi request** — 2 field khác nhau, xem giải thích ngay dưới |

**`trace_id` khác `attributes.correlation.id` như thế nào** — phát hiện mới, chưa từng nhắc ở file
00-04: `trace_id` là ID chuẩn OTel, được SDK tự sinh và tự lan truyền qua header `traceparent` chuẩn
W3C **hoàn toàn tự động**, không cần code nào trong dự án này viết ra. `attributes.correlation.id` là ID
riêng của dự án, do `CorrelationIdMiddleware` tự viết và lan truyền qua `X-Correlation-Id`. Cả 2 đều nối
được các span của cùng 1 request xuyên nhiều service — khác biệt duy nhất: `correlation.id` là thứ con
người dễ tự đặt/dễ đọc (Postman tự sinh, bạn tự gõ được trong `curl`), còn `trace_id` luôn tồn tại kể cả
khi không ai chủ động gửi `X-Correlation-Id` nào — vì nó không phụ thuộc code nghiệp vụ, chỉ phụ thuộc
auto-instrumentation (đúng cơ chế đã xác nhận ở mục "Vì sao cần 3 Data View" phía trên).

### Logs — 8 field cốt lõi, chia làm 3 nhóm

**Nhóm "ai/ở đâu"**: `resource.attributes.service.name`, `attributes.TenantId`, `attributes.SubjectId`.

**Nhóm "chuyện gì xảy ra"**: `severity_text` (lọc nhanh theo mức độ — `Information`/`Warning`/`Error`),
`body.text` (nội dung đã render sẵn, dễ đọc hơn `attributes.{OriginalFormat}` là chuỗi mẫu chưa điền
giá trị), và bộ 3 `attributes.exception.type` / `attributes.exception.message` /
`attributes.exception.stacktrace` — chỉ xuất hiện khi có exception thật (đã xác nhận: 454 document
trong hệ thống của bạn hiện có ít nhất 1 trong 3 field này).

**Nhóm "3 loại ID — nguồn gây rối chính khi mới bắt đầu"**: 1 document log thật có thể mang **cả 3**
cùng lúc, mỗi cái phục vụ mục đích khác nhau:

| Field | Sinh ra từ đâu | Phạm vi | Dùng khi nào |
|---|---|---|---|
| `attributes.RequestId` | ASP.NET Core/Kestrel tự đặt (dạng `0HNOCS6K2QPLQ:00000009`) | Chỉ có ý nghĩa **cục bộ trong 1 service**, không lan truyền qua network | Ít dùng để tra cứu liên-service; chỉ hữu ích khi đọc log thô của đúng 1 tiến trình |
| `attributes.TraceId` | OTel SDK tự sinh, lan truyền tự động qua `traceparent` | Xuyên toàn bộ chuỗi request, **kể cả khi không ai chủ động gửi correlation id** | Khi bạn có sẵn 1 `trace_id` từ tab Traces và muốn tìm log tương ứng |
| `attributes.CorrelationId` | `CorrelationIdMiddleware` của dự án, lan truyền qua `X-Correlation-Id` | Xuyên toàn bộ chuỗi request, **do người/công cụ gọi API chủ động đặt hoặc để hệ thống tự sinh** | Khi bạn đang cầm sẵn 1 `X-Correlation-Id` từ response header (đúng cách file 03 đã dạy) |

### Metrics — 4 field khung + 5 metric tiêu biểu

| Field | Ý nghĩa |
|---|---|
| `resource.attributes.service.name` | Service nào |
| `unit` | **Đơn vị đo thật** — bỏ qua field này là nguyên nhân phổ biến nhất khi đọc sai con số (vd tưởng giây nhưng thực ra là byte) |
| `metrics.<tên>` | Giá trị số thật, tên field đổi theo từng loại metric |
| `attributes.error.type` | Dimension riêng của `dotnet.exceptions` — phân loại theo tên exception |

5 metric tiêu biểu cho bảo trì (trong số 34 metric thật đã xác minh tồn tại), mỗi cái đại diện 1 nhóm
tín hiệu khác nhau — đã lấy mẫu giá trị thật từ `Products.Api`:

| Metric | Đơn vị (`unit`) | Nhóm tín hiệu | Giá trị mẫu thật |
|---|---|---|---|
| `dotnet.exceptions` | `{exception}` (đếm dồn) | Lỗi | `6` |
| `dotnet.process.memory.working_set` | `By` (byte) | Bộ nhớ | `206778368` (~197 MB) |
| `dotnet.gc.pause.time` | `s` (giây, đếm dồn) | Áp lực Garbage Collector | `1.778184` |
| `dotnet.thread_pool.queue.length` | `{work_item}` (đếm tức thời) | Nghẽn thread pool | `0` |
| `kestrel.active_connections` | `{connection}` (đếm tức thời) | Tải/kết nối đồng thời | `0` |

## Bài tập tình huống bảo trì thật

3 tình huống dưới đây dùng đúng field vừa giới thiệu, theo đúng ngữ cảnh 1 kỹ sư bảo trì thật sẽ gặp —
không lặp lại các kịch bản 401/403/tenant đã có ở file 04.

### Tình huống 1 — "Endpoint này đang chậm hơn bình thường, kiểm tra giúp"

**Bối cảnh**: 1 đồng nghiệp báo `Products.Api` phản hồi chậm gần đây, không rõ request nào, không có
correlation-id trong tay.

**Field cần dùng và vì sao**: `duration` (để sắp xếp/tìm span chậm nhất — không dùng `status_code` vì
1 request chậm vẫn có thể trả `200`, không phải lỗi), kết hợp `resource.attributes.service.name` để
giới hạn đúng service bị báo cáo, và `attributes.http.route` để biết chính xác endpoint nào.

**Bước làm**:
1. Kibana → Discover, Data View **Traces**
2. Query: `resource.attributes.service.name : "Products.Api"`
3. Bấm **Sort fields** hoặc thêm cột `duration` rồi bấm vào tiêu đề cột để sắp xếp giảm dần

**Bạn sẽ thấy**: span có `duration` cao nhất nổi lên đầu bảng — mở nó, đọc `attributes.http.route` và
`name` để biết chính xác endpoint nào.

**Tự kiểm tra**: so `duration` của span cao nhất với vài span cùng route khác — nếu chênh lệch không
lớn (vd tất cả đều quanh vài chục ms), có thể "chậm" chỉ là 1 lần bất thường (outlier), không phải xu
hướng — bước tiếp theo hợp lý là chuyển sang Tình huống 3 (metrics theo thời gian) để xác nhận đây có
phải xu hướng thật hay không.

### Tình huống 2 — "Có log lỗi Error, điều tra nguyên nhân gốc"

**Bối cảnh**: đúng dữ liệu **thật** đang có trong hệ thống của bạn — không phải dựng kịch bản. Có 2
document log `severity_text : "Error"` mang `attributes.exception.type : "DbUpdateException"`, 1 ở
`Orders.Api` (route `/orders`), 1 ở `Baskets.Api` (route `/baskets/current/clear`) — nhiều khả năng
sinh ra từ chính lúc thực hành tắt `baskets-db` ở file 03.

**Field cần dùng và vì sao**: `severity_text : "Error"` để lọc thô trước (không dùng `status_code` vì
đang ở Logs, không phải Traces), rồi `attributes.exception.type` để phân loại theo tên lỗi cụ thể thay
vì đọc từng dòng `body.text` bằng mắt, cuối cùng `attributes.exception.stacktrace` để đọc nguyên nhân
gốc thật (không dừng ở `exception.message` — message ở đây chỉ nói chung chung "an error occurred",
stack trace mới lộ ra `SqlException`/"Operation cancelled by user" là nguyên nhân sát thực tế nhất).

**Bước làm**:
1. Kibana → Discover, Data View **Logs**
2. Query: `severity_text : "Error" and attributes.exception.type : *`
3. Thêm cột `resource.attributes.service.name`, `attributes.exception.type`, `attributes.RequestPath`
4. Mở 1 document, đọc hết `attributes.exception.stacktrace`

**Bạn sẽ thấy**: nhiều loại `exception.type` khác nhau (không chỉ `DbUpdateException`) — hệ thống của
bạn hiện có 454 document mang field `exception.type`, không phải chỉ 2 cái vừa nêu.

**Tự kiểm tra**: lấy `attributes.TraceId` của 1 document lỗi bất kỳ, sang Data View **Traces**, tìm
`trace_id` trùng giá trị đó — bạn có thấy đúng span đã trả lỗi tương ứng (status code 5xx) không? Đây
chính là lúc dùng `TraceId` thay vì `CorrelationId` như bảng 3-loại-ID ở trên đã giải thích — vì
document lỗi này có thể không mang `CorrelationId` (không phải request nào cũng có).

### Tình huống 3 — "So sánh sức khoẻ giữa các service theo thời gian, không phải chỉ 1 con số"

**Bối cảnh**: trước khi báo cáo "hệ thống ổn định", bạn cần biết `dotnet.exceptions` của service nào
đang tích luỹ nhanh nhất — không phải đếm 1 lần, mà **so sánh giữa các service**.

**Field cần dùng và vì sao**: `metrics.dotnet.exceptions` (không dùng `dotnet.gc.pause.time` hay
`kestrel.active_connections` cho câu hỏi này — sai nhóm tín hiệu), kết hợp
`resource.attributes.service.name` để tách theo từng service, và bắt buộc chú ý `unit` (`{exception}`,
tức đếm dồn — con số chỉ tăng, không giảm, nên "cao" không tự động nghĩa là "đang tệ đi", phải nhìn tốc
độ tăng theo thời gian).

**Bước làm**:
1. Kibana → Discover, Data View **Metrics**
2. Field sidebar, tìm `metrics.dotnet.exceptions`, bấm vào field đó để xem "Top values" theo
   `resource.attributes.service.name` (áp dụng lại cách đã học ở file 01 Bước "Cách 1" khi xem
   `service.name`)
3. Bấm **Visualize** để có con số chính xác theo từng service (không phải ước lượng mẫu, theo đúng
   phân biệt Cách 1 vs Cách 2 đã học trước đó)

**Bạn sẽ thấy**: 1 biểu đồ so sánh `dotnet.exceptions` giữa 7 service.

**Tự kiểm tra**: đổi khoảng thời gian (time picker) từ "Last 15 minutes" sang "Last 24 hours", so sánh
thứ hạng service — nếu thứ hạng đổi nhiều, nghĩa là có 1 đợt lỗi tập trung gần đây ở 1 service cụ thể,
đáng điều tra tiếp bằng Tình huống 2; nếu thứ hạng gần như không đổi, nhiều khả năng đó là mức nền bình
thường của service đó.

### Tình huống 4 — "Đồng nghiệp gửi 1 dòng log Kestrel thô, không phải correlation-id — tìm tiếp thế nào"

**Bối cảnh**: đồng nghiệp copy nguyên 1 dòng log console vào ticket, chỉ có
`RequestId: 0HNOCS69UGMHG:00000001`, không có `X-Correlation-Id` nào cả (vì request đó gọi trực tiếp
bằng `curl` thô, không qua Postman collection — không có gì tự sinh correlation id cho nó). Bạn cần tìm
đủ các service liên quan tới đúng request này.

**Field cần dùng và vì sao** — đây chính là lúc phải chọn đúng, không phải chọn field quen tay:
`attributes.RequestId` **không dùng được** để tra cứu xuyên service, vì nó chỉ có ý nghĩa cục bộ trong
đúng 1 tiến trình sinh ra nó (bảng 3-loại-ID ở trên) — tìm theo field này ở Discover chỉ ra đúng 1
document duy nhất, không hơn. Phải **chuyển sang `attributes.TraceId`** lấy được từ chính document đó
(luôn có mặt, vì OTel tự sinh không phụ thuộc việc có `X-Correlation-Id` hay không) để tìm tiếp.

**Bước làm**:
1. Kibana → Discover, Data View **Logs**, query: `attributes.RequestId : "0HNOCS69UGMHG:00000001"`
   (hoặc 1 `RequestId` thật khác bạn tự lấy từ Tình huống 2)
2. **Bạn sẽ thấy**: đúng 1 document — thử đổi time range rộng ra tối đa, số lượng vẫn không đổi. Đây là
   bằng chứng `RequestId` không lan truyền sang service khác.
3. Mở document đó, copy giá trị `attributes.TraceId`
4. Đổi query: `attributes.TraceId : "<giá trị vừa copy>"` — vẫn ở Data View **Logs**

**Bạn sẽ thấy**: có thể xuất hiện thêm document khác (nếu request đó chạm nhiều service và từng service
đều có dòng log), nhiều hơn hẳn kết quả bước 2.

**Tự kiểm tra**: sang Data View **Traces**, query `trace_id : "<cùng giá trị đó>"` — đếm số span trả
về, so với số document log ở bước 4. **Đã tự kiểm chứng bằng ví dụ thật ở trên**: 2 con số này **không
có quan hệ hơn-kém cố định** — ví dụ thật cho ra 27 document log nhưng chỉ 13 span, tức là **nhiều hơn**,
ngược với suy nghĩ "trace luôn tạo tự động nên phải nhiều hơn log" mà file 01 dạy trước đó. Lý do: bài
học file 01 chỉ đúng ở mức *1 request có hay không có ít nhất 1 log* — còn 1 span duy nhất vẫn có thể
chứa **nhiều dòng log** nếu code xử lý bên trong nó gọi `ILogger` nhiều lần (mỗi dòng log 1 document
riêng). Đừng suy ra bất đẳng thức nào giữa 2 con số — chỉ cần biết cả 2 đều lọc đúng theo `TraceId`, và
việc con số chênh lệch bao nhiêu tự nó là 1 thông tin (request đó "nói" nhiều hay ít) chứ không phải
dấu hiệu lỗi.

## Bài tập tự làm

1. Viết 1 query `_count` đếm số span có `resource.attributes.service.name : "Baskets.Api"` **và**
   `attributes.http.response.status_code : 500` — dùng `bool`/`must` như Bước 4. So sánh kết quả với
   con số bạn đếm bằng tay ở bài tập 1 của file 01 (dùng Discover) — 2 cách phải cho cùng 1 số, vì đó
   là cùng 1 dữ liệu, chỉ khác cách hỏi.
2. Đổi Bước 5 sang index `logs-generic.otel-default*`, và đổi field `terms` thành `severity_text` thay
   vì `resource.attributes.service.name`. Chạy thử và tự đọc kết quả: hệ thống của bạn đang phát log ở
   những mức độ nghiêm trọng (`Information`, `Warning`, `Error`...) nào, và mức nào chiếm nhiều nhất?
