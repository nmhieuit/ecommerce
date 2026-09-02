# Kiến trúc: Consumer-driven contract test giữa BFF/service

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-21 ("[CONTRACT-2] Consumer-driven contract tests across BFF/service
boundaries"), đặc tả tại
[`specs/011-consumer-contract-tests/`](../../specs/011-consumer-contract-tests/). Quyết định kiến
trúc gốc: [ADR-0006](../adr/0006-contract-testing-tool.md) (chọn PactNet, rollout theo giai đoạn —
thí điểm một event boundary trước khi áp dụng toàn nền tảng). Đây là các `*.ContractTests` project
(`PactProviderHost.cs`) mà [014-identity-server-auth](../../specs/014-identity-server-auth/) sau này
phải vá riêng (tắt `FallbackPolicy` cho 3 provider host) vì Pact-recorded interaction không mang
Authorization header.

**Trạng thái xác minh**: 4 checkpoint trong `tasks.md`: Setup (project + thư mục `pacts/` build được)
→ US1 (FR-001–003/005/006 đúng trên cả 3 boundary HTTP) → US2 (FR-004–006 đúng cho event pilot) → US3
(FR-007–009 đúng, SC-001/003/004 đóng lại — coverage audit đầy đủ 3 story).

## 1. Kiến trúc tổng thể

**Phạm vi "thin slice"** (spec Assumptions): 3 boundary HTTP (BFF↔products, BFF↔baskets, BFF↔orders)
+ 1 cặp event thí điểm (`BasketCheckedOut`, baskets là producer ↔ orders là consumer — research.md
Decision 4), rút ra từ rollout theo giai đoạn của ADR-0006.

- **Consumer** (BFF cho HTTP; orders cho event) khai báo kỳ vọng, sinh ra một file Pact.
- **Provider** (products/baskets/orders cho HTTP; baskets cho event) tự verify **hành vi HTTP thật**
  (hoặc **payload event thật được construct**, research.md Decision 3 — KHÔNG qua MassTransit, vì
  chưa có publisher/consumer thật nào) của chính mình đối chiếu với file Pact đó, **trong build của
  chính provider** — đây là nguyên lý cốt lõi FR-005: lỗi hiện ở build của bên PHÁT, không phải bên
  NHẬN.

## 2. Quyết định kỹ thuật đáng chú ý (research.md)

| # | Quyết định |
|---|---|
| 1 | Thư viện contract testing: PactNet |
| 2 | Trao đổi file Pact qua filesystem, KHÔNG dùng Pact Broker ở feature này |
| 3 | Event pilot gọi thẳng đường xây dựng payload, KHÔNG qua MassTransit |
| 4 | Cặp event thí điểm: baskets (producer) ↔ orders (consumer) cho `BasketCheckedOut` |
| 5 | Provider verification chạy in-process, KHÔNG cần Testcontainers/Docker |

Quyết định 5 đáng chú ý về mặt vận hành: verify hợp đồng KHÔNG cần một database thật hay container nào
— nhanh, chạy được ở mọi build, khác hẳn `*.IntegrationTests` (Testcontainers SQL Server thật). Đây là
lý do 3 `PactProviderHost.cs` (products/baskets/orders) mà [014's Phase 4/5](../../specs/014-identity-server-auth/tasks.md)
sau này phải `PostConfigure<AuthorizationOptions>(o => o.FallbackPolicy = null)` — Pact-recorded
interaction không mang `Authorization` header thật (vì được ghi trước khi có xác thực token thật),
verification cần tắt riêng fallback policy cho đúng provider host đó, không ảnh hưởng service thật.

## 3. Giới hạn phạm vi đã biết

- Phạm vi chỉ dừng ở 4 boundary của "thin slice" — mở rộng ra boundary khác là việc tương lai, ngoài
  phạm vi này.
- Event boundary được verify **không cần broker thật/không cần delivery đầu-cuối** (spec Assumptions)
  — vì chưa service nào thật sự publish/consume `BasketCheckedOut`/`OrderPlaced` (nhất quán với
  [008](../../specs/008-versioned-event-schemas/) và [010](../../specs/010-testcontainers-integration-tests/)).
  Khi hạ tầng messaging thật được đấu nối (SCRUM-31), cặp contract test này là điểm khởi đầu, không
  phải điểm kết thúc.

## 4. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/011-consumer-contract-tests-component.drawio`](../diagrams/011-consumer-contract-tests-component.drawio)
- Sơ đồ trình tự (consumer khai báo kỳ vọng → provider tự verify hành vi thật trong build của chính
  nó, gồm nhánh sai lệch chặn build của bên phát): [`docs/diagrams/011-consumer-contract-tests-sequence.drawio`](../diagrams/011-consumer-contract-tests-sequence.drawio)
- Sơ đồ luồng nghiệp vụ đơn giản hoá (đi kèm tài liệu PO):
  [`docs/diagrams/011-consumer-contract-tests-flow-nghiep-vu.drawio`](../diagrams/011-consumer-contract-tests-flow-nghiep-vu.drawio)
