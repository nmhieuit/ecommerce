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
    /// Kiểm tra: khi database của Products kết nối được (1 SQL Server thật do Testcontainers dựng),
    /// `GET /health/ready` trả 200 OK — service báo "sẵn sàng phục vụ".
    /// Lý do: đây là nhánh "khoẻ mạnh" đối chứng cho 2 test bên dưới — thiếu nó thì không có gì đảm
    /// bảo readiness không bị lỗi ngược (luôn báo lỗi) mà vẫn "vô tình" pass các test còn lại vốn
    /// chỉ kiểm tra trường hợp database chết.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T013, US1.
    /// </summary>
    [Fact]
    public async Task HealthReady_ReturnsOk_WhenDatabaseReachable()
    {
        await using var factory = CreateFactory(sqlServer.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: khi database KHÔNG kết nối được, `GET /health/ready` trả 503 Service Unavailable —
    /// service báo "chưa sẵn sàng" thay vì báo khoẻ giả.
    /// Lý do: readiness phải "fail closed" — báo lỗi rõ ràng thay vì âm thầm báo khoẻ mạnh khi thực
    /// chất không phục vụ được request nào (FR-003, mục Edge Cases của spec).
    /// Lưu ý: test chỉ kiểm tra mã trạng thái; nội dung body do test kế tiếp kiểm.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T013, US1.
    /// </summary>
    [Fact]
    public async Task HealthReady_ReturnsServiceUnavailable_WhenDatabaseUnreachable()
    {
        await using var factory = CreateFactory(UnreachableConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. So mã HTTP kỳ vọng (503)
        // với mã thực tế. ĐẠT khi 503. ĐỎ khi 200 (readiness "nói dối", báo khoẻ khi database chết
        // — đúng lỗi mà test này sinh ra để chặn) hoặc 500 (ứng dụng vỡ thay vì báo lỗi có kiểm
        // soát).
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: khi CHỈ database của chính Products chết nhưng có sẵn 1 database khác kết nối được
    /// (đóng vai database của service khác), readiness vẫn trả 503 và trong body nêu rõ check tên
    /// "self-database" ở trạng thái "Unhealthy" — service không được âm thầm chuyển sang dùng
    /// database không thuộc về mình.
    /// Lý do: đây là kịch bản chấp nhận số 2 của US2 — service không được âm thầm fallback sang
    /// data store của service khác hay 1 mặc định dùng chung khi database riêng gặp sự cố. Dùng 1
    /// SQL Server thật làm database ngoại lai để chứng minh việc fail không phải vì "không có SQL
    /// Server nào", mà vì service từ chối dùng database không thuộc về mình. (Đã từng thử phá: đổi
    /// key kết nối của health check thành `PartiesDb` thì test này đỏ với "Expected
    /// ServiceUnavailable / Actual OK".)
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

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Mã HTTP phải là 503. ĐỎ
        // với 200: nghĩa là readiness đã dùng 1 trong 3 database ngoại lai còn sống và báo khoẻ —
        // chính là lỗi "fallback sang database của service khác".
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        // Named explicitly so the assertion cannot be satisfied by some unrelated failure.
        var body = await response.Content.ReadAsStringAsync();
        // Assert.Contains(phần tử, tập hợp): xanh khi tập hợp có chứa phần tử, đỏ khi không.
        Assert.Contains("self-database", body);
        // Assert.Contains(phần tử, tập hợp): xanh khi tập hợp có chứa phần tử, đỏ khi không. Body
        // phải chứa chữ "Unhealthy", tức check đó báo hỏng. Hai Assert cuối "đinh" nguyên nhân:
        // test không thể pass chỉ vì 1 lỗi không liên quan gây ra 503.
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
