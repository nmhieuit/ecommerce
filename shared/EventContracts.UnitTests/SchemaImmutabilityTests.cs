using System.Security.Cryptography;

namespace EventContracts.UnitTests;

/// <summary>
/// Proves a published schema version cannot be silently edited (spec FR-003, FR-006, SC-002).
/// </summary>
/// <remarks>
/// <para>
/// <b>If one of these tests is failing for you, do not update the constant.</b> That is the
/// failure working as designed. A committed schema version is frozen: the sanctioned way to change
/// an event's shape is to add <c>{Event}.v{N+1}.schema.json</c> plus a matching
/// <c>{Event}V{N+1}</c> record, leaving the published version untouched, and then to add a *new*
/// constant here for the *new* file. Editing the constant below to match your edit defeats the
/// only thing standing between a consumer and an unannounced contract change.
/// </para>
/// <para>
/// The check is deliberately blunt: it hashes the whole document, so it fires on any edit at all,
/// including a cosmetic one. Classifying breaking versus non-breaking JSON Schema changes is a hard
/// problem this feature does not attempt (research.md Decision 3) — an occasional forced version
/// bump for a typo fix is a much cheaper mistake than a missed breaking change. Consumer-aware
/// compatibility analysis is ADR-0006/SCRUM-21's job, not this test's.
/// </para>
/// </remarks>
public sealed class SchemaImmutabilityTests
{
    /// <summary>SHA-256 of <c>OrderPlaced.v1.schema.json</c> as first published.</summary>
    private const string OrderPlacedV1Sha256 =
        "3518223B9534D182A8CD11564E671BE3E1420A9A027973CFD42DEE221CADD601";

    /// <summary>SHA-256 of <c>BasketCheckedOut.v1.schema.json</c> as first published.</summary>
    private const string BasketCheckedOutV1Sha256 =
        "4BCE5A5DF0A3B296F94AEAC6A08CAB711C593B377342BBF36AE910ECF0172DCF";

    [Fact]
    public void OrderPlaced_V1_Schema_Content_Is_Frozen()
    {
        AssertSchemaUnchanged(EmbeddedSchema.OrderPlacedV1ResourceName, OrderPlacedV1Sha256);
    }

    [Fact]
    public void BasketCheckedOut_V1_Schema_Content_Is_Frozen()
    {
        AssertSchemaUnchanged(EmbeddedSchema.BasketCheckedOutV1ResourceName, BasketCheckedOutV1Sha256);
    }

    private static void AssertSchemaUnchanged(string resourceName, string expectedSha256)
    {
        var actual = Convert.ToHexString(
            SHA256.HashData(EmbeddedSchema.ReadNormalisedBytes(resourceName)));

        Assert.True(
            string.Equals(expectedSha256, actual, StringComparison.Ordinal),
            $"'{resourceName}' has changed since it was published (expected SHA-256 " +
            $"{expectedSha256}, got {actual}). A published schema version is immutable: ship the " +
            "change as a new version file and a new record type instead of editing this one, and " +
            "add a new constant here for the new file. Do not update the constant above.");
    }
}
