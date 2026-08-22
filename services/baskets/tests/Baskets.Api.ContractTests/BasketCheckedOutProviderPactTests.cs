using Baskets.Api.Data;
using Baskets.Api.Features.Checkout;
using PactNet.Verifier;
using Xunit.Abstractions;

namespace Baskets.Api.ContractTests;

/// <summary>
/// This service's own build, verifying the <c>BasketCheckedOut</c> payload it would publish against
/// what the orders service says it will rely on (<c>pacts/orders-basketcheckedout.json</c>).
/// A change to what checkout constructs fails here, in the baskets build
/// (011-consumer-contract-tests FR-004, FR-005; spec SC-002).
/// </summary>
/// <remarks>
/// <para>
/// No broker, no MassTransit, no HTTP endpoint: a message pact verifies a payload, so the real
/// <see cref="BasketCheckedOutMapper.ToEvent"/> is called directly and its result handed to the
/// verifier (research.md Decision 3). When SCRUM-31 wires the outbox and the publisher, this test
/// keeps working unchanged — what it checks is the shape, not the delivery.
/// </para>
/// <para>
/// The basket is built through <see cref="Basket.ForCustomer"/> and <see cref="Basket.AddItem"/>
/// rather than by setting properties, so the payload is derived from a basket this service's own
/// domain rules produced — including the total, which is computed from the lines rather than
/// stored.
/// </para>
/// </remarks>
public class BasketCheckedOutProviderPactTests(ITestOutputHelper output)
{
    private static readonly Guid ProductId = new("9f8d6b1e-0001-4000-8000-000000000001");

    [Fact]
    public void CheckedOutPayload_SatisfiesTheOrdersServicesRecordedExpectations()
    {
        using var verifier = new PactVerifier(
            "basketcheckedout",
            new PactVerifierConfig { Outputters = [new PactTestOutput(output)] });

        verifier
            // A placeholder, and two things about it are load-bearing. It comes first because
            // PactNet initialises the provider from whichever transport is registered first, and
            // registering messages first stamps the base URL with a "message://" scheme the native
            // verifier cannot build a request from. Its host is localhost because the verifier
            // combines this host with the messaging transport's port, and PactNet's own message
            // listener binds the localhost prefix — 127.0.0.1 reaches the socket but not the
            // prefix, and comes back as a bare 400. Nothing is ever sent to port 1: this pact
            // holds messages and no HTTP interactions.
            .WithHttpEndpoint(new Uri("http://localhost:1"))
            .WithMessages(scenarios => scenarios.Add(
                "a basket checked out",
                CheckOutABasketHoldingOneItem))
            .WithFileSource(new FileInfo(Path.Combine(PactPaths.Directory, "orders-basketcheckedout.json")))
            .Verify();
    }

    /// <summary>
    /// The payload a checkout of a basket holding one item would publish — produced by the real
    /// mapper, never hand-written, which is what FR-006 means by verifying against real producer
    /// behaviour rather than a maintained double.
    /// </summary>
    private static object CheckOutABasketHoldingOneItem()
    {
        var basket = Basket.ForCustomer("pact-shopper");
        basket.AddItem(ProductId, quantity: 2, unitPrice: 12.50m);

        return BasketCheckedOutMapper.ToEvent(
            basket,
            tenantId: "contoso",
            correlationId: "0f9c3b2a4e5d6f7081920a1b2c3d4e5f",
            eventId: new Guid("4d1b9e77-0004-4000-8000-000000000001"),
            occurredAtUtc: new DateTime(2026, 8, 22, 10, 15, 30, DateTimeKind.Utc));
    }
}
