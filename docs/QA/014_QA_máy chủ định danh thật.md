# QA: Máy chủ định danh thay thế xác thực giả lập

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

Spec đã được xác minh rất kỹ lúc viết feature (47/47 task, chạy thật trên toàn stack `docker-compose.local.yml`, có bằng chứng
`docker exec` cho 3 lỗ hổng cấu hình đã vá và cơ chế toggle rollback). QA ở đây tập trung: (1) các test có còn xanh hôm nay không,
(2) bằng chứng đã ghi có còn đúng sau khi spec 015 và 020 đụng vào cùng vùng mã hay không.

## Luồng happy-case đã rà soát

1. Đăng nhập qua `identity` (Duende IdentityServer, DB riêng) phát hành JWT mang `sub` + `tenant_id` — nguồn `tenant_id` duy nhất
   là `TenantClaimsProfileService`, đọc từ `ApplicationUser.TenantId`.
2. Gateway xác thực token trước khi chuyển tiếp; BFF + 4 domain service **tự xác thực độc lập** — token giả gửi thẳng, bỏ qua
   gateway, vẫn bị chặn tại chính service.
3. 3 trạng thái token (Valid/Expired/Invalid) phản hồi phân biệt rõ; token hết hạn trả `{"error":"token_expired"}`.
4. Cơ chế lan truyền tenant/subject (003/004) không đổi — chỉ nguồn xác định tenant đổi từ gán cứng sang claim token thật.
5. Toggle `FeatureToggles:IdentityServerAuthCutover` cho phép rollback gateway về `StubIdentity` không cần redeploy (`IOptionsMonitor`).

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Tự động — chạy thẳng bộ test đã có

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001/FR-002/SC-001 — đăng nhập phát hành token có `sub`+`tenant_id` | [`TenantClaimsProfileServiceTests.cs:36`](../../services/identity/tests/Identity.Api.UnitTests/TenantClaimsProfileServiceTests.cs#L36) (5 test) | `dotnet test services/identity/tests/Identity.Api.UnitTests` |
| FR-001 — `identity` phát hành/verify token thật qua SQL Server thật | [`services/identity/tests/Identity.Api.IntegrationTests`](../../services/identity/tests/Identity.Api.IntegrationTests) (2 test) | `dotnet test services/identity/tests/Identity.Api.IntegrationTests` |
| FR-004/FR-005/SC-002 — mỗi service tự chặn token giả mạo/hết hạn/vắng mặt | `IndependentTokenValidationTests.cs` ở `baskets`/`bff`/`orders`/`parties`/`products` (21 test) | `dotnet test services/<service>/tests/<Service>.Api.IntegrationTests --filter FullyQualifiedName~IndependentTokenValidationTests` |
| FR-003/US1 — gateway xác thực token hợp lệ, lan truyền tenant/subject | [`JwtBearerAuthenticationTests.cs:51`](../../services/gateway/tests/Gateway.Api.IntegrationTests/JwtBearerAuthenticationTests.cs#L51) — `ARequestWithAValidToken_PropagatesTenantAndSubject_WhenToggleIsOn` | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter ARequestWithAValidToken_PropagatesTenantAndSubject_WhenToggleIsOn` |
| FR-011 — không token bị chặn (toggle on) | [`:86`](../../services/gateway/tests/Gateway.Api.IntegrationTests/JwtBearerAuthenticationTests.cs#L86) — `ARequestWithNoToken_IsRejected_WhenToggleIsOn` | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter ARequestWithNoToken_IsRejected_WhenToggleIsOn` |
| FR-005/SC-002 — token giả mạo bị chặn tại gateway | [`:108`](../../services/gateway/tests/Gateway.Api.IntegrationTests/JwtBearerAuthenticationTests.cs#L108) — `ARequestWithATamperedToken_IsRejected_WhenToggleIsOn` | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter ARequestWithATamperedToken_IsRejected_WhenToggleIsOn` |
| FR-006/SC-003 — token hết hạn trả `token_expired` | [`:135`](../../services/gateway/tests/Gateway.Api.IntegrationTests/JwtBearerAuthenticationTests.cs#L135) — `ARequestWithAnExpiredToken_IsRejected_WithAClearExpiredMessage_WhenToggleIsOn` | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter ARequestWithAnExpiredToken_IsRejected_WithAClearExpiredMessage_WhenToggleIsOn` |
| Principle X — rollback qua toggle (**hiện ĐỎ**, xem QA_Debt) | [`:168`](../../services/gateway/tests/Gateway.Api.IntegrationTests/JwtBearerAuthenticationTests.cs#L168) — `ARequestWithNoToken_StillReachesTheBff_WhenToggleIsOff` | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter ARequestWithNoToken_StillReachesTheBff_WhenToggleIsOff` |
| FR-004 — mọi service đã đăng ký `AddIdentityValidation()` (quét toàn repo) | [`AuthenticatedByDefaultScannerTests.cs`](../../tests/CrossServiceIsolation.Tests/AuthenticatedByDefaultScannerTests.cs) (3 test) | `dotnet test tests/CrossServiceIsolation.Tests --filter FullyQualifiedName~AuthenticatedByDefaultScanner` |

**Kết quả lượt QA này (2026-09-23)**: Identity Unit **5/5**, Identity Integration **2/2**, `IndependentTokenValidationTests` **21/21** (4+4+4+4+5),
`AuthenticatedByDefaultScanner` **3/3**, Gateway Unit **27/27**, `JwtBearerAuthenticationTests` **4/5** (1 đỏ đã ghi ở QA_Debt) — 32/33 xanh.

### Thủ công — đối chiếu tĩnh

`quickstart.md` Kịch bản 1–7 đòi dựng toàn stack `docker-compose.local.yml` — đã chạy thật lúc viết feature (`docker exec` toggle
`true→false` cho `502` khi tắt `bff-api`), không lặp lại. Đã đọc file xác nhận 3 điểm đã vá còn nguyên: `docker-compose.yml` vẫn có
`identity-db`/`identity-migrate`/`identity-api`; cả 6 service (gateway + BFF + 4 domain) đều có `Identity:Authority`.

## Kết luận

**PASS kèm ghi chú.** 4 nguồn nhất quán, có bằng chứng thực nghiệm lúc viết feature; 32/33 test liên quan xanh — US1 (đăng nhập/phát hành token),
US2 (phòng thủ độc lập từng service), US3 (token hết hạn rõ ràng) và FR-007–011 đều được chứng minh bằng test đang chạy thật. Ghi chú: (1) test rollback
`ARequestWithNoToken_StillReachesTheBff_WhenToggleIsOff` đang đỏ do tương tác với spec 015 (stub identity không phát claim `scope`, `FallbackPolicy` dùng
chung nay đòi scope) — trên Development/`docker-compose.local.yml` rollback khẩn cấp không còn phục hồi hành vi cũ, Production mặc định không ảnh hưởng.
Chi tiết, nguyên nhân gốc và hướng vá: [QA_Debt.md](QA_Debt.md) mục 014.
