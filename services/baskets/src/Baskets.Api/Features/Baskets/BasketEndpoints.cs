using Baskets.Api.Data;
using Identity;
using Microsoft.EntityFrameworkCore;
using Tenancy;

namespace Baskets.Api.Features.Baskets;

/// <summary>
/// The basket capability, whole: route mapping and response shapes live together in this one folder
/// rather than being split across technical-layer folders (constitution's vertical-slice default).
/// Contract: <c>specs/004-minimal-shopping-spa/contracts/downstream-openapi.yaml</c>.
/// </summary>
/// <remarks>
/// "Current" means the caller's own basket, resolved from the <c>X-Subject-Id</c> the gateway
/// stamped — never from a path or query value the caller could change (spec FR-006). That is why
/// there is no route to create a basket and no route to name someone else's.
/// </remarks>
public static class BasketEndpoints
{
    public static WebApplication MapBasketEndpoints(this WebApplication app)
    {
        // A caller who has never added anything gets an empty basket, not a 404: "nothing in it
        // yet" is a legitimate state, and making it an error would force the storefront to treat a
        // first visit as a failure.
        app.MapGet("/baskets/current", async (
            BasketsDbContext dbContext,
            CallerContext caller,
            CancellationToken cancellationToken) =>
        {
            var basket = await FindOrCreateCurrentAsync(dbContext, caller, cancellationToken);

            return Results.Ok(ToResponse(basket));
        })
            .RequireAuthorization(AuthorizationPolicies.ApiScope);

        app.MapPost("/baskets/current/items", async (
            AddBasketItemRequest request,
            BasketsDbContext dbContext,
            CallerContext caller,
            CancellationToken cancellationToken) =>
        {
            // Validated here as well as at the edge: client-side validation is UX only, and this
            // service is reachable by anything inside the mesh (constitution Principle VI).
            if (request.Quantity < 1)
            {
                return Results.BadRequest(new { error = "Quantity must be at least 1." });
            }

            if (request.UnitPrice < 0m)
            {
                return Results.BadRequest(new { error = "Unit price cannot be negative." });
            }

            var basket = await FindOrCreateCurrentAsync(dbContext, caller, cancellationToken);

            basket.AddItem(request.ProductId, request.Quantity, request.UnitPrice);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(ToResponse(basket));
        })
            .RequireAuthorization(AuthorizationPolicies.ApiScope);

        app.MapPost("/baskets/current/clear", async (
            BasketsDbContext dbContext,
            CallerContext caller,
            CancellationToken cancellationToken) =>
        {
            var basket = await FindOrCreateCurrentAsync(dbContext, caller, cancellationToken);

            // Reported rather than silently succeeding, so a checkout of an empty basket cannot
            // reach the orders service (spec FR-008).
            if (basket.LineItems.Count == 0)
            {
                return Results.Conflict(new { error = "The basket is already empty." });
            }

            basket.Clear();
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
            .RequireAuthorization(AuthorizationPolicies.ApiScope);

        // Kept from 002: the read-by-id the BFF's original basket route proxies. Its shape now
        // carries line items and a total like every other basket response.
        app.MapGet("/baskets/{basketId:guid}", async (
            Guid basketId,
            BasketsDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var basket = await dbContext.Baskets
                .AsNoTracking()
                .Include(entity => entity.LineItems)
                .SingleOrDefaultAsync(entity => entity.Id == basketId, cancellationToken);

            return basket is null ? Results.NotFound() : Results.Ok(ToResponse(basket));
        })
            .RequireAuthorization(AuthorizationPolicies.ApiScope);

        return app;
    }

    /// <summary>
    /// The caller's basket, created empty the first time they are seen.
    /// </summary>
    /// <remarks>
    /// <see cref="CallerContext.RequireSubjectId"/> throws when no caller was resolved, which is
    /// the intended outcome: a request that did not come through the gateway must not be given
    /// somebody's basket, and there is no default caller to fall back to.
    /// </remarks>
    private static async Task<Basket> FindOrCreateCurrentAsync(
        BasketsDbContext dbContext,
        CallerContext caller,
        CancellationToken cancellationToken)
    {
        var customerRef = caller.RequireSubjectId();

        var basket = await dbContext.Baskets
            .Include(entity => entity.LineItems)
            .SingleOrDefaultAsync(entity => entity.CustomerRef == customerRef, cancellationToken);

        if (basket is not null)
        {
            return basket;
        }

        basket = Basket.ForCustomer(customerRef);
        dbContext.Baskets.Add(basket);
        await dbContext.SaveChangesAsync(cancellationToken);

        return basket;
    }

    /// <summary>
    /// The wire shape, kept separate from the entities so the stored model can grow without
    /// silently widening what callers receive.
    /// </summary>
    internal static BasketResponse ToResponse(Basket basket) =>
        new(
            basket.Id,
            basket.CustomerRef,
            [.. basket.LineItems
                .OrderBy(line => line.ProductId)
                .Select(line => new BasketLineItemResponse(
                    line.ProductId,
                    line.Quantity,
                    line.UnitPrice,
                    line.LineTotal))],
            basket.Total);

    public sealed record BasketResponse(
        Guid Id,
        string CustomerRef,
        IReadOnlyList<BasketLineItemResponse> Items,
        decimal Total);

    /// <summary>
    /// <c>LineTotal</c> is included rather than left for the caller to work out. It is money
    /// arithmetic, and money arithmetic belongs in the service that owns the data — the BFF above
    /// is an aggregation layer and must not be multiplying anything (004 plan.md, post-design
    /// re-check).
    /// </summary>
    public sealed record BasketLineItemResponse(
        Guid ProductId,
        int Quantity,
        decimal UnitPrice,
        decimal LineTotal);

    public sealed record AddBasketItemRequest(Guid ProductId, int Quantity, decimal UnitPrice);
}
