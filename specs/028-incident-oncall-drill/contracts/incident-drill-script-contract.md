# Contract: Script diễn tập sự cố `scripts/incident-drill.ps1`

**Feature**: [../spec.md](../spec.md) (FR-001, FR-002, FR-003, FR-015) | **Người tiêu thụ**: người vận
hành; được kiểm chứng bằng [../quickstart.md](../quickstart.md) (không có test tự động — xem plan.md,
Complexity Tracking).

## Bất biến

| # | Bất biến |
|---|---|
| 1 | Nếu `.env` không có `CHAOS_ALLOW_FAULT_INJECTION=true`, `-Start` thoát với mã khác 0, không ghi file, không tạo lại container nào. |
| 2 | `-Start` chỉ in ra `runId` và mã băm SHA-256 của `sealed.json`; KHÔNG in service, loại hỏng hóc, tham số hay thời điểm tiêm. |
| 3 | Mọi file sinh ra nằm dưới `.incident-drill/` (có trong `.gitignore`). |
| 4 | Script KHÔNG sửa file nào đã commit (compose, `.env.example`, code dưới `services/`/`shared/`). Cấu hình sai chỉ nằm trong file override tạm. |
| 5 | Lúc tiêm, cả 7 container service được tạo lại trong cùng một lệnh; chỉ service đích nhận biến môi trường sai. |
| 6 | `faultType` ∈ {`A-wrong-target`, `B-pool-exhaustion`, `C-027-5xx`} và luôn áp dụng được cho `service`: chỉ `gateway-api` có thể nhận `B-pool-exhaustion` (người dùng chốt 2026-10-02). |
| 7 | `delaySeconds` ∈ [0, 1800]; `errorRatePct` ∈ [5, 50] khi và chỉ khi `faultType = C-027-5xx`. |
| 8 | Với `C-027-5xx`, việc gửi request mang header dừng khi container đích có Id khác lúc tiêm. |
| 9 | `-Reveal` báo lỗi nếu mã băm tính lại khác mã băm ghi lúc `-Start`; khi khớp, in lựa chọn + thời điểm tiêm thực tế và xoá file override. |
| 11 | Chế độ xác minh không mù: `-Start -Service <svc> -FaultType <A|B|C> [-DelaySeconds 0]` bỏ bốc thăm cho các tham số được chỉ định (vẫn kiểm bất biến 1, 6, 7), in rõ dòng "CHẾ ĐỘ KHÔNG MÙ — không dùng cho buổi diễn tập", và ghi `"blind": false` vào `sealed.json`. Buổi diễn tập thật không dùng các tham số này. |
| 10 | Khôi phục chuẩn: đặt `CHAOS_ALLOW_FAULT_INJECTION` về tắt trong `.env` rồi chạy lại stack (`docker compose -f docker-compose.local.yml up -d --build --wait`, không kèm override) → cả 7 container được tạo lại với cấu hình đúng, và vòng gửi header loại C dừng. Chỉ chạy lại compose mà không đổi cờ thì loại C KHÔNG được gỡ. |
| 12 | `-Load`: lấy token (folder 00) một lần, chạy folder 26 + 28 bằng `npx.cmd --yes newman@6.2.2`; lấy token lại khi hết 30 phút, khi runner gặp `401`, hoặc khi Id container `identity-api` đổi. |
