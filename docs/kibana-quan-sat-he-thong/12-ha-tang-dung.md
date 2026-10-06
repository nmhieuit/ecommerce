# 12 — Nhóm 4: Phụ thuộc hạ tầng dừng

*(Cần đã làm file [09](09-dich-ket-noi-sai.md): file này dùng lại cách đọc dashboard `Xử lý sự cố — 7 service`, truy vấn xác nhận
khôi phục 15 phút ở [file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md) và các giới hạn nền đã nêu ở file 09. Nên đọc thêm
[file 10](10-nghen-va-loi-theo-ty-le.md) để phân biệt với lỗi theo tỷ lệ.)*

Nhóm này là **một cơ sở dữ liệu dừng** trong khi service dùng nó vẫn chạy. Về triệu chứng, nó giống rất nhiều với nhóm 1 (đích kết nối
sai) — cả hai cho "không kết nối được tới SQL Server" — nên điểm cần học là **cách phân biệt hai nhóm** bằng trạng thái container, chứ
không bằng log.

Phạm vi: chỉ 5 cơ sở dữ liệu (`products-db`, `baskets-db`, `orders-db`, `parties-db`, `identity-db`), dừng bằng `docker stop`.
Redis và RabbitMQ **không** nằm trong nhóm này vì stack hiện tại không có service nào dùng Redis và RabbitMQ chỉ được `orders-api`
dùng khi biến `ORDERS_RABBITMQ_CONNECTION` được đặt (mặc định rỗng): dừng chúng không gây triệu chứng đo được.

Đây là hướng dẫn của dạng (a) — lỗi **có kịch bản**. Luyện không biết trước: [file 17](17-goi-y-theo-trieu-chung.md).

## Bạn sẽ thấy

- Service dùng DB đó (ví dụ `Orders.Api`) trả 5xx cho khoảng một nửa request, `/health/ready` trả 503, và sau vài phút container của service
  chuyển `unhealthy`.
- Lỗi **lan ngược lên** `Bff.Api` và `Gateway.Api` (502/500/504) như nhóm 1.
- `docker ps -a` cho thấy container DB ở trạng thái `Exited` — khác nhóm 1 (DB vẫn `Up`).
- Log báo `Name or service not known` (nhiều nhất) và `Connection refused` (ít hơn) — gần như trùng log của nhóm 1.

## Điều kiện

- Giống file 09: `.env` có `CHAOS_ALLOW_FAULT_INJECTION=true`, tải nền `-Load` đang chạy, dashboard và rule đã import.
- Khác nhóm 1: loại E **không tạo lại container nào**, nên không có nhiễu khởi động nguội và không che đích qua thời gian chạy của container.

## Loại E — Cơ sở dữ liệu dừng

**Đích hợp lệ**: 5 cơ sở dữ liệu nêu trên. Tham số: `{"action":"stop"}`.

### Tiêm

```powershell
./scripts/incident-drill.ps1 -Inject -Type E -Target orders-db
```

Đo ngày 2026-10-06: bắt đầu 11:28:07, `injected` lúc 11:28:13 (6 giây vì không tạo lại container). `docker ps -a` ngay sau đó: `orders-db`
`Exited (137)`, trong khi `orders-api` vẫn `Up ... (healthy)` (kiểm sức khoẻ chưa kịp thất bại).

### Nơi nhìn trên Kibana

1. **Bảng SLO — 7 service**: `Orders.Api` Error-rate khoảng 50%; `Bff.Api` và `Gateway.Api` cũng vượt 1%.
2. **5xx theo phút theo service**: ba đường lên cùng lúc, lên nhanh ngay từ phút đầu (không có đỉnh cold start).
3. **Lỗi gọi hạ lưu**: `Bff.Api → Orders.Api` 500 và `Gateway.Api → Bff.Api` 502/504, giống nhóm 1.
4. **Log lỗi gần nhất** (lọc `Orders.Api`): nội dung báo không kết nối được SQL Server.
5. **Phân biệt với nhóm 1** — dùng thông tin ngoài Kibana: `docker ps -a` cho thấy container DB `Exited`. Với nhóm 1, DB vẫn `Up` và chính
   service trỏ nhầm host.

### Truy vấn tự đối chiếu

**5xx và độ trễ theo service trong 4 phút gần nhất**, **route và mã lỗi**, **cặp gọi → đích**: dùng đúng ba truy vấn đầu của
[file 09](09-dich-ket-noi-sai.md#truy-vấn-tự-đối-chiếu), thay `Orders.Api` bằng service dùng DB bị dừng.

**p95 và tỷ lệ 5xx từng phút của service nghi vấn** (truy vấn "Xác nhận khôi phục 15 phút" của file 08):

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 12 minutes AND resource.attributes.service.name == "Orders.Api"
| EVAL is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0)
| STATS total = COUNT(*), bad_5xx = SUM(is_5xx), p95_ns = PERCENTILE(duration, 95), p99_ns = PERCENTILE(duration, 99) BY minute = BUCKET(@timestamp, 1 minute)
| EVAL err_pct = ROUND(TO_DOUBLE(bad_5xx) / total * 100.0, 2), p95_ms = ROUND(p95_ns / 1000000.0), p99_ms = ROUND(p99_ns / 1000000.0)
| KEEP minute, total, err_pct, p95_ms, p99_ms
| SORT minute
```

Trực tiếp từ container:

```powershell
docker ps -a --format '{{.Names}} {{.Status}}' | Select-String 'orders-'     # DB Exited? api unhealthy?
curl.exe -s http://localhost:5041/health/ready                                 # 503 + self-database Unhealthy
docker logs --since 4m ecomerce-local-orders-api-1 2>&1 | Select-String 'SocketException'
```

### Số đo (ngày 2026-10-06, có tải nền, dừng `orders-db`)

Nền trước khi tiêm: 11:26 và 11:27 `dat = true`, p95 22–64 ms, 0% 5xx. Từ 11:28 (từng phút của `Orders.Api`):

| Phút | 11:28 | 11:29 | 11:30 | 11:31 | 11:32 |
|---|---|---|---|---|---|
| Span | 99 | 108 | 109 | 105 | 100 |
| 5xx (%) | 41,41 | 50,93 | 51,38 | 49,52 | 53,00 |
| p95 (ms) | 1004 | 1003 | 1000 | 1000 | 15 |

Trong 4 phút tới 11:33: `Orders.Api` 211/409 5xx (51,6%), `Bff.Api` 356/1419 (25,1%), `Gateway.Api` 368/1146 (32,1%); `Baskets.Api`,
`Parties.Api`, `Identity.Api`, `Products.Api` 0%. Chi tiết: `Orders.Api` `/orders` 500 ×173 và `/health/ready` 503 ×37; `Bff.Api`
`/bff/checkout` 502 ×171 và 504 ×12; `Gateway.Api` 502 ×171 và 504 ×12. Log lỗi trong 4 phút: 567 dòng của `Orders.Api`, 202 dòng của
`Bff.Api`; trong log container của `orders-api`: 603 dòng `SocketException ... Name or service not known` và 16 dòng `Connection refused`.
Sau 5 phút `orders-api` chuyển `Up 17 minutes (unhealthy)` (health check đã thất bại liên tiếp 49 lần); `orders-db` `Exited (137)`.
p95 từng phút khoảng 1000 ms ở bốn phút đầu rồi 15 ms ở phút 11:32 (chưa xác định nguyên nhân thay đổi này).

Cảnh báo `incident-fast-detection`: `Orders.Api` và `Gateway.Api` đã `active` từ 11:20:32 (nhóm 3) và không `recovered` trước khi nhóm này
bắt đầu, nên **không đo được thời gian báo riêng** của loại E.

### Khôi phục

```powershell
./scripts/incident-drill.ps1 -Restore -RunId <runId>
```

Khởi động lại container DB và chờ khoẻ tối đa 10 phút. Lần đo: 11:33:28 → 11:34:16 (48 giây); `orders-db` `Up ... (healthy)`, `orders-api` trở
lại `healthy` mà **không bị khởi động lại** (không có `restart:`; health check tự hồi phục khi DB lên).

### Xác nhận đã khỏi

Phút 11:34 còn `err_pct = 28,97` (nửa phút đầu), phút 11:35 `total = 91`, `err_pct = 0`, p95 15 ms, `dat = true`. Mốc giải quyết theo file 08: 15 phút
liên tục `dat = true` và không còn alert active. Bản đo này chưa chờ đủ 15 phút (file 09 đã đo trọn chuỗi 15 phút).

## Giới hạn đã biết

- **E không che đích**: dừng container DB hiện ngay ở `docker ps -a`; trong bài mù (dạng b) người luyện có thể lộ đáp án chỉ bằng lệnh
  này (QA_Debt mục 031).
- **Log gần như trùng nhóm 1**: cả `Name or service not known` lẫn `A network-related or instance-specific error` đều xuất hiện ở hai nhóm.
  Phân biệt bằng `docker ps -a` và cấu hình, không bằng log.
- **Không đo được thời gian báo riêng** khi alert của nhóm trước còn active (xem trên); hãy chờ cảnh báo `recovered` rồi mới tiêm lần kế.
- **Log lỗi bị `Identity.Api` lấp** (chỉ sau khi tạo lại container) và **dòng 100% giả `Bff.Api → Parties.Api`**: xem file 09.

## Bài tập tự làm

1. Tiêm loại E vào một DB **khác** `orders-db` (ví dụ `products-db`) với `-DurationSeconds 240`, rồi tự trả lời không xem lại mục trên:
   (a) service nào lỗi và tỷ lệ 5xx; (b) lỗi có lan lên BFF/gateway không; (c) tại sao biết đây là DB dừng chứ không phải cấu hình sai
   (nhóm 1); (d) container nào `unhealthy` và sau bao lâu.
   Đáp án tham khảo từ lần thử (`products-db`, 11:36:58 → tự gỡ 11:41:59): `Products.Api` 5xx 87–100% từng phút (cao hơn `Orders.Api` vì mọi route
   của nó cần DB), `/health/ready` 503 ngay sau khi tiêm, `products-db` `Exited (137)`.
2. Quan sát `docker ps -a` và `/health/ready` của service ngay sau khi tiêm và 5 phút sau; ghi lại khác biệt.
3. Sau khi script tự khôi phục, dùng truy vấn từng phút để ghi phút đầu tiên `dat = true`.

## Đã đạt khi

- [ ] Chỉ ra đúng service lỗi và DB bị dừng; tỷ lệ 5xx của service đó khoảng 40–55%.
- [ ] Phân biệt được nhóm 4 với nhóm 1 bằng `docker ps -a` (DB `Exited`) và nêu đúng lý do log không đủ để phân biệt.
- [ ] Nêu được lỗi lan lên BFF/gateway và dòng nào của bảng "Lỗi gọi hạ lưu" là điểm bắt đầu.
- [ ] Khôi phục bằng `-Restore` (hoặc `-DurationSeconds`) và DB `healthy`, service `healthy` không cần khởi động lại.
- [ ] Truy vấn 15 phút ở file 08 cho service đó đạt 15 phút liền `dat = true`, không còn alert active.

## Xem thêm

- Quy trình triage, SEV và bản ghi sự cố:
  [`docs/dien-tap-chaos-engineering/README.md`](../dien-tap-chaos-engineering/README.md#diễn-tập-sự-cố-on-call-scrum-36) và
  [`mau-ban-ghi-su-co.md`](../dien-tap-chaos-engineering/mau-ban-ghi-su-co.md).
- Truy vấn xác nhận khôi phục 15 phút: [file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md#xác-nhận-khôi-phục-15-phút).
- Luyện không biết trước: [file 17](17-goi-y-theo-trieu-chung.md).
- Trước: [11 — Độ trễ Orders](11-do-tre-orders.md). Sau: [13 — Xác thực hỏng](13-xac-thuc-hong.md).
