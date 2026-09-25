# QA: Rà soát N+1 query, truy vấn không giới hạn và thiếu phân trang

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

Không có spec 022 (đã có ghi chú ở `development/023`, xác nhận bằng `ls specs/`).

## Luồng happy-case đã rà soát

1. **Kiểm kê** (research.md Decision 1): grep `MapGet("` toàn `services/*/src` — chỉ **`/products`** (Products và BFF) trả về tập hợp; mọi route còn lại là get-by-id / `/basket` / `/baskets/current`.
2. `GET /products`: `DefaultPageSize 20`, `MaxPageSize 100`, `Math.Clamp` phía server, envelope `PagedProductsResponse` — khớp `development/023` mục 1.
3. BFF render giỏ/add-item dùng `GetProductsByIdsAsync(ids)` (`BasketsEndpoints.cs:57`, `:129`) — đúng 1 lời gọi `GET /products?ids=…`; giỏ rỗng return sớm, không gọi Products.
4. `QueryCountInterceptor` + `tests/QueryCoverageTests` — hạ tầng test, đã chạy lại.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001/US1/SC-001 — không tham số → trang mặc định 20 | [`ProductListingPaginationTests.cs:36`](../../services/products/tests/Products.Api.IntegrationTests/ProductListingPaginationTests.cs#L36) | `dotnet test services/products/tests/Products.Api.IntegrationTests --filter FullyQualifiedName~ProductListingPaginationTests` |
| FR-004/US3/SC-003 — `pageSize=1000000` bị ép trần 100 | [`:67`](../../services/products/tests/Products.Api.IntegrationTests/ProductListingPaginationTests.cs#L67) | (lệnh như trên) |
| FR-004/US3-KB3 — `pageSize` 0/âm → mặc định | [`:95`](../../services/products/tests/Products.Api.IntegrationTests/ProductListingPaginationTests.cs#L95) (2 ca) | (lệnh như trên) |
| US2/Decision 4 — bộ lọc `ids` | [`:123`](../../services/products/tests/Products.Api.IntegrationTests/ProductListingPaginationTests.cs#L123) | (lệnh như trên) |
| Hợp đồng envelope (spec 002 cập nhật bởi 023) | [`CatalogEndpointsTests.cs:39`](../../services/products/tests/Products.Api.IntegrationTests/CatalogEndpointsTests.cs#L39) · [`:80`](../../services/products/tests/Products.Api.IntegrationTests/CatalogEndpointsTests.cs#L80) | `dotnet test services/products/tests/Products.Api.IntegrationTests --filter FullyQualifiedName~CatalogEndpointsTests` |
| FR-002/US2 — render giỏ nhiều dòng → đúng 1 lời gọi Products | [`ProductLookupBatchingTests.cs:36`](../../services/bff/tests/Bff.Api.UnitTests/ProductLookupBatchingTests.cs#L36) · [`:64`](../../services/bff/tests/Bff.Api.UnitTests/ProductLookupBatchingTests.cs#L64) · [`:96`](../../services/bff/tests/Bff.Api.UnitTests/ProductLookupBatchingTests.cs#L96) (giỏ rỗng) | `dotnet test services/bff/tests/Bff.Api.UnitTests --filter FullyQualifiedName~ProductLookupBatchingTests` |
| FR-001/FR-007 — BFF chuyển tiếp `page`/`pageSize`/`ids`, giữ metadata trang | [`ProductsEndpointPaginationTests.cs:31`](../../services/bff/tests/Bff.Api.UnitTests/ProductsEndpointPaginationTests.cs#L31) · [`:56`](../../services/bff/tests/Bff.Api.UnitTests/ProductsEndpointPaginationTests.cs#L56) · [`:83`](../../services/bff/tests/Bff.Api.UnitTests/ProductsEndpointPaginationTests.cs#L83) · [`:105`](../../services/bff/tests/Bff.Api.UnitTests/ProductsEndpointPaginationTests.cs#L105) | `dotnet test services/bff/tests/Bff.Api.UnitTests --filter FullyQualifiedName~ProductsEndpointPaginationTests` |
| FR-002/SC-002 — số câu lệnh SQL không tăng theo số dòng giỏ | [`BasketQueryCountTests.cs:46`](../../services/baskets/tests/Baskets.Api.IntegrationTests/BasketQueryCountTests.cs#L46) | `dotnet test services/baskets/tests/Baskets.Api.IntegrationTests --filter FullyQualifiedName~BasketQueryCountTests` |
| FR-005 — rà soát tĩnh lặp lại được (8 test, gồm test tự bảo vệ) | [`QueryCoverageTests.cs:28`](../../tests/QueryCoverageTests/QueryCoverageTests.cs#L28) · [`:44`](../../tests/QueryCoverageTests/QueryCoverageTests.cs#L44) · [`:65`](../../tests/QueryCoverageTests/QueryCoverageTests.cs#L65) · [`:80`](../../tests/QueryCoverageTests/QueryCoverageTests.cs#L80) · [`:98`](../../tests/QueryCoverageTests/QueryCoverageTests.cs#L98) · [`:124`](../../tests/QueryCoverageTests/QueryCoverageTests.cs#L124) · [`:152`](../../tests/QueryCoverageTests/QueryCoverageTests.cs#L152) · [`:175`](../../tests/QueryCoverageTests/QueryCoverageTests.cs#L175) | `dotnet test tests/QueryCoverageTests` |

**Kết quả lượt QA này (2026-09-24)**: `QueryCoverageTests` **8/8**; `Products.Api.IntegrationTests` toàn suite **23/23** (Testcontainers, ~6 phút 25 giây); `Bff.Api.UnitTests` **22/22**;
`BasketQueryCountTests` **1/1** — không đổi sau khi dịch comment. Hợp đồng Pact `ProductsConsumerPactTests` không chạy lại ở đây (chạy sẽ ghi lại `pacts/bff-products.json`, xem QA 011).

### Thủ công — gieo 500 sản phẩm (tag `QA023-…`, tổng 503) vào `products-db` của stack, gọi qua gateway `:5300/bff/products` với token thật

| Request | Kỳ vọng theo tài liệu | **Đã quan sát** |
|---|---|---|
| (không tham số) | trang mặc định 20 (US1/SC-001) | `200`, `items=20`, `totalCount=503` |
| `?pageSize=1000000` | ép trần 100 (US3/SC-003) | `200`, `items=100` |
| `?pageSize=0` / `-5` | fallback mặc định | `200`, `items=20` |
| `?page=6&pageSize=100` / `?page=999` / `?page=0` | trang cuối / rỗng / chuẩn hoá về 1 | `items=3` / `0` / `20` |
| Render giỏ 2 dòng (`GET /bff/basket`) | 1 lời gọi Products cho cả request (US2) | `200`, tên sản phẩm join đúng; Elasticsearch: **đúng 1 span `GET /products`** |
| Đầu vào bất thường: `?pageSize=abc` / `?pageSize=99999999999` / `?page=2147483647&pageSize=100` | (spec không nêu tường minh) | `500` / `500` / `502` — xem QA_Debt |
| Mutation: `BasketsEndpoints.cs:129` `GetProductsByIdsAsync` → `GetProductsAsync(1,100).Items` | Test đỏ | **`Bff.Api.UnitTests` 22/22 và `QueryCoverageTests` 8/8 vẫn xanh** — xem QA_Debt (đã `git checkout --`, `git status` sạch) |

Đã dọn: `DELETE FROM Products WHERE Name LIKE 'QA023-%'` → còn đúng 3 dòng gốc.

## Kết luận

**PASS kèm ghi chú nghiêm trọng.** 4 nguồn nhất quán với nhau và với mã thật; kiểm kê đúng (chỉ `/products` là endpoint danh sách); mọi tiêu chí happy-case (US1 mặc định 20, US3 trần 100,
US2 1 lời gọi/giỏ và số câu lệnh SQL không tăng theo dòng) chứng minh cả bằng test lẫn dữ liệu thật trên stack Docker. 23+8+22+1 test xanh. Ghi chú: (1) hồi quy đúng ở "khoảng hở nặng nhất"
(BFF render giỏ over-fetch) không làm test nào đỏ — bộ test chỉ đếm lời gọi/kiểm chuỗi, không kiểm request; (2) đầu vào `page`/`pageSize` bất thường gây `500`/`502` (kể cả tràn `int` → `OFFSET` âm);
(3) nhánh `ids` vượt trần 100 (đo 200 mục); (4) `quickstart.md` Bước 3 chạy 0 test, thoát mã 0. Chi tiết và hướng vá: [QA_Debt.md](QA_Debt.md) mục 023.
