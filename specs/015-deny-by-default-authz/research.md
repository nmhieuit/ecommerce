# Research: Phân quyền từ chối theo mặc định trên mọi endpoint/handler

**Ngày**: 2026-09-02 | **Spec**: [spec.md](spec.md)

Mục tiêu của Phase 0 là giải quyết mọi mục `NEEDS CLARIFICATION` còn lại trong Technical Context của `plan.md` bằng cách đối chiếu với mã nguồn hiện có (không đoán). Mỗi quyết định dưới đây được rút ra từ việc đọc trực tiếp `shared/Identity`, `shared/Tenancy`, các `*Endpoints.cs` hiện có, `services/identity/src/Identity.Api/Config.cs`, và tiền lệ `tests/CrossServiceIsolation.Tests/AuthenticatedByDefaultScanner*.cs` do [014-identity-server-auth](../014-identity-server-auth/) để lại.

## Decision 1 — "Chính sách phân quyền" cụ thể là gì, khi hệ thống chưa có RBAC theo vai trò

**Decision**: Không phát minh vai trò nghiệp vụ mới (admin, operator, v.v.) — hệ thống hiện chỉ có đúng một loại danh tính đã xác thực (khách hàng nền tảng), và không nơi nào trong roadmap/docs hiện tại mô tả một vai trò thứ hai. Thay vào đó, chính sách phân quyền mới dựa trên một claim **đã tồn tại sẵn và đã được cấp thật** trên mọi token hợp lệ: claim `scope` OAuth2/OIDC chuẩn, với giá trị `ecommerce-api` — API scope duy nhất mà `services/identity/src/Identity.Api/Config.cs` (`Config.ApiScopeName`) đã đăng ký và cấp cho cả client SPA (`ecommerce-web-spa`) lẫn client kiểm thử tích hợp (`integration-test-ropc`) từ tính năng 014.

**Rationale**: `AuthenticationFallbackPolicy.Build()` hiện tại (`shared/Identity/AuthenticationFallbackPolicy.cs`) chỉ gọi `RequireAuthenticatedUser()` — nghĩa là một token hợp lệ nhưng **không mang scope `ecommerce-api`** (ví dụ một token chỉ có `openid profile`, phát hành cho một mục đích khác trong tương lai) vẫn vượt qua được mọi endpoint nghiệp vụ hôm nay. Đây là một khoảng trống thật, đã tồn tại sẵn trong mã nguồn, không phải một kịch bản giả định — và việc đóng nó lại chính là điều Test Scenario 2 của Jira SCRUM-24 mô tả ("token thiếu claim theo đúng chính sách yêu cầu → 403, không phải 200"). Dùng `scope` chuẩn OAuth2 thay vì một claim tự chế nghĩa là không cần sửa client SPA hay client kiểm thử — cả hai đã được cấp đúng scope này từ 014.

**Alternatives considered**:
- *Vai trò ASP.NET Identity (`IdentityRole`) mới, ví dụ "Customer"/"Admin"*: `AddIdentity<ApplicationUser, IdentityRole>()` đã sẵn có ở `services/identity/src/Identity.Api/Program.cs`, nhưng chưa có vai trò nào được seed hay cấp claim `role` cho bất kỳ user nào. Tạo một vai trò nghiệp vụ mới để chứng minh cơ chế phân quyền sẽ là việc bịa ra một khái niệm sản phẩm (ai được gán vai trò nào, bằng luồng nào) nằm ngoài phạm vi Jira SCRUM-24 — bị từ chối theo đúng tinh thần constitution Principle I ("unjustified architectural ceremony is a violation").
- *Dựa trên claim `tenant_id` hoặc sự hiện diện của `X-Subject-Id`/`X-Tenant-Id`*: cả hai được lan truyền qua **header** do gateway đặt (`shared/Tenancy`), không phải đọc trực tiếp từ claim JWT tại từng service ở tầng dưới — biến chúng thành yêu cầu của `AuthorizationPolicy` sẽ đòi hỏi đảo thứ tự middleware đã ổn định (`UseIdentityValidation()` hiện chạy **trước** `UseTenancy()` ở mọi service), một thay đổi kiến trúc rủi ro hơn nhiều so với phạm vi tính năng này, và có thể vi phạm ràng buộc "cơ chế lan truyền tenant giữ nguyên" đã được 014 xác lập. Bị loại.

## Decision 2 — Cơ chế khai báo tường minh trên từng endpoint

**Decision**: Mọi lệnh gọi `Map(Get|Post|Put|Delete|Patch)` trong từng `*Endpoints.cs` (baskets, orders, parties, products, và toàn bộ `bff/src/Bff.Api/Features/*/`) PHẢI chain thêm đúng một trong hai: `.RequireAuthorization(AuthorizationPolicies.ApiScope)` (đặt tên hằng số dùng chung trong `shared/Identity`, mirror `AuthenticationFallbackPolicy`) hoặc `.AllowAnonymous()`. `FallbackPolicy` (đã có từ 014) vẫn được giữ và nâng cấp nội dung theo Decision 1 — đóng vai trò lưới an toàn thứ hai cho một endpoint tương lai lỡ quên khai báo, đúng tinh thần "deny-by-default" — nhưng bản thân sự khai báo tường minh tại từng route mới là điều FR-001 yêu cầu và điều cổng build/review ở Decision 3 kiểm chứng.

**Rationale**: Đây là cách tối thiểu, nhất quán với phong cách Minimal API đã dùng trong toàn bộ codebase (không có controller/`[Authorize]` attribute nào tồn tại — xác nhận bằng việc grep `Authorize` trên `services/` không khớp file nào). `.RequireAuthorization(...)`/`.AllowAnonymous()` là "tương đương Minimal-API" chính xác của `[Authorize]`/`[AllowAnonymous]` mà Jira Test Scenario 1 nhắc tới.

**Alternatives considered**: Viết một Roslyn analyzer tùy biến để bắt buộc khai báo tại thời điểm biên dịch — mạnh hơn nhưng là hạ tầng build mới, chi phí cao hơn nhiều so với một bài scanner test cấu trúc đã có tiền lệ hoạt động tốt (Decision 3), và constitution không yêu cầu mức độ đó. Bị hoãn lại như một cải tiến tương lai, không phải yêu cầu của tính năng này.

## Decision 3 — Cơ chế "build hoặc review thất bại" khi thiếu quyết định phân quyền

**Decision**: Thêm một scanner test cấu trúc mới, `AuthorizationPolicyDeclaredScanner` + `AuthorizationPolicyDeclaredScannerTests`, vào `tests/CrossServiceIsolation.Tests` — đọc trực tiếp mã nguồn đã commit của từng `*Endpoints.cs`, đếm số lệnh `Map(Get|Post|Put|Delete|Patch)` và xác nhận mỗi lệnh có đúng một trong hai hậu tố `.RequireAuthorization(`/`.AllowAnonymous()` theo sau. Test thất bại (và do đó chặn merge) nếu tìm thấy bất kỳ route nào thiếu cả hai.

**Rationale**: Đây là bản sao chính xác kiểu dáng của `AuthenticatedByDefaultScanner`/`AuthenticatedByDefaultScannerTests.cs` mà 014 để lại — cùng đọc mã nguồn tĩnh, cùng nằm trong bộ test chạy ở bước "unit test" của pipeline PR gate mà [013-sonarqube-merge-blocker](../013-sonarqube-merge-blocker/) đã xác lập (build → unit test → integration test → contract test → SonarQube quality gate, không có đường vòng). Vì vậy, "build hoặc review thất bại" của FR-004 không cần hạ tầng CI mới — nó tự động được 013 thực thi ngay khi test mới này tồn tại trong bộ test hiện có.

**Alternatives considered**: Một custom SonarQube rule (Roslyn analyzer plugin) — mạnh hơn nhưng đòi hỏi mở rộng hạ tầng SonarQube ([ADR-0012](../../docs/adr/0012-ci-quality-gate-enforcement.md)) theo cách chưa có tiền lệ trong repo này; không cần thiết khi scanner test cấu trúc đã đủ để thỏa FR-004/FR-008 và đã được 014 chứng minh hoạt động.

## Decision 4 — Message handler (FR-002): chưa có handler nào tồn tại hôm nay

**Decision**: Xác nhận bằng cách grep `IConsumer<` trên toàn bộ `services/` — không có kết quả nào. Tính năng này không tạo ra handler mới (nằm ngoài phạm vi Jira SCRUM-24), nhưng thiết lập **hợp đồng bắt buộc** (`contracts/message-handler-authorization-contract.md`) rằng bất kỳ `IConsumer<T>` nào được thêm vào trong tương lai phải khai báo một quyết định tin cậy tường minh (ví dụ một thuộc tính/doc-comment quy ước nêu rõ nguồn phát hành sự kiện được tin cậy), và mở rộng scanner ở Decision 3 để quét toàn bộ `services/**/*.cs` tìm `IConsumer<` — quét này pass rỗng hôm nay (0 handler, 0 vi phạm) nhưng sẽ chặn build ngay khi handler đầu tiên được thêm mà thiếu khai báo.

**Rationale**: FR-002/SC-001 phải đúng cho "mọi message handler", kể cả những handler chưa tồn tại — một guard cấu trúc quét toàn repo thỏa điều đó mà không cần bịa ra một handler giả để có gì đó mà bảo vệ. Điều này nhất quán với cách `ConnectionStringScanner`/`TenantGatedConnectionScanner` (013, 003) đã quét cấu trúc thay vì hành vi runtime.

**Alternatives considered**: Hoãn hoàn toàn FR-002 tới khi handler đầu tiên xuất hiện (tính năng Event-Driven chưa được xây) — bị loại vì spec FR-002 yêu cầu rõ ràng, và một guard có thể viết ngay hôm nay với chi phí thấp thì không có lý do để hoãn.

## Decision 5 — Toggle-gated để có thể rollback không cần redeploy (constitution Principle X)

**Decision**: Việc `AuthenticationFallbackPolicy` (và policy `ApiScope` mới) chuyển từ "chỉ yêu cầu đã xác thực" sang "yêu cầu đã xác thực VÀ đúng scope" được bọc trong một toggle Unleash mới (cùng cơ chế [ADR-0008](../../docs/adr/0008-feature-toggle-system.md) mà 014 đã dùng cho việc chuyển scheme ở gateway) — khi tắt, hành vi rơi về đúng như trước tính năng này (`RequireAuthenticatedUser()` mà không kiểm tra scope); khi bật, yêu cầu scope được thực thi.

**Rationale**: Đây là thay đổi non-trivial chạm tới đường đi của **mọi** request nghiệp vụ trên **mọi** service — nếu việc cấp/đọc claim `scope` có sai sót không lường trước ở môi trường thật, hậu quả là toàn bộ nền tảng từ chối mọi người dùng hợp lệ. Constitution Principle X bắt buộc khả năng rollback không cần redeploy cho đúng loại thay đổi này, và 014 đã thiết lập tiền lệ cụ thể cho một thay đổi xác thực/phân quyền rủi ro tương tự. Sự khai báo tường minh tại từng route (Decision 2) — bản thân "có một quyết định phân quyền" — KHÔNG phụ thuộc toggle này; chỉ nội dung nghiêm ngặt của chính sách mới bị toggle chi phối, nên FR-001 vẫn đúng bất kể trạng thái toggle.

**Alternatives considered**: Không toggle-gate, dựa vào rollback qua redeploy như một thay đổi mã nguồn thông thường — bị loại, vi phạm trực tiếp Principle X cho đúng loại thay đổi mà nguyên tắc này được viết ra để bao phủ.

## Decision 6 — Phản hồi 403 rõ ràng, nhất quán với phong cách 401 đã có

**Decision**: Thêm một cấu hình nhỏ tương tự `ClearUnauthorizedResponseEvents` (014) cho trường hợp bị từ chối bởi chính sách (403) — đăng ký qua `AuthorizationOptions`/`IAuthorizationMiddlewareResultHandler` để trả về một thân JSON rõ ràng (`{ "error": "forbidden_scope", "message": "..." }`) thay vì thân rỗng mặc định của framework.

**Rationale**: Nhất quán với triết lý đã có ở 014 (spec US3/FR-006 của 014: "một từ chối rõ ràng, minh bạch, không phải một thất bại chung chung") — áp dụng lại cho 403 thay vì chỉ 401. Chi phí thấp (một handler nhỏ, không có dependency mới), lợi ích quan sát (Principle VII: một lỗi 403 phải phân biệt được với một 404/500 khi debug từ log).

**Alternatives considered**: Giữ nguyên thân 403 rỗng mặc định của framework — được phép về mặt kỹ thuật (spec chỉ yêu cầu mã trạng thái 403, không yêu cầu hình dạng thân phản hồi), nhưng bị loại vì không nhất quán với tiền lệ đã có và làm giảm khả năng gỡ lỗi.

## Decision 7 — Kiểm tra dữ liệu độc lập phía máy chủ (US3): kiểm kê những gì đã có, không xây lại từ đầu

**Decision**: US3 không tạo ra quy tắc nghiệp vụ mới. Việc rà soát mã nguồn hiện tại cho thấy nhiều cặp quy tắc client/server đã tồn tại sẵn:
- `services/baskets/.../BasketEndpoints.cs`: kiểm tra `Quantity >= 1` và `UnitPrice >= 0` ở phía máy chủ — phía SPA (`AddToBasketButton.tsx`) hiện gửi cứng `quantity: 1`, không có ô nhập số lượng nào ở client để kiểm tra tương ứng (máy chủ đã nghiêm ngặt hơn client, không có khoảng trống).
- `services/bff/.../CheckoutEndpoints.cs`: từ chối checkout khi giỏ hàng rỗng (409) — phía SPA (`CheckoutButton.tsx`) đã vô hiệu hóa nút bấm khi `itemCount === 0` (quy tắc UX tương ứng, đã có kiểm tra phía máy chủ độc lập).

Công việc của US3 là **lập hợp đồng đối chiếu tường minh** (`contracts/client-server-validation-parity-contract.md`) ghi lại từng cặp này, và bổ sung integration test gọi thẳng API — bỏ qua SPA — để chứng minh bằng test tự động (không chỉ khẳng định bằng lời) rằng máy chủ tự từ chối, theo đúng tiền lệ `IndependentTokenValidationTests.cs` (014) đã làm cho việc xác thực token.

**Rationale**: Tránh việc "sửa một thứ chưa hỏng" — cả hai quy tắc hiện có đều đã đúng theo tinh thần US3; điều còn thiếu chỉ là (a) một tài liệu đối chiếu tường minh để việc thêm quy tắc SPA mới trong tương lai không quên phần máy chủ, và (b) test tự động xác nhận độc lập, thay vì suy luận từ việc đọc mã.

**Alternatives considered**: Xây thêm ô nhập số lượng ở SPA kèm kiểm tra client-side để có "quy tắc SPA mới cần đối chiếu" — bị loại, đây là thay đổi UI nằm ngoài phạm vi Jira SCRUM-24 (một câu chuyện DevOps về phân quyền/kiểm tra dữ liệu, không phải một câu chuyện UX mới).
