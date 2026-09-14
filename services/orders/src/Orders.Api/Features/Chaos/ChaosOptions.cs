namespace Orders.Api.Features.Chaos;

/// <summary>
/// Binds to the <c>Chaos</c> configuration section (specs/025-chaos-pod-kill-latency data-model.md
/// mục 1). A permanent operational tool for the SRE-hat-wearer's chaos exercises, not a rollout
/// toggle with a removal date — see plan.md Constitution Check mục X.
/// </summary>
public sealed class ChaosOptions
{
    public const string SectionName = "Chaos";

    /// <summary>
    /// Off by default — the safe state. MUST NOT be <see langword="true"/> in any committed
    /// configuration that represents production
    /// (contracts/chaos-latency-injection-contract.md Bất biến 1).
    /// </summary>
    public bool AllowLatencyInjection { get; set; }

    /// <summary>
    /// Safety cap, in milliseconds, for the <c>X-Chaos-Latency-Ms</c> header
    /// (contracts/chaos-latency-injection-contract.md Bất biến 4) — a request for more than this is
    /// clamped, never honoured in full, so a mistyped header cannot turn a controlled exercise into
    /// an unbounded wait.
    /// </summary>
    public const int MaxInjectedLatencyMs = 30_000;
}
