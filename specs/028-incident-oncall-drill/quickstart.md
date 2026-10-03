# Quickstart: Diễn tập sự cố thật và phản ứng on-call

**Feature**: [spec.md](./spec.md) | **Contracts**: [contracts/](./contracts/)

Hướng dẫn kiểm chứng end-to-end trên Docker Compose local. Không có test tự động (plan.md, Complexity
Tracking), nên đây là cách duy nhất chứng minh tính năng chạy đúng.

## Điều kiện trước

- Stack 027 đã chạy được: `.env` có `KIBANA_ENCRYPTION_KEY`, 4 rule `slo-error-budget` đã enable.
- Node có sẵn. Lần đầu chạy, `npx newman` sẽ tải gói `newman` từ npm.
- `.env` có `CHAOS_ALLOW_FAULT_INJECTION=true` (chỉ cho buổi diễn tập; trả về `false` sau đó).

```powershell
docker compose -f docker-compose.local.yml up -d --build --wait
```

## Kịch bản 0 — Cờ tắt thì không có gì xảy ra (FR-003, SC-006)

1. Đặt `CHAOS_ALLOW_FAULT_INJECTION=false` trong `.env`.
2. Chạy `scripts/incident-drill.ps1 -Start`.
3. **Kỳ vọng**: script thoát với mã khác 0, không có thư mục mới dưới `.incident-drill/`, không
   container nào được tạo lại (`docker ps` giữ nguyên uptime).
   Xem [incident-drill-script-contract.md](./contracts/incident-drill-script-contract.md) bất biến 1.

## Kịch bản 1 — Import rule phát hiện nhanh và panel (FR-005, FR-006)

1. Import `docs/kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson` và
   `dashboards/slo-van-hanh-hang-ngay.ndjson` bằng `_import?overwrite=true`, cùng cách README alerts
   của 027.
2. Enable rule tag `incident-fast-detection`. Kibana nhập rule ở trạng thái disabled.
3. **Kỳ vọng**: sau ≤ 5 phút rule có "Last run"; dashboard SLO hằng ngày có bảng "Phát hiện nhanh — vượt
   SLO trong 5 phút gần nhất", đang trống.

## Kịch bản 2 — Tải nền phủ cả 7 service (FR-004)

```powershell
./scripts/incident-drill.ps1 -Load
```

Chạy trong một terminal riêng, Ctrl+C để dừng. Chế độ này lấy token (folder 00) một lần, rồi chạy
folder 26 + 28 bằng `npx.cmd --yes newman@6.2.2`, nghỉ 1000 ms giữa các request. Token được lấy lại
khi hết 30 phút, khi runner gặp `401`, hoặc khi container `identity-api` được tạo lại.

**Kỳ vọng**: sau 5 phút, ES|QL `STATS COUNT(*) BY resource.attributes.service.name` trên traces 5 phút
gần nhất trả đủ 7 service; rule phát hiện nhanh không có alert nào (baseline khoẻ).

## Kịch bản 3 — Buổi diễn tập đầy đủ (US1–US4)

1. Bật cờ trong `.env`, giữ tải nền của kịch bản 2.
2. Chạy `scripts/incident-drill.ps1 -Start`. **Kỳ vọng**: chỉ in `runId` và mã băm
   ([incident-drill-script-contract.md](./contracts/incident-drill-script-contract.md) bất biến 2).
3. Chờ cảnh báo. Alert chỉ có trong một lần chạy rule ngay sau khi các container được tạo lại là nhiễu
   khởi động nguội; alert còn active qua ≥ 2 lần chạy là sự cố (nhiễu đo được có thể kéo dài tới 2 lần
   chạy — giới hạn đã biết, xem `docs/kibana-quan-sat-he-thong/08-phat-hien-nhanh-va-xu-ly-su-co.md`). **Kỳ vọng**: trong ≤ 30 phút + 10 phút, rule `incident-fast-detection` có alert, và
   bảng phát hiện nhanh trên dashboard hiện service đó (SC-002).
4. Triage theo `docs/dien-tap-chaos-engineering/README.md` mục "Diễn tập sự cố on-call":
   - tạo Kibana Case, dán mã băm;
   - ghi severity;
   - comment tại mỗi mốc và mỗi 30 phút;
   - tìm nguyên nhân từ telemetry/log.
5. Giảm thiểu: mở và merge vào master một PR phòng ngừa tái diễn, rồi:
   Rồi đặt `CHAOS_ALLOW_FAULT_INJECTION` về tắt trong `.env` (bỏ dòng đó) và chạy:
   ```powershell
   git pull; docker compose -f docker-compose.local.yml up -d --build --wait
   ```
   Cờ đổi nên cả 7 container được tạo lại không kèm file override: cấu hình sai được gỡ, và việc gửi
   header của loại 5xx dừng. Chỉ chạy lại compose mà không đổi cờ thì loại 5xx KHÔNG được gỡ. Ghi mốc giảm thiểu và baseline
   "alert → merge" vào Case.
6. Theo dõi tới khi đạt SLO liên tục 15 phút và rule không còn active cho service. Ghi mốc giải quyết
   kèm bằng chứng, rồi đóng Case.
7. Chạy `scripts/incident-drill.ps1 -Reveal -RunId <runId>`. **Kỳ vọng**: mã băm khớp, in lựa chọn
   và thời điểm tiêm thực tế.
8. Điền bản ghi `docs/dien-tap-chaos-engineering/ket-qua/<YYYY-MM-DD>-su-co-<runId>.md` theo
   [incident-record-contract.md](./contracts/incident-record-contract.md), thêm vào "Lịch sử chạy".
   **Kỳ vọng**: đủ trường, các mốc đúng thứ tự, phát hiện / giảm thiểu / giải quyết là ba thời điểm
   khác nhau (SC-003).

## Kịch bản 4 — Kiểm tra từng loại hỏng hóc (xác minh V2, V4 của research)

Dùng chế độ không mù, ví dụ `scripts/incident-drill.ps1 -Start -Service orders-api -FaultType B -DelaySeconds 0`, rồi reveal
ngay để xác nhận mỗi loại A/B/C tạo ra 5xx hoặc trễ vượt ngưỡng đủ để rule bắn. Ghi kết quả vào
`docs/QA/028_QA_*.md`, và mọi phát hiện vào `QA_Debt.md`.

## Dọn dẹp

- `CHAOS_ALLOW_FAULT_INJECTION=false` trong `.env`, rồi
  `docker compose -f docker-compose.local.yml up -d --wait`.
- Dừng newman (Ctrl+C).
- Dọn đơn hàng do folder 26 tạo, cùng cách QA 026 đã làm.
- Xoá `.incident-drill/<runId>/` sau khi bản ghi đã hoàn tất (tuỳ chọn).
