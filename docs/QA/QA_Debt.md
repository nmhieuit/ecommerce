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
- **[NGHIÊM TRỌNG] Message `OrderPlaced` thật trên RabbitMQ vi phạm schema v1 đã công bố: `total` và `unitPrice` là CHUỖI, không phải số (đã đo trên hệ thống chạy thật, QA lượt làm lại 008 theo hướng Postman + cấu hình)**: bật kết nối RabbitMQ cho `orders-api` (biến `ORDERS_RABBITMQ_CONNECTION`), đặt 1 đơn, đọc message từ queue tạm bind vào exchange `EventContracts:OrderPlacedV1` → `"total": "12.50"`, `"unitPrice": "12.50"` (content-type `application/vnd.masstransit+json`), trong khi `OrderPlaced.v1.schema.json` khai `"total": {"type": "number"}` và `"unitPrice": {"type": "number"}`. Phần còn lại khớp (đủ đúng 7 trường bắt buộc, `eventId`/`orderId` UUID, `occurredAtUtc` UTC, `tenantId`, `correlationId` = `X-Correlation-Id` đã gửi, `lines` ≥ 1). Lý do test không bắt được: `SchemaValidationTests` serialize bằng `JsonSerializerOptions(JsonSerializerDefaults.General)` với giả định "đó là serializer MassTransit dùng ở production" (ghi trong comment, dòng 16/32) — thực tế MassTransit 8.5.4 dùng cấu hình serialize riêng, ghi số thập phân thành chuỗi; nên "record + schema khớp nhau" (test xanh 6/6) nhưng "message thật + schema" thì lệch. Hệ quả: consumer hoặc validator dựa vào schema (kể cả generator client từ schema) sẽ từ chối/đọc sai mọi `OrderPlaced` thật; hợp đồng bất biến (US2) đang bất biến trên giấy nhưng sai so với thực tế phát. Đề xuất: ép MassTransit ghi số (cấu hình `JsonNumberHandling`/converter cho `decimal`) HOẶC sửa schema thành `number|string` bằng phiên bản v2 (vì v1 đã đóng băng), và thêm 1 test tích hợp đọc message thật từ RabbitMQ Testcontainers rồi validate với schema. Tái hiện bằng Postman: folder `08 - Event schema OrderPlaced (RabbitMQ)` (2 test cuối của bước 04 đỏ).
- **Công tắc kết nối RabbitMQ của `orders-api` (thay đổi cấu hình kèm lượt QA này)**: `docker-compose.local.yml` không cấu hình RabbitMQ cho `orders-api` (xem mục 024) nên không thể xem message thật; đã thêm biến `ORDERS_RABBITMQ_CONNECTION` (local: mặc định trống = TẮT như cũ; `docker-compose.yml`: mặc định = URI cũ, đặt rỗng để TẮT) và ghi chú trong `.env.example`. Trạng thái TẮT đã đo: `POST /orders` vẫn `201`, message kẹt trong bảng `OutboxMessage` (1 hàng) và Postman bước 04 đỏ "0 message"; bật lại + tạo lại container → hàng kẹt được giao, `OutboxMessage` về 0.
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
- **Guard `quantity ≥ 1` ở domain bị che bởi kiểm tra ở tầng HTTP — Postman không phân biệt được (đã tự mutate, dựng lại image)**: comment `ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);` trong `Basket.AddItem` → unit test `AddItem_Rejects_AQuantityBelowOne` đỏ 2/8 (0 và -1), nhưng folder Postman `09` (dựng lại `baskets-api` với mã đã sửa) vẫn 12/12 xanh vì handler `POST /baskets/current/items` tự trả `400 "Quantity must be at least 1."` trước khi tới domain. Nghĩa là quy tắc FR-001 ở tầng domain chỉ còn được unit test bảo vệ; ai gọi `Basket.AddItem` từ nơi khác (không qua endpoint) sẽ không được che. Ngược lại guard FR-006 (`Total = lines.Sum(...)` → `0m`) làm cả 3 unit test và bước 10 của Postman đỏ (`expected +0 to equal 203.25`). Không phải lỗi — ghi nhận để hiểu "gỡ guard làm đúng 1 test đỏ" của SC-001 chỉ đúng ở tầng unit.
- **`400` trả nguyên thông điệp ngoại lệ, lộ tên tham số nội bộ và gọi sai tên**: `POST /orders` với dòng số lượng 0 → `{"error":"lines ('0') must be greater than or equal to '1'. (Parameter 'lines')\nActual value was 0."}`, giá âm → `lines ('-1') must be a non-negative value. (Parameter 'lines')` — do endpoint trả `ArgumentException.Message` nguyên văn (`OrderEndpoints.cs`, nhánh `catch (ArgumentException)`); thông điệp nói "lines" trong khi giá trị sai là số lượng/đơn giá, kèm `Parameter 'lines'` và xuống dòng `\n`. Đơn 0 dòng và baskets thì thông điệp riêng, rõ ràng ("An order needs at least one line.", "Quantity must be at least 1."). Đề xuất: map ngoại lệ sang thông điệp cố định theo trường lỗi.
- **Test tích hợp Testcontainers có thể timeout SQL khi máy bận**: `dotnet test …Orders.Api.IntegrationTests --filter PlaceOrderTests` lần đầu (chạy ngay sau khi dựng lại image + unit test) 5/8 đỏ với `SqlException: Execution Timeout Expired … The wait operation timed out` (mỗi test 1–1.5 phút), lần chạy lại khi máy rảnh 8/8 (1 phút 51 giây). Đừng kết luận hồi quy nếu gặp đúng lỗi này; chạy lại lúc rảnh.
- **Lịch sử commit đã có thêm commit ngoài 3 mã của research.md Decision 2 (không mâu thuẫn)**: `git log --follow` nay còn `59af85f` (khởi tạo mô hình ban đầu của `Basket.cs`/`Order.cs`), `0351698` (spec 024 sửa comment `Order.cs`, không đổi test — hợp lý), `bfb0162` và `6c6e8bb` (dịch comment test của QA). SC-002 vẫn đúng: mỗi thay đổi logic của impl đi cùng thay đổi test trong cùng commit.
- **Quan sát chưa tái hiện**: ở 1 lượt chạy folder `09` ngay sau khi dựng lại `orders-api`, bước 01 (`POST baskets/current/clear` tới `baskets-api`, token vừa lấy) trả `401` thay vì `204/409`, các lượt khác không lặp lại; không rõ nguyên nhân (nghi ngờ nhịp khởi động lại của dịch vụ/tải khoá ký từ identity, xem mục 017).

## 010 — Hạ tầng integration test bằng Testcontainers

- **Vi phạm ràng buộc cột lộ ra dưới dạng `500`, không phải `4xx` (đã đo trên stack thật)**: `POST /orders` với header `X-Tenant-Id` dài 129 ký tự (cột `Orders.TenantId` là `nvarchar(128)`) và `POST /baskets/current/items` với `X-Subject-Id` dài 201 ký tự (cột `Baskets.CustomerRef` là `nvarchar(200)`) đều trả `500` (`DbUpdateException` ← `SqlException: String or binary data would be truncated`); ở biên hợp lệ (128 / 200 ký tự) đơn/giỏ được ghi bình thường, và với 129/201 ký tự không có hàng nào được ghi (đã kiểm bảng). Thân `500` là trang lỗi Development ~7 kB kèm stack trace (vì `docker-compose.local.yml` đặt `ASPNETCORE_ENVIRONMENT: Development`). Nghĩa là ràng buộc DB thật hoạt động đúng (đúng điều spec 010 muốn chứng minh) nhưng API chưa kiểm tra độ dài đầu vào; header tenant/subject vốn do gateway phân giải nên rủi ro thấp, chỉ đáng lưu ý khi gọi thẳng service. Đề xuất: kiểm độ dài ở `TenantContextMiddleware`/`CallerContextMiddleware` và trả `400`.
- **Quickstart Scenario 1 ("`docker ps` thấy container SQL Server")**: trên máy đang chạy stack docker-compose, `docker ps` luôn liệt kê sẵn `mcr.microsoft.com/mssql/server:2022-latest` (3 DB của stack), nên phải nhận ra container của test qua tên ngẫu nhiên kiểu `exciting_goodall` (xuất hiện ~8 giây sau khi test bắt đầu, kèm `testcontainers/ryuk:0.14.0`); lúc mới có `ryuk` thì chưa có container SQL Server của test. Không phải lỗi, chỉ là điều kiện quan sát.
- **Không phát hiện lệch tài liệu**: 4 test ràng buộc + 3 test fixture + 2 lượt cố ý phá (gỡ `.IsUnique()`, đổi tag image Redis) khớp mô tả; không file production nào tham chiếu `RedisFixture`/`RabbitMqFixture` (FR-009). `architecture/010` và Amendment 2026-09-12 (RabbitMQ) vẫn đúng.

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
  **Cập nhật 2026-09-23**: chạy lại cùng test, cùng máy, không đổi gì — PASS. Cổng OS cấp cho
  `HttpListener` là ngẫu nhiên mỗi lần chạy nên đây là rủi ro chập chờn thật, không phải đã hết vấn
  đề; không có thay đổi mã nào liên quan (chỉ mục 401 phía trên được vá).

## 013 — Cổng chất lượng CI

- **[NGHIÊM TRỌNG] Cơ chế chặn merge — giá trị cốt lõi duy nhất của spec 013 — hiện không còn hoạt
  động, từ khoảng 3 tuần nay, chưa ai phát hiện (đã tự kiểm tra bằng API công khai của GitHub, không
  suy diễn)**: `curl https://api.github.com/repos/nmhieuit/ecommerce/commits/master/status` trả về
  `total_count: 0` cho HEAD của `master` và cho ít nhất 13 commit merge gần nhất khác (PR #32 → #45,
  2026-09-05 → 09-22). Dò ngược tìm mốc chuyển tiếp: PR #17 (`bb61212`, 2026-09-02) vẫn có 3 check
  `success` (`ci/build`, `ci/unit-tests`, `continuous-integration/jenkins/branch`); PR #18 (`d309283`,
  ngay sau đó, cùng đợt spec 015 deny-by-default-authz) đã là `total_count: 0` — và không PR nào từ đó
  (ít nhất 27 PR, #18 → #45) từng có lại CI check nào. Xác nhận độc lập bằng
  `curl https://api.github.com/repos/nmhieuit/ecommerce/rules/branches/master` → trả về `[]` (không
  rule nào đang thật sự áp dụng cho `master`) — khớp đúng với việc 27+ PR đó vẫn merge bình thường dù
  0 check từng chạy. Phủ định trực tiếp FR-003 ("chặn merge khi cổng chất lượng thất bại, không đường
  vòng cho bất kỳ vai trò nào") vì hiện tại không có gì để thất bại và không có gì chặn.
  Trang `github.com/nmhieuit/ecommerce/branches` (xem qua trình duyệt, không đăng nhập) vẫn hiển thị
  tooltip "This branch is protected by branch protections" — gây hiểu nhầm là còn hoạt động; hợp lý
  nhất là rule protection vẫn TỒN TẠI nhưng danh sách "required status checks" bên trong đã rỗng hoặc
  tuỳ chọn liên quan đã tắt — đúng loại lỗi `tasks.md` T009 từng ghi nhận xảy ra 1 lần lúc setup ban
  đầu ("lần lưu đầu chỉ lưu đúng option boolean, danh sách 5 required check bị lưu rỗng"). Không xác
  nhận được nguyên nhân chính xác trong phiên này — không có token GitHub hợp lệ để đọc trực tiếp cấu
  hình protection (xem phát hiện tiếp theo). Cần chủ sở hữu: (1) xác nhận lại danh sách required
  status checks trên `Settings → Branches` của `master`, (2) quyết định có dựng lại Jenkins/SonarQube
  cục bộ để CI chạy thật trở lại hay không.
- **Jenkins/SonarQube cục bộ hiện không chạy (nhất quán với phát hiện đầu, không phải phát hiện độc lập)**:
  `docker ps -a` rỗng — không container `jenkins`/`sonarqube` nào tồn tại, kể cả đã dừng; volume dữ liệu
  (`ecomerce-ci_jenkins-data`, `ecomerce-ci_sonarqube-data`, `-extensions`, `-logs`) vẫn còn, tức
  `docker compose -f docker-compose.ci.yml down` (không kèm `-v`) đã được chạy ở 1 thời điểm nào đó. Tự nó không
  phải lỗi (instance dev cục bộ, `technical-debt.md` mục 013 ghi "chưa cần chạy thường trực") nhưng giải thích
  trực tiếp vì sao không còn check nào báo về GitHub. Quickstart Kịch bản 1–5 (5 stage đúng thứ tự) không chạy
  lại được trong phiên QA vì cần setup tương tác (Jenkins wizard, đăng nhập SonarQube, PAT mới).
- **Token CI cục bộ (`.ci-secrets/github-pat`) đã hết hạn**: `curl -H "Authorization: Bearer ..."
  https://api.github.com/repos/nmhieuit/ecommerce` → `401 Bad credentials`. Token tạo 2026-08-23,
  nhiều khả năng đã hết hạn dùng (~30 ngày). Ai dựng lại Jenkins từ `docker-compose.ci.yml` sẽ cần
  sinh PAT mới trước khi `githubNotify`/`scripts/ci/setup-branch-protection.sh` hoạt động lại được —
  bước này không nằm trong "Điều kiện tiên quyết" của `quickstart.md` vì lúc viết token còn hạn.
- **`docs/github-jenkins-sonarqube-setup.md` (thành quả T016, runbook dựng lại CI từ đầu) đã bị xoá
  khỏi repo, nhiều khả năng do nhầm lẫn**: bị xoá nguyên vẹn (286 dòng) trong commit `8fcbbdf` ("Add
  specifications for cluster secret store and liveness/readiness probes", 2026-09-07) — nội dung
  commit đó hoàn toàn về spec 018/019, không nhắc gì tới việc xoá tài liệu CI, nên nhiều khả năng là
  side effect của một thao tác khác (merge/rebase), không phải quyết định có chủ đích. `quickstart.md`
  mục "Còn thiếu" vẫn trỏ tới file này — hiện là link chết. Mã cấu hình mà file đó mô tả cách dựng
  (`docker/ci/jenkins.Dockerfile`, `docker-compose.ci.yml`) vẫn còn nguyên trên `master`, chỉ tài liệu
  runbook bị mất — cần viết lại hoặc khôi phục từ lịch sử Git (`git show f6128cc:docs/github-jenkins-sonarqube-setup.md`)
  nếu muốn dựng lại instance CI từ đầu trong tương lai.

## 014 — Máy chủ định danh thật

- **`JwtBearerAuthenticationTests.ARequestWithNoToken_StillReachesTheBff_WhenToggleIsOff` đang ĐỎ —
  tương tác thật giữa 014 và 015 (đã chạy thật, xác nhận nguyên nhân bằng probe tạm rồi hoàn tác)**:
  `StubIdentityAuthenticationHandler.cs` (viết cho 014, chưa từng sửa) chỉ phát hành claim `tenant_id`
  và `NameIdentifier`, không có `scope`. `shared/Identity/AuthenticationFallbackPolicy.cs` (sửa bởi
  015) giờ đòi cả `RequireApiScopeRequirement()` trong `FallbackPolicy` dùng chung — enforce thật khi
  `AuthorizationRequireApiScope=true`, giá trị mặc định của
  `services/gateway/src/Gateway.Api/appsettings.Development.json` (Production giữ `false`, không ảnh
  hưởng). Response thật đo được: `403 {"error":"forbidden_scope","message":"Authentication succeeded,
  but the token does not carry the required scope."}` thay vì request được chuyển tiếp tới BFF.
  **Hệ quả thật, không chỉ lý thuyết**: `docker-compose.local.yml` (stack QA thủ công dùng xuyên suốt
  bộ tài liệu này) đặt `ASPNETCORE_ENVIRONMENT: Development` cho cả 6 service — tức đúng cấu hình bị
  ảnh hưởng. Kịch bản rollback khẩn cấp mà `specs/014-identity-server-auth/quickstart.md` Scenario 7
  mô tả (gạt `IdentityServerAuthCutover` về `false` ngay trên container đang chạy, không redeploy) —
  trên chính stack này — sẽ không còn phục hồi hành vi "gateway chuyển tiếp request không token" như
  tài liệu hứa; gateway tự chặn bằng `403 forbidden_scope`, sớm hơn cả điểm BFF được ghi là sẽ chặn.
  Bằng chứng T047 (`502` khi tắt `bff-api`, dùng làm bằng chứng rollback trong `technical-debt.md` mục
  014) được đo TRƯỚC KHI `AuthorizationRequireApiScope` tồn tại nên không sai tại thời điểm đó, chỉ là
  chưa được đo lại sau khi 015 chồng lên. Cần chủ sở hữu quyết định hướng vá: thêm claim `scope` vào
  `StubIdentityAuthenticationHandler`, hoặc đặt `AuthorizationRequireApiScope=false` mặc định trong
  `appsettings.Development.json` (đánh đổi: mất khả năng tự phục vụ quickstart của 015 mà không cấu
  hình thêm).

## 015 — Phân quyền từ chối theo mặc định

- **2/4 `AuthorizationPolicyDeclaredScannerTests` đang ĐỎ — đúng kịch bản "Failure Modes" mà chính
  hợp đồng của 015 tự dự đoán, lần đầu xảy ra thật (đã chạy thật, không suy diễn)**:
  `dotnet test tests/CrossServiceIsolation.Tests --filter FullyQualifiedName~AuthorizationPolicyDeclaredScanner`
  → `EveryMessageConsumer_DeclaresATrustedSource` và `ScanConsumers_ActuallyExaminesEveryService` đỏ.
  Nguyên nhân: `specs/024-verify-transactional-outbox/` (sau 015) thêm `IConsumer<T>` **đầu tiên** trong
  toàn repo —
  [`services/orders/tests/Orders.Api.IntegrationTests/Support/OrderPlacedVerificationConsumer.cs`](../../services/orders/tests/Orders.Api.IntegrationTests/Support/OrderPlacedVerificationConsumer.cs) —
  có doc-comment giải thích mục đích nhưng không chứa đúng cụm chữ literal `/// Trusted source: ...` mà
  `AuthorizationPolicyDeclaredScanner.cs` (hằng số `TrustedSourceMarker`, dòng 66) đòi hỏi.
  `specs/015-deny-by-default-authz/contracts/message-handler-authorization-contract.md` (mục "Failure
  Modes") tự viết trước đúng kịch bản này: "Handler mới không có khai báo nguồn tin cậy → Scanner thất
  bại, chặn merge (FR-004)" — và `technical-debt.md` mục 015 cũng tự ghi trước rằng `ScanConsumers()`
  "chưa được chứng minh bắt được vi phạm thật" vì lúc viết chưa có `IConsumer<T>` nào trong repo để thử.
  Cả 2 điều đó nay đã đúng: cơ chế hoạt động y như thiết kế, chỉ là hoạt động theo hướng "chặn", và
  chưa ai gắn nhãn.
  **Điểm mơ hồ phạm vi cần chủ sở hữu quyết định**: `OrderPlacedVerificationConsumer` là 1 class TEST
  HELPER (namespace `Orders.Api.IntegrationTests.Support`, dùng để xác minh cơ chế outbox/inbox của
  024, không xử lý sự kiện nghiệp vụ thật), nhưng scanner quét `services/**/*.cs` theo nghĩa đen, không
  phân biệt project production/test. Hợp đồng 015 viết phạm vi là "Mọi kiểu implement `IConsumer<T>`...
  được thêm vào bất kỳ service nào trong `services/`" — không loại trừ tường minh test project. Hai
  hướng xử lý khả dĩ: (a) thêm dòng `/// Trusted source: ...` cho đúng vì đây đúng là 1 consumer đọc từ
  message bus dùng chung (RabbitMQ), kể cả khi chỉ phục vụ test; (b) sửa scanner loại trừ
  `*.IntegrationTests`/`*.Tests`/`*.UnitTests` khỏi phạm vi quét consumer (nhưng vẫn giữ quét route, vì
  route test-only không tồn tại trong repo này). Vì đây là mã test, không phải mã sản xuất, QA không tự
  sửa (đúng nguyên tắc không suy diễn/không tự đổi hành vi) — chỉ ghi nhận để chủ sở hữu quyết định.
  **Xác nhận cơ chế chính (route, không phải consumer) vẫn hoạt động đúng**: tự thêm tạm 1 route
  `/qa-probe-015-temp-route` không khai `.RequireAuthorization()`/`.AllowAnonymous()` vào
  `services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs`, chạy lại
  `EveryMappedRoute_DeclaresAnAuthorizationDecision` → đỏ, nêu đích danh route/file vi phạm (đúng
  `quickstart.md` Scenario 3, đúng "sanity check" `technical-debt.md` mục 015 đã tự làm lúc viết spec);
  `git checkout --` khôi phục, chạy lại → xanh, `git status` sạch.
- **Ghi chú đối chiếu hồi quy (không phải phát hiện mới)**: `technical-debt.md` mục 015 ghi 27/48 test `Bff.Api.IntegrationTests`
  đỏ lúc viết spec (quy cho độ trễ Docker khởi động lại). Chạy lại toàn suite 2026-09-23: **49/50 PASS** (7 phút 16 giây) — chỉ
  1 đỏ, `DownstreamUnavailableTests.EveryRoute_FailsAsAProblemDetails_WhenItsDownstreamIsUnreachable` (`BasketsApi`), thuộc phạm
  vi spec 020, không phải họ test phân quyền của 015; chạy riêng ở QA 020 thì xanh (8/8) → chập chờn do tải.

## 016 — Lan truyền Correlation ID từ Edge đến Frontend

- **[ĐÃ VÁ] 2/4 test trong `CorrelationIdPropagationTests.cs` (gateway) bị gán nhầm "Task nguồn: spec
  002" thay vì spec 016 — lỗi từ lượt dịch comment ở QA 002, tự phát hiện và sửa ở QA 016 (đã xác nhận
  bằng `git show`, không suy diễn)**: QA_Debt mục 002 (ở trên) đã đúng khi tách file
  [`Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs)
  ra khỏi `Bff.Api.IntegrationTests/CorrelationPropagationTests.cs` (file kia mới thật sự của 016) —
  nhưng khi dịch toàn bộ 4 test của file gateway sang tiếng Việt (lúc QA 002), cả 4 đều bị gắn chung
  "Task nguồn: spec 002". `git show 2abe34d --stat -- .../CorrelationIdPropagationTests.cs` (chính commit
  hoàn tất 016) cho thấy diff "72 insertions" — 2 test `ACorrelationIdContainingControlCharacters_...`
  và `ACorrelationIdLongerThan128Characters_...` **không tồn tại trước 016**, được thêm mới hoàn toàn bởi
  chính spec này (khớp `quickstart.md` "Automated Coverage": "`CorrelationIdPropagationTests.cs` (mở
  rộng) — thêm test cho ràng buộc hợp lệ mới, Decision 2"). Chỉ 2 test đầu (`AGeneratedCorrelationId_...`,
  `ACallerSuppliedCorrelationId_...`) thật sự có từ spec 002 (`git show --stat` xác nhận commit `c9db644`).
  Đã tự sửa lại comment của đúng 2 test bị gán nhầm ngay trong file (chỉ sửa comment, không đổi hành vi
  — rebuild + chạy lại 4/4 vẫn PASS). Bài học phương pháp: khi 1 file test có tuổi đời trải dài nhiều
  spec, "file được tạo bởi spec X" không đồng nghĩa "mọi test trong file thuộc về spec X" — cần soi
  từng hàm bằng `git show <commit> --stat` khi file đó được sửa bởi spec sau, không chỉ soi `git log
  --follow` ở mức file.
- **Nhánh async (US1 AC3, FR-004) vẫn chưa có test tự động xác nhận correlation ID xuyên suốt outbox →
  consumer, dù spec 024 đã nối publisher thật (đã đọc mã + đọc test thật, không suy diễn)**:
  `technical-debt.md` (nhóm "Broker/messaging", Amendment 2026-09-12) khẳng định "016: xác nhận thay vì
  còn là giới hạn — payload `OrderPlacedV1` nay thực sự mang `CorrelationId` qua publisher thật (024)" —
  đúng ở mức TĨNH: đọc
  [`services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs:101`](../../services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs#L101)
  xác nhận `httpContext.Items[CorrelationIdMiddleware.HeaderName]` thật sự được gán vào trường
  `CorrelationId` của `OrderPlacedV1` khi publish. Nhưng đọc toàn bộ hạ tầng test outbox/inbox của 024
  ([`Orders.Api.IntegrationTests/Support/OrderPlacedVerificationConsumer.cs`](../../services/orders/tests/Orders.Api.IntegrationTests/Support/OrderPlacedVerificationConsumer.cs))
  cho thấy `ProcessedCounts` (dùng để chứng minh idempotency) chỉ đếm theo `EventId`, không hề đọc hoặc
  so sánh `context.Message.CorrelationId` — grep `CorrelationId` trong toàn bộ
  `Orders.Api.IntegrationTests` (gồm cả `Support/`) trả về 0 kết quả. Nghĩa là Jira Test Scenario 1 gốc
  của SCRUM-26 ("consumer xử lý `OrderPlaced` cũng chứa đúng correlation ID") — chính nhánh mà spec 016's
  research.md Decision 3 tự nhận "chưa xác minh đầy đủ, chờ SCRUM-31" — **vẫn để ngỏ** dù SCRUM-31/024 đã
  xong và có sẵn `VerificationConsumerHost` (bus MassTransit độc lập, đóng vai người tiêu thụ thật) để
  viết đúng bài test đó. Không phải lỗi hành vi (mã đã đúng, xác nhận tĩnh) — là khoảng hở coverage: nếu
  tương lai ai đó vô tình xoá tham số `CorrelationId` khỏi lệnh `Publish(new OrderPlacedV1(...))`, không
  có test nào bắt được. Đề xuất: thêm 1 assertion vào bộ test hiện có của 024 (hoặc 1 test mới cạnh
  `OrderPlacedVerificationConsumer`) so khớp `context.Message.CorrelationId` với giá trị request gốc —
  không cần hạ tầng mới, chỉ cần dùng lại `VerificationConsumerHost` đã có.
- **Ghi chú công cụ (không phải phát hiện về sản phẩm)**: `corepack pnpm ...` chạy từ thư mục gốc repo trên máy QA trỏ nhầm 1 bản
  cache hỏng (`pnpm@12.5.1`, thiếu `bin/pnpm.cjs`) → `Cannot find module …pnpm.cjs`. `frontend/package.json` đã ghim
  `packageManager: "pnpm@9.15.9"`; chạy `corepack pnpm ...` **từ trong thư mục `frontend/`** (để corepack đọc đúng field) là cách
  khắc phục, không cần sửa gì trong repo.

## 017 — Phát telemetry OTel qua ServiceDefaults tới Elastic

- **[NGHIÊM TRỌNG] Mọi histogram metric — bao gồm đúng chỉ số độ trễ FR-009/SC-005 yêu cầu để đo SLO —
  bị `otel-collector` âm thầm loại bỏ hoàn toàn trước khi tới Elasticsearch, chưa từng được phát hiện
  (đã tự dựng stack thật, đọc log collector thật, truy vấn Elasticsearch thật để xác nhận, không suy
  diễn)**: `docker logs ecomerce-local-otel-collector-1` phát cảnh báo lặp lại mỗi ~20 giây:
  `elasticsearchexporter@v0.160.0/exporter.go:352 validation errors ... "dropping cumulative
  temporality histogram \"http.server.request.duration\""` (kèm 15-30 tên histogram khác mỗi lần, gồm
  cả `resilience.polly.pipeline.duration`, `kestrel.connection.duration`...). Xác nhận trực tiếp bằng
  truy vấn `exists: metrics.http.server.request.duration` trên `metrics-generic.otel-default*` →
  **0 kết quả** trên toàn bộ dữ liệu đã thu thập (7 service, hàng nghìn document). Nguyên nhân gốc:
  exporter `elasticsearch` (`mapping.mode: otel`) của `otel-collector-contrib 0.160.0` chỉ chấp nhận
  histogram temporality **delta**; SDK OpenTelemetry .NET (dùng bởi `ServiceDefaults`) mặc định phát
  **cumulative** — `docker/otel-collector-config.yaml`'s pipeline `metrics` chỉ có processor `[batch]`,
  không có `cumulativetodeltaprocessor` hay tương đương để chuyển đổi trước khi xuất. Ảnh hưởng đều
  nhau trên mọi service (không phải lỗi riêng 1 nơi).
  **Đối chiếu trực tiếp với yêu cầu spec**: FR-009 viết "Metrics phát ra bởi ServiceDefaults PHẢI đủ để
  đo các SLO đã khai báo... độ trễ (p95/p99)"; SC-005 viết "...xác nhận được qua Elastic mà không cần
  công cụ đo bổ sung nào khác" — theo đúng nghĩa đen (dữ liệu **metrics**), cả hai đều SAI trên thực tế.
  **Giảm nhẹ tự phát hiện, không suy diễn**: đọc
  [`docs/architecture/021_Architect_khai báo và đo SLO theo từng service.md`](021_Architect_khai%20báo%20và%20đo%20SLO%20theo%20từng%20service.md)
  (dòng 24, 63) cho thấy dashboard SLO của spec 021 (sau 017) đo "Latency p95, p99 **từ traces OTel
  thật**" — tức 021 đã độc lập né được đúng lỗ hổng này bằng cách tính percentile từ trường `duration`
  của span trong `traces-generic.otel-default*` (đã xác nhận có mặt, đơn vị nanosecond, trong dữ liệu
  trace đo được ở lượt QA này) thay vì dùng data stream `metrics`. Kết quả cuối (SRE đo được p95/p99
  qua Elastic) vẫn đạt trên thực tế nhờ lối đi vòng này — nhưng không đạt được bằng cơ chế FR-009 mô tả
  (metrics), và nếu sau này có công cụ/dashboard nào đọc trực tiếp histogram OTel chuẩn (thay vì tự
  tính lại từ trace như 021 đã làm), lỗ hổng sẽ lộ ra ngay. Đề xuất hướng vá: thêm processor
  `cumulativetodeltaprocessor` (có sẵn trong `otel-collector-contrib`) vào pipeline `metrics` của
  `docker/otel-collector-config.yaml`, hoặc đổi cấu hình OTel SDK phía `ServiceDefaults` sang phát
  delta temporality trực tiếp.
- **Sự cố hạ tầng không liên quan 017, tái hiện độc lập hồ sơ đã có ở mục 026**: trong lúc làm sống
  Scenario 5 (gỡ `ServiceDefaults`), `identity-api` bị recreate nhiều lần và mỗi lần đều báo
  `Duende.IdentityServer...KeyManager: CryptographicException: The key {...} was not found in the key
  ring` — dẫn tới `401`/`504` dây chuyền ở mọi service khác (JWKS backchannel timeout). Đúng hiện tượng
  `technical-debt.md` mục 026 đã ghi ("dấu hiệu race/deadlock khi identity-api tự sinh signing key...
  chưa kết luận nguyên nhân gốc") — bằng chứng mới ở đây cho thấy nguyên nhân liên quan tới vòng đời
  container (Data Protection key ring không sống sót qua lần khởi động lại của `identity-api`, trong
  khi khóa ký IdentityServer đã lưu trong `identity-db` từ trước), không chỉ riêng tải đồng thời như
  026 nghi ngờ ban đầu. `docker compose restart identity-api` (+ `gateway-api`/`bff-api` để xoá JWKS
  cache cũ) khắc phục ngay. Đề xuất liên kết bằng chứng này với mục 026 khi có ai điều tra nguyên nhân
  gốc.
- **Lệch nhỏ giữa tài liệu và mã (không ảnh hưởng hành vi)**: `research.md` Decision 7 và
  `contracts/otel-collector-elasticsearch-export-contract.md` viết "cùng một tag 8.x" cho Elasticsearch/Kibana, còn
  `docker-compose*.yml` thực tế ghim `9.4.4` (cả 2 image cùng tag — đúng phần "cùng một tag"; `otel-collector` ghim
  riêng `0.160.0`). `research.md` tự nói không đóng đinh số phiên bản vì có thể lỗi thời lúc triển khai, nhưng contract viết sau
  triển khai chưa được cập nhật.
- **Ghi chú phương pháp luận QA (không phải lỗi sản phẩm)**: khi làm sống kịch bản "sửa mã → rebuild → quan sát", lần
  `docker compose up -d --build products-api` có cache đầu tiên vẫn cho ra span `Products.Api` dù đã comment
  `AddServiceDefaults()`/`UseServiceDefaults()` (container chạy code cũ; nghi do 2 lượt `docker compose up --build` chạy đè
  nhau khi dựng stack để lại trạng thái cache BuildKit không nhất quán — cũng gây xung đột tên container). Chỉ
  `docker compose build --no-cache products-api` rồi `up -d` mới phản ánh đúng thay đổi (image ID đổi `917e16…` → `99e580…`).
  Bài học: khi kết quả không khớp dự đoán, trước tiên xác nhận image thật sự mới bằng
  `docker images <tên> --format "{{.ID}} {{.CreatedAt}}"` và cân nhắc `--no-cache`; không chạy 2 `docker compose up` cùng lúc.
- **Môi trường**: `nginx:alpine` không pull được từ Docker Hub lúc dựng stack (timeout) nên bỏ `storefront` khỏi lượt QA;
  `grep -r` của shell treo bất thường khi quét toàn `services/` (gồm `bin/`/`obj/`) — dùng công cụ Grep của agent thay thế.

## 018 — Secrets qua Cluster Secret Store, loại bỏ cấu hình hardcode

- **[NGHIÊM TRỌNG] Cổng `ci/secret-scan` hiện sẽ ĐỎ ngay nếu chạy thật — 36 phát hiện gitleaks mới
  (34 fingerprint) không nằm trong `.gitleaks-baseline.json`, chưa từng được review (đã tự chạy
  gitleaks thật qua Docker — máy QA không cài sẵn CLI — không suy diễn từ tài liệu)**:
  ```
  docker run --rm -v "$(pwd)":/repo zricethezav/gitleaks:latest detect --source /repo \
    --config /repo/.gitleaks.toml --baseline-path /repo/.gitleaks-baseline.json \
    --log-opts="--all" --redact
  ```
  → `leaks found: 36`. Đã xác nhận `.gitleaks-baseline.json` đúng 9 fingerprint như tài liệu mô tả
  (toàn bộ thuộc 5 `appsettings.Development.json` trước khi 018 xoá) — 36 phát hiện này hoàn toàn MỚI,
  phát sinh từ các spec SAU 018 (009-011's `pacts/*.json`, và chính 018's tài liệu/test tự trích dẫn
  lại các mẫu credential làm ví dụ) mà không ai chạy lại gitleaks để cập nhật baseline. Phân rã: rule
  `connection-string-password` (28 — hầu hết là chuỗi ví dụ trong `RequiredSecretsValidationTests.cs`
  và trong chính văn bản `docs/architecture/018...`/`development/018...`/`onboarding/11-...`/ADR-0007/
  `specs/018-.../{research,quickstart,tasks,contracts}.md` mô tả lại giá trị ĐÃ BỊ XOÁ), rule `jwt`
  (7 — token ví dụ PactNet ghi literal vào phần "request" của `pacts/bff-{baskets,orders,products}.json`
  dù `matchingRules` verify bằng regex, không dùng giá trị cố định — xem thêm QA_Debt mục 011), rule
  `generic-api-key` (1 — `SecurityStamp` GUID ngẫu nhiên trong 1 EF migration seed, bị heuristic entropy
  nhận nhầm). Lấy mẫu xác nhận không phải secret thật: JWT trong pact ký bằng khoá test dùng chung toàn
  bộ suite (`TestJwtBearer`, không phải secret sản xuất); chuỗi trong unit test không kết nối database
  thật; GUID trong migration không phải mật khẩu.
  **Vẫn là vi phạm thật đối với SC-001/FR-004 theo nghĩa đen** ("0 phát hiện") — không phải vấn đề "false
  positive nên bỏ qua được", vì baseline hiện có chính là cơ chế spec 018 tự thiết kế để phân biệt
  "đã review, chấp nhận" khỏi "chưa ai xem qua", và 36 phát hiện này rơi vào nhóm sau. Kết hợp với
  [QA_Debt mục 013](#013--cổng-chất-lượng-ci) (CI thật đã ngừng chạy ~3 tuần) tạo thành 1 "khoá kép"
  chưa ai lường trước: ngay cả khi CI được khôi phục hôm nay, `ci/secret-scan` sẽ chặn merge của MỌI PR
  ngay lập tức cho tới khi có người chủ động rà soát + cập nhật baseline — khác hẳn kỳ vọng "cổng đã
  sẵn sàng chờ CI chạy lại là dùng được ngay". Đề xuất hướng vá (cần chủ sở hữu quyết định, QA không tự
  sửa): (a) chạy `gitleaks detect` 1 lần, review đủ 34 fingerprint, cập nhật baseline; (b) cân nhắc thu
  hẹp rule `connection-string-password` bỏ qua `*.md`/`*Tests.cs`/`pacts/` — phạm vi gốc FR-001 chỉ nói
  "mã nguồn, appsettings.json, Dockerfile", không nói tài liệu; (c) đưa bước "gitleaks + cập nhật
  baseline" thành yêu cầu bắt buộc trước mỗi lần merge, không chỉ làm 1 lần lúc viết spec 018.

## 019 — Liveness/Readiness Probe cho mọi service

*Cả 3 phát hiện dưới đây nằm ở "lớp kiểm tra 2" (`scripts/ci/lint-deployment-manifests.sh`) — phần mà
chính `technical-debt.md` mục 019 tự thừa nhận "CHƯA từng chạy thật trong chính phiên triển khai —
Ansible không chạy native trên Windows (`WinError 87`), WSL thiếu quyền `sudo`". Lần đầu tiên chạy thật
là ở lượt QA này, qua Docker (bỏ qua đúng rào cản Windows/WSL đó). Không phát hiện nào ảnh hưởng hành vi
runtime thật — 58/58 test C# lớp kiểm tra 1 vẫn xanh, và bằng chứng cluster `kind` thật của phiên gốc
không bị ảnh hưởng.*

- **[NGHIÊM TRỌNG] `ansible-lint` báo ĐỎ thật với 7 vi phạm — CI stage `deployment manifest lint` sẽ
  dừng ngay dòng đầu tiên nếu được kích hoạt (đã tự chạy thật qua Docker, exit code 2)**:
  ```
  docker run --rm -v "$(pwd)":/code -w /code pipelinecomponents/ansible-lint:latest \
    ansible-lint roles/service_deployment deploy.yml
  ```
  → `var-naming[no-role-prefix]` × 6 (`service_name`, `service_vars`, `probe_defaults`, `probe_group`,
  `resolved_liveness`, `resolved_readiness` — biến của role phải có tiền tố `service_deployment_`) +
  `name[template]` × 1 (`"Áp dụng manifest {{ service_name }} vào cluster"` — Jinja phải nằm cuối tên
  task). `lint-deployment-manifests.sh` dùng `set -eu` và gọi `ansible-lint ...` không bọc `if` — exit
  2 dừng script ngay tại đây, không bao giờ chạy tới phần render/kubeconform phía sau. Đây là style
  violation (không phải lỗi hành vi — manifest vẫn render đúng, test C# vẫn xanh), nhưng đủ để làm ĐỎ
  cả stage nếu bật.
- **[NGHIÊM TRỌNG] `--limit "$service"` trong vòng lặp per-service của chính script CI không khớp được
  host nào — luôn exit 0 "thành công giả", rồi `kubeconform` fail vì thiếu file, chẩn đoán sai hướng
  (đã tự chạy thật, xác nhận từng bước)**: `deploy.yml` khai `hosts: localhost` + `loop: "{{ services |
  dict2items }}"` — không có host nào tên `orders`/`parties`/... để `--limit` khớp. Chạy
  `ansible-playbook deploy.yml --check --diff --limit orders` → `[WARNING]: Could not match supplied
  host pattern, ignoring: orders`, `skipping: no hosts matched`, **exit code 0**. Đoạn
  `if ! ansible-playbook ... --limit "$service" >/dev/null; then failed=...(render)` do đó KHÔNG BAO
  GIỜ kích hoạt; file `.rendered/${service}.deployment.yaml` KHÔNG BAO GIỜ được tạo; `kubeconform` chạy
  tiếp báo `lstat ...: no such file or directory` (exit 1, xác nhận trực tiếp bằng `kubeconform` chạy
  tay), script ghi nhận `${service}(kubeconform)` — trông như "manifest không hợp lệ theo schema
  Kubernetes" trong khi sự thật là "chưa từng render được file nào". Nếu phát hiện đầu (ansible-lint)
  được vá, script vẫn sẽ ĐỎ tiếp — nhưng thông báo lỗi sẽ dẫn người sửa đi sai hướng (sửa nội dung
  template thay vì sửa logic filter service).
- **`--check --diff` (dry-run) tự mâu thuẫn với chuỗi task `template` → `k8s` của chính role, độc lập
  với phát hiện trên (đã tự chạy thật, bỏ `--limit`)**: `ansible.builtin.template` ở `--check` chỉ
  hiển thị diff, không ghi file thật (đúng bản chất dry-run); task kế `kubernetes.core.k8s` cần đọc
  lại chính file đó (`src: "{{ playbook_dir }}/.rendered/{{ service_name }}.deployment.yaml"`) →
  `fatal: ... Could not find or access '/code/.rendered/parties.deployment.yaml'`. Playbook dừng ngay
  ở service đầu tiên (`parties`), 6 service còn lại không bao giờ được thử. Sửa lỗi `--limit` ở trên
  KHÔNG tự động sửa lỗi này — cần thay đổi cấu trúc role (tách "render ra file" khỏi "áp dụng vào
  cluster") hoặc bỏ `--check` khỏi kịch bản lint. Giải thích tại sao `technical-debt.md` mục 019 có
  bằng chứng cluster `kind` thành công: phiên đó chạy `ansible-playbook` KHÔNG kèm `--check`, không
  chạm lỗi này; chính script CI mới dùng `--check` (hợp lý cho lint không chạm cluster) và chính lựa
  chọn đó đối đầu với thiết kế của role.
  **Đề xuất hướng vá cho cả 3** (cần chủ sở hữu quyết định, QA không tự sửa mã sản xuất): (a) đổi 6
  biến thành tiền tố `service_deployment_*`, đưa Jinja ra cuối tên task; (b) bỏ `--limit "$service"`
  khỏi vòng lặp script, thay bằng cách truyền `services` được lọc qua `-e` hoặc tách playbook riêng
  cho lint offline; (c) thêm điều kiện `when: apply_to_cluster | default(true)` bọc quanh
  `kubernetes.core.k8s`, để kịch bản lint chạy render-rồi-dừng mà không cần `--check` hay cluster thật.

## 020 — Timeout, retry và circuit breaker cho mọi cuộc gọi ra ngoài

- **[ĐÁNG KỂ] Circuit breaker BFF→4 downstream chưa từng có ngưỡng mở được đặt tường minh — mặc định
  Polly `MinimumThroughput = 100` (trong cửa sổ `SamplingDuration` 10 s) khiến mạch không mở ở lưu lượng
  thường (đã tự làm sống trên stack Docker thật, đọc sự kiện Polly thật trong Elasticsearch)**:
  `services/bff/src/Bff.Api/DownstreamClients/DownstreamClientRegistrationExtensions.cs` chỉ đặt
  `CircuitBreaker.SamplingDuration`; `FailureRatio` (0.1), `MinimumThroughput` (100), `BreakDuration` (5 s)
  giữ mặc định — đã grep `.cs/.json/.md` toàn services/shared/tests/specs/020/docs: không nơi nào đặt hay nêu
  `MinimumThroughput` ngoài 1 dòng ghi chú ở `tasks.md` T025 ("cần khoảng 100 request lỗi liên tiếp ... chưa
  thử ở quy mô đó"). Thí nghiệm: `docker compose stop baskets-api`, gọi `GET /bff/basket` qua gateway —
  8 request tuần tự (~30 s) đều `504` sau 3.2-4.0 s (mạch KHÔNG mở, mỗi request chờ trọn
  `TotalRequestTimeout`); dồn 150 request/50 song song thì mạch mở: 116×`502` + 34×`504`, rồi các GET đơn lẻ
  kế tiếp trả `502` trong **36-77 ms**; Elastic ghi `OnCircuitOpened`, `OnCircuitHalfOpened`,
  `OnCircuitClosed` (chu kỳ đủ sau khi `start baskets-api`). Hệ quả: fail-fast (FR-003, SC-003, US2-KB2) chỉ
  hoạt động khi ≥ ~10 request/giây tới đúng 1 downstream; dưới ngưỡng đó — mọi môi trường dev/QA và nhiều
  môi trường thật — 1 downstream sập không được ngắt mạch. Rất có thể là 1 nguyên nhân của phát hiện ở
  `technical-debt.md` mục 025 (breaker `OrdersApiClient`/`BasketsApiClient` "chưa từng trip" qua 4 lần
  thử, chỉ 8-9 lỗi rải rác — 025 quy cho tiêm độ trễ không chặn thread và không xét ngưỡng này). Gateway→BFF
  dùng `MinimalTotalCountThreshold` mặc định 10 của YARP nên mở sớm hơn (~10 request). Đề xuất: đặt tường
  minh `MinimumThroughput` (vd 5-10), `FailureRatio`, `BreakDuration` và ghi vào `research.md`; hoặc ghi rõ
  ngưỡng ≥100 request/10 s vào tài liệu.
- **FR-007 ("kể cả điểm gọi mới thêm sau này") chưa được thực thi bởi cơ chế tự động — `Orders.Api →
  RabbitMQ` (spec 024) là ví dụ sống**: `ResilienceCoverageScanner.ExpectedCallSites` là danh sách viết tay;
  scanner chỉ kiểm tra marker trong file đã liệt kê, không tự phát hiện `AddHttpClient`/`AddMassTransit`
  mới. `Orders.Api/Program.cs:62-93` (`AddMassTransit ... UsingRabbitMq`, thêm bởi 024 sau 020) không có
  `UseMessageRetry`/`UseCircuitBreaker`/timeout kết nối tường minh, không nằm trong inventory, và
  `Scan_ReportsNoViolations_ForCurrentInventory` vẫn xanh 5/5. Đúng loại điểm gọi FR-001 nêu đích danh
  ("service gửi message tới broker"); Amendment 024 ở `technical-debt.md` đã tự ghi "chưa rà soát ... chưa
  có ticket riêng" nhưng không test nào bắt nên sẽ không tự lộ. Giảm nhẹ: Bus Outbox đưa `Publish` trên
  đường request thành 1 lệnh ghi DB cùng transaction nên broker chậm/sập không làm treo `POST /orders`; rủi
  ro còn lại ở hosted service giao outbox chạy nền. Đề xuất: thêm test quét `services/**/*.cs` tìm
  `AddHttpClient`/`AddMassTransit`/`UsingRabbitMq` rồi so với `ExpectedCallSites`.
- **`architecture/020` và `technical-debt.md` mục 020 lỗi thời về Bước 6 quickstart/T025**: `tasks.md` ghi
  T025 `[X]`, 27/27 task, kèm bằng chứng Elastic thật ngày 2026-09-12 (commit `a695c6f`, `development/020` đã
  nhắc), nhưng `architecture/020` (dòng 12-15, 81) và phần giới hạn phạm vi của `technical-debt.md` vẫn
  viết "26/27 task, Bước 6 chưa thực hiện được ... chưa có bằng chứng runtime thật". QA đã tự xác nhận độc lập
  Bước 6 (Polly `OnRetry` 155 / `OnTimeout` 196 / 3 sự kiện mạch trong `logs-generic.otel-default*`). Liên
  quan QA_Debt mục 017: histogram `resilience.polly.strategy.attempt.duration`/`resilience.polly.pipeline.duration`
  bị collector loại bỏ (`dropping cumulative temporality histogram`) — sự kiện Polly tới Elastic qua log,
  nhưng phân bố thời gian từng attempt thì không.

## 021 — Khai báo và đo lường liên tục SLO theo từng service

*Nửa "khai báo" (US1/US2) đạt đầy đủ: 29/29 test xanh, Bước 2 quickstart tự làm sống (đổi p95 của `orders`
→ đúng 1 test đỏ nêu `orders` → revert → xanh), hằng số mặc định khớp hiến chương. Các phát hiện dưới đây
thuộc nửa "đo liên tục" (US3, dashboard) — đã import dashboard vào Kibana thật và tự chạy đúng công thức của
nó trên Elasticsearch (trình duyệt tích hợp từ chối `localhost:5601` nên không render được).*

- **[NGHIÊM TRỌNG cho FR-006/SC-005] Span health-probe khiến service KHÔNG có traffic thật vẫn hiển thị "0 %
  lỗi, p95 ≈ 1 ms" thay vì "không có dữ liệu"**: công thức dashboard (`count(kql='...status_code >= 500') /
  count()`, `percentile(duration, …)`) không lọc route nên đếm cả `/health/live`, `/health/ready` (Docker/K8s
  probe mỗi vài giây; spec 019 đặt probe cho cả 7 service). Đo thật, 30 phút không có request người dùng nào:
  `Bff.Api` 65 span `Server` = 65 `/health/ready`; `Orders.Api` 65/65 `/health/ready`; `Gateway.Api` 65/65
  `/health/live`; bucket idle 20 phút của `Bff.Api`: `n=161, err=0.0 %, p95=0 ms`. Service idle do đó hiện
  "đạt hoàn hảo" — đúng điều FR-006/SC-005/contract bất biến 3 cấm — và khi có ít traffic thật, span probe
  (luôn 200, ~1 ms) pha loãng error-rate và p95 về phía đẹp hơn thực tế. Quickstart Bước 5 gốc chỉ kiểm
  khoảng thời gian **trước khi hệ thống tồn tại** (2020 → 0 hit → "No results found", đã tự tái hiện) — trường
  hợp rỗng hoàn toàn, không phải trường hợp service còn sống nhưng không có người dùng. Đề xuất: lọc
  `attributes.http.route` khác `/health/*` (và `kind: Server`) trong mọi công thức, hoặc thêm điều kiện số
  request tối thiểu để hiện "không có dữ liệu".
- **Công thức đếm MỌI loại span thay vì chỉ request đến (`kind: Server`) — số đo lệch khỏi định nghĩa SLO
  ("5xx dưới 0.1 % số request") ở Gateway/BFF**: 5 domain service gần như không có span `Client` nên lệch không
  đáng kể; Gateway/BFF thì có nhiều lời gọi ra. Đo thật 24 h: error-rate Gateway 19.19 % (công thức dashboard)
  vs 11.02 % (chỉ span Server); p95 Gateway 4518.9 vs 3403.9 ms (+33 %), p95 BFF 1323.7 vs 1047.1 ms (+26 %);
  error-rate BFF 10.14 % vs 11.09 % (chiều lệch không cố định nên không phải "lệch an toàn"). Số cao do đợt
  ngắt `baskets-api` ở QA 020 nhưng cơ chế lệch là cấu trúc. Đề xuất lọc `kind : "Server"` trong Lens Formula và
  panel theo ngày.
- **Ngưỡng trên dashboard là chữ cứng trong tên cột, không nối với manifest, và không test nào giữ chúng
  khớp**: cột ghi "ngưỡng 150ms; riêng Bff.Api 300ms", "ngưỡng ≤ 0.1%, cả 7 service" (phương án (c) đã chốt có
  lý do ở `06-dashboard...md`). 1 service sau này khai ngoại lệ có `slos.justification` (đúng cơ chế US2) thì
  test manifest vẫn xanh nhưng nhãn dashboard giữ ngưỡng cũ; 29 test chỉ đọc manifest, không đọc `.ndjson`.
  Test cũng chỉ so 4 giá trị cấp service, không kiểm `latency` theo từng endpoint trong `endpoints:`. Chi tiết
  lỗi thời nhỏ đi kèm: `services/gateway/src/Gateway.Api/service-manifest.yaml` vẫn ghi
  `authentication: anonymous  # no identity server yet` cho route catch-all, trong khi từ spec 014 gateway đòi
  Bearer (QA 014 đo `401` không token).

## 023 — Rà soát N+1 query, truy vấn không giới hạn và thiếu phân trang

*Mọi tiêu chí happy-case đạt (đã gieo 500 sản phẩm vào stack Docker rồi dọn sạch; mặc định 20, trần 100, render giỏ đúng
1 span `GET /products`, 23+8+22+1 test xanh). Các phát hiện dưới đây là điểm yếu của bộ test và của xử lý đầu vào.*

- **[NGHIÊM TRỌNG] Hồi quy ở đúng "khoảng hở nặng nhất" của spec (BFF render giỏ over-fetch) không làm test nào đỏ
  (đã tự mutate, `git checkout --` hoàn tác)**: đổi `BasketsEndpoints.cs:129` từ `GetProductsByIdsAsync(distinctProductIds, …)`
  sang `GetProductsAsync(1, 100, …).Items` (vẫn 1 lời gọi HTTP, chỉ lấy 100 mục đầu catalog) → `Bff.Api.UnitTests` 22/22 xanh,
  `QueryCoverageTests` 8/8 xanh. Lý do: (1) `ProductLookupBatchingTests` chỉ đếm `InvocationCount == 1` — code CŨ trước bản sửa
  cũng 1 lời gọi (toàn catalog) nên test không phân biệt bản sửa với lỗi gốc, và tự ghi "no assertions on the request itself";
  (2) scanner `ScanBoundedQuerySites` kiểm chuỗi `GetProductsByIdsAsync` có mặt trong file, mà route add-item (dòng 57) vẫn
  dùng nó nên marker còn dù đường render đã đổi; `ProductsEndpointPaginationTests` chỉ chứng minh hình dạng request của
  client. Hệ quả thực: catalog > 100 mục thì tên sản phẩm ngoài 100 mục đầu không join được — lỗi chỉ lộ ở dữ liệu lớn.
  Đề xuất: cho handler giả bắt `RequestUri`, assert query chứa `ids=` và không có `pageSize`.
- **Đầu vào `page`/`pageSize` bất thường gây `500`/`502` thay vì `400`; tràn số nguyên → `OFFSET` âm (đã tự tái hiện trên
  stack thật, 503 sản phẩm)**: `?pageSize=abc`, `?page=abc`, `?pageSize=99999999999` → BFF `500`
  (`BadHttpRequestException: Failed to bind parameter "Nullable<int> pageSize" from "abc"` bị exception handler biến thành
  500, đáng ra 400). `?page=2147483647&pageSize=100` → Products `SqlException: The offset specified in a OFFSET clause may not
  be negative.` do `(effectivePage - 1) * effectivePageSize` nhân 2 `int` tràn số (`CatalogEndpoints.cs`) → Products 500 → BFF
  retry `GET` 2 lần (spec 020) → `502`; đo: 9 `SqlException` cho 3 request thử — 1 request có token hợp lệ tạo 3 lỗi 5xx ở
  Products, cộng vào error-rate SLO (spec 021) và mẫu breaker (spec 020). Test hiện có chỉ phủ `pageSize` = 0/âm/1000000.
  Đề xuất: tính offset bằng `long` + chặn `page` tối đa; trả `400` cho lỗi binding.
- **Nhánh `ids` không chịu trần `MaxPageSize = 100`**: đo trực tiếp `products-api` (`:5088`, có token + `X-Tenant-Id`) —
  100 id → 100 mục, 150 → 150, 200 → 200 (`pageSize=5` bị bỏ qua hoàn toàn); chỉ bị chặn bởi độ dài request line của Kestrel
  (~220 GUID). `research.md` Decision 4 chủ đích để `ids` bị chặn bởi tập id, nhưng FR-004/US3-KB1/SC-003 viết "không vượt
  quá mức trần bất kể giá trị client truyền vào". BFF không mở `ids` cho SPA nên rủi ro thấp; Products publish cổng `5088` ở
  `docker-compose.local.yml`. Đề xuất: ghi ngoại lệ vào FR-004 hoặc kẹp số id tối đa.
- **`quickstart.md` Bước 3 chạy 0 test và thoát mã 0 ("xanh giả")**: lệnh trỏ `Products.Api.UnitTests` (chỉ có
  `HealthCheckTests`) với filter `ProductListingPaginationTests` (thực tế nằm ở `Products.Api.IntegrationTests`); đã chạy
  đúng lệnh → "A total of 1 test files matched" rồi exit 0, không test nào chạy. Phần "Kết quả xác thực" cuối chính quickstart
  tự ghi Bước 3 "nằm trong 23/23 ở Bước 2". Đề xuất sửa đường dẫn sang `Products.Api.IntegrationTests`.

## 024 — Xác minh outbox pattern giao dịch trên dịch vụ phát sự kiện đơn hàng

*Happy-case đạt cả trên test lẫn trên stack thật: 4/4 test outbox và 29/29 toàn `Orders.Api.IntegrationTests` xanh; 20 `POST /orders` đồng thời → 20 đơn, 20 bản ghi outbox, 20 message riêng biệt tới RabbitMQ, 0 tồn đọng; broker sập + `docker kill orders-api` → khởi động lại tự giao message. Các phát hiện dưới đây là điểm yếu của bộ test, cấu hình triển khai và tài liệu. Toàn bộ mutation đều đã `git checkout --` hoàn tác.*

- **[NGHIÊM TRỌNG] "Cùng một transaction" (FR-001) ở đường `POST /orders` thật không được test nào bảo vệ (đã tự mutate)**: thêm 1 dòng `await dbContext.SaveChangesAsync(cancellationToken);` ngay sau `dbContext.Orders.Add(order)` trong `OrderEndpoints.cs` (đơn hàng commit ở transaction riêng, tách khỏi bản ghi outbox — sập giữa 2 lần commit là mất sự kiện, đúng điều spec cấm) → **4/4 test outbox vẫn xanh**. Lý do: `PlaceOrder_WritesTheOrder_AndTheOutboxRecord_InTheSameTransaction` chỉ khẳng định "sau khi xong, cả 2 hàng cùng tồn tại"; `PlaceOrder_WhenTheTransactionRollsBack_…` không đi qua endpoint mà tự dựng `OrdersDbContext` + `IPublishEndpoint`, nên không bảo vệ logic ghi thật. Đề xuất: test qua HTTP với 1 lỗi ép ở `SaveChanges` (interceptor/trùng khoá) rồi assert cả `Orders` lẫn `OutboxMessage` không đổi.
- **Test rollback rỗng nghĩa khi bus outbox bị tắt (đã tự mutate)**: comment `o.UseBusOutbox();` trong `Program.cs` → 2 test đỏ (`PlaceOrder_WritesTheOrder…` với `Collection: []`, `…CrashRecovery…` với `Expected: 1, Actual: 0`) nhưng `PlaceOrder_WhenTheTransactionRollsBack_…` và `Consumer_ProcessesTheSameRedeliveredMessage_ExactlyOnce` vẫn **xanh** — bảng `OutboxMessage` luôn rỗng nên `0 == 0`; test rollback không khẳng định outbox từng có hàng trong đơn hợp lệ trước đó ở mức "đúng 1". Đề xuất: assert `outboxCountAfterFirstOrder == 1` trước khi thử đơn trùng, và assert số hàng outbox đúng bằng 1 ở test đầu (US1-KB3/SC-001 nói "đúng một").
- **Test crash-recovery không khẳng định premise và không giữ cấu hình chu kỳ quét (đã tự mutate)**: (1) gán cứng `QueryDelay = 1s` trong `Program.cs` (bỏ qua `Outbox:QueryDelaySeconds`) → test vẫn **xanh** — Host A có thể tự gửi trước khi bị huỷ mà test không phát hiện, vì chỉ đếm `ProcessedCounts.Count` toàn cục (static, chung mọi sự kiện) tăng sau khi Host B khởi động, không khẳng định "chưa gửi trước Host B"; (2) ngược lại bỏ Host B → test **đỏ sau ~48 s** (`Host B never delivered…`), nên test thật sự phụ thuộc vào lần khởi động lại. Chỉ chứng minh 1 lần khởi động lại (US2-KB3 "nhiều lần" không có test); "đánh dấu đã gửi để không gửi lại" (US2-KB2) không có test.
- **5 kịch bản chấp nhận không có test tự động**: US1-KB3 (nhiều yêu cầu đồng thời), US2-KB2, US2-KB3, US3-KB2 (2 bản sao gần như đồng thời — test chỉ publish 2 lần nối tiếp), US3-KB3 (2 sự kiện khác nhau của cùng 1 đơn). SC-001 "100% đơn hàng" chỉ được thử trên 1 đơn. Đã tự làm sống US1-KB3 trên stack thật (20 POST song song với `X-Correlation-Id` riêng): 20 `201`, `Orders` 3 → 23, `OutboxMessage` về 0 sau ~4 s, 20 message vào queue tạm (bind vào exchange `EventContracts:OrderPlacedV1`) với 20 `messageId`/`orderId`/`eventId` khác nhau và `correlationId` trong payload = đúng `X-Correlation-Id` đã gửi (20/20) — đồng thời đóng khoảng hở FR-004 của QA 016 ("nhánh async chưa có bằng chứng"). Test idempotency có "cắn": gỡ `UseEntityFrameworkOutbox` khỏi consumer host → `Expected: 1, Actual: 2`.
- **`orders-api` của `docker-compose.local.yml` không được cấu hình RabbitMQ → outbox không bao giờ giao được (đã đo)**: chỉ `docker-compose.yml:326` có `ConnectionStrings__RabbitMq: amqp://guest:guest@rabbitmq:5672`; `docker-compose.local.yml` (stack QA/local đang chạy) không có, nên `orders-api` rơi về mặc định `localhost` — log có 411 dòng `Connection Failed: rabbitmq://localhost/` sau ~8 giờ; bản ghi outbox từ đơn của QA 017 kẹt suốt 7+ giờ (`EnqueueTime` NULL), đơn mới cũng chỉ tích luỹ thêm. `POST /orders` vẫn `201` trong 0.33 s (đúng FR-007) và `/health/ready` vẫn healthy (cố ý lọc, xem `HealthCheckEndpoints.cs`), nên **không có tín hiệu nào ngoài log**. Thêm `ConnectionStrings__RabbitMq` qua file override tạm rồi tạo lại container → cả 2 message kẹt được giao ngay (exchange `publish_in: 2`), bảng outbox về 0. Tương tự K8s: `deploy/k8s/orders/external-secret.yaml` chỉ khai `ConnectionStrings__OrdersDb`, không có cấu hình RabbitMQ nào trong `deploy/` (kể cả role Ansible) — khi triển khai thật `orders` sẽ ở đúng trạng thái này. Đề xuất: bổ sung biến vào `docker-compose.local.yml` và `ExternalSecret`; cân nhắc metric/health "outbox backlog" thay vì chỉ warn log.
- **Tài liệu mô tả sai ý nghĩa cột outbox**: `specs/024-verify-transactional-outbox/data-model.md` dòng 16 và `quickstart.md` Kịch bản 1 bước 3 viết "`SentTime` còn rỗng" = chưa gửi. Thực tế MassTransit 8.5.4 ghi `SentTime` ngay lúc xếp hàng (đo: `2026-09-24 14:06:48` có giá trị dù chưa gửi); chưa gửi = `EnqueueTime` NULL, và sau khi giao bản ghi bị xoá luôn (0 hàng sau ~5 s) thay vì "đánh dấu đã gửi" như US2-KB2/spec-summary mô tả — kết quả tương đương (không gửi lại) nhưng ai kiểm tay theo quickstart sẽ tìm sai cột. Đề xuất sửa 2 chỗ trên.
- **Quickstart 3 kịch bản thủ công không tái hiện nguyên văn được**: Kịch bản 1 khó "dừng trước khi log delivered" vì chu kỳ quét mặc định 1 s (đã tái hiện bằng cách dừng RabbitMQ + `docker kill`, xem trên: `POST` `201` trong 0.91 s, bản ghi còn nguyên sau khi kill, khởi động lại thì được giao); Kịch bản 2 cần "queue của consumer xác minh" nhưng consumer chỉ tồn tại trong test (production chưa có) — phải tự tạo queue tạm bind vào exchange; Kịch bản 3 (ép lỗi HTTP sau khi transaction bắt đầu) không thực hiện được qua API thật, chỉ qua test. Đề xuất ghi rõ 3 điểm này vào quickstart.
- **Ghi chú phương pháp**: tải trọng thủ công gọi thẳng `orders-api :5041` cần thêm `X-Tenant-Id` **và** `X-Subject-Id` (thiếu `X-Subject-Id` → `500` `MissingCallerContextException`, không phải `401/400`); `Orders.Api.IntegrationTests` mỗi lần chạy dựng SQL Server + RabbitMQ Testcontainers riêng (~2.6 phút cho 4 test outbox, 29 test toàn suite chạy được cùng lúc với stack Docker đang bật). Dữ liệu QA (22 đơn thêm, queue `qa024`) đã dọn.

## 025 — Diễn tập chaos engineering: giết pod / tiêm độ trễ

*Cơ chế tiêm độ trễ đúng hợp đồng (đã đo sống Bất biến 1–6 trên `orders-api` thật; 8/8 test đơn vị xanh); kill-pod trên Kubernetes thật với image thật phục hồi ≈ 14 s, lặp lại được; độ trễ tiêm hiện lên trong Elasticsearch trong ~13 s. Các phát hiện dưới đây làm thay đổi cách đọc "kết luận sai lệch" của 3 bản ghi kết quả. Mọi mutation đã `git checkout --` hoàn tác.*

- **[NGHIÊM TRỌNG] Header `X-Chaos-Latency-Ms` không bao giờ tới `orders` khi đi qua gateway/BFF — công cụ tiêm độ trễ không thể làm BFF timeout hay mở breaker (đã đo)**: bật `Chaos__AllowLatencyInjection=true` cho `orders-api`, gọi trực tiếp `GET /orders/{id}` + header `2000` → `2.01 s`; gọi cùng route qua `gateway :5300/bff/orders/{id}` + header `2000` → `0.016–0.031 s` (3/3 lần, như không có header). Không có dòng mã nào ở `services/bff`/`services/gateway` chuyển tiếp header này (grep `Chaos`/`X-Chaos` = 0 kết quả). Hệ quả: Quickstart Bước 2 ("`AttemptTimeout=1s` < 2 s nên BFF timeout ở lần thử đầu … circuit breaker mở mạch") và Acceptance Criteria SCRUM-34 ("circuit breaker trips") **không thể xảy ra bằng thiết kế hiện tại**, bất kể tải mạnh cỡ nào; 4 lần thử trong bản ghi (`autocannon -c 50` …) đều gửi header THẲNG vào orders, trong khi tải nền qua BFF không mang header nên không bao giờ bị làm chậm. Các `504` quan sát được (7/90, 1/100, 2/120) là nhiễu không liên quan tới độ trễ tiêm. Kết luận trong `2026-09-14-inject-latency.md` ("nguyên nhân là `Task.Delay` không chặn thread") vì vậy là **giả thuyết sai**, và câu hỏi mở ở `technical-debt.md` mục 025 nên được trả lời lại. (Ngay cả khi header được chuyển tiếp thì breaker BFF→orders vẫn cần ≥100 mẫu/10 s — xem QA_Debt mục 020.) Đề xuất: cho gateway/BFF chuyển tiếp header khi cờ bật (allow-list), hoặc tiêm ở phía BFF; sửa lại kết luận của bản ghi.
- **Bản ghi kết quả vi phạm chính hợp đồng của nó**: `exercise-outcome-writeup-contract.md` Bất biến 3 — bản ghi `sai_lech` PHẢI có `jira_ticket` khác rỗng; 3/3 bản ghi ở `docs/dien-tap-chaos-engineering/ket-qua/` đều `sai_lech` với `jira_ticket: (chưa mở …)`. Đây cũng là FR-008, US3-KB2 và SC-004 ("100% … có kết luận 'xác nhận đạt' hoặc 'đã mở bug ticket kèm liên kết'"). Hợp đồng tự ghi "kiểm bằng mắt, không có test tự động" nên không gì chặn được. Đề xuất: mở ticket cho phát hiện ở trên rồi điền link; cân nhắc 1 test/CI check đơn giản quét `ket-qua/*.md`.
- **Test đơn vị chỉ bảo vệ middleware đứng riêng (đã tự mutate)**: chạy `Orders.Api.UnitTests` (8 ca chaos xanh) — chuyển `app.UseMiddleware<ChaosLatencyInjectionMiddleware>()` xuống sau `UseTenancy()` trong `Program.cs` (phá Bất biến 5: độ trễ phải áp dụng TRƯỚC xác thực) → **vẫn xanh**; đổi `"AllowLatencyInjection": false` thành `true` trong `appsettings.json` (phá Bất biến 1: mặc định phải tắt) → **vẫn xanh**. Ngược lại bỏ `Math.Min` (kẹp trần) → `InvokeAsync_HeaderAboveSafetyCap_ClampsToMax` đỏ; bỏ điều kiện `AllowLatencyInjection &&` → `InvokeAsync_InjectionDisabled_IgnoresHeaderEntirely` đỏ. Bất biến 6 chỉ được kiểm ở mức "`next` được gọi", không so status/body. Đề xuất: 1 test tích hợp `WebApplicationFactory` khẳng định cấu hình mặc định tắt + thứ tự middleware (request chưa xác thực vẫn bị trễ khi bật; 401 vẫn trả về).
- **Kill-pod: số liệu cũ (35–42 s) không đại diện; đo lại trên Kubernetes thật với image thật ≈ 14 s**: dựng `baskets` (image `ecomerce-local-baskets-api` nạp vào worker bằng `docker save | docker exec -i desktop-worker ctr -n k8s.io images import -`, probe/`maxUnavailable: 0` đúng mẫu `deployment.yaml.j2`, `initialDelaySeconds: 10`), thăm dò `/health/ready` qua API-server proxy mỗi 0.2 s, `kubectl delete pod` ×3: lần cuối thất bại sau khi xoá **+13.2 s / +14.9 s / +13.8 s** (21/20/21 lần thăm dò lỗi), lặp lại được (SC-001). Phần lớn là `initialDelaySeconds: 10` + chu kỳ readiness 5 s của nhóm db_backed. Bản ghi cũ 35–42 s gồm thao tác thủ công `kubectl cp` + `kubectl exec` (chính bản ghi thừa nhận) nên không phản ánh Kubernetes thuần; nên ghi 1 bản ghi mới với số liệu này.
- **SC-002 ("100% lần kill-pod quan sát được breaker engage") không đạt được với tải nền quickstart đề nghị**: quickstart cho phép "vòng lặp `curl` thủ công là đủ" (~2 req/s); breaker BFF→baskets mặc định cần `MinimumThroughput 100` mẫu/10 s (QA_Debt mục 020) nên với ~2 req/s và cửa sổ gián đoạn ≈ 14 s chỉ thấy `OnRetry`/`OnTimeout`, không bao giờ `OnCircuitOpened` — khớp mọi bản ghi ("breaker không trip"). Cần tải ≥ ~10 req/s để breaker có cơ hội mở (QA 020 đã tái hiện: 150 request/50 song song mở mạch).
- **Dọn dẹp chưa hoàn tất từ lần diễn tập gốc**: namespace `chaos-exercise` và `chaos-exercise-2` vẫn `Terminating` sau 10 ngày trên cluster docker-desktop — `kubectl get ns … -o jsonpath` báo "`service.kubernetes.io/load-balancer-cleanup` còn trong 1 Service" (finalizer của 1 Service loại LoadBalancer không được gỡ). Mục "Dọn dẹp" của quickstart chỉ nói về header và cờ, không nhắc xoá namespace/Service. Không xử lý ở lượt QA này (không phải do QA tạo).
- **Ghi nhận mặt tích cực/thiết kế**: khi cờ bật, request CHƯA xác thực vẫn bị trễ trước khi trả `401` (đo `2.05 s` so với `0.11 s` khi không header — đúng Bất biến 5, có chủ đích), nghĩa là bất kỳ ai với tới cổng service đều làm chậm được tới 30 s/request — chấp nhận được trên cluster diễn tập, không được bật ở nơi khác (Bất biến 1/FR-006). Độ trễ tiêm hiện trong Elasticsearch: 6 request có `X-Chaos-Latency-Ms: 2500` → span `Orders.Api` (`duration ≥ 2 s`, p95 = 3117 ms) truy vấn được sau ≈ 12.7 s kể từ khi request kết thúc (batch OTLP + collector) — đủ "near real time" cho FR-005/SC-003, còn các sai lệch của dashboard nêu ở QA_Debt mục 021.
- **Ghi chú phương pháp**: yêu cầu mở `NodePort` để nối BFF tới pod trên cluster bị chặn bởi chính sách môi trường nên đổi sang Service `ClusterIP` + đo qua API-server proxy của `kubectl` (không nối BFF vào pod k8s; hành vi BFF khi mất baskets đã có ở QA 020). Đã dọn: xoá namespace `qa025`, gỡ image khỏi worker, ngắt worker khỏi mạng compose, tạo lại `orders-api` đúng theo compose gốc. `Orders.Api.UnitTests` có sẵn 1 test đỏ `HealthCheckTests.HealthLive_ReturnsOk` do thiếu `ConnectionStrings__OrdersDb` (đã ghi ở mục 001/009).

## 026 — Kiểm thử tải/hiệu năng luồng trọng yếu đối chiếu ngân sách hiến chương

*Ghi chú đánh số: tài liệu 026 tương ứng thư mục spec `specs/025-load-performance-test-budgets/` (SCRUM-32). Khi có token, cơ chế đo → báo cáo → cổng chạy đúng cả 3 kịch bản Jira: baseline PASS; chèn `Task.Delay(600)` vào `baskets` → FAIL đúng bước `POST /bff/basket/items` (p95 930.3 ms và 675.3 ms so với ngưỡng 300 ms, exit 1); hoàn tác → PASS (exit 0); 6 test thuần xanh. Các phát hiện dưới đây là điểm yếu của chính cổng. Đã hoàn tác mọi mutation và dọn `artifacts/performance` do QA tạo.*

- **[NGHIÊM TRỌNG] Bài kiểm thử tải không đính token nên trên stack hiện tại luôn đỏ — và đỏ sai cách, không có báo cáo (đã chạy thật)**: `GatewayClient.Create()` không gắn `Authorization`; tài liệu (`architecture/026`, `quickstart.md`, `technical-debt.md`) mô tả đây là "khoảng trống xác thực toàn nền tảng, không có cách lấy token". Nay không còn đúng: từ spec 014 có identity thật và tài khoản test `postman-test@local.test` (nạp bằng `TestUserPassword` trong `.env`, `scripts/up.sh` đã dùng để lấy token và làm ấm); tôi lấy được token qua `/connect/token` ngay. Chạy `dotnet test tests/CriticalPathLoadTests` trên stack local: 60/60 request `401`, test đỏ bằng `InvalidOperationException: Sequence contains no matching element` ở `CriticalPathLoadTest.cs:38` (`StepStats.Single(...)` — 3 bước sau không bao giờ chạy nên không có số liệu), **không có báo cáo nào được ghi** (`artifacts/performance/` không tồn tại) — trái tài liệu ("FAIL đúng thiết kế") và trái `load-test-run-contract.md` bất biến 5 (báo cáo phải được ghi vô điều kiện trước khi fail). Thông báo lỗi không nhắc tới 401 hay ngân sách. Đề xuất: đọc token từ biến môi trường (hoặc tự gọi `/connect/token` như `up.sh`), và xử lý bước không có số liệu thành 1 vi phạm có tên thay vì `Single`.
- **Cổng chỉ xét độ trễ của request THÀNH CÔNG — tỷ lệ lỗi không nằm trong cổng và không có trong báo cáo (đã đo)**: `CriticalPathLoadTest.cs` lấy `stepStats.Ok.Latency.Percent95/99`, báo cáo không có cột lỗi/số request. Với token (giỏ hàng dùng chung 1 subject nên các luồng đồng thời đụng nhau; baskets log `DbUpdateConcurrencyException` → `500`, BFF trả `502/504`): 5/60 luồng lỗi ở lần chạy thứ 2, 17/60 ở lần 1, 27/60 ở lần sau khi khởi động lại, 4/60 ở lần ổn định — nhưng báo cáo ghi `Overall: PASS` khi các bước còn lại nhanh (vd `Overall: PASS`, checkout p95 110.5 ms với 5 luồng lỗi). Nghĩa là 1 hồi quy "một phần request lỗi nhanh" (5xx trả về trong vài ms) không làm cổng đỏ — trái tinh thần FR-004/SC-002. Phần lớn lỗi ở bước checkout còn cho thấy 1 vấn đề thật của baskets: sửa đồng thời cùng 1 giỏ trả `500` (`DbUpdateConcurrencyException`) thay vì `409`/thử lại. Đề xuất: thêm ngưỡng tỷ lệ lỗi tối đa vào `BudgetAssertions` và cột lỗi vào báo cáo.
- **Lần chạy đầu sau khi khởi động/triển khai dịch vụ thường đỏ do độ trễ nguội — pipeline không làm ấm (đã đo)**: kịch bản `.WithoutWarmUp()` và `scripts/ci/run-performance-tests.sh` chỉ `docker compose up --build --wait` rồi chạy luôn (không gọi khối làm ấm 2 lượt mà `scripts/up.sh` có, vốn ghi rõ lượt đầu "có thể vượt ngân sách 3 s của BFF"). Đo: sau khi tạo lại `orders-api`, lần 1 `Overall: FAIL` (GET products p95 564.7 ms, add-item 829.4 ms — cả 2 vượt 300 ms), lần 2 `PASS` (29.7/100.2/110.5/27.3 ms); sau khi khởi động lại `bff/baskets/orders/products/gateway` (docker báo `healthy`), lần 1 **60/60 request `503`/`504`** (crash `Single` như trên, không báo cáo), lần 2 `FAIL` (45 % checkout lỗi; p95 511.7/408.6/369.1 ms), lần 3 `PASS`. Với cron hằng đêm dựng stack mới rồi chạy ngay, cổng có nguy cơ đỏ giả thường xuyên và chỉ đỏ "vì nguội", không vì hồi quy thật. Đề xuất: thêm bước làm ấm (bỏ qua N lần chạy đầu) trước khi đo.
- **FR-008/US3-KB3 ("có khả năng chặn phát hành") chưa hiện thực — chỉ đăng 1 trạng thái GitHub không ai đọc**: `Jenkinsfile.performance` chỉ `githubNotify` `ci/performance-gate` (cron `H 2 * * *`), hợp đồng nêu rõ "KHÔNG đăng ký vào required status checks", và "cơ chế wiring cụ thể vào bước phát hành … thuộc `tasks.md`" — nhưng `tasks.md` không có task nào về việc này; grep `performance-gate` trong `scripts/`, `.github/`, `Jenkinsfile` chỉ thấy đúng `Jenkinsfile.performance`. Không có quy trình phát hành nào đọc check này, nên FAIL hằng đêm không chặn được gì. Ngoài ra Jenkins/SonarQube cục bộ không chạy (QA 013) nên cả pipeline chưa từng chạy thật; tôi chạy tương đương bằng `dotnet test` trực tiếp.
- **Bất biến "không hard-code ngân sách" (`research.md` Quyết định 0) không được test giữ (đã tự mutate)**: hard-code `var p95 = 300.0; var p99 = 800.0;` trong `CriticalPathStepBudgets.LoadAll` → 6/6 test vẫn xanh vì `LoadAll_MatchesTheClientFacingBffDefaultDeclaredInTheManifest` khẳng định thẳng literal `300`/`800` (comment gốc nói "đọc lại giá trị trên đĩa" — không đúng). Ngược lại đổi `<=` thành `<` trong `StepResult.Passed` → `AssertAllStepsWithinBudget_DoesNotThrow_…` đỏ đúng. Đề xuất: so sánh `LoadAll()` với giá trị đọc trực tiếp từ `ServiceManifestFixture` trong test.
- **6 test thuần của `CriticalPathLoadTests` không chạy trên PR**: project bị loại khỏi tier `unit` (đúng chủ đích của hợp đồng) nên `BudgetAssertionsTests`/`CriticalPathStepBudgetsTests` — logic thuần, chạy 0.3 s không cần stack — chỉ chạy khi cron nightly chạy được; 1 lỗi ở `BudgetAssertions` sẽ chỉ bị phát hiện sau khi cổng thật đã sai. Đề xuất: tách phần thuần sang project thuộc tier `unit`.
- **Tài liệu lỗi thời sau spec 014/QA**: `architecture/026`, `quickstart.md` ("Trạng thái đã biết"), `technical-debt.md` mục 026 và comment của `GatewayClient.cs` còn mô tả "gateway chế độ stub không chuyển tiếp token; SeedData không có tài khoản demo" — không còn đúng trên stack hiện tại (xem phát hiện đầu). Đề xuất cập nhật khi vá `GatewayClient`.
- **Ghi chú phương pháp**: chạy `dotnet test` trực tiếp trên stack `docker-compose.local.yml` (cổng gateway `:5300` = mặc định `GATEWAY_ORIGIN`) thay cho `scripts/ci/run-performance-tests.sh` (dựng stack `docker-compose.yml` + `demo.yml` — không chạy để khỏi đè stack QA đang dùng); để chạy có token, tôi vá tạm `GatewayClient` đọc `LOADTEST_TOKEN` rồi `git checkout --` hoàn tác. Hồi quy chèn vào `services/baskets/.../BasketEndpoints.cs` (handler `POST /baskets/current/items`, 600 ms trước `FindOrCreateCurrentAsync`), dựng lại image `baskets-api` cho cả 2 lượt. Mỗi lần chạy tạo thêm ~60 đơn hàng thật trong `orders-db` (bước reset giỏ `POST /bff/checkout` cũng tạo đơn khi giỏ có hàng) — đã dọn sau khi chạy (giữ lại đơn gốc của QA 017, xoá bảng outbox). Script tìm project của tier `performance` (`find`) cũng khớp bản sao trong `.claude/worktrees/*` trên máy này.
