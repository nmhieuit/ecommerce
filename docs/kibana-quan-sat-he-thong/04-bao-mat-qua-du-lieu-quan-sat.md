# 04 — Bảo mật ứng dụng qua dữ liệu quan sát được

*(Cần đã làm trọn vẹn file [01](01-lam-quen-kibana-discover.md),
[02](02-elasticsearch-rest-api.md), [03](03-observability-traces-metrics-logs.md).)*

## Giới hạn thật của file này — đọc trước khi bắt đầu

Kibana ở đây chạy `xpack.security.enabled: false` (`docker-compose.local.yml`), không có Fleet, không
có Elastic Agent, không có dữ liệu endpoint/network. App **"Security"** đầy đủ của Kibana (SIEM,
detection rule, alert...) **không dùng được** với setup hiện tại và không xuất hiện trong file này.

Điều file này thật sự dạy: dùng đúng traces/logs bạn đã học ở file 01-03 để **soi các sự kiện liên quan
bảo mật đã có sẵn trong dữ liệu** — request bị từ chối xác thực (401), bị từ chối phân quyền (403), và
vi phạm cô lập tenant (500 khi thiếu định danh tenant). Đây là 3 loại sự kiện bảo mật thật sự tồn tại
trong hệ thống này, có bằng chứng thật kiểm chứng được ngay bây giờ.

## Bước 1 — Tạo 401 và 403 thật bằng Postman

Import đúng **collection v2** nếu chưa làm (`postman/ecommerce.postman_collection.v2.json` +
`postman/local.postman_environment.v2.json` — xem [00](00-tong-quan-lo-trinh.md)). Mở folder
**"00 - Xác thực & phân quyền (Get Token)"**, chạy lần lượt cả 4 request theo đúng thứ tự:

1. **`01 Lấy access token (đủ scope ecommerce-api)`** — `POST {{identityUrl}}/connect/token`, grant
   `password` trên client test `integration-test-ropc`. **Bạn sẽ thấy** `200`, response có
   `access_token`; test script tự lưu vào biến `accessToken`.
2. **`02 Không có token thì bị chặn (401)`** — `GET {{basketsUrl}}/baskets/current`, không gắn
   Authorization. **Bạn sẽ thấy** `401` — đây là 1 trong 44 request 401 thật đã/đang tích luỹ trong hệ
   thống của bạn.
3. **`03 Lấy access token KHÔNG có scope ecommerce-api`** — cùng client, cùng user, chỉ xin scope
   `openid profile` (không xin `ecommerce-api`). **Bạn sẽ thấy** `200` (lấy token vẫn thành công — token
   hợp lệ, chỉ là thiếu quyền), lưu vào biến `accessTokenNoScope`.
4. **`04 Có token nhưng thiếu scope thì bị chặn (403)`** — cùng `GET .../baskets/current`, lần này có
   Bearer `{{accessTokenNoScope}}` cùng header `X-Tenant-Id`/`X-Subject-Id`. **Bạn sẽ thấy** `403`, và
   thân lỗi có `error: "forbidden_scope"` — không phải 1 lỗi 403 rỗng mặc định.

## Bước 2 — Tìm đúng request 401 vừa tạo trong Kibana

Kibana → Discover → Data View **Traces**, query:

```
resource.attributes.service.name : "Baskets.Api" and attributes.http.response.status_code : 401
```

**Bạn sẽ thấy** span mới nhất trùng thời điểm bạn vừa bấm Send ở Bước 1.2. Mở nó, đọc
`attributes.correlation.id` — copy lại giá trị này.

## Bước 3 — Tìm đúng request 403 vừa tạo

Đổi query:

```
resource.attributes.service.name : "Baskets.Api" and attributes.http.response.status_code : 403
```

**Bạn sẽ thấy** span mới nhất của Bước 1.4. Trước khi bạn tự tạo 2 request này, hệ thống đã có sẵn 13
request 403 thật từ những lần thử trước — bạn có thể tự kiểm tra con số này bằng câu `_count` đã học ở
file 02 Bước 6, rồi so sánh trước/sau khi thêm request của mình.

**Điều đáng chú ý**: request 403 KHÔNG có log tương ứng dễ tìm bằng đúng cách bạn vừa tra 401 — tự thử
tra `attributes.CorrelationId` của span 403 này trong Data View `Logs`, áp dụng đúng bài học ở file 03
Bước 3: log chỉ tồn tại khi có dòng code thật sự gọi `ILogger`, không phải mọi request đều có.

## Bước 4 — Vi phạm cô lập tenant: gọi thẳng service không mang định danh tenant

`docs/local-testing.md` Scenario 2 đã mô tả và verify thật kịch bản này. Tự làm lại:

```bash
curl -o /dev/null -s -w '%{http_code}\n' http://localhost:5088/products
```

**Bạn sẽ thấy** `500` — đây là kết quả **được chấp nhận** cho Phase 1: service thà fail lộ liễu còn hơn
trả về catalog của 1 tenant mặc định nào đó. Trong Postman v2, request tương đương là
**Product → "Thiếu header tenant thì không phục vụ catalog"**.

Tìm nó trong Kibana:

```
resource.attributes.service.name : "Products.Api" and attributes.http.response.status_code : 500
```

**Vì sao đây tính là 1 sự kiện bảo mật, không chỉ là 1 lỗi kỹ thuật**: constitution Principle V coi
tenant là 1 ranh giới bảo mật — 1 request không xác định được tenant mà vẫn được phục vụ dữ liệu (dù là
catalog công khai) là rò rỉ tiềm ẩn. `500` ở đây là hệ thống đang **từ chối đúng cách**, không phải 1
lỗi cần fix.

## Bước 5 — Điều tra 1 request theo correlation-id, như đang xử lý ticket thật

Kịch bản: giả sử bạn nhận được báo cáo "có người gặp lỗi 403 lúc nãy, giúp tôi xem họ là ai và request
gì". Dùng đúng correlation-id bạn lấy ở Bước 3:

1. Data View **Traces**, query `attributes.correlation.id : "<giá trị>"` — xem `name`,
   `attributes.http.route`, `resource.attributes.service.name` để biết chính xác endpoint nào bị chặn.
2. Data View **Logs**, cùng correlation-id (đổi tên field thành `attributes.CorrelationId` theo đúng
   quy tắc đã học) — nếu có log, đọc `attributes.TenantId`/`attributes.SubjectId` để biết request đó
   thuộc tenant/user nào (nếu những field đó đã được resolve tới lúc log được ghi).

Đây là đúng quy trình bạn sẽ dùng thật khi vận hành: không đọc log thô từng dòng, mà lọc thẳng theo 1
định danh duy nhất nối mọi tín hiệu lại.

## Bài tập tự làm

1. Đổi Bước 1.3 sang xin 1 scope khác không tồn tại (vd `scope = openid profile khong-ton-tai`), chạy
   lại request 04 với token mới — response có còn `error: "forbidden_scope"` không, hay khác đi? Tự
   kiểm tra bằng cách đọc response body, không đoán trước.
2. Tự chọn 1 trong 44 request 401 có sẵn từ trước (không phải request bạn vừa tạo ở Bước 1), tra
   `attributes.correlation.id` của nó, rồi thử tìm xem request đó ban đầu được gọi tới **route** nào
   (field `attributes.http.route`) và từ **`resource.attributes.service.name`** nào — endpoint đó có
   phải endpoint bạn dự đoán hay không?
