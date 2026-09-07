# Thử các test case bằng tay (Trying the test cases by hand)

*(Bản dịch tiếng Việt của [`local-testing.md`](local-testing.md) — bản gốc tiếng Anh vẫn được giữ
nguyên, không sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn đúng.)*

`docker-compose.local.yml` khởi động toàn bộ nền tảng với **mọi port được publish** và **1 SQL Server
cho mỗi service**, để các kiểu lỗi (failure mode) mà bộ test tự động khẳng định có thể được tái hiện
bằng cách dừng 1 container và gửi 1 request.

Mọi lệnh và mọi response trên trang này đều đã được chạy trên stack mô tả ở đây. Khi hành vi quan sát
được khác với điều bạn có thể mong đợi chỉ từ việc đọc test, điều đó được nêu rõ ra thay vì lấp liếm.

Id test case tham chiếu tới [`test-cases-2026-08-21.xlsx`](test-cases-2026-08-21.xlsx).

---

## Khởi động và dừng

```bash
./scripts/local-up.ps1          # hoặc ./scripts/local-up.sh
./scripts/local-down.ps1        # dừng, giữ nguyên dữ liệu
./scripts/local-down.ps1 -DiscardData   # dừng và xoá bỏ database
```

> **Stack này không thể chạy cùng lúc với stack mặc định.** Nó dùng lại đúng các port đó, nên
> `ecomerce-stack` (`docker-compose.yml`) và `ecomerce` (`docker-compose.deps.yml`) sẽ đụng độ với
> nó trên 4173, 5300 và 14330–14333. Chạy `./scripts/down.ps1` trước — `local-up` kiểm tra điều này
> và từ chối chạy kèm thông báo nêu tên stack kia, thay vì để Compose tự fail vì trùng port.

### Các port

| Thành phần | URL / địa chỉ | Ghi chú |
|---|---|---|
| Storefront | http://localhost:4173 | SPA, gọi tới gateway |
| Gateway | http://localhost:5300 | địa chỉ duy nhất storefront dùng |
| BFF | http://localhost:5301 | `/openapi/v1.json` được publish (Development) |
| Products | http://localhost:5088 | |
| Baskets | http://localhost:5188 | |
| Orders | http://localhost:5041 | |
| Parties | http://localhost:5204 | |
| parties-db | `localhost,14330` | `sa` / giá trị của `MSSQL_SA_PASSWORD` trong `.env` |
| products-db | `localhost,14331` | |
| baskets-db | `localhost,14332` | |
| orders-db | `localhost,14333` | |
| Redis | `localhost:6379` | chưa có gì kết nối tới nó |
| RabbitMQ | `localhost:5672` | chưa có gì kết nối tới nó |
| RabbitMQ UI | http://localhost:15672 | `guest` / `guest` |
| OTel collector | `localhost:4317` (gRPC), `localhost:4318` (HTTP) | trace đi vào log riêng của nó |
| Elasticsearch | http://localhost:9200 | không auth (017-otel-servicedefaults-elastic) |
| Kibana | http://localhost:5601 | không auth — app Observability đọc trace/metric/log mà collector chuyển tiếp |

Mọi service ở đây đều chạy dưới `ASPNETCORE_ENVIRONMENT=Development`, đây là điều publish tài liệu
OpenAPI của BFF. `docker-compose.yml` chạy chúng dưới Production.

---

## Kịch bản 1 — 1 service mất database của chính nó, và chỉ của chính nó

**Bao phủ:** TC-BSK-029, TC-BSK-030 · và cặp tương tự cho các service khác: TC-ORD-024/025,
TC-PTY-005/006, TC-PRD-008/009.

Đây là kịch bản mà cả file này tồn tại vì nó. Với SQL Server dùng chung của `docker-compose.yml`,
dừng database làm sập cả 4 service cùng lúc và "nó không fallback sang database của service khác" là
điều không quan sát được — không còn database nào khác đang chạy để fallback tới.

```bash
# trước khi dừng
curl -o /dev/null -s -w '%{http_code}\n' http://localhost:5188/health/ready   # 200
curl -o /dev/null -s -w '%{http_code}\n' http://localhost:5088/health/ready   # 200

docker compose -f docker-compose.local.yml stop baskets-db
```

Quan sát được:

```
baskets  :5188 -> 503 trong 4.10s
products :5088 -> 200 trong 0.03s
orders   :5041 -> 200 trong 0.01s
parties  :5204 -> 200 trong 0.01s
```

```bash
curl -s http://localhost:5188/health/ready
```

```json
{"status":"Unhealthy","checks":[{"name":"self-database","status":"Unhealthy",
"description":"A network-related or instance-specific error occurred while establishing a
connection to SQL Server. ..."}]}
```

`self-database` chính là tên check mà TC-BSK-030 khẳng định. Việc 3 service anh em vẫn trả lời 200
xuyên suốt là nửa còn lại của test đó: Baskets có connection string cho database của chính nó và
không gì khác, nên không có gì để nó fallback tới.

**Vì sao là 4 giây chứ không phải 15.** Connection string trong `docker-compose.local.yml` mang
`Connect Timeout=3;ConnectRetryCount=0`, sao chép từ `appsettings.Development.json`. Nếu không có
chúng, driver sẽ retry theo mặc định 15 giây và lỗi 503 tới rất lâu sau khi bạn đã ngừng quan sát.

**Container vẫn ở trạng thái Up.** `docker compose ps` vẫn hiện `baskets-api` đang chạy — nó đang trả
lời, với 503. Nó không có policy `restart:` chính vì lý do này; 1 container đang restart thì không trả
lời gì cả. Trạng thái health của Docker bị trễ: healthcheck cần 20 lần fail liên tiếp mỗi 5 giây trước
khi container được đánh dấu `unhealthy`, nên hãy tin vào response HTTP, không phải cột `ps`.

### Khôi phục lại

**Bao phủ:** TC-BSK-028 (và TC-ORD-023, TC-PTY-004, TC-PRD-007).

```bash
docker compose -f docker-compose.local.yml start baskets-db
curl -o /dev/null -s -w '%{http_code}\n' http://localhost:5188/health/ready   # 200
```

**Thử lại 1 lần nếu lời gọi đầu tiên vẫn fail.** Quan sát được: 1 request gửi đi đúng thời điểm
`baskets-db` báo khoẻ mạnh trả về 503 trong 0.02 giây — quá nhanh để đã thử kết nối, tức là 1
connection trong pool vẫn còn bị nhiễm độc (poisoned). Request ngay sau đó trả về 200. 1 lỗi 503 kéo
dài qua vài giây là 1 lỗi thật; 1 lỗi tức thời duy nhất lúc phục hồi là do pool đang tự làm sạch.

---

## Kịch bản 2 — 1 request chưa từng qua gateway thì không resolve được tenant nào

**Bao phủ:** TC-PRD-011 · và test tương tự cho các service khác: TC-BSK-032, TC-ORD-027, TC-PTY-008.

Chỉ có thể làm được ở đây vì mọi service đều có port được publish. Với `docker-compose.yml` không có
địa chỉ nào để gọi trực tiếp 1 service.

```bash
curl -o /dev/null -s -w '%{http_code}\n' http://localhost:5088/products
#   -> 500

curl -o /dev/null -s -w '%{http_code}\n' -H 'X-Tenant-Id: contoso' http://localhost:5088/products
#   -> 200
```

500 là kết quả được chấp nhận cho Giai đoạn 1: service fail rõ ràng thay vì trả về 200 với catalog của
1 tenant mặc định nào đó. Với header, catalog đã seed trả về (TC-PRD-002, TC-PRD-004):

```json
[{"id":"9f8d6b1e-0001-4000-8000-000000000002","name":"Ceramic Pour-Over Set","price":48.00},
 {"id":"9f8d6b1e-0001-4000-8000-000000000001","name":"Field Notes Notebook","price":12.50},
 {"id":"9f8d6b1e-0001-4000-8000-000000000003","name":"Linen Apron","price":34.25}]
```

### 1 lượt ghi không có tenant không để lại gì cả

**Bao phủ:** TC-ORD-028.

Mã trạng thái chứng minh người gọi đã bị từ chối; số dòng chứng minh không có gì được ghi, và đó là 2
khẳng định khác nhau.

```bash
# đếm trước, POST không có X-Tenant-Id, đếm sau
curl -o /dev/null -s -w '%{http_code}\n' -X POST -H 'Content-Type: application/json' \
  -d '{"items":[{"productId":"9f8d6b1e-0001-4000-8000-000000000001","quantity":1,"unitPrice":12.50}]}' \
  http://localhost:5041/orders
```

Quan sát được: `HTTP 500`, số lượng đơn hàng `3` trước và `3` sau.

Đếm dòng cần 1 client tới `localhost,14333` — bất kỳ SQL client nào cũng dùng được, hoặc:

```bash
docker run --rm mcr.microsoft.com/mssql/server:2022-latest \
  /opt/mssql-tools18/bin/sqlcmd -S host.docker.internal,14333 -U sa -P "$MSSQL_SA_PASSWORD" \
  -C -d orders -Q "SELECT COUNT(*) FROM Orders;"
```

> Trong Git Bash, thêm tiền tố `MSYS_NO_PATHCONV=1` nếu không `/opt/...` sẽ bị viết lại thành 1
> đường dẫn Windows và container báo "No such file or directory".

---

## Kịch bản 3 — 1 service downstream đã mất, gateway vẫn trả lời

**Bao phủ:** TC-GTW-021, TC-CMN-025, TC-CMN-027, TC-CMN-028.

```bash
docker compose -f docker-compose.local.yml stop products-api

curl -o /dev/null -s -w '%{http_code}\n' http://localhost:5300/health/live   # 200
curl -s -w '\nHTTP %{http_code} trong %{time_total}s\n' http://localhost:5300/bff/products
```

Quan sát được:

```json
{"type":"https://ecommerce.internal/errors/downstream-timeout",
 "title":"Downstream service timed out","status":504,
 "detail":"The 'ProductsApi' service did not respond within its timeout budget.",
 "correlationId":"e5c44fc033bf4457a84cb5af134ba4e7",
 "traceId":"00-2badd9f2ab2d0819b3bdb0dc3a2c2a77-cd99d245066c5a14-01"}
HTTP 504 trong 3.01s
```

3 điều cần đọc ra từ đó, mỗi điều là 1 khẳng định trong bộ test:

- Liveness của chính gateway vẫn ở 200 trong khi 1 service phía sau nó đã sập (TC-GTW-021). Readiness
  của nó cố tình để trống để 1 sự cố downstream không thể kéo gateway ra khỏi vòng quay — nhiệm vụ
  của nó là vẫn trả lời, với 1 lỗi rõ ràng.
- Nội dung theo chuẩn RFC 7807 với 1 `correlationId` (TC-CMN-027), và nó nêu tên service ở mức
  **logic** — `ProductsApi` — không có host, scheme, hay stack trace (TC-CMN-028).
- 3.01 giây là ngân sách cho mỗi downstream của BFF, nằm gọn trong giới hạn 5 giây mà SC-003 yêu cầu.

**504 ở đây, 502 trong test.** TC-CMN-025 kỳ vọng 502 vì nó tiêm 1 lỗi transport, fail ngay lập tức.
`docker compose stop` gỡ container khỏi mạng của Docker, nên connection bị treo và timeout của BFF
kích hoạt trước — 504. Cả 2 đều là câu trả lời đúng cho "dependency không trả lời", đây là lý do vì
sao hàm `ADownstreamFailure_IsBoundedAndStructured_AgainstARealUnreachableHost` của chính bộ test
chấp nhận cả 2. Điều đang được kiểm tra là lỗi có giới hạn, có cấu trúc, và nêu tên dependency.

```bash
docker compose -f docker-compose.local.yml start products-api
```

---

## Kịch bản 4 — cả lượt mua hàng, từ đầu tới cuối

**Bao phủ:** TC-CMN-019, TC-CMN-020, TC-CMN-021, TC-CMN-022, TC-ORD-013, TC-CMN-013.

Làm điều này trên trình duyệt tại **http://localhost:4173** — thêm 2 quyển Field Notes Notebook và 1
Linen Apron, rồi checkout. Cùng luồng đó qua gateway bằng curl, đây là những gì đã chạy để tạo ra
output dưới đây:

```bash
N=9f8d6b1e-0001-4000-8000-000000000001   # Field Notes Notebook, $12.50
A=9f8d6b1e-0001-4000-8000-000000000003   # Linen Apron, $34.25

curl -s -X POST -H 'Content-Type: application/json' -d "{\"productId\":\"$N\",\"quantity\":1}" \
  http://localhost:5300/bff/basket/items > /dev/null
curl -s -X POST -H 'Content-Type: application/json' -d "{\"productId\":\"$N\",\"quantity\":1}" \
  http://localhost:5300/bff/basket/items > /dev/null
curl -s -X POST -H 'Content-Type: application/json' -d "{\"productId\":\"$A\",\"quantity\":1}" \
  http://localhost:5300/bff/basket/items > /dev/null

curl -s http://localhost:5300/bff/basket
```

2 quyển notebook gộp thành 1 dòng, và con số bài hướng dẫn trích dẫn:

```json
{"id":"af715f57-f14a-4e04-9c3c-5e2a4ed22cbb","customerRef":"phase1-stub-user","items":[
 {"productId":"9f8d...0001","name":"Field Notes Notebook","quantity":2,"unitPrice":12.50,"lineTotal":25.00},
 {"productId":"9f8d...0003","name":"Linen Apron","quantity":1,"unitPrice":34.25,"lineTotal":34.25}],
 "total":59.25}
```

```bash
curl -s -X POST http://localhost:5300/bff/checkout
#  {"id":"75f5868c-171e-44c9-aa8b-3cdc1bc6be62","placedAtUtc":"...","total":59.25}

curl -s http://localhost:5300/bff/orders/75f5868c-171e-44c9-aa8b-3cdc1bc6be62
#  cùng id, cùng total 59.25            → TC-CMN-020

curl -s http://localhost:5300/bff/basket
#  items [], total 0, và CÙNG basket id như trước → TC-CMN-021

curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:5300/bff/checkout
#  409                                   → TC-CMN-022
```

### Đơn hàng ghi lại tenant của nó

**Bao phủ:** TC-ORD-013.

Hình dạng đơn hàng của BFF không mang tenant, nên đọc đơn hàng trực tiếp từ service orders — service
mà stack này publish trên 5041:

```bash
curl -s -H 'X-Tenant-Id: contoso' http://localhost:5041/orders/75f5868c-171e-44c9-aa8b-3cdc1bc6be62
```

```json
{"id":"75f5868c-...","placedAtUtc":"2026-08-20T14:01:46.66","total":59.25,"tenantId":"contoso"}
```

### 1 mức giá client tự chọn bị loại bỏ

**Bao phủ:** TC-CMN-013.

```bash
curl -s -X POST -H 'Content-Type: application/json' \
  -d "{\"productId\":\"$N\",\"quantity\":1,\"unitPrice\":0.01}" \
  http://localhost:5300/bff/basket/items
```

Quan sát được: dòng hàng trả về với `unitPrice 12.5`, total `12.5`. BFF resolve giá từ catalog và bỏ
qua bất kỳ điều gì người gọi nói về tiền.

---

## Kịch bản 5 — các hành vi ở edge

**Bao phủ:** TC-GTW-036, TC-GTW-026.

```bash
curl -o /dev/null -s -w '%{http_code}\n' http://localhost:5300/no-such-path
#   -> 404, ngay lập tức, không có cluster hay destination nào được nêu tên trong nội dung

curl -s -D - -o /dev/null -X OPTIONS \
  -H 'Origin: http://localhost:4173' -H 'Access-Control-Request-Method: GET' \
  http://localhost:5300/bff/products
```

```
HTTP/1.1 204 No Content
Access-Control-Allow-Credentials: true
Access-Control-Allow-Methods: GET
Access-Control-Allow-Origin: http://localhost:4173
```

1 origin mà không ai cấu hình sẽ không nhận được `Access-Control-Allow-Origin` nào cả — thử với
`-H 'Origin: http://evil.example'`.

---

## Reset giữa các lượt chạy

Giỏ hàng và đơn hàng tồn tại trong 4 volume database, nên 1 kịch bản giả định 1 giỏ hàng rỗng sẽ không
hành xử như đã viết ở lượt chạy thứ 2. Hoặc checkout để làm rỗng nó, hoặc bắt đầu lại từ đầu:

```bash
./scripts/local-down.ps1 -DiscardData    # ./scripts/local-down.sh --discard-data
./scripts/local-up.ps1
```

Catalog được seed bởi các migration, nên 1 lần khởi động mới luôn có lại đủ 3 sản phẩm.
