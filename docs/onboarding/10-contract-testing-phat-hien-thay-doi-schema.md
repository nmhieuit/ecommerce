# 10 — Contract testing: phát hiện thay đổi schema ở đâu, và chuyện gì xảy ra nếu nó lọt qua

> Đọc [05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md § Spec 011](05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md#spec-011--hợp-đồng-giữa-2-service-chưa-từng-gọi-nhau-qua-http) và [07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md](07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md) trước — tài liệu này không lặp lại cơ chế Pact cơ bản, chỉ trả lời chính xác 1 câu hỏi: nếu 1 service downstream âm thầm đổi schema, ai/cái gì phát hiện ra, và phát hiện được TỚI ĐÂU thì dừng.

## 1. "Downstream đổi schema" được phát hiện Ở ĐÂU — 1 hiểu lầm dễ gặp cần làm rõ trước

Câu trả lời chính xác, dễ hiểu nhầm nhất trong toàn tài liệu này: **contract testing hiện tại phát hiện lỗi trong CHÍNH BUILD CỦA SERVICE ĐỔI SCHEMA (downstream/provider) — không phải trong service đang gọi nó (consumer) lúc đang chạy.**

Ví dụ cụ thể, đúng luồng đã thấy ở [05](05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md): giả sử `orders` đổi response `/orders/{id}`, đổi tên field `total` thành `orderTotal`.
1. `bff` (consumer) đã viết sẵn kỳ vọng của nó vào `pacts/bff-orders.json` (từ trước, khi feature còn đúng).
2. Khi ai đó sửa `orders` và mở PR, **chính build CI của `orders`** chạy [`OrdersProviderPactTests.cs`](../../services/orders/tests/Orders.Api.ContractTests/OrdersProviderPactTests.cs):
   ```csharp
   verifier
       .WithHttpEndpoint(provider.BaseUri)
       .WithFileSource(new FileInfo(Path.Combine(PactPaths.Directory, "bff-orders.json")))
       .WithProviderStateUrl(provider.ProviderStateUri)
       .Verify();
   ```
   Test này khởi động `orders` thật (trong bộ nhớ, kiểu `WebApplicationFactory` đã quen ở [08](08-chien-luoc-test-unit-integration-va-playwright.md)), gọi `/orders/{id}` thật, rồi so response THẬT với file `bff-orders.json` — field `total` mà pact khai báo giờ không còn tồn tại trong response → **`OrdersProviderPactTests` FAIL ngay trong build của `orders`**.
3. `bff` — service đang thực sự "chạy" và gọi `orders` — **không tham gia bước này chút nào**. Nó không tự động re-verify pact của mình lúc runtime; nó chỉ *viết ra* kỳ vọng 1 lần (`pacts/bff-orders.json`, qua `Bff.Api.ContractTests`), rồi từ đó công việc "giữ đúng lời hứa" hoàn toàn thuộc trách nhiệm của bên `orders`.

**Nếu ý bạn hỏi là 1 cơ chế khác — `bff` đang chạy THẬT tự phát hiện ra `orders` vừa đổi schema mà không cần chờ `orders` tự chạy provider test của chính nó — cơ chế đó KHÔNG tồn tại trong repo hiện tại.** Đây sẽ là 1 dạng "runtime schema validation" (ví dụ: BFF tự kiểm tra response nhận được có đúng shape mong đợi trước khi dùng), và không có bằng chứng nào trong `DownstreamClients/` cho thấy điều đó được làm — xem Phần 3 để biết chính xác điều gì xảy ra khi không có lớp bảo vệ đó.

## 2. Cơ chế này có ĐANG THỰC SỰ chặn được gì tính đến hiện tại không?

Đối chiếu trực tiếp với phát hiện ở [07](07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md), đọc lại [`Jenkinsfile`](../../Jenkinsfile) **tại đúng thời điểm viết tài liệu này** (dòng 87, chưa đổi kể từ lần đọc ở tài liệu 07):
```groovy
CI_FAST_ITERATION = 'true'
```
Stage `contract tests` vẫn nằm sau điều kiện `when { environment name: 'CI_FAST_ITERATION', value: 'false' }` — nghĩa là **hiện tại `PactVerifier` không hề chạy trong CI thật**, bất kể `orders` (hay bất kỳ service nào) đổi schema gì. Kết hợp với việc bạn đã xác nhận trước đó ([07](07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md#6-thất-bại-chặn-gì--phần-cần-xác-nhận-không-suy-đoán)) rằng 5 required status check đã bị gỡ khỏi branch protection: **kết luận thực tế hiện tại là 1 PR đổi schema breaking ở `orders` HOÀN TOÀN CÓ THỂ merge được mà `OrdersProviderPactTests` chưa từng chạy 1 lần nào.** Cơ chế bảo vệ vẫn tồn tại nguyên vẹn trong code (Phần 1), nhưng không được nối vào cổng chặn merge tại thời điểm này.

Đây là trạng thái tôi đọc trực tiếp từ file ngay lúc viết tài liệu — nếu bạn đã bật lại `CI_FAST_ITERATION=false` hoặc khôi phục required status check sau thời điểm này, thông tin trên đã lỗi thời và cần xác nhận lại, tôi không có cách tự động biết được thay đổi đó.

## 3. Nếu lỗi "lọt" qua được — chuyện gì xảy ra ở RUNTIME khi BFF thật sự gọi phải response đã đổi schema?

Đây là phần được kiểm chứng **bằng cách chạy thử thật** (viết 1 chương trình console tạm dùng đúng `System.Text.Json` với đúng cấu hình mặc định mà [`ProductsApiClient.cs`](../../services/bff/src/Bff.Api/DownstreamClients/ProductsApiClient.cs) đang dùng — `httpClient.GetFromJsonAsync<T>(...)` không truyền `JsonSerializerOptions` tường minh nghĩa là .NET tự dùng `JsonSerializerDefaults.Web`, tức camelCase + so khớp tên không phân biệt hoa/thường), rồi xoá đi ngay sau khi ghi lại kết quả — không phải suy đoán từ tài liệu .NET chung chung. Dùng lại đúng khuôn `ProductResource(Guid Id, string Name, decimal Price)` (chính là record thật trong `ProductsApiClient.cs`):

| Kịch bản downstream đổi | Kết quả THẬT quan sát được | Có exception không? |
|---|---|---|
| Không đổi gì (baseline) | `Id`, `Name`, `Price` đúng | Không |
| **Đổi tên field** `price` → `unitPrice` | `Price = 0` | **KHÔNG — âm thầm** |
| **Xoá hẳn field** `name` | `Name = null` | **KHÔNG — âm thầm** |
| Đổi kiểu `price`: số → chuỗi `"12.50"` | `Price = 12.50` (vẫn đúng) | Không (bộ chuyển đổi `decimal` của .NET đủ khoan dung để đọc số dạng chuỗi) |
| Đổi kiểu `id`: chuỗi GUID → số `12345` | — | **`System.Text.Json.JsonException`: "The JSON value could not be converted to ProductResource. Path: $.id"** |

**Đây là phát hiện quan trọng nhất tài liệu này:** 2 trong 4 kiểu thay đổi schema phổ biến nhất (đổi tên field, xoá field) **không hề ném lỗi** — BFF nhận được `Price = 0` hoặc `Name = null` một cách hoàn toàn im lặng, trả thẳng xuống cho SPA như thể đó là dữ liệu thật. Không có exception nghĩa là [`DownstreamCall.cs`](../../services/bff/src/Bff.Api/DownstreamClients/DownstreamCall.cs) (đã xem ở [09](09-bff-dependency-downstream-va-trien-khai.md)) không có gì để bắt — request vẫn trả về `200 OK` với dữ liệu sai.

Chỉ khi kiểu dữ liệu đổi tới mức **hoàn toàn không tương thích cấu trúc** (số thay cho chuỗi GUID có cấu trúc) mới ném `JsonException` — nhưng đến đây lại lộ ra 1 lỗ hổng thứ hai: xem lại danh sách `IsDownstreamFailure` trong `DownstreamCall.cs` (đã trích ở [09](09-bff-dependency-downstream-va-trien-khai.md#khi-downstream-lỗi-1-chỗ-xử-lý-duy-nhất-không-route-nào-tự-viết-trycatch-riêng)):
```csharp
private static bool IsDownstreamFailure(Exception exception) => exception is
    HttpRequestException or TimeoutRejectedException or BrokenCircuitException
    or TaskCanceledException or OperationCanceledException;
```
**`JsonException` không có trong danh sách này.** Nghĩa là ngay cả trong trường hợp HIẾM HOI mà .NET thật sự ném lỗi, lỗi đó vẫn **không** được `DownstreamCall.ExecuteAsync` bọc thành `DownstreamServiceException` — nó sẽ đi thẳng qua, không khớp điều kiện `catch (Exception exception) when (IsDownstreamFailure(exception))`, và trở thành 1 exception chưa xử lý → `500` chung chung, không phải `502`/`504` có cấu trúc rõ ràng như [09](09-bff-dependency-downstream-va-trien-khai.md) đã mô tả cho lỗi mạng. Đây khớp đúng chủ đích ghi trong comment gốc của `DownstreamCall.cs` ("chỉ lỗi CỦA dependency mới được bắt, để 1 bug thật trong BFF không bị báo nhầm là lỗi của bên khác") — nhưng hệ quả là 1 schema đổi kiểu dữ liệu không tương thích SẼ hiện ra như "BFF có bug", không phải "downstream đổi hợp đồng", trong log/monitoring.

## 4. "Trước khi deploy lên stage" — cần gì để thật sự chặn kịp thời điểm đó?

Thứ tự 5 stage thật trong [`Jenkinsfile`](../../Jenkinsfile): `sonarqube: begin analysis` → `build` → `unit tests` → `integration tests` → `contract tests` → `sonarqube quality gate`. Stage `contract tests` chạy `scripts/ci/run-dotnet-tests.sh contract` — script này tự tìm mọi `*ContractTests.csproj` (đã xác nhận cơ chế phát hiện theo tên ở [07](07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md)) và chạy `dotnet test`, tức là chạy đúng `OrdersProviderPactTests`/tương tự ở mọi service.

Để bảo đảm KHÔNG THỂ deploy lên stage khi có breaking change, dựa trên bằng chứng đã có (Phần 2), cần **đồng thời cả 3 điều kiện** sau — thiếu 1 trong 3 là hổng:
1. `CI_FAST_ITERATION` phải là `'false'` (hoặc dòng đó bị xoá khỏi `Jenkinsfile`) — nếu không, stage `contract tests` không chạy dòng nào cả.
2. `ci/contract-tests` phải nằm trong `required_status_checks.contexts` của branch protection thật trên GitHub ([`scripts/ci/setup-branch-protection.sh`](../../scripts/ci/setup-branch-protection.sh) định nghĩa đúng điều này, nhưng — theo bạn xác nhận ở [07](07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md) — hiện đã bị gỡ khỏi cấu hình sống).
3. Route/sự kiện bị đổi phải nằm trong 4 "ranh giới" (`Boundary`) mà [`ContractCoverageScanner.ExpectedBoundaries`](../../tests/ContractCoverageTests/ContractCoverageScanner.cs) đã liệt kê (đã xem ở [07](07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md#3-testscontractcoveragetests--4-ranh-giới-hợp-đồng-pact-phải-luôn-đủ-cả-2-phía)) — 1 route BFF/service KHÔNG nằm trong 4 ranh giới đó (ví dụ `parties`, hiện không có pact nào theo danh sách đã liệt kê) thì dù cả 2 điều kiện trên đúng, vẫn **không có pact nào tồn tại để phát hiện** thay đổi schema của nó — không phải vì cơ chế hỏng, mà vì chưa từng có ai viết hợp đồng cho ranh giới đó.

Không tìm thấy bằng chứng nào khác trong repo về 1 điều kiện thứ 4 cần thiết — nếu bạn biết có ràng buộc nào nữa (ví dụ 1 quy trình phê duyệt thủ công trước khi deploy stage nằm ngoài Jenkinsfile), tôi không có cách xác nhận điều đó chỉ từ code.

## Phần 5 — "Ép" upstream dùng version mới rồi bỏ hẳn version cũ: cái gì đã có, cái gì chưa

> Quan trọng: `V1`/`V2` mà [05](05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md) nhắc tới (`OrderPlacedV1`, `BasketCheckedOutV1`) là versioning cho **event bất đồng bộ**, KHÔNG PHẢI cho response HTTP của BFF/4 service — 2 thứ này KHÔNG được lẫn vào nhau trong repo. Đã kiểm tra trực tiếp bằng cách quét toàn bộ `services/` và `shared/`: **không có package `Asp.Versioning`, không có route nào dạng `/v1/...`/`/v2/...`**. Bề mặt HTTP (route `/products`, `/orders/{id}`... mà bạn đã thấy xuyên suốt tài liệu 02-09) hoàn toàn không có versioning.

### 5.1 Quy tắc bắt buộc đã được viết ra — áp dụng cho cả 2, nhưng chỉ 1 bên có cơ chế thật

`.specify/memory/constitution.md`, Principle II ("Contract-First Integration"), nguyên văn:
> *"Breaking changes to any published contract require a new explicit version plus a documented deprecation window, and the previous version MUST keep working for the duration of that window."*

Theo câu chữ, quy tắc này áp dụng cho **cả** HTTP API (OpenAPI) lẫn event contract — nhưng như sẽ thấy ở 5.2-5.3, chỉ có event contract thực sự có cơ chế thi hành quy tắc này; HTTP API thì chưa.

### 5.2 Cho event contract — quy tắc ĐÃ được thiết kế đầy đủ, trích thẳng từ `shared/EventContracts/README.md`

Trả lời gần như trọn vẹn chính câu hỏi bạn đặt ra, chỉ khác là cho event chứ không phải HTTP:
> *"A superseded version does not disappear when its replacement ships. `V{N}` stays defined, compiled, and covered by its own tests until **both** of these hold: 1. `V{N+1}` has shipped, and 2. no known consumer still depends on `V{N}` — confirmed through the consumer-driven contract tests... There is deliberately no fixed day count. A number picked today would be picked with no consumers to measure against, and would either be ignored or enforced arbitrarily; 'confirmed to have no consumers' is the condition that actually makes removal safe."*

3 điểm đáng nhớ trong thiết kế này:
1. **Không có ngày hết hạn cố định** — điều kiện bỏ version cũ là "xác nhận không còn ai phụ thuộc", đo bằng chính contract test (Pact), không phải đếm ngày trên lịch. Đây là câu trả lời trực tiếp cho phần "sau 1 ngày nào đó" trong câu hỏi của bạn: repo này **cố tình từ chối** kiểu "1 ngày nào đó" — vì 1 con số chọn ra lúc chưa biết ai đang dùng version cũ sẽ hoặc bị phá vỡ (ép người còn phụ thuộc) hoặc vô nghĩa (không ai theo dõi).
2. **`V{N}` không thể bị sửa ngầm** — [`SchemaImmutabilityTests.cs`](../../shared/EventContracts.UnitTests/SchemaImmutabilityTests.cs) hash SHA-256 từng file schema đã publish, fail nếu nội dung đổi dù chỉ 1 ký tự. Muốn đổi shape bắt buộc phải tạo file `V{N+1}` mới, để nguyên `V{N}` — đây chính là cơ chế kỹ thuật khiến "phải giữ version cũ chạy" không phải lời hứa suông mà là 1 test tự động chặn build nếu ai đó phá lời hứa đó.
3. **Chưa từng được thực thi thật** — README ghi rõ: *"Only `V1` of each event exists today, so nothing has been superseded and no removal has happened."* Toàn bộ cơ chế 5.2 này là thiết kế đã sẵn sàng, nhưng chưa có 1 lần "ép version mới, bỏ version cũ" nào thực sự xảy ra trong lịch sử repo để kiểm chứng nó hoạt động đúng trên thực tế.

### 5.3 Cho HTTP API — khoảng trống thật, không suy đoán ra quy trình chưa tồn tại

Đối chiếu Principle II với những gì đã xác nhận từ đầu tài liệu này: HTTP API **chưa có** bất kỳ mảnh nào của cơ chế 5.2 —
- Không có cách đánh dấu 1 route là "sắp deprecated" (không header, không attribute, không mục ghi chú nào trong `*Endpoints.cs`).
- Không có gì đo/theo dõi "còn ai đang gọi route cũ hay chưa" — khác hẳn event, nơi Pact đóng đúng vai trò "đo consumer còn phụ thuộc hay không".
- Không có nơi nào ghi ngày hết hạn của 1 version HTTP, vì bản thân khái niệm "version của 1 route HTTP" chưa tồn tại.

Đây là khoảng trống Principle II **yêu cầu** phải có nhưng repo **chưa xây** cho bề mặt HTTP — không phải do quên, chỉ đơn giản là chưa có breaking change HTTP nào từng buộc đội ngũ phải giải quyết nó.

### 5.4 Gợi ý theo thực hành ngành (khuyến nghị — không phải đã có trong repo)

Nếu phải tự thiết kế cho HTTP API, cách tự nhiên nhất là **mượn lại đúng triết lý đã chứng minh ở 5.2 cho event**, thay vì nghĩ ra quy trình mới:
- **Điều kiện bỏ version cũ = "không còn ai phụ thuộc", đo bằng chính Pact** — không phải ngày cố định: nếu 1 route HTTP có versioning (`/v2/orders`), chỉ xoá `/v1/orders` khi **mọi** file `pacts/*.json` từng tham chiếu shape v1 đã được cập nhật sang v2 và `ContractCoverageScanner` (đã xem ở [07](07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md)) xác nhận không còn ranh giới nào còn khai báo phụ thuộc v1.
- **Header `Deprecation`/`Sunset`** (RFC 8594) trên response của route sắp bỏ — báo hiệu tường minh ngay trong chính response, không cần consumer phải đọc changelog riêng.
- **Đếm số request thật vào route cũ** (qua log có sẵn/OpenTelemetry đã có ở [03](03-giai-doan-1-nen-tang-dich-vu-va-routing.md)) làm bằng chứng thực nghiệm bổ sung cho Pact — Pact chứng minh "còn khai báo phụ thuộc", số liệu request thật chứng minh "còn ai thực sự GỌI" (2 tín hiệu này có thể lệch nhau nếu 1 consumer có pact nhưng đã ngừng gọi thật).

## Phần 6 — Các file trong `pacts/`, và điều gì đổi nếu 2 service ở 2 repo khác nhau

### 6.A Giải thích các file trong `pacts/`

[`pacts/`](../../pacts) ở gốc repo có đúng 5 file: [`README.md`](../../pacts/README.md) (bảng 4 ranh giới, đã trích ở [07](07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md)) và 4 file JSON. Với MỖI file, có đúng 2 dự án test .cs liên quan — 1 dự án **VIẾT** ra nó (consumer, khai báo kỳ vọng), 1 dự án **ĐỌC** nó (provider, verify response thật):

| File JSON | Ranh giới | Test VIẾT (consumer) | Test ĐỌC (provider) |
|---|---|---|---|
| `bff-products.json` | BFF↔products | `Bff.Api.ContractTests/ProductsConsumerPactTests.cs` | `Products.Api.ContractTests/ProductsProviderPactTests.cs` |
| `bff-baskets.json` | BFF↔baskets | `Bff.Api.ContractTests/BasketsConsumerPactTests.cs` | `Baskets.Api.ContractTests/BasketsProviderPactTests.cs` |
| `bff-orders.json` | BFF↔orders | `Bff.Api.ContractTests/OrdersConsumerPactTests.cs` | `Orders.Api.ContractTests/OrdersProviderPactTests.cs` |
| `orders-basketcheckedout.json` | BasketCheckedOut (event) | `Orders.Api.ContractTests/BasketCheckedOutConsumerPactTests.cs` | `Baskets.Api.ContractTests/BasketCheckedOutProviderPactTests.cs` |

**Cơ chế đọc/ghi dùng chung 1 thư mục** — [`PactPaths.cs`](../../services/orders/tests/Orders.Api.ContractTests/PactPaths.cs) (mỗi service 1 bản, cùng nội dung, cùng kỹ thuật "đi ngược tìm `Ecommerce.slnx`" đã thấy ở mọi scanner trong `tests/`, [07](07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md)):
```csharp
internal static class PactPaths
{
    public static string Directory { get; } = Locate();
}
```
Cả 4 test VIẾT lẫn 4 test ĐỌC đều gọi `PactPaths.Directory` — tìm ra **cùng 1 đường dẫn tuyệt đối**, trỏ vào **cùng 1 file trên cùng 1 ổ đĩa**, chỉ "miễn phí" được vì mọi service nằm trong **cùng 1 lần `git checkout`**, cùng 1 monorepo (chi tiết đa-repo ở 6.B).

#### `bff-products.json` — [`ProductsConsumerPactTests.cs`](../../services/bff/tests/Bff.Api.ContractTests/ProductsConsumerPactTests.cs) viết, [`ProductsProviderPactTests.cs`](../../services/products/tests/Products.Api.ContractTests/ProductsProviderPactTests.cs) đọc

Phía **VIẾT** — khai báo kỳ vọng qua chính `ProductsApiClient` thật (không tự tay soạn request), rồi assert trên **object đã deserialize**, không phải JSON thô:
```csharp
pact
    .UponReceiving("a request for the catalog")
        .Given("the catalog contains at least one product")
        .WithRequest(HttpMethod.Get, "/products")
    .WillRespond()
        .WithStatus(HttpStatusCode.OK)
        .WithJsonBody(Match.MinType(new { id = Match.Type(...), name = Match.Type(...), price = Match.Number(12.50m) }, 1));

await pact.VerifyAsync(async context =>
{
    var client = new ProductsApiClient(BffPact.CreateRelayingClient(context.MockServerUri));
    var products = await client.GetProductsAsync(CancellationToken.None);   // gọi ĐÚNG client thật BFF dùng
    Assert.Equal(12.50m, Assert.Single(products).Price);
});
```
Comment gốc giải thích lý do đi qua client thật: *"đây chính là ý nghĩa của 'consumer-driven pact' — thứ được ghi lại là thứ client THẬT gửi đi và THẬT giải mã được, nên 1 field BFF không bao giờ đọc không thể lọt vào hợp đồng và trói buộc producer vô cớ."* Lệnh `pact.VerifyAsync(...)` chạy xong là lúc `bff-products.json` được ghi ra `pacts/`.

Phía **ĐỌC** — [`ProductsProviderPactTests.cs`](../../services/products/tests/Products.Api.ContractTests/ProductsProviderPactTests.cs):
```csharp
verifier
    .WithHttpEndpoint(provider.BaseUri)   // Products.Api THẬT, khởi động trong bộ nhớ
    .WithFileSource(new FileInfo(Path.Combine(PactPaths.Directory, "bff-products.json")))
    .WithProviderStateUrl(provider.ProviderStateUri)
    .Verify();
```
Chạy trong build CI của **`products`** — đây là nơi upstream (`bff`) và downstream (`products`) "gặp nhau" đúng nghĩa: `products` không cần biết `bff` tồn tại lúc code, chỉ cần đọc đúng file này khi build.

#### `bff-baskets.json` — [`BasketsConsumerPactTests.cs`](../../services/bff/tests/Bff.Api.ContractTests/BasketsConsumerPactTests.cs) viết, [`BasketsProviderPactTests.cs`](../../services/baskets/tests/Baskets.Api.ContractTests/BasketsProviderPactTests.cs) đọc

File này đặc biệt: **1 bài test khai báo cả 4 tương tác** (đọc giỏ, thêm hàng, xoá giỏ có hàng → `204`, xoá giỏ rỗng → `409`) trong cùng 1 `[Fact]`, không tách 4 test riêng — comment gốc giải thích lý do: *"pact writer GỘP vào file đã có thay vì THAY THẾ nó, nên nếu tách theo test method, 1 tương tác không còn tồn tại nữa vẫn có thể sống sót trong file đã commit — đúng thứ tính năng này sinh ra để làm cho không thể xảy ra."*
```csharp
pact.UponReceiving("a request to clear a basket that is already empty")
    .Given("an empty basket exists for the caller", CallerState(ShopperWithEmptyBasket))
    .WithRequest(HttpMethod.Post, "/baskets/current/clear")
    .WillRespond()
        .WithStatus(HttpStatusCode.Conflict)   // 409 — chính status này LÀ hợp đồng, BFF dựa vào đúng mã này để biết "giỏ đã rỗng"
        .WithJsonBody(new { error = Match.Type("The basket is already empty.") });
```
Phía **ĐỌC** ([`BasketsProviderPactTests.cs`](../../services/baskets/tests/Baskets.Api.ContractTests/BasketsProviderPactTests.cs)) cùng khuôn hệt `ProductsProviderPactTests` ở trên — chỉ đổi tên file: `.WithFileSource(..., "bff-baskets.json")`, chạy trong build của `baskets`.

#### `bff-orders.json` — [`OrdersConsumerPactTests.cs`](../../services/bff/tests/Bff.Api.ContractTests/OrdersConsumerPactTests.cs) viết, [`OrdersProviderPactTests.cs`](../../services/orders/tests/Orders.Api.ContractTests/OrdersProviderPactTests.cs) đọc

Đã trích phía ĐỌC đầy đủ ở Phần 1. Phía **VIẾT** có 1 chi tiết đáng nhớ, đúng ví dụ thật của "tolerant reader" đã bàn ở Phần 3 — comment gốc:
> *"`tenantId` bị cố tình bỏ khỏi cả 2 response mong đợi dù `orders` service THẬT SỰ trả về field đó. `OrderResource` (record phía BFF) không đọc field này — 1 pact có nhắc tới nó sẽ ngăn `orders` xoá field mà chẳng ai dùng, đúng thứ quy tắc tolerant-reader FR-007 tồn tại để ngăn."*

Nghĩa là: `orders` được **tự do đổi hoặc xoá** `tenantId` bất cứ lúc nào mà không phá `bff-orders.json` — vì hợp đồng chỉ ràng buộc đúng những gì `OrdersConsumerPactTests` chứng minh `OrdersApiClient` thật sự đọc (`id`, `placedAtUtc`, `total`).

#### `orders-basketcheckedout.json` — [`BasketCheckedOutConsumerPactTests.cs`](../../services/orders/tests/Orders.Api.ContractTests/BasketCheckedOutConsumerPactTests.cs) viết, [`BasketCheckedOutProviderPactTests.cs`](../../services/baskets/tests/Baskets.Api.ContractTests/BasketCheckedOutProviderPactTests.cs) đọc

**Chú ý: chiều NGƯỢC lại với 3 file trên.** Trong 3 ranh giới HTTP ở trên, `bff` luôn là consumer (nó gọi các service kia). Nhưng với sự kiện `BasketCheckedOut`, **`orders` mới là consumer** (nó là bên sẽ *nhận* sự kiện này trong tương lai, theo thiết kế outbox chưa xây — xem [05](05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md)) và **`baskets` là provider** (nó *phát ra* sự kiện). Đừng nhầm "ai là consumer của 1 hợp đồng Pact" với "ai gọi ai qua HTTP" (đã có nền tảng ở [09](09-bff-dependency-downstream-va-trien-khai.md)) — 2 trục hoàn toàn khác nhau: `baskets` không hề gọi HTTP tới `orders` ở đâu cả, nhưng vẫn là "producer" của 1 hợp đồng Pact vì nó là bên *tạo ra dữ liệu* sự kiện.

Phía **ĐỌC** (`baskets`, dù đóng vai "provider" của sự kiện) có 1 điểm đặc biệt: không gọi qua HTTP, không có broker/MassTransit — nó gọi **thẳng hàm dựng payload thật**:
```csharp
verifier
    .WithMessages(scenarios => scenarios.Add("a basket checked out", CheckOutABasketHoldingOneItem))
    .WithFileSource(new FileInfo(Path.Combine(PactPaths.Directory, "orders-basketcheckedout.json")))
    .Verify();

private static object CheckOutABasketHoldingOneItem()
{
    var basket = Basket.ForCustomer("pact-shopper");
    basket.AddItem(ProductId, quantity: 2, unitPrice: 12.50m);
    return BasketCheckedOutMapper.ToEvent(basket, tenantId: "contoso", ...);   // hàm THẬT, chưa ai gọi lúc runtime (xem 05)
}
```
Comment gốc: *"khi SCRUM-31 nối outbox và publisher thật, test này giữ nguyên không đổi — thứ nó kiểm tra là HÌNH DẠNG payload, không phải cách nó được gửi đi."*

### 6.B Nếu 2 service ở 2 repo khác nhau — câu trả lời đã có sẵn trong repo, không phải giải pháp tôi tự nghĩ ra

Đây là điều đáng chú ý nhất: câu hỏi của bạn **đã được chính dự án này đặt ra và trả lời từ trước** khi chọn công cụ, ghi trong [`docs/adr/0006-contract-testing-tool.md`](../../docs/adr/0006-contract-testing-tool.md) (Status: **Accepted**):
> *"Use **Pact, with a self-hosted Pact Broker**, and Pact's message-pact feature... for event boundaries."*

Nghĩa là kiến trúc GỐC được duyệt **không phải** "file JSON commit chung trong 1 repo" — đó chỉ là cách feature 011 chọn làm **trước**, có ghi rõ lý do phạm vi trong [`specs/011-consumer-contract-tests/research.md` Decision 2](../../specs/011-consumer-contract-tests/research.md):
> *"No Pact Broker is stood up as part of this feature... Standing up and operating a Broker service is real infrastructure work... that belongs to ADR-0006 Action Item 1 as its own scoped effort, not an implicit dependency of this feature."*

ADR-0006's Action Item 1 — *"Stand up a self-hosted Pact Broker"* — vẫn ở trạng thái **`[ ]` chưa làm**.

**Pact Broker giải quyết đúng vấn đề "không còn chung ổ đĩa" của bạn như sau** (đọc từ chính lý lẽ ADR-0006 đưa ra khi so sánh 2 phương án):
- **Consumer publish, không ghi file cục bộ:** thay vì `PactPaths.Directory` trỏ vào 1 thư mục trên đĩa, consumer (ở repo A) `POST` pact JSON lên Broker qua HTTP, gắn kèm version (thường là git SHA của chính build đó).
- **Provider pull, không cần biết đường dẫn nào:** provider (ở repo B, build hoàn toàn độc lập) hỏi Broker qua HTTP *"đưa tao mọi pact mà bất kỳ ai từng khai là phụ thuộc vào tao"* — Broker tự trả lời, provider không cần biết trước A tồn tại hay đặt tên gì. Đây chính là điều ADR-0006's Trade-off Analysis gọi là *"vấn đề dò dependency phân tán"* — Broker giải quyết nó thay vì để đội ngũ tự xây tay.
- **`can-i-deploy` — trả lời trực tiếp phần "trước khi deploy lên stage" của Phần 4, cho kịch bản đa-repo:** ADR-0006 gọi đây là *"core workflow"* của Pact. Trước khi deploy 1 version provider mới lên `stage`, hỏi Broker: *"version này của tôi có an toàn với TẤT CẢ consumer đang thực sự chạy ở môi trường `stage` không?"* — Broker biết điều này vì nó theo dõi consumer nào đã publish pact nào, và biết version nào đang chạy ở môi trường nào (qua Broker ghi nhận triển khai). Đây là cơ chế polyrepo tương đương với việc `ContractCoverageScanner` (monorepo, đọc file tĩnh) làm — chỉ khác là hỏi qua mạng tới 1 dịch vụ trung tâm thay vì đọc thư mục cục bộ.

### 6.C Nếu polyrepo cần contract test ngay hôm nay, trong khi Broker chưa dựng?

Không bịa thêm phương án — chỉ suy ra từ đúng lý lẽ đã ghi trong `research.md` Decision 2's phần "Alternatives considered", vốn liệt kê VÀ TỪ CHỐI phương án: *"Publishing pact files as CI build artifacts instead of committing them"* — bị từ chối **CHO FEATURE 011** vì lý do cụ thể: *"làm cho việc audit của User Story 3 (SC-003: trong dưới 5 phút, không cần đọc source code) phụ thuộc vào việc tìm đúng lần chạy CI, thay vì đọc thẳng repo."*

Lý do từ chối đó **gắn chặt với bối cảnh monorepo** (đang có sẵn 1 thư mục để đọc trực tiếp) — 1 polyrepo vốn dĩ **không có** "1 thư mục chung để đọc trực tiếp" ngay từ đầu, nên lý do từ chối đó không còn áp dụng. Suy ra hợp lý (không phải bằng chứng đã ghi sẵn, mà là 1 bước suy luận từ chính lý lẽ trên): với polyrepo, "publish pact file làm CI artifact rồi tải về ở build của phía kia" có thể là phương án tạm hợp lý hơn — miễn là chấp nhận đánh đổi ngược lại (audit khó hơn, cần tìm đúng artifact của đúng lần build) — cho tới khi Action Item 1 của ADR-0006 (Pact Broker) thật sự được dựng.

## Đi đâu tiếp theo

- [05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md](05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md) — cơ chế Pact cơ bản (consumer/provider, message pact).
- [07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md](07-cac-du-an-test-quy-uoc-va-ci-quality-gate.md) — `ContractCoverageTests`, và trạng thái branch protection đã xác nhận.
- [09-bff-dependency-downstream-va-trien-khai.md](09-bff-dependency-downstream-va-trien-khai.md) — `DownstreamCall.cs`/`DownstreamExceptionHandler.cs` đầy đủ.
- [`shared/EventContracts/README.md`](../../shared/EventContracts/README.md) — toàn văn quy tắc versioning/deprecation cho event contract, trích ở Phần 5.
- [`docs/adr/0006-contract-testing-tool.md`](../../docs/adr/0006-contract-testing-tool.md) và [`specs/011-consumer-contract-tests/research.md`](../../specs/011-consumer-contract-tests/research.md) — toàn văn quyết định Pact Broker, trích ở Phần 6.
