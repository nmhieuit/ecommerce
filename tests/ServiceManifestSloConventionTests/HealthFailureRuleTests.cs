using System.Text.Json;
using System.Text.RegularExpressions;

namespace ServiceManifestSloConventionTests;

/// <summary>
/// Spec 033: rule cảnh báo `health-failure` đã export (`docs/kibana-quan-sat-he-thong/alerts/health-failure-rule.ndjson`)
/// luôn khớp contract `specs/033-exclude-health-spans/contracts/health-failure-rule-contract.md` bất biến 1–8 và tiền tố
/// health khai báo trong `error-budget-policy.excluded-path-prefixes` của manifest. Health đã bị loại khỏi ngân sách nên rule
/// này là tín hiệu duy nhất cho "service không sẵn sàng": nó chỉ tính span health trả 5xx, không đụng ngân sách. Rule sống
/// trong Kibana, không nằm trong git; file export là bản duy nhất kiểm tra được trong CI.
/// </summary>
public partial class HealthFailureRuleTests
{
    private const string RuleName = "health-failure";

    /// <summary>
    /// Kiểm tra: file export có đúng một rule `health-failure`, loại `.es-query` ES|QL, chạy mỗi `5m`, cửa sổ rule `5 m`,
    /// mang tag `health-failure` (không dùng tag `slo-error-budget`), nhóm theo dòng kết quả, dùng `@timestamp`.
    /// Lý do: FR-009 — chu kỳ khác 5 phút làm "báo trong vòng một chu kỳ đánh giá" không còn đúng; dùng tag của ngân sách
    /// thì panel/QA lọc theo `slo-error-budget` nhặt nhầm rule này; `groupBy` khác `row` làm alert không tách theo service.
    /// Task nguồn: spec 033 (loại span health khỏi ngân sách lỗi) — FR-009, US3 (bất biến 1).
    /// </summary>
    [Fact]
    public void Rule_IsExported_EveryFiveMinutes_WithItsOwnTag()
    {
        var rule = RequireRule();
        var parameters = rule.Attributes.GetProperty("params");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi rule là loại ES|QL của Kibana, chạy mỗi 5 phút, cửa sổ 5 phút.
        Assert.Equal(".es-query", rule.Attributes.GetProperty("alertTypeId").GetString());
        Assert.Equal("5m", rule.Interval);
        Assert.Equal(5, parameters.GetProperty("timeWindowSize").GetInt32());
        Assert.Equal("m", parameters.GetProperty("timeWindowUnit").GetString());

        // Assert.Equal(kỳ vọng, thực tế): xanh khi alert tách theo từng dòng kết quả và theo trường thời gian chuẩn.
        Assert.Equal("esqlQuery", parameters.GetProperty("searchType").GetString());
        Assert.Equal("row", parameters.GetProperty("groupBy").GetString());
        Assert.Equal("@timestamp", parameters.GetProperty("timeField").GetString());

        // Assert.Contains/DoesNotContain: xanh khi rule mang tag riêng và không mang tag của ngân sách.
        Assert.Contains(RuleName, rule.Tags);
        Assert.DoesNotContain("slo-error-budget", rule.Tags);
    }

    /// <summary>
    /// Kiểm tra: rule bắn khi có ít nhất một service vi phạm (`thresholdComparator = ">"`, `threshold = [0]`) và không
    /// có action/connector nào (không ghi vào index sự kiện ngân sách).
    /// Lý do: truy vấn chỉ trả các service đang vi phạm; ngưỡng khác 0 làm service đầu tiên không bắn. Action ghi vào
    /// `slo-error-budget-events` sẽ làm health lỗi bị coi là "cạn ngân sách" — đúng điều spec này loại bỏ.
    /// Task nguồn: spec 033 (loại span health khỏi ngân sách lỗi) — FR-009, US3 (bất biến 7, 8).
    /// </summary>
    [Fact]
    public void Rule_AlertsWhenAnyServiceBreaches_AndHasNoActions()
    {
        var rule = RequireRule();
        var parameters = rule.Attributes.GetProperty("params");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi rule bắn khi số dòng kết quả > 0.
        Assert.Equal(">", parameters.GetProperty("thresholdComparator").GetString());
        Assert.Equal([0], parameters.GetProperty("threshold").EnumerateArray().Select(t => t.GetInt32()).ToArray());

        // Assert.Empty(tập hợp): xanh khi rule không có action nào.
        Assert.Empty(rule.Attributes.GetProperty("actions").EnumerateArray());
    }

    /// <summary>
    /// Kiểm tra: truy vấn chỉ nhìn 5 phút gần nhất: có đúng một điều kiện `@timestamp > NOW() - 5 minutes`.
    /// Lý do: điều kiện bắn là "từ 50% span health trong 5 phút"; cửa sổ dài hơn làm lỗi thoáng qua lúc khởi động bị
    /// tính, ngắn hơn làm tỷ lệ nhảy theo từng span.
    /// Task nguồn: spec 033 (loại span health khỏi ngân sách lỗi) — FR-009, US3 (bất biến 2).
    /// </summary>
    [Fact]
    public void Rule_LooksAtTheLastFiveMinutesOnly()
    {
        var esql = Regex.Replace(RequireRule().Esql, @"\s+", " ");

        // Assert.Single(tập hợp): xanh khi truy vấn có đúng một điều kiện cửa sổ 5 phút.
        Assert.Single(LastFiveMinutesFilter().Matches(esql));
    }

    /// <summary>
    /// Kiểm tra: rule CHỈ tính span có đường dẫn bắt đầu bằng tiền tố health khai báo trong manifest
    /// (`COALESCE(attributes.url.path, "") LIKE "<tiền tố>*"`, đúng một lần cho mỗi tiền tố) và KHÔNG đảo điều kiện thành
    /// `NOT (...)` (đó là điều kiện loại của rule ngân sách).
    /// Lý do: FR-009 — đảo nhầm sẽ biến rule thành "5xx của request nghiệp vụ", trùng rule ngân sách và hết báo health lỗi.
    /// Tiền tố đọc từ manifest, không hard-code, để đổi tiền tố ở một nơi làm test đỏ nơi còn lại.
    /// Task nguồn: spec 033 (loại span health khỏi ngân sách lỗi) — FR-009, US3 (bất biến 3).
    /// </summary>
    [Fact]
    public void Rule_CountsOnlyTheManifestDeclaredHealthPrefixes()
    {
        var esql = Regex.Replace(RequireRule().Esql, @"\s+", " ");

        foreach (var prefix in ExpectedPrefixes())
        {
            var include = $"COALESCE(attributes.url.path, \"\") LIKE \"{prefix}*\"";

            // Assert.Equal(kỳ vọng, thực tế): xanh khi điều kiện lấy-health có đúng một lần cho mỗi tiền tố.
            Assert.Equal(1, Regex.Count(esql, Regex.Escape(include)));

            // Assert.DoesNotContain(chuỗi con, chuỗi): xanh khi không có dạng đảo `NOT (COALESCE(...) LIKE ...)`.
            Assert.DoesNotContain($"NOT ({include})", esql, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Kiểm tra: rule chỉ đếm span trả 5xx (`attributes.http.response.status_code >= 500`) và không tham chiếu độ trễ
    /// (`duration`).
    /// Lý do: người dùng chốt chỉ báo "health lỗi" chỉ tính 5xx, không tính health chậm — health chậm lúc khởi động nguội
    /// chính là nhiễu đã làm ngân sách vọt; tính cả độ trễ sẽ đưa nhiễu đó trở lại thành cảnh báo.
    /// Task nguồn: spec 033 (loại span health khỏi ngân sách lỗi) — FR-009, US3 (bất biến 4).
    /// </summary>
    [Fact]
    public void Rule_CountsOnlyFiveXx_NotSlowness()
    {
        var esql = RequireRule().Esql;

        // Assert.Contains(chuỗi con, chuỗi): xanh khi rule đếm 5xx.
        Assert.Contains("attributes.http.response.status_code >= 500", esql, StringComparison.Ordinal);

        // Assert.DoesNotContain(chuỗi con, chuỗi): xanh khi rule không nhìn độ trễ.
        Assert.DoesNotContain("duration", esql, StringComparison.Ordinal);
    }

    /// <summary>
    /// Kiểm tra: điều kiện bắn là `health_5xx_pct >= 50` (từ 50% span health của service trả 5xx) và kết quả cuối chỉ giữ
    /// đúng cột `service`.
    /// Lý do: 50% là con số người dùng chốt (bắt service không sẵn sàng kéo dài, bỏ qua vài 5xx thoáng qua lúc khởi động);
    /// giữ thêm cột số là mã alert đổi sau mỗi lần chạy, alert cũ "recovered" và alert mới mọc ra mỗi 5 phút (cùng ràng buộc
    /// của rule 027/028, specs/027-error-budget-alerting/research.md "Ràng buộc 2").
    /// Task nguồn: spec 033 (loại span health khỏi ngân sách lỗi) — FR-009, US3 (bất biến 5, 6).
    /// </summary>
    [Fact]
    public void Rule_FiresAtFiftyPercent_AndKeepsOnlyTheServiceColumn()
    {
        var esql = Regex.Replace(RequireRule().Esql, @"\s+", " ");
        var thresholds = HealthPercentThreshold().Matches(esql);

        // Assert.Single(tập hợp): xanh khi truy vấn có đúng một điều kiện ngưỡng; giá trị đúng 50.
        var match = Assert.Single(thresholds);
        Assert.Equal("50", match.Groups["n"].Value);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi lệnh cuối của truy vấn chỉ giữ cột định danh alert.
        Assert.Equal("KEEP service", RequireRule().Esql.Split('|').Last().Trim());
    }

    private static IReadOnlyList<string> ExpectedPrefixes()
    {
        var prefixSets = ServiceManifestFixture.DiscoverAll(ServiceManifestFixture.LocateRepositoryRoot()).Values
            .Select(m => (IReadOnlyList<string>)(m.Document.ErrorBudgetPolicy?.ExcludedPathPrefixes ?? []))
            .ToList();

        // Assert.NotEmpty(tập hợp): xanh khi có manifest để đọc.
        Assert.NotEmpty(prefixSets);

        // Assert.All(tập hợp, kiểm tra): xanh khi mọi manifest khai cùng danh sách tiền tố.
        Assert.All(prefixSets, set => Assert.Equal(prefixSets[0], set));

        // Assert.NotEmpty(tập hợp): xanh khi manifest thực sự khai tiền tố.
        Assert.NotEmpty(prefixSets[0]);
        return prefixSets[0];
    }

    private static ErrorBudgetRuleDefinitionTests.ExportedRule RequireRule()
    {
        var path = Path.Combine(
            ServiceManifestFixture.LocateRepositoryRoot(),
            "docs", "kibana-quan-sat-he-thong", "alerts", "health-failure-rule.ndjson");

        // Assert.True(điều kiện, thông báo): xanh khi file export tồn tại.
        Assert.True(File.Exists(path), $"'{path}' does not exist — export the rule (docs/kibana-quan-sat-he-thong/alerts/README.md).");

        var rules = new List<ErrorBudgetRuleDefinitionTests.ExportedRule>();
        foreach (var line in File.ReadLines(path).Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            if (!root.TryGetProperty("type", out var type) || type.GetString() != "alert")
            {
                continue; // dòng tóm tắt export, connector, ...
            }

            var attributes = root.GetProperty("attributes");
            if (attributes.GetProperty("name").GetString() != RuleName)
            {
                continue;
            }

            rules.Add(new ErrorBudgetRuleDefinitionTests.ExportedRule(
                attributes.GetProperty("name").GetString()!,
                attributes.GetProperty("schedule").GetProperty("interval").GetString()!,
                attributes.GetProperty("tags").EnumerateArray().Select(t => t.GetString()!).ToList(),
                attributes.GetProperty("params").GetProperty("esqlQuery").GetProperty("esql").GetString()!,
                attributes.Clone()));
        }

        // Assert.Single(tập hợp): xanh khi file export có đúng một rule tên này.
        return Assert.Single(rules);
    }

    [GeneratedRegex(@"@timestamp > NOW\(\) - 5 minutes")]
    private static partial Regex LastFiveMinutesFilter();

    [GeneratedRegex(@"WHERE health_5xx_pct >= (?<n>[0-9.]+)")]
    private static partial Regex HealthPercentThreshold();
}
