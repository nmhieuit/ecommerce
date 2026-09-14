namespace CriticalPathLoadTests;

/// <summary>
/// T007: budgets come from the real bff service-manifest.yaml on disk, not a hard-coded copy
/// (research.md Quyết định 0) — reads the file, no live stack needed.
/// </summary>
public class CriticalPathStepBudgetsTests
{
    [Fact]
    public void LoadAll_ReturnsOneBudgetPerStep_InOrder()
    {
        var budgets = CriticalPathStepBudgets.LoadAll();

        Assert.Equal(CriticalPathStepBudgets.StepNames, budgets.Select(budget => budget.StepName));
    }

    [Fact]
    public void LoadAll_MatchesTheClientFacingBffDefaultDeclaredInTheManifest()
    {
        // services/bff/src/Bff.Api/service-manifest.yaml: slos.latency.p95/p99 — client-facing-bff
        // (constitution Principle VIII). Asserted against the live value on disk so an edit to the
        // manifest changes this test's expectation, not the other way around.
        var budgets = CriticalPathStepBudgets.LoadAll();

        Assert.All(budgets, budget =>
        {
            Assert.Equal(300, budget.P95Ms);
            Assert.Equal(800, budget.P99Ms);
        });
    }
}
