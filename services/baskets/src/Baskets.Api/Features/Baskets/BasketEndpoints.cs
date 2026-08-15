using Baskets.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Baskets.Api.Features.Baskets;

/// <summary>
/// The basket read capability, whole: route mapping and response shape live together in this one
/// folder rather than being split across technical-layer folders (spec SC-004, constitution's
/// vertical-slice default).
/// Contract: <c>specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml</c>.
/// </summary>
public static class BasketEndpoints
{
    public static WebApplication MapBasketEndpoints(this WebApplication app)
    {
        // 404 rather than an empty 200 for an unknown id: the BFF must be able to tell "no such
        // basket" from "a basket that happens to be empty".
        app.MapGet("/baskets/{basketId:guid}", async (
            Guid basketId,
            BasketsDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var basket = await dbContext.Baskets
                .AsNoTracking()
                .Where(entity => entity.Id == basketId)
                .Select(entity => new BasketResponse(entity.Id, entity.CustomerId))
                .SingleOrDefaultAsync(cancellationToken);

            return basket is null ? Results.NotFound() : Results.Ok(basket);
        });

        return app;
    }

    /// <summary>
    /// The wire shape, kept separate from the <see cref="Basket"/> entity so the stored model can
    /// grow with this service's first domain story without silently widening what callers receive.
    /// </summary>
    public sealed record BasketResponse(Guid Id, Guid CustomerId);
}
