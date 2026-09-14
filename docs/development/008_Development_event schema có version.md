# Bước 008: Thay đổi nghiệp vụ so với bước 007

## Phạm vi

Tài liệu này mô tả phần code được tạo bởi bước 008 so với trạng thái code sau bước 006. Bước 007
(`specs/007-bff-openapi-contracts`) không đổi bất kỳ backend production code nào — commit `fa4a16d`
chỉ thêm file trong `specs/` và `docs/spec-summary-vi/`, commit `8587dff` chỉ thêm test tolerant-reader
phía **frontend** (`BasketView.test.tsx`, `ProductList.test.tsx`, `DoubleSubmit.test.tsx`); không có
file nào dưới `services/` hay `shared/` bị đổi. Vì vậy bước 008 được so sánh trực tiếp với trạng thái
code của bước 006.

Ranh giới commit được xác định từ lịch sử Git:

- Mốc hoàn tất bước 006: commit `4024234`.
- Bước 007 (không đổi backend): commit `fa4a16d` (đặc tả), commit `8587dff` (test frontend).
- Đặc tả bước 008: commit `c549ec7`.
- Triển khai `shared/EventContracts`: commit `5233bd6`.

Theo `tasks.md` của bước 008: "No existing service (`Orders.Api`, `Baskets.Api`, `Bff.Api`) is touched
by any task — this feature is scoped entirely to `shared/EventContracts` and its test project." Do đó
toàn bộ nội dung bước 008 nằm trong một shared project mới; không có service nào được sửa. Nội dung
kiểm thử (`shared/EventContracts.UnitTests`) và test convention được bỏ qua theo đúng phạm vi đã áp
dụng ở các bước trước.

## 1. Package mới `shared/EventContracts`

Bước 008 tạo một shared project thuần dữ liệu: JSON Schema định nghĩa hợp đồng, C# record phản chiếu
đúng schema đó field-theo-field.

[shared/EventContracts/EventContracts.csproj](../../shared/EventContracts/EventContracts.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <!-- 008: không có PackageReference/FrameworkReference nào — đây là data contract thuần,
       không phải middleware, nên không cần kiểu ASP.NET Core hay validator runtime. -->

  <!-- 008: schema được đóng gói vào assembly (embedded resource), không chỉ nằm cạnh nó,
       để bất kỳ ai cần đọc schema đều dùng Assembly.GetManifestResourceStream thay vì
       đoán working directory. -->
  <ItemGroup>
    <EmbeddedResource Include="schemas/*.json">
      <LogicalName>%(Filename)%(Extension)</LogicalName>
    </EmbeddedResource>
  </ItemGroup>

</Project>
```

Project không tham chiếu `ServiceDefaults` hay `Tenancy`. Đây là điểm khác biệt so với mọi shared
project trước đó (001–006): `EventContracts` không phải hạ tầng dùng chung cho request pipeline, mà
là hợp đồng dữ liệu độc lập.

## 2. `OrderPlacedV1` — event đầu tiên có version tường minh

[shared/EventContracts/OrderPlacedV1.cs](../../shared/EventContracts/OrderPlacedV1.cs)

```csharp
// 008: version nằm trong chính tên type, không phải header hay envelope field.
public sealed record OrderPlacedV1(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("occurredAtUtc")] DateTime OccurredAtUtc,
    [property: JsonPropertyName("orderId")] Guid OrderId,

    // 008: bắt buộc ở tầng hợp đồng dù OrderResponse.TenantId hiện đang nullable —
    // hợp đồng được viết trước publisher sẽ thoả mãn nó.
    [property: JsonPropertyName("tenantId")] string TenantId,
    [property: JsonPropertyName("correlationId")] string CorrelationId,

    // 008: Total lấy từ Order.Total — do Orders service tính, không phải publisher tự tính.
    [property: JsonPropertyName("total")] decimal Total,
    [property: JsonPropertyName("lines")] IReadOnlyList<OrderLineV1> Lines);

// 008: OrderLineV1 không tự version riêng — đổi theo OrderPlacedV1,
// nằm dưới $defs của cùng schema file.
public sealed record OrderLineV1(
    [property: JsonPropertyName("productId")] Guid ProductId,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("unitPrice")] decimal UnitPrice);
```

Tên field JSON được ghim bằng `JsonPropertyName` thay vì để mặc định theo naming policy của
serializer, để wire format luôn khớp schema bất kể publisher cấu hình `System.Text.Json` ra sao.

## 3. `BasketCheckedOutV1` — event thứ hai, cùng quy ước version

[shared/EventContracts/BasketCheckedOutV1.cs](../../shared/EventContracts/BasketCheckedOutV1.cs)

```csharp
public sealed record BasketCheckedOutV1(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("occurredAtUtc")] DateTime OccurredAtUtc,
    [property: JsonPropertyName("basketId")] Guid BasketId,

    // 008: CustomerRef lấy từ Basket.CustomerRef — subject id đã resolve của caller.
    [property: JsonPropertyName("customerRef")] string CustomerRef,
    [property: JsonPropertyName("tenantId")] string TenantId,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("items")] IReadOnlyList<BasketLineItemV1> Items,
    [property: JsonPropertyName("total")] decimal Total);

public sealed record BasketLineItemV1(
    [property: JsonPropertyName("productId")] Guid ProductId,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("unitPrice")] decimal UnitPrice,

    // 008: LineTotal lấy từ BasketLineItemResponse.LineTotal — phép tính tiền ở lại
    // service sở hữu, event chỉ mang theo kết quả, consumer không tự tính lại.
    [property: JsonPropertyName("lineTotal")] decimal LineTotal);
```

## 4. Schema JSON là hợp đồng có hiệu lực, record C# chỉ phản chiếu

[shared/EventContracts/schemas/OrderPlaced.v1.schema.json](../../shared/EventContracts/schemas/OrderPlaced.v1.schema.json)

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "OrderPlacedV1",
  "description": "An order has been placed. Version 1. Immutable once committed: any change to this event's shape ships as OrderPlaced.v2.schema.json, never as an edit to this file.",
  "type": "object",

  "additionalProperties": false,
  "required": [
    "eventId", "occurredAtUtc", "orderId",
    "tenantId", "correlationId", "total", "lines"
  ]
}
```

`additionalProperties: false` ở top level nghĩa là publisher không thể lặng lẽ thêm field mà không
phát hành version mới. Đây là ràng buộc phía publisher; phía consumer lại dùng hành vi mặc định của
`System.Text.Json` (bỏ qua property lạ) để đọc được payload mới mà không sập — hai chiều được thiết kế
bất đối xứng có chủ đích.

## 5. Quy ước version hoá và bất biến

[shared/EventContracts/README.md](../../shared/EventContracts/README.md)

```text
# 008: quy ước version — version nằm trong tên, không phải trong envelope.
Type name:   {EventName}V{N}       -> OrderPlacedV1, BasketCheckedOutV1
Schema file: {EventName}.v{N}.schema.json
N bắt đầu từ 1 cho hình dạng đầu tiên được công bố của một event.

# 008: một phiên bản đã công bố là BẤT BIẾN.
Sau khi một schema file được commit, nó bị đóng băng. Đổi field, đổi type, hay chỉ sửa
một dòng description cũng bị xem là vi phạm như nhau — không có phân loại breaking/
non-breaking; sửa gì cũng phải bằng cách thêm version mới, không sửa file cũ tại chỗ.
```

Quyết định "bất kỳ chỉnh sửa nào sau khi công bố cũng là vi phạm" (thay vì phân loại breaking/
non-breaking) là lựa chọn có chủ đích: JSON Schema diffing là bài toán khó, còn quy tắc "sửa gì cũng
phải version mới" thì không có false negative cho trường hợp thật sự quan trọng — đổi mà không ai biết.
Cơ chế thực thi quy tắc này (hash nội dung schema đã commit) nằm trong test project, ngoài phạm vi của
tài liệu này.

## Tóm tắt 006 → 008

| Khu vực | Bước 006 | Bước 008 |
|---|---|---|
| Event contract | Chưa có | `shared/EventContracts`: `OrderPlacedV1`, `BasketCheckedOutV1` + JSON Schema tương ứng |
| Publisher | Không áp dụng | Chưa có — không service nào publish event |
| Consumer | Không áp dụng | Chưa có — không service nào consume event |
| Message broker | RabbitMQ chạy trong Compose (005) nhưng chưa ai kết nối | Vẫn chưa kết nối; 008 chỉ là tầng hợp đồng |
| Versioning | Không áp dụng | Quy ước `{Event}V{N}` + `{Event}.v{N}.schema.json`, bất biến sau khi công bố |
| Orders/Baskets/BFF | Order có `TenantId` (006) | Không đổi — 008 không chạm `Orders.Api`, `Baskets.Api`, `Bff.Api` |

**Kết luận:** bước 008 thiết lập tầng hợp đồng sự kiện có version cho `OrderPlaced` và
`BasketCheckedOut`, tách biệt hoàn toàn khỏi các service đang chạy. Không có publisher, consumer hay
broker nào được đấu nối; đây là bước chuẩn bị hợp đồng trước khi việc publish/consume thật sự được xây
(SCRUM-31, theo README của package).

## 6. Shared project trong bước 008

Bước 008 tạo shared project mới `shared/EventContracts`, bên cạnh `ServiceDefaults` (001) và
`Tenancy` (003) đã có từ trước. Khác với hai project đó, `EventContracts` không được bất kỳ service
nào tham chiếu.

[shared/EventContracts/README.md](../../shared/EventContracts/README.md)

```text
# 008: xác nhận rõ ràng — chưa service nào dùng package này.
"Nothing here is referenced by a service yet. No broker exists...
This library is the schema half of that work, delivered first so the
contract is reviewed before any publisher is written."
```

[Ecommerce.slnx](../../Ecommerce.slnx)

```text
<!-- 008: EventContracts và EventContracts.UnitTests được thêm vào solution,
     độc lập với các service project đã có. -->
```

Tăng trưởng của shared layer trong bước 008 khác về bản chất so với 003 (nơi `Tenancy` được toàn bộ
service dùng ngay lập tức): `EventContracts` tồn tại như một hợp đồng chờ sẵn, chưa có điểm tích hợp
nào trong hệ thống đang chạy.
