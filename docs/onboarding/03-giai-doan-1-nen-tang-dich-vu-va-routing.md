# 03 — Giai đoạn 1: Nền tảng dịch vụ & routing

> Đọc trước [01-tong-quan-kien-truc.md](01-tong-quan-kien-truc.md) và [02-orders-service-va-cac-api-endpoint.md](02-orders-service-va-cac-api-endpoint.md) nếu chưa quen các khái niệm .NET và cấu trúc 1 service. Tài liệu này KHÔNG lặp lại nội dung đó — chỉ kể phần code MỚI được xây trong giai đoạn đầu tiên của repo.
>
> **Specs thuộc giai đoạn này:** `specs/001-scaffold-service-shells`, `specs/002-gateway-bff-routing`, `specs/003-stub-identity-tenant-context`.
> **Kỹ thuật trọng tâm:** dựng khung 6 service độc lập, gateway + BFF (reverse-proxy YARP), và cơ chế "tenant/caller context" — nền tảng multi-tenant của toàn bộ platform, lúc này còn dùng danh tính **giả lập (stub)** vì chưa có máy chủ định danh thật (cái đó tới ở Giai đoạn 4).

## Thay đổi trong shared/

### `shared/ServiceDefaults` — logging/tracing dùng chung (spec 001)

Commit: `693c4e3 feat: Implement OpenTelemetry integration and correlation ID middleware for observability`

Vấn đề cần giải quyết: 6 service độc lập, nếu mỗi service tự viết logging/tracing riêng thì sẽ lệch nhau (1 service log JSON, 1 service log text...) và không thể lần theo 1 request đi qua nhiều service. Giải pháp: 1 thư viện dùng chung, gọi giống hệt nhau ở mọi `Program.cs`.

[`shared/ServiceDefaults/CorrelationIdMiddleware.cs`](../../shared/ServiceDefaults/CorrelationIdMiddleware.cs):
```csharp
public async Task InvokeAsync(HttpContext context)
{
    var correlationId = ResolveCorrelationId(context); // lấy từ header X-Correlation-Id, hoặc sinh Guid mới

    // Ghi NGƯỢC LẠI vào request (không chỉ response!) — vì sao?
    context.Request.Headers[HeaderName] = correlationId;
    // ...
}
```
Dòng `context.Request.Headers[HeaderName] = correlationId` là điểm hay nhất của file này: ghi ID vào chính **request** đang xử lý, không chỉ trả về response. Lý do: gateway dùng YARP để forward request sang BFF, và YARP **copy nguyên request headers** khi forward — nên nếu service A sinh ra 1 correlation ID và ghi vào request của chính nó, request đó (đã được forward tiếp) sẽ mang ID đó sang service B. Nếu chỉ ghi vào response, ID sẽ dừng lại ở A, và B sẽ tự sinh ra 1 ID khác — mất khả năng lần theo 1 request xuyên suốt nhiều service (log của A và log của B sẽ không có gì chung để join lại).

Mọi service gọi 2 dòng dùng chung này trong `Program.cs` (đã thấy ở Tài liệu 2):
```csharp
builder.AddServiceDefaults();  // đăng ký OpenTelemetry + DI
app.UseServiceDefaults();      // gắn CorrelationIdMiddleware vào pipeline, chạy ĐẦU TIÊN
```

### `shared/Tenancy` — cơ chế "tenant" và "người gọi" dùng chung (spec 003)

Commit: `2aac70a feat: Scaffold Tenancy shared library and integrate with existing services`, `fad924f feat: Implement tenancy support across services with middleware and context management`

Đây là khái niệm quan trọng nhất bạn cần nắm trong giai đoạn này: hệ thống này là **multi-tenant** (nhiều khách hàng/tổ chức dùng chung 1 hạ tầng, nhưng dữ liệu phải tách biệt tuyệt đối). "Tenant" = tổ chức nào đang gọi; "caller/subject" = người dùng cụ thể nào bên trong tổ chức đó đang gọi (ví dụ: phân biệt giỏ hàng của người dùng nào trong cùng 1 tenant).

[`shared/Tenancy/TenantContext.cs`](../../shared/Tenancy/TenantContext.cs) — chỉ 1 field, nhưng có 1 hàm quan trọng:
```csharp
public sealed class TenantContext
{
    public string? TenantId { get; set; }   // null = "chưa xác định được tenant"

    public string RequireTenantId() =>
        string.IsNullOrWhiteSpace(TenantId)
            ? throw new MissingTenantContextException()   // NÉM LỖI, không trả về giá trị mặc định
            : TenantId;
}
```
Điểm mấu chốt: **không có tenant mặc định**. Nếu 1 request không xác định được tenant, `RequireTenantId()` ném exception — không bao giờ âm thầm coi "chưa xác định" là "tenant A" hay bất kỳ giá trị cứng nào. Đây là nguyên tắc xuyên suốt cả platform: *"an toàn khi lỗi"* — thiếu thông tin thì dừng lại, không đoán.

[`shared/Tenancy/TenantContextMiddleware.cs`](../../shared/Tenancy/TenantContextMiddleware.cs) — nơi giá trị này được điền vào, đọc từ 1 header HTTP:
```csharp
public const string HeaderName = "X-Tenant-Id";

public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
{
    if (context.Request.Headers.TryGetValue(HeaderName, out var header) && !string.IsNullOrWhiteSpace(header))
    {
        tenantContext.TenantId = header.ToString();
        // ... mở "logging scope" để mọi dòng log của request này tự động kèm TenantId
    }
    // Không có header → không set gì cả → TenantContext ở lại trạng thái "chưa xác định"
}
```
Middleware này **chỉ đọc** header `X-Tenant-Id`, không tự quyết định tenant là gì.

**Câu hỏi quan trọng: ai là người GHI header `X-Tenant-Id` này?** Câu trả lời nằm ở phía gateway, mục dưới — đây chính là quy tắc "chỉ 1 điểm resolve tenant duy nhất" nhắc ở Tài liệu 1.

[`shared/Tenancy/TenancyExtensions.cs`](../../shared/Tenancy/TenancyExtensions.cs) — cách 1 service "bật" toàn bộ cơ chế trên chỉ bằng 2 dòng, y hệt pattern của `ServiceDefaults`, **ở trạng thái ban đầu của spec 003** (chỉ có tenant, chưa có caller — phần "người gọi" `X-Subject-Id`/`CallerContext` chỉ được thêm vào ở spec 004, xem [04-giai-doan-2-spa-va-demo-end-to-end.md](04-giai-doan-2-spa-va-demo-end-to-end.md)):
```csharp
public static IServiceCollection AddTenancy(this IServiceCollection services)
{
    services.AddScoped<TenantContext>();   // scoped — mỗi request 1 bản riêng, không dùng chung giữa các request
    return services;
}

public static WebApplication UseTenancy(this WebApplication app)
{
    app.UseMiddleware<TenantContextMiddleware>();
    return app;
}
```

## Thay đổi trong service nghiệp vụ

### Dựng khung 4 service (`baskets`, `orders`, `parties`, `products`) — spec 001

Commit: `939b96e feat: Add scaffolding for parties, products, baskets, and orders service shells`, `3a382c2 feat: Implement health checks and database contexts for Parties, Products, and Baskets services`

Ở bước này, 4 service được tạo ra với đúng khuôn đã thấy ở Tài liệu 2 (mỗi service: `Program.cs` + `Data/<Entity>DbContext.cs` + `Features/HealthCheck/`) nhưng **chưa có** `AddIdentityValidation()`/`AddTenancy()` (2 dòng đó tới ở spec 002/003) — ở bước này mỗi service chỉ có `AddServiceDefaults()` + 1 `DbContext` riêng trỏ vào 1 database riêng. Đây là lúc quyết định kiến trúc "mỗi service 1 database, không service nào đọc chung database service khác" được đặt nền — được 1 scanner tự động kiểm tra ngay từ đây (`017b03b feat: Implement cross-service connection string isolation checks`, tiền thân của `ConnectionStringScanner` nhắc ở Tài liệu 4 cũ, xem `tests/CrossServiceIsolation.Tests/`).

### Route + response shape đầu tiên — spec 002

Commit: `59af85f feat: Implement initial data models and endpoints for Baskets, Orders, Parties, and Products services`

Đây là lần đầu tiên pattern "1 folder `Features/<Tên>/` = 1 route + 1 response shape" (đã giải thích kỹ ở Tài liệu 2) xuất hiện trong repo — `OrderEndpoints.cs`, `BasketEndpoints.cs`, `PartyEndpoints.cs`, `CatalogEndpoints.cs` ra đời ở chính commit này, ở dạng đơn giản hơn nhiều (chưa có `.RequireAuthorization(...)` — cái đó chỉ tới ở Giai đoạn 4, spec 015).

Enforce việc **không thể chạm database khi thiếu tenant** (`0160999 Implement tenant enforcement across services with integration tests`) đã được lắp vào đúng chỗ `Program.cs` mọi service nghiệp vụ (`TenantContext.RequireTenantId()` gọi trước khi `DbContext` mở kết nối, xem Tài liệu 2 mục 2) ngay từ spec 003 này. Nhưng lúc này `Order.cs` **chưa có field `TenantId`** — đơn hàng chưa thật sự "gắn nhãn" thuộc tenant nào, mới chỉ có DB được bảo vệ khỏi bị chạm nhầm. Việc gắn `TenantId` vào chính entity `Order` là 1 bước riêng, tới muộn hơn ở spec 006 — xem [04-giai-doan-2-spa-va-demo-end-to-end.md § Gán tenant vào đơn hàng](04-giai-doan-2-spa-va-demo-end-to-end.md#gán-tenant-vào-đơn-hàng--spec-006).

### Gateway "sản xuất" ra header, service nghiệp vụ chỉ "tiêu thụ" nó

Commit: `aa0b8db feat: Implement tenant propagation across BFF and gateway services with authentication and middleware`

[`services/gateway/src/Gateway.Api/Identity/TenantHeaderPropagationMiddleware.cs`](../../services/gateway/src/Gateway.Api/Identity/TenantHeaderPropagationMiddleware.cs) — mảnh ghép còn thiếu ở mục `shared/Tenancy` phía trên:
```csharp
public async Task InvokeAsync(HttpContext context)
{
    var tenantId = context.User.FindFirst(StubIdentityAuthenticationHandler.TenantClaimType)?.Value;

    if (string.IsNullOrWhiteSpace(tenantId))
    {
        // Không tự đoán — mà còn CHỦ ĐỘNG XOÁ header này nếu client tự gửi lên
        context.Request.Headers.Remove(HeaderName);
        await _next(context);
        return;
    }

    context.Request.Headers[HeaderName] = tenantId;   // GHI ĐÈ, không bao giờ tin giá trị client gửi
    // ...
}
```
Dòng `context.Request.Headers.Remove(HeaderName)` là chi tiết bảo mật quan trọng nhất file này: nếu 1 client tò mò tự gắn header `X-Tenant-Id: tenant-cua-nguoi-khac` vào request của họ, gateway **xoá** header đó trước khi forward đi (dù bản thân client cũng không xác định được tenant nào). Không xoá thì service phía sau (chỉ biết đọc header, xem `TenantContextMiddleware` ở trên) sẽ vô tình tin luôn giá trị client tự khai — đây chính là kiểu lỗ hổng "tin dữ liệu từ client" mà nguyên tắc "1 điểm resolve tenant duy nhất, ở gateway" của Tài liệu 1 tồn tại để chặn.

`StubIdentityAuthenticationHandler` (danh tính giả lập) là do giai đoạn này **chưa có** máy chủ định danh thật — client tự khai họ là ai qua 1 cơ chế đơn giản, gateway tin vào đó để đọc ra claim tenant. Cơ chế giả lập này được thay bằng Duende IdentityServer thật ở Giai đoạn 4 ([06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md](06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md)) — nhưng **toàn bộ** cơ chế header/middleware ở tài liệu này (`X-Tenant-Id`, `X-Subject-Id`, `TenantContext`, `RequireTenantId()`) giữ nguyên không đổi, chỉ có nguồn phát sinh claim thay đổi. Đây là lý do đầu tư vào 1 lớp trừu tượng tốt ngay từ giai đoạn 1 có giá trị lâu dài.

## Đi đâu tiếp theo

- [04-giai-doan-2-spa-va-demo-end-to-end.md](04-giai-doan-2-spa-va-demo-end-to-end.md) — nối SPA vào backend, chạy toàn hệ thống bằng 1 lệnh, luồng đặt hàng end-to-end đầu tiên.
