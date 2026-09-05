# 05 — Giai đoạn 3: Hợp đồng API & hạ tầng kiểm thử

> Đọc [03](03-giai-doan-1-nen-tang-dich-vu-va-routing.md) và [04](04-giai-doan-2-spa-va-demo-end-to-end.md) trước — giai đoạn này không thêm khái niệm nền tảng mới (tenant/caller đã xong), mà **siết chặt cách các service cam kết với nhau** và **thay dần test giả bằng test thật**.
>
> **Specs thuộc giai đoạn này:** `specs/007-bff-openapi-contracts`, `specs/008-versioned-event-schemas`, `specs/009-retrofit-tdd-basket-order`, `specs/010-testcontainers-integration-tests`, `specs/011-consumer-contract-tests`.
> **Kỹ thuật trọng tâm:** OpenAPI làm hợp đồng sinh code tự động cho frontend, schema sự kiện có version ngay cả khi chưa có publisher, quy tắc TDD tường minh cho logic tiền tệ, test trên database/Redis/RabbitMQ **thật** thay vì mock, và test hợp đồng (contract test) giữa 2 service chưa từng gọi nhau qua HTTP.

## Thay đổi trong shared/

### `shared/EventContracts` — định nghĩa "hình dạng" sự kiện TRƯỚC KHI có ai publish nó (spec 008)

Commit: `5233bd6 Add initial implementation of EventContracts with OrderPlaced and BasketCheckedOut events`, `c549ec7 feat: Implement versioned event schemas for OrderPlaced and BasketCheckedOut`

[`shared/EventContracts/OrderPlacedV1.cs`](../../shared/EventContracts/OrderPlacedV1.cs):
```csharp
public sealed record OrderPlacedV1(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("occurredAtUtc")] DateTime OccurredAtUtc,
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("tenantId")] string TenantId,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("total")] decimal Total,
    [property: JsonPropertyName("lines")] IReadOnlyList<OrderLineV1> Lines);
```
Chi tiết đáng chú ý nhất: **hậu tố `V1` trong tên type**. Comment trong code nói rõ luật: 1 thay đổi phá vỡ tương thích (thêm field bắt buộc, xoá field, đổi kiểu/ý nghĩa) phải ra đời dưới tên `OrderPlacedV2` — **không sửa `V1` tại chỗ**. Đây là kỹ thuật "versioned schema" kinh điển cho hệ thống hướng sự kiện: khi 1 sự kiện đã có người tiêu thụ (consumer), sửa thẳng vào schema cũ có thể làm consumer đó đọc sai dữ liệu mà không hề biết — buộc tạo bản mới, giữ bản cũ, để consumer tự quyết định khi nào nâng cấp.

Một chi tiết dễ gây nhầm lẫn nếu chỉ đọc code hiện tại: **`OrderPlacedV1` KHÔNG được publish ở đâu cả** — không có message broker nào gọi tới type này trong toàn bộ `services/`. Đây là quyết định có chủ đích, không phải việc dang dở bị bỏ quên: hạ tầng "outbox" (cơ chế đảm bảo 1 sự kiện được publish tin cậy cùng lúc với việc ghi database) là công việc của 1 công việc khác trong tương lai (nhắc tới trong code là "SCRUM-18"). Điều spec 008 làm là **chốt hình dạng hợp đồng trước**, để khi hạ tầng publish thật được xây, nó xây theo đúng 1 hợp đồng đã được thống nhất, thay vì vừa xây publisher vừa nghĩ ra schema.

### `shared/IntegrationTestSupport` — dependency thật, dùng chung cho mọi service (spec 010)

Commit: `cba4449 feat: Add Testcontainers integration test infrastructure for SQL Server, Redis, and RabbitMQ`

Trước spec này, mỗi service tự có 1 `SqlServerFixture` riêng (đã thấy trong danh sách file Orders ở Tài liệu 2) — Testcontainers tự khởi động 1 container SQL Server thật cho mỗi lượt chạy test. Spec 010 kéo phần **dùng chung được** (Redis, RabbitMQ — 2 dependency không service nào có riêng) ra `shared/IntegrationTestSupport`, để service tương lai cần Redis/RabbitMQ không phải viết lại. [`shared/IntegrationTestSupport/RedisFixture.cs`](../../shared/IntegrationTestSupport/RedisFixture.cs):
```csharp
public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:7.4").Build();
    public string ConnectionString => _container.GetConnectionString();
    public Task InitializeAsync() => _container.StartAsync();   // khởi động container Docker THẬT
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
```
`IAsyncLifetime` là giao diện của xUnit: `InitializeAsync()` chạy 1 lần trước khi bộ test dùng fixture này bắt đầu, `DisposeAsync()` chạy sau khi xong — tự động khởi động/dọn container, không ai phải nhớ chạy `docker run` tay. `RabbitMqFixture.cs` là bản sao y hệt cho RabbitMQ. Đúng như `docker-compose.local.yml` từng ghi chú (Tài liệu 1): "chưa service nào thật sự kết nối tới Redis/RabbitMQ" — 2 fixture này tồn tại **trước** khi có code nghiệp vụ dùng chúng, cùng tinh thần "chuẩn bị hạ tầng test trước khi cần" như `EventContracts` ở trên.

## Thay đổi trong service nghiệp vụ

### Spec 007 — không có code sản xuất mới, chỉ xác nhận lại 1 cơ chế đã có

Kiểm tra qua `git show --stat fa4a16d` (commit chính của spec này): **toàn bộ thay đổi là tài liệu** (`spec.md`, `research.md`, `plan.md`...), không có file `.cs` nào bị sửa. `data-model.md` của chính spec này ghi thẳng: *"không có entity mới, chỉ thêm test coverage"*. Cơ chế `builder.Services.AddOpenApi()` mà bạn đã thấy ở [01-tong-quan-kien-truc.md](01-tong-quan-kien-truc.md) (Program.cs của BFF) thực ra được xây từ **spec 002** (`dfb8f2f feat(bff): enhance products endpoint with detailed OpenAPI responses`) — spec 007 chỉ audit lại rằng cơ chế đó đã đủ tốt, rồi bổ sung test "tolerant reader" (frontend chịu được khi backend trả thêm field lạ) hoàn toàn ở phía `frontend/`, không đụng gì tới C#. Đây là 1 ví dụ tốt cho việc **không phải spec nào cũng thêm code mới** — một số chỉ xác nhận và test lại cái đã có, và điều đó vẫn xứng đáng là 1 spec riêng vì nó cần điều tra thật để kết luận được.

### Spec 009 — không phải code, mà là 1 QUY TẮC ràng buộc cách viết code

`specs/009-retrofit-tdd-basket-order` cũng không sửa `.cs` nào (`c831df4`, `579bcd7`, `0098fe9` đều là tài liệu). Kết quả cụ thể của spec này là [`docs/engineering/test-first-commits.md`](../engineering/test-first-commits.md) — đáng đọc trực tiếp, nhưng tóm tắt phần quan trọng nhất: 1 audit `git log --follow` phát hiện các test cho `Basket.AddItem`/`Order.PlaceFrom` (đã xem ở Giai đoạn 2) đều nằm **chung 1 commit** với code, không hề có commit "test đỏ trước". Điều đó **không sai** quy tắc TDD (test không hề đến muộn), nhưng cũng không để lại bằng chứng cho người viết code sau này biết hình dạng commit được kỳ vọng. Quy tắc được chốt lại từ đó, áp dụng riêng cho `Basket.cs`/`Order.cs` (khu vực tính tiền — rủi ro cao nhất nếu sai):

| Được chấp nhận | Không được chấp nhận |
|---|---|
| Test đỏ trước, rồi code làm nó xanh — 2 commit riêng | Code trước, test viết sau (dù chỉ vài giờ) trong 1 commit khác |
| Test + code trong CÙNG 1 commit, có thể chứng minh test sẽ đỏ nếu thiếu code | — |

Lý do chỉ áp dụng cho riêng khu vực tính tiền của basket/order (không áp dụng toàn repo): đây là khu vực Principle III (test-first, "không thể thương lượng") của hiến pháp dự án (`.specify/memory/constitution.md`) quan tâm nhất, và mở rộng quy tắc ra toàn repo sẽ cần "sửa hiến pháp" — việc đó dành cho người duy trì nền tảng, không phải 1 spec đơn lẻ.

### Spec 010 — service nghiệp vụ chỉ đổi 1 chỗ: kế thừa fixture dùng chung

Business service không cần sửa gì nhiều ở bước này — `SqlServerFixture` riêng của từng service vẫn giữ nguyên (đã có từ trước), chỉ có phần Redis/RabbitMQ (chưa ai dùng) được chuẩn bị sẵn ở `shared/` như mục trên. Đây là bước "dọn hạ tầng trước khi cần", không phải thay đổi hành vi.

### Spec 011 — hợp đồng giữa 2 service CHƯA TỪNG gọi nhau qua HTTP

Commit: `514c6c1 Add contract tests for Orders and Products services`, `8ba963b feat: Implement consumer-driven contract tests across BFF/service boundaries`

Đây là phần thú vị nhất giai đoạn này, và dễ hiểu nhầm nhất nếu không đọc kỹ comment trong code. `services/baskets/src/Baskets.Api/Features/Checkout/BasketCheckedOutMapper.cs` (mới):
```csharp
public static class BasketCheckedOutMapper
{
    public static BasketCheckedOutV1 ToEvent(Basket basket, string tenantId, string correlationId, Guid eventId, DateTime occurredAtUtc)
    {
        // ...
        return new BasketCheckedOutV1(eventId, occurredAtUtc, basket.Id, basket.CustomerRef, tenantId, correlationId,
            [.. basket.LineItems.Select(line => new BasketLineItemV1(line.ProductId, line.Quantity, line.UnitPrice, line.LineTotal))],
            basket.Total);
    }
}
```
Comment đầu file nói thẳng: *"Chưa ai gọi hàm này cả, và đó là chủ ý. Checkout hiện tại vẫn là orchestration đồng bộ trong BFF (xem Giai đoạn 2); outbox và publisher thật sự sẽ gửi cái này là việc của SCRUM-31."* Vậy nếu chưa ai publish `BasketCheckedOutV1`, làm sao "test hợp đồng" được? Câu trả lời nằm ở `services/orders/tests/Orders.Api.ContractTests/BasketCheckedOutConsumerPactTests.cs` — đây gọi là **"message Pact"**, một biến thể của kỹ thuật contract-testing (thư viện **Pact**) áp dụng cho message/sự kiện thay vì cho HTTP request/response:

```csharp
var pact = Pact.V3("orders", "basketcheckedout", ...).WithMessageInteractions();

await pact.ExpectsToReceive("a basket checked out")
    .WithJsonContent(new {
        eventId = Match.Regex(...), tenantId = Match.Type("contoso"),
        items = Match.MinType(new { productId = ..., lineTotal = Match.Number(25.00m) }),
    })
    .Verify(...);
```
Cơ chế 2 chiều của Pact (áp dụng y hệt cho cả HTTP lẫn message):
1. **Bên tiêu thụ** (`orders`, ở trên) viết 1 bài test khai báo: "tôi chỉ cần các field này từ message, không quan tâm field khác" → sinh ra 1 file JSON hợp đồng (`pacts/orders-basketcheckedout.json`).
2. **Bên cung cấp** (`baskets`) chạy `OrdersProviderPactTests.cs`/`BasketCheckedOutProviderPactTests` verify **payload thật** nó tạo ra (qua chính `BasketCheckedOutMapper.ToEvent(...)` ở trên) khớp với đúng file hợp đồng đó — không cần khởi động broker, không cần service kia chạy thật.

Lợi ích: nếu sau này `baskets` đổi tên field `lineTotal` thành `total`, build của **chính `baskets`** sẽ đỏ ngay (vì payload nó tạo ra không còn khớp hợp đồng `orders` đã khai báo phụ thuộc) — phát hiện lỗi phá vỡ tương thích **trước khi merge**, không phải sau khi 2 service đã chạy thật cùng nhau và lỗi ở production. Đây cũng là lúc kỹ thuật "tolerant reader" xuất hiện lại: `OrdersProviderPactTests.cs` có 1 comment đáng nhớ — service `orders` trả về `tenantId` trong response HTTP thật, nhưng pact **không hề nhắc tới field đó**, và điều đó là **đúng như kỳ vọng**: BFF (bên tiêu thụ của route HTTP `/orders/{id}`) chưa từng đọc field `tenantId`, nên không khai báo phụ thuộc vào nó — nghĩa là `orders` có thể đổi hoặc xoá field đó bất cứ lúc nào **mà không phá vỡ hợp đồng**, vì hợp đồng chỉ ràng buộc những gì thực sự được dùng.

## Đi đâu tiếp theo

- [06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md](06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md) — cổng chất lượng CI/SonarQube, máy chủ định danh thật thay thế stub, và phân quyền deny-by-default trên mọi endpoint.
