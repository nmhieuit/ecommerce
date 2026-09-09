# 01 — Làm quen Kibana & Discover

*(Đọc [00-tong-quan-lo-trinh.md](00-tong-quan-lo-trinh.md) trước nếu chưa chuẩn bị stack + import
Postman.)*

Mục tiêu file này: mở được Kibana, tự tạo "Data View" (thứ Kibana cần để biết đọc data stream nào), và
dùng **Discover** — màn hình xem dữ liệu thô — để tự lọc ra đúng thứ bạn cần tìm, không cần biết
Elasticsearch là gì.

## Bước 1 — Mở Kibana

```
http://localhost:5601
```

Không cần đăng nhập. Nếu trang không load, kiểm tra container đang chạy:

```bash
docker compose -f docker-compose.local.yml ps kibana
```

**Bạn sẽ thấy**: trạng thái `Up (healthy)`.

## Bước 2 — Tạo Data View cho traces

Kibana không tự biết `traces-generic.otel-default` là gì cho tới khi bạn khai báo. Tại thời điểm viết
tài liệu này, Kibana của bạn **chưa có Data View nào** — đây là bước bắt buộc, không phải tuỳ chọn.

1. Menu ☰ góc trên trái → **Stack Management** (dưới mục Management)
2. **Data Views** → **Create data view**
3. **Name**: gõ `Traces` (tên hiển thị, tuỳ bạn đặt)
4. **Index pattern**: gõ `traces-generic.otel-default*` — Kibana sẽ hiện số document khớp ngay bên dưới
5. **Timestamp field**: chọn `@timestamp` (Kibana tự đề xuất, thường đã chọn sẵn)
6. **Save data view to Kibana**

**Bạn sẽ thấy**: thông báo tạo thành công, và số lượng document khớp pattern hiển thị khác 0 (chứng tỏ
đã có dữ liệu trace thật sẵn trong hệ thống, không cần tạo thêm gì).

## Bước 3 — Mở Discover, đọc 1 document thật

1. Menu ☰ → **Discover**
2. Chọn Data View **Traces** vừa tạo ở góc trên trái màn hình Discover
3. Chỉnh khoảng thời gian ở góc trên phải thành **Last 24 hours** (mặc định 15 phút có thể bỏ sót dữ
   liệu cũ)

**Bạn sẽ thấy**: 1 bảng document, mỗi dòng là 1 span (1 lời gọi HTTP tại 1 service). Bấm mũi tên `>`
đầu dòng bất kỳ để xem toàn bộ field của nó — bạn sẽ thấy các field thật sau (không phải field ví dụ
Elastic hay dùng để minh hoạ):

| Field | Ý nghĩa | Ví dụ thật |
|---|---|---|
| `resource.attributes.service.name` | Service nào phát ra span này | `Gateway.Api` |
| `name` | Tên thao tác | `GET /health/ready` |
| `attributes.http.route` | Route đã match | `/health/ready` |
| `attributes.http.response.status_code` | Mã HTTP trả về | `200` |
| `attributes.correlation.id` | Correlation ID lan truyền xuyên service | 1 chuỗi hex 32 ký tự |
| `duration` | Thời gian xử lý, đơn vị **nanosecond** | vd `1482800` = ~1.48 ms |

## Bước 4 — Thêm cột để đọc nhanh hơn thay vì mở từng document

Bảng mặc định chỉ có 1 cột `@timestamp` + tài liệu rút gọn, khó lướt nhanh. Với mỗi field muốn thêm
làm cột: rê chuột qua tên field ở panel bên trái, bấm dấu `+` xuất hiện.

Thêm lần lượt 4 field:
- `resource.attributes.service.name`
- `name`
- `attributes.http.response.status_code`
- `attributes.correlation.id`

**Bạn sẽ thấy**: bảng giờ có 5 cột (kèm timestamp), đọc được ngay service nào, gọi gì, trả mã nào mà
không cần mở từng dòng.

## Bước 5 — Lọc bằng KQL (Kibana Query Language)

Thanh tìm kiếm phía trên bảng nhận cú pháp KQL. Gõ lần lượt và Enter sau mỗi lần:

```
resource.attributes.service.name : "Gateway.Api"
```

**Bạn sẽ thấy**: bảng chỉ còn span của `Gateway.Api`.

Lọc theo mã lỗi — đổi câu query thành:

```
attributes.http.response.status_code >= 400
```

**Bạn sẽ thấy**: chỉ còn các request lỗi (4xx/5xx). Hệ thống của bạn hiện có sẵn cả 401 lẫn 403 thật từ
việc bạn đã thử Postman trước đó — đủ dữ liệu để lọc ra ngay, không cần tự tạo lỗi mới ở bước này (file
04 sẽ khai thác đúng các dòng 401/403 này kỹ hơn).

Kết hợp 2 điều kiện bằng `and`:

```
resource.attributes.service.name : "Gateway.Api" and attributes.http.response.status_code >= 400
```

## Bước 6 — Tạo thêm Data View cho logs (bạn tự làm, áp dụng lại Bước 2)

Lặp lại đúng Bước 2, nhưng:
- Name: `Logs`
- Index pattern: `logs-generic.otel-default*` (chú ý có `.otel` — xem giải thích ở file 00)
- Timestamp field: `@timestamp`

Mở Discover với Data View `Logs`, bạn sẽ thấy các field khác hẳn traces: `severity_text`,
`attributes.TenantId`, `attributes.CorrelationId`, `body.text` (nội dung log đã render sẵn) — đây là
những field bạn cần cho file 03.

## Bài tập tự làm

1. Trong Data View **Traces**, lọc ra tất cả span có `resource.attributes.service.name : "Baskets.Api"`
   **và** `attributes.http.response.status_code : 500`. Có bao nhiêu dòng? (Không có gợi ý đáp án — tự
   kiểm tra bằng cách đếm dòng Discover hiển thị ở góc dưới trái bảng.)
2. Chuyển sang Data View **Logs**, mở 1 document bất kỳ, copy giá trị `attributes.CorrelationId` của
   nó. Chuyển ngược lại Data View **Traces**, dán giá trị đó vào query dạng
   `attributes.correlation.id : "<giá trị vừa copy>"`. **Bạn sẽ thấy** ít nhất 1 (thường vài) span khớp
   — chiều này gần như luôn khớp. Giờ thử chiều ngược lại: mở 1 document bất kỳ trong **Traces** (không
   chọn theo status code cụ thể), copy `attributes.correlation.id`, tìm trong **Logs**. Nhiều khả năng
   bạn sẽ **không thấy dòng log nào** — không phải lỗi thao tác. Trace được hệ thống tự tạo cho MỌI
   request (auto-instrumentation của ASP.NET Core chạy trước khi code ứng dụng làm gì cả), còn log chỉ
   xuất hiện khi có dòng code nào đó thực sự gọi `ILogger.LogXxx(...)` trong lúc xử lý request đó — nên
   log luôn có trace đi kèm, nhưng phần lớn trace không có log đi kèm. File 03 sẽ dùng đúng tính chất
   này để giải thích cách chọn nguồn tra cứu cho đúng.
