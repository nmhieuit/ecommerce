# Diễn tập chaos engineering

Runbook cho [SCRUM-34](https://nmhieuit.atlassian.net/browse/SCRUM-34) — chủ động tiêm độ trễ vào
hệ thống đang chạy để kiểm chứng bằng thực nghiệm rằng các lưới an toàn resilience
(020) và ngân sách SLO (021) hoạt động đúng, không chỉ đúng trên giấy.

Đây là thư mục tài liệu vận hành sống — tích luỹ dần theo mỗi lần bài tập được chạy — khác với
`specs/025-chaos-pod-kill-latency/`, nơi giữ hồ sơ thiết kế một lần (spec/plan/tasks) của tính năng
đã xây dựng ra công cụ tiêm lỗi dùng ở đây.

## Cách chạy một bài tập

Làm theo từng bước tại
[specs/025-chaos-pod-kill-latency/quickstart.md](../../specs/025-chaos-pod-kill-latency/quickstart.md)
— không lặp lại nội dung ở đây để tránh hai nơi lệch nhau theo thời gian. Tóm tắt kịch bản:

- **inject-latency**: bật `Chaos:AllowLatencyInjection=true` trên môi trường diễn tập, gửi header
  `X-Chaos-Latency-Ms` tới Orders.Api, quan sát dashboard Xử lý sự cố (021) thể hiện ngân sách bị tiêu hao.

Kịch bản giết pod trên Kubernetes đã gỡ ở spec 031; việc luyện "service chết" và các nhóm lỗi khác nay
nằm ở mục "Danh mục nhóm lỗi" bên dưới.

Sau khi chạy, điền [mau-ket-qua.md](./mau-ket-qua.md) thành một file mới trong
[ket-qua/](./ket-qua/) và thêm vào mục "Lịch sử chạy" dưới đây (mới nhất trước).

## Diễn tập sự cố on-call (SCRUM-36)

Khác bài tập trên: người vận hành **không biết trước** service nào hỏng, hỏng kiểu gì, và lúc nào.
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

Từ spec 031, `-Start` bốc từ tám nhóm lỗi (chín loại A–I: đích kết nối sai, nghẽn và lỗi theo tỷ lệ, độ
trễ, cơ sở dữ liệu dừng, xác thực hỏng, thiếu tài nguyên, mạng đứt, container chết). Chi tiết nằm trong
danh mục và research.md của spec, nhưng đừng đọc trước buổi diễn tập. Khi bế tắc, xin gợi ý theo mức
(`-Hint`, xem dưới); mỗi lần xin được ghi lại.

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

## Danh mục nhóm lỗi (spec 031)

Tám nhóm, chín loại lỗi (A–I) mô tả trong [`scripts/incident-drill/catalog.json`](../../scripts/incident-drill/catalog.json),
tiêm bằng cấu hình sai hoặc công cụ Docker bên ngoài (không sửa code service), chỉ trên Docker Compose local.

| Nhóm | Loại | Đích |
|---|---|---|
| 1 Đích kết nối sai | A | 7 service |
| 2 Nghẽn và lỗi theo tỷ lệ | B (cạn pool), C (5xx theo tỷ lệ) | B: gateway; C: 7 service |
| 3 Độ trễ | D | orders-api |
| 4 Phụ thuộc hạ tầng dừng | E | 5 cơ sở dữ liệu |
| 5 Xác thực hỏng | F | 6 service dùng máy chủ định danh |
| 6 Thiếu tài nguyên | G | 7 service |
| 7 Mạng đứt | H | 7 service |
| 8 Container chết | I | 7 service |

Lệnh (cần `CHAOS_ALLOW_FAULT_INJECTION=true` trong `.env`; loại D còn cần `CHAOS_ALLOW_LATENCY_INJECTION=true`
và đã chạy `-Load` để có token):

```powershell
./scripts/incident-drill.ps1 -Inject -Type E -Target orders-db -DurationSeconds 600   # dạng a: có chủ đích, tự gỡ sau 10 phút
./scripts/incident-drill.ps1 -Restore -RunId <runId>                                  # khôi phục mọi loại
./scripts/incident-drill.ps1 -Hint -RunId <runId> -Level 1                            # gợi ý bài mù: 1 triệu chứng, 2 nhóm, 3 đáp án
```

Dạng (a) không phải bài mù: script in rõ đã tiêm gì. Chỉ một lần chạy mở tại một thời điểm.

## Lịch sử chạy

Gồm bản ghi bài tập chaos (`ket-qua/<ngày>-<kịch bản>.md`, theo [mau-ket-qua.md](./mau-ket-qua.md)) và
bản ghi sự cố (`ket-qua/<ngày>-su-co-<runId>.md`, theo [mau-ban-ghi-su-co.md](./mau-ban-ghi-su-co.md)).
Với bản ghi sự cố, ghi kèm kết luận ngắn: khớp/không khớp niêm phong, severity, baseline alert→merge.

- [2026-09-14 — inject-latency (dùng `autocannon`, dashboard SLO đã xác nhận)](./ket-qua/2026-09-14-inject-latency.md)
  — sai lệch (circuit breaker không trip sau 4 lần thử độc lập kể cả với công cụ load-test thật)

Hai bản ghi kill-pod (2026-09-12, 2026-09-14) đã gỡ cùng phần Kubernetes của 025 ở spec 031.
