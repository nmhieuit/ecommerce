namespace ServiceDefaults;

/// <summary>
/// Binds to the <c>Chaos</c> configuration section (specs/027-error-budget-alerting data-model.md
/// mục 6) — the same section Orders.Api's 025 latency-injection options read, so one place in each
/// service's configuration holds every chaos switch. A permanent SRE exercise tool, not a rollout
/// toggle with a removal date, exactly like 025's.
/// </summary>
public sealed class ChaosFaultOptions
{
    public const string SectionName = "Chaos";

    /// <summary>
    /// Off by default — the safe state. MUST NOT be <see langword="true"/> in any committed
    /// configuration that represents production
    /// (contracts/chaos-fault-injection-contract.md Bất biến 1).
    /// </summary>
    public bool AllowFaultInjection { get; set; }
}
