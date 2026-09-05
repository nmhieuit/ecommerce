# 04 — Giai đoạn 2: SPA & demo end-to-end

> Đọc [03-giai-doan-1-nen-tang-dich-vu-va-routing.md](03-giai-doan-1-nen-tang-dich-vu-va-routing.md) trước — giai đoạn này xây tiếp trên `TenantContext`/`shared/Tenancy` đã có ở đó, và sửa 1 chi tiết bị gán nhầm ở tài liệu đó (xem mục "Gán tenant vào đơn hàng" bên dưới).
>
> **Specs thuộc giai đoạn này:** `specs/004-minimal-shopping-spa`, `specs/005-one-command-local-run`, `specs/006-e2e-order-demo`.
> **Kỹ thuật trọng tâm:** thêm khái niệm "người gọi" (subject) bên cạnh "tenant", ghép nối luồng checkout xuyên nhiều service lần đầu tiên, và chạy toàn bộ platform bằng 1 lệnh để tự kiểm tra bằng demo thật.

## Thay đổi trong shared/

### `CallerContext` — mảnh còn thiếu của `shared/Tenancy` (spec 004)

Commit: `f3f13ac feat: Implement caller context and subject header propagation`

Ở Giai đoạn 1, `shared/Tenancy` mới chỉ trả lời được câu hỏi "request này thuộc **tenant** nào" (tổ chức nào). Spec 004 cần trả lời thêm câu hỏi thứ hai: "**ai** trong tổ chức đó đang gọi" — cần thiết ngay khi có khái niệm giỏ hàng, vì giỏ hàng của người dùng A không được lẫn với giỏ hàng của người dùng B dù họ cùng 1 tenant.

Giải pháp: 1 file mới gần như **sao chép y hệt cấu trúc** của `TenantContext`/`TenantContextMiddleware` đã có, chỉ đổi tên khái niệm — [`shared/Tenancy/CallerContext.cs`](../../shared/Tenancy/CallerContext.cs):
```csharp
public sealed class CallerContext
{
    public string? SubjectId { get; set; }

    public string RequireSubjectId() =>
        string.IsNullOrWhiteSpace(SubjectId)
            ? throw new MissingCallerContextException()   // giống hệt cách TenantContext ném lỗi
            : SubjectId;
}
```
[`shared/Tenancy/CallerContextMiddleware.cs`](../../shared/Tenancy/CallerContextMiddleware.cs) đọc header `X-Subject-Id` — cùng cơ chế header với `X-Tenant-Id`, chỉ khác tên. Đây là quyết định thiết kế đáng chú ý: thay vì gộp chung "tenant + subject" vào 1 khái niệm, repo tách thành 2 class riêng biệt nhưng **cùng hình dạng**. Lý do (đúng như tên gọi 2 khái niệm): tenant quyết định request được chạm vào **kho dữ liệu nào**; subject quyết định request được chạm vào **hàng nào bên trong kho đó**. Hai câu hỏi khác nhau, nên 2 cơ chế riêng — nhưng dùng chung 1 khuôn để ai hiểu 1 cái là hiểu ngay cái kia.

`TenancyExtensions.cs` (đã cho xem bản gốc chỉ có tenant ở Giai đoạn 1) được MỞ RỘNG thêm nửa còn lại:
```diff
 public static IServiceCollection AddTenancy(this IServiceCollection services)
 {
     services.AddScoped<TenantContext>();
+    services.AddScoped<CallerContext>();
     return services;
 }

 public static WebApplication UseTenancy(this WebApplication app)
 {
     app.UseMiddleware<TenantContextMiddleware>();
+    app.UseMiddleware<CallerContextMiddleware>();
     return app;
 }
```
Điểm đáng nhớ: đây vẫn là **1 lời gọi `AddTenancy()`/`UseTenancy()` duy nhất** cho cả 2 khái niệm, không tách thành `AddTenancy()` + `AddCallerIdentity()` riêng. Lý do ghi trong code: 1 service resolve được tenant nhưng quên resolve caller là 1 service **nửa vời** — tách thành 2 lời gọi là tạo ra 2 cơ hội để quên mất 1 cái.

Phía gateway, [`SubjectHeaderPropagationMiddleware.cs`](../../services/gateway/src/Gateway.Api/Identity/SubjectHeaderPropagationMiddleware.cs) (mới) là bản sao gần như y hệt `TenantHeaderPropagationMiddleware` của Giai đoạn 1 — cùng nguyên tắc "ghi đè, không bao giờ tin giá trị client tự gửi lên", chỉ đổi nguồn đọc từ claim tenant sang `ClaimTypes.NameIdentifier` (claim chuẩn .NET cho "định danh người dùng"). Comment trong code nói thẳng lý do lặp lại: *"giữ 2 middleware giống hệt nhau nghĩa là ai hiểu 1 cái thì hiểu cả 2, và việc thay bằng danh tính thật ở Giai đoạn 4 sẽ không đổi cái nào trong 2 cái"* — dự đoán này hoàn toàn đúng, xem [06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md](06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md).

### Spec 005 và 006 — không có thay đổi shared/

`specs/005-one-command-local-run` (dựng `docker-compose.yml`, script `up`/`down`) và `specs/006-e2e-order-demo` (kịch bản demo Playwright) không sửa gì trong `shared/` — 2 spec này làm việc ở tầng hạ tầng chạy/kiểm thử, không phải cơ chế dùng chung giữa các service.

## Thay đổi trong service nghiệp vụ

### Tổng tiền đơn hàng được tính ở đâu? (spec 004)

Commit: `c99783c feat: Implement checkout workflow in BFF`

Trước commit này, `Order.cs` chỉ có 3 field trơ (`Id`, `PlacedAtUtc`, `Total`) — không có cách nào tạo 1 order kèm tính tổng tiền, `Total` phải được set thủ công từ bên ngoài. Diff dưới đây (rút gọn) cho thấy method `PlaceFrom` ra đời:

```diff
 public class Order
 {
     public Guid Id { get; set; }
     public DateTime PlacedAtUtc { get; set; }
     public decimal Total { get; set; }
+
+    public static Order PlaceFrom(IReadOnlyCollection<OrderLine> lines, DateTime placedAtUtc)
+    {
+        if (lines.Count == 0)
+            throw new ArgumentException("An order needs at least one line...", nameof(lines));
+
+        foreach (var line in lines)
+        {
+            ArgumentOutOfRangeException.ThrowIfLessThan(line.Quantity, 1, nameof(lines));
+            ArgumentOutOfRangeException.ThrowIfNegative(line.UnitPrice, nameof(lines));
+        }
+
+        return new Order
+        {
+            Id = Guid.NewGuid(),
+            PlacedAtUtc = placedAtUtc,
+            Total = lines.Sum(line => line.Quantity * line.UnitPrice),   // ← tính Ở ĐÂY
+        };
+    }
 }
+public sealed record OrderLine(Guid ProductId, int Quantity, decimal UnitPrice);
```

Đây là quyết định kiến trúc nhỏ nhưng quan trọng, đáng nhớ khi review PR sau này: **tổng tiền được tính trong chính service `orders`, không phải ở BFF hay ở client**. Lý do ghi rõ trong code: "BFF cung cấp đã mua gì; **giá trị nó thành bao nhiêu tiền là câu trả lời của service này**" — nói cách khác, phép toán tiền tệ luôn thuộc về service sở hữu bản ghi, tầng tổng hợp phía trên (BFF) không bao giờ được tự cộng trừ tiền. Cùng nguyên tắc này lặp lại ở `Baskets.Api` cùng thời điểm (`1bc77a6 Implement basket total computation and BFF integration for shopping basket`) — `LineTotal` mỗi dòng giỏ hàng cũng được tính trong `Baskets.Api`, không phải BFF (đã thấy ở [02-orders-service-va-cac-api-endpoint.md § So sánh](02-orders-service-va-cac-api-endpoint.md#4-so-sánh-orders-với-3-service-còn-lại)).

### Route "trải dài nhiều service" đầu tiên: checkout (spec 004)

Cùng commit `c99783c`. Tất cả route đã thấy ở Tài liệu 2 chỉ gọi **1** service phía sau. `checkout` là route đầu tiên phá vỡ điều đó — [`services/bff/src/Bff.Api/Features/Checkout/CheckoutEndpoints.cs`](../../services/bff/src/Bff.Api/Features/Checkout/CheckoutEndpoints.cs):

```csharp
group.MapPost("/checkout", async (BasketsApiClient baskets, OrdersApiClient orders, ...) =>
{
    var basket = await baskets.GetCurrentBasketAsync(cancellationToken);      // (1) đọc giỏ hàng

    if (basket.Items.Count == 0)
        return Results.Problem(title: "Basket is empty", statusCode: 409);    // chặn giỏ rỗng

    var order = await orders.PlaceOrderAsync(new PlaceOrderCommand([...]), cancellationToken); // (2) đặt hàng

    var cleared = await baskets.ClearCurrentBasketAsync(cancellationToken);   // (3) xoá giỏ hàng
    if (!cleared)
        LogBasketNotCleared(logger, order.Id);   // (3) thất bại → chỉ LOG, không báo lỗi cho khách

    return Results.Created($"/bff/orders/{order.Id}", new OrderConfirmationResponse(order.Id, order.PlacedAtUtc, order.Total));
})
.RequireAuthorization(AuthorizationPolicies.ApiScope);
```

Đây là 1 ví dụ thực tế cho khái niệm **"workflow xuyên nhiều service"** — và quan trọng hơn, code tự thừa nhận 1 giới hạn kỹ thuật thay vì giấu đi. Đọc comment gốc trong file: *"Đây là route DUY NHẤT trong BFF trải dài 1 workflow thay vì 1 lần đọc đơn: đọc giỏ hàng của người gọi, đặt hàng cho các dòng trong đó, rồi xoá giỏ hàng. Về nguyên tắc, đây nên được mô hình hoá thành 1 'saga' có bước bù trừ tường minh (compensation) khi 1 bước giữa chừng thất bại; nhưng chưa có hạ tầng messaging nào tồn tại ở giai đoạn này, nên đây là 1 sự sai lệch có ghi chú, có giới hạn thời gian (đóng lại bởi SCRUM-18/SCRUM-31)."*

Nói dễ hiểu: nếu bước (2) đặt hàng thành công nhưng bước (3) xoá giỏ hàng thất bại (ví dụ Baskets service tạm thời lỗi), khách hàng vẫn nhận được đơn hàng hợp lệ (ưu tiên: **thà thừa 1 dòng giỏ hàng cũ, còn hơn mất luôn đơn hàng đã đặt** — đó là lý do đặt hàng luôn chạy TRƯỚC xoá giỏ, không phải ngược lại) nhưng giỏ hàng của họ vẫn còn sản phẩm cũ. Đây là kiểu vấn đề "distributed transaction" kinh điển trong microservices — repo chọn giải pháp đơn giản nhất có thể (log lại, không rollback) cho giai đoạn này, và ghi rõ đây là nợ kỹ thuật đã biết, không phải sơ suất. Khi bạn review 1 PR có 2+ lệnh gọi service liên tiếp mà không có cơ chế bù trừ, đây là điểm cần hỏi: "nếu bước 2 thất bại sau khi bước 1 đã thành công thì sao?"

### Gán tenant vào đơn hàng — spec 006

Commit: `b3873b5 feat: Add tenant attribution to orders` (thuộc `specs/006-e2e-order-demo`, KHÔNG phải spec 002/003 như bản nháp đầu của tài liệu Giai đoạn 1 từng ghi nhầm).

Kịch bản demo end-to-end (`frontend/apps/web/demo/order-demo.spec.ts`, chạy bằng Playwright) cần 1 cách **nhìn thấy bằng mắt** rằng 2 tenant khác nhau thực sự bị cô lập dữ liệu — đó là động lực khiến `TenantId` được thêm thẳng vào entity `Order` (trước đó `Order` không có field này, dù `Program.cs` đã chặn truy cập DB thiếu tenant từ spec 003):

```diff
 public static Order PlaceFrom(
     IReadOnlyCollection<OrderLine> lines,
     DateTime placedAtUtc,
+    string tenantId)
 {
+    if (string.IsNullOrWhiteSpace(tenantId))
+        throw new ArgumentException("An order needs the tenant it was placed for...", nameof(tenantId));
+
     if (lines.Count == 0) { ... }
     // ...
     return new Order
     {
         Id = Guid.NewGuid(),
         PlacedAtUtc = placedAtUtc,
         Total = lines.Sum(line => line.Quantity * line.UnitPrice),
+        TenantId = tenantId,
     };
 }
```

Đây chính là phiên bản `Order.PlaceFrom` bạn đã thấy đầy đủ ở [02-orders-service-va-cac-api-endpoint.md § 3.2](02-orders-service-va-cac-api-endpoint.md#32-tạo-đơn-hàng--post-orders) — giờ bạn đã biết nó được ghép từ 3 mảnh, xây ở 3 thời điểm khác nhau: khung rỗng (spec 002) → phép tính `Total` (spec 004) → gán `TenantId` (spec 006). Đây là ví dụ rất thực tế của việc 1 file "trông đơn giản" thực ra là kết quả tích luỹ của nhiều quyết định nhỏ theo thời gian — lý do càng nên tra `git log --follow <file>` khi 1 đoạn code khiến bạn thắc mắc "sao lại làm thế này" thay vì đoán.

### Spec 005 — hạ tầng chạy, không phải logic nghiệp vụ

`specs/005-one-command-local-run` chủ yếu thêm `docker-compose.yml`, script `scripts/up.sh`/`up.ps1`/`down.sh` và 1 dự án test riêng (`tests/ContainerConventionTests`) quét các `Dockerfile` để đảm bảo mọi service build đúng quy ước — đây là loại "scanner tĩnh" cùng họ với các scanner đã nhắc ở Tài liệu 4 cũ (đọc file cấu hình, không khởi động service). Có 1 thay đổi code nhỏ đáng chú ý: `fd8ed2d` thêm "request path warming" (gọi thử 1 request nội bộ ngay sau khi service khởi động, để lần gọi ĐẦU TIÊN của người dùng thật không phải chịu độ trễ "cold start" của .NET JIT) — nhưng đây là 1 dòng cấu hình nhỏ trong `appsettings.Development.json`, không phải logic nghiệp vụ mới.

## Đi đâu tiếp theo

- [05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md](05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md) — hợp đồng OpenAPI, schema sự kiện có version, viết lại theo TDD, test trên database thật, test hợp đồng giữa các service.
