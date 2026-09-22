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
    /// Kiểm tra: gọi `GET /health/live` vào service Products đang chạy trong bộ nhớ thì nhận về mã
    /// HTTP 200 (OK) — tiến trình còn sống và trả lời được, bất kể database có kết nối được hay
    /// không.
    /// Lý do: liveness không được chạm vào database — tiến trình .NET còn sống là phải trả 200
    /// ngay. Đây là ranh giới phân biệt liveness với readiness (xem `ReadinessTests`) mà FR-003 dựa
    /// vào; nếu liveness phụ thuộc database, 1 lần database chết sẽ khiến Kubernetes khởi động lại
    /// cả những pod khoẻ mạnh.
    /// Lưu ý: từ spec 018 ứng dụng từ chối khởi động nếu thiếu chuỗi kết nối có `Password=`, nên
    /// phải đặt biến môi trường trước, vd. `ConnectionStrings__ProductsDb="Server=x;Database=y;User
    /// Id=sa;Password=p;TrustServerCertificate=true"` (giá trị giả là đủ vì test không chạm
    /// database). Thiếu biến này test đỏ ngay lúc khởi động (`OptionsValidationException: Missing
    /// required secret(s)`), không phải vì logic liveness.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T009, US1.
    /// </summary>
    [Fact]
    public async Task HealthLive_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
