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
> kết quả dưới đây là của lượt kiểm tra lại. Các thay đổi này hiện **chưa commit** (nhánh chưa có commit
> nào trước `master`).

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

Test hậu tố `IntegrationTests` dùng Testcontainers → cần Docker Desktop đang chạy. Nên tắt stack local
(`docker compose -f docker-compose.local.yml down`) trước khi chạy để đỡ tranh chấp bộ nhớ (ở lượt 1, khi
để cả 2, suite Orders chạy 3m14s với 1 test đỏ tạm thời và suite BFF mất ~6 phút).

Frontend chạy từ thư mục `frontend/`; máy này không có `pnpm` trên PATH — dùng `corepack pnpm ...`
(riêng Playwright còn cần lệnh `pnpm` thật trong PATH vì `playwright.config.ts` khởi động dev server bằng
`pnpm`; đã dùng 1 file shim `pnpm.cmd` gọi `corepack pnpm` đặt ở thư mục tạm, không sửa repo).

| Cần xác nhận | Lệnh | Kết quả |
|---|---|---|
| US1 — catalog seed cố định (FR-018) | `dotnet test services/products/tests/Products.Api.IntegrationTests --filter CatalogSeedTests` | 3/3 PASS (lượt 1) |
| US2 — gộp dòng + tổng giỏ (đơn vị) | `dotnet test services/baskets/tests/Baskets.Api.UnitTests --filter "BasketLineMergeTests\|BasketTotalTests"` | 14/14 PASS (lượt 1) |
| US2 — giỏ theo người gọi + xoá giỏ (SQL thật) | `dotnet test services/baskets/tests/Baskets.Api.IntegrationTests --filter "CurrentBasketTests\|ClearBasketTests"` | 13/13 PASS (lượt 1) |
| US3 — tổng đơn (đơn vị) | `dotnet test services/orders/tests/Orders.Api.UnitTests --filter OrderTotalTests` | 8/8 PASS (lượt 1) |
| US3 — đặt đơn | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter PlaceOrderTests` | Lượt 1: lần đầu 7/8 (test đỏ khi máy đang chạy cả stack, chưa ghi tên), chạy lại 8/8 PASS |
| BFF — giỏ, checkout, lan truyền subject | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter "BasketFlowTests\|CheckoutTests\|SubjectPropagationTests"` | 15/15 PASS (lượt 1) |
| Lan truyền danh tính người gọi (subject) | `dotnet test shared/Tenancy.UnitTests --filter "CallerContextTests\|CallerContextMiddlewareTests"` và `dotnet test services/gateway/tests/Gateway.Api.UnitTests --filter SubjectHeaderPropagationMiddlewareTests` | 14/14 và 7/7 PASS (lượt 1) |
| Identity đã sửa (client SPA, `IssuerUri`) | `dotnet test services/identity/tests/Identity.Api.UnitTests` và `...Identity.Api.IntegrationTests` | 5/5 và 2/2 PASS |
| Frontend — component/unit (Vitest + MSW), gồm `tests/auth` | `cd frontend && corepack pnpm --filter @ecommerce/web test` | 13 file, **60/60 PASS**; `lint`, `typecheck` sạch |
| FR-025/SC-011 — ngân sách dung lượng | `cd frontend && corepack pnpm --filter @ecommerce/web build` rồi `... size` | **108.07 kB** gzip so với hạn mức 115 kB — PASS (tăng từ 106.51 kB do mã đăng nhập) |
| SC-002/005/008/009/010 — walkthrough trình duyệt thật | `E2E_USERNAME=postman-test@local.test E2E_PASSWORD=<TestUserPassword> corepack pnpm --filter @ecommerce/web e2e` (stack đang chạy) | **3/4 PASS, 1 FAIL**: `browse, add to basket, check out, and see the confirmation` đỏ ở `walkthrough.spec.ts:112` — `expect(storedKeys).toHaveLength(0)` nhận `["storefront.session"]` (xem QA_Debt) |

## Kết luận

**PASS kèm ghi chú** — luồng storefront đã chạy được đầu-cuối (đăng nhập → catalog → giỏ → thanh toán →
xác nhận → đăng xuất; mã đơn khớp backend, giỏ rỗng sau khi mua, đúng 1 đơn dù double-click), 60/60 test
frontend và các test backend liên quan PASS, bundle trong hạn mức. Ghi chú cần xử lý: (1) test e2e
walkthrough chính của 004 đang **đỏ** vì assertion "không có gì trong browser storage" chưa cập nhật cho
token đăng nhập (và có thể cả assertion SC-010 "chỉ gateway" — chưa xác nhận); (2) bốn tài liệu 004 chưa
phản ánh đăng nhập (FR-026) và 2 FR đã bị amend; (3) `504` do cold start ở vài request đầu. Chi tiết:
[QA_Debt.md](QA_Debt.md).
