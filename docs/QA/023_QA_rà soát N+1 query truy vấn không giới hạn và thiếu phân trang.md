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

### Thủ công — gieo dữ liệu rồi bấm Postman qua gateway

Dựng stack: `docker compose -f docker-compose.local.yml up -d --wait gateway-api bff-api products-api baskets-api elasticsearch otel-collector` (kéo theo identity-api và DB).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**; chạy `00 - Xác thực & phân quyền (Get Token) → 01`, rồi folder
**`23 - Phân trang và chống N+1 (/bff/products)`** (21 assertion) bằng **Runner**.

**Công tắc = dữ liệu** (spec này không có cờ cấu hình; điều kiện chạy là số sản phẩm trong catalog). *Ngoại lệ*: gieo bằng SQL (Products không có API tạo hàng loạt):

```bash
source .env; MSYS_NO_PATHCONV=1 docker exec ecomerce-local-products-db-1 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -d products -Q "INSERT INTO Products (Id,Name,Price) SELECT TOP 500 NEWID(), CONCAT('QA023-', ROW_NUMBER() OVER (ORDER BY (SELECT 1))), 1.00 FROM sys.all_objects a CROSS JOIN sys.all_objects b"
```

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-27)** |
|---|---|---|---|---|
| FR-001/US1/SC-001 — trang mặc định | Đã gieo 500 (tổng 503) | `23` bước 01 (`GET /bff/products`) | 20 dòng | `200`, `items=20`, `page=1`, `totalCount=503` |
| FR-004/US3/SC-003 — ép trần | (như trên) | `23` bước 02 (`?pageSize=1000000`) | 100 dòng | `200`, `items=100` |
| US3-KB3 — `pageSize` không hợp lệ | (như trên) | `23` bước 03, 04 (`pageSize=0` / `-5`) | Mặc định | `200`, `items=20` cả hai |
| Biên trang | (như trên) | `23` bước 05, 06, 07 (`page=6&pageSize=100` / `page=999` / `page=0`) | Trang cuối / rỗng / về trang 1 | `items=3` / `0` / `page=1, items=20` |
| US2/Decision 4 — bộ lọc `ids` *(gọi thẳng products-api, vì route công khai `/bff/products` không có tham số `ids`)* | (như trên) | `23` bước 08 (`GET {{productsUrl}}/products?ids=…`) | Đúng 3 dòng được hỏi | `200`, `items=3` |
| US2 — render giỏ 2 dòng chỉ gọi Products 1 lần, lọc theo ids | Dọn giỏ rồi thêm 2 sản phẩm | `23` bước 09 → 12 (ES theo correlation ID) | Đúng 1 span `GET /products`, `url.query` bắt đầu bằng `?ids=` | `200`, tên sản phẩm join đúng cho 2 dòng; Elasticsearch: **đúng 1 span**, `url.query = "?ids=Redacted"` |
| Dọn giỏ | (không có) | `23` bước 13 | Giỏ về rỗng | `204`; `GET /bff/basket` → `items: []` |
| Đầu vào bất thường (spec không nêu tường minh) | (như trên) | `23` bước 14, 15, 16 (`pageSize=abc` / `99999999999` / `page=2147483647&pageSize=100`) | Đáng ra `400` | `500` / `500` / `502` — xem QA_Debt |
| Nhánh `ids` vượt trần | (như trên) | `23` bước 17a, 17b, 17c (hỏi 200 id trực tiếp Products) | Ép trần 100 | Trả đủ **200 dòng** — xem QA_Debt |
| Mutation: `BasketsEndpoints.cs:129` `GetProductsByIdsAsync` → `GetProductsAsync(1,100).Items` *(ngoại lệ: sửa mã production + dựng lại `bff-api`)* | — | — | Test đỏ | **Không chạy lại ở lượt này** (sửa mã production bị chặn bởi quyền của phiên). Kết quả lượt trước còn nguyên: `Bff.Api.UnitTests` và `QueryCoverageTests` vẫn xanh. Bước 12 mới của Postman (kiểm `url.query` có `ids=`) nhắm đúng lỗ hổng này |

Dọn dẹp: `DELETE FROM Products WHERE Name LIKE 'QA023-%'` (còn đúng 3 dòng gốc, `SELECT COUNT(*) FROM Products` = 3); giỏ đã rỗng.

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

**Kết quả lượt QA này (2026-09-27)**: `QueryCoverageTests` **8/8**; `Products.Api.IntegrationTests` toàn suite **23/23** (Testcontainers); `Bff.Api.UnitTests` **22/22**;
`BasketQueryCountTests` **1/1**. Hợp đồng Pact `ProductsConsumerPactTests` không chạy lại ở đây (chạy sẽ ghi lại `pacts/bff-products.json`, xem QA 011).

## Kết luận

**PASS kèm ghi chú nghiêm trọng.** 4 nguồn nhất quán với nhau và với mã thật; kiểm kê đúng (chỉ `/products` là endpoint danh sách); mọi tiêu chí happy-case (US1 mặc định 20, US3 trần 100, US2 1 lời gọi/giỏ và số câu lệnh SQL không tăng theo dòng) chứng minh cả bằng test lẫn Postman trên stack Docker (21/21 assertion). Test xanh (chi tiết ở phần Tự động).
Ghi chú: (1) hồi quy đúng ở "khoảng hở nặng nhất" (BFF render giỏ over-fetch) không làm test nào đỏ — bộ test chỉ đếm lời gọi/kiểm chuỗi, không kiểm request (bước 12 của Postman kiểm được); (2) đầu vào `page`/`pageSize` bất thường gây `500`/`502` (kể cả tràn `int` → `OFFSET` âm);
(3) nhánh `ids` vượt trần 100 (đo 200 mục); (4) `quickstart.md` Bước 3 chạy 0 test, thoát mã 0. Chi tiết và hướng vá: [QA_Debt.md](QA_Debt.md) mục 023.
