using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ServiceManifestSloConventionTests;

/// <summary>
/// Spec 030 (đóng sai lệch Nguyên tắc III của spec 028): rule phát hiện nhanh `incident-fast-detection`
/// đã export (`docs/kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson`) luôn khớp
/// `service-manifest.yaml` — specs/030-incident-and-weekly-dashboards/contracts/incident-fast-detection-rule-contract.md
/// bất biến 1–7. Rule sống trong Kibana, không nằm trong git; file export là bản duy nhất kiểm tra được
/// trong CI, nên mọi lần sửa rule trên UI phải export lại — test này đỏ ngay khi rule và manifest nói
/// hai con số khác nhau. Dùng lại cách đọc `ErrorBudgetRuleDefinitionTests` (027) cho các biểu thức CASE.
/// </summary>
public partial class IncidentFastDetectionRuleDefinitionTests
{
    private const string RuleName = "incident-fast-detection";

    /// <summary>
    /// Kiểm tra: file export có đúng một rule `incident-fast-detection`, loại `.es-query` ES|QL, chạy mỗi `5m`
    /// với cửa sổ rule `5 m`, mang tag `incident-fast-detection`, nhóm theo dòng kết quả (`groupBy = row`) và
    /// dùng `@timestamp` làm trường thời gian.
    /// Lý do: FR-009 (spec 029) và US3 của 028 — chu kỳ khác 5 phút làm "phát hiện trong vòng 5 phút" không
    /// còn đúng; thiếu tag thì panel Phát hiện nhanh trên dashboard lọc theo tag bỏ sót alert; `groupBy` khác
    /// `row` làm alert không tách theo service.
    /// Task nguồn: spec 030 (test canh gác rule 028) — FR-015 (bất biến 1).
    /// </summary>
    [Fact]
    public void Rule_IsExported_EveryFiveMinutes_WithTheTag()
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

        // Assert.Contains(phần tử, tập hợp): xanh khi rule mang tag mà dashboard và QA lọc theo.
        Assert.Contains(RuleName, rule.Tags);
    }

    /// <summary>
    /// Kiểm tra: rule bắn khi có ít nhất một service vi phạm: `thresholdComparator = ">"` và `threshold = [0]`.
    /// Lý do: truy vấn chỉ trả các service đang vi phạm; đổi sang ngưỡng khác 0 thì service đầu tiên vi phạm
    /// không bắn cảnh báo.
    /// Task nguồn: spec 030 (test canh gác rule 028) — FR-015 (bất biến 7).
    /// </summary>
    [Fact]
    public void Rule_AlertsWhenAnyServiceBreaches()
    {
        var parameters = RequireRule().Attributes.GetProperty("params");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi rule bắn khi số dòng kết quả > 0.
        Assert.Equal(">", parameters.GetProperty("thresholdComparator").GetString());
        Assert.Equal([0], parameters.GetProperty("threshold").EnumerateArray().Select(t => t.GetInt32()).ToArray());
    }

    /// <summary>
    /// Kiểm tra: truy vấn chỉ nhìn 5 phút gần nhất: có đúng một điều kiện `WHERE @timestamp > NOW() - 5 minutes`.
    /// Lý do: 028 FR — "5xx ≥ ngưỡng trong 5 phút gần nhất"; cửa sổ dài hơn pha loãng sự cố đang diễn ra,
    /// ngắn hơn bắt nhiễu.
    /// Task nguồn: spec 030 (test canh gác rule 028) — FR-015 (bất biến 2).
    /// </summary>
    [Fact]
    public void Rule_LooksAtTheLastFiveMinutesOnly()
    {
        var esql = Regex.Replace(RequireRule().Esql, @"\s+", " ");

        // Assert.Single(tập hợp): xanh khi truy vấn có đúng một điều kiện cửa sổ 5 phút.
        Assert.Single(LastFiveMinutesFilter().Matches(esql));
    }

    /// <summary>
    /// Kiểm tra: ngưỡng 5xx của rule (`WHERE err_pct >= N OR latency_breach`, tính theo phần trăm) bằng
    /// `slos.error-rate.max-5xx-ratio` của MỌI manifest (ví dụ `1%` → 1).
    /// Lý do: FR-009 (spec 029) — rule bắn khi 5xx chạm đúng SLO đã cam kết; ngưỡng chép tay nên là chỗ dễ trôi
    /// khi SLO đổi (ngưỡng 0.1 cũ so với SLO 1% mới là cảnh báo bắn sớm gấp 10 lần).
    /// Task nguồn: spec 030 (test canh gác rule 028) — FR-015 (bất biến 3).
    /// </summary>
    [Fact]
    public void Rule_FiveXxThresholdMatchesEveryManifest()
    {
        var esql = Regex.Replace(RequireRule().Esql, @"\s+", " ");
        var matches = FiveXxThreshold().Matches(esql);

        // Assert.Single(tập hợp): xanh khi truy vấn có đúng một điều kiện ngưỡng 5xx.
        var match = Assert.Single(matches);
        var thresholdPercent = decimal.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);

        foreach (var manifest in ServiceManifestFixture.DiscoverAll(ServiceManifestFixture.LocateRepositoryRoot()).Values)
        {
            var ratio = manifest.Document.Slos?.ErrorRate?.MaxFiveXxRatio;

            // Assert.True(điều kiện, thông báo): xanh khi manifest khai ngưỡng 5xx dạng `<số>%`.
            Assert.True(ratio is not null && ratio.EndsWith('%'),
                $"'{manifest.ServiceDirectoryName}': max-5xx-ratio '{ratio}' is not in '<n>%' form.");

            // Assert.Equal(kỳ vọng, thực tế): xanh khi ngưỡng trong rule = SLO 5xx của service (phần trăm).
            Assert.Equal(decimal.Parse(ratio![..^1], CultureInfo.InvariantCulture), thresholdPercent);
        }
    }

    /// <summary>
    /// Kiểm tra: với mỗi service trong 7 manifest, ngưỡng độ trễ (nanosecond) mà truy vấn áp cho service đó
    /// (`p95_slo_ns`/`p99_slo_ns` = CASE theo tên service, hoặc giá trị mặc định cuối CASE) bằng đúng
    /// `slos.latency.p95/p99` của manifest × 1 000 000.
    /// Lý do: ngân sách độ trễ phải dùng ngưỡng của chính service (BFF 700/1000ms, Gateway 800/1100ms, còn lại 500/700ms); rule chép tay
    /// ngưỡng nên đây là chỗ dễ trôi dạt nhất khi một manifest đổi SLO.
    /// Task nguồn: spec 030 (test canh gác rule 028) — FR-015 (bất biến 4).
    /// </summary>
    [Fact]
    public void Rule_LatencyThresholdsMatchEveryManifest()
    {
        var esql = RequireRule().Esql;
        var p95 = ErrorBudgetRuleDefinitionTests.ParseCase(esql, "p95_slo_ns");
        var p99 = ErrorBudgetRuleDefinitionTests.ParseCase(esql, "p99_slo_ns");

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
    /// Kiểm tra: `latency_breach` không còn nhánh riêng `CASE(service == "Gateway.Api", false, ...)` — Gateway bị xét độ trễ
    /// như mọi service khác, theo ngưỡng gateway trong manifest (đã được `ThresholdRule_...` ở trên so khớp).
    /// Lý do: trước 2026-10-06 gateway chỉ bị xét 5xx vì ngưỡng 150/500 ms của nó chặt hơn ngưỡng 300/800 ms của BFF mà
    /// mọi request gateway đều đi qua BFF. Sau khi nâng SLO, gateway là 800/1100 ms, cao hơn BFF 700/1000 ms, nên lý do đó
    /// không còn và ngoại lệ bị gỡ để rule khớp manifest.
    /// Task nguồn: spec 030 (test canh gác rule 028) — FR-015 (bất biến 5, đổi ngày 2026-10-06).
    /// </summary>
    [Fact]
    public void Rule_GatewayLatencyIsJudgedLikeEveryOtherService()
    {
        var esql = Regex.Replace(RequireRule().Esql, @"\s+", " ");

        // Assert.DoesNotContain(chuỗi con, chuỗi): xanh khi không còn nhánh riêng nào buộc latency_breach = false cho Gateway.Api.
        Assert.DoesNotContain("service == \"Gateway.Api\", false", esql, StringComparison.Ordinal);

        // Assert.Contains(chuỗi con, chuỗi): xanh khi latency_breach vẫn là phép so p95/p99 với ngưỡng của service.
        Assert.Contains("latency_breach = p95_ns > p95_slo_ns OR p99_ns > p99_slo_ns", esql, StringComparison.Ordinal);
    }

    /// <summary>
    /// Kiểm tra: kết quả ES|QL của rule kết thúc bằng đúng `KEEP service`.
    /// Lý do: Kibana ghép mã alert từ giá trị MỌI cột kết quả; thêm một cột số (vd `err_pct`) là mã alert đổi
    /// sau mỗi lần chạy, alert cũ "recovered" và alert mới mọc ra mỗi 5 phút thay vì giữ active liên tục
    /// (cùng ràng buộc với 027, specs/027-error-budget-alerting/research.md "Ràng buộc 2").
    /// Task nguồn: spec 030 (test canh gác rule 028) — FR-015 (bất biến 6).
    /// </summary>
    [Fact]
    public void Rule_ReturnsOnlyTheAlertIdentityColumn()
    {
        var lastCommand = RequireRule().Esql.Split('|').Last().Trim();

        // Assert.Equal(kỳ vọng, thực tế): xanh khi lệnh cuối của truy vấn chỉ giữ cột định danh alert.
        Assert.Equal("KEEP service", Regex.Replace(lastCommand, @"\s+", " "));
    }

    private static ErrorBudgetRuleDefinitionTests.ExportedRule RequireRule()
    {
        var path = Path.Combine(
            ServiceManifestFixture.LocateRepositoryRoot(),
            "docs", "kibana-quan-sat-he-thong", "alerts", "incident-fast-detection-rule.ndjson");

        // Assert.True(điều kiện, thông báo): xanh khi file export tồn tại.
        Assert.True(File.Exists(path), $"'{path}' does not exist — export the rule (docs/kibana-quan-sat-he-thong/alerts/README.md).");

        var rules = new List<ErrorBudgetRuleDefinitionTests.ExportedRule>();
        foreach (var line in File.ReadLines(path).Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            if (!root.TryGetProperty("type", out var type) || type.GetString() != "alert")
            {
                continue; // connector, export summary line, ...
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

    private static decimal ToNanoseconds(string? milliseconds, string service)
    {
        // Assert.True(điều kiện, thông báo): xanh khi ngưỡng manifest có dạng `<số>ms`.
        Assert.True(milliseconds is not null && milliseconds.EndsWith("ms", StringComparison.Ordinal),
            $"'{service}': latency '{milliseconds}' is not in '<n>ms' form.");
        return decimal.Parse(milliseconds![..^2], CultureInfo.InvariantCulture) * 1_000_000m;
    }

    [GeneratedRegex(@"WHERE @timestamp > NOW\(\) - 5 minutes")]
    private static partial Regex LastFiveMinutesFilter();

    [GeneratedRegex(@"WHERE err_pct >= (?<n>[0-9.]+) OR latency_breach")]
    private static partial Regex FiveXxThreshold();
}
