using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ServiceManifestSloConventionTests;

/// <summary>
/// User Story 2/3 (spec 027, SCRUM-35; tuần lịch và tỷ lệ 1% theo spec 029): bộ rule cảnh báo ngân
/// sách lỗi đã export (`docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson`) luôn khớp
/// `service-manifest.yaml` — specs/029-error-budget-weekly/contracts/error-budget-alert-rules-contract.md
/// bất biến 1–4, 1b, 11–13. Rule sống trong Kibana, không nằm
/// trong git; file export là bản duy nhất có thể kiểm tra trong CI, nên mọi lần sửa rule trên UI phải
/// export lại — test này đỏ ngay khi rule và manifest nói hai con số khác nhau.
/// Hình dạng ES|QL mà test dựa vào (các dòng `EVAL p95_ns = CASE(...)`, `allowed = CASE(...)`,
/// `WHERE consumed_pct >= N`) được mô tả ở `docs/kibana-quan-sat-he-thong/07-canh-bao-ngan-sach-loi.md`.
/// </summary>
public partial class ErrorBudgetRuleDefinitionTests
{
    private const string RuleTag = "slo-error-budget";

    /// <summary>Tên rule mốc → mốc so sánh, đúng bảng "Bộ rule" của contract.</summary>
    private static readonly Dictionary<string, int> ThresholdRules = new Dictionary<string, int>
    {
        ["error-budget-50"] = 50,
        ["error-budget-75"] = 75,
        ["error-budget-100"] = 100,
    };

    /// <summary>Tỷ lệ request xấu cho phép theo ngân sách — contract bất biến 4 (spec 029: khả dụng/5xx 0.01).</summary>
    private static readonly Dictionary<string, decimal> AllowedBadRatios = new Dictionary<string, decimal>
    {
        ["availability"] = 0.01m,
        ["error-rate"] = 0.01m,
        ["latency-p95"] = 0.05m,
        ["latency-p99"] = 0.01m,
    };

    /// <summary>
    /// Kiểm tra: file export có đủ 3 rule mốc `error-budget-50/75/100`, mỗi rule chạy mỗi `5m` và mang
    /// tag `slo-error-budget`.
    /// Lý do: FR-006 — thiếu một rule là thiếu hẳn một mốc cảnh báo; chu kỳ khác 5 phút làm SC-002
    /// ("bắn trong vòng một chu kỳ") không còn đúng; thiếu tag thì panel dashboard lọc theo tag bỏ sót.
    /// Task nguồn: spec 027 (chính sách ngân sách lỗi) — FR-006, US2 (bất biến 1).
    /// </summary>
    [Theory]
    [InlineData("error-budget-50")]
    [InlineData("error-budget-75")]
    [InlineData("error-budget-100")]
    public void ThresholdRule_IsExported_EveryFiveMinutes_WithTheTag(string ruleName)
    {
        var rule = RequireRule(ruleName);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi rule chạy mỗi 5 phút.
        Assert.Equal("5m", rule.Interval);

        // Assert.Contains(phần tử, tập hợp): xanh khi rule mang tag mà dashboard lọc theo.
        Assert.Contains(RuleTag, rule.Tags);
    }

    /// <summary>
    /// Kiểm tra: mỗi rule mốc lọc đúng `WHERE consumed_pct >= &lt;mốc của rule&gt;`.
    /// Lý do: FR-006 — tên rule nói 75% nhưng truy vấn lọc 70% là cảnh báo bắn sớm mà không ai biết.
    /// Task nguồn: spec 027 (chính sách ngân sách lỗi) — FR-006, US2 (bất biến 2).
    /// </summary>
    [Theory]
    [InlineData("error-budget-50")]
    [InlineData("error-budget-75")]
    [InlineData("error-budget-100")]
    public void ThresholdRule_FiltersOnItsOwnThreshold(string ruleName)
    {
        var thresholds = ThresholdFilter().Matches(RequireRule(ruleName).Esql)
            .Select(m => int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture))
            .ToList();

        // Assert.Equal(kỳ vọng, thực tế): xanh khi truy vấn có đúng một điều kiện mốc và đúng mốc của rule.
        Assert.Equal([ThresholdRules[ruleName]], thresholds);
    }

    /// <summary>
    /// Kiểm tra: với mỗi service trong 7 manifest, ngưỡng độ trễ (nanosecond) mà truy vấn của mỗi rule
    /// mốc áp cho service đó (`p95_ns`/`p99_ns` = CASE theo tên service, hoặc giá trị mặc định cuối
    /// CASE) bằng đúng `slos.latency.p95/p99` của manifest × 1 000 000.
    /// Lý do: FR-013 — ngân sách độ trễ phải dùng ngưỡng của chính service (BFF 700/1000ms, Gateway 800/1100ms, còn lại
    /// 500/700ms); rule chép tay ngưỡng nên đây là chỗ dễ trôi dạt nhất khi một manifest đổi SLO.
    /// Task nguồn: spec 027 (chính sách ngân sách lỗi) — FR-013, US2 (bất biến 3).
    /// </summary>
    [Theory]
    [InlineData("error-budget-50")]
    [InlineData("error-budget-75")]
    [InlineData("error-budget-100")]
    public void ThresholdRule_LatencyThresholdsMatchEveryManifest(string ruleName)
    {
        var esql = RequireRule(ruleName).Esql;
        var p95 = ParseCase(esql, "p95_ns");
        var p99 = ParseCase(esql, "p99_ns");

        foreach (var manifest in ServiceManifestFixture.DiscoverAll(ServiceManifestFixture.LocateRepositoryRoot()).Values)
        {
            // Tên service trong telemetry là tên thư mục project (vd `Bff.Api`), = resource.attributes.service.name.
            var telemetryName = Path.GetFileName(Path.GetDirectoryName(manifest.FilePath))!;
            var latency = manifest.Document.Slos?.Latency;

            // Assert.Equal(kỳ vọng, thực tế): xanh khi ngưỡng trong rule = ngưỡng manifest đổi sang ns.
            Assert.Equal(ToNanoseconds(latency?.P95, manifest.ServiceDirectoryName), p95.Resolve(telemetryName));
            Assert.Equal(ToNanoseconds(latency?.P99, manifest.ServiceDirectoryName), p99.Resolve(telemetryName));
        }
    }

    /// <summary>
    /// Kiểm tra: `allowed = CASE(budget == ..., tỷ lệ, ...)` trong truy vấn của mỗi rule mốc ánh xạ đúng
    /// `availability`/`error-rate` → 0.01, `latency-p95` → 0.05, `latency-p99` → 0.01, không thừa không thiếu.
    /// Lý do: FR-003 (spec 029) — tỷ lệ cho phép là mẫu số của mức tiêu hao; sai một chữ số là mức tiêu
    /// hao sai 10 lần (đúng khoảng cách giữa 0.001 cũ và 0.01 mới).
    /// Task nguồn: spec 029 (ngân sách lỗi theo tuần lịch) — FR-003, US2 (bất biến 4).
    /// </summary>
    [Theory]
    [InlineData("error-budget-50")]
    [InlineData("error-budget-75")]
    [InlineData("error-budget-100")]
    public void ThresholdRule_AllowedRatiosMatchTheContract(string ruleName)
    {
        var allowed = ParseCase(RequireRule(ruleName).Esql, "allowed");

        // Assert.Null(giá trị): xanh khi CASE không có nhánh mặc định — một ngân sách lạ không được âm thầm nhận tỷ lệ nào.
        Assert.Null(allowed.Default);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bảng ngân sách → tỷ lệ giống hệt contract.
        Assert.Equal(
            AllowedBadRatios.OrderBy(p => p.Key, StringComparer.Ordinal),
            allowed.Branches.OrderBy(p => p.Key, StringComparer.Ordinal));
    }

    /// <summary>
    /// Kiểm tra: ES|QL của mỗi rule mốc có đúng một điều kiện
    /// `WHERE @timestamp >= DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours` và không còn `1 month`.
    /// Lý do: FR-001/FR-006 (spec 029) — mức tiêu hao chỉ tính request từ thứ Hai 00:00 giờ Việt Nam
    /// của tuần hiện tại; biểu thức này đã kiểm chứng ra đúng thứ Hai 00:00 UTC+7 trên Elasticsearch
    /// 9.4.4 (research.md V1). Còn sót `1 month` là rule vẫn tính theo tháng của 027.
    /// Task nguồn: spec 029 (ngân sách lỗi theo tuần lịch) — FR-001, FR-006, US2 (bất biến 11).
    /// </summary>
    [Theory]
    [InlineData("error-budget-50")]
    [InlineData("error-budget-75")]
    [InlineData("error-budget-100")]
    public void ThresholdRule_StartsAtMondayMidnightVietnamTime(string ruleName)
    {
        var esql = Regex.Replace(RequireRule(ruleName).Esql, @"\s+", " ");

        // Assert.Single(tập hợp): xanh khi truy vấn có đúng một điều kiện đầu tuần UTC+7.
        Assert.Single(WeekStartFilter().Matches(esql));

        // Assert.DoesNotContain(chuỗi con, chuỗi): xanh khi không còn ranh giới tháng của 027.
        Assert.DoesNotContain("1 month", esql, StringComparison.Ordinal);
    }

    /// <summary>
    /// Kiểm tra: mỗi rule mốc có cửa sổ rule `timeWindowSize = 7`, `timeWindowUnit = d`.
    /// Lý do: FR-006 (spec 029) — Kibana tự lọc `@timestamp` theo cửa sổ rule trước khi chạy ES|QL; một
    /// tuần lịch dài tối đa 6 ngày 23 giờ 59 phút tính từ thứ Hai 00:00, nên 7 ngày đủ phủ đầu tuần
    /// (research.md V2), còn cửa sổ ngắn hơn sẽ cắt hụt request đầu tuần mà không ai biết.
    /// Task nguồn: spec 029 (ngân sách lỗi theo tuần lịch) — FR-006, US2 (bất biến 12).
    /// </summary>
    [Theory]
    [InlineData("error-budget-50")]
    [InlineData("error-budget-75")]
    [InlineData("error-budget-100")]
    public void ThresholdRule_LooksBackSevenDays(string ruleName)
    {
        var parameters = RequireRule(ruleName).Attributes.GetProperty("params");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi cửa sổ rule đúng 7 ngày.
        Assert.Equal(7, parameters.GetProperty("timeWindowSize").GetInt32());
        Assert.Equal("d", parameters.GetProperty("timeWindowUnit").GetString());
    }

    /// <summary>
    /// Kiểm tra: kết quả ES|QL của mỗi rule mốc kết thúc bằng đúng `KEEP service, budget`.
    /// Lý do: FR-007 — Kibana ghép mã alert từ giá trị MỌI cột kết quả; thêm một cột số (vd
    /// `consumed_pct`) là mã alert đổi sau mỗi lần chạy, alert cũ "recovered" và alert mới mọc ra mỗi 5
    /// phút thay vì giữ active liên tục (specs/027-error-budget-alerting/research.md "Ràng buộc 2", đã xác minh trên Kibana 9.4.4 thật).
    /// Task nguồn: spec 027 (chính sách ngân sách lỗi) — FR-007, US2 (bất biến 1b).
    /// </summary>
    [Theory]
    [InlineData("error-budget-50")]
    [InlineData("error-budget-75")]
    [InlineData("error-budget-100")]
    public void ThresholdRule_ReturnsOnlyTheAlertIdentityColumns(string ruleName)
    {
        var lastCommand = RequireRule(ruleName).Esql.Split('|').Last().Trim();

        // Assert.Equal(kỳ vọng, thực tế): xanh khi lệnh cuối của truy vấn chỉ giữ 2 cột định danh alert.
        Assert.Equal("KEEP service, budget", Regex.Replace(lastCommand, @"\s+", " "));
    }

    /// <summary>
    /// Kiểm tra: file export có rule `error-budget-frozen` chạy mỗi `5m`, tag `slo-error-budget`, và kết
    /// quả ES|QL chỉ giữ cột `service`.
    /// Lý do: FR-010/FR-011 — trạng thái "cạn ngân sách — ưu tiên độ tin cậy" chỉ tồn tại nếu rule này có
    /// mặt; giữ thêm cột số là mã alert đổi mỗi lần chạy (specs/027-error-budget-alerting/research.md "Ràng buộc 2").
    /// Task nguồn: spec 027 (chính sách ngân sách lỗi) — FR-010, FR-011, US3 (bất biến 1, 1b).
    /// </summary>
    [Fact]
    public void FrozenRule_IsExported_EveryFiveMinutes_KeepingOnlyTheService()
    {
        var rule = RequireRule("error-budget-frozen");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi rule chạy mỗi 5 phút.
        Assert.Equal("5m", rule.Interval);

        // Assert.Contains(phần tử, tập hợp): xanh khi rule mang tag mà dashboard lọc theo.
        Assert.Contains(RuleTag, rule.Tags);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi lệnh cuối của truy vấn chỉ giữ cột định danh alert.
        Assert.Equal("KEEP service", Regex.Replace(rule.Esql.Split('|').Last().Trim(), @"\s+", " "));
    }

    /// <summary>
    /// Kiểm tra: rule `error-budget-frozen` có cửa sổ rule `timeWindowSize = 14`, `timeWindowUnit = d`.
    /// Lý do: FR-008 (spec 029) — rule đóng băng không có `WHERE @timestamp`, chỉ thấy sự kiện "cạn" và
    /// ngày xấu nằm trong cửa sổ rule; 14 ngày (2 tuần) là con số người dùng chốt. Đóng băng kéo dài hơn
    /// 14 ngày có thể tự mất — giới hạn đã biết, ghi ở technical-debt.
    /// Task nguồn: spec 029 (ngân sách lỗi theo tuần lịch) — FR-008, US3 (bất biến 12).
    /// </summary>
    [Fact]
    public void FrozenRule_LooksBackFourteenDays()
    {
        var parameters = RequireRule("error-budget-frozen").Attributes.GetProperty("params");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi cửa sổ rule đúng 14 ngày.
        Assert.Equal(14, parameters.GetProperty("timeWindowSize").GetInt32());
        Assert.Equal("d", parameters.GetProperty("timeWindowUnit").GetString());
    }

    /// <summary>
    /// Kiểm tra: điều kiện "ngày không đạt SLO" (`day_missed_slo`) của rule `error-budget-frozen` so 5xx
    /// bằng `>= <tỷ lệ error-rate>`, vượt p95 bằng `> <tỷ lệ latency-p95>`, vượt p99 bằng
    /// `> <tỷ lệ latency-p99>` — ba hằng số lấy từ `AllowedBadRatios`, không chép tay.
    /// Lý do: FR-008 (spec 029) — hồi phục cần 3 ngày đạt SLO; ngưỡng ngày phải bằng SLO mới (5xx dưới
    /// 1%), nếu còn 0.001 thì một ngày 5xx 0.5% vẫn bị tính là xấu và service không bao giờ hồi phục
    /// đúng hạn.
    /// Task nguồn: spec 029 (ngân sách lỗi theo tuần lịch) — FR-008, US3 (bất biến 13).
    /// </summary>
    [Fact]
    public void FrozenRule_DailySloThresholdsMatchTheBudgets()
    {
        var esql = Regex.Replace(RequireRule("error-budget-frozen").Esql, @"\s+", " ");
        var match = Regex.Match(esql, @"EVAL day_missed_slo = CASE\((?<body>.*?)\) \|");

        // Assert.True(điều kiện, thông báo): xanh khi truy vấn có biểu thức `day_missed_slo`.
        Assert.True(match.Success, "error-budget-frozen ES|QL has no 'EVAL day_missed_slo = CASE(...)'.");
        var body = match.Groups["body"].Value;

        // Assert.Contains(chuỗi con, chuỗi): xanh khi mỗi ngưỡng ngày bằng đúng tỷ lệ ngân sách tương ứng.
        Assert.Contains($"TO_DOUBLE(bad_5xx) / spans >= {Format(AllowedBadRatios["error-rate"])}", body, StringComparison.Ordinal);
        Assert.Contains($"TO_DOUBLE(bad_p95) / spans > {Format(AllowedBadRatios["latency-p95"])}", body, StringComparison.Ordinal);
        Assert.Contains($"TO_DOUBLE(bad_p99) / spans > {Format(AllowedBadRatios["latency-p99"])}", body, StringComparison.Ordinal);
    }

    private static string Format(decimal ratio) => ratio.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Kiểm tra: rule `error-budget-100` có đúng một action loại Index (`.index`), chạy theo từng alert khi
    /// alert đổi trạng thái (`onActionGroupChange`), và file export có connector Index ghi vào
    /// `slo-error-budget-events`.
    /// Lý do: FR-010 — `error-budget-frozen` suy ra lần cạn gần nhất từ index này; action chạy mỗi lần
    /// kiểm tra (thay vì chỉ khi đổi trạng thái) sẽ đẩy `exhausted_at` về sau liên tục và service không bao
    /// giờ hồi phục.
    /// Task nguồn: spec 027 (chính sách ngân sách lỗi) — FR-010, US3 (data-model mục 4).
    /// </summary>
    [Fact]
    public void ExhaustionRule_WritesAnEventOnlyWhenAnAlertBecomesActive()
    {
        var rule = RequireRule("error-budget-100");
        var actions = rule.Attributes.GetProperty("actions").EnumerateArray().ToList();

        // Assert.Single(tập hợp): xanh khi rule 100 có đúng một action.
        var action = Assert.Single(actions);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi action là connector Index, theo từng alert, khi đổi trạng thái.
        Assert.Equal(".index", action.GetProperty("actionTypeId").GetString());
        var frequency = action.GetProperty("frequency");
        Assert.False(frequency.GetProperty("summary").GetBoolean());
        Assert.Equal("onActionGroupChange", frequency.GetProperty("notifyWhen").GetString());

        var connectorIndexes = LoadExportedConnectors()
            .Where(c => c.GetProperty("attributes").GetProperty("actionTypeId").GetString() == ".index")
            .Select(c => c.GetProperty("attributes").GetProperty("config").GetProperty("index").GetString())
            .ToList();

        // Assert.Equal(kỳ vọng, thực tế): xanh khi file export có đúng một connector Index trỏ vào index sự kiện.
        Assert.Equal(["slo-error-budget-events"], connectorIndexes);
    }

    internal static IReadOnlyList<JsonElement> LoadExportedConnectors()
    {
        var path = Path.Combine(
            ServiceManifestFixture.LocateRepositoryRoot(),
            "docs", "kibana-quan-sat-he-thong", "alerts", "error-budget-rules.ndjson");
        var connectors = new List<JsonElement>();
        foreach (var line in File.ReadLines(path).Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            using var json = JsonDocument.Parse(line);
            if (json.RootElement.TryGetProperty("type", out var type) && type.GetString() == "action")
            {
                connectors.Add(json.RootElement.Clone());
            }
        }

        return connectors;
    }

    internal sealed record ExportedRule(string Name, string Interval, IReadOnlyList<string> Tags, string Esql, JsonElement Attributes);

    /// <summary>Một biểu thức `name = CASE(x == "k1", v1, x == "k2", v2, ..., [mặc định])` đã tách nhánh.</summary>
    internal sealed record CaseExpression(IReadOnlyDictionary<string, decimal> Branches, decimal? Default)
    {
        public decimal? Resolve(string key) => Branches.TryGetValue(key, out var value) ? value : Default;
    }

    internal static IReadOnlyList<ExportedRule> LoadExportedRules()
    {
        var path = Path.Combine(
            ServiceManifestFixture.LocateRepositoryRoot(),
            "docs", "kibana-quan-sat-he-thong", "alerts", "error-budget-rules.ndjson");

        // Assert.True(điều kiện, thông báo): xanh khi file export tồn tại.
        Assert.True(File.Exists(path), $"'{path}' does not exist — export the rules (docs/kibana-quan-sat-he-thong/alerts/README.md).");

        var rules = new List<ExportedRule>();
        foreach (var line in File.ReadLines(path).Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            if (!root.TryGetProperty("type", out var type) || type.GetString() != "alert")
            {
                continue; // connector, export summary line, ...
            }

            var attributes = root.GetProperty("attributes");
            rules.Add(new ExportedRule(
                attributes.GetProperty("name").GetString()!,
                attributes.GetProperty("schedule").GetProperty("interval").GetString()!,
                attributes.GetProperty("tags").EnumerateArray().Select(t => t.GetString()!).ToList(),
                attributes.GetProperty("params").GetProperty("esqlQuery").GetProperty("esql").GetString()!,
                attributes.Clone()));
        }

        return rules;
    }

    internal static ExportedRule RequireRule(string ruleName)
    {
        var matches = LoadExportedRules().Where(r => r.Name == ruleName).ToList();

        // Assert.Single(tập hợp): xanh khi file export có đúng một rule tên này.
        return Assert.Single(matches);
    }

    internal static CaseExpression ParseCase(string esql, string variable)
    {
        var match = Regex.Match(esql, $@"\b{Regex.Escape(variable)}\s*=\s*CASE\((?<body>[^)]*)\)", RegexOptions.Singleline);

        // Assert.True(điều kiện, thông báo): xanh khi truy vấn có biểu thức `<variable> = CASE(...)`.
        Assert.True(match.Success, $"ES|QL has no '{variable} = CASE(...)' expression.");

        var branches = new Dictionary<string, decimal>();
        foreach (Match branch in CaseBranch().Matches(match.Groups["body"].Value))
        {
            branches[branch.Groups["key"].Value] = decimal.Parse(branch.Groups["value"].Value, CultureInfo.InvariantCulture);
        }

        var tail = CaseBranch().Replace(match.Groups["body"].Value, string.Empty).Trim().Trim(',').Trim();
        decimal? fallback = tail.Length == 0 ? null : decimal.Parse(tail, CultureInfo.InvariantCulture);
        return new CaseExpression(branches, fallback);
    }

    private static decimal ToNanoseconds(string? milliseconds, string service)
    {
        // Assert.True(điều kiện, thông báo): xanh khi ngưỡng manifest có dạng `<số>ms`.
        Assert.True(milliseconds is not null && milliseconds.EndsWith("ms", StringComparison.Ordinal),
            $"'{service}': latency '{milliseconds}' is not in '<n>ms' form.");
        return decimal.Parse(milliseconds![..^2], CultureInfo.InvariantCulture) * 1_000_000m;
    }

    [GeneratedRegex(@"WHERE @timestamp >= DATE_TRUNC\(1 week, NOW\(\) \+ 7 hours\) - 7 hours")]
    private static partial Regex WeekStartFilter();

    [GeneratedRegex(@"WHERE\s+consumed_pct\s*>=\s*(?<n>\d+)")]
    private static partial Regex ThresholdFilter();

    [GeneratedRegex(@"\w+\s*==\s*""(?<key>[^""]+)""\s*,\s*(?<value>[0-9.]+)\s*,?")]
    private static partial Regex CaseBranch();
}
