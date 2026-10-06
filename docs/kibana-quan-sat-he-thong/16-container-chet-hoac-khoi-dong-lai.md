# 16 — Nhóm 8: Container chết hoặc khởi động lại

*(Cần đã làm file [09](09-dich-ket-noi-sai.md) và [15](15-mang-dut.md): file này dùng lại cách đọc dashboard `Xử lý sự cố — 7 service`, truy vấn xác nhận khôi phục 15 phút ở
[file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md), các giới hạn nền ở file 09 và cách phân biệt "service biến mất khỏi dữ liệu" ở file 15.)*

Nhóm này là **một container service bị buộc dừng đột ngột** (`docker kill`), như khi tiến trình bị OOM-kill hoặc node sập. Container **không tự sống lại** vì 7 service app trong `docker-compose.local.yml`
không có chính sách `restart:` (chỉ container `migrate` mới có `restart: on-failure:3`). Triệu chứng gần như trùng nhóm 7 (mạng đứt), điểm cần học là phân biệt bằng trạng thái container.

Nhóm này thay cho kịch bản "giết pod" trước đây của spec 025 (đã gỡ phần Kubernetes).

Đây là hướng dẫn của dạng (a) — lỗi **có kịch bản**. Luyện không biết trước: [file 17](17-goi-y-theo-trieu-chung.md).

## Bạn sẽ thấy

- Service bị kill **biến mất khỏi dữ liệu quan sát** như nhóm 7: không còn span, dòng của nó biến khỏi bảng SLO.
- Service gọi nó (ví dụ `Bff.Api`) báo timeout 1 giây (span Client không có mã HTTP, `TaskCanceledException`) rồi trả **504**; `Gateway.Api` cũng 504.
- `docker ps -a` cho thấy container `Exited (137)`, `OOMKilled=false`, `restartPolicy=no` — **khác nhóm 7** (ở đó container vẫn `Up` nhưng không có mạng).
- Cổng publish ra host (ví dụ `localhost:5041`) không còn kết nối được; không tự sống lại dù chờ lâu.

## Điều kiện

- Giống file 09: `.env` có `CHAOS_ALLOW_FAULT_INJECTION=true`, tải nền `-Load` đang chạy, dashboard và rule đã import.
- Khác nhóm 1: loại I dùng `docker kill` trên container đang chạy, **không** tạo lại container nào nên không có nhiễu khởi động nguội lúc tiêm; nhiễu khởi động nguội chỉ có sau khi **khôi phục** (tạo lại đúng service đó).

## Loại I — Tiến trình bị buộc dừng

**Đích hợp lệ**: cả 7 service app. Tham số: `{"action":"kill"}`. Khôi phục bằng tạo lại container đích từ compose (`up -d --force-recreate --no-deps <service>`) rồi chờ khoẻ.

### Tiêm

```powershell
./scripts/incident-drill.ps1 -Inject -Type I -Target orders-api
```

Đo ngày 2026-10-06: bắt đầu 12:32:01, `injected` lúc 12:32:04 (3 giây). Kiểm tra trạng thái container:

```powershell
docker ps -a --format '{{.Names}} {{.Status}}' | Select-String 'orders-api'       # ... Exited (137) ...
docker inspect ecomerce-local-orders-api-1 --format 'running={{.State.Running}} exit={{.State.ExitCode}} oom={{.State.OOMKilled}} restartPolicy={{.HostConfig.RestartPolicy.Name}}'
# running=false exit=137 oom=false restartPolicy=no
curl.exe -s -o NUL -w "%{http_code}`n" http://localhost:5041/health/live          # 000
```

### Nơi nhìn trên Kibana

1. **Bảng SLO — 7 service**: dòng `Orders.Api` biến mất sau vài phút (không có span).
2. **Traffic + 401/403 theo phút theo service**: đường Request/phút của `Orders.Api` về 0.
3. **5xx theo phút theo service**: `Bff.Api` và `Gateway.Api` có 504, `Orders.Api` không có đường nào.
4. **Lỗi gọi hạ lưu**: cặp `Bff.Api → Orders.Api` chỉ có span lỗi không mã HTTP (xem truy vấn).
5. **Log lỗi gần nhất** (lọc `Bff.Api`): `OnTimeout ... AttemptTimeout` và "Downstream call to OrdersApi failed with 504".
6. **Phát hiện nhanh**: giống nhóm 7 — chỉ service **có span** mới có hàng; `Orders.Api` không bao giờ hiện đỏ.

### Truy vấn tự đối chiếu

**Service nào có span trong 4 phút gần nhất** (service bị kill là service **thiếu** trong kết quả), cùng 5xx và p95:

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 4 minutes AND kind == "Server"
| EVAL is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0)
| STATS total = COUNT(*), bad = SUM(is_5xx), p95_ms = ROUND(PERCENTILE(duration, 95) / 1000000.0) BY service = resource.attributes.service.name
| EVAL err_pct = ROUND(TO_DOUBLE(bad) / total * 100.0, 1)
| SORT service
```

**Route lỗi và mã**:

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 4 minutes AND kind == "Server" AND attributes.http.response.status_code >= 500
| STATS n = COUNT(*) BY service = resource.attributes.service.name, route = attributes.http.route, code = attributes.http.response.status_code
| SORT n DESC
| LIMIT 6
```

**Span Client của service gọi** — không có mã HTTP là timeout/không kết nối:

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 4 minutes AND kind == "Client" AND resource.attributes.service.name == "Bff.Api" AND attributes.server.address == "orders-api"
| STATS n = COUNT(*), p50_ms = ROUND(PERCENTILE(duration, 50) / 1000000.0) BY st = status.code, code = attributes.http.response.status_code, err = attributes.error.type
| SORT n DESC
```

### Số đo (ngày 2026-10-06, có tải nền, kill `orders-api`)

Trong 4 phút tới 12:38 (tính từ ~6 phút sau khi kill): **chỉ 6 service có span**, `Orders.Api` không có dòng nào. `Bff.Api` 347 span, 101 5xx (29,1%), p95 1013 ms (`/bff/checkout` 504 ×100);
`Gateway.Api` 344 span, 100 5xx (29,1%), p95 1017 ms (504 ×99); các service còn lại 0% 5xx. Span Client `Bff.Api → orders-api`: 101 span `status.code = Error`, mã HTTP null, `TaskCanceledException`, p50 1001 ms.
Log `Bff.Api` (`docker logs`): 108 dòng `Sending HTTP request POST http://orders-api:8080/orders`. Trạng thái container: `Exited (137)`, `running=false`, `OOMKilled=false`, `restartPolicy=no`; `localhost:5041` → `000`.
Từng phút của `Orders.Api` trước khi kill: 12:28 và 12:29 `dat = true` (9–12 span, p95 6–9 ms); dòng cuối cùng là 12:31 (90 span); từ 12:32 không còn dòng.

### Khôi phục

```powershell
./scripts/incident-drill.ps1 -Restore -RunId <runId>
```

Tạo lại chỉ container đích và chờ khoẻ. Đo: 12:38:19 → 12:38:28 (9 giây); `orders-api` `Up 6 seconds (healthy)`.

### Xác nhận đã khỏi

Container mới có nhiễu khởi động nguội ngắn ở service đó. Mốc giải quyết theo file 08: 15 phút liên tục `dat = true` (**phút không có dòng làm đứt chuỗi**) và không còn alert active.
Bản đo này chưa chờ đủ 15 phút (file 09 đã đo trọn chuỗi 15 phút).

## Giới hạn đã biết

- **Service biến mất khỏi dữ liệu**: như nhóm 7 — bảng SLO và cảnh báo chỉ có hàng cho service có span; phát hiện qua phía gọi và qua `docker ps -a`.
- **Không che đích**: trong bài mù, `docker ps -a` lộ ngay container `Exited (137)` (QA_Debt mục 031).
- **Không tự sống lại**: 7 service app không có `restart:` nên container nằm `Exited` cho tới khi bạn khôi phục.
- **Triệu chứng gần như trùng nhóm 7** (timeout 1 giây ở phía gọi): phân biệt bằng `docker ps -a` (`Exited (137)` so với `Up ... (unhealthy)`) và `docker inspect` mạng.
- **OOM-kill là cách chết thật của nhóm 6**: nếu bộ nhớ giới hạn quá thấp (đo ở 96 MB), container bị OOM-kill giống nhóm này nhưng `OOMKilled=true`.
- **Cảnh báo không phân biệt được từng nhóm khi tiêm nối tiếp**: xem file 14.
- **Log lỗi bị `Identity.Api` lấp** và **dòng 100% giả `Bff.Api → Parties.Api`**: xem file 09.

## Bài tập tự làm

1. Tiêm loại I vào một service **khác** `orders-api` (ví dụ `parties-api`) với `-DurationSeconds 240` và tự trả lời không xem lại mục trên: (a) service nào biến mất và từ phút nào;
   (b) service gọi báo gì; (c) hai lệnh `docker` chứng minh container bị kill chứ không phải mạng đứt; (d) vì sao nó không tự sống lại.
   Đáp án tham khảo từ lần thử (`parties-api`, 12:38:36 → tự gỡ `restored` 12:42:43): trong 3 phút `Parties.Api` hầu như không còn span (8 span, p95 1918 ms ở đuôi),
   `Bff.Api` `/bff/parties/{partyId:guid}` 504 ×30, `Gateway.Api` 504 ×30, p95 khoảng 3005 ms; `docker ps -a` báo `Exited (137)` rồi `Up ... (healthy)` sau khôi phục.
2. Sau khi script tự khôi phục, ghi phút đầu tiên service có lại span và phút đầu tiên `dat = true`.
3. So triệu chứng với bài tập nhóm 7 (file 15): liệt kê ba điểm khác nhau.

## Đã đạt khi

- [ ] Chỉ ra đúng service bị kill và phút mất traffic.
- [ ] Phân biệt được nhóm 8 với nhóm 7 bằng `docker ps -a` / `docker inspect`.
- [ ] Giải thích được vì sao container không tự sống lại.
- [ ] Giải thích được vì sao bảng SLO không báo đỏ cho service bị kill.
- [ ] Khôi phục bằng `-Restore` (hoặc `-DurationSeconds`) và container `healthy`.
- [ ] Truy vấn 15 phút ở file 08 cho service đó đạt 15 phút liền `dat = true` (không còn phút trống), không còn alert active.

## Xem thêm

- Quy trình triage, SEV và bản ghi sự cố:
  [`docs/dien-tap-chaos-engineering/README.md`](../dien-tap-chaos-engineering/README.md#diễn-tập-sự-cố-on-call-scrum-36) và
  [`mau-ban-ghi-su-co.md`](../dien-tap-chaos-engineering/mau-ban-ghi-su-co.md).
- Truy vấn xác nhận khôi phục 15 phút: [file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md#xác-nhận-khôi-phục-15-phút).
- Luyện không biết trước: [file 17](17-goi-y-theo-trieu-chung.md).
- Trước: [15 — Mạng đứt](15-mang-dut.md). Sau: [17 — Gợi ý theo triệu chứng](17-goi-y-theo-trieu-chung.md).
