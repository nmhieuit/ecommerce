using IntegrationTestSupport;
using Microsoft.Data.SqlClient;

namespace IntegrationTestSupport.Tests;

/// <summary>
/// Proves <see cref="SqlServerFixture"/> starts a real SQL Server container and that a real client
/// can run a query against it — same smoke-test shape 010-testcontainers-integration-tests gave
/// <see cref="RedisFixture"/> and <see cref="RabbitMqFixture"/>.
/// </summary>
public class SqlServerFixtureTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    /// <summary>
    /// Kiểm tra: mở `SqlConnection` thật tới container do `SqlServerFixture` dựng rồi chạy `SELECT 1`
    /// — kết quả trả về phải đúng bằng 1.
    /// Lý do: chứng minh fixture dùng chung khởi động được SQL Server thật và nhận truy vấn được,
    /// độc lập với mọi service đang dùng nó.
    /// Task nguồn: gộp `SqlServerFixture` của từng service vào `shared/IntegrationTestSupport` — việc
    /// spec 010 (research.md Decision 3) để lại làm sau.
    /// </summary>
    [Fact]
    public async Task SqlServerFixture_AnswersARealQuery()
    {
        await using var connection = new SqlConnection(sqlServer.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT 1", connection);

        var result = await command.ExecuteScalarAsync();

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. SQL Server thật phải trả
        // đúng 1; đỏ khi container không khởi động được hoặc chuỗi kết nối sai.
        Assert.Equal(1, result);
    }
}
