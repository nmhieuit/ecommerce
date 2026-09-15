# Bước 023: Thay đổi nghiệp vụ so với bước 021

## Phạm vi

Tài liệu này mô tả phần code được tạo bởi bước 023 so với trạng thái code sau bước 021 — không có
bước "022". **Khác với trường hợp 012** (có lịch sử Git thật: 5 commit dưới `specs/012-sonarqube-quality-gate/`,
kết thúc bằng 1 commit xoá thư mục đặc tả sau khi 013 kế thừa — xem
[013_Architect_*.md](../architecture/013_Architect_cổng%20chất%20lượng%20CI.md)), **022 không để lại
bất kỳ dấu vết nào trong lịch sử Git** — đã kiểm tra `git log --all` cho `specs/022*`, không có commit,
không có branch, không có file nào từng nhắc tới nó. Không đủ bằng chứng để kết luận lý do (có thể là
1 thư mục cục bộ bị bỏ trước khi commit, hoặc đơn giản là số bị bỏ qua) — chỉ xác nhận được sự thật là
số 022 không tồn tại, không suy đoán thêm.

Ranh giới commit được xác định từ lịch sử Git:

- Mốc hoàn tất bước 021: commit `c17a5d0`.
- Đặc tả bước 023: commit `1bc3c50` (spec), `c56607a` (plan), `b8bfa75` (tasks).
- Scanner rà soát (`tests/QueryCoverageTests`): commit `e9a536c`.
- Triển khai chính (phân trang, filter `ids`, ép trần server-side): commit `d73ddb1`.
- Polish (T021-T025): commit `b8740d5`.
- Xác nhận test thật qua Testcontainers (Docker khởi động lại giữa phiên): commit `abe081b`.
- Merge vào `master`: commit `b70a08e`.

## 1. `GET /products`: từ mảng trần không giới hạn sang trang có giới hạn + envelope

[services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs](../../services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs)

```csharp
public static class CatalogEndpoints
{
    // 023: mặc định khi caller không truyền pageSize (hoặc truyền giá trị <= 0) — trước bước này
    // KHÔNG tồn tại, GET /products trả toàn bộ bảng Products không điều kiện.
    public const int DefaultPageSize = 20;

    // 023: trần server-side — FR-004 cấm việc trần chỉ là gợi ý phía client. Math.Clamp áp dụng bên
    // dưới bất kể caller truyền gì, kể cả pageSize=1000000.
    public const int MaxPageSize = 100;

    public static WebApplication MapCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/products", async (
                int? page, int? pageSize, string? ids,   // 023: 3 query param mới — trước chỉ nhận request rỗng
                ProductsDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var query = dbContext.Products.AsNoTracking();

                // 023: filter ids= chặn thẳng trên endpoint đã có, thay vì thêm GET /products/{id}
                // hay POST /products/batch riêng — bị chặn bởi chính số lượng ids caller truyền
                // (research.md Decision 4), KHÔNG áp dụng page/pageSize.
                if (!string.IsNullOrWhiteSpace(ids))
                {
                    var requestedIds = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(id => Guid.TryParse(id, out var parsed) ? parsed : (Guid?)null)
                        .Where(id => id.HasValue).Select(id => id!.Value).ToArray();

                    var matched = await query.Where(product => requestedIds.Contains(product.Id))
                        .OrderBy(product => product.Name)
                        .Select(product => new ProductResponse(product.Id, product.Name, product.Price))
                        .ToListAsync(cancellationToken);

                    return new PagedProductsResponse(matched, Page: 1, PageSize: matched.Count, TotalCount: matched.Count);
                }

                // 023: Math.Clamp — trước bước này chỉ có OrderBy(Name) rồi ToListAsync() không giới hạn.
                var effectivePageSize = Math.Clamp(pageSize is > 0 ? pageSize.Value : DefaultPageSize, 1, MaxPageSize);
                var effectivePage = page is > 0 ? page.Value : 1;
                var totalCount = await query.CountAsync(cancellationToken);
                var items = await query.OrderBy(product => product.Name)
                    .Skip((effectivePage - 1) * effectivePageSize).Take(effectivePageSize)
                    .Select(product => new ProductResponse(product.Id, product.Name, product.Price))
                    .ToListAsync(cancellationToken);

                // 023: envelope thay ProductResponse[] trần — chấp nhận đổi shape vì đúng 1 consumer
                // (BFF), sinh lại client trong cùng PR (research.md Decision 5).
                return new PagedProductsResponse(items, effectivePage, effectivePageSize, totalCount);
            })
            .RequireAuthorization(AuthorizationPolicies.ApiScope);

        return app;
    }

    public sealed record PagedProductsResponse(
        IReadOnlyList<ProductResponse> Items, int Page, int PageSize, int TotalCount);   // 023: kiểu mới
}
```

## 2. BFF → Products: chặn over-fetch xuyên service (khoảng hở nặng nhất của cuộc rà soát)

[services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs](../../services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs)

```csharp
group.MapPost("/basket/items", async (AddBasketItemRequest request, BasketsApiClient baskets,
        ProductsApiClient products, CancellationToken cancellationToken) =>
    {
        // 023: TRƯỚC bước này, dòng tương đương gọi products.GetProductsAsync() — TOÀN BỘ catalog —
        // chỉ để tìm đúng 1 sản phẩm bằng SingleOrDefault phía client. Nay gọi đúng 1 id.
        var catalog = await products.GetProductsByIdsAsync([request.ProductId], cancellationToken);
        var product = catalog.SingleOrDefault(entry => entry.Id == request.ProductId);
        // ...
    })

internal static async Task<BasketResponse> ToResponseAsync(
    BasketResource basket, ProductsApiClient products, CancellationToken cancellationToken)
{
    if (basket.Items.Count == 0)
    {
        return new BasketResponse(basket.Id, basket.CustomerRef, [], basket.Total);
    }

    // 023: TRƯỚC bước này, dòng này gọi products.GetProductsAsync() — TOÀN BỘ catalog — mỗi lần
    // render basket, chỉ để join tên sản phẩm vào từng dòng. Đây là "over-fetch xuyên service" —
    // tệ hơn N+1 kiểu EF Core cổ điển vì không tự nhiên tốt hơn khi basket có nhiều dòng.
    var distinctProductIds = basket.Items.Select(item => item.ProductId).Distinct().ToArray();
    var namesById = (await products.GetProductsByIdsAsync(distinctProductIds, cancellationToken))
        .ToDictionary(product => product.Id, product => product.Name);
    // ...
}
```

`ProductsApiClient.GetProductsByIdsAsync(...)` (mới, gọi `GET /products?ids=...`) trả `[]` ngay khi
`ids` rỗng — không gọi HTTP nào cho basket rỗng.

## 3. Rà soát lặp lại được — hạ tầng test mới (không phải thay đổi nghiệp vụ)

`shared/IntegrationTestSupport/QueryCountInterceptor.cs` (đếm câu lệnh SQL qua `DbCommandInterceptor`,
chỉ đăng ký trong test host) và `tests/QueryCoverageTests` (đúng khuôn mẫu `ResilienceCoverageTests`)
là hạ tầng kiểm thử, không phải mã nghiệp vụ — bị loại khỏi tài liệu này theo đúng phạm vi, chỉ được
nhắc tên. Chi tiết: [023_Architect_*.md](../architecture/023_Architect_rà%20soát%20N%2B1%20query%20truy%20vấn%20không%20giới%20hạn%20và%20thiếu%20phân%20trang.md).

## Tóm tắt 021 → 023

| Khu vực | Bước 021 | Bước 023 |
|---|---|---|
| `GET /products` | Không phân trang, trả toàn bộ bảng | Trang mặc định 20, trần 100, envelope `PagedProductsResponse` |
| `GET /bff/products` | Forward không tham số | Forward `page`/`pageSize`, envelope thêm `Page`/`PageSize`/`TotalCount` |
| BFF → Products (render basket / add-item) | `GetProductsAsync()` toàn bộ catalog mỗi lần | `GetProductsByIdsAsync(ids)` — đúng 1 lời gọi bất kể số dòng |
| `GET /baskets/{id}` (`.Include(LineItems)`) | Eager load đúng cách, chưa rà soát bằng log truy vấn | Không đổi — khoá lại thành regression test (`QueryCountInterceptor`) |
| `orders`/`parties` | Không có endpoint danh sách | Không đổi — xác nhận lại bằng grep (T025) |

**Kết luận:** bước 023 không thêm nghiệp vụ mua hàng mới. Nó sửa đúng 1 bug thiếu phân trang
(`GET /products`) và 1 mẫu hình over-fetch xuyên service (BFF gọi Products) — khoảng hở nặng nhất mà
cuộc rà soát tìm thấy không phải N+1 kiểu EF Core cổ điển.

## Shared project trong bước 023

Bước 023 mở rộng `shared/IntegrationTestSupport` (010) với 1 file mới (`QueryCountInterceptor.cs`,
cần thêm `PackageReference Microsoft.EntityFrameworkCore.Relational`) — không tạo shared project
production mới, không service nào cần thêm `ProjectReference` production nào.
