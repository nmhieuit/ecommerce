namespace CriticalPathLoadTests;

/// <summary>
/// US2: turns a measured run into a real gate. Pure logic — takes an already-computed
/// <see cref="LoadTestRunResult"/>, never makes an HTTP call itself, so it can be exercised without a
/// live stack (<see cref="BudgetAssertionsTests"/>).
/// </summary>
public static class BudgetAssertions
{
    /// <summary>
    /// Throws when any step is over budget (contracts/load-test-run-contract.md bất biến 3), naming
    /// every violating step — not just the first — and pointing at the report already written for
    /// this run (bất biến 5) so the failure message is enough to investigate without re-running.
    /// </summary>
    public static void AssertAllStepsWithinBudget(LoadTestRunResult result, string reportPath)
    {
        var violations = result.Steps.Where(step => !step.Passed).ToList();

        if (violations.Count == 0)
        {
            return;
        }

        var descriptions = violations.Select(step => string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0} (p95 {1:F1}ms / budget {2:F1}ms, p99 {3:F1}ms / budget {4:F1}ms)",
            step.StepName,
            step.MeasuredP95Ms,
            step.ThresholdP95Ms,
            step.MeasuredP99Ms,
            step.ThresholdP99Ms));

        Assert.Fail(
            $"{violations.Count} step(s) exceeded their declared budget — see {reportPath}: " +
            string.Join("; ", descriptions));
    }
}
