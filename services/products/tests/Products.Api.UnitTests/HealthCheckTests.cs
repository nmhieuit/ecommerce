using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Products.Api.UnitTests;

public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Kiểm tra: gọi GET /health/live trả về 200 OK.
    /// Lý do phải test: liveness không được chạm vào database — tiến trình .NET còn sống là phải trả
    /// 200 ngay, bất kể database có kết nối được hay không. Đây là ranh giới phân biệt liveness với
    /// readiness (xem ReadinessTests) mà FR-003 dựa vào.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T009, US1.
    /// </summary>
    [Fact]
    public async Task HealthLive_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
