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
    /// <summary>
    /// Kiểm tra: chèn 1 `Party` với `DisplayName` dài 201 ký tự thẳng qua `PartiesDbContext` (SQL
    /// Server thật) — phải bị `SaveChangesAsync` từ chối.
    /// Lý do: chứng minh cột `nvarchar(200)` là ràng buộc THẬT của SQL Server, không phải guard tầng
    /// ứng dụng; dùng provider giả (in-memory) sẽ không bắt được lỗi này.
    /// Task nguồn: spec 010 (hạ tầng kiểm thử container thật) — T010/T011, US1 (FR-001, FR-002).
    /// </summary>
    [Fact]
    public async Task DisplayName_ExceedingMaxLength_IsRejectedByTheDatabase()
    {
        await using var context = await CreateContextAsync("party-constraints-display-name-length");

        context.Parties.Add(new Party
        {
            Id = Guid.NewGuid(),
            DisplayName = new string('a', 201),
        });

        // Assert.ThrowsAsync(loại ngoại lệ, đoạn mã): xanh khi đoạn mã ném đúng loại ngoại lệ, đỏ
        // khi không ném hoặc ném loại khác. Cột nvarchar(200) thật phải từ chối giá trị quá dài nên
        // SaveChangesAsync ném DbUpdateException; đỏ nếu ai đó nới cột lên (ví dụ HasMaxLength(500)).
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
