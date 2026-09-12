---

description: "Task list template for feature implementation"
---

# Tasks: Rà soát N+1 query, truy vấn không giới hạn và thiếu phân trang trên toàn bộ dịch vụ

**Input**: Design documents from `specs/023-audit-n1-unbounded-pagination/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Constitution Principle III (Test-First, NON-NEGOTIABLE) áp dụng cho tính năng này — các task test dưới đây là BẮT BUỘC, không tùy chọn, và PHẢI được viết trước, xác nhận FAIL, rồi mới triển khai.

**Organization**: Task được nhóm theo user story trong spec.md. 7 điểm kiểm kê ở `plan.md`/`research.md` Decision 1 được phân bổ: #1/#2 (GET /products, GET /bff/products không phân trang) → US1; #6/#7 (BFF full-catalog fetch) và #5 (khoá `.Include()` đúng của basket thành regression test) → US2; phần "ép trần khi client truyền pageSize lớn" (tách khỏi "có mặc định" của US1, theo đúng cách spec.md tách User Story 1 và User Story 3) → US3.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Có thể chạy song song (khác file, không phụ thuộc task chưa hoàn thành)
- **[Story]**: User story mà task thuộc về (US1, US2, US3)
- Mỗi task đều có đường dẫn file cụ thể

## Path Conventions

Monorepo backend nhiều service hiện có (xem plan.md § Project Structure) — không có service runtime mới:

- Products: `services/products/src/Products.Api/Features/Catalog/`, test tại `services/products/tests/`
- BFF: `services/bff/src/Bff.Api/{DownstreamClients,Features/Products,Features/Baskets}/`, test tại `services/bff/tests/`
- Baskets: test tại `services/baskets/tests/Baskets.Api.IntegrationTests/`
- Hạ tầng test dùng chung: `shared/IntegrationTestSupport/`
- Test quét mới: `tests/QueryCoverageTests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Chuẩn bị khung dự án test cần thiết cho toàn bộ tính năng (không có NuGet package mới — plan.md Technical Context)

- [X] T001 Tạo dự án xUnit `tests/QueryCoverageTests/QueryCoverageTests.csproj` theo đúng khuôn mẫu `tests/ResilienceCoverageTests/ResilienceCoverageTests.csproj` (không tham chiếu project service nào — đọc file dạng text), đăng ký dự án này trong `Ecommerce.slnx`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Khung scanner dùng chung mà CẢ 3 user story đều cần populate (US1/US3 → `ExpectedListEndpoints`, US2 → `ExpectedBoundedQuerySites`)

**⚠️ CRITICAL**: Không user story nào được coi là hoàn tất trước khi phase này xong

- [X] T002 [P] Cài đặt khung `tests/QueryCoverageTests/QueryCoverageScanner.cs` theo đúng khuôn mẫu `tests/ResilienceCoverageTests/ResilienceCoverageScanner.cs`: record `ExpectedListEndpoint` (Name, SourceFile, RequiredMarkers), record `ExpectedBoundedQuerySite` (Name, SourceFile, RequiredMarkers), record `CoverageViolation`, record `CoverageScanResult`, hàm `LocateRepositoryRoot()`, và hai hàm `ScanListEndpoints(...)`/`ScanBoundedQuerySites(...)` đọc file dạng text và xác nhận từng marker trong `RequiredMarkers` xuất hiện trong `SourceFile` — phụ thuộc T001.
      **Khác kế hoạch ban đầu**: điền sẵn cả 4 dòng (2 `ExpectedListEndpoints` của US1/US3, 2 `ExpectedBoundedQuerySites` của US2) ngay tại đây thay vì để rỗng rồi populate dần ở T006/T013/T018 — đơn giản hơn và vẫn đúng tinh thần Test-First (`dotnet test` xác nhận cả 4 dòng RED thật ngay từ Foundational, xem T003); T006/T013/T018 dưới đây đổi thành "xác nhận dòng đã có sẵn" thay vì "thêm dòng mới"
- [X] T003 [P] Viết `tests/QueryCoverageTests/QueryCoverageTests.cs`: `ScanListEndpoints_ReportsNoViolations_ForCurrentInventory`, `ScanListEndpoints_ActuallyExaminesEveryExpectedEndpoint`, `ScanBoundedQuerySites_ReportsNoViolations_ForCurrentInventory`, `ScanBoundedQuerySites_ActuallyExaminesEveryExpectedSite`, cộng 4 test cho chính scanner (fixture thư mục tạm, theo khuôn mẫu `ResilienceCoverageTests.cs`) — phụ thuộc T002.
      **Kết quả xác nhận**: `dotnet test tests/QueryCoverageTests` → 6/8 PASS (4 test cho scanner + 2 `ActuallyExamines*`), 2 FAIL đúng như kỳ vọng (`ScanListEndpoints_ReportsNoViolations_ForCurrentInventory`: thiếu `DefaultPageSize`/`MaxPageSize`/`page`/`pageSize`; `ScanBoundedQuerySites_ReportsNoViolations_ForCurrentInventory`: thiếu `GetProductsByIdsAsync` × 2) — đúng RED state cần có trước khi triển khai US1/US2/US3

**Checkpoint**: Nền tảng sẵn sàng — scanner có khung, đã xác nhận RED đúng chỗ (2/8 test FAIL vì marker thật chưa tồn tại). Có thể bắt đầu triển khai từng user story.

---

## Phase 3: User Story 1 - Mọi endpoint trả về danh sách đều tự động phân trang (Priority: P1) 🎯 MVP

**Goal**: `GET /products` và `GET /bff/products` trả về một trang có kích thước mặc định (`DefaultPageSize = 20`) ngay cả khi caller không truyền tham số phân trang nào — khép kín khoảng hở #1/#2 (plan.md Summary).

**Independent Test**: Seed 500 sản phẩm, gọi `GET /products` không tham số, xác nhận kết quả là một trang 20 bản ghi kèm `totalCount = 500`, không phải toàn bộ 500 (Jira Test Scenario 1).

### Tests for User Story 1 ⚠️

> **Viết các test này TRƯỚC, xác nhận chúng FAIL trước khi triển khai (endpoint hiện trả toàn bộ catalog)**

- [X] T004 [P] [US1] Viết `services/products/tests/Products.Api.IntegrationTests/ProductListingPaginationTests.cs`: `ListProducts_WithoutQueryParameters_ReturnsOnlyDefaultPageSize_NotTheWholeCatalog` — seed 500 sản phẩm vào SQL Server (Testcontainers, theo khuôn mẫu `SqlServerFixture` đã có ở baskets), gọi `GET /products` không tham số, xác nhận `items.Count == 20` và `totalCount == 500` — phụ thuộc T007.
      **Khác kế hoạch ban đầu**: viết chung một lần với các test của US3 (T017) trong cùng file, cộng một test cho filter `ids` (`ListProducts_WithIdsFilter_ReturnsExactlyTheMatchingProducts_IgnoringPageSize`) không có trong kế hoạch ban đầu — bổ sung vì T014 (US2) cần xác minh trực tiếp ở tầng products, không chỉ gián tiếp qua test BFF (T012). Cũng phải cập nhật 2 test có sẵn từ trước (`CatalogEndpointsTests.cs`) và `CatalogSeedTests.cs` để đọc `PagedProductsResponse` thay vì `ProductResponse[]` trần — bị vỡ bởi chính thay đổi shape của T007, không phải lỗi mới.
- [X] T005 [P] [US1] Viết `services/bff/tests/Bff.Api.UnitTests/ProductsEndpointPaginationTests.cs`: xác nhận `ProductsApiClient.GetProductsAsync`/`GetProductsByIdsAsync` forward đúng query string (`page`/`pageSize`/`ids`) qua handler đếm+ghi lại request (khuôn mẫu `RetryMethodPolicyTests`), và `ProductsEndpoints.ToListResponse` map đúng `Page`/`PageSize`/`TotalCount` — phụ thuộc T008.
      **Khác kế hoạch ban đầu**: thêm hàm thuần `internal static ProductsEndpoints.ToListResponse(ProductPageResource)` (T008) để có một điểm test không cần HTTP cho phần shaping, theo đúng khuôn mẫu `ToSummary`/`ResponseMappingTests` đã có — kế hoạch ban đầu chỉ nói "xác nhận handler forward", không nêu chi tiết kỹ thuật này.
- [X] T006 [P] [US1] Bổ sung 2 dòng vào `ExpectedListEndpoints` của `tests/QueryCoverageTests/QueryCoverageScanner.cs`: `products-listing` (marker `"DefaultPageSize"`, `"MaxPageSize"`), `bff-products-listing` (marker `"page"`, `"pageSize"`) — đã thực hiện sớm hơn kế hoạch, tại T002 (xem ghi chú Foundational) thay vì ở đây; task này giữ lại chỉ để xác nhận dòng đã đúng và khớp US1's scope.

### Implementation for User Story 1

- [X] T007 [US1] Trong `services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs`: thêm hằng số `DefaultPageSize = 20`, `MaxPageSize = 100` (US3, xem T019); `GET /products` nhận `int? page`, `int? pageSize`, `string? ids` qua query binding; nhánh `ids` (US2, xem T014) và nhánh phân trang (`Skip`/`Take` sau `OrderBy(Name)`, `CountAsync()` cho `TotalCount`) cùng trả về record mới `PagedProductsResponse(Items, Page, PageSize, TotalCount)` thay `ProductResponse[]` trần — phụ thuộc T004, T006.
      **Khác kế hoạch ban đầu**: gộp chung với T014 (ids filter) và T019 (MaxPageSize clamp) trong cùng một lần sửa file, vì cả ba nhánh nằm chung một hàm nhỏ, tách thành 3 lần sửa riêng sẽ giả tạo — xem T014/T019 để biết chi tiết từng phần.
- [X] T008 [US1] Trong `services/bff/src/Bff.Api/DownstreamClients/ProductsApiClient.cs`: `GetProductsAsync(int page, int pageSize, ct)` đọc `ProductPageResource`; trong `ProductsEndpoints.cs`: nhận `page`/`pageSize` từ query (mặc định 1/20), forward xuống client, thêm hàm `ToListResponse(ProductPageResource)`, `ProductListResponse` thêm 3 field `Page`/`PageSize`/`TotalCount` (chỉ thêm, không đổi `Items` — spec FR-007) — phụ thuộc T005, T007. Đồng thời cập nhật `ProductsConsumerPactTests.cs` (Pact contract test có sẵn) sang envelope mới — bị vỡ bởi thay đổi shape, không phải lỗi mới.
- [X] T009 [US1] Chạy lại T004, T005, T006, xác nhận PASS (Green) — phụ thuộc T007, T008.
      **Kết quả xác nhận**: `dotnet test tests/QueryCoverageTests` 8/8 PASS; `dotnet test services/bff/tests/Bff.Api.UnitTests` 19/19 PASS (trước khi thêm T012); `dotnet test services/bff/tests/Bff.Api.ContractTests --filter ProductsConsumerPactTests` 1/1 PASS; `dotnet build Ecommerce.slnx` 0 lỗi. T004 (integration, cần SQL Server qua Testcontainers) ban đầu KHÔNG chạy được trong sandbox này (Docker Desktop không chạy). **Cập nhật sau khi Docker Desktop được khởi động lại giữa phiên**: `dotnet test services/products/tests/Products.Api.IntegrationTests` → **PASS thật 23/23** (toàn bộ suite, gồm `ProductListingPaginationTests`).

**Checkpoint**: Tại đây, User Story 1 hoạt động độc lập và kiểm thử được (trong phạm vi có thể xác thực ở sandbox này) — khoảng hở nghiêm trọng nhất (unbounded mặc định) đã khép kín. Đây là MVP.

---

## Phase 4: User Story 2 - Truy vấn dữ liệu liên quan không phát sinh mẫu hình N+1 (Priority: P1)

**Goal**: `BasketsEndpoints` (BFF) tra cứu sản phẩm qua lookup bị chặn (`ids`) thay vì fetch toàn bộ catalog — đúng 1 lời gọi downstream bất kể basket có bao nhiêu dòng (khoảng hở #6/#7); hành vi `.Include()` đúng sẵn có của basket được khoá lại thành regression test dựa trên đếm câu lệnh SQL thật (khoảng hở #5, spec FR-002/SC-002).

**Independent Test**: Bật `QueryCountInterceptor`, tải một basket có nhiều dòng qua `GET /baskets/{id}` thật, xác nhận số câu lệnh SQL không tăng theo số dòng; dựng `BasketsEndpoints` với `ProductsApiClient` đếm lời gọi, xác nhận render một basket nhiều dòng chỉ gọi client đúng 1 lần (Jira Test Scenario 2, ở cả hai tầng — EF Core và BFF).

### Tests for User Story 2 ⚠️

> **Viết các test này TRƯỚC, xác nhận chúng FAIL trước khi triển khai (hai điểm gọi BFF hiện fetch toàn bộ catalog)**

- [X] T010 [P] [US2] Viết `shared/IntegrationTestSupport/QueryCountInterceptor.cs`: `DbCommandInterceptor` đếm `ReaderExecuting`/`ReaderExecutingAsync` (property `ExecutedCommandCount`, `Reset()`) — hạ tầng dùng chung, cần trước T011.
      **Khác kế hoạch ban đầu**: cần thêm `PackageReference Microsoft.EntityFrameworkCore.Relational` vào `shared/IntegrationTestSupport.csproj` (và `PackageVersion` tương ứng vào `Directory.Packages.props`) — `DbCommandInterceptor`/`CommandEventData` nằm ở gói `.Relational`, không phải gói `Microsoft.EntityFrameworkCore` lõi như dự kiến ban đầu; phát hiện qua lỗi biên dịch CS0246.
- [X] T011 [P] [US2] Viết `services/baskets/tests/Baskets.Api.IntegrationTests/BasketQueryCountTests.cs`: seed basket 1 dòng và basket 5 dòng trực tiếp qua `BasketsDbContext` (không qua HTTP — nhanh hơn và không phụ thuộc route add-item), gắn `QueryCountInterceptor` vào `BasketsDbContext` qua `host.ConfigureServices` re-registering `AddDbContext` (KHÔNG phải `ConfigureTestServices` như kế hoạch ban đầu — API đó không tồn tại trên `IWebHostBuilder` trong stack này; `ConfigureServices` là quy ước đã dùng nhất quán ở `PactProviderHost.cs`/`BffTestHost.cs`/`GatewayTestHost.cs`), tải cả hai basket qua `GET /baskets/{id}` thật, xác nhận `ExecutedCommandCount` BẰNG NHAU giữa hai basket — phụ thuộc T010.
      Ban đầu KHÔNG chạy được trong sandbox này (Docker không khả dụng). **Cập nhật sau khi Docker khởi động lại**: chạy riêng (`--filter BasketQueryCountTests`) → **PASS thật 1/1** qua SQL Server thật, số câu lệnh SQL bằng nhau giữa basket 1 dòng và 5 dòng, đúng như dự kiến.
- [X] T012 [P] [US2] Viết `services/bff/tests/Bff.Api.UnitTests/ProductLookupBatchingTests.cs`: `RenderingBasket_WithMultipleDistinctProducts_CallsProductsClientExactlyOnce`, `RenderingBasket_WithDuplicateProductAcrossLines_StillCallsProductsClientExactlyOnce`, `RenderingEmptyBasket_DoesNotCallProductsClient` — gọi `BasketsEndpoints.ToResponseAsync` trực tiếp (đổi từ `private` sang `internal static` để test được, cùng khuôn mẫu `ToItem`/`ToSummary`) với `ProductsApiClient` thật + handler đếm lời gọi.
      **Khác kế hoạch ban đầu**: bỏ test riêng `AddingItem_CallsProductsClientExactlyOnce` — logic add-item nằm inline trong lambda `MapPost` (không phải hàm tĩnh tách riêng như `ToResponseAsync`), và số lần gọi ở đó vốn đã luôn là 1 trước lẫn sau khi sửa (khoảng hở thật ở đó là KÍCH THƯỚC fetch, không phải SỐ LẦN gọi) — thuộc tính đó đã được `ProductsEndpointPaginationTests.GetProductsByIdsAsync_SendsIdsAsCommaSeparatedQueryParameter_NotFullCatalogFetch` (T005) và marker `GetProductsByIdsAsync` trong `QueryCoverageTests` (T013) bao phủ đầy đủ hơn; thêm 1 test mới không có trong kế hoạch (`RenderingBasket_WithDuplicateProductAcrossLines...`) để khoá riêng hành vi `.Distinct()` mới thêm ở T015.
- [X] T013 [P] [US2] Bổ sung 2 dòng vào `ExpectedBoundedQuerySites` của `QueryCoverageScanner.cs`: `bff-basket-render`, `bff-basket-add-item` (marker `"GetProductsByIdsAsync"`) — đã thực hiện sớm hơn kế hoạch, tại T002 (như T006) thay vì ở đây.

### Implementation for User Story 2

- [X] T014 [US2] Trong `CatalogEndpoints.cs`: filter `ids` (GUID phân tách dấu phẩy) trên `GET /products` — bỏ qua `page`/`pageSize`, trả đúng tập khớp, `Page=1`, `PageSize=Items.Count`, `TotalCount=Items.Count` — phụ thuộc T007 (gộp chung, xem ghi chú T007).
- [X] T015 [US2] Trong `ProductsApiClient.cs`: thêm `GetProductsByIdsAsync(IReadOnlyCollection<Guid> ids, ct)` (trả `[]` ngay, không gọi HTTP, khi `ids` rỗng); trong `BasketsEndpoints.cs`: `ToResponseAsync` dùng `basket.Items.Select(i => i.ProductId).Distinct()`, `POST /basket/items` dùng `[request.ProductId]` — phụ thuộc T012, T013, T014.
- [X] T016 [US2] Chạy lại T011, T012, T013, xác nhận PASS — phụ thuộc T015.
      **Kết quả xác nhận**: `dotnet test tests/QueryCoverageTests` 8/8 PASS; `dotnet test services/bff/tests/Bff.Api.UnitTests` 22/22 PASS; `dotnet build Ecommerce.slnx` 0 lỗi; T011 (`BasketQueryCountTests`, chạy riêng sau khi Docker khởi động lại) PASS thật 1/1.

**Checkpoint**: User Story 1 VÀ 2 đều hoạt động độc lập, đã xác thực thật (Testcontainers) — khoảng hở nặng nhất của cuộc rà soát (over-fetch xuyên service ở BFF) đã khép kín, hành vi `.Include()` đúng của basket đã có regression test thật, đã chạy PASS qua SQL Server thật.

---

## Phase 5: User Story 3 - Giới hạn kích thước trang được ép buộc ở phía server (Priority: P2)

**Goal**: Một client truyền `pageSize` lớn bất thường vẫn bị giới hạn ở `MaxPageSize = 100` — khép kín phần còn lại của khoảng hở #1/#2 mà US1 chưa xử lý (US1 chỉ xử lý "không truyền tham số", chưa ép trần khi có truyền).

**Independent Test**: Gọi `GET /products?pageSize=1000000`, xác nhận số bản ghi trả về bị giới hạn ở 100, không phản ánh giá trị client yêu cầu (Jira Test Scenario 3).

### Tests for User Story 3 ⚠️

> **Viết các test này TRƯỚC, xác nhận chúng FAIL trước khi triển khai (chưa có trần — US1 dùng thẳng giá trị client truyền)**

- [X] T017 [P] [US3] Bổ sung vào `ProductListingPaginationTests.cs` (T004): `ListProducts_WithExcessivePageSize_IsCappedAtMaxPageSize` (`pageSize=1000000` → `items.Count == 100`), `ListProducts_WithZeroOrNegativePageSize_FallsBackToDefault` (`[Theory]` `pageSize=0`/`-5` → dùng `DefaultPageSize`) — viết cùng lúc với T004 (cùng file), không tách thành lần sửa riêng.
- [X] T018 [P] [US3] Cập nhật `ExpectedListEndpoints`: marker `"MaxPageSize"` — đã có sẵn từ T002/T006 (viết đủ marker ngay từ đầu thay vì bổ sung dần).

### Implementation for User Story 3

- [X] T019 [US3] Trong `CatalogEndpoints.cs`: hằng số `MaxPageSize = 100`; `Math.Clamp(pageSize is > 0 ? pageSize.Value : DefaultPageSize, 1, MaxPageSize)` — phụ thuộc T007 (gộp chung, xem ghi chú T007).
- [X] T020 [US3] Chạy lại T017, T018, xác nhận PASS — phụ thuộc T019.
      **Kết quả xác nhận**: `dotnet test tests/QueryCoverageTests` 8/8 PASS (marker `MaxPageSize` đã pass từ đầu). T017 (`ListProducts_WithExcessivePageSize_IsCappedAtMaxPageSize`, `ListProducts_WithZeroOrNegativePageSize_FallsBackToDefault`) nằm trong 23/23 PASS thật của `Products.Api.IntegrationTests` (xem T009) sau khi Docker khởi động lại.

**Checkpoint**: Cả 3 user story hoạt động độc lập, đã xác thực thật qua SQL Server (Testcontainers) sau khi Docker khởi động lại giữa phiên — xem T009/T011/T020. Toàn bộ khoảng hở ở `plan.md` Summary đã được khép kín và xác thực bằng test chạy thật, trừ một lần flake hạ tầng không liên quan (xem T023/quickstart.md).

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Xác thực toàn diện cuối cùng, không thuộc riêng một user story nào

- [X] T021 [P] Cập nhật `contracts/query-coverage-inventory-contract.md` và `contracts/downstream-openapi.yaml`/`bff-openapi.yaml` nếu tên marker/tham số thực tế khác kế hoạch — phụ thuộc T009, T016, T020.
      **Kết quả xác nhận**: Đối chiếu từng dòng `query-coverage-inventory-contract.md` với `QueryCoverageScanner.ExpectedListEndpoints`/`ExpectedBoundedQuerySites` thật — khớp 100%, không có tên marker nào lệch kế hoạch ban đầu, không cần sửa. `downstream-openapi.yaml`/`bff-openapi.yaml` (viết ở `/speckit-plan`) cũng khớp implementation thật (query param `page`/`pageSize`/`ids`, response `PagedProductsResponse`/`ProductListResponse` với `items`/`page`/`pageSize`/`totalCount`) — không cần sửa.
- [X] T022 [P] Cập nhật XML-doc comment "Contract:" trong `CatalogEndpoints.cs` và `ProductsEndpoints.cs` (BFF) trỏ sang `specs/023-audit-n1-unbounded-pagination/contracts/...` thay vì `specs/002-gateway-bff-routing/contracts/...` — đúng tiền lệ mỗi feature chạm một contract thì code trỏ sang feature mới nhất chạm nó (ví dụ `BasketsEndpoints.cs` đã trỏ `specs/004`) — phụ thuộc T009. `BasketsEndpoints.cs`'s pointer (`specs/004`) giữ nguyên — hình dạng `BasketResponse`/`BasketItem` không đổi, chỉ cách BFF lấy dữ liệu nội bộ đổi.
- [X] T023 [P] Thực hiện [quickstart.md](./quickstart.md) Bước 1–6 — phụ thuộc T009, T016, T020.
      **Kết quả xác nhận** (đã ghi vào quickstart.md "Kết quả xác thực trong phiên triển khai"): toàn bộ Bước 1–6 PASS thật sau khi Docker Desktop được khởi động lại giữa phiên — Bước 1 (8/8), Bước 2+3 (23/23 `Products.Api.IntegrationTests`), Bước 4+6 (22/22 `Bff.Api.UnitTests`), Bước 5 (`BasketQueryCountTests` 1/1 khi chạy riêng). Một lần flake hạ tầng (SQL Server container crash do hết đĩa khi nhiều container chạy nối tiếp trong cùng suite `Baskets.Api.IntegrationTests`) khiến 3/28 test khác fail — không liên quan tới tính năng, xem quickstart.md. Ngoài phạm vi: `Products.Api.ContractTests`/`Baskets.Api.ContractTests` (verify Pact tầng provider) fail 401 Unauthorized trên mọi route — đã xác nhận bằng cách chạy lại trên baseline commit `10ea911` (trước khi tính năng này sửa gì) và thấy lỗi giống hệt, nên là vấn đề có sẵn của sandbox, không phải hồi quy.
- [X] T024 Build sạch toàn `Ecommerce.slnx` (0 lỗi/cảnh báo) — phụ thuộc T023.
      **Kết quả xác nhận**: `dotnet build Ecommerce.slnx` → Build succeeded, 0 Warning(s), 0 Error(s) (xác nhận lại nhiều lần trong phiên, lần cuối sau khi sửa T022).
- [X] T025 [P] Rà soát lại spec.md Assumptions và research.md Decision 1: xác nhận `orders`/`parties` vẫn không có endpoint danh sách nào tại thời điểm tính năng này hoàn tất (grep `MapGet.*orders\b`, `MapGet.*parties\b`) — nếu có, ghi nhận là phát hiện ngoài phạm vi, không tự thêm task sửa.
      **Kết quả xác nhận**: `OrderEndpoints.cs` chỉ có `MapGet("/orders/{orderId:guid}", ...)`, `PartyEndpoints.cs` chỉ có `MapGet("/parties/{partyId:guid}", ...)` — không có endpoint danh sách nào ở cả hai service, giả định ở research.md Decision 1 (#3/#4) vẫn đúng.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Không phụ thuộc — bắt đầu ngay
- **Foundational (Phase 2)**: Phụ thuộc hoàn tất Setup — CHẶN việc coi bất kỳ user story nào là hoàn tất (scanner cần có khung trước khi mỗi story populate danh sách của mình)
- **User Stories (Phase 3-5)**: Đều phụ thuộc hoàn tất Foundational
  - US1 và US2 đều CHẠM `CatalogEndpoints.cs` (US1: nhánh `page`/`pageSize`; US2: nhánh `ids`) — độc lập về logic nhưng cùng file, nên làm US1 trước US2 để tránh xung đột merge, dù về nguyên tắc có thể làm song song bởi 2 người khác nhau nếu phối hợp merge cẩn thận
  - US2's T014 phụ thuộc trực tiếp `PagedProductsResponse` mà US1's T007 tạo ra — US2 KHÔNG hoàn toàn độc lập về code với US1 dù độc lập về acceptance test (giống tiền lệ 020: US3 phụ thuộc shared client mà US1 tạo)
  - US3 phụ thuộc trực tiếp T007 (US1) — chỉ bọc thêm `Math.Clamp` quanh cơ chế đã có, không phải cơ chế mới
  - Thứ tự đề xuất: US1 (P1, nền tảng — nền cho cả US2 và US3) → US2 (P1, độc lập với US3, chạm file khác ở BFF) → US3 (P2, delta nhỏ nhất trên `CatalogEndpoints.cs` đã ổn định từ US1)
- **Polish (Phase 6)**: Phụ thuộc cả 3 user story hoàn tất

### Within Each User Story

- Test viết trước, xác nhận FAIL trước khi triển khai (Constitution Principle III, NON-NEGOTIABLE)
- Cấu hình/endpoint trước khi chạy lại test xác nhận Green
- Story hoàn tất (test chuyển Green, không hồi quy) trước khi coi là sẵn sàng tích hợp

### Parallel Opportunities

- T004, T005, T006 (test US1) chạy song song — khác file
- T010, T011, T012, T013 (test US2) chạy song song — khác file (T011 phụ thuộc T010 hoàn tất trước khi PASS được, nhưng viết file có thể bắt đầu song song)
- T017, T018 (test US3) chạy song song — khác file
- T021, T022, T023, T025 (Polish) chạy song song — khác file/mối quan tâm

---

## Parallel Example: User Story 1

```bash
# Chạy song song các test của User Story 1 (khác file):
Task: "Viết ProductListingPaginationTests.cs xác nhận GET /products không tham số trả về trang mặc định"
Task: "Viết ProductsEndpointPaginationTests.cs xác nhận BFF forward page/pageSize"
Task: "Bổ sung ExpectedListEndpoints trong tests/QueryCoverageTests/QueryCoverageScanner.cs"

# Sau khi cả ba FAIL đúng như kỳ vọng, triển khai tuần tự (T008 phụ thuộc T007):
Task: "Thêm DefaultPageSize + page/pageSize + PagedProductsResponse vào CatalogEndpoints.cs"
Task: "Forward page/pageSize + mở rộng ProductListResponse trong BFF"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Hoàn tất Phase 1: Setup
2. Hoàn tất Phase 2: Foundational (CHẶN mọi story)
3. Hoàn tất Phase 3: User Story 1 — mặc định không truyền tham số đã bị chặn
4. **DỪNG và XÁC THỰC**: chạy T004, T005, T006 độc lập, xác nhận Green
5. Đây là MVP có thể trình diễn — khoảng hở nghiêm trọng nhất (unbounded mặc định trên endpoint duy nhất còn thiếu phân trang) đã khép kín, dù chưa ép trần với giá trị client truyền tường minh (US3) và chưa sửa over-fetch ở BFF (US2)

### Incremental Delivery

1. Setup + Foundational → nền tảng sẵn sàng (scanner có khung)
2. + User Story 1 → kiểm thử độc lập → MVP (mặc định đã bị chặn)
3. + User Story 2 → kiểm thử độc lập (basket rendering/add-item không còn over-fetch, `.Include()` đúng đã khoá thành regression test)
4. + User Story 3 → kiểm thử độc lập (trần server-side với giá trị client truyền tường minh)
5. Mỗi story bổ sung giá trị mà không phá vỡ story trước — xác nhận bằng cách chạy lại toàn bộ test của story trước đó ở mỗi checkpoint (T009, T016, T020)

---

## Notes

- [P] = khác file, không phụ thuộc task chưa hoàn thành
- Nhãn [Story] gắn task với đúng user story để truy vết
- Test PHẢI được viết trước và xác nhận FAIL trước khi triển khai (Constitution Principle III, NON-NEGOTIABLE — không tùy chọn cho tính năng này)
- `orders`/`parties` không có task nào trong file này — kiểm kê xác nhận không tồn tại endpoint danh sách nào ở hai service đó (research.md Decision 1, #3/#4); T025 chỉ xác nhận lại giả định đó vẫn đúng tại thời điểm hoàn tất, không tạo endpoint mới cho hai service này
- Commit sau mỗi task hoặc mỗi nhóm task liên quan
- Dừng ở mỗi checkpoint để xác thực từng story độc lập trước khi tiếp tục
