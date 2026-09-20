# QA Debt — phát hiện và giới hạn khi rà soát chất lượng happy-case (001-026)

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử. File này gom mọi phát hiện (tài liệu sai lệch với code thật,
narrative thiếu bước cho 1 user story, thiếu bằng chứng định lượng cho tiêu chí nghiệm thu...) khi rà
soát chéo `architecture`/`development`/`summary`/`spec-summary-vi` cho từng spec — để mỗi file
`0NN_QA_*.md` chỉ giữ kết luận ngắn gọn (PASS/PASS kèm ghi chú/FAIL) và trỏ về đây. Không có nội dung
nào bị bỏ — chỉ gom lại 1 chỗ.*

## 001 — Dựng khung 4 dịch vụ

- **Tài liệu sai lệch với code thật**: `architecture/001` mô tả readiness dùng `AddDbContextCheck<T>()`
  để kiểm tra database. Code hiện tại (`services/products/src/Products.Api/Features/HealthCheck/HealthCheckEndpoints.cs`)
  đã đổi sang `.AddSqlServer(...)` (mở kết nối SQL thô) từ khi **003-stub-identity-tenant-context** gate
  `DbContext` theo tenant — health probe của Kubernetes gọi thẳng `/health/ready`, không qua gateway,
  nên không có tenant để dựng `DbContext`. Đây là thay đổi xuyên-spec chưa từng được ghi Amendment ở
  `architecture/001` lẫn `architecture/003`. **Không ảnh hưởng kết quả happy-case** (readiness vẫn đúng
  chức năng — phản ánh thật khả năng kết nối database), chỉ sai phần mô tả cơ chế kỹ thuật cụ thể.
- **PO thiếu 1 bước trải nghiệm cho US2**: mục "Trải nghiệm thực tế" của `summary/001` chỉ có 3 bước,
  ánh xạ tới US1 (bước 1-2) và US3 (bước 3) — US2 (cách ly dữ liệu chéo) không có bước trải nghiệm
  riêng, chỉ được nhắc ở đoạn giới thiệu giải pháp và ở `functional-debt.md`.
- **Thiếu bằng chứng định lượng cho SC-001/002/004**: `architecture`/`development` không trích số đo
  cụ thể (thời gian khởi động, số lần chạy khoẻ, ví dụ cấu trúc thư mục) — khác các spec sau (005, 006)
  đã có số liệu thật. Không phải lỗi, chỉ là đặc điểm của spec sớm trong dự án.
- **Test đơn vị `HealthCheckTests` cần biến môi trường mà tài liệu chưa nêu (phát hiện thêm ở lượt QA 004)**:
  chạy `dotnet test services/products/tests/Products.Api.UnitTests --filter HealthCheckTests` (và tương tự
  baskets, parties, orders) trên máy sạch **đỏ 4/4 service** với `OptionsValidationException: Missing required
  secret(s): ConnectionStrings:<X>Db` — validation secret của spec 018 (`shared/ServiceDefaults/RequiredSecretsValidation.cs`)
  yêu cầu chuỗi kết nối có `Password=` (hoặc Integrated Security) thì service mới khởi động. Đặt
  `ConnectionStrings__ProductsDb="Server=x;Database=y;User Id=sa;Password=p;TrustServerCertificate=true"` thì test
  PASS (đã xác nhận trên products; test không chạm database thật nên giá trị giả là đủ). Bản QA 001 gốc liệt kê
  lệnh này mà không nêu điều kiện tiên quyết — đã bổ sung vào bảng "Tự động" của file 001.

## 002 — Định tuyến gateway-BFF

- **Tài liệu sai lệch với cổng thật**: [`specs/002-gateway-bff-routing/quickstart.md`](../../specs/002-gateway-bff-routing/quickstart.md)
  (dòng "Note the local ports") ghi baskets = `5041`, orders = `5188`. Thực tế
  (`docker-compose.local.yml`, `postman/local.postman_environment.v2.json`, và đã xác nhận lại ở QA
  001) là **ngược lại**: baskets-api publish `5188`, orders-api publish `5041`. Không ảnh hưởng happy-
  case (Postman collection dùng biến `{{basketsUrl}}`/`{{ordersUrl}}` nên không bị lỗi cổng), chỉ gây
  nhầm nếu ai đó gọi `curl` tay theo đúng số quickstart ghi.
- **2 request CORS preflight trong folder Postman `Gateway`** ("CORS preflight từ storefront/origin
  lạ") không thuộc phạm vi happy-case của spec 002 — `specs/002-gateway-bff-routing/spec.md` không có
  FR/SC nào về CORS; theo chú thích trong `docker-compose.yml` (`Cors__AllowedOrigins__0`), CORS được
  thêm bởi spec 004 (SPA storefront) sau khi phát hiện lỗi thật lúc walkthrough. Cả 2 request này nên
  được rà soát ở QA của spec 004, không phải ở đây — chỉ ghi chú để không bị hiểu nhầm là thiếu sót của
  002.
- **Bảng "Tự động" (bản trước khi sửa) gán nhầm 1 file test của spec 016 cho spec 002**:
  `services/bff/tests/Bff.Api.IntegrationTests/CorrelationPropagationTests.cs` bị liệt kê chung với
  `Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs` như thể cả 2 cùng là regression test
  cho bug correlation-ID của 002. Đã xác minh qua `git log --follow`: file BFF được tạo ở commit
  `2abe34d` ("feat(correlation-id): implement correlation ID propagation from edge to frontend",
  2026-09-06) — chính là **016-correlation-id-propagation** — muộn hơn spec 002 (hoàn tất 2026-08-15)
  gần 3 tuần; bản thân class đó cũng tự chú thích "016-correlation-id-propagation spec US1/US3". File
  Gateway (`CorrelationIdPropagationTests.cs`) mới thật sự là của spec 002 (commit `c9db644`, cùng ngày
  với phần còn lại của 002, và được `tasks.md` của 002 ghi nhận trong "Phase 6 implementation notes").
  Đã sửa bảng "Tự động" của `002_QA` để chỉ còn trỏ tới file Gateway; việc rà soát `CorrelationPropagationTests.cs`
  (bff) để dành cho QA của spec 016.

## 003 — Danh tính giả lập và tenant

- **Test SC-003 đang FAIL trên `master`**: `dotnet test tests/CrossServiceIsolation.Tests --filter TenantGatedConnectionTests`
  → `EveryDbContextRegistration_IsGatedOnAResolvedTenant` đỏ ở `orders` (kỳ vọng 1 call site gated, thực
  tế 0). Nguyên nhân: `services/orders/src/Orders.Api/Program.cs` không còn gate `RequireTenantId()` tại
  `AddDbContext` — spec 024 chuyển cổng xuống `OrderEndpoints.cs` vì hosted service outbox của MassTransit
  dựng `OrdersDbContext` ngoài HTTP request (không có tenant). Hành vi runtime vẫn an toàn (mọi đường ghi/đọc
  Order vẫn gọi `RequireTenantId()`), nhưng scanner và cả 4 tài liệu 003 (`architecture` §2 Quyết định 6 +
  §3, `summary`, `spec-summary-vi` SC-003/US2-KB2) vẫn khẳng định "mọi service gate tại `AddDbContext`" — chưa có
  Amendment. Cần chủ sở hữu quyết định: cập nhật scanner (ngoại lệ Orders có kiểm chứng ở call site) hoặc
  ghi Amendment.
- **Quickstart 003 không còn tái hiện được như viết**: Scenario 3 kỳ vọng gọi thẳng service không header → `500`;
  thực tế (đã chạy) `401` cả khi có/không có `X-Tenant-Id`, vì deny-by-default (014/015) chặn trước. Gateway local
  cũng đã cutover JWT (`appsettings.Development.json`: `IdentityServerAuthCutover: true`), nên đường "stub
  identity" của US1 chỉ còn khi tắt toggle. Các request Postman "Thiếu header tenant…" (kỳ vọng `500`) cần
  bearer token. *Cập nhật lần 2 (nhánh `claude/spa-identity-server-login-e6100d`): lấy token nay thành công (200) trên
  `docker-compose.local.yml`; đã xác nhận không token → `401`, có token nhưng không `X-Tenant-Id` → `500` (cổng tenant),
  qua gateway có token → `200`. Lượt đầu không lấy được token vì identity chưa seed client.*
- **Schema-per-tenant chưa từng triển khai nhưng `architecture/003` (Quyết định 5) mô tả như đang có**:
  `HasDefaultSchema` không xuất hiện trong mã nguồn (đã grep `services/`, `shared/`). Sai lệch đã được ghi ở
  `technical-debt.md` (mục 3) nhưng `architecture/003` không có con trỏ tới đó.
- **`development/003` lỗi thời ở 1 đoạn**: snippet `Gateway.Api/Program.cs` dùng `AddAuthentication(StubIdentity…)`
  trực tiếp; code hiện tại là `AddToggleGatedIdentity(...)` (spec 014). Đúng với tính chất tài liệu "thay đổi
  theo bước" nên chỉ ghi chú, không phải lỗi nội dung.
- **Ghi chú môi trường**: dựng lần đầu 5 SQL Server cùng lúc mất ~9.5 phút và `orders-db` báo unhealthy ở lượt
  `up --wait` đầu; chạy lại thì các service lên bình thường. Nên chỉ dựng service cần test.

## 004 — SPA mua sắm tối thiểu

*Cập nhật lần 2, sau khi nhánh `claude/spa-identity-server-login-e6100d` thêm đăng nhập cho SPA. Các thay đổi
(identity, compose, frontend, spec 004) đã được commit (`bfb0162`) và merge vào `master` qua PR #43.*

- **Đã khắc phục — storefront không chạy được (lượt 1 ghi FAIL)**: lượt 1 quan sát trang kẹt "Loading
  products…" (gateway trả `401`, SPA không có đăng nhập). Nay SPA có form đăng nhập (`frontend/apps/web/src/auth/`),
  gắn `Authorization: Bearer` qua `bffFetch`, identity seed thêm client công khai `ecommerce-web-spa-password`
  (chỉ khi `SpaPasswordClient__Enabled=true` — chỉ có trong `docker-compose.local.yml`) và `IssuerUri` được ghim
  về `http://identity-api:8080` để token lấy từ trình duyệt qua cổng `5205` vẫn được mọi service chấp nhận.
  Đã kiểm chứng bằng trình duyệt thật: đăng nhập sai → "Incorrect username or password."; đăng nhập đúng →
  catalog 3 sản phẩm, giỏ, thanh toán, xác nhận (mã đơn đọc lại được, giỏ rỗng, đúng 1 đơn), đăng xuất về `/login`.
- **Test e2e walkthrough chính đang ĐỎ (mới)**: `corepack pnpm --filter @ecommerce/web e2e` → 3 PASS, 1 FAIL
  (`browse, add to basket, check out, and see the confirmation`). Dòng 112 của `e2e/walkthrough.spec.ts` khẳng định
  `localStorage` + `sessionStorage` không có key nào (ý gốc: "giỏ không nằm trong trình duyệt", quickstart Scenario 3
  bước 3) nhưng token đăng nhập được lưu ở `sessionStorage` (`storefront.session`). Assertion cần thu hẹp thành
  "không có gì liên quan tới giỏ hàng". Cùng file, dòng 133 `expect([...requestOrigins]).toEqual([GATEWAY_ORIGIN])`
  (SC-010) nhiều khả năng cũng lỗi thời vì lần gọi `POST localhost:5205/connect/token` của form đăng nhập là 1 origin
  khác — spec.md FR-014 đã được amend để cho phép ngoại lệ này. **Chưa xác nhận**: test dừng ở dòng 112 trước khi
  tới dòng 133.
- **Bốn tài liệu 004 chưa phản ánh đăng nhập/amend (mới)**: `specs/004-minimal-shopping-spa/spec.md` nay có FR-026
  (đăng nhập), FR-014 (ngoại lệ gọi identity) và FR-015 (không còn "danh tính stub") đã đổi nghĩa; `quickstart.md`
  có mục "Signing in". Nhưng `docs/architecture/004`, `docs/development/004`, `docs/summary/004` và
  `docs/spec-summary-vi/004-minimal-shopping-spa.json` vẫn mô tả luồng không đăng nhập; JSON vẫn giữ nguyên nội
  dung FR-014 ("không được gọi trực tiếp bất kỳ service nào") và FR-015 ("không màn hình nào yêu cầu danh tính") và
  không có FR-026. Riêng `docs/local-testing.md` Scenario 2 ("no tenant → 500") chỉ đúng khi có token: gọi không token
  giờ trả `401`.
- **`504` khi chạy lần đầu trên stack mới (mới, quan sát thật)**: dù `up --wait` báo mọi container `healthy`, vài
  request đầu tiên trả `504`: `GET /bff/products` (4.4 s, sau đó tự thử lại thành `200`), `POST /bff/basket/items`
  ×2 (1.4 s, 1.0 s — 2 lần thêm Notebook không được ghi nhận, giỏ chỉ có Apron), `POST /bff/checkout` (1.1 s — hiện
  "We could not place your order. Your basket is unchanged", 1 request duy nhất, không sinh đơn). Hành vi UI đúng
  US2-KB5/US3-KB4 nhưng console có lỗi `504` nên 1 lượt walkthrough đầu không đạt SC-002 (0 lỗi console). Nguyên
  nhân đã biết từ spec 002 (`technical-debt.md`: cold start, phải chờ readiness chứ không chỉ liveness); `--wait`
  của compose chưa làm ấm đường đi thật. Nội dung thông báo lỗi ở 2 lần thêm giỏ thất bại chưa được ghi lại.
- **FR-011 (giỏ qua đóng-mở trình duyệt) — cần diễn giải lại**: token ở `sessionStorage` chết theo tab, nên sau khi
  đóng-mở trình duyệt người mua phải đăng nhập lại rồi mới thấy lại giỏ (giỏ vẫn do server giữ theo subject).
  Tải lại trang (F5) đã kiểm chứng giữ nguyên phiên và giỏ; **đóng-mở lại trình duyệt chưa thử**.
- **Số liệu xác minh trong `architecture/004` lỗi thời (không phải lỗi)**: tài liệu trích "frontend 45 tests across
  11 files", "bundle 106.46 kB". Hiện 60 tests / 13 file (kèm `tests/auth`) và bundle 108.07 kB (hạn mức 115 kB vẫn đạt).
- **`development/004` cố ý chỉ mô tả backend** ("Frontend và kiểm thử được bỏ qua" trong mục Phạm vi); không phải
  thiếu sót — nhưng phần đăng nhập (frontend + identity) nằm hoàn toàn ngoài 4 tài liệu.
- **Cổng trong quickstart 004 đúng**: baskets `:5188`, orders `:5041` khớp `docker-compose.local.yml`.
- **Test đỏ tạm thời (lượt 1)**: `Orders.Api.IntegrationTests --filter PlaceOrderTests` đỏ 1/8 ở lần chạy đầu
  (3m14s, máy chạy cả stack), chạy lại 8/8 PASS. Chưa ghi lại tên test lỗi và nguyên nhân; nghi thiếu tài nguyên.
- **Ghi chú công cụ**: `playwright.config.ts` khởi động dev server bằng lệnh `pnpm` nên trên máy không có `pnpm`
  trong PATH phải có shim `pnpm.cmd` (gọi `corepack pnpm`) trước khi chạy `e2e`; đã dùng shim đặt ngoài repo.
- **Không đổi so với lượt 1**: SC-012 (Web Vitals) do chính spec khai báo là không đo ở feature này.
