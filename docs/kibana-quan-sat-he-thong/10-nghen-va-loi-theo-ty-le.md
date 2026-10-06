# 10 — Nhóm 2: Nghẽn và lỗi theo tỷ lệ

*(Cần đã làm file [09](09-dich-ket-noi-sai.md): file này dùng lại cách đọc dashboard `Xử lý sự cố — 7 service`, truy vấn
xác nhận khôi phục 15 phút ở [file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md) và các giới hạn đã nêu ở file 09.)*

Nhóm này gồm hai loại có hình dạng khác hẳn nhau:
- **Loại B — cạn pool kết nối của gateway**: giới hạn số kết nối đồng thời từ gateway tới BFF bị đặt rất thấp. Về lý
  thuyết request phải xếp hàng rồi lỗi khi tải tăng. **Trên hệ thống hiện tại loại B không tạo ra triệu chứng đo được**
  (xem mục "Loại B" bên dưới) — đây là lý do nó được giữ trong danh mục như một bài học về "cấu hình sai nhưng chẳng sao".
- **Loại C — 5xx theo tỷ lệ**: một phần cố định request tới một service trả 500, độ trễ gần như không đổi và service vẫn
  báo khoẻ.

Đây là hướng dẫn của dạng (a) — lỗi **có kịch bản**. Luyện không biết trước: [file 17](17-goi-y-theo-trieu-chung.md).

## Bạn sẽ thấy

- Loại C: một service (ở đây `Products.Api`) có tỷ lệ 5xx khoảng 23–26% ổn định từng phút, p95 vẫn khoảng 10 ms, `/health/ready`
  vẫn 200, **không có dòng log lỗi nào**, và lỗi **không lan** sang service khác. Các span 500 đến từ một địa chỉ và một
  user agent khác với traffic thường.
- Loại B: không có gì khác thường — 0% 5xx, độ trễ như lúc bình thường.

## Điều kiện

- Giống file 09: `.env` có `CHAOS_ALLOW_FAULT_INJECTION=true`, tải nền `-Load` đang chạy, dashboard và rule đã import.
- Loại C chỉ gửi request chaos tới service đích **bằng cổng publish của host** (ví dụ `localhost:5088` cho `products-api`),
  nên cần đích có cổng publish theo `docker-compose.local.yml`.
- Bài đo burst của loại B cần token do `-Load` sinh ra: file `.incident-drill/load/environment-with-token.json`.

## Loại B — Cạn pool kết nối của gateway

**Đích hợp lệ**: chỉ `gateway-api`. Tham số: `ReverseProxy__Clusters__bff-cluster__HttpClient__MaxConnectionsPerServer=1`.

### Tiêm

```powershell
./scripts/incident-drill.ps1 -Inject -Type B -Target gateway-api
```

Đo ngày 2026-10-06: tiêm xong sau 27 giây (bắt đầu 10:20:36, `injected` lúc 10:21:03). Có thể kiểm cấu hình đã vào container:

```powershell
docker inspect ecomerce-local-gateway-api-1 --format '{{range .Config.Env}}{{println .}}{{end}}' | Select-String 'MaxConnections'
```

### Nơi nhìn và cách thử

Dashboard `Xử lý sự cố — 7 service`: **5xx theo phút theo service** và **Latency p95 theo phút theo service** của `Gateway.Api`.
Để ép tình huống nghẽn, bắn nhiều request cùng lúc qua gateway bằng `curl` song song (đã thử trên Windows 10+, `curl 8.0.1`):

```powershell
$token = ((Get-Content .incident-drill/load/environment-with-token.json -Raw | ConvertFrom-Json).values | Where-Object key -eq 'accessToken').value
$n = 100
$out = 1..3 | ForEach-Object { curl.exe -s -Z --parallel-max $n -o NUL -w "%{http_code} %{time_total}\n" -H "Authorization: Bearer $token" "http://localhost:5300/bff/products?n=[1-$n]" } | Where-Object { $_ }
$out | ForEach-Object { $_.Split(' ')[0] } | Group-Object | Select-Object Name, Count
$ms = $out | ForEach-Object { [double]($_.Split(' ')[1]) * 1000 } | Sort-Object
"p50={0:N0} p95={1:N0} max={2:N0} ms" -f $ms[[int]($ms.Count*0.5)], $ms[[int]($ms.Count*0.95)], $ms[-1]
```

### Số đo (ngày 2026-10-06, có tải nền)

| Điều kiện | 30 song song × 3 vòng | 100 song song × 3 vòng |
|---|---|---|
| Loại B đang tiêm (11:00) | 90 × `200`, p50 155, p95 272, max 297 ms | 300 × `200`, p50 607, p95 989, max 1085 ms |
| Đã khôi phục (11:08) | 90 × `200`, p50 167, p95 516, max 534 ms | 300 × `200`, p50 509, p95 889, max 990 ms |

Không có mã 5xx nào và hiệu số p95 đổi dấu giữa hai cột (272 so với 516; 989 so với 889), tức nằm trong nhiễu của máy. Dưới tải
nền bình thường, từng phút của `Gateway.Api` khi B đang tiêm vẫn `dat = true` (0% 5xx, p95 50–85 ms). Bắn request đồng thời qua
gateway **không** làm chúng xếp hàng, nên cấu hình `MaxConnectionsPerServer=1` không có tác dụng quan sát được ở đây.

### Khôi phục và xác nhận

```powershell
./scripts/incident-drill.ps1 -Restore -RunId <runId>
```

Tạo lại 7 container không kèm cấu hình sai; đo: 26–31 giây. Xác nhận bằng truy vấn 15 phút của file 08 cho `Gateway.Api` (chỉ xét
`err_pct`). Vì B không gây lỗi, bước xác nhận chủ yếu để chắc cấu hình đã gỡ (`docker inspect` không còn dòng `MaxConnections`).

## Loại C — Lỗi 5xx theo tỷ lệ

**Đích hợp lệ**: cả 7 service. Tham số: tỷ lệ request mang header chaos từ 5% tới 50%, script bốc ngẫu nhiên và in ra (lần đo: 14%).
Script gửi liên tục request có header `X-Chaos-Fault: 5xx` tới service đích; service trả 500 cho đúng các request đó.

### Tiêm

```powershell
./scripts/incident-drill.ps1 -Inject -Type C -Target products-api
```

Đo: bắt đầu 10:37:38, `injected` lúc 10:38:11. `injector.log` ghi tốc độ nền đo được lúc tiêm (2,98 span/giây) và tốc độ gửi chaos
(0,485 request/giây, cứ 2061 ms một request). Không đặt `-DurationSeconds` thì lỗi giữ tới khi `-Restore`.

### Nơi nhìn trên Kibana

1. **Bảng SLO**: `Products.Api` cột Error-rate vượt 1%; các service khác sạch.
2. **5xx theo phút theo service**: chỉ một đường lên, ổn định (không có đỉnh nhọn như lỗi khởi động).
3. **Latency p95**: không đổi (khoảng 10 ms) — khác với lỗi kết nối ở file 09 (p95 hàng giây).
4. **Phân bố status code theo service**: `500` chỉ ở `Products.Api`.
5. **Lỗi gọi hạ lưu**: không có cặp nào nhắm vào `Products.Api` tăng lỗi, vì request chaos không đi qua BFF.
6. **Log lỗi gần nhất**: **không có dòng nào** của `Products.Api` — 500 do cơ chế tiêm không ghi log lỗi. Thiếu log lỗi mà vẫn
   5xx là dấu hiệu của lỗi do đầu vào/tiêm, không phải do bug hay hạ tầng.
7. **Phát hiện nhanh**: `Products.Api` chuyển `active` lúc 10:39:59, khoảng 1 phút 48 giây sau khi tiêm.

### Truy vấn tự đối chiếu

**Tỷ lệ 5xx từng phút của service nghi vấn** (truy vấn "Xác nhận khôi phục 15 phút" của file 08, xem cột `err_pct`):

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 12 minutes AND resource.attributes.service.name == "Products.Api"
| EVAL is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0)
| STATS total = COUNT(*), bad_5xx = SUM(is_5xx) BY minute = BUCKET(@timestamp, 1 minute)
| EVAL err_pct = ROUND(TO_DOUBLE(bad_5xx) / total * 100.0, 2)
| SORT minute
```

**Request lỗi khác request thường ở điểm nào** — cùng route, tách theo mã, địa chỉ và user agent:

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 6 minutes AND resource.attributes.service.name == "Products.Api" AND kind == "Server" AND attributes.http.route == "/products"
| STATS n = COUNT(*) BY code = attributes.http.response.status_code, caller_host = attributes.server.address, ua = attributes.user_agent.original
| SORT n DESC
```

**Lỗi có lan sang service khác không**:

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 6 minutes AND kind == "Server" AND attributes.http.response.status_code >= 500
| STATS n = COUNT(*) BY service = resource.attributes.service.name
```

### Số đo (ngày 2026-10-06, có tải nền, `Products.Api`)

Sau khi cold start qua đi (từ 10:39), mỗi phút `Products.Api` có 107–121 span và 5xx ở mức **23,1–25,2%** (phút 10:39: 25,23;
10:40: 23,73; 10:41: 23,08; 10:42: 23,93; 10:43–10:45: 23,14–23,36), p95 7–19 ms. Trong 6 phút tới 10:46: 471 span `200` có
`attributes.server.address = products-api` và không có user agent; 163 span `500` có `server.address = localhost` và
`user_agent.original = ...WindowsPowerShell/5.1...` (tức do script gửi, 25,7%). Toàn bộ 163 lỗi 5xx của hệ thống trong 6 phút
nằm ở `Products.Api`; log lỗi của `Products.Api` trong cùng khoảng: 0 dòng.

Tỷ lệ đạt được (khoảng 24%) **cao hơn** tỷ lệ in ra (14%): script tính tốc độ gửi từ tốc độ nền đo lúc tiêm (2,98 span/giây khi đang khởi
động), nhưng tốc độ nền sau đó thấp hơn (khoảng 1,95 span/giây).

### Khôi phục và xác nhận

```powershell
./scripts/incident-drill.ps1 -Restore -RunId <runId>
```

Loại C chỉ cần ngừng gửi request chaos nên lệnh xong gần như tức thì (đo: 0,5 giây, 10:46:28). Phút 10:46 còn `err_pct = 12,84`
(nửa phút đầu), phút 10:47 `total = 51`, `err_pct = 0`, `dat = true`. Mốc giải quyết vẫn theo file 08: 15 phút liên tục
`dat = true` và không còn alert active. Bản đo này chưa chờ đủ 15 phút (file 09 đã đo trọn chuỗi 15 phút).

## Giới hạn đã biết

- **Loại B không có triệu chứng**: không 5xx và không tăng độ trễ trong cả tải nền lẫn bắn 100 request song song (QA_Debt mục
  031, 032). Đừng chờ thấy lỗi; hãy dùng `docker inspect` để thấy cấu hình, và coi đây là bài học "cấu hình bất thường nhưng vô hại".
- **Tỷ lệ chaos thực tế lệch tỷ lệ in ra** (24% so với 14%) do cách script ước lượng tốc độ nền.
- **Lỗi loại C chỉ nằm ở một service và không lan** vì request chaos gửi thẳng vào cổng của service đích, không đi qua gateway/BFF.
  Lỗi thật xuất hiện ở service sâu thường lan lên BFF/gateway (xem file 09).
- **Nhiễu cảnh báo sau khi tạo lại container**: lần đo có thêm alert `active` của `Parties.Api`, `Identity.Api` (từ 10:34:59) và `Bff.Api`,
  `Baskets.Api` (từ 10:39:59) do nhiễu khởi động nguội, không phải do loại C; chỉ `Products.Api` có lỗi thật. Nhìn nguyên nhân
  (log, span 5xx), đừng chỉ nhìn bảng cảnh báo.
- **Log lỗi bị `Identity.Api` lấp** và **dòng 100% giả `Bff.Api → Parties.Api`**: xem file 09.

## Bài tập tự làm

1. Tiêm loại C vào một service **khác** `Products.Api` (ví dụ `baskets-api`) và tự tìm ra, không xem lại mục trên:
   (a) tỷ lệ 5xx từng phút; (b) hai điểm khác nhau giữa span 500 và span 200 cùng route; (c) lỗi có lan sang service nào không.
   Dùng `-DurationSeconds 300` để script tự gỡ rồi xác nhận lỗi đã dừng. Thời lượng tính từ lúc tiêm xong (lần thử: `injected` 11:09:53,
   `restored` 11:14:54). Gợi ý đáp án để tự đối chiếu: route `/baskets/current`, span 500 có `server.address = localhost` và user agent
   PowerShell, span 200 có `server.address = baskets-api`; trong 3 phút thử có 95 span 500 và 81 span 200.
2. Với loại B, tự chạy đoạn bắn 100 request song song hai lần: một lần khi B đang tiêm, một lần khi đã gỡ, và viết kết luận một câu
   "B có tạo triệu chứng không?" kèm số p95 của cả hai lần.
3. Mở bảng "Phát hiện nhanh" trong lúc C đang chạy và ghi lại: service nào `active` do lỗi thật, service nào do nhiễu khởi động nguội,
   và dựa vào đâu bạn phân biệt.

## Đã đạt khi

- [ ] Chỉ ra đúng service bị lỗi và tỷ lệ 5xx (±3 điểm phần trăm so với truy vấn từng phút).
- [ ] Nêu được ít nhất hai điểm khác nhau giữa request lỗi và request thường (`server.address`, `user_agent.original`).
- [ ] Giải thích được vì sao không có log lỗi dù có 5xx và vì sao lỗi không lan.
- [ ] Kết luận đúng rằng loại B không tạo triệu chứng đo được, kèm số p95 hai lần đo.
- [ ] Khôi phục bằng `-Restore` (hoặc `-DurationSeconds`) và `err_pct` về 0 trong truy vấn từng phút.
- [ ] Truy vấn 15 phút ở file 08 cho service đó đạt 15 phút liền `dat = true`, không còn alert active.

## Xem thêm

- Quy trình triage, SEV và bản ghi sự cố:
  [`docs/dien-tap-chaos-engineering/README.md`](../dien-tap-chaos-engineering/README.md#diễn-tập-sự-cố-on-call-scrum-36) và
  [`mau-ban-ghi-su-co.md`](../dien-tap-chaos-engineering/mau-ban-ghi-su-co.md).
- Truy vấn xác nhận khôi phục 15 phút: [file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md#xác-nhận-khôi-phục-15-phút).
- Luyện không biết trước: [file 17](17-goi-y-theo-trieu-chung.md).
- Trước: [09 — Đích kết nối sai](09-dich-ket-noi-sai.md). Sau: [11 — Độ trễ Orders](11-do-tre-orders.md).
