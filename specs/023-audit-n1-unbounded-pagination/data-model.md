# Phase 1 Data Model: Rà soát N+1 query, truy vấn không giới hạn và thiếu phân trang

Tính năng này không thêm bảng/cột nào (xem `plan.md` Technical Context — Storage: không có migration).
Các "thực thể" dưới đây là hình dạng request/response và bản ghi kiểm kê dùng trong scanner test, không
phải bản ghi lưu trữ lâu dài.

## Page Request (tham số truy vấn, `GET /products` và `GET /bff/products`)

| Field | Description | Notes |
|---|---|---|
| Page | Số trang, 1-based | Mặc định 1 khi không truyền; giá trị < 1 được coi như 1 (research.md Decision 3) |
| PageSize | Số bản ghi mỗi trang | Mặc định `DefaultPageSize = 20`; bị `Math.Clamp(1, MaxPageSize)`, `MaxPageSize = 100` — client không thể vượt trần (spec FR-004) |
| Ids | Tuỳ chọn, danh sách GUID phân tách dấu phẩy | Khi có mặt, bỏ qua Page/PageSize — trả đúng tập khớp `ids` (research.md Decision 4); dùng bởi BFF để lookup bị chặn sẵn, không phải bởi client cuối |

**Validation rules**: `PageSize` sau khi clamp PHẢI nằm trong `[1, MaxPageSize]` — không có đường nào
để trả về nhiều hơn `MaxPageSize` bản ghi trong một lần gọi không dùng `ids`. `Ids` rỗng (mảng 0 phần
tử được truyền tường minh) trả về danh sách rỗng, không phải toàn bộ catalog — tránh một client gửi
`ids=` trống vô tình rơi lại vào hành vi unbounded cũ.

## Paged Products Response (`GET /products`, products service)

| Field | Description | Notes |
|---|---|---|
| Items | Danh sách `ProductResponse` (Id, Name, Price) của trang hiện tại (hoặc đúng tập khớp `ids`) | Giữ nguyên hình dạng từng phần tử — không đổi (spec FR-007) |
| Page | Trang đã trả về | Khi dùng `ids` filter: cố định `1` |
| PageSize | Kích thước trang đã áp dụng (sau clamp) | Khi dùng `ids` filter: bằng `Items.Count` |
| TotalCount | Tổng số bản ghi khớp điều kiện (toàn catalog khi không filter; số `ids` khớp được khi có filter) | Dùng để client tính có còn trang tiếp theo hay không |

**Validation rules**: `Items.Count` PHẢI ≤ `PageSize`. `TotalCount` PHẢI phản ánh tổng thật của catalog
(hoặc tập khớp `ids`), không phải `Items.Count` của riêng trang hiện tại — nếu không, client không thể
biết còn trang nào nữa.

## Product List Response (`GET /bff/products`, BFF — mở rộng `ProductListResponse` đã có từ 002)

| Field | Description | Notes |
|---|---|---|
| Items | Danh sách `ProductSummary` (Id, Name, Price) | Không đổi — field đã tồn tại từ 002 |
| Page | Forward từ Paged Products Response của products | **Field mới**, bổ sung thuần tuý |
| PageSize | Forward từ Paged Products Response của products | **Field mới**, bổ sung thuần tuý |
| TotalCount | Forward từ Paged Products Response của products | **Field mới**, bổ sung thuần tuý |

**Validation rules**: Ba field mới CHỈ được thêm vào, KHÔNG được đổi kiểu hay xoá `Items` — một client
SPA cũ (chưa biết đọc Page/PageSize/TotalCount) vẫn parse được `Items` y hệt trước đây (spec FR-007).

## Bounded Product Lookup (BFF nội bộ — không phải một request/response public, tham số gọi `GetProductsByIdsAsync`)

| Field | Description | Notes |
|---|---|---|
| Ids | Tập `Guid` cần tra tên/giá | `BasketsEndpoints.ToResponseAsync`: `basket.Items.Select(i => i.ProductId).Distinct()`; `POST /basket/items`: đúng 1 phần tử (`request.ProductId`) |

**Validation rules**: Một lần gọi `ToResponseAsync`/`POST /basket/items` PHẢI phát sinh đúng MỘT lời
gọi `ProductsApiClient` (dù `Ids` có 1 hay N phần tử) — đây là bất biến mà
`ProductLookupBatchingTests` (BFF) khoá lại thành test tự động (spec FR-002/SC-002, Jira Test Scenario
2).

## Query Count Observation (hạ tầng test, `shared/IntegrationTestSupport/QueryCountInterceptor.cs`)

| Field | Description | Notes |
|---|---|---|
| ExecutedCommandCount | Số lần `ReaderExecutingAsync` được gọi trong phạm vi một request/scope | Reset đầu mỗi test; đọc lại sau khi gọi endpoint cần kiểm tra |

**Validation rules**: Với `GET /baskets/{id}` trên một basket có N dòng (N thay đổi giữa các lần chạy
test, ví dụ 1 và 5), `ExecutedCommandCount` PHẢI là một hằng số KHÔNG phụ thuộc N (khoá hành vi
`.Include()` hiện có thành regression test — spec FR-002/SC-002).

## List Endpoint Inventory Entry (mã nguồn test, `tests/QueryCoverageTests/QueryCoverageScanner.cs`)

Một dòng trong `ExpectedListEndpoints` — cùng hình dạng với `Boundary`/`OutboundCallSite` record đã có
ở `ContractCoverageScanner`/`ResilienceCoverageScanner`.

| Field | Description | Notes |
|---|---|---|
| Name | Tên định danh endpoint | `products-listing`, `bff-products-listing` |
| SourceFile | File nguồn nơi endpoint được khai báo | `services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs`, `services/bff/src/Bff.Api/Features/Products/ProductsEndpoints.cs` |
| RequiredMarkers | Chuỗi PHẢI xuất hiện trong `SourceFile`, chứng minh đã phân trang + ép trần | `["DefaultPageSize", "MaxPageSize"]` cho products; `["Page", "PageSize"]` cho BFF (forward tham số) |

**Validation rules**: Danh sách này PHẢI liệt kê đủ 2 endpoint danh sách đang tồn tại (kiểm kê ở
`research.md` Decision 1, #1/#2). `orders`/`parties` KHÔNG có dòng — vì #3/#4 xác nhận không tồn tại
endpoint danh sách nào ở hai service đó (ghi chú bằng văn bản trong `research.md`, không phải một dòng
scanner "trống" — một dòng trỏ tới file không tồn tại sẽ tự làm scanner đỏ sai mục đích).

## Bounded Query Site Inventory Entry (mã nguồn test, cùng scanner)

Một dòng trong `ExpectedBoundedQuerySites`.

| Field | Description | Notes |
|---|---|---|
| Name | Tên định danh điểm gọi | `bff-basket-render`, `bff-basket-add-item` |
| SourceFile | File nguồn | `services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs` (cả hai) |
| RequiredMarkers | Chuỗi PHẢI xuất hiện, chứng minh dùng lookup bị chặn thay vì full-fetch | `["GetProductsByIdsAsync"]` |

**Validation rules**: Thêm một điểm gọi tải-dữ-liệu-quan-hệ mới trong hệ thống (ví dụ orders sau này
join order lines với product) PHẢI thêm một dòng tương ứng vào đây trong cùng PR — thiếu là vi phạm
hợp đồng (xem `contracts/query-coverage-inventory-contract.md`).
