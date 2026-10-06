---

description: "Danh sách task triển khai danh mục 8 nhóm lỗi để luyện troubleshoot (Spec C của đợt rà soát nợ kỹ thuật)"
---

# Tasks: Danh mục 8 nhóm lỗi để luyện troubleshoot (tiêm bằng cấu hình trên Docker Compose, lỗi có chủ đích và lỗi bất ngờ, gợi ý theo mức)

**Input**: Tài liệu thiết kế tại `specs/031-error-group-catalog/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: KHÔNG. Người dùng chốt không viết test tự động cho script mở rộng. Đây là sai lệch Nguyên
tắc III, đã ghi ở plan.md Complexity Tracking, hạn tới khi spec D hoàn tất. Mọi kiểm chứng chạy thật
theo [quickstart.md](./quickstart.md); chỉ đánh `[X]` khi đã có bằng chứng (đầu ra lệnh, trạng thái
container, số đo telemetry) ghi vào ghi chú của task hoặc vào research.md "Kết quả xác minh".

**Ràng buộc chung**:
- KHÔNG sửa file nào dưới `services/` hay `shared/`.
- KHÔNG sửa `docker-compose*.yml` hay `.env.example` để tiêm lỗi.
- KHÔNG sửa `deploy/ansible/**`, `Jenkinsfile`, `scripts/ci/lint-deployment-manifests.sh`,
  `tests/DeploymentManifestConventionTests`, hiến chương.
- `CHAOS_ALLOW_FAULT_INJECTION` và `CHAOS_ALLOW_LATENCY_INJECTION` về `false` sau mỗi buổi chạy.
- KHÔNG commit (người dùng tự commit). KHÔNG xoá Elastic khi chưa hỏi lại người dùng.
- Mọi mục có chữ **DỪNG VÀ HỎI** nghĩa là: không tự đổi phương án, báo kết quả đo và hỏi người dùng.

**Organization**: Nhóm theo user story của spec.md (US1–US6).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Chạy song song được (khác file, không phụ thuộc task chưa xong)
- **[Story]**: User story mà task phục vụ (US1–US6)

## Path Conventions

- Script: `scripts/incident-drill.ps1`; danh mục: `scripts/incident-drill/catalog.json`; file sinh ra:
  `.incident-drill/<runId>/` (không commit; đã có trong `.gitignore` từ 028).
- Container compose: `ecomerce-local-<service>-1`; network `ecomerce-local_backbone`.
- 7 service app: `gateway-api`, `bff-api`, `products-api`, `baskets-api`, `orders-api`, `parties-api`,
  `identity-api`; 5 DB: `products-db`, `baskets-db`, `orders-db`, `parties-db`, `identity-db`.
- Quy trình và bản ghi: `docs/dien-tap-chaos-engineering/`.

---

## Phase 1: Setup

**Purpose**: Nhánh làm việc đúng và stack chạy được.

- [X] T001 Xác nhận đang ở nhánh `feature/031-error-group-catalog` và `git status` chỉ có file của `specs/031-error-group-catalog/` (và `.specify/feature.json`). Nếu không phải, báo người dùng, không tự chuyển nhánh.
- [X] T002 Dựng stack bằng `docker compose -f docker-compose.local.yml up -d --build --wait`, xác nhận 7 service app `healthy`. Nếu stack hỏng vì lý do không thuộc 031 thì **DỪNG VÀ HỎI**. Đặt `CHAOS_ALLOW_FAULT_INJECTION=true` và `CHAOS_ALLOW_LATENCY_INJECTION=true` trong `.env` (cục bộ, không commit), tạo lại stack để container nhận cờ. Ghi vào ghi chú.
- [X] T003 [P] Tạo thư mục `scripts/incident-drill/` (file `.gitkeep` nếu cần để theo dõi trước khi có `catalog.json`).

---

## Phase 2: Foundational — Kiểm chứng V1–V9 trên stack thật (Blocking)

**Purpose**: Mọi thiết kế của plan dựa vào hành vi Docker/service chưa kiểm chứng. Kết quả ghi vào
mục "Kết quả xác minh" của [research.md](./research.md) (thay dòng "Chưa xác minh").

**⚠️ CRITICAL**: chặn mọi user story. Chạy tuần tự (dùng chung một stack). Mỗi task ghi: lệnh đã chạy,
số đo, kết luận "đúng dự kiến / sai dự kiến". Tải nền `./scripts/incident-drill.ps1 -Load` chạy ở một
terminal riêng suốt Phase 2.

- [X] T004 V1 (nhóm E): `docker stop ecomerce-local-orders-db-1` → xác nhận `orders-api` vẫn chạy, `/health/ready` trả `503` trong vài giây; `docker start` → xác nhận `orders-api` ready lại KHÔNG cần restart, và đo thời gian phục hồi. Lặp cho một DB khác (ví dụ `products-db`). **Sai dự kiến → DỪNG VÀ HỎI.**
- [X] T005 V2 (nhóm F biến thể authority-wrong): tạo lại một service (ví dụ `products-api`) với `Identity__Authority=http://incident-missing-host:8080` bằng file override tạm trong scratchpad; gọi route có token qua gateway và trực tiếp; ghi triệu chứng đo được (401/500/timeout) và service nào bị ảnh hưởng nếu đích là `gateway-api` hoặc `bff-api`. Khôi phục bằng tạo lại không override. **Không có triệu chứng đo được → DỪNG VÀ HỎI.**
- [X] T006 V3 (nhóm F biến thể identity-stopped — **ĐÃ BỎ biến thể**: 10 phút dừng identity-api không có triệu chứng trên đường dữ liệu; người dùng chốt bỏ F-ii ở `/speckit-implement`): `docker stop ecomerce-local-identity-api-1` trong khi `-Load` đang chạy; theo dõi 10 phút, ghi có triệu chứng đo được không (token cũ có thể vẫn hợp lệ nhờ khoá ký cache) và sau bao lâu. `docker start`, ghi `-Load` lấy token lại thế nào. **Không có triệu chứng → DỪNG VÀ HỎI người dùng** (có thể bỏ biến thể ii).
- [X] T007 V4 (**Kết quả: 96m OOM-kill; người dùng chốt 256m ở `/speckit-implement`**) (nhóm G): `docker update --cpus 0.1 --memory 256m --memory-swap 256m ecomerce-local-products-api-1`; ghi lệnh được chấp nhận không, container có bị OOM-kill (`docker inspect` `OOMKilled`/exit 137) không, p95 trong telemetry/qua Postman. Khôi phục bằng `docker compose -p ecomerce-local -f docker-compose.local.yml up -d --force-recreate --no-deps products-api`. **Nếu OOM-kill → DỪNG VÀ HỎI** (triệu chứng thuộc nhóm 8; có thể phải tăng memory).
- [X] T008 V5 (nhóm H): `docker network disconnect ecomerce-local_backbone ecomerce-local-baskets-api-1`; ghi triệu chứng (gateway/BFF, cổng `localhost:5188`). Khôi phục `docker network connect --alias baskets-api ecomerce-local_backbone ecomerce-local-baskets-api-1`; xác nhận healthy và cổng publish hoạt động lại, đo thời gian. **Sai dự kiến → DỪNG VÀ HỎI.**
- [X] T009 V6 (nhóm I): `docker kill ecomerce-local-parties-api-1`; xác nhận exit 137, KHÔNG tự sống lại (không có `restart:`); khôi phục bằng `up -d --force-recreate --no-deps parties-api`, xác nhận healthy trong ≤ 10 phút. **Sai dự kiến → DỪNG VÀ HỎI.**
- [X] T010 (**Kết quả: header KHÔNG tới orders qua gateway; người dùng chốt gửi thẳng :5041 kèm token + X-Tenant-Id**) V9 (nhóm D, đường gateway): lấy token từ `.incident-drill/load/environment-with-token.json`; gọi `GET http://localhost:5300/bff/orders/{guid}` có header `X-Chaos-Latency-Ms: 2000` và không có header; so thời gian phản hồi. **Header không làm tăng ≈ 2 s (không tới orders) → DỪNG VÀ HỎI** (phương án dự phòng: gửi trực tiếp `:5041/orders/{guid}` như loại C, cần người dùng duyệt).
- [X] T011 V7 (nhóm D, tốc độ gửi): đo tốc độ span nền của `Orders.Api` bằng ES|QL (như `Get-BaselineSpansPerSecond` trong script); thử gửi bất đồng bộ (không chờ phản hồi) request mang header với tốc độ `r/(1−r) × nền` cho `r` = 5% và 50% bằng `[System.Net.Http.HttpClient]` và `SendAsync` trong PowerShell 5.1; ghi tỷ lệ span chậm đạt được so với mục tiêu và cách gửi chọn dùng.
- [X] T012 V8 (PowerShell 5.1): trong scratchpad, thử ghi/đọc `state.json` và `hint-log.json` không BOM (`[System.IO.File]::WriteAllText` với `UTF8Encoding($false)`), `ConvertFrom-Json` mảng rỗng/mảng một phần tử (PowerShell 5.1 làm phẳng mảng một phần tử), và hủy tiến trình nền đang `Start-Sleep` bằng `Stop-Process -Id` từ PID đã lưu. Ghi kết luận và cách xử lý mảng một phần tử.
- [X] T013 Cập nhật [research.md](./research.md) mục "Kết quả xác minh" với kết quả V1–V9 (bảng: điểm, lệnh, số đo, kết luận). Phụ thuộc T004–T012. Nếu có bất kỳ V nào "sai dự kiến" và người dùng đã quyết, ghi quyết định mới vào đây và vào Clarifications của spec.md.

**Checkpoint**: V1–V9 xong và không còn điểm chờ quyết định — mới được làm US1.

---

## Phase 3: User Story 1 - Danh mục 8 nhóm lỗi độc lập với nơi tiêm (Priority: P1) 🎯 MVP

**Goal**: Danh mục dữ liệu và bộ chuyển đổi Compose tách bạch; script nạp và kiểm danh mục; mỗi loại có
thao tác tiêm/khôi phục trong bộ chuyển đổi.

**Independent Test**: Quickstart Bước 1 (đọc danh mục, không cần Docker) rồi từ chối loại sai đích
(Bước 2, mục từ chối).

### Implementation for User Story 1

- [X] T014 [US1] Viết `scripts/incident-drill/catalog.json` theo [data-model.md](./data-model.md) mục 1 và [contracts/fault-catalog-contract.md](./contracts/fault-catalog-contract.md): `version: 1`, 8 nhóm (tên và mô tả tiếng Việt có dấu), 9 loại A–I với `targetKind`, `targets`, `variants` (F), `parameters` (B `maxConnectionsPerServer=1`; C `errorRatePctRange=[5,50]`; D `latencyMs=2000`, `errorRatePctRange=[5,50]`; G `cpuLimit=0.1`, `memoryLimitMb=256` — hoặc giá trị mới nếu T007/T013 đã đổi theo người dùng), `requiresFlags`, `masksByRecreatingAll`, `restoreKind`, `symptoms`. `hints` tạm là ba chuỗi `""` (viết ở T035). Không chứa chữ `docker` hay `${`.
- [X] T015 [US1] Trong `scripts/incident-drill.ps1`, thêm hàm nạp danh mục (`Import-FaultCatalog`) đọc `$PSScriptRoot/incident-drill/catalog.json`, kiểm bất biến 1, 2, 4, 6, 7, 8, 10 của fault-catalog-contract và `hints.1`/`hints.2` không chứa tên đích hay mã loại (chỉ kiểm khi `hints` không rỗng); lỗi thì `Stop-WithReason`. Gọi hàm này đầu mọi lệnh tiêm (bất biến 22 của script contract).
- [X] T016 [US1] Trong `scripts/incident-drill.ps1`, thêm kiểm cờ mới: giữ `Test-FaultInjectionAllowed` cho mọi loại, thêm `Test-LatencyInjectionAllowed` đọc `CHAOS_ALLOW_LATENCY_INJECTION` từ `.env` cho loại D; thông báo nêu rõ cờ nào thiếu (bất biến 20, 21).
- [X] T017 [US1] Trong `scripts/incident-drill.ps1`, thêm hàm kiểm áp dụng được `Test-FaultApplicable` dựa trên danh mục (loại + đích + biến thể F), thay thế `Get-ApplicableFaultTypes` cho các đường mới; giữ `Get-ApplicableFaultTypes` cho đường không mù cũ nếu cần tương thích (bất biến 23).
- [X] T018 [US1] Trong `scripts/incident-drill.ps1`, thêm khung bộ chuyển đổi Compose: các hàm `Invoke-ComposeInject` và `Invoke-ComposeRestore` nhận `$Sealed` và `switch` trên `faultType`; dựng thân cho A, B, C (tái dùng logic hiện có: `Write-OverrideFile`, recreate 7 container, vòng header C) và để lại `throw "chưa hỗ trợ"` cho D–I (sẽ điền ở US2). Không đổi hành vi `-Start`/`-Reveal` hiện có ở bước này.
- [X] T019 [US1] Kiểm chứng: đọc `catalog.json` theo Quickstart Bước 1 và gọi hàm `Test-FaultApplicable` (T017) trong một phiên PowerShell với `B`/`orders-api` và `D`/`products-api` để xác nhận bị từ chối (lệnh `-Inject` chưa có ở phase này). Ghi kết quả vào ghi chú. Phụ thuộc T014–T018.

**Checkpoint**: danh mục và khung bộ chuyển đổi tồn tại; chưa tiêm được loại mới nào.

---

## Phase 4: User Story 2 - Tiêm lỗi có chủ đích theo nhóm (dạng a) (Priority: P1)

**Goal**: `-Inject`, `-Restore`, `-DurationSeconds` chạy được cho cả 9 loại, với triệu chứng đo được và khôi phục về healthy.

**Independent Test**: Quickstart Bước 2 cho từng loại A–I.

### Implementation for User Story 2

- [X] T020 [US2] Trong `scripts/incident-drill.ps1`, thêm tham số: `-Inject`, `-Restore`, `-Type` (ValidateSet A–I), `-Group` (ValidateRange 1–8), `-Target`, `-DurationSeconds` (ValidateRange 1–86400). Không đổi tham số cũ. Cập nhật mục `.SYNOPSIS/.DESCRIPTION/.EXAMPLE` ở đầu file (tiếng Việt có dấu) mô tả lệnh mới.
- [X] T021 [US2] Trong `scripts/incident-drill.ps1`, thêm quản lý `state.json` (`Get-RunState`, `Set-RunState`) và hàm `Assert-NoOpenRun` quét `.incident-drill/*/state.json` từ chối khi có `status ∈ {pending, injected, failed}` (bất biến 24); lần chạy 028 cũ không có `state.json` được bỏ qua. Ghi không BOM, xử lý mảng một phần tử theo kết quả T012.
- [X] T022 [US2] Trong `scripts/incident-drill.ps1`, thêm nhánh `-Inject`: kiểm cờ (T016), nạp danh mục (T015), `Assert-NoOpenRun`, suy ra loại từ `-Group`/`-Type`, bốc đích hợp lệ nếu thiếu `-Target`, suy biến thể F từ đích, ghi `sealed.json` (`blind=false`, `mode=scripted`, `delaySeconds=0`) và `hash.txt`, ghi `state.json` (`pending`), khởi tiến trình nền, in rõ nhóm/loại/đích/tham số (bất biến 25).
- [X] T023 [US2] Trong `scripts/incident-drill.ps1`, mở rộng `Invoke-InjectorProcess` để gọi `Invoke-ComposeInject`, ghi `injected-at.txt`, `state.json` (`injected`, `injectorPid`, `injectedAt`), và khi có `durationSeconds` thì ngủ đủ thời lượng rồi gọi `Invoke-ComposeRestore` (bất biến 26). Lỗi ở bất kỳ bước → `state.json` `failed` kèm `failure`, ghi `injector.log`.
- [X] T024 [US2] Trong `scripts/incident-drill.ps1`, điền bộ chuyển đổi cho **nhóm E** (`docker stop` DB; khôi phục `docker start`, chờ DB `healthy` rồi service chủ `/health/ready` 200, ≤ 10 phút) và **nhóm I** (`docker kill`; khôi phục tạo lại container đích từ compose, chờ healthy ≤ 10 phút).
- [X] T025 [US2] Trong `scripts/incident-drill.ps1`, điền bộ chuyển đổi cho **nhóm F** (chỉ `Identity__Authority=http://incident-missing-host:8080` cho đích qua file override, recreate 7 container như A; KHÔNG có biến thể dừng identity-api, `identity-api` không là đích) và **nhóm G** (`docker update --cpus <cpuLimit> --memory <memoryLimitMb>m --memory-swap <memoryLimitMb>m`; khôi phục tạo lại container đích từ compose). Tham số lấy từ danh mục, không hard-code.
- [X] T026 [US2] Trong `scripts/incident-drill.ps1`, điền bộ chuyển đổi cho **nhóm H** (`docker network disconnect ecomerce-local_backbone <container>`; khôi phục `docker network connect --alias <service> ecomerce-local_backbone <container>`, chờ healthy) và **nhóm D** (recreate 7 container nhận cờ từ `.env`; vòng gửi bất đồng bộ header `X-Chaos-Latency-Ms: <latencyMs>` thẳng tới `GET http://localhost:5041/orders/{guid}` kèm `Authorization: Bearer <token>` (lấy từ `.incident-drill/load/environment-with-token.json`) và `X-Tenant-Id: contoso` (V9: header không đi xuyên gateway), tốc độ `r/(1−r) × nền` theo T011; điều chỉnh route/cách gửi nếu T010/T013 đã đổi theo người dùng). Dừng vòng gửi khi `stop.flag` xuất hiện hoặc Id container đích đổi (bất biến 29).
- [X] T027 [US2] Trong `scripts/incident-drill.ps1`, mở rộng vòng gửi header của loại C để cùng dừng khi `stop.flag` xuất hiện (bất biến 29), giữ nguyên điều kiện dừng theo Id container.
- [X] T028 [US2] Trong `scripts/incident-drill.ps1`, thêm nhánh `-Restore -RunId`: nạp `sealed.json`/`state.json`; trạng thái `restored` → no-op mã 0; `pending` → dừng tiến trình nền theo `injectorPid`, đánh dấu `restored`, báo "chưa tiêm gì" (bất biến 28); ngược lại tạo `stop.flag` (C, D), gọi `Invoke-ComposeRestore`, chờ healthy ≤ 10 phút, ghi `restored-at.txt` và `state.json` `restored`; quá hạn → `failed`, mã khác 0 (bất biến 27, 30).
- [X] T029 [US2] Kiểm chứng từng loại theo Quickstart Bước 2, với tải nền chạy: A (một DB), B (`gateway-api`), C (`products-api`), D (`orders-api`), E (`orders-db`), F (`gateway-api`), G (`products-api`), H (`baskets-api`), I (`parties-api`). Với mỗi loại: lệnh `-Inject`, triệu chứng đo được, lệnh `-Restore`, container healthy, `state.json`. Kiểm thêm: `-DurationSeconds 120` tự khôi phục; từ chối khi cờ tắt; từ chối loại sai đích (`-Type B -Target orders-api`, `-Type D -Target products-api`); `-Inject` lần hai khi còn lần chạy mở bị từ chối (bất biến 24). Ghi bằng chứng vào ghi chú và, nếu phát hiện lỗi/giới hạn mới, ghi để T053 đưa vào QA_Debt. Phụ thuộc T020–T028.

**Checkpoint**: 9 loại tiêm và khôi phục được bằng lệnh tay.

---

## Phase 5: User Story 3 - Lỗi bất ngờ không báo trước (dạng b) trên 8 nhóm (Priority: P1)

**Goal**: `-Start` bốc mù từ cả 8 nhóm theo quy tắc đã duyệt; `-Reveal` mở rộng; hành vi 028 không đổi.

**Independent Test**: Quickstart Bước 3 (phần `-Start`/`-Restore`/`-Reveal`), cộng chạy lại các lệnh 028.

### Implementation for User Story 3

- [X] T030 [US3] Trong `scripts/incident-drill.ps1`, thay phần bốc thăm của `-Start` (nhánh mù hoàn toàn) bằng quy tắc: nhóm đều 1/8 → loại đều trong nhóm (nhóm 2: B hoặc C 1/2; B luôn đích gateway) → đích đều trong các đích áp dụng được → tham số (tỷ lệ 5–50% cho C/D) → độ trễ 0–30 phút; ghi `sealed.json` với `mode=blind` và các trường mới; ghi `state.json` (`pending`); gọi `Assert-NoOpenRun` và các kiểm cờ. Đầu ra chỉ `runId` và mã băm (bất biến 33). Nhánh không mù cũ (`-Start -Service -FaultType`) giữ nguyên, ghi `mode=legacy-nonblind`.
- [X] T031 [US3] Trong `scripts/incident-drill.ps1`, đảm bảo `Invoke-InjectorProcess` tạo lại cả 7 container chỉ với loại có `masksByRecreatingAll = true` (A, B, C, D, F) và KHÔNG tạo lại với E, G, H, I (bất biến "Thay đổi so với 028" mục 5); tách hàm `Invoke-RecreateAllServices` dùng chung nếu cần.
- [X] T032 [US3] Trong `scripts/incident-drill.ps1`, mở rộng `-Reveal`: giữ nguyên kiểm băm/in lựa chọn/xoá override; thêm in `state.json` (trạng thái, thời điểm tiêm/khôi phục) và `hint-log.json` (số lần, mức, thời điểm) khi có; chạy được với lần chạy 028 cũ không có hai file này (bất biến 34).
- [X] T033 [US3] Kiểm chứng: (a) chạy `-Start` ≥ 20 lần, mỗi lần `-Restore` ngay khi còn `pending` (hủy tiến trình nền, chưa tiêm) và đọc `sealed.json` để thống kê nhóm bốc được, thống kê số nhóm phủ ≥ 6/8 (SC-003); (b) với 5–6 lần cho các loại khác nhau chạy trọn vòng tiêm → `-Restore` → `-Reveal`, xác nhận băm khớp; (c) chạy lại `-Start -Service orders-api -FaultType B -DelaySeconds 0`, `-Load`, `-Reveal` của 028 và xác nhận hành vi cũ không đổi (FR-019). Ghi bằng chứng. Phụ thuộc T030–T032.

**Checkpoint**: dạng (b) chạy được; 028 không bị hồi quy.

---

## Phase 6: User Story 4 - Gợi ý theo mức cho dạng (b) (Priority: P2)

**Goal**: `-Hint` ba mức, nội dung trong danh mục, ghi `hint-log.json`, không ảnh hưởng mốc thời gian.

**Independent Test**: Quickstart Bước 3 (phần `-Hint`).

### Implementation for User Story 4

- [X] T034 [US4] Trong `scripts/incident-drill.ps1`, thêm tham số `-Hint` và `-Level` (ValidateSet 1,2,3) và nhánh `-Hint -RunId -Level`: nạp `sealed.json`; mức 1 và 2 đọc `hints.1`/`hints.2` của loại từ danh mục, KHÔNG in service/tham số/mã loại; mức 3 kiểm băm như `-Reveal` rồi in đáp án điền dấu giữ chỗ từ `sealed.json` và `hints.3`; luôn ghi thêm `{at, level}` vào `.incident-drill/<runId>/hint-log.json` (kể cả bỏ cách mức); không ghi vào Kibana Case, không sửa `sealed.json`/`injected-at.txt` (bất biến 31, 32). Xử lý mảng một phần tử khi ghi `hint-log.json` theo T012.
- [X] T035 [P] [US4] Viết văn bản `hints` ba mức cho 9 loại (27 đoạn tiếng Việt có dấu) vào `scripts/incident-drill/catalog.json`, theo phong cách đã duyệt: mức 1 mô tả triệu chứng và hướng nhìn (không tên service/DB/tham số/mã loại); mức 2 chỉ nêu tên nhóm lỗi; mức 3 có dấu giữ chỗ `{service}`, `{target}`, `{parameters}`. Ví dụ loại D: mức 1 "Độ trễ p95 của một service tăng vọt trong khi tỷ lệ 5xx gần như không đổi; hãy so sánh span chậm với span bình thường"; mức 2 "Nhóm 3 — độ trễ"; mức 3 "Loại D: Orders.Api, trễ 2000 ms, tỷ lệ {errorRatePct}%".
- [X] T036 [US4] **DỪNG VÀ HỎI người dùng**: trình toàn bộ 27 đoạn hints (bảng loại × mức) để người dùng duyệt/sửa; chỉ khi có xác nhận mới đánh `[X]` T035 và tiếp tục. Phụ thuộc T035.
- [X] T037 [US4] Kiểm chứng theo Quickstart Bước 3: với một runId bốc mù (rồi `-Restore`), mở mức 1, 2, 3; xác nhận mức 1/2 không chứa tên đích/tham số/mã loại; mức 3 đúng đáp án; `hint-log.json` có đúng 3 dòng đủ thời điểm và mức; bỏ cách mức (xin mức 3 trước) vẫn ghi đúng mức đã xin; `-Reveal` in nhật ký; `injected-at.txt` không đổi. Phụ thuộc T034, T036.

**Checkpoint**: gợi ý theo mức dùng được, văn bản đã được người dùng duyệt.

---

## Phase 7: User Story 5 - Gỡ phần Kubernetes của 025 (Priority: P2)

**Goal**: Không còn kill-pod/hướng dẫn k8s trong tài liệu sống của 025; hạ tầng Ansible/k8s thật giữ nguyên.

**Independent Test**: Quickstart Bước 4 (`rg` không có kết quả; `git diff` ansible/CI trống).

### Implementation for User Story 5

- [X] T038 [US5] Lập danh sách vị trí cần sửa: chạy `rg -n -i "kubectl|kill-pod|kill pod|giết pod|delete pod|cluster kind|kind cluster|\bpod\b" specs/025-chaos-pod-kill-latency docs/dien-tap-chaos-engineering docs/PO/025* docs/QA/025* docs/architecture/025* docs/development/025* docs/spec-summary-vi/025* docs/diagrams/025*` và `docs/QA/QA_Debt.md` (mục 025), `docs/architecture/technical-debt.md` (mục 025); ghi danh sách vào ghi chú của task. Không sửa gì ở task này.
- [X] T039 [P] [US5] Sửa `specs/025-chaos-pod-kill-latency/spec.md`: bỏ User Story kill-pod, kịch bản kill-pod, FR/SC kill-pod, điều kiện cluster; giữ phần tiêm độ trễ; thêm ghi chú đầu file "Kịch bản kill-pod/Kubernetes đã gỡ ở spec 031 (thay bằng nhóm 8 — container chết)". Giữ nguyên các FR/SC còn lại đúng số thứ tự hoặc ghi chú "đã gỡ" thay vì đánh số lại.
- [X] T040 [P] [US5] Sửa `specs/025-chaos-pod-kill-latency/plan.md`, `research.md`, `data-model.md` theo cách T039: bỏ phần kill-pod/k8s, thêm cùng ghi chú trỏ sang spec 031.
- [X] T041 [P] [US5] Sửa `specs/025-chaos-pod-kill-latency/tasks.md` (32 chỗ nhắc k8s): đánh dấu các task kill-pod/k8s "đã gỡ ở spec 031" (không xoá số task để giữ tham chiếu), giữ task tiêm độ trễ. Sửa `contracts/exercise-outcome-writeup-contract.md`, `contracts/chaos-latency-injection-contract.md`, `checklists/requirements.md` nếu còn nhắc kill-pod/k8s.
- [X] T042 [P] [US5] Sửa `specs/025-chaos-pod-kill-latency/quickstart.md`: bỏ Bước 1 (kill pod bằng kubectl, dòng ~30–31) và điều kiện tiên quyết về cluster; giữ Bước 2–4 (tiêm độ trễ, dashboard, ghi nhận) — đổi điều kiện tiên quyết sang Docker Compose local (`docker-compose.local.yml`, `CHAOS_ALLOW_LATENCY_INJECTION`); thêm đoạn trỏ sang spec 031 cho kịch bản "container chết" (nhóm 8).
- [X] T043 [US5] Xoá `docs/dien-tap-chaos-engineering/ket-qua/2026-09-12-kill-pod.md` và `docs/dien-tap-chaos-engineering/ket-qua/2026-09-14-kill-pod.md` (chỉ hai file này; giữ `2026-09-14-inject-latency.md`). Sửa `docs/dien-tap-chaos-engineering/README.md` (bỏ hai bản ghi khỏi "Lịch sử chạy", bỏ phần kill-pod/k8s) và `mau-ket-qua.md` (bỏ trường/ví dụ kill-pod); thêm vào README mục "Danh mục nhóm lỗi (spec 031)" trỏ tới `scripts/incident-drill/catalog.json` và lệnh `-Inject`/`-Restore`/`-Hint`. Phụ thuộc T038. Trước khi xoá, `ls` xác nhận đúng hai file.
- [X] T044 [P] [US5] Sửa `docs/PO/025_PO_diễn tập chaos engineering giết pod tiêm độ trễ.md`, `docs/QA/025_QA_diễn tập chaos giết pod tiêm độ trễ.md`, `docs/architecture/025_Architect_diễn tập chaos engineering giết pod tiêm độ trễ.md`, `docs/development/025_Development_diễn tập chaos engineering giết pod tiêm độ trễ.md`: gỡ phần kill-pod/k8s, thêm ghi chú thay bằng nhóm 8 của 031; giữ nguyên tên file (đã được tham chiếu từ nơi khác).
- [X] T045 [P] [US5] Sửa `docs/spec-summary-vi/025-chaos-pod-kill-latency.json` (gỡ FR/SC kill-pod, giữ JSON hợp lệ) và `docs/diagrams/025-chaos-pod-kill-latency-component.drawio` (gỡ khối k8s/kill-pod, giữ XML hợp lệ, đúng 1 `<diagram>`).
- [X] T046 [US5] Sửa `docs/QA/QA_Debt.md` mục `## 025` và `docs/architecture/technical-debt.md` mục 025: gỡ câu/mục về kill-pod và cluster (kể cả finding namespace `chaos-exercise` kẹt `Terminating`, `kubectl cp`/`exec`), giữ phần tiêm độ trễ; KHÔNG sửa mục 018/019 (hạ tầng k8s thật). Phụ thuộc T038.
- [X] T047 [US5] Kiểm chứng gỡ xong (SC-005): chạy lại `rg` của T038 — không còn kết quả trong phạm vi 025 (trừ chỗ trỏ sang 031); chạy `git diff master -- deploy/ansible Jenkinsfile scripts/ci/lint-deployment-manifests.sh tests/DeploymentManifestConventionTests` — trống; xác nhận `ls docs/dien-tap-chaos-engineering/ket-qua/` chỉ còn `2026-09-14-inject-latency.md`; parse JSON/drawio đã sửa. Ghi kết quả. Phụ thuộc T039–T046.

**Checkpoint**: tài liệu 025 không còn k8s; hạ tầng Ansible nguyên vẹn.

---

## Phase 8: User Story 6 - Postman và tài liệu đi kèm theo nếp 027/028 (Priority: P3)

**Goal**: Folder Postman 31, tài liệu PO/QA/Architect, 3 sơ đồ drawio, cập nhật nợ.

**Independent Test**: Chạy folder Postman 31 theo hướng dẫn QA thủ công; `rg`/parse kiểm tài liệu.

### Implementation for User Story 6

- [X] T048 [US6] Thêm folder `31 - Danh mục nhóm lỗi` vào `postman/ecommerce.postman_collection.v2.json`, theo khuôn các folder 25–28, có mô tả tiếng Việt giải thích vì sao thao tác tiêm/khôi phục do người vận hành điều khiển ngoài Postman (`.env`/compose/`incident-drill.ps1`). 6 subfolder `D … I`; mỗi subfolder gồm 2 request: `01 Gây triệu chứng — …` (kỳ vọng lỗi/chậm theo từng nhóm, ví dụ D: `GET {{ordersUrl}}/orders/{{$guid}}` có header `X-Chaos-Latency-Ms: 2000`, `X-Tenant-Id: {{tenantId}}` và token, test thời gian phản hồi ≥ 1900 ms (V9: không qua gateway); E: `GET /products` hoặc route của service chủ DB, test `503`/lỗi; F: route có token, test 401/5xx; G: test độ trễ tăng; H/I: test lỗi kết nối/5xx) và `02 Kiểm khôi phục — …` (kỳ vọng bình thường: 2xx hoặc 404 đúng như folder 26/28). Dùng đúng tên biến token/URL mà folder 26/28 đang dùng (mở folder đó để chép). Điều chỉnh kỳ vọng theo kết quả T013. Phụ thuộc T029.
- [X] T049 [US6] Chạy từng subfolder D–I bằng newman theo hướng dẫn thủ công (tiêm bằng `-Inject`, chạy request 01, `-Restore`, chạy request 02) và ghi kết quả; sửa test/kỳ vọng của folder nếu sai. Phụ thuộc T048.
- [X] T050 [P] [US6] Tạo `docs/architecture/031_Architect_danh mục 8 nhóm lỗi luyện troubleshoot.md` theo cấu trúc và văn phong `docs/architecture/028_Architect_diễn tập sự cố thật và phản ứng on-call.md`, gồm mục `## Sơ đồ` trỏ tới 3 file của T054–T056; nêu rõ tách danh mục khỏi bộ chuyển đổi Compose và điểm mở rộng cho bộ chuyển đổi CD/Kubernetes.
- [X] T051 [US6] Cập nhật `docs/architecture/technical-debt.md`, thêm mục 031: sai lệch Nguyên tắc III (không test, hạn tới khi spec D hoàn tất); rủi ro danh mục và bộ chuyển đổi lệch nhau; không che đích cho nhóm E, G, H, I; Redis/RabbitMQ ngoài phạm vi; nhiễu khởi động nguội 5–7 phút và lỗi lan theo chuỗi phụ thuộc áp dụng thêm cho E/F; token hỏng sau khi tạo lại identity áp dụng thêm cho I; Max Pool Size=1 không gây lỗi ở service có DB (không có loại B cho 5 service có DB); các phát hiện V1–V9 và quyết định kèm theo; Elastic sẽ clean sau triển khai (hỏi lại).
- [X] T052 [P] [US6] Tạo `docs/QA/031_QA_danh mục 8 nhóm lỗi luyện troubleshoot.md` theo khuôn QA 024–028 (nếp QA 008+): phần Thủ công đứng trước, bảng `Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | Đã quan sát`; điều khiển bằng `CHAOS_ALLOW_FAULT_INJECTION`/`CHAOS_ALLOW_LATENCY_INJECTION` (BẬT/TẮT và cách khôi phục `-Restore`), các lệnh `-Inject`/`-Restore`/`-Hint`/`-Start`, folder Postman 31; phần Tự động ghi "không có test tự động — sai lệch Nguyên tắc III, xem plan.md"; không có khối "Nguồn đối chiếu"; mọi phát hiện chỉ ở QA_Debt. Liên kết từng bước tới dòng/file tương ứng theo nếp QA hiện có.
- [X] T053 [US6] Thêm mục `## 031 — …` vào `docs/QA/QA_Debt.md` với mọi phát hiện từ T004–T049 (kể cả ghi chú "finding kill-pod/k8s của 025 đã gỡ ở T046"), và cập nhật số đếm trong tiêu đề file nếu có. Phụ thuộc T052.
- [X] T054 [P] [US6] Tạo `docs/diagrams/031-error-group-catalog-component.drawio` theo mẫu `docs/diagrams/028-incident-oncall-drill-component.drawio`, `<diagram>` duy nhất, nhãn tiếng Việt. Các khối: `incident-drill.ps1`; `catalog.json` (danh mục); bộ chuyển đổi Compose; `.incident-drill/` (sealed, state, hint-log); Docker Compose (7 service, 5 DB, network `backbone`); newman folder 31; OTel → Elasticsearch; điểm mở rộng "bộ chuyển đổi CD/Kubernetes (sau này)".
- [X] T055 [P] [US6] Tạo `docs/diagrams/031-error-group-catalog-flow-nghiep-vu.drawio` theo mẫu `028-...-flow-nghiep-vu.drawio`, ngôn ngữ nghiệp vụ: chọn cách luyện (có chủ đích theo nhóm / bất ngờ) → tiêm → quan sát triệu chứng → (xin gợi ý theo mức) → khôi phục → mở niêm phong đối chiếu.
- [X] T056 [P] [US6] Tạo `docs/diagrams/031-error-group-catalog-sequence.drawio` theo mẫu `028-...-sequence.drawio`, là trình tự thật của T029/T037 (người vận hành → script → danh mục → bộ chuyển đổi → Docker → telemetry), với số đo thật làm nhãn phụ.
- [X] T057 [US6] Tạo tài liệu PO `docs/PO/031_PO_danh mục 8 nhóm lỗi luyện troubleshoot.md` (tên file đã được người dùng chốt ở `/speckit-specify`): khuôn `docs/PO/027_PO_chính sách ngân sách lỗi và ngưỡng cảnh báo.md`, ~45–55 dòng, không tên công cụ kỹ thuật; các mục `## Vấn đề trước đây` → `## Giải pháp: …` → `## Trải nghiệm thực tế diễn ra như thế nào` → `## Lợi ích kinh doanh`.
- [X] T058 [US6] Cập nhật `docs/PO/functional-debt.md`: thêm mục 031 (link tới file PO của T057) vào cả `## 1. Điều đặc biệt` và `## 2. Giới hạn hiện tại`, ngôn ngữ nghiệp vụ, đối chiếu QA_Debt và technical-debt mục 031. Tiêu đề đang ghi "toàn bộ 26 tính năng": **DỪNG VÀ HỎI người dùng con số mới** trước khi sửa. Phụ thuộc T051, T053, T057.

**Checkpoint**: bộ tài liệu và Postman đầy đủ theo nếp 027/028.

---

## Phase 9: Polish & Cross-Cutting Concerns

- [X] T059 Kiểm tra cuối: (a) mọi link tương đối trong các file của T050–T058 trỏ tới file có thật (giải mã `%20`/tiếng Việt); (b) 3 file `031-*.drawio` và `025-*.drawio` parse được bằng `python -c "import xml.etree.ElementTree as E; E.parse(...)"`, mỗi file đúng 1 `<diagram>`; (c) `git status` KHÔNG có file nào dưới `services/`, `shared/`, `deploy/ansible/`, `.incident-drill/`, `docker-compose*.yml`, `.env.example`, `Jenkinsfile` bị sửa; (d) `catalog.json` không chứa `docker` hay `${`. Ghi kết quả vào ghi chú. Phụ thuộc T047, T049, T050–T058.
- [X] T060 Dọn cấu hình: trả `CHAOS_ALLOW_FAULT_INJECTION` và `CHAOS_ALLOW_LATENCY_INJECTION` về `false` (hoặc xoá dòng) trong `.env` cục bộ, `-Restore` mọi lần chạy còn mở, chạy lại stack không kèm override và xác nhận mọi container healthy; dừng tải nền `-Load` (Ctrl+C). Phụ thuộc mọi task chạy stack.
- [X] T061 **HỎI NGƯỜI DÙNG trước khi làm**: xoá toàn bộ dữ liệu Elastic sau khi triển khai xong spec này (đã chốt "clean toàn bộ, hỏi lại trước khi xoá"). Chỉ thực hiện khi người dùng xác nhận rõ phạm vi (index/data stream nào, hay xoá volume `local-es-data`). Task này để mở nếu người dùng chưa trả lời.
- [ ] T062 **HỎI NGƯỜI DÙNG**: buổi diễn tập mù thật (`-Start` rồi triage theo quy trình 028, xin `-Hint` khi bế tắc) do ai thực hiện và khi nào — Claude đã viết script nên không "mù". Task này để mở như T031 của 028 nếu người dùng tự thực hiện sau.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)** → **Phase 2 (V1–V9)** → mọi user story (Phase 2 chặn).
- **US1 (Phase 3)** chặn US2, US3, US4 (script cần danh mục và khung bộ chuyển đổi).
- **US2 (Phase 4)** chặn US3 (dạng b dùng chung bộ chuyển đổi và `-Restore`) và US4 (`-Hint` cần lần chạy có sealed).
- **US5 (Phase 7)** độc lập về kỹ thuật với US1–US4 (chỉ tài liệu); nên làm sau US2 để nhóm 8 (I) đã có bằng chứng thay cho kill-pod.
- **US6 (Phase 8)**: T048–T049 phụ thuộc US2; tài liệu (T050–T058) phụ thuộc kết quả US1–US5.
- **Phase 9** sau cùng.

### Within Each User Story

- Danh mục (T014) → nạp/kiểm (T015) → kiểm cờ/áp dụng (T016–T017) → khung bộ chuyển đổi (T018).
- Tham số/trạng thái (T020–T021) → `-Inject` (T022) → injector (T023) → các nhóm bộ chuyển đổi (T024–T027) → `-Restore` (T028) → kiểm chứng (T029).
- Hints: T034 (lệnh) → T035 (văn bản) → T036 (người dùng duyệt) → T037 (kiểm).

### Parallel Opportunities

- Phase 3: T014 (danh mục) song song T016 (khác chỗ trong script) chỉ khi hai người làm hai file khác nhau — thực tế cùng `incident-drill.ps1` nên làm tuần tự.
- Phase 7: T039, T040, T041, T042, T044, T045 sửa các file khác nhau → song song được sau T038.
- Phase 8: T050, T052, T054, T055, T056 (tài liệu/sơ đồ khác file) song song được; T051, T053, T058 sửa file dùng chung nên tuần tự.

## Parallel Example: Phase 7

```text
Sau T038 (lập danh sách vị trí):
  T039 specs/025/spec.md            ┐
  T040 specs/025/plan|research|data-model ├─ chạy song song
  T041 specs/025/tasks|contracts|checklist ┤
  T042 specs/025/quickstart.md      ┤
  T044 docs/PO|QA|architecture|development/025 ┤
  T045 spec-summary-vi + drawio 025 ┘
Rồi tuần tự: T043 (xoá bản ghi, README, mẫu) → T046 (QA_Debt/technical-debt) → T047 (kiểm chứng)
```

## Implementation Strategy

### MVP First (User Story 1 + 2)

1. Phase 1 → Phase 2 (kiểm chứng V1–V9; dừng hỏi nếu V3/V4/V9 sai dự kiến).
2. Phase 3 (US1): danh mục + khung.
3. Phase 4 (US2): tiêm có chủ đích 9 loại và `-Restore`. **DỪNG VÀ KIỂM**: MVP dùng được để luyện từng nhóm.

### Incremental Delivery

1. US3 (bốc mù 8 nhóm) → kiểm hồi quy 028.
2. US4 (gợi ý) → người dùng duyệt văn bản.
3. US5 (gỡ k8s 025) → kiểm `rg`/`git diff`.
4. US6 (Postman + tài liệu) → kiểm cuối Phase 9.

## Notes

- Mọi giá trị "ví dụ" đã được người dùng chốt: độ trễ 2000 ms, tỷ lệ 5–50%, `--cpus 0.1`, `--memory 256m` (96m bị OOM-kill ở V4), chờ healthy 10 phút. `--memory 256m` còn phải đo lại dưới tải nền ở T029.
- Không sửa `docker-compose*.yml`/`.env.example` để tiêm lỗi; mọi thay đổi `.env` chỉ cục bộ và hoàn tác ở T060.
- Không commit; không xoá Elastic khi chưa có xác nhận rõ (T061).
- Đề xuất đã duyệt ở phiên `/speckit-tasks` (2026-10-05): quy tắc bốc thăm mù, route nhóm D qua gateway có token, phong cách hints (duyệt văn bản ở T036).
