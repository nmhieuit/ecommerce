namespace CriticalPathLoadTests;

/// <summary>
/// US2: a step that crosses its budget must fail the run; a step within budget must not — including
/// at the exact boundary. Pure logic, no HTTP — runs fast even though it lives in a project excluded
/// from the "unit" CI tier at the csproj level (scripts/ci/run-dotnet-tests.sh; T003).
/// </summary>
public class BudgetAssertionsTests
{
    private static StepResult Step(double measuredP95, double measuredP99, double thresholdP95 = 300, double thresholdP99 = 800) =>
        new("some step", measuredP95, measuredP99, thresholdP95, thresholdP99);

    [Fact]
    public void AssertAllStepsWithinBudget_DoesNotThrow_WhenEveryStepIsWithinBudget()
    {
        var result = new LoadTestRunResult(
            DateTime.UtcNow,
            "test",
            [Step(measuredP95: 250, measuredP99: 750), Step(measuredP95: 300, measuredP99: 800)]);

        var exception = Record.Exception(() => BudgetAssertions.AssertAllStepsWithinBudget(result, "report.md"));

        Assert.Null(exception);
    }

    [Fact]
    public void AssertAllStepsWithinBudget_Throws_WhenP95ExceedsBudget()
    {
        var result = new LoadTestRunResult(
            DateTime.UtcNow,
            "test",
            [Step(measuredP95: 301, measuredP99: 750)]);

        var exception = Record.Exception(() => BudgetAssertions.AssertAllStepsWithinBudget(result, "report.md"));

        Assert.NotNull(exception);
    }

    [Fact]
    public void AssertAllStepsWithinBudget_Throws_WhenP99ExceedsBudget()
    {
        var result = new LoadTestRunResult(
            DateTime.UtcNow,
            "test",
            [Step(measuredP95: 250, measuredP99: 801)]);

        var exception = Record.Exception(() => BudgetAssertions.AssertAllStepsWithinBudget(result, "report.md"));

        Assert.NotNull(exception);
    }

    [Fact]
    public void AssertAllStepsWithinBudget_ThrowsOnce_NamingEveryViolatingStep_NotJustTheFirst()
    {
        var result = new LoadTestRunResult(
            DateTime.UtcNow,
            "test",
            [
                new StepResult("step A", MeasuredP95Ms: 301, MeasuredP99Ms: 750, ThresholdP95Ms: 300, ThresholdP99Ms: 800),
                new StepResult("step B", MeasuredP95Ms: 250, MeasuredP99Ms: 801, ThresholdP95Ms: 300, ThresholdP99Ms: 800),
                new StepResult("step C", MeasuredP95Ms: 250, MeasuredP99Ms: 750, ThresholdP95Ms: 300, ThresholdP99Ms: 800),
            ]);

        var exception = Record.Exception(() => BudgetAssertions.AssertAllStepsWithinBudget(result, "report.md"));

        Assert.NotNull(exception);
        Assert.Contains("step A", exception.Message);
        Assert.Contains("step B", exception.Message);
        Assert.DoesNotContain("step C", exception.Message);
    }
}
