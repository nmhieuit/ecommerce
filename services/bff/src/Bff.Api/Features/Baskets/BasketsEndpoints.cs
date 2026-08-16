using Bff.Api.DownstreamClients;

namespace Bff.Api.Features.Baskets;

/// <summary>
/// The client-facing basket capability, whole: route mapping and response shape live together in
/// this one folder (constitution's vertical-slice default).
/// Contract: <c>specs/004-minimal-shopping-spa/contracts/bff-openapi.yaml</c>.
/// </summary>
/// <remarks>
/// Aggregation and shaping only (spec 002 FR-005). The basket total is computed by the baskets
/// service and passed through untouched; the product names are joined in from the catalog, because
/// the baskets service stores identifiers and the shopper needs words. Nothing here multiplies or
/// sums anything.
/// </remarks>
public static class BasketsEndpoints
{
    public static WebApplication MapBasketsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff");

        group.MapGet("/basket", async (
                BasketsApiClient baskets,
                ProductsApiClient products,
                CancellationToken cancellationToken) =>
            {
                var basket = await baskets.GetCurrentBasketAsync(cancellationToken);

                return Results.Ok(await ToResponseAsync(basket, products, cancellationToken));
            })
            .WithName("getCurrentBasket")
            .Produces<BasketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        group.MapPost("/basket/items", async (
                AddBasketItemRequest request,
                BasketsApiClient baskets,
                ProductsApiClient products,
                CancellationToken cancellationToken) =>
            {
                if (request.Quantity < 1)
                {
                    return Results.Problem(
                        title: "Invalid quantity",
                        detail: "Quantity must be at least 1.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                // The price is resolved here, from the catalog, and never taken from the request —
                // a client-supplied price is a client-supplied discount. The request record has no
                // price field at all, so there is nothing to accidentally trust.
                var catalog = await products.GetProductsAsync(cancellationToken);
                var product = catalog.SingleOrDefault(entry => entry.Id == request.ProductId);

                if (product is null)
                {
                    return Results.Problem(
                        title: "No such product",
                        detail: "That product is not in the catalog.",
                        statusCode: StatusCodes.Status404NotFound);
                }

                var basket = await baskets.AddItemAsync(
                    new AddBasketItemCommand(product.Id, request.Quantity, product.Price),
                    cancellationToken);

                return Results.Ok(await ToResponseAsync(basket, products, cancellationToken));
            })
            .WithName("addBasketItem")
            .Produces<BasketResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        // Kept from 002: the read-by-id route. Its shape now carries line items and a total.
        group.MapGet("/baskets/{basketId:guid}", async (
                Guid basketId,
                BasketsApiClient baskets,
                ProductsApiClient products,
                CancellationToken cancellationToken) =>
            {
                var basket = await baskets.GetBasketAsync(basketId, cancellationToken);

                // A downstream 404 stays a 404. It means the basket does not exist, which is a
                // correct answer the SPA must be able to act on — not a downstream failure.
                return basket is null
                    ? Results.NotFound()
                    : Results.Ok(await ToResponseAsync(basket, products, cancellationToken));
            })
            .WithName("getBasket")
            .Produces<BasketResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return app;
    }

    /// <summary>
    /// Joins the catalog's product names onto the basket's lines.
    /// </summary>
    /// <remarks>
    /// The catalog call is skipped entirely for an empty basket — there is nothing to name, and a
    /// first-time shopper's basket view should not depend on the products service being up.
    /// </remarks>
    private static async Task<BasketResponse> ToResponseAsync(
        BasketResource basket,
        ProductsApiClient products,
        CancellationToken cancellationToken)
    {
        if (basket.Items.Count == 0)
        {
            return new BasketResponse(basket.Id, basket.CustomerRef, [], basket.Total);
        }

        var namesById = (await products.GetProductsAsync(cancellationToken))
            .ToDictionary(product => product.Id, product => product.Name);

        return new BasketResponse(
            basket.Id,
            basket.CustomerRef,
            [.. basket.Items.Select(line => ToItem(line, namesById))],
            basket.Total);
    }

    /// <summary>
    /// The whole of the per-line shaping, named so it can be unit-tested without HTTP.
    /// </summary>
    /// <remarks>
    /// A line whose product has since left the catalog keeps its place rather than vanishing: the
    /// shopper chose it and is being charged for it, so hiding it would misrepresent the total.
    /// </remarks>
    internal static BasketItem ToItem(
        BasketLineItemResource line,
        IReadOnlyDictionary<Guid, string> namesById) =>
        new(
            line.ProductId,
            namesById.TryGetValue(line.ProductId, out var name) ? name : "Unavailable product",
            line.Quantity,
            line.UnitPrice,
            // Passed through, not recomputed: the baskets service owns the arithmetic.
            line.LineTotal);

    /// <summary>A basket as the client sees it (contracts/bff-openapi.yaml — BasketResponse).</summary>
    public sealed record BasketResponse(
        Guid Id,
        string CustomerRef,
        IReadOnlyList<BasketItem> Items,
        decimal Total);

    /// <summary>One line of the basket, as the client sees it.</summary>
    public sealed record BasketItem(
        Guid ProductId,
        string Name,
        int Quantity,
        decimal UnitPrice,
        decimal LineTotal);

    /// <summary>
    /// What the client may say when adding to the basket: which product, and how many. Deliberately
    /// no price field — see the route.
    /// </summary>
    public sealed record AddBasketItemRequest(Guid ProductId, int Quantity);
}
