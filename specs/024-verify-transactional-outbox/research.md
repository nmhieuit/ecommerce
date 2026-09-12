# Research: Xác minh outbox pattern giao dịch trên dịch vụ phát sự kiện đơn hàng

**Feature**: [spec.md](./spec.md) | **Date**: 2026-09-11

## Quyết định 0: Xác nhận hiện trạng trước khi thiết kế

Trước khi quyết định cách tiếp cận, đã rà soát trực tiếp trạng thái hiện tại của repository (không
suy đoán):

- **Không có bất kỳ hạ tầng nhắn tin thật nào trong mã nguồn**: grep toàn repo cho
  `MassTransit|AddEntityFrameworkOutbox|IPublishEndpoint|IBus\b` chỉ khớp các dòng comment nói rằng
  cơ chế này CHƯA tồn tại (`services/orders/tests/Orders.Api.ContractTests/BasketCheckedOutConsumerPactTests.cs:14`:
  "No broker and no MassTransit. Nothing publishes or consumes `BasketCheckedOut` yet"). `Directory.Packages.props`
  không có gói `MassTransit*` nào — chỉ có `RabbitMQ.Client` 7.2.2 và `Testcontainers.RabbitMq` 4.14.0,
  cả hai được ghi chú rõ là "test-only, referenced only by `shared/IntegrationTestSupport.Tests`... never
  by a service's runtime code".
- **`POST /orders` (`services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs`) không publish
  gì cả** sau khi lưu đơn hàng — chỉ `dbContext.SaveChangesAsync()` rồi trả `201`.
  `Order.cs` tự xác nhận trong docstring: "The outbox table (constitution Principle IV) is likewise
  absent; it lands alongside the first event this platform publishes" — tức chính tác giả trước đã ghi
  nhận đây là việc còn thiếu.
- **`OrderPlacedV1` (`shared/EventContracts/OrderPlacedV1.cs`) chỉ là hợp đồng (schema + record), chưa
  từng được publish** — không có `OrderPlacedConsumerPactTests` hay `OrderPlacedProviderPactTests` nào
  tồn tại (khác với `BasketCheckedOut`, vốn đã có provider/consumer pact test dù cũng "chưa ai gọi").
  Trường của nó (`EventId`, `OccurredAtUtc`, `OrderId`, `TenantId`, `CorrelationId`, `Total`, `Lines`)
  **không có bất kỳ định danh khách hàng/giỏ hàng nào** (không `CustomerRef`, không `BasketId`).
- **`RabbitMQ` container ĐÃ tồn tại sẵn** trong `docker-compose.yml`, `docker-compose.local.yml`, và
  `docker-compose.debug.yml` — nhưng KHÔNG có trong `docker-compose.deps.yml` (file khởi động phụ
  thuộc theo từng service riêng lẻ cho dev). Comment tại `docker-compose.yml:76-78` ghi rõ: "Nothing
  connects to this. Spec FR-017 chose to run the platform's declared dependencies even where no code
  uses them, so the story that first needs one finds it present." — tính năng này chính là "the story
  that first needs one".
- **`shared/IntegrationTestSupport/RabbitMqFixture.cs` đã tồn tại** (từ 010-testcontainers-integration-tests),
  gồm cả `KillBrokerAsync()` để mô phỏng broker chết — nhưng đó là mô phỏng **broker** chết, khác với
  kịch bản thật của Jira SCRUM-31 ("kill the orders service **process**"), nên fixture này tái dùng
  được cho RabbitMQ thật trong test nhưng không tự nó giải quyết được kịch bản mô phỏng crash tiến
  trình.
- **ADR-0011 (`docs/adr/0011-checkout-orchestration.md`/`.vi.md`)** ghi nhận chính thức: checkout hiện
  là 2 bước đồng bộ do BFF điều phối (tạo đơn → xoá giỏ hàng), đây là "1 sai lệch có ghi nhận, có giới
  hạn thời gian, so với Principle IV", với "Việc cần làm" #2: *"SCRUM-31: thay thế điều phối này bằng 1
  saga dựa trên outbox và verify nó bằng cách kill tiến trình giữa lúc publish"*. Tương tự,
  `services/bff/src/Bff.Api/Features/Checkout/CheckoutEndpoints.cs` và
  `services/baskets/src/Baskets.Api/Features/Checkout/BasketCheckedOutMapper.cs` đều có comment nói
  "the outbox and the publisher... is SCRUM-31's work", và `specs/004-minimal-shopping-spa/plan.md`
  Complexity Tracking đánh dấu deviation Principle IV là "Time-bound: closed by SCRUM-18/SCRUM-31".
  **Đây là kỳ vọng rộng hơn 3 tiêu chí chấp nhận thật sự của Jira SCRUM-31** (xem Quyết định 1 bên
  dưới) — các comment này được viết tại thời điểm 004/ADR-0011, mang tính dự đoán trước khi Jira
  SCRUM-31 có nội dung cụ thể, không phải một cam kết ràng buộc phạm vi của story này.

## Quyết định 1: Phạm vi — chỉ đóng outbox của "order publisher" (OrderPlaced), KHÔNG dựng lại saga checkout đầy đủ

**Decision**: Tính năng này chỉ hiện thực và xác minh outbox pattern cho việc `orders` publish
`OrderPlacedV1` (đúng 3 tiêu chí chấp nhận của Jira SCRUM-31: ghi nguyên tử, phục hồi khi crash,
consumer idempotent). **Không** chuyển bước "tạo đơn" của BFF sang tiêu thụ `BasketCheckedOutV1`,
và **không** dùng bước "xoá giỏ hàng" làm consumer thật của `OrderPlacedV1`. ADR-0011 và
`specs/004-minimal-shopping-spa/plan.md` Complexity Tracking (Principle IV) **giữ nguyên, không đóng**
bởi tính năng này — chỉ cập nhật một ghi chú nhỏ nói rõ hạ tầng outbox nay đã tồn tại cho `OrderPlaced`
nhưng `BasketCheckedOut` vẫn chưa được publish/consume.

**Rationale**:
- Cả 3 tiêu chí chấp nhận của Jira SCRUM-31 (xem `spec.md` Input) chỉ nói về `orders` publish
  `OrderPlaced` và một "consumer" nhận nó — không có tiêu chí nào nhắc tới `BasketCheckedOut` hay việc
  đổi cách BFF gọi `orders`. Mở rộng sang đó là thêm phạm vi không ai yêu cầu (vi phạm nguyên tắc
  "không thêm trừu tượng/tính năng ngoài phạm vi cần thiết").
- Thử dùng bước "xoá giỏ hàng" làm consumer thật cho `OrderPlaced` (phương án ban đầu được cân nhắc)
  **không khả thi mà không phá vỡ hợp đồng sự kiện đã publish**: `OrderPlacedV1` không mang
  `CustomerRef` hay `BasketId` (chỉ `OrderId`, `TenantId`, `CorrelationId`, `Total`, `Lines`), nên một
  consumer nhận sự kiện này không có cách nào biết phải xoá giỏ hàng của ai. Thêm trường đó là một
  **thay đổi hợp đồng** (theo constitution Principle II, phải là `OrderPlacedV2` mới, kèm bộ test
  schema/immutability/tolerant-reader riêng) — một hạng mục lớn hơn hẳn 3 tiêu chí chấp nhận đang có,
  và không được Jira SCRUM-31 yêu cầu.
- Chuyển bước "tạo đơn" (BFF gọi `orders`) sang bất đồng bộ qua `BasketCheckedOut` sẽ phá vỡ hợp đồng
  phản hồi hiện có của `/bff/checkout` (`specs/004-minimal-shopping-spa/contracts/bff-openapi.yaml`):
  storefront cần `OrderConfirmationResponse` (id, ngày đặt, tổng tiền) trả về **ngay trong response
  HTTP** để hiển thị trang xác nhận; một luồng saga bất đồng bộ không thể trả những giá trị đó tại thời
  điểm response mà không đổi luôn UX xác nhận đơn — một quyết định vượt xa phạm vi "verify outbox".
- FR-005 của `spec.md` chỉ yêu cầu "consumer... không tạo tác dụng phụ trùng lặp khi nhận trùng sự
  kiện" — không yêu cầu consumer đó phải là một nghiệp vụ thật (`spec.md` Assumptions đã nói rõ: "đặc
  tả không định nghĩa lại logic nghiệp vụ cụ thể của từng consumer"). Một consumer xác minh chuyên
  dụng, chạy trong chính bộ test tích hợp, dùng đúng cơ chế Consumer Outbox/Inbox thật của MassTransit
  (không phải logic tự viết), vẫn chứng minh được cơ chế idempotent hoạt động thật với hạ tầng thật
  (Testcontainers), đúng tinh thần constitution Principle III.

**Alternatives considered**:
- **Dựng saga đầy đủ (baskets publish `BasketCheckedOut` → orders consume để tạo đơn → orders publish
  `OrderPlaced` → baskets consume để xoá giỏ)**: Bị loại — nhân quy mô tính năng lên nhiều lần, đòi hỏi
  đổi hợp đồng `OrderPlacedV1` (thêm định danh khách hàng) và đổi UX xác nhận đơn (không còn đồng bộ),
  trong khi không có tiêu chí chấp nhận nào của Jira SCRUM-31 yêu cầu điều này. Đúng hướng ADR-0011 dự
  tính, nhưng sai phạm vi của đúng story này — để dành cho một story riêng nếu cần đóng ADR-0011 hoàn
  toàn sau này.
- **Đổi `OrderPlacedV1` thành `OrderPlacedV2` thêm `CustomerRef`/`BasketId` ngay trong tính năng này**:
  Bị loại cùng lý do trên — không phục vụ trực tiếp bất kỳ FR nào của `spec.md`.

## Quyết định 2: Cơ chế outbox kỹ thuật — dùng MassTransit + MassTransit.EntityFrameworkCore, không tự viết

**Decision**: Dùng gói `MassTransit` + `MassTransit.RabbitMQ` + `MassTransit.EntityFrameworkCore`,
**pin ở dòng 8.x (8.5.4)**, dùng cơ chế **Bus Outbox** có sẵn của thư viện
(`AddEntityFrameworkOutbox<TDbContext>` + `UseBusOutbox()`) cho phía publisher (`orders`), thay vì tự
viết bảng outbox và một `BackgroundService` quét tay.

**Sửa lại sau khi triển khai (tasks.md T001, phát hiện khi chạy `orders-api` thật, ngoài
`WebApplicationFactory`)**: quyết định ban đầu ở đây chọn `9.2.1` dựa trên xác minh NuGet Gallery
(tương thích .NET 10, ràng buộc phiên bản transitive khớp đúng) — nhưng **bỏ sót hoàn toàn việc kiểm
tra giấy phép**. Chạy `orders-api` thật với `9.2.1` crash ngay khi khởi động:
`MassTransit.ConfigurationException: License must be specified with SetLicense/SetLicenseLocation`.
Tra cứu xác nhận: **MassTransit v9 (bản ổn định đầu tiên ~Q1 2026) là sản phẩm thương mại có phí**
(domain giấy phép `masstransit.massient.com` là hợp pháp — công ty thương mại của MassTransit đổi
tên — không phải giả mạo, dù trông khả nghi lúc research ban đầu); **v8.x vẫn miễn phí/OSS
(Apache-2.0)** và vẫn được vá bảo mật tới hết 2026. Mọi phụ thuộc khác trong repo này đều là OSS —
đưa vào một phụ thuộc có phí là một quyết định kinh doanh không nằm trong thẩm quyền của bất kỳ bước
plan/implementation nào tự quyết định.

**Đã đổi sang 8.5.4** (bản 8.x mới nhất, đã xác minh trên NuGet Gallery): `MassTransit.RabbitMQ`
8.5.4 phụ thuộc `RabbitMQ.Client >= 7.1.2` (thoả bởi 7.2.2 đã pin sẵn); `MassTransit.EntityFrameworkCore`
8.5.4 phụ thuộc `Microsoft.EntityFrameworkCore.Relational >= 9.0.1` (thoả bởi EF Core 10.0.0 đã
dùng — đây là mức sàn, không phải pin cứng, nên không có hạ cấp EF Core nào xảy ra). 8.5.4 target
`net8.0` (không phải `net10.0`) nhưng chạy được bình thường trên các service `net10.0` của nền tảng
này, như mọi thư viện target `net8.0` khác. **Model entity outbox/inbox giữa 8.x và 9.x khác nhau**
(migration EF Core sinh ra khác nhau giữa 2 phiên bản) — đã xoá và tạo lại migration
`AddTransactionalOutbox` sau khi đổi phiên bản (`dotnet ef migrations remove` rồi `add` lại), xác
nhận qua lỗi `PendingModelChangesWarning` thật khi chạy test với migration cũ.
Toàn bộ 29 test tích hợp và việc chạy `orders-api` thật (kết nối RabbitMQ thật, log xác nhận
`Bus started: rabbitmq://localhost/`) đã xác nhận lại PASS/hoạt động đúng sau khi đổi phiên bản —
hành vi outbox/inbox không đổi giữa 2 dòng phiên bản, chỉ khác license.

**Rationale**:
- Constitution's "Technology and Infrastructure Constraints" đã chốt cứng từ trước:
  *"Messaging: RabbitMQ via MassTransit, with outbox and retry/dead-letter policies configured
  centrally"* — đây là quyết định kiến trúc nền tảng, không phải lựa chọn riêng của tính năng này.
  `MassTransit.EntityFrameworkCore`'s Bus Outbox ghi bản ghi outbox trong đúng transaction EF Core mà
  `SaveChangesAsync` của yêu cầu tạo đơn đang chạy, cộng một hosted "outbox delivery service" tự quét
  bảng theo chu kỳ và gửi — kể cả sau khi tiến trình khởi động lại, đúng thứ acceptance criterion 2 của
  Jira yêu cầu ("kill tiến trình giữa lúc publish... restart... relay vẫn publish").
- Tự viết lại đúng cơ chế mà một gói đã công bố, đã kiểm thử, và constitution đã chỉ định là trùng lặp
  công sức không cần thiết (cùng lý lẽ 021 dùng khi chọn tái sử dụng dashboard Kibana có sẵn thay vì
  xây mới) — và có rủi ro thật (race condition khi nhiều instance cùng quét 1 bảng) mà thư viện đã xử
  lý.

**Alternatives considered**:
- **Tự viết bảng `OutboxMessage` + `BackgroundService` quét tay + publish qua `RabbitMQ.Client` trực
  tiếp**: Bị loại — trùng lặp với `MassTransit.EntityFrameworkCore` đã xác minh tương thích, và đi
  ngược constitution (vốn đã chỉ định MassTransit, không phải RabbitMQ.Client trực tiếp, là cơ chế
  nhắn tin của nền tảng — `RabbitMQ.Client` hiện chỉ dùng cho mục đích test/smoke theo comment trong
  `Directory.Packages.props`).
- **Dùng `MassTransit.RabbitMQ`'s in-memory/transport-only outbox (`services.AddTransactionalOutboxInMemory` kiểu cũ)**:
  Không tồn tại một cơ chế "trong bộ nhớ" nào thoả được yêu cầu "sống sót qua crash tiến trình" —
  chỉ có cơ chế dựa trên bảng CSDL bền vững (EF Core outbox) mới thoả acceptance criterion 2.

## Quyết định 3: Consumer xác minh idempotency — dùng Consumer Outbox/Inbox thật của MassTransit, chạy trong bộ test tích hợp

**Decision**: Thêm một consumer `OrderPlacedV1` **chỉ tồn tại trong bộ test tích hợp**
(`services/orders/tests/Orders.Api.IntegrationTests`), đăng ký qua
`WebApplicationFactory.WithWebHostBuilder` để gắn thêm một receive endpoint vào cùng host đang chạy
`Orders.Api`, dùng đúng cơ chế **Consumer Outbox/Inbox** của `MassTransit.EntityFrameworkCore`
(`UseEntityFrameworkOutbox<OrdersDbContext>()` trên receive endpoint, dựa trên bảng `InboxState` khoá
theo `MessageId`) để chặn xử lý trùng. Consumer này không phải một nghiệp vụ thật (không xoá giỏ hàng,
không gọi service nào khác) — nó chỉ ghi một bản ghi "đã xử lý sự kiện X" vào một bảng test riêng, để
assertion của test kiểm tra được: nhận lần 1 → ghi 1 bản ghi; nhận lại lần 2 (cùng `EventId`) → không
ghi thêm.

**Rationale**:
- Xác minh đúng **cơ chế thật** (`InboxState`, row-level lock theo `MessageId`) mà bất kỳ consumer thật
  nào trong tương lai (logistics, invoices — hiện đang "Later/Parked" theo `docs/roadmap.md`) sẽ dùng
  lại, thay vì assert một đoạn code tự viết dedupe theo `HashSet` không phản ánh hành vi thật với hạ
  tầng thật (đi ngược Principle III: "Testcontainers... In-memory... fakes... are NOT acceptable
  substitutes").
- Không cần đổi `OrderPlacedV1`, không cần thêm nghiệp vụ mới cho `baskets` hay bất kỳ service nào khác
  — giữ đúng phạm vi ở Quyết định 1.

**Alternatives considered**:
- **Consumer tự viết dedupe bằng bảng riêng không dùng `InboxState`**: Bị loại — không chứng minh được
  cơ chế Consumer Outbox thật của MassTransit (thứ mà một consumer sản xuất tương lai sẽ thực sự dùng)
  hoạt động đúng; chỉ chứng minh code test tự viết đúng, không phải điều Jira acceptance criterion 3
  thật sự cần được tin tưởng.
- **Đặt consumer xác minh này vào một service/project sản xuất mới**: Bị loại — không có nghiệp vụ thật
  nào cần một service mới; một project mới chỉ để "có chỗ chứa 1 consumer test" là phức tạp không
  tương xứng (Principle I).

## Quyết định 4: Kịch bản mô phỏng crash tiến trình (Test Scenario 1 của Jira)

**Decision**: Dùng 2 `WebApplicationFactory<Program>` trỏ tới **cùng 1** cặp Testcontainers
(`SqlServerFixture` đã có của `Orders.Api.IntegrationTests` + `RabbitMqFixture` của
`shared/IntegrationTestSupport`) trong cùng 1 test, mô phỏng "khởi động lại":
1. **Host A** (mô phỏng tiến trình trước khi sập): khởi động với chu kỳ quét outbox
   (`QueryDelay`/tương đương) được cấu hình dài hơn thời lượng bước gọi API, để đảm bảo outbox delivery
   service CHƯA kịp quét lần đầu. Gọi `POST /orders` qua `HttpClient` của Host A, xác nhận `201`
   (transaction đã commit — order + outbox record cùng tồn tại trong CSDL thật). `Dispose()` Host A
   ngay sau đó — mô phỏng tiến trình sập trước khi outbox delivery service kịp gửi.
2. **Host B** (mô phỏng khởi động lại): tạo mới, trỏ cùng connection string CSDL và cùng RabbitMQ
   container. Outbox delivery service của Host B tự quét thấy bản ghi outbox cũ (do Host A để lại,
   chưa gửi) và gửi nó — không cần gọi API nào khác, không can thiệp thủ công.
3. Assert: consumer xác minh (Quyết định 3), chạy trên Host B, nhận được đúng sự kiện đó sau khi Host B
   khởi động — chứng minh "at-least-once" sống sót qua vòng đời tiến trình, đúng nghĩa đen của Test
   Scenario 1 trong Jira ("kill the orders service process... restart... confirm the event still gets
   published").

**Rationale**: Đây là cách duy nhất dùng hạ tầng thật (không mock đồng hồ, không mock bus) để tái hiện
đúng "khoảng trống" giữa commit và publish mà pattern outbox tồn tại để đóng — nhất quán với Principle
III và với cách `RabbitMqFixture.KillBrokerAsync()` đã mô phỏng sự cố hạ tầng (broker chết) cho
010-testcontainers-integration-tests, chỉ khác đối tượng bị "giết" là tiến trình ứng dụng, không phải
broker.

**Alternatives considered**:
- **Mock/fake thời gian hoặc mock `IBus`**: Bị loại — không chứng minh được hành vi thật qua một crash
  và restart thật, và vi phạm trực tiếp Principle III.
- **Dùng `docker kill` lên container thật của Orders.Api**: Cân nhắc nhưng loại — tính năng này chưa
  đóng gói Orders.Api thành container riêng cho test tích hợp (khác biệt với cách 019 dùng cluster
  `kind` thật cho smoke test K8s); dùng 2 `WebApplicationFactory` trong cùng tiến trình test đạt được
  đúng ngữ nghĩa "sập rồi khởi động lại" mà không cần thêm hạ tầng Docker mới cho riêng tính năng này.

## Quyết định 5: Hạ tầng RabbitMQ cho dev cục bộ

**Decision**: Container RabbitMQ đã có sẵn trong `docker-compose.yml`/`docker-compose.local.yml`/
`docker-compose.debug.yml` được dùng nguyên trạng cho môi trường chạy đầy đủ. Bổ sung thêm 1 service
`rabbitmq` **dùng chung** (không phải per-service như các `*-db`/`*-db-init`) vào
`docker-compose.deps.yml`, vì message broker vốn là hạ tầng chia sẻ giữa các service tham gia
trao đổi sự kiện (khác bản chất với "mỗi service sở hữu CSDL riêng" của Principle I) — và thêm biến môi
trường kết nối RabbitMQ (`ConnectionStrings__RabbitMq` hoặc tương đương) cho `orders-api` trong
`docker-compose.yml`.

**Rationale**: Giữ đúng khuôn mẫu comment đã có tại `docker-compose.yml` ("the story that first needs
one finds it present") — không cần dựng lại container, chỉ cần nối dây (wiring) thật lần đầu tiên.

**Alternatives considered**:
- **Chỉ dùng Testcontainers cho test, không đụng docker-compose nào**: Bị loại — `orders-api` cần chạy
  được cục bộ (một lệnh, theo 005-one-command-local-run) với RabbitMQ thật để nhà phát triển có thể tự
  tay tái hiện 3 kịch bản kiểm thử của Jira ngoài CI, không chỉ trong test tự động.

## Tổng kết: NEEDS CLARIFICATION đã được giải quyết

Không có mục nào trong Technical Context còn để "NEEDS CLARIFICATION". Năm quyết định trên đều dựa
trên: (a) hiện trạng đã xác nhận trực tiếp trong repository (Quyết định 0), (b) phiên bản gói NuGet đã
xác minh thật trên NuGet Gallery (Quyết định 2), và (c) tài liệu quyết định kiến trúc đã có
(ADR-0011, constitution) — không có quyết định nào cần thêm thông tin từ bên ngoài đặc tả hay
constitution. Quyết định 1 là quyết định phạm vi quan trọng nhất của kế hoạch này: **thu hẹp có chủ ý**
so với kỳ vọng rộng hơn mà một số comment cũ trong mã nguồn gợi ý, với lý do kỹ thuật cụ thể (hợp đồng
sự kiện thiếu định danh khách hàng) chứ không phải cắt giảm tuỳ tiện.
