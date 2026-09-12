# Implementation Plan: Xác minh outbox pattern giao dịch trên dịch vụ phát sự kiện đơn hàng

**Branch**: `code/Verify-transactional-outbox-on-the-order-publisher` | **Date**: 2026-09-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/024-verify-transactional-outbox/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Rà soát trực tiếp mã nguồn (research.md Quyết định 0) xác nhận: hệ thống hiện **không có bất kỳ hạ
tầng nhắn tin thật nào** — không gói MassTransit, không outbox, `POST /orders` chỉ lưu CSDL rồi trả về,
`OrderPlacedV1` mới chỉ là một hợp đồng (schema + record) chưa từng được publish. RabbitMQ container đã
tồn tại sẵn (docker-compose) nhưng "chưa ai nối dây" — đúng như comment tại đó đã dự đoán, tính năng
này là "the story that first needs one".

Cách tiếp cận: (1) thêm `MassTransit`/`MassTransit.RabbitMQ`/`MassTransit.EntityFrameworkCore` (9.2.1,
đã xác minh tương thích .NET 10/EF Core 10 trên NuGet Gallery) vào `orders`; (2) `POST /orders` ghi bản
ghi outbox (Bus Outbox của MassTransit) trong cùng transaction EF Core với việc lưu đơn hàng, và một
outbox delivery service có sẵn của thư viện tự gửi `OrderPlacedV1` ra RabbitMQ, kể cả sau khi tiến
trình khởi động lại; (3) một consumer xác minh idempotency (dùng Consumer Outbox/Inbox thật của
MassTransit) chạy trong chính bộ test tích hợp — không phải một nghiệp vụ sản xuất mới; (4) một kịch
bản test 2-host mô phỏng đúng nghĩa đen "kill tiến trình giữa lúc publish, restart" của Jira.

**Quyết định phạm vi quan trọng nhất** (research.md Quyết định 1): tính năng này **không** dựng lại
saga checkout đầy đủ mà ADR-0011 dự tính (baskets publish `BasketCheckedOut` → orders consume để tạo
đơn → orders publish `OrderPlaced` → baskets consume để xoá giỏ). Chỉ đóng đúng 3 tiêu chí chấp nhận
thật sự của Jira SCRUM-31 (ghi nguyên tử, phục hồi sau crash, consumer idempotent cho riêng
`OrderPlaced`). Lý do kỹ thuật cụ thể: `OrderPlacedV1` không mang định danh khách hàng/giỏ hàng, nên
dùng nó để lái nghiệp vụ "xoá giỏ hàng" thật đòi hỏi một phiên bản hợp đồng mới (`OrderPlacedV2`) — một
hạng mục lớn hơn phạm vi được yêu cầu. ADR-0011 và deviation Principle IV ở
`specs/004-minimal-shopping-spa/plan.md` **không** bị đóng bởi tính năng này (xem Complexity Tracking).

## Technical Context

**Language/Version**: C#/.NET 10 — không có ngôn ngữ ứng dụng mới.

**Primary Dependencies**: `MassTransit` + `MassTransit.RabbitMQ` + `MassTransit.EntityFrameworkCore`
9.2.1 (mới, xác minh trên NuGet Gallery — research.md Quyết định 2): `MassTransit.RabbitMQ` phụ thuộc
`RabbitMQ.Client >= 7.2.2`, khớp đúng phiên bản đã pin sẵn trong `Directory.Packages.props`;
`MassTransit.EntityFrameworkCore` (target .NET 10) phụ thuộc `Microsoft.EntityFrameworkCore.Relational
>= 10.0.0`, khớp EF Core 10.0.0 đã dùng — không xung đột phiên bản transitive nào.

**Storage**: SQL Server (đã có, `OrdersDbContext`) — thêm bảng outbox (`OutboxMessage`, `OutboxState`)
do `MassTransit.EntityFrameworkCore` định nghĩa sẵn qua `AddEntityFrameworkOutbox`, cộng bảng
`InboxState` cho consumer xác minh (Quyết định 3) — không phải entity tự thiết kế, chỉ một migration
EF Core mới áp dụng các entity thư viện cung cấp.

**Testing**: xUnit + `Testcontainers.MsSql` (đã có, `SqlServerFixture` của
`Orders.Api.IntegrationTests`) + `Testcontainers.RabbitMq` (đã có ở `shared/IntegrationTestSupport`,
lần đầu được một service thật sử dụng thay vì chỉ smoke-test). Kịch bản crash dùng 2
`WebApplicationFactory<Program>` trỏ cùng 1 cặp container (research.md Quyết định 4) — không cần hạ
tầng Docker/K8s mới ngoài 2 container đã có sẵn khuôn mẫu trong repo.

**Target Platform**: RabbitMQ container đã tồn tại sẵn trong `docker-compose.yml`/`.local.yml`/
`.debug.yml` (chưa ai nối dây); bổ sung 1 service RabbitMQ dùng chung vào `docker-compose.deps.yml`
(khác per-service `*-db` — message broker là hạ tầng chia sẻ) và biến kết nối cho `orders-api`. Không
có hạ tầng Kubernetes/CI mới ngoài các container đã có.

**Project Type**: Bổ sung vào 1 service hiện có (`orders`) — không phải service runtime mới. Không
đụng tới `baskets` hay `bff` (xem quyết định phạm vi ở Summary).

**Performance Goals**: Không có mục tiêu riêng — publish qua outbox là fire-and-forget nội bộ trong
transaction đã có, không được phép làm chậm response `201` hiện tại của `POST /orders` (xem Constraints).

**Constraints**: KHÔNG được đổi hành vi phản hồi HTTP hiện có của `POST /orders` hay `/bff/checkout`
đối với người gọi (spec FR-007) — response vẫn trả `201` ngay sau khi transaction CSDL commit; việc
publish `OrderPlacedV1` xảy ra bất đồng bộ qua outbox delivery service, không chặn response.
KHÔNG đổi `OrderPlacedV1` (không thêm trường) — mọi consumer xác minh trong tính năng này chỉ đọc các
trường đã có. KHÔNG đụng tới `BasketCheckedOutV1`, `BasketCheckedOutMapper`, hay bất kỳ hành vi nào của
`baskets`/`bff` liên quan tới checkout (quyết định phạm vi ở Summary).

**Scale/Scope**: 1 service sửa thật sự (`orders`: `Program.cs`, `OrdersDbContext`, `OrderEndpoints.cs`,
1 migration mới, `Orders.Api.csproj`); 1 project test tích hợp mở rộng (`Orders.Api.IntegrationTests`,
+ 1 consumer xác minh nội bộ + các test case mới); `Directory.Packages.props` +3 dòng gói;
`docker-compose.yml`/`docker-compose.deps.yml` +1 service RabbitMQ dùng chung mỗi file; 1 ghi chú cập
nhật trên `Order.cs` (xoá docstring đã lỗi thời) và trên `BasketCheckedOutMapper.cs`/
`BasketCheckedOutConsumerPactTests.cs` (làm rõ SCRUM-31, như đã hiện thực, không đụng tới
`BasketCheckedOut`).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Áp dụng thế nào | Trạng thái |
|---|---|---|
| I. Service Autonomy and Bounded Context | Không đổi quyền sở hữu dữ liệu; `orders` vẫn là chủ sở hữu duy nhất của bảng outbox/inbox của chính nó (nằm trong `OrdersDbContext`, cùng CSDL `orders`). Không service nào đọc/ghi CSDL của service khác. | PASS |
| II. Contract-First Integration | Không đổi `OrderPlacedV1` — dùng nguyên trạng hợp đồng đã có, viết trước bởi 008-versioned-event-schemas. Không có hợp đồng HTTP/event mới nào cần review trước khi code (Bus Outbox/Inbox là hạ tầng truyền tải, không phải hợp đồng nghiệp vụ). | PASS |
| III. Test-First Development | Toàn bộ 4 đảm bảo (ghi nguyên tử, rollback-safety, phục hồi sau crash, idempotent consumer) được viết thành test tích hợp dùng Testcontainers thật (SQL Server + RabbitMQ) trước khi coi là hoàn thành — không mock bus, không in-memory outbox. | PASS (thực hiện tại Phase 2/implementation) |
| IV. Event-Driven by Default | Đây chính là nội dung cốt lõi của nguyên tắc này cho `orders`: outbox pattern bắt buộc cho mọi publisher nay được hiện thực thật cho `OrderPlacedV1`. **Nhưng** deviation đã ghi nhận từ 004/ADR-0011 (checkout là 2 bước đồng bộ do BFF điều phối, chưa phải saga) **không bị đóng bởi tính năng này** — quyết định phạm vi có chủ ý (research.md Quyết định 1), không phải bỏ sót. | PASS cho phần `OrderPlaced` (mục tiêu chính) — deviation Principle IV pre-existing ở bước "tạo đơn"/`BasketCheckedOut` vẫn còn, xem Complexity Tracking |
| V. Tenant Isolation Is a Security Boundary | Không liên quan trực tiếp — `OrderPlacedV1` đã mang `TenantId` từ trước; không có đường dẫn dữ liệu tenant mới nào được mở. | N/A |
| VI. Secure by Default | Không có endpoint HTTP mới, không có secret mới lộ ra ngoài — connection string RabbitMQ được truyền qua biến môi trường/cluster secret store giống các connection string CSDL hiện có (018-cluster-secret-store). | PASS |
| VII. Observable by Default | Thêm `.AddSource("MassTransit")`/`.AddMeter("MassTransit")` vào `ServiceDefaultsExtensions` (cùng khuôn mẫu đã làm cho "Polly" ở 020), để hoạt động publish/consume/outbox-delivery hiện lên trong OTel traces/metrics — một feature "MUST be debuggable in production from telemetry alone" không thể bỏ qua bước này. | PASS |
| VIII. Performance and Resilience Budgets | Publish qua outbox không thêm độ trễ đồng bộ vào `POST /orders` (Constraints ở trên); outbox delivery service chạy nền, không nằm trên đường dẫn yêu cầu tới hạn (critical path) của bất kỳ SLO nào đã khai báo ở 021. | PASS |
| IX. Frontend Discipline | Không liên quan — không đụng frontend. | N/A |
| X. Toggle-Gated, Reversible Delivery | Migration EF Core mới (thêm bảng outbox/inbox) là additive (bảng mới hoàn toàn, không sửa bảng `Orders` hiện có) — thoả expand/contract. Rollback là revert migration + gỡ đăng ký MassTransit trong `Program.cs`; `POST /orders` vẫn hoạt động đúng như trước nếu rollback (outbox chỉ là một bước *thêm vào* sau `SaveChangesAsync`, không thay thế nó). | PASS |

**Gate result**: tiếp tục, với 1 mục ở Complexity Tracking (deviation Principle IV pre-existing, không
do tính năng này tạo ra và không được đóng bởi tính năng này).

## Project Structure

### Documentation (this feature)

```text
specs/024-verify-transactional-outbox/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
├── checklists/          # /speckit-specify output (đã có)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
Directory.Packages.props                          # sửa: +MassTransit, +MassTransit.RabbitMQ, +MassTransit.EntityFrameworkCore (9.2.1)

services/orders/src/Orders.Api/
├── Orders.Api.csproj                              # sửa: thêm 3 PackageReference trên
├── Program.cs                                     # sửa: đăng ký MassTransit (RabbitMQ transport + AddEntityFrameworkOutbox<OrdersDbContext> + UseBusOutbox)
├── Data/
│   ├── OrdersDbContext.cs                         # sửa: modelBuilder.AddTransactionalOutboxEntities() (bảng OutboxMessage/OutboxState)
│   ├── Order.cs                                   # sửa: xoá docstring lỗi thời "the outbox table... is absent"
│   └── OrdersDbContextFactory.cs                  # không đổi (đã tồn tại cho design-time migration)
├── Migrations/
│   └── <timestamp>_AddTransactionalOutbox.cs      # mới: bảng outbox do MassTransit.EntityFrameworkCore định nghĩa
└── Features/Orders/
    └── OrderEndpoints.cs                          # sửa: publish OrderPlacedV1 (từ request items + order đã lưu) trong cùng transaction, sau PlaceFrom trước/cùng SaveChangesAsync

services/orders/tests/Orders.Api.IntegrationTests/
├── Orders.Api.IntegrationTests.csproj             # sửa: +ProjectReference tới EventContracts, dùng RabbitMqFixture có sẵn của IntegrationTestSupport
├── OrderPlacedOutboxAtomicityTests.cs             # mới — FR-001/FR-002: ghi nguyên tử order+outbox; rollback → không outbox record
├── OrderPlacedOutboxCrashRecoveryTests.cs         # mới — FR-003/FR-004: kịch bản 2-host (research.md Quyết định 4)
├── OrderPlacedIdempotentConsumerTests.cs          # mới — FR-005: consumer xác minh (Consumer Outbox/Inbox), nhận trùng EventId → no-op
└── Support/
    └── OrderPlacedVerificationConsumer.cs         # mới — consumer chỉ dùng cho test, đăng ký qua WithWebHostBuilder (research.md Quyết định 3)

shared/ServiceDefaults/
└── ServiceDefaultsExtensions.cs                   # sửa: thêm .AddSource("MassTransit")/.AddMeter("MassTransit") vào pipeline OTel đã có

docker-compose.yml                                 # sửa: thêm biến kết nối RabbitMQ cho orders-api, depends_on rabbitmq healthy
docker-compose.deps.yml                            # sửa: thêm 1 service rabbitmq dùng chung (không phải per-service)

docs/adr/0011-checkout-orchestration.md            # sửa: ghi chú "Việc cần làm #2" — outbox cho OrderPlaced đã có (024), BasketCheckedOut vẫn chưa (không đóng ADR)
docs/adr/0011-checkout-orchestration.vi.md         # sửa: bản dịch tương ứng

services/baskets/src/Baskets.Api/Features/Checkout/BasketCheckedOutMapper.cs           # sửa: cập nhật comment — làm rõ 024 không đụng BasketCheckedOut, vẫn "chưa ai gọi"
services/orders/tests/Orders.Api.ContractTests/BasketCheckedOutConsumerPactTests.cs    # sửa: cập nhật comment tương ứng
```

**Structure Decision**: Không tạo service runtime mới, không đụng `baskets` hay `bff` (research.md
Quyết định 1). Toàn bộ thay đổi nghiệp vụ nằm gọn trong `orders` (thêm outbox vào publisher đã có, theo
đúng vertical-slice hiện tại — publish nằm ngay trong `OrderEndpoints.cs`, không tách một "Application
layer" mới mà constitution Principle I yêu cầu phải biện minh nếu dùng tới). Consumer xác minh idempotency
sống trong bộ test tích hợp (`Support/OrderPlacedVerificationConsumer.cs`), không phải một dự án hay
service sản xuất mới — tránh phức tạp không tương xứng (Principle I) cho một nhu cầu chỉ để xác minh.
Các sửa đổi ở `docker-compose*.yml`, `ServiceDefaults`, và 2 comment lỗi thời là các thay đổi hạ tầng/tài
liệu đi kèm bắt buộc, không phải mở rộng phạm vi nghiệp vụ.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| **Principle IV (pre-existing, không do tính năng này tạo ra)** — checkout vẫn là 2 bước đồng bộ do BFF điều phối (tạo đơn qua HTTP, xoá giỏ hàng qua HTTP); `BasketCheckedOutV1` vẫn chưa được publish/consume. | Đã ghi nhận từ `specs/004-minimal-shopping-spa/plan.md` Complexity Tracking và ADR-0011, đánh dấu "closed by SCRUM-18/SCRUM-31" — nhưng 3 tiêu chí chấp nhận thật sự của Jira SCRUM-31 chỉ yêu cầu outbox cho `OrderPlaced`, không yêu cầu dựng saga đầy đủ. Đóng trọn ADR-0011 đòi hỏi thêm định danh khách hàng/giỏ hàng vào hợp đồng sự kiện (`OrderPlacedV2`) và đổi UX xác nhận đơn từ đồng bộ sang bất đồng bộ — vượt xa phạm vi được yêu cầu (research.md Quyết định 1). | Dựng saga đầy đủ trong tính năng này để đóng hẳn ADR-0011: bị loại vì nhân quy mô lên nhiều lần, đổi hợp đồng sự kiện đã publish, và đổi hành vi phản hồi mà spec FR-007 yêu cầu giữ nguyên — không có tiêu chí chấp nhận nào của Jira SCRUM-31 đòi hỏi việc này. **Time-bound**: còn lại một story riêng (nếu được ưu tiên) để hoàn tất phần "tạo đơn"/`BasketCheckedOut` của ADR-0011; `docs/adr/0011-checkout-orchestration.md` được cập nhật ghi chú tình trạng một phần này, không đánh dấu "Superseded". |

## Re-check sau Phase 1 (post-design)

Thiết kế ở `data-model.md`/`contracts/`/`quickstart.md` không phát sinh entity, hợp đồng, hay đường dẫn
dữ liệu tenant mới nào ngoài những gì đã đánh giá ở Constitution Check trên — toàn bộ bảng mới
(`OutboxMessage`, `OutboxState`, `InboxState`) do thư viện `MassTransit.EntityFrameworkCore` định nghĩa,
nằm trong đúng CSDL `orders` mà `orders` đã sở hữu (Principle I), và `OrderPlacedV1` không đổi
(Principle II). Không có gate nào bị lật lại; kết quả Constitution Check giữ nguyên.
