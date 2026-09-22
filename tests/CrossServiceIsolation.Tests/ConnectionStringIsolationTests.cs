using System.Text.Json;

namespace CrossServiceIsolation.Tests;

/// <summary>
/// Spec SC-003: "Zero successful cross-service data accesses are possible — verified by a
/// repeatable check." This is that check. It reads what is actually committed to the repository
/// rather than what any one service believes about itself, so it keeps holding as services are
/// added (constitution Principle I: no service may read or write another service's database).
/// </summary>
public class ConnectionStringIsolationTests
{
    /// <summary>Every service under <c>services/</c>, in the ordinal order the scanner returns them.</summary>
    private static readonly string[] ExpectedServices =
        ["baskets", "bff", "gateway", "identity", "orders", "parties", "products"];

    /// <summary>
    /// The services that own a database, and so are the only ones expected to declare a connection
    /// string. <c>bff</c> and <c>gateway</c> are stateless proxy/aggregation layers — constitution
    /// Principle I gives persistence only to services that own business invariants — so counting
    /// connection strings against <see cref="ExpectedServices"/> would assert something untrue of
    /// two of them. <c>identity</c> joined this list in 014-identity-server-auth's User Story 1
    /// (tasks.md T020-T021), which gave it a configuration/operational store and a user credential
    /// store (data-model.md — Client Application, Identity User) — all in its own "identity"
    /// database, per research.md Decision 8.
    /// </summary>
    private static readonly string[] DatabaseOwningServices = ["baskets", "identity", "orders", "parties", "products"];

    /// <summary>The services that must declare no connection string at all.</summary>
    private static readonly string[] StatelessServices = ["bff", "gateway"];

    /// <summary>
    /// Kiểm tra: quét toàn bộ file `appsettings*.json` dưới `services/` — không service nào có
    /// chuỗi kết nối trỏ vào database của 1 service khác.
    /// Lý do: đây chính là bài kiểm chứng cho SC-003 ("không có truy cập dữ liệu chéo service nào
    /// thành công") — assertion trực tiếp, ngắn gọn nhất cho tiêu chí nghiệm thu này. Nó đọc thứ
    /// thật sự được commit, không phải thứ 1 service tự tin về mình, nên vẫn đúng khi thêm service
    /// mới.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T036, US2.
    /// </summary>
    [Fact]
    public void NoServiceConfiguration_NamesAnotherServicesDatabase()
    {
        var result = ConnectionStringScanner.Scan(ConnectionStringScanner.LocateServicesDirectory());

        // Assert.Empty(tập hợp): xanh khi không có phần tử nào, đỏ khi có. ĐẠT khi không có vi phạm
        // nào. ĐỎ khi có: xUnit in từng vi phạm, cho biết service nào ("OwningService") đang trỏ
        // vào database của service nào ("ForeignService") — 1 đường đọc/ghi chéo dữ liệu bị cấm bởi
        // Constitution Principle I.
        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// Kiểm tra: scanner thực sự quét đúng cả 7 service (đúng danh sách, đúng tên file cấu hình từng
    /// service, và tìm được ít nhất 1 connection string cho mỗi service có sở hữu database).
    /// Lý do phải test: bảo vệ assertion ở test phía trên khỏi "pass vì lý do sai" — 1 scanner trỏ
    /// sai thư mục, hoặc không khớp được file nào sau khi đổi cấu trúc thư mục, vẫn báo "0 vi phạm"
    /// và trông giống hệt kết quả của 1 hệ thống thực sự cách ly tốt.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T036, US2.
    /// </summary>
    [Fact]
    public void Scan_ActuallyExaminesEveryServicesConfiguration()
    {
        var result = ConnectionStringScanner.Scan(ConnectionStringScanner.LocateServicesDirectory());

        Assert.Equal(ExpectedServices, result.ScannedServices);
        Assert.All(
            ExpectedServices,
            service => Assert.Contains(
                result.ScannedConfigurationFiles,
                file => file.Contains(service, StringComparison.OrdinalIgnoreCase)));
        Assert.True(
            result.ScannedConnectionStringCount >= DatabaseOwningServices.Length,
            "Expected at least one connection string per database-owning service, "
            + $"found {result.ScannedConnectionStringCount}.");
    }

    /// <summary>
    /// Kiểm tra: 2 service không sở hữu database (bff, gateway) không khai bất kỳ mục
    /// `ConnectionStrings` nào trong file `appsettings*.json` của chúng.
    /// Lý do: nửa còn lại của Constitution Principle I — không chỉ "không đụng database của service
    /// khác" mà "service không sở hữu dữ liệu thì không được cấp database nào cả". gateway/bff gọi
    /// service nghiệp vụ qua HTTP; nếu 1 trong hai bị lộ chuỗi kết nối thì test kia (chỉ tìm chuỗi
    /// trỏ SANG service khác) không phát hiện được, vì chúng vốn không có database của chính mình
    /// để so.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T036, US2.
    /// </summary>
    [Fact]
    public void NoStatelessService_DeclaresAConnectionString()
    {
        var servicesDirectory = ConnectionStringScanner.LocateServicesDirectory();

        // Assert.All(tập hợp, hành động): chạy hành động cho từng phần tử, đỏ nếu bất kỳ phần tử
        // nào không đạt.
        Assert.All(StatelessServices, service =>
        {
            var configurationFiles = Directory.GetFiles(
                Path.Combine(servicesDirectory, service),
                "appsettings*.json",
                SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                            && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

            // Guards against the assertion passing because the files were never found.
            // Assert.NotEmpty(tập hợp): xanh khi có ít nhất 1 phần tử, đỏ khi rỗng.
            Assert.NotEmpty(configurationFiles);

            Assert.All(configurationFiles, file =>
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                // Assert.False(điều kiện): xanh khi điều kiện sai, đỏ khi đúng.
                Assert.False(
                    document.RootElement.TryGetProperty("ConnectionStrings", out _),
                    $"'{file}' declares a ConnectionStrings section, but {service} owns no database "
                    + "(constitution Principle I).");
            });
        });
    }

    /// <summary>
    /// Kiểm tra: với 1 cây thư mục services/ giả (dựng tạm), khi 1 service khai connection string
    /// trỏ đúng vào database của service khác (dù bằng tên service nội bộ Docker hay bằng địa
    /// chỉ/cổng localhost thật), scanner phải phát hiện đúng 1 vi phạm, nêu đúng service vi phạm và
    /// đúng service bị chạm tới.
    /// Lý do phải test: đối chứng "chiều dương" cho 2 test bên dưới — thiếu test này, 1 scanner luôn
    /// trả về "0 vi phạm" (dù không kiểm tra gì cả) vẫn làm SC-003 trông như đã đạt mãi mãi.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T036, US2.
    /// </summary>
    [Theory]
    [InlineData("OrdersDb", "Server=parties-db;Database=parties;TrustServerCertificate=True")]
    [InlineData("PartiesDb", "Server=orders-db;Database=orders;TrustServerCertificate=True")]
    [InlineData("PartiesDb", "Server=localhost,14333;Database=orders;TrustServerCertificate=True")]
    public void Scan_FlagsAConfigurationThatReachesAnotherServicesDatabase(string key, string connectionString)
    {
        using var fixture = new ServicesDirectoryFixture();
        fixture.WriteService("parties", "Parties", key, connectionString);
        fixture.WriteService("orders", "Orders", "OrdersDb", "Server=orders-db;Database=orders;TrustServerCertificate=True");

        var result = ConnectionStringScanner.Scan(fixture.ServicesDirectory);

        var violation = Assert.Single(result.Violations);
        Assert.Equal("parties", violation.OwningService);
        Assert.Equal("orders", violation.ForeignService);
    }

    /// <summary>
    /// Kiểm tra: khi mỗi service chỉ khai connection string trỏ vào đúng database của chính nó,
    /// scanner không báo vi phạm nào.
    /// Lý do phải test: đảm bảo scanner không quá tay/dương tính giả (false positive) — 1 cấu hình
    /// hoàn toàn hợp lệ, đúng theo Principle I, không được bị chặn nhầm.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T036, US2.
    /// </summary>
    [Fact]
    public void Scan_AllowsAServiceToNameItsOwnDatabase()
    {
        using var fixture = new ServicesDirectoryFixture();
        fixture.WriteService("parties", "Parties", "PartiesDb", "Server=parties-db;Database=parties;TrustServerCertificate=True");
        fixture.WriteService("orders", "Orders", "OrdersDb", "Server=orders-db;Database=orders;TrustServerCertificate=True");

        var result = ConnectionStringScanner.Scan(fixture.ServicesDirectory);

        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// A throwaway <c>services/</c> tree on disk, shaped exactly like the real one, so the scanner
    /// can be pointed at deliberately broken configuration without breaking the repository.
    /// </summary>
    private sealed class ServicesDirectoryFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"isolation-scan-{Guid.NewGuid():N}");

        public string ServicesDirectory => Path.Combine(_root, "services");

        public void WriteService(string serviceName, string projectPrefix, string key, string connectionString)
        {
            var projectDirectory = Path.Combine(ServicesDirectory, serviceName, "src", $"{projectPrefix}.Api");
            Directory.CreateDirectory(projectDirectory);
            var settings = new Dictionary<string, Dictionary<string, string>>
            {
                ["ConnectionStrings"] = new() { [key] = connectionString },
            };
            File.WriteAllText(
                Path.Combine(projectDirectory, "appsettings.json"),
                JsonSerializer.Serialize(settings));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
