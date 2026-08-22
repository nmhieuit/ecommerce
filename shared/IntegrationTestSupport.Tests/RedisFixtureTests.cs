using IntegrationTestSupport;
using StackExchange.Redis;

namespace IntegrationTestSupport.Tests;

/// <summary>
/// 010-testcontainers-integration-tests spec FR-003, FR-005: proves <see cref="RedisFixture"/>
/// starts a real Redis container and that a real client can read and write against it.
/// </summary>
public class RedisFixtureTests(RedisFixture redis) : IClassFixture<RedisFixture>
{
    [Fact]
    public async Task RedisFixture_Roundtrips_ARealValue()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        var database = connection.GetDatabase();

        await database.StringSetAsync("010-smoke-test-key", "010-smoke-test-value");
        var value = await database.StringGetAsync("010-smoke-test-key");

        Assert.Equal("010-smoke-test-value", value);
    }
}
