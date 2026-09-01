using Microsoft.Extensions.Configuration;

namespace Identity.UnitTests;

/// <summary>
/// data-model.md — cấu hình xác thực: <see cref="IdentityServerOptions"/> là nơi
/// <c>AddIdentityValidation()</c> đọc Authority/Audience từ, nên việc bind đúng từ configuration
/// section "Identity" là điều kiện tiên quyết cho mọi service gọi nó (research.md Decision 4).
/// </summary>
public class IdentityServerOptionsTests
{
    [Fact]
    public void Get_BindsAuthorityAndAudience_FromTheIdentityConfigurationSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Authority"] = "http://identity-api:8080",
                ["Identity:Audience"] = "ecommerce-api",
            })
            .Build();

        // The section name is a constant on the type itself (single source of truth — no string
        // literal duplicated between this test and the extension that binds it in production).
        var options = configuration.GetSection(IdentityServerOptions.ConfigSectionName).Get<IdentityServerOptions>();

        Assert.NotNull(options);
        Assert.Equal("http://identity-api:8080", options.Authority);
        Assert.Equal("ecommerce-api", options.Audience);
    }

    [Fact]
    public void Get_ReturnsNull_WhenTheSectionIsAbsent()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = configuration.GetSection(IdentityServerOptions.ConfigSectionName).Get<IdentityServerOptions>();

        // IConfigurationSection.Get<T>() returns null, not an all-default instance, when the
        // section has no value and no children — asserted explicitly because
        // IdentityValidationExtensions.AddIdentityValidation relies on exactly this behavior to
        // decide when to fall back to `new IdentityServerOptions()` (data-model.md — no state is
        // treated as a hidden default; a silent all-null Authority would fail confusingly later).
        Assert.Null(options);
    }
}
