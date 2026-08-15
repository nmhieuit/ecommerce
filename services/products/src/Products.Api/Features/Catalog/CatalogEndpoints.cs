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
    public static WebApplication MapCatalogEndpoints(this WebApplication app)
    {
        // An empty catalog returns an empty array, never 404: "no products yet" is a legitimate
        // state, and making it an error would force every caller to treat it as a failure.
        app.MapGet("/products", async (ProductsDbContext dbContext, CancellationToken cancellationToken) =>
            await dbContext.Products
                .AsNoTracking()
                .OrderBy(product => product.Name)
                .Select(product => new ProductResponse(product.Id, product.Name, product.Price))
                .ToListAsync(cancellationToken));

        return app;
    }

    /// <summary>
    /// The wire shape, kept separate from the <see cref="Product"/> entity so the stored model can
    /// grow with this service's first domain story without silently widening what the BFF and the
    /// SPA behind it receive.
    /// </summary>
    public sealed record ProductResponse(Guid Id, string Name, decimal Price);
}
