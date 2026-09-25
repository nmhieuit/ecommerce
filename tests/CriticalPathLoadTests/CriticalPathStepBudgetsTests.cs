namespace CriticalPathLoadTests;

/// <summary>
/// Spec 026 (kiểm thử tải/hiệu năng đối chiếu ngân sách) — T007: ngân sách lấy từ
/// `service-manifest.yaml` thật của bff trên đĩa, không phải bản sao hard-code (`research.md` Quyết
/// định 0) — đọc file, không cần stack chạy. Lưu ý: không test nào phát hiện được nếu `LoadAll` bị
/// hard-code lại 300/800 (xem QA_Debt mục 026).
/// </summary>
public class CriticalPathStepBudgetsTests
{
    /// <summary>
    /// Kiểm tra: `LoadAll()` trả đúng 1 ngân sách cho mỗi bước và đúng thứ tự 4 bước
    /// (`GET /bff/products` → `POST /bff/basket/items` → `POST /bff/checkout` → `GET /bff/orders/{orderId}`).
    /// Lý do (phải test): FR-001/FR-002 — báo cáo và cổng đối chiếu theo từng bước, không gộp chung.
    /// Task nguồn: spec 026 (kiểm thử tải/hiệu năng đối chiếu ngân sách) — T007, US1.
    /// </summary>
    [Fact]
    public void LoadAll_ReturnsOneBudgetPerStep_InOrder()
    {
        var budgets = CriticalPathStepBudgets.LoadAll();

        // Assert.Equal(mong đợi, thực tế): xanh khi danh sách tên bước của ngân sách khớp đúng
        // `StepNames` (đủ 4 bước, đúng thứ tự); đỏ khi thiếu/thừa/đảo thứ tự bước.
        Assert.Equal(CriticalPathStepBudgets.StepNames, budgets.Select(budget => budget.StepName));
    }

    /// <summary>
    /// Kiểm tra: cả 4 bước mang đúng ngân sách `client-facing-bff` — p95 = 300 ms, p99 = 800 ms.
    /// Lý do (phải test): FR-003 — ngưỡng phải khớp ngân sách đã khai báo của bff (hiến chương
    /// Principle VIII).
    /// Lưu ý: 300/800 được viết cứng trong test, không đọc lại từ manifest như comment gốc ám chỉ — nên
    /// sửa manifest phải sửa cả test, và hard-code lại `LoadAll` vẫn xanh (xem QA_Debt mục 026).
    /// Task nguồn: spec 026 (kiểm thử tải/hiệu năng đối chiếu ngân sách) — T007, FR-003.
    /// </summary>
    [Fact]
    public void LoadAll_MatchesTheClientFacingBffDefaultDeclaredInTheManifest()
    {
        // `services/bff/src/Bff.Api/service-manifest.yaml`: slos.latency.p95/p99 — client-facing-bff
        // (hiến chương Principle VIII).
        var budgets = CriticalPathStepBudgets.LoadAll();

        Assert.All(budgets, budget =>
        {
            // Assert.Equal #1: xanh khi p95 của mọi bước đọc ra đúng 300 ms; đỏ khi manifest/parse cho giá
            // trị khác (ví dụ manifest sửa thành 350ms mà test chưa đổi).
            Assert.Equal(300, budget.P95Ms);
            // Assert.Equal #2: xanh khi p99 của mọi bước đọc ra đúng 800 ms; đỏ khi khác.
            Assert.Equal(800, budget.P99Ms);
        });
    }
}
