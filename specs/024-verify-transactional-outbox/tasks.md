---

description: "Task list template for feature implementation"
---

# Tasks: Xác minh outbox pattern giao dịch trên dịch vụ phát sự kiện đơn hàng

**Input**: Design documents from `specs/024-verify-transactional-outbox/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Constitution Principle III (Test-First, NON-NEGOTIABLE) áp dụng cho tính năng này — các
task test dưới đây là BẮT BUỘC, không tùy chọn, PHẢI được viết trước và xác nhận FAIL (chưa có
implementation) rồi mới triển khai cho tới khi PASS, dùng Testcontainers thật (SQL Server + RabbitMQ),
không mock bus.

**Organization**: Task được nhóm theo user story trong spec.md để mỗi story triển khai và kiểm thử
độc lập được.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Có thể chạy song song (khác file, không phụ thuộc task chưa hoàn thành)
- **[Story]**: User story mà task thuộc về (US1, US2, US3)
- Mỗi task đều có đường dẫn file cụ thể

## Path Conventions

Bổ sung vào 1 service hiện có (`orders`) trong monorepo — không có service runtime mới, không đụng
`baskets`/`bff` (xem plan.md § Project Structure và research.md Quyết định 1):

- Service sửa: `services/orders/src/Orders.Api/`
- Test tích hợp mở rộng: `services/orders/tests/Orders.Api.IntegrationTests/`
- Gói NuGet: `Directory.Packages.props`
- Hạ tầng dev cục bộ: `docker-compose.yml`, `docker-compose.deps.yml`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Thêm gói MassTransit và hạ tầng RabbitMQ mà mọi user story đều cần trước khi có bất kỳ
code nghiệp vụ nào

- [X] T001 [P] Thêm `PackageVersion` cho `MassTransit`, `MassTransit.RabbitMQ`,
      `MassTransit.EntityFrameworkCore` (9.2.1 — research.md Quyết định 2) vào
      `Directory.Packages.props`, cạnh nhóm comment "Messaging" mới (chưa có nhóm này — thêm ngay sau
      nhóm `Testcontainers.RabbitMq`/`RabbitMQ.Client` hiện có, kèm comment trỏ về research.md Quyết
      định 2 giải thích vì sao chọn 9.2.1 và các ràng buộc phiên bản transitive đã xác minh)
- [X] T002 [P] Thêm `ProjectReference` tới `shared/EventContracts/EventContracts.csproj` và
      `PackageReference` cho `MassTransit`, `MassTransit.RabbitMQ`, `MassTransit.EntityFrameworkCore`
      vào `services/orders/src/Orders.Api/Orders.Api.csproj`
- [X] T003 [P] Thêm `PackageReference` cho `MassTransit` vào
      `services/orders/tests/Orders.Api.IntegrationTests/Orders.Api.IntegrationTests.csproj` (cần cho
      consumer xác minh viết ở US3 tham chiếu `IConsumer<T>`/`ConsumeContext<T>`)
- [X] T004 [P] Thêm 1 service `rabbitmq` dùng chung (khác per-service `*-db`) vào
      `docker-compose.deps.yml` (research.md Quyết định 5), dùng cùng image
      `rabbitmq:3-management-alpine` đã dùng ở `docker-compose.yml`
- [X] T005 [P] Thêm biến kết nối RabbitMQ (`ConnectionStrings__RabbitMq` hoặc tương đương) và
      `depends_on: rabbitmq: condition: service_healthy` cho service `orders-api` trong
      `docker-compose.yml`

**Checkpoint**: Gói và hạ tầng RabbitMQ sẵn sàng — có thể bắt đầu đăng ký MassTransit trong code

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Đăng ký MassTransit + outbox/inbox trên `OrdersDbContext`, hạ tầng test dùng chung — mọi
user story đều phụ thuộc vào việc này tồn tại trước

**⚠️ CRITICAL**: Không có user story nào bắt đầu được trước khi phase này hoàn tất

- [X] T006 Trong `services/orders/src/Orders.Api/Data/OrdersDbContext.cs`, thêm
      `modelBuilder.AddTransactionalOutboxEntities()` vào `OnModelCreating` — đăng ký 3 bảng
      `OutboxMessage`/`OutboxState`/`InboxState` do `MassTransit.EntityFrameworkCore` định nghĩa sẵn
      (data-model.md) — phụ thuộc T002
- [X] T007 Tạo migration EF Core mới `AddTransactionalOutbox` cho `OrdersDbContext`
      (`dotnet ef migrations add AddTransactionalOutbox --project services/orders/src/Orders.Api`) —
      additive, không sửa bảng `Orders` hiện có (Principle X) — phụ thuộc T006
- [X] T008 Trong `services/orders/src/Orders.Api/Program.cs`, đăng ký MassTransit: `AddMassTransit(x => { x.AddEntityFrameworkOutbox<OrdersDbContext>(o => { ... }); x.UsingRabbitMq((context, cfg) => cfg.Host(...)); })`,
      dùng `UseBusOutbox()` cho Bus Outbox phía publisher (research.md Quyết định 2) — đọc host/thông
      tin kết nối RabbitMQ từ configuration (khớp biến môi trường T005) — phụ thuộc T006
- [X] T009 [P] Trong `shared/ServiceDefaults/ServiceDefaultsExtensions.cs`, thêm
      `.AddSource("MassTransit")` (tracing) và `.AddMeter("MassTransit")` (metrics) vào
      `AddServiceDefaults`, cùng khuôn mẫu đã làm cho `"Polly"` (020) — để hoạt động
      publish/outbox-delivery hiện lên trong OTel (Principle VII)
- [X] T010 [P] Tạo `services/orders/tests/Orders.Api.IntegrationTests/OutboxTestCollection.cs` — 1
      `ICollectionFixture` kết hợp `SqlServerFixture` (đã có trong project) và `RabbitMqFixture`
      (`shared/IntegrationTestSupport`), dùng chung bởi cả 3 file test của US1/US2/US3 bên dưới
- [X] T011 [P] Trong `services/orders/src/Orders.Api/Data/Order.cs`, xoá đoạn docstring lỗi thời
      "The outbox table (constitution Principle IV) is likewise absent; it lands alongside the first
      event this platform publishes (SCRUM-18)" — outbox nay đã tồn tại (T006/T007)

**Checkpoint**: Hạ tầng outbox/inbox + MassTransit sẵn sàng trên `orders` — có thể bắt đầu viết
implementation/test riêng cho từng user story

---

## Phase 3: User Story 1 - Ghi đơn hàng và ghi outbox không bao giờ lệch nhau (Priority: P1) 🎯 MVP

**Goal**: `POST /orders` ghi bản ghi `Order` và bản ghi outbox chứa `OrderPlacedV1` trong cùng 1
transaction; rollback thì cả hai cùng không tồn tại.

**Independent Test**: `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter OrderPlacedOutboxAtomicityTests`
pass — tự đủ, không cần US2/US3.

### Tests for User Story 1

- [X] T012 [P] [US1] Viết
      `services/orders/tests/Orders.Api.IntegrationTests/OrderPlacedOutboxAtomicityTests.cs` — test 1
      (Bất biến 1, `contracts/outbox-guarantees-contract.md`): gọi `POST /orders` qua `HttpClient` của
      `WebApplicationFactory`, sau đó đọc trực tiếp `OrdersDbContext` (không qua API) để xác nhận đúng
      1 hàng `Order` và đúng 1 hàng outbox chứa payload `OrderPlacedV1` khớp `OrderId` cùng tồn tại —
      phụ thuộc T010; PHẢI FAIL trước khi có T014 (chưa có gì publish)
- [X] T013 [P] [US1] Trong cùng file `OrderPlacedOutboxAtomicityTests.cs`, thêm test 2 (Bất biến 2):
      buộc transaction tạo đơn rollback (ví dụ chèn 1 dòng hàng vi phạm ràng buộc CSDL có chủ đích sau
      khi domain validation đã qua), xác nhận KHÔNG có hàng `Order` VÀ KHÔNG có hàng outbox nào cho
      request đó — phụ thuộc T010

### Implementation for User Story 1

- [X] T014 [US1] Trong
      `services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs`, handler `POST /orders`: sau
      `Order.PlaceFrom(...)` và trước/cùng `dbContext.SaveChangesAsync()`, dựng `OrderPlacedV1` từ
      `order` (`Id`, `PlacedAtUtc`, `Total`, `TenantId`), `request.Items` (map sang `OrderLineV1`, vì
      `Order` không lưu line item — data-model.md), `Guid.NewGuid()` cho `EventId`, và
      `context.Items[CorrelationIdMiddleware.HeaderName]` cho `CorrelationId`; publish qua
      `IPublishEndpoint` sao cho MassTransit's Bus Outbox (T008) ghi nó vào cùng transaction EF Core
      của `SaveChangesAsync()` (tham khảo `github.com/MassTransit/Sample-Outbox` cho đúng thứ tự gọi
      `Publish`/`SaveChangesAsync`) — phụ thuộc T008; chạy lại T012/T013, xác nhận PASS

**Checkpoint**: User Story 1 hoàn thiện, kiểm thử độc lập được — ghi đơn hàng và ghi outbox không bao
giờ lệch nhau

---

## Phase 4: User Story 2 - Sự kiện đơn hàng không bị mất kể cả khi tiến trình sập ngay sau khi ghi dữ liệu (Priority: P1)

**Goal**: Khi tiến trình `orders` sập ngay sau commit nhưng trước khi outbox delivery service gửi
message, khởi động lại vẫn tự động phát sự kiện đó ra ngoài.

**Independent Test**: `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter OrderPlacedOutboxCrashRecoveryTests`
pass — chỉ cần US1's T014 đã tồn tại (dùng lại đúng production code path đó), không phụ thuộc
implementation chi tiết của US3.

### Tests for User Story 2

- [ ] T015 [P] [US2] Viết
      `services/orders/tests/Orders.Api.IntegrationTests/OrderPlacedOutboxCrashRecoveryTests.cs` —
      kịch bản 2-host của research.md Quyết định 4: Host A (chu kỳ quét outbox — `QueryDelay` hoặc
      tương đương — cấu hình dài hơn thời lượng test, T016) gọi `POST /orders` thành công rồi
      `Dispose()` ngay; Host B mới trỏ cùng CSDL + cùng RabbitMQ container; assert đúng
      `OrderPlacedV1` đó được gửi ra sau khi Host B khởi động, không cần thao tác thủ công nào khác —
      phụ thuộc T010, T014; PHẢI FAIL trước khi có T016 (nếu `QueryDelay` mặc định quá ngắn, Host A có
      thể gửi trước khi kịp `Dispose()`, khiến test không mô phỏng đúng "sập trước khi gửi")

### Implementation for User Story 2

- [ ] T016 [US2] Thêm khả năng cấu hình khoảng quét outbox delivery service
      (`QueryDelay`/tương đương của `AddEntityFrameworkOutbox`, T008) qua configuration, để Host A của
      T015 có thể đặt giá trị dài hơn thời lượng bước gọi API — phụ thuộc T008; chạy lại T015, xác
      nhận PASS

**Checkpoint**: User Story 1 VÀ User Story 2 đều hoạt động độc lập — outbox không chỉ ghi đúng mà còn
sống sót qua crash tiến trình

---

## Phase 5: User Story 3 - Consumer nhận trùng sự kiện không gây ra tác dụng phụ trùng lặp (Priority: P2)

**Goal**: Một consumer nhận cùng `OrderPlacedV1` (cùng `EventId`) hai lần chỉ thực sự xử lý đúng 1
lần, dùng cơ chế Consumer Outbox/Inbox thật của MassTransit (`InboxState`), không phải dedupe tự viết.

**Independent Test**: `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter OrderPlacedIdempotentConsumerTests`
pass — chỉ cần US1's T014 đã publish được ít nhất 1 sự kiện thật để phát lại; độc lập với chi tiết
implementation của US2.

### Tests for User Story 3

- [ ] T019 [P] [US3] Viết
      `services/orders/tests/Orders.Api.IntegrationTests/OrderPlacedIdempotentConsumerTests.cs` —
      publish (hoặc redeliver) cùng 1 `OrderPlacedV1` (cùng `EventId`) hai lần vào consumer xác minh
      (T017/T018); assert bảng "đã xử lý" (data-model.md) chỉ có đúng 1 hàng cho `EventId` đó sau cả
      2 lần nhận — phụ thuộc T010, T014, T018; PHẢI FAIL trước khi có T017/T018

### Implementation for User Story 3

- [ ] T017 [US3] Tạo
      `services/orders/tests/Orders.Api.IntegrationTests/Support/OrderPlacedVerificationConsumer.cs`
      — `IConsumer<OrderPlacedV1>` chỉ dùng cho test (không phải nghiệp vụ thật — research.md Quyết
      định 3): khi `Consume` chạy, ghi 1 hàng vào bảng "đã xử lý" (data-model.md) khoá theo
      `context.Message.EventId`
- [ ] T018 [US3] Đăng ký consumer T017 vào host test qua
      `WebApplicationFactory.WithWebHostBuilder` trong
      `services/orders/tests/Orders.Api.IntegrationTests/OrderPlacedIdempotentConsumerTests.cs` (hoặc
      1 fixture riêng dùng chung), cấu hình receive endpoint của nó với
      `UseEntityFrameworkOutbox<OrdersDbContext>()` để dùng `InboxState` khoá theo `MessageId` chặn xử
      lý trùng (data-model.md, research.md Quyết định 3) — phụ thuộc T006, T017; chạy lại T019, xác
      nhận PASS

**Checkpoint**: Cả 3 user story hoạt động độc lập — outbox ghi đúng, sống sót qua crash, và consumer
phía nhận không bao giờ xử lý trùng

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Cập nhật tài liệu liên quan để không ai hiểu lầm phạm vi đã đóng, và xác thực toàn bộ
tính năng liền mạch

- [ ] T020 [P] Cập nhật comment trong
      `services/baskets/src/Baskets.Api/Features/Checkout/BasketCheckedOutMapper.cs` và
      `services/orders/tests/Orders.Api.ContractTests/BasketCheckedOutConsumerPactTests.cs` — làm rõ:
      024-verify-transactional-outbox đã hiện thực outbox cho `OrderPlaced`, nhưng KHÔNG đụng
      `BasketCheckedOut` (vẫn "chưa ai gọi") — tránh để comment cũ ngụ ý SCRUM-31 đã đóng cả 2 vế
- [ ] T021 [P] Cập nhật `docs/adr/0011-checkout-orchestration.md` và bản dịch `.vi.md` — mục "Việc cần
      làm" #2: đánh dấu vế outbox cho `OrderPlaced` đã hoàn thành (024), vế "tạo đơn"/`BasketCheckedOut`
      vẫn chưa (không đánh dấu ADR là "Superseded" — plan.md Complexity Tracking)
- [ ] T022 Chạy `dotnet test services/orders/tests/Orders.Api.IntegrationTests` một lượt cuối, xác
      nhận toàn bộ test mới (T012, T013, T015, T019) PASS — phụ thuộc T014, T016, T018
- [ ] T023 Thực hiện toàn bộ [quickstart.md](./quickstart.md) (cả 3 kịch bản của Jira) một lượt thủ
      công trên môi trường local, ghi lại kết quả thật — phụ thuộc T022

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Không phụ thuộc gì — bắt đầu ngay
- **Foundational (Phase 2)**: Phụ thuộc Setup hoàn tất — CHẶN cả 3 user story
- **User Story 1 (Phase 3)**: Phụ thuộc Foundational hoàn tất
- **User Story 2 (Phase 4)**: Phụ thuộc Foundational HOÀN TẤT VÀ User Story 1's T014 (dùng lại đúng
  production code path publish trong `OrderEndpoints.cs`) — không độc lập hoàn toàn về implementation,
  nhưng độc lập về mặt test/giá trị (kiểm chứng 1 khía cạnh khác của cùng cơ chế)
- **User Story 3 (Phase 5)**: Phụ thuộc Foundational hoàn tất và User Story 1's T014 (cần publish được
  ít nhất 1 sự kiện thật để phát lại) — độc lập với User Story 2
- **Polish (Phase 6)**: Phụ thuộc cả 3 user story hoàn tất

### User Story Dependencies

- **User Story 1 (P1)**: Không phụ thuộc story khác — đây là implementation gốc mà US2/US3 dùng lại
- **User Story 2 (P1)**: Phụ thuộc US1's T014 đã tồn tại (không thể mô phỏng crash-giữa-publish nếu
  chưa có gì publish); độc lập với US3
- **User Story 3 (P2)**: Phụ thuộc US1's T014 đã publish được sự kiện thật để phát lại; độc lập với US2

### Within Each User Story

- Test viết trước, xác nhận FAIL (chưa có implementation) trước khi triển khai
- Foundational (đăng ký MassTransit/outbox) trước mọi story
- Story hoàn tất (test PASS) trước khi coi là "xong" để chuyển tiếp

### Parallel Opportunities

- T001–T005 (Setup) chạy song song (khác file)
- T009, T010, T011 (Foundational) chạy song song với nhau và với T006→T007→T008 (khác file, không phụ
  thuộc nhau)
- T012, T013 (US1 tests, cùng file nhưng 2 test case độc lập) có thể viết song song rồi gộp
- Sau khi US1's T014 xong: US2 (Phase 4) và US3 (Phase 5) triển khai song song được bởi 2 người khác
  nhau (khác file test, không sửa chung code)

---

## Parallel Example: User Story 2 & User Story 3 (sau khi User Story 1 hoàn tất)

```bash
# US2 và US3 có thể triển khai song song bởi 2 người/2 phiên khác nhau:
Task: "Viết OrderPlacedOutboxCrashRecoveryTests.cs cho US2 (T015/T016)"
Task: "Viết OrderPlacedVerificationConsumer.cs + OrderPlacedIdempotentConsumerTests.cs cho US3 (T017-T019)"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Hoàn tất Phase 1: Setup
2. Hoàn tất Phase 2: Foundational (CHẶN cả 3 story)
3. Hoàn tất Phase 3: User Story 1
4. **DỪNG VÀ XÁC THỰC**: `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter OrderPlacedOutboxAtomicityTests`
   pass độc lập
5. Đây đã là giá trị triển khai được ngay: `OrderPlacedV1` được publish thật, đúng nghĩa đen với ghi
   nhận nguyên tử — dù chưa chứng minh được khả năng sống sót qua crash (US2) hay idempotency phía nhận
   (US3)

### Incremental Delivery

1. Setup + Foundational → hạ tầng MassTransit/outbox sẵn sàng trên `orders`
2. Thêm US1 → kiểm thử độc lập → merge/deploy (MVP: `OrderPlaced` được publish, ghi nguyên tử)
3. Thêm US2 → kiểm thử độc lập → merge/deploy (giờ chứng minh được sống sót qua crash tiến trình)
4. Thêm US3 → kiểm thử độc lập → merge/deploy (giờ chứng minh được consumer phía nhận không xử lý trùng)
5. Mỗi story thêm giá trị mà không phá story trước — cả 3 dùng chung 1 production code path (T014)

### Parallel Team Strategy

Với nhiều người triển khai cùng lúc:

1. Cùng hoàn tất Setup + Foundational
2. 1 người hoàn tất User Story 1 trước (bắt buộc — US2/US3 phụ thuộc T014)
3. Sau đó:
   - Người A: User Story 2 (Phase 4)
   - Người B: User Story 3 (Phase 5)
4. Cả 3 story tích hợp độc lập vào cùng 1 production code path

---

## Notes

- [P] = khác file, không phụ thuộc task chưa hoàn thành
- Nhãn [Story] gắn task với đúng user story để truy vết
- Xác nhận test FAIL trước khi implementation, PASS sau khi implementation — không đảo ngược
- Commit sau mỗi task hoặc mỗi nhóm task logic
- Có thể dừng ở bất kỳ Checkpoint nào để xác thực độc lập một story
- Tránh: task mơ hồ, hai task cùng sửa một file cùng lúc, phụ thuộc chéo giữa các story phá vỡ tính
  độc lập ngoài những phụ thuộc đã ghi rõ ở trên (US2/US3 → US1's T014)
