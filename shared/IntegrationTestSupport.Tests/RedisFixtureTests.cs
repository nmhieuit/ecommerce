using IntegrationTestSupport;
using StackExchange.Redis;

namespace IntegrationTestSupport.Tests;

/// <summary>
/// 010-testcontainers-integration-tests spec FR-003, FR-005: proves <see cref="RedisFixture"/>
/// starts a real Redis container and that a real client can read and write against it.
/// </summary>
public class RedisFixtureTests(RedisFixture redis) : IClassFixture<RedisFixture>
{
    /// <summary>
    /// Kiểm tra: kết nối `StackExchange.Redis` thật tới container do `RedisFixture` dựng, ghi 1 khoá
    /// rồi đọc lại — giá trị đọc được phải khớp giá trị vừa ghi.
    /// Lý do: chứng minh fixture dùng chung khởi động được container Redis thật và đọc/ghi được,
    /// trước khi bất kỳ service nào cần dùng tới nó.
    /// Task nguồn: spec 010 (hạ tầng kiểm thử container thật) — T015-T017, US2 (FR-003, FR-005).
    /// </summary>
    [Fact]
    public async Task RedisFixture_Roundtrips_ARealValue()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var database = connection.GetDatabase();

        await database.StringSetAsync("010-smoke-test-key", "010-smoke-test-value");
        var value = await database.StringGetAsync("010-smoke-test-key");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Giá trị đọc lại từ Redis
        // thật phải đúng giá trị vừa ghi; đỏ khi container không khởi động được hoặc kết nối sai.
        Assert.Equal("010-smoke-test-value", value);
    }
}
