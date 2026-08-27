# Tài liệu thiết kế hệ thống (System Design)

*Đối tượng đọc: kiến trúc sư (architect) dùng để giải thích cho đội phát triển (development team) hiểu kiến trúc hệ thống. Đi kèm sơ đồ trực quan tại [`diagrams/kien-truc-3-nhom.drawio`](diagrams/kien-truc-3-nhom.drawio) (mở bằng [app.diagrams.net](https://app.diagrams.net) hoặc VS Code có extension draw.io).*

*Tài liệu này khác với [`system-design.md`](system-design.md) (đã có sẵn trong repo, mô tả kiến trúc MỤC TIÊU đầy đủ 6 service theo góc nhìn C4). Tài liệu này vẽ **cả kiến trúc thực tế đang chạy lẫn phần mục tiêu tương lai trên cùng một sơ đồ**, và phân loại thành phần theo 3 nhóm sở hữu/vận hành thay vì theo vai trò C4.*

---

## 1. Cách đọc sơ đồ đi kèm — quy ước phân 3 nhóm

File [`diagrams/kien-truc-3-nhom.drawio`](diagrams/kien-truc-3-nhom.drawio) có 2 trang (tab ở dưới cùng khi mở bằng draw.io):

- **Trang 1 — "Kiến trúc phân 3 nhóm"**: toàn cảnh hệ thống.
- **Trang 2 — "Sequence — Checkout"**: luồng đặt hàng đầu-cuối (mục 3 bên dưới).

Ở Trang 1, mọi khối được tô theo đúng 3 nhóm sau — đây là cách phân loại theo **ai sở hữu/vận hành thành phần đó**, không phải theo chức năng kỹ thuật:

| Nhóm | Màu | Ý nghĩa | Ví dụ trong hệ thống |
|---|---|---|---|
| 🟩 **Trong repo** | Xanh lá | Code do dự án này viết ra, nằm trong `services/` hoặc `shared/`, đội phát triển toàn quyền sửa | 4 service nghiệp vụ, Gateway, BFF, `shared/ServiceDefaults`, `shared/Tenancy`, `shared/EventContracts` |
| 🟦 **3rd-party library** | Xanh dương | Thư viện/gói phần mềm được tham chiếu vào code trong repo (qua NuGet), **chạy lồng bên trong tiến trình của service**, không phải một tiến trình/container riêng | YARP, Entity Framework Core, OpenTelemetry SDK, Microsoft.Extensions.Http.Resilience, PactNet |
| 🟧 **External component** | Cam | Thành phần chạy **tách biệt, ngoài tiến trình của service** — container riêng, dịch vụ ngoài, hoặc (khi lên production thật) một dịch vụ cloud/SaaS quản lý | SQL Server, Redis, RabbitMQ, OTel Collector, Jenkins, SonarQube |

Phần khung nét đứt màu đỏ ở rìa sơ đồ là **kiến trúc mục tiêu tương lai, chưa được xây** (Logistics, Invoices, Identity Server, Vault, Unleash, Pact Broker, Elastic Stack) — vẽ kèm để đội phát triển thấy hướng đi dài hạn, nhưng **không được hiểu nhầm là đang chạy**.

> **Vì sao "external component" ghi thêm "cloud/SaaS"?** Dự án hiện chạy toàn bộ bằng Docker Compose trên máy cá nhân (container tự host), không dùng dịch vụ cloud thật nào. Nhưng về vai trò kiến trúc, các thành phần này (CSDL, hàng đợi tin nhắn, hạ tầng CI) đóng đúng vai trò mà một dịch vụ cloud/SaaS quản lý (managed service) sẽ đóng nếu triển khai thật — ví dụ SQL Server container hôm nay tương đương một Azure SQL Database khi lên production. Sơ đồ ghi chú rõ điều này ở từng khối.

## 2. Kiến trúc tổng quan (dành cho đội phát triển)

### 2.1 Trong repo — 6 thành phần đang chạy thật

| Thành phần | Vai trò | Cổng công bố ra ngoài |
|---|---|---|
| **Gateway.Api** | Cổng vào duy nhất, xác thực (giả lập), gắn `X-Tenant-Id`/`X-Subject-Id`/`X-Correlation-Id`, chuyển tiếp 100% request sang BFF (không có route nào đi thẳng tới service nghiệp vụ — bị chặn bởi test tự động) | `5300` (duy nhất công bố ra ngoài mặc định) |
| **Bff.Api** | Tổng hợp cho giao diện web: gọi song song/tuần tự 4 service nghiệp vụ, là nơi duy nhất điều phối luồng Checkout | `5301` (chỉ mở ở chế độ debug) |
| **Products.Api** | Danh sách sản phẩm | `5088` (debug) |
| **Baskets.Api** | Giỏ hàng theo người gọi | `5188` (debug) |
| **Orders.Api** | Tạo/đọc đơn hàng | `5041` (debug/demo) |
| **Parties.Api** | Tra cứu khách hàng/đối tác | `5204` (debug) |

4 service nghiệp vụ **hoàn toàn không biết đến nhau** — không service nào gọi service nào, không tham chiếu code chéo nhau. Duy nhất BFF được phép gọi cả 4. Đây là nguyên tắc kiến trúc cố định (Principle I — Service Autonomy), được một bộ test tự động canh giữ: `tests/CrossServiceIsolation.Tests`.

Ngoài 6 service trên, còn 3 thư viện dùng chung (không phải service, không tự chạy độc lập):

- `shared/ServiceDefaults` — OpenTelemetry (log/trace/metric) + correlation-id, dùng chung cho cả 6 service.
- `shared/Tenancy` — bắt buộc mọi request phải có `X-Tenant-Id` + `X-Subject-Id` trước khi chạm tới cơ sở dữ liệu.
- `shared/EventContracts` — định nghĩa 2 schema sự kiện (`OrderPlacedV1`, `BasketCheckedOutV1`) — **đã định nghĩa, chưa có ai publish/consume thật** (xem mục 2.3).

### 2.2 3rd-party library chính (chạy lồng trong service, không phải container riêng)

| Thư viện | Dùng ở đâu | Vai trò |
|---|---|---|
| `Yarp.ReverseProxy` | Gateway | Định tuyến request |
| `Microsoft.EntityFrameworkCore` + `.SqlServer` | 4 service nghiệp vụ | ORM, truy cập SQL Server |
| `Microsoft.Extensions.Http.Resilience` | BFF | Timeout/retry/circuit-breaker khi gọi 4 service |
| `OpenTelemetry.*` | Cả 6 service (qua `ServiceDefaults`) | Xuất log/trace/metric ra OTel Collector |
| `AspNetCore.HealthChecks.SqlServer` | 4 service nghiệp vụ | Endpoint `/health/ready` |
| `PactNet`, `Testcontainers.*`, `xunit` | Chỉ các dự án test | Contract test, integration test — không chạy trong service thật |

**Không có** trong hệ thống hiện tại (dù được nhắc tới trong tài liệu kiến trúc mục tiêu): MassTransit, thư viện xác thực JWT/OIDC, AutoMapper/Mapster, FluentValidation.

### 2.3 External component — hạ tầng chạy tách biệt

| Thành phần | Trạng thái thật | Ghi chú |
|---|---|---|
| **SQL Server** (`mcr.microsoft.com/mssql/server`) | **Đang dùng thật** | 1 container, 4 database riêng (mỗi service 1 database, không service nào đọc CSDL của service khác) |
| **OTel Collector** | **Đang dùng thật** | Nhận log/trace/metric từ cả 6 service |
| **Redis** | Chạy nhưng **chưa có code nào kết nối vào** | Có chủ đích, dự phòng cho tương lai |
| **RabbitMQ** | Chạy nhưng **chưa có code nào kết nối vào** | Đã có hạ tầng test thật (Testcontainers) dùng RabbitMQ, nhưng chưa service sản phẩm nào publish/consume |
| **Jenkins + SonarQube** | Chỉ tồn tại trong `docker-compose.ci.yml`, đã viết xong cấu hình nhưng **CI/CD chưa thực sự bật** (cần quản trị viên bật Jenkins + branch protection GitHub) | Xem [`github-jenkins-sonarqube-setup.md`](github-jenkins-sonarqube-setup.md) |

### 2.4 Mục tiêu tương lai — chưa xây (đánh dấu nét đứt đỏ trong sơ đồ)

Theo [`roadmap.md`](roadmap.md) và [`system-design.md`](system-design.md): dịch vụ **Logistics**, **Invoices**; **Identity Server** thật (Duende, thay cho xác thực giả lập); **RabbitMQ + MassTransit** nối dây thật (outbox/saga); **HashiCorp Vault** + External Secrets Operator; **Unleash** (feature toggle); **Pact Broker** (hiện hợp đồng Pact chỉ lưu file `.json` thẳng trong repo, chưa có broker); **Elastic Stack** (hiện OTel Collector không có nơi nào nhận log/trace phía sau); triển khai lên **Kubernetes**.

## 3. Luồng quan trọng nhất: Checkout (đặt hàng) — sequence diagram end-to-end

Đây là luồng nghiệp vụ lõi duy nhất đã chạy thật đầu-cuối trong hệ thống hôm nay (xem [`demo-phase-1.md`](demo-phase-1.md)). Sơ đồ tuần tự đầy đủ nằm ở **Trang 2** của [`diagrams/kien-truc-3-nhom.drawio`](diagrams/kien-truc-3-nhom.drawio). Tóm tắt các bước:

1. **Storefront → Gateway**: `POST` yêu cầu checkout, qua cổng `5300`.
2. **Gateway → BFF**: chuyển tiếp toàn bộ, gắn thêm `X-Tenant-Id`, `X-Subject-Id`, `X-Correlation-Id`.
3. **BFF → Baskets**: đọc giỏ hàng hiện tại của người gọi.
4. **Rẽ nhánh tại BFF**: nếu giỏ hàng rỗng → trả lỗi `409` ngay, **dừng luồng, không tạo đơn**.
5. **BFF → Orders**: gửi các dòng giỏ hàng, yêu cầu tạo đơn hàng. Orders tự tính lại tổng tiền (không tin số tiền từ BFF).
6. **Orders → BFF**: trả về đơn hàng đã tạo (mã đơn, tổng tiền).
7. **BFF → Baskets**: xoá sạch giỏ hàng — **chỉ gọi bước này sau khi bước 5 đã thành công**.
8. **BFF → Gateway → Storefront**: trả kết quả xác nhận đặt hàng.

Toàn bộ 8 bước trên là **đồng bộ, tuần tự, trong cùng một request** — không có bước nào chạy nền qua hàng đợi sự kiện. Về rủi ro của thứ tự bước 5→7 và lý do chọn thứ tự này, xem [`chuc-nang-nghiep-vu/dat-hang.md`](chuc-nang-nghiep-vu/dat-hang.md) mục "Rủi ro nghiệp vụ" và [`adr/0011-checkout-orchestration.md`](adr/0011-checkout-orchestration.md).

**So với kiến trúc mục tiêu:** sơ đồ Diagram 3 trong [`system-design.md`](system-design.md) mô tả cùng luồng này nhưng ở dạng mục tiêu tương lai — có thêm bước publish sự kiện `OrderPlacedV1` qua RabbitMQ để Logistics/Invoices xử lý bất đồng bộ phía sau. Bước đó **chưa tồn tại trong code hôm nay** — schema sự kiện đã định nghĩa sẵn (`shared/EventContracts`) nhưng chưa ai publish/consume thật.

## 4. Tài liệu liên quan

- Kiến trúc mục tiêu đầy đủ (C4, tiếng Anh): [`system-design.md`](system-design.md)
- Sơ đồ trạng thái hiện tại (đã có sẵn trước tài liệu này, không phân theo 3 nhóm): [`diagrams/current-state-architecture.drawio`](diagrams/current-state-architecture.drawio)
- Các quyết định kiến trúc (ADR): [`adr/`](adr/)
- Quy tắc cô lập dịch vụ, cấu trúc thư mục bắt buộc: [`../services/README.md`](../services/README.md)
- Tổng quan nghiệp vụ cho người không đọc code: [`tong-quan-du-an.md`](tong-quan-du-an.md)
