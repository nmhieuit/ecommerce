---

description: "Task list template for feature implementation"
---

# Tasks: Secrets qua Cluster Secret Store, loại bỏ cấu hình hardcode

**Input**: Design documents from `/specs/018-cluster-secret-store/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Constitution Principle III (Test-First Development) là NON-NEGOTIABLE cho repository này, nên các task test được đưa vào đầy đủ — không phải tùy chọn — và PHẢI được viết trước, xác nhận FAIL, rồi mới implement.

**Organization**: Task được nhóm theo user story trong `spec.md` để mỗi story implement và test được độc lập.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Có thể chạy song song (khác file, không phụ thuộc task chưa xong)
- **[Story]**: User story mà task thuộc về (US1, US2, US3)
- Mỗi task nêu rõ đường dẫn file

## Path Conventions

Monorepo hiện có — không dùng cấu trúc mẫu `src/`/`tests/` gốc. Đường dẫn thực tế theo `plan.md` §Project Structure: `shared/ServiceDefaults/`, `services/<service>/src/<Project>/`, `services/<service>/tests/`, `deploy/k8s/<service>/` (mới), `scripts/ci/`, `Jenkinsfile`, `docker/ci/jenkins.Dockerfile`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Hạ tầng CI dùng chung cho cả hai công cụ secret-scanning, và khung thư mục khai báo mới

- [X] T001 [P] Tạo `.gitleaks.toml` ở gốc repo với ruleset baseline mặc định của gitleaks (chưa cần allowlist cụ thể ở bước này)
- [X] T002 [P] Thêm cài đặt gitleaks CLI vào `docker/ci/jenkins.Dockerfile`
- [X] T003 [P] Thêm cài đặt Trivy CLI vào `docker/ci/jenkins.Dockerfile`
- [X] T004 [P] Tạo `deploy/k8s/README.md` giải thích quan hệ giữa thư mục này với `docs/adr/0007-secrets-delivery.md` (manifest khai báo, Vault/ESO thật chưa được provision — xem research.md #1)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cơ chế fail-fast dùng chung trong `shared/ServiceDefaults` mà mọi service ở User Story 2 sẽ cần

**⚠️ CRITICAL**: Không service nào được wiring vào cơ chế này trước khi Phase này hoàn thành

- [X] T005 [P] Viết unit test (kỳ vọng FAIL) cho logic validate secret bắt buộc trong `shared/ServiceDefaults.UnitTests/RequiredSecretsValidationTests.cs` (project test mới, theo convention `*.UnitTests` đã có của `Tenancy.UnitTests`/`EventContracts.UnitTests`) — case: thiếu `RequiredSecret` → validation thất bại rõ ràng; case: đủ giá trị hợp lệ → pass — **xác nhận RED**: 16 lỗi CS0246/CS0103 (type chưa tồn tại)
- [X] T006 Tạo `shared/ServiceDefaults/RequiredSecretsValidation.cs` định nghĩa record `RequiredSecret` (Name, Resolve) và logic `IValidateOptions` fail-fast theo data-model.md §1, để T005 chuyển PASS (depends on T005) — ghi chú: dùng `Func<IConfiguration,string?> Resolve` thay vì field `OwningService`/`Source`/`IsSensitive` riêng lẻ của data-model.md — `Name` đã mang đủ thông tin để đối chiếu hợp đồng, và `Resolve` là cách duy nhất thực sự cần để validate; giữ implementation tối giản theo tinh thần "no premature abstraction". **Sửa bổ sung khi implement T019 (US2)**: phát hiện `appsettings.json` (Production) vẫn có giá trị `ConnectionStrings` non-blank hợp lệ theo chủ đích (chỉ host/database, không credential — đúng contracts/service-configuration-contract.md rule 2) — nếu chỉ check "non-blank" thì fail-fast sẽ KHÔNG bao giờ kích hoạt khi cluster quên inject credential, vì giá trị base đã không rỗng. Đã thêm `HasCredential(...)` vào `RequiredSecret.ConnectionString(...)`: resolve về `null` (coi là thiếu) trừ khi chuỗi có `Password=`/`Pwd=`/`Integrated Security=true`/`Trusted_Connection=true` — viết test RED trước (`ConnectionString_TreatsAHostOnlyValueWithNoCredential_AsMissing`), xác nhận FAIL, rồi fix để GREEN (13/13 pass)
- [X] T007 Thêm extension method `AddRequiredSecretsValidation(...)` gọi `ValidateOnStart()` trong `shared/ServiceDefaults/ServiceDefaultsExtensions.cs` (depends on T006) — **xác nhận GREEN**: `dotnet test shared/ServiceDefaults.UnitTests` → 8/8 pass

**Checkpoint**: Foundation sẵn sàng — User Story 1 và User Story 2 có thể bắt đầu

---

## Phase 3: User Story 1 - Loại bỏ secret hardcode khỏi source, config và image (Priority: P1) 🎯 MVP (phần 1/2)

**Goal**: Không còn connection string/khóa/token hardcode trong `appsettings*.json` đã commit hay Dockerfile; vi phạm bị chặn tự động bởi CI

**Independent Test**: `gitleaks detect --source . --log-opts="--all"` trả về 0 phát hiện; `git grep` trên `services/**/appsettings*.json` không còn khớp credential nào (Kịch bản 1 trong quickstart.md)

### Implementation for User Story 1

- [X] T008 [P] [US1] Xoá mật khẩu literal khỏi `services/baskets/src/Baskets.Api/appsettings.Development.json`, thay bằng hướng dẫn `dotnet user-secrets` cho phần credential (theo contracts/service-configuration-contract.md) — kèm thêm `<UserSecretsId>` vào `Baskets.Api.csproj` để lệnh hướng dẫn thực sự chạy được
- [X] T009 [P] [US1] Xoá mật khẩu literal khỏi `services/orders/src/Orders.Api/appsettings.Development.json`, thay bằng hướng dẫn `dotnet user-secrets` — kèm `<UserSecretsId>` vào `Orders.Api.csproj`
- [X] T010 [P] [US1] Xoá mật khẩu literal khỏi `services/parties/src/Parties.Api/appsettings.Development.json`, thay bằng hướng dẫn `dotnet user-secrets` — kèm `<UserSecretsId>` vào `Parties.Api.csproj`
- [X] T011 [P] [US1] Xoá mật khẩu literal khỏi `services/products/src/Products.Api/appsettings.Development.json`, thay bằng hướng dẫn `dotnet user-secrets` — kèm `<UserSecretsId>` vào `Products.Api.csproj`
- [X] T012 [P] [US1] Xoá mật khẩu literal khỏi `services/identity/src/Identity.Api/appsettings.Development.json`, thay bằng hướng dẫn `dotnet user-secrets` — kèm `<UserSecretsId>` vào `Identity.Api.csproj`
- [X] T013 [P] [US1] Rà soát `services/gateway/src/Gateway.Api/appsettings.Development.json`, xác nhận không có secret (đã sạch theo khảo sát research Phase 0) — xác nhận lại bằng đọc trực tiếp file: chỉ có Cors/Identity Authority/FeatureToggles/ReverseProxy/Logging, không có credential
- [X] T014 [P] [US1] Rà soát `services/bff/src/Bff.Api/appsettings.Development.json`, xác nhận không có secret — xác nhận lại: chỉ có Services BaseUrl/Identity Authority/FeatureToggles/Logging, không có credential
- [X] T015 [US1] Rà soát toàn bộ `services/*/src/*/Dockerfile` bằng `git grep` cho ENV/ARG giống secret — `git grep -niE "password=|apikey|secret" services/*/src/*/Dockerfile` không có kết quả nào, xác nhận sạch
- [X] T016 [US1] Tạo `scripts/ci/run-secret-scan.sh` chạy gitleaks quét toàn bộ lịch sử git bằng `.gitleaks.toml`, exit non-zero khi có finding, không in giá trị secret ra output (theo contracts/ci-secret-scan-stage-contract.md) (depends on T001) — **phát sinh ngoài kế hoạch, đã xử lý và ghi vào research.md Decision 6**: ruleset mặc định của gitleaks không khớp pattern connection-string password (verify thực nghiệm bằng `docker run zricethezav/gitleaks`), nên đã thêm custom rule `connection-string-password` vào `.gitleaks.toml`; rule này sau đó khớp lại 9 commit lịch sử đã hardcode mật khẩu dev (trước khi T008-T012 dọn sạch working tree) — dùng `--baseline-path .gitleaks-baseline.json` (9 finding đã biết, đã chốt) để gate chỉ chặn finding MỚI, không yêu cầu viết lại lịch sử git
- [X] T017 [US1] Thêm stage `secret scan` vào `Jenkinsfile` gọi `scripts/ci/run-secret-scan.sh`, publish check `ci/secret-scan` qua `githubNotify` (không dùng `publishChecks`, theo bài học ADR-0012) (depends on T002, T016)
- [X] T018 [US1] Chạy Kịch bản 1 trong `quickstart.md` để xác thực User Story 1 end-to-end (depends on T008-T017) — **xác thực thực tế bằng Docker** (`zricethezav/gitleaks:latest`, do agent không có quyền cài binary hệ thống): quét 117 commit, toàn bộ lịch sử git → `no leaks found` (exit 0) sau khi áp `.gitleaks-baseline.json`; `git grep` xác nhận không còn credential thật trong `appsettings*.json` đã commit

**Checkpoint**: User Story 1 hoạt động và test được độc lập

---

## Phase 4: User Story 2 - Inject secret vào service tại thời điểm chạy trong cluster (Priority: P1) 🎯 MVP (phần 2/2)

**Goal**: Service nhận secret bắt buộc từ biến môi trường/K8s Secret tại runtime; dừng khởi động rõ ràng khi thiếu; image build không chứa secret

**Independent Test**: Khởi động một service thiếu biến môi trường bắt buộc → service dừng khởi động với lỗi rõ ràng; build image → Trivy secret-scan báo 0 phát hiện (Kịch bản 2 trong quickstart.md)

### Tests for User Story 2 ⚠️

> Viết test này TRƯỚC, xác nhận FAIL trước khi thực hiện task implementation tương ứng

- [X] T019 [P] [US2] Viết integration test (kỳ vọng FAIL) trong `services/orders/tests/Orders.Api.IntegrationTests/RequiredSecretsFailFastTests.cs`: khởi động `Orders.Api` khi thiếu biến môi trường `ConnectionStrings__Default` bắt buộc → xác nhận service dừng khởi động với lỗi có cấu trúc nêu tên secret còn thiếu — **xác nhận RED**: `Assert.ThrowsAny<Exception>` fail vì host khởi động bình thường (chưa wiring). Test dùng `appsettings.json` (Production, không set env var override) thay vì xoá hẳn key, vì đó mới là tình huống thật khi cluster quên inject secret — phát hiện ra vấn đề dẫn tới sửa T006 (xem trên)

### Implementation for User Story 2

- [X] T020 [US2] Wiring `AddRequiredSecretsValidation()` vào `services/orders/src/Orders.Api/Program.cs`, khai báo `RequiredSecret` cho `ConnectionStrings:Default`, để T019 chuyển PASS (depends on T007, T019) — **xác nhận GREEN**: `dotnet test --filter RequiredSecretsFailFastTests` → 1/1 pass. Tên connection string thực tế là `OrdersDb` (không phải `Default` — mỗi service dùng key `<Service>Db` riêng, xem `service-manifest.yaml`), tasks.md đã điều chỉnh theo thực tế code
- [X] T021 [P] [US2] Wiring `AddRequiredSecretsValidation()` vào `services/baskets/src/Baskets.Api/Program.cs`, khai báo `RequiredSecret` cho connection string SQL Server (không có Redis — baskets hiện chưa dùng Redis trong code, dù constitution nêu Redis là default nền tảng) (depends on T007). **Regression phát hiện sau khi chạy toàn bộ suite `Orders.Api.IntegrationTests` (Failed: 6/25)**: `AuthorizationPolicyTests` và `IndependentTokenValidationTests` ở cả 4 service (orders/baskets/parties/products) dựng `WebApplicationFactory<Program>` mà không set `ConnectionStrings:*Db` — trước đây host vẫn khởi động được vì EF Core không kết nối DB ngay; giờ `RequiredSecretsValidation` (đúng như thiết kế) khiến host dừng khởi động. Đây không phải lỗi của validation mà là các test cũ chưa từng cần secret thật. Đã thêm `shared/IntegrationTestSupport/RequiredSecretsTestSupport.cs` (extension `UseUnreachableRequiredSecret(string dbKey)`, dùng chung giữa 4 service, cùng giá trị `UnreachableCredentialedConnectionString` mà `ReadinessTests.cs` mỗi service từng tự khai riêng) và cập nhật `CreateFactory()` của 8 file (`AuthorizationPolicyTests.cs`/`IndependentTokenValidationTests.cs` × orders/baskets/parties/products) để gọi thêm `.UseUnreachableRequiredSecret("<Service>Db")`. **Xác nhận GREEN sau fix**: orders 7/7, baskets 6/6, parties 6/6, products 7/7 (tất cả AuthorizationPolicyTests + IndependentTokenValidationTests + RequiredSecretsFailFastTests), rebuild toàn solution 0 lỗi/0 cảnh báo.
- [X] T022 [P] [US2] Wiring `AddRequiredSecretsValidation()` vào `services/parties/src/Parties.Api/Program.cs`, khai báo `RequiredSecret` cho connection string (depends on T007)
- [X] T023 [P] [US2] Wiring `AddRequiredSecretsValidation()` vào `services/products/src/Products.Api/Program.cs`, khai báo `RequiredSecret` cho connection string (depends on T007)
- [X] T024 [P] [US2] Wiring `AddRequiredSecretsValidation()` vào `services/identity/src/Identity.Api/Program.cs`, khai báo `RequiredSecret` cho connection string (không có khóa ký JWT — Program.cs hiện không cấu hình signing credential tường minh, Duende dùng developer signing credential mặc định; đây là khoảng trống có sẵn từ trước, ghi chú trong `deploy/k8s/identity/external-secret.yaml`, ngoài phạm vi feature này) (depends on T007)
- [X] T025 [P] [US2] Tạo `deploy/k8s/orders/external-secret.yaml` và `deploy/k8s/orders/secret.example.yaml` theo contracts/external-secret-manifest-contract.md (depends on T004, T020)
- [X] T026 [P] [US2] Tạo `deploy/k8s/baskets/external-secret.yaml` và `deploy/k8s/baskets/secret.example.yaml` (depends on T004, T021)
- [X] T027 [P] [US2] Tạo `deploy/k8s/parties/external-secret.yaml` và `deploy/k8s/parties/secret.example.yaml` (depends on T004, T022)
- [X] T028 [P] [US2] Tạo `deploy/k8s/products/external-secret.yaml` và `deploy/k8s/products/secret.example.yaml` (depends on T004, T023)
- [X] T029 [P] [US2] Tạo `deploy/k8s/identity/external-secret.yaml` và `deploy/k8s/identity/secret.example.yaml` (depends on T004, T024)
- [X] T030 [US2] Tạo `scripts/ci/run-image-secret-scan.sh` chạy Trivy secret-scan trên image đã build, exit non-zero khi có finding (theo contracts/ci-secret-scan-stage-contract.md) (depends on T003)
- [X] T031 [US2] Thêm stage `image secret scan` vào `Jenkinsfile` (sau stage `build`) gọi `scripts/ci/run-image-secret-scan.sh`, publish check `ci/image-secret-scan` qua `githubNotify` (depends on T030)
- [X] T032 [US2] Chạy Kịch bản 2 trong `quickstart.md` để xác thực User Story 2 end-to-end (depends on T019-T031) — **xác thực đầy đủ cả 3 phần trên cluster Docker Desktop Kubernetes thật** (2026-09-07): (2a) `dotnet test --filter RequiredSecretsFailFastTests` → host dừng khởi động đúng khi thiếu credential; (2b) `docker build` + Trivy scan → 0 secret trong image; (2c) cài Vault dev-mode + External Secrets Operator qua Helm, tạo `ClusterSecretStore`, apply đúng `deploy/k8s/orders/external-secret.yaml` của repo → `ExternalSecret` báo `SecretSynced`, `Secret` K8s tạo ra có `ConnectionStrings__OrdersDb` khớp chính xác giá trị seed trong Vault; chạy pod thật với `envFrom.secretRef.name: orders-secrets` → biến môi trường đến container đúng giá trị. Phát hiện & sửa trong lúc chạy: ESO bản mới chỉ serve `external-secrets.io/v1` (không còn `v1beta1`) — đã sửa cả 5 manifest + contract (research.md Decision 7). Giới hạn môi trường: cluster test 2-node không chia sẻ image store với `docker build` cục bộ nên không pull được image `orders-api` thật vào pod (`ErrImageNeverPull`, vấn đề phân phối image của môi trường test, không phải lỗi cơ chế secret) — đã thay bằng pod `busybox` public image để xác thực trực tiếp env var, kết hợp bằng chứng đã có (WebApplicationFactory test + Trivy) vẫn phủ đầy đủ FR-002/FR-003/SC-003

**Checkpoint**: User Story 1 VÀ User Story 2 hoạt động độc lập — đây là phạm vi MVP đầy đủ (cả hai đều P1)

---

## Phase 5: User Story 3 - Xoay vòng secret mà không cần redeploy (Priority: P2)

**Goal**: `ExternalSecret` đồng bộ lại từ Vault trong tối đa 5 phút mà không cần redeploy service

**Independent Test**: Xoay vòng giá trị secret trong Vault dev-mode của cluster thử nghiệm, không redeploy, xác nhận K8s `Secret` cập nhật trong ≤5 phút (Kịch bản 3 trong quickstart.md)

### Implementation for User Story 3

- [X] T033 [P] [US3] Tạo `scripts/ci/validate-external-secrets.sh` đối chiếu `refreshInterval` (≤5m) và `secretKey` của mọi `deploy/k8s/*/external-secret.yaml` với `RequiredSecret.Name` tương ứng trong code, theo contracts/external-secret-manifest-contract.md (depends on T025-T029) — **xác nhận chạy thật**: cả 5 manifest đều `refreshInterval=5m (300s): OK` và `secretKey` khớp đúng `RequiredSecret.ConnectionString(...)` tương ứng trong `Program.cs`
- [X] T034 [US3] Chạy Kịch bản 3 trong `quickstart.md` (cần cluster thử nghiệm kind/k3d + Vault dev-mode + ESO, theo điều kiện tiên quyết) để xác thực rotate secret không cần redeploy trong ≤5 phút (SC-004) (depends on T033) — **xác thực thực tế** (2026-09-07, trên cluster Docker Desktop Kubernetes dựng cùng T032): giá trị `Secret` trước rotate = `...Password=TestOnly123!...`; chạy `vault kv put secret/orders connectionstring=...Password=RotatedValue456!...` (Vault version 2); ép `ExternalSecret` đồng bộ lại bằng annotation `force-sync` (KHÔNG `kubectl apply`/redeploy bất kỳ resource nào khác) → `Secret` K8s cập nhật ngay thành `...Password=RotatedValue456!...`, `ExternalSecret` vẫn `SecretSynced`/`Ready=True`, `refreshInterval` giữ nguyên `5m` — đúng SC-004

**Checkpoint**: Cả 3 user story hoạt động độc lập

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Hoàn thiện tài liệu, xác nhận CI gate chặn merge thật, và rà soát sót lại ngoài phạm vi appsettings

- [X] T035 [P] Thêm Amendment vào `docs/adr/0007-secrets-delivery.md` ghi nhận: phần application-side contract (config, fail-fast, CI gate) đã hoàn thành tại `specs/018-cluster-secret-store/`; Action Items 1–3 (provision Vault/ESO thật) vẫn còn mở
- [X] T036 [P] Ghi chú follow-up cập nhật `scripts/ci/setup-branch-protection.sh`/required checks để thêm `ci/secret-scan` và `ci/image-secret-scan` vào danh sách required status checks (hành động cấp repository admin, theo tiền lệ ADR-0012 Action Item 1) — đã sửa file script (thêm 2 context vào JSON), **chưa** chạy script với quyền admin thật trên `nmhieuit/ecommerce` (hành động ảnh hưởng cấu hình chia sẻ, cần chủ repo tự chạy — đúng như ADR-0012 đã làm cho 5 check đầu)
- [ ] T037 Chạy Kịch bản 4 trong `quickstart.md` (mở một PR sạch và một PR thử chèn secret giả lập) để xác thực `ci/secret-scan`/`ci/image-secret-scan` chặn merge đúng cách (SC-005) (depends on T017, T031) — **CHƯA THỰC HIỆN**: đòi hỏi push nhánh và mở PR thật trên GitHub — hành động hiển thị với người khác, cần người dùng xác nhận/tự thực hiện, ngoài phạm vi một phiên implement tự động
- [X] T038 [P] `git grep` toàn repository (ngoài `appsettings*.json` đã rà ở Phase 3) cho pattern secret còn sót trong mã nguồn (`.cs`, `.cshtml`, script CI, `Directory.Build.props`/`Directory.Packages.props`) để đối chiếu FR-001 áp dụng cho toàn bộ "mã nguồn", không chỉ file cấu hình — sạch, không có kết quả nào ngoài các giá trị test/placeholder đã biết

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Không phụ thuộc — bắt đầu ngay
- **Foundational (Phase 2)**: Không phụ thuộc Setup về mặt kỹ thuật (file khác nhau) nhưng nên hoàn thành trước Phase 3/4 vì US2 cần T007
- **User Story 1 (Phase 3)**: Có thể bắt đầu song song với Phase 2 — không phụ thuộc `RequiredSecretsValidation`
- **User Story 2 (Phase 4)**: PHẢI đợi Phase 2 hoàn thành (cần T007); độc lập với Phase 3 về mặt kỹ thuật nhưng nên chạy sau khi appsettings đã sạch (Phase 3) để tránh xung đột merge trên cùng file `appsettings.Development.json`/`Program.cs`
- **User Story 3 (Phase 5)**: PHẢI đợi manifest của Phase 4 (T025-T029) hoàn thành
- **Polish (Phase 6)**: Phụ thuộc các stage CI đã wiring ở Phase 3 (T017) và Phase 4 (T031)

### User Story Dependencies

- **User Story 1 (P1)**: Không phụ thuộc story khác
- **User Story 2 (P1)**: Phụ thuộc kỹ thuật vào Foundational (T007); không phụ thuộc nghiệp vụ vào US1, nhưng thực thi sau US1 để tránh đụng file
- **User Story 3 (P2)**: Phụ thuộc manifest đã tạo ở US2 (T025-T029)

### Parallel Opportunities

- T001-T004 (Setup) chạy song song
- T005 (test) độc lập, chạy trước T006/T007
- T008-T014 (dọn appsettings 7 service) chạy song song — khác file
- T021-T024 (wiring 4 service còn lại sau T020) chạy song song — khác file
- T025-T029 (5 manifest) chạy song song — khác file
- T035, T036, T038 (Polish) chạy song song — khác file

---

## Parallel Example: User Story 1

```bash
# Dọn secret hardcode ở 7 service cùng lúc (khác file):
Task: "Xoá mật khẩu literal khỏi services/baskets/src/Baskets.Api/appsettings.Development.json"
Task: "Xoá mật khẩu literal khỏi services/orders/src/Orders.Api/appsettings.Development.json"
Task: "Xoá mật khẩu literal khỏi services/parties/src/Parties.Api/appsettings.Development.json"
Task: "Xoá mật khẩu literal khỏi services/products/src/Products.Api/appsettings.Development.json"
Task: "Xoá mật khẩu literal khỏi services/identity/src/Identity.Api/appsettings.Development.json"
Task: "Rà soát services/gateway/src/Gateway.Api/appsettings.Development.json"
Task: "Rà soát services/bff/src/Bff.Api/appsettings.Development.json"
```

## Parallel Example: User Story 2

```bash
# Sau khi T020 (Orders, mẫu tham chiếu) hoàn thành, wiring 4 service còn lại song song:
Task: "Wiring AddRequiredSecretsValidation() vào services/baskets/src/Baskets.Api/Program.cs"
Task: "Wiring AddRequiredSecretsValidation() vào services/parties/src/Parties.Api/Program.cs"
Task: "Wiring AddRequiredSecretsValidation() vào services/products/src/Products.Api/Program.cs"
Task: "Wiring AddRequiredSecretsValidation() vào services/identity/src/Identity.Api/Program.cs"

# Tạo 5 manifest song song (khác file):
Task: "Tạo deploy/k8s/orders/external-secret.yaml và secret.example.yaml"
Task: "Tạo deploy/k8s/baskets/external-secret.yaml và secret.example.yaml"
Task: "Tạo deploy/k8s/parties/external-secret.yaml và secret.example.yaml"
Task: "Tạo deploy/k8s/products/external-secret.yaml và secret.example.yaml"
Task: "Tạo deploy/k8s/identity/external-secret.yaml và secret.example.yaml"
```

---

## Implementation Strategy

### MVP First (User Story 1 + User Story 2 — cả hai đều P1)

1. Hoàn thành Phase 1: Setup
2. Hoàn thành Phase 2: Foundational (CRITICAL — chặn US2)
3. Hoàn thành Phase 3: User Story 1
4. Hoàn thành Phase 4: User Story 2
5. **DỪNG và XÁC THỰC**: chạy Kịch bản 1 + 2 trong quickstart.md độc lập
6. Đây là MVP đầy đủ của feature — thỏa toàn bộ Acceptance Criteria gốc của Jira SCRUM-27

### Incremental Delivery

1. Setup + Foundational → nền tảng sẵn sàng
2. Thêm User Story 1 → test độc lập (gitleaks 0 findings) → có thể merge riêng
3. Thêm User Story 2 → test độc lập (fail-fast + image sạch) → merge → **MVP hoàn chỉnh**
4. Thêm User Story 3 → test độc lập (rotate ≤5 phút, cần cluster thử nghiệm) → merge
5. Phase 6 (Polish) hoàn thiện tài liệu và xác thực CI gate chặn merge thật

### Parallel Team Strategy

Với nhiều lập trình viên:

1. Cả team hoàn thành Setup + Foundational cùng nhau (T001-T007)
2. Sau đó:
   - Lập trình viên A: User Story 1 (dọn appsettings 7 service + gitleaks stage)
   - Lập trình viên B: User Story 2 (wiring fail-fast + manifest + Trivy stage) — bắt đầu sau khi T007 xong, có thể làm song song với A trên các file khác nhau miễn phối hợp tránh đụng cùng `Program.cs`/`appsettings` của cùng một service
3. User Story 3 bắt đầu sau khi manifest của US2 (T025-T029) sẵn sàng

---

## Notes

- [P] = khác file, không phụ thuộc task chưa hoàn thành
- Nhãn [Story] giúp truy vết task về đúng user story
- Test PHẢI viết trước và xác nhận FAIL trước khi implement (Principle III — NON-NEGOTIABLE)
- Commit sau mỗi task hoặc mỗi nhóm task liên quan
- Dừng ở mỗi checkpoint để xác thực story độc lập trước khi qua story tiếp theo
- Tránh: task mơ hồ, hai task cùng sửa một file được đánh dấu [P], phụ thuộc chéo giữa story phá vỡ tính độc lập
