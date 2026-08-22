using EventContracts;
using PactNet;
using PactNet.Matchers;

namespace Orders.Api.ContractTests;

/// <summary>
/// What this service will rely on from a <c>BasketCheckedOut</c> message, stated as a message Pact
/// document (<c>pacts/orders-basketcheckedout.json</c>) for the baskets service's own build to
/// verify (011-consumer-contract-tests FR-004; ADR-0006 Action Item 3).
/// </summary>
/// <remarks>
/// <para>
/// No broker and no MassTransit. Nothing publishes or consumes <c>BasketCheckedOut</c> yet —
/// checkout is synchronous BFF orchestration today (ADR-0011) — and a message pact verifies a
/// payload rather than a delivery, so the transport that will eventually carry it is not needed to
/// pin its shape down now (research.md Decisions 3 and 4). Wiring the outbox and publisher is
/// SCRUM-31's work; this exists so that work starts against a contract instead of defining one.
/// </para>
/// <para>
/// The provider participant is the event rather than the service that will publish it, so this
/// pact stays scoped to one message: <c>baskets</c> will publish others, and they are not this
/// consumer's business.
/// </para>
/// </remarks>
public class BasketCheckedOutConsumerPactTests
{
    private static readonly Guid ProductId = new("9f8d6b1e-0001-4000-8000-000000000001");

    [Fact]
    public async Task BasketCheckedOut_DependsOnTheIdentifiersTenantLinesAndTotal()
    {
        var pact = Pact.V3("orders", "basketcheckedout", new PactConfig { PactDir = PactPaths.Directory })
            .WithMessageInteractions();

        await pact
            .ExpectsToReceive("a basket checked out")
            .Given("a basket holding one item was checked out")
            .WithJsonContent(new
            {
                // Carried so a redelivered message can be discarded rather than ordered twice.
                eventId = Match.Regex("4d1b9e77-0004-4000-8000-000000000001", PactRegex.Uuid),
                occurredAtUtc = Match.Regex("2026-08-22T10:15:30.1234567Z", PactRegex.Iso8601DateTime),
                basketId = Match.Regex("2b7c1f5a-0002-4000-8000-000000000001", PactRegex.Uuid),
                customerRef = Match.Type("pact-shopper"),
                // Required even though no basket HTTP response surfaces it: an order this service
                // creates from this message has to be attributable to a tenant, and there is
                // nowhere else in an asynchronous flow for that to come from
                // (constitution Principle V).
                tenantId = Match.Type("contoso"),
                correlationId = Match.Type("0f9c3b2a4e5d6f7081920a1b2c3d4e5f"),
                items = Match.MinType(
                    new
                    {
                        productId = Match.Regex(ProductId.ToString(), PactRegex.Uuid),
                        quantity = Match.Integer(2),
                        unitPrice = Match.Number(12.50m),
                        // Money arithmetic stays in the service that owns the basket; this service
                        // reads the line total rather than multiplying it out again.
                        lineTotal = Match.Number(25.00m),
                    },
                    1),
                total = Match.Number(25.00m),
            })
            .VerifyAsync<BasketCheckedOutV1>(message =>
            {
                // Deserialised into the published contract type, which is what proves the shape
                // recorded above is one this service could actually consume.
                Assert.NotEqual(Guid.Empty, message.EventId);
                Assert.NotEqual(Guid.Empty, message.BasketId);
                Assert.False(string.IsNullOrWhiteSpace(message.CustomerRef));
                Assert.False(string.IsNullOrWhiteSpace(message.TenantId));
                Assert.False(string.IsNullOrWhiteSpace(message.CorrelationId));

                var line = Assert.Single(message.Items);
                Assert.Equal(ProductId, line.ProductId);
                Assert.Equal(25.00m, line.LineTotal);
                Assert.Equal(25.00m, message.Total);

                return Task.CompletedTask;
            });
    }
}
