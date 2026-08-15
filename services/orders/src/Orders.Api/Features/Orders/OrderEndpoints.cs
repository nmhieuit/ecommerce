using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;

namespace Orders.Api.Features.Orders;

/// <summary>
/// The order read capability, whole: route mapping and response shape live together in this one
/// folder rather than being split across technical-layer folders (spec SC-004, constitution's
/// vertical-slice default).
/// Contract: <c>specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml</c>.
/// </summary>
public static class OrderEndpoints
{
    public static WebApplication MapOrderEndpoints(this WebApplication app)
    {
        app.MapGet("/orders/{orderId:guid}", async (
            Guid orderId,
            OrdersDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var order = await dbContext.Orders
                .AsNoTracking()
                .Where(entity => entity.Id == orderId)
                .Select(entity => new OrderResponse(entity.Id, entity.PlacedAtUtc, entity.Total))
                .SingleOrDefaultAsync(cancellationToken);

            return order is null ? Results.NotFound() : Results.Ok(order);
        });

        return app;
    }

    /// <summary>
    /// The wire shape, kept separate from the <see cref="Order"/> entity so the stored model can
    /// grow with this service's first domain story without silently widening what callers receive.
    /// </summary>
    public sealed record OrderResponse(Guid Id, DateTime PlacedAtUtc, decimal Total);
}
