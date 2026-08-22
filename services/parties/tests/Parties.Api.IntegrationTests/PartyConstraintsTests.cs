using Microsoft.EntityFrameworkCore;
using Parties.Api.Data;

namespace Parties.Api.IntegrationTests;

/// <summary>
/// 010-testcontainers-integration-tests spec FR-001, FR-002: proves the SQL Server Testcontainers
/// pattern still catches a real database-level constraint violation, not just an application-level
/// guard. Goes straight at <see cref="PartiesDbContext"/> rather than through the HTTP surface —
/// the <c>nvarchar(200)</c> column bound on <see cref="Party.DisplayName"/>, not any endpoint
/// behaviour, is the subject under test.
/// </summary>
public class PartyConstraintsTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task DisplayName_ExceedingMaxLength_IsRejectedByTheDatabase()
    {
        await using var context = await CreateContextAsync("party-constraints-display-name-length");

        context.Parties.Add(new Party
        {
            Id = Guid.NewGuid(),
            DisplayName = new string('a', 201),
        });

        // The real nvarchar(200) column rejects the over-length value; an in-memory provider would
        // not (research.md Decision 4 — Testcontainers.MsSql is what makes this assertion mean
        // anything).
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private async Task<PartiesDbContext> CreateContextAsync(string database)
    {
        var connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            sqlServer.ConnectionString)
        {
            InitialCatalog = database,
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<PartiesDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        var context = new PartiesDbContext(options);
        await context.Database.MigrateAsync();

        return context;
    }
}
