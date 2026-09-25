using System.Net;
using System.Net.Http.Headers;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Baskets.Api.IntegrationTests;

/// <summary>
/// Kiểm tra: token đã xác thực hợp lệ nhưng thiếu claim `scope` mà policy `ApiScope` đòi hỏi bị từ chối
/// đúng `403`, không được xử lý như thể là `200`.
/// Lý do: spec 015 (phân quyền từ chối theo mặc định) US1 Acceptance Scenario 2, Test Scenario 2.
/// Lưu ý: policy này được gạt bởi toggle (research.md Decision 5) — `appsettings.Development.json` bật
/// toggle, đúng là file `WebApplicationFactory` nạp mặc định khi chạy test.
/// </summary>
public class AuthorizationPolicyTests
{
    private static readonly string BasketRoute = $"/baskets/{Guid.NewGuid():D}";

    /// <summary>
    /// Task nguồn: spec 015 (phân quyền từ chối theo mặc định) — US1 Acceptance Scenario 2, Test
    /// Scenario 2 (FR-003).
    /// </summary>
    [Fact]
    public async Task ARequestWithATokenMissingTheApiScopeClaim_IsForbidden()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, BasketRoute);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtBearer.CreateToken(includeApiScope: false));

        var response = await client.SendAsync(request);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — phải đúng `403 Forbidden`,
        // không phải `401`/`200`.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: token có đủ claim `scope` không bị từ chối vì lý do phân quyền.
    /// Lý do phải test: chặn hồi quy — đảm bảo việc thêm policy `ApiScope` không vô tình chặn luôn cả
    /// những request hợp lệ đã có đủ scope.
    /// Task nguồn: spec 015 (phân quyền từ chối theo mặc định) — regression guard cho FR-003.
    /// </summary>
    [Fact]
    public async Task ARequestWithATokenCarryingTheApiScopeClaim_IsNotRejectedForAuthorization()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, BasketRoute);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtBearer.CreateToken(includeApiScope: true));

        var response = await client.SendAsync(request);

        // Id giỏ hàng này có tồn tại hay không (404) không phải điều test quan tâm — chỉ cần request
        // không bị từ chối vì lý do phân quyền.
        // Assert.NotEqual(giá trị cấm, thực tế): xanh khi khác nhau, đỏ khi bằng nhau.
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        // Assert.NotEqual(giá trị cấm, thực tế): xanh khi khác nhau, đỏ khi bằng nhau.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseTestJwtBearer().UseUnreachableRequiredSecret("BasketsDb"));
}
