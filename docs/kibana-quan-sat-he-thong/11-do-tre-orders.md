# 11 — Nhóm 3: Độ trễ (chỉ Orders.Api)

*(Cần đã làm file [09](09-dich-ket-noi-sai.md) và [10](10-nghen-va-loi-theo-ty-le.md): file này dùng lại cách đọc dashboard
`Xử lý sự cố — 7 service`, truy vấn xác nhận khôi phục 15 phút ở [file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md) và các giới hạn nền đã
nêu ở file 09.)*

Nhóm này làm **một phần request tới `Orders.Api` chậm đúng 2 giây** trong khi kết quả trả về vẫn đúng và không có 5xx. Đây là kiểu
sự cố "chậm chứ không hỏng": người dùng thấy treo, còn bảng lỗi sạch. Độ trễ được tiêm bằng header `X-Chaos-Latency-Ms` mà
`Orders.Api` tự đọc khi cờ độ trễ được bật.

Đây là hướng dẫn của dạng (a) — lỗi **có kịch bản**. Luyện không biết trước: [file 17](17-goi-y-theo-trieu-chung.md).

## Bạn sẽ thấy

- p95 và p99 của `Orders.Api` vọt lên khoảng 2 giây (p95 2009–2437 ms từng phút), trong khi tỷ lệ 5xx giữ nguyên 0%.
- `/health/ready` vẫn 200 và không có dòng log lỗi nào.
- Các span chậm đều trả **404** (đơn hàng giả không tồn tại) chứ không phải 5xx — "chậm" và "lỗi" là hai tín hiệu khác nhau.
- Trên dashboard, độ trễ **không lan** sang `Bff.Api` và `Gateway.Api` vì request chậm đi thẳng vào `Orders.Api`, không qua chúng.

## Điều kiện

- Giống file 09, **và** `.env` có thêm `CHAOS_ALLOW_LATENCY_INJECTION=true` (ngoài `CHAOS_ALLOW_FAULT_INJECTION=true`); thiếu cờ này script
  từ chối và nói rõ cờ nào thiếu. Cờ được nạp vào container lúc `-Inject` tạo lại `orders-api`.
- Tải nền `-Load` đã chạy ít nhất một lần để có file token `.incident-drill/load/environment-with-token.json` (loại D cần token này
  để gửi request).

## Loại D — Độ trễ theo tỷ lệ

**Đích hợp lệ**: chỉ `orders-api`. Tham số: độ trễ 2000 ms; tỷ lệ request mang header chaos từ 5% tới 50%, script bốc ngẫu nhiên và in
ra (lần đo: 47%).

### Tiêm

```powershell
./scripts/incident-drill.ps1 -Inject -Type D -Target orders-api
```

Đo ngày 2026-10-06: bắt đầu 11:15:21, `injected` lúc 11:15:53. `injector.log` ghi tốc độ nền lúc tiêm (1,36 span/giây) và tốc độ gửi
chaos (1,206 request/giây, cứ 829 ms một request). Script gửi **thẳng** `GET http://localhost:5041/orders/<guid>` kèm token, header
`X-Tenant-Id: contoso` và `X-Chaos-Latency-Ms: 2000`.

Thử bằng tay một request (cần `orders-api` đang chạy với cờ latency bật):

```powershell
$token = ((Get-Content .incident-drill/load/environment-with-token.json -Raw | ConvertFrom-Json).values | Where-Object key -eq 'accessToken').value
$id = [guid]::NewGuid()
curl.exe -s -o NUL -w "%{http_code} %{time_total}s`n" -H "Authorization: Bearer $token" -H "X-Tenant-Id: contoso" -H "X-Chaos-Latency-Ms: 2000" "http://localhost:5041/orders/$id"   # trực tiếp
curl.exe -s -o NUL -w "%{http_code} %{time_total}s`n" -H "Authorization: Bearer $token" -H "X-Tenant-Id: contoso" "http://localhost:5041/orders/$id"                             # không header
curl.exe -s -o NUL -w "%{http_code} %{time_total}s`n" -H "Authorization: Bearer $token" -H "X-Chaos-Latency-Ms: 2000" "http://localhost:5300/bff/orders/$id"                     # qua gateway
```

Kết quả đã đo: `404 2.03s` (trực tiếp), `404 0.013s` (không header), `404 0.040s` (qua gateway). **Header không đi xuyên
gateway/BFF tới `Orders.Api`**; vì vậy công cụ này không thể làm BFF timeout hay mở circuit breaker (QA_Debt mục 025).
Thiếu `X-Tenant-Id` thì `Orders.Api` trả 500 `MissingTenantContext` — lỗi giả, đừng nhầm với lỗi tiêm.

### Nơi nhìn trên Kibana

1. **Bảng SLO — 7 service**: dòng `Orders.Api` có cột Latency p95/p99 vượt ngưỡng (150 ms / 500 ms), Error-rate vẫn 0%.
2. **Latency p95 theo phút theo service**: chỉ đường của `Orders.Api` nhảy lên khoảng 2000 ms và nằm phẳng ở đó.
3. **5xx theo phút theo service**: không có đường nào thay đổi — đây là điểm phân biệt với file 09 và file 10 (loại C).
4. **Top endpoint chậm nhất**: `/orders/{orderId:guid}` đứng đầu (trung bình 1424 ms trong 10 phút tới 11:27, route đứng thứ hai chỉ 119 ms).
5. **Lỗi gọi hạ lưu**: không có cặp mới bất thường (chậm không qua BFF).
6. **Log lỗi gần nhất**: không có dòng nào của `Orders.Api`.
7. **Phát hiện nhanh**: `Orders.Api` chuyển `active` lúc 11:20:32, khoảng 4 phút 39 giây sau khi tiêm; `Gateway.Api` cũng `active` cùng lúc
   nhưng chỉ 2/641 span 5xx trong 5 phút (0,3%) — nhiễu khởi động nguội, không phải lỗi độ trễ.

### Truy vấn tự đối chiếu

**p95 và tỷ lệ 5xx từng phút của `Orders.Api`** (truy vấn "Xác nhận khôi phục 15 phút" của file 08; chú ý `p95_ms`):

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 12 minutes AND resource.attributes.service.name == "Orders.Api"
| EVAL is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0)
| STATS total = COUNT(*), bad_5xx = SUM(is_5xx), p95_ns = PERCENTILE(duration, 95), p99_ns = PERCENTILE(duration, 99) BY minute = BUCKET(@timestamp, 1 minute)
| EVAL err_pct = ROUND(TO_DOUBLE(bad_5xx) / total * 100.0, 2), p95_ms = ROUND(p95_ns / 1000000.0), p99_ms = ROUND(p99_ns / 1000000.0)
| KEEP minute, total, err_pct, p95_ms, p99_ms
| SORT minute
```

**Span chậm so với span thường, cùng service** — tách theo mã, route và địa chỉ gọi:

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 5 minutes AND resource.attributes.service.name == "Orders.Api" AND kind == "Server"
| EVAL slow = CASE(duration > 1500000000, "chậm >1,5 s", "bình thường")
| STATS n = COUNT(*), p50_ms = ROUND(PERCENTILE(duration, 50) / 1000000.0), max_ms = ROUND(MAX(duration) / 1000000.0) BY slow, code = attributes.http.response.status_code, route = attributes.http.route, caller_host = attributes.server.address
| SORT n DESC
| LIMIT 10
```

**Độ trễ có lan sang service khác không**:

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 5 minutes AND kind == "Server"
| EVAL is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0)
| STATS total = COUNT(*), bad = SUM(is_5xx), p95_ms = ROUND(PERCENTILE(duration, 95) / 1000000.0), p99_ms = ROUND(PERCENTILE(duration, 99) / 1000000.0) BY service = resource.attributes.service.name
| SORT service
```

### Số đo (ngày 2026-10-06, có tải nền, `Orders.Api`)

Nền trước khi tiêm (11:14): 90 span/phút, 0% 5xx, p95 34 ms, p99 66 ms. Từ khi tiêm xong (từng phút, 0% 5xx cả chín phút):

| Phút | 11:15 | 11:16 | 11:17 | 11:18 | 11:19 | 11:20 | 11:21 | 11:22 | 11:23 |
|---|---|---|---|---|---|---|---|---|---|
| Span | 49 | 129 | 152 | 155 | 158 | 161 | 143 | 126 | 96 |
| p95 (ms) | 9388 | 2039 | 2019 | 2015 | 2013 | 2009 | 2077 | 2194 | 2437 |
| p99 (ms) | 10727 | 3938 | 2047 | 2041 | 2022 | 2022 | 2726 | 2355 | 2670 |

Phút 11:15 là khởi động nguội cộng với lần tiêm đầu. Trong 5 phút tới 11:24: 311 span `404` ở `/orders/{orderId:guid}` có `server.address
= localhost`, p50 2010 ms, max 4004 ms (đây là các request chaos); 98 span `201` ở `/orders` (p50 23 ms) và 98 span `200` ở
`/orders/{orderId:guid}` (p50 4 ms) có `server.address = orders-api` (tải nền). Tính chung 5 phút: `Orders.Api` p95 2172 ms, p99 2453 ms, 0 5xx; `Bff.Api` p95
378 ms; `Gateway.Api` p95 416 ms; các service còn lại p95 79–282 ms. Log lỗi `Orders.Api` 5 phút: 0 dòng; `/health/ready` 200.

Tỷ lệ chậm thực tế khoảng 56% (311/554 span) so với 47% script in ra, do cách ước lượng tốc độ nền (như loại C ở file 10).

### Khôi phục và xác nhận

```powershell
./scripts/incident-drill.ps1 -Restore -RunId <runId>
```

Loại D chỉ cần ngừng gửi header nên lệnh xong sau khoảng 1,5 giây (11:24:39 → 11:24:41). Từng phút sau đó: 11:25 p95 190 ms (đuôi các
request đang bay), 11:26 `total = 17`, p95 42 ms, p99 58 ms, `dat = true`. Mốc giải quyết theo file 08: 15 phút liên tục `dat = true` và không còn alert
active. Bản đo này chưa chờ đủ 15 phút (file 09 đã đo trọn chuỗi 15 phút).

## Giới hạn đã biết

- **Header không đi qua gateway/BFF** nên chỉ `Orders.Api` bị chậm; BFF không timeout, circuit breaker không trip (QA_Debt mục 025, 031).
  Nếu bạn tiêm qua `:5300/bff/orders/...` sẽ không thấy gì (404 sau 40 ms).
- **Span chậm là 404, không phải đơn thật**: tiêm trên id giả để tránh làm hỏng dữ liệu; đừng nhầm với lỗi 404 thật.
- **Tỷ lệ chậm thực tế lệch tỷ lệ in ra** (56% so với 47%).
- **Nhiễu cảnh báo**: `Gateway.Api` `active` cùng `Orders.Api` do khởi động nguội; đối chiếu bằng truy vấn, đừng tin riêng bảng cảnh báo.
- **Log lỗi bị `Identity.Api` lấp** và **dòng 100% giả `Bff.Api → Parties.Api`**: xem file 09.

## Bài tập tự làm

1. Tiêm loại D bằng `-DurationSeconds 300`. Trong lúc chạy, tự trả lời không xem lại mục trên: (a) service nào chậm và chậm bao nhiêu (p95 từng
   phút); (b) tỷ lệ span chậm so với tổng; (c) vì sao 5xx giữ 0%; (d) vì sao `Bff.Api` không chậm theo.
2. Gửi tay ba request như mục "Tiêm" (trực tiếp, không header, qua gateway), ghi lại ba thời gian và giải thích sự khác nhau.
3. Viết lại truy vấn "Span chậm so với span thường" để tách theo `attributes.user_agent.original` và tìm dấu hiệu khác ngoài `server.address`
   giữa request chaos và tải nền.

## Đã đạt khi

- [ ] Chỉ ra đúng service chậm và p95 từng phút khoảng 2 giây, 0% 5xx.
- [ ] Giải thích được vì sao không có 5xx và không có log lỗi dù người dùng thấy chậm.
- [ ] Phân biệt được request chaos (`server.address = localhost`) với tải nền (`server.address = orders-api`).
- [ ] Nêu đúng vì sao độ trễ không lan sang BFF/gateway (header không được chuyển tiếp).
- [ ] Khôi phục bằng `-Restore` (hoặc `-DurationSeconds`) và p95 về dưới 150 ms.
- [ ] Truy vấn 15 phút ở file 08 cho `Orders.Api` đạt 15 phút liền `dat = true`, không còn alert active.

## Xem thêm

- Quy trình triage, SEV và bản ghi sự cố:
  [`docs/dien-tap-chaos-engineering/README.md`](../dien-tap-chaos-engineering/README.md#diễn-tập-sự-cố-on-call-scrum-36) và
  [`mau-ban-ghi-su-co.md`](../dien-tap-chaos-engineering/mau-ban-ghi-su-co.md).
- Truy vấn xác nhận khôi phục 15 phút: [file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md#xác-nhận-khôi-phục-15-phút).
- Luyện không biết trước: [file 17](17-goi-y-theo-trieu-chung.md).
- Trước: [10 — Nghẽn và lỗi theo tỷ lệ](10-nghen-va-loi-theo-ty-le.md). Sau: [12 — Hạ tầng dừng](12-ha-tang-dung.md).
