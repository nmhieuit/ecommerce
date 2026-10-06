# 13 — Nhóm 5: Xác thực hỏng

*(Cần đã làm file [09](09-dich-ket-noi-sai.md) và [10](10-nghen-va-loi-theo-ty-le.md): file này dùng lại cách đọc dashboard
`Xử lý sự cố — 7 service`, truy vấn xác nhận khôi phục 15 phút ở [file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md) và các giới hạn nền đã
nêu ở file 09. Về bảo mật qua dữ liệu quan sát, xem thêm [file 04](04-bao-mat-qua-du-lieu-quan-sat.md).)*

Nhóm này là **một service trỏ sai địa chỉ máy chủ định danh** (`Identity__Authority`). Service vẫn chạy và báo khoẻ, nhưng không tải được
cấu hình OpenID (`/.well-known/openid-configuration`) nên không xác thực được token: request có token hợp lệ vẫn bị từ chối 401, và
mỗi request phải chờ hết các lần thử lại trước khi bị từ chối.

Phạm vi: **một trong 6 service dùng máy chủ định danh** (`gateway-api`, `bff-api`, `products-api`, `baskets-api`, `orders-api`,
`parties-api`). `identity-api` không là đích của nhóm này: biến thể "dừng `identity-api`" đã bỏ vì không có triệu chứng trên đường dữ liệu
trong 10 phút (QA_Debt mục 031).

Đây là hướng dẫn của dạng (a) — lỗi **có kịch bản**. Luyện không biết trước: [file 17](17-goi-y-theo-trieu-chung.md).

## Bạn sẽ thấy

- `Orders.Api` (đích) trả **401** cho hầu hết request có token hợp lệ, và độ trễ p95 của nó lên hàng chục giây (10–36 s từng phút).
- **Container vẫn `healthy`**, `/health/ready` vẫn 200 — cấu hình định danh không nằm trong kiểm tra sức khoẻ.
- Sau vài phút, 401 chỉ còn ở **một** service; các service khác nhận cùng token vẫn trả 200. Đây là điểm phân biệt với lỗi "token hỏng sau khi tạo
  lại identity" (xem mục Giới hạn).
- Lỗi lan ngược lên `Bff.Api` (`/bff/checkout` 502) và `Gateway.Api` (502, 504).
- Log của service nói thẳng địa chỉ sai: `incident-missing-host:8080` và `/.well-known/openid-configuration`.

## Điều kiện

- Giống file 09: `.env` có `CHAOS_ALLOW_FAULT_INJECTION=true`, tải nền `-Load` đang chạy, dashboard và rule đã import.
- Loại F tạo lại cả 7 container (như loại A), nên có nhiễu khởi động nguội 5–7 phút và token của tải nền phải được lấy lại.

## Loại F — Địa chỉ máy chủ định danh sai

**Đích hợp lệ**: 6 service dùng `Identity__Authority`. Tham số: `{"authority":"wrong"}` — địa chỉ được đổi thành
`http://incident-missing-host:8080`.

### Tiêm

```powershell
./scripts/incident-drill.ps1 -Inject -Type F -Target orders-api
```

Đo ngày 2026-10-06: bắt đầu 11:42:21, `injected` lúc 11:43:09. Kiểm cấu hình đã vào container:

```powershell
docker inspect ecomerce-local-orders-api-1 --format '{{range .Config.Env}}{{println .}}{{end}}' | Select-String 'Authority'
# Identity__Authority=http://incident-missing-host:8080
```

### Nơi nhìn trên Kibana

1. **Traffic + 401/403 theo phút theo service**: đường 401 chỉ nảy lên ở `Orders.Api`.
2. **Latency p95 theo phút theo service**: `Orders.Api` lên hàng chục giây (đỉnh 35959 ms).
3. **Phân bố status code theo service**: cột `401` xuất hiện ở `Orders.Api` mà không có ở service khác.
4. **Lỗi gọi hạ lưu**: `Bff.Api → Orders.Api` có lỗi (401 từ orders được BFF dịch thành 502).
5. **Log** (lọc `Orders.Api`, kể cả mức Warning, vì chỉ có vài dòng Error): lời gọi `.well-known/openid-configuration` tới `incident-missing-host`.
6. **Phát hiện nhanh**: không đo được riêng cho loại F — `Orders.Api` và `Gateway.Api` đã `active` từ 11:20:32 (nhóm 3) và liên tục `active` qua nhóm 4 và 5.

### Truy vấn tự đối chiếu

**5xx, 401 và p95 theo service trong 5 phút gần nhất** — tìm service có `n401` cao:

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 5 minutes
| EVAL is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0), is_401 = CASE(attributes.http.response.status_code == 401, 1, 0)
| STATS total = COUNT(*), bad_5xx = SUM(is_5xx), n401 = SUM(is_401), p95_ms = ROUND(PERCENTILE(duration, 95) / 1000000.0) BY service = resource.attributes.service.name
| SORT service
```

**Route nào và mã nào** (so mã 4xx với 5xx):

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 5 minutes AND attributes.http.response.status_code >= 400
| STATS n = COUNT(*) BY service = resource.attributes.service.name, route = attributes.http.route, code = attributes.http.response.status_code, kind = kind
| SORT n DESC
| LIMIT 12
```

**Log cảnh báo/lỗi của service nghi vấn** (`severity_number >= 13` gồm Warning):

```esql
FROM logs-generic.otel-default*
| WHERE @timestamp > NOW() - 5 minutes AND resource.attributes.service.name == "Orders.Api" AND severity_number >= 13
| STATS n = COUNT(*) BY severity_text
```

Trực tiếp từ container:

```powershell
docker logs --since 5m ecomerce-local-orders-api-1 2>&1 | Select-String 'openid-configuration|incident-missing|OnRetry'
curl.exe -s -o NUL -w "%{http_code}`n" http://localhost:5041/health/ready          # vẫn 200
docker ps --format '{{.Names}} {{.Status}}' | Select-String 'orders-api'           # vẫn healthy
```

### Số đo (ngày 2026-10-06, có tải nền, `Orders.Api`)

Nền trước khi tiêm: 11:40–11:42 `dat = true`, p95 3–45 ms. Từ 11:43 (từng phút, 0% 5xx cả tám phút):

| Phút | 11:43 | 11:44 | 11:45 | 11:46 | 11:47 | 11:48 | 11:49 | 11:50 |
|---|---|---|---|---|---|---|---|---|
| Span | 17 | 30 | 1 | 9 | 18 | 28 | 48 | 57 |
| p95 (ms) | 35959 | 27745 | 462 | 15287 | 16753 | 3904 | 10042 | 1945 |

Trong 5 phút tới 11:51: `Orders.Api` 159 span, 110 trả `401` ở `/orders` (69%), p95 10581 ms; `Bff.Api` 882 span, 116 5xx (13,2%) với `/bff/checkout` 502 ×93;
`Gateway.Api` 695 span, 228 5xx (502 ×91, 504 ×23 và các mã khác); `Baskets.Api`, `Parties.Api`, `Products.Api`, `Identity.Api`: không có 401.
Dòng 404 của `Gateway.Api` (108) và `Parties.Api` (55) là nền của tải nền, không liên quan. Log `Orders.Api` 5 phút: 38 Warning và 5 Error; trong log
container: 45 dòng `HttpRequestException: Name or service not known (incident-missing-host:8080)`, 15 dòng `Sending HTTP request GET
http://incident-missing-host:8080/.well-known/openid-configuration` và 15 dòng `Resilience event ... OnRetry ... IdentityBackchannel`.
`orders-api` `Up 8 minutes (healthy)`, `/health/ready` 200.

### Khôi phục

```powershell
./scripts/incident-drill.ps1 -Restore -RunId <runId>
```

Tạo lại 7 container không kèm cấu hình sai. Đo: 11:51:20 → 11:52:17 (57 giây), `Identity__Authority=http://identity-api:8080` trở lại.

### Xác nhận đã khỏi

Phút 11:52 còn `total = 43`, p95 412 ms, p99 3410 ms (khởi động nguội); từ phút 11:53 `dat = true` (87 span, p95 38 ms) và giữ qua 11:57 (p95 17–38 ms). Mốc
giải quyết theo file 08: 15 phút liên tục `dat = true` và không còn alert active. Bản đo này chưa chờ đủ 15 phút (file 09 đã đo trọn chuỗi 15 phút).

## Giới hạn đã biết

- **Phân biệt với "token hỏng sau khi tạo lại identity"**: sau khi tạo lại cả 7 container, service vừa tạo lại từ chối token cũ của tải nền (401) và BFF trả 502/504
  trong 1–2 phút cho tới khi `-Load` lấy token mới (QA_Debt mục 028, 031) — rất giống nhóm 5 trong lúc đó. Khác ở chỗ lỗi do token tự hết khi token mới được lấy,
  còn nhóm 5 vẫn 401 ở đúng một service dù token mới. Đợi 2–3 phút sau khi tạo lại rồi mới kết luận.
- **Triệu chứng chậm kéo dài**: p95 hàng chục giây do cơ chế thử lại tải cấu hình OpenID, nên số span từng phút của service đích rất thấp (1–57); đừng đọc
  tỷ lệ phần trăm khi mẫu quá ít.
- **Log mức Error rất ít** (5 dòng trong 5 phút), nội dung chính nằm ở mức Warning: lọc `severity_number >= 13`, không chỉ `>= 17` như panel "Log lỗi gần nhất".
- **Sức khoẻ vẫn xanh**: container `healthy` và `/health/ready` 200 dù mọi request xác thực đều hỏng.
- **Biến thể dừng `identity-api` đã bỏ** (không có triệu chứng đo được); `identity-api` không là đích.
- **Không đo được thời gian báo riêng** vì alert của nhóm trước còn `active`.
- **Log lỗi bị `Identity.Api` lấp** và **dòng 100% giả `Bff.Api → Parties.Api`**: xem file 09.

## Bài tập tự làm

1. Tiêm loại F vào một service **khác** `orders-api` (ví dụ `products-api`) với `-DurationSeconds 300` và tự trả lời không xem lại mục trên:
   (a) service nào trả 401 và tỷ lệ; (b) vì sao container vẫn `healthy`; (c) làm sao chứng minh đây là cấu hình định danh sai, chứ không phải token hỏng;
   (d) lỗi có lan lên BFF/gateway không.
   Đáp án tham khảo từ lần thử (`products-api`, 11:59:41 → `restored` 12:05:05): 144/213 span `Products.Api` trả 401, p95 12810 ms, `Bff.Api` 98 5xx và 77×401,
   `/health/ready` 200, container `healthy`; các service khác không có 401. Khôi phục xong muộn hơn 5 phút khoảng nửa phút vì phải tạo lại container.
2. Chạy truy vấn log với `severity_number >= 17` rồi `>= 13` và giải thích sự khác biệt giữa hai kết quả.
3. Sau khi script tự khôi phục, ghi phút đầu tiên `dat = true` và so với thời lượng nhiễu khởi động nguội.

## Đã đạt khi

- [ ] Chỉ ra đúng service đích và 401 chỉ ở service đó.
- [ ] Nêu đúng địa chỉ sai (`incident-missing-host:8080`) lấy từ log hoặc `docker inspect`.
- [ ] Giải thích được vì sao sức khoẻ vẫn xanh và vì sao độ trễ lên hàng chục giây.
- [ ] Phân biệt được nhóm 5 với "token hỏng sau khi tạo lại identity".
- [ ] Khôi phục bằng `-Restore` (hoặc `-DurationSeconds`) và `Identity__Authority` về `http://identity-api:8080`.
- [ ] Truy vấn 15 phút ở file 08 cho service đó đạt 15 phút liền `dat = true`, không còn alert active.

## Xem thêm

- Quy trình triage, SEV và bản ghi sự cố:
  [`docs/dien-tap-chaos-engineering/README.md`](../dien-tap-chaos-engineering/README.md#diễn-tập-sự-cố-on-call-scrum-36) và
  [`mau-ban-ghi-su-co.md`](../dien-tap-chaos-engineering/mau-ban-ghi-su-co.md).
- Truy vấn xác nhận khôi phục 15 phút: [file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md#xác-nhận-khôi-phục-15-phút).
- Luyện không biết trước: [file 17](17-goi-y-theo-trieu-chung.md).
- Trước: [12 — Hạ tầng dừng](12-ha-tang-dung.md). Sau: [14 — Thiếu tài nguyên](14-thieu-tai-nguyen.md).
