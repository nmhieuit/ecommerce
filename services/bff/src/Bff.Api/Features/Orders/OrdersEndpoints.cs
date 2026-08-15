using Bff.Api.DownstreamClients;

namespace Bff.Api.Features.Orders;

/// <summary>
/// The client-facing order capability, whole: route mapping and response shape live together in
/// this one folder (spec SC-004, constitution's vertical-slice default).
/// Contract: <c>specs/002-gateway-bff-routing/contracts/bff-openapi.yaml</c> —
/// <c>/bff/orders/{orderId}</c>.
/// </summary>
/// <remarks>Spec FR-005: aggregation and shaping only — no domain rules.</remarks>
public static class OrdersEndpoints
{
    public static WebApplication MapOrdersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff");

        group.MapGet("/orders/{orderId:guid}", async (
            Guid orderId,
            OrdersApiClient orders,
            CancellationToken cancellationToken) =>
        {
            var order = await orders.GetOrderAsync(orderId, cancellationToken);

            return order is null
                ? Results.NotFound()
                : Results.Ok(ToResponse(order));
        });

        return app;
    }

    internal static OrderResponse ToResponse(OrderResource order) =>
        new(order.Id, order.PlacedAtUtc, order.Total);

    /// <summary>
    /// An order as the client sees it. Shape marked "to be finalized" in the contract; it gains
    /// line items and status when the orders service's own domain story adds them.
    /// </summary>
    public sealed record OrderResponse(Guid Id, DateTime PlacedAtUtc, decimal Total);
}
