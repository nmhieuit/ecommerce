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
| US1/FR-018 — catalog seed cố định 3 sản phẩm, id ổn định, mua được ngay không cần setup | [`CatalogSeedTests.cs:37`](../../services/products/tests/Products.Api.IntegrationTests/CatalogSeedTests.cs#L37) · [`:70`](../../services/products/tests/Products.Api.IntegrationTests/CatalogSeedTests.cs#L70) · [`:100`](../../services/products/tests/Products.Api.IntegrationTests/CatalogSeedTests.cs#L100) | `dotnet test services/products/tests/Products.Api.IntegrationTests --filter "FullyQualifiedName~CatalogSeedTests"` |
| US2/FR-005,021 — quy tắc gộp dòng (đơn vị): 1 sản phẩm tối đa 1 dòng, giữ giá đã chụp, từ chối số lượng/giá sai | [`BasketLineMergeTests.cs:27`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L27) · [`:51`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L51) · [`:71`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L71) · [`:92`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L92) · [`:116`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L116) · [`:140`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L140) · [`:157`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs#L157) | `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter "FullyQualifiedName~BasketLineMergeTests"` |
| US2/FR-004 — tổng giỏ = Σ số lượng × đơn giá, chính xác kiểu decimal, khớp con số $59.25 của walkthrough | [`BasketTotalTests.cs:25`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketTotalTests.cs#L25) · [`:38`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketTotalTests.cs#L38) · [`:55`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketTotalTests.cs#L55) · [`:74`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketTotalTests.cs#L74) · [`:95`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketTotalTests.cs#L95) · [`:114`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketTotalTests.cs#L114) | `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter "FullyQualifiedName~BasketTotalTests"` |
| US2/FR-006,011 — giỏ theo người gọi (SQL thật): giỏ rỗng lần đầu, cùng giỏ qua các request, mỗi người 1 giỏ, không người gọi thì lỗi | [`CurrentBasketTests.cs:39`](../../services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs#L39) · [`:70`](../../services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs#L70) · [`:97`](../../services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs#L97) · [`:123`](../../services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs#L123) · [`:152`](../../services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs#L152) · [`:183`](../../services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs#L183) · [`:206`](../../services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs#L206) · [`:229`](../../services/baskets/tests/Baskets.Api.IntegrationTests/CurrentBasketTests.cs#L229) | `dotnet test services/baskets/tests/Baskets.Api.IntegrationTests --filter "FullyQualifiedName~CurrentBasketTests"` |
| US3/FR-008,010,016 — xoá giỏ sau checkout: rỗng nhưng giữ giỏ, lần xoá thứ hai trả `409` | [`ClearBasketTests.cs:31`](../../services/baskets/tests/Baskets.Api.IntegrationTests/ClearBasketTests.cs#L31) · [`:71`](../../services/baskets/tests/Baskets.Api.IntegrationTests/ClearBasketTests.cs#L71) · [`:90`](../../services/baskets/tests/Baskets.Api.IntegrationTests/ClearBasketTests.cs#L90) · [`:117`](../../services/baskets/tests/Baskets.Api.IntegrationTests/ClearBasketTests.cs#L117) | `dotnet test services/baskets/tests/Baskets.Api.IntegrationTests --filter "FullyQualifiedName~ClearBasketTests"` |
| US3/FR-009,022 — tổng đơn do Orders tự tính (đơn vị) | [`OrderTotalTests.cs:28`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L28) · [`:45`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L45) · [`:65`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L65) · [`:86`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L86) · [`:101`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L101) · [`:115`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L115) · [`:130`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs#L130) | `dotnet test services/orders/tests/Orders.Api.UnitTests --filter "FullyQualifiedName~OrderTotalTests"` |
| US3/FR-022, SC-005 — đặt đơn: tạo đơn, mã đọc lại được, từ chối đơn rỗng, tenant lấy từ gateway | [`PlaceOrderTests.cs:32`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L32) · [`:68`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L68) · [`:93`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L93) · [`:118`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L118) · [`:136`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L136) · [`:158`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L158) · [`:183`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L183) · [`:213`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs#L213) | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter "FullyQualifiedName~PlaceOrderTests"` |
| US2/FR-003,004,021 — BFF giỏ: tra giá từ catalog, bỏ qua giá client khai, gộp dòng, 404 khi không có sản phẩm | [`BasketFlowTests.cs:35`](../../services/bff/tests/Bff.Api.IntegrationTests/BasketFlowTests.cs#L35) · [`:65`](../../services/bff/tests/Bff.Api.IntegrationTests/BasketFlowTests.cs#L65) · [`:99`](../../services/bff/tests/Bff.Api.IntegrationTests/BasketFlowTests.cs#L99) · [`:127`](../../services/bff/tests/Bff.Api.IntegrationTests/BasketFlowTests.cs#L127) · [`:153`](../../services/bff/tests/Bff.Api.IntegrationTests/BasketFlowTests.cs#L153) · [`:177`](../../services/bff/tests/Bff.Api.IntegrationTests/BasketFlowTests.cs#L177) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter "FullyQualifiedName~BasketFlowTests"` |
| US3/FR-008,009,010,016, SC-005,008 — BFF checkout 2 bước: đơn tạo trước khi xoá giỏ, giỏ rỗng → 409, bấm 2 lần chỉ 1 đơn | [`CheckoutTests.cs:37`](../../services/bff/tests/Bff.Api.IntegrationTests/CheckoutTests.cs#L37) · [`:70`](../../services/bff/tests/Bff.Api.IntegrationTests/CheckoutTests.cs#L70) · [`:95`](../../services/bff/tests/Bff.Api.IntegrationTests/CheckoutTests.cs#L95) · [`:118`](../../services/bff/tests/Bff.Api.IntegrationTests/CheckoutTests.cs#L118) · [`:136`](../../services/bff/tests/Bff.Api.IntegrationTests/CheckoutTests.cs#L136) · [`:160`](../../services/bff/tests/Bff.Api.IntegrationTests/CheckoutTests.cs#L160) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter "FullyQualifiedName~CheckoutTests"` |
| FR-006 — subject lan truyền BFF → downstream, không bịa subject khi không có | [`SubjectPropagationTests.cs:36`](../../services/bff/tests/Bff.Api.IntegrationTests/SubjectPropagationTests.cs#L36) · [`:65`](../../services/bff/tests/Bff.Api.IntegrationTests/SubjectPropagationTests.cs#L65) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter "FullyQualifiedName~SubjectPropagationTests"` |
| FR-006 — `CallerContext`/middleware dùng chung: subject Resolved/Unresolved, đọc `X-Subject-Id`, logging scope | [`CallerContextTests.cs:22`](../../shared/Tenancy.UnitTests/CallerContextTests.cs#L22) · [`:38`](../../shared/Tenancy.UnitTests/CallerContextTests.cs#L38) · [`:53`](../../shared/Tenancy.UnitTests/CallerContextTests.cs#L53) · [`:72`](../../shared/Tenancy.UnitTests/CallerContextTests.cs#L72) · [`CallerContextMiddlewareTests.cs:20`](../../shared/Tenancy.UnitTests/CallerContextMiddlewareTests.cs#L20) · [`:44`](../../shared/Tenancy.UnitTests/CallerContextMiddlewareTests.cs#L44) · [`:68`](../../shared/Tenancy.UnitTests/CallerContextMiddlewareTests.cs#L68) · [`:91`](../../shared/Tenancy.UnitTests/CallerContextMiddlewareTests.cs#L91) · [`:111`](../../shared/Tenancy.UnitTests/CallerContextMiddlewareTests.cs#L111) | `dotnet test shared/Tenancy.UnitTests --filter "FullyQualifiedName~CallerContext"` |
| FR-006 — gateway stamp/ghi đè/xoá `X-Subject-Id` | [`SubjectHeaderPropagationMiddlewareTests.cs:32`](../../services/gateway/tests/Gateway.Api.UnitTests/SubjectHeaderPropagationMiddlewareTests.cs#L32) · [`:53`](../../services/gateway/tests/Gateway.Api.UnitTests/SubjectHeaderPropagationMiddlewareTests.cs#L53) · [`:82`](../../services/gateway/tests/Gateway.Api.UnitTests/SubjectHeaderPropagationMiddlewareTests.cs#L82) · [`:104`](../../services/gateway/tests/Gateway.Api.UnitTests/SubjectHeaderPropagationMiddlewareTests.cs#L104) | `dotnet test services/gateway/tests/Gateway.Api.UnitTests --filter "FullyQualifiedName~SubjectHeaderPropagationMiddlewareTests"` |
| US1/FR-001,002,012,014,017,024 — catalog trên SPA: danh sách, trạng thái rỗng, lỗi + thử lại, giá USD | [`ProductList.test.tsx:43`](../../frontend/apps/web/tests/catalog/ProductList.test.tsx#L43) · [`:66`](../../frontend/apps/web/tests/catalog/ProductList.test.tsx#L66) · [`:86`](../../frontend/apps/web/tests/catalog/ProductList.test.tsx#L86) · [`:105`](../../frontend/apps/web/tests/catalog/ProductList.test.tsx#L105) · [`:128`](../../frontend/apps/web/tests/catalog/ProductList.test.tsx#L128) · [`:154`](../../frontend/apps/web/tests/catalog/ProductList.test.tsx#L154) · [`EmptyCatalog.test.tsx:17`](../../frontend/apps/web/tests/catalog/EmptyCatalog.test.tsx#L17) · [`:31`](../../frontend/apps/web/tests/catalog/EmptyCatalog.test.tsx#L31) · [`CatalogError.test.tsx:18`](../../frontend/apps/web/tests/catalog/CatalogError.test.tsx#L18) · [`:34`](../../frontend/apps/web/tests/catalog/CatalogError.test.tsx#L34) · [`:50`](../../frontend/apps/web/tests/catalog/CatalogError.test.tsx#L50) · [`:68`](../../frontend/apps/web/tests/catalog/CatalogError.test.tsx#L68) · [`money.test.ts:17`](../../frontend/apps/web/tests/shared/money.test.ts#L17) · [`:32`](../../frontend/apps/web/tests/shared/money.test.ts#L32) · [`:44`](../../frontend/apps/web/tests/shared/money.test.ts#L44) · [`:58`](../../frontend/apps/web/tests/shared/money.test.ts#L58) · [`:70`](../../frontend/apps/web/tests/shared/money.test.ts#L70) · [`:81`](../../frontend/apps/web/tests/shared/money.test.ts#L81) | `cd frontend && corepack pnpm --filter @ecommerce/web test` (chạy cả bộ, xem kết quả bên dưới) |
| US2/FR-003,004,012 — giỏ trên SPA: hiển thị dòng + tổng, thêm vào giỏ, lỗi không để lại món ảo | [`BasketView.test.tsx:66`](../../frontend/apps/web/tests/basket/BasketView.test.tsx#L66) · [`:99`](../../frontend/apps/web/tests/basket/BasketView.test.tsx#L99) · [`:121`](../../frontend/apps/web/tests/basket/BasketView.test.tsx#L121) · [`:144`](../../frontend/apps/web/tests/basket/BasketView.test.tsx#L144) · [`:166`](../../frontend/apps/web/tests/basket/BasketView.test.tsx#L166) · [`:190`](../../frontend/apps/web/tests/basket/BasketView.test.tsx#L190) · [`AddItemError.test.tsx:40`](../../frontend/apps/web/tests/basket/AddItemError.test.tsx#L40) · [`:75`](../../frontend/apps/web/tests/basket/AddItemError.test.tsx#L75) · [`:100`](../../frontend/apps/web/tests/basket/AddItemError.test.tsx#L100) · [`:139`](../../frontend/apps/web/tests/basket/AddItemError.test.tsx#L139) | `cd frontend && corepack pnpm --filter @ecommerce/web test` (cùng lệnh) |
| US3/FR-008,009,016 — thanh toán trên SPA: giỏ rỗng chặn và không gửi request, xác nhận nguyên văn, chống double-submit | [`EmptyBasketBlocks.test.tsx:36`](../../frontend/apps/web/tests/checkout/EmptyBasketBlocks.test.tsx#L36) · [`:49`](../../frontend/apps/web/tests/checkout/EmptyBasketBlocks.test.tsx#L49) · [`:76`](../../frontend/apps/web/tests/checkout/EmptyBasketBlocks.test.tsx#L76) · [`Confirmation.test.tsx:32`](../../frontend/apps/web/tests/checkout/Confirmation.test.tsx#L32) · [`:47`](../../frontend/apps/web/tests/checkout/Confirmation.test.tsx#L47) · [`:60`](../../frontend/apps/web/tests/checkout/Confirmation.test.tsx#L60) · [`:75`](../../frontend/apps/web/tests/checkout/Confirmation.test.tsx#L75) · [`DoubleSubmit.test.tsx:40`](../../frontend/apps/web/tests/checkout/DoubleSubmit.test.tsx#L40) · [`:85`](../../frontend/apps/web/tests/checkout/DoubleSubmit.test.tsx#L85) · [`:119`](../../frontend/apps/web/tests/checkout/DoubleSubmit.test.tsx#L119) · [`:149`](../../frontend/apps/web/tests/checkout/DoubleSubmit.test.tsx#L149) · [`:178`](../../frontend/apps/web/tests/checkout/DoubleSubmit.test.tsx#L178) | `cd frontend && corepack pnpm --filter @ecommerce/web test` (cùng lệnh) |
| FR-026 — đăng nhập SPA: điều hướng vào form, token Bearer, sai mật khẩu/sự cố, đăng xuất, 401 thì về đăng nhập | [`signIn.test.tsx:53`](../../frontend/apps/web/tests/auth/signIn.test.tsx#L53) · [`:74`](../../frontend/apps/web/tests/auth/signIn.test.tsx#L74) · [`:108`](../../frontend/apps/web/tests/auth/signIn.test.tsx#L108) · [`:136`](../../frontend/apps/web/tests/auth/signIn.test.tsx#L136) · [`:156`](../../frontend/apps/web/tests/auth/signIn.test.tsx#L156) · [`:179`](../../frontend/apps/web/tests/auth/signIn.test.tsx#L179) · [`fetcher.test.ts:28`](../../frontend/apps/web/tests/shared/fetcher.test.ts#L28) · [`:55`](../../frontend/apps/web/tests/shared/fetcher.test.ts#L55) · [`:79`](../../frontend/apps/web/tests/shared/fetcher.test.ts#L79) · [`:101`](../../frontend/apps/web/tests/shared/fetcher.test.ts#L101) · [`:124`](../../frontend/apps/web/tests/shared/fetcher.test.ts#L124) | `cd frontend && corepack pnpm --filter @ecommerce/web test` (cùng lệnh) |
| SC-002/005/008/009/010 — walkthrough trình duyệt thật (Playwright): cả luồng, giỏ rỗng chặn, double checkout, chỉ bàn phím | [`walkthrough.spec.ts:78`](../../frontend/apps/web/e2e/walkthrough.spec.ts#L78) · [`:153`](../../frontend/apps/web/e2e/walkthrough.spec.ts#L153) · [`:182`](../../frontend/apps/web/e2e/walkthrough.spec.ts#L182) · [`:217`](../../frontend/apps/web/e2e/walkthrough.spec.ts#L217) | `E2E_USERNAME=postman-test@local.test E2E_PASSWORD=<TestUserPassword> corepack pnpm --filter @ecommerce/web e2e` (stack đang chạy; cần shim `pnpm`) |

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
