# Data Model: Diễn tập chaos engineering — giết pod / tiêm độ trễ

**Feature**: [spec.md](./spec.md) | Nghiên cứu: [research.md](./research.md)

Tính năng này không có dữ liệu nghiệp vụ (không bảng CSDL, không sự kiện mới). "Thực thể" ở đây là
cấu hình/tài liệu — được ghi lại để `/speckit-tasks` có cơ sở tách việc.

## 1. Cấu hình tiêm độ trễ (Chaos Latency Injection Configuration)

Sống trong `appsettings.json` của Orders.Api, đọc qua `IConfiguration`/`IOptions<ChaosOptions>`.

| Trường | Kiểu | Mặc định | Ràng buộc |
|---|---|---|---|
| `Chaos:AllowLatencyInjection` | bool | `false` | Chỉ được `true` ở môi trường dành riêng cho diễn tập (spec FR-006). KHÔNG BAO GIỜ `true` ở cấu hình production đã commit. |
| Header request `X-Chaos-Latency-Ms` | int (per-request, không phải cấu hình tĩnh) | vắng mặt = không tiêm | Chỉ có hiệu lực khi `AllowLatencyInjection=true`. Giá trị âm hoặc không parse được → bị bỏ qua (coi như vắng mặt). Giá trị > `MaxInjectedLatencyMs` (30 000) → bị kẹp (clamp) về `MaxInjectedLatencyMs`, không bị treo vô hạn. |

**Quan hệ với pipeline middleware của Orders.Api** (xem `Program.cs` hiện có): đăng ký
`ChaosLatencyInjectionMiddleware` ngay sau `app.UseServiceDefaults()` và **trước**
`app.UseIdentityValidation()` — độ trễ áp dụng cho mọi request (kể cả request sẽ bị từ chối xác
thực), phản ánh đúng độ trễ hạ tầng thật mà một dependency chậm gây ra cho caller (BFF), thay vì chỉ
độ trễ của logic nghiệp vụ. Middleware không đọc/ghi gì tới `OrdersDbContext` hay tenant — không có
tương tác với 003-stub-identity-tenant-context/005-one-command-local-run.

**Trạng thái/vòng đời**: không có trạng thái lưu trữ; mỗi request tự quyết định có bị trì hoãn hay
không dựa trên header của chính nó — không có "phiên tiêm lỗi" toàn cục cần bật/tắt/dọn dẹp.

## 2. Bài tập chaos (Chaos Exercise Run) — thực thể tài liệu, không phải bảng dữ liệu

Ghi trong bản ghi kết quả (mục 3), không cần lưu trữ có cấu trúc máy đọc được (spec.md không yêu cầu
truy vấn/API cho thực thể này — chỉ yêu cầu con người tra cứu lại được, FR-009).

| Trường | Mô tả |
|---|---|
| Loại kịch bản | `kill-pod` (basket service) hoặc `inject-latency` (orders service) |
| Service mục tiêu | `baskets` hoặc `orders` (khớp `app` label / `service_name` trong `deploy/ansible/inventories/services.yml`) |
| Tham số | Với `inject-latency`: giá trị `X-Chaos-Latency-Ms` đã dùng. Với `kill-pod`: tên pod đã xoá (`kubectl get pods -l app=baskets`) |
| Thời điểm bắt đầu/kết thúc | Ghi tại chỗ khi thực hiện |
| Trạng thái tải nền | Có/không có tải tổng hợp đang chạy, và bằng công cụ nào (spec.md Assumptions: không ràng buộc công cụ cụ thể) |

## 3. Bản ghi kết quả (Exercise Outcome Write-up)

File markdown theo mẫu tại `docs/dien-tap-chaos-engineering/mau-ket-qua.md`, một bản sao được điền
mỗi lần chạy lưu tại `docs/dien-tap-chaos-engineering/ket-qua/<YYYY-MM-DD>-<loai-kich-ban>.md`.

| Trường | Bắt buộc | Mô tả |
|---|---|---|
| `ngay_chay` | Có | Ngày thực hiện bài tập |
| `kich_ban` | Có | `kill-pod` hoặc `inject-latency` (mục 2) |
| `nguoi_thuc_hien` | Có | Ai chạy bài tập |
| `quan_sat` | Có | Mô tả quan sát được — thời gian phục hồi đo được (kill-pod) hoặc mức ngân sách SLO bị tiêu hao quan sát trên dashboard (inject-latency), kèm bằng chứng (ảnh chụp/link Kibana) |
| `ket_luan` | Có | Một trong hai giá trị: `dat` (lưới an toàn hoạt động đúng) hoặc `sai_lech` |
| `jira_ticket` | Có nếu `ket_luan = sai_lech` | Liên kết bug ticket đã tạo thủ công (FR-008); để trống nếu `ket_luan = dat` |

**Bất biến** (kiểm bằng mắt qua `quickstart.md`, không có test tự động — bản chất là tài liệu):
1. Mỗi bài tập đã chạy MUST có đúng một file bản ghi kết quả tương ứng (FR-007).
2. `ket_luan = sai_lech` MUST đi kèm `jira_ticket` khác rỗng (FR-008).
3. `docs/dien-tap-chaos-engineering/README.md` MUST liệt kê mọi file trong `ket-qua/` (FR-009).

## 4. Bug Ticket (khi phát hiện sai lệch)

Thực thể ngoài hệ thống (Jira, tạo thủ công — xem research.md Quyết định 3). Ràng buộc duy nhất
thuộc phạm vi tính năng này: liên kết ngược của nó phải xuất hiện trong trường `jira_ticket` của bản
ghi kết quả tương ứng (mục 3) — không có ràng buộc nào khác (không trạng thái, không quy trình xử lý)
vì nằm ngoài hệ thống này.
