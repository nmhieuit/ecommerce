using System.Net;
using Bff.Api.DownstreamClients;
using PactNet.Matchers;

namespace Bff.Api.ContractTests;

/// <summary>
/// What the BFF relies on from the orders service, stated as a Pact document
/// (<c>pacts/bff-orders.json</c>) for the orders service's own build to verify
/// (011-consumer-contract-tests FR-001, FR-002).
/// </summary>
/// <remarks>
/// <c>tenantId</c> is deliberately absent from both expected responses even though the orders
/// service returns it. <see cref="OrderResource"/> does not read it, and a pact that named it would
/// stop the orders service dropping a field nobody consumes — the tolerant-reader rule FR-007
/// exists to prevent.
/// </remarks>
public class OrdersConsumerPactTests
{
    private const string Provider = "orders";

    private static readonly Guid ExistingOrderId = new("7c2e0d44-0003-4000-8000-000000000001");
    private static readonly Guid ProductId = new("9f8d6b1e-0001-4000-8000-000000000001");

    [Fact]
    public async Task OrderInteractions_DependOnIdPlacedAtUtcAndTotal()
    {
        var pact = BffPact.For(Provider);

        pact
            .UponReceiving("a request for an order by id")
                .Given(
                    "an order exists",
                    new Dictionary<string, string> { ["orderId"] = ExistingOrderId.ToString() })
                .WithRequest(HttpMethod.Get, $"/orders/{ExistingOrderId}")
                .WithHeader("X-Tenant-Id", BffPact.TenantId)
                .WithHeader("X-Subject-Id", BffPact.SubjectId)
            .WillRespond()
                .WithStatus(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(OrderBody());

        pact
            .UponReceiving("a request to place an order")
                .WithRequest(HttpMethod.Post, "/orders")
                .WithHeader("X-Tenant-Id", BffPact.TenantId)
                .WithHeader("X-Subject-Id", BffPact.SubjectId)
                // Lines only. No total is sent, and the pact says so: the total is the orders
                // service's answer, not the BFF's claim (004 research.md Decision 8).
                .WithJsonBody(new
                {
                    items = new[]
                    {
                        new { productId = ProductId, quantity = 2, unitPrice = 12.50m },
                    },
                })
            .WillRespond()
                // 201, because the client calls EnsureSuccessStatusCode and then reads the body —
                // a producer that switched to 202 with no body would break it.
                .WithStatus(HttpStatusCode.Created)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(OrderBody());

        await pact.VerifyAsync(async context =>
        {
            using var httpClient = BffPact.CreateRelayingClient(context.MockServerUri);
            var client = new OrdersApiClient(httpClient);

            var fetched = await client.GetOrderAsync(ExistingOrderId, CancellationToken.None);
            AssertReadable(fetched);

            var placed = await client.PlaceOrderAsync(
                new PlaceOrderCommand([new PlaceOrderLine(ProductId, Quantity: 2, UnitPrice: 12.50m)]),
                CancellationToken.None);
            AssertReadable(placed);
        });
    }

    private static object OrderBody() => new
    {
        id = Match.Regex(ExistingOrderId.ToString(), PactRegex.Uuid),
        placedAtUtc = Match.Regex("2026-08-22T10:15:30.1234567Z", PactRegex.Iso8601DateTime),
        total = Match.Number(25.00m),
    };

    private static void AssertReadable(OrderResource? order)
    {
        Assert.NotNull(order);
        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.NotEqual(default, order.PlacedAtUtc);
        Assert.Equal(25.00m, order.Total);
    }
}
