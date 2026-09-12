using System.Net.Http.Json;

namespace Bff.Api.DownstreamClients;

/// <summary>
/// Calls the products service's catalog read surface
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml).
/// </summary>
/// <remarks>
/// The client speaks the downstream's shape, not the client-facing one. Translating
/// <see cref="ProductResource"/> into the SPA-facing summary is the route group's job, which keeps
/// a downstream field rename from silently reaching the SPA.
/// </remarks>
public sealed class ProductsApiClient(HttpClient httpClient)
{
    /// <summary>The logical service name, matching this client's configuration section.</summary>
    public const string ServiceName = "ProductsApi";

    /// <summary>
    /// Lists a page of the catalog. An empty catalog is a legitimate answer, so this returns an
    /// empty page rather than treating "no products" as a failure.
    /// specs/023-audit-n1-unbounded-pagination spec FR-001/FR-004: the products service — not this
    /// client — is the single source of truth for the default page size and the server-side cap;
    /// this method only forwards what the caller asked for.
    /// </summary>
    public Task<ProductPageResource> GetProductsAsync(
        int page, int pageSize, CancellationToken cancellationToken) =>
        DownstreamCall.ExecuteAsync(ServiceName, async () =>
        {
            var result = await httpClient.GetFromJsonAsync<ProductPageResource>(
                $"/products?page={page}&pageSize={pageSize}",
                cancellationToken);

            return result ?? new ProductPageResource([], page, pageSize, TotalCount: 0);
        });

    /// <summary>
    /// Resolves exactly the products named by <paramref name="ids"/> in one downstream call —
    /// regardless of how many ids are requested — instead of fetching the whole catalog to search
    /// it client-side (research.md Decision 4; spec FR-002/FR-003, User Story 2). The BFF's basket
    /// rendering and add-item routes are the reason this exists: a basket with N distinct products
    /// must still resolve names/prices with exactly one call to this method, not N.
    /// </summary>
    public Task<IReadOnlyList<ProductResource>> GetProductsByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            // Nothing to resolve — and, just as importantly, nothing to call. A basket with no
            // lines must not touch the products service at all.
            return Task.FromResult<IReadOnlyList<ProductResource>>([]);
        }

        return DownstreamCall.ExecuteAsync(ServiceName, async () =>
        {
            var query = string.Join(',', ids);
            var result = await httpClient.GetFromJsonAsync<ProductPageResource>(
                $"/products?ids={query}",
                cancellationToken);

            return result?.Items ?? (IReadOnlyList<ProductResource>)[];
        });
    }
}

/// <summary>A product exactly as the products service returns it.</summary>
public sealed record ProductResource(Guid Id, string Name, decimal Price);

/// <summary>
/// A page of the catalog exactly as the products service returns it (or, when <c>ids</c> was
/// supplied, the exact matching set — data-model.md "Paged Products Response").
/// </summary>
public sealed record ProductPageResource(
    IReadOnlyList<ProductResource> Items, int Page, int PageSize, int TotalCount);
