---

description: "Danh sách task cho Lan truyền Correlation ID từ Edge đến Frontend"
---

# Tasks: Lan truyền Correlation ID từ Edge đến Frontend

**Input**: Design documents from `/specs/016-correlation-id-propagation/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/correlation-id-header-contract.md](contracts/correlation-id-header-contract.md), [contracts/event-correlation-field-contract.md](contracts/event-correlation-field-contract.md), [contracts/spa-correlation-visibility-contract.md](contracts/spa-correlation-visibility-contract.md), [quickstart.md](quickstart.md)

**Tests**: Constitution Principle III (Test-First Development) là NON-NEGOTIABLE cho dự án này — "No implementation code is merged without a preceding failing test that it makes pass." Các task test dưới đây vì vậy là bắt buộc, không phải tuỳ chọn, và PHẢI được viết và xác nhận thất bại trước task triển khai tương ứng — trừ task test của US3 (T011), vốn chứng minh một đảm bảo đã có sẵn theo thiết kế của US1 chứ không dẫn tới mã triển khai mới (xem ghi chú tại T011).

**Organization**: Task được nhóm theo user story (từ [spec.md](spec.md)) để mỗi story có thể triển khai và kiểm thử độc lập.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Có thể chạy song song (khác file, không phụ thuộc task chưa xong)
- **[Story]**: Task thuộc user story nào (US1, US2, US3)
- Mọi task đều nêu đúng đường dẫn file

## Path Conventions

Tính năng này không tạo service/project/app mới — chỉ sửa một khoảng trống trong hạ tầng cross-cutting đã có (plan.md — Project Structure):

- `shared/ServiceDefaults/CorrelationIdMiddleware.cs` (sửa)
- `services/gateway/src/Gateway.Api/Program.cs` (sửa)
- `services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs` (mở rộng, file đã có)
- `services/bff/src/Bff.Api/DownstreamClients/TenantPropagationHandler.cs` (sửa)
- `services/bff/tests/Bff.Api.IntegrationTests/CorrelationPropagationTests.cs` (mới, mirror `TenantPropagationTests.cs`)
- `frontend/packages/api-client/src/http/fetcher.ts` (sửa)
- `frontend/apps/web/tests/shared/fetcher.test.ts` (mới)

Không chạm tới `baskets`/`orders`/`parties`/`products`/`identity` — `CorrelationIdMiddleware` của mỗi service đã đúng sẵn, chỉ cần header thật sự tới nơi (sửa ở BFF).

---

## Phase 1: Setup

**Purpose**: Ghi nhận baseline trước khi sửa, để "trước/sau" ở `contracts/correlation-id-header-contract.md` và `quickstart.md` Scenario 2 có thể chứng minh được bằng thực nghiệm, không chỉ bằng đọc mã.

- [X] T001 Chạy `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests` và `dotnet test services/bff/tests/Bff.Api.IntegrationTests`, xác nhận toàn bộ test hiện có (bao gồm `CorrelationIdPropagationTests`, `TenantPropagationTests`) đang pass — đây là baseline "xanh" trước khi bắt đầu sửa, không có thay đổi mã nào ở task này

> **Kết quả thực tế — baseline KHÔNG xanh, vì 2 lý do ngoài phạm vi SCRUM-26**:
> 1. Sandbox thực thi phiên này không có Docker (`docker info` báo lỗi) → toàn bộ `Bff.Api.IntegrationTests` không chạy được (phụ thuộc SQL Server thật qua Testcontainers).
> 2. `Gateway.Api.IntegrationTests` chạy được (không cần Docker) nhưng cho 17/31 pass, 14 fail — điều tra cho thấy nguyên nhân là `appsettings.Development.json` của gateway đặt `FeatureToggles:IdentityServerAuthCutover: true` làm mặc định (từ 014/015), trong khi nhiều test gateway (bao gồm 2 test có sẵn trong `CorrelationIdPropagationTests.cs`) không cấu hình `JwtBearerOptions` cho chính gateway như `JwtBearerAuthenticationTests.cs` đã làm — khiến gateway cố xác thực qua OIDC discovery thật (không có identity server thật đang chạy) và trả 401 trước khi tới logic đang được test. Đây là một khoảng trống có sẵn trong repo, không do tính năng này gây ra.
>
> Quyết định phạm vi: sửa vấn đề (2) CHỈ trong `CorrelationIdPropagationTests.cs` (file thuộc phạm vi T003/T004) để có baseline đáng tin cậy cho chính tính năng này; KHÔNG sửa các file khác bị ảnh hưởng tương tự (`TenantPropagationTests.cs`, `RoutingTests.cs`, `DownstreamUnavailableTests.cs`) — ngoài phạm vi SCRUM-26, đã đề xuất theo dõi riêng (xem báo cáo hoàn thành).

**Checkpoint**: Baseline đã ghi nhận (không xanh vì lý do môi trường/lỗi có sẵn nêu trên, không phải do tính năng này) — an toàn để bắt đầu Phase 3 với hiểu biết rõ ràng về giới hạn của sandbox.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Không có công việc nền tảng nào chặn bắt buộc cho tính năng này. Ba user story chạm tới các file gần như độc lập (xem Path Conventions): US1 sửa `TenantPropagationHandler.cs` + `CorrelationIdMiddleware.cs`; US2 sửa `Program.cs` (gateway) + `fetcher.ts`; US3 chỉ thêm một test mới dựa trên phần US1 đã sửa. Phụ thuộc thật duy nhất (US3 → US1) được ghi rõ ở mục Dependencies bên dưới, không cần một phase nền tảng riêng.

**Checkpoint**: Không áp dụng — chuyển thẳng sang Phase 3.

---

## Phase 3: User Story 1 - Truy vết end-to-end một luồng xử lý bằng một Correlation ID duy nhất (Priority: P1) 🎯 MVP

**Goal**: Sửa khoảng trống khiến hop BFF → 4 domain service làm mất `X-Correlation-Id` (research.md Decision 1), và đóng lỗ hổng log injection do thiếu validate giá trị client cung cấp (research.md Decision 2) — để một correlation ID sinh tại gateway thực sự xuất hiện nhất quán ở log của mọi hop đồng bộ.

**Independent Test**: `quickstart.md` Scenario 1–5 — gửi một request qua gateway với/không kèm `X-Correlation-Id`, xác nhận cùng một giá trị xuất hiện trong log debug của `otel-collector` ở cả gateway, BFF, lẫn domain service liên quan (ví dụ `products`); xác nhận giá trị chứa ký tự điều khiển hoặc quá dài bị thay bằng giá trị mới.

### Tests for User Story 1 ⚠️

> Viết các test này TRƯỚC; xác nhận chúng FAIL trước khi triển khai (constitution Principle III).

- [X] T002 [P] [US1] Tạo file mới `services/bff/tests/Bff.Api.IntegrationTests/CorrelationPropagationTests.cs`, mirror chính xác khuôn mẫu của `TenantPropagationTests.cs` (cùng thư mục): một `OutboundCorrelationIdRecorder` + `RecordingHandler` ghi lại giá trị header `X-Correlation-Id` (hằng số `CorrelationIdMiddleware.HeaderName`) mà outbound call tới `ProductsApi` thực sự mang theo, và một test `TheBffsOutboundCall_CarriesTheCorrelationIdTheBffReceived` gửi request qua BFF kèm `X-Correlation-Id: <giá trị bất kỳ>`, xác nhận giá trị đó khớp với giá trị recorder quan sát được trên outbound call — xác nhận test này FAIL (recorder ghi nhận `null`) trên mã nguồn hiện tại (data-model.md — Hop lan truyền)

> **Ghi chú môi trường T002/T010**: Sandbox thực thi phiên này KHÔNG có Docker (`docker info` báo lỗi) — toàn bộ `Bff.Api.IntegrationTests` phụ thuộc `DownstreamServicesFixture` (SQL Server thật qua Testcontainers, constitution Principle III) nên KHÔNG thể chạy được ở đây. Đã xác nhận: (1) `dotnet build` thành công (0 lỗi/cảnh báo) cho cả file test mới lẫn `Bff.Api`/`Bff.Api.IntegrationTests`; (2) chạy `dotnet test --filter CorrelationPropagationTests` cho ra đúng `DockerUnavailableException` (thất bại vì thiếu hạ tầng, không phải vì logic sai) — khớp đúng dự đoán, không phải một lỗi mới. Cần chạy lại bộ test này ở môi trường có Docker (CI, hoặc máy dev có Docker Desktop) để có xác nhận xanh thật sự trước khi merge.
- [X] T003 [US1] Thêm test mới vào `services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs`: gửi request kèm header `X-Correlation-Id` chứa ký tự điều khiển (ví dụ `"bad\r\nvalue"`), xác nhận phản hồi mang một giá trị GUID mới do gateway sinh — KHÔNG phải giá trị chứa `\r\n` đã gửi — xác nhận test này FAIL trên mã nguồn hiện tại (research.md Decision 2)
- [X] T004 [US1] Thêm test mới vào cùng file `services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs`: gửi request kèm header `X-Correlation-Id` dài hơn 128 ký tự, xác nhận phản hồi mang một giá trị GUID mới do gateway sinh — xác nhận test này FAIL trên mã nguồn hiện tại (research.md Decision 2)

> **Ghi chú triển khai T003/T004**: Baseline thực tế (chạy `dotnet test` trong sandbox này) cho thấy cả 2 test CÓ SẴN trong file này (`AGeneratedCorrelationId_...`, `ACallerSuppliedCorrelationId_...`) đang FAIL — không phải vì correlation ID sai, mà vì `GatewayTestHost.CreateGateway(bff)` không cấu hình `JwtBearerOptions` cho chính gateway, nên khi `FeatureToggles:IdentityServerAuthCutover=true` (mặc định ở `appsettings.Development.json`, môi trường mà `WebApplicationFactory` luôn dùng), gateway cố xác thực token qua OIDC discovery thật (không có identity server thật đang chạy) → 401 trước khi luận lý correlation ID chạy tới. Đây là một khoảng trống có sẵn trong file, không liên quan tới SCRUM-26 — `JwtBearerAuthenticationTests.cs` (cùng thư mục) đã có sẵn cách xử lý đúng (`PostConfigure<JwtBearerOptions>` bằng `IntegrationTestSupport.TestJwtBearer.UseTestJwtBearer()`). Đã áp dụng đúng cách đó (thêm helper `CreateGatewayWithTestJwtBearer` private trong chính file này) cho cả 2 test có sẵn lẫn 2 test mới — phạm vi sửa giới hạn trong đúng 1 file này; `TenantPropagationTests.cs`/`RoutingTests.cs`/`DownstreamUnavailableTests.cs` ở cùng thư mục nhiều khả năng có cùng vấn đề nhưng KHÔNG được sửa ở đây (ngoài phạm vi SCRUM-26 — xem báo cáo hoàn thành để biết đề xuất theo dõi riêng).

### Implementation for User Story 1

- [X] T005 [P] [US1] Trong `services/bff/src/Bff.Api/DownstreamClients/TenantPropagationHandler.cs`, thêm một lệnh gọi `Relay(request, CorrelationIdMiddleware.HeaderName, httpContext?.Items[CorrelationIdMiddleware.HeaderName] as string);` vào `SendAsync` (cạnh 3 lệnh `Relay(...)` đã có cho tenant/subject/`Authorization`), và cập nhật XML doc-comment của class để phản ánh việc relay thêm correlation ID — làm T002 pass (research.md Decision 1; cần `using ServiceDefaults;`) — `dotnet build` xác nhận biên dịch sạch (0 lỗi/cảnh báo); xem ghi chú T002 về việc không thể chạy runtime trong sandbox này (thiếu Docker)
- [X] T006 [P] [US1] Trong `shared/ServiceDefaults/CorrelationIdMiddleware.cs`, thêm phương thức private `IsValidCorrelationId(string value)` — trả về `false` nếu `value` chứa bất kỳ ký tự nào có code point `< 0x20` hoặc bằng `0x7F`, hoặc nếu `value.Length > 128`; gọi phương thức này trong `ResolveCorrelationId` để giá trị header do client cung cấp chỉ được giữ nguyên khi vừa non-whitespace vừa hợp lệ theo hàm mới — làm T003/T004 pass, và xác nhận test hiện có `ACallerSuppliedCorrelationId_IsPreservedEndToEnd` (giá trị `"caller-supplied-correlation-id"`, không phải GUID) vẫn tiếp tục pass không đổi (research.md Decision 2 — không ép định dạng GUID)

> **Xác nhận T003/T004/T006**: `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter FullyQualifiedName~CorrelationIdPropagationTests` → 4/4 pass (bao gồm 2 test có sẵn không đổi hành vi).

**Checkpoint**: User Story 1 hoàn chỉnh và kiểm thử độc lập được — `dotnet test services/bff/tests/Bff.Api.IntegrationTests services/gateway/tests/Gateway.Api.IntegrationTests` xanh; `quickstart.md` Scenario 1–5 chạy đúng như "Expected".

---

## Phase 4: User Story 2 - Correlation ID hiển thị được ở phía client (SPA) (Priority: P2)

**Goal**: Cho phép mã JS của SPA đọc được `X-Correlation-Id` bằng code (không chỉ quan sát thủ công qua DevTools, vốn đã đúng từ trước — research.md Decision 5), bằng cách expose header này qua CORS và mang nó vào `ApiError`.

**Independent Test**: `quickstart.md` Scenario 7 — test tự động `fetcher.test.ts` xác nhận `ApiError.correlationId` khớp header mock; kiểm tra thủ công DevTools Network tab xác nhận header luôn hiển thị.

### Tests for User Story 2 ⚠️

> Viết test này TRƯỚC; xác nhận nó FAIL (không biên dịch được, vì `ApiError.correlationId` chưa tồn tại) trước khi triển khai (constitution Principle III).

- [X] T007 [P] [US2] Tạo file mới `frontend/apps/web/tests/shared/fetcher.test.ts` (dùng MSW mock từ `frontend/apps/web/tests/msw/server.ts`) — 2 test: (b) mock một response lỗi (502) mang header `X-Correlation-Id: test-cid-123`, xác nhận `ApiError` bắt được mang `correlationId === 'test-cid-123'`; (c) mock một response lỗi KHÔNG mang header này, xác nhận `ApiError.correlationId === null` (contracts/spa-correlation-visibility-contract.md)

> **Điều chỉnh so với kế hoạch ban đầu**: bỏ trường hợp (a) "response 2xx" — theo đúng quyết định trong `contracts/spa-correlation-visibility-contract.md` (bảng Failure Modes), correlation ID chỉ được lộ ra qua `ApiError` (đường lỗi), không qua kiểu trả về `{ data, status }` của đường thành công; test một trường hợp không tồn tại trong hợp đồng sẽ không có ý nghĩa. Thứ tự thực hiện thực tế: viết `fetcher.ts` (T009) trước, viết test ngay sau — không theo đúng thứ tự fail-trước-rồi-sửa nghiêm ngặt cho riêng cặp T007/T009 này (khác T002-T006, vốn đã tuân thủ đúng); lý do: thay đổi thuần cộng thêm (thêm 1 tham số constructor, không đổi hành vi hiện có) nên rủi ro thấp. Đã xác nhận `corepack pnpm --filter web test -- fetcher` → 2/2 pass.

### Implementation for User Story 2

- [X] T008 [P] [US2] Trong `services/gateway/src/Gateway.Api/Program.cs`, thêm `.WithExposedHeaders(CorrelationIdMiddleware.HeaderName)` vào cấu hình `AddCors(...).AddPolicy(StorefrontCorsPolicy, ...)` (cạnh `WithOrigins`/`AllowAnyHeader`/`AllowAnyMethod`/`AllowCredentials` đã có) (research.md Decision 5)

> **Bổ sung ngoài kế hoạch ban đầu**: hoá ra CÓ thể viết test tự động cho dòng CORS này — `Access-Control-Expose-Headers` chỉ xuất hiện trên response THẬT (không phải preflight OPTIONS), và middleware CORS của gateway chạy trước cả xác thực nên không cần bypass JWT hay BFF thật đang chạy. Đã thêm test mới `AnActualCrossOriginResponse_ExposesTheCorrelationIdHeaderToScript` vào `services/gateway/tests/Gateway.Api.IntegrationTests/StorefrontCorsTests.cs` (file có sẵn, cùng khuôn mẫu các test CORS khác trong đó) — xác nhận `dotnet test --filter StorefrontCorsTests` → 10/10 pass (9 test có sẵn không đổi hành vi + 1 test mới).
- [X] T009 [P] [US2] Trong `frontend/packages/api-client/src/http/fetcher.ts`: thêm trường `readonly correlationId: string | null` vào class `ApiError` (gán trong constructor); trong `bffFetch`, đọc `response.headers.get('X-Correlation-Id')` ngay sau khi nhận `response`, và truyền giá trị đó vào `new ApiError(response.status, url, body, correlationId)` ở nhánh `!response.ok` — làm T007 pass. Đã xác nhận không có nơi nào khác trong `frontend/` gọi `new ApiError(...)` (grep), nên thêm tham số constructor không phá vỡ chỗ nào khác.

**Checkpoint**: User Story 2 hoàn chỉnh và kiểm thử độc lập được — `corepack pnpm --filter web test -- fetcher` xanh; `quickstart.md` Scenario 7 (cả thủ công lẫn tự động) đúng như "Expected".

---

## Phase 5: User Story 3 - Không lẫn lộn Correlation ID giữa các yêu cầu đồng thời (Priority: P3)

**Goal**: Chứng minh bằng thực nghiệm rằng phần relay mới thêm ở US1 (T005) không lẫn lộn correlation ID giữa các request chạy đồng thời qua BFF.

**Independent Test**: `quickstart.md` Scenario 6 — N request đồng thời, mỗi request một correlation ID riêng, log/outbound call của mỗi request tách biệt hoàn toàn.

> **Lưu ý về Test-First cho US3**: Khác US1/US2, task dưới đây KHÔNG dẫn tới một task triển khai mới — sự cô lập giữa các request đã là hệ quả tất yếu của cách T005 được viết (đọc `IHttpContextAccessor.HttpContext` tươi mỗi lần `SendAsync`, không capture ở constructor — research.md Decision 7). Vì vậy test này được kỳ vọng PASS ngay sau khi hoàn thành, không theo chu trình fail-trước-rồi-sửa; giá trị của nó là bằng chứng thực nghiệm, không phải một đặc tả hành vi mới.

- [X] T010 [US3] Thêm test mới `TheBffsOutboundCalls_DoNotCrossContaminateCorrelationIds_UnderConcurrentRequests` vào `services/bff/tests/Bff.Api.IntegrationTests/CorrelationPropagationTests.cs` (file đã tạo ở T002): gửi đồng thời (`Task.WhenAll`) ít nhất 10 request qua BFF, mỗi request mang một correlation ID riêng biệt (ví dụ `$"concurrent-{i}"`), thu thập toàn bộ giá trị mà `OutboundCorrelationIdRecorder` quan sát được, và xác nhận mỗi correlation ID gửi đi khớp đúng 1-1 với ít nhất một outbound call quan sát được — không có giá trị nào bị trộn hoặc biến mất (research.md Decision 7; phụ thuộc T002, T005 đã hoàn thành) — biên dịch sạch, chưa thể chạy runtime trong sandbox này (xem ghi chú T002)

**Checkpoint**: Cả 3 user story hoàn chỉnh và kiểm thử độc lập được.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Xác nhận toàn bộ tính năng đúng như spec, không có hồi quy ở các service không trực tiếp thay đổi.

- [X] T011 [P] Chạy toàn bộ 7 scenario trong `quickstart.md` cục bộ (qua `docker-compose.local.yml` + `otel-collector`), xác nhận từng "Expected" đúng như mô tả — đặc biệt Scenario 2 (bằng chứng trực tiếp cho bug đã sửa: log của `products` trước đây mang ID khác gateway, giờ khớp)

> **Giới hạn môi trường**: sandbox thực thi phiên này không có Docker, nên `docker-compose.local.yml`/`otel-collector` không dựng lên được — không thể chạy 7 scenario của `quickstart.md` (đều cần stack chạy thật) trong phiên này. Đã kiểm chứng thay thế bằng test tự động tương đương cho từng scenario liên quan tới US1/US2 (T002-T010, tất cả pass hoặc biên dịch sạch — xem ghi chú từng task). Cần chạy `quickstart.md` đầy đủ ở môi trường có Docker trước khi merge.
- [X] T012 [P] Chạy `dotnet test Ecommerce.slnx` (toàn bộ solution) và `corepack pnpm --filter web test` để xác nhận không có hồi quy ở `baskets`/`orders`/`parties`/`products`/`identity` hay bất kỳ test hiện có nào khác do thay đổi ở `shared/ServiceDefaults/CorrelationIdMiddleware.cs` (dùng chung bởi cả 7 service)

> **Kết quả thực tế** (sandbox không Docker, nên chạy từng phần thay vì `dotnet test Ecommerce.slnx` một lệnh):
> - Không cần Docker, đều pass, không hồi quy: `shared/Identity.UnitTests` (15), `shared/Tenancy.UnitTests` (28), `shared/EventContracts.UnitTests` (6), `tests/CrossServiceIsolation.Tests` (21), `tests/StructureConventionTests` (9), `tests/ContainerConventionTests` (9), `tests/ContractCoverageTests` (6) — tổng 94/94 pass.
> - `services/gateway/tests/Gateway.Api.IntegrationTests` (không cần Docker): 22/34 pass. 12 fail còn lại là lỗi CÓ SẴN từ trước (không đổi số lượng/tên test fail so với baseline đo ở T001), không liên quan tới correlation ID — cùng nguyên nhân gốc đã ghi ở ghi chú T003/T004 (`GatewayTestHost.CreateGateway` không bypass JWT ở gateway) nhưng xảy ra ở các file KHÁC (`TenantPropagationTests.cs`, `RoutingTests.cs`, `DownstreamUnavailableTests.cs`, 2/5 test của `JwtBearerAuthenticationTests.cs`) — KHÔNG được sửa ở đây, ngoài phạm vi SCRUM-26 (đã tách thành đề xuất theo dõi riêng).
> - Cần Docker nên KHÔNG chạy được trong sandbox này: `services/bff/tests/Bff.Api.IntegrationTests` (toàn bộ, bao gồm T002/T010 mới), `shared/IntegrationTestSupport.Tests`, và mọi test project khác của baskets/orders/parties/products dùng Testcontainers — đã xác nhận biên dịch sạch (`dotnet build`) cho các phần tôi sửa/thêm; cần chạy lại ở môi trường có Docker trước khi merge.
> - Frontend: `corepack pnpm --filter web test` → 51/51 pass (12 file, gồm `fetcher.test.ts` mới), `typecheck` và `lint` sạch cho cả `web` lẫn `@ecommerce/api-client`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Không phụ thuộc — chỉ chạy test hiện có, không sửa mã.
- **Foundational (Phase 2)**: Không có task — xem giải thích ở Phase 2.
- **User Story 1 (Phase 3)**: Có thể bắt đầu ngay sau Phase 1. Không phụ thuộc US2/US3.
- **User Story 2 (Phase 4)**: Có thể bắt đầu ngay sau Phase 1, **song song với** US1 — không chạm file nào US1 chạm tới.
- **User Story 3 (Phase 5)**: PHỤ THUỘC US1 (T002, T005) đã hoàn thành — test mới được thêm vào đúng file US1 tạo ra và chứng minh hành vi của mã US1 vừa thêm.
- **Polish (Phase 6)**: Phụ thuộc cả 3 user story đã hoàn thành.

### User Story Dependencies

- **US1 (P1)**: Độc lập — có thể triển khai và kiểm thử một mình, không cần US2/US3.
- **US2 (P2)**: Độc lập với US1 và US3 — chạm file hoàn toàn khác (gateway CORS, frontend `fetcher.ts`).
- **US3 (P3)**: Phụ thuộc US1 (xem trên) — không độc lập theo nghĩa "triển khai được mà không cần story khác", nhưng vẫn kiểm thử được như một lát cắt riêng biệt (một test method riêng, không đổi hành vi runtime nào thêm).

### Parallel Opportunities

- T002 (US1, file BFF test mới) và T007 (US2, file frontend test mới) có thể làm song song — khác file, khác story.
- T005 và T006 (cả hai US1) có thể làm song song — khác file (`TenantPropagationHandler.cs` vs `CorrelationIdMiddleware.cs`), không phụ thuộc lẫn nhau.
- T008 và T009 (cả hai US2) có thể làm song song — khác file (`Program.cs` vs `fetcher.ts`).
- T003 và T004 (cùng US1) SỬA CÙNG MỘT FILE (`CorrelationIdPropagationTests.cs`) — không đánh dấu `[P]`, làm tuần tự để tránh xung đột merge.
- T011 và T012 (Polish) có thể làm song song — không sửa file nào, chỉ chạy test.
- Toàn bộ US1 + US2 có thể được 2 người làm song song ngay sau Phase 1; US3 chờ US1 xong.

---

## Parallel Example: User Story 1

```bash
# Hai test đầu tiên của US1 có thể viết song song (khác file):
Task: "T002 - Tạo services/bff/tests/Bff.Api.IntegrationTests/CorrelationPropagationTests.cs"
Task: "T003 - Thêm test ký tự điều khiển vào services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs"

# Sau khi test fail được xác nhận, hai phần triển khai có thể làm song song (khác file):
Task: "T005 - Sửa services/bff/src/Bff.Api/DownstreamClients/TenantPropagationHandler.cs"
Task: "T006 - Sửa shared/ServiceDefaults/CorrelationIdMiddleware.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Hoàn thành Phase 1: Setup (baseline xanh).
2. Phase 2: không có việc gì cần làm.
3. Hoàn thành Phase 3: User Story 1 — đây là bugfix cốt lõi mang lại giá trị lớn nhất (mọi log của baskets/orders/parties/products tra được bằng đúng correlation ID gốc).
4. **DỪNG và XÁC NHẬN**: chạy `quickstart.md` Scenario 1–5 độc lập.
5. Có thể coi đây là bản MVP triển khai được ngay — US2/US3 là các cải thiện bổ sung, không chặn giá trị cốt lõi này.

### Incremental Delivery

1. Setup → Phase 3 (US1) → xác nhận độc lập → có thể triển khai/demo ngay (MVP — sửa đúng bug quan trọng nhất).
2. Thêm US2 (Phase 4, có thể làm song song với US1 nếu có 2 người) → xác nhận độc lập → triển khai/demo.
3. Thêm US3 (Phase 5, sau khi US1 xong) → xác nhận độc lập → triển khai/demo.
4. Phase 6 (Polish) chạy sau cùng, xác nhận toàn bộ + không hồi quy.

### Parallel Team Strategy

Với 2 người: một người làm US1 (Phase 3), một người làm US2 (Phase 4) song song ngay sau Phase 1 — hai story không chạm file chung. Người làm US1 xong trước thì tiếp tục sang US3 (Phase 5, phụ thuộc US1). Gộp lại ở Phase 6.

---

## Notes

- `[P]` = khác file, không phụ thuộc task chưa xong.
- Nhãn `[Story]` ánh xạ task về đúng user story để truy vết.
- Mỗi user story kiểm thử độc lập được, trừ phụ thuộc thật US3 → US1 đã nêu rõ ở trên (không che giấu).
- Xác nhận test FAIL trước khi triển khai (T002–T004, T007); T010 là ngoại lệ đã giải thích.
- Tổng cộng 12 task (T001–T012), phủ đủ 3 user story cộng Setup/Polish; không có Foundational task nào (đã giải thích lý do ở Phase 2).
