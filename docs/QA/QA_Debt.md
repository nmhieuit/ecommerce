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
- **2 test gateway "xanh giả" do không gắn bearer token** (phát hiện lúc viết lại comment chi tiết, đã
  xác minh bằng probe tạm rồi hoàn tác): từ spec 014 gateway deny-by-default nên request không token
  nhận `401 {"error":"unauthorized"}` trước khi tới BFF. Hai test bị ảnh hưởng:
  `RoutingTests.AClientFacingRoute_IsForwardedToTheBffsHandler` (chỉ `Assert.NotEqual(NotFound)` — 401
  cũng qua, nên không chứng minh được việc chuyển tiếp) và
  `DownstreamUnavailableTests.TheError_LeaksNoInternalRoutingDetail` (body 401 vốn không chứa chuỗi
  nội bộ nên `DoesNotContain` xanh mà chưa kiểm tra thông báo 502). Khuyến nghị: thêm
  `UseTestBearerToken()` và assert đúng mã (`502`/mã do BFF trả).

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

## 005 — Chạy local một lệnh

- **`up.ps1` trên Windows PowerShell 5.1 không in được thông báo tiên quyết "daemon không phản hồi" và vỡ khi bị chuyển hướng luồng (đã tái hiện)**:
  `scripts/up.ps1` đặt `$ErrorActionPreference = 'Stop'`; trong PowerShell 5.1 mọi dòng docker ghi ra stderr có thể bị bọc thành
  `NativeCommandError` khi luồng lỗi bị gộp/chuyển hướng. Hai hệ quả quan sát được: (1) với `DOCKER_HOST` trỏ vào 1 địa chỉ không
  có daemon, thay vì câu của script ("the Docker daemon is not responding...") người dùng nhận 1 lỗi PowerShell thô
  `docker : error during connect ... NativeCommandError` — trái ý FR-011/US1-KB3 (nêu đích danh điều kiện còn thiếu); (2) chạy
  `.\scripts\up.ps1 *> file.log` (hoặc `2>&1`) thoát ngay `exit 1` ở dòng `& docker @composeArgs` vì stderr tiến độ build bị coi là lỗi,
  dù build không hỏng. Máy này chỉ có PowerShell 5.1 (không có `pwsh`), và tài liệu 005 không nói cần PowerShell 7. Điều kiện thiếu `.env`
  thì đúng thiết kế (nêu tên `.env` + template, `exit 1`, trước khi khởi động gì).
- **Tài liệu 005 lỗi thời về số thành phần và số cổng công bố (mới)**: quickstart Scenario 1 ghi "fifteen components: 10 healthy, 1 Up,
  4 Exited(0)" và `architecture/005` ghi "chỉ hai cổng công bố (5300, 4173)". `docker compose config` hiện có **19** thành phần và
  **3** cổng công bố (`4173`, `5300` và `5205` — cổng token của identity cho form đăng nhập, thêm cùng đợt spec 004 FR-026;
  `stack-interface.md` đã cập nhật, `architecture/005`/`summary/005`/`spec-summary-vi/005` thì chưa).
- **Số liệu thời gian lần chạy đầu (SC-001 dưới 10 phút) chưa còn đúng trên máy này**: lượt đo được **8m15s** (`exit 0`) nhưng cache build đã ấm một phần vì có 1 lượt build trước đó bị gián đoạn; lượt build lạnh hoàn toàn (image identity-migrate, 3 bundle `dotnet ef`, riêng 2 bundle đầu mất ~550 s và ~1183 s khi máy còn chạy việc khác) chưa hoàn tất sau hơn 45 phút quan sát nên **SC-001 (< 10 phút) chưa được xác nhận**; các lượt sau đo 1m36s (không đổi gì), 2m51s (sau `down`), 3m01s (sau `reset`) — 2 số cuối sát hoặc vượt ngưỡng 3 phút của SC-001.
- **Các tài liệu 005 chưa nhắc tới đăng nhập**: `architecture/development/summary/spec-summary-vi` mô tả "mở URL là dùng được"; hiện storefront yêu cầu
  đăng nhập (`postman-test@local.test` + `TestUserPassword` trong `.env`, in ra ở cuối lệnh `up`). Riêng `specs/005-.../quickstart.md` và
  `contracts/stack-interface.md` đã được cập nhật.
- **Kịch bản "thiếu dependency" (quickstart Scenario 5) như viết không tạo ra lỗi (mới)**: `docker compose stop sqlserver` rồi `./scripts/up.ps1` thành công (`exit 0`, 1m41s) vì
  Compose tự khởi động lại `sqlserver` — nó là 1 thành phần của stack. Để đo SC-005 ("lỗi nêu đích danh thành phần trong ≤ 2 phút") cần làm cho 1 thành phần *không thể* healthy
  (biến thể "gỡ migrator" hoặc sai cấu hình), chưa chạy.
- **Số volume lệch tài liệu (mới)**: quickstart Scenario 3 ghi 2 volume (`sqlserver-data`, `rabbitmq-data`); thực tế còn có `elasticsearch-data` (3 volume) — cả 3 bị `reset` xoá.
- **Walkthrough e2e trên container không ổn định ngay sau khi dựng lại stack (mới)**: lần chạy đầu 2/4 đỏ (giỏ trống sau khi thêm; thanh toán trả lỗi), lần 2 chỉ còn lỗi assertion storage của QA 004.
  Chưa xác định nguyên nhân gốc lần 1 (nghi cold start sau `up` dù đã làm nóng).
- **Ghi chú môi trường**: `.env` của máy QA (tạo trước spec 014) thiếu `ClientSecret`/`TestUserPassword`; đã thêm 2 dòng này (lấy nguyên từ `.env.example`, file gitignored) để có mật khẩu đăng nhập và cho bước làm nóng chạy.
  Không đổi `MSSQL_SA_PASSWORD` (đổi sẽ hỏng volume SQL Server đã khởi tạo).
- **Chưa kiểm chứng**: 10 chu kỳ dừng/khởi động (SC-004; mới 1 chu kỳ), Scenario 6 (đổi mã nguồn, FR-009), chế độ `--debug`, `up.sh` (chỉ chạy `up.ps1`), lượt cài đặt từ máy hoàn toàn sạch.

## 006 — Demo đặt hàng end-to-end

- **`./scripts/demo.ps1` không chạy được — FAIL (đã chạy thật)**: sau 3m41s (gồm dựng stack demo mode) dừng ở bước "Clearing the basket..." với
  "Cannot run the demo: the basket could not be cleared (HTTP 401)…", `exit 1`. Script gọi thẳng `POST :5188/baskets/current/clear` chỉ kèm
  `X-Tenant-Id: contoso` + `X-Subject-Id: phase1-stub-user` (danh tính stub thời Phase 1); từ spec 014 mọi service đòi JWT (deny-by-default) nên `401`. Cùng
  nguyên nhân sẽ làm hỏng tiếp: bước xác minh gọi thẳng Orders `:5041` (đã kiểm chứng: không token → `401`) và walkthrough
  `frontend/apps/web/demo/order-demo.spec.ts` (không có đăng nhập, trong khi storefront đứng sau form đăng nhập theo spec 004 FR-026). Cả
  `demo.ps1`, `demo.sh` lẫn spec đều không có mã lấy token (đã grep `token`, `Authorization`, `E2E_`, `TestUserPassword`). Hệ quả: US1
  (SC-001/002/003/003a, FR-001…FR-007c), phần thu hop-evidence (FR-011a) và bản ghi hình/ảnh mới đều không tạo ra được. Đây là hệ quả xuyên-spec của việc cutover
  JWT (014) + đăng nhập SPA (004 FR-026): spec 005 đã được cập nhật cho đăng nhập (in hướng dẫn, làm nóng có token) còn 006 thì chưa. Thông báo lỗi tự nó rõ ràng (nêu
  bước và mã HTTP), đúng tinh thần FR-016.
- **Bài tường thuật `docs/demo-phase-1.md` không còn trong repository (mới, đã xác minh bằng git)**: file 238 dòng được thêm ở commit `c351bc2` và **bị xoá ở
  `8fcbbdf`** ("Add specifications for cluster secret store and liveness/readiness probes" — commit không liên quan tới demo, có vẻ là xoá nhầm kèm theo). Vẫn còn tham
  chiếu tới nó: `docs/local-development.md` (dòng 56), `docs/local-development.vi.md` (dòng 57), `docs/architecture/006`, `specs/006-e2e-order-demo/{quickstart,plan,tasks,data-model,contracts/demo-interface}.md`. Hệ quả: FR-014 (tìm thấy được từ điểm vào tài liệu),
  FR-015 (ánh xạ tiêu chí Phase 1), US3, SC-005, SC-006 hiện không có bằng chứng; các link hỏng. `docs/README.md` cũng không có mục nào trỏ tới demo.
- **Hướng dẫn thủ công trong quickstart 006 không còn đúng**: các lệnh `curl` ở Scenario 2/4 chỉ có `X-Tenant-Id` (nay `401`); phải kèm `Authorization: Bearer <token>` (token lấy từ
  `POST localhost:5205/connect/token`, client `ecommerce-web-spa-password`, mật khẩu = `TestUserPassword` trong `.env`). Với token: đọc đơn ở Orders → `200` kèm `tenantId: "contoso"`; bỏ `X-Tenant-Id` → `500`;
  `POST /orders` không tenant → `500` và số dòng bảng `Orders` không đổi (1 → 1); qua BFF vẫn 3 trường.
- **Tên ảnh lệch giữa tài liệu**: `data-model.md` ghi `01-catalog.png … 04-confirmation.png`; thực tế 4 file là `01-catalog`, `02-basket`, `03-confirmation`, `04-basket-empty` (`tasks.md` giải thích
  `03-checkout.png` bị bỏ vì trùng ảnh giỏ). Không phải lỗi, chỉ là tài liệu chưa đồng bộ.
- **`up` chỉ làm nóng đường đọc (mới, quan sát thật)**: ngay sau `up.ps1` xong (đã in "Warming the request path..."), `POST /bff/basket/items` và `POST /bff/checkout` vẫn trả `504`
  ở lượt đầu (thử lại thì thành công) — bước làm nóng gọi `GET /bff/products`, `/bff/basket`, `/bff/orders/<id giả>`, không chạm đường ghi. Lần `add` `504` đầu tiên có vẻ vẫn đã ghi món vào giỏ
  (giỏ sau đó cộng dồn thành tổng `50.00` = 4 Notebook thay vì 2), tức 1 `504` có thể để lại thay đổi ở downstream. Chưa điều tra nguyên nhân.
- **Chưa kiểm chứng**: Scenario 5 (lặp lại), 6 (hop evidence), 8 (cold start), phần dừng `orders-api` của Scenario 9, `demo.sh`; T042 (đính video vào Jira) vẫn là việc thủ công như tài liệu đã nêu.

## 007 — Hợp đồng OpenAPI cho BFF

- **Client đã commit lỗi thời so với tài liệu OpenAPI của BFF — cổng `verify-generated` ĐỎ (đã chạy thật)**: `corepack pnpm --filter @ecommerce/api-client verify-generated` với BFF chạy ở `:5301` thoát `1`
  ("Generated client is not committed, or has drifted"). Sinh lại cho ra: sửa `generated/endpoints.ts` (+38/-15), `generated/model/index.ts`, `generated/model/productListResponse.ts` (thêm `page`, `pageSize`, `totalCount`) và
  thêm mới `listProductsParams.ts`, `productListResponsePage.ts`, `productListResponsePageSize.ts`, `productListResponseTotalCount.ts`. Nguyên nhân: spec 023/SCRUM-33 (commit `d73ddb1`) thêm phân trang cho `/bff/products`
  mà không sinh lại client; lần sinh cuối là `c99783c` (004). Hệ quả: FR-004/SC-002 không đạt trên `master`; SPA hiện không có kiểu cho phân trang. Bản diff đầy đủ đã lưu ở scratchpad (`codegen_drift_007/drift.patch`) rồi hoàn tác để repo không bị đổi.
- **Không có gì bắt buộc chạy `verify-generated` (mới)**: lệnh chỉ có trong `frontend/package.json`/`turbo.json`; không có tham chiếu nào trong `Jenkinsfile`, `docker/ci`, `scripts` (đã grep) — nên chính cơ chế "CI phải fail khi lệch" (ADR-0004, mô tả ở comment của script) chưa được thực thi; lệch ở trên tồn tại mà không ai biết.
- **Test hợp đồng chỉ ghim 4/7 route (mới)**: `GeneratedContractTests` (thuộc spec 002 T062) kiểm `/bff/products`, `/bff/baskets/{id}`, `/bff/orders/{id}`, `/bff/parties/{id}`; tài liệu thật có 7 route — `/bff/basket`, `/bff/basket/items`, `/bff/checkout` (thêm ở spec 004) chưa có
  test nào chống hồi quy (dù Postman request "Tài liệu OpenAPI của BFF" có kiểm cả 7).
- **Quickstart 007 lỗi thời một chỗ**: bước SC-003 "`grep -rE "fetch\(|axios\(" frontend/apps/web/src` — kỳ vọng 0 kết quả" nay có 1 kết quả hợp lệ (`src/auth/identityClient.ts`, gọi `/connect/token` của identity, ngoại lệ đã ghi ở spec 004 FR-014); và quickstart
  dùng `dotnet run` thay vì stack Docker. Không phải lỗi mã, chỉ là hướng dẫn chưa cập nhật.
- **Phạm vi tài liệu**: `architecture/007`/`summary/007` ghi phạm vi 3 mảng products/baskets/orders và "không có sai lệch nào ghi nhận, 4 checkpoint PASS" — đúng tại thời điểm viết; nay lệch (mục đầu). `docs/development/007` không tồn tại (thiết kế đúng, spec không đổi mã sản xuất).
- **Chưa kiểm chứng**: walkthrough SPA đầy đủ (đã có ở QA 004/005), `pnpm ... build/lint` của `api-client` sau khi sinh lại (chưa sinh lại vào repo).

## 008 — Event schema có version

- **README của `EventContracts` và quickstart lỗi thời (đã xác minh)**: `shared/EventContracts/README.md` (dòng 6-9) ghi "Nothing here is referenced by a service yet. No broker exists… wiring RabbitMQ + MassTransit and outbox-backed publishing is SCRUM-31's job"; quickstart SC-001 kỳ vọng
  `grep -rl "OrderPlaced|BasketCheckedOut" services/orders services/baskets services/bff --include="*.cs"` trả **0 kết quả**. Thực tế `Orders.Api`, `Baskets.Api` và 2 project `*.ContractTests` tham chiếu `shared/EventContracts`, lệnh grep trả **9 file** (`OrdersDbContext.cs`, `OrderEndpoints.cs`, `BasketCheckedOutMapper.cs`
  và các file test) vì spec 024 đã nối MassTransit + outbox + RabbitMQ. Đã xác minh **không** có `record OrderPlaced…`/`BasketCheckedOut…` định nghĩa lại trong `services/` (US1-KB2/SC-001 vẫn đúng). Sai lệch đã có Amendment ở `technical-debt.md` (mục 3, ~dòng 270) nhưng README và quickstart chưa cập nhật;
  `architecture/008` §1 ("chưa có publisher/consumer thật nào") và `development/008` ("chưa service nào dùng package này") cũng vậy.
- **Đường nâng phiên bản chưa từng được thực hành**: cả 2 event chỉ có V1 nên FR-004 (phiên bản cũ vẫn hoạt động suốt khung ngưng dùng), US2-KB1 và US2-KB3 chỉ tồn tại dưới dạng chính sách trong README (mục "Deprecation window") và quy trình 5 bước; không có V2 nào để test cả 2 phiên bản cùng sống, và không test nào kiểm chứng "consumer theo V1 xử lý được sự kiện V2".
- **Kiểm tra tương thích chỉ là hash đóng băng thô (đã biết, ghi ở README)**: mọi chỉnh sửa, kể cả sửa chính tả `description`, làm test đỏ; không phân loại thay đổi phá vỡ/không phá vỡ (research.md Decision 3, phân tích theo consumer để dành cho ADR-0006/spec 011). Thí nghiệm xác nhận: thêm thuộc tính bắt buộc `qaExperimentField` vào `OrderPlaced.v1.schema.json`
  → `SchemaImmutabilityTests.OrderPlaced_V1_Schema_Content_Is_Frozen` và `SchemaValidationTests.Serialized_OrderPlacedV1_Validates_Against_Its_Published_Schema` đỏ (2/6), hoàn tác → 6/6 PASS.
- **Chặn merge: mới đọc script, chưa chạy CI**: `scripts/ci/run-dotnet-tests.sh unit` gom mọi `*Tests.csproj` không thuộc tầng integration/contract/performance — gồm `shared/EventContracts.UnitTests` — nên cổng PR của Jenkins ("unit tests") sẽ bắt thay đổi schema; chưa chạy Jenkins thật để xác nhận.
- **Tài liệu**: `docs/development/008` bỏ qua phần kiểm thử theo thiết kế; phạm vi 2 event (`OrderPlaced`, `BasketCheckedOut`) đúng với spec. Không phát hiện lệch nào khác giữa 4 nguồn.

## 009 — TDD hồi tố giỏ hàng và đơn hàng

- **`tasks.md` T013 ("chạy cùng lúc, xác nhận tất cả xanh") không xanh tuyệt đối nếu làm đúng nghĩa
  đen**: `dotnet test services/baskets/tests/Baskets.Api.UnitTests` và
  `services/orders/tests/Orders.Api.UnitTests` (chạy cả project, không lọc filter) mỗi bên có 1 test
  đỏ — `HealthCheckTests.HealthLive_ReturnsOk` (`OptionsValidationException: Missing required
  secret(s)` vì thiếu biến môi trường `ConnectionStrings__...Db` chứa `Password=`, từ spec 018). Đây
  **không phải** lỗi của 6 quy tắc mà spec 009 audit (`BasketLineMergeTests`/`OrderTotalTests` đều
  8/8 xanh khi lọc đúng class) — cùng nguyên nhân đã ghi ở mục 001, chỉ nhắc lại ở đây vì T013 dùng
  đúng cách chạy "cả project" nên vô tình chạm phải. Không cần sửa gì ở 009, chỉ cần biết khi tái hiện
  T013/T015 không nên hoảng vì 1 test đỏ không liên quan.

## 011 — Kiểm thử hợp đồng tiêu dùng

- **[ĐÃ VÁ 2026-09-22] Cả 3 provider contract test phía HTTP (products/baskets/orders) từng ĐỎ 100%
  từ 2026-09-03 tới hôm nay (đã chạy thật, không suy diễn) — mục bên dưới là hồ sơ nguyên nhân gốc,
  giữ nguyên cho lịch sử**: `dotnet test` từng project báo
  `expected 200 but was 401` ở mọi interaction. Nguyên nhân xác nhận bằng `git show`: commit
  `4940818` (2026-09-02, spec 014) tắt `AuthorizationOptions.FallbackPolicy` trong
  `PactProviderHost.cs` của cả 3 service, đúng lúc đó là đủ — nhưng đúng 1 ngày sau, commit `be79cbf`
  (2026-09-03, spec 015 "enforce authorization requirements across services") thêm
  `.RequireAuthorization(AuthorizationPolicies.ApiScope)` **thẳng vào từng endpoint**
  (`CatalogEndpoints.cs`/`BasketEndpoints.cs`/`OrderEndpoints.cs`). Policy gắn thẳng vào endpoint
  không bị `FallbackPolicy = null` vô hiệu hoá, nên patch của 014 hết tác dụng từ đó tới nay và không
  ai cập nhật lại `PactProviderHost.cs` (vd. thêm `.UseTestJwtBearer()` + ghi token vào pact) để bù.
  **Đã có dấu vết ở nơi khác**: `technical-debt.md` mục 023 từng ghi nhận đúng hiện tượng 401 này khi
  audit spec 023 (xác nhận không phải hồi quy của 023 bằng cách so với baseline `10ea911`), nhưng
  chưa từng được ghi thành phát hiện của chính spec 011 — nơi test này thuộc về. Hệ quả: FR-001/
  FR-002/FR-003/FR-005 và nửa HTTP của SC-002 hiện không kiểm chứng được — 1 thay đổi phá vỡ hợp đồng
  thật (giống kịch bản T013/T015/T017) sẽ không phân biệt được với lỗi 401 nền, nên bộ test không còn
  làm đúng việc nó được sinh ra để làm. Cần chủ sở hữu quyết định hướng vá (thêm `.UseTestJwtBearer()`
  + token giả vào pact, hoặc tắt riêng `RequireAuthorization` cho route trong `PactProviderHost`).
  **Hướng đã chọn (2026-09-22, chủ sở hữu xác nhận từng bước, không tự suy diễn)**: ghi
  `Authorization` vào chính pact phía consumer (`BffPact.CreateRelayingClient` gắn token thật qua
  `TestJwtBearer.CreateToken`, khai trong 3 file `*ConsumerPactTests.cs` bằng
  `Match.Regex(..., "^Bearer .+$")` — regex, không phải giá trị cố định, để literal token không bị
  đóng băng vào file đã commit); phía provider, `PactProviderHost.cs` của cả 3 service đổi
  `FallbackPolicy = null` thành `.UseTestJwtBearer()` (cùng helper mọi `*.Api.IntegrationTests` đã
  dùng, không gọi identity server thật), và mỗi `*ProviderPactTests.cs` gắn
  `.WithCustomHeader("Authorization", "Bearer " + TestJwtBearer.CreateToken())` trước `.Verify()` —
  ghi đè token trong pact bằng token còn hạn, nên không phụ thuộc thời điểm lần cuối regenerate pact.
  Đã xác nhận bằng red-green-red thật (không chỉ xanh vì trùng hợp): đổi tên `ProductResponse.Price`
  → `Cost`, chạy lại → đỏ đúng vào `price` bị thiếu (không phải 401 nào khác), revert → xanh lại.
  **Phát hiện phụ khi vá (không liên quan 015)**: `pacts/bff-products.json` giữ 1 interaction thừa
  "a request for the catalog" từ commit `514c6c1` (22/8, bản gốc spec 011, response là mảng trần) —
  commit `d73ddb1` (11/9, spec 023 thêm phân trang) đổi tên interaction thành "a request for a page
  of the catalog" và đổi hình dạng response (mảng → envelope `items/page/pageSize/totalCount`) nhưng
  không xoá interaction cũ, và cơ chế ghi pact của PactNet gộp/giữ theo mô tả thay vì ghi đè toàn bộ
  file, nên interaction thừa nằm im trong file đã commit, tự fail vì lệch hình dạng (không phải 401)
  ngay khi 401 nền được vá xong mới lộ ra. Đã xoá thủ công interaction đó khỏi
  `pacts/bff-products.json` (chủ sở hữu xác nhận trước khi xoá); `bff-baskets.json`/`bff-orders.json`
  không có interaction thừa tương tự — kiểm tra thủ công 2 file này khớp đúng số interaction mà
  consumer test hiện tại khai.
- **Test provider phía event (`BasketCheckedOutProviderPactTests`) không chạy được trên máy đang rà
  soát — xung đột cổng Windows/Docker Desktop, không phải lỗi mã**: PactNet cố mở
  `http://localhost:49152/pact-messages/`, ném `HttpListenerException: The process cannot access the
  file because it is being used by another process`. `netsh interface ipv4 show excludedportrange
  protocol=tcp` xác nhận cổng `49152` nằm trong dải `49152–49251` mà mạng WSL2 của Docker Desktop đã
  giữ trước — xung đột cổng động đã biết giữa `HttpListener` .NET và Docker Desktop/WSL2 trên Windows.
  `tasks.md` T020 xác nhận test từng PASS lúc viết feature; đây là vấn đề môi trường máy, không cần
  sửa mã. Không loại trừ khả năng ảnh hưởng các máy Windows + Docker Desktop khác — QA nên biết để
  không nhầm là lỗi hợp đồng thật khi gặp lại.
