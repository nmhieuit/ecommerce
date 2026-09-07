# Các quyết định về Technical Stack (Technical Stack Decisions)

*(Bản dịch tiếng Việt của [`tech-stack-decisions.md`](tech-stack-decisions.md) — bản gốc tiếng Anh
vẫn được giữ nguyên, không sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn đúng.)*

**Đầu vào cho:** `/engineering:system-design`
**Trạng thái:** Đã chấp thuận — xem từng ADR riêng trong [`docs/adr/`](adr/) để biết bối cảnh, phương
án, và phân tích đánh đổi đầy đủ
**Phạm vi:** Lựa chọn sản phẩm/công cụ trong các danh mục đã được cố định bởi
[constitution.md](../.specify/memory/constitution.md). Các "Ràng buộc Công nghệ và Hạ tầng" của chính
constitution (C#/.NET 10, EF Core + SQL Server + Redis, RabbitMQ + MassTransit, edge gateway→BFF,
React/TS/Vite/TanStack Query, Kubernetes qua Ansible, Jenkins + SonarQube) **không** được quyết định
lại ở đây — chúng là đầu vào, không phải đầu ra, của tài liệu này.

## Bảng quyết định

| # | Vùng quyết định | Đã chọn | Lý do 1 dòng | ADR |
|---|---|---|---|---|
| 1 | Nhà cung cấp định danh | **Duende IdentityServer** | Phương án duy nhất triển khai giống mọi service khác và tiêu thụ thành phần telemetry `ServiceDefaults` dùng chung — runtime Java của Keycloak thì không | [ADR-0001](adr/0001-identity-provider.vi.md) |
| 2 | API gateway | **YARP** | Chạy như 1 app ASP.NET Core; xác thực token và telemetry khớp với mọi service phía sau nó; được bảo trì tích cực hơn Ocelot | [ADR-0002](adr/0002-api-gateway.vi.md) |
| 3 | Cài đặt BFF | **Minimal APIs** | Sinh OpenAPI native, ít nghi thức khớp với "tổng hợp, không tự cài đặt logic nghiệp vụ"; GraphQL bị loại vì mâu thuẫn với Principle II | [ADR-0003](adr/0003-bff-implementation-pattern.vi.md) |
| 4 | Codegen client OpenAPI→TS | **Orval** | Công cụ duy nhất sinh trực tiếp TanStack Query hook — khép khoảng trống Principle IX mà không cần code wrapper viết tay | [ADR-0004](adr/0004-openapi-client-codegen.vi.md) |
| 5 | Định dạng hợp đồng event | **JSON Schema** (package contract dùng chung, không có service registry) | Khớp serialization JSON gốc của MassTransit; hành vi tolerant-reader có miễn phí từ `System.Text.Json`; tránh 1 registry có trạng thái mới | [ADR-0005](adr/0005-event-contract-format.vi.md) |
| 6 | Kiểm thử hợp đồng | **Pact** + Pact Broker tự host | "Làm fail build của bên sản xuất" chính là workflow của Pact, gồm cả theo dõi phụ thuộc liên-service mà nếu không sẽ phải tự xây bằng tay | [ADR-0006](adr/0006-contract-testing-tool.vi.md) |
| 7 | Phân phối secrets | **External Secrets Operator + Vault tự host** | Credential động/dấu vết audit của Vault, tiêu thụ qua K8s Secret thông thường nên không service nào cần tích hợp Vault gốc | [ADR-0007](adr/0007-secrets-delivery.vi.md) |
| 8 | Feature toggle | **Unleash** (tự host) | Tự host như phần còn lại của nền tảng; admin UI có sẵn phục vụ "rollback không cần redeploy" của Principle X tốt hơn 1 bảng tự xây | [ADR-0008](adr/0008-feature-toggle-system.vi.md) |
| 9 | Nền tảng design-system | **Radix UI + Tailwind CSS**, tài liệu qua Storybook | Styling lúc build khớp ngân sách CWV/bundle; khả năng tiếp cận có sẵn của Radix giảm gánh nặng test WCAG 2.2 AA | [ADR-0009](adr/0009-design-system-foundation.vi.md) |
| 10 | Công cụ monorepo frontend | **Turborepo** | Caching tăng dần, cấu hình thấp giữ CI nhanh cho phát triển trunk-based, PR nhỏ thường xuyên, không cần mô hình khung nặng nề của Nx | [ADR-0010](adr/0010-frontend-monorepo-tooling.vi.md) |

## Bản kê stack đầy đủ (cố định + đã chọn)

| Tầng | Công nghệ |
|---|---|
| Ngôn ngữ/runtime backend | C# / .NET 10 (cố định) |
| Persistence | EF Core trên SQL Server, database/schema theo từng tenant; Redis cho giỏ hàng + caching (cố định) |
| Messaging | RabbitMQ qua MassTransit, outbox + retry/DLQ (cố định) |
| Định dạng hợp đồng event | JSON Schema, package contract dùng chung |
| Nhà cung cấp định danh | Duende IdentityServer |
| API gateway | YARP |
| BFF | ASP.NET Core Minimal APIs |
| Framework frontend | React + TypeScript strict + Vite (cố định) |
| Trạng thái phía server | TanStack Query (cố định) |
| Client API sinh ra | Orval (OpenAPI → TS + TanStack Query hook) |
| Design system | Radix UI + Tailwind CSS, tài liệu hoá trong Storybook |
| Công cụ monorepo frontend | Turborepo + pnpm workspace |
| Kiểm thử hợp đồng | Pact + Pact Broker tự host |
| Secrets | External Secrets Operator + HashiCorp Vault tự host |
| Feature toggle | Unleash (tự host) |
| Nền tảng | Kubernetes, cấp phát qua Ansible (cố định) |
| CI/CD & cổng chất lượng | Jenkins + SonarQube (cố định) |
| Observability | OpenTelemetry → Elastic stack qua `ServiceDefaults` dùng chung (cố định) |

## Hạ tầng mới được đưa vào

Đây là các service có trạng thái mà lựa chọn stack này thêm vào ngoài danh sách cố định của
constitution — mỗi cái là 1 cam kết vận hành thật sự, không phải 1 lựa chọn cấu hình:

- **Duende IdentityServer** — chạy như 1 service bình thường khác (không phải 1 *loại* hạ tầng mới,
  nhưng có chi phí bản quyền)
- **HashiCorp Vault** (+ External Secrets Operator) — bề mặt HA/backup/DR mới
- **Pact Broker** — nhẹ, nhưng là mới
- **Unleash** — dựa trên Postgres, mới

## Việc cần theo dõi tiếp cho System Design

- Mô hình ánh xạ tenant → identity-client (ADR-0001) cần được thiết kế trước khi
  `/engineering:system-design` có thể hoàn thiện luồng auth
- Chính sách credential động của Vault theo từng loại dependency (SQL Server, RabbitMQ) chưa được
  định nghĩa
- GraphQL đã bị loại tường minh cho BFF (ADR-0003) vì lý do hiến pháp — nếu nỗi đau tổng hợp
  theo-hình-dạng-từng-client xuất hiện trong lúc thiết kế hệ thống, đó là tín hiệu để đưa ra thảo luận
  tu chính hiến pháp, không phải để lách qua nó
