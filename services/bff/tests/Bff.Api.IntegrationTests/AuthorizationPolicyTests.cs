using System.Net;
using System.Net.Http.Headers;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// Kiểm tra: token đã xác thực hợp lệ nhưng thiếu claim `scope` mà policy `ApiScope` đòi hỏi bị từ chối
/// đúng `403`, không được xử lý như thể là `200`.
/// Lý do: spec 015 (phân quyền từ chối theo mặc định) US1 Acceptance Scenario 2, Test Scenario 2.
/// Lưu ý: dùng đúng route đã khai báo thật (`/bff/products`), không dùng 1 path chưa map — path chưa
/// map cũng bị `FallbackPolicy` của framework chặn bất kể tính năng này có hay không, nên sẽ không
/// chứng minh được điều gì về khai báo tường minh theo từng route mà T028 thêm vào.
/// </summary>
public class AuthorizationPolicyTests
{
    /// <summary>
    /// Task nguồn: spec 015 (phân quyền từ chối theo mặc định) — US1 Acceptance Scenario 2, Test
    /// Scenario 2 (FR-003).
    /// </summary>
    [Fact]
    public async Task ARequestWithATokenMissingTheApiScopeClaim_IsForbidden()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
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

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtBearer.CreateToken(includeApiScope: true));

        var response = await client.SendAsync(request);

        // Assert.NotEqual(giá trị cấm, thực tế): xanh khi khác nhau, đỏ khi bằng nhau.
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        // Assert.NotEqual(giá trị cấm, thực tế): xanh khi khác nhau, đỏ khi bằng nhau.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseTestJwtBearer());
}
