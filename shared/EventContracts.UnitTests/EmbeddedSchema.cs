using System.Reflection;
using EventContracts;

namespace EventContracts.UnitTests;

/// <summary>
/// Loads a committed schema document out of the <c>EventContracts</c> assembly.
/// </summary>
/// <remarks>
/// Reading the embedded resource rather than a path under <c>shared/EventContracts/schemas/</c>
/// keeps every test independent of the working directory it happens to be run from, and means the
/// tests check the schema that actually shipped in the assembly rather than a file that might have
/// been excluded from the build.
/// </remarks>
internal static class EmbeddedSchema
{
    /// <summary>The manifest name of the OrderPlaced v1 schema.</summary>
    internal const string OrderPlacedV1ResourceName = "OrderPlaced.v1.schema.json";

    /// <summary>The manifest name of the BasketCheckedOut v1 schema.</summary>
    internal const string BasketCheckedOutV1ResourceName = "BasketCheckedOut.v1.schema.json";

    private static readonly Assembly ContractsAssembly = typeof(OrderPlacedV1).Assembly;

    /// <summary>
    /// Returns the raw bytes of an embedded schema, with line endings normalised to LF.
    /// </summary>
    /// <remarks>
    /// The repository has no <c>.gitattributes</c> and git's <c>core.autocrlf</c> is on for Windows
    /// checkouts, so the same committed schema arrives as LF on one machine and CRLF on another.
    /// Normalising first means the immutability hash identifies the schema's *content*, not the
    /// platform that checked it out — otherwise the check would fail for half the team on a clean
    /// clone, which is the fastest way to get a test deleted.
    /// </remarks>
    internal static byte[] ReadNormalisedBytes(string resourceName)
    {
        var text = ReadText(resourceName).ReplaceLineEndings("\n");
        return System.Text.Encoding.UTF8.GetBytes(text);
    }

    /// <summary>Returns an embedded schema document as text, exactly as embedded.</summary>
    internal static string ReadText(string resourceName)
    {
        using var stream = ContractsAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded schema '{resourceName}' not found. Available resources: " +
                string.Join(", ", ContractsAssembly.GetManifestResourceNames()));

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
