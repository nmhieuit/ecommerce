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
    /// two of them. <c>identity</c> will join this list once 014-identity-server-auth's User
    /// Story 1 (tasks.md T020-T021) adds its own database — see <see cref="StatelessServices"/>.
    /// </summary>
    private static readonly string[] DatabaseOwningServices = ["baskets", "orders", "parties", "products"];

    /// <summary>
    /// The services that must declare no connection string at all. <c>identity</c> is here only for
    /// now (Setup/Foundational time, tasks.md T012-T016) — it moves to
    /// <see cref="DatabaseOwningServices"/> once User Story 1 gives it a configuration/user-
    /// credential store (data-model.md — Identity User, Client Application).
    /// </summary>
    private static readonly string[] StatelessServices = ["bff", "gateway", "identity"];

    [Fact]
    public void NoServiceConfiguration_NamesAnotherServicesDatabase()
    {
        var result = ConnectionStringScanner.Scan(ConnectionStringScanner.LocateServicesDirectory());

        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// Guards the assertion above against passing for the wrong reason. A scan that resolved the
    /// wrong directory, or that matched no files after a layout change, would report zero
    /// violations and look identical to genuine isolation.
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
    /// The other half of Principle I: a service that owns no data must not be handed a database at
    /// all. The gateway and BFF reach the domain services over HTTP, and a connection string
    /// appearing in either one's configuration would be a boundary breach the scan above cannot
    /// see — it looks for connections that name <em>another</em> service's database, and a
    /// stateless service has no database of its own for one to be compared against.
    /// </summary>
    [Fact]
    public void NoStatelessService_DeclaresAConnectionString()
    {
        var servicesDirectory = ConnectionStringScanner.LocateServicesDirectory();

        Assert.All(StatelessServices, service =>
        {
            var configurationFiles = Directory.GetFiles(
                Path.Combine(servicesDirectory, service),
                "appsettings*.json",
                SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                            && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

            // Guards against the assertion passing because the files were never found.
            Assert.NotEmpty(configurationFiles);

            Assert.All(configurationFiles, file =>
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                Assert.False(
                    document.RootElement.TryGetProperty("ConnectionStrings", out _),
                    $"'{file}' declares a ConnectionStrings section, but {service} owns no database "
                    + "(constitution Principle I).");
            });
        });
    }

    /// <summary>
    /// Guards the same assertion against a scanner that cannot detect anything at all. Without
    /// this, an implementation that always returned zero violations would satisfy SC-003 forever.
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
