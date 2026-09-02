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

## 3. Sự cố thật đã phát hiện và sửa khi chạy thử

**Chế độ debug (publish thêm cổng nội bộ để debug) không thể tồn tại đúng như thiết kế ban đầu** — một
Compose profile chỉ quyết định một service có KHỞI ĐỘNG hay không, không thể thêm cổng cho một service
đã khởi động sẵn. Phải chuyển thành một file override (`docker-compose.debug.yml`), truy cập qua
`./scripts/up.sh --debug` hoặc `up.ps1 -PublishInternalPorts`.

**Bản override đầu tiên vô dụng đúng mục đích nó sinh ra để phục vụ**: công bố cổng 5301 nhưng
`/openapi/v1.json` trả về **404**, vì BFF chỉ map document đó ở môi trường Development trong khi stack
chạy như Production — trong khi việc sinh lại API client chính là lý do người ta muốn mở cổng đó.
Override phải đổi luôn cả `ASPNETCORE_ENVIRONMENT` của BFF, kéo theo phải khôi phục lại các hostname
compose mà cấu hình Development của nó trỏ về `localhost`. Xác nhận sau khi sửa: document trả về 200
**và** `/bff/products` vẫn trả 200 — cặp kết quả quan trọng, vì một override phục vụ được document
nhưng làm hỏng mọi lệnh gọi khác còn tệ hơn không có override nào.

## 4. Giới hạn phạm vi đã biết

- **Schema-per-tenant separation vẫn CHƯA được đóng lại** — lần thứ hai bị nêu ra (sau 004): plan của
  003 đã đặc tả và đánh dấu hoàn thành trên giấy, mã nguồn chưa từng có nó, 004 đã ghi nhận cùng phát
  hiện. Feature này không làm nó tệ hơn (chỉ dễ nhận ra hơn khi dùng chung một máy chủ), nhưng cũng
  không đóng lại — **vẫn đang chờ quyết định của maintainer.**
- Redis/RabbitMQ chạy sẵn theo đúng khai báo phụ thuộc của nền tảng, nhưng **không có service nào kết
  nối tới chúng** trong phạm vi feature này — một health check khoẻ mạnh chỉ chứng minh "dependency có
  mặt", không chứng minh "dependency đang hoạt động trong một luồng thật" (ADR-0011 ghi nhận thiết kế
  tạm thời: checkout vẫn đồng bộ vì chưa có hạ tầng messaging).
- Image storefront đóng cứng địa chỉ backend lúc build — chỉ phù hợp cho đúng stack này; cấu hình theo
  runtime là việc cần làm khi triển khai thật sự.
- Số liệu tài nguyên đo được (~1.3 GB lúc rảnh, ~2.2 GB lúc cao điểm, ngưỡng sàn 6 GB) được đo khi
  image nền đã tải sẵn từ trước — một máy hoàn toàn sạch còn phải tải thêm khoảng 2 GB image, chưa
  được đo trong con số trên (ghi nhận minh bạch, không che giấu).

## 5. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/005-one-command-local-run-component.drawio`](../diagrams/005-one-command-local-run-component.drawio)
- Sơ đồ trình tự (một lệnh → build → health-gate từng thành phần → chỉ báo thành công khi mọi thứ
  sẵn sàng): [`docs/diagrams/005-one-command-local-run-sequence.drawio`](../diagrams/005-one-command-local-run-sequence.drawio)
- Sơ đồ luồng nghiệp vụ đơn giản hoá (đi kèm tài liệu PO):
  [`docs/diagrams/005-one-command-local-run-flow-nghiep-vu.drawio`](../diagrams/005-one-command-local-run-flow-nghiep-vu.drawio)
