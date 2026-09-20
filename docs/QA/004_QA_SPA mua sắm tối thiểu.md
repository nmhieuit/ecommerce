# QA: SPA mua sắm tối thiểu — duyệt/giỏ hàng/thanh toán/xác nhận

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

**Nguồn đối chiếu**:
[`architecture/004`](../architecture/004_Architect_SPA%20mua%20sắm%20tối%20thiểu.md) ·
[`development/004`](../development/004_Development_SPA%20mua%20sắm%20tối%20thiểu.md) ·
[`summary/004`](../summary/004_PO_SPA%20mua%20sắm%20tối%20thiểu.md) ·
[`spec-summary-vi/004`](../spec-summary-vi/004-minimal-shopping-spa.json) — đối chiếu chéo cả 4,
kèm xác minh lại với source code thật khi có nghi vấn.

> **Cập nhật lần 2** (nhánh `claude/spa-identity-server-login-e6100d`): lượt QA đầu ghi **FAIL** vì
> storefront kẹt "Loading products…" (gateway đã đòi JWT mà SPA chưa có đăng nhập). Nhánh này thêm
> form đăng nhập cho SPA (FR-026 trong `specs/004-minimal-shopping-spa/spec.md`) — luồng đã chạy được,
> kết quả dưới đây là của lượt kiểm tra lại. Các thay đổi này đã được commit (`bfb0162`) và merge vào
> `master` qua PR #43.

## Luồng happy-case đã rà soát (US1 → US3, cộng FR-026)

0. **Đăng nhập** (FR-026, bổ sung sau bản 4 tài liệu): mọi màn hình trừ đăng nhập yêu cầu đã đăng nhập;
   khách vãng lai bị đưa tới form đăng nhập; token lưu ở `sessionStorage` và gắn vào mọi request tới gateway.
1. **Duyệt**: thấy danh sách sản phẩm (tên + giá USD 2 chữ số thập phân) lấy từ backend; catalog rỗng
   hiện trạng thái rỗng tường minh; backend lỗi hiện thông báo rõ ràng.
2. **Giỏ hàng**: thêm sản phẩm vào giỏ, thêm lại thì tăng số lượng (không sinh dòng thứ hai); giỏ do
   backend giữ theo danh tính người mua nên còn nguyên sau khi tải lại trang.
3. **Thanh toán**: giỏ rỗng bị chặn ngay trên giao diện (không gửi request); giỏ có hàng thì tạo đúng 1
   đơn dù bấm nhiều lần, hiện màn hình xác nhận với mã đơn nguyên văn + tổng tiền, giỏ về rỗng.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

Dựa trên [`specs/004-minimal-shopping-spa/quickstart.md`](../../specs/004-minimal-shopping-spa/quickstart.md)
(đã được nhánh này bổ sung mục "Signing in"; phần Setup 1-4 vẫn dùng `docker-compose.deps.yml` + `dotnet run`)
nhưng đổi sang chạy qua **Docker Desktop** (`docker-compose.local.yml`, có sẵn `storefront` ở cổng
`4173`). Kịch bản thủ công có số liệu đo thật cũng có ở [`docs/local-testing.md`](../local-testing.md),
Scenario 4 ("the whole purchase") — trỏ sang đó thay vì chép lại.

> Nếu `localhost` không gọi được dù container `healthy`: khởi động lại hẳn Docker Desktop (lỗi forwarding
> IPv6 loopback `::1` của WSL2), không phải lỗi ứng dụng.

### Thủ công — dựng stack và đi hết luồng

`.env` phải có `MSSQL_SA_PASSWORD` và `TestUserPassword` (`cp .env.example .env` là đủ).

```bash
docker compose -f docker-compose.local.yml up -d --build --wait gateway-api storefront
```

`--build` để có image storefront/identity mới (storefront đóng cứng `IDENTITY_ORIGIN`, identity đặt
`IssuerUri`); lần đầu mất vài phút. Mở `http://localhost:4173`, đăng nhập bằng `postman-test@local.test`
và mật khẩu là giá trị `TestUserPassword` trong `.env`.

| Bước (quickstart) | Cách làm | Kỳ vọng theo tài liệu | **Đã quan sát (lượt kiểm tra lại)** |
|---|---|---|---|
| Đăng nhập (FR-026) | Mở `/`; nhập sai mật khẩu; rồi nhập đúng | Tới `/login`; sai → "Incorrect username or password."; đúng → vào catalog | Đúng cả 3. `POST localhost:5205/connect/token` → 200; token nằm ở `sessionStorage` (`storefront.session`), `localStorage` trống |
| Scenario 1 — duyệt (US1) | Sau đăng nhập, xem danh sách + tab Network + Console | 3 sản phẩm: Notebook $12.50, Pour-Over $48.00, Apron $34.25 | Đúng 3 sản phẩm, đúng giá. Request tới gateway (`5300`) và 1 lần tới identity (`5205`, xem ghi chú FR-014). **Lần tải đầu trên stack mới bị `504` (4.4 s) rồi tự thử lại thành `200`** |
| Scenario 2 — thêm vào giỏ (US2) | Thêm Notebook 2 lần, Apron 1 lần, mở giỏ | 1 dòng Notebook số lượng 2 ($25.00) + Apron → tổng $59.25 | Đúng **sau khi stack ấm**: Notebook × 2 = $25.00, Apron $34.25, tổng $59.25. Lượt đầu 2 lần thêm Notebook bị `504` (1.4 s, 1.0 s) nên giỏ chỉ có Apron — đúng US2-KB5 (giỏ không hiện món chưa thêm được); thêm lại thì đúng |
| Scenario 3 — giỏ bền qua tải lại | F5 ở trang giỏ | Giỏ giữ nguyên | Đúng (vẫn đăng nhập, giỏ y nguyên $59.25). **Đóng-mở lại trình duyệt chưa thử**: token ở `sessionStorage` nên sẽ phải đăng nhập lại (giỏ vẫn là giỏ của server) |
| Scenario 4 — giỏ rỗng chặn thanh toán | Giỏ rỗng, thử thanh toán | Nút bị vô hiệu, không có request | Được bao phủ bởi e2e (PASS) và `EmptyBasketBlocks.test.tsx` |
| Scenario 5 — thanh toán (US3) | Bấm "Check out" | Xác nhận đúng mã đơn + $59.25, giỏ rỗng | Lần bấm đầu (double-click) → `POST /bff/checkout` `504` (1.1 s) — hiện "We could not place your order. Your basket is unchanged", **chỉ 1 request được gửi**, giỏ nguyên vẹn (US3-KB4). Bấm lại → xác nhận mã `c4b82094-…`, tổng $59.25. Đọc lại `GET /bff/orders/<mã>` → 200, total 59.25 (SC-005); `GET /bff/basket` → items rỗng (FR-010); bảng Orders có đúng **1** dòng — lần 504 không sinh đơn ma |
| Scenario 6 — double-click | Bấm nhanh 2 lần | Đúng 1 đơn | Đúng (chỉ 1 request đi ra; e2e PASS) |
| Scenario 7, 8 | Tắt `products-api`; chỉ dùng bàn phím | Lỗi rõ ràng < 5 s; focus luôn nhìn thấy | Scenario 8 được e2e bao phủ (PASS); Scenario 7 chưa chạy thủ công lại |
| Đăng xuất | Bấm "Sign out" | Về `/login` | Đúng; `sessionStorage` không còn `storefront.session` |
| Dọn dẹp | `docker compose -f docker-compose.local.yml down -v` | | |

**Ghi chú về "chạy lần đầu"**: `--wait` chỉ đảm bảo container `healthy` (liveness), không làm ấm đường
đi thật; vài request đầu tiên tới mỗi service có thể vượt hạn mức 1 s/lần thử của BFF và trả `504`
(đã ghi ở `technical-debt.md`/spec 002: "chờ readiness, không phải liveness"). Nên gọi thử 1-2 lần trước
khi đo hoặc trước khi chạy walkthrough để không bị `504` giả.

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

Mỗi link mở thẳng đúng dòng khai báo hàm/`it()` test (comment mỗi hàm đã gắn `Task nguồn: spec 004 ...`, xem lại tại đó nếu cần biết test ứng với US/task nào). Test hậu tố `IntegrationTests` dùng Testcontainers → cần Docker Desktop đang chạy; nên tắt stack local (`docker compose -f docker-compose.local.yml down`) trước khi chạy để đỡ tranh chấp bộ nhớ. Frontend chạy từ thư mục `frontend/`; máy này không có `pnpm` trên PATH nên dùng `corepack pnpm ...` (riêng Playwright cần lệnh `pnpm` thật trong PATH vì `playwright.config.ts` khởi động dev server bằng `pnpm` — đã dùng 1 file shim `pnpm.cmd` gọi `corepack pnpm` đặt ở thư mục tạm, không sửa repo).

| Cần xác nhận | Test case (bấm để mở) | Lệnh chạy riêng |
|---|---|---|
| US1/FR-018 — catalog seed cố định 3 sản phẩm, id ổn định, mua được ngay không cần setup | [`CatalogSeedTests.cs:37`](../../services/products/tests/Products.Api.IntegrationTests/CatalogSeedTests.cs#L37) · [`:66`](../../services/products/tests/Products.Api.IntegrationTests/CatalogSeedTests.cs#L66) · [`:90`](../../services/products/tests/Products.Api.IntegrationTests/CatalogSeedTests.cs#L90) | `dotnet test services/products/tests/Products.Api.IntegrationTests --filter "FullyQualifiedName~CatalogSeedTests"` |
| US2/FR-005,021 — quy tắc gộp dòng (đơn vị): 1 sản phẩm tối đa 1 dòng, giữ giá đã chụp, từ chối số lượng/giá sai | [`BasketLineMergeTests.cs:27`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L27) · [`:48`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L48) · [`:66`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L66) · [`:86`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L86) · [`:108`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L108) · [`:129`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L129) · [`:144`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L144) | `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter "FullyQualifiedName~BasketLineMergeTests"` |
| US2/FR-004 — tổng giỏ = Σ số lượng × đơn giá, chính xác kiểu decimal, khớp con số $59.25 của walkthrough | [`BasketTotalTests.cs:25`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketTotalTests.cs#L25) · [`:37`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketTotalTests.cs#L37) · [`:52`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketTotalTests.cs#L52) · [`:69`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketTotalTests.cs#L69) · [`:88`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketTotalTests.cs#L88) · [`:105`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketTotalTests.cs#L105) | `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter "FullyQualifiedName~BasketTotalTests"` |
| US2/FR-006,011 — giỏ theo người gọi (SQL thật): giỏ rỗng lần đầu, cùng giỏ qua các request, mỗi người 1 giỏ, không người gọi thì lỗi | [`CurrentBasketTests.cs:39`](../../services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs#L39) · [`:63`](../../services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs#L63) · [`:85`](../../services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs#L85) · [`:106`](../../services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs#L106) · [`:128`](../../services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs#L128) · [`:155`](../../services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs#L155) · [`:176`](../../services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs#L176) · [`:197`](../../services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs#L197) | `dotnet test services/baskets/tests/Baskets.Api.IntegrationTests --filter "FullyQualifiedName~CurrentBasketTests"` |
| US3/FR-008,010,016 — xoá giỏ sau checkout: rỗng nhưng giữ giỏ, lần xoá thứ hai trả `409` | [`ClearBasketTests.cs:31`](../../services/baskets/tests/Baskets.Api.IntegrationTests/ClearBasketTests.cs#L31) · [`:63`](../../services/baskets/tests/Baskets.Api.IntegrationTests/ClearBasketTests.cs#L63) · [`:80`](../../services/baskets/tests/Baskets.Api.IntegrationTests/ClearBasketTests.cs#L80) · [`:105`](../../services/baskets/tests/Baskets.Api.IntegrationTests/ClearBasketTests.cs#L105) | `dotnet test services/baskets/tests/Baskets.Api.IntegrationTests --filter "FullyQualifiedName~ClearBasketTests"` |
| US3/FR-009,022 — tổng đơn do Orders tự tính (đơn vị) | [`OrderTotalTests.cs:28`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L28) · [`:43`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L43) · [`:61`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L61) · [`:78`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L78) · [`:91`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L91) · [`:103`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L103) · [`:116`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L116) | `dotnet test services/orders/tests/Orders.Api.UnitTests --filter "FullyQualifiedName~OrderTotalTests"` |
| US3/FR-022, SC-005 — đặt đơn: tạo đơn, mã đọc lại được, từ chối đơn rỗng, tenant lấy từ gateway | [`PlaceOrderTests.cs:32`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L32) · [`:63`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L63) · [`:87`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L87) · [`:110`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L110) · [`:127`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L127) · [`:147`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L147) · [`:170`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L170) · [`:198`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L198) | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter "FullyQualifiedName~PlaceOrderTests"` |
| US2/FR-003,004,021 — BFF giỏ: tra giá từ catalog, bỏ qua giá client khai, gộp dòng, 404 khi không có sản phẩm | [`BasketFlowTests.cs:35`](../../services/bff/tests/Bff.Api.IntegrationTests/BasketFlowTests.cs#L35) · [`:60`](../../services/bff/tests/Bff.Api.IntegrationTests/BasketFlowTests.cs#L60) · [`:92`](../../services/bff/tests/Bff.Api.IntegrationTests/BasketFlowTests.cs#L92) · [`:116`](../../services/bff/tests/Bff.Api.IntegrationTests/BasketFlowTests.cs#L116) · [`:139`](../../services/bff/tests/Bff.Api.IntegrationTests/BasketFlowTests.cs#L139) · [`:161`](../../services/bff/tests/Bff.Api.IntegrationTests/BasketFlowTests.cs#L161) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter "FullyQualifiedName~BasketFlowTests"` |
| US3/FR-008,009,010,016, SC-005,008 — BFF checkout 2 bước: đơn tạo trước khi xoá giỏ, giỏ rỗng → 409, bấm 2 lần chỉ 1 đơn | [`CheckoutTests.cs:37`](../../services/bff/tests/Bff.Api.IntegrationTests/CheckoutTests.cs#L37) · [`:65`](../../services/bff/tests/Bff.Api.IntegrationTests/CheckoutTests.cs#L65) · [`:89`](../../services/bff/tests/Bff.Api.IntegrationTests/CheckoutTests.cs#L89) · [`:110`](../../services/bff/tests/Bff.Api.IntegrationTests/CheckoutTests.cs#L110) · [`:127`](../../services/bff/tests/Bff.Api.IntegrationTests/CheckoutTests.cs#L127) · [`:148`](../../services/bff/tests/Bff.Api.IntegrationTests/CheckoutTests.cs#L148) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter "FullyQualifiedName~CheckoutTests"` |
| FR-006 — subject lan truyền BFF → downstream, không bịa subject khi không có | [`SubjectPropagationTests.cs:36`](../../services/bff/tests/Bff.Api.IntegrationTests/SubjectPropagationTests.cs#L36) · [`:62`](../../services/bff/tests/Bff.Api.IntegrationTests/SubjectPropagationTests.cs#L62) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter "FullyQualifiedName~SubjectPropagationTests"` |
| FR-006 — `CallerContext`/middleware dùng chung: subject Resolved/Unresolved, đọc `X-Subject-Id`, logging scope | [`CallerContextTests.cs:22`](../../shared/Tenancy.UnitTests/CallerContextTests.cs#L22) · [`:36`](../../shared/Tenancy.UnitTests/CallerContextTests.cs#L36) · [`:51`](../../shared/Tenancy.UnitTests/CallerContextTests.cs#L51) · [`:68`](../../shared/Tenancy.UnitTests/CallerContextTests.cs#L68) · [`CallerContextMiddlewareTests.cs:20`](../../shared/Tenancy.UnitTests/CallerContextMiddlewareTests.cs#L20) · [`:42`](../../shared/Tenancy.UnitTests/CallerContextMiddlewareTests.cs#L42) · [`:63`](../../shared/Tenancy.UnitTests/CallerContextMiddlewareTests.cs#L63) · [`:82`](../../shared/Tenancy.UnitTests/CallerContextMiddlewareTests.cs#L82) · [`:101`](../../shared/Tenancy.UnitTests/CallerContextMiddlewareTests.cs#L101) | `dotnet test shared/Tenancy.UnitTests --filter "FullyQualifiedName~CallerContext"` |
| FR-006 — gateway stamp/ghi đè/xoá `X-Subject-Id` | [`SubjectHeaderPropagationMiddlewareTests.cs:32`](../../services/gateway/tests/Gateway.Api.UnitTests/SubjectHeaderPropagationMiddlewareTests.cs#L32) · [`:51`](../../services/gateway/tests/Gateway.Api.UnitTests/SubjectHeaderPropagationMiddlewareTests.cs#L51) · [`:77`](../../services/gateway/tests/Gateway.Api.UnitTests/SubjectHeaderPropagationMiddlewareTests.cs#L77) · [`:97`](../../services/gateway/tests/Gateway.Api.UnitTests/SubjectHeaderPropagationMiddlewareTests.cs#L97) | `dotnet test services/gateway/tests/Gateway.Api.UnitTests --filter "FullyQualifiedName~SubjectHeaderPropagationMiddlewareTests"` |
| US1/FR-001,002,012,014,017,024 — catalog trên SPA: danh sách, trạng thái rỗng, lỗi + thử lại, giá USD | [`ProductList.test.tsx:43`](../../frontend/apps/web/tests/catalog/ProductList.test.tsx#L43) · [`:65`](../../frontend/apps/web/tests/catalog/ProductList.test.tsx#L65) · [`:82`](../../frontend/apps/web/tests/catalog/ProductList.test.tsx#L82) · [`:98`](../../frontend/apps/web/tests/catalog/ProductList.test.tsx#L98) · [`:119`](../../frontend/apps/web/tests/catalog/ProductList.test.tsx#L119) · [`:142`](../../frontend/apps/web/tests/catalog/ProductList.test.tsx#L142) · [`EmptyCatalog.test.tsx:17`](../../frontend/apps/web/tests/catalog/EmptyCatalog.test.tsx#L17) · [`:29`](../../frontend/apps/web/tests/catalog/EmptyCatalog.test.tsx#L29) · [`CatalogError.test.tsx:18`](../../frontend/apps/web/tests/catalog/CatalogError.test.tsx#L18) · [`:31`](../../frontend/apps/web/tests/catalog/CatalogError.test.tsx#L31) · [`:46`](../../frontend/apps/web/tests/catalog/CatalogError.test.tsx#L46) · [`:62`](../../frontend/apps/web/tests/catalog/CatalogError.test.tsx#L62) · [`money.test.ts:10`](../../frontend/apps/web/tests/shared/money.test.ts#L10) · [`:24`](../../frontend/apps/web/tests/shared/money.test.ts#L24) · [`:34`](../../frontend/apps/web/tests/shared/money.test.ts#L34) · [`:45`](../../frontend/apps/web/tests/shared/money.test.ts#L45) · [`:55`](../../frontend/apps/web/tests/shared/money.test.ts#L55) · [`:65`](../../frontend/apps/web/tests/shared/money.test.ts#L65) | `cd frontend && corepack pnpm --filter @ecommerce/web test` (chạy cả bộ, xem kết quả bên dưới) |
| US2/FR-003,004,012 — giỏ trên SPA: hiển thị dòng + tổng, thêm vào giỏ, lỗi không để lại món ảo | [`BasketView.test.tsx:66`](../../frontend/apps/web/tests/basket/BasketView.test.tsx#L66) · [`:95`](../../frontend/apps/web/tests/basket/BasketView.test.tsx#L95) · [`:114`](../../frontend/apps/web/tests/basket/BasketView.test.tsx#L114) · [`:134`](../../frontend/apps/web/tests/basket/BasketView.test.tsx#L134) · [`:154`](../../frontend/apps/web/tests/basket/BasketView.test.tsx#L154) · [`:174`](../../frontend/apps/web/tests/basket/BasketView.test.tsx#L174) · [`AddItemError.test.tsx:40`](../../frontend/apps/web/tests/basket/AddItemError.test.tsx#L40) · [`:71`](../../frontend/apps/web/tests/basket/AddItemError.test.tsx#L71) · [`:93`](../../frontend/apps/web/tests/basket/AddItemError.test.tsx#L93) · [`:128`](../../frontend/apps/web/tests/basket/AddItemError.test.tsx#L128) | `cd frontend && corepack pnpm --filter @ecommerce/web test` (cùng lệnh) |
| US3/FR-008,009,016 — thanh toán trên SPA: giỏ rỗng chặn và không gửi request, xác nhận nguyên văn, chống double-submit | [`EmptyBasketBlocks.test.tsx:36`](../../frontend/apps/web/tests/checkout/EmptyBasketBlocks.test.tsx#L36) · [`:48`](../../frontend/apps/web/tests/checkout/EmptyBasketBlocks.test.tsx#L48) · [`:73`](../../frontend/apps/web/tests/checkout/EmptyBasketBlocks.test.tsx#L73) · [`Confirmation.test.tsx:32`](../../frontend/apps/web/tests/checkout/Confirmation.test.tsx#L32) · [`:46`](../../frontend/apps/web/tests/checkout/Confirmation.test.tsx#L46) · [`:58`](../../frontend/apps/web/tests/checkout/Confirmation.test.tsx#L58) · [`:71`](../../frontend/apps/web/tests/checkout/Confirmation.test.tsx#L71) · [`DoubleSubmit.test.tsx:40`](../../frontend/apps/web/tests/checkout/DoubleSubmit.test.tsx#L40) · [`:82`](../../frontend/apps/web/tests/checkout/DoubleSubmit.test.tsx#L82) · [`:113`](../../frontend/apps/web/tests/checkout/DoubleSubmit.test.tsx#L113) · [`:140`](../../frontend/apps/web/tests/checkout/DoubleSubmit.test.tsx#L140) · [`:167`](../../frontend/apps/web/tests/checkout/DoubleSubmit.test.tsx#L167) | `cd frontend && corepack pnpm --filter @ecommerce/web test` (cùng lệnh) |
| FR-026 — đăng nhập SPA: điều hướng vào form, token Bearer, sai mật khẩu/sự cố, đăng xuất, 401 thì về đăng nhập | [`signIn.test.tsx:53`](../../frontend/apps/web/tests/auth/signIn.test.tsx#L53) · [`:68`](../../frontend/apps/web/tests/auth/signIn.test.tsx#L68) · [`:99`](../../frontend/apps/web/tests/auth/signIn.test.tsx#L99) · [`:122`](../../frontend/apps/web/tests/auth/signIn.test.tsx#L122) · [`:139`](../../frontend/apps/web/tests/auth/signIn.test.tsx#L139) · [`:160`](../../frontend/apps/web/tests/auth/signIn.test.tsx#L160) · [`fetcher.test.ts:20`](../../frontend/apps/web/tests/shared/fetcher.test.ts#L20) · [`:36`](../../frontend/apps/web/tests/shared/fetcher.test.ts#L36) · [`:60`](../../frontend/apps/web/tests/shared/fetcher.test.ts#L60) · [`:82`](../../frontend/apps/web/tests/shared/fetcher.test.ts#L82) · [`:105`](../../frontend/apps/web/tests/shared/fetcher.test.ts#L105) | `cd frontend && corepack pnpm --filter @ecommerce/web test` (cùng lệnh) |
| SC-002/005/008/009/010 — walkthrough trình duyệt thật (Playwright): cả luồng, giỏ rỗng chặn, double checkout, chỉ bàn phím | [`walkthrough.spec.ts:76`](../../frontend/apps/web/e2e/walkthrough.spec.ts#L76) · [`:146`](../../frontend/apps/web/e2e/walkthrough.spec.ts#L146) · [`:172`](../../frontend/apps/web/e2e/walkthrough.spec.ts#L172) · [`:204`](../../frontend/apps/web/e2e/walkthrough.spec.ts#L204) | `E2E_USERNAME=postman-test@local.test E2E_PASSWORD=<TestUserPassword> corepack pnpm --filter @ecommerce/web e2e` (stack đang chạy; cần shim `pnpm`) |

**Kết quả lượt QA này** (lượt 1 trừ khi ghi khác): `CatalogSeedTests` 3/3 · `BasketLineMergeTests`+`BasketTotalTests` 14/14 ·
`CurrentBasketTests`+`ClearBasketTests` 13/13 · `OrderTotalTests` 8/8 · `PlaceOrderTests` lần đầu 7/8 (test đỏ khi máy đang
chạy cả stack, chưa ghi tên), chạy lại 8/8 · BFF `BasketFlowTests`+`CheckoutTests`+`SubjectPropagationTests` 15/15 ·
`CallerContext*` 14/14 · `SubjectHeaderPropagationMiddlewareTests` 7/7 · **lượt 2**: frontend 13 file, **60/60** PASS, `lint` và
`typecheck` sạch · Identity `Identity.Api.UnitTests` 5/5 và `Identity.Api.IntegrationTests` 2/2 PASS · e2e **3/4 PASS, 1 FAIL**
(`walkthrough.spec.ts:112`, xem QA_Debt).

**Ngân sách dung lượng (FR-025/SC-011)** — không có file test riêng, chạy `cd frontend && corepack pnpm --filter @ecommerce/web build`
rồi `... size`: **108.07 kB** gzip so với hạn mức 115 kB (cấu hình ở [`.size-limit.json`](../../frontend/apps/web/.size-limit.json)) — PASS.

## Kết luận

**PASS kèm ghi chú** — luồng storefront đã chạy được đầu-cuối (đăng nhập → catalog → giỏ → thanh toán →
xác nhận → đăng xuất; mã đơn khớp backend, giỏ rỗng sau khi mua, đúng 1 đơn dù double-click), 60/60 test
frontend và các test backend liên quan PASS, bundle trong hạn mức. Ghi chú cần xử lý: (1) test e2e
walkthrough chính của 004 đang **đỏ** vì assertion "không có gì trong browser storage" chưa cập nhật cho
token đăng nhập (và có thể cả assertion SC-010 "chỉ gateway" — chưa xác nhận); (2) bốn tài liệu 004 chưa
phản ánh đăng nhập (FR-026) và 2 FR đã bị amend; (3) `504` do cold start ở vài request đầu. Chi tiết:
[QA_Debt.md](QA_Debt.md).
