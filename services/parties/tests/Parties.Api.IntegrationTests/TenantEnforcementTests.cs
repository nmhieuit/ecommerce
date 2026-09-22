using System.Net;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Parties.Api.Data;
using Tenancy;

namespace Parties.Api.IntegrationTests;

/// <summary>
/// Spec US2 Acceptance Scenario 1 and Test Scenario 2: reaching this service without going through
/// the gateway means no tenant was ever resolved, and that must fail loudly rather than quietly
/// serving whatever lives in a default schema.
/// </summary>
/// <remarks>
/// Run against the real SQL Server the rest of the suite uses, deliberately: pointing these at an
/// unreachable database would make them pass whether or not the gate exists, since the request
/// would fail either way.
/// </remarks>
public class TenantEnforcementTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    private static readonly Guid AnyPartyId = new("8a1f6f6e-0000-4000-8000-000000000003");

    /// <summary>
    /// Kiểm tra: lấy `PartiesDbContext` từ DI khi chưa có tenant nào được phân giải thì ném
    /// `MissingTenantContextException`.
    /// Lý do: cổng tenant nằm ngay tại điểm gọi `AddDbContext` duy nhất (research.md Decision 6),
    /// nên không thể có khoảng thời gian DbContext đã tồn tại mà chưa bị kiểm tra tenant. Chạy với
    /// SQL Server thật để chứng minh lỗi đến từ cổng tenant chứ không phải từ việc không kết nối
    /// được database.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T027, US2.
    /// </summary>
    [Fact]
    public async Task ResolvingTheDbContext_Throws_WhenNoTenantHasBeenResolved()
    {
        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();

        // Assert.Throws(loại ngoại lệ, đoạn mã): xanh khi đoạn mã ném đúng loại ngoại lệ, đỏ khi
        // không ném hoặc ném loại khác.
        Assert.Throws<MissingTenantContextException>(
            () => scope.ServiceProvider.GetRequiredService<PartiesDbContext>());
    }

    /// <summary>
    /// Kiểm tra: gọi thẳng `/parties/{id}` mà không có `X-Tenant-Id` trả về `500 Internal Server
    /// Error`.
    /// Lý do: quickstart.md Scenario 3 — ở Phase 1 chấp nhận mã lỗi nhóm 500; điều cần chứng minh
    /// là service thất bại to tiếng thay vì trả `200 OK` với dữ liệu của 1 tenant/schema mặc định
    /// nào đó (FR-004, FR-005, SC-002). Có database thật phía sau nên nếu thiếu cổng tenant thì
    /// service sẽ trả 200 — vì vậy việc lỗi ở đây là bằng chứng của cổng tenant.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T027, US2.
    /// </summary>
    [Fact]
    public async Task ARequestWithoutATenant_Fails_RatherThanServingDefaultSchemaData()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient().UseTestBearerToken();

        var response = await client.GetAsync($"/parties/{AnyPartyId}");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng 500 (cổng tenant
        // chặn request không có X-Tenant-Id); thực tế là mã service trả. Đỏ khi service trả 200
        // (tức phục vụ dữ liệu schema mặc định) hoặc mã khác.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PartiesDb"] = sqlServer.ConnectionString,
                }));
            builder.UseTestJwtBearer();
        });
}
