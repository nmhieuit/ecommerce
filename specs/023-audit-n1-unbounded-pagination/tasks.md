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

- [ ] T001 Tạo dự án xUnit `tests/QueryCoverageTests/QueryCoverageTests.csproj` theo đúng khuôn mẫu `tests/ResilienceCoverageTests/ResilienceCoverageTests.csproj` (không tham chiếu project service nào — đọc file dạng text), đăng ký dự án này trong `Ecommerce.slnx`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Khung scanner dùng chung mà CẢ 3 user story đều cần populate (US1/US3 → `ExpectedListEndpoints`, US2 → `ExpectedBoundedQuerySites`)

**⚠️ CRITICAL**: Không user story nào được coi là hoàn tất trước khi phase này xong

- [ ] T002 [P] Cài đặt khung `tests/QueryCoverageTests/QueryCoverageScanner.cs` theo đúng khuôn mẫu `tests/ResilienceCoverageTests/ResilienceCoverageScanner.cs`: record `ExpectedListEndpoint` (Name, SourceFile, RequiredMarkers), record `ExpectedBoundedQuerySite` (Name, SourceFile, RequiredMarkers), record `CoverageViolation`, record `CoverageScanResult`, hàm `LocateRepositoryRoot()`, và hai hàm `ScanListEndpoints(...)`/`ScanBoundedQuerySites(...)` đọc file dạng text và xác nhận từng marker trong `RequiredMarkers` xuất hiện trong `SourceFile` — `ExpectedListEndpoints`/`ExpectedBoundedQuerySites` khởi tạo RỖNG (populate dần theo từng story ở dưới, data-model.md "List/Bounded Query Site Inventory Entry") — phụ thuộc T001
- [ ] T003 [P] Viết `tests/QueryCoverageTests/QueryCoverageTests.cs`: `Scan_ReportsNoViolations_ForCurrentInventory` (chạy với danh sách thật, rỗng ở bước này nên PASS vô nghĩa — có ý nghĩa khi US1-3 populate danh sách), `Scan_DetectsViolation_WhenMarkerMissing`, `Scan_DetectsViolation_WhenSourceFileMissing` (dùng fixture thư mục tạm, theo khuôn mẫu `ResilienceCoverageTests.cs`) — phụ thuộc T002

**Checkpoint**: Nền tảng sẵn sàng — scanner có khung (danh sách rỗng). Có thể bắt đầu triển khai từng user story.

---

## Phase 3: User Story 1 - Mọi endpoint trả về danh sách đều tự động phân trang (Priority: P1) 🎯 MVP

**Goal**: `GET /products` và `GET /bff/products` trả về một trang có kích thước mặc định (`DefaultPageSize = 20`) ngay cả khi caller không truyền tham số phân trang nào — khép kín khoảng hở #1/#2 (plan.md Summary).

**Independent Test**: Seed 500 sản phẩm, gọi `GET /products` không tham số, xác nhận kết quả là một trang 20 bản ghi kèm `totalCount = 500`, không phải toàn bộ 500 (Jira Test Scenario 1).

### Tests for User Story 1 ⚠️

> **Viết các test này TRƯỚC, xác nhận chúng FAIL trước khi triển khai (endpoint hiện trả toàn bộ catalog)**

- [ ] T004 [P] [US1] Viết `services/products/tests/Products.Api.IntegrationTests/ProductListingPaginationTests.cs`: `ListProducts_WithoutQueryParameters_ReturnsOnlyDefaultPageSize_NotTheWholeCatalog` — seed 500 sản phẩm vào SQL Server (Testcontainers, theo khuôn mẫu `SqlServerFixture` đã có ở baskets), gọi `GET /products` không tham số, xác nhận `items.Count == 20` và `totalCount == 500` — xác nhận FAIL trước T007
- [ ] T005 [P] [US1] Viết `services/bff/tests/Bff.Api.UnitTests/ProductsEndpointPaginationTests.cs`: xác nhận handler của `GET /bff/products` forward `page`/`pageSize` nhận từ query cho `ProductsApiClient.GetProductsAsync` và map `Page`/`PageSize`/`TotalCount` từ kết quả downstream vào `ProductListResponse` — xác nhận FAIL trước T008
- [ ] T006 [P] [US1] Bổ sung 2 dòng vào `ExpectedListEndpoints` của `tests/QueryCoverageTests/QueryCoverageScanner.cs` (T002): `products-listing` (`services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs`, marker `"DefaultPageSize"`), `bff-products-listing` (`services/bff/src/Bff.Api/Features/Products/ProductsEndpoints.cs`, marker `"page"`, `"pageSize"`) — chạy `dotnet test tests/QueryCoverageTests`, xác nhận cả 2 dòng FAIL (marker chưa tồn tại) — phụ thuộc T002

### Implementation for User Story 1

- [ ] T007 [US1] Trong `services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs`: thêm hằng số `DefaultPageSize = 20`; đổi `GET /products` nhận `int? page`, `int? pageSize` qua query binding; khi không truyền, dùng `page = 1`, `pageSize = DefaultPageSize`; áp dụng `.Skip((page-1) * pageSize).Take(pageSize)` sau `.OrderBy(product => product.Name)`; tính `TotalCount` bằng một `.CountAsync()` riêng trên cùng `IQueryable` trước khi áp `Skip/Take`; trả về record mới `PagedProductsResponse(Items, Page, PageSize, TotalCount)` (data-model.md "Paged Products Response") thay cho `ProductResponse[]` trần — phụ thuộc T004, T006
- [ ] T008 [US1] Trong `services/bff/src/Bff.Api/DownstreamClients/ProductsApiClient.cs`: đổi `GetProductsAsync(CancellationToken)` thành `GetProductsAsync(int page, int pageSize, CancellationToken)`, đọc `PagedProductsResponse` từ downstream; trong `services/bff/src/Bff.Api/Features/Products/ProductsEndpoints.cs`: nhận `page`/`pageSize` từ query (mặc định 1/20, khớp phía products), forward xuống client, `ProductListResponse` thêm 3 field `Page`/`PageSize`/`TotalCount` (chỉ thêm, không đổi `Items` — spec FR-007, data-model.md "Product List Response") — phụ thuộc T005, T007
- [ ] T009 [US1] Chạy lại T004, T005, T006, xác nhận PASS (Green) — phụ thuộc T007, T008

**Checkpoint**: Tại đây, User Story 1 hoạt động độc lập và kiểm thử được — khoảng hở nghiêm trọng nhất (unbounded mặc định) đã khép kín. Đây là MVP.

---

## Phase 4: User Story 2 - Truy vấn dữ liệu liên quan không phát sinh mẫu hình N+1 (Priority: P1)

**Goal**: `BasketsEndpoints` (BFF) tra cứu sản phẩm qua lookup bị chặn (`ids`) thay vì fetch toàn bộ catalog — đúng 1 lời gọi downstream bất kể basket có bao nhiêu dòng (khoảng hở #6/#7); hành vi `.Include()` đúng sẵn có của basket được khoá lại thành regression test dựa trên đếm câu lệnh SQL thật (khoảng hở #5, spec FR-002/SC-002).

**Independent Test**: Bật `QueryCountInterceptor`, tải một basket có nhiều dòng qua `GET /baskets/{id}` thật, xác nhận số câu lệnh SQL không tăng theo số dòng; dựng `BasketsEndpoints` với `ProductsApiClient` đếm lời gọi, xác nhận render một basket nhiều dòng chỉ gọi client đúng 1 lần (Jira Test Scenario 2, ở cả hai tầng — EF Core và BFF).

### Tests for User Story 2 ⚠️

> **Viết các test này TRƯỚC, xác nhận chúng FAIL trước khi triển khai (hai điểm gọi BFF hiện fetch toàn bộ catalog)**

- [ ] T010 [P] [US2] Viết `shared/IntegrationTestSupport/QueryCountInterceptor.cs`: `DbCommandInterceptor` đếm số lần `ReaderExecutingAsync`/`ReaderExecuting` được gọi (property `ExecutedCommandCount`, có thể `Reset()`) — hạ tầng dùng chung, không phải test tự nó, cần trước T011
- [ ] T011 [P] [US2] Viết `services/baskets/tests/Baskets.Api.IntegrationTests/BasketQueryCountTests.cs`: seed một basket 1 dòng và một basket khác 5 dòng (theo khuôn mẫu `SqlServerFixture`/`WebApplicationFactory<Program>` đã có ở `CurrentBasketTests.cs`), gắn `QueryCountInterceptor` (T010) vào `BasketsDbContext` qua `ConfigureTestServices`, tải cả hai basket qua `GET /baskets/{id}` thật, xác nhận `ExecutedCommandCount` của hai lần tải BẰNG NHAU (không tỷ lệ theo số dòng) — dự kiến PASS ngay vì `.Include(b => b.LineItems)` đã đúng từ trước (research.md Decision 1 #5) — đây là test khoá hành vi, không phải sửa lỗi — phụ thuộc T010
- [ ] T012 [P] [US2] Viết `services/bff/tests/Bff.Api.UnitTests/ProductLookupBatchingTests.cs`: `RenderingBasket_WithMultipleDistinctProducts_CallsProductsClientExactlyOnce`, `AddingItem_CallsProductsClientExactlyOnce`, `RenderingEmptyBasket_DoesNotCallProductsClient` — dùng một `ProductsApiClient` giả lập/spy đếm số lần gọi mỗi phương thức — xác nhận 2 test đầu FAIL trước T015 (hiện gọi `GetProductsAsync()` không tham số, đầy đủ catalog), test thứ 3 PASS ngay (hành vi return-sớm cho basket rỗng đã đúng từ trước)
- [ ] T013 [P] [US2] Bổ sung 2 dòng vào `ExpectedBoundedQuerySites` của `tests/QueryCoverageTests/QueryCoverageScanner.cs` (T002): `bff-basket-render`, `bff-basket-add-item` (cả hai trỏ `services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs`, marker `"GetProductsByIdsAsync"`) — xác nhận FAIL — phụ thuộc T002

### Implementation for User Story 2

- [ ] T014 [US2] Trong `services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs`: thêm filter tuỳ chọn `ids` (danh sách GUID phân tách dấu phẩy) trên `GET /products` — khi có mặt, bỏ qua `page`/`pageSize`, trả đúng tập khớp qua `.Where(p => ids.Contains(p.Id))`, đóng gói vào cùng `PagedProductsResponse` với `Page = 1`, `PageSize = Items.Count`, `TotalCount = Items.Count` (research.md Decision 4) — phụ thuộc T007 (tái sử dụng `PagedProductsResponse` của US1)
- [ ] T015 [US2] Trong `services/bff/src/Bff.Api/DownstreamClients/ProductsApiClient.cs`: thêm `GetProductsByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken)`; trong `services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs`: `ToResponseAsync` đổi sang gọi `GetProductsByIdsAsync(basket.Items.Select(i => i.ProductId).Distinct().ToArray(), ct)` thay `GetProductsAsync(ct)`; `POST /basket/items` đổi sang gọi `GetProductsByIdsAsync([request.ProductId], ct)` thay tìm trong toàn bộ catalog — phụ thuộc T012, T013, T014
- [ ] T016 [US2] Chạy lại T011, T012, T013, xác nhận PASS — phụ thuộc T015

**Checkpoint**: User Story 1 VÀ 2 đều hoạt động độc lập — khoảng hở nặng nhất của cuộc rà soát (over-fetch xuyên service ở BFF) đã khép kín, hành vi `.Include()` đúng của basket đã có regression test thật.

---

## Phase 5: User Story 3 - Giới hạn kích thước trang được ép buộc ở phía server (Priority: P2)

**Goal**: Một client truyền `pageSize` lớn bất thường vẫn bị giới hạn ở `MaxPageSize = 100` — khép kín phần còn lại của khoảng hở #1/#2 mà US1 chưa xử lý (US1 chỉ xử lý "không truyền tham số", chưa ép trần khi có truyền).

**Independent Test**: Gọi `GET /products?pageSize=1000000`, xác nhận số bản ghi trả về bị giới hạn ở 100, không phản ánh giá trị client yêu cầu (Jira Test Scenario 3).

### Tests for User Story 3 ⚠️

> **Viết các test này TRƯỚC, xác nhận chúng FAIL trước khi triển khai (chưa có trần — US1 dùng thẳng giá trị client truyền)**

- [ ] T017 [P] [US3] Bổ sung vào `services/products/tests/Products.Api.IntegrationTests/ProductListingPaginationTests.cs` (T004) và `services/products/tests/Products.Api.UnitTests/`: `ListProducts_WithExcessivePageSize_IsCappedAtMaxPageSize` (`pageSize=1000000` → `items.Count <= 100`), `ListProducts_WithZeroOrNegativePageSize_FallsBackToDefault` (`pageSize=0` và `pageSize=-5` → dùng `DefaultPageSize`, không lỗi 500) — xác nhận FAIL trước T019
- [ ] T018 [P] [US3] Cập nhật `ExpectedListEndpoints` của `tests/QueryCoverageTests/QueryCoverageScanner.cs`: bổ sung marker `"MaxPageSize"` vào 2 dòng đã có từ T006 — xác nhận FAIL — phụ thuộc T006

### Implementation for User Story 3

- [ ] T019 [US3] Trong `services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs`: thêm hằng số `MaxPageSize = 100`; bọc giá trị `pageSize` hiệu lực bằng `Math.Clamp(requested is > 0 ? requested.Value : DefaultPageSize, 1, MaxPageSize)` (áp dụng cho cả nhánh mặc định lẫn nhánh client truyền tường minh của T007) — phụ thuộc T007, T017, T018
- [ ] T020 [US3] Chạy lại T017, T018, xác nhận PASS — phụ thuộc T019

**Checkpoint**: Cả 3 user story hoạt động độc lập. Toàn bộ khoảng hở ở `plan.md` Summary đã được khép kín.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Xác thực toàn diện cuối cùng, không thuộc riêng một user story nào

- [ ] T021 [P] Cập nhật `specs/023-audit-n1-unbounded-pagination/contracts/query-coverage-inventory-contract.md` và `contracts/downstream-openapi.yaml`/`bff-openapi.yaml` nếu tên marker/tham số thực tế khác kế hoạch — phụ thuộc T009, T016, T020
- [ ] T022 [P] Cập nhật XML-doc comment "Contract:" trong `CatalogEndpoints.cs` và `ProductsEndpoints.cs` (BFF) trỏ sang `specs/023-audit-n1-unbounded-pagination/contracts/...` thay vì `specs/002-gateway-bff-routing/contracts/...` — đúng tiền lệ mỗi feature chạm một contract thì code trỏ sang feature mới nhất chạm nó (ví dụ `BasketsEndpoints.cs` đã trỏ `specs/004`) — phụ thuộc T009
- [ ] T023 [P] Thực hiện [quickstart.md](./quickstart.md) Bước 1–6 — phụ thuộc T009, T016, T020
- [ ] T024 Build sạch toàn `Ecommerce.slnx` (0 lỗi/cảnh báo) — phụ thuộc T023
- [ ] T025 [P] Rà soát lại spec.md Assumptions và research.md Decision 1: xác nhận `orders`/`parties` vẫn không có endpoint danh sách nào tại thời điểm tính năng này hoàn tất (grep `MapGet.*orders\b`, `MapGet.*parties\b`) — nếu có, ghi nhận là phát hiện ngoài phạm vi, không tự thêm task sửa

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
