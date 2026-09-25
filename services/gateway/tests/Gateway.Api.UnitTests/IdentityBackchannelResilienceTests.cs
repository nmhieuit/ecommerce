using Gateway.Api.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Gateway.Api.UnitTests;

/// <summary>
/// Spec 020 (timeout/retry/circuit breaker) FR-001 / research.md Decision 4: backchannel của JwtBearer
/// (fetch OIDC discovery + JWKS từ identity server) là 1 lời gọi ra ngoài như mọi lời gọi khác, và
/// phải khai báo TƯỜNG MINH 1 chính sách resilience thay vì dựa vào <see cref="System.Net.Http.HttpClient"/>
/// mặc định ngầm của framework.
/// </summary>
/// <remarks>
/// Gateway đăng ký scheme JwtBearer qua <c>AddToggleGatedIdentity</c> chứ không qua
/// <c>AddIdentityValidation</c> dùng chung của mọi service khác (xem remarks của class đó), nên cần
/// bản sao riêng của bản sửa này và bản sao riêng của test này — phản chiếu
/// <c>Bff.Api.UnitTests/IdentityBackchannelResilienceTests.cs</c>, kể cả lý do khẳng định đặt ở tầng
/// đăng ký <c>IHttpClientFactory</c> thay vì kiểm tra thẳng <c>JwtBearerOptions.Backchannel</c>
/// (thuộc tính đó khác null theo mặc định của framework).
/// </remarks>
public class IdentityBackchannelResilienceTests
{
    private const string BackchannelClientName = "IdentityBackchannel";

    /// <summary>
    /// Kiểm tra: sau `AddToggleGatedIdentity` (đường đăng ký riêng của gateway), client đặt tên
    /// `IdentityBackchannel` có pipeline resilience gắn vào.
    /// Lý do: FR-001 — gateway cũng gọi identity server (OIDC/JWKS) và không được dựa vào timeout ngầm
    /// 60 giây của framework; gateway không dùng chung helper của các service khác nên cần test riêng.
    /// Task nguồn: spec 020 (timeout/retry/circuit breaker) — FR-001, research.md Decision 4.
    /// </summary>
    [Fact]
    public void AddToggleGatedIdentity_RegistersAResiliencePipelineForTheBackchannelClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddToggleGatedIdentity(BuildConfiguration());

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
