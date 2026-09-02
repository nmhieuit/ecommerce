# Kiến trúc: Hạ tầng integration test bằng Testcontainers

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-20 ("[CONTRACT-2] Integration tests via Testcontainers"), đặc tả tại
[`specs/010-testcontainers-integration-tests/`](../../specs/010-testcontainers-integration-tests/).

**Trạng thái xác minh**: 4 checkpoint trong `tasks.md`, mỗi user story một checkpoint riêng: Setup
(project mới build được) → US1 (FR-001/FR-002/SC-002 chứng minh đúng trên cả 4 service đã audit) →
US2 (FR-003/FR-005, Redis fixture sẵn sàng tái sử dụng) → US3 (FR-004/FR-006/FR-008/SC-004, cả hai
fixture mới sẵn sàng).

## 1. Kiến trúc tổng thể

Thư viện mới `shared/IntegrationTestSupport` (research.md Decision 3) — nơi duy nhất chứa
`RedisFixture.cs`/`RabbitMqFixture.cs`, tham chiếu qua `ProjectReference` giống hệt tiền lệ
`shared/EventContracts` (một định nghĩa, nhiều nơi dùng). **Đây chính là thư viện
[014-identity-server-auth](../../specs/014-identity-server-auth/) sau này mở rộng thêm
`TestJwtBearer.cs` vào** — cùng nguyên tắc "hạ tầng test dùng chung, không copy-paste mỗi service".

`SqlServerFixture.cs` hiện tại vẫn bị copy-paste nguyên văn ở cả 4 service test project
(`baskets`/`orders`/`parties`/`products`) — feature này CHỈ audit lại pattern đó (theo đúng FR-001/
FR-002), KHÔNG gộp nó vào thư viện chung; việc gộp được ghi nhận là follow-up riêng, không âm thầm làm
kèm.

## 2. Quyết định kỹ thuật đáng chú ý (research.md)

| # | Quyết định |
|---|---|
| 3 | Fixture mới sống ở `shared/IntegrationTestSupport`, không copy-paste mỗi service |
| 4 | "Fail loudly" tận dụng đúng hành vi mặc định của Testcontainers (`StartAsync()` tự throw khi wait strategy timeout) — không viết logic health-check tuỳ chỉnh nào |
| 5 | Chứng minh RabbitMQ chết giữa test fail nhanh: `ContinuationTimeout` ngắn của client + một `Task.WhenAny` giới hạn 30 giây bọc quanh assertion, để CHÍNH BÀI TEST không treo dù client mặc định có generous đến đâu |
| 6 | KHÔNG đưa MassTransit vào — chưa có publisher/consumer thật nào để nó trừu tượng hoá, thêm vào bây giờ là scope creep |

## 3. Chứng minh "fail loudly" không cần code mới

Decision 4 là một phát hiện đáng chú ý: hành vi "thất bại rõ ràng khi container không khoẻ, không bao
giờ âm thầm skip" (FR-007) **đã là hành vi mặc định của chính thư viện Testcontainers** — mỗi builder
(`MsSqlBuilder`, `RedisBuilder`, `RabbitMqBuilder`) đều có sẵn một wait strategy tự throw khi timeout;
xUnit tự nhiên biến exception đó thành lỗi khởi tạo fixture, fail toàn bộ test trong collection thay
vì skip. Task của US1 chỉ cần CHỨNG MINH hành vi này, không phải xây thêm plumbing nào.

## 4. Giới hạn phạm vi đã biết

- **Không có chức năng nghiệp vụ mới nào dùng Redis/RabbitMQ** (spec FR-009) — hai fixture này đứng
  chờ tính năng tương lai đầu tiên cần chúng, giống hệt cách
  [008-versioned-event-schemas](../../specs/008-versioned-event-schemas/) đứng chờ.
- Chịu lỗi broker toàn diện (retry, circuit breaker cho outbound call) **ngoài phạm vi** — thuộc
  SCRUM-30 (Phase 4). Feature này chỉ chứng minh CHÍNH BÀI TEST không treo, không phải chính sách
  resilience cho hệ thống thật.
- Việc gộp 4 bản copy-paste của `SqlServerFixture.cs` vào thư viện chung KHÔNG nằm trong phạm vi này —
  ghi nhận là follow-up, chưa quyết định.

## 5. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/010-testcontainers-integration-tests-component.drawio`](../diagrams/010-testcontainers-integration-tests-component.drawio)
- Sơ đồ trình tự (fixture khởi động container thật → wait strategy → fail loudly hoặc chạy test thật,
  gồm nhánh RabbitMQ chết giữa test): [`docs/diagrams/010-testcontainers-integration-tests-sequence.drawio`](../diagrams/010-testcontainers-integration-tests-sequence.drawio)
- Sơ đồ luồng nghiệp vụ đơn giản hoá (đi kèm tài liệu PO):
  [`docs/diagrams/010-testcontainers-integration-tests-flow-nghiep-vu.drawio`](../diagrams/010-testcontainers-integration-tests-flow-nghiep-vu.drawio)
