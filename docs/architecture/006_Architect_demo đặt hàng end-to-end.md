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

## 3. Ba lỗi thật đã phát hiện và sửa

1. **Kịch bản PowerShell dừng nhầm vì một cảnh báo vô hại.** Node ghi một cảnh báo ra stderr; dưới
   `$ErrorActionPreference = 'Stop'`, PowerShell 5.1 biến stderr của lệnh native thành lỗi terminating
   — demo dừng dù luồng phía dưới đang pass. Sửa bằng một helper `Invoke-Native` chuyển về `Continue`
   quanh lệnh native, dùng exit code làm tín hiệu duy nhất.
2. **Phiên bản đầu của helper đó lại trộn output với exit code thành một mảng** (`& $Command` vừa ghi
   output vào pipeline vừa trả code) — so sánh mảng với `0` báo lỗi trên một lần chạy PASS, rồi
   `exit` trên mảng lại thoát 0 — sai theo cả hai hướng ngược nhau. Sửa bằng cách trả code qua biến
   script-scoped riêng, để output tự chảy qua pipeline.
3. **Bộ lọc bằng chứng mỗi-hop phải loại health check, nếu không mọi con số đều vô nghĩa.** Đo được
   trong một cửa sổ demo: `Parties.Api` phát ra 461 span, **cả 461 đều là health check** (Docker probe
   mỗi service mỗi 5 giây). Đếm span thô, một component không phục vụ gì vẫn "trông bận rộn" — khiến
   assertion FR-011a pass ngay cả trên một stack chưa từng chạy demo. Có lọc: Parties đúng là KHÔNG
   xuất hiện (nó không nằm trên đường đi đặt hàng), 5 hop thật xuất hiện đúng. Assertion còn được thử
   nghiệm ngược lại — cắt bớt file bằng chứng để xác nhận nó THẤT BẠI khi thiếu một component, không
   chỉ được quan sát là pass.

**Một ảnh chụp là "lời nói dối" và đã bị loại bỏ**: phiên bản đầu của `03-checkout.png` (chụp sau khi
focus nút thanh toán) trùng khớp **từng byte** với `02-basket.png`, vì viền focus không lưu lại trong
ảnh chụp màn hình. Thay bằng `04-basket-empty.png` — cho thấy giỏ hàng đã trống sau thanh toán, khác
biệt trực quan thật, và cũng là bằng chứng cho FR-017.

## 4. Kết quả xác minh đầy đủ (T038-T041)

| Scenario | Kết quả |
|---|---|
| 1 — demo chạy | exit 0 |
| 2 — tổng tiền | 59.25, khớp màn hình xác nhận |
| 3 — tenant | `tenantId` = `contoso` |
| 4 — không có tenant | 500, không tạo record, cả đọc lẫn ghi |
| 5 — lặp lại | 5 lần chạy liên tiếp, 5 đơn hàng riêng biệt |
| 6 — bằng chứng hop | 5 component xuất hiện trong `hops.txt`, Parties đúng là vắng mặt |
| 7 — artifact | walkthrough + 4 ảnh commit được, `artifacts/` bị git-ignore, không có `.webm` nào dưới `docs/` |
| 8 — cold start | 2 phút 48 giây |
| 9 — downstream không khả dụng | giỏ hàng trống → `409` với thông báo có thể hành động, không tạo order; service dừng → thông báo đúng nguyên nhân sau khi sửa (mục dưới) |

**Task T039 tìm ra một chẩn đoán sai**: khi dừng `orders-api`, demo thất bại đúng (exit 1) nhưng báo
"stack không chạy ở demo mode" — sai, chỉ sai hướng khắc phục. Ba loại lỗi trông giống nhau từ bên
ngoài (chưa publish, đã chết, hoặc up-nhưng-không-khoẻ) — bản sửa hỏi thẳng Compose/Docker để phân
biệt đúng nguyên nhân, cho ra thông báo chính xác cho từng trường hợp.

**Build + test toàn bộ (T040)**: `dotnet build Ecommerce.slnx` — 0 lỗi (warnings-as-errors);
`dotnet test Ecommerce.slnx` — **16 project test, 247 test, 0 fail**, gồm cả structure/container
convention suite (9+9) và `CrossServiceIsolation.Tests` (14). Frontend: 46 test Vitest, `tsc --noEmit`
sạch, ESLint sạch toàn workspace.

**Thời gian chạy lặp lại (T041)**: `--skip-start` chỉ mất **10 giây**, so với ngân sách kế hoạch 90
giây.

## 5. Giới hạn phạm vi đã biết

- T042 (đính video vào Jira) **chưa hoàn thành**, chờ một người thực hiện thủ công — xem đầu tài liệu.
- Demo chỉ chứng minh single-tenant (một khách hàng doanh nghiệp) — chứng minh cách ly giữa 2 tenant
  song song nằm ngoài phạm vi này.
- Không có event/outbox/messaging nào được thêm ở feature này — thuộc về SCRUM-18, để dành riêng.

## 6. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/006-e2e-order-demo-component.drawio`](../diagrams/006-e2e-order-demo-component.drawio)
- Sơ đồ trình tự (chạy demo → walkthrough thật → verify tenant → thu bằng chứng hop từ OTel, gồm
  nhánh downstream không khả dụng): [`docs/diagrams/006-e2e-order-demo-sequence.drawio`](../diagrams/006-e2e-order-demo-sequence.drawio)
- Sơ đồ luồng nghiệp vụ đơn giản hoá (đi kèm tài liệu PO):
  [`docs/diagrams/006-e2e-order-demo-flow-nghiep-vu.drawio`](../diagrams/006-e2e-order-demo-flow-nghiep-vu.drawio)

Luồng mua sắm bốn bước mà demo dựa trên đó (duyệt → giỏ hàng → thanh toán → xác nhận) đã có sơ đồ
riêng ở [004-minimal-shopping-spa](../diagrams/004-minimal-shopping-spa-sequence.drawio) — sơ đồ trình
tự ở đây chỉ thêm phần xác minh tenant và bằng chứng hop mà 004 không có.
