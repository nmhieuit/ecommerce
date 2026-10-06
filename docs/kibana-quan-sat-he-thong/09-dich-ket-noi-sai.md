# 09 — Nhóm 1: Đích kết nối sai

*(Cần đã làm file [06](06-dashboard-xu-ly-su-co-va-ngan-sach-tuan.md) (dashboard `Xử lý sự cố — 7 service`) và
[08](08-phat-hien-nhanh-va-xu-ly-su-co.md) (truy vấn xác nhận khôi phục 15 phút). Đây là file đầu tiên của chuỗi luyện
troubleshoot 09–17; cách chạy lệnh tiêm và khôi phục nằm trong
[`docs/dien-tap-chaos-engineering/README.md`](../dien-tap-chaos-engineering/README.md#danh-mục-nhóm-lỗi-spec-031).)*

Nhóm này là lỗi **cấu hình trỏ tới một đích không tồn tại**: một service được khởi động với địa chỉ cơ sở dữ
liệu (hoặc địa chỉ service phía sau) sai. Service vẫn chạy, nhưng mọi lần nó cần đích đó đều thất bại. Đây là kiểu
sự cố điển hình sau một lần đổi cấu hình hay đổi tên host.

Đây là hướng dẫn của dạng (a) — lỗi **có kịch bản**, bạn biết trước nhóm nào đang chạy. Khi muốn luyện không biết
trước, dùng `-Start` và tra [file 17](17-goi-y-theo-trieu-chung.md) theo triệu chứng.

## Bạn sẽ thấy

Khi tiêm loại A vào một service (ví dụ `orders-api`):
- `Orders.Api` trả 5xx cho khoảng một nửa số request và `/health/ready` trả 503; container không lên `healthy`
  (lúc đầu `health: starting`, sau vài phút `unhealthy`).
- Lỗi **lan ngược lên** `Bff.Api` (502/500 ở route gọi orders) và `Gateway.Api` (502, thỉnh thoảng 504) dù cấu hình của
  hai service này không sai.
- Các service không liên quan (`Products.Api`, `Identity.Api`, `Baskets.Api`) gần như không có 5xx.
- Log của `Orders.Api` đầy `Name or service not known` và `A network-related or instance-specific error occurred`.

## Điều kiện

- Stack chạy bằng `docker-compose.local.yml`; `.env` có `CHAOS_ALLOW_FAULT_INJECTION=true` (chỉ bật trong lúc luyện, tắt
  sau khi xong).
- Tải nền đang chạy ở một terminal riêng (`./scripts/incident-drill.ps1 -Load`) để dashboard có traffic; không có tải
  nền thì số liệu thưa (khoảng 10 span mỗi phút mỗi service, chỉ từ kiểm tra sức khoẻ).
- Kibana đã import dashboard `Xử lý sự cố — 7 service` ([`dashboards/README.md`](dashboards/README.md)) và rule
  `incident-fast-detection` ([`alerts/README.md`](alerts/README.md)), rule ở trạng thái enabled.
- Chưa có lần chạy diễn tập nào chưa khôi phục (script từ chối `-Inject` mới khi còn lần chưa `-Restore`).

## Loại A — Đích kết nối sai

**Đích hợp lệ**: cả 7 service (`gateway-api`, `bff-api`, `products-api`, `baskets-api`, `orders-api`, `parties-api`,
`identity-api`). Với service có cơ sở dữ liệu, địa chỉ DB bị đổi sang `incident-missing-db`; với `bff-api` và
`gateway-api`, địa chỉ service phía sau bị đổi sang `incident-missing-host`.

### Tiêm

```powershell
./scripts/incident-drill.ps1 -Inject -Type A -Target orders-api
```

Script in `runId`, nhóm, loại, đích và tham số đã tiêm. Nó **tạo lại cả 7 container** (để không lộ đích qua thời gian
chạy của container), nên mất khoảng 3 phút mới tiêm xong: lần đo ngày 2026-10-06 bắt đầu lúc 09:55:18 và `state.json`
báo `injected` lúc 09:58:07. Mọi service vừa tạo lại đều chậm một lúc; xem mục "Giới hạn đã biết".

### Nơi nhìn trên Kibana

Mở dashboard `Xử lý sự cố — 7 service`, đặt thanh thời gian 15 phút:

1. **Bảng SLO — 7 service**: dòng `Orders.Api` đỏ ở cột Error-rate; `Bff.Api` và `Gateway.Api` cũng đỏ.
2. **5xx theo phút theo service**: ba đường lên gần như cùng lúc. Đường của service **ở xa người dùng nhất** (gateway) và
   **ở gần người dùng nhất** (orders) cùng lên là gợi ý lỗi nằm ở cuối chuỗi gọi, không phải ở gateway.
3. **Lỗi gọi hạ lưu — cặp service gọi → đích**: có các dòng `Bff.Api → Orders.Api` và `Gateway.Api → Bff.Api`. Dòng ở
   **cuối chuỗi** (`Bff.Api → Orders.Api`) là chỗ lỗi bắt đầu. Cẩn thận: dòng `Bff.Api → Parties.Api` có `bad_pct = 100`
   dù không liên quan — đó là nền của tải nền (request tới một đối tác không tồn tại trả 404, tính là "lỗi" ở span
   Client), không phải lỗi bạn đã tiêm.
4. **Log lỗi gần nhất (Error trở lên)**: sau mỗi lần tạo lại container, panel này bị `Identity.Api` lấp đầy bằng dòng
   `Error unprotecting the IdentityServer signing key` (1057 dòng trong 30 phút của lần đo). Thêm bộ lọc
   `resource.attributes.service.name : "Orders.Api"` thì mới thấy nội dung nêu thẳng "database" và "server was not found".
5. **Phát hiện nhanh**: rule `incident-fast-detection` báo service vượt SLO trong 5 phút gần nhất (xem file 08). Lần đo này
   rule được bật ngay sau khi khôi phục nên lần chạy đầu (10:05) báo cả **7 service** cùng `active`: nhiễu khởi động nguội
   cộng với 5 phút lỗi.

### Truy vấn tự đối chiếu

Tất cả chạy trên Elasticsearch (`http://localhost:9200/_query?format=txt`) hoặc trong Discover ở chế độ ES|QL.

**5xx và độ trễ theo service trong 4 phút gần nhất** — tìm service có `err_pct` cao:

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 4 minutes
| EVAL is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0)
| STATS total = COUNT(*), bad_5xx = SUM(is_5xx), p95_ms = ROUND(PERCENTILE(duration, 95) / 1000000.0) BY service = resource.attributes.service.name
| EVAL err_pct = ROUND(TO_DOUBLE(bad_5xx) / total * 100.0, 1)
| SORT service
```

**Route nào lỗi, mã nào** — phân biệt lỗi nghiệp vụ với lỗi hạ tầng (health check trả 503):

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 4 minutes AND attributes.http.response.status_code >= 500
| STATS n = COUNT(*) BY service = resource.attributes.service.name, route = attributes.http.route, code = attributes.http.response.status_code, kind = kind
| SORT n DESC
| LIMIT 12
```

**Ai gọi ai và lỗi gì** — span `Client`, đích suy ra từ `attributes.server.address`:

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 10 minutes AND kind == "Client" AND attributes.http.response.status_code >= 500
| STATS n = COUNT(*) BY caller = resource.attributes.service.name, target = attributes.server.address, code = attributes.http.response.status_code
| SORT n DESC
| LIMIT 6
```

**Log lỗi của service nghi vấn**:

```esql
FROM logs-generic.otel-default*
| WHERE @timestamp > NOW() - 5 minutes AND resource.attributes.service.name == "Orders.Api" AND severity_text IN ("Error", "Warning")
| STATS n = COUNT(*) BY severity_text
```

Trực tiếp từ container (không cần Kibana):

```powershell
curl.exe -s http://localhost:5041/health/ready        # orders-api, cổng 5041 theo docker-compose.local.yml
docker logs --tail 50 ecomerce-local-orders-api-1
```

### Số đo (đo ngày 2026-10-06, có tải nền `-Load`, tiêm vào `orders-api`)

Nền trước khi tiêm: mỗi service khoảng 10–12 span mỗi phút, p95 từ 2 đến 52 ms, 0% 5xx. Trong 4 phút tính tới 10:03 (lỗi
đang tồn tại):

| Service | Số span | 5xx | Tỷ lệ 5xx | p95 |
|---|---|---|---|---|
| `Orders.Api` | 285 | 152 | 53,3% | 851 ms |
| `Bff.Api` | 996 | 245 | 24,6% | 300 ms |
| `Gateway.Api` | 805 | 260 | 32,3% | 317 ms |
| `Baskets.Api` | 244 | 1 | 0,4% | 274 ms |
| `Parties.Api` | 111 | 1 | 0,9% | 411 ms |
| `Identity.Api`, `Products.Api` | 117 và 239 | 0 | 0% | 191 ms và 58 ms |

Chi tiết lỗi: `Orders.Api` `/orders` trả 500 (115 span) và `/health/ready` trả 503 (37 span); `Bff.Api` `/bff/checkout` trả
502 (115 span) và 504 (10 span); `Gateway.Api` trả 502 (115 span) và 504 (15 span). Cặp gọi: `Bff.Api → orders-api` 500 (137),
`Gateway.Api → bff-api` 502 (137) và 504 (32). Trong 5 phút, log `Orders.Api` có 442 dòng Error và 21 dòng Warning; 129 dòng
chứa `Name or service not known`. `curl http://localhost:5041/health/ready` trả 503 với `self-database` là `Unhealthy` và
mô tả `A network-related or instance-specific error occurred while establishing a connection to SQL Server ... (provider:
TCP Provider, error: 35)`; log container có `SocketException ... Name or service not known`.

Con số phụ thuộc máy và tải nền; hãy tự đo lại bằng các truy vấn trên.

### Khôi phục

```powershell
./scripts/incident-drill.ps1 -Restore -RunId <runId>
```

Lệnh tạo lại 7 container không kèm cấu hình sai và chờ đích khoẻ tối đa 10 phút. Lần đo: bắt đầu 10:03:19, xong 10:04:10
(51 giây), `state.json` báo `restored`. Có thể thay bằng chạy lại stack không kèm override:
`docker compose -f docker-compose.local.yml up -d --build --wait`.

### Xác nhận đã khỏi

"Đã gỡ" mới là lệnh `-Restore` chạy xong và container đích `healthy`. Mốc **giải quyết** vẫn theo file 08: service đạt SLO
liên tục 15 phút có traffic và rule không còn alert active. Chạy truy vấn "Xác nhận khôi phục 15 phút" của
[file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md#xác-nhận-khôi-phục-15-phút) cho `Orders.Api` (ngưỡng 150000000 / 500000000
ns). Số đo thật sau lần khôi phục này (giờ Việt Nam): phút 10:04 còn `total = 37`, p95 15750 ms (khởi động nguội); từ phút
10:05 tới 10:19 là **15 phút liên tiếp** `dat = true` (84–92 span mỗi phút, 0% 5xx, p95 20–59 ms), nên mốc giải quyết là
10:19. Rule `incident-fast-detection` chuyển cả 7 alert sang `recovered` lúc 10:20:01 (alert bắt đầu 10:05:00). Mốc giải
quyết chỉ ghi khi cả hai điều kiện đúng: chuỗi 15 phút và không còn alert active.

## Giới hạn đã biết

- **Log lỗi bị Identity.Api lấp**: mỗi lần tạo lại container, `Identity.Api` ghi hàng nghìn dòng `Error unprotecting the
  IdentityServer signing key` (khoá bảo vệ dữ liệu mất khi container tạo lại). Luôn lọc theo service nghi vấn trước khi đọc log.
- **Dòng 100% giả ở bảng cặp gọi**: `Bff.Api → Parties.Api` toàn 404 do tải nền, không phải lỗi tiêm.
- **Nhiễu khởi động nguội 5–7 phút**: tạo lại 7 container làm mọi service chậm một lúc, cả service không bị tiêm; phút đầu
  sau khôi phục p95 của `Orders.Api` là 15750 ms. Đừng kết luận trước khi so với service không bị tiêm và chờ qua vài phút
  (QA_Debt mục 028).
- **Lỗi lan theo chuỗi gọi**: thấy 5xx ở gateway/BFF **không** có nghĩa lỗi ở gateway/BFF; xem cặp gọi → đích để tìm điểm
  bắt đầu (QA_Debt mục 002, 020).
- **Môi trường chậm từng đợt**: một số service không bị tiêm đôi khi có p95 320–560 ms không rõ nguyên nhân (QA_Debt mục 028).
- **Token hỏng sau khi tạo lại identity**: tạo lại cả 7 container (kể cả `identity-api`) có thể làm token của tải nền hỏng,
  gây 401/502/504 dây chuyền; không phải triệu chứng của loại A (QA_Debt mục 028, 031).

## Bài tập tự làm

1. Tiêm loại A vào một service **khác** `orders-api` (ví dụ `products-api`) với `-DurationSeconds 300` và tự trả lời không xem lại mục trên:
   (a) service nào có tỷ lệ 5xx cao nhất và bao nhiêu; (b) lỗi lan lên những service nào, qua route nào; (c) địa chỉ sai nằm ở đâu (dùng lệnh dưới,
   chỉ lấy phần `Server=`, **không** in mật khẩu); (d) log nào nói thẳng nguyên nhân.

   ```powershell
   docker inspect ecomerce-local-products-api-1 --format '{{range .Config.Env}}{{println .}}{{end}}' | Select-String -Pattern 'Server=[^;,]+' | ForEach-Object { $_.Matches.Value }
   ```

   Đáp án tham khảo từ lần thử (`products-api`, `injected` 12:43:52 → tự gỡ `restored` 12:49:19): lệnh trên in `Server=incident-missing-db`; trong 4 phút
   `Products.Api` 358/370 5xx (96,8%; `/products` 500 ×318 và `/health/ready` 503 ×40), `Bff.Api` 502 ở `/bff/products` ×51 và `/bff/basket/items` ×55, `Gateway.Api` 502 ×104;
   `products-api` `Up 4 minutes (unhealthy)`.
2. Mở bảng "Lỗi gọi hạ lưu" trong lúc lỗi tồn tại và chỉ ra dòng nào là **điểm bắt đầu**, dòng nào là nhiễu của tải nền, dựa vào đâu bạn phân biệt.
3. Sau khi script tự khôi phục, dùng truy vấn từng phút để ghi phút đầu tiên `dat = true` và ước lượng thời gian khởi động nguội.

## Đã đạt khi

- [ ] Chỉ ra đúng service bị tiêm (tỷ lệ 5xx cao nhất) và địa chỉ sai (`incident-missing-db` hoặc `incident-missing-host`).
- [ ] Nêu đúng đường lan lỗi (service → BFF → gateway) và dòng của bảng "Lỗi gọi hạ lưu" là điểm bắt đầu.
- [ ] Phân biệt được nhiễu (log của `Identity.Api`, dòng 100% giả của `Parties.Api`, alert khởi động nguội) với lỗi thật.
- [ ] Khôi phục bằng `-Restore` (hoặc `-DurationSeconds`) và container đích `healthy`.
- [ ] Truy vấn 15 phút ở file 08 cho service đó đạt 15 phút liền `dat = true`, không còn alert active.

## Xem thêm

- Quy trình triage, mức SEV và bản ghi sự cố của buổi diễn tập:
  [`docs/dien-tap-chaos-engineering/README.md`](../dien-tap-chaos-engineering/README.md#diễn-tập-sự-cố-on-call-scrum-36) và
  [`mau-ban-ghi-su-co.md`](../dien-tap-chaos-engineering/mau-ban-ghi-su-co.md).
- Truy vấn xác nhận khôi phục 15 phút: [file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md#xác-nhận-khôi-phục-15-phút).
- Luyện không biết trước: [file 17](17-goi-y-theo-trieu-chung.md).
- Nhóm kế tiếp: [10 — Nghẽn và lỗi theo tỷ lệ](10-nghen-va-loi-theo-ty-le.md).
