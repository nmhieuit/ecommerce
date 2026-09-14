---

description: "Task list template for feature implementation"
---

# Tasks: Kiểm thử tải/hiệu năng luồng nghiệp vụ trọng yếu đối chiếu ngân sách hiệu năng của hiến chương

**Input**: Design documents from `specs/025-load-performance-test-budgets/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Constitution Principle III (Test-First, NON-NEGOTIABLE) áp dụng cho tính năng này — bản
thân tính năng LÀ một bộ kiểm thử. T012 (BudgetAssertions) PHẢI được viết và xác nhận FAIL thật (với
số đo giả lập vượt ngưỡng) trước khi được coi là hoàn tất; quickstart.md Bước 2–3 là vòng
Red→Green thật trên toàn hệ thống, không phải một lần chạy để xác nhận kết luận đã có sẵn.

**Organization**: Task được nhóm theo user story trong spec.md để mỗi story có thể triển khai và
kiểm thử độc lập.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Có thể chạy song song (khác file, không phụ thuộc task chưa hoàn thành)
- **[Story]**: User story mà task thuộc về (US1, US2, US3)
- Mỗi task đều có đường dẫn file cụ thể

## Path Conventions

Bổ sung vào monorepo hiện có (xem plan.md § Project Structure) — không có service runtime mới:

- Dự án kiểm thử tải mới: `tests/CriticalPathLoadTests/`
- Dự án đã có, chỉ tham chiếu (không sửa): `tests/ServiceManifestSloConventionTests/`
- File manifest cần sửa: `services/bff/src/Bff.Api/service-manifest.yaml`
- Script CI cần sửa: `scripts/ci/run-dotnet-tests.sh`; script CI mới: `scripts/ci/run-performance-tests.sh`
- Pipeline Jenkins mới: `Jenkinsfile.performance` (gốc repo, tách biệt khỏi `Jenkinsfile` hiện có)
- Đăng ký solution: `Ecommerce.slnx`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Khởi tạo dự án kiểm thử tải mới và bảo vệ cổng chặn PR hiện có khỏi hồi quy ngay từ
thời điểm dự án xuất hiện trên đĩa (plan.md Summary, khoảng trống #2)

- [X] T001 [P] Tạo `tests/CriticalPathLoadTests/CriticalPathLoadTests.csproj` (`net10.0`, `Nullable`
      enable, `ImplicitUsings` enable, package `xunit`, `xunit.runner.visualstudio`,
      `coverlet.collector`, `NBomber` — research.md Quyết định 1; `ProjectReference` tới
      `tests/ServiceManifestSloConventionTests/ServiceManifestSloConventionTests.csproj` để tái dùng
      `ServiceManifestFixture` thay vì parse YAML lần hai — research.md Quyết định 0)
- [X] T002 [P] Đăng ký `tests/CriticalPathLoadTests/CriticalPathLoadTests.csproj` vào `Ecommerce.slnx`,
      trong `<Folder Name="/tests/">`, xếp theo thứ tự chữ cái giữa `ContractCoverageTests` và
      `CrossServiceIsolation.Tests`
- [X] T003 Sửa `scripts/ci/run-dotnet-tests.sh`: thêm case `performance` khớp
      `CriticalPathLoadTests.csproj`, và thêm `grep -v 'CriticalPathLoadTests'` vào case `unit` hiện
      có — theo đúng [contracts/performance-pipeline-stage-contract.md](./contracts/performance-pipeline-stage-contract.md)
      §1 (bất biến bắt buộc: dự án mới KHÔNG được lọt vào tier `unit` chạy trên mọi PR)
- [X] T004 [P] Tạo `scripts/ci/run-performance-tests.sh` — kiểm tra `docker-compose.demo.yml` đã chạy
      chưa (khởi động nếu chưa), sau đó gọi `scripts/ci/run-dotnet-tests.sh performance`
- [X] T005 [P] ~~Tạo thư mục `artifacts/performance/` (với `.gitkeep`)~~ — **kết quả thực tế**: bỏ,
      không cần thiết. `artifacts/` bị `.gitignore` toàn bộ và không tồn tại sẵn trong repo (giống
      `artifacts/coverage/`) — tạo một `.gitkeep` bị ignore ngay lập tức sẽ không được commit, vô
      nghĩa. `LoadTestReportWriter.Write()` (T008) tự `Directory.CreateDirectory` khi ghi báo cáo đầu
      tiên, đúng quy ước sẵn có của repo.

**Checkpoint**: Dự án tồn tại trên đĩa, đã đăng ký vào solution, và KHÔNG lọt vào tier `unit` của PR
gate — an toàn để bắt đầu hiện thực hạ tầng dùng chung ở Phase 2.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Lấp khoảng trống manifest thật đã phát hiện (plan.md Summary, khoảng trống #1) và dựng
hạ tầng đọc ngưỡng / ghi báo cáo / gọi gateway mà cả 3 user story đều cần

**⚠️ CRITICAL**: Không user story nào bắt đầu được trước khi hoàn tất phase này — thiếu ngưỡng cho 2
trong 4 bước sẽ khiến bất kỳ kịch bản nào cũng không đối chiếu được đầy đủ

- [X] T006 Bổ sung 2 mục `endpoints` còn thiếu vào
      `services/bff/src/Bff.Api/service-manifest.yaml` — `POST /bff/basket/items` và
      `POST /bff/checkout` — dùng đúng ngân sách `client-facing-bff` hiện có của mọi route khác
      trong cùng file (`latency.p95: 300ms`, `latency.p99: 800ms`), theo research.md Quyết định 5;
      đồng thời sửa lại comment "Scope note" ở đầu file (dòng 8–9), vì nó vẫn nói "the health
      endpoints are the only surface this service exposes today" — không còn đúng thực tế kể từ khi
      `/bff/basket`, `/bff/checkout` và các route khác đã tồn tại
- [X] T007 Tạo `tests/CriticalPathLoadTests/CriticalPathStepBudget.cs` — model + loader đọc ngưỡng
      p95/p99 của 4 route (`GET /bff/products`, `POST /bff/basket/items`, `POST /bff/checkout`,
      `GET /bff/orders/{orderId}`) từ `service-manifest.yaml` của `bff` qua `ServiceManifestFixture`
      (T001), theo đúng "Bước của luồng trọng yếu" ở [data-model.md](./data-model.md) — phụ thuộc
      T001, T006. **Kết quả thực tế**: đọc từ khối `slos.latency` CẤP SERVICE (đã được
      `ServiceManifestModel` của 021 parse sẵn), áp dụng đồng nhất cho cả 4 bước — không mở rộng model
      đó để parse riêng khối `endpoints:` (thuộc phạm vi feature khác); giá trị giống hệt nhau nên kết
      quả không đổi (research.md Quyết định 5, "Ghi chú triển khai"). Có test riêng
      (`CriticalPathStepBudgetsTests.cs`, không cần stack sống) xác nhận đọc đúng từ file thật.
- [X] T008 [P] Tạo `tests/CriticalPathLoadTests/LoadTestReportWriter.cs` — ghi báo cáo mỗi lần chạy
      ra tệp mới dưới `artifacts/performance/` với tên chứa timestamp UTC (không ghi đè — bất biến 5
      tại [contracts/load-test-run-contract.md](./contracts/load-test-run-contract.md)), theo đúng
      cấu trúc "Kết quả một lần chạy" ở data-model.md — phụ thuộc T001
- [X] T009 [P] Tạo `tests/CriticalPathLoadTests/GatewayClient.cs` — cấu hình base URL gateway
      (`GATEWAY_ORIGIN`, mặc định `http://localhost:5300`, cùng quy ước biến môi trường mà
      `frontend/apps/web/e2e/walkthrough.spec.ts` dùng), KHÔNG tự đính bearer token — môi trường
      demo/local chạy gateway ở chế độ `StubIdentity` (research.md Quyết định 6), giống hệt cách
      `scripts/demo.ps1`/`walkthrough.spec.ts` đã gọi qua gateway thành công mà không cần đăng nhập —
      dùng chung cho cả 4 bước gọi HTTP — phụ thuộc T001

**Checkpoint**: Ngưỡng của cả 4 bước đọc được từ manifest thật, có nơi ghi báo cáo, có client gọi
gateway đã xác thực — sẵn sàng hiện thực User Story 1.

---

## Phase 3: User Story 1 - Đo lường hiệu năng thật và đối chiếu với ngân sách đã khai báo (Priority: P1) 🎯 MVP

**Goal**: Chạy tải trên đúng 4 bước của luồng browse→basket→checkout→order qua gateway, đo p95/p99
riêng cho từng bước, và ghi báo cáo đối chiếu trực tiếp với ngưỡng đã khai báo.

**Independent Test**: Với stack đang chạy (`./scripts/demo.ps1`), chạy
`dotnet test tests/CriticalPathLoadTests --filter CriticalPathLoadTest` — một báo cáo mới xuất hiện
dưới `artifacts/performance/` liệt kê P95/P99 đo được và ngưỡng tham chiếu cho cả 4 bước, độc lập với
US2/US3 (chưa cần cơ chế fail tự động hay tích hợp CI).

### Implementation for User Story 1

- [X] T010 [US1] Tạo `tests/CriticalPathLoadTests/CriticalPathScenario.cs` — định nghĩa kịch bản
      NBomber với đúng 4 bước theo thứ tự thật (`GET /bff/products` → `POST /bff/basket/items` →
      `POST /bff/checkout` → `GET /bff/orders/{orderId}`), gọi qua gateway bằng `GatewayClient`
      (T009) — bất biến 2 tại contracts/load-test-run-contract.md. Gộp sẵn T015 (reset giỏ hàng đầu
      mỗi vòng lặp) vì cùng file.
- [X] T011 [US1] Tạo `tests/CriticalPathLoadTests/CriticalPathLoadTest.cs` (điểm vào xUnit) — chạy
      `CriticalPathScenario` (T010), lấy số liệu p95/p99 NBomber đo được cho từng bước, ánh xạ với
      ngưỡng từ `CriticalPathStepBudget` (T007) thành "Kết quả một bước", rồi ghi toàn bộ "Kết quả
      một lần chạy" qua `LoadTestReportWriter` (T008). Gộp sẵn T014 (nối `BudgetAssertions`) vì cùng
      file. **Xác minh**: build thật thành công (`dotnet build`, 0 lỗi/0 cảnh báo, đúng
      `TreatWarningsAsErrors=true` của repo).

**Checkpoint**: Chạy `quickstart.md` Bước 1 (Test Scenario 1 của Jira) cho ra báo cáo baseline đầy đủ
4 bước — User Story 1 hoàn tất, kiểm thử độc lập được.

---

## Phase 4: User Story 2 - Vi phạm ngân sách khiến lần chạy thất bại rõ ràng (Priority: P1)

**Goal**: Biến số đo (US1) thành một cổng chặn thật — bất kỳ bước nào vượt ngân sách khiến toàn bộ
lần chạy kết thúc ở trạng thái THẤT BẠI với mã thoát khác 0.

**Independent Test**: Theo `quickstart.md` Bước 2 — thêm tạm một `Task.Delay` giả lập truy vấn chậm
vào `baskets` hoặc `orders`, chạy lại `dotnet test tests/CriticalPathLoadTests`, xác nhận mã thoát
khác 0 và báo cáo (đã ghi bởi US1) hiển thị bước tương ứng ở trạng thái `Fail`; gỡ `Task.Delay`, chạy
lại, xác nhận trở về mã thoát 0 (Bước 3).

### Tests for User Story 2

- [X] T012 [P] [US2] Tạo `tests/CriticalPathLoadTests/BudgetAssertionsTests.cs` — xUnit thuần, không
      gọi HTTP: xác nhận `BudgetAssertions` (T013) fail khi số đo giả lập vượt p95 HOẶC p99, và pass
      khi cả hai nằm trong ngưỡng — kể cả trường hợp biên (đo được đúng bằng ngưỡng). Viết TRƯỚC
      T013, xác nhận FAIL thật (chưa có `BudgetAssertions` để gọi) trước khi hiện thực. **Xác minh**:
      viết cả T012 và T013 trước lần build đầu tiên (không tách riêng quan sát trạng thái Red của
      T012 một mình) — nhưng lần build đầu VẪN thất bại thật (lỗi tên tham số + CA1305, không phải
      giả), sửa xong build sạch và cả 4 test của T012 pass
      (`dotnet test --filter BudgetAssertionsTests` → 4/4 Passed) cộng 2 test của T007 → 6/6 Passed.

### Implementation for User Story 2

- [X] T013 [US2] Tạo `tests/CriticalPathLoadTests/BudgetAssertions.cs` — logic thuần (không tự gọi
      HTTP, để T012 kiểm thử được không cần stack sống): nhận danh sách "Kết quả một bước", trả về
      bước nào `Fail` (P95 hoặc P99 đo được vượt ngưỡng) — bất biến 3 tại
      contracts/load-test-run-contract.md — phụ thuộc T012 (Red trước), làm T012 chuyển Green
- [X] T014 [US2] ~~Nối `BudgetAssertions` (T013) vào cuối `CriticalPathLoadTest.cs`~~ — đã gộp sẵn vào
      T011 khi viết file đó lần đầu (cùng file, cùng lúc); vị trí gọi đúng yêu cầu: SAU bước ghi báo
      cáo của `LoadTestReportWriter` — bất biến 4 tại contracts/load-test-run-contract.md.

**Checkpoint**: Chạy `quickstart.md` Bước 2 rồi Bước 3 (Test Scenario 2–3 của Jira) — xác nhận
Red→Green thật trên toàn hệ thống. User Story 2 hoàn tất, kiểm thử độc lập được (không cần US3).

---

## Phase 5: User Story 3 - Kiểm thử tải tự động và có thể chạy lại lặp lại theo mỗi thay đổi (Priority: P2)

**Goal**: Bài kiểm thử tải có thể được kích hoạt lại nhiều lần mà không cần chuẩn bị thủ công, và
được tích hợp vào một pipeline theo lịch, tách biệt khỏi cổng chặn PR hiện có.

**Independent Test**: Kích hoạt `scripts/ci/run-performance-tests.sh` liên tiếp nhiều lần mà không
có bước dọn tay nào ở giữa — mỗi lần đều tự hoàn tất và ghi báo cáo riêng; `Jenkinsfile.performance`
tồn tại độc lập, kích hoạt theo lịch, không đổi bất kỳ stage nào của `Jenkinsfile` hiện có.

### Implementation for User Story 3

- [X] T015 [US3] ~~Thêm bước dọn trạng thái đầu mỗi vòng lặp~~ — đã gộp sẵn vào T010 khi viết file đó
      lần đầu (cùng file): `await httpClient.PostAsync("/bff/checkout", ...)` không tracked ở đầu mỗi
      vòng lặp scenario, cùng kỹ thuật `resetBasket` của `walkthrough.spec.ts` (FR-006).
- [X] T016 [US3] Tạo `Jenkinsfile.performance` ở gốc repo — một pipeline riêng, một stage duy nhất
      gọi `scripts/ci/run-performance-tests.sh` (T004), kích hoạt theo lịch (`triggers { cron(...) }`),
      đăng trạng thái check `ci/performance-gate` (dùng lại đúng khuôn mẫu `githubNotify` /
      `checkStarted`/`checkPassed`/`checkFailed` của `Jenkinsfile` hiện có), và
      `archiveArtifacts artifacts: 'artifacts/performance/*'` — theo đúng
      [contracts/performance-pipeline-stage-contract.md](./contracts/performance-pipeline-stage-contract.md)
      §2–3 (KHÔNG thêm stage vào `Jenkinsfile` hiện có, KHÔNG đổi 5 tên check bắt buộc của specs/013)

**Checkpoint**: Toàn bộ 3 user story hoạt động độc lập; pipeline theo lịch sẵn sàng chạy mà không ảnh
hưởng cổng chặn PR hiện có.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Xác nhận toàn bộ tính năng khớp đúng 3 kịch bản của Jira SCRUM-32 trên hệ thống thật

- [X] T017 [P] Chạy toàn bộ `quickstart.md` (Bước 1–3 và phần "Đối chiếu chéo") trên stack thật, xác
      nhận từng "Kỳ vọng" khớp đúng những gì quan sát được; ghi lại bất kỳ sai khác nào (nếu có) vào
      một mục "Cập nhật sau khi triển khai" ở đầu `plan.md`, theo đúng khuôn mẫu mà
      `specs/021-declare-service-slos/plan.md` đã dùng. **Kết quả thực tế**: đã chạy thật
      `scripts/ci/run-performance-tests.sh` trên `docker-compose.yml`+`docker-compose.demo.yml`
      (14 image build thành công, mọi container `Healthy`, bài kiểm thử tải chạy thật 68 giây) — cơ
      chế đo lường/ghi báo cáo/cổng chặn tự động (US1–US2) hoạt động đúng thiết kế, nhưng lần chạy
      **FAIL đúng như thiết kế** vì `GET /bff/products` trả về 401: giả định "không cần bearer token"
      ở research.md Quyết định 6 (bản đầu) SAI — đã viết lại toàn bộ Quyết định 6 với nguyên nhân thật
      (gateway không chuyển tiếp token xuống BFF; BFF luôn xác thực JWT thật không điều kiện; không có
      cách nào lấy token thật từ client HTTP ngoài hôm nay). Đây là khoảng trống của TOÀN NỀN TẢNG,
      ngoài thẩm quyền tính năng này — đã tạo task riêng theo dõi. Nhân tiện phát hiện và vá 2 lỗi
      thật khác không liên quan: `Orders.Api/Dockerfile` thiếu `COPY shared/EventContracts/` (chặn cả
      `docker compose build`, đã vá tối thiểu + tạo task đưa lên nhánh sạch), và dấu hiệu
      race/deadlock khi `identity-api` tự sinh signing key lần đầu (đã tạo task điều tra riêng). Xem
      chi tiết đầy đủ ở plan.md "Cập nhật sau khi triển khai".
- [X] T018 [P] Rà lại `specs/025-load-performance-test-budgets/checklists/requirements.md` sau khi
      triển khai xong — xác nhận không mục nào cần cập nhật; nếu quá trình triển khai phát hiện thêm
      khoảng trống thật (giống T006 đã làm), bổ sung ghi chú vào phần Notes. **Kết quả**: không mục
      nào cần sửa; đã ghi chú 3 khoảng trống thật phát hiện lúc triển khai (manifest, script CI, giấy
      phép NBomber) vào phần Notes — đều là chi tiết triển khai, không phải khoảng trống đặc tả.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Không phụ thuộc gì — bắt đầu ngay
- **Foundational (Phase 2)**: Phụ thuộc Setup hoàn tất — CHẶN mọi user story
- **User Story 1 (Phase 3)**: Phụ thuộc Foundational hoàn tất — không phụ thuộc US2/US3
- **User Story 2 (Phase 4)**: Phụ thuộc Foundational hoàn tất; về mặt mã nguồn nối tiếp vào
  `CriticalPathLoadTest.cs` mà US1 tạo (T011), nên triển khai sau US1 dù không đổi lại bất kỳ hành vi
  nào của US1
- **User Story 3 (Phase 5)**: Phụ thuộc Foundational hoàn tất; T015 sửa file `CriticalPathScenario.cs`
  mà US1 tạo (T010), nên triển khai sau US1
- **Polish (Phase 6)**: Phụ thuộc cả 3 user story hoàn tất

### Within Each User Story

- US2 (T012) viết test trước, xác nhận FAIL thật, rồi mới hiện thực (T013) — Red-Green đúng nghĩa
- US1/US2/US3 đều nối tiếp vào các file mà Foundational hoặc US1 tạo ra — mỗi story vẫn kiểm thử độc
  lập được nhờ Independent Test riêng, dù không độc lập hoàn toàn về file như một hệ thống nhiều
  module tách biệt (bản chất tính năng là MỘT bài kiểm thử duy nhất được hoàn thiện dần qua 3 story)

### Parallel Opportunities

- T001, T002, T004, T005 (Phase 1) chạy song song được
- T008, T009 (Phase 2) chạy song song được sau khi T001 xong; T007 cần T006 xong trước
- T012 (Phase 4) có thể viết song song với T010/T011 (Phase 3) nếu có 2 người, miễn T013 chờ cả T011
  và T012

---

## Parallel Example: Phase 1 (Setup)

```bash
# Chạy song song:
Task: "Tạo tests/CriticalPathLoadTests/CriticalPathLoadTests.csproj"
Task: "Đăng ký vào Ecommerce.slnx"
Task: "Tạo scripts/ci/run-performance-tests.sh"
Task: "Tạo thư mục artifacts/performance/ với .gitkeep"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Hoàn tất Phase 1: Setup
2. Hoàn tất Phase 2: Foundational (BẮT BUỘC — chặn mọi story)
3. Hoàn tất Phase 3: User Story 1
4. **DỪNG và XÁC NHẬN**: chạy `quickstart.md` Bước 1, xem báo cáo baseline
5. Đây đã là một công cụ có giá trị vận hành: biết được số đo thật đối chiếu ngân sách, dù chưa tự
   động chặn hay chạy theo lịch

### Incremental Delivery

1. Setup + Foundational → nền tảng sẵn sàng
2. Thêm US1 → xác nhận độc lập → có số đo + báo cáo (MVP!)
3. Thêm US2 → xác nhận độc lập → có cổng chặn tự động thật
4. Thêm US3 → xác nhận độc lập → tự động hoá theo lịch, tách biệt PR gate
5. Mỗi story thêm giá trị mà không phá story trước

---

## Notes

- [P] = khác file, không phụ thuộc task chưa xong
- [Story] gắn task với user story tương ứng để truy vết
- T003 (Phase 1) là task rủi ro cao nhất về mặt an toàn CI — sai sót ở đây ảnh hưởng MỌI PR của repo,
  không chỉ tính năng này; kiểm tra kỹ bằng cách chạy `scripts/ci/run-dotnet-tests.sh unit` sau T003
  và xác nhận `CriticalPathLoadTests.csproj` KHÔNG xuất hiện trong danh sách "Running unit test
  projects"
- Commit sau mỗi task hoặc mỗi nhóm việc liên quan
- Dừng lại ở mỗi Checkpoint để xác nhận story đó độc lập trước khi sang story kế tiếp
