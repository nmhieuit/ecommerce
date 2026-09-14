using NBomber.Contracts.Stats;
using NBomber.CSharp;
using ServiceManifestSloConventionTests;

namespace CriticalPathLoadTests;

/// <summary>
/// Runs the critical-path load test once (US1), measures p95/p99 per step against the budget read
/// from the bff manifest, writes the run's report (FR-005), and then fails the run if any step is
/// over budget (US2 — see <see cref="BudgetAssertions"/>).
/// </summary>
public class CriticalPathLoadTest
{
    [Fact]
    public void CriticalPath_MeasuredAgainstDeclaredBudget()
    {
        var startedAtUtc = DateTime.UtcNow;
        var budgets = CriticalPathStepBudgets.LoadAll();

        using var httpClient = GatewayClient.Create();
        var scenario = CriticalPathScenario.Create(httpClient)
            // Deliberately modest: every virtual user authenticates as the same stub identity today
            // and therefore shares one basket (CriticalPathScenario remarks) — a higher rate would
            // measure basket contention, not the endpoints' own latency.
            .WithLoadSimulations(Simulation.Inject(rate: 2, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30)));

        NodeStats nodeStats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder(Path.Combine(Path.GetTempPath(), "critical-path-load-test-nbomber-reports"))
            .WithReportFormats(ReportFormat.Txt)
            .Run();

        var scenarioStats = nodeStats.ScenarioStats.Single(stats => stats.ScenarioName == "critical_path");

        var stepResults = budgets
            .Select(budget =>
            {
                var stepStats = scenarioStats.StepStats.Single(stats => stats.StepName == budget.StepName);

                return new StepResult(
                    budget.StepName,
                    MeasuredP95Ms: stepStats.Ok.Latency.Percent95,
                    MeasuredP99Ms: stepStats.Ok.Latency.Percent99,
                    ThresholdP95Ms: budget.P95Ms,
                    ThresholdP99Ms: budget.P99Ms);
            })
            .ToList();

        var runResult = new LoadTestRunResult(startedAtUtc, "local/demo (docker-compose.demo.yml)", stepResults);

        var repositoryRoot = ServiceManifestFixture.LocateRepositoryRoot();
        var reportPath = LoadTestReportWriter.Write(runResult, repositoryRoot);

        // The report above is written unconditionally, BEFORE this can fail the test — a lần chạy
        // thất bại still leaves a baseline to look back at (contracts/load-test-run-contract.md bất
        // biến 5; FR-005 is not sacrificed for FR-004).
        BudgetAssertions.AssertAllStepsWithinBudget(runResult, reportPath);
    }
}
