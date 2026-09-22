using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Api.IntegrationTests;

/// <summary>
/// The storefront calls the gateway from its own origin, so the browser will not send a request at
/// all unless the gateway admits that origin.
/// </summary>
/// <remarks>
/// This suite exists because the gap it covers was shipped and then found by the end-to-end
/// walkthrough (004-minimal-shopping-spa T065). Nothing before it could have caught the problem:
/// the component tests mock fetch, which has no notion of origins, and every manual check used
/// curl, which does not enforce CORS. The only honest way to keep it fixed is an assertion that
/// speaks in preflights.
/// </remarks>
public class StorefrontCorsTests
{
    private const string StorefrontOrigin = "http://localhost:5173";

    /// <summary>
    /// Kiểm tra: preflight (`OPTIONS /bff/products`, GET) từ "http://localhost:5173" nhận
    /// `Access-Control-Allow-Origin` đúng origin đó. Các test dưới dựng gateway trong bộ nhớ bằng
    /// `WebApplicationFactory`; origin lấy từ cấu hình `Cors:AllowedOrigins`.
    /// Lý do: trình duyệt không gửi request thật nếu preflight bị từ chối; lỗi này lọt ở spec 004
    /// vì test component giả lập fetch (không có origin) và curl không thực thi CORS.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T065, bổ sung cho spec 005 T023 (US2).
    /// </summary>
    [Fact]
    public async Task APreflightFromTheStorefront_IsAllowed()
    {
        await using var gateway = CreateGateway();
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/bff/products");
        request.Headers.Add("Origin", StorefrontOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau. response.Headers.GetValues(tên) lấy
        // các giá trị của header (thiếu header thì tự ném lỗi, test đỏ). Assert.Single(tập hợp):
        // xanh khi có đúng 1 phần tử và trả phần tử đó ra. Đỏ khi thiếu header, trả `*` hoặc origin
        // khác.
        Assert.Equal(
            StorefrontOrigin,
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    /// <summary>
    /// Kiểm tra: preflight `/bff/basket` (POST) trả `Access-Control-Allow-Credentials: true` và
    /// Allow-Origin là origin cụ thể, không phải `*`.
    /// Lý do: client gửi credentials, chuẩn CORS cấm trả `*` trong trường hợp đó nên origin phải
    /// được liệt kê tường minh.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T065, bổ sung cho spec 005 T023 (US2).
    /// </summary>
    [Fact]
    public async Task APreflightFromTheStorefront_AllowsCredentials()
    {
        await using var gateway = CreateGateway();
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/bff/basket");
        request.Headers.Add("Origin", StorefrontOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau. response.Headers.GetValues(tên) lấy
        // các giá trị của header (thiếu header thì tự ném lỗi, test đỏ). Assert.Single(tập hợp):
        // xanh khi có đúng 1 phần tử và trả phần tử đó ra. Đỏ khi thiếu Allow-Credentials hoặc khác
        // "true" (trình duyệt không cho SPA gửi kèm thông tin xác thực).
        Assert.Equal(
            "true",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
        // Assert.NotEqual(giá trị cấm, thực tế): ngược Assert.Equal, đỏ khi 2 giá trị bằng nhau,
        // tức gateway trả `*` cho Allow-Origin.
        Assert.NotEqual("*", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    /// <summary>
    /// Kiểm tra: preflight từ origin chưa cấu hình ("http://evil.example") không nhận header
    /// `Access-Control-Allow-Origin`.
    /// Lý do: CORS phải là danh sách cho phép, không phải cửa mở toang cho trang web bất kỳ.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T065, bổ sung cho spec 005 T023 (US2).
    /// </summary>
    [Fact]
    public async Task ARequestFromAnUnknownOrigin_IsNotAdmitted()
    {
        await using var gateway = CreateGateway();
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/bff/products");
        request.Headers.Add("Origin", "http://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        // Assert.False(điều kiện): xanh khi điều kiện sai. response.Headers.Contains(tên) là true
        // nếu phản hồi có header đó; đỏ khi header xuất hiện, tức gateway cấp quyền cho origin
        // không ai cấu hình.
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    /// <summary>
    /// Kiểm tra: origin chỉ tồn tại trong cấu hình bơm lúc chạy ("https://storefront.example") vẫn
    /// được chấp nhận, tức danh sách đọc từ cấu hình chứ không viết cứng.
    /// Lý do: mỗi triển khai khai tên origin của mình mà không phải build lại image
    /// (`docker-compose.yml` truyền `Cors__AllowedOrigins__0`).
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T065, bổ sung cho spec 005 T023 (US2).
    /// </summary>
    [Fact]
    public async Task TheAllowedOrigins_ComeFromConfiguration()
    {
        const string ConfiguredOrigin = "https://storefront.example";

        await using var gateway = CreateGateway(ConfiguredOrigin);
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/bff/products");
        request.Headers.Add("Origin", ConfiguredOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau. response.Headers.GetValues(tên) lấy
        // các giá trị của header (thiếu header thì tự ném lỗi, test đỏ). Assert.Single(tập hợp):
        // xanh khi có đúng 1 phần tử và trả phần tử đó ra. Đỏ khi thiếu header, nghĩa là mã bỏ qua
        // cấu hình (có thể dùng danh sách cứng).
        Assert.Equal(
            ConfiguredOrigin,
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    /// <summary>
    /// Kiểm tra: `GET /health/live` không có header `Origin` vẫn trả 200 và không bị thêm header
    /// `Access-Control-Allow-Origin`.
    /// Lý do: health probe, curl và lời gọi cùng origin không mang `Origin`; CORS không được đổi
    /// hành vi của chúng.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T065, bổ sung cho spec 005 T023 (US2).
    /// </summary>
    [Fact]
    public async Task ARequestWithNoOrigin_IsUntouched()
    {
        await using var gateway = CreateGateway();
        var client = gateway.CreateClient();

        var response = await client.GetAsync("/health/live");

        // Assert.Equal(kỳ vọng, thực tế): so mã HTTP với 200; đỏ khi mã khác (401, 500...), CORS đã
        // làm hỏng request bình thường.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Assert.False(điều kiện): xanh khi điều kiện sai. Đỏ khi phản hồi có header Allow-Origin,
        // tức CORS đang chạm vào request không liên quan.
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    /// <summary>
    /// Kiểm tra: khi cấu hình cho phép cả "http://localhost:5173" và "http://localhost:4173",
    /// preflight từ mỗi origin đều nhận lại đúng origin đó (`[Theory]` chạy 2 lần).
    /// Lý do: storefront chạy 2 kiểu: dev server cổng 5173 và container cổng 4173 trong stack 1
    /// lệnh, nên chính sách phải xử lý nhiều origin.
    /// Task nguồn: spec 005 — T023, US2 (research.md Decision 7).
    /// </summary>
    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://localhost:4173")]
    public async Task EachConfiguredOrigin_IsAdmitted(string origin)
    {
        await using var gateway = CreateGateway("http://localhost:5173", "http://localhost:4173");
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/bff/products");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau. response.Headers.GetValues(tên) lấy
        // các giá trị của header (thiếu header thì tự ném lỗi, test đỏ). Assert.Single(tập hợp):
        // xanh khi có đúng 1 phần tử và trả phần tử đó ra. Đỏ ở lần chạy nào thì origin đó chưa
        // được chấp nhận (nhận `*` hoặc origin kia đều đỏ).
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    /// <summary>
    /// Kiểm tra: file cấu hình thật `appsettings.Development.json` của gateway (không phải cấu hình
    /// giả) đã cho phép cả 2 origin trên (`[Theory]` chạy 2 lần).
    /// Lý do: các test khác tự cấp origin nên chỉ chứng minh chính sách chạy đúng, không chứng minh
    /// repo được cấu hình cho storefront nó phát hành; đây là cách spec 004 phát hành gateway không
    /// có CORS mà test vẫn xanh.
    /// Task nguồn: spec 005 — T023, US2 (research.md Decision 7).
    /// </summary>
    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://localhost:4173")]
    public async Task TheDevelopmentConfiguration_Admits_BothStorefrontOrigins(string origin)
    {
        await using var gateway = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/bff/products");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        // Assert.True(điều kiện, thông báo): xanh khi điều kiện đúng; thông báo hiện khi đỏ. Điều
        // kiện: phản hồi có header Allow-Origin (chỉ kiểm sự có mặt, không so giá trị). Đỏ khi
        // origin không nằm trong allow-list của file cấu hình thật, storefront ở đó sẽ bị trình
        // duyệt chặn.
        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            $"'{origin}' is not in the gateway's configured allow-list, so a storefront served there "
            + "would be blocked by the browser before any request left it.");
    }

    /// <summary>
    /// Kiểm tra: phản hồi thật (`GET /bff/products`, có `Origin`) có
    /// `Access-Control-Expose-Headers` chứa "X-Correlation-Id", để JavaScript của SPA đọc được.
    /// Lý do: chỉ Expose-Headers mới cho script đọc header tuỳ chỉnh (DevTools luôn thấy nên không
    /// phát hiện được), và chỉ phản hồi thật mới mang header này, preflight thì không.
    /// Task nguồn: spec 016 (lan truyền correlation ID) — research.md Decision 5; nằm cùng file với
    /// bộ test CORS.
    /// </summary>
    [Fact]
    public async Task AnActualCrossOriginResponse_ExposesTheCorrelationIdHeaderToScript()
    {
        await using var gateway = CreateGateway();
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Add("Origin", StorefrontOrigin);

        var response = await client.SendAsync(request);

        // Assert.Contains(giá trị, tập hợp): xanh khi tập hợp có ít nhất 1 phần tử bằng giá trị đó.
        // GetValues thiếu header thì tự ném lỗi. Đỏ khi thiếu Expose-Headers hoặc không liệt kê
        // "X-Correlation-Id": code SPA gọi response.headers.get(...) sẽ nhận null.
        Assert.Contains(
            "X-Correlation-Id",
            response.Headers.GetValues("Access-Control-Expose-Headers"));
    }

    private static WebApplicationFactory<Program> CreateGateway(params string[] origins)
    {
        var configured = origins.Length == 0 ? [StorefrontOrigin] : origins;

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                configured
                    .Select((origin, index) =>
                        new KeyValuePair<string, string?>($"Cors:AllowedOrigins:{index}", origin))
                    .ToDictionary())));
    }
}
