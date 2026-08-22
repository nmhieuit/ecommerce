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

        // The real nvarchar(128) column rejects the over-length value; an in-memory provider would
        // not (research.md Decision 4 — Testcontainers.MsSql is what makes this assertion mean
        // anything).
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
