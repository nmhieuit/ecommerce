# Diễn tập chaos engineering

Runbook cho [SCRUM-34](https://nmhieuit.atlassian.net/browse/SCRUM-34) — chủ động giết một pod hoặc
tiêm độ trễ vào hệ thống đang chạy để kiểm chứng bằng thực nghiệm rằng các lưới an toàn resilience
(020) và ngân sách SLO (021) hoạt động đúng, không chỉ đúng trên giấy.

Đây là thư mục tài liệu vận hành sống — tích luỹ dần theo mỗi lần bài tập được chạy — khác với
`specs/025-chaos-pod-kill-latency/`, nơi giữ hồ sơ thiết kế một lần (spec/plan/tasks) của tính năng
đã xây dựng ra công cụ tiêm lỗi dùng ở đây.

## Cách chạy một bài tập

Làm theo từng bước tại
[specs/025-chaos-pod-kill-latency/quickstart.md](../../specs/025-chaos-pod-kill-latency/quickstart.md)
— không lặp lại nội dung ở đây để tránh hai nơi lệch nhau theo thời gian. Tóm tắt hai kịch bản:

- **kill-pod**: xóa một pod đang chạy của `baskets` trong lúc có tải nhẹ, quan sát Kubernetes tái
  lập lịch và circuit breaker/retry của BFF engage.
- **inject-latency**: bật `Chaos:AllowLatencyInjection=true` trên môi trường diễn tập, gửi header
  `X-Chaos-Latency-Ms` tới Orders.Api, quan sát dashboard Xử lý sự cố (021) thể hiện ngân sách bị tiêu hao.

Sau khi chạy, điền [mau-ket-qua.md](./mau-ket-qua.md) thành một file mới trong
[ket-qua/](./ket-qua/) và thêm vào mục "Lịch sử chạy" dưới đây (mới nhất trước).

## Diễn tập sự cố on-call (SCRUM-36)

Khác hai bài tập trên: người vận hành **không biết trước** service nào hỏng, hỏng kiểu gì, và lúc nào.
Phải phát hiện nhờ cảnh báo, rồi tự xử lý như sự cố thật. Đặc tả:
[specs/028-incident-oncall-drill/](../../specs/028-incident-oncall-drill/spec.md). Cách đo và cảnh báo:
[08-phat-hien-nhanh-va-xu-ly-su-co.md](../kibana-quan-sat-he-thong/08-phat-hien-nhanh-va-xu-ly-su-co.md).
Chạy trên Docker Compose local (`docker-compose.local.yml`).

### 1. Chuẩn bị

1. Stack đang chạy. Rule `incident-fast-detection` và 4 rule `slo-error-budget` đã import và enable
   ([alerts/README.md](../kibana-quan-sat-he-thong/alerts/README.md)).
2. Thêm dòng `CHAOS_ALLOW_FAULT_INJECTION=true` vào `.env`. Thiếu dòng này thì script từ chối chạy.
3. Ở một terminal riêng, chạy tải nền cho cả 7 service và để chạy suốt buổi:
   ```powershell
   ./scripts/incident-drill.ps1 -Load
   ```
   Tải nền lấy token một lần, chạy folder Postman 26 + 28 bằng newman, nghỉ 1000 ms giữa request.
   Token được lấy lại khi hết 30 phút, khi gặp `401`, hoặc khi `identity-api` được tạo lại.
4. Chờ ≥ 10 phút cho tải nền ổn định. Bảng "Phát hiện nhanh" trên dashboard phải trống.
5. Khởi chạy diễn tập:
   ```powershell
   ./scripts/incident-drill.ps1 -Start
   ```
   Script chỉ in `runId` và mã băm SHA-256. Chép cả hai lại. Trong khoảng 0–30 phút tới, cả 7
   container sẽ được tạo lại, và một service bị hỏng bằng cấu hình sai. **Không mở thư mục
   `.incident-drill/`.**

Ba loại hỏng hóc có thể gặp: đích kết nối sai; cạn connection pool (chỉ ở gateway); lỗi 5xx của 027
theo một tỷ lệ 5–50%. Chi tiết nằm trong research.md của spec, nhưng đừng đọc trước buổi diễn tập.

### 2. Phát hiện

Theo dõi bảng **"Phát hiện nhanh — service vượt SLO trong khoảng thời gian đã chọn"** trên dashboard
`Xử lý sự cố — 7 service`.

- Lúc các container được tạo lại, mọi service đều chậm do khởi động nguội; đo thật kéo dài 5–7 phút.
- Alert chỉ có trong **một** lần chạy rule là nhiễu.
- Alert còn active qua **≥ 2 lần chạy** (5 phút một lần) là **sự cố**. Ghi `moc_phat_hien`.
- Giới hạn đã biết: nhiễu đôi khi kéo dài tới 2 lần chạy, nên có thể bị tính là sự cố. Khi đó service
  sẽ tự hết lỗi mà không cần làm gì; ghi điều này vào bản ghi.

### 3. Mở Kibana Case và đánh giá severity

Tạo Case bằng tay: ☰ → **Stack Management** → **Cases** → **Create case**.

| Trường | Giá trị |
|---|---|
| Tiêu đề | `[SEVn] Sự cố diễn tập <runId> — <service đang có cảnh báo>` |
| Mô tả | mã băm SHA-256 của `-Start` |
| Severity | SEV1 → `critical`, SEV2 → `high`, SEV3 → `medium` |
| Tag | `incident-drill` và `sev1` / `sev2` / `sev3` |

Tiêu chí severity:

| Mức | Khi nào |
|---|---|
| SEV1 | Luồng đặt hàng (browse → giỏ → checkout → đơn) hỏng hoàn toàn |
| SEV2 | Một chức năng giảm cấp rõ rệt |
| SEV3 | Ảnh hưởng nhỏ hoặc có cách vòng |

Ghi `moc_xac_dinh_severity`.

### 4. Thông báo trạng thái

Thêm một comment vào Case:
- tại **mỗi mốc**: phát hiện, severity, nguyên nhân, giảm thiểu, giải quyết;
- và **mỗi 30 phút** trong lúc sự cố còn mở.

Mỗi comment nói ngắn gọn: đang thấy gì, đang làm gì, bước tiếp theo.

### 5. Tìm nguyên nhân và giảm thiểu

1. Tìm nguyên nhân từ telemetry và log (Discover, `docker logs`, `docker inspect`). Ghi
   `moc_xac_dinh_nguyen_nhan`.
2. Mở và merge vào master một **PR phòng ngừa tái diễn**: thay đổi khiến lỗi kiểu này không xảy ra lại,
   hoặc bị phát hiện sớm hơn. Ghi `moc_giam_thieu` = lúc merge, rồi thêm comment
   `baseline: alert→merge = <phút>` vào Case.
3. Khôi phục: **bỏ dòng `CHAOS_ALLOW_FAULT_INJECTION=true` khỏi `.env`** rồi:
   ```powershell
   git pull; docker compose -f docker-compose.local.yml up -d --build --wait
   ```
   Cờ đổi nên cả 7 container được tạo lại với cấu hình đúng. Chỉ chạy lại compose mà không đổi cờ thì
   lỗi 5xx kiểu 027 **không** được gỡ.

### 6. Xác nhận giải quyết

1. Chạy truy vấn "Xác nhận khôi phục 15 phút" của
   [file 08](../kibana-quan-sat-he-thong/08-phat-hien-nhanh-va-xu-ly-su-co.md#xác-nhận-khôi-phục-15-phút)
   cho service gặp sự cố.
2. Chờ tới khi có **15 phút liên tục** có traffic và `dat = true`, đồng thời bảng phát hiện nhanh không
   còn service đó.
3. Ghi `moc_giai_quyet` (phút cuối của chuỗi), lưu ảnh hoặc link làm bằng chứng, rồi đóng Case.
4. Chỉ sau đó mới mở niêm phong:
   ```powershell
   ./scripts/incident-drill.ps1 -Reveal -RunId <runId>
   ```
   Mã băm phải khớp. Đối chiếu lựa chọn với nguyên nhân đã tự tìm ra.
5. Điền [mau-ban-ghi-su-co.md](./mau-ban-ghi-su-co.md) thành `ket-qua/<YYYY-MM-DD>-su-co-<runId>.md`,
   rồi thêm vào "Lịch sử chạy".
6. Dừng tải nền (Ctrl+C), và dọn đơn hàng do folder 26 tạo, cùng cách QA 026 đã làm.

Postmortem không đổ lỗi và ticket follow-up: SCRUM-37.

### Chế độ không mù (chỉ để xác minh/QA)

```powershell
./scripts/incident-drill.ps1 -Start -Service orders-api -FaultType A -DelaySeconds 0
```

Script in rõ "CHẾ ĐỘ KHÔNG MÙ" và ghi `"blind": false` vào bản niêm phong. Không dùng chế độ này cho
buổi diễn tập.

## Lịch sử chạy

Gồm bản ghi bài tập chaos (`ket-qua/<ngày>-<kịch bản>.md`, theo [mau-ket-qua.md](./mau-ket-qua.md)) và
bản ghi sự cố (`ket-qua/<ngày>-su-co-<runId>.md`, theo [mau-ban-ghi-su-co.md](./mau-ban-ghi-su-co.md)).
Với bản ghi sự cố, ghi kèm kết luận ngắn: khớp/không khớp niêm phong, severity, baseline alert→merge.

- [2026-09-14 — kill-pod (chạy liền mạch trên Kubernetes thật, gồm cả Bước 1→4 + Dọn dẹp)](./ket-qua/2026-09-14-kill-pod.md)
  — sai lệch (thời gian phục hồi lẫn thao tác thủ công tái cấp app; circuit breaker không trip)
- [2026-09-14 — inject-latency (chạy liền mạch trên Kubernetes thật, dùng `autocannon`, dashboard SLO đã xác nhận)](./ket-qua/2026-09-14-inject-latency.md)
  — sai lệch (circuit breaker không trip sau 4 lần thử độc lập kể cả với công cụ load-test thật)
- [2026-09-12 — kill-pod (trên container Docker)](./ket-qua/2026-09-12-kill-pod.md) — sai lệch (chạy
  trên container Docker thay vì pod Kubernetes thật; xem bản ghi 2026-09-14 để có kết quả trên k8s
  thật)
