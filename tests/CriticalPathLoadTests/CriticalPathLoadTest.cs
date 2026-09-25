using NBomber.Contracts.Stats;
using NBomber.CSharp;
using ServiceManifestSloConventionTests;

namespace CriticalPathLoadTests;

/// <summary>
/// Spec 026 (kiểm thử tải/hiệu năng đối chiếu ngân sách) — chạy bài kiểm thử tải luồng trọng yếu 1
/// lần (US1), đo p95/p99 từng bước so với ngân sách đọc từ manifest của bff, ghi báo cáo của lần chạy
/// (FR-005), rồi làm lần chạy thất bại nếu có bước vượt ngân sách (US2 — xem <see cref="BudgetAssertions"/>).
/// Cần stack đang chạy (gateway ở `GATEWAY_ORIGIN`, mặc định `http://localhost:5300`) và hiện KHÔNG đính
/// bearer token nên chạy với stack có identity thật sẽ thất bại vì 401 (xem QA_Debt mục 026).
/// </summary>
public class CriticalPathLoadTest
{
    /// <summary>
    /// Kiểm tra: chạy kịch bản 4 bước browse→basket→checkout→order (2 luồng/giây trong 30 giây) qua
    /// gateway, ghi báo cáo `artifacts/performance/critical-path-load-test-*.md` (P95/P99 đo được, ngưỡng,
    /// trạng thái từng bước), rồi thất bại nếu bất kỳ bước nào có p95 hoặc p99 vượt ngân sách.
    /// Lý do (phải test): FR-001…FR-005 — đây là cổng hiệu năng tự động; SC-002 đòi mọi lần vượt ngân sách
    /// đều thất bại rõ ràng.
    /// Lưu ý: chỉ đo độ trễ của request THÀNH CÔNG (`Ok.Latency`); tỷ lệ lỗi không nằm trong cổng — đo thật
    /// cho thấy 4–27 lỗi/60 luồng vẫn ra `Overall: PASS`; nếu 1 bước không có request thành công nào (ví dụ
    /// 401 hàng loạt, hoặc dịch vụ vừa khởi động lại) `Single(...)` ném `InvalidOperationException` TRƯỚC khi
    /// ghi báo cáo, trái bất biến 5 (xem QA_Debt mục 026). Kịch bản không có bước làm ấm (`WithoutWarmUp`)
    /// nên lần chạy đầu sau khi khởi động lại dịch vụ dễ đỏ do độ trễ khởi động nguội.
    /// Task nguồn: spec 026 (kiểm thử tải/hiệu năng đối chiếu ngân sách) — US1/US2, FR-001…FR-005.
    /// </summary>
    [Fact]
    public void CriticalPath_MeasuredAgainstDeclaredBudget()
    {
        var startedAtUtc = DateTime.UtcNow;
        var budgets = CriticalPathStepBudgets.LoadAll();

        using var httpClient = GatewayClient.Create();
        var scenario = CriticalPathScenario.Create(httpClient)
            // Cố ý khiêm tốn: mọi người dùng ảo hiện xác thực cùng 1 danh tính và do đó dùng chung 1 giỏ
            // hàng (xem remarks của CriticalPathScenario) — tốc độ cao hơn sẽ đo tranh chấp giỏ hàng, không
            // phải độ trễ của chính các endpoint.
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

        // Báo cáo ở trên được ghi vô điều kiện, TRƯỚC khi lệnh dưới đây có thể làm test thất bại — 1 lần
        // chạy thất bại vẫn để lại 1 mốc nền để xem lại (`contracts/load-test-run-contract.md` bất biến 5;
        // FR-005 không bị hy sinh cho FR-004).
        // Assert (trong BudgetAssertions): xanh khi mọi bước có p95 ≤ ngưỡng và p99 ≤ ngưỡng; đỏ (nêu tên
        // từng bước vi phạm kèm đường dẫn báo cáo) khi có bước vượt ngân sách.
        BudgetAssertions.AssertAllStepsWithinBudget(runResult, reportPath);
    }
}
