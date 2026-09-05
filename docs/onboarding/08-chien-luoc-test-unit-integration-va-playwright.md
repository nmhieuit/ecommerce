# 08 — Chiến lược test: unit, integration hiện tại và định hướng theo test pyramid

> Đọc [01](01-tong-quan-kien-truc.md), [02](02-orders-service-va-cac-api-endpoint.md) và [07](07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md) trước. Tài liệu này viết cho người đã có kinh nghiệm sâu về **Playwright** (cả API test lẫn E2E test) và nắm vững **test pyramid**, nhưng chưa quen unit/integration test viết bằng .NET/xUnit trong repo này.
>
> **Quy ước đọc tài liệu này:** Phần 1-3 là **sự thật** — mọi con số, tên file, đoạn code đều trích trực tiếp từ repo. Phần 4-5 là **khuyến nghị** — có thể tranh luận, và tôi ghi rõ ràng buộc/giả định đằng sau mỗi khuyến nghị thay vì trình bày như thể đó cũng là sự thật đã kiểm chứng.

## Phần 1 — Unit test hiện tại: khác gì với "unit test" bạn quen ở JS/TS

Điểm khác biệt quan trọng nhất cần nắm trước: unit test .NET trong repo này **không bao giờ** gọi HTTP, không chạm database, không cần server nào chạy — nó gọi thẳng 1 hàm/1 class C# trong bộ nhớ, y hệt việc bạn test 1 hàm JS thuần (`function calculateTotal(items) {...}`) mà không dựng Express server nào cả. Khác với việc dùng Playwright để test API (vẫn là request HTTP thật), unit test .NET ở đây kiểm tra **domain logic thuần tuý** trước khi nó chạm tới bất kỳ lớp HTTP/DB nào.

Ví dụ tiêu biểu nhất — [`services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs`](../../services/baskets/tests/Baskets.Api.UnitTests/BasketLineMergeTests.cs):
```csharp
[Fact]
public void AddItem_KeepsTheOriginallyCapturedPrice_WhenTheCatalogPriceHasChanged()
{
    var basket = Basket.ForCustomer("phase1-stub-user");   // KHÔNG có DB, KHÔNG có HTTP — chỉ new 1 object
    basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);

    basket.AddItem(Notebook, quantity: 1, unitPrice: 99.99m);  // giá catalog "đã đổi"

    var line = Assert.Single(basket.LineItems);
    Assert.Equal(12.50m, line.UnitPrice);   // giá GIỮ NGUYÊN như lần đầu thêm vào
    Assert.Equal(2, line.Quantity);
}
```
`[Fact]` (xUnit) tương đương `it()`/`test()` của Jest. Test này chạy xong trong vài mili-giây vì không có I/O nào cả — đây chính là lý do comment trong file nói thẳng: *"Unit test chứ không phải integration test, vì đây là 1 quy tắc domain — phải đúng TRƯỚC KHI bất cứ thứ gì được lưu, và assert ở đây nghĩa là 1 regression fail trong vài mili-giây thay vì sau khi 1 container khởi động."*

Tương tự, [`services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs`](../../services/orders/tests/Orders.Api.UnitTests/OrderTotalTests.cs) kiểm tra phép tính tiền của `Order.PlaceFrom(...)` (đã xem ở [02](02-orders-service-va-cac-api-endpoint.md)) — kể cả 1 case rất cụ thể đáng chú ý:
```csharp
[Fact]
public void PlaceFrom_IsExact_ForAmountsThatFloatingPointWouldRound()
{
    var order = Order.PlaceFrom([new OrderLine(Notebook, 1, 0.10m), new OrderLine(Apron, 1, 0.20m)], Now, Tenant);
    Assert.Equal(0.30m, order.Total);   // 0.10 + 0.20 = ĐÚNG 0.30, không phải 0.30000000000000004
}
```
Đây là 1 lớp bug .NET giải quyết sẵn bằng kiểu dữ liệu `decimal` (khác `double`) cho tiền tệ — 1 khái niệm không có tương đương trực tiếp bên JS/TS (JS chỉ có `number`, phải tự xử lý bằng thư viện như `decimal.js` nếu muốn tránh lỗi làm tròn tương tự).

**Bảng đầy đủ unit test hiện có** (đã liệt kê trực tiếp từ hệ thống file, không suy đoán):

| Vị trí | File | Kiểm tra domain logic gì |
|---|---|---|
| `services/baskets` | `BasketLineMergeTests.cs`, `BasketTotalTests.cs` | Gộp dòng trùng sản phẩm, giữ giá ban đầu, tính tổng tiền giỏ hàng |
| `services/orders` | `OrderTotalTests.cs`, `OrderTenantTests.cs` | Tính tổng đơn hàng, validate input, gán tenant (đã xem ở [04](04-giai-doan-2-spa-va-demo-end-to-end.md)) |
| `services/parties`, `services/products` | chỉ có `HealthCheckTests.cs` | Không có domain logic tính toán nào để unit test — Parties/Products chỉ đọc dữ liệu, không có phép tính riêng (khớp với [02](02-orders-service-va-cac-api-endpoint.md): Products/Parties là 2 service đơn giản nhất) |
| `services/bff` | `DownstreamServiceClientOptionsTests.cs`, `ResponseMappingTests.cs` | Cấu hình HttpClient, ánh xạ response — không phải domain logic tiền tệ vì BFF không có domain logic (đã nhắc ở [02](02-orders-service-va-cac-api-endpoint.md#5-bff--cùng-pattern-khác-vai-trò)) |
| `services/gateway` | `RouteConfigurationTests.cs`, `ForwardingTimeoutBudgetTests.cs`, `StubIdentityAuthenticationHandlerTests.cs`, `SubjectHeaderPropagationMiddlewareTests.cs` | Cấu hình route YARP, ngân sách timeout, logic middleware stub identity (đã xem ở [03](03-giai-doan-1-nen-tang-dich-vu-va-routing.md)) |
| `shared/Identity.UnitTests` | 4 file | `RequireApiScopeAuthorizationHandler`, `AuthenticationFallbackPolicy`... (đã xem code thật ở [06](06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md)) |
| `shared/Tenancy.UnitTests` | 4 file | `TenantContext`/`CallerContext` và 2 middleware (đã xem code thật ở [03](03-giai-doan-1-nen-tang-dich-vu-va-routing.md), [04](04-giai-doan-2-spa-va-demo-end-to-end.md)) |
| `shared/EventContracts.UnitTests` | `SchemaValidationTests.cs`, `SchemaImmutabilityTests.cs`, `TolerantReaderTests.cs` | Đối tượng C# `OrderPlacedV1` khớp đúng file JSON schema; schema `V1` không bị sửa tại chỗ (đã nhắc ở [05](05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md)) |

## Phần 2 — Integration test hiện tại: gần với "API test" bạn quen, nhưng chạy trong 1 tiến trình

Đây là tầng gần nhất với khái niệm "API test" của Playwright — thật sự gọi HTTP (`client.PostAsJsonAsync(...)`, `client.GetAsync(...)`) vào 1 service, và service đó thật sự chạm database thật (SQL Server qua Testcontainers, đã giải thích ở [05](05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md)). Khác biệt cốt lõi so với Playwright API test: **`client` ở đây không phải HTTP client thật gọi qua mạng** — nó là `WebApplicationFactory<Program>.CreateClient()`, một client đặc biệt của .NET chạy thẳng vào tiến trình service **trong cùng bộ nhớ** (in-process), không mở port TCP nào, không đi qua network stack thật.

Ví dụ 1 service đơn lẻ — [`services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs`](../../services/orders/tests/Orders.Api.IntegrationTests/PlaceOrderTests.cs):
```csharp
public class PlaceOrderTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task PlaceOrder_CreatesTheOrder_AndComputesItsTotal()
    {
        await using var factory = await CreateFactoryAsync("orders-place");   // khởi động Orders.Api thật, trong bộ nhớ
        var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/orders", new { items = new[] { ... } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.Equal(59.25m, order.Total);   // cùng con số 59.25 sẽ gặp lại ở Phần 3
    }
}
```

Mỗi service cũng có 1 bộ integration test lặp lại cùng khuôn cho các mối quan tâm xuyên suốt (cross-cutting) đã học ở tài liệu trước — ví dụ `TenantEnforcementTests.cs` (gọi service KHÔNG qua gateway → không có tenant → phải lỗi to, khớp [03](03-giai-doan-1-nen-tang-dich-vu-va-routing.md)):
```csharp
[Fact]
public async Task ResolvingTheDbContext_Throws_WhenNoTenantHasBeenResolved()
{
    Assert.Throws<MissingTenantContextException>(
        () => scope.ServiceProvider.GetRequiredService<BasketsDbContext>());
}
```
và `IndependentTokenValidationTests.cs` (gọi service không kèm token hoặc token giả mạo → `401`, khớp [06](06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md)), và `AuthorizationPolicyTests.cs` (token thiếu scope → `403`).

### Trường hợp đặc biệt: `CheckoutTests.cs` — integration test XUYÊN 3 SERVICE

[`services/bff/tests/Bff.Api.IntegrationTests/CheckoutTests.cs`](../../services/bff/tests/Bff.Api.IntegrationTests/CheckoutTests.cs) khác hẳn mọi test ở trên: nó khởi động **3 service thật cùng lúc** (`extern alias BasketsApi/OrdersApi/ProductsApi` ở đầu file) làm 3 `WebApplicationFactory` riêng, chia sẻ **1 SQL Server thật** (mỗi service 1 database riêng trên cùng server đó — [`DownstreamServicesFixture.cs`](../../services/bff/tests/Bff.Api.IntegrationTests/DownstreamServicesFixture.cs)), rồi gọi qua BFF thật:
```csharp
[Fact]
public async Task Checkout_CreatesAnOrder_ForWhatIsInTheBasket()
{
    await using var services = await StartServicesAsync("checkout-happy");   // 3 service thật, trong 1 tiến trình test
    var client = BffTestHost.CreateShopperClient(services.Bff);

    await AddAsync(client, Notebook, quantity: 2);
    await AddAsync(client, Apron, quantity: 1);

    var response = await client.PostAsync("/bff/checkout", content: null);

    var confirmation = await response.Content.ReadFromJsonAsync<OrderConfirmationResponse>();
    Assert.Equal(59.25m, confirmation.Total);   // CÙNG con số sẽ gặp lại ở order-demo.spec.ts, Phần 3
}
```
Đây thực chất là 1 test "end-to-end" theo đúng nghĩa nghiệp vụ (basket → checkout → order → basket rỗng, đã kể ở [04](04-giai-doan-2-spa-va-demo-end-to-end.md)) — chỉ khác là chạy **trong bộ nhớ**, không qua network thật, không qua Docker, không qua trình duyệt. Giữ ý này lại, vì đây chính là trọng tâm của Phần 4.

### Kỹ thuật đáng chú ý: giả lập lỗi mạng KHÔNG cần dừng container thật

[`DownstreamUnavailableTests.cs`](../../services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs) kiểm tra "BFF trả lỗi rõ ràng trong < 5 giây khi 1 service phía sau chết" — nhưng **không hề khởi động rồi tắt 1 container thật**. Comment nói rõ: *"Không service nào được khởi động ở đây... chỉ có tầng transport bên dưới bị thay thế"* — nó thay `HttpClient` thật bằng 1 handler giả luôn ném lỗi mạng, để kiểm soát chính xác thời điểm lỗi xảy ra (phân biệt `502` và `504` phụ thuộc lỗi xảy ra trong hay ngoài ngân sách timeout 1 giây — comment kể lại: assert theo 1 host thật-không-tồn-tại từng PASS khi chạy riêng nhưng FAIL không ổn định khi chạy cùng các suite Testcontainers khác, vì tốc độ phân giải DNS thay đổi theo tải máy). Giữ ý này lại cho Phần 4 — đây là ví dụ test **không nên** đẩy lên tầng cao hơn.

## Phần 3 — Đối chiếu với Playwright hiện có trong repo

Tìm kiếm trực tiếp xác nhận: repo hiện chỉ có **đúng 2 file spec Playwright**, không hơn:
- [`frontend/apps/web/e2e/walkthrough.spec.ts`](../../frontend/apps/web/e2e/walkthrough.spec.ts) — chạy trên dev server, kiểm tra hành vi CỦA STOREFRONT (lỗi console, thao tác bàn phím, request đi đúng đích).
- [`frontend/apps/web/demo/order-demo.spec.ts`](../../frontend/apps/web/demo/order-demo.spec.ts) — chạy trên **toàn bộ docker-compose stack thật**, chứng minh NỀN TẢNG hoạt động đúng.

**Quan sát thực tế (không phải khuyến nghị):** repo hiện **chưa có** 1 tầng "Playwright API test" riêng (gọi thẳng API bằng `request` context của Playwright, không qua UI trình duyệt) — cả 2 spec trên đều lái qua UI thật (`page.goto`, `page.getByRole(...).click()`), dù `order-demo.spec.ts` có DÙNG `page.request.get(...)` cho 1 bước xác nhận đơn lẻ (đọc lại đơn hàng qua gateway) chứ không phải cả bài test.

**Phát hiện quan trọng nhất cho Phần 4:** `order-demo.spec.ts` (Playwright, chạy qua UI + docker-compose thật) và `CheckoutTests.cs` (Phần 2, .NET, chạy in-process) đang **kiểm tra cùng 1 kịch bản nghiệp vụ**, với cùng 1 con số:
| | `CheckoutTests.cs` (.NET, in-process) | `order-demo.spec.ts` (Playwright, stack thật) |
|---|---|---|
| Thêm 2 notebook + 1 apron | ✅ | ✅ |
| Checkout → tổng tiền = 59.25 | ✅ (`Assert.Equal(59.25m, ...)`) | ✅ (`EXPECTED_TOTAL = '$59.25'`) |
| Đọc lại đơn hàng khớp với xác nhận | ✅ | ✅ (`page.request.get(...)`) |
| Giỏ hàng rỗng sau checkout | ✅ | ✅ |
| Chạy qua | 3 `WebApplicationFactory` trong 1 tiến trình, network giả | Trình duyệt thật → gateway thật → BFF thật → 3 service thật, network THẬT |

## Phần 4 — Khuyến nghị: case nào nên đẩy lên tầng Playwright (có thể tranh luận)

> Toàn bộ mục này là **nhận định của tôi** dựa trên phát hiện ở Phần 3, không phải sự thật đã được ai trong dự án xác nhận là định hướng chính thức.

**Khuyến nghị 1 — cân nhắc bớt phạm vi của `CheckoutTests.cs`, không xoá nó.** Theo test pyramid, việc `CheckoutTests.cs` và `order-demo.spec.ts` cùng assert đúng 1 con số nghiệp vụ (59.25, giỏ hàng rỗng, đọc lại khớp) ở 2 tầng là trùng lặp phạm vi kiểm tra — nhưng lý do KHÔNG PHẢI "trùng thì bỏ 1 cái", vì 2 test này thật ra đang chứng minh 2 điều khác nhau:
- `CheckoutTests.cs` chứng minh: **code C# của 3 service phối hợp đúng logic** (đúng thứ tự gọi, đúng phép tính) — nhanh (không cần Docker/browser), chạy được trên máy dev bất kỳ.
- `order-demo.spec.ts` chứng minh: **cấu hình triển khai thật hoạt động** (đúng port publish, đúng DNS nội bộ Docker, CORS đúng origin, token thật được service thật chấp nhận qua network thật).

Đây chính là đúng loại lỗi mà `CheckoutTests.cs` **không thể** bắt được vì nó không rời khỏi tiến trình .NET: 2 bug thật đã gặp trong phiên làm việc trước với Postman (Docker port chưa publish; OIDC issuer lệch do header `Host` khác nhau giữa `localhost` và `identity-api:8080`) — cả 2 đều là lỗi tầng **triển khai/network**, và `CheckoutTests.cs` sẽ **PASS** ngay cả khi 2 bug đó tồn tại, vì nó chưa từng gọi qua network thật. Do đó khuyến nghị: bổ sung 1 **Playwright API test** (gọi thẳng gateway thật qua HTTP, không qua UI — nhanh hơn `order-demo.spec.ts` vì bỏ qua trình duyệt) cho đúng kịch bản checkout này, chạy trên docker-compose thật, để bắt được lớp lỗi triển khai mà cả `CheckoutTests.cs` lẫn unit test không bao giờ chạm tới. `CheckoutTests.cs` vẫn giữ nguyên vai trò bảo vệ đúng logic nghiệp vụ nhanh, không cần xoá.

**Khuyến nghị 2 — KHÔNG đẩy `DownstreamUnavailableTests.cs` lên Playwright.** Đây là ví dụ ngược lại: test pyramid không có nghĩa "mọi thứ đẩy lên cao nhất có thể". Muốn tái hiện đúng kịch bản "1 service chết giữa chừng" bằng Playwright E2E thật sẽ cần dừng 1 container Docker đúng lúc test đang chạy — chậm, khó lặp lại chính xác thời điểm (race condition y hệt vấn đề `502` vs `504` mà chính comment trong file đã ghi nhận). Giữ nguyên ở tầng integration .NET (giả lập transport) là lựa chọn ĐÚNG, không phải thiếu sót cần "nâng cấp".

**Khuyến nghị 3 (thấp ưu tiên hơn) —** các integration test theo khuôn lặp lại y hệt nhau ở 6 service (`IndependentTokenValidationTests.cs`, `AuthorizationPolicyTests.cs`, `TenantEnforcementTests.cs`) nên **giữ nguyên ở tầng integration**, không đẩy lên Playwright: đây là hành vi của TỪNG service riêng lẻ (401/403/lỗi tenant), test 1 lần cho mỗi service là đủ và rẻ hơn nhiều so với việc dựng 1 kịch bản Playwright cho từng service — Playwright/E2E chỉ nên xác nhận 1-2 đại diện của lớp bảo vệ này hoạt động xuyên suốt hệ thống thật (ví dụ 1 request không token bị chặn ngay ở gateway), không cần lặp lại cho cả 6 service.

## Phần 5 — Case nào bắt buộc auto, case nào nên manual

### Bắt buộc auto (dựa trên rủi ro đã thấy xuyên suốt tài liệu 01-07)

Mọi thứ liên quan tới **tiền** (`OrderTotalTests.cs`, `BasketTotalTests.cs`), **cô lập dữ liệu giữa tenant** (`TenantEnforcementTests.cs`, `ConnectionStringScanner`, `TenantGatedConnectionScanner`), và **phân quyền** (`AuthorizationPolicyTests.cs`, `AuthorizationPolicyDeclaredScanner`) đã VÀ NÊN TIẾP TỤC là tự động 100% — đây là 3 nhóm rủi ro cao nhất đã được chính đội ngũ trước bạn xác định và tự động hoá xuyên suốt 015 features, không phải khuyến nghị mới từ tôi.

### Đã ghi nhận là thủ công trong repo, và LÝ DO thật (không phải "không tự động hoá được")

Chỉ có **1 kỹ thuật** được tài liệu hoá rõ ràng là làm thủ công, không phải mọi test case: `specs/009-retrofit-tdd-basket-order/research.md` (Decision 4) — kỹ thuật "làm yếu 1 guard, xem test đỏ, rồi phục hồi" để CHỨNG MINH 1 test thật sự bắt được lỗi nó tuyên bố bắt (đã nhắc ở [05](05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md#spec-009--không-phải-code-mà-là-1-quy-tắc-ràng-buộc-cách-viết-code)). Lý do ghi thẳng trong code: đây là *"bước xác minh một lần, phù hợp với 1 story retrofit nhỏ, với 1 tập quy tắc đã nhỏ và đã có test"* — và có nêu rõ giải pháp tự động hoá đã bị loại (mutation testing, ví dụ Stryker.NET) vì *"không tương xứng cho 6 quy tắc đã được test, trong 1 repo chưa có tiền lệ mutation-testing"* — kèm điều kiện rõ ràng để xem lại: *"đáng cân nhắc lại ở quy mô toàn nền tảng nếu mô hình này cần mở rộng"*. Nói cách khác: đây là thủ công **theo lựa chọn có cân nhắc chi phí/lợi ích tại thời điểm đó**, không phải giới hạn kỹ thuật vĩnh viễn.

Ngoài trường hợp này, tôi **không tìm thấy bằng chứng nào khác** trong repo ghi nhận rõ 1 test case cụ thể là "bắt buộc phải thủ công".

### Gợi ý theo kinh nghiệm chung của ngành (KHÔNG phải bằng chứng từ repo — bạn tự cân nhắc)

Theo yêu cầu của bạn, đây là các loại việc mà thực hành chung của ngành thường xếp vào "nên giữ thủ công", áp dụng cho các luồng rủi ro cao đã biết của repo này (thanh toán/checkout, phân quyền):
- **Exploratory testing** trước mỗi lần bật 1 feature toggle rủi ro cao lên production lần đầu (ví dụ `AuthorizationRequireApiScope` hay `IdentityServerAuthCutover`, cả 2 đã xem ở [06](06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md)) — 1 người thử "phá" hệ thống theo cách không kịch bản nào lường trước, đúng ngay thời điểm rủi ro cao nhất (lần bật đầu tiên).
- **UAT (User Acceptance Testing)** cho các thay đổi UI ảnh hưởng trực tiếp tới luồng checkout — máy không đánh giá được "trải nghiệm có ổn không", chỉ đánh giá được "có đúng như kịch bản viết sẵn không".
- **Visual regression / kiểm tra bằng mắt** cho các trang có yêu cầu hiển thị chính xác (ví dụ trang xác nhận đơn hàng) — Playwright có thể tự động hoá phần này bằng screenshot-diffing, nhưng bước "cái khác biệt này có chấp nhận được không" vẫn cần con người quyết định, không nên để ngưỡng tự động tự ý pass/fail.

## Đi đâu tiếp theo

- [05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md](05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md) — cơ chế Testcontainers/Pact contract test đứng sau tầng integration.
- [07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md](07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md) — nơi unit/integration test này chạy trong pipeline CI thật.
