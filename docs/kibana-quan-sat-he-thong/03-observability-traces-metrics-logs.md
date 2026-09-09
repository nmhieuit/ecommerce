# 03 — Observability: nối traces, metrics, logs lại với nhau

*(Cần đã làm file [01](01-lam-quen-kibana-discover.md) và [02](02-elasticsearch-rest-api.md).)*

File 01 dạy bạn đọc 1 data stream riêng lẻ. File này dạy điều thật sự quan trọng: **1 hành động của
người dùng (đặt 1 đơn hàng) chạm vào nhiều service, và correlation-id là sợi chỉ nối chúng lại** — đây
chính là lý do dự án này thêm OTel/Elastic (`docs/architecture/017_Architect_*.md`).

## Ngữ cảnh: vì sao cần nối lại

Khi bạn gọi `POST /bff/checkout`, BFF không tự xử lý — nó gọi tiếp xuống `Baskets.Api` (đọc giỏ hàng),
`Orders.Api` (tạo đơn), rồi `Baskets.Api` lần nữa (dọn giỏ). 1 request của người dùng = nhiều span nằm
rải rác ở nhiều service khác nhau trong Elasticsearch. Nếu không có 1 giá trị chung để lọc, bạn sẽ
không thể biết 4-5 span nào thuộc về cùng 1 lần checkout, giữa hàng nghìn span của những người dùng
khác (hoặc chính bạn, ở những lần thử trước).

`shared/ServiceDefaults/CorrelationIdMiddleware.cs` giải quyết đúng việc đó: mọi request được gắn 1
`X-Correlation-Id` — dùng lại giá trị client gửi lên nếu có, tự sinh nếu không — rồi middleware ghi
**cùng 1 giá trị đó** vào 2 nơi trong cùng 1 lần xử lý:
- Tag `correlation.id` trên span hiện tại (dòng `Activity.Current?.SetTag(...)`) → xuất hiện trong
  `traces-generic.otel-default`
- Scope log `CorrelationId` (dòng `_logger.BeginScope(...)`) → xuất hiện trong mọi dòng log
  `logs-generic.otel-default` được ghi trong lúc xử lý request đó

Giá trị này cũng được ghi ngược lại vào **response header** `X-Correlation-Id` — nghĩa là sau khi gọi
xong 1 request, bạn tra được đúng id của chính request đó ngay trong tab response, không cần đoán.

## Bước 1 — Đặt 1 đơn hàng thật, lấy đúng correlation-id của lần checkout đó

Trong Postman (collection v2, environment **Ecommerce - Local**), mở folder **"00 - Smoke Flow"**, chạy
lần lượt từ request `00` tới `06 Đặt hàng` (đủ để có 1 giỏ hàng và đặt hàng thành công) — hoặc bấm
**Run** cả folder bằng Collection Runner.

Sau khi request **`06 Đặt hàng`** trả về, mở tab **Headers** của **response** (không phải request).

**Bạn sẽ thấy** header `X-Correlation-Id: <1 chuỗi thật>` — copy giá trị này, đây là correlation-id
thật của đúng lần checkout bạn vừa làm.

> **Vì sao không dùng correlation-id của các request khác trong Smoke Flow**: script sinh
> `X-Correlation-Id` chạy lại cho **mỗi request** trong collection (upsert theo `{{$guid}}`), nên request
> `01 Xem danh sách sản phẩm` và request `06 Đặt hàng` có 2 correlation-id khác nhau. Chỉ correlation-id
> của chính request checkout mới lan truyền xuống Baskets/Orders — vì đó là request duy nhất khiến BFF
> gọi tiếp 2 service kia trong cùng 1 lần xử lý.

## Bước 2 — Tìm đủ các hop trong Kibana → Observability → Traces

1. Menu ☰ → **Observability** → **Traces**
2. Nếu Kibana hỏi chọn Data View/index, dùng `traces-generic.otel-default*`
3. Lọc theo `attributes.correlation.id : "<giá trị bạn vừa copy>"`

**Bạn sẽ thấy** nhiều span, thuộc về nhiều `resource.attributes.service.name` khác nhau — theo đúng
`specs/017-otel-servicedefaults-elastic/quickstart.md` Scenario 2 đã verify thật, các hop tham gia
checkout là `Bff.Api` (điều phối) cùng `Baskets.Api` và `Orders.Api`. Không hop nào bị đứt đoạn — nếu
bạn thấy thiếu 1 service nào, đó là dấu hiệu đáng điều tra, không phải bình thường.

Nếu bạn thích xem trong Discover thay vì app Traces (cách này tương đương, chỉ khác giao diện):
- Discover, Data View **Traces**, cùng query trên
- Thêm cột `resource.attributes.service.name`, `name`, `duration` để so sánh service nào xử lý lâu hơn

## Bước 3 — Tìm log tương ứng cùng correlation-id đó

Chuyển sang Data View **Logs**, query:

```
attributes.CorrelationId : "<giá trị bạn vừa copy>"
```

**Bạn sẽ thấy** ít nhất 1 dòng log — field `body.text` chứa nội dung đã render sẵn (dễ đọc hơn
`attributes.{OriginalFormat}`, vốn là chuỗi mẫu chưa điền giá trị). Field `attributes.TenantId` trên
dòng log này cũng cho bạn biết tenant nào đã thực hiện request — đúng dữ liệu file 04 cần.

## Bước 4 — Metrics: quan sát theo service thay vì theo 1 request

Metrics không gắn với 1 request cụ thể như traces/logs — nó là số liệu tích luỹ theo thời gian
(counter, gauge...) của từng service. Query đã verify thật ở `specs/017-otel-servicedefaults-elastic/
quickstart.md` Scenario 3 (bạn đã chạy dạng REST ở file 02 Bước 5); giờ xem qua Kibana:

1. Tạo thêm 1 Data View tên `Metrics`, pattern `metrics-generic.otel-default*`, timestamp `@timestamp`
   (áp dụng lại file 01 Bước 2)
2. Discover trên Data View `Metrics`, lọc `resource.attributes.service.name : "Orders.Api"`

**Bạn sẽ thấy** các document metric của riêng `Orders.Api` — mở 1 cái, field `metrics.<tên metric>`
chứa giá trị số thật (vd `metrics.dotnet.monitor.lock_contentions`) — đây là chỉ số runtime .NET, tự
động phát ra bởi `AddRuntimeInstrumentation()`, không phải nghiệp vụ.

## Bước 5 — Nối 2 tài liệu lại: tái hiện 1 lỗi thật rồi quan sát nó trong Kibana

`docs/local-testing.md` Scenario 1 dạy cách tắt database riêng của 1 service để tạo lỗi thật bằng tay.
Giờ làm lại đúng kịch bản đó, nhưng quan sát qua Kibana thay vì chỉ đọc response HTTP:

```bash
docker compose -f docker-compose.local.yml stop baskets-db
curl -o /dev/null -s -w '%{http_code}\n' http://localhost:5188/health/ready   # nên thấy 503
```

Sau đó trong Kibana, Data View **Traces**, lọc:

```
resource.attributes.service.name : "Baskets.Api" and attributes.http.response.status_code : 503
```

**Bạn sẽ thấy** span 503 mới nhất xuất hiện gần như ngay lập tức (vài giây, tuỳ chu kỳ export của
`otel-collector`). Đây là điều "quan sát được" nghĩa là gì trên thực tế: bạn không cần đọc log thô hay
gọi lại API — chỉ cần biết đúng field để lọc.

Nhớ khởi động lại database trước khi rời khỏi bài tập:

```bash
docker compose -f docker-compose.local.yml start baskets-db
```

## Bài tập tự làm

1. Đặt 1 đơn hàng khác (Smoke Flow lần 2), lấy correlation-id mới của riêng bạn, tự tìm đủ các hop
   trong Traces — không copy lại ví dụ ở Bước 2, dùng đúng giá trị bạn vừa tạo.
2. Trong khi `baskets-db` vẫn đang dừng (lặp lại Bước 5), thử gọi `06 Đặt hàng` trong Postman xem có gì
   xảy ra, rồi tự tìm trace của đúng lần gọi lỗi này trong Kibana bằng correlation-id lấy từ response
   header — kể cả khi response là lỗi, header `X-Correlation-Id` vẫn được middleware ghi lại như đã giải
   thích ở đầu bài. Đừng quên bật lại `baskets-db` sau khi xong.
