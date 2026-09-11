# Implementation Plan: Rà soát N+1 query, truy vấn không giới hạn và thiếu phân trang trên toàn bộ dịch vụ

**Branch**: `code/Audit-for-N+1-queries-unbounded-queries-missing-pagination` | **Date**: 2026-09-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/023-audit-n1-unbounded-pagination/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Rà soát mã nguồn hiện có (4 service nghiệp vụ + BFF) cho thấy phạm vi thực tế hẹp hơn nhiều so với
việc đọc ba tiêu chí chấp nhận của SCRUM-33 theo nghĩa đen — giống đúng bài học của
**020-timeouts-retry-circuit-breaker**: kiểm kê trước, thiết kế sau.

1. **`GET /products` (products service) không phân trang** — `CatalogEndpoints.cs:20-25` trả về
   toàn bộ bảng `Products` không giới hạn. Đây là endpoint danh sách KHÔNG-phân-trang duy nhất còn
   tồn tại trong hệ thống (`orders` và `parties` không có endpoint danh sách nào cả — chỉ có
   get-by-id — nên không có gì để sửa ở đó).
2. **`GET /bff/products` (BFF) kế thừa nguyên khoảng hở đó** — `ProductsEndpoints.cs:22-29` gọi
   `products.GetProductsAsync()` không tham số và forward toàn bộ. Đáng chú ý: response đã được bọc
   trong `ProductListResponse { Items }` từ trước (002-gateway-bff-routing), với comment tại chỗ nói
   rõ chủ đích: "lets the response grow later (paging, totals) without becoming a breaking change" —
   tính năng này hiện thực đúng chủ đích đã để sẵn đó, không phát minh một shape mới.
3. **N+1 kiểu EF Core cổ điển KHÔNG tồn tại** ở bất kỳ service nào — kiến trúc hiện tại nhỏ (DbContext
   dùng thẳng trong endpoint, không có tầng repository lặp), và nơi duy nhất tải dữ liệu quan hệ
   (`BasketEndpoints` tải `Basket` cùng `LineItems`) đã dùng `.Include()` — eager load đúng cách.
   Nhưng đây là "đúng theo đọc code", chưa phải "đã rà soát bằng log truy vấn" mà spec FR-002/User
   Story 2 Independent Test yêu cầu tường minh — cần một test hồi quy dựa trên đếm câu lệnh SQL thật,
   không chỉ tin vào việc đọc code.
4. **Khoảng hở thật sự tương đương N+1 nằm ở tầng BFF, không phải EF Core**: `BasketsEndpoints.cs`
   có hai chỗ gọi `products.GetProductsAsync()` — tải **toàn bộ catalog** — chỉ để (a) tìm một sản
   phẩm theo id khi thêm vào giỏ (`:55-56`), và (b) join tên sản phẩm vào từng dòng khi hiển thị giỏ
   hàng (`:122`). Đây chính xác là điều Jira Test Scenario 2 mô tả ("load a basket with multiple
   items — confirm one query... not one query per item") — chỉ khác là khoảng hở thực tế còn tệ hơn:
   không phải "một truy vấn cho mỗi item" mà là "một lần fetch TOÀN BỘ catalog" bất kể basket có bao
   nhiêu item, mỗi lần render.
5. **Không có giới hạn kích thước trang phía server ở đâu cả** (FR-004) — hệ quả tất yếu của #1/#2,
   sẽ được xây cùng lúc với phân trang, không phải một bước riêng.

Cách tiếp cận: thêm `page`/`pageSize` (có trần server-side) và một filter `ids` bị chặn sẵn (bounded)
vào `GET /products`; BFF forward `page`/`pageSize` cho `/bff/products` và đổi hai điểm gọi catalog đầy
đủ ở `BasketsEndpoints` sang gọi `ids`-filtered (đúng 1 lời gọi downstream bất kể basket có bao nhiêu
dòng); thêm một EF Core query-count interceptor (chưa từng tồn tại trong repo) để biến "đã rà soát
bằng log truy vấn" từ một khẳng định thành một test hồi quy thật; và một dự án scanner mới
`tests/QueryCoverageTests` theo đúng khuôn mẫu `tests/ResilienceCoverageTests` để khoảng hở không thể
tái xuất hiện âm thầm.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`, khớp constitution và toàn bộ `.csproj` hiện có).

**Primary Dependencies**: Không thêm NuGet package mới. `Skip`/`Take` là LINQ lõi đã có sẵn qua EF
Core (đã dùng trong `CatalogEndpoints.cs` qua `.OrderBy`/`.Select`/`.ToListAsync`). Interceptor đếm
câu lệnh SQL dùng `Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor`, một phần của
`Microsoft.EntityFrameworkCore` đã được tham chiếu ở mọi service — không phải gói mới.

**Storage**: SQL Server qua EF Core (đã có). KHÔNG có migration nào — `page`/`pageSize`/`ids` là tham
số truy vấn ở tầng API, không đổi hình dạng bảng `Products`/`Baskets`/`BasketLineItems`.

**Testing**: xUnit, Testcontainers (SQL Server) cho integration test — khuôn mẫu đã có ở
`services/baskets/tests/Baskets.Api.IntegrationTests`. Unit test kiểu counting-handler đã có tiền lệ ở
`RetryMethodPolicyTests` (020) — tái sử dụng đúng kỹ thuật đó cho việc đếm số lần gọi
`ProductsApiClient` khi render basket. Dự án scanner mới theo khuôn mẫu
`tests/ResilienceCoverageTests` (đọc file dạng văn bản, không compile ngược lại project bị kiểm tra).

**Target Platform**: Container Linux trên Kubernetes (theo constitution) — không đổi.

**Project Type**: Bổ sung vào monorepo backend nhiều service hiện có — không phải service runtime
mới; không đụng frontend (xem Assumptions bên dưới về vì sao an toàn để hoãn phần UI).

**Performance Goals**: Không thêm ngân sách SLO mới ngoài các mặc định đã có ở Principle VIII (BFF
read p95 ≤ 300ms/p99 ≤ 800ms) — phân trang làm response nhỏ hơn, không nặng hơn, nên không có rủi ro
đụng ngân sách hiện có.

**Constraints**: `DefaultPageSize = 20`, `MaxPageSize = 100` — hai hằng số mới do plan này quyết định
(spec's Assumptions để ngỏ con số cụ thể là chi tiết triển khai). KHÔNG được đổi cột/entity nào của
`Products`/`Baskets`/`BasketLineItems` (spec FR-007). KHÔNG được đổi `BasketResponse`/`BasketItem` của
BFF (chỉ đổi *cách* dữ liệu được lấy, không đổi *hình dạng* trả về cho basket). `ProductListResponse`
của BFF CHỈ được bổ sung field (Page/PageSize/TotalCount), không được đổi/xoá field `Items` hiện có.

**Scale/Scope**: 2 endpoint danh sách thực sự tồn tại trong toàn hệ thống (`GET /products` của
products, `GET /bff/products` của BFF) cần phân trang; 2 điểm gọi ở BFF (`BasketsEndpoints.cs`) cần
đổi sang gọi bị chặn (`ids`); 1 interceptor mới dùng chung; 1 dự án scanner mới; `orders` và `parties`
không có gì để sửa (không có endpoint danh sách) nhưng được ghi nhận tường minh trong kiểm kê để một
endpoint danh sách tương lai ở đó bị bắt buộc đi qua cùng cơ chế review.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Áp dụng thế nào | Trạng thái |
|---|---|---|
| I. Service Autonomy and Bounded Context | Không đổi quyền sở hữu dữ liệu; `products` vẫn là service duy nhất đọc/ghi bảng `Products`. BFF vẫn chỉ gọi qua `ProductsApiClient` (HTTP), không truy cập DB của products trực tiếp. | PASS |
| II. Contract-First Integration | `GET /products` đổi từ mảng trần sang envelope (`items`/`page`/`pageSize`/`totalCount`) — về mặt kỹ thuật là một thay đổi shape. Nhưng contract này CHỈ có một consumer: `ProductsApiClient` được generate lại trong cùng PR (không phải một service độc lập-versioned tiêu thụ nó theo lịch riêng — spec FR-004 gốc chính là chủ ý "server-side bound", và constitution Principle VIII tự thân yêu cầu "every collection endpoint MUST paginate"). Theo đúng tiền lệ đã có (mỗi feature chạm tới một contract thì viết bản cập nhật trong `contracts/` của CHÍNH feature đó, rồi XML-doc trong code trỏ sang feature mới nhất — ví dụ `BasketsEndpoints.cs` đã trỏ sang `specs/004.../bff-openapi.yaml` thay vì `specs/002`), tính năng này viết `contracts/downstream-openapi.yaml` + `contracts/bff-openapi.yaml` của riêng nó (chỉ 2 path đổi: `/products`, `/bff/products`), không sửa đè lên snapshot của 002. `ProductListResponse` của BFF chỉ THÊM field, không phá field cũ (đúng như comment gốc đã dự liệu). | PASS (có ghi chú, không phải ngoại lệ cần Complexity Tracking) |
| III. Test-First Development | Interceptor đếm câu lệnh + integration test cho basket, coverage scanner, và counting-handler test cho BFF đều viết trước ở dạng thất bại (basket load hôm nay đã đúng nên test này thất bại vì *thiếu công cụ đo*, không phải vì hành vi sai — viết interceptor trước, sau đó test pass ngay vì hành vi vốn đã đúng; ngược lại, hai điểm gọi catalog-đầy-đủ ở BFF thì test thất bại vì hành vi sai thật, sửa code để pass). Chi tiết trình tự ở `tasks.md`. | PASS (kế hoạch ở Phase 2/tasks) |
| IV. Event-Driven by Default | Không liên quan — tính năng không đụng messaging/broker. | N/A |
| V. Tenant Isolation Is a Security Boundary | `GET /products` không phân biệt theo tenant hiện tại (catalog dùng chung); phân trang không đổi cách tenant context được resolve/propagate. | N/A |
| VI. Secure by Default | Không đổi authorization policy của bất kỳ route nào (`RequireAuthorization(AuthorizationPolicies.ApiScope)` giữ nguyên). `ids` filter chỉ nhận GUID, không mở khả năng injection (EF Core parameterizes `Where(x => ids.Contains(x.Id))`). | PASS |
| VII. Observable by Default | Không đổi cách OTel/log được cấu hình. Interceptor đếm câu lệnh chỉ hoạt động trong test host, không đăng ký ở production `AddServiceDefaults()`. | N/A (không phải mục tiêu của tính năng này) |
| VIII. Performance and Resilience Budgets | Đây chính là Principle VIII — tính năng khép kín đúng ba khoảng hở nêu tên trong đó ("every collection endpoint MUST paginate, every query MUST be bounded, N+1 access patterns are defects"). | PASS (là mục tiêu chính) |
| IX. Frontend Discipline | Ngoài phạm vi trực tiếp — xem Assumptions: catalog seed hiện có 3 sản phẩm, dưới `DefaultPageSize=20`, nên SPA (`frontend/apps/web/src/features/catalog/ProductList.tsx`) không có regression quan sát được hôm nay. Khi catalog vượt `DefaultPageSize`, SPA cần một cập nhật riêng (phân trang/"load more" ở UI) — ghi nhận như một rủi ro/khoản nợ cần theo dõi, không phải một việc tính năng này phải làm (SCRUM-33 là ticket của Developer về tầng dữ liệu, không phải một ticket UX). | PASS (có ghi chú rủi ro tương lai, không phải vi phạm hiện tại) |
| X. Toggle-Gated, Reversible Delivery | Đây là một defect fix bắt buộc bởi chính constitution (Principle VIII dùng từ "MUST"), không phải một tính năng nghiệp vụ mới hướng người dùng — giống lý do 020 không cần toggle cho việc siết cấu hình resilience. Với dữ liệu thật hôm nay (3 sản phẩm), hành vi API không đổi quan sát được. Rollback là revert code, không cần schema rollback (không có migration). | PASS (lý giải, không phải vi phạm) |

Không có vi phạm nào cần biện minh tại Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/023-audit-n1-unbounded-pagination/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
services/
├── products/
│   └── src/Products.Api/
│       └── Features/Catalog/
│           └── CatalogEndpoints.cs        # [SỬA] page/pageSize (mặc định 20, trần 100) + ids filter; response bọc PagedProductsResponse
│   └── tests/
│       ├── Products.Api.UnitTests/
│       │   └── ProductListingPaginationTests.cs   # [MỚI] mặc định bị chặn, trần bị ép, ids filter trả đúng tập
│       └── Products.Api.IntegrationTests/
│           └── ProductListingPaginationTests.cs   # [MỚI] seed 500 sản phẩm, gọi không tham số, xác nhận trang bị giới hạn (Jira Test Scenario 1)
│
└── bff/
    └── src/Bff.Api/
        ├── DownstreamClients/
        │   └── ProductsApiClient.cs               # [SỬA] GetProductsAsync(page,pageSize) + GetProductsByIdsAsync(ids)
        └── Features/
            ├── Products/
            │   └── ProductsEndpoints.cs            # [SỬA] nhận page/pageSize, ProductListResponse thêm Page/PageSize/TotalCount
            └── Baskets/
                └── BasketsEndpoints.cs              # [SỬA] ToResponseAsync + add-item dùng GetProductsByIdsAsync thay vì GetProductsAsync() đầy đủ
    └── tests/Bff.Api.UnitTests/
        └── ProductLookupBatchingTests.cs            # [MỚI] basket N dòng → đúng 1 lời gọi products client, không phải N (Jira Test Scenario 2 ở tầng BFF)

shared/
└── IntegrationTestSupport/
    └── QueryCountInterceptor.cs                     # [MỚI] DbCommandInterceptor đếm câu lệnh SQL, dùng chung cho mọi service's integration test

services/baskets/tests/Baskets.Api.IntegrationTests/
└── BasketQueryCountTests.cs                          # [MỚI] basket N dòng, tải qua GET /baskets/{id} thật, đếm câu lệnh SQL không tăng theo N (khoá hành vi .Include() đúng hiện có thành regression test)

tests/
└── QueryCoverageTests/                                # [MỚI] dự án xUnit quét, theo khuôn mẫu tests/ResilienceCoverageTests
    ├── QueryCoverageTests.csproj
    ├── QueryCoverageScanner.cs                         # Danh sách tường minh: 2 endpoint danh sách (products, bff/products) + 2 điểm gọi bounded (BFF basket) + ghi nhận orders/parties "0 endpoint danh sách"
    └── QueryCoverageTests.cs
```

**Structure Decision**: Không tạo service mới. Thay đổi tập trung ở đúng 2 service đã kiểm kê có
endpoint danh sách (`products`) và service phía trước nó (`bff`) — không đụng `orders`/`parties`
(không có gì để sửa). Interceptor dùng chung đặt ở `shared/IntegrationTestSupport` — đúng vị trí đã có
sẵn vai trò hạ tầng test dùng chung (song song cách `shared/ServiceDefaults` giữ cấu hình runtime dùng
chung). Dự án scanner mới đặt tại `tests/QueryCoverageTests`, song song `tests/ResilienceCoverageTests`
đã có, giữ đúng khuôn mẫu "scanner filesystem-only, danh sách kỳ vọng viết tay, không tự khám phá".

## Complexity Tracking

> Không có vi phạm Constitution Check nào cần biện minh — bảng này để trống có chủ đích.
