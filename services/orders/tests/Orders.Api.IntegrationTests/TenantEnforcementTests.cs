using System.Net;
using System.Net.Http.Json;
using IntegrationTestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Data;
using Tenancy;

namespace Orders.Api.IntegrationTests;

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
    private const string CountingTenantId = "contoso";

    private static readonly Guid AnyOrderId = new("8a1f6f6e-0000-4000-8000-000000000002");
    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");

    /// <summary>
    /// Kiểm tra: lấy `OrdersDbContext` từ DI khi chưa có tenant nào được phân giải thì THÀNH CÔNG (không
    /// ném exception).
    /// Lý do phải test: cố ý ngược với những gì test này khẳng định ở 3 service còn lại (và ngược với
    /// chính nó trước spec 024). Hosted service outbox/cleanup của MassTransit dựng cùng DbContext này
    /// từ scope nền, không có HTTP request nên cũng không có tenant; theo chính maintainer MassTransit,
    /// chặn ngay lúc dựng DbContext bằng 1 dependency scoped theo request là không được hỗ trợ. Cổng
    /// tenant vì vậy đã dời xuống các điểm chạm dữ liệu Order (`tenant.RequireTenantId()` tường minh
    /// trong `OrderEndpoints`) và được kiểm chứng đầu-cuối bởi 2 test bên dưới. Đây cũng là lý do
    /// `TenantGatedConnectionTests.EveryDbContextRegistration_IsGatedOnAResolvedTenant` (SC-003) đang đỏ
    /// ở `orders` — xem docs/QA/QA_Debt.md, mục 003.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T026 (đã đổi ý nghĩa bởi spec 024-verify-transactional-outbox), US2.
    /// </summary>
    [Fact]
    public async Task ResolvingTheDbContext_Succeeds_EvenWhenNoTenantHasBeenResolved()
    {
        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        Assert.NotNull(dbContext);
    }

    /// <summary>
    /// Kiểm tra: gọi thẳng `/orders/{id}` mà không có `X-Tenant-Id` trả về `500 Internal Server Error`.
    /// Lý do phải test: quickstart.md Scenario 3 — ở Phase 1 chấp nhận mã lỗi nhóm 500; điều cần chứng
    /// minh là service thất bại to tiếng thay vì trả `200 OK` với dữ liệu của 1 tenant/schema mặc định
    /// nào đó (FR-004, FR-005, SC-002). Có database thật phía sau nên nếu thiếu cổng tenant thì service
    /// sẽ trả 200 — vì vậy việc lỗi ở đây là bằng chứng của cổng tenant.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T026, US2.
    /// </summary>
    [Fact]
    public async Task ARequestWithoutATenant_Fails_RatherThanServingDefaultSchemaData()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient().UseTestBearerToken();

        var response = await client.GetAsync($"/orders/{AnyOrderId}");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: `POST /orders` khi chưa có tenant trả `500` VÀ số dòng trong bảng Orders không đổi.
    /// Lý do phải test: assert số dòng chứ không chỉ mã trạng thái — response lỗi chỉ chứng minh caller
    /// bị từ chối, chưa chứng minh không có gì được ghi; đó là 2 khẳng định khác nhau. Đây chính là
    /// assertion mà bước "WITHOUT A TENANT" của demo dựa vào.
    /// Task nguồn: spec 006 (demo đặt hàng end-to-end) — FR-006, US2 kịch bản 2 (mở rộng bộ test tenant của 003).
    /// </summary>
    [Fact]
    public async Task AWriteWithoutATenant_CreatesNoOrder()
    {
        await using var factory = CreateFactory();

        var before = await CountOrdersAsync(factory);

        var response = await factory.CreateClient().UseTestBearerToken().PostAsJsonAsync("/orders", new
        {
            items = new[]
            {
                new { productId = Notebook, quantity = 1, unitPrice = 12.50m },
            },
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(before, await CountOrdersAsync(factory));
    }

    /// <summary>
    /// Counted through a scope that primes a tenant of its own, because counting is itself a read
    /// and this service refuses reads without one. That is the behaviour under test in the sibling
    /// cases here, so the counter has to opt in explicitly rather than inherit it.
    /// </summary>
    private static async Task<int> CountOrdersAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = CountingTenantId;

        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await dbContext.Database.MigrateAsync();

        return await dbContext.Orders.CountAsync();
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:OrdersDb"] = sqlServer.ConnectionString,
                }));
            builder.UseTestJwtBearer();
        });
}
