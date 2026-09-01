# Phase 0 Research: Triển khai máy chủ định danh, thay thế xác thực giả lập

Không còn `[NEEDS CLARIFICATION]` nào trong Technical Context của `plan.md` — mọi lựa chọn kỹ thuật dưới đây được suy ra từ [ADR-0001](../../docs/adr/0001-identity-provider.md), [ADR-0008](../../docs/adr/0008-feature-toggle-system.md), constitution, và mã nguồn hiện có (đặc biệt là `services/gateway/src/Gateway.Api/Identity/StubIdentityAuthenticationHandler.cs`, vốn đã ghi chú trước rằng SCRUM-23 sẽ thay nó).

## Decision 1: Dùng Duende IdentityServer, triển khai như một service mới `services/identity`

**Decision**: Máy chủ định danh là Duende IdentityServer, đóng gói thành một service ASP.NET Core mới (`services/identity/src/Identity.Api`), theo đúng khuôn mẫu container/pipeline/Ansible mà mọi service khác trong hạm đội đang dùng.

**Rationale**: [ADR-0001](../../docs/adr/0001-identity-provider.md) đã chốt sản phẩm này ở cấp kiến trúc — lý do chính là nhất quán vận hành: mọi thành phần khác của nền tảng đều là container C#/.NET được quan sát qua cùng một `ServiceDefaults` và triển khai qua cùng một pipeline. Tính năng này là nơi hiện thực hoá quyết định đó, không phải nơi chọn lại nó.

**Alternatives considered**: Không có — việc chọn sản phẩm nằm ngoài phạm vi tính năng này, đã được quyết định ở cấp ADR trước khi Jira SCRUM-23 tồn tại.

## Decision 2: Ở gateway, thay đúng một dòng đăng ký — `AddScheme<...>` → `AddJwtBearer(...)`

**Decision**: `builder.Services.AddAuthentication(StubIdentityAuthenticationHandler.SchemeName).AddScheme<StubIdentityAuthenticationSchemeOptions, StubIdentityAuthenticationHandler>(...)` trong `services/gateway/src/Gateway.Api/Program.cs` được thay bằng `AddAuthentication(...).AddJwtBearer(options => options.Authority = <identity-service-url>)`.

**Rationale**: Đây chính xác là điều dòng comment hiện tại trong `Program.cs` đã dự đoán ("Phase 3 (SCRUM-23) swaps this one registration for AddJwtBearer(...) and nothing downstream changes"). `AddJwtBearer` là extension point chuẩn của ASP.NET Core cho xác thực Bearer/JWT, tương thích trực tiếp với `AuthenticationHandler<TOptions>` mà stub identity đã dùng — cùng một điểm mở rộng, không phải một kiến trúc khác.

**Alternatives considered**: Viết một `AuthenticationHandler` tuỳ biến tự gọi tới máy chủ định danh — bị bác bỏ vì `AddJwtBearer` đã là giải pháp chuẩn, được chính comment trong code hiện tại và ADR-0001 giả định trước; tự viết lại là công sức thừa và rủi ro lệch chuẩn OIDC.

## Decision 3: Ánh xạ claim mặc định giữ nguyên — `TenantHeaderPropagationMiddleware`/`SubjectHeaderPropagationMiddleware` không cần sửa

**Decision**: Máy chủ định danh phát hành token với claim `sub` (chuẩn OIDC) và một claim tuỳ biến `tenant_id` — đúng tên mà `StubIdentityAuthenticationHandler.TenantClaimType` đã dùng. `JwtBearerOptions.MapInboundClaims` giữ giá trị mặc định (`true`), khiến ASP.NET Core tự ánh xạ `sub` → `ClaimTypes.NameIdentifier`, còn `tenant_id` (không phải claim chuẩn) đi qua nguyên vẹn, không đổi tên.

**Rationale**: Đây chính xác là hai claim mà `TenantHeaderPropagationMiddleware` (đọc `TenantClaimType` = `"tenant_id"`) và `SubjectHeaderPropagationMiddleware` (đọc `ClaimTypes.NameIdentifier`) đã đọc từ `StubIdentityAuthenticationHandler`. Kết quả: hai middleware này — và toàn bộ `shared/Tenancy` phía sau chúng — không cần sửa một dòng code nào (spec FR-008 thoả mãn structurally, không phải bằng kỷ luật code review).

**Alternatives considered**: Đặt `MapInboundClaims = false` và tự ánh xạ thủ công trong một `ClaimsTransformation` — bị bác bỏ vì không cần thiết; hành vi mặc định của ASP.NET Core đã khớp chính xác với những gì downstream mong đợi, thêm một bước ánh xạ thủ công chỉ tăng bề mặt có thể sai.

## Decision 4: Xác thực JWT độc lập ở BFF và 4 domain service qua thư viện chia sẻ mới `shared/Identity`

**Decision**: Thêm một thư viện chia sẻ mới `shared/Identity` (sibling của `shared/Tenancy`) cung cấp `AddIdentityValidation()`/`UseIdentityValidation()`, đóng gói cấu hình `AddJwtBearer` dùng chung. BFF và cả 4 domain service (parties, products, baskets, orders) gọi đúng hai dòng này thay vì tự cấu hình `AddJwtBearer` thủ công.

**Rationale**: Đúng tiền lệ mà [003-stub-identity-tenant-context/research.md](../003-stub-identity-tenant-context/research.md) đã lập cho `shared/Tenancy`: "một mối quan tâm xuyên suốt, dùng giống hệt ở mọi nơi, không phải thứ cấu hình tay từng service" (áp dụng lại nguyên lý mà Principle VII đã đặt ra cho `ServiceDefaults`). Hiện tại cả 5 service (BFF + 4 domain service) đều hoàn toàn không xác thực gì — mỗi service tự cấu hình `AddJwtBearer` sẽ là 5 bản sao gần giống nhau, đúng kiểu trôi dạt (một service quên áp policy, một service khác cấu hình sai `Authority`) mà một thư viện chia sẻ ngăn được.

**Alternatives considered**: Copy cấu hình `AddJwtBearer` vào từng `Program.cs` — bị bác bỏ vì lặp lại đúng rủi ro trôi dạt nêu trên; không có lợi ích nào so với một thư viện chia sẻ.

## Decision 5: Xác thực bằng JWKS đã cache cục bộ — không gọi trực tiếp máy chủ định danh mỗi request

**Decision**: `AddJwtBearer` dùng `Authority`/`MetadataAddress` trỏ tới tài liệu khám phá OIDC (`/.well-known/openid-configuration`) của máy chủ định danh; ASP.NET Core's `ConfigurationManager` tự tải và cache JWKS (khoá công khai dùng để xác minh chữ ký), làm mới định kỳ theo chu kỳ mặc định — không gọi introspection endpoint trên mỗi request.

**Rationale**: Thoả trực tiếp edge case trong spec: "máy chủ định danh tạm thời không khả dụng → token đã phát hành, còn hạn, vẫn tiếp tục được xác thực độc lập". Xác minh chữ ký cục bộ (kiểm tra `exp`, `iss`, `aud`, chữ ký RS256 bằng khoá đã cache) không cần máy chủ định danh phải "sống" tại thời điểm xác thực — chỉ cần đã lấy được JWKS một lần trước đó. Cách này cũng tránh thêm một lệnh gọi mạng vào đường đi của mỗi request, giữ ngân sách hiệu năng hiện có (constitution Principle VIII) không đổi.

**Alternatives considered**: Gọi introspection endpoint (`/connect/introspect`) trên mỗi request — bị bác bỏ vì tạo phụ thuộc runtime vào máy chủ định danh cho MỌI request ở MỌI service, đúng thứ edge case ở trên yêu cầu tránh, và thêm một lệnh gọi mạng đồng bộ vào mọi request.

## Decision 6: `FallbackPolicy = RequireAuthenticatedUser()` — phân quyền deny-by-default tường minh

**Decision**: Mỗi service (BFF + 4 domain service + gateway) đặt `AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`. Chỉ những route đã đánh dấu `[AllowAnonymous]` tường minh (health probe: `/health/live`, `/health/ready`) mới được bỏ qua yêu cầu xác thực.

**Rationale**: Constitution Principle VI yêu cầu "mỗi endpoint... khai báo policy của nó một cách tường minh, và một endpoint không có quyết định phân quyền PHẢI fail build hoặc review". `FallbackPolicy` biến "yêu cầu đăng nhập" thành mặc định tường minh cho toàn service — một endpoint mới thêm vào sau này tự động yêu cầu xác thực trừ khi ai đó chủ động đánh dấu `[AllowAnonymous]`, nghĩa là "quên" không còn là một lỗ hổng có thể xảy ra âm thầm. Quyết định này khép lại deviation Principle VI mà [003](../003-stub-identity-tenant-context/plan.md) mang theo (theo dõi bởi SCRUM-23), trong đúng phạm vi Jira SCRUM-23 (xác thực có/không) — RBAC/scope chi tiết theo vai trò là một tính năng riêng, không nằm trong acceptance criteria của ticket này.

**Alternatives considered**: Đánh dấu `[Authorize]` thủ công trên từng endpoint hiện có — bị bác bỏ vì im lặng-mặc-định (một endpoint mới quên đánh dấu `[Authorize]`) chính là lỗ hổng mà "deny-by-default" phải ngăn; `FallbackPolicy` đảo ngược mặc định đó một cách an toàn hơn.

## Decision 7: Bọc việc chuyển scheme ở gateway trong một toggle Unleash

**Decision**: Đăng ký `AddJwtBearer(...)` ở gateway được bọc trong một toggle Unleash ([ADR-0008](../../docs/adr/0008-feature-toggle-system.md)), ví dụ `identity-server-auth-cutover`, có chủ sở hữu (platform maintainers) và ngày gỡ bỏ ghi nhận ngay khi tạo toggle. Khi tắt, gateway quay lại `StubIdentity`; khi bật, gateway dùng `JwtBearer` thật. Rollback là gạt toggle, không cần redeploy.

**Rationale**: Constitution Principle X yêu cầu mọi thay đổi không tầm thường phải có toggle với rollback không cần redeploy. Chuyển đổi cơ chế xác thực cho toàn nền tảng — điểm mà mọi request đều đi qua — là chính xác loại thay đổi rủi ro cao mà nguyên tắc này nhắm tới: nếu cutover phát sinh sự cố (ví dụ cấu hình `Authority` sai khiến mọi request bị từ chối), team cần gạt về trạng thái cũ ngay lập tức, không chờ một lượt deploy mới.

**Alternatives considered**: Triển khai thẳng không toggle, lý luận giống cách [003](../003-stub-identity-tenant-context/plan.md) đã lý luận cho việc lan truyền tenant ("hạ tầng nền tảng, không phải hành vi tuỳ chọn có thể tắt giữa chừng") — bị bác bỏ vì khác với 003: bật/tắt giữa hai scheme xác thực ở đây khả thi về mặt kỹ thuật (cả hai đã tồn tại song song trong code cho tới khi `StubIdentityAuthenticationHandler` được gỡ) và mang lại giá trị rollback thật sự, không giả tạo.

## Decision 8: Máy chủ định danh sở hữu kho thông tin đăng nhập riêng, tách biệt khỏi dữ liệu nghiệp vụ của `parties`

**Decision**: Service `identity` có database riêng của nó, lưu thông tin đăng nhập (username/password hash, liên kết tenant) qua ASP.NET Core Identity — hoàn toàn tách biệt khỏi database của `parties` (hồ sơ khách hàng: tên, địa chỉ, thông tin liên hệ). Hai bên chỉ liên kết logic qua claim `sub` trong token, không chia sẻ bảng hay schema.

**Rationale**: Constitution Principle I: "mỗi service PHẢI sở hữu dữ liệu của mình độc quyền... truy cập dữ liệu chéo service chỉ qua API hoặc event đã công bố". "Danh tính xác thực" (ai vừa đăng nhập, bằng mật khẩu nào) và "hồ sơ khách hàng" (họ tên, địa chỉ giao hàng) là hai mối quan tâm khác nhau về bản chất, dù cùng nói về "một người dùng" — đây cũng là mô hình chuẩn mà mọi identity provider (Duende, Keycloak, Auth0...) đều theo.

**Alternatives considered**: Lưu mật khẩu/thông tin đăng nhập trực tiếp trong database của `parties` — bị bác bỏ vì trộn lẫn dữ liệu bảo mật xác thực với dữ liệu nghiệp vụ, khiến `parties` sở hữu một mối quan tâm (xác thực) mà theo constitution nó không nên sở hữu, và làm phức tạp hoá việc audit/rotate credential riêng biệt với việc sửa hồ sơ khách hàng.

## Decision 9: Trang đăng nhập tương tác do Duende tự host, không xây trong frontend monorepo

**Decision**: Luồng đăng nhập tương tác (Authorization Code + PKCE — chuẩn OIDC cho ứng dụng SPA) dùng trang đăng nhập Razor Pages đi kèm sẵn của Duende IdentityServer, host trực tiếp trên service `identity`. Frontend SPA chỉ redirect người dùng tới đó và nhận lại token qua callback — không có màn hình đăng nhập tuỳ biến nào được xây trong `frontend/`.

**Rationale**: Đây là mô hình OIDC tiêu chuẩn (redirect tới identity provider, không phải ứng dụng client tự thu thập mật khẩu), và giữ Principle IX (Frontend Discipline) hoàn toàn ngoài phạm vi tính năng này — không có component UI mới, không có bundle-size mới cần đo. Đồng thời khớp với spec Assumptions: đăng ký/quản lý tài khoản (bao gồm UI đăng nhập) nằm ngoài phạm vi story này.

**Alternatives considered**: Xây trang đăng nhập tuỳ chỉnh trong `frontend/apps` gọi thẳng một API xác thực tự viết (Resource Owner Password flow) — bị bác bỏ vì đi ngược chuẩn OIDC/khuyến nghị bảo mật hiện đại (ROPC bị khuyến cáo tránh cho ứng dụng SPA), và kéo Principle IX vào phạm vi tính năng này một cách không cần thiết.
