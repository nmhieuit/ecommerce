using System.Globalization;
using ServiceManifestSloConventionTests;

namespace CriticalPathLoadTests;

/// <summary>One step of the critical path, with the latency budget it must be measured against.</summary>
public sealed record CriticalPathStepBudget(string StepName, double P95Ms, double P99Ms);

/// <summary>
/// Reads the "client-facing-bff" latency budget once from the bff's own <c>service-manifest.yaml</c>
/// — via <see cref="ServiceManifestFixture"/> (021-declare-service-slos), never a second YAML parser
/// or a hard-coded number (research.md Quyết định 0) — and applies it to all 4 steps of the critical
/// path. All 4 are bff routes, so all 4 carry the same budget class (data-model.md — "Nhóm ngân sách
/// luôn là client-facing-bff"; research.md Quyết định 2).
/// </summary>
public static class CriticalPathStepBudgets
{
    /// <summary>The 4 steps of browse→basket→checkout→order, in the order they execute.</summary>
    public static readonly IReadOnlyList<string> StepNames =
    [
        "GET /bff/products",
        "POST /bff/basket/items",
        "POST /bff/checkout",
        "GET /bff/orders/{orderId}",
    ];

    private const string BffServiceDirectoryName = "bff";

    /// <summary>
    /// Loads the current budget for every step. Called fresh at the start of each run (not cached
    /// across runs), so a manifest edit takes effect on the very next run with no code change
    /// (contracts/load-test-run-contract.md bất biến 1).
    /// </summary>
    public static IReadOnlyList<CriticalPathStepBudget> LoadAll()
    {
        var repositoryRoot = ServiceManifestFixture.LocateRepositoryRoot();
        var manifests = ServiceManifestFixture.DiscoverAll(repositoryRoot);

        if (!manifests.TryGetValue(BffServiceDirectoryName, out var bff))
        {
            throw new InvalidOperationException(
                $"Could not find a service-manifest.yaml for '{BffServiceDirectoryName}'.");
        }

        var latency = bff.Document.Slos?.Latency
            ?? throw new InvalidOperationException(
                $"'{bff.FilePath}' has no slos.latency section.");

        var p95 = ParseMilliseconds(latency.P95, $"{bff.FilePath}: slos.latency.p95");
        var p99 = ParseMilliseconds(latency.P99, $"{bff.FilePath}: slos.latency.p99");

        return [.. StepNames.Select(name => new CriticalPathStepBudget(name, p95, p99))];
    }

    /// <summary>Parses a manifest value like <c>"300ms"</c> into <c>300.0</c>.</summary>
    private static double ParseMilliseconds(string? value, string fieldDescription)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{fieldDescription} must be a millisecond value like '300ms', but was '{value}'.");
        }

        var numericPart = value[..^"ms".Length];

        return double.Parse(numericPart, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
