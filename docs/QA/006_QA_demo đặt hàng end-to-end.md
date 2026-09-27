# QA: Demo đặt hàng end-to-end — bằng chứng thoát Phase 1

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát (US1 → US3)

1. **Demo một lệnh** (US1): `./scripts/demo.ps1` (hoặc `demo.sh`) đưa stack vào "demo mode", dọn giỏ, chạy
   walkthrough Playwright thật (duyệt → 2 Notebook + 1 Apron → giỏ $59.25 → thanh toán → xác nhận), đọc lại
   đơn từ service Orders, thu bằng chứng từng chặng từ log OpenTelemetry — lặp lại được, mỗi lần 1 đơn riêng.
2. **Quy thuộc tenant** (US2): đơn lưu kèm `tenantId` do gateway phân giải, đọc lại thấy `tenantId` ở service
   Orders (không lộ ra hợp đồng BFF); đặt đơn không có tenant thì thất bại và không tạo đơn.
3. **Bằng chứng để lại** (US3): bài tường thuật viết tay + ảnh từng bước được commit; video chỉ đính kèm Jira.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

Chủ thể của spec này là **script demo** chạy trên stack mặc định `docker-compose.yml` + lớp
`docker-compose.demo.yml` (công bố thêm cổng `5041` Orders và `5188` Baskets cho bước xác minh), nên — như 005 —
phần thủ công chạy chính script đó thay vì `docker-compose.local.yml`. Kịch bản gốc:
[`specs/006-e2e-order-demo/quickstart.md`](../../specs/006-e2e-order-demo/quickstart.md) (9 scenario). Tiên quyết một lần:
`cp .env.example .env` (đủ `ClientSecret` + `TestUserPassword`), `corepack pnpm install` trong `frontend/`, cài chromium cho Playwright.

> Nếu `localhost` không gọi được dù container `healthy`: khởi động lại hẳn Docker Desktop (lỗi forwarding
> IPv6 loopback `::1` của WSL2), không phải lỗi ứng dụng.

### Thủ công — chạy đúng như spec mô tả

```bash
./scripts/demo.ps1            # hoặc demo.sh — đưa stack vào demo mode, dọn giỏ, chạy walkthrough, xác minh, thu bằng chứng
```

| Bước (quickstart) | Cách làm | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-27)** |
|---|---|---|---|
| Scenario 1 — demo một lệnh (US1, FR-001…004, SC-001/002) | `./scripts/demo.ps1` trên máy đã có `.env` đầy đủ, stack dựng sẵn được | `exit 0`, in mã đơn + $59.25 + tenant; 4 ảnh mới; 1 file `.webm` | **Không đổi — vẫn FAIL**: dừng ở "Clearing the basket..." → "Cannot run the demo: the basket could not be cleared (HTTP 401)…", `exit 1` sau **152 giây** |
| Nguyên nhân của Scenario 1 | `POST http://localhost:5188/baskets/current/clear` chỉ kèm `X-Tenant-Id`/`X-Subject-Id` (đúng như script) | Được chấp nhận (thời stub identity) | Vẫn `401` — script không có mã lấy token; đã grep lại `token`/`Authorization` trong `scripts/demo.ps1`, `demo.sh`, `order-demo.spec.ts`: không có |
| Scenario 2 — đơn lưu khớp xác nhận (FR-003/004) | Không chạy được bằng demo; làm tay trên `docker-compose.local.yml`: lấy token, thêm giỏ, `POST /bff/checkout`, rồi `GET :5041/orders/<mã>` **kèm `Authorization` + `X-Tenant-Id: contoso`** | `200`, total khớp | Đúng: checkout `201` total `12.50` → gọi thẳng Orders `200` cùng `id`, total `12.50` |
| Scenario 3 — quy thuộc tenant hiển thị (US2/FR-005a) | Cùng lệnh trên | `"tenantId": "contoso"` | Đúng: `{"id":"c0961142-…","placedAtUtc":"…","total":12.50,"tenantId":"contoso"}`. Qua BFF (`GET /bff/orders/<mã>`) chỉ còn 3 trường `id/placedAtUtc/total` — đúng Decision 4 |
| Scenario 4 — không tenant, không đơn (FR-006) | Có token nhưng bỏ `X-Tenant-Id`: `GET` rồi `POST :5041/orders`; đếm dòng bảng `Orders` trước/sau | Thất bại, không dòng mới | Đúng: `GET` → `500`; `POST` → `500`; số đơn **14 → 14** không đổi |
| Scenario 5 — lặp lại (FR-007) | `./scripts/demo.ps1 -SkipStart` | `exit 0`, mã đơn khác lần 1 | **Chưa chạy được** (Scenario 1 chưa qua) |
| Scenario 6 — bằng chứng từng chặng (FR-011a) | `cat artifacts/demo/hops.txt` | 5 chặng, mỗi chặng ≥ 1 span | **Chưa chạy được**; `artifacts/demo/` chỉ còn file của các lần chạy trước đây |
| Scenario 7 — bằng chứng đã commit (US3, FR-013a/14/15, SC-005/006) | `git status docs/`; mở bài tường thuật | `docs/demo-phase-1.md` + ảnh `docs/demo/` được track; không `.webm` dưới `docs/`; `artifacts/` bị ignore | **Vẫn thiếu bài tường thuật**: `docs/demo-phase-1.md` **không tồn tại** trên `master` (không đổi so với lượt trước). Còn đúng 4 ảnh (`01-catalog`, `02-basket`, `03-confirmation`, `04-basket-empty`); không `.webm` dưới `docs/`; `artifacts` bị ignore |
| Scenario 8 — cold start (FR-007a) | `reset.ps1` → `up.ps1` → `demo.ps1` | Tới màn xác nhận, không seed/sửa tay | **Chưa chạy** (demo bị chặn ở bước dọn giỏ). Phần dựng nguội `reset`+`up` đã đo lại ở QA 005 (16 giây / 164 giây) |
| Scenario 9 — nhánh lỗi dễ hiểu (FR-016) | Giỏ rỗng; dừng `orders-api` rồi chạy demo | Thông báo dễ hiểu, không đơn dở | **Chưa chạy** phần dừng `orders-api`. Ghi nhận: lần thất bại 401 ở Scenario 1 vẫn cho thông báo nêu rõ bước và mã lỗi |
| Ghi nhận thêm — cold start sau khi dựng | Thêm giỏ / thanh toán ngay sau khi tạo lại `gateway-api`/`orders-api` | Không lỗi | Lần `POST /bff/checkout` đầu tiên → `504` (`downstream-timeout`, `OrdersApi` không kịp trong ngân sách); thử lại ngay → `201` |
| Dọn dẹp | `docker compose -f docker-compose.yml -f docker-compose.demo.yml down` | | Đã `down`; dựng lại `docker-compose.local.yml` cho các bước làm tay ở trên |

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

Mỗi link mở thẳng đúng dòng khai báo hàm/`test()` (comment mỗi hàm đã gắn `Task nguồn: spec 006 ...` hoặc spec gốc, xem lại tại đó nếu cần biết test ứng với US/task nào). Test hậu tố `IntegrationTests` dùng Testcontainers → cần Docker Desktop đang chạy.

| Cần xác nhận | Test case (bấm để mở) | Lệnh chạy riêng |
|---|---|---|
| US2/FR-005 — `Order.PlaceFrom` bắt buộc tenant: ghi nhận tenant, từ chối tenant null/rỗng/trắng, vẫn từ chối đơn không có dòng | [`OrderTenantTests.cs:36`](../../services/orders/tests/Orders.Api.UnitTests/OrderTenantTests.cs#L36) · [`:56`](../../services/orders/tests/Orders.Api.UnitTests/OrderTenantTests.cs#L56) · [`:73`](../../services/orders/tests/Orders.Api.UnitTests/OrderTenantTests.cs#L73) | `dotnet test services/orders/tests/Orders.Api.UnitTests --filter "FullyQualifiedName~OrderTenantTests"` |
| US2/FR-005 — đơn lưu đúng tenant đã phân giải (trên dòng đã lưu, không phải response); tenant khai trong body bị bỏ qua | [`PlaceOrderTests.cs:183`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L183) · [`:213`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L213) | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter "FullyQualifiedName~PlaceOrderTests"` |
| US2/FR-005a — đọc lại đơn ở service Orders thấy `tenantId` không rỗng và đúng tenant | [`OrderEndpointsTests.cs:37`](../../services/orders/tests/Orders.Api.IntegrationTests/OrderEndpointsTests.cs#L37) · [`:75`](../../services/orders/tests/Orders.Api.IntegrationTests/OrderEndpointsTests.cs#L75) · [`:110`](../../services/orders/tests/Orders.Api.IntegrationTests/OrderEndpointsTests.cs#L110) | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter "FullyQualifiedName~OrderEndpointsTests"` |
| US2/FR-006, SC-008 — không tenant thì ghi/đọc đơn thất bại và **không tạo dòng nào** (assert số dòng không đổi) | [`TenantEnforcementTests.cs:46`](../../services/orders/tests/Orders.Api.IntegrationTests/TenantEnforcementTests.cs#L46) · [`:69`](../../services/orders/tests/Orders.Api.IntegrationTests/TenantEnforcementTests.cs#L69) · [`:92`](../../services/orders/tests/Orders.Api.IntegrationTests/TenantEnforcementTests.cs#L92) | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter "FullyQualifiedName~TenantEnforcementTests"` |
| T029 (research Decision 4) — BFF vẫn đọc được đơn từ Orders sau khi Orders thêm `tenantId` (hình dạng 3 trường kiểm thủ công, xem bên dưới) | [`OrdersRouteTests.cs:28`](../../services/bff/tests/Bff.Api.IntegrationTests/OrdersRouteTests.cs#L28) · [`:62`](../../services/bff/tests/Bff.Api.IntegrationTests/OrdersRouteTests.cs#L62) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter "FullyQualifiedName~OrdersRouteTests"` |
| US1/US3 — **bài demo chính**: walkthrough Playwright trên container, ảnh từng bước vào `docs/demo/`, đọc lại đơn và so mã/tổng (chạy qua script, không chạy trực tiếp) | [`order-demo.spec.ts:65`](../../frontend/apps/web/demo/order-demo.spec.ts#L65) | `./scripts/demo.ps1` (hoặc `demo.sh`); lặp lại khi stack đã ở demo mode: `./scripts/demo.ps1 -SkipStart` |

**Kết quả lượt QA này (2026-09-27)**: `OrderTenantTests`+`OrderTotalTests` **14/14** · Orders integration (`OrderEndpointsTests`+`PlaceOrderTests`+`TenantEnforcementTests`) **14/14** (1 lần chạy trước đó đỏ 1/14 do Testcontainers tranh chấp tài nguyên khi 2 stack Docker cùng chạy — chạy lại riêng thì 14/14 xanh, không phải hồi quy) ·
BFF `OrdersRouteTests` **2/2** · `./scripts/demo.ps1`: **FAIL** (`exit 1`, HTTP 401 ở bước dọn giỏ, **152 giây** — không đổi so với lượt trước).
## Kết luận

**FAIL** đối với bài demo một lệnh — sản phẩm chính của spec 006: `./scripts/demo.ps1` vẫn không chạy được trên nền tảng hiện tại (401 ở bước dọn giỏ, không có mã lấy token), và bài tường thuật viết tay `docs/demo-phase-1.md` (US3, FR-014/015, SC-005/006) vẫn không có trong repository — cả hai không đổi so với lượt QA trước.
Phần backend của 006 (lưu và trả `tenantId` trên đơn, từ chối khi không có tenant và không tạo đơn) **đạt**: 14+14+2 test PASS và đã kiểm chứng lại tay trên stack thật (`tenantId: "contoso"`, BFF chỉ trả 3 trường, không tenant → `500` và số đơn không đổi 14→14). Chi tiết: [QA_Debt.md](QA_Debt.md).
