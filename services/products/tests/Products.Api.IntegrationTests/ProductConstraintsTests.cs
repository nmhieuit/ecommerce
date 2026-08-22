using Microsoft.EntityFrameworkCore;
using Products.Api.Data;

namespace Products.Api.IntegrationTests;

/// <summary>
/// 010-testcontainers-integration-tests spec FR-001, FR-002: proves the SQL Server Testcontainers
/// pattern still catches a real database-level constraint violation, not just an application-level
/// guard. Goes straight at <see cref="ProductsDbContext"/> rather than through the HTTP surface —
/// the <c>nvarchar(200)</c> column bound on <see cref="Product.Name"/>, not any endpoint behaviour,
/// is the subject under test.
/// </summary>
public class ProductConstraintsTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task Name_ExceedingMaxLength_IsRejectedByTheDatabase()
    {
        await using var context = await CreateContextAsync("product-constraints-name-length");

        context.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            Name = new string('a', 201),
            Price = 1m,
        });

        // The real nvarchar(200) column rejects the over-length value; an in-memory provider would
        // not (research.md Decision 4 — Testcontainers.MsSql is what makes this assertion mean
        // anything).
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private async Task<ProductsDbContext> CreateContextAsync(string database)
    {
        var connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            sqlServer.ConnectionString)
        {
            InitialCatalog = database,
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var context = new ProductsDbContext(options);
        await context.Database.MigrateAsync();

        return context;
    }
}
