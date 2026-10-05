using YamlDotNet.RepresentationModel;

namespace ServiceManifestSloConventionTests;

/// <summary>
/// User Story 1 (spec 027, SCRUM-35; chu kỳ tuần và tỷ lệ 1% theo spec 029): mọi service-manifest.yaml
/// mang khối `error-budget-policy` định nghĩa ngân sách lỗi bằng con số —
/// specs/029-error-budget-weekly/contracts/error-budget-policy-manifest-shape.md bất biến 1–7.
/// </summary>
public class ErrorBudgetPolicyTests
{
    /// <summary>4 ngân sách và giá trị bắt buộc của chúng — đúng bảng trong contract (spec 029: khả dụng/5xx 1%).</summary>
    private static readonly IReadOnlyDictionary<string, (string BadRequest, string AllowedBadRatio)> ExpectedBudgets =
        new Dictionary<string, (string, string)>
        {
            ["availability"] = ("http-5xx", "1%"),
            ["error-rate"] = ("http-5xx", "1%"),
            ["latency-p95"] = ("slower-than-slo-p95", "5%"),
            ["latency-p99"] = ("slower-than-slo-p99", "1%"),
        };

    /// <summary>Khoá cấp 1 của khối chính sách theo contract, đã sắp xếp theo thứ tự ordinal.</summary>
    private static readonly string[] ExpectedPolicyKeys =
        ["alert-thresholds", "budgets", "exhausted-when", "on-exhausted", "recovery", "timezone", "window"];

    /// <summary>Khoá của mỗi ngân sách theo contract, đã sắp xếp theo thứ tự ordinal.</summary>
    private static readonly string[] ExpectedBudgetKeys = ["allowed-bad-ratio", "bad-request"];

    /// <summary>
    /// Kiểm tra: manifest có khối `error-budget-policy`, và khối đó đứng ngay sau khối `slos`.
    /// Lý do: FR-001 — chính sách phải nằm cạnh SLO mà nó tiêu hao, để người đọc manifest thấy cả hai
    /// cùng lúc thay vì phải đi tìm.
    /// Task nguồn: spec 027 (chính sách ngân sách lỗi) — FR-001, US1 (bất biến 1).
    /// </summary>
    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_DeclaresThePolicy_RightAfterSlos(string serviceDirectoryName)
    {
        var manifest = Load(serviceDirectoryName);

        // Assert.NotNull(giá trị): xanh khi manifest có khối `error-budget-policy:`, đỏ khi chưa có.
        Assert.NotNull(manifest.Document.ErrorBudgetPolicy);

        var topLevelKeys = RootMapping(manifest.FilePath).Children.Keys.Select(k => k.ToString()).ToList();

        // Assert.Equal(kỳ vọng, thực tế): xanh khi khoá đứng ngay sau `slos` là `error-budget-policy`.
        Assert.Equal("error-budget-policy", topLevelKeys[topLevelKeys.IndexOf("slos") + 1]);
    }

    /// <summary>
    /// Kiểm tra: `budgets` có đúng 4 khoá `availability`, `error-rate`, `latency-p95`, `latency-p99`,
    /// mỗi khoá có `bad-request` và `allowed-bad-ratio` đúng giá trị của contract (khả dụng 1%, 5xx 1%,
    /// p95 5%, p99 1%).
    /// Lý do: FR-003 (spec 029) — "cạn" chỉ đo được khi mỗi ngân sách nói rõ request nào là xấu và được
    /// phép xấu bao nhiêu; tỷ lệ khả dụng/5xx phải bằng 1 − SLO mới (99% / 1%), và thiếu hoặc thừa một
    /// ngân sách là rule cảnh báo và chính sách nói hai điều khác nhau.
    /// Task nguồn: spec 029 (ngân sách lỗi theo tuần lịch) — FR-003, US1 (bất biến 2).
    /// </summary>
    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_DeclaresExactlyTheFourBudgets_WithContractValues(string serviceDirectoryName)
    {
        var budgets = RequirePolicy(serviceDirectoryName).Budgets;

        // Assert.NotNull(giá trị): xanh khi có khối `budgets:`.
        Assert.NotNull(budgets);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi tập tên ngân sách (đã sắp xếp) giống hệt contract.
        Assert.Equal(ExpectedBudgets.Keys.OrderBy(k => k), budgets!.Keys.OrderBy(k => k));

        foreach (var (name, expected) in ExpectedBudgets)
        {
            // Assert.Equal(kỳ vọng, thực tế): xanh khi quy tắc request xấu và tỷ lệ cho phép khớp contract.
            Assert.Equal(expected.BadRequest, budgets[name].BadRequest);
            Assert.Equal(expected.AllowedBadRatio, budgets[name].AllowedBadRatio);
        }
    }

    /// <summary>
    /// Kiểm tra: `window` là `calendar-week` và `timezone` là `UTC+07:00`.
    /// Lý do: FR-001 — ngân sách đặt lại vào thứ Hai 00:00 giờ Việt Nam (tuần lịch thay tháng lịch của
    /// 027); một cửa sổ khác làm mức tiêu hao trên manifest và trên rule lệch nhau ngay ở ngày đầu tuần.
    /// Task nguồn: spec 029 (ngân sách lỗi theo tuần lịch) — FR-001, US1 (bất biến 3).
    /// </summary>
    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_UsesACalendarWeekInVietnamTime(string serviceDirectoryName)
    {
        var policy = RequirePolicy(serviceDirectoryName);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi cửa sổ và múi giờ đúng nguyên văn contract.
        Assert.Equal("calendar-week", policy.Window);
        Assert.Equal("UTC+07:00", policy.Timezone);
    }

    /// <summary>
    /// Kiểm tra: `alert-thresholds` đúng `[50%, 75%, 100%]` và `exhausted-when` là `any-budget-at-100%`.
    /// Lý do: FR-004/FR-006 — "cạn" phải là một con số, và các mốc cảnh báo phải trùng với 3 rule
    /// `error-budget-50/75/100` đang chạy trong Kibana.
    /// Task nguồn: spec 027 (chính sách ngân sách lỗi) — FR-004, FR-006, US1 (bất biến 4).
    /// </summary>
    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_DefinesThresholdsAndExhaustionNumerically(string serviceDirectoryName)
    {
        var policy = RequirePolicy(serviceDirectoryName);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi danh sách mốc giống hệt, đúng thứ tự.
        Assert.Equal(["50%", "75%", "100%"], policy.AlertThresholds ?? []);
        Assert.Equal("any-budget-at-100%", policy.ExhaustedWhen);
    }

    /// <summary>
    /// Kiểm tra: `on-exhausted.who`, `.stops`, `.does` đều có nội dung.
    /// Lý do: FR-009/SC-004 — người đọc manifest phải trả lời được "ai dừng, dừng cái gì" mà không
    /// cần tài liệu khác; một trường rỗng là hệ quả không có ai chịu trách nhiệm.
    /// Task nguồn: spec 027 (chính sách ngân sách lỗi) — FR-009, SC-004, US1 (bất biến 5).
    /// </summary>
    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_NamesWhoStopsWhatWhenExhausted(string serviceDirectoryName)
    {
        var onExhausted = RequirePolicy(serviceDirectoryName).OnExhausted;

        // Assert.NotNull(giá trị): xanh khi có khối `on-exhausted:`.
        Assert.NotNull(onExhausted);

        // Assert.False(điều kiện, thông báo): xanh khi trường có chữ; đỏ kèm tên trường bị rỗng.
        Assert.False(string.IsNullOrWhiteSpace(onExhausted!.Who), $"'{serviceDirectoryName}': on-exhausted.who is empty.");
        Assert.False(string.IsNullOrWhiteSpace(onExhausted.Stops), $"'{serviceDirectoryName}': on-exhausted.stops is empty.");
        Assert.False(string.IsNullOrWhiteSpace(onExhausted.Does), $"'{serviceDirectoryName}': on-exhausted.does is empty.");
    }

    /// <summary>
    /// Kiểm tra: `recovery` là 3 ngày đạt SLO liên tục, ngày không traffic tính là đạt, và việc đặt lại
    /// ngân sách đầu tháng KHÔNG gỡ trạng thái đóng băng.
    /// Lý do: FR-010 — điều kiện thoát phải rõ ràng và trùng với logic của rule `error-budget-frozen`.
    /// Task nguồn: spec 027 (chính sách ngân sách lỗi) — FR-010, US1 (bất biến 6).
    /// </summary>
    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_DefinesRecoveryAsThreeDaysMeetingSlo(string serviceDirectoryName)
    {
        var recovery = RequirePolicy(serviceDirectoryName).Recovery;

        // Assert.NotNull(giá trị): xanh khi có khối `recovery:`.
        Assert.NotNull(recovery);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi giá trị khớp nguyên văn contract.
        Assert.Equal("3", recovery!.ConsecutiveDaysMeetingSlo);
        Assert.Equal("true", recovery.NoTrafficDayCountsAsMet);
        Assert.Equal("false", recovery.BudgetResetClearsFreeze);
    }

    /// <summary>
    /// Kiểm tra: khối `error-budget-policy` chỉ chứa các khoá của contract, và mỗi ngân sách chỉ có
    /// `bad-request` và `allowed-bad-ratio` — không có ngưỡng độ trễ nào khai báo lại ở đây.
    /// Lý do: FR-013 — ngưỡng độ trễ chỉ có một nguồn là `slos.latency`; khai báo lại ở đây sớm muộn
    /// sẽ lệch với `slos`. Đọc khoá thô từ YAML vì model bỏ qua khoá lạ (IgnoreUnmatchedProperties).
    /// Task nguồn: spec 027 (chính sách ngân sách lỗi) — FR-013, US1 (bất biến 7).
    /// </summary>
    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_PolicyCarriesNoKeysBeyondTheContract(string serviceDirectoryName)
    {
        var manifest = Load(serviceDirectoryName);
        var root = RootMapping(manifest.FilePath);
        var policyKey = new YamlScalarNode("error-budget-policy");

        // Assert.True(điều kiện, thông báo): xanh khi manifest có khối chính sách.
        Assert.True(root.Children.ContainsKey(policyKey), $"'{serviceDirectoryName}' has no error-budget-policy block.");
        var policy = (YamlMappingNode)root.Children[policyKey];

        // Assert.Equal(kỳ vọng, thực tế): xanh khi tập khoá cấp 1 của khối giống hệt contract.
        Assert.Equal(
            ExpectedPolicyKeys,
            policy.Children.Keys.Select(k => k.ToString()).OrderBy(k => k, StringComparer.Ordinal));

        var budgets = (YamlMappingNode)policy.Children[new YamlScalarNode("budgets")];
        foreach (var (name, budget) in budgets.Children)
        {
            // Assert.Equal(kỳ vọng, thực tế): xanh khi ngân sách chỉ có 2 khoá, không có ngưỡng độ trễ.
            Assert.Equal(
                ExpectedBudgetKeys,
                ((YamlMappingNode)budget).Children.Keys.Select(k => k.ToString()).OrderBy(k => k, StringComparer.Ordinal));
        }
    }

    private static DiscoveredServiceManifest Load(string serviceDirectoryName)
    {
        var discovered = ServiceManifestFixture.DiscoverAll(ServiceManifestFixture.LocateRepositoryRoot());

        // Assert.True(điều kiện, thông báo): xanh khi service có file manifest; đỏ kèm tên service.
        Assert.True(discovered.ContainsKey(serviceDirectoryName), $"'{serviceDirectoryName}' has no service-manifest.yaml.");
        return discovered[serviceDirectoryName];
    }

    private static ErrorBudgetPolicySection RequirePolicy(string serviceDirectoryName)
    {
        var policy = Load(serviceDirectoryName).Document.ErrorBudgetPolicy;

        // Assert.NotNull(giá trị): xanh khi manifest có khối `error-budget-policy:`.
        Assert.NotNull(policy);
        return policy!;
    }

    private static YamlMappingNode RootMapping(string filePath)
    {
        var stream = new YamlStream();
        using var reader = new StreamReader(filePath);
        stream.Load(reader);
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }
}
