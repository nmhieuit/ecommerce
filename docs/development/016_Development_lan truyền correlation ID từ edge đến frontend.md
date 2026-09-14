# Bước 016: Thay đổi nghiệp vụ so với bước 015

## Phạm vi

Bước 016 vá một khoảng trống thật trong cơ chế `CorrelationIdMiddleware` đã có từ bước 001 — không
xây cơ chế mới. Toàn bộ thay đổi nằm trong đúng 1 commit (đặc tả + triển khai gộp chung).

Ranh giới commit được xác định từ lịch sử Git:

- Mốc hoàn tất bước 015: commit `be79cbf`.
- Đặc tả và triển khai bước 016 — mốc hoàn tất: commit `2abe34d`.

Một commit theo sau (`cf8477f`, "fix(correlation-id): resolve tenant on BFF correlation propagation
tests") chỉ sửa 1 file test (`CorrelationPropagationTests.cs`) — không có production code, bị loại
theo đúng phạm vi đã áp dụng.

## 1. `CorrelationIdMiddleware` validate giá trị client tự cung cấp

[shared/ServiceDefaults/CorrelationIdMiddleware.cs](../../shared/ServiceDefaults/CorrelationIdMiddleware.cs)

```csharp
// 016: dài hơn bất kỳ caller thật nào cần (GUID có dấu gạch là 36 ký tự), đủ ngắn để chặn log
// phình ra từ một giá trị lỗi/ác ý.
private const int MaxLength = 128;

private static string ResolveCorrelationId(HttpContext context)
{
    if (context.Request.Headers.TryGetValue(HeaderName, out var existing) &&
        !string.IsNullOrWhiteSpace(existing) &&

        // 016: thêm bước validate — trước đó bất kỳ giá trị non-empty nào cũng được giữ nguyên.
        IsValidCorrelationId(existing.ToString()))
    {
        return existing.ToString();
    }

    return Guid.NewGuid().ToString("n");
}

// 016: có chủ đích KHÔNG ép định dạng GUID — client có thể mang bất kỳ token mờ nào. Chỉ loại
// 2 thứ thực sự nguy hiểm khi giá trị này bị đẩy thẳng vào mọi dòng log có cấu trúc.
private static bool IsValidCorrelationId(string value)
{
    // 016: chặn phình log từ một giá trị quá dài.
    if (value.Length > MaxLength)
    {
        return false;
    }

    foreach (var c in value)
    {
        // 016: ký tự điều khiển (kể cả CRLF) có thể giả mạo thêm một dòng log — loại trực tiếp,
        // không cố "làm sạch" giá trị cũ.
        if (c < '\x20' || c == '\x7f')
        {
            return false;
        }
    }

    return true;
}
```

Giá trị không hợp lệ bị bỏ qua hoàn toàn, thay bằng một `Guid` mới do hệ thống sinh — không có bước
"làm sạch" giá trị client gửi lên. `IsValidCorrelationId` là hàm thuần, không phụ thuộc `HttpContext`.

## 2. BFF → domain service: correlation ID trước đây bị âm thầm rớt

[services/bff/src/Bff.Api/DownstreamClients/TenantPropagationHandler.cs](../../services/bff/src/Bff.Api/DownstreamClients/TenantPropagationHandler.cs)

```csharp
protected override Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request, CancellationToken cancellationToken)
{
    var httpContext = httpContextAccessor.HttpContext;

    Relay(request, TenantContextMiddleware.HeaderName, /* ... */);
    Relay(request, CallerContextMiddleware.HeaderName, /* ... */);
    Relay(request, "Authorization", httpContext?.Request.Headers.Authorization.ToString());

    // 016: đọc từ HttpContext.Items, không resolve lại — đây chính là giá trị mà
    // CorrelationIdMiddleware đã quyết định cho request BFF đang xử lý (sinh mới hoặc giữ
    // nguyên), cùng nguồn mà DownstreamExceptionHandler đọc để đưa correlation ID vào
    // ProblemDetails khi báo lỗi downstream (đã có từ 009, không đổi bởi 016).
    Relay(
        request,
        CorrelationIdMiddleware.HeaderName,
        httpContext?.Items[CorrelationIdMiddleware.HeaderName] as string);

    return base.SendAsync(request, cancellationToken);
}
```

Trước bước 016, hop BFF → domain service **âm thầm làm rớt** correlation ID: mỗi domain service chỉ
tới được qua BFF sẽ tự sinh ID riêng của nó thay vì mang theo ID đã sinh ở edge — đúng khoảng trống mà
architecture doc ghi nhận là "vá 1 khoảng trống thật trong cơ chế đã có từ 001".

## 3. Gateway expose header cho JavaScript phía SPA đọc được

[services/gateway/src/Gateway.Api/Program.cs](../../services/gateway/src/Gateway.Api/Program.cs)

```csharp
builder.Services.AddCors(options => options.AddPolicy(
    StorefrontCorsPolicy,
    policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()

        // 016: thiếu dòng này, browser vẫn HIỂN THỊ header ở tab Network của DevTools (view đó
        // không chịu chi phối bởi CORS), nhưng JS của SPA gọi response.headers.get(...) sẽ nhận
        // null — X-Correlation-Id không nằm trong danh sách response header "safelisted" mà
        // browser mặc định lộ ra cho script.
        .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));
```

## 4. Frontend: `ApiError` mang theo correlation ID

[frontend/packages/api-client/src/http/fetcher.ts](../../frontend/packages/api-client/src/http/fetcher.ts)

```typescript
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly url: string,
    readonly body: unknown,
    // 016: X-Correlation-Id mà gateway đặt lên response, hoặc null khi response chưa từng tới
    // (lỗi network) hoặc — bất thường — không mang theo. Cho phép một báo lỗi hay support
    // ticket sau này trích đúng giá trị nối kết lỗi phía client với log phía backend.
    readonly correlationId: string | null,
  ) {
    super(`Request to ${url} failed with status ${status}.`);
    this.name = 'ApiError';
  }
}
```

```typescript
if (!response.ok) {
  // 016: trước đó ApiError không mang correlation ID.
  throw new ApiError(response.status, url, body, response.headers.get('X-Correlation-Id'));
}
```

Đây là phần "đến frontend" trong tên tính năng: trước 016, correlation ID chỉ tồn tại trong response
header (xem được ở DevTools) nhưng code JavaScript của SPA không có cách nào đọc hay dùng lại nó.

## Tóm tắt 015 → 016

| Khu vực | Bước 015 | Bước 016 |
|---|---|---|
| Giá trị client tự gửi | Bất kỳ chuỗi non-empty nào cũng được giữ nguyên | Validate: loại ký tự điều khiển (CRLF) và độ dài > 128 |
| BFF → domain service | Correlation ID bị âm thầm rớt, mỗi service tự sinh ID riêng | `TenantPropagationHandler` relay đúng ID đã sinh ở edge |
| CORS (Gateway) | Không expose `X-Correlation-Id` | `WithExposedHeaders(...)` — SPA đọc được bằng JS |
| `ApiError` (frontend) | Không mang correlation ID | Có field `correlationId` lấy từ response header |
| Tenant/subject propagation, xác thực (003/004/014/015) | — | Không đổi |

**Kết luận:** bước 016 không thêm nghiệp vụ mua hàng mới, không đổi cơ chế xác thực/phân quyền (014/
015). Nó vá đúng 3 điểm rò rỉ trong một cơ chế đã tồn tại từ 001: giá trị client gửi lên chưa được
validate, correlation ID bị rớt ở hop BFF → domain service, và SPA không đọc được header dù nó đã luôn
có mặt trên response.

## 5. Shared project trong bước 016

Bước 016 không tạo shared project mới. Nó sửa `shared/ServiceDefaults/CorrelationIdMiddleware.cs`
(001) — thêm bước validate — và không đổi bất kỳ `.csproj`/`Dockerfile` nào, vì `CorrelationIdMiddleware`
đã được mọi service tham chiếu qua `ServiceDefaults` từ bước 001.

[shared/ServiceDefaults/CorrelationIdMiddleware.cs](../../shared/ServiceDefaults/CorrelationIdMiddleware.cs)

```text
# 016: đây là lần sửa đầu tiên của CorrelationIdMiddleware kể từ khi nó được tạo ở bước 001 —
# tăng trưởng của shared layer ở bước này nằm ở việc SIẾT CHẶT một hàm đã có,
# không phải thêm project mới hay thêm điểm tích hợp mới.
```
