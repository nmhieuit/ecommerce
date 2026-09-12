# Phase 0 Research: Rà soát N+1 query, truy vấn không giới hạn và thiếu phân trang

Không còn `[NEEDS CLARIFICATION]` nào trong Technical Context của `plan.md` — mọi quyết định dưới đây
rút ra trực tiếp từ việc đọc mã nguồn hiện có (dẫn chiếu file/dòng cụ thể), không phải giả định.

## Decision 1: Kiểm kê thực tế toàn bộ endpoint danh sách và truy vấn quan hệ hiện có

**Decision**: Trước khi thiết kế, kiểm kê toàn bộ endpoint trả về danh sách và toàn bộ truy vấn tải
dữ liệu quan hệ đang tồn tại trong 4 service nghiệp vụ + BFF, rồi chỉ thiết kế cho những gì thực sự
tồn tại.

| # | Vị trí | Trạng thái hiện tại | Nguồn |
|---|---|---|---|
| 1 | `GET /products` (products) | Không phân trang — trả toàn bộ bảng `Products`, không tham số query nào được chấp nhận. | `services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs:20-25` |
| 2 | `GET /bff/products` (BFF) | Kế thừa #1 — gọi `products.GetProductsAsync()` không tham số, forward toàn bộ. Response đã bọc `ProductListResponse { Items }` sẵn từ 002, với comment chủ đích để dành chỗ cho paging/totals. | `services/bff/src/Bff.Api/Features/Products/ProductsEndpoints.cs:22-29,50-54` |
| 3 | `GET /orders/{orderId}` (orders) | Chỉ get-by-id, KHÔNG có endpoint danh sách nào. `OrdersDbContext` cũng không có `OrderLine`/related DbSet — dòng đơn hàng chỉ dùng để tính `Order.Total` lúc tạo, không lưu quan hệ riêng. | `services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs`, `OrdersDbContext.cs` |
| 4 | `GET /parties/{partyId}` (parties) | Chỉ get-by-id, KHÔNG có endpoint danh sách. `PartiesDbContext` không có navigation nào. | `services/parties/src/Parties.Api/Features/Parties/PartyEndpoints.cs`, `PartiesDbContext.cs` |
| 5 | `GET /baskets/current`, `GET /baskets/{id}` (baskets) | Không phải endpoint "danh sách nhiều basket" — một basket, kèm `LineItems` (0..n). Tải bằng `.Include(b => b.LineItems)` — eager load đúng cách, KHÔNG N+1 ở tầng EF Core. | `services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs:92-95,119-121` |
| 6 | `BasketsEndpoints.ToResponseAsync` (BFF, dùng cho cả `GET /bff/basket` và `GET /bff/baskets/{id}`) | Gọi `products.GetProductsAsync()` — toàn bộ catalog — mỗi lần render basket, chỉ để join tên sản phẩm vào từng dòng. | `services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs:112-130` |
| 7 | `POST /bff/basket/items` (BFF) | Gọi `products.GetProductsAsync()` — toàn bộ catalog — chỉ để tìm MỘT sản phẩm theo id (`SingleOrDefault`). | `services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs:55-56` |

**Rationale**: Ba tiêu chí chấp nhận gốc của SCRUM-33 (phân trang / không N+1 / trần server-side) đọc
theo nghĩa đen gợi ý một cuộc rà soát trải rộng trên cả 4 service. Kiểm kê thực tế cho thấy chỉ có
đúng #1/#2 là "thiếu phân trang" thật, #3/#4 không có gì để phân trang (không tồn tại endpoint), #5 đã
đúng nhưng chưa được "rà soát bằng log truy vấn" như spec yêu cầu tường minh, và khoảng hở nặng nhất
thực ra không phải N+1 kiểu EF Core cổ điển mà là #6/#7 — một mẫu hình "over-fetch xuyên service" tệ
hơn N+1 (N+1 = N truy vấn nhỏ; đây = 1 lần fetch TOÀN BỘ catalog, tệ hơn khi catalog lớn dần và không
tự nhiên tốt hơn khi basket có nhiều dòng).

**Alternatives considered**:
- *Coi #3/#4 (orders/parties) là thiếu sót cần tạo mới endpoint danh sách để có cái mà "phân trang"*:
  bị loại — spec's Assumptions nói rõ phạm vi là rà soát những gì tồn tại, tạo endpoint danh sách mới
  cho hai service này là một tính năng nghiệp vụ khác (ai cần xem danh sách order/party, quyền hạn gì)
  không nằm trong AC/Test Scenario nào của SCRUM-33, và sẽ vi phạm YAGNI.
- *Bỏ qua #6/#7 vì "không phải N+1 đúng nghĩa EF Core"*: bị loại — Jira Test Scenario 2 mô tả đúng
  hành vi mà #6/#7 vi phạm ("load a basket with multiple items — confirm one query, not one per
  item"); #6/#7 là hiện thân thực tế gần nhất của tiêu chí đó trong toàn bộ hệ thống, chỉ khác tầng
  (BFF gọi HTTP, không phải EF Core gọi SQL) — bỏ qua sẽ bỏ lọt khoảng hở nghiêm trọng nhất mà cuộc
  rà soát tìm thấy.

## Decision 2: Cơ chế phân trang — offset-based (`page`/`pageSize`), không cursor

**Decision**: Dùng `page` (mặc định 1) + `pageSize` (mặc định 20, trần 100) cho `GET /products` và
`GET /bff/products`. Không dùng cursor-based paging.

**Rationale**: `GET /products` đã có thứ tự ổn định sẵn (`OrderBy(product => product.Name)`), đúng
điều kiện tiên quyết để offset-paging cho kết quả nhất quán giữa các trang. Catalog là dữ liệu đọc
nhiều/ghi hiếm (không phải một feed thay đổi liên tục cần cursor để tránh lệch trang khi dữ liệu chèn
giữa chừng) — offset-based đơn giản hơn, đủ dùng, và spec's Assumptions xác nhận cơ chế cụ thể là chi
tiết triển khai không bị ràng buộc.

**Alternatives considered**:
- *Cursor-based (keyset pagination trên `Name`)*: mạnh hơn cho tập dữ liệu rất lớn/ghi đồng thời cao,
  nhưng phức tạp hơn hẳn nhu cầu thực tế (catalog demo hiện có 3 sản phẩm, Jira Test Scenario 1 chỉ
  yêu cầu tới 500) — vi phạm "phức tạp phải tương xứng với nhu cầu".

## Decision 3: `DefaultPageSize = 20`, `MaxPageSize = 100` — hằng số do plan quyết định

**Decision**: `CatalogEndpoints` khai báo hai hằng số `DefaultPageSize = 20` và `MaxPageSize = 100`.
`pageSize` do client truyền bị `Math.Clamp(requested, 1, MaxPageSize)`; không truyền → dùng
`DefaultPageSize`.

**Rationale**: Spec's Assumptions để ngỏ con số cụ thể cho plan quyết định. 20 là một trang catalog
điển hình cho một client-facing listing (đủ nhỏ để BFF read budget p95 ≤ 300ms không bị đe doạ, đủ lớn
để không cần phân trang ngay với catalog demo 3 sản phẩm hiện tại). 100 làm trần đủ rộng cho một tác
vụ vận hành/nội bộ cần nhiều bản ghi hơn một trang UI thông thường, nhưng vẫn chặn được trường hợp
"pageSize=1000000" của Jira Test Scenario 3.

**Alternatives considered**:
- *Để `pageSize` không có trần, chỉ có `DefaultPageSize`*: bị loại thẳng — đây chính xác là điều spec
  FR-004 cấm ("bound is enforced server-side, not just a client-side page-size hint").
- *Đọc trần từ configuration (`appsettings.json`) thay vì hằng số*: cân nhắc nhưng bị loại cho vòng
  audit này — không có yêu cầu nào trong spec về việc trần này cần thay đổi được không cần build lại;
  thêm một trục cấu hình mới cho một con số không ai yêu cầu thay đổi là phức tạp không tương xứng.

## Decision 4: Filter `ids` bị chặn sẵn trên `GET /products` — sửa khoảng hở #6/#7 mà không thêm endpoint mới

**Decision**: `GET /products` chấp nhận thêm một query param tuỳ chọn `ids` (danh sách GUID phân tách
bởi dấu phẩy). Khi có mặt, trả về đúng tập sản phẩm khớp `ids` (không áp dụng `page`/`pageSize`, vì
tập đã bị chặn bởi chính số lượng `ids` mà caller truyền — thường là số dòng trong một basket, luôn
nhỏ). `ProductsApiClient` (BFF) thêm `GetProductsByIdsAsync(IReadOnlyCollection<Guid> ids, ct)`.
`BasketsEndpoints.ToResponseAsync` gọi với `basket.Items.Select(i => i.ProductId).Distinct()`;
`POST /basket/items` gọi với đúng một id (`[request.ProductId]`).

**Rationale**: Đây là cách sửa #6/#7 rẻ nhất tương xứng với nhu cầu — tái sử dụng đúng một endpoint
(`/products`) thay vì thêm `GET /products/{id}` (endpoint riêng cho lookup đơn) VÀ một
`POST /products/batch` (endpoint riêng cho lookup nhiều) như hai khả năng thay thế bên dưới. Với `ids`
filter, cả hai trường hợp dùng chung một code path: lookup 1 sản phẩm là `ids` filter với 1 phần tử.
Kết quả trực tiếp: basket có bao nhiêu dòng cũng chỉ tốn đúng MỘT lời gọi HTTP tới products — khớp
nguyên văn Jira Test Scenario 2 ("confirm one query... not one query per item"), dù đây là một lời gọi
HTTP giữa hai service chứ không phải một câu lệnh SQL.

**Alternatives considered**:
- *Thêm `GET /products/{id}` (lookup đơn) + giữ `GetProductsAsync()` đầy đủ cho basket rendering*:
  sửa được #7 (POST /basket/items) nhưng không sửa được #6 — basket rendering với basket có nhiều
  dòng khác nhau vẫn cần join nhiều tên, nghĩa là N lời gọi `GET /products/{id}` riêng lẻ (chính là
  N+1 kiểu client-side) hoặc quay lại full-fetch. Không giải quyết trọn vẹn khoảng hở nặng nhất.
- *Thêm `POST /products/batch` endpoint riêng cho cả hai use case*: giải quyết được cả #6/#7 nhưng
  thêm một endpoint hoàn toàn mới (và một method HTTP không thường dùng cho một thao tác đọc) trong
  khi `GET /products?ids=...` làm được y hệt bằng một filter bổ sung trên endpoint đã có — vi phạm
  "không thêm abstraction/bề mặt API vượt quá nhu cầu".

## Decision 5: `GET /products` đổi từ mảng trần sang envelope — chấp nhận như một phần bắt buộc của FR-001/FR-004, không phải một ngoại lệ Principle II cần biện minh riêng

**Decision**: `GET /products` trả về `PagedProductsResponse { Items, Page, PageSize, TotalCount }`
thay vì `ProductResponse[]` trần. Cập nhật `specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml`
(nơi `CatalogEndpoints.cs`'s XML-doc hiện đang trỏ tới) trong cùng PR với code.

**Rationale**: Đây đúng là một thay đổi shape (mảng → object), nhưng contract này có đúng MỘT
consumer — `ProductsApiClient` của BFF, sinh lại (regenerate) trong cùng PR, không phải một hợp đồng
được nhiều team/service độc lập-triển khai tiêu thụ theo lịch riêng như constitution Principle II hình
dung khi yêu cầu "a new explicit version plus a documented deprecation window". `ProductListResponse`
của BFF (consumer thật sự của frontend) thì CHỈ thêm field, không đổi field cũ — đúng ràng buộc spec
FR-007 ("trừ phần bổ sung liên quan trực tiếp đến phân trang"). Đối xứng với việc BFF đã tự bọc sẵn
envelope từ 002 với đúng lý do "để mở rộng sau mà không phá vỡ" — tính năng này hiện thực đúng phần
"mở rộng sau" đó, không phát minh một quyết định thiết kế mới.

**Alternatives considered**:
- *Giữ mảng trần, thêm `X-Total-Count` header thay vì bọc object*: tránh được hoàn toàn câu hỏi
  "breaking shape", nhưng lệch khỏi tiền lệ đã có ở BFF (`ProductListResponse` đã chọn cách bọc
  object, không phải header, cho đúng lý do tương tự) — hai lớp liền kề (products, bff) dùng hai quy
  ước khác nhau cho cùng một khái niệm sẽ khó theo dõi hơn là bất nhất, không phải đơn giản hơn.
- *Coi đây là breaking change thật, mở `/v2/products` song song `/products` cũ với deprecation
  window*: đúng quy trình Principle II cho một hợp đồng nhiều consumer độc lập, nhưng vượt xa mức độ
  cần thiết cho một contract nội bộ một-consumer, một-PR — 5 story point của SCRUM-33 không đủ chỗ
  cho một quy trình versioning đầy đủ mà không ai thực sự cần ở đây.

## Decision 6: Interceptor đếm câu lệnh SQL — hạ tầng test dùng chung mới, đặt ở `shared/IntegrationTestSupport`

**Decision**: Viết `QueryCountInterceptor : DbCommandInterceptor` (đếm số lần `ReaderExecutingAsync`
được gọi trong một scope) tại `shared/IntegrationTestSupport/QueryCountInterceptor.cs`, đăng ký qua
`optionsBuilder.AddInterceptors(...)` CHỈ trong test host (`WebApplicationFactory` override cho
integration test), không đăng ký ở cấu hình production của bất kỳ service nào.

**Rationale**: Rà soát xác nhận không có interceptor/`LogTo`/query-logging nào tồn tại trong repo
hôm nay (spec FR-002 dựa vào "rà soát bằng công cụ ghi log truy vấn" nhưng công cụ đó chưa tồn tại) —
phải xây trước khi audit được. Đặt ở `shared/IntegrationTestSupport` vì đây đúng vị trí đã đảm nhận
vai trò "hạ tầng test dùng chung cho nhiều service" (đối xứng với `shared/ServiceDefaults` cho hạ tầng
runtime dùng chung) — bất kỳ service nào khác cần một regression test kiểu "N câu lệnh, không đổi khi
N tăng" trong tương lai đều dùng lại được, không phải viết riêng cho baskets.

**Alternatives considered**:
- *Bật `EnableSensitiveDataLogging()` + `LogTo()` và đếm dòng log thủ công trong test*: khả thi nhưng
  giòn hơn (phải parse text log để đếm, dễ vỡ khi EF Core đổi format log giữa các phiên bản) so với
  interceptor gõ thẳng vào API chính thức (`DbCommandInterceptor`) được thiết kế đúng cho mục đích
  quan sát câu lệnh.
- *Đặt interceptor trong `services/baskets/tests/...` (cục bộ, không dùng chung)*: đơn giản hơn cho
  phạm vi ngay lập tức (chỉ baskets cần hôm nay), nhưng khi service khác cần cùng khả năng (ví dụ nếu
  orders sau này có `OrderLine` quan hệ) sẽ phải copy-paste — vi phạm đúng tinh thần "cross-cutting
  concern dùng chung, không hand-roll từng service" mà `IdentityValidationExtensions` (020) đã thiết
  lập tiền lệ.

## Decision 7: Rà soát lặp lại được — `tests/QueryCoverageTests`, đúng khuôn mẫu `ResilienceCoverageTests`

**Decision**: Dự án `tests/QueryCoverageTests` theo đúng khuôn mẫu
`tests/ResilienceCoverageTests/ResilienceCoverageScanner.cs`: danh sách viết tay
(`ExpectedListEndpoints`) liệt kê 2 endpoint danh sách kỳ vọng (products, bff/products) + file nguồn +
marker bắt buộc (`"DefaultPageSize"`, `"MaxPageSize"`) chứng minh đã phân trang và ép trần; và một
danh sách thứ hai (`ExpectedBoundedQuerySites`) liệt kê 2 điểm gọi BFF đã sửa
(`GetProductsByIdsAsync`) + marker bắt buộc. `orders`/`parties` được ghi chú tường minh trong
`research.md`/`data-model.md` là "0 endpoint danh sách tại thời điểm này", không có dòng trong scanner
(không có gì để scan) — đối xứng với cách 020's Decision 7 xử lý call site #4 (không tồn tại → không
có dòng, nhưng được ghi nhận bằng văn bản).

**Rationale**: Giống lý do gốc của `ContractCoverageScanner`/`ResilienceCoverageScanner`: danh sách
tường minh buộc một PR thêm endpoint danh sách mới hoặc một điểm gọi tải-dữ-liệu-quan-hệ mới phải
CHỦ ĐỘNG cập nhật scanner — nếu quên, `Scan_ActuallyExaminesEveryExpectedCallSite`-kiểu test (đếm
`ScannedCallSites.Count`) không tự bắt được endpoint hoàn toàn mới không có trong danh sách (đúng giới
hạn đã biết của khuôn mẫu này, không phải lỗi mới do tính năng này tạo ra — xem `plan.md`'s Assumption
dưới), nhưng bắt được ngay khi marker bị xoá/đổi tên ở một endpoint ĐÃ có trong danh sách — đúng cơ
chế review-bắt-buộc mà spec's User Story 1 Acceptance Scenario 3 ("endpoint danh sách mới... không có
ngoại lệ mặc định") kỳ vọng ở mức quy trình (PR review), không phải một cơ chế runtime tự động toàn
diện.

**Alternatives considered**:
- *Roslyn/reflection quét mọi `MapGet` trả về `IEnumerable`/mảng trong solution*: mạnh hơn về lý
  thuyết (tự phát hiện endpoint mới không cần cập nhật danh sách tay) nhưng phức tạp hơn nhiều so với
  mọi convention test khác trong repo, và có nguy cơ false-positive cao (nhiều `MapGet` trả về mảng
  không phải "danh sách không giới hạn cần phân trang", ví dụ một response cố định 3 phần tử) — không
  tương xứng với quy mô 5 story point của ticket này. Danh sách tường minh nhất quán với
  `ContractCoverageTests`/`ResilienceCoverageTests` đã được review và chấp nhận trước đó.
