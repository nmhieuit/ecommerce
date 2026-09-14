# Bước 020: Thay đổi nghiệp vụ so với bước 019

## Phạm vi

`research.md` Decision 1 kiểm kê lại toàn bộ mã nguồn thật trước khi thiết kế — chỉ 3/5 loại cuộc gọi
trong acceptance criteria gốc của Jira thực sự tồn tại (service→service đồng bộ và service→broker
**không tồn tại** tại thời điểm này). Bước 020 chỉ bọc resilience cho 3 điểm gọi có thật.

Ranh giới commit được xác định từ lịch sử Git:

- Mốc hoàn tất bước 019: commit `00eeba0`.
- Đặc tả và triển khai bước 020 — mốc hoàn tất: commit `272b1ce`.
- Xác nhận T025 bước 6 (quan sát sự kiện Polly qua OTel/Elastic thật) — chỉ sửa `tasks.md`, không
  code: commit `a695c6f`.

`ResilienceCoverageTests`/`ResilienceCoverageScanner` (cơ chế rà soát lặp lại được, FR-007) là nội
dung kiểm thử, bị loại theo đúng phạm vi đã áp dụng — chỉ được nhắc tên.

## 1. Gateway → BFF: circuit breaker qua passive health check của YARP, không thêm retry

[services/gateway/src/Gateway.Api/appsettings.json](../../services/gateway/src/Gateway.Api/appsettings.json)

```jsonc
{
  "HttpRequest": { "ActivityTimeout": "00:00:10" },

  // 020: circuit breaker cho hop reverse-proxy — retry ở đây có nguy cơ nhân đôi side effect vì
  // gateway forward nguyên văn mọi method (kể cả POST /basket/items, POST /checkout) mà không
  // biết ngữ nghĩa idempotency của route (spec FR-006 cấm).
  "HealthCheck": {
    "Passive": {
      "Enabled": true,
      "Policy": "TransportFailureRate",
      "ReactivationPeriod": "00:00:30"
    },

    // 020: BUG THẬT tìm được khi implement — mặc định của YARP ("HealthyOrPanic") coi mọi
    // destination là "available" khi không có destination nào healthy, việc này VÔ HIỆU HOÁ
    // circuit breaker cho cluster chỉ có 1 destination (bff-cluster). "HealthyAndUnknown" mới
    // thật sự fail fast (503) thay vì vẫn thử kết nối biết trước sẽ fail.
    "AvailableDestinationsPolicy": "HealthyAndUnknown"
  },
  "Metadata": {
    "TransportFailureRateHealthPolicy.RateLimit": "0.5"
  }
}
```

[services/gateway/src/Gateway.Api/Program.cs](../../services/gateway/src/Gateway.Api/Program.cs)

```csharp
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// 020: bind riêng để test override được MinimalTotalCountThreshold mà không đụng file này.
builder.Services.Configure<TransportFailureRateHealthPolicyOptions>(
    builder.Configuration.GetSection("ReverseProxy:TransportFailureRateHealthPolicy"));

// ...

// 020: BUG THẬT thứ hai — MapReverseProxy() không tham số (dùng trước tính năng này) KHÔNG bật
// UsePassiveHealthChecks(). appsettings.json cấu hình policy, nhưng thiếu middleware này thì
// không có gì đánh giá nó hay loại trừ 1 destination bị đánh dấu unhealthy — circuit không bao
// giờ mở dù đã cấu hình đúng.
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UsePassiveHealthChecks();
    proxyPipeline.UseLoadBalancing();
});
```

## 2. JwtBearer backchannel: từ `HttpClient` mặc định ẩn sang tường minh

[shared/Identity/IdentityBackchannelResilience.cs](../../shared/Identity/IdentityBackchannelResilience.cs)

```csharp
// 020: bọc backchannel mà JwtBearer dùng để fetch OIDC discovery/JWKS bằng timeout/retry/circuit
// breaker tường minh, thay vì để JwtBearerOptions.Backchannel ở giá trị mặc định ẩn của
// framework (HttpClient với BackchannelTimeout = 60s không khai báo ở đâu cả).
public static class IdentityBackchannelResilience
{
    public const string BackchannelClientName = "IdentityBackchannel";

    // 020: tần suất fetch OIDC discovery/JWKS thấp hơn nhiều so với 4 downstream client nghiệp
    // vụ (cache bởi ConfigurationManager của framework) — budget rộng hơn AttemptTimeout của
    // DownstreamClientRegistrationExtensions (1s) là hợp lý.
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TotalRequestTimeout = TimeSpan.FromSeconds(15);

    public static IServiceCollection AddIdentityBackchannelResilience(
        this IServiceCollection services, string authenticationScheme)
    {
        services.AddHttpClient(BackchannelClientName)
            .AddStandardResilienceHandler(resilience =>
            {
                resilience.AttemptTimeout.Timeout = AttemptTimeout;
                resilience.TotalRequestTimeout.Timeout = TotalRequestTimeout;
            });

        services.AddOptions<JwtBearerOptions>(authenticationScheme)
            .Configure<IHttpClientFactory>((jwtOptions, httpClientFactory) =>
            {
                jwtOptions.Backchannel = httpClientFactory.CreateClient(BackchannelClientName);
            });

        return services;
    }
}
```

[shared/Identity/IdentityValidationExtensions.cs](../../shared/Identity/IdentityValidationExtensions.cs)

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(jwtOptions => { /* ... */ });

// 020: sửa đúng 1 lần ở đây (bao phủ BFF + 4 domain service) và 1 lần ở gateway
// (ToggleGatedAuthenticationExtensions) — không lặp lại cấu hình backchannel ở 6 nơi gọi.
services.AddIdentityBackchannelResilience(JwtBearerDefaults.AuthenticationScheme);
```

## 3. BFF → 4 downstream service: thu hẹp retry theo method an toàn

[services/bff/src/Bff.Api/DownstreamClients/DownstreamClientRegistrationExtensions.cs](../../services/bff/src/Bff.Api/DownstreamClients/DownstreamClientRegistrationExtensions.cs)

```csharp
// 020: BUG TIỀM ẨN THẬT đã tồn tại từ spec 002, không phải tính năng mới — cấu hình retry cũ áp
// dụng cho MỌI HTTP method, nghĩa là POST /basket/items hay POST /checkout có thể bị gửi lại sau
// khi bản gốc đã tới server thành công nhưng response bị mất trên đường về, tạo dòng giỏ hàng
// hoặc ĐƠN HÀNG TRÙNG. Hệ thống không có cơ chế idempotency-key.
private static readonly HashSet<HttpMethod> SafeToRetryMethods = [HttpMethod.Get, HttpMethod.Head];

resilience.Retry.MaxRetryAttempts = MaxRetryAttempts;
resilience.Retry.Delay = RetryDelay;

// 020: xếp CHỒNG lên predicate transient-failure chuẩn, không thay thế nó — một cuộc gọi vẫn
// phải trông "transient" (network/5xx/timeout) VÀ nhắm vào method an toàn mới được retry.
// GetRequestMessage() đọc từ ResilienceContext, không phải Outcome.Result, vì lỗi transport
// (trường hợp phổ biến ở đây) không bao giờ sinh ra HttpResponseMessage để đọc request gốc.
resilience.Retry.ShouldHandle = args =>
{
    var method = args.Context.GetRequestMessage()?.Method;

    return ValueTask.FromResult(
        HttpClientResiliencePredicates.IsTransient(args.Outcome)
        && method is not null
        && SafeToRetryMethods.Contains(method));
};
```

`POST` vẫn giữ nguyên timeout và circuit breaker — chỉ mất khả năng tự động retry. `GET`/`HEAD` retry
như trước, vì đọc lại hai lần không đổi kết quả nghiệp vụ.

## 4. Sự kiện Polly được phát ra OTel, tới được Elastic

[shared/ServiceDefaults/ServiceDefaultsExtensions.cs](../../shared/ServiceDefaults/ServiceDefaultsExtensions.cs)

```csharp
.WithTracing(tracing => tracing
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()

    // 020: mọi attempt/retry/đổi trạng thái circuit breaker mà Microsoft.Extensions.Http.
    // Resilience (Polly v8) tạo ra được phát dưới activity source này. Thiếu dòng này, sự kiện
    // resilience vẫn xảy ra nhưng không bao giờ tới Elastic — chỉ kết quả CUỐI CÙNG mới tới qua
    // HttpClient instrumentation, không phân biệt được "fail ngay lần đầu" với "fail sau 2 lần
    // retry và circuit đã mở" (spec FR-008).
    .AddSource("Polly")
    .AddOtlpExporter())
.WithMetrics(metrics => metrics
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddRuntimeInstrumentation()
    .AddMeter("Polly")
    .AddOtlpExporter());
```

## Tóm tắt 019 → 020

| Khu vực | Bước 019 | Bước 020 |
|---|---|---|
| Gateway → BFF | Timeout (`ActivityTimeout`), không circuit breaker | + Passive health check = circuit breaker; sửa 2 bug thật (mặc định YARP + thiếu middleware) |
| Identity backchannel (OIDC/JWKS) | Timeout ẩn 60s mặc định framework | `HttpClient` tường minh `"IdentityBackchannel"`, resilience 5s/15s |
| BFF → 4 downstream service | Retry cho MỌI method (bug tiềm ẩn từ 002) | Retry chỉ `GET`/`HEAD`; `POST` giữ timeout/circuit breaker, mất auto-retry |
| Quan sát sự kiện Polly | Không phát qua OTel | `AddSource("Polly")`/`AddMeter("Polly")` — tới được Elastic (017) |
| Service→service đồng bộ, service→broker | Không tồn tại | Không đổi — không có gì để bọc (xác nhận lại bằng kiểm kê) |

**Kết luận:** bước 020 không thêm nghiệp vụ mua hàng mới. Nó bọc resilience tường minh cho đúng 3/5
điểm gọi ra ngoài thực sự tồn tại, phát hiện và sửa 2 bug thật đang tồn tại từ trước (circuit breaker
Gateway→BFF bị vô hiệu hoá bởi 2 lý do khác nhau; retry mù có thể tạo đơn hàng trùng), và làm cho sự
kiện Polly quan sát được qua đúng hạ tầng OTel/Elastic đã dựng ở bước 017.

## 5. Shared project trong bước 020

Bước 020 không tạo shared project mới. Nó mở rộng `shared/Identity` (014) với 1 file mới
(`IdentityBackchannelResilience.cs`) và mở rộng `shared/ServiceDefaults` (001) bằng 2 dòng cấu hình
OTel (`AddSource("Polly")`/`AddMeter("Polly")`). Không service nào cần thêm `ProjectReference` mới —
cả hai shared project đã được dùng từ trước.

[shared/Identity/Identity.csproj](../../shared/Identity/Identity.csproj)

```text
# 020: Identity.csproj chỉ thêm PackageReference cho Microsoft.Extensions.Http.Resilience
# (Polly v8) — không có ProjectReference production mới nào giữa các shared project hay service.
```

Đây là bước thứ ba (sau 015, 018) mở rộng năng lực của một shared project đã tồn tại mà không service
nào cần đổi cách nó tham chiếu shared project đó.
