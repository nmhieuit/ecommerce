using Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Bff.Api.UnitTests;

/// <summary>
/// Spec 020 (timeout/retry/circuit breaker) FR-001 / research.md Decision 4: backchannel của JwtBearer
/// (fetch OIDC discovery + JWKS từ identity server) là 1 lời gọi ra ngoài như mọi lời gọi khác, và
/// phải khai báo TƯỜNG MINH 1 chính sách resilience thay vì dựa vào <see cref="System.Net.Http.HttpClient"/>
/// mặc định ngầm của framework.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddIdentityValidation</c> (<c>shared/Identity/IdentityValidationExtensions.cs</c>) là đăng ký
/// duy nhất mà mọi service không phải gateway dùng chung (parties, products, orders, baskets, và BFF
/// ở đây) — sửa 1 lần ở đó bao phủ cả 5 (research.md Decision 4). Scheme riêng của gateway được test
/// tách ở <c>Gateway.Api.UnitTests/IdentityBackchannelResilienceTests.cs</c>, vì gateway không gọi
/// trực tiếp helper này được (xem remarks của <c>ToggleGatedAuthenticationExtensions</c>).
/// </para>
/// <para>
/// Khẳng định đặt ở tầng đăng ký <c>IHttpClientFactory</c>, không kiểm tra thẳng
/// <c>JwtBearerOptions.Backchannel</c>: thuộc tính đó khác null kể cả TRƯỚC tính năng này (mặc định
/// của framework là 1 <c>HttpClient</c> trơn gán lúc dựng options), nên kiểm tra null không phân biệt
/// được "resilience pipeline tường minh" với "mặc định framework". <c>AddHttpClient("IdentityBackchannel")
/// .AddStandardResilienceHandler(...)</c> có chạy thật hay không thể hiện qua số
/// <see cref="HttpClientFactoryOptions.HttpMessageHandlerBuilderActions"/> đăng ký thêm cho tên đó —
/// 1 tên chưa ai cấu hình thì không có action nào.
/// </para>
/// </remarks>
public class IdentityBackchannelResilienceTests
{
    private const string BackchannelClientName = "IdentityBackchannel";

    /// <summary>
    /// Kiểm tra: sau `AddIdentityValidation`, client đặt tên `IdentityBackchannel` có nhiều
    /// handler-builder action hơn 1 tên client chưa từng đăng ký (nghĩa là có pipeline resilience gắn
    /// vào).
    /// Lý do: FR-001 — không lời gọi ra ngoài nào (kể cả fetch OIDC/JWKS ít được để ý) được dựa vào
    /// timeout ngầm 60 giây của framework.
    /// Task nguồn: spec 020 (timeout/retry/circuit breaker) — FR-001, research.md Decision 4.
    /// </summary>
    [Fact]
    public void AddIdentityValidation_RegistersAResiliencePipelineForTheBackchannelClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityValidation(BuildConfiguration());

        var factoryOptions = services.BuildServiceProvider()
            .GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>();

        var configured = factoryOptions.Get(BackchannelClientName);
        var neverRegistered = factoryOptions.Get("a-name-nobody-configured");

        // Assert.True(điều kiện, thông báo): xanh khi client backchannel có NHIỀU handler-builder action
        // hơn tên client chưa đăng ký (đã gắn resilience); đỏ kèm thông báo khi không nhiều hơn.
        Assert.True(
            configured.HttpMessageHandlerBuilderActions.Count > neverRegistered.HttpMessageHandlerBuilderActions.Count,
            $"Expected '{BackchannelClientName}' to have a resilience handler pipeline attached via "
            + "AddStandardResilienceHandler(), but it has no more handler-builder actions than a "
            + "client name nobody registered.");
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Authority"] = "http://identity-api:8080",
                ["Identity:Audience"] = "ecommerce-api",
            })
            .Build();
}
