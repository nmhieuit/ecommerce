# 14 — Nhóm 6: Thiếu tài nguyên

*(Cần đã làm file [09](09-dich-ket-noi-sai.md) và [11](11-do-tre-orders.md): file này dùng lại cách đọc dashboard
`Xử lý sự cố — 7 service`, truy vấn xác nhận khôi phục 15 phút ở [file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md) và các giới hạn nền đã
nêu ở file 09; cách phân biệt "chậm mà không lỗi" đã học ở file 11.)*

Nhóm này là **một service bị giới hạn CPU và bộ nhớ rất thấp** (0,1 CPU và 256 MB) trong khi vẫn nhận traffic bình thường. Đây là kiểu
sự cố sau khi đặt giới hạn tài nguyên quá chặt hoặc khi một node bị chiếm: service không chết, chỉ chậm dần, và khó thấy vì **không có lỗi**.

Đây là hướng dẫn của dạng (a) — lỗi **có kịch bản**. Luyện không biết trước: [file 17](17-goi-y-theo-trieu-chung.md).

## Bạn sẽ thấy

- Độ trễ của service bị giới hạn tăng gấp nhiều lần so với các service cùng loại (p95 khoảng 100 ms so với khoảng 8–25 ms), nhưng **thường chưa
  vượt ngưỡng SLO 500 ms** sau vài phút đầu.
- 0% 5xx, không có log lỗi, container vẫn `healthy`, không bị OOM-kill (bộ nhớ dùng khoảng 110 MiB trên giới hạn 256 MiB).
- `docker stats` hiện giới hạn bộ nhớ **256 MiB** thay vì toàn bộ RAM của máy (14,64 GiB ở các service khác).

## Điều kiện

- Giống file 09: `.env` có `CHAOS_ALLOW_FAULT_INJECTION=true`, tải nền `-Load` đang chạy, dashboard và rule đã import.
- Khác nhóm 1: loại G dùng `docker update` trên container **đang chạy**, **không** tạo lại container nào nên không có nhiễu khởi động nguội khi tiêm.
- Cần tải nền: không có tải thì service gần như rảnh và giới hạn CPU không gây chậm.

## Loại G — Giới hạn CPU và bộ nhớ thấp

**Đích hợp lệ**: cả 7 service. Tham số: `{"cpuLimit":0.1,"memoryLimitMb":256}` (`--cpus 0.1`, `--memory 256m`, `--memory-swap 256m`).

### Tiêm

```powershell
./scripts/incident-drill.ps1 -Inject -Type G -Target products-api
```

Đo ngày 2026-10-06: bắt đầu 12:05:26, `injected` lúc 12:05:28 (2 giây, không tạo lại container). Kiểm giới hạn đã vào container:

```powershell
docker inspect ecomerce-local-products-api-1 --format 'cpus={{.HostConfig.NanoCpus}} mem={{.HostConfig.Memory}} swap={{.HostConfig.MemorySwap}} oom={{.State.OOMKilled}}'
# cpus=100000000 mem=268435456 swap=268435456 oom=false
docker stats --no-stream --format '{{.Name}} cpu={{.CPUPerc}} mem={{.MemUsage}}' ecomerce-local-products-api-1 ecomerce-local-baskets-api-1
```

### Nơi nhìn trên Kibana

1. **Bảng SLO — 7 service**: cột Latency p95 của service bị giới hạn cao hơn rõ rệt so với các service còn lại; Error-rate vẫn 0%.
2. **Latency p95 theo phút theo service**: đường của service đó nhảy lên ~200–250 ms trong hai phút đầu rồi xuống quanh 80–100 ms, vẫn cao hơn
   hẳn các đường khác.
3. **5xx theo phút theo service**: không đổi.
4. **Top endpoint chậm nhất**: route chính của service đó nhích lên.
5. **Log lỗi gần nhất**: không có dòng nào của service đó.
6. **Phát hiện nhanh**: có thể **không** báo, vì p95 5 phút gần nhất (khoảng 103 ms) chưa vượt ngưỡng 500 ms (xem Giới hạn đã biết).

### Truy vấn tự đối chiếu

**p95, p99 và p50 theo service trong 5 phút gần nhất** — so service nghi vấn với phần còn lại:

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 5 minutes AND kind == "Server"
| EVAL is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0)
| STATS total = COUNT(*), bad = SUM(is_5xx), p50_ms = ROUND(PERCENTILE(duration, 50) / 1000000.0), p95_ms = ROUND(PERCENTILE(duration, 95) / 1000000.0), p99_ms = ROUND(PERCENTILE(duration, 99) / 1000000.0) BY service = resource.attributes.service.name
| SORT service
```

**p95 từng phút của service nghi vấn** (truy vấn "Xác nhận khôi phục 15 phút" của file 08; cột `p95_ms` và `dat`):

```esql
FROM traces-generic.otel-default*
| WHERE @timestamp > NOW() - 12 minutes AND resource.attributes.service.name == "Products.Api"
| EVAL is_5xx = CASE(attributes.http.response.status_code >= 500, 1, 0)
| STATS total = COUNT(*), bad_5xx = SUM(is_5xx), p95_ns = PERCENTILE(duration, 95), p99_ns = PERCENTILE(duration, 99) BY minute = BUCKET(@timestamp, 1 minute)
| EVAL err_pct = ROUND(TO_DOUBLE(bad_5xx) / total * 100.0, 2), p95_ms = ROUND(p95_ns / 1000000.0), p99_ms = ROUND(p99_ns / 1000000.0)
| KEEP minute, total, err_pct, p95_ms, p99_ms
| SORT minute
```

### Số đo (ngày 2026-10-06, có tải nền, `Products.Api`)

Nền trước khi tiêm: p95 8 ms (trước đó 35–40 phút, nhiều lần ghi 9–18 ms). Từng phút sau khi tiêm lúc 12:05:28 (0% 5xx):

| Phút | 12:05 | 12:06 | 12:07 | 12:08 | 12:09 | 12:10 |
|---|---|---|---|---|---|---|
| Span | 39 | 75 | 85 | 89 | 86 | 69 |
| p95 (ms) | 244 | 200 | 100 | 100 | 89 | 81 |
| p99 (ms) | 299 | 347 | 115 | 135 | 98 | 96 |
| Đạt SLO | không | không | có | có | có | có |

*(Hàng "Đạt SLO" đo theo ngưỡng cũ p95 150 ms / p99 500 ms; theo SLO mới 500/700 ms thì cả 6 cột đều đạt.)*

Trong 5 phút tới 12:11 (các phút còn lại là nền): `Products.Api` p50 4 ms, p95 103 ms, p99 196 ms; `Baskets.Api` p95 23 ms; `Orders.Api` 25 ms; `Parties.Api` 9 ms;
`Identity.Api` 20 ms; `Bff.Api` p95 173 ms; `Gateway.Api` p95 174 ms. Chi tiết: `/products` 360 span `200`, p95 102 ms. 0 5xx ở mọi service; log mức Warning trở lên của
`Products.Api` 0 dòng. `docker stats`: `products-api` 109,9 MiB / 256 MiB, `cpu=0,34%` (so với `baskets-api` 122,7 MiB / 14,64 GiB). `OOMKilled=false`, `RestartCount=0`, `healthy`.

Giới hạn CPU 0,1 chỉ làm chậm khoảng 10 lần chứ không làm vỡ SLO: p95 sau hai phút đầu quanh 80–100 ms, dưới ngưỡng 500 ms.

### Khôi phục

```powershell
./scripts/incident-drill.ps1 -Restore -RunId <runId>
```

Tạo lại **chỉ container đích** từ compose và chờ khoẻ. Đo: 12:11:32 → 12:11:41 (9 giây); `docker inspect` về `cpus=0 mem=0` (không giới hạn), container `healthy`.

### Xác nhận đã khỏi

Tạo lại container đích nên có nhiễu khởi động nguội ngắn ở service đó. Mốc giải quyết theo file 08: 15 phút liên tục `dat = true` và không còn alert active; riêng loại G,
vì p95 sau tiêm đã dưới ngưỡng, nhìn "đã khỏi" bằng p95 về mức nền (khoảng 8–25 ms) và `docker inspect` không còn giới hạn. Bản đo này chưa chờ đủ 15 phút (file 09 đã đo trọn chuỗi 15 phút).

## Giới hạn đã biết

- **Triệu chứng nhẹ, chưa vượt SLO** (QA_Debt mục 031): sau hai phút đầu p95 chỉ khoảng 100 ms so với ngưỡng 500 ms; rule `incident-fast-detection` có thể không báo. Phải so với
  chính nền của service và với các service cùng loại, không chờ cảnh báo.
- **96 MB làm OOM-kill**: mức bộ nhớ thấp hơn 256 MB (đo ở 96 MB) làm `products-api` bị OOM-kill (exit 137, 504 qua gateway), tức chuyển thành triệu chứng của nhóm 8; script chọn 256 MB để tránh điều đó.
- **Cảnh báo không phân biệt được từng nhóm**: khi các lần tiêm nối tiếp nhau, cả 7 alert vẫn `active` liên tục từ 10:34:59 tới 12:07 (lần đo này), nên không đo được thời gian báo của riêng loại G.
  Hãy chờ mọi alert về `recovered` (khoảng 15–20 phút sạch) trước khi tiêm lần kế.
- **Tải nền quyết định độ chậm**: không có tải, giới hạn CPU gần như vô hình.
- **Log lỗi bị `Identity.Api` lấp** và **dòng 100% giả `Bff.Api → Parties.Api`**: xem file 09.

## Bài tập tự làm

1. Tiêm loại G vào một service **khác** `products-api` (ví dụ `baskets-api`) với `-DurationSeconds 300`. Tự trả lời không xem lại mục trên: (a) service nào chậm và p95 từng phút;
   (b) có 5xx hay log lỗi không; (c) làm sao chứng minh giới hạn nằm ở container (hai lệnh `docker`); (d) cảnh báo có bắn không và vì sao.
   Đáp án tham khảo từ lần thử (`baskets-api`, 12:11:48 → `restored` 12:17:02): p95 từ 16–25 ms (trước khi tiêm) lên 42–133 ms (đỉnh 133 ms ở phút 12:12), 0% 5xx,
   không phút nào vượt 500 ms, `docker inspect` ghi `cpus=100000000 mem=268435456` rồi về `cpus=0 mem=0` sau khi khôi phục.
2. So `docker stats` của service bị giới hạn với một service không bị giới hạn: cột nào khác nhau?
3. Sau khi script tự khôi phục, ghi lại thời điểm p95 về mức nền.

## Đã đạt khi

- [ ] Chỉ ra đúng service chậm và p95 từng phút (có số so với nền).
- [ ] Giải thích được vì sao 0% 5xx, không log lỗi và container vẫn `healthy`.
- [ ] Chứng minh giới hạn bằng `docker inspect` (`NanoCpus`, `Memory`) hoặc `docker stats`.
- [ ] Nêu đúng vì sao cảnh báo có thể không bắn (p95 chưa vượt 500 ms).
- [ ] Khôi phục bằng `-Restore` (hoặc `-DurationSeconds`) và `docker inspect` về `cpus=0 mem=0`.
- [ ] Truy vấn 15 phút ở file 08 cho service đó đạt 15 phút liền `dat = true`, không còn alert active.

## Xem thêm

- Quy trình triage, SEV và bản ghi sự cố:
  [`docs/dien-tap-chaos-engineering/README.md`](../dien-tap-chaos-engineering/README.md#diễn-tập-sự-cố-on-call-scrum-36) và
  [`mau-ban-ghi-su-co.md`](../dien-tap-chaos-engineering/mau-ban-ghi-su-co.md).
- Truy vấn xác nhận khôi phục 15 phút: [file 08](08-phat-hien-nhanh-va-xu-ly-su-co.md#xác-nhận-khôi-phục-15-phút).
- Luyện không biết trước: [file 17](17-goi-y-theo-trieu-chung.md).
- Trước: [13 — Xác thực hỏng](13-xac-thuc-hong.md). Sau: [15 — Mạng đứt](15-mang-dut.md).
