using Microsoft.EntityFrameworkCore;
using Orders.Api.Data;

namespace Orders.Api.IntegrationTests;

/// <summary>
/// 010-testcontainers-integration-tests spec FR-001, FR-002: proves the SQL Server Testcontainers
/// pattern still catches a real database-level constraint violation, not just an application-level
/// guard. Goes straight at <see cref="OrdersDbContext"/> rather than through the HTTP surface — the
/// <c>nvarchar(128)</c> column bound on <see cref="Order.TenantId"/>, not any endpoint behaviour,
/// is the subject under test.
/// </summary>
public class OrderConstraintsTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    /// <summary>
    /// Kiểm tra: chèn 1 `Order` với `TenantId` dài 129 ký tự thẳng qua `OrdersDbContext` (SQL Server
    /// thật) — phải bị `SaveChangesAsync` từ chối.
    /// Lý do: chứng minh cột `nvarchar(128)` là ràng buộc THẬT của SQL Server, không phải guard tầng
    /// ứng dụng; dùng provider giả (in-memory) sẽ không bắt được lỗi này.
    /// Task nguồn: spec 010 (hạ tầng kiểm thử container thật) — T008/T009, US1 (FR-001, FR-002).
    /// </summary>
    [Fact]
    public async Task TenantId_ExceedingMaxLength_IsRejectedByTheDatabase()
    {
        await using var context = await CreateContextAsync("order-constraints-tenant-id-length");

        context.Orders.Add(new Order
        {
            Id = Guid.NewGuid(),
            PlacedAtUtc = DateTime.UtcNow,
            Total = 10m,
            TenantId = new string('a', 129),
        });

        // Assert.ThrowsAsync(loại ngoại lệ, đoạn mã): xanh khi đoạn mã ném đúng loại ngoại lệ, đỏ
        // khi không ném hoặc ném loại khác. Cột nvarchar(128) thật phải từ chối giá trị quá dài nên
        // SaveChangesAsync ném DbUpdateException; đỏ nếu ai đó nới cột lên (ví dụ HasMaxLength(500)).
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private async Task<OrdersDbContext> CreateContextAsync(string database)
    {
        var connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            sqlServer.ConnectionString)
        {
            InitialCatalog = database,
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var context = new OrdersDbContext(options);
        await context.Database.MigrateAsync();

        return context;
    }
}
