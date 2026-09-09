---

description: "Task list template for feature implementation"
---

# Tasks: Khai báo và đo lường liên tục SLO theo từng service trong service manifest

**Input**: Design documents from `specs/021-declare-service-slos/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Constitution Principle III (Test-First, NON-NEGOTIABLE) áp dụng cho tính năng này — các
task test dưới đây là BẮT BUỘC, không tùy chọn, và PHẢI được viết trước, xác nhận kết quả thật, rồi
mới triển khai/sửa (hoặc kết luận không cần sửa). Kết quả thật khi thực thi khác với dự tính ban đầu
ở cả hai story — US1 (kỳ vọng PASS ngay) thực ra FAIL một lần vì phát hiện `identity` thiếu
`service.classification`; US2 (kỳ vọng FAIL vì `bff`) thực ra PASS ngay 7/7 vì giả định về ngoại lệ
của `bff` là sai. Đây chính là giá trị của Test-First: viết test trước để chạy thật quyết định, không
phải để xác nhận một kết luận đã có sẵn — chi tiết ở ghi chú "kết quả thực tế" tại T007 và T008.

**Organization**: Task được nhóm theo user story trong spec.md để mỗi story có thể triển khai và
kiểm thử độc lập.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Có thể chạy song song (khác file, không phụ thuộc task chưa hoàn thành)
- **[Story]**: User story mà task thuộc về (US1, US2, US3)
- Mỗi task đều có đường dẫn file cụ thể

## Path Conventions

Bổ sung vào monorepo hiện có (xem plan.md § Project Structure) — không có thư mục `src/`/`frontend/`
mới, không có service runtime mới:

- Test quy ước mới: `tests/ServiceManifestSloConventionTests/`
- File manifest cần sửa: `services/bff/src/Bff.Api/service-manifest.yaml`
- Đăng ký solution: `Ecommerce.slnx`
- Tài liệu dashboard đã có, chỉ tham chiếu: `docs/kibana-quan-sat-he-thong/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Khởi tạo dự án test quy ước mới, đăng ký vào solution

- [X] T001 [P] Tạo `tests/ServiceManifestSloConventionTests/ServiceManifestSloConventionTests.csproj`
      (`net10.0`, `Nullable` enable, `ImplicitUsings` enable, package `xunit`, `xunit.runner.visualstudio`,
      `coverlet.collector`, `YamlDotNet` — theo đúng khuôn mẫu
      `tests/DeploymentManifestConventionTests/DeploymentManifestConventionTests.csproj`; không tham
      chiếu project service nào, chỉ đọc file trên đĩa)
- [X] T002 [P] Đăng ký `tests/ServiceManifestSloConventionTests/ServiceManifestSloConventionTests.csproj`
      vào `Ecommerce.slnx`, trong `<Folder Name="/tests/">`, xếp theo thứ tự chữ cái giữa
      `DeploymentManifestConventionTests` và `StructureConventionTests`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Hạ tầng đọc/parse manifest dùng chung cho cả User Story 1 và User Story 2

**⚠️ CRITICAL**: US1 và US2 đều cần hạ tầng này trước khi viết test riêng của từng story

- [X] T003 Tạo `tests/ServiceManifestSloConventionTests/ServiceManifestModel.cs` — model C# ánh xạ
      đúng hình dạng YAML tại [contracts/service-manifest-slo-shape.md](./contracts/service-manifest-slo-shape.md)
      (`ServiceSection.Name`, `ServiceSection.Classification`, `SlosSection.Availability`,
      `SlosSection.ErrorRate.MaxFiveXxRatio`, `SlosSection.Latency.P95`, `SlosSection.Latency.P99`,
      `SlosSection.Justification` — nullable)
- [X] T004 Tạo `tests/ServiceManifestSloConventionTests/ServiceManifestFixture.cs` — dò tìm toàn bộ
      `services/*/src/*/service-manifest.yaml` từ gốc repository, parse mỗi file bằng `YamlDotNet`
      thành `ServiceManifestModel` (T003), expose danh sách 7 manifest đã parse kèm tên service suy ra
      từ đường dẫn thư mục (phục vụ SC-001: đối chiếu `service.name` với tên thư mục) — phụ thuộc T003
- [X] T005 [P] Tạo `tests/ServiceManifestSloConventionTests/PlatformSloDefaults.cs` — bảng 2 hồ sơ mặc
      định (`client-facing-bff`, `internal-service-api`) đúng số liệu tại
      [data-model.md](./data-model.md) § Hồ sơ mặc định nền tảng, kèm comment trỏ về constitution
      Principle VIII làm nguồn gốc

**Checkpoint**: Hạ tầng parse/đối chiếu manifest sẵn sàng — có thể bắt đầu viết test riêng cho US1 và US2

---

## Phase 3: User Story 1 - Mọi service khai báo đầy đủ ngân sách SLO trong manifest (Priority: P1) 🎯 MVP

**Goal**: Đảm bảo cả 7 `service-manifest.yaml` khai báo đủ 4 chỉ tiêu SLO (độ trễ p95/p99, tỷ lệ lỗi,
độ khả dụng), không rỗng/không placeholder, và không có manifest "mồ côi".

**Independent Test**: `dotnet test tests/ServiceManifestSloConventionTests --filter SloDeclarationTests`
pass cho toàn bộ 7 service — tự đủ, không phụ thuộc US2/US3.

### Tests for User Story 1

- [X] T006 [P] [US1] Viết `tests/ServiceManifestSloConventionTests/SloDeclarationTests.cs` — với mỗi
      trong 7 manifest (T004), assert đủ 4 giá trị SLO hiện diện và không rỗng/không placeholder rõ
      ràng (bất biến 1–2, [contracts/service-manifest-slo-shape.md](./contracts/service-manifest-slo-shape.md)),
      `service.classification` là một trong hai giá trị đã biết (bất biến 3), và `service.name` khớp
      tên thư mục service tương ứng (bất biến 6, SC-001) — phụ thuộc T004, T005

### Implementation for User Story 1

- [X] T007 [US1] Chạy T006 (`dotnet test tests/ServiceManifestSloConventionTests --filter SloDeclarationTests`);
      theo Quyết định 0 của [research.md](./research.md), kết quả kỳ vọng là PASS NGAY LẬP TỨC (cả 7
      manifest đã đúng sẵn) — nếu có manifest nào FAIL, sửa đúng `slos:` block của manifest đó (không
      sửa test) cho tới khi PASS
      (**kết quả thực tế**: FAIL lần đầu — `services/identity/src/Identity.Api/service-manifest.yaml`
      thiếu hẳn `service.classification`, một trường mà data-model.md xếp là tiền đề để đối chiếu SLO
      với mặc định, nên test bắt đúng một lỗ hổng thật ngoài dự kiến của Quyết định 0. Đã sửa: thêm
      `classification: internal-service-api` [khớp đúng 4 giá trị SLO hiện có của `identity`]. Chạy
      lại: 22/22 PASS)

**Checkpoint**: User Story 1 hoàn thiện, kiểm thử độc lập được — mọi manifest được bảo vệ khỏi thiếu
khai báo SLO trong tương lai

---

## Phase 4: User Story 2 - Ngân sách SLO khai báo khớp chuẩn nền tảng, hoặc có lý do ngoại lệ được ghi rõ (Priority: P1)

**Goal**: Giá trị SLO của mỗi service khớp đúng hồ sơ mặc định theo phân loại, trừ khi có
`slos.justification` ghi rõ lý do.

**Independent Test**: `dotnet test tests/ServiceManifestSloConventionTests --filter SloDefaultComplianceTests`
pass cho cả 7 service; và [quickstart.md](./quickstart.md) Bước 2 (tạm sửa sai một giá trị của
`orders`, xác nhận test bắt lỗi, rồi hoàn tác) — tự đủ, chỉ dùng lại hạ tầng T003–T005 từ Foundational,
không phụ thuộc chi tiết implementation của US1.

### Tests for User Story 2

- [X] T008 [P] [US2] Viết `tests/ServiceManifestSloConventionTests/SloDefaultComplianceTests.cs` — với
      mỗi service không có `slos.justification`, assert cả 4 giá trị khớp đúng hồ sơ mặc định
      (`PlatformSloDefaults`, T005) theo `service.classification` (bất biến 4); nếu có
      `slos.justification`, assert nó hiện diện và không rỗng khi có giá trị khác mặc định (bất biến
      5) — phụ thuộc T004, T005.
      (**kết quả thực tế**: kỳ vọng ban đầu "FAIL cho `bff`" trong bản kế hoạch là SAI — chạy thật cho
      kết quả **7/7 PASS ngay lần đầu**. Lý do: `bff` mang `classification: client-facing-bff` và
      khớp đúng hồ sơ mặc định của CHÍNH phân loại đó [p95 300ms/p99 800ms] — đây không phải một ngoại
      lệ, chỉ là mặc định đúng của đúng phân loại [constitution Principle VIII định nghĩa 2 hồ sơ mặc
      định song song, không cái nào "nới lỏng" của cái kia]. Đã sửa lại research.md Quyết định 0/2 và
      contracts/service-manifest-slo-shape.md cho đúng)

### Implementation for User Story 2

> Không cần sửa manifest nào — T008 xác nhận cả 7/7 service đã khớp đúng mặc định của chính phân loại
> của mình, không có ngoại lệ thật nào cần `slos.justification` ở hiện tại. Trường `slos.justification`
> trong schema (ServiceManifestModel.cs, T003) và logic assert nó (T008) vẫn được giữ lại làm cơ chế
> phòng ngừa cho một ngoại lệ thật trong tương lai (FR-003) — KHÔNG thêm dữ liệu giả vào bất kỳ manifest
> nào chỉ để có một ví dụ.

- [X] T009 [US2] ~~Sửa `services/bff/src/Bff.Api/service-manifest.yaml`~~ — **bỏ qua có chủ đích**:
      tiền đề của task này (`bff` có một ngoại lệ SLO cần chuyển thành `slos.justification`) sai (xem
      ghi chú ở T008); sửa manifest theo hướng đó sẽ là đưa một lý do bịa đặt vào một service vốn
      không hề lệch mặc định — vi phạm chính tinh thần của tính năng ("ngân sách là cam kết thật")
- [X] T010 [US2] Chạy lại T008, xác nhận PASS cho cả 7 service (7/7 khớp mặc định của chính phân loại
      của mình — đã chạy trong T008, không cần chạy thêm lần nào khác vì không có gì thay đổi sau T009)

**Checkpoint**: User Story 1 VÀ User Story 2 đều hoạt động độc lập — khai báo SLO được bảo vệ cả về
tính đầy đủ lẫn tính đúng đắn so với chuẩn nền tảng

---

## Phase 5: User Story 3 - Ngân sách SLO đã khai báo được đo lường liên tục từ dữ liệu vận hành thật (Priority: P2)

**Goal**: Chính thức hoá dashboard Kibana đã dựng làm cơ chế đo lường liên tục của tính năng, xác
nhận lại đúng 3 kịch bản kiểm thử của Jira SCRUM-29 trên dữ liệu thật.

**Independent Test**: [quickstart.md](./quickstart.md) Bước 3–5, chạy trên Elastic stack đang có dữ
liệu OTel thật — tự đủ, không cần US1/US2 đã hoàn thành (chỉ cần manifest có giá trị để đối chiếu,
vốn đã đúng sẵn theo Quyết định 0 của research.md).

### Implementation for User Story 3

> Không có task viết code mới — dashboard và pipeline telemetry đã tồn tại (research.md Quyết định 3).
> Các task dưới đây là xác thực trên dữ liệu thật, không chặn PR.

- [X] T011 [US3] Xác nhận dashboard `SLO vận hành hằng ngày — 7 service` đã được import vào Kibana từ
      `docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson`; nếu chưa, import theo
      [quickstart.md](./quickstart.md) § Điều kiện tiên quyết
      (**kết quả thực tế**: đã có sẵn — `GET /api/saved_objects/dashboard/e2e06ff5-9cdf-4bea-acc8-5fd60ce26170`
      trên Kibana đang chạy tại `localhost:5601` trả về đúng dashboard "SLO vận hành hằng ngày — 7
      service", không cần import lại)
- [X] T012 [US3] Thực hiện [quickstart.md](./quickstart.md) Bước 3 — đối chiếu giá trị "Thực tế" trên
      dashboard với giá trị khai báo trong `services/orders/src/Orders.Api/service-manifest.yaml`,
      xác nhận bất biến 1–2 tại [contracts/continuous-measurement-contract.md](./contracts/continuous-measurement-contract.md) —
      phụ thuộc T011
      (**kết quả thực tế**: đối chiếu trực tiếp bằng truy vấn Elasticsearch thô [`traces-generic.otel-default`,
      `now-24h`] cho `Orders.Api` — tổng 9237 request, 1 lỗi 5xx → error-rate thật = 0.0108% [ngưỡng
      0.1%, đạt]; p95 thật = 33.6ms [ngưỡng 150ms, đạt]; p99 thật = 393.4ms [ngưỡng 500ms, đạt, gần
      ngưỡng]. Khớp đúng phương pháp đối chiếu đã dùng khi build dashboard gốc)
- [X] T013 [US3] Thực hiện [quickstart.md](./quickstart.md) Bước 4 — làm chậm có chủ đích một endpoint,
      xác nhận dashboard thể hiện rõ ngân sách bị tiêu hao (bất biến 4), rồi hoàn tác thay đổi làm
      chậm — phụ thuộc T011
      (**kết quả thực tế**: không sửa code/rebuild container [tránh ảnh hưởng phiên khác đang dùng
      chung hạ tầng Docker] — thay vào đó gửi 300 request đồng thời tới `orders`'s `/health/ready`
      [route đã có sẵn trong manifest với ngân sách riêng p95 150ms/p99 500ms] để tạo tranh chấp tài
      nguyên thật. Baseline 15 phút trước khi tạo tải: p95=157.9ms/p99=440.1ms [môi trường demo vốn đã
      hơi tải]. Trong 5 phút NGAY SAU khi tạo tải [323 request, tăng mạnh so với ~136/15 phút bình
      thường]: p95=**14,975.7ms**/p99=**18,519.2ms** — tăng khoảng 95 lần, vượt xa ngân sách 150ms/500ms.
      Xác nhận rõ ràng bất biến 4: giá trị đo được phản ánh đúng suy giảm thật trong cùng cửa sổ quan
      sát, không cần thao tác thủ công thu thập lại số liệu. Không cần "hoàn tác" vì không có thay đổi
      cấu hình/code nào được thực hiện — tải chỉ là traffic tạm thời, tự hết ngay sau khi burst kết thúc)
- [X] T014 [P] [US3] Thực hiện [quickstart.md](./quickstart.md) Bước 5 — chọn khoảng thời gian không
      có traffic, xác nhận dashboard hiển thị "không có dữ liệu" thay vì "0% lỗi" (bất biến 3) — phụ
      thuộc T011
      (**kết quả thực tế**: truy vấn Elasticsearch thô cho `Orders.Api` trong khoảng 2020-09-07 đến
      2020-09-08 [chắc chắn trước khi môi trường demo tồn tại] → `hits.total.value = 0`, bucket lỗi
      cũng `doc_count = 0` — ở tầng dữ liệu thô, "không có tài liệu nào khớp" [0 total] khác hẳn "có
      traffic nhưng 0 lỗi" [total=9237, err=0 nếu không có lỗi thật]. Đây chính xác là cơ chế mà
      `docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md` đã xác minh cho Lens Table:
      0 tài liệu → dòng biến mất khỏi bảng [terms aggregation], không hiển thị `0%` gây hiểu lầm)

**Checkpoint**: Cả 3 user story đều hoạt động độc lập — SLO vừa được khai báo đúng, vừa được bảo vệ
khỏi trôi dạt, vừa được đo lường liên tục từ dữ liệu thật

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Hoàn thiện liên kết tài liệu và xác nhận toàn bộ tính năng liền mạch

- [X] T015 [P] Thêm một dòng tham chiếu ngược từ
      `docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md` tới
      `specs/021-declare-service-slos/` (hợp đồng đo lường liên tục chính thức), để người đọc tài liệu
      vận hành biết dashboard này giờ được một đặc tả chính thức tham chiếu
- [X] T016 Chạy `dotnet test tests/ServiceManifestSloConventionTests` một lượt cuối, xác nhận toàn bộ
      test (T006, T008) PASS — phụ thuộc T007, T010
      (**kết quả thực tế**: 29/29 PASS. `Ecommerce.slnx` xác nhận vẫn là XML hợp lệ sau khi đăng ký
      dự án mới)
- [X] T017 Thực hiện toàn bộ [quickstart.md](./quickstart.md) từ Bước 1 đến Bước 5 một lượt liền mạch,
      ghi lại kết quả thực tế (số liệu đối chiếu, ảnh chụp hoặc log nếu cần) — phụ thuộc T007, T010,
      T012, T013, T014
      (**kết quả thực tế**: đã thực hiện thật trên môi trường demo Docker đang chạy [7 service + Elastic
      stack, `docker ps`] và Elasticsearch/Kibana thật tại `localhost:9200`/`localhost:5601` — không mô
      phỏng. Toàn bộ số liệu ghi ở T007, T008, T011–T014. Không dùng `kind`/cluster K8s [tính năng này
      không cần, khác 019] — chỉ cần Docker Compose stack đã có sẵn của repo)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Không phụ thuộc gì — bắt đầu ngay
- **Foundational (Phase 2)**: Phụ thuộc Setup hoàn tất — CHẶN US1 và US2 (không chặn US3, vì US3
  không dùng hạ tầng parse manifest)
- **User Story 1 (Phase 3)**: Phụ thuộc Foundational hoàn tất
- **User Story 2 (Phase 4)**: Phụ thuộc Foundational hoàn tất — độc lập với US1 (dùng chung hạ tầng
  T003–T005, không phụ thuộc task cụ thể nào của US1)
- **User Story 3 (Phase 5)**: Chỉ phụ thuộc Setup hoàn tất (không cần Foundational/US1/US2) — có thể
  chạy song song với Phase 2–4 nếu có nhân lực riêng
- **Polish (Phase 6)**: Phụ thuộc cả 3 user story hoàn tất

### User Story Dependencies

- **User Story 1 (P1)**: Không phụ thuộc story khác
- **User Story 2 (P1)**: Không phụ thuộc US1 về mặt implementation (chỉ dùng chung hạ tầng
  Foundational); có thể triển khai song song với US1
- **User Story 3 (P2)**: Không phụ thuộc US1/US2 về mặt implementation (không có code mới); độc lập
  hoàn toàn về mặt kỹ thuật, nhưng về mặt giá trị nghiệp vụ chỉ thực sự có ý nghĩa sau khi US1/US2 đảm
  bảo dữ liệu khai báo đáng tin cậy để đối chiếu

### Within Each User Story

- Test viết trước, xác nhận đúng kết quả kỳ vọng (FAIL với US2, PASS ngay với US1) trước khi
  sửa/triển khai
- Model trước Fixture (T003 trước T004)
- Story hoàn tất trước khi coi là "xong" để chuyển sang Polish

### Parallel Opportunities

- T001, T002 (Setup) chạy song song
- T005 (Foundational) chạy song song với T003→T004 (khác file, không phụ thuộc nhau)
- Sau khi Foundational (Phase 2) xong: US1 (Phase 3) và US2 (Phase 4) triển khai song song được (khác
  file test, dùng chung nhưng không sửa đổi hạ tầng Foundational)
- US3 (Phase 5) chạy song song với Phase 2–4 ngay từ đầu nếu có nhân lực riêng (không phụ thuộc
  Foundational)
- T014 (US3, Bước 5 quickstart) chạy song song với T012/T013 (khác khoảng thời gian quan sát trên
  cùng dashboard, không xung đột)

---

## Parallel Example: User Story 1 & User Story 2 (sau khi Foundational hoàn tất)

```bash
# US1 và US2 có thể triển khai song song bởi 2 người/2 phiên khác nhau:
Task: "Viết SloDeclarationTests.cs cho US1 (T006)"
Task: "Viết SloDefaultComplianceTests.cs cho US2 (T008)"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Hoàn tất Phase 1: Setup
2. Hoàn tất Phase 2: Foundational (CHẶN US1 và US2)
3. Hoàn tất Phase 3: User Story 1
4. **DỪNG VÀ XÁC THỰC**: `dotnet test tests/ServiceManifestSloConventionTests --filter SloDeclarationTests`
   pass độc lập
5. Đây đã là một cải thiện triển khai được ngay: mọi manifest được bảo vệ khỏi thiếu khai báo SLO,
   dù chưa bảo vệ tính đúng đắn so với mặc định (US2) hay đo lường liên tục (US3)

### Incremental Delivery

1. Setup + Foundational → hạ tầng parse manifest sẵn sàng
2. Thêm US1 → kiểm thử độc lập → có thể merge/deploy ngay (MVP)
3. Thêm US2 → kiểm thử độc lập → merge/deploy (khai báo giờ được bảo vệ cả tính đúng đắn)
4. Thêm US3 → kiểm thử độc lập trên dữ liệu thật → merge/deploy (SLO giờ là tín hiệu vận hành sống,
   không chỉ tài liệu)
5. Mỗi story thêm giá trị mà không phá story trước

### Parallel Team Strategy

Với nhiều người triển khai cùng lúc:

1. Cùng hoàn tất Setup + Foundational
2. Sau đó:
   - Người A: User Story 1 (Phase 3)
   - Người B: User Story 2 (Phase 4)
   - Người C: User Story 3 (Phase 5) — có thể bắt đầu ngay từ đầu, không cần chờ Foundational
3. Các story hoàn tất và tích hợp độc lập

---

## Notes

- [P] = khác file, không phụ thuộc task chưa hoàn thành
- Nhãn [Story] gắn task với đúng user story để truy vết
- Xác nhận kết quả test đúng kỳ vọng (FAIL/PASS) trước khi coi task implementation là "xong"
- Commit sau mỗi task hoặc mỗi nhóm task logic
- Có thể dừng ở bất kỳ Checkpoint nào để xác thực độc lập một story
- Tránh: task mơ hồ, hai task cùng sửa một file cùng lúc, phụ thuộc chéo giữa các story phá vỡ tính
  độc lập
