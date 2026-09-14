# Bước 011: Thay đổi nghiệp vụ so với bước 010

## Phạm vi

Tài liệu này mô tả phần code được tạo bởi bước 011 so với trạng thái code sau bước 006. Các bước xen
giữa không đổi business/devops code: 007 và 009 không đổi bất kỳ file nào dưới `services/` hay
`shared/` (đã xác nhận khi review từng bước); 010 chỉ thêm hạ tầng test
(`shared/IntegrationTestSupport`). Bước 008 có thêm shared library `shared/EventContracts`, nhưng
chưa service nào tham chiếu nó. **Bước 011 là bước đầu tiên một service thật sự dùng
`EventContracts`.**

Ranh giới commit được xác định từ lịch sử Git:

- Mốc hoàn tất bước 010: commit `cba4449`.
- Đặc tả bước 011 và phần lớn contract-test scaffolding (BFF/Baskets/Orders): commit `8ba963b`.
- Baskets.Api tham chiếu `EventContracts`, thêm `BasketCheckedOutMapper`, cập nhật Dockerfile, thêm
  contract test cho Orders/Products — mốc hoàn tất bước 011: commit `514c6c1`.

Theo `git diff --stat` trên toàn bộ range, chỉ 3 file ngoài `*.ContractTests`/`*.Tests` bị đổi:
`Baskets.Api.csproj`, `Baskets.Api/Dockerfile`, và `BasketCheckedOutMapper.cs` (file mới). Toàn bộ
phần còn lại của bước 011 (các project `*.Api.ContractTests`, `PactProviderHost.cs`,
`ProviderStateStartupFilter.cs`, `SqlServerFixture.cs`...) là hạ tầng kiểm thử hợp đồng (PactNet) —
nội dung kiểm thử được bỏ qua theo đúng phạm vi đã áp dụng ở các bước trước.

## 1. Baskets.Api lần đầu tham chiếu `shared/EventContracts`

[services/baskets/src/Baskets.Api/Baskets.Api.csproj](../../services/baskets/src/Baskets.Api/Baskets.Api.csproj)

```xml
<ItemGroup>
    <ProjectReference Include="..\..\..\..\shared\ServiceDefaults\ServiceDefaults.csproj" />
    <ProjectReference Include="..\..\..\..\shared\Tenancy\Tenancy.csproj" />

    <!-- 011: hợp đồng event đã công bố (008) mà service này dựng payload checkout theo đó.
         Tham chiếu, không copy: một service tự khai báo lại hình dạng sẽ có thể trôi khỏi
         schema mà consumer đang verify theo. -->
    <ProjectReference Include="..\..\..\..\shared\EventContracts\EventContracts.csproj" />
</ItemGroup>
```

[services/baskets/src/Baskets.Api/Dockerfile](../../services/baskets/src/Baskets.Api/Dockerfile)

```dockerfile
COPY shared/ServiceDefaults/ServiceDefaults.csproj shared/ServiceDefaults/
COPY shared/Tenancy/Tenancy.csproj shared/Tenancy/
# 011: tham chiếu từ 011-consumer-contract-tests, cho payload BasketCheckedOut
# mà service này dựng lúc checkout.
COPY shared/EventContracts/EventContracts.csproj shared/EventContracts/
COPY services/baskets/src/Baskets.Api/Baskets.Api.csproj services/baskets/src/Baskets.Api/
RUN dotnet restore services/baskets/src/Baskets.Api/Baskets.Api.csproj

COPY shared/ServiceDefaults/ shared/ServiceDefaults/
COPY shared/Tenancy/ shared/Tenancy/
COPY shared/EventContracts/ shared/EventContracts/
COPY services/baskets/src/Baskets.Api/ services/baskets/src/Baskets.Api/
```

Chỉ Baskets.Api đổi theo cách này. Orders và Products (provider phía HTTP contract test) và BFF
(consumer phía HTTP contract test) không thêm reference tới `EventContracts` ở bước 011 — cặp event
thí điểm (`BasketCheckedOut`) chỉ có baskets là producer.

## 2. `BasketCheckedOutMapper` — dựng payload event, chưa có ai gọi

[services/baskets/src/Baskets.Api/Features/Checkout/BasketCheckedOutMapper.cs](../../services/baskets/src/Baskets.Api/Features/Checkout/BasketCheckedOutMapper.cs)

```csharp
// 011: hàm thuần (pure function) trên Basket, không phải method của entity — các định danh
// dưới đây thuộc về message, không thuộc về basket; đưa chúng vào domain model sẽ nhét
// transport concern vào bên trong domain.
public static class BasketCheckedOutMapper
{
    public static BasketCheckedOutV1 ToEvent(
        Basket basket,
        string tenantId,
        string correlationId,
        Guid eventId,
        DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(basket);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        return new BasketCheckedOutV1(
            eventId,
            occurredAtUtc,
            basket.Id,
            basket.CustomerRef,
            tenantId,
            correlationId,
            [.. basket.LineItems
                .OrderBy(line => line.ProductId)
                .Select(line => new BasketLineItemV1(
                    line.ProductId,
                    line.Quantity,
                    line.UnitPrice,

                    // 011: carried từ Basket, không tính lại — phép tính tiền ở lại
                    // service sở hữu (004 plan.md).
                    line.LineTotal))],
            basket.Total);
    }
}
```

Theo chính comment trong file: **"Nothing calls this yet, and that is deliberate."** Checkout ở bước
011 vẫn là orchestration đồng bộ của BFF (ADR-0011, không đổi từ bước 004). Hàm này tồn tại để:

1. Cho `BasketCheckedOutProviderPactTests` (test) verify được payload thật đối chiếu với schema đã
   công bố ở 008 — mà không cần broker.
2. Cho công việc publish/consume thật sau này (bước 024 mới làm outbox cho `OrderPlaced`, chưa đụng
   `BasketCheckedOut`) có sẵn một contract đã được provider tự verify, thay vì định nghĩa contract sau
   khi đã viết code publish.

`tenantId` là tham số bắt buộc dù chưa response nào của Basket lộ nó ra — lý do nêu trong chính
docstring của hàm: "an event nobody can attribute is one nobody can act on" (constitution Principle
V). `eventId` và `occurredAtUtc` được truyền vào từ caller thay vì tự sinh bên trong hàm, để một lần
publish lại (retry) của cùng một checkout mang đúng cùng `eventId`.

## Tóm tắt 008 → 011

| Khu vực | Bước 008 | Bước 011 |
|---|---|---|
| `shared/EventContracts` | Tồn tại, chưa service nào dùng | Baskets.Api tham chiếu qua `ProjectReference` |
| Payload construction | Chưa có | `BasketCheckedOutMapper.ToEvent` — pure function, có trong Baskets.Api |
| Publisher thật | Chưa có | Vẫn chưa có — mapper tồn tại nhưng chưa được gọi ở đâu |
| Checkout flow | Đồng bộ qua BFF (từ 004/006, ADR-0011) | Không đổi — vẫn đồng bộ |
| Contract verify | Chưa có | PactNet: 3 boundary HTTP (BFF↔products/baskets/orders) + 1 cặp event thí điểm (baskets→orders) |
| Docker build Baskets | Không COPY `EventContracts` | COPY thêm `shared/EventContracts` trước khi restore/publish |

**Kết luận:** bước 011 không thêm nghiệp vụ mua hàng mới và không nối publisher/consumer thật nào. Nó
làm hai việc: (1) đưa hạ tầng kiểm thử hợp đồng PactNet vào 3 boundary HTTP và 1 boundary event thí
điểm, và (2) cho Baskets.Api dựng sẵn payload `BasketCheckedOutV1` thật — bước chuẩn bị để contract
test có nội dung thật để verify, chưa phải bước nối message broker.

## 3. Shared project trong bước 011

Bước 011 không tạo shared project mới. Nó là lần đầu tiên `shared/EventContracts` (tạo ở bước 008)
được một service tham chiếu trong code không phải test — trước đó package này chỉ tồn tại như hợp
đồng chờ sẵn (README ghi rõ "Nothing here is referenced by a service yet").

[services/baskets/src/Baskets.Api/Baskets.Api.csproj](../../services/baskets/src/Baskets.Api/Baskets.Api.csproj)

```xml
<!-- 011: Baskets giờ tham chiếu cả ba shared project: hạ tầng chung (ServiceDefaults),
     tenant boundary (Tenancy), và hợp đồng event (EventContracts). -->
<ProjectReference Include="..\..\..\..\shared\ServiceDefaults\ServiceDefaults.csproj" />
<ProjectReference Include="..\..\..\..\shared\Tenancy\Tenancy.csproj" />
<ProjectReference Include="..\..\..\..\shared\EventContracts\EventContracts.csproj" />
```

Orders, Products, Parties và BFF chưa tham chiếu `EventContracts` ở bước này — chỉ Baskets, đúng vai
producer của cặp event thí điểm `BasketCheckedOut`. Việc Orders tham chiếu `EventContracts` (cho
`OrderPlaced`) là công việc của bước 024, ngoài phạm vi 011.
