using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Orders.Api.IntegrationTests;

/// <summary>
/// Spec 018 (secrets qua cluster secret store) FR-007, Independent Test của User Story 2: 1 service
/// chưa từng nhận được secret bắt buộc từ cluster secret store phải khởi động THẤT BẠI, kèm lý do rõ
/// ràng — không được khởi động ở trạng thái chưa xác định. `appsettings.json` (không phải
/// `.Development`) đã commit cố tình vẫn giữ 1 connection string chỉ có host/database, không
/// credential (`contracts/service-configuration-contract.md` rule 2) — đúng hình dạng 1 cluster thật
/// sẽ rơi vào nếu `ExternalSecret`/`Secret` chưa từng được nối — nên test này không cần xoá hẳn key,
/// chỉ cần không cấp credential cho nó, khớp đúng thực tế production hơn là 1 ca giả lập "cấu hình
/// hoàn toàn không tồn tại".
/// </summary>
public class RequiredSecretsFailFastTests
{
    /// <summary>
    /// Kiểm tra: khi chạy ở môi trường `Production` (không có `ASPNETCORE_ENVIRONMENT`, giống
    /// `docker-compose.yml` thật) mà `ConnectionStrings:OrdersDb` chỉ có host/database (không
    /// credential — đúng giá trị base `appsettings.json` đã commit), host ném exception ngay khi
    /// khởi động, và chuỗi exception (kể cả inner exception) phải nêu đích danh
    /// `ConnectionStrings:OrdersDb` là secret còn thiếu.
    /// Lý do: FR-007 — đây là bằng chứng chạy thật (không chỉ unit test thuần của
    /// `RequiredSecretsValidator`) cho việc toàn bộ pipeline khởi động ASP.NET Core thật sự dừng lại
    /// trước khi phục vụ bất kỳ request nào, và lý do dừng lại phải đọc được, không phải 1
    /// exception mơ hồ xảy ra ở lần chạm database đầu tiên.
    /// Task nguồn: spec 018 (secrets qua cluster secret store) — FR-007, User Story 2 Independent
    /// Test.
    /// </summary>
    [Fact]
    public async Task HostFailsToStart_WhenOrdersDbConnectionStringHasNoCredential()
    {
        await using var factory = CreateFactoryWithoutCredential();

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        // Assert.True(điều kiện, thông báo): xanh khi chuỗi exception (đệ quy qua InnerException/
        // AggregateException) có nhắc đúng tên secret còn thiếu; đỏ (kèm toàn bộ chuỗi exception
        // thật để dễ chẩn đoán) khi không tìm thấy tên secret đó ở đâu trong lỗi.
        Assert.True(ExceptionChainMentions(exception, "ConnectionStrings:OrdersDb"),
            $"Expected the startup failure to name the missing secret 'ConnectionStrings:OrdersDb'. Actual exception chain: {Describe(exception)}");
    }

    private static bool ExceptionChainMentions(Exception? exception, string text)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(text, StringComparison.Ordinal))
            {
                return true;
            }

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    if (ExceptionChainMentions(inner, text))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static string Describe(Exception? exception) =>
        exception is null ? "<none>" : string.Join(" -> ", EnumerateMessages(exception));

    private static IEnumerable<string> EnumerateMessages(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return $"{current.GetType().Name}: {current.Message}";
        }
    }

    private static WebApplicationFactory<Program> CreateFactoryWithoutCredential()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Matches docker-compose.yml's own Production stack: no ASPNETCORE_ENVIRONMENT is set,
            // so a service that never got its cluster secret runs on appsettings.json's base
            // (credential-less) ConnectionStrings:OrdersDb value — never appsettings.Development.json.
            builder.UseEnvironment("Production");
        });
    }
}
