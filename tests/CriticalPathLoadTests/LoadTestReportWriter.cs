using System.Globalization;

namespace CriticalPathLoadTests;

/// <summary>
/// One step's measured latency against its declared budget (data-model.md — "Kết quả một bước").
/// </summary>
public sealed record StepResult(
    string StepName,
    double MeasuredP95Ms,
    double MeasuredP99Ms,
    double ThresholdP95Ms,
    double ThresholdP99Ms)
{
    /// <summary>
    /// FR-004: a step fails the moment EITHER percentile crosses its threshold — not just one of
    /// them, and not "noted" — contracts/load-test-run-contract.md bất biến 3.
    /// </summary>
    public bool Passed => MeasuredP95Ms <= ThresholdP95Ms && MeasuredP99Ms <= ThresholdP99Ms;
}

/// <summary>One full run of the critical path load test (data-model.md — "Kết quả một lần chạy").</summary>
public sealed record LoadTestRunResult(DateTime StartedAtUtc, string Environment, IReadOnlyList<StepResult> Steps)
{
    /// <summary>FR-004: any failing step fails the whole run.</summary>
    public bool Passed => Steps.All(step => step.Passed);
}

/// <summary>
/// Writes every run to its own timestamped file under <c>artifacts/performance/</c> — a baseline the
/// next run can be compared against (FR-005/SC-001) — and never overwrites a previous run's report
/// (contracts/load-test-run-contract.md bất biến 5).
/// </summary>
public static class LoadTestReportWriter
{
    public static string Write(LoadTestRunResult result, string repositoryRoot)
    {
        var directory = Path.Combine(repositoryRoot, "artifacts", "performance");
        Directory.CreateDirectory(directory);

        var fileName = $"critical-path-load-test-{result.StartedAtUtc:yyyyMMdd-HHmmssfff}.md";
        var path = Path.Combine(directory, fileName);

        File.WriteAllLines(path, BuildReportLines(result));

        return path;
    }

    private static IEnumerable<string> BuildReportLines(LoadTestRunResult result)
    {
        yield return $"# Critical path load test — {result.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture)}";
        yield return string.Empty;
        yield return $"Environment: {result.Environment}";
        yield return $"Overall: {(result.Passed ? "PASS" : "FAIL")}";
        yield return string.Empty;
        yield return "| Step | P95 đo được (ms) | Ngưỡng P95 (ms) | P99 đo được (ms) | Ngưỡng P99 (ms) | Trạng thái |";
        yield return "|---|---|---|---|---|---|";

        foreach (var step in result.Steps)
        {
            yield return string.Format(
                CultureInfo.InvariantCulture,
                "| {0} | {1:F1} | {2:F1} | {3:F1} | {4:F1} | {5} |",
                step.StepName,
                step.MeasuredP95Ms,
                step.ThresholdP95Ms,
                step.MeasuredP99Ms,
                step.ThresholdP99Ms,
                step.Passed ? "Pass" : "Fail");
        }
    }
}
