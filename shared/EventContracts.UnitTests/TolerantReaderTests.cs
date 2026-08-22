using System.Text.Json;
using EventContracts;

namespace EventContracts.UnitTests;

/// <summary>
/// Proves a consumer built against version N survives a payload carrying fields it has never heard
/// of (spec FR-007, SC-003, User Story 3 Acceptance Scenario 1).
/// </summary>
/// <remarks>
/// <para>
/// Each payload below simulates a hypothetical additive future version: it is a valid <c>V1</c>
/// event plus extra top-level and nested properties. The schema itself would reject those extras
/// — <c>additionalProperties: false</c> governs what a *producer* is allowed to publish — but a
/// consumer must not. That asymmetry is the tolerant-reader pattern, and it is what lets a new
/// version roll out without every consumer being redeployed first.
/// </para>
/// <para>
/// <c>System.Text.Json</c> ignores unrecognized properties by default (ADR-0005), so these tests
/// prove behaviour nobody had to write. That is the point: "the serializer does this for free" is
/// an assumption a future options change (<c>UnmappedMemberHandling.Disallow</c>, a source-generated
/// context, a switch to another serializer) could silently break. Asserting it makes the guarantee
/// load-bearing instead of incidental.
/// </para>
/// </remarks>
public sealed class TolerantReaderTests
{
    private static readonly JsonSerializerOptions ConsumerOptions = new(JsonSerializerDefaults.General);

    [Fact]
    public void OrderPlacedV1_Deserializes_Payload_Carrying_Unknown_Fields()
    {
        const string futureVersionPayload = """
            {
              "eventId": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
              "occurredAtUtc": "2026-08-22T10:15:30Z",
              "orderId": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
              "tenantId": "acme",
              "correlationId": "0HN7A2C3D4E5F-00000001",
              "total": 59.97,
              "lines": [
                {
                  "productId": "9a1b2c3d-4e5f-6071-8293-a4b5c6d7e8f9",
                  "quantity": 2,
                  "unitPrice": 19.99,
                  "discountCode": "SPRING26"
                }
              ],
              "placedByUserId": "auth0|64f1c2a9e3b4",
              "shipping": { "method": "express", "estimatedDays": 2 }
            }
            """;

        var @event = JsonSerializer.Deserialize<OrderPlacedV1>(futureVersionPayload, ConsumerOptions);

        Assert.NotNull(@event);
        Assert.Equal(Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff"), @event.EventId);
        Assert.Equal(
            new DateTime(2026, 8, 22, 10, 15, 30, DateTimeKind.Utc),
            @event.OccurredAtUtc.ToUniversalTime());
        Assert.Equal(Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"), @event.OrderId);
        Assert.Equal("acme", @event.TenantId);
        Assert.Equal("0HN7A2C3D4E5F-00000001", @event.CorrelationId);
        Assert.Equal(59.97m, @event.Total);

        var line = Assert.Single(@event.Lines);
        Assert.Equal(Guid.Parse("9a1b2c3d-4e5f-6071-8293-a4b5c6d7e8f9"), line.ProductId);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(19.99m, line.UnitPrice);
    }

    [Fact]
    public void BasketCheckedOutV1_Deserializes_Payload_Carrying_Unknown_Fields()
    {
        const string futureVersionPayload = """
            {
              "eventId": "c56a4180-65aa-42ec-a945-5fd21dec0538",
              "occurredAtUtc": "2026-08-22T10:15:29Z",
              "basketId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
              "customerRef": "auth0|64f1c2a9e3b4",
              "tenantId": "acme",
              "correlationId": "0HN7A2C3D4E5F-00000001",
              "items": [
                {
                  "productId": "9a1b2c3d-4e5f-6071-8293-a4b5c6d7e8f9",
                  "quantity": 2,
                  "unitPrice": 19.99,
                  "lineTotal": 39.98,
                  "reservedStockId": "b3f1e0d2-1111-2222-3333-444455556666"
                }
              ],
              "total": 39.98,
              "checkoutChannel": "web",
              "promotions": [ { "code": "SPRING26", "amount": 0.00 } ]
            }
            """;

        var @event = JsonSerializer.Deserialize<BasketCheckedOutV1>(futureVersionPayload, ConsumerOptions);

        Assert.NotNull(@event);
        Assert.Equal(Guid.Parse("c56a4180-65aa-42ec-a945-5fd21dec0538"), @event.EventId);
        Assert.Equal(
            new DateTime(2026, 8, 22, 10, 15, 29, DateTimeKind.Utc),
            @event.OccurredAtUtc.ToUniversalTime());
        Assert.Equal(Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"), @event.BasketId);
        Assert.Equal("auth0|64f1c2a9e3b4", @event.CustomerRef);
        Assert.Equal("acme", @event.TenantId);
        Assert.Equal("0HN7A2C3D4E5F-00000001", @event.CorrelationId);
        Assert.Equal(39.98m, @event.Total);

        var item = Assert.Single(@event.Items);
        Assert.Equal(Guid.Parse("9a1b2c3d-4e5f-6071-8293-a4b5c6d7e8f9"), item.ProductId);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(19.99m, item.UnitPrice);
        Assert.Equal(39.98m, item.LineTotal);
    }
}
