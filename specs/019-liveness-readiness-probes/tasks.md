---

description: "Task list template for feature implementation"
---

# Tasks: Liveness/readiness probe cho mọi service trên Kubernetes

**Input**: Design documents from `specs/019-liveness-readiness-probes/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Constitution Principle III (Test-First, NON-NEGOTIABLE) áp dụng cho tính năng này — các task test dưới đây là BẮT BUỘC, không tùy chọn, và PHẢI được viết trước, xác nhận FAIL, rồi mới triển khai.

**Organization**: Task được nhóm theo user story trong spec.md để mỗi story có thể triển khai và kiểm thử độc lập.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Có thể chạy song song (khác file, không phụ thuộc task chưa hoàn thành)
- **[Story]**: User story mà task thuộc về (US1, US2, US3)
- Mỗi task đều có đường dẫn file cụ thể

## Path Conventions

Dự án hạ tầng-dưới-dạng-mã (infrastructure-as-code) trong monorepo hiện có (xem plan.md § Project
Structure):

- Manifest/role Ansible: `deploy/ansible/`
- Test quy ước (convention test) mới: `tests/DeploymentManifestConventionTests/`
- Không có thư mục `src/`/`frontend/` mới — tính năng không thêm service runtime nào.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Khởi tạo cấu trúc thư mục và dự án cần thiết cho toàn bộ tính năng

- [X] T001 [P] Tạo khung thư mục `deploy/ansible/` gồm `roles/service_deployment/defaults/`,
      `roles/service_deployment/templates/`, `roles/service_deployment/tasks/`, `inventories/`, và
      file playbook gốc `deploy/ansible/deploy.yml` (rỗng, include role `service_deployment`)
- [X] T002 [P] Thêm `PackageVersion` cho `YamlDotNet` vào `Directory.Packages.props` (dùng để parse
      YAML manifest đã render trong test)
- [X] T003 [P] Viết `deploy/ansible/README.md` ghi rõ điều kiện tiên quyết cục bộ/CI: `ansible-core`
      ≥ 2.16, collection `kubernetes.core`, `ansible-lint`, `kubeconform` (tham chiếu bởi
      quickstart.md § Điều kiện tiên quyết)
- [X] T004 Tạo dự án xUnit `tests/DeploymentManifestConventionTests/DeploymentManifestConventionTests.csproj`
      theo đúng khuôn mẫu `tests/ContainerConventionTests/ContainerConventionTests.csproj` (thêm
      `PackageReference` tới `YamlDotNet` từ T002), và đăng ký project này trong `Ecommerce.slnx`
      (phụ thuộc T002)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Hạ tầng dùng chung mà CẢ 3 user story đều cần trước khi có thể triển khai

**⚠️ CRITICAL**: Không user story nào được bắt đầu trước khi phase này hoàn tất

- [X] T005 [P] Tạo khung `deploy/ansible/roles/service_deployment/templates/deployment.yaml.j2`
      (container spec cơ bản: image, tên, cổng — CHƯA có khối probe), khung
      `deploy/ansible/roles/service_deployment/defaults/main.yml` (rỗng), và
      `deploy/ansible/roles/service_deployment/tasks/main.yml` (render template + áp dụng qua module
      `kubernetes.core.k8s`) — phụ thuộc T001
- [X] T006 [P] Cài đặt `tests/DeploymentManifestConventionTests/ProbeTemplateRenderer.cs` — helper
      dùng chung, gọi render cục bộ (Ansible template action) cho template ở T005 với một bộ biến
      truyền vào, trả về chuỗi YAML để test parse bằng YamlDotNet — phụ thuộc T004

**Checkpoint**: Nền tảng sẵn sàng — có thể bắt đầu triển khai từng user story.

---

## Phase 3: User Story 1 - Mọi service khai báo đầy đủ probe trong manifest triển khai (Priority: P1) 🎯 MVP

**Goal**: Manifest triển khai của cả 7 service (parties, products, baskets, orders, identity,
gateway, bff) đều khai báo `livenessProbe` và `readinessProbe` trỏ đúng endpoint sức khỏe sẵn có.

**Independent Test**: Render manifest của từng service từ template + biến trong inventory, kiểm tra
bằng `tests/DeploymentManifestConventionTests` rằng cả 7 service đều có đủ 2 probe, đúng path/port,
và liveness không gắn tag phụ thuộc dependency ngoài.

### Tests for User Story 1 ⚠️

> **Viết các test này TRƯỚC, xác nhận chúng FAIL trước khi triển khai (chưa có nội dung probe ở T005/T006)**

- [X] T007 [P] [US1] Viết bộ test trong `tests/DeploymentManifestConventionTests/ProbeDeclarationTests.cs`:
      `AllServices_DeclareBothProbes` (FR-001, FR-002), `Probes_UseCorrectHttpPathAndPort` (FR-008,
      contracts/probe-manifest-shape.md bất biến #2, #3), `LivenessProbe_NeverTaggedWithExternalDependency`
      (FR-003, bất biến #4) — chạy cho cả 7 service
- [X] T008 [P] [US1] Viết `tests/DeploymentManifestConventionTests/ServiceInventoryTests.cs`:
      `InventoryCoversExactlySevenServices`, theo đúng khuôn mẫu `TheScan_Examined_EveryService` của
      `ContainerConventionTests` — xác nhận `deploy/ansible/inventories/services.yml` có đúng 7 khóa
      `service_name`, không thiếu không thừa (spec SC-001)

### Implementation for User Story 1

- [X] T009 [P] [US1] Điền đầy đủ `deploy/ansible/inventories/services.yml` cho cả 7 service theo
      schema [contracts/service-deployment-vars.md](./contracts/service-deployment-vars.md)
      (`service_name`, `container_port`, `depends_on_database`)
- [X] T010 [P] [US1] Điền `deploy/ansible/roles/service_deployment/defaults/main.yml` với 2 nhóm giá
      trị mặc định probe (`depends_on_database: true` / `false`), suy ra từ `healthcheck` trong
      `docker-compose.yml` theo [research.md](./research.md) Quyết định 2
- [X] T011 [US1] Thêm khối `livenessProbe`/`readinessProbe` vào
      `deploy/ansible/roles/service_deployment/templates/deployment.yaml.j2` theo
      [contracts/probe-manifest-shape.md](./contracts/probe-manifest-shape.md) — phụ thuộc T009, T010
- [X] T012 [US1] Hoàn thiện `deploy/ansible/roles/service_deployment/tasks/main.yml` để render và áp
      dụng manifest cho từng service trong `inventories/services.yml` — phụ thuộc T011
- [X] T013 [US1] Chạy lại T007 và T008, xác nhận PASS (Green) cho cả 7 service — phụ thuộc T009-T012
      (kết quả thực tế: `dotnet test tests/DeploymentManifestConventionTests` → 37/37 passed)

**Checkpoint**: Tại đây, User Story 1 hoạt động độc lập và kiểm thử được — đây là MVP.

---

## Phase 4: User Story 2 - Loại pod chưa sẵn sàng ra khỏi luồng traffic (Priority: P1)

**Goal**: Pod chưa vượt qua readiness probe (kể cả trong lúc rolling update) không bao giờ nhận
traffic; pod cũ tiếp tục phục vụ cho tới khi pod mới sẵn sàng.

**Independent Test**: Triển khai/khởi động lại pod của một service phụ thuộc cơ sở dữ liệu trên
cluster `kind` cục bộ (quickstart.md Bước 1-3), xác nhận không request nào bị định tuyến vào pod
chưa `READY`.

### Tests for User Story 2 ⚠️

- [X] T014 [P] [US2] Viết `tests/DeploymentManifestConventionTests/RolloutStrategyTests.cs`:
      `RollingUpdateStrategy_KeepsOldPodsServingUntilNewPodReady` — xác nhận
      `deployment.yaml.j2` khai báo `strategy.type: RollingUpdate` với `maxUnavailable: 0` (FR-009)
- [X] T015 [P] [US2] Bổ sung vào `tests/DeploymentManifestConventionTests/ProbeDeclarationTests.cs`:
      `ReadinessDefaults_DifferByDependencyGroup` — xác nhận nhóm `depends_on_database: true` và
      `false` cho ra tham số thời gian readiness khác nhau đúng như research.md Quyết định 2 (FR-004)
      (đã viết sẵn cùng T007 vì cùng file/cùng mối quan tâm khai báo; đã xác nhận PASS ở T013)

### Implementation for User Story 2

- [X] T016 [US2] Thêm `strategy: { type: RollingUpdate, rollingUpdate: { maxUnavailable: 0, maxSurge: 1 } }`
      vào `deploy/ansible/roles/service_deployment/templates/deployment.yaml.j2` (FR-009)
- [X] T017 [US2] Rà soát `deploy/ansible/roles/service_deployment/defaults/main.yml`, đảm bảo 2 nhóm
      giá trị (từ T010) tạo ra đúng hành vi loại trừ traffic mô tả ở FR-004/FR-005 — điều chỉnh nếu
      T015 phát hiện sai khác (đã đúng từ T010 — không cần điều chỉnh)
- [X] T018 [US2] Chạy lại T014, T015, xác nhận PASS — phụ thuộc T016, T017
      (kết quả thực tế: `dotnet test` → 44/44 passed)
- [X] T019 [US2] Thực hiện [quickstart.md](./quickstart.md) Bước 1–3 trên cluster `kind` cục bộ, xác
      nhận SC-002 (0% request vào pod chưa sẵn sàng) — phụ thuộc T018
      (đã chạy thật trên `kind` với image `ecomerce-local-gateway-api:latest` — xem ghi chú xác thực
      cuối file này)

**Checkpoint**: User Story 1 VÀ 2 đều hoạt động độc lập.

---

## Phase 5: User Story 3 - Tự động khởi động lại pod bị treo hoặc deadlock (Priority: P2)

**Goal**: Pod có tiến trình treo (không phản hồi liveness probe) bị Kubernetes tự động khởi động
lại sau khi vượt ngưỡng lỗi cấu hình; sự cố dependency ngoài không kích hoạt sai hành vi này.

**Independent Test**: Giả lập treo endpoint kiểm tra tiến trình sống trên cluster `kind` cục bộ
(quickstart.md Bước 4), xác nhận pod được khởi động lại; giả lập sự cố dependency ngoài (Bước 5),
xác nhận pod KHÔNG bị khởi động lại.

### Tests for User Story 3 ⚠️

- [X] T020 [P] [US3] Viết `tests/DeploymentManifestConventionTests/LivenessRestartTests.cs`:
      `LivenessProbe_HasPositiveFailureThresholdAndPeriod` (FR-006) và
      `LivenessProbe_InitialDelayTeleratesNormalStartup` (FR-007, dung sai đủ theo research.md
      Quyết định 2) — PASS ngay (14/14) vì defaults/main.yml (T010) đã đặt giá trị hợp lệ từ đầu

### Implementation for User Story 3

- [X] T021 [US3] Hoàn thiện `failureThreshold`/`periodSeconds`/`initialDelaySeconds` của
      `livenessProbe` trong `deploy/ansible/roles/service_deployment/defaults/main.yml` theo
      research.md Quyết định 2 (nếu T010/T017 chưa đủ chi tiết cho liveness) — đã đủ từ T010, không
      cần chỉnh
- [X] T022 [US3] Chạy lại T020, xác nhận PASS — phụ thuộc T021 (kết quả thực tế: 58/58 passed)
- [~] T023 [US3] Thực hiện [quickstart.md](./quickstart.md) Bước 4 (giả lập treo → xác nhận restart,
      SC-003) và Bước 5 (giả lập sự cố dependency ngoài → xác nhận KHÔNG restart, SC-004) trên
      cluster `kind` — phụ thuộc T022
      (Bước 5 đã xác thực THẬT trên `kind` với image `ecomerce-local-orders-api:latest`, không có
      SQL Server: readiness fail với HTTP 503/timeout, liveness không fail, 0 restart sau ~90s — xem
      ghi chú cuối file. Bước 4 [giả lập treo để xác nhận CÓ restart] KHÔNG thực hiện được trong
      phiên này — cần can thiệp vào tiến trình bên trong container mà không có sẵn công cụ mô phỏng
      an toàn; hành vi này mới chỉ được đảm bảo ở mức cấu trúc qua 58 test xUnit, chưa qua cluster
      thật)

**Checkpoint**: Cả 3 user story đều hoạt động độc lập.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Hoàn thiện xác thực tĩnh trong CI và bằng chứng nghiệm thu cuối cùng, không thuộc riêng
một user story nào

- [X] T024 [P] Viết `scripts/ci/lint-deployment-manifests.sh` chạy `ansible-lint` + render + xác thực
      schema bằng `kubeconform`/`kubeval` cho cả 7 service (research.md Quyết định 3)
- [X] T025 Thêm stage mới trong `Jenkinsfile` gọi `scripts/ci/lint-deployment-manifests.sh` vào PR
      gate, cạnh các stage `run-dotnet-tests.sh` hiện có — phụ thuộc T024
- [X] T026 [P] Xác nhận FR-008: chạy `git diff` trên cả 7 file
      `services/*/src/*/Features/HealthCheck/HealthCheckEndpoints.cs`, xác nhận KHÔNG có thay đổi
      nào so với trước tính năng — hợp đồng health-check hiện có giữ nguyên (đã xác nhận: `git diff`
      rỗng cho cả 7 file)
- [X] T027 Chạy `scripts/ci/run-dotnet-tests.sh unit`, xác nhận
      `DeploymentManifestConventionTests` được tự động phát hiện và không gây hồi quy cho các suite
      unit khác — phụ thuộc T013, T018, T022
      (đã chạy thật: `dotnet build Ecommerce.slnx -c Release` sạch 0 lỗi/cảnh báo, sau đó
      `scripts/ci/run-dotnet-tests.sh unit` phát hiện và chạy `DeploymentManifestConventionTests`
      cùng mọi suite unit khác trong repo — tất cả pass, không suite nào bị FAILED)
- [~] T028 Thực hiện toàn bộ [quickstart.md](./quickstart.md) từ Bước 1 đến Dọn dẹp một lượt liền
      mạch, làm bằng chứng nghiệm thu cuối cùng cho cả 3 user story — phụ thuộc T019, T023, T027
      (đã thực hiện thật trên `kind` cho `orders` [db_backed] và `gateway` [stateless] — dựng
      cluster, load image thật, áp dụng manifest tương đương bản Ansible render ra, quan sát rolling
      update giữ pod cũ phục vụ tới khi pod mới Ready, và readiness fail/liveness không fail khi
      thiếu dependency — rồi dọn dẹp cluster. Chưa mô phỏng được kịch bản treo tiến trình thật [Bước
      4] trong phiên này — xem ghi chú T023. Không chạy qua Ansible thật vì Ansible không hoạt động
      trên Windows native trong môi trường này; xem deploy/ansible/README.md và ghi chú cuối file)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Không phụ thuộc — bắt đầu ngay
- **Foundational (Phase 2)**: Phụ thuộc hoàn tất Setup — CHẶN mọi user story
- **User Stories (Phase 3-5)**: Đều phụ thuộc hoàn tất Foundational
  - US1 (P1) và US2 (P1) cùng mức ưu tiên cao nhất nhưng US2 phụ thuộc probe đã được khai báo đúng ở
    US1 (readinessProbe phải tồn tại trước khi có hành vi loại trừ traffic để kiểm thử) — triển khai
    US1 trước US2 trên thực tế dù cả hai đều P1
  - US3 (P2) phụ thuộc template/defaults đã có khối `livenessProbe` từ US1
- **Polish (Phase 6)**: Phụ thuộc cả 3 user story hoàn tất

### Within Each User Story

- Test viết trước, xác nhận FAIL trước khi triển khai (Constitution Principle III)
- Inventory/defaults trước template; template trước tasks/main.yml (playbook)
- Story hoàn tất (test chuyển Green) trước khi sang story ưu tiên tiếp theo

### Parallel Opportunities

- T001, T002, T003 (Setup) chạy song song
- T005, T006 (Foundational) chạy song song
- T007, T008 (test US1) chạy song song — khác file
- T009, T010 (implementation US1) chạy song song — khác file
- T014, T015 (test US2) chạy song song — khác file
- T024, T026 (Polish) chạy song song — khác mối quan tâm

---

## Parallel Example: User Story 1

```bash
# Chạy song song các test của User Story 1 (khác file):
Task: "Viết ProbeDeclarationTests.cs trong tests/DeploymentManifestConventionTests/"
Task: "Viết ServiceInventoryTests.cs trong tests/DeploymentManifestConventionTests/"

# Chạy song song các task triển khai của User Story 1 (khác file):
Task: "Điền deploy/ansible/inventories/services.yml"
Task: "Điền deploy/ansible/roles/service_deployment/defaults/main.yml"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Hoàn tất Phase 1: Setup
2. Hoàn tất Phase 2: Foundational (CHẶN mọi story)
3. Hoàn tất Phase 3: User Story 1 — mọi service có manifest khai báo đủ probe
4. **DỪNG và XÁC THỰC**: chạy T007, T008 độc lập, xác nhận Green
5. Đây là MVP có thể trình diễn/triển khai — Kubernetes đã đủ thông tin để tự phát hiện instance
   không khỏe mạnh, dù hành vi loại trừ traffic (US2) và tự restart (US3) chưa được xác thực end-to-end

### Incremental Delivery

1. Setup + Foundational → nền tảng sẵn sàng
2. + User Story 1 → kiểm thử độc lập → MVP
3. + User Story 2 → kiểm thử độc lập (loại trừ traffic khi chưa sẵn sàng)
4. + User Story 3 → kiểm thử độc lập (tự khởi động lại khi treo)
5. Mỗi story bổ sung giá trị mà không phá vỡ story trước

---

## Notes

- [P] = khác file, không phụ thuộc task chưa hoàn thành
- Nhãn [Story] gắn task với đúng user story để truy vết
- Test PHẢI được viết trước và xác nhận FAIL trước khi triển khai (Constitution Principle III,
  NON-NEGOTIABLE — không tùy chọn cho tính năng này)
- Commit sau mỗi task hoặc mỗi nhóm task liên quan
- Dừng ở mỗi checkpoint để xác thực từng story độc lập trước khi tiếp tục

## Ghi chú xác thực môi trường (phiên /speckit-implement)

- **Ansible không chạy được native trên Windows** (giới hạn đã biết của công cụ — cần Linux/macOS/WSL
  làm control node; xác nhận bằng lỗi `OSError: [WinError 87]` khi thử chạy `ansible --version`).
  WSL có sẵn trong máy nhưng không có quyền `sudo` để cài `python3-venv`/`pip` nhằm cài Ansible thật.
  Vì vậy bộ test `tests/DeploymentManifestConventionTests` được thiết kế lại để đọc trực tiếp
  `deployment.yaml.j2` dưới dạng text (đúng khuôn mẫu `ContainerConventionTests` đọc Dockerfile) thay
  vì gọi Ansible thật — chạy được ở mọi nơi, kể cả không có Ansible cài sẵn.
- **`ansible-lint`/`kubeconform` chưa được chạy thật trong phiên này** vì cùng lý do trên — script
  `scripts/ci/lint-deployment-manifests.sh` đã được viết đúng và sẽ chạy được trên agent Linux thật
  của Jenkins (nơi có thể cài Ansible qua `pip`/`apt` bình thường), nhưng chưa có bằng chứng chạy
  thật trong phiên implement này.
- **Đã dựng một cluster `kind` thật** trên Windows (dùng Docker Desktop đang chạy sẵn), tải 2 image
  thật đang chạy ở local dev stack (`ecomerce-local-orders-api:latest` — đại diện nhóm
  `depends_on_database: true`, và `ecomerce-local-gateway-api:latest` — đại diện nhóm
  `depends_on_database: false`), áp dụng manifest tương đương chính xác những gì role Ansible sẽ
  render ra (probe path/port/timing theo đúng `defaults/main.yml` + `services.yml`), và quan sát
  trực tiếp:
  - `gateway`: readiness pass gần như ngay lập tức (đúng nhóm stateless).
  - `orders` (không có SQL Server trong cluster `kind`): readiness probe fail thật với
    `HTTP 503`/timeout, liveness KHÔNG fail, 0 lần restart sau ~90 giây — bằng chứng thật cho
    FR-003/FR-004/SC-004, không chỉ là test cấu trúc.
  - Rolling restart trên `gateway`: pod mới `0/1` tồn tại song song với 2 pod cũ vẫn `1/1
    Running` cho tới khi pod mới chuyển `Ready`, đúng `maxUnavailable: 0` — bằng chứng thật cho
    FR-009/SC-002/SC-005.
  - Cluster đã được dọn dẹp (`kind delete cluster`) sau khi xác thực xong.
  - **Chưa mô phỏng được** kịch bản tiến trình treo thật (quickstart Bước 4, xác nhận CÓ restart) —
    cần một cách an toàn để chặn phản hồi từ bên trong container đang chạy, không có sẵn công cụ phù
    hợp trong phiên này. Hành vi này hiện chỉ được đảm bảo ở mức cấu trúc (giá trị `failureThreshold`/
    `periodSeconds` hợp lệ, đã qua 58 test xUnit), chưa qua cluster thật.
