namespace ServiceManifestSloConventionTests;

/// <summary>One platform-default SLO profile — the 4 values a service.classification maps to.</summary>
public sealed record SloProfile(string Availability, string MaxFiveXxRatio, string LatencyP95, string LatencyP99);

/// <summary>
/// The two platform-default SLO profiles from constitution Principle VIII ("Performance and
/// Resilience Budgets") — mirrored here, not redefined. If the constitution's numbers ever change,
/// this is the one place in the test suite that must be echoed (data-model.md § Hồ sơ mặc định nền
/// tảng).
/// </summary>
public static class PlatformSloDefaults
{
    public const string ClientFacingBff = "client-facing-bff";
    public const string InternalServiceApi = "internal-service-api";

    public static readonly IReadOnlyDictionary<string, SloProfile> ByClassification = new Dictionary<string, SloProfile>
    {
        [ClientFacingBff] = new SloProfile(
            Availability: "99.9%", MaxFiveXxRatio: "0.1%", LatencyP95: "300ms", LatencyP99: "800ms"),
        [InternalServiceApi] = new SloProfile(
            Availability: "99.9%", MaxFiveXxRatio: "0.1%", LatencyP95: "150ms", LatencyP99: "500ms"),
    };
}
