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

    /// <summary>
    /// SCRUM-35 error-budget policy (specs/027-error-budget-alerting
    /// contracts/error-budget-policy-manifest-shape.md).
    /// </summary>
    [YamlMember(Alias = "error-budget-policy")]
    public ErrorBudgetPolicySection? ErrorBudgetPolicy { get; set; }
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

/// <summary>
/// The <c>error-budget-policy</c> block (specs/029-error-budget-weekly
/// contracts/error-budget-policy-manifest-shape.md, which replaces the 027 calendar-month shape). Every value is read as written — strings stay
/// strings (<c>1%</c>, <c>UTC+07:00</c>) so the tests compare against the contract verbatim instead
/// of against a number YamlDotNet happened to parse.
/// </summary>
public sealed class ErrorBudgetPolicySection
{
    [YamlMember(Alias = "window")]
    public string? Window { get; set; }

    [YamlMember(Alias = "timezone")]
    public string? Timezone { get; set; }

    /// <summary>Keyed by budget name, so an extra or missing budget is visible to the tests.</summary>
    [YamlMember(Alias = "budgets")]
    public Dictionary<string, ErrorBudgetSection>? Budgets { get; set; }

    [YamlMember(Alias = "alert-thresholds")]
    public List<string>? AlertThresholds { get; set; }

    [YamlMember(Alias = "exhausted-when")]
    public string? ExhaustedWhen { get; set; }

    [YamlMember(Alias = "on-exhausted")]
    public OnExhaustedSection? OnExhausted { get; set; }

    [YamlMember(Alias = "recovery")]
    public RecoverySection? Recovery { get; set; }
}

/// <summary>
/// One budget. Deliberately has no latency-threshold field: thresholds come only from
/// <see cref="SlosSection.Latency"/> (contract bất biến 7), and IgnoreUnmatchedProperties would hide
/// one — <see cref="ErrorBudgetPolicyTests"/> checks the raw keys instead.
/// </summary>
public sealed class ErrorBudgetSection
{
    [YamlMember(Alias = "bad-request")]
    public string? BadRequest { get; set; }

    [YamlMember(Alias = "allowed-bad-ratio")]
    public string? AllowedBadRatio { get; set; }
}

public sealed class OnExhaustedSection
{
    [YamlMember(Alias = "who")]
    public string? Who { get; set; }

    [YamlMember(Alias = "stops")]
    public string? Stops { get; set; }

    [YamlMember(Alias = "does")]
    public string? Does { get; set; }
}

public sealed class RecoverySection
{
    [YamlMember(Alias = "consecutive-days-meeting-slo")]
    public string? ConsecutiveDaysMeetingSlo { get; set; }

    [YamlMember(Alias = "no-traffic-day-counts-as-met")]
    public string? NoTrafficDayCountsAsMet { get; set; }

    [YamlMember(Alias = "budget-reset-clears-freeze")]
    public string? BudgetResetClearsFreeze { get; set; }
}
