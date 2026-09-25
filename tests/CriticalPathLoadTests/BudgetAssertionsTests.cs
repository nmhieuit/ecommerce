namespace CriticalPathLoadTests;

/// <summary>
/// Spec 026 (kiểm thử tải/hiệu năng đối chiếu ngân sách) — US2: 1 bước vượt ngân sách phải làm cả lần
/// chạy thất bại; 1 bước nằm trong ngân sách thì không — kể cả đúng tại biên. Logic thuần, không gọi
/// HTTP nên chạy nhanh, dù nằm trong 1 project bị loại khỏi tier "unit" của CI ở mức csproj
/// (`scripts/ci/run-dotnet-tests.sh`; T003) — nghĩa là các test này chỉ chạy ở tier `performance`
/// theo lịch, không chạy trên PR (xem QA_Debt mục 026).
/// </summary>
public class BudgetAssertionsTests
{
    private static StepResult Step(double measuredP95, double measuredP99, double thresholdP95 = 300, double thresholdP99 = 800) =>
        new("some step", measuredP95, measuredP99, thresholdP95, thresholdP99);

    /// <summary>
    /// Kiểm tra: mọi bước nằm trong ngân sách (p95 250/300 và 300/300, p99 750/800 và 800/800 — gồm cả
    /// đúng bằng ngưỡng) thì `AssertAllStepsWithinBudget` không ném lỗi.
    /// Lý do (phải test): US2-KB3/SC-004 — cổng phải trở lại xanh khi không còn vi phạm, và đúng-bằng-ngưỡng
    /// vẫn là "đạt" (`<=`); nếu không, cổng luôn báo đỏ.
    /// Task nguồn: spec 026 (kiểm thử tải/hiệu năng đối chiếu ngân sách) — US2, FR-004.
    /// </summary>
    [Fact]
    public void AssertAllStepsWithinBudget_DoesNotThrow_WhenEveryStepIsWithinBudget()
    {
        var result = new LoadTestRunResult(
            DateTime.UtcNow,
            "test",
            [Step(measuredP95: 250, measuredP99: 750), Step(measuredP95: 300, measuredP99: 800)]);

        var exception = Record.Exception(() => BudgetAssertions.AssertAllStepsWithinBudget(result, "report.md"));

        // Assert.Null: xanh khi không có exception (mọi bước đạt, kể cả tại biên); đỏ khi cổng ném lỗi dù
        // không bước nào vượt ngân sách (đổi `<=` thành `<` trong `StepResult.Passed` làm test này đỏ).
        Assert.Null(exception);
    }

    /// <summary>
    /// Kiểm tra: 1 bước có p95 = 301 ms (vượt ngưỡng 300 ms) làm `AssertAllStepsWithinBudget` ném lỗi.
    /// Lý do (phải test): FR-004/US2-KB1/SC-002 — vượt ngân sách p95 phải là THẤT BẠI, không phải cảnh báo.
    /// Task nguồn: spec 026 (kiểm thử tải/hiệu năng đối chiếu ngân sách) — US2, FR-004.
    /// </summary>
    [Fact]
    public void AssertAllStepsWithinBudget_Throws_WhenP95ExceedsBudget()
    {
        var result = new LoadTestRunResult(
            DateTime.UtcNow,
            "test",
            [Step(measuredP95: 301, measuredP99: 750)]);

        var exception = Record.Exception(() => BudgetAssertions.AssertAllStepsWithinBudget(result, "report.md"));

        // Assert.NotNull: xanh khi cổng ném lỗi; đỏ khi p95 vượt ngân sách mà vẫn "thành công".
        Assert.NotNull(exception);
    }

    /// <summary>
    /// Kiểm tra: 1 bước có p99 = 801 ms (vượt ngưỡng 800 ms) dù p95 đạt vẫn làm cổng ném lỗi.
    /// Lý do (phải test): FR-004 — chỉ cần MỘT trong hai chỉ tiêu vượt là thất bại; không được bỏ sót p99.
    /// Task nguồn: spec 026 (kiểm thử tải/hiệu năng đối chiếu ngân sách) — US2, FR-004.
    /// </summary>
    [Fact]
    public void AssertAllStepsWithinBudget_Throws_WhenP99ExceedsBudget()
    {
        var result = new LoadTestRunResult(
            DateTime.UtcNow,
            "test",
            [Step(measuredP95: 250, measuredP99: 801)]);

        var exception = Record.Exception(() => BudgetAssertions.AssertAllStepsWithinBudget(result, "report.md"));

        // Assert.NotNull: xanh khi cổng ném lỗi; đỏ khi chỉ kiểm p95 mà bỏ qua p99.
        Assert.NotNull(exception);
    }

    /// <summary>
    /// Kiểm tra: khi 2 trong 3 bước vượt ngân sách (A vượt p95, B vượt p99, C đạt), 1 lỗi duy nhất được
    /// ném ra, thông báo nêu tên cả "step A" và "step B" nhưng không nêu "step C".
    /// Lý do (phải test): bất biến 5 của hợp đồng chạy thử — thông báo đủ để điều tra mà không cần chạy lại;
    /// liệt kê MỌI bước vi phạm, không chỉ bước đầu tiên.
    /// Task nguồn: spec 026 (kiểm thử tải/hiệu năng đối chiếu ngân sách) — US2, load-test-run-contract bất biến 3/5.
    /// </summary>
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

        // Assert.NotNull: xanh khi cổng ném lỗi vì có bước vi phạm; đỏ khi im lặng.
        Assert.NotNull(exception);
        // Assert.Contains #1/#2: xanh khi thông báo lỗi nêu tên cả 2 bước vi phạm (A và B); đỏ khi chỉ nêu
        // bước đầu tiên hoặc thiếu tên bước.
        Assert.Contains("step A", exception.Message);
        Assert.Contains("step B", exception.Message);
        // Assert.DoesNotContain: xanh khi bước đạt (C) không bị nêu nhầm; đỏ khi liệt kê cả bước không vi phạm.
        Assert.DoesNotContain("step C", exception.Message);
    }
}
