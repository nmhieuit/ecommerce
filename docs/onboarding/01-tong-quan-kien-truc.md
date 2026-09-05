# 01 — Tổng quan kiến trúc & chức năng toàn repo

> **Đối tượng đọc:** Software Manager có nền tảng lập trình nhưng **chưa từng làm việc trực tiếp với .NET/C#**.
> **Mục tiêu:** đọc xong tài liệu này, bạn nắm được repo gồm những gì, mỗi phần làm gì, và các khái niệm .NET lặp đi lặp lại khắp nơi nghĩa là gì — đủ để đọc code, review PR, và nói chuyện kỹ thuật với đội engineering.
> Đây là tài liệu onboarding cấp repo, không thay thế các tài liệu đặc tả từng tính năng ở `docs/summary/` và `docs/architecture/` (xem [Đi đâu tiếp theo](#đi-đâu-tiếp-theo) ở cuối bài).

---

## 1. Repo này là gì

Đây là một nền tảng thương mại điện tử demo, viết theo kiến trúc **microservices**, nằm trong **một repo duy nhất** (monorepo) gồm 2 phần:

- **Backend** (`services/`, `shared/`, `tests/`) — .NET 10 / C#, mỗi service là 1 tiến trình HTTP độc lập, mỗi service có database SQL Server riêng.
- **Frontend** (`frontend/`) — ứng dụng SPA (React/TypeScript, quản lý bằng pnpm/Turborepo), không phải trọng tâm tài liệu này.

Toàn bộ backend được ghép lại và chạy bằng Docker Compose — xem [docker-compose.local.yml](../../docker-compose.local.yml), file cấu hình "chạy thử toàn bộ hệ thống trên máy" với mọi port được publish ra ngoài để có thể gọi trực tiếp bằng Postman/curl.

## 2. Các thành phần & vai trò

| Thành phần | Vai trò | Có database riêng? | Gọi trực tiếp từ ngoài? |
|---|---|---|---|
| **gateway** (`services/gateway`) | Cổng vào duy nhất của storefront. Xác thực, CORS, reverse-proxy (dựa trên thư viện **YARP**) mọi request sang BFF. Không chứa logic nghiệp vụ. | Không | Có (port 5300) |
| **bff** (`services/bff`, Backend-For-Frontend) | Tầng tổng hợp: mỗi route ở đây gọi 1 hoặc vài service nghiệp vụ phía sau qua HTTP, định hình lại response cho đúng nhu cầu của SPA. Không chứa logic nghiệp vụ, chỉ tổng hợp/định hình. | Không | Có, nhưng bình thường chỉ gateway gọi (5301) |
| **identity** (`services/identity`) | Máy chủ định danh (OAuth2/OIDC), dựa trên thư viện **Duende IdentityServer**. Phát hành access token (JWT) sau khi xác thực username/password; giữ danh sách user, client, scope. | Có (`identity-db`) | Có, để lấy token (5205) |
| **products** (`services/products`) | Danh mục sản phẩm (catalog), đọc là chính. | Có (`products-db`) | Có (5088) |
| **baskets** (`services/baskets`) | Giỏ hàng của người dùng. | Có (`baskets-db`) | Có (5188) |
| **orders** (`services/orders`) | Đơn hàng — xem chi tiết đầy đủ ở [02-orders-service-va-cac-api-endpoint.md](02-orders-service-va-cac-api-endpoint.md). | Có (`orders-db`) | Có (5041) |
| **parties** (`services/parties`) | Thông tin khách hàng/đối tác. | Có (`parties-db`) | Có (5204) |
| **shared/** | Các thư viện .NET dùng chung, KHÔNG phải service — được các service trên tham chiếu như thư viện (giống việc nhiều app Node cùng `import` 1 package nội bộ). Gồm: `Identity` (xác thực/phân quyền), `Tenancy` (tenant/caller context), `ServiceDefaults` (logging/tracing chung), `EventContracts` (schema sự kiện), `IntegrationTestSupport` (hạ tầng test dùng chung). | — | — |
| **tests/** | Các bộ test **chạy ngang toàn repo**, không thuộc về 1 service — chủ yếu là "scanner" đọc source code tĩnh để khẳng định một quy ước kiến trúc được tuân thủ ở MỌI service (ví dụ cụ thể ở [06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md](06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md)). | — | — |

Ghi chú: `baskets`, `orders`, `parties`, `products` có cấu trúc gần như giống hệt nhau (cùng pattern, khác tên miền nghiệp vụ) — đó là lý do Tài liệu 2 chỉ giải thích chi tiết 1 service (Orders) rồi liệt kê điểm khác biệt của 3 service còn lại, thay vì lặp lại 4 lần.

## 3. Các khái niệm .NET lặp lại khắp repo — giải thích không cần biết .NET trước

Bạn sẽ thấy các từ khoá sau trong hầu như mọi file `Program.cs`. Dưới đây là "bản dịch" sang khái niệm lập trình tổng quát.

| Khái niệm .NET | Nó là gì | Tương đương ở framework khác |
|---|---|---|
| **Minimal API** (`app.MapGet(...)`, `app.MapPost(...)`) | Cách khai báo 1 route HTTP trực tiếp bằng 1 lambda, không cần class Controller. | Giống `app.get('/orders/:id', handler)` của Express.js, hoặc route handler của FastAPI/Flask. |
| **Dependency Injection (DI)** (`builder.Services.AddXxx(...)`, tham số constructor/lambda tự động được "bơm" vào) | Một registry trung tâm giữ các "dịch vụ" (DbContext, HttpClient, v.v.), framework tự truyền chúng vào nơi cần dùng thay vì code tự `new` ra. | Giống Spring's `@Autowired`, hoặc NestJS's DI container. |
| **Middleware pipeline** (`app.UseXxx(...)`, gọi theo thứ tự trước `app.MapXxx(...)`) | Chuỗi các bước xử lý mọi request phải đi qua tuần tự (log, xác thực, phân quyền, resolve tenant...) trước khi tới route handler thực sự. | Giống middleware chain của Express.js/Koa, hoặc filter chain của Django. |
| **`IAuthorizationHandler` / policy** | Đơn vị logic quyết định 1 request có được phép gọi 1 route hay không, gắn vào route qua tên ("policy"). | Giống guard/interceptor của NestJS, hoặc decorator `@login_required`/`@permission_required` của Django. |
| **EF Core** (Entity Framework Core) — `DbContext`, `Migrations/` | ORM: ánh xạ class C# ↔ bảng SQL, sinh SQL từ LINQ. `Migrations/` là các file version hoá schema, chạy tuần tự để đưa database từ trạng thái này sang trạng thái khác. | Giống Prisma/TypeORM (Node), SQLAlchemy + Alembic (Python), ActiveRecord + Rails migrations (Ruby). |
| **xUnit** | Framework viết test (`[Fact]` = 1 test case, tương đương `it()`/`test()`). | Giống Jest/Mocha (JS), pytest (Python). |
| **Testcontainers** | Thư viện khởi động container Docker thật (SQL Server thật) ngay trong lúc chạy test, để test integration chạy trên database thật thay vì mock. | Cùng ý tưởng có ở mọi ngôn ngữ (testcontainers.org là dự án đa ngôn ngữ, không riêng .NET). |
| **`IOptionsMonitor<T>` / feature toggle** | Đọc 1 giá trị cấu hình (`appsettings.json`) tại **mỗi lần dùng**, không cache lúc khởi động — nghĩa là đổi file cấu hình là có hiệu lực ngay, không cần khởi động lại service. | Giống việc đọc config từ 1 feature-flag service (LaunchDarkly...) nhưng ở đây chỉ là file JSON được theo dõi live. |
| **`WebApplicationFactory<Program>`** | Cách khởi động cả một service **trong bộ nhớ** để test gọi HTTP thật vào nó mà không cần `dotnet run` một tiến trình riêng. | Giống `supertest` chạy thẳng lên Express app instance (Node), hoặc Django's `test.Client`. |

## 4. Luồng 1 request đi qua hệ thống

```
Trình duyệt (SPA)
   │  HTTPS, kèm Bearer token (JWT) nếu đã đăng nhập
   ▼
gateway-api (5300)
   │  1. CORS check
   │  2. Xác thực token (UseAuthentication)
   │  3. Phân quyền (UseAuthorization — deny-by-default, xem Tài liệu 4)
   │  4. Đọc claim từ token → gắn header X-Tenant-Id / X-Subject-Id
   │  5. Forward nguyên request sang BFF (reverse-proxy bằng YARP)
   ▼
bff-api (5301)
   │  1. Xác thực token LẦN NỮA (độc lập, không tin tưởng gateway đã làm — xem mục 6)
   │  2. Đọc X-Tenant-Id từ header (không tự resolve lại)
   │  3. Gọi 1 hoặc nhiều service nghiệp vụ qua HttpClient có timeout/retry
   │  4. Định hình lại response cho đúng nhu cầu SPA
   ▼
orders-api / baskets-api / products-api / parties-api (mỗi cái 1 port riêng)
   │  1. Xác thực token LẦN NỮA (độc lập)
   │  2. Resolve tenant, bắt buộc phải có trước khi mở kết nối DB
   │  3. Logic nghiệp vụ + đọc/ghi database RIÊNG của mình
   ▼
SQL Server riêng của từng service (không service nào đọc được DB của service khác)
```

Điểm quan trọng cần nhớ: **mỗi service tự xác thực token của chính nó**, không có chuyện "gateway đã kiểm tra rồi nên phía sau tin luôn". Đây là quyết định kiến trúc có chủ đích (xem mục 6 và [ADR-0001](../adr/0001-identity-provider.md), [ADR-0002](../adr/0002-api-gateway.md)) — lý do và cách hoạt động của policy phân quyền "deny-by-default" được giải thích sâu ở [06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md § Authentication/Authorization](06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md#thay-đổi-trong-shared).

Diagram trực quan hơn: [current-state-architecture.drawio](../diagrams/current-state-architecture.drawio) (đã có sẵn trong repo, mức tổng quan toàn hệ thống — tài liệu này không vẽ lại).

## 5. Vertical slice — quy ước tổ chức code chung mọi service

Mọi service nghiệp vụ (baskets/orders/parties/products) tổ chức code theo **"vertical slice"**: 1 chức năng = 1 thư mục `Features/<TênChứcNăng>/` chứa cả route mapping lẫn shape dữ liệu trả về, thay vì tách theo lớp kỹ thuật (Controllers/, Services/, Models/ riêng biệt như nhiều codebase .NET truyền thống khác). Ví dụ: `services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs` chứa toàn bộ route `/orders` của service Orders.

Lý do (trích comment trong code): giữ 1 chức năng "trọn vẹn" trong 1 chỗ, dễ đọc từ đầu đến cuối hơn phải nhảy qua 3-4 thư mục để hiểu 1 route làm gì.

## 6. Vì sao mỗi service tự xác thực (không tin gateway)?

Đây là điểm mà một Software Manager quen kiến trúc "gateway xác thực 1 lần, phía sau tin tưởng" sẽ thấy khác biệt. Repo này chọn: **mọi service xác thực token độc lập** (constitution Principle V, đặc tả ở `specs/014-identity-server-auth`). Lý do thực dụng: một service domain có thể trong tương lai bị gọi trực tiếp (bỏ qua gateway) — nếu chỉ gateway xác thực, đường tắt đó sẽ hoàn toàn không được bảo vệ. Cái giá phải trả là mỗi request được xác thực nhiều lần (gateway → BFF → service), nhưng đổi lại không có "mắt xích yếu" nào trong chuỗi.

## Đi đâu tiếp theo

- **Tài liệu 2** — [02-orders-service-va-cac-api-endpoint.md](02-orders-service-va-cac-api-endpoint.md): đi sâu vào 1 service cụ thể (Orders) để hiểu API/endpoint hoạt động thế nào, rồi áp dụng cách hiểu đó sang các service còn lại.
- **Tài liệu 3-6** — code shared/service nghiệp vụ thay đổi qua từng giai đoạn thật của repo, đối chiếu `specs/` và git commit: [03-giai-doan-1-nen-tang-dich-vu-va-routing.md](03-giai-doan-1-nen-tang-dich-vu-va-routing.md), [04-giai-doan-2-spa-va-demo-end-to-end.md](04-giai-doan-2-spa-va-demo-end-to-end.md), [05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md](05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md), [06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md](06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md).
- **Đặc tả từng tính năng** (`specs/0NN-*/spec.md`) và **tài liệu kiến trúc/tóm tắt cho từng tính năng** (`docs/architecture/0NN_Architect_*.md`, `docs/summary/0NN_PO_*.md`) — tài liệu này KHÔNG lặp lại nội dung đó, chỉ tổng hợp góc nhìn "đọc code" xuyên suốt repo.
- **ADR** (`docs/adr/000N-*.md`) — các quyết định kiến trúc lớn (chọn Duende IdentityServer, YARP làm gateway, pattern BFF, v.v.), kèm lý do và phương án đã cân nhắc.
