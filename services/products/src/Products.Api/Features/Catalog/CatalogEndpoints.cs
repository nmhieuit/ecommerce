using Identity;
using Microsoft.EntityFrameworkCore;
using Products.Api.Data;

namespace Products.Api.Features.Catalog;

/// <summary>
/// The catalog read capability, whole: route mapping and response shape live together in this one
/// folder rather than being split across technical-layer folders (spec SC-004, constitution's
/// vertical-slice default).
/// Contract: <c>specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml</c> — this is the
/// surface the BFF's product-listing route proxies (spec FR-004).
/// </summary>
public static class CatalogEndpoints
{
    /// <summary>
    /// Applied when the caller supplies no <c>pageSize</c> (or an invalid one — zero or negative).
    /// specs/023-audit-n1-unbounded-pagination research.md Decision 3.
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// The server-side cap no caller-supplied <c>pageSize</c> can exceed (spec FR-004 — the bound is
    /// enforced server-side, not just a client-side hint). research.md Decision 3.
    /// </summary>
    public const int MaxPageSize = 100;

    public static WebApplication MapCatalogEndpoints(this WebApplication app)
    {
        // An empty catalog returns an empty page, never 404: "no products yet" is a legitimate
        // state, and making it an error would force every caller to treat it as a failure.
        app.MapGet("/products", async (
                int? page,
                int? pageSize,
                string? ids,
                ProductsDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var query = dbContext.Products.AsNoTracking();

                // research.md Decision 4: an explicit `ids` filter is bounded by the caller-supplied
                // id set itself, not by page/pageSize — this is what lets the BFF resolve exactly
                // the products a basket needs in one round trip instead of fetching the whole
                // catalog (spec FR-002, User Story 2).
                if (!string.IsNullOrWhiteSpace(ids))
                {
                    var requestedIds = ids
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(id => Guid.TryParse(id, out var parsed) ? parsed : (Guid?)null)
                        .Where(id => id.HasValue)
                        .Select(id => id!.Value)
                        .ToArray();

                    var matched = await query
                        .Where(product => requestedIds.Contains(product.Id))
                        .OrderBy(product => product.Name)
                        .Select(product => new ProductResponse(product.Id, product.Name, product.Price))
                        .ToListAsync(cancellationToken);

                    return new PagedProductsResponse(matched, Page: 1, PageSize: matched.Count, TotalCount: matched.Count);
                }

                var effectivePageSize = Math.Clamp(
                    pageSize is > 0 ? pageSize.Value : DefaultPageSize,
                    1,
                    MaxPageSize);
                var effectivePage = page is > 0 ? page.Value : 1;

                var totalCount = await query.CountAsync(cancellationToken);

                var items = await query
                    .OrderBy(product => product.Name)
                    .Skip((effectivePage - 1) * effectivePageSize)
                    .Take(effectivePageSize)
                    .Select(product => new ProductResponse(product.Id, product.Name, product.Price))
                    .ToListAsync(cancellationToken);

                return new PagedProductsResponse(items, effectivePage, effectivePageSize, totalCount);
            })
            .RequireAuthorization(AuthorizationPolicies.ApiScope);

        return app;
    }

    /// <summary>
    /// The wire shape, kept separate from the <see cref="Product"/> entity so the stored model can
    /// grow with this service's first domain story without silently widening what the BFF and the
    /// SPA behind it receive.
    /// </summary>
    public sealed record ProductResponse(Guid Id, string Name, decimal Price);

    /// <summary>
    /// A page of the catalog (or, when <c>ids</c> was supplied, the exact matching set).
    /// specs/023-audit-n1-unbounded-pagination data-model.md — "Paged Products Response".
    /// </summary>
    /// <param name="TotalCount">
    /// Total records matching the query (full catalog count, or the count of ids that matched) —
    /// NOT <c>Items.Count</c>. Lets a caller determine whether another page exists.
    /// </param>
    public sealed record PagedProductsResponse(
        IReadOnlyList<ProductResponse> Items, int Page, int PageSize, int TotalCount);
}
