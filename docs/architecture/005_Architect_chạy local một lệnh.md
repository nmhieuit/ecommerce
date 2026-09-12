# Kiến trúc: Chạy toàn bộ local bằng một lệnh, container thật

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-15 ("[WALK-1] One-command local run with real containers"), đặc tả tại
[`specs/005-one-command-local-run/`](../../specs/005-one-command-local-run/), gộp toàn bộ nền tảng đã
xây từ [001](../../specs/001-scaffold-service-shells/) đến
[004](../../specs/004-minimal-shopping-spa/) vào một Docker Compose stack chạy được bằng một lệnh. Đây
là file `docker-compose.yml` (mặc định, không cần `-f`) mà
[014-identity-server-auth](../../specs/014-identity-server-auth/) sau này bổ sung thêm service
`identity` vào.

**Trạng thái xác minh**: 43/43 task trong `tasks.md` đã hoàn thành. Bảng kết quả 8 scenario cuối
`tasks.md`, trích nguyên văn: cold start **85 s** (exit 0, 10 khoẻ mạnh/1 running/4 exited 0);
storefront đầu-cuối qua Playwright **4/4 trong 10.8 s**; dừng/khởi động lại — 0 container mồ côi, cả
hai volume giữ nguyên, giỏ hàng sống sót; reset — volume bị xoá, lần chạy tiếp theo cho ra 3 sản phẩm
và giỏ hàng trống; thiếu dependency — thất bại trong **89 s**, nêu đúng từng thành phần bị ảnh hưởng
(ngân sách 120 s); thay đổi mã nguồn — marker xuất hiện đúng trong bundle được phục vụ; thiếu prerequisite
(`.env`) — báo trước khi bất kỳ thứ gì khởi động; chỉ hai cổng công bố ra ngoài (5300, 4173) trả lời,
5301/5088 bị từ chối.

## 1. Kiến trúc tổng thể

Docker Compose, `docker-compose.yml` là mặc định của repository (research.md Decision 1) — một script
wrapper mỏng (`scripts/up.ps1`/`.sh`) là "lệnh được tài liệu hoá", nhưng `docker compose` gốc vẫn dùng
trực tiếp được (Decision 2). Một SQL Server duy nhất, một database riêng mỗi service, không có bước
tạo database riêng (Decision 3) — đánh đổi tài nguyên máy cá nhân lấy đơn giản, được ghi rõ trong tài
liệu là "quy ước cục bộ, không phải hình dạng triển khai thật" (spec FR-019).

## 2. Quyết định kỹ thuật đáng chú ý (research.md)

| # | Quyết định |
|---|---|
| 3 | Một SQL Server, một database mỗi service, không có bước tạo database riêng |
| 4 | Migration đóng gói thành bundle độc lập, chạy như init container |
| 5 | Storefront có image riêng dùng nginx, có SPA history fallback |
| 6 | Địa chỉ backend của storefront được đóng cứng lúc build image, phải là địa chỉ host-reachable |
| 7 | Storefront công bố ở cổng 4173, gateway phải admit đúng origin đó |
| 8 | Chỉ gateway và storefront được công bố ra ngoài — mọi thứ khác ở nội bộ |
| 9 | Health check cần một probe binary mà runtime image mặc định không có sẵn |
| 10 | Kèm theo một OpenTelemetry Collector, vì thiếu nó gây nhiễu log liên tục |
| 11 | Hai volume riêng, và lệnh reset là một lệnh tách biệt |
| 12 | Bài test chấp nhận chính là walkthrough đã có sẵn từ trước, không viết lại |

## 3. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/005-one-command-local-run-component.drawio`](../diagrams/005-one-command-local-run-component.drawio)
- Sơ đồ trình tự (một lệnh → build → health-gate từng thành phần → chỉ báo thành công khi mọi thứ
  sẵn sàng): [`docs/diagrams/005-one-command-local-run-sequence.drawio`](../diagrams/005-one-command-local-run-sequence.drawio)
- Sơ đồ luồng nghiệp vụ đơn giản hoá (đi kèm tài liệu PO):
  [`docs/diagrams/005-one-command-local-run-flow-nghiep-vu.drawio`](../diagrams/005-one-command-local-run-flow-nghiep-vu.drawio)

2 sự cố thật đã phát hiện và sửa khi chạy thử, cộng giới hạn phạm vi đã biết (schema-per-tenant,
Redis/RabbitMQ chưa kết nối, địa chỉ backend đóng cứng, số liệu tài nguyên): xem
[technical-debt.md](technical-debt.md).
