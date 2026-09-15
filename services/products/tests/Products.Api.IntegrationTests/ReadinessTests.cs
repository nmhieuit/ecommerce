using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Products.Api.IntegrationTests;

/// <summary>
/// Constitution Principle III: real SQL Server via Testcontainers, never an in-memory
/// provider or a hand-rolled fake. Proves /health/ready reflects actual database
/// connectivity (spec FR-003), not just process liveness.
/// </summary>
public class ReadinessTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    /// <summary>
    /// Syntactically valid, deliberately unroutable — there is nothing at this address to answer.
    /// </summary>
    private const string UnreachableConnectionString =
        "Server=127.0.0.1,1;Database=unreachable;User Id=sa;Password=wrong;Connect Timeout=1;TrustServerCertificate=True";

    /// <summary>
    /// Kiểm tra: khi database kết nối được, GET /health/ready trả về 200 OK.
    /// Lý do phải test: đây là nhánh "khoẻ mạnh" đối chứng cho 2 test bên dưới — thiếu test này thì
    /// không có gì đảm bảo readiness không bị lỗi ngược (luôn báo lỗi) mà vẫn "vô tình" pass các test
    /// còn lại vốn chỉ kiểm tra trường hợp database chết.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T013, US1.
    /// </summary>
    [Fact]
    public async Task HealthReady_ReturnsOk_WhenDatabaseReachable()
    {
        await using var factory = CreateFactory(sqlServer.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: khi database không kết nối được, GET /health/ready phải trả 503 Service Unavailable.
    /// Lý do phải test: readiness phải "fail closed" — báo lỗi rõ ràng thay vì âm thầm báo khoẻ mạnh
    /// khi thực chất không phục vụ được request nào (FR-003, mục Edge Cases của spec).
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T013, US1.
    /// </summary>
    [Fact]
    public async Task HealthReady_ReturnsServiceUnavailable_WhenDatabaseUnreachable()
    {
        await using var factory = CreateFactory(UnreachableConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: khi CHỈ database của chính service này không kết nối được — trong lúc database của
    /// 1 service khác (đóng vai database "ngoại lai", hoàn toàn kết nối được) vẫn sẵn sàng — readiness
    /// vẫn phải trả 503, và trong response phải nêu rõ check tên "self-database" ở trạng thái
    /// "Unhealthy".
    /// Lý do phải test: đây là kịch bản chấp nhận số 2 của US2 trong spec — service không được âm
    /// thầm fallback sang data store của service khác hay 1 mặc định dùng chung khi database riêng
    /// của nó gặp sự cố. Test dùng chính 1 SQL Server thật (đóng vai database ngoại lai) để chứng
    /// minh việc fail không phải vì "không có SQL Server nào cả", mà đúng là do service từ chối dùng
    /// database không thuộc về mình.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T038, US2.
    /// </summary>
    [Fact]
    public async Task HealthReady_DoesNotFallBackToAnotherServicesDatabase_WhenOwnDatabaseUnreachable()
    {
        await using var factory = CreateFactory(
            UnreachableConnectionString,
            reachableForeignConnectionString: sqlServer.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        // Named explicitly so the assertion cannot be satisfied by some unrelated failure.
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("self-database", body);
        Assert.Contains("Unhealthy", body);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString,
        string? reachableForeignConnectionString = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ProductsDb"] = connectionString,
                };

                if (reachableForeignConnectionString is not null)
                {
                    settings["ConnectionStrings:PartiesDb"] = reachableForeignConnectionString;
                    settings["ConnectionStrings:BasketsDb"] = reachableForeignConnectionString;
                    settings["ConnectionStrings:OrdersDb"] = reachableForeignConnectionString;
                }

                config.AddInMemoryCollection(settings);
            });
        });
    }
}
