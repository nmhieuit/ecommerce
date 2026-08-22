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
    [Fact]
    public async Task CustomerRef_Is_UniquePerBasket()
    {
        await using var context = await CreateContextAsync("basket-constraints-customer-ref-unique");

        context.Baskets.Add(Basket.ForCustomer("duplicate-shopper"));
        await context.SaveChangesAsync();

        context.Baskets.Add(Basket.ForCustomer("duplicate-shopper"));

        // A real unique index rejects the second row; an in-memory provider would not (research.md
        // Decision 4 — Testcontainers.MsSql, not InMemory, is what makes this assertion meaningful).
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
