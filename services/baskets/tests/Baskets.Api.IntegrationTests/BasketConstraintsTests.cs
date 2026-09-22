using Baskets.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Baskets.Api.IntegrationTests;

/// <summary>
/// 010-testcontainers-integration-tests spec FR-001, FR-002: proves the SQL Server Testcontainers
/// pattern still catches a real database-level constraint violation, not just an application-level
/// guard. Goes straight at <see cref="BasketsDbContext"/> rather than through the HTTP surface —
/// the unique index on <see cref="Basket.CustomerRef"/>, not any endpoint behaviour, is the subject
/// under test.
/// </summary>
public class BasketConstraintsTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    /// <summary>
    /// Kiểm tra: chèn 2 basket cùng `CustomerRef` thẳng qua `BasketsDbContext` (SQL Server thật qua
    /// Testcontainers) — dòng thứ 2 phải bị `SaveChangesAsync` từ chối.
    /// Lý do: chứng minh index unique trên `CustomerRef` là ràng buộc THẬT của SQL Server, không
    /// phải guard tầng ứng dụng; dùng provider giả (in-memory) sẽ không bắt được lỗi này.
    /// Task nguồn: spec 010 (hạ tầng kiểm thử container thật) — T006/T007, US1 (FR-001, FR-002).
    /// </summary>
    [Fact]
    public async Task CustomerRef_Is_UniquePerBasket()
    {
        await using var context = await CreateContextAsync("basket-constraints-customer-ref-unique");

        context.Baskets.Add(Basket.ForCustomer("duplicate-shopper"));
        await context.SaveChangesAsync();

        context.Baskets.Add(Basket.ForCustomer("duplicate-shopper"));

        // Assert.ThrowsAsync(loại ngoại lệ, đoạn mã): xanh khi đoạn mã ném đúng loại ngoại lệ, đỏ
        // khi không ném hoặc ném loại khác. Index unique thật của SQL Server phải chặn dòng thứ 2
        // nên SaveChangesAsync phải ném DbUpdateException; đỏ nếu ai đó gỡ `.IsUnique()` khỏi index.
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private async Task<BasketsDbContext> CreateContextAsync(string database)
    {
        var connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            sqlServer.ConnectionString)
        {
            InitialCatalog = database,
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<BasketsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var context = new BasketsDbContext(options);
        await context.Database.MigrateAsync();

        return context;
    }
}
