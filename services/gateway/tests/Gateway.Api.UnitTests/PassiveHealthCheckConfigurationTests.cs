using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Gateway.Api.UnitTests;

/// <summary>
/// Spec 020 (timeout/retry/circuit breaker) FR-002/FR-003 (research.md Decision 3): lời gọi forward
/// của gateway tới BFF có timeout (<see cref="ForwardingTimeoutBudgetTests"/>) nhưng, trước tính năng
/// này, KHÔNG có circuit breaker — BFF sập nghĩa là mọi request cứ thử kết nối thật và chờ hết
/// <c>ActivityTimeout</c> thay vì fail fast. Passive health check của YARP là cơ chế đóng khoảng trống
/// đó mà không phải gắn 1 Polly pipeline vào chính reverse proxy (các phương án đã loại ở
/// research.md Decision 3).
/// </summary>
public class PassiveHealthCheckConfigurationTests
{
    private const string RepositoryRootMarker = "Ecommerce.slnx";

    /// <summary>
    /// Kiểm tra: `appsettings.json` của gateway bật `HealthCheck:Passive:Enabled = true` cho
    /// `bff-cluster`.
    /// Lý do: FR-002 — không bật thì 1 BFF liên tục lỗi không có circuit breaker nào, mọi request cứ
    /// thử kết nối thật.
    /// Task nguồn: spec 020 (timeout/retry/circuit breaker) — FR-002, research.md Decision 3.
    /// </summary>
    [Fact]
    public void BffCluster_HasPassiveHealthCheckEnabled()
    {
        var passive = ReadPassiveHealthCheck();

        // Assert.True(điều kiện, thông báo): xanh khi khoá `Enabled` tồn tại và bằng true; đỏ kèm
        // thông báo khi thiếu hoặc false.
        Assert.True(
            passive.RootElement.TryGetProperty("Enabled", out var enabled) && enabled.GetBoolean(),
            "bff-cluster declares no enabled HealthCheck:Passive policy — a repeatedly failing BFF "
            + "has no circuit breaker and every request keeps attempting a real connection.");
    }

    /// <summary>
    /// Kiểm tra: `HealthCheck:Passive` khai báo 1 `Policy` không rỗng.
    /// Lý do: FR-002 — thiếu `Policy` thì YARP không có ngưỡng nào để đánh giá lỗi.
    /// Task nguồn: spec 020 (timeout/retry/circuit breaker) — FR-002.
    /// </summary>
    [Fact]
    public void BffCluster_DeclaresAPassiveHealthCheckPolicy()
    {
        var passive = ReadPassiveHealthCheck();

        var policy = passive.RootElement.TryGetProperty("Policy", out var value) ? value.GetString() : null;

        // Assert.False(điều kiện, thông báo): xanh khi `Policy` KHÔNG rỗng/null (điều kiện "rỗng" là
        // false); đỏ kèm thông báo khi thiếu.
        Assert.False(
            string.IsNullOrWhiteSpace(policy),
            "bff-cluster's HealthCheck:Passive declares no Policy — YARP cannot evaluate failures "
            + "against a threshold without one.");
    }

    /// <summary>
    /// Kiểm tra: `ReactivationPeriod` được khai báo và là 1 khoảng thời gian dương, hữu hạn.
    /// Lý do: hiến chương Principle VIII — không được tồn tại chờ vô hạn; 1 destination bị đánh dấu
    /// unhealthy mà không bao giờ có cơ hội hồi phục sẽ biến 1 trục trặc thoáng qua của BFF thành sự
    /// cố vĩnh viễn (tương ứng nửa "half-open" của FR-004).
    /// Task nguồn: spec 020 (timeout/retry/circuit breaker) — FR-004, hiến chương Principle VIII.
    /// </summary>
    [Fact]
    public void BffCluster_ReactivatesAfterABoundedPeriod()
    {
        var passive = ReadPassiveHealthCheck();

        var raw = passive.RootElement.TryGetProperty("ReactivationPeriod", out var value)
            ? value.GetString()
            : null;

        // Assert.False(điều kiện, thông báo): xanh khi `ReactivationPeriod` có khai báo (không rỗng);
        // đỏ kèm thông báo khi thiếu.
        Assert.False(
            string.IsNullOrWhiteSpace(raw),
            "bff-cluster's HealthCheck:Passive declares no ReactivationPeriod — an unhealthy "
            + "destination would never be retried again.");

        var reactivationPeriod = TimeSpan.Parse(raw!, CultureInfo.InvariantCulture);

        // Assert.True(điều kiện, thông báo): xanh khi khoảng thời gian > 0; đỏ khi 0 hoặc âm.
        Assert.True(reactivationPeriod > TimeSpan.Zero, "ReactivationPeriod must be a positive, bounded duration.");
    }

    /// <summary>
    /// Kiểm tra: `AvailableDestinationsPolicy` được đặt tường minh là `HealthyAndUnknown`.
    /// Lý do: bug thật tìm được lúc xác thực thủ công — mặc định của YARP (`HealthyOrPanic`) coi mọi
    /// destination là "available" khi không còn destination nào khoẻ; hợp lý với cluster nhiều
    /// destination, nhưng với cluster chỉ có 1 destination như `bff-cluster` nghĩa là cứ tiếp tục thử
    /// kết nối biết trước sẽ fail, không bao giờ fail fast thật. Đã kiểm chứng sống: không override
    /// thì request lặp lại vẫn trả 502 sau ~2-4 giây/lần dù destination đã ghi log 'Unhealthy'; đặt
    /// `HealthyAndUnknown` thì cùng request trả 503 trong vài mili giây khi mạch đã mở.
    /// Task nguồn: spec 020 (timeout/retry/circuit breaker) — FR-003, tasks.md T016.
    /// </summary>
    [Fact]
    public void BffCluster_UsesAvailableDestinationsPolicyThatActuallyExcludesUnhealthyDestinations()
    {
        var healthCheck = ReadHealthCheck();

        var policy = healthCheck.RootElement.TryGetProperty("AvailableDestinationsPolicy", out var value)
            ? value.GetString()
            : null;

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — phải đúng chuỗi
        // "HealthyAndUnknown"; giá trị khác (hoặc thiếu → mặc định HealthyOrPanic) vô hiệu hoá
        // circuit breaker của cluster 1 destination.
        Assert.Equal("HealthyAndUnknown", policy);
    }

    private static JsonDocument ReadPassiveHealthCheck()
    {
        var healthCheck = ReadHealthCheck();

        if (!healthCheck.RootElement.TryGetProperty("Passive", out var passive))
        {
            Assert.Fail("bff-cluster declares no HealthCheck:Passive section at all.");
            throw new UnreachableException(); // Assert.Fail always throws; satisfies definite assignment.
        }

        // Cloned so it survives the `using` document above being disposed.
        return JsonDocument.Parse(passive.GetRawText());
    }

    private static JsonDocument ReadHealthCheck()
    {
        var settingsPath = Path.Combine(
            LocateRepositoryRoot(),
            "services", "gateway", "src", "Gateway.Api", "appsettings.json");

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));

        var clusterElement = document.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Clusters")
            .GetProperty("bff-cluster");

        if (!clusterElement.TryGetProperty("HealthCheck", out var healthCheck))
        {
            Assert.Fail("bff-cluster declares no HealthCheck section at all.");
            throw new UnreachableException(); // Assert.Fail always throws; satisfies definite assignment.
        }

        // Cloned so it survives the `using` document above being disposed.
        return JsonDocument.Parse(healthCheck.GetRawText());
    }

    private static string LocateRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, RepositoryRootMarker)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            $"Could not locate '{RepositoryRootMarker}' walking up from '{AppContext.BaseDirectory}'.");
    }
}
