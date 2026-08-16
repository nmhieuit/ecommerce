using Bff.Api.DownstreamClients;

namespace Bff.Api.Features.Checkout;

/// <summary>
/// The client-facing checkout capability, whole: route mapping and response shape live together in
/// this one folder (constitution's vertical-slice default).
/// Contract: <c>specs/004-minimal-shopping-spa/contracts/bff-openapi.yaml</c> — <c>/bff/checkout</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the one route in the BFF that spans a workflow rather than a single read: read the
/// caller's basket, place an order for its lines, then empty the basket. Principle IV would have
/// this modelled as a saga with explicit compensation; no messaging infrastructure exists yet, so
/// it is a documented, time-bound deviation closed by SCRUM-18/SCRUM-31 (plan.md, Complexity
/// Tracking; research.md Decision 9).
/// </para>
/// <para>
/// The order is created <em>before</em> the basket is cleared, deliberately. A failure between the
/// two leaves the shopper with a real order they can be shown, which is recoverable; clearing first
/// and then failing to order would lose their basket with nothing to show for it.
/// </para>
/// <para>
/// Still no arithmetic here. The lines come from the baskets service and the total comes back from
/// the orders service (spec 002 FR-005).
/// </para>
/// </remarks>
public static partial class CheckoutEndpoints
{
    /// <summary>
    /// Source-generated rather than a direct <c>LogWarning</c> call: the message template is
    /// compiled once instead of being parsed and boxed on every invocation (CA1848). It also gives
    /// the one gap the missing compensation leaves a single, greppable name.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Order {OrderId} was created but the basket reported nothing to clear.")]
    private static partial void LogBasketNotCleared(ILogger logger, Guid orderId);

    public static WebApplication MapCheckoutEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff");

        group.MapPost("/checkout", async (
                BasketsApiClient baskets,
                OrdersApiClient orders,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            {
                var basket = await baskets.GetCurrentBasketAsync(cancellationToken);

                // Spec FR-008's server-side half. The storefront blocks this before it is sent, but
                // client-side validation is UX only (constitution Principle VI).
                if (basket.Items.Count == 0)
                {
                    return Results.Problem(
                        title: "Basket is empty",
                        detail: "There is nothing in your basket to check out.",
                        statusCode: StatusCodes.Status409Conflict);
                }

                var order = await orders.PlaceOrderAsync(
                    new PlaceOrderCommand(
                        [.. basket.Items.Select(line =>
                            new PlaceOrderLine(line.ProductId, line.Quantity, line.UnitPrice))]),
                    cancellationToken);

                var cleared = await baskets.ClearCurrentBasketAsync(cancellationToken);

                if (!cleared)
                {
                    // The order exists and the shopper is about to be shown it, so this is not a
                    // failure to report to them — but a basket that would not clear is exactly the
                    // gap the missing compensation leaves, and it must not pass silently.
                    LogBasketNotCleared(loggerFactory.CreateLogger(typeof(CheckoutEndpoints)), order.Id);
                }

                return Results.Created(
                    $"/bff/orders/{order.Id}",
                    new OrderConfirmationResponse(order.Id, order.PlacedAtUtc, order.Total));
            })
            .WithName("checkout")
            .Produces<OrderConfirmationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return app;
    }

    /// <summary>
    /// What the shopper is shown once the order exists. The identifier is the order's own, shown
    /// verbatim — there is no separate, friendlier order number (spec FR-009, Clarifications
    /// 2026-08-16).
    /// </summary>
    public sealed record OrderConfirmationResponse(Guid Id, DateTime PlacedAtUtc, decimal Total);
}
