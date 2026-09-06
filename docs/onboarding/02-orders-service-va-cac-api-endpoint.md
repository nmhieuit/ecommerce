# 02 — API/Endpoint chung, lấy Orders service làm ví dụ đại diện

> Đọc trước [01-tong-quan-kien-truc.md](01-tong-quan-kien-truc.md) nếu chưa quen các khái niệm .NET (minimal API, DI, middleware).
> Bốn service nghiệp vụ trong repo — `baskets`, `orders`, `parties`, `products` — dùng **chung một khuôn**. Tài liệu này giải thích chi tiết Orders 1 lần, sau đó chỉ liệt kê phần khác biệt của 3 service còn lại (§4), để tránh đọc lại cùng một cấu trúc 4 lần.

## 1. Cấu trúc thư mục của Orders service

```
services/orders/src/Orders.Api/
├── Program.cs                          # điểm khởi động: đăng ký middleware, DB, endpoint
├── Data/
│   ├── Order.cs                        # entity + domain logic (Order.PlaceFrom)
│   ├── OrdersDbContext.cs              # "kết nối" EF Core tới database orders
│   └── OrdersDbContextFactory.cs       # cho phép công cụ EF CLI tạo DbContext lúc design-time
├── Features/
│   ├── Orders/
│   │   └── OrderEndpoints.cs           # TOÀN BỘ route /orders — trọng tâm tài liệu này
│   └── HealthCheck/
│       └── HealthCheckEndpoints.cs     # /health/live, /health/ready (không cần đăng nhập)
├── Migrations/                         # lịch sử thay đổi schema DB (do EF Core sinh)
└── service-manifest.yaml               # khai báo metadata cho tooling nội bộ (không phải code chạy)
```

Đây chính là pattern **vertical slice** nhắc ở Tài liệu 1: mọi thứ liên quan đến "đơn hàng" (route + shape dữ liệu trả về) nằm gọn trong `Features/Orders/OrderEndpoints.cs`, không tách ra `Controllers/`, `DTOs/`, `Services/` như nhiều dự án .NET truyền thống khác.

## 2. `Program.cs` — thứ tự khởi động và pipeline

File: [`services/orders/src/Orders.Api/Program.cs`](../../services/orders/src/Orders.Api/Program.cs)

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();                 // (1) logging/tracing chung — shared/ServiceDefaults
builder.Services.AddIdentityValidation(...);   // (2) đăng ký xác thực + phân quyền — shared/Identity
builder.Services.AddTenancy();                 // (3) đăng ký cơ chế resolve "tenant" — shared/Tenancy
builder.Services.AddDbContext<OrdersDbContext>((sp, options) => {
    sp.GetRequiredService<TenantContext>().RequireTenantId(); // (4) BẮT BUỘC có tenant TRƯỚC KHI mở kết nối DB
    options.UseSqlServer(...);
});
builder.Services.AddHealthCheckFeature();

var app = builder.Build();
app.UseServiceDefaults();      // middleware logging/correlation-id
app.UseIdentityValidation();   // middleware xác thực + phân quyền (401/403 nếu fail)
app.UseTenancy();              // middleware resolve tenant (400 nếu thiếu)
app.MapHealthCheckEndpoints(); // route /health/*  — KHÔNG cần đăng nhập
app.MapOrderEndpoints();       // route /orders/*  — CẦN đăng nhập + đúng scope
app.Run();
```

Điều quan trọng nhất ở đây là **thứ tự**: `UseIdentityValidation()` chạy trước `UseTenancy()`, chạy trước khi route handler nào được gọi. Nghĩa là:
- 1 request không có token hợp lệ bị chặn ở bước (2) trên pipeline — **không bao giờ** chạm tới logic nghiệp vụ hay database.
- 1 request có token hợp lệ nhưng không resolve được tenant bị chặn ở bước (3) — cũng không chạm database.

Đây chính là ý nghĩa "deny-by-default": mặc định **từ chối**, phải vượt qua từng lớp kiểm tra mới tới được logic thật. Cơ chế phân quyền này được xây dựng qua các commit thật ở [06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md](06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md).

## 3. `OrderEndpoints.cs` — cách 1 route được khai báo và xử lý

File: [`services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs`](../../services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs)

### 3.1 Đọc 1 đơn hàng — `GET /orders/{orderId}`

```csharp
app.MapGet("/orders/{orderId:guid}", async (
    Guid orderId,               // ← tự động lấy từ path, ép kiểu Guid (400 nếu sai định dạng)
    OrdersDbContext dbContext,  // ← DI tự "bơm" kết nối DB vào, mỗi request 1 instance riêng
    CancellationToken cancellationToken) =>
{
    var order = await dbContext.Orders
        .AsNoTracking()                                  // chỉ đọc, không theo dõi thay đổi (nhanh hơn)
        .Where(entity => entity.Id == orderId)
        .Select(entity => new OrderResponse(...))         // chỉ lấy đúng field cần trả về
        .SingleOrDefaultAsync(cancellationToken);

    return order is null ? Results.NotFound() : Results.Ok(order);
})
    .RequireAuthorization(AuthorizationPolicies.ApiScope); // ← BẮT BUỘC: route này cần policy "ApiScope"
```

`AsNoTracking()`/`.Select(...)` là cú pháp LINQ (giống query builder) — EF Core dịch nó thành 1 câu SQL `SELECT` thật, không load toàn bộ bảng vào bộ nhớ.

### 3.2 Tạo đơn hàng — `POST /orders`

```csharp
app.MapPost("/orders", async (
    PlaceOrderRequest request,   // ← body JSON tự động deserialize vào record này
    OrdersDbContext dbContext,
    CallerContext caller,        // ← "ai đang gọi" — resolve từ token, KHÔNG bao giờ lấy từ request
    TenantContext tenant,        // ← "tenant nào" — resolve từ token, KHÔNG bao giờ lấy từ request
    CancellationToken cancellationToken) =>
{
    caller.RequireSubjectId();   // ném lỗi nếu không xác định được người gọi

    if (request.Items is null || request.Items.Count == 0)
        return Results.BadRequest(new { error = "An order needs at least one line." });

    Order order;
    try
    {
        order = Order.PlaceFrom(items, DateTime.UtcNow, tenant.RequireTenantId());
        // ^ TỔNG TIỀN được tính TRONG domain logic (Order.cs), không phải do client gửi lên và tin luôn
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message }); // lỗi nghiệp vụ → 400, không phải 500
    }

    dbContext.Orders.Add(order);
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Created($"/orders/{order.Id}", ToResponse(order));
})
    .RequireAuthorization(AuthorizationPolicies.ApiScope);
```

**Pattern validate input** dùng trong toàn repo (không riêng Orders):
1. Validate hình thức đơn giản (null/rỗng) ngay trong lambda → trả `400 BadRequest` sớm.
2. Validate quy tắc nghiệp vụ (ví dụ: số lượng phải ≥ 1) nằm **trong** class domain (`Order.cs`), ném `ArgumentException` → route handler bắt và convert thành `400`.
3. Dữ liệu "nhạy cảm" (tenant, người gọi, tổng tiền) **không bao giờ** lấy trực tiếp từ request body — luôn lấy từ context đã được server tự resolve (token/middleware), để client không thể tự khai "tôi là tenant khác" hay "tổng tiền của tôi là 0đ".

**Pattern trả lỗi** dùng trong toàn repo: không có 1 "exception handler" chung bắt mọi lỗi rồi trả 500 kèm message tuỳ tiện — mỗi loại lỗi được ánh xạ tường minh sang đúng mã HTTP (400 cho lỗi input/nghiệp vụ, 404 cho không tìm thấy, 401/403 do middleware phân quyền tự xử lý trước khi vào tới đây — xem Tài liệu 4).

## 4. So sánh Orders với 3 service còn lại

Tất cả 4 service dùng **chung khuôn `Program.cs`** ở mục 2 (chỉ khác tên `DbContext` và tên method `Map*Endpoints`). Khác biệt thật sự chỉ nằm ở `Features/<Tên>/*.cs`:

| Service | Route | Điểm khác biệt so với Orders |
|---|---|---|
| **Products** (`CatalogEndpoints.cs`) | `GET /products` | Chỉ đọc, không có POST. Danh mục rỗng trả `[]` (mảng rỗng), không phải `404` — "chưa có sản phẩm" là trạng thái hợp lệ, không phải lỗi. |
| **Parties** (`PartyEndpoints.cs`) | `GET /parties/{id}` | Gần như giống hệt `GET /orders/{id}` của Orders (đọc theo id, `404` nếu không có) — service đơn giản nhất trong 4 service. |
| **Baskets** (`BasketEndpoints.cs`) | `GET /baskets/current`, `POST /baskets/current/items`, `POST /baskets/current/clear`, `GET /baskets/{id}` | Khác biệt lớn nhất: khái niệm **"current"** — giỏ hàng luôn được resolve từ `CallerContext` (ai đang gọi), **không có route tạo giỏ hàng bằng tay** hay xem giỏ của người khác. `LineTotal` (tiền dòng) được tính và trả về từ chính service này — quy tắc chung: **tính toán tiền tệ luôn nằm ở service sở hữu dữ liệu**, không phải ở BFF. |
| **Orders** (`OrderEndpoints.cs`) | `GET /orders/{id}`, `POST /orders` | (đã giải thích ở mục 3) |

Kết luận: nếu bạn đã đọc hiểu `OrderEndpoints.cs`, bạn đọc hiểu được cả 4 service — chỉ cần chú ý phần domain logic riêng của từng bên (ví dụ Baskets có khái niệm "current/của tôi", Products là read-only).

## 5. BFF — cùng pattern, khác vai trò

`services/bff/src/Bff.Api/Features/*/`. Cấu trúc file y hệt (1 folder = 1 chức năng, route + response shape trong 1 file), nhưng **không có `DbContext`** — mỗi route BFF gọi 1 `HttpClient` đã đăng ký sẵn (gọi là "typed client", trong `DownstreamClients/`) sang đúng 1 service phía sau rồi định hình lại response, ví dụ [`OrdersEndpoints.cs`](../../services/bff/src/Bff.Api/Features/Orders/OrdersEndpoints.cs) gọi `OrdersApiClient` sang `orders-api`. BFF không tự viết thêm logic nghiệp vụ nào — chỉ tổng hợp/định hình.