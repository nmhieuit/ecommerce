namespace Orders.Api.ContractTests;

/// <summary>
/// The regular expressions the pacts here match identifier and timestamp fields with.
/// </summary>
/// <remarks>
/// A type matcher would only say "a string", and the BFF deserialises these into
/// <see cref="Guid"/> and <see cref="DateTime"/> — a producer that started returning a string of
/// some other shape would break it while satisfying a type match. These say what the string has to
/// look like without pinning its value.
/// </remarks>
internal static class PactRegex
{
    public const string Uuid = "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";

    /// <summary>
    /// ISO-8601, with the zone designator optional. Both forms occur legitimately: a timestamp the
    /// service has just produced carries <c>Z</c>, and one read back from a <c>datetime2</c> column
    /// comes back kind-less and serialises without it. Requiring <c>Z</c> would fail the read path
    /// for a difference the BFF cannot observe.
    /// </summary>
    public const string Iso8601DateTime =
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?$";
}
