using Bff.Api.DownstreamClients;

namespace Bff.Api.Features.Baskets;

/// <summary>
/// The client-facing basket capability, whole: route mapping and response shape live together in
/// this one folder (spec SC-004, constitution's vertical-slice default).
/// Contract: <c>specs/002-gateway-bff-routing/contracts/bff-openapi.yaml</c> —
/// <c>/bff/baskets/{basketId}</c>.
/// </summary>
/// <remarks>Spec FR-005: aggregation and shaping only — no domain rules.</remarks>
public static class BasketsEndpoints
{
    public static WebApplication MapBasketsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff");

        group.MapGet("/baskets/{basketId:guid}", async (
            Guid basketId,
            BasketsApiClient baskets,
            CancellationToken cancellationToken) =>
        {
            var basket = await baskets.GetBasketAsync(basketId, cancellationToken);

            // A downstream 404 stays a 404. It means the basket does not exist, which is a correct
            // answer the SPA must be able to act on — not a downstream failure (that is US3's 502).
            return basket is null
                ? Results.NotFound()
                : Results.Ok(ToResponse(basket));
        })
            .WithName("getBasket")
            // See ProductsEndpoints for why the failure responses are declared explicitly: the
            // generated document is the contract the frontend's client is built from.
            .Produces<BasketResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return app;
    }

    internal static BasketResponse ToResponse(BasketResource basket) =>
        new(basket.Id, basket.CustomerId);

    /// <summary>
    /// A basket as the client sees it. contracts/bff-openapi.yaml marks this shape "to be
    /// finalized" — it is a genuine proxy of what the baskets service exposes today, and gains
    /// line items and totals when that service's own domain story adds them.
    /// </summary>
    public sealed record BasketResponse(Guid Id, Guid CustomerId);
}
