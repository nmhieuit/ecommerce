using System.Text.Json;
using EventContracts;
using Json.Schema;

namespace EventContracts.UnitTests;

/// <summary>
/// Proves that an event a publisher actually produces satisfies its published schema
/// (spec FR-009, User Story 3 Acceptance Scenario 2).
/// </summary>
/// <remarks>
/// <para>
/// The record and the schema are both hand-authored (research.md Decision 2): the schema is the
/// artifact a reviewer approves, the record is the shape code constructs. That split is only safe
/// if something proves the two still agree, which is this suite's whole job — it serializes a real
/// instance through <c>System.Text.Json</c>, the serializer MassTransit will use in production per
/// ADR-0005, and validates the resulting JSON against the committed schema.
/// </para>
/// <para>
/// <c>format</c> assertions are switched on deliberately. JSON Schema treats <c>format</c> as an
/// annotation by default, which would let a <c>uuid</c> or <c>date-time</c> field serialize into
/// something unusable and still pass — exactly the drift this suite exists to catch.
/// </para>
/// </remarks>
public sealed class SchemaValidationTests
{
    /// <summary>
    /// Serializer options a publisher would use. No naming policy is set on purpose: the records
    /// pin their JSON property names with <c>[JsonPropertyName]</c>, so the wire format holds even
    /// for a publisher that never configures one.
    /// </summary>
    private static readonly JsonSerializerOptions PublisherOptions = new(JsonSerializerDefaults.General);

    private static readonly EvaluationOptions StrictEvaluation = new()
    {
        OutputFormat = OutputFormat.Hierarchical,
        RequireFormatValidation = true,
    };

    [Fact]
    public void Serialized_OrderPlacedV1_Validates_Against_Its_Published_Schema()
    {
        var @event = new OrderPlacedV1(
            EventId: Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff"),
            OccurredAtUtc: new DateTime(2026, 8, 22, 10, 15, 30, DateTimeKind.Utc),
            OrderId: Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"),
            TenantId: "acme",
            CorrelationId: "0HN7A2C3D4E5F-00000001",
            Total: 59.97m,
            Lines:
            [
                new OrderLineV1(
                    ProductId: Guid.Parse("9a1b2c3d-4e5f-6071-8293-a4b5c6d7e8f9"),
                    Quantity: 2,
                    UnitPrice: 19.99m),
                new OrderLineV1(
                    ProductId: Guid.Parse("11112222-3333-4444-5555-666677778888"),
                    Quantity: 1,
                    UnitPrice: 19.99m),
            ]);

        AssertValidatesAgainst(@event, EmbeddedSchema.OrderPlacedV1ResourceName);
    }

    [Fact]
    public void Serialized_BasketCheckedOutV1_Validates_Against_Its_Published_Schema()
    {
        var @event = new BasketCheckedOutV1(
            EventId: Guid.Parse("c56a4180-65aa-42ec-a945-5fd21dec0538"),
            OccurredAtUtc: new DateTime(2026, 8, 22, 10, 15, 29, DateTimeKind.Utc),
            BasketId: Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"),
            CustomerRef: "auth0|64f1c2a9e3b4",
            TenantId: "acme",
            CorrelationId: "0HN7A2C3D4E5F-00000001",
            Items:
            [
                new BasketLineItemV1(
                    ProductId: Guid.Parse("9a1b2c3d-4e5f-6071-8293-a4b5c6d7e8f9"),
                    Quantity: 2,
                    UnitPrice: 19.99m,
                    LineTotal: 39.98m),
                new BasketLineItemV1(
                    ProductId: Guid.Parse("11112222-3333-4444-5555-666677778888"),
                    Quantity: 1,
                    UnitPrice: 19.99m,
                    LineTotal: 19.99m),
            ],
            Total: 59.97m);

        AssertValidatesAgainst(@event, EmbeddedSchema.BasketCheckedOutV1ResourceName);
    }

    private static void AssertValidatesAgainst<T>(T @event, string schemaResourceName)
    {
        var payload = JsonSerializer.SerializeToElement(@event, PublisherOptions);
        var schema = JsonSchema.FromText(EmbeddedSchema.ReadText(schemaResourceName));

        var results = schema.Evaluate(payload, StrictEvaluation);

        Assert.True(
            results.IsValid,
            $"A serialized {typeof(T).Name} did not validate against '{schemaResourceName}'. " +
            $"The record and its schema have drifted apart.{Environment.NewLine}" +
            $"Payload: {payload.GetRawText()}{Environment.NewLine}" +
            $"Evaluation: {JsonSerializer.Serialize(results, PublisherOptions)}");
    }
}
