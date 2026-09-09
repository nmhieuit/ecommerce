using YamlDotNet.Serialization;

namespace ServiceManifestSloConventionTests;

/// <summary>
/// Minimal projection of a <c>service-manifest.yaml</c> — only the fields this suite asserts on.
/// YAML keys are kebab-case and cannot all be produced by a single YamlDotNet naming convention
/// (<c>max-5xx-ratio</c> contains a digit, <c>p95</c>/<c>p99</c> have no hyphen at all), so every
/// property carries an explicit <see cref="YamlMemberAttribute"/> alias instead
/// (contracts/service-manifest-slo-shape.md).
/// </summary>
public sealed class ServiceManifestDocument
{
    [YamlMember(Alias = "service")]
    public ServiceSection? Service { get; set; }

    [YamlMember(Alias = "slos")]
    public SlosSection? Slos { get; set; }
}

public sealed class ServiceSection
{
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    [YamlMember(Alias = "classification")]
    public string? Classification { get; set; }
}

public sealed class SlosSection
{
    [YamlMember(Alias = "availability")]
    public string? Availability { get; set; }

    [YamlMember(Alias = "error-rate")]
    public ErrorRateSection? ErrorRate { get; set; }

    [YamlMember(Alias = "latency")]
    public LatencySection? Latency { get; set; }

    /// <summary>
    /// Only required when one of the four SLO values above differs from the platform default for
    /// <see cref="ServiceSection.Classification"/> (contracts/service-manifest-slo-shape.md bất biến 5).
    /// </summary>
    [YamlMember(Alias = "justification")]
    public string? Justification { get; set; }
}

public sealed class ErrorRateSection
{
    [YamlMember(Alias = "max-5xx-ratio")]
    public string? MaxFiveXxRatio { get; set; }
}

public sealed class LatencySection
{
    [YamlMember(Alias = "p95")]
    public string? P95 { get; set; }

    [YamlMember(Alias = "p99")]
    public string? P99 { get; set; }
}
