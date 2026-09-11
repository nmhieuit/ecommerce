using Bff.Api.DownstreamClients;
using Identity;

namespace Bff.Api.Features.Products;

/// <summary>
/// The client-facing product-listing capability, whole: route mapping and response shape live
/// together in this one folder (spec SC-004, constitution's vertical-slice default).
/// Contract: <c>specs/002-gateway-bff-routing/contracts/bff-openapi.yaml</c> — <c>/bff/products</c>.
/// </summary>
/// <remarks>
/// Spec FR-005 / SC-004: aggregation and shaping only. The handler calls one downstream client and
/// renames its fields into the client-facing shape — no domain rule, no computed business value,
/// no persistence.
/// </remarks>
public static class ProductsEndpoints
{
    /// <summary>
    /// Mirrors <c>Products.Api.Features.Catalog.CatalogEndpoints.DefaultPageSize</c> only for the
    /// case where the caller omits <c>pageSize</c> entirely — the products service remains the
    /// single source of truth for the actual cap enforcement (spec FR-004; this default is just
    /// what gets forwarded downstream, not a second place the bound is decided).
    /// </summary>
    private const int DefaultPageSize = 20;

    public static WebApplication MapProductsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff");

        group.MapGet("/products", async (
                int? page,
                int? pageSize,
                ProductsApiClient products,
                CancellationToken cancellationToken) =>
            {
                var catalogPage = await products.GetProductsAsync(
                    page ?? 1, pageSize ?? DefaultPageSize, cancellationToken);

                return ToListResponse(catalogPage);
            })
            .WithName("listProducts")
            // The generated OpenAPI document is the authoritative contract (ADR-0004), and the
            // frontend's client is generated from it — so the failure responses this route really
            // produces have to be declared, or the SPA gets a client that believes 200 is the only
            // possible outcome. Inferred metadata covers the success case only.
            .Produces<ProductListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout)
            .RequireAuthorization(AuthorizationPolicies.ApiScope);

        return app;
    }

    /// <summary>
    /// The whole of the shaping step, kept as a named function so it can be unit-tested without
    /// HTTP (T061) and so the mapping is reviewable in one place rather than inline in a lambda.
    /// </summary>
    internal static ProductSummary ToSummary(ProductResource product) =>
        new(product.Id, product.Name, product.Price);

    /// <summary>
    /// Carries the downstream page metadata straight through onto <see cref="ProductListResponse"/>
    /// (data-model.md "Product List Response") — additive only, spec FR-007: <c>Items</c> keeps the
    /// exact shape it always had, so an SPA build that has not yet been updated to read
    /// Page/PageSize/TotalCount keeps working unmodified.
    /// </summary>
    internal static ProductListResponse ToListResponse(ProductPageResource page) =>
        new([.. page.Items.Select(ToSummary)], page.Page, page.PageSize, page.TotalCount);

    /// <summary>
    /// The listing envelope. Wrapping the array in an object — rather than returning a bare array —
    /// lets the response grow later (paging, totals) without becoming a breaking change for the SPA.
    /// Page/PageSize/TotalCount added by 023-audit-n1-unbounded-pagination — exactly the growth this
    /// shape was built to absorb.
    /// </summary>
    public sealed record ProductListResponse(
        IReadOnlyList<ProductSummary> Items, int Page, int PageSize, int TotalCount);

    /// <summary>A product as the client sees it (contracts/bff-openapi.yaml — ProductSummary).</summary>
    public sealed record ProductSummary(Guid Id, string Name, decimal Price);
}
