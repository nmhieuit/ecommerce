using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;
using Tenancy;

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

        // Spec FR-022: creates an order from the lines it is sent. The total is computed here and
        // is deliberately not a field of the request — see Order.PlaceFrom.
        app.MapPost("/orders", async (
            PlaceOrderRequest request,
            OrdersDbContext dbContext,
            CallerContext caller,
            CancellationToken cancellationToken) =>
        {
            // Requiring a caller before touching persistence, the same way the tenant is required.
            // An order that belongs to nobody is not an order this service should be able to hold.
            caller.RequireSubjectId();

            if (request.Items is null || request.Items.Count == 0)
            {
                return Results.BadRequest(new { error = "An order needs at least one line." });
            }

            Order order;

            try
            {
                order = Order.PlaceFrom(
                    [.. request.Items.Select(item =>
                        new OrderLine(item.ProductId, item.Quantity, item.UnitPrice))],
                    DateTime.UtcNow);
            }
            catch (ArgumentException exception)
            {
                // The domain rules reject malformed lines; surfacing them as 400 keeps a bad
                // request from reading as a server fault.
                return Results.BadRequest(new { error = exception.Message });
            }

            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = new OrderResponse(order.Id, order.PlacedAtUtc, order.Total);

            return Results.Created($"/orders/{order.Id}", response);
        });

        return app;
    }

    /// <summary>
    /// The wire shape, kept separate from the <see cref="Order"/> entity so the stored model can
    /// grow with this service's first domain story without silently widening what callers receive.
    /// </summary>
    public sealed record OrderResponse(Guid Id, DateTime PlacedAtUtc, decimal Total);

    /// <summary>
    /// What the BFF sends to place an order. Carries the lines, never a total — the total is this
    /// service's answer, not the caller's claim (004 research.md Decision 8).
    /// </summary>
    public sealed record PlaceOrderRequest(IReadOnlyList<PlaceOrderLine> Items);

    public sealed record PlaceOrderLine(Guid ProductId, int Quantity, decimal UnitPrice);
}
