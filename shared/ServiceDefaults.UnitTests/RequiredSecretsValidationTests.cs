using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ServiceDefaults;

namespace ServiceDefaults.UnitTests;

/// <summary>
/// Spec 018 (secrets qua cluster secret store) FR-007: 1 service thiếu 1 secret đã khai báo là bắt
/// buộc phải dừng khởi động ngay (fail-fast) với lý do rõ ràng, có cấu trúc — không bao giờ khởi
/// động ở trạng thái chưa xác định, không bao giờ để lộ giá trị thật của secret. `data-model.md` §1
/// (`RequiredSecret`) và `research.md` #3 chỉ cho phép đúng 2 kết quả — validate thành công, hoặc
/// thất bại kèm tên secret còn thiếu — bộ test này giữ cho không có kết quả thứ 3 ("vẫn khởi động
/// bình thường") âm thầm xuất hiện.
/// </summary>
public class RequiredSecretsValidationTests
{
    private static IConfiguration ConfigurationWith(params (string Key, string? Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => p.Value))
            .Build();

    /// <summary>
    /// Kiểm tra: secret có giá trị hợp lệ (kèm credential thật) thì validate thành công.
    /// Lý do phải test: chặn hồi quy — đảm bảo trường hợp bình thường (secret đã cấp đúng) không
    /// vô tình bị validate chặn nhầm.
    /// Task nguồn: spec 018 (secrets qua cluster secret store) — FR-002/FR-007.
    /// </summary>
    [Fact]
    public void Validate_Succeeds_WhenEveryRequiredSecretHasAValue()
    {
        var configuration = ConfigurationWith(("ConnectionStrings:OrdersDb", "Server=orders-db;User Id=sa;Password=Sup3r$ecret;..."));
        var validator = new RequiredSecretsValidator(configuration);
        var options = new RequiredSecretsOptions { Secrets = [RequiredSecret.ConnectionString("OrdersDb")] };

        var result = validator.Validate(name: null, options);

        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai — validate phải thành công.
        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Kiểm tra: secret không tồn tại trong configuration (không có key nào) thì validate thất bại.
    /// Lý do: FR-007 — thiếu hoàn toàn secret là ca cơ bản nhất phải chặn được.
    /// Task nguồn: spec 018 (secrets qua cluster secret store) — FR-007.
    /// </summary>
    [Fact]
    public void Validate_Fails_WhenARequiredSecretIsMissing()
    {
        var configuration = ConfigurationWith();
        var validator = new RequiredSecretsValidator(configuration);
        var options = new RequiredSecretsOptions { Secrets = [RequiredSecret.ConnectionString("OrdersDb")] };

        var result = validator.Validate(name: null, options);

        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai — validate phải thất bại.
        Assert.True(result.Failed);
    }

    /// <summary>
    /// Kiểm tra: secret có key nhưng giá trị rỗng/chỉ có khoảng trắng/tab thì vẫn bị coi là thiếu.
    /// Lý do phải test: rỗng không phải là 1 giá trị — cùng quy tắc `Tenancy.UnitTests` áp dụng cho
    /// `TenantContext`; 1 biến môi trường lỡ đặt thành chuỗi rỗng không được coi là "đã cấp".
    /// Task nguồn: spec 018 (secrets qua cluster secret store) — FR-007.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Validate_Fails_WhenARequiredSecretIsBlank(string blank)
    {
        var configuration = ConfigurationWith(("ConnectionStrings:OrdersDb", blank));
        var validator = new RequiredSecretsValidator(configuration);
        var options = new RequiredSecretsOptions { Secrets = [RequiredSecret.ConnectionString("OrdersDb")] };

        var result = validator.Validate(name: null, options);

        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai — giá trị trắng cũng phải bị
        // coi là thiếu, không được coi là "đã cấp".
        Assert.True(result.Failed);
    }

    /// <summary>
    /// Kiểm tra: khi có nhiều secret khai báo, thông báo lỗi chỉ nêu TÊN secret còn thiếu, không
    /// bao giờ in ra GIÁ TRỊ thật của bất kỳ secret nào (kể cả secret đã có sẵn).
    /// Lý do: an toàn bảo mật cơ bản — 1 thông báo lỗi khởi động thường lọt vào log tập trung; nếu
    /// vô tình in giá trị secret ra đó, log tập trung trở thành 1 kênh rò rỉ secret mới.
    /// Task nguồn: spec 018 (secrets qua cluster secret store) — FR-007 ("never leak the secret's
    /// value").
    /// </summary>
    [Fact]
    public void FailureMessage_NamesTheMissingSecret_ButNeverASecretValue()
    {
        var configuration = ConfigurationWith(("ConnectionStrings:PartiesDb", "Server=parties-db;Password=TopSecret123;..."));
        var validator = new RequiredSecretsValidator(configuration);
        var options = new RequiredSecretsOptions
        {
            Secrets =
            [
                RequiredSecret.ConnectionString("PartiesDb"),
                RequiredSecret.ConnectionString("MissingDb"),
            ],
        };

        var result = validator.Validate(name: null, options);

        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai.
        Assert.True(result.Failed);
        // Assert.Contains(chuỗi con, chuỗi): xanh khi thông báo lỗi có nêu đúng tên secret còn
        // thiếu; đỏ khi thiếu tên, khiến người vận hành không biết phải cấp secret nào.
        Assert.Contains("ConnectionStrings:MissingDb", result.FailureMessage);
        // Assert.DoesNotContain(chuỗi con, chuỗi): xanh khi thông báo KHÔNG chứa giá trị secret
        // thật; đỏ nếu giá trị secret bị rò rỉ vào thông báo lỗi.
        Assert.DoesNotContain("TopSecret123", result.FailureMessage);
    }

    /// <summary>
    /// Kiểm tra: khi không khai báo secret bắt buộc nào cả, validate luôn thành công.
    /// Lý do phải test: trường hợp biên — 1 service chưa cần secret nào (hoặc bị cấu hình sai,
    /// không truyền secret nào vào `AddRequiredSecretsValidation`) không được vô tình bị chặn.
    /// Task nguồn: spec 018 (secrets qua cluster secret store) — FR-007.
    /// </summary>
    [Fact]
    public void Validate_Succeeds_WhenNoSecretsAreDeclared()
    {
        var configuration = ConfigurationWith();
        var validator = new RequiredSecretsValidator(configuration);
        var options = new RequiredSecretsOptions();

        var result = validator.Validate(name: null, options);

        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai.
        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// Kiểm tra: `RequiredSecret.ConnectionString(name)` dựng đúng tên khoá theo quy ước
    /// `ConnectionStrings:<name>` chuẩn của .NET configuration.
    /// Lý do phải test: đây là bề mặt hợp đồng phải khớp CHÍNH XÁC giữa mã và
    /// `deploy/k8s/&lt;service&gt;/external-secret.yaml` (`secretKey`) — sai lệch tên khiến
    /// ExternalSecret không bao giờ ánh xạ đúng biến môi trường.
    /// Task nguồn: spec 018 (secrets qua cluster secret store) — FR-002, hợp đồng đặt tên secret.
    /// </summary>
    [Fact]
    public void ConnectionString_BuildsANameMatchingTheStandardConnectionStringsConfigurationKey()
    {
        var secret = RequiredSecret.ConnectionString("OrdersDb");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal("ConnectionStrings:OrdersDb", secret.Name);
    }

    /// <summary>
    /// Kiểm tra: connection string chỉ có host/database (không có `Password=`/`Pwd=`/
    /// `Integrated Security=true`/`Trusted_Connection=true`) phải bị coi là THIẾU secret, dù bản
    /// thân giá trị không hề rỗng.
    /// Lý do: phát hiện thật lúc viết `tasks.md` T019 (test tích hợp fail-fast của US2) —
    /// `appsettings.json` (không phải `.Development`) đã commit cố tình vẫn giữ 1 connection string
    /// hợp lệ chỉ có host/database, không credential
    /// (`contracts/service-configuration-contract.md` rule 2). Nếu chỉ kiểm tra rỗng/không-rỗng
    /// thông thường, giá trị đó sẽ KHÔNG BAO GIỜ fail — kể cả khi cluster chưa từng inject credential
    /// thật — đúng ca mà FR-007 sinh ra để chặn. `RequiredSecret.ConnectionString` phải resolve về
    /// "thiếu" (`null`) khi chuỗi không có phần credential, không chỉ khi hoàn toàn không tồn tại.
    /// Task nguồn: spec 018 (secrets qua cluster secret store) — FR-007, tasks.md T019.
    /// </summary>
    [Theory]
    [InlineData("Server=orders-db;Database=orders;TrustServerCertificate=True")]
    [InlineData("Server=orders-db;Database=orders;User Id=sa;TrustServerCertificate=True")]
    public void ConnectionString_TreatsAHostOnlyValueWithNoCredential_AsMissing(string credentialLessConnectionString)
    {
        var configuration = ConfigurationWith(("ConnectionStrings:OrdersDb", credentialLessConnectionString));
        var validator = new RequiredSecretsValidator(configuration);
        var options = new RequiredSecretsOptions { Secrets = [RequiredSecret.ConnectionString("OrdersDb")] };

        var result = validator.Validate(name: null, options);

        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai — chuỗi chỉ có host/database
        // (không credential) phải bị coi là thiếu secret.
        Assert.True(result.Failed);
    }

    /// <summary>
    /// Kiểm tra: connection string có 1 trong 4 dạng credential hợp lệ (`Password=`, `Pwd=`,
    /// `Integrated Security=true`, hoặc mang `User Id` cùng `Password=`) thì validate thành công.
    /// Lý do phải test: chặn hồi quy đối xứng với test phía trên — đảm bảo `HasCredential(...)`
    /// không siết quá tay, chặn nhầm cả những connection string hợp lệ thật.
    /// Task nguồn: spec 018 (secrets qua cluster secret store) — FR-007.
    /// </summary>
    [Theory]
    [InlineData("Server=orders-db;Database=orders;User Id=sa;Password=Sup3r$ecret;TrustServerCertificate=True")]
    [InlineData("Server=orders-db;Database=orders;Pwd=Sup3r$ecret;TrustServerCertificate=True")]
    [InlineData("Server=orders-db;Database=orders;Integrated Security=true;TrustServerCertificate=True")]
    public void ConnectionString_Succeeds_WhenACredentialComponentIsPresent(string credentialedConnectionString)
    {
        var configuration = ConfigurationWith(("ConnectionStrings:OrdersDb", credentialedConnectionString));
        var validator = new RequiredSecretsValidator(configuration);
        var options = new RequiredSecretsOptions { Secrets = [RequiredSecret.ConnectionString("OrdersDb")] };

        var result = validator.Validate(name: null, options);

        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai.
        Assert.True(result.Succeeded);
    }
}
