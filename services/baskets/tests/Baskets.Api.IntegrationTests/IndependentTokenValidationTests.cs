using System.Net;
using System.Net.Http.Headers;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Baskets.Api.IntegrationTests;

/// <summary>
/// spec US2 Acceptance Scenario 2/3, Test Scenario 2: this service authenticates a request's token
/// independently — it does not trust that the gateway already did, and a request reaching it
/// directly (bypassing the gateway entirely) is still validated. spec FR-011: no token is exactly
/// as unauthenticated as an invalid one.
/// </summary>
/// <remarks>
/// Uses <c>IntegrationTestSupport.TestJwtBearer</c> (shared across every service's integration
/// tests) rather than a locally-issued token: <c>FallbackPolicy</c> rejects an unauthenticated
/// request before the endpoint ever resolves a tenant or touches persistence, so this suite needs
/// no running identity server or database.
/// </remarks>
public class IndependentTokenValidationTests
{
    private static readonly string BasketRoute = $"/baskets/{Guid.NewGuid():D}";

    /// <summary>
    /// Kiểm tra: gọi thẳng `Baskets.Api` (không qua gateway/BFF) mà không kèm token nào bị chính
    /// service này từ chối `401`.
    /// Lý do: FR-004/FR-011 — service không được tin rằng "gateway đã kiểm rồi", phải tự xác thực
    /// độc lập; không token thì không có danh tính mặc định.
    /// Task nguồn: spec 014 (máy chủ định danh thật) — US2, US2 Acceptance Scenario 2/3 (FR-011).
    /// </summary>
    [Fact]
    public async Task ARequestWithNoToken_IsRejected()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(BasketRoute);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: token hợp lệ bị nối thêm chuỗi rác (làm hỏng chữ ký), gửi thẳng tới `Baskets.Api`
    /// (bỏ qua gateway hoàn toàn) — vẫn bị chính service này từ chối `401`.
    /// Lý do: US2 Test Scenario 2 — chứng minh "phòng thủ theo chiều sâu" thật, không phải giả định:
    /// dù không đi qua gateway, service vẫn tự phát hiện chữ ký sai.
    /// Task nguồn: spec 014 (máy chủ định danh thật) — US2, Test Scenario 2 (FR-005, SC-002).
    /// </summary>
    [Fact]
    public async Task ARequestWithATamperedToken_IsRejected()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, BasketRoute);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtBearer.CreateToken() + "tampered");

        var response = await client.SendAsync(request);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: `/health/live` và `/health/ready` vẫn trả lời bình thường (không phải `401`) dù
    /// không có token nào (`[Theory]` chạy 2 lần, mỗi lần 1 route từ `[InlineData]`).
    /// Lý do: research.md Decision 6 — health probe là ngoại lệ `[AllowAnonymous]` DUY NHẤT; nếu
    /// deny-by-default lỡ áp cả lên đây, Kubernetes sẽ không bao giờ thăm dò được service.
    /// Task nguồn: spec 014 (máy chủ định danh thật) — research.md Decision 6.
    /// </summary>
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task AHealthProbe_RemainsAnonymous_EvenWithNoToken(string route)
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(route);

        // Assert.NotEqual(giá trị cấm, thực tế): xanh khi khác nhau, đỏ khi bằng nhau.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseTestJwtBearer().UseUnreachableRequiredSecret("BasketsDb"));
}
