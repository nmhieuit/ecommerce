using System.Text.Json;
using System.Text.RegularExpressions;

namespace ServiceManifestSloConventionTests;

/// <summary>
/// Panel "Cạn ngân sách — ưu tiên độ tin cậy (trạng thái hiện tại)" trên dashboard "Ngân sách lỗi tuần — 7 service"
/// (Discover session `slo-error-budget-frozen`, export trong `docs/kibana-quan-sat-he-thong/dashboards/ngan-sach-loi-tuan.ndjson`)
/// cho biết mỗi service đã từng cạn trong tuần đang ở bước nào: `active` / `recovering` / `recovered` — nhánh
/// fix/frozen-panel-status. Saved search sống trong Kibana, file export là bản duy nhất kiểm tra được trong CI.
/// </summary>
public class FrozenPanelStatusTests
{
    private const string SavedSearchId = "slo-error-budget-frozen";

    /// <summary>
    /// Kiểm tra: truy vấn của panel kết thúc bằng các cột `service, status, consumed_max_pct, frozen_since, recovered_at`.
    /// Lý do: panel cũ chỉ có `service` và `kibana.alert.start`, không cho biết service đã hồi phục tới đâu nên người
    /// vận hành thấy service "đóng băng" dù ngân sách tuần đã tốt.
    /// Task nguồn: nhánh fix/frozen-panel-status (tech-debt: cột status active/recovering/recovered).
    /// </summary>
    [Fact]
    public void Panel_ShowsTheStatusColumns()
    {
        // Assert.Contains(chuỗi con, chuỗi): xanh khi truy vấn giữ đúng 5 cột theo thứ tự đã chốt.
        Assert.Contains("| KEEP service, status, consumed_max_pct, frozen_since, recovered_at", RequirePanelEsql(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Kiểm tra: cột `status` là `active` khi alert frozen đang active và mức tiêu hao cao nhất ≥ 100%, `recovering`
    /// khi alert còn active nhưng mức tiêu hao dưới 100% (hoặc tuần chưa có request), còn lại là `recovered`.
    /// Lý do: quy tắc hồi phục mới — đóng băng ở cả `active` và `recovering`; `recovered` chỉ khi rule
    /// `error-budget-frozen` đã gỡ alert (mức tiêu hao dưới `recovery.recovered-below-consumption`).
    /// Task nguồn: nhánh fix/frozen-panel-status (tech-debt: cột status active/recovering/recovered).
    /// </summary>
    [Fact]
    public void Panel_DerivesTheStatusFromTheAlertAndTheConsumption()
    {
        // Assert.Contains(chuỗi con, chuỗi): xanh khi CASE ra đúng 3 giá trị tiếng Anh theo đúng thứ tự ưu tiên.
        Assert.Contains(
            "EVAL status = CASE(frozen_since IS NOT NULL AND consumed_max_pct >= 100, \"active\", frozen_since IS NOT NULL, \"recovering\", \"recovered\")",
            RequirePanelEsql(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Kiểm tra: `consumed_max_pct` là `GREATEST` của ba tỷ lệ xấu (5xx, vượt p95, vượt p99) chia cho tỷ lệ cho phép
    /// 0.01 / 0.05 / 0.01 trên số span của tuần.
    /// Lý do: con số trên panel phải bằng con số rule `error-budget-frozen` dùng để gỡ đóng băng
    /// (`FrozenRule_ConsumptionUsesTheBudgetRatios`); lệch tỷ lệ là panel nói `recovering` trong khi rule đã gỡ.
    /// Task nguồn: nhánh fix/frozen-panel-status (tech-debt: cột status active/recovering/recovered).
    /// </summary>
    [Fact]
    public void Panel_ConsumptionUsesTheBudgetRatios()
    {
        // Assert.Contains(chuỗi con, chuỗi): xanh khi công thức mức tiêu hao cao nhất dùng đúng 3 tỷ lệ cho phép.
        Assert.Contains(
            "GREATEST(TO_DOUBLE(bad_5xx) / spans / 0.01, TO_DOUBLE(bad_p95) / spans / 0.05, TO_DOUBLE(bad_p99) / spans / 0.01)",
            RequirePanelEsql(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Kiểm tra: panel chỉ đọc từ thứ Hai 00:00 giờ Việt Nam của tuần hiện tại, lấy alert của rule `error-budget-frozen`
    /// và chỉ đếm span Server không phải `/health*`.
    /// Lý do: mức tiêu hao trên panel phải tính cùng tập span như 3 rule mốc (spec 033 loại health, spec 034 chỉ span
    /// Server) và cùng ranh giới tuần (spec 029); panel là trạng thái hiện tại, không đổi theo tuần đã chọn.
    /// Task nguồn: nhánh fix/frozen-panel-status (tech-debt: cột status active/recovering/recovered).
    /// </summary>
    [Fact]
    public void Panel_ReadsTheCurrentWeekOfServerSpansAndFrozenAlerts()
    {
        var esql = RequirePanelEsql();

        // Assert.Contains(chuỗi con, chuỗi): xanh khi panel bắt đầu từ thứ Hai 00:00 UTC+7 của tuần hiện tại.
        Assert.Contains("EVAL t0 = DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours", esql, StringComparison.Ordinal);
        Assert.Contains("WHERE @timestamp >= t0", esql, StringComparison.Ordinal);

        // Assert.Contains(chuỗi con, chuỗi): xanh khi panel lấy alert của đúng rule frozen.
        Assert.Contains("kibana.alert.rule.name == \"error-budget-frozen\"", esql, StringComparison.Ordinal);

        // Assert.Contains(chuỗi con, chuỗi): xanh khi span vào mức tiêu hao là span Server và không phải health check.
        Assert.Contains("kind == \"Server\" AND NOT (COALESCE(attributes.url.path, \"\") LIKE \"/health*\")", esql, StringComparison.Ordinal);
    }

    /// <summary>Truy vấn ES|QL của saved search `slo-error-budget-frozen` trong file export, khoảng trắng đã gộp.</summary>
    private static string RequirePanelEsql()
    {
        var path = Path.Combine(
            ServiceManifestFixture.LocateRepositoryRoot(),
            "docs", "kibana-quan-sat-he-thong", "dashboards", "ngan-sach-loi-tuan.ndjson");

        // Assert.True(điều kiện, thông báo): xanh khi file export dashboard tồn tại.
        Assert.True(File.Exists(path), $"'{path}' does not exist — export the dashboard (docs/kibana-quan-sat-he-thong/dashboards/README.md).");

        var queries = new List<string>();
        foreach (var line in File.ReadLines(path).Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            if (!root.TryGetProperty("id", out var id) || id.GetString() != SavedSearchId)
            {
                continue; // dashboard, saved search khác, dòng tổng kết export, ...
            }

            var searchSource = root.GetProperty("attributes").GetProperty("kibanaSavedObjectMeta")
                .GetProperty("searchSourceJSON").GetString()!;
            using var source = JsonDocument.Parse(searchSource);
            queries.Add(source.RootElement.GetProperty("query").GetProperty("esql").GetString()!);
        }

        // Assert.Single(tập hợp): xanh khi file export có đúng một saved search `slo-error-budget-frozen`.
        return Regex.Replace(Assert.Single(queries), @"\s+", " ");
    }
}
