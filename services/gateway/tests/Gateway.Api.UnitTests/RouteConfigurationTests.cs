using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;

namespace Gateway.Api.UnitTests;

/// <summary>
/// The gateway's entire routing surface is declarative configuration (research.md Decision 2), so
/// its correctness is a property of <c>appsettings.json</c> rather than of any code path a test
/// could otherwise exercise.
/// </summary>
/// <remarks>
/// Loaded through YARP's own <c>LoadFromConfig</c> binding rather than read as raw JSON. A test
/// that only asserted the presence of JSON keys would pass on config YARP rejects — a misspelled
/// <c>ClusterId</c>, a malformed match — which is precisely the failure worth catching before it
/// reaches a running gateway.
/// </remarks>
public class RouteConfigurationTests
{
    private const string RepositoryRootMarker = "Ecommerce.slnx";

    private const string ExpectedRouteId = "bff-route";
    private const string ExpectedClusterId = "bff-cluster";

    /// <summary>
    /// research.md Decision 1: "The gateway's YARP configuration defines exactly one destination
    /// cluster — the BFF. It does not define clusters for products/baskets/orders."
    /// </summary>
    private static readonly string[] DomainServices = ["products", "baskets", "orders", "parties"];

    /// <summary>
    /// Kiểm tra: cấu hình YARP của gateway chỉ khai đúng 1 route, trỏ đúng vào 1 cluster tên
    /// "bff-cluster".
    /// Lý do: đây là assertion trực tiếp cho FR-001 — gateway chỉ được biết tới BFF, không được
    /// biết tới bất kỳ service nghiệp vụ nào đứng sau nó.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T060, US2.
    /// </summary>
    [Fact]
    public void TheConfiguration_DefinesExactlyOneRoute_ToTheBffCluster()
    {
        var config = LoadProxyConfig();

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
        // nhiều hơn. Đỏ nếu gateway có 0 route hoặc ≥ 2 route (ví dụ ai đó thêm route thẳng tới
        // Products).
        var route = Assert.Single(config.Routes);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng: id route đúng
        // tên quy ước; thực tế: id đọc từ cấu hình. Đỏ khi route bị đổi tên.
        Assert.Equal(ExpectedRouteId, route.RouteId);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng: route trỏ tới
        // cluster của BFF; thực tế: cluster trong cấu hình. Đỏ khi route trỏ sang cluster khác.
        Assert.Equal(ExpectedClusterId, route.ClusterId);
    }

    /// <summary>
    /// Kiểm tra: route duy nhất đó khớp MỌI path (`{**catch-all}`), không liệt kê path cụ thể nào.
    /// Lý do: đây không phải chi tiết phụ — nếu gateway liệt kê từng path của BFF, mỗi lần BFF thêm
    /// route mới sẽ phải sửa gateway theo, tái tạo lại đúng sự ràng buộc topology mà FR-001 muốn
    /// xoá bỏ.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T060, US2.
    /// </summary>
    [Fact]
    public void TheRoute_MatchesEveryPath()
    {
        var config = LoadProxyConfig();

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
        // nhiều hơn.
        var route = Assert.Single(config.Routes);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng: mẫu đường dẫn
        // là catch-all (khớp mọi path); thực tế: mẫu trong cấu hình. Đỏ khi bị thu hẹp (ví dụ chỉ
        // /bff/{**rest}) vì khi đó các path còn lại không được chuyển tiếp.
        Assert.Equal("{**catch-all}", route.Match.Path);
    }

    /// <summary>
    /// Kiểm tra: cấu hình chỉ khai đúng 1 cluster ("bff-cluster"), cluster đó chỉ có đúng 1
    /// destination và địa chỉ destination không rỗng.
    /// Lý do: đối chứng cho test route phía trên — route trỏ đúng tên cluster là chưa đủ, cluster
    /// đó còn phải thực sự trỏ được tới đâu đó (BFF), nếu không route "hợp lệ" vẫn không đi tới đâu
    /// cả.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T060, US2.
    /// </summary>
    [Fact]
    public void TheConfiguration_DefinesExactlyOneCluster_WithOneDestination()
    {
        var config = LoadProxyConfig();

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
        // nhiều hơn.
        var cluster = Assert.Single(config.Clusters);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng: cluster mang id
        // của BFF;
        Assert.Equal(ExpectedClusterId, cluster.ClusterId);

        var destination = Assert.Single(cluster.Destinations!);
        // Assert.False(điều kiện): xanh khi điều kiện sai, đỏ khi đúng. Điều kiện ở đây là "địa chỉ
        // rỗng hoặc toàn khoảng trắng"; đạt khi địa chỉ có nội dung, đỏ khi để trống.
        Assert.False(string.IsNullOrWhiteSpace(destination.Value.Address));
    }

    /// <summary>
    /// Kiểm tra: không địa chỉ destination nào trong cấu hình chứa tên 1 trong 4 service nghiệp vụ
    /// (products/baskets/orders/parties).
    /// Lý do: gateway tuyệt đối không được có route thẳng tới 1 service nghiệp vụ — nếu có, caller
    /// có thể vòng qua BFF để chạm thẳng service đó, phá vỡ đúng chuỗi biên constitution quy định
    /// (load balancer → gateway → BFF).
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T060, US2.
    /// </summary>
    [Fact]
    public void TheConfiguration_NamesNoDomainServiceAsADestination()
    {
        var config = LoadProxyConfig();

        var addresses = config.Clusters
            .SelectMany(cluster => cluster.Destinations?.Values ?? [])
            .Select(destination => destination.Address)
            .ToArray();

        // Assert.All(tập hợp, hành động): chạy hành động cho từng phần tử, đỏ nếu bất kỳ phần tử
        // nào không đạt. Đạt khi không địa chỉ nào chứa tên service miền (products, baskets,
        // orders, parties); đỏ khi có, tức gateway đi thẳng tới service miền và bỏ qua BFF.
        Assert.All(addresses, address => Assert.All(
            DomainServices,
            service => Assert.DoesNotContain(service, address, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Kiểm tra: mọi route trong cấu hình đều có `ClusterId` khác null và trỏ tới 1 cluster có thật
    /// trong danh sách cluster đã khai.
    /// Lý do: quy tắc kiểm chứng của data-model.md — "route không khớp cluster nào là lỗi cấu hình,
    /// PHẢI làm fail lúc khởi động, không được lặng lẽ route vào hư không". Thiếu test này, 1 route
    /// trỏ sai tên cluster có thể lọt qua tới runtime rồi mới lộ ra.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T060, US2.
    /// </summary>
    [Fact]
    public void EveryRoute_ResolvesToADefinedCluster()
    {
        var config = LoadProxyConfig();
        var clusterIds = config.Clusters.Select(cluster => cluster.ClusterId).ToHashSet(StringComparer.Ordinal);

        // Assert.All(tập hợp, hành động): chạy hành động cho từng phần tử, đỏ nếu bất kỳ phần tử
        // nào không đạt. Chạy kiểm tra cho từng route;
        Assert.All(config.Routes, route =>
        {
            // A route with no ClusterId routes nowhere, which the rule below could not otherwise
            // distinguish from a route pointing at a cluster that happens to exist.
            // Assert.NotNull(giá trị): xanh khi khác null, đỏ khi null. Đỏ khi route không khai báo
            // cluster.
            Assert.NotNull(route.ClusterId);
            // Assert.Contains(phần tử, tập hợp): xanh khi tập hợp có chứa phần tử, đỏ khi không. Đỏ
            // khi route trỏ tới cluster không tồn tại (ví dụ gõ sai tên).
            Assert.Contains(route.ClusterId, clusterIds);
        });
    }

    /// <summary>
    /// Binds the committed <c>appsettings.json</c> exactly as <c>Program.cs</c> does.
    /// </summary>
    private static IProxyConfig LoadProxyConfig()
    {
        var settingsPath = Path.Combine(
            LocateRepositoryRoot(),
            "services", "gateway", "src", "Gateway.Api", "appsettings.json");

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(settingsPath, optional: false)
            .Build();

        var services = new ServiceCollection();
        // YARP's configuration provider resolves an ILogger; a bare ServiceCollection has none,
        // unlike the host Program.cs builds.
        services.AddLogging();
        services.AddReverseProxy().LoadFromConfig(configuration.GetSection("ReverseProxy"));

        return services.BuildServiceProvider()
            .GetRequiredService<IProxyConfigProvider>()
            .GetConfig();
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
