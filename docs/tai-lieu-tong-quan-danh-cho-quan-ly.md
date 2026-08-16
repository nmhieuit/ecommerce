# Tài liệu kỹ thuật tổng quan — Nền tảng Ecommerce

*Viết cho: quản lý không trực tiếp code .NET. Mục tiêu: hiểu codebase hiện tại đang có gì, các phần liên hệ với nhau ra sao, và vì sao nó được thiết kế như vậy — không cần đọc code.*

*Cập nhật lần cuối: sau khi hoàn thành tính năng `003-stub-identity-tenant-context` (SCRUM-12 — định danh giả lập + ranh giới "tenant"). Tính năng `004-minimal-shopping-spa` (SCRUM-14 — giao diện) đã có bản đặc tả (`spec.md`) nhưng **chưa có một dòng code nào**; xem Mục 5.*

---

## Điều quan trọng nhất cần biết trước khi đọc tiếp

Đây **không phải** một hệ thống thương mại điện tử đang chạy production. Theo [`docs/roadmap.md`](roadmap.md), đây là **dự án luyện tập cá nhân (solo)** để một người thực hành đầy đủ vòng đời phần mềm (Product Owner → Dev → QA → DevOps → SRE), đang ở **Giai đoạn 1/5 ("Walking Skeleton" — bộ khung đi được, chưa có thịt)**.

Trong repo có **hai tầng thông tin** dễ nhầm lẫn với nhau:

| Tầng | Là gì | Đã có code chưa? |
|---|---|---|
| **Bản thiết kế mục tiêu** (`docs/system-design.md`, `docs/tech-stack-decisions.md`, `docs/adr/`, `.specify/memory/constitution.md`) | Kiến trúc đầy đủ dự kiến: 6 service nghiệp vụ, Identity Server thật, message queue, Redis, phân vùng dữ liệu vật lý theo tenant, web app React... | **Một phần nhỏ** — xem cột bên phải cho từng mục |
| **Codebase thực tế hôm nay** (`services/`, `shared/`, `tests/`) | 6 service chạy được (4 nghiệp vụ + Gateway + BFF), có "chốt chặn" tenant giả lập, nhưng **chưa có giao diện, chưa có dữ liệu mẫu, chưa có phân vùng dữ liệu vật lý theo tenant** | **Có**, và là toàn bộ những gì tài liệu này mô tả |

**Ba tính năng đã hoàn thành theo đúng quy trình đặc tả (spec-kit)** — `specs/001-scaffold-service-shells`, `specs/002-gateway-bff-routing`, `specs/003-stub-identity-tenant-context` — đều có đủ tài liệu đặc tả/kế hoạch/nhiệm vụ và **100% nhiệm vụ đã đánh dấu hoàn thành**. Tính năng thứ tư, `specs/004-minimal-shopping-spa`, mới chỉ có bản đặc tả yêu cầu; file kế hoạch triển khai của nó vẫn là **file mẫu trống chưa điền** — nghĩa là về mặt kế hoạch còn chưa bắt đầu, chứ chưa nói đến code.

Phần công cụ sinh tự động của Spec-Kit (`.specify/`, các slash-command) không được giải thích ở đây vì đó là công cụ hỗ trợ viết đặc tả, không phải sản phẩm — nhưng nội dung *bên trong* các đặc tả đã hoàn thành ở `specs/` thì có, vì đó là nguồn xác nhận đáng tin cậy cho những gì đã thực sự được xây.

---

## 1. Giải thích codebase hiện tại

### 1.1 Đây là gì, về mặt kỹ thuật

- Ngôn ngữ/nền tảng: **C# trên .NET 10**.
- File gốc mở dự án: [`Ecommerce.slnx`](../Ecommerce.slnx) — "solution file", giống một tủ hồ sơ liệt kê toàn bộ **23 dự án con** (đếm trực tiếp từ file này, tăng từ 21 ở lần cập nhật trước do thêm thư viện tenant — xem 1.5).
- 23 dự án con chia làm 3 nhóm thư mục:
  - `services/` — **6 dự án API** (4 nghiệp vụ + Gateway + BFF), kèm 12 dự án test đi theo (mỗi service 2: UnitTests + IntegrationTests)
  - `shared/` — **2 thư viện dùng chung** (`ServiceDefaults`, `Tenancy`) + 1 dự án test cho `Tenancy` — không cái nào là service
  - `tests/` — 2 dự án kiểm tra kiến trúc ở cấp toàn hệ thống
- `dotnet build` cho toàn bộ solution chạy **sạch, không lỗi, không cảnh báo**. Tổng số bài kiểm thử chính xác **chưa được xác minh lại trong lần cập nhật này** (cần Docker để chạy trọn bộ vì nhiều test dùng SQL Server thật trong container) — số cũ "96 bài" đã lỗi thời vì tính năng tenant vừa thêm ít nhất khoảng chục file test mới. Muốn biết con số chính xác, chạy `dotnet test Ecommerce.slnx`.

### 1.2 Sáu service, chia hai loại

**Bốn service nghiệp vụ** — mỗi cái sở hữu một cơ sở dữ liệu riêng, không ai được đụng vào CSDL của ai:

| Service | Nghiệp vụ | Cổng (máy dev) | API đọc đã có |
|---|---|---|---|
| Products | Danh mục sản phẩm | 5088 | `GET /products` (hiện **luôn trả về rỗng** — chưa có dữ liệu mẫu, xem 1.3) |
| Baskets | Giỏ hàng | 5188 | `GET /baskets/{id}` |
| Orders | Đơn hàng | 5041 | `GET /orders/{id}` |
| Parties | Khách hàng / định danh | 5204 | `GET /parties/{id}` |

**Hai service ở biên** — không sở hữu dữ liệu, chỉ điều phối:

| Service | Vai trò | Cổng | API đã có |
|---|---|---|---|
| Gateway | Cửa vào duy nhất của toàn hệ thống; cũng là nơi gán danh tính giả lập (Mục 1.5) | 5300 | Chuyển tiếp mọi thứ sang BFF |
| BFF | Gộp và định hình dữ liệu cho giao diện | 5301 | `GET /bff/products`, `/bff/baskets/{id}`, `/bff/orders/{id}`, `/bff/parties/{id}` |

Ngoài ra **cả 6 service** đều có `GET /health/live` và `GET /health/ready`. Với 4 service nghiệp vụ, **cả hai** endpoint đọc dữ liệu đều bị chặn nếu request thiếu thông tin "tenant" hợp lệ — xem Mục 1.5.

### 1.3 Dữ liệu đã có thật, nhưng còn rỗng và chưa được ngăn cách vật lý theo tenant

- Mỗi service nghiệp vụ có **một bảng dữ liệu thật** (`Product`, `Basket`, `Order`, `Party`), cố tình tối giản — chỉ đúng những trường mà BFF cần đọc.
- Mỗi service có **script tạo bảng (EF Core migration)** lưu trong repo, review được như code, kèm sẵn kịch bản lùi lại.
- **Chưa có dữ liệu mẫu (seed data)** ở bất kỳ service nào. Hệ quả cụ thể: gọi `GET /products` hôm nay **luôn trả về danh sách rỗng**, không phải vì lỗi mà vì bảng thật sự không có dòng nào. Đây là ghi chú chính thức trong đặc tả của tính năng `004` (viết ngày 2026-08-16), không phải suy đoán.
- **Về việc "ngăn cách dữ liệu theo tenant":** kế hoạch ban đầu (đọc được trong `specs/003-stub-identity-tenant-context/research.md`) là mỗi tenant có **schema CSDL riêng** trong cùng một database (`HasDefaultSchema(tenantId)`). Khi triển khai thực tế, cách này **gặp lỗi kỹ thuật thật** (migration đã tạo bảng không gắn schema từ trước, đổi giữa chừng làm SQL Server báo "không tìm thấy đối tượng") và đã bị **chủ động huỷ bỏ**, ghi lại rõ ràng trong tài liệu đặc tả kèm lý do. Những gì thực sự chạy hôm nay chỉ là **chốt chặn logic** (không cho phép bất kỳ truy vấn nào chạy nếu chưa xác định được tenant — Mục 1.5), **chưa có ngăn cách vật lý** giữa dữ liệu các tenant. Vô hại ở thời điểm hiện tại vì hệ thống mới chỉ phục vụ đúng 1 tenant, nhưng là một khoản nợ kỹ thuật thật, cần nhớ trước khi có tenant thứ hai.

### 1.4 Một request đi như thế nào

```
Người dùng / curl
      │  chỉ biết DUY NHẤT địa chỉ cổng 5300
      ▼
  Gateway (5300)     ── xác thực giả lập LUÔN thành công, gán tenant cố định "contoso",
      │                  gán mã theo dõi (Correlation-Id), rồi chuyển tiếp nguyên vẹn
      ▼
  BFF (5301)          ── giải mã, tự gắn lại tenant + mã theo dõi lên từng lời gọi ra
      ▼                  ngoài (bắt buộc phải làm thủ công — xem Mục 2.4), cắt gọt dữ liệu
  Products (5088)     ── kiểm tra co tenant hợp lệ chưa (chưa có → dừng, lỗi 500);
      ▼                  nếu có, mới đọc CSDL của riêng nó
  SQL Server "products"
```

Người gọi **không cần biết** có bao nhiêu service, chúng nằm ở đâu, hay tên gì — và (kể từ tính năng `003`) cũng không cần tự khai "tôi là ai/thuộc khách hàng nào", vì Gateway tự gán sẵn. Đó là giá trị của hai tầng biên cộng với tầng định danh.

### 1.5 Định danh & ranh giới "tenant" — giả lập nhưng được ép buộc thật

Đây là điều tính năng `003` vừa hoàn thành (SCRUM-12 trong roadmap: *"giả lập định danh, nhưng phải xác định tenant thật, không được để bất kỳ đường nào chạm dữ liệu mà chưa qua bước này"*):

- **`shared/Tenancy`** là thư viện dùng chung mới (không phải service), gồm 4 file chính: `TenantContext` (nơi lưu tenant đang xử lý cho request hiện tại), `TenantContextMiddleware` (đọc header `X-Tenant-Id` và nạp vào `TenantContext`), `MissingTenantContextException`, và `TenancyExtensions`.
- **Gateway giả lập một người dùng và một tenant cố định** — cấu hình thẳng trong `appsettings.json`: `TenantId: "contoso"`, `SubjectId: "phase1-stub-user"`. Đây là "cắm tạm" đứng thay cho Identity Server thật (việc đó thuộc SCRUM-23, Giai đoạn 3). Gateway **luôn tự ghi đè** header `X-Tenant-Id` bằng giá trị cố định này trước khi chuyển tiếp — không tin bất kỳ giá trị nào client tự gửi lên, để không ai giả mạo được tenant qua request.
- **Lan truyền qua 3 tầng:** Gateway → BFF được YARP tự động chuyển tiếp header. Nhưng BFF → 4 service nghiệp vụ thì **không tự động** — công cụ gọi HTTP có kiểu (typed `HttpClient`) trong .NET không tự sao chép header của request gốc, nên BFF phải có thêm một đoạn code riêng (`TenantPropagationHandler`) chỉ để gắn lại `X-Tenant-Id` lên từng lời gọi ra ngoài. Đây là kiểu lỗi rất dễ bị bỏ sót nếu không viết test riêng cho nó — thực tế đã có test.
- **"Chốt chặn" (gate) thực sự:** mỗi service nghiệp vụ chỉ có **đúng một chỗ trong code** khởi tạo kết nối tới CSDL của nó (trong `Program.cs`), và chỗ đó bắt buộc phải gọi `RequireTenantId()` trước — hàm này **ném lỗi ngay** nếu chưa xác định được tenant. Vì toàn bộ hệ thống chỉ có một cửa để lấy kết nối CSDL, không có đường vòng nào để một đoạn code mới lỡ quên kiểm tra tenant mà vẫn đọc được dữ liệu.
- **Hệ quả kiểm chứng được:** nếu ai đó gọi thẳng vào một service nghiệp vụ (bỏ qua Gateway, ví dụ script nội bộ hoặc gọi nhầm khi debug) mà không có header `X-Tenant-Id`, request đó nhận về lỗi **500**, chứ không âm thầm trả về dữ liệu nào đó. Có bài test riêng xác nhận đúng hành vi này cho cả 4 service (`TenantEnforcementTests`, xem Mục 2.6).

### 1.6 Cách tổ chức code trong mỗi service

Thay vì chia theo "tầng kỹ thuật" kiểu cổ điển (Controller / Service / Repository), dự án **chủ động chọn chia theo tính năng** ("vertical-slice"): mỗi năng lực nghiệp vụ có một thư mục `Features/<TênNăngLực>/` chứa mọi thứ liên quan trong cùng một chỗ. Quy tắc này được **ép buộc bằng máy** (Mục 2.6) chứ không chỉ là quy ước bằng lời, và áp dụng cho cả Gateway lẫn BFF chứ không riêng service nghiệp vụ.

---

## 2. Các dự án/component và cách chúng phụ thuộc lẫn nhau

### 2.1 Bản đồ quan hệ

- **4 service nghiệp vụ vẫn hoàn toàn không biết đến nhau.** Không service nào gọi service khác, không service nào tham chiếu code của service khác.
- **BFF gọi 4 service nghiệp vụ, nhưng chỉ qua HTTP** — đúng như một khách hàng bên ngoài. Code sản phẩm của BFF không tham chiếu code của bất kỳ service nào (chỉ *dự án test* của BFF mới tham chiếu, để chạy service thật trong bộ nhớ khi kiểm thử).
- **Gateway chỉ biết duy nhất địa chỉ của BFF.** Không có đường đi thẳng tới service nghiệp vụ nào, có bài test canh đúng điều đó.
- **Cả 6 service dùng chung 2 thư viện:** `shared/ServiceDefaults` (log/theo dõi) và `shared/Tenancy` (xác định + chặn theo tenant).
- **3 dự án đứng ngoài, tự quét chứ không bị ai tham chiếu:** `tests/CrossServiceIsolation.Tests`, `tests/StructureConventionTests`, và bản thân `shared/Tenancy.UnitTests` (test riêng cho thư viện tenant, độc lập với mọi service).

### 2.2 `shared/ServiceDefaults` — thư viện dùng chung

Gói cấu hình chung để 6 service không mỗi cái tự cấu hình một kiểu. Lo 2 việc: (1) bật ghi log/theo dõi hiệu năng chuẩn hoá (OpenTelemetry), (2) gắn **mã theo dõi (`X-Correlation-Id`)** vào mỗi request.

**Một lỗi thật đã được phát hiện và sửa ở tính năng `002`:** mã theo dõi do Gateway sinh ra trước đây **không đi kèm sang BFF** — BFF tự sinh một mã thứ hai. Hậu quả: khách hàng gặp lỗi, gửi mã tra cứu, nhưng mã đó chỉ tìm thấy log của Gateway, không thấy gì của BFF. Lỗi chỉ lộ ra khi **chạy thật cả chuỗi**, không test đơn lẻ nào thấy được. Sửa bằng một dòng trong thư viện dùng chung, nên **cả 6 service cùng được vá**.

### 2.3 Gateway (`services/gateway`) — cửa vào duy nhất kiêm nơi gán danh tính

Dùng YARP (reverse-proxy của Microsoft). Toàn bộ định tuyến nằm trong **file cấu hình JSON**, không có logic định tuyến trong code C#. Bảng route hiện tại đúng **một dòng**: "mọi đường dẫn → BFF" — có chủ đích, để BFF thêm tính năng mới không bao giờ cần sửa/deploy lại Gateway.

Từ tính năng `003`, Gateway còn làm thêm việc thứ hai: chạy một "handler xác thực" luôn tự động thành công (`StubIdentity`), gán cố định tenant `contoso` và một người dùng giả lập, rồi ghi đè header `X-Tenant-Id` trước khi chuyển tiếp (Mục 1.5).

### 2.4 BFF (`services/bff`) — gộp, định hình dữ liệu, và tự tay lan truyền ngữ cảnh

BFF = "Backend For Frontend". Khác Gateway ở chỗ: **Gateway chuyển tiếp, BFF mở gói ra đọc và ghép lại**. Bốn cơ chế đáng chú ý:

**a) Ngân sách thời gian rõ ràng** cho mỗi lời gọi ra ngoài (số liệu lấy trực tiếp từ code, `DownstreamClientRegistrationExtensions.cs`): **1 giây** cho một lần thử, tối đa **2 lần thử lại**, tổng cộng không quá **3 giây**, và "cầu dao" (circuit breaker) tự ngắt sau 10 giây lỗi liên tục. Không có chỗ nào chờ vô hạn.

**b) Bốn loại lỗi được phân biệt rõ ràng khi trả về người gọi** (đọc từ `DownstreamExceptionHandler.cs`):

| Mã | Nghĩa | Người trực nên làm gì |
|---|---|---|
| 502 | Service kia **không kết nối được** / cầu dao đã ngắt | Gọi đội phụ trách service đó |
| 504 | Service kia **vượt quá thời gian chờ** | Xem tải, CPU, cơ sở dữ liệu |
| 500 | Lỗi của **chính BFF** (không phải downstream) | Đội BFF tự xử lý |
| 404 | Không tìm thấy bản ghi — **không phải lỗi** | Không làm gì, đây là câu trả lời đúng |

**c) Dữ liệu được cắt gọt trước khi ra ngoài** qua các hàm ánh xạ riêng (ví dụ `ToSummary`) — trường nội bộ mới thêm ở service phía sau không tự động lộ ra ngoài.

**d) Tự tay lan truyền tenant** (mới từ `003`): vì `HttpClient` có kiểu không tự sao chép header của request gốc, BFF có riêng một `TenantPropagationHandler` gắn lại `X-Tenant-Id` lên cả 4 lời gọi ra ngoài (Mục 1.5).

### 2.5 `shared/Tenancy` — thư viện xác định & ép buộc "đang phục vụ ai"

Thư viện dùng chung thứ hai (không phải service), tách riêng khỏi `ServiceDefaults` vì lo một việc rất khác: trả lời câu hỏi "request này đang phục vụ tenant nào" và **từ chối phục vụ nếu câu hỏi đó chưa có câu trả lời**. Xem chi tiết cơ chế ở Mục 1.5. Có bộ test riêng (`Tenancy.UnitTests`) không phụ thuộc vào bất kỳ service nào.

### 2.6 Sáu "lưới an toàn" kiến trúc tự động

Phần đáng chú ý nhất của codebase: những cơ chế **tự động ngăn lỗi kiến trúc**, chạy như bài kiểm thử mỗi lần build.

| Cơ chế | Chặn điều gì |
|---|---|
| `tests/CrossServiceIsolation.Tests` | Service A cầm chuỗi kết nối tới CSDL của service B. Gateway/BFF **không được** cầm bất kỳ chuỗi kết nối CSDL nào (chúng không sở hữu dữ liệu). **Mở rộng thêm ở tính năng `003`:** mỗi service nghiệp vụ phải có **đúng một** điểm khởi tạo `DbContext`, và điểm đó bắt buộc phải gọi `RequireTenantId()` — nếu thiếu, build fail |
| `tests/StructureConventionTests` | Ai đó tạo lại thư mục `Controllers/`, `Services/`, `Repositories/` — đi ngược quy ước đã chọn. Áp dụng cả Gateway và BFF |
| `Gateway.Api.UnitTests/RouteConfigurationTests` | Cấu hình định tuyến sai chính tả, hoặc có route đi thẳng tới service nghiệp vụ (bỏ qua BFF) |
| `Gateway.Api.UnitTests/ForwardingTimeoutBudgetTests` | Timeout của Gateway (10s) bị đặt **thấp hơn** ngân sách 3s của BFF — nếu vậy Gateway sẽ cắt ngang khi BFF còn đang soạn thông báo lỗi có ích |
| `Bff.Api.IntegrationTests/GeneratedContractTests` | Tài liệu API sinh tự động thiếu mất các trường hợp lỗi (404/502/504), không chỉ khai báo trường hợp thành công |
| `*/tests/*IntegrationTests/TenantEnforcementTests` (1 bộ / service nghiệp vụ) | Một request không có `X-Tenant-Id` lại nhận được `200 OK` thay vì bị chặn cứng — mỗi service nghiệp vụ có bộ test riêng xác nhận hành vi 500 khi thiếu tenant |

### 2.7 Hạ tầng cục bộ và triển khai

- [`docker-compose.deps.yml`](../docker-compose.deps.yml) khởi động **4 container SQL Server độc lập** (cổng 14330–14333), mỗi service một cái, kèm 4 job tạo database rỗng.
- **Cả 6 service đều đã có `Dockerfile`**, build được thành container, chạy bằng tài khoản không phải root. File cấu hình dành cho môi trường container (`appsettings.json` gốc của BFF) trỏ tới các service khác **bằng tên miền nội bộ** (ví dụ `http://baskets-api:8080`) thay vì cổng số — cách này *không* dính lỗi nêu ở Mục 5, nhưng hiện **chưa có** file docker-compose nào thực sự chạy 6 container API cùng lúc để cách này được dùng tới.
- Công cụ tạo migration được **ghim phiên bản trong repo** (`.config/dotnet-tools.json`).
- **Vẫn chưa có pipeline CI/CD** và **chưa có lệnh duy nhất chạy toàn bộ hệ thống** (SCRUM-15, chưa làm) — hiện phải mở 6 cửa sổ terminal, tự chạy migration cho từng service.

---

## 3. Mục đích từng phần + các tình huống thực tế được giải quyết

### 3.1 Mục đích từng dự án

| Dự án | Mục đích |
|---|---|
| `Products.Api` | Sở hữu danh mục sản phẩm. Có bảng dữ liệu, chưa có dữ liệu mẫu, 1 API đọc. |
| `Baskets.Api` | Sở hữu dữ liệu giỏ hàng. Có bảng dữ liệu và 1 API đọc. |
| `Orders.Api` | Sở hữu dữ liệu đơn hàng. Có bảng dữ liệu và 1 API đọc. |
| `Parties.Api` | Sở hữu định danh khách hàng/đối tác. Có bảng dữ liệu và 1 API đọc. |
| `Gateway.Api` | Cửa vào duy nhất; gán danh tính/tenant giả lập; che giấu cấu trúc bên trong. |
| `Bff.Api` | Gộp dữ liệu nhiều service cho giao diện; cắt gọt dữ liệu; xử lý lỗi có kiểm soát; lan truyền tenant thủ công. |
| `shared/ServiceDefaults` | Chuẩn hoá log/theo dõi và mã tra cứu cho mọi service. |
| `shared/Tenancy` | Xác định và ép buộc "đang phục vụ tenant nào" trước khi cho chạm CSDL. |
| `tests/CrossServiceIsolation.Tests` | Chặn đọc/ghi nhầm CSDL của service khác, và chặn CSDL bị đọc mà chưa qua chốt tenant. |
| `tests/StructureConventionTests` | Chặn phá vỡ quy ước tổ chức code. |
| `*.UnitTests` / `*.IntegrationTests` | Kiểm tra logic riêng, kiểm tra với SQL Server thật trong container (không giả lập). |

### 3.2 Bảy tình huống thật mà giải pháp này giải quyết

**1) Ngăn rò rỉ/đè dữ liệu chéo giữa các bộ phận nghiệp vụ, trước khi nó xảy ra.**
Một lập trình viên copy-paste nhầm file cấu hình khiến service Baskets trỏ sang CSDL của Orders. Ở đây build **thất bại ngay**, trước khi code được merge — bắt lỗi ở khâu *sở hữu*, không đợi đến khâu *sử dụng*.

**2) Phát hiện sự cố hạ tầng tự động, không cần con người theo dõi 24/7.**
`/health/ready` thật sự thử mở kết nối tới CSDL; `/health/live` **cố tình không** kiểm tra CSDL, để một sự cố CSDL 30 giây không biến thành sự cố ứng dụng 5 phút do bị khởi động lại hàng loạt.

**3) Giữ chất lượng kiến trúc ổn định khi thời gian trôi qua, không phụ thuộc trí nhớ một người.**
Các bài test kiến trúc biến quy ước tổ chức code thành điều kiện bắt buộc để build thành công.

**4) Một service chết không kéo sập cả hệ thống.**
Không có ngân sách thời gian, mặc định .NET chờ tới 100 giây mỗi lời gọi — một service treo có thể làm BFF cạn luồng xử lý và ngừng trả lời **mọi thứ**, kể cả phần không liên quan. Với ngân sách 3 giây hiện tại, người gọi luôn nhận lỗi rõ ràng trong vài giây, các phần khác vẫn chạy bình thường.

**5) Lỗi có mã tra cứu, thay vì "hệ thống đang bận, vui lòng thử lại".**
Một mã theo dõi (`X-Correlation-Id`) lần được toàn bộ hành trình của request qua cả ba tầng trong hệ thống ghi log — không có nó, điều tra một khiếu nại cụ thể gần như bất khả thi trong hệ phân tán.

**6) Đội frontend không thể vô tình bỏ sót trường hợp lỗi.**
Tài liệu API sinh tự động từng chỉ khai báo trường hợp thành công; đã sửa để bắt buộc khai báo cả 404/502/504, và có test canh để không lệch trở lại — quan trọng vì mã nguồn giao diện dự kiến sẽ được sinh tự động từ chính tài liệu này.

**7) "Quên xác định đang phục vụ ai" biến thành lỗi ồn ào ngay lập tức, không phải rò rỉ dữ liệu âm thầm.**
Nếu một script nội bộ, một lần debug thủ công, hay một tính năng tương lai gọi thẳng vào service nghiệp vụ mà bỏ qua Gateway (nên thiếu header `X-Tenant-Id`), hệ thống **dừng cứng với lỗi 500** thay vì âm thầm trả lời bằng dữ liệu của tenant mặc định nào đó. Vì toàn hệ thống chỉ có đúng một điểm khởi tạo kết nối CSDL cho mỗi service, và điểm đó bắt buộc phải xác định tenant trước — không có đường vòng nào để tính năng mới lỡ quên bước này mà vẫn chạy được.

---

## 4. Sơ đồ kiến trúc hiện tại (dùng được với draw.io)

Sơ đồ vẽ **đúng những gì đang có trong code hôm nay** (phần trên) và đối chiếu với **phần chưa xây** (phần dưới, viền đứt màu đỏ nhạt).

> **Lưu ý phân biệt:** repo có sẵn 3 sơ đồ khác ở [`docs/system-design.md`](system-design.md) — nhưng chúng vẽ **kiến trúc mục tiêu đầy đủ**, không phải trạng thái hiện tại.

### Cách dùng
1. Mở [app.diagrams.net](https://app.diagrams.net) (draw.io).
2. Vào menu **Extras → Edit Diagram…**
3. Xoá nội dung trống, dán toàn bộ khối XML bên dưới vào, bấm **Save/OK**.

*(Hoặc mở trực tiếp file [`docs/diagrams/current-state-architecture.drawio`](diagrams/current-state-architecture.drawio) đã có sẵn trong repo — nội dung giống hệt khối XML bên dưới.)*

```xml
<mxfile host="app.diagrams.net" modified="2026-08-16T00:00:00.000Z" agent="5.0" version="24.0.0" type="device">
  <diagram id="current-state-003" name="Trang thai hien tai - sau 003">
    <mxGraphModel dx="1450" dy="1100" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1450" pageHeight="1600" math="0" shadow="0">
      <root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />

        <mxCell id="title1" value="PHAN 1 -- DA CO TRONG CODE HOM NAY (23 du an; build sach 0 loi 0 canh bao; so luong test xem docs)" style="text;html=1;fontStyle=1;fontSize=15;fontColor=#2d6a2d;" vertex="1" parent="1">
          <mxGeometry x="30" y="10" width="1200" height="26" as="geometry" />
        </mxCell>

        <mxCell id="client" value="Nguoi goi (curl / Postman)&#10;SPA React: CHUA CO CODE (xem specs/004, con la spec)&#10;CHI biet DUY NHAT dia chi cong 5300&#10;Chua dang nhap that -- Gateway tu dong gan 1 danh tinh gia lap" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6cc;strokeColor=#d79b00;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="30" y="50" width="320" height="100" as="geometry" />
        </mxCell>

        <mxCell id="sd" value="shared/ServiceDefaults&#10;(thu vien dung chung, KHONG phai service)&#10;- OpenTelemetry: log / trace / metric&#10;- Correlation-Id (X-Correlation-Id): sinh o Gateway,&#10;  ghi vao request de moi hop sau dung chung 1 ma" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="860" y="50" width="280" height="100" as="geometry" />
        </mxCell>

        <mxCell id="tenancy" value="shared/Tenancy&#10;(thu vien dung chung, KHONG phai service, MOI tu 003)&#10;- TenantContext.RequireTenantId(): chan MOI ket noi&#10;  CSDL neu tenant chua duoc xac dinh&#10;- Header lan truyen: X-Tenant-Id" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="1160" y="50" width="260" height="100" as="geometry" />
        </mxCell>

        <mxCell id="gw" value="Gateway.Api (services/gateway) -- YARP&#10;Cong 5300&#10;Bang route = 1 dong duy nhat: MOI duong dan -&gt; BFF&#10;StubIdentity: xac thuc LUON thanh cong, gan co dinh&#10;tenant &quot;contoso&quot; + nguoi dung gia lap, ghi de X-Tenant-Id&#10;(khong tin gia tri tu ngoai gui vao)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="30" y="175" width="460" height="140" as="geometry" />
        </mxCell>

        <mxCell id="gwnote" value="Vi sao 1 route catch-all: neu Gateway phai liet ke tung duong dan cua BFF thi&#10;moi lan BFF them tinh nang lai phai sua + deploy Gateway.&#10;&#10;Gateway CO TINH khong co duong di thang toi service nghiep vu nao&#10;(co bai test canh dieu nay).&#10;&#10;StubIdentity la &quot;cam tam&quot; thay cho Identity Server that (SCRUM-23, Giai&#10;doan 3) -- hom nay MOI request deu duoc xac thuc thanh cong." style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;align=left;" vertex="1" parent="1">
          <mxGeometry x="510" y="175" width="650" height="140" as="geometry" />
        </mxCell>

        <mxCell id="bff" value="Bff.Api (services/bff) -- Backend For Frontend&#10;Cong 5301  |  KHONG co co so du lieu&#10;GET /bff/products, /bff/baskets/{id},&#10;      /bff/orders/{id}, /bff/parties/{id}&#10;TenantPropagationHandler: gan lai X-Tenant-Id len ca 4&#10;loi goi ra ngoai (HttpClient khong tu chuyen tiep header)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="30" y="335" width="460" height="160" as="geometry" />
        </mxCell>

        <mxCell id="bffnote" value="Ngan sach thoi gian cho MOI loi goi ra ngoai (khong cho vo han):&#10;   1 giay / lan thu (toi da 2 lan thu lai)  |  toi da 3 giay tong cong  |  cau dao ngat sau 10s loi lien tuc&#10;&#10;Phan biet loai loi tra ve nguoi dung:&#10;   502 = downstream khong ket noi duoc / cau dao da ngat        504 = downstream vuot thoi gian cho&#10;   500 = loi cua chinh BFF (khong phai downstream)                404 = khong tim thay ban ghi (KHONG phai loi)&#10;&#10;Du lieu duoc CAT GOT lai truoc khi ra ngoai qua ham anh xa rieng (vd ToSummary): truong noi bo&#10;khong tu dong lot ra ngoai khi service phia sau them truong moi." style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;align=left;" vertex="1" parent="1">
          <mxGeometry x="510" y="335" width="890" height="160" as="geometry" />
        </mxCell>

        <mxCell id="svcnote" value="4 service nghiep vu HOAN TOAN khong biet den nhau: khong cai nao goi cai nao, khong cai nao tham chieu code cua cai nao." style="text;html=1;fontSize=10;fontColor=#666666;whiteSpace=wrap;" vertex="1" parent="1">
          <mxGeometry x="30" y="510" width="1390" height="20" as="geometry" />
        </mxCell>

        <mxCell id="svc1" value="Products.Api&#10;Cong 5088&#10;GET /products -- HIEN LUON TRA VE RONG (chua co du lieu mau)&#10;+ /health/live, /health/ready&#10;Bang: Product (Id, Name, Price)&#10;Chan doc/ghi CSDL neu thieu X-Tenant-Id (RequireTenantId)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="30" y="535" width="320" height="145" as="geometry" />
        </mxCell>
        <mxCell id="svc2" value="Baskets.Api&#10;Cong 5188&#10;GET /baskets/{id}&#10;+ /health/live, /health/ready&#10;Bang: Basket (Id, CustomerId)&#10;Chan doc/ghi CSDL neu thieu X-Tenant-Id (RequireTenantId)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="380" y="535" width="320" height="145" as="geometry" />
        </mxCell>
        <mxCell id="svc3" value="Orders.Api&#10;Cong 5041&#10;GET /orders/{id}&#10;+ /health/live, /health/ready&#10;Bang: Order (Id, PlacedAtUtc, Total)&#10;Chan doc/ghi CSDL neu thieu X-Tenant-Id (RequireTenantId)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="730" y="535" width="320" height="145" as="geometry" />
        </mxCell>
        <mxCell id="svc4" value="Parties.Api&#10;Cong 5204&#10;GET /parties/{id}&#10;+ /health/live, /health/ready&#10;Bang: Party (Id, DisplayName)&#10;Chan doc/ghi CSDL neu thieu X-Tenant-Id (RequireTenantId)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="1080" y="535" width="320" height="145" as="geometry" />
        </mxCell>

        <mxCell id="composebg" value="" style="rounded=0;whiteSpace=wrap;html=1;fillColor=none;strokeColor=#999999;dashed=1;verticalAlign=top;fontColor=#666666;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="20" y="695" width="1400" height="140" as="geometry" />
        </mxCell>
        <mxCell id="composelbl" value="docker-compose.deps.yml -- 4 container SQL Server rieng biet + 4 job tao database rong. Bang du lieu ben trong do EF Core migration tao ra. CHUA co compose chay ca 6 service API cung luc." style="text;html=1;fontSize=11;fontColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="40" y="702" width="1350" height="20" as="geometry" />
        </mxCell>

        <mxCell id="db1" value="SQL Server (container rieng)&#10;Database: products -- cong 14331&#10;Khong service nao khac duoc cham vao" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="30" y="735" width="320" height="85" as="geometry" />
        </mxCell>
        <mxCell id="db2" value="SQL Server (container rieng)&#10;Database: baskets -- cong 14332&#10;Khong service nao khac duoc cham vao" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="380" y="735" width="320" height="85" as="geometry" />
        </mxCell>
        <mxCell id="db3" value="SQL Server (container rieng)&#10;Database: orders -- cong 14333&#10;Khong service nao khac duoc cham vao" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="730" y="735" width="320" height="85" as="geometry" />
        </mxCell>
        <mxCell id="db4" value="SQL Server (container rieng)&#10;Database: parties -- cong 14330&#10;Khong service nao khac duoc cham vao" style="shape=cylinder;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontSize=10;boundedLbl=1;" vertex="1" parent="1">
          <mxGeometry x="1080" y="735" width="320" height="85" as="geometry" />
        </mxCell>

        <mxCell id="guardtitle" value="'Luoi an toan' kien truc tu dong -- chay nhu bai test moi lan build, FAIL build khi bi vi pham (6 loai)" style="text;html=1;fontStyle=1;fontSize=13;fontColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="30" y="855" width="1100" height="24" as="geometry" />
        </mxCell>

        <mxCell id="guard1" value="tests/CrossServiceIsolation.Tests&#10;- Service A cam chuoi ket noi CSDL cua service B&#10;- Gateway/BFF cam BAT KY chuoi ket noi nao (khong so huu du lieu)&#10;- MOI (003): moi service phai co DUNG 1 diem khoi tao&#10;  DbContext, va diem do phai goi RequireTenantId()" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="30" y="890" width="460" height="120" as="geometry" />
        </mxCell>
        <mxCell id="guard2" value="tests/StructureConventionTests&#10;FAIL build neu service co thu muc Controllers/, Services/,&#10;Repositories/... Bat buoc moi service co it nhat 1 thu muc&#10;Features/&lt;TenNangLuc&gt; -- ap dung ca Gateway va BFF" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="510" y="890" width="460" height="120" as="geometry" />
        </mxCell>
        <mxCell id="guard3" value="Gateway.Api.UnitTests&#10;- RouteConfigurationTests: cau hinh dinh tuyen sai chinh&#10;  ta, hoac co route di thang toi service nghiep vu&#10;- ForwardingTimeoutBudgetTests: timeout Gateway (10s) bi&#10;  dat THAP HON ngan sach 3s cua BFF" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="990" y="890" width="430" height="120" as="geometry" />
        </mxCell>

        <mxCell id="guard4" value="Bff.Api.IntegrationTests/GeneratedContractTests&#10;Tai lieu API sinh tu dong phai khai bao DU ca truong hop&#10;loi (404/502/504), khong chi truong hop thanh cong -- vi ma&#10;nguon giao dien se duoc SINH TU DONG tu tai lieu nay&#10;(SCRUM-14, chua bat dau)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="30" y="1025" width="460" height="100" as="geometry" />
        </mxCell>
        <mxCell id="guard5" value="services/*/tests/*.IntegrationTests -- Testcontainers.MsSql:&#10;chay SQL Server THAT trong container, khong dung gia lap.&#10;Test cua BFF con chay ca 4 service THAT trong bo nho de&#10;kiem tra that su goi duoc, thay vi gia lap cau tra loi." style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="510" y="1025" width="460" height="100" as="geometry" />
        </mxCell>
        <mxCell id="guard6" value="*/tests/*.IntegrationTests/TenantEnforcementTests&#10;(1 bo / service nghiep vu, MOI tu 003)&#10;Request KHONG co X-Tenant-Id phai nhan loi 500,&#10;khong duoc am tham tra ve du lieu" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="990" y="1025" width="430" height="100" as="geometry" />
        </mxCell>

        <mxCell id="e_client_gw" value="HTTP :5300" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="client" target="gw">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_gw_bff" value="YARP chuyen tiep TAT CA + X-Tenant-Id + X-Correlation-Id" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="gw" target="bff">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="e_bff_1" value="HTTP + ngan sach thoi gian + X-Tenant-Id" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="bff" target="svc1">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_bff_2" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="bff" target="svc2">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_bff_3" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="bff" target="svc3">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_bff_4" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=10;" edge="1" parent="1" source="bff" target="svc4">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="e_db1" value="EF Core (sau RequireTenantId)" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="svc1" target="db1">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_db2" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="svc2" target="db2">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_db3" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="svc3" target="db3">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>
        <mxCell id="e_db4" style="edgeStyle=orthogonalEdgeStyle;html=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="svc4" target="db4">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="e_sd_gw" value="tham chieu thu vien (ca 6 service)" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="sd" target="gw">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="920" y="160" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="e_tn_gw" value="tham chieu thu vien (4 service nghiep vu + BFF + Gateway)" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="tenancy" target="gw">
          <mxGeometry relative="1" as="geometry">
            <Array as="points">
              <mxPoint x="1290" y="160" />
            </Array>
          </mxGeometry>
        </mxCell>
        <mxCell id="e_tn_svc1" style="edgeStyle=orthogonalEdgeStyle;html=1;dashed=1;endArrow=block;fontSize=9;" edge="1" parent="1" source="tenancy" target="svc1">
          <mxGeometry relative="1" as="geometry" />
        </mxCell>

        <mxCell id="title2" value="PHAN 2 -- CHUA XAY DUNG, MOI LA KE HOACH (Phase 2-5, xem docs/roadmap.md)" style="text;html=1;fontStyle=1;fontSize=16;fontColor=#a03030;" vertex="1" parent="1">
          <mxGeometry x="30" y="1145" width="1000" height="26" as="geometry" />
        </mxCell>

        <mxCell id="futurebg" value="" style="rounded=0;whiteSpace=wrap;html=1;fillColor=#fafafa;strokeColor=#cc6666;dashed=1;" vertex="1" parent="1">
          <mxGeometry x="20" y="1180" width="1410" height="180" as="geometry" />
        </mxCell>

        <mxCell id="f1" value="React SPA&#10;(specs/004: co spec,&#10;CHUA co code)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="35" y="1205" width="185" height="70" as="geometry" />
        </mxCell>
        <mxCell id="f2" value="Chay toan bo bang&#10;1 lenh duy nhat&#10;(SCRUM-15)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="235" y="1205" width="185" height="70" as="geometry" />
        </mxCell>
        <mxCell id="f3" value="Identity Server that&#10;(Duende)&#10;(SCRUM-23)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="435" y="1205" width="185" height="70" as="geometry" />
        </mxCell>
        <mxCell id="f4" value="Ngan cach VAT LY theo&#10;tenant (da thu, da&#10;chu dong huy -- muc 1.3)" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="635" y="1205" width="185" height="70" as="geometry" />
        </mxCell>
        <mxCell id="f5" value="RabbitMQ +&#10;MassTransit" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="835" y="1205" width="185" height="70" as="geometry" />
        </mxCell>
        <mxCell id="f6" value="Redis, Logistics +&#10;Invoices service" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="1035" y="1205" width="185" height="70" as="geometry" />
        </mxCell>
        <mxCell id="f7" value="Jenkins CI/CD,&#10;Vault, Unleash,&#10;Pact Broker" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;fontColor=#a03030;fontSize=10;" vertex="1" parent="1">
          <mxGeometry x="1235" y="1205" width="185" height="70" as="geometry" />
        </mxCell>

        <mxCell id="fnote" value="Cac khoi nay moi la quyet dinh trong ADR / ban ve trong docs/system-design.md, hoac (voi &quot;Ngan cach vat ly theo tenant&quot;) da thu va bi huy giua chung khi trien khai -- chua co dong code chay that nao cho chung. Gateway, BFF, va co che tenant gia lap DA o Phan 1, khong con la ke hoach nua." style="text;html=1;fontSize=10;fontColor=#a03030;whiteSpace=wrap;" vertex="1" parent="1">
          <mxGeometry x="35" y="1285" width="1370" height="60" as="geometry" />
        </mxCell>

        <mxCell id="lgtitle" value="Chu giai" style="text;html=1;fontStyle=1;fontSize=12;" vertex="1" parent="1">
          <mxGeometry x="30" y="1375" width="100" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc1" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;" vertex="1" parent="1">
          <mxGeometry x="30" y="1405" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc1t" value="Service nghiep vu (so huu du lieu rieng)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="55" y="1405" width="300" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc2" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;" vertex="1" parent="1">
          <mxGeometry x="30" y="1435" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc2t" value="Service o bien (khong so huu du lieu)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="55" y="1435" width="300" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc3" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;" vertex="1" parent="1">
          <mxGeometry x="390" y="1405" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc3t" value="Thu vien dung chung (khong phai service) -- 2 cai" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="415" y="1405" width="330" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc4" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#666666;" vertex="1" parent="1">
          <mxGeometry x="390" y="1435" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc4t" value="Luoi an toan kien truc (tests) -- 6 loai" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="415" y="1435" width="330" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc5" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;" vertex="1" parent="1">
          <mxGeometry x="790" y="1405" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc5t" value="Co so du lieu (moi service 1 CSDL rieng)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="815" y="1405" width="300" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc6" style="rounded=1;whiteSpace=wrap;html=1;fillColor=#f5f5f5;strokeColor=#cc6666;dashed=1;" vertex="1" parent="1">
          <mxGeometry x="790" y="1435" width="20" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgc6t" value="Chi la ke hoach, hoac da thu va bi huy -- chua co code" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="815" y="1435" width="330" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgline1" style="edgeStyle=none;html=1;endArrow=block;fontSize=9;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="1150" y="1415" as="sourcePoint" />
            <mxPoint x="1200" y="1415" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lgline1t" value="Goi that luc chay (HTTP / EF Core)" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="1210" y="1405" width="230" height="20" as="geometry" />
        </mxCell>
        <mxCell id="lgline2" style="edgeStyle=none;dashed=1;html=1;endArrow=block;fontSize=9;" edge="1" parent="1">
          <mxGeometry relative="1" as="geometry">
            <mxPoint x="1150" y="1445" as="sourcePoint" />
            <mxPoint x="1200" y="1445" as="targetPoint" />
          </mxGeometry>
        </mxCell>
        <mxCell id="lgline2t" value="Tham chieu thu vien luc build" style="text;html=1;fontSize=11;" vertex="1" parent="1">
          <mxGeometry x="1210" y="1435" width="230" height="20" as="geometry" />
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
```

---

## 5. Rủi ro và việc còn treo

**1) Lỗi cấu hình cổng đã xác minh trực tiếp trong code, còn tồn tại.** File cấu hình BFF dùng khi chạy từng service trực tiếp trên máy (`services/bff/src/Bff.Api/appsettings.Development.json`) có 2 dòng bị hoán đổi: mục `BasketsApi` trỏ tới cổng `5041` (đó thực ra là cổng của **Orders**, xác nhận trực tiếp từ `Orders.Api`'s `launchSettings.json`), và mục `OrdersApi` trỏ tới cổng `5188` (cổng thật của **Baskets**). Hướng dẫn chạy thử (`specs/002-gateway-bff-routing/quickstart.md`, dòng 56) cũng đang ghi lại đúng cặp số bị hoán đổi này, nên không ai phát hiện ra khi đối chiếu tài liệu với cấu hình — cả hai cùng sai giống nhau. Hậu quả: gọi `/bff/baskets/{id}` qua BFF sẽ thực ra chạm vào service Orders (không có route đó → 404 dù giỏ hàng có thật), và ngược lại.
Điểm cần lưu ý về quy trình: **không bài test nào phát hiện được lỗi này**, vì các bài test thay thế toàn bộ phần kết nối mạng, không bao giờ dùng tới giá trị cấu hình thật trong file này. File cấu hình dùng cho container (không dùng số cổng mà dùng tên miền nội bộ) **không bị ảnh hưởng** — nhưng cách chạy bằng container đầy đủ chưa tồn tại, nên hôm nay ai làm đúng theo hướng dẫn chạy thử cũng sẽ dính lỗi này.

**2) Chưa có ngăn cách vật lý dữ liệu theo tenant.** Như nêu ở Mục 1.3, kế hoạch "mỗi tenant một schema CSDL riêng" đã thử và bị huỷ giữa chừng vì vướng lỗi kỹ thuật thật với migration hiện có. Hiện chỉ có chốt chặn logic (`RequireTenantId()`), chưa có ngăn cách vật lý. Vô hại lúc này (1 tenant duy nhất), là khoản nợ kỹ thuật thật cần giải quyết trước khi có tenant thứ hai.

**3) Tính năng giao diện (SCRUM-14) chưa bắt đầu.** `specs/004-minimal-shopping-spa` mới có bản đặc tả yêu cầu; file kế hoạch triển khai (`plan.md`) vẫn là mẫu trống chưa điền. Không có bất kỳ thư mục frontend, file `.tsx`, hay `package.json` nào trong repo. Đặc tả này cũng tự ghi nhận rằng backend hiện tại **chưa hỗ trợ thêm-vào-giỏ hoặc đặt-hàng** — 4 API hiện có đều chỉ là đọc.

**4) Chưa có dữ liệu mẫu.** `GET /products` hôm nay luôn trả về danh sách rỗng vì bảng `Product` không có dòng nào.

**5) Chưa có CI/CD.** Mọi bài test hiện phải chạy thủ công trên máy cá nhân. "Lưới an toàn kiến trúc" ở Mục 2.6 chỉ phát huy tác dụng đầy đủ khi có pipeline tự động chạy chúng trên mỗi thay đổi.

**6) Chạy thử cục bộ còn nặng.** Hiện phải mở 6 cửa sổ terminal, khởi động 4 container CSDL, chạy 4 lệnh tạo bảng. SCRUM-15 sẽ gom lại thành một lệnh.

**7) Chưa có xác thực/phân quyền thật.** Toàn bộ "định danh" hôm nay là một người dùng và một tenant **giả lập cố định** trong file cấu hình, luôn xác thực thành công. Đây là khoản nợ đã được ghi nhận có chủ đích, thuộc SCRUM-23 (Identity Server thật) ở Giai đoạn 3.

**8) Một ghi chú tài liệu bị lỗi thời (không ảnh hưởng chức năng).** Phần đầu `specs/002-gateway-bff-routing/spec.md` vẫn ghi "Draft — đang chờ quyết định phạm vi", dù thực tế toàn bộ nhiệm vụ của tính năng này đã hoàn thành từ lâu. Chỉ là quên cập nhật dòng trạng thái, không phải khoảng trống thực thi.

---

## Tổng kết ngắn cho quản lý

- Codebase hiện tại = **6 service chạy được** (4 nghiệp vụ có CSDL riêng + Gateway + BFF), **2 thư viện dùng chung** (theo dõi/log, và xác định-ép buộc tenant), tổng **23 dự án con**, build sạch không lỗi.
- Ba tính năng đã hoàn thành đúng quy trình đặc tả: dựng vỏ service (`001`), nối Gateway/BFF thành một cửa vào (`002`), và thêm cơ chế định danh/tenant giả lập nhưng được ép buộc thật (`003`). Tính năng thứ tư — giao diện người dùng (`004`) — mới có bản đặc tả yêu cầu, **kế hoạch triển khai còn là file mẫu trống, chưa một dòng code**.
- Phần được đầu tư kỹ nhất vẫn không phải là tính năng, mà là **các cơ chế tự động ngăn lỗi kiến trúc** — nay đã có 6 loại, canh từ ranh giới dữ liệu, cấu trúc thư mục, cấu hình định tuyến, ngân sách thời gian giữa các service, tính đầy đủ của tài liệu API, cho tới việc không service nào được chạm dữ liệu khi chưa xác định tenant.
- Có **một lỗi cấu hình thật, đã xác minh trực tiếp trong code**, đang treo: BFF gọi nhầm cổng giữa Baskets và Orders khi chạy theo đúng hướng dẫn quickstart cục bộ (Mục 5, mục 1). Nên ưu tiên sửa sớm vì bất kỳ ai làm theo hướng dẫn chạy thử hôm nay đều sẽ gặp phải.
- Đây vẫn là lựa chọn có chủ đích của Giai đoạn 1 ("chứng minh nền móng đi được trước, thêm nghiệp vụ sau"), không phải dự án chậm tiến độ. Hai việc còn lại của Giai đoạn 1: giao diện React (SCRUM-14, chưa bắt đầu) và chạy toàn bộ bằng một lệnh (SCRUM-15, chưa làm).
- Việc đọc hiểu tiến độ nên dựa vào [`docs/roadmap.md`](roadmap.md) (5 giai đoạn, đang ở giai đoạn 1) và trạng thái `[X]`/`[ ]` trong từng `specs/*/tasks.md` hơn là dựa vào độ dày tài liệu thiết kế trong `docs/`.
