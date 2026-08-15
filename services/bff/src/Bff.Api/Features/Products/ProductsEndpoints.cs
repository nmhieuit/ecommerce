using Bff.Api.DownstreamClients;

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
    public static WebApplication MapProductsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff");

        group.MapGet("/products", async (
                ProductsApiClient products,
                CancellationToken cancellationToken) =>
            {
                var catalog = await products.GetProductsAsync(cancellationToken);

                return new ProductListResponse([.. catalog.Select(ToSummary)]);
            })
            .WithName("listProducts")
            // The generated OpenAPI document is the authoritative contract (ADR-0004), and the
            // frontend's client is generated from it — so the failure responses this route really
            // produces have to be declared, or the SPA gets a client that believes 200 is the only
            // possible outcome. Inferred metadata covers the success case only.
            .Produces<ProductListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return app;
    }

    /// <summary>
    /// The whole of the shaping step, kept as a named function so it can be unit-tested without
    /// HTTP (T061) and so the mapping is reviewable in one place rather than inline in a lambda.
    /// </summary>
    internal static ProductSummary ToSummary(ProductResource product) =>
        new(product.Id, product.Name, product.Price);

    /// <summary>
    /// The listing envelope. Wrapping the array in an object — rather than returning a bare array —
    /// lets the response grow later (paging, totals) without becoming a breaking change for the SPA.
    /// </summary>
    public sealed record ProductListResponse(IReadOnlyList<ProductSummary> Items);

    /// <summary>A product as the client sees it (contracts/bff-openapi.yaml — ProductSummary).</summary>
    public sealed record ProductSummary(Guid Id, string Name, decimal Price);
}
