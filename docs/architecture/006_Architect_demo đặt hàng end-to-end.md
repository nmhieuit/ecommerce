# Kiến trúc: Demo đặt hàng end-to-end — bằng chứng thoát Phase 1

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-16 ("[WALK-1] Demo: place one order end to end"), đặc tả tại
[`specs/006-e2e-order-demo/`](../../specs/006-e2e-order-demo/), chạy trên stack một-lệnh của
[005-one-command-local-run](../../specs/005-one-command-local-run/) và luồng mua sắm của
[004-minimal-shopping-spa](../../specs/004-minimal-shopping-spa/). Tài liệu tường thuật đầy đủ:
[`docs/demo-phase-1.md`](../demo-phase-1.md).

**Trạng thái xác minh**: 41/42 task trong `tasks.md` hoàn thành. Task còn lại (T042 — đính kèm video
vào Jira SCRUM-16) **BLOCKED, cần một người thực hiện thủ công** — công cụ Atlassian sẵn có hỗ trợ
comment/edit/transition nhưng KHÔNG hỗ trợ đính kèm file, và đăng lên Jira là hành động hướng ngoại,
không nên tự động thực hiện không được yêu cầu. Toàn bộ nội dung đã được chuẩn bị sẵn trong
`docs/demo-phase-1.md`.

## 1. Kiến trúc tổng thể

Kịch bản demo (`scripts/demo.ps1`/`.sh`) chạy trên container stack thật (research.md Decision 1, không
phải dev server), thực hiện: chờ platform sẵn sàng → dọn giỏ hàng qua chính baskets service (Decision
7, không dùng checkout để dọn) → chạy walkthrough Playwright thật qua trình duyệt thật → đọc lại order
qua chính orders service (không query thẳng database — Decision giữ nguyên nguyên tắc "không đọc chéo
store của service khác") → thu thập bằng chứng mỗi hop đã thực sự phục vụ traffic từ log của
OpenTelemetry Collector (Decision 6) → chụp ảnh từng bước, commit dưới `docs/demo/` (Decision 8, video
KHÔNG commit — chỉ đính kèm Jira).

## 2. Thay đổi dữ liệu — `tenantId` được thêm vào Order

- **Migration chỉ mở rộng** (research.md Decision 3): cột `tenantId` thêm vào, việc siết `NOT NULL`
  là một bước riêng sau — tránh phá vỡ dữ liệu cũ trong một migration.
- `tenantId` được thêm vào **hợp đồng downstream** (orders service trả về), **không** thêm vào hợp
  đồng client-facing của BFF (Decision 4) — xác nhận bằng cách chạy lại toàn bộ test BFF và thấy
  **54 test pass không đổi** (BFF không cần biết về trường mới).
- Endpoint `POST /orders` inject `TenantContext`, dùng `RequireTenantId()` — request không có tenant
  đã resolve **thất bại, không tạo order** (spec FR-006), xác nhận bằng bước verification riêng gọi
  trực tiếp orders service.

Xác nhận trên stack đang chạy thật (trích `tasks.md`):

```
orders service : {"id":"...","placedAtUtc":"...","total":59.25,"tenantId":"contoso"}
BFF (client)   : {"id":"...","placedAtUtc":"...","total":59.25}
```

Kết quả test: **15 unit test + 17 integration test (SQL Server thật) cho orders, tất cả pass**; TDD
đúng thứ tự đỏ-trước-xanh (`'Order' does not contain a definition for 'TenantId'` trước khi implement).

## 3. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/006-e2e-order-demo-component.drawio`](../diagrams/006-e2e-order-demo-component.drawio)
- Sơ đồ trình tự (chạy demo → walkthrough thật → verify tenant → thu bằng chứng hop từ OTel, gồm
  nhánh downstream không khả dụng): [`docs/diagrams/006-e2e-order-demo-sequence.drawio`](../diagrams/006-e2e-order-demo-sequence.drawio)
- Sơ đồ luồng nghiệp vụ đơn giản hoá (đi kèm tài liệu PO):
  [`docs/diagrams/006-e2e-order-demo-flow-nghiep-vu.drawio`](../diagrams/006-e2e-order-demo-flow-nghiep-vu.drawio)

Luồng mua sắm bốn bước mà demo dựa trên đó (duyệt → giỏ hàng → thanh toán → xác nhận) đã có sơ đồ
riêng ở [004-minimal-shopping-spa](../diagrams/004-minimal-shopping-spa-sequence.drawio) — sơ đồ trình
tự ở đây chỉ thêm phần xác minh tenant và bằng chứng hop mà 004 không có.

3 lỗi thật đã phát hiện và sửa, kết quả xác minh đầy đủ theo scenario, và giới hạn phạm vi đã biết
(T042 blocked, chỉ single-tenant, chưa có event/outbox): xem [technical-debt.md](technical-debt.md).
