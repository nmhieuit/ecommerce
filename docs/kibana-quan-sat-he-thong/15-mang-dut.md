# 15 — Nhóm 7: Mạng đứt

*(Cần đã làm file [09](09-dich-ket-noi-sai.md): file này dùng lại cách đọc dashboard `Xử lý sự cố — 7 service`, truy vấn xác nhận khôi phục 15 phút ở
[file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md) và các giới hạn nền đã nêu ở file 09. Nên đọc thêm [file 12](12-ha-tang-dung.md): mạng đứt cho triệu chứng
"không kết nối được" giống hạ tầng dừng, nhưng container vẫn chạy.)*

Nhóm này là **một service bị tách khỏi mạng chung** (`backbone`) trong khi container của nó vẫn chạy. Mọi service khác không gọi tới được nó, và chính nó cũng không gọi
ra ngoài được — kể cả cơ sở dữ liệu của nó. Đây là kiểu sự cố khi cấu hình mạng bị đổi hay một node mất kết nối: tiến trình vẫn sống nhưng "biến mất" khỏi hệ thống.

Đây là hướng dẫn của dạng (a) — lỗi **có kịch bản**. Luyện không biết trước: [file 17](17-goi-y-theo-trieu-chung.md).

## Bạn sẽ thấy

- Service bị tách **biến mất khỏi dữ liệu quan sát**: không còn span nào, nên dòng của nó biến khỏi bảng SLO thay vì hiện đỏ (giới hạn đã nêu ở ô "Ngưỡng SLO" của dashboard).
- Service gọi nó (ở đây `Bff.Api`) báo **timeout** — span Client không có mã HTTP, báo `TaskCanceledException` sau 1 giây, rồi trả **504** cho người dùng; `Gateway.Api` cũng 504.
- Container bị tách vẫn `Up` lúc đầu, nhưng sau vài phút chuyển `unhealthy` vì kiểm tra sức khoẻ của chính nó trả 503 (không tới được DB).
- Cổng publish ra host (ví dụ `localhost:5088`) không còn kết nối được.

## Điều kiện

- Giống file 09: `.env` có `CHAOS_ALLOW_FAULT_INJECTION=true`, tải nền `-Load` đang chạy, dashboard và rule đã import.
- Khác nhóm 1: loại H dùng `docker network disconnect` trên container **đang chạy**, **không** tạo lại container nên không có nhiễu khởi động nguội khi tiêm.

## Loại H — Tách khỏi mạng chung

**Đích hợp lệ**: cả 7 service. Tham số: `{"action":"detach-network"}`. Khôi phục bằng nối lại mạng `backbone` kèm đúng bí danh gốc (tên service) rồi chờ khoẻ.

### Tiêm

```powershell
./scripts/incident-drill.ps1 -Inject -Type H -Target products-api
```

Đo ngày 2026-10-06: bắt đầu 12:17:23, `injected` lúc 12:17:25 (2 giây). Kiểm tra mạng của container:

```powershell
docker inspect ecomerce-local-products-api-1 --format 'networks={{json .NetworkSettings.Networks}} status={{.State.Health.Status}}'
# networks={} status=healthy      (ngay sau khi tiêm; vài phút sau status chuyển unhealthy)
curl.exe -s -o NUL -w "%{http_code}`n" http://localhost:5088/health/ready    # 000: không kết nối được
```

### Nơi nhìn trên Kibana

1. **Bảng SLO — 7 service**: dòng `Products.Api` **biến mất** sau vài phút (không có span nào) — đây không phải "khoẻ", mà là mất dữ liệu.
2. **Traffic + 401/403 theo phút theo service**: đường Request/phút của `Products.Api` về 0 trong khi các service khác vẫn có traffic.
3. **5xx theo phút theo service**: `Bff.Api` và `Gateway.Api` có 504, `Products.Api` không có đường nào.
4. **Lỗi gọi hạ lưu**: cặp `Bff.Api → Products.Api` chỉ có span lỗi không mã HTTP (xem truy vấn dưới); `Gateway.Api → Bff.Api` 504.
5. **Log lỗi gần nhất** (lọc `Bff.Api`): `Resilience event ... OnTimeout ... AttemptTimeout` và `Downstream call to ProductsApi failed with 504`.
6. **Phát hiện nhanh**: bảng này chỉ có hàng cho service **có span**; `Bff.Api` và `Gateway.Api` có thể `active`, nhưng `Products.Api` thì không (đã mất dữ liệu).

### Truy vấn tự đối chiếu

**Service nào có span trong 4 phút gần nhất** — service bị tách là service **thiếu** trong kết quả (đếm lại bằng 7 tên thật):

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 4 minutes AND kind == "Server"
| EVAL is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0)
| STATS total = COUNT(*), bad = SUM(is_5xx), p95_ms = ROUND(PERCENTILE(duration, 95) / 1000000.0) BY service = resource.attributes.service.name
| EVAL err_pct = ROUND(TO_DOUBLE(bad) / total * 100.0, 1)
| SORT service
```

**Ai gọi ai và gọi hỏng thế nào** — span Client không có mã HTTP là timeout/không kết nối:

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 4 minutes AND kind == "Client" AND resource.attributes.service.name == "Bff.Api" AND attributes.server.address == "products-api"
| STATS n = COUNT(*), p50_ms = ROUND(PERCENTILE(duration, 50) / 1000000.0), p95_ms = ROUND(PERCENTILE(duration, 95) / 1000000.0) BY st = status.code, code = attributes.http.response.status_code, err = attributes.error.type
| SORT n DESC
```

**5xx từng phút của service gọi** (truy vấn "Xác nhận khôi phục 15 phút" của file 08 cho `Bff.Api`, ngưỡng 300000000 / 800000000 ns). Với service bị tách thì chạy truy vấn đó và **tìm phút không có dòng**.

Trực tiếp từ Docker:

```powershell
docker ps --format '{{.Names}} {{.Status}}' | Select-String 'products-api'          # lúc đầu healthy, sau ~5 phút unhealthy
docker inspect ecomerce-local-products-api-1 --format '{{json .NetworkSettings.Networks}}'    # {}  (không có mạng)
docker logs --since 4m ecomerce-local-bff-api-1 2>&1 | Select-String 'products-api'
```

### Số đo (ngày 2026-10-06, có tải nền, tách `products-api`)

Trong 4 phút tới 12:22: **chỉ 6 service có span**; `Products.Api` không có dòng nào. `Bff.Api` 211 span, 55 5xx (26,1%), p95 3006 ms (`/bff/products` 504 ×28, `/bff/basket/items` 504 ×27);
`Gateway.Api` 210 span, 54 5xx (25,7%), p95 3009 ms (504 ×54); các service còn lại 0%. Span Client `Bff.Api → products-api`: 165 span `status.code = Error`, **không có mã HTTP**,
`TaskCanceledException`, p50 1000 ms và p95 1003 ms (khớp `AttemptTimeout` 1 giây trong log). Log `Bff.Api`: 218 dòng Error; trong `docker logs` 175 dòng `Sending HTTP request GET http://products-api:8080/products`.
Từng phút của `Bff.Api`: 12:20 5xx 11,2%, p95 3004 ms; 12:21 12,61%, p95 3003 ms; 12:22 3,91%.
Từng phút của `Products.Api`: 12:12–12:16 `dat = true` (p95 9–12 ms); 12:17 chỉ còn 31 span, p99 233641 ms (một span dài bất thường); **12:18–12:21 không có dòng nào** (không traffic).
Container: `Up 10 minutes (unhealthy)`; kiểm tra sức khoẻ cuối trả `503`. `curl http://localhost:5088/health/ready` → `000`.

### Khôi phục

```powershell
./scripts/incident-drill.ps1 -Restore -RunId <runId>
```

Nối lại mạng `backbone` kèm bí danh gốc (`products-api`) rồi chờ khoẻ. Đo: 12:22:36 → 12:23:24 (48 giây); `docker inspect` thấy `ecomerce-local_backbone`, `aliases=[products-api ...]`, `healthy`, `/health/ready` 200.

### Xác nhận đã khỏi

Phút 12:22 `Products.Api` 34 span, 17,65% 5xx (health check 503 lúc vừa nối lại); 12:23 94 span, 4,26%; từ phút 12:24 `dat = true` (89 span, p95 9 ms). `Bff.Api` đạt `dat = true` từ 12:23 (436 span, p95 37 ms). Mốc giải quyết
theo file 08: 15 phút liên tục `dat = true` và không còn alert active; **phút không có dòng làm đứt chuỗi**, nên chuỗi chỉ bắt đầu sau phút có lại traffic. Bản đo này chưa chờ đủ 15 phút (file 09 đã đo trọn chuỗi 15 phút).

## Giới hạn đã biết

- **Service biến mất khỏi dữ liệu**: bảng SLO và cảnh báo chỉ có dòng cho service có span, nên service bị tách không bao giờ hiện đỏ; phải nhìn phía gọi (`Bff.Api` timeout) và tìm service **thiếu** (dashboard nói rõ giới hạn này).
- **Triệu chứng giống hạ tầng dừng (file 12) nhưng khác chỗ nhìn**: mạng đứt không tạo log "name not known" ở phía bị tách; phía gọi chỉ thấy timeout không có mã HTTP. `docker inspect` mạng `{}` và `docker ps` không báo `Exited` là dấu hiệu phân biệt.
- **H không che đích**: trong bài mù, `docker ps`/`docker inspect` lộ ngay container bị tách (QA_Debt mục 031).
- **Cảnh báo không phân biệt được từng nhóm khi tiêm nối tiếp** (alert nhóm trước còn `active`): xem file 14.
- **Log lỗi bị `Identity.Api` lấp** và **dòng 100% giả `Bff.Api → Parties.Api`**: xem file 09.

## Bài tập tự làm

1. Tiêm loại H vào một service **khác** `products-api` (ví dụ `baskets-api`) với `-DurationSeconds 300` và tự trả lời không xem lại mục trên: (a) service nào biến mất khỏi dữ liệu và từ phút nào;
   (b) service nào kêu timeout và mã trả cho người dùng là gì; (c) vì sao service bị tách không hiện đỏ ở bảng SLO; (d) hai lệnh `docker` nào chứng minh mạng bị tách.
   Đáp án tham khảo từ lần thử (`baskets-api`, 12:25:43 → `restored` 12:31:41): trong 3 phút tới 12:30 chỉ 6 service có span, `Baskets.Api` không có dòng nào; `Bff.Api` 54/144 5xx (504 ở
   `/bff/checkout` ×36 và `/bff/basket/items` ×18), `Gateway.Api` 504 ×54, p95 khoảng 3005 ms; `baskets-api` `Up 13 minutes (unhealthy)`.
2. Khi script khôi phục, ghi phút đầu tiên service quay lại có span và phút đầu tiên `dat = true`.
3. So triệu chứng với bài tập nhóm 4 (file 12): liệt kê ba điểm khác nhau.

## Đã đạt khi

- [ ] Chỉ ra đúng service bị tách và phút mất traffic.
- [ ] Nêu đúng service gọi báo timeout, span Client không có mã HTTP, và 504 ở gateway.
- [ ] Giải thích được vì sao bảng SLO không báo đỏ.
- [ ] Chứng minh mạng bị tách bằng `docker inspect` và phân biệt được với DB dừng.
- [ ] Khôi phục bằng `-Restore` (hoặc `-DurationSeconds`) và `docker inspect` thấy lại mạng `backbone` cùng bí danh gốc.
- [ ] Truy vấn 15 phút ở file 08 cho service đó đạt 15 phút liền `dat = true` (không còn phút trống), không còn alert active.

## Xem thêm

- Quy trình triage, SEV và bản ghi sự cố:
  [`docs/dien-tap-chaos-engineering/README.md`](../dien-tap-chaos-engineering/README.md#diễn-tập-sự-cố-on-call-scrum-36) và
  [`mau-ban-ghi-su-co.md`](../dien-tap-chaos-engineering/mau-ban-ghi-su-co.md).
- Truy vấn xác nhận khôi phục 15 phút: [file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md#xác-nhận-khôi-phục-15-phút).
- Luyện không biết trước: [file 17](17-goi-y-theo-trieu-chung.md).
- Trước: [14 — Thiếu tài nguyên](14-thieu-tai-nguyen.md). Sau: [16 — Container chết hoặc khởi động lại](16-container-chet-hoac-khoi-dong-lai.md).
