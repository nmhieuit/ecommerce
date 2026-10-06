---

description: "Danh sách task triển khai tài liệu luyện troubleshoot theo nhóm lỗi (Spec D của đợt rà soát nợ kỹ thuật)"
---

# Tasks: Tài liệu luyện troubleshoot theo nhóm lỗi (hướng dẫn step by step cho lỗi có kịch bản, gợi ý theo triệu chứng cho lỗi bất ngờ)

**Input**: Tài liệu thiết kế tại `specs/032-troubleshoot-practice-docs/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: CÓ, nhưng chỉ cho tài liệu: project `tests/TroubleshootGuideConventionTests` (Nguyên tắc III: viết
trước, thấy đỏ, rồi mới viết tài liệu cho xanh). Script và `catalog.json` của 031 KHÔNG có test và KHÔNG sửa ở
spec này (sai lệch Nguyên tắc III của 031 còn mở — plan.md Complexity Tracking). Số đo trong tài liệu chỉ ghi khi
đã chạy thật; chỉ đánh `[X]` khi có bằng chứng (đầu ra lệnh, số đo, kết quả test) ghi vào ghi chú task hoặc
vào research.md "Kết quả xác minh".

**Ràng buộc chung**:
- KHÔNG sửa file nào dưới `scripts/`, `services/`, `shared/`, `docker/`, `deploy/`; KHÔNG sửa `docker-compose*.yml`,
  `.env.example`, dashboard/rule `.ndjson`, `scripts/incident-drill/catalog.json` (FR-011, SC-006).
- Một lần chỉ một lần chạy diễn tập mở (script của 031 từ chối `-Inject`/`-Start` mới khi còn lần chưa
  `-Restore`) nên các task chạy thật phải làm TUẦN TỰ, không đánh `[P]`.
- `CHAOS_ALLOW_FAULT_INJECTION` và `CHAOS_ALLOW_LATENCY_INJECTION` về `false` sau mỗi buổi chạy.
- KHÔNG commit (người dùng tự commit). KHÔNG bật stack khi chưa được người dùng đồng ý (T001). KHÔNG xoá Elastic
  khi chưa hỏi lại (T051).
- Máy này KHÔNG có Python: parse JSON/XML bằng `node -e` hoặc PowerShell (`[xml]`, `ConvertFrom-Json`); ghi file Python
  bằng công cụ Write nếu cần, không dùng heredoc nhiều dấu nháy.
- Tài liệu tiếng Việt có dấu, văn phong và cấu trúc "Bạn sẽ thấy" của file 01–08; tên field/route/truy vấn đã chạy thật.
- Mọi mục có chữ **DỪNG VÀ HỎI** nghĩa là: không tự đổi phương án, báo kết quả và hỏi người dùng.
- Không dùng `Get-Content`/`Set-Content` của Windows PowerShell 5.1 để sửa file tiếng Việt (đọc theo ANSI làm hỏng dấu); dùng công cụ Edit/Write hoặc `sed`/`node` (UTF-8).

**Organization**: Nhóm theo user story của spec.md (US1–US5); US3 (bài tập) làm sau US1 vì dùng chung file.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 **HỎI NGƯỜI DÙNG trước khi làm**: xin phép bật stack `docker-compose.local.yml` và bật `CHAOS_ALLOW_FAULT_INJECTION=true` (cùng `CHAOS_ALLOW_LATENCY_INJECTION=true` cho nhóm 3) trong `.env` cục bộ để chạy thật 8 nhóm; nhắc rằng lỗi chạy đo tính vào ngân sách lỗi tuần như thật và Elastic sẽ cần dọn sau (T051). Chỉ khi có xác nhận rõ mới làm T009 trở đi. *(Người dùng đồng ý "chạy trên stack hiện tại"; `.env` chép từ checkout chính sang worktree, thêm hai cờ — gitignore.)*
- [X] T002 Kiểm tra điều kiện tiên quyết trên đĩa (không cần stack): `scripts/incident-drill/catalog.json` có 8 `groups` và 9 `types` A–I; `scripts/incident-drill.ps1` có `-Inject`, `-Restore`, `-Hint`, `-Start`, `-Reveal`, `-Load`; folder Postman 26/28/31 có mặt; ghi kết quả vào research.md mục "Kết quả xác minh". Nếu lệch so với giả định của spec thì **DỪNG VÀ HỎI**.
- [X] T003 Đọc `docs/QA/QA_Debt.md` và `docs/architecture/technical-debt.md` (mục 025, 027, 028, 031) và lập bảng nguồn triệu chứng thật cho file 17 trong research.md "Kết quả xác minh": lỗi lan gateway/BFF khi orders hỏng; token hỏng sau khi tạo lại identity (401 hoặc 502/504 qua BFF); nhiễu khởi động nguội 5–7 phút; môi trường chậm từng đợt; circuit breaker không trip (025) — mỗi dòng ghi số mục/dòng nguồn. KHÔNG thêm triệu chứng không có nguồn.

---

## Phase 2: Foundational — test quy ước tài liệu (đỏ trước)

**Mục tiêu**: có project test xanh/đỏ đúng theo [guide-convention-test-contract.md](./contracts/guide-convention-test-contract.md) TRƯỚC khi có tài liệu. **Chặn mọi user story.**

- [X] T004 Tạo `tests/TroubleshootGuideConventionTests/TroubleshootGuideConventionTests.csproj` theo khuôn `tests/ServiceManifestSloConventionTests/ServiceManifestSloConventionTests.csproj` (net10.0, xUnit, KHÔNG tham chiếu service, KHÔNG thêm package mới) và thêm `<Project Path="tests/TroubleshootGuideConventionTests/TroubleshootGuideConventionTests.csproj" />` vào `Ecommerce.slnx` (đúng thứ tự cạnh `ServiceManifestSloConventionTests`).
- [X] T005 [P] Tạo `tests/TroubleshootGuideConventionTests/GuideFixture.cs`: tìm gốc repo bằng marker `Ecommerce.slnx` đi lên từ `AppContext.BaseDirectory` (không dùng `.git` vì worktree), đọc `scripts/incident-drill/catalog.json` bằng `System.Text.Json` (`groups[].id/name`, `types[].code/groupId`), liệt kê file `docs/kibana-quan-sat-he-thong/NN-*.md`. Comment tiếng Việt theo nếp QA 008+ (Kiểm tra / Lý do phải test / Task nguồn).
- [X] T006 [P] Tạo `tests/TroubleshootGuideConventionTests/GroupCoverageTests.cs` cho bất biến 1–6 của hợp đồng: mỗi nhóm có đúng một file `<08+id>-*.md` với tiêu đề `# <08+id> —`; mỗi loại có mục `## Loại <code>` trong file nhóm chứa nó; file nhóm đủ 6 mục bắt buộc; file 17 có `**Mức 3 — Đáp án (Loại <code>)**` cho mỗi loại A–I; mỗi mục triệu chứng có mức 1→2→3 theo thứ tự; khối mức 1–2 không chứa `Loại <chữ cái>` hay tên 7 service. Mỗi `[Fact]`/`[Theory]` có comment tiếng Việt: kiểm gì, vì sao, task nguồn.
- [X] T007 [P] Tạo `tests/TroubleshootGuideConventionTests/InternalLinkTests.cs` cho bất biến 7–8: mọi link nội bộ `[text](path)` (bỏ `http(s)://`, `mailto:`; giải mã `%20`/tiếng Việt) trong file 09–17 và `00-tong-quan-lo-trinh.md` trỏ tới file/thư mục có thật, neo `#...` trỏ tới tiêu đề có thật; `00-tong-quan-lo-trinh.md` liệt kê link tới đủ file 09–17.
- [X] T008 Chạy `dotnet test tests/TroubleshootGuideConventionTests` và xác nhận ĐỎ đúng lý do (thiếu file 09–17, chưa có mục trong 00) — KHÔNG phải đỏ do lỗi biên dịch hay không tìm thấy catalog. Ghi số test đỏ vào research.md "Kết quả xác minh". Sửa test nếu đỏ sai lý do. Phụ thuộc T004–T007.

**Checkpoint**: test đỏ đúng lý do; có thể bắt đầu viết tài liệu.

---

## Phase 3: User Story 1 - Hướng dẫn step by step cho từng nhóm lỗi có kịch bản (Priority: P1) 🎯 MVP

**Goal**: 8 file 09–16 theo [guide-structure-contract.md](./contracts/guide-structure-contract.md), mỗi file đã chạy thật.

**Independent Test**: Làm theo một file bất kỳ từ `-Inject` tới xác nhận khỏi; số đo khớp số ghi trong file; phần test của file đó xanh.

**Quy trình chung cho T009–T016** (mỗi nhóm, tuần tự): (1) `-Load` nếu cần token/tải nền; (2) lấy số nền trước khi tiêm; (3) `-Inject -Type <loại> -Target <đích>` rồi đo bằng đúng các truy vấn sẽ ghi trong file (dashboard Xử lý sự cố, Discover, `_search`/`_count`/ES|QL trên `traces-generic.otel-default*`, `logs-generic.otel-default*`, `metrics-generic.otel-default*`); (4) `-Restore -RunId`, chờ container đích healthy, chạy truy vấn xác nhận 15 phút của file 08; (5) viết file theo khung hợp đồng, ghi ngày + điều kiện + kết quả đo; nêu rõ giới hạn đã biết và nhiễu khởi động nguội 5–7 phút. Mọi phát hiện ghi nháp vào research.md "Kết quả xác minh" để chuyển sang QA_Debt ở T040. Nếu loại không có triệu chứng như tài liệu 031 ghi (B), hoặc số đo lệch lớn so với QA_Debt/technical-debt 031 thì **DỪNG VÀ HỎI**, không tự đổi phương án.

### Implementation for User Story 1

- [X] T009 [US1] Nhóm 1 (loại A, đích kết nối sai): chạy thật theo quy trình chung với một service đích, rồi viết `docs/kibana-quan-sat-he-thong/09-dich-ket-noi-sai.md` (mục: Bạn sẽ thấy, Điều kiện, `## Loại A — Đích kết nối sai`, Giới hạn đã biết, Xem thêm; bài tập để T018). Phụ thuộc T001, T008.
- [X] T010 [US1] Nhóm 2 (loại B cạn pool gateway và loại C 5xx theo tỷ lệ): chạy thật cả hai; với B nêu trung thực (đo lại) rằng không có triệu chứng đo được ở cấu hình hiện tại nếu đúng như QA_Debt 031; với C dùng đích và tỷ lệ 5–50% thật; viết `docs/kibana-quan-sat-he-thong/10-nghen-va-loi-theo-ty-le.md` có hai mục `## Loại B — …` và `## Loại C — …`. Phụ thuộc T009.
- [X] T011 [US1] Nhóm 3 (loại D, trễ chỉ Orders.Api): cần `CHAOS_ALLOW_LATENCY_INJECTION=true` và `-Load`; ghi đúng route đã đo (gửi thẳng `GET :5041/orders/{guid}` kèm token, `X-Tenant-Id: contoso`, `X-Chaos-Latency-Ms`), nêu rõ header không đi xuyên gateway/BFF; viết `docs/kibana-quan-sat-he-thong/11-do-tre-orders.md`. Phụ thuộc T010.
- [X] T012 [US1] Nhóm 4 (loại E, hạ tầng dừng — chỉ 5 DB): chạy thật với một DB đích, quan sát triệu chứng ở service phụ thuộc và container; nêu Redis/RabbitMQ ngoài phạm vi và lý do; viết `docs/kibana-quan-sat-he-thong/12-ha-tang-dung.md`. Phụ thuộc T011.
- [X] T013 [US1] Nhóm 5 (loại F, xác thực hỏng — chỉ `Identity__Authority` sai cho một trong 6 service): chạy thật, ghi triệu chứng 401/502/504 thật đã đo và phân biệt với trường hợp token hỏng sau khi tạo lại identity (QA_Debt); viết `docs/kibana-quan-sat-he-thong/13-xac-thuc-hong.md`. Phụ thuộc T012.
- [X] T014 [US1] Nhóm 6 (loại G, thiếu tài nguyên — `--cpus` 0.1, `--memory` 256m): chạy thật dưới `-Load`, đo p95/p99 so với nền; nêu giới hạn triệu chứng nhẹ (chưa vượt ngưỡng p95 150 ms nếu đúng như đo); viết `docs/kibana-quan-sat-he-thong/14-thieu-tai-nguyen.md`. Nếu bị OOM-kill thì ghi thành triệu chứng của nhóm 8 và **DỪNG VÀ HỎI**. Phụ thuộc T013.
- [X] T015 [US1] Nhóm 7 (loại H, mạng đứt — tách một service khỏi network `backbone`): chạy thật, quan sát phía gọi và phía bị tách; khôi phục bằng nối lại mạng và chờ healthy; viết `docs/kibana-quan-sat-he-thong/15-mang-dut.md`. Phụ thuộc T014.
- [X] T016 [US1] Nhóm 8 (loại I, container chết — `docker kill` một trong 7 service app): chạy thật, quan sát `docker ps`/health và telemetry mất traffic; nêu rõ 7 service app không có `restart:` nên không tự sống lại; viết `docs/kibana-quan-sat-he-thong/16-container-chet-hoac-khoi-dong-lai.md`. Phụ thuộc T015.
- [X] T017 [US1] Kiểm tra chéo 8 file: mọi truy vấn trong tài liệu chạy lại được và cho kết quả như đã ghi (lấy mẫu chạy lại ít nhất 1 truy vấn mỗi file); mọi file link (không chép) quy trình triage 028, `mau-ban-ghi-su-co.md` và truy vấn 15 phút của file 08; không chứa mật khẩu/token thật. Ghi kết quả vào research.md. Phụ thuộc T009–T016.

**Checkpoint**: 8 file nhóm có số đo thật; phần test mục 1–3 (trừ bài tập/đã đạt) xanh dần.

---

## Phase 4: User Story 3 - Bài tập tự làm và tiêu chí "đã đạt" (Priority: P2)

**Goal**: Mỗi file 09–16 có `## Bài tập tự làm` và `## Đã đạt khi`. (Làm sau US1 vì cùng file; làm tuần tự vì cần stack.)

**Independent Test**: Làm bài tập của một file bất kỳ trên stack, đối chiếu từng mục checklist; test bất biến 3 xanh.

- [X] T018 [US3] Viết và chạy thử bài tập + checklist cho file 09 (nhóm 1): bài tập bốc một service khác với lần đo ở T009; checklist `- [ ]` đo được (đúng nhóm, đúng đích, khôi phục, SLO sạch 15 phút theo file 08).
- [X] T019 [US3] Như T018 cho file 10 (nhóm 2, bài tập cho cả B và C hoặc nêu rõ B không có triệu chứng).
- [X] T020 [US3] Như T018 cho file 11 (nhóm 3).
- [X] T021 [US3] Như T018 cho file 12 (nhóm 4, bài tập chọn DB khác).
- [X] T022 [US3] Như T018 cho file 13 (nhóm 5, bài tập chọn service khác).
- [X] T023 [US3] Như T018 cho file 14 (nhóm 6, bài tập chọn service khác).
- [X] T024 [US3] Như T018 cho file 15 (nhóm 7).
- [X] T025 [US3] Như T018 cho file 16 (nhóm 8). Sau cùng chạy `dotnet test tests/TroubleshootGuideConventionTests --filter GroupCoverage` và xác nhận bất biến 1–3 xanh cho cả 8 file. Phụ thuộc T018–T024.

**Checkpoint**: bài tập chạy được; mỗi nhóm tự đánh giá được.

---

## Phase 5: User Story 2 - Gợi ý theo triệu chứng cho lỗi bất ngờ (Priority: P1)

**Goal**: File 17 theo [guide-structure-contract.md](./contracts/guide-structure-contract.md): triệu chứng → 3 mức mở dần, kèm bẫy/nhiễu thật.

**Independent Test**: Chạy một lần `-Start`, tra file 17 theo triệu chứng thấy, mở dần mức 1→2→3, đối chiếu `-Reveal`; test bất biến 4–6 xanh.

- [X] T026 [US2] Lập danh sách triệu chứng cho file 17 từ số đo T009–T016 và bảng nguồn T003 (ví dụ 5xx, trễ, 401/xác thực, mất traffic, container báo lỗi, service không sẵn sàng): mỗi triệu chứng ghi loại có thể gây ra; trình người dùng duyệt danh sách và tên triệu chứng. **DỪNG VÀ HỎI** nếu có triệu chứng mà số đo không xác nhận. Phụ thuộc T017. *(Người dùng duyệt cả 7 triệu chứng.)*
- [X] T027 [US2] Viết `docs/kibana-quan-sat-he-thong/17-goi-y-theo-trieu-chung.md`: cảnh báo đầu file (làm bài mù thì đừng mở file nhóm); mỗi triệu chứng một mục với mức 1 (gợi ý + cách kiểm panel/truy vấn, không tên service/mã loại), mức 2 (tên nhóm), mức 3 (`**Mức 3 — Đáp án (Loại <code>)**` + khôi phục + link file nhóm); mỗi loại A–I có ít nhất một mức 3. Phụ thuộc T026.
- [X] T028 [US2] Viết mục `## Bẫy và nhiễu thường gặp` trong file 17 từ bảng T003: lỗi lan gateway/BFF khi orders hỏng; token hỏng sau khi tạo lại identity (401 hoặc 502/504 qua BFF); nhiễu khởi động nguội 5–7 phút; môi trường chậm từng đợt; circuit breaker không trip (025) — mỗi bẫy ghi cách phân biệt và nguồn QA_Debt/technical-debt. Phụ thuộc T027.
- [X] T029 [US2] Kiểm chứng bằng một lần mù thật: `-Start`, quan sát triệu chứng, tra file 17, mở mức 1→2→3 và so với `-Hint -Level 1|2|3` của script; `-Reveal` đối chiếu; `-Restore`. Ghi sự khác nhau giữa mức tài liệu và mức `-Hint` (tài liệu nặng hơn, đúng quyết định) vào research.md. Lưu ý Claude đã viết tài liệu nên không "mù": chỉ kiểm cấu trúc và không lộ, không kiểm độ khó. Phụ thuộc T028.
- [X] T030 [US2] Chạy `dotnet test tests/TroubleshootGuideConventionTests --filter GroupCoverage` — bất biến 4–6 xanh; rà thủ công mức 1 và 2 của TỪNG triệu chứng không lộ tên service, tham số, mã loại (SC-003) và ghi kết quả. Phụ thuộc T029.

**Checkpoint**: dạng (b) dùng được và không lộ đáp án.

---

## Phase 6: User Story 4 - Chuỗi tài liệu nhất quán và test chống sót (Priority: P2)

**Goal**: 00 và README diễn tập phản ánh chuỗi mới; toàn bộ test xanh và chứng minh bắt được lỗi.

**Independent Test**: `dotnet test tests/TroubleshootGuideConventionTests` xanh; làm hỏng cố ý một link/một file nhóm thì đỏ.

- [X] T031 [US4] Sửa `docs/kibana-quan-sat-he-thong/00-tong-quan-lo-trinh.md`: thêm mục 09–17 vào "Lộ trình đọc" đúng thứ tự (mỗi mục có link, nêu cần file nào trước), sửa "Bộ 4 file" → số đúng, "4 file đều" → số đúng, "Cần trọn vẹn 4 file trước" → đúng thứ tự, "Chuẩn bị chung cho cả 6 file" → số đúng; rà lại toàn file tìm số liệu/ghi chú lỗi thời khác; mô tả ngắn dạng (a)/(b) và trỏ tới quy trình triage 028 (link, không chép).
- [X] T032 [P] [US4] Sửa `docs/dien-tap-chaos-engineering/README.md`: thêm một đoạn trong mục "Danh mục nhóm lỗi (spec 031)" trỏ sang file 09–17 (link tương đối đúng); không đụng quy trình triage và các mục khác.
- [X] T033 [US4] Chạy toàn bộ `dotnet test tests/TroubleshootGuideConventionTests` và xác nhận XANH (bất biến 1–8). Phụ thuộc T025, T030, T031, T032.
- [X] T034 [US4] Kiểm tra bắt lỗi (SC-004): tạm đổi tên một file nhóm, tạm làm hỏng một link và tạm xoá một dòng `**Mức 3 — Đáp án (Loại X)**`, mỗi lần chạy test phải ĐỎ đúng bất biến; hoàn tác từng thay đổi và chạy lại xanh. Ghi kết quả. Phụ thuộc T033.
- [X] T035 [US4] Chạy `dotnet test Ecommerce.slnx --filter "FullyQualifiedName~ConventionTests"` (hoặc lệnh tương đương đã dùng cho các suite quy ước) để xác nhận project mới không làm hỏng suite khác và được solution nhận. Phụ thuộc T033. *(Solution nhận project; TroubleshootGuide 38/38, Container 9, Deployment 58, Structure 9 xanh; `IntegrationTestSupport` abort vì thiếu `BouncyCastle.Cryptography 2.7.0` — không liên quan, ghi ở QA_Debt/technical-debt.)*

**Checkpoint**: chuỗi nhất quán; test chống sót hoạt động.

---

## Phase 7: User Story 5 - Tài liệu đi kèm theo nếp 027/028/031 (Priority: P3)

**Goal**: Folder Postman 32, PO/QA/Architect, 3 drawio, cập nhật nợ.

**Independent Test**: Chạy folder Postman 32 theo hướng dẫn QA thủ công; parse JSON/XML; kiểm link.

- [X] T036 [US5] Thêm folder `32 - Luyện troubleshoot` vào `postman/ecommerce.postman_collection.v2.json` theo khuôn folder 31: 8 subfolder nhóm 1–8, mỗi subfolder có request quan sát triệu chứng và request xác nhận khỏi (tái dùng request nền của folder 26/28/31 bằng cách sao chép sâu, KHÔNG sửa folder cũ); mô tả tiếng Việt giải thích tiêm/khôi phục do người vận hành điều khiển ngoài Postman bằng `incident-drill.ps1`. Chèn văn bản trước `\r\n  ],\r\n  "event"` (file CRLF viết tay), KHÔNG dump lại cả file.
- [X] T037 [US5] Chạy từng subfolder bằng newman (cài trong thư mục scratchpad) theo hướng dẫn thủ công: `-Inject`, request quan sát, `-Restore`, request xác nhận; dùng `pm.environment.set/get` cho biến giữa các lần chạy; ghi kết quả, sửa kỳ vọng nếu sai. Cần stack (T001). Phụ thuộc T036.
- [X] T038 [P] [US5] Tạo `docs/architecture/032_Architect_tài liệu luyện troubleshoot theo nhóm lỗi.md` theo cấu trúc và văn phong `docs/architecture/031_Architect_danh mục 8 nhóm lỗi luyện troubleshoot.md`: cấu trúc chuỗi 09–17, khung file nhóm, test quy ước, quan hệ với 028/031, mục `## Sơ đồ` trỏ tới 3 file T043–T045.
- [X] T039 [P] [US5] Tạo `docs/QA/032_QA_tài liệu luyện troubleshoot theo nhóm lỗi.md` theo nếp QA 008+ và `docs/QA/031_QA_danh mục 8 nhóm lỗi luyện troubleshoot.md`: phần Thủ công đứng trước (bảng `Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | Đã quan sát`, điều khiển bằng `.env`/compose + folder Postman 32, nêu BẬT/TẮT và cách khôi phục), rồi bảng Tự động 3 cột (`Cần xác nhận | Test case (bấm để mở) | Lệnh chạy riêng`) mỗi test là link tới dòng khai báo (`File.cs:LINE`, kiểm `#L` đúng), mục Kết quả lượt QA ngắn, Kết luận một đoạn trỏ `[QA_Debt.md](QA_Debt.md)`. KHÔNG có khối "Nguồn đối chiếu", KHÔNG có phần phát hiện dài (~60–140 dòng). Phụ thuộc T033, T037.
- [X] T040 [US5] Thêm mục `## 032 — …` vào `docs/QA/QA_Debt.md` với mọi phát hiện từ T009–T037 (số đo lệch, nhiễu, giới hạn loại B/G/F, v.v.); cập nhật số đếm trong tiêu đề file nếu có. Phụ thuộc T039. *(Mục đã thêm; còn bổ sung kết quả newman T037.)*
- [X] T041 [US5] Cập nhật `docs/architecture/technical-debt.md`, thêm mục 032: rủi ro tài liệu và `catalog.json` lệch (hai nơi gợi ý, test chỉ kiểm phủ); sai lệch Nguyên tắc III của script 031 còn mở, hạn của 031 hết ở đây và CHƯA có hạn mới (chờ người dùng quyết — trình ở T042); số đo phụ thuộc máy.
- [X] T042 [US5] Hỏi người dùng về hạn mới (hoặc spec riêng) cho sai lệch Nguyên tắc III của script 031 và ghi quyết định vào technical-debt mục 032 và plan.md Complexity Tracking. **HỎI NGƯỜI DÙNG**; nếu chưa trả lời thì để nợ MỞ và ghi rõ. *(Người dùng quyết 2026-10-06: mở spec riêng cho test script; ghi ở technical-debt mục 032, plan.md Complexity Tracking, Architect và functional-debt.)*
- [X] T043 [P] [US5] Tạo `docs/diagrams/032-troubleshoot-practice-docs-component.drawio` theo mẫu `docs/diagrams/031-error-group-catalog-component.drawio`, `<diagram>` duy nhất, nhãn tiếng Việt: chuỗi file 09–17, `catalog.json`, `incident-drill.ps1`, Kibana/Elasticsearch, project test quy ước, folder Postman 32.
- [X] T044 [P] [US5] Tạo `docs/diagrams/032-troubleshoot-practice-docs-flow-nghiep-vu.drawio`, ngôn ngữ nghiệp vụ: chọn dạng (a) hay (b) → (a) làm theo hướng dẫn / (b) xin gợi ý mở dần → khôi phục → xác nhận 15 phút → bài tập/đã đạt.
- [X] T045 [P] [US5] Tạo `docs/diagrams/032-troubleshoot-practice-docs-sequence.drawio`: trình tự thật của T029 (người học → script `-Start` → Kibana → file 17 mức 1→2→3 → `-Reveal` → `-Restore`), số đo thật làm nhãn phụ.
- [X] T046 [US5] Tạo `docs/PO/032_PO_tài liệu luyện troubleshoot theo nhóm lỗi.md` theo khuôn `docs/PO/031_PO_danh mục 8 nhóm lỗi luyện troubleshoot.md` (~45–55 dòng, ngôn ngữ nghiệp vụ, không tên công cụ kỹ thuật).
- [X] T047 [US5] Cập nhật `docs/PO/functional-debt.md`: thêm mục 032 (link tới file PO T046) vào cả `## 1. Điều đặc biệt` và `## 2. Giới hạn hiện tại`, ngôn ngữ nghiệp vụ, đối chiếu QA_Debt và technical-debt mục 032. Phụ thuộc T040, T041, T046.
- [X] T048 [US5] Rà lại comment trong 3 file test của T005–T007: tiếng Việt, đủ `Kiểm tra / Lý do phải test / Task nguồn`; số dòng link trong QA T039 vẫn đúng sau mọi sửa.

**Checkpoint**: bộ tài liệu và Postman đầy đủ theo nếp 027–031.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T049 Kiểm tra cuối: (a) mọi link tương đối trong file của T038–T047 trỏ tới file có thật (giải mã `%20`/tiếng Việt); (b) 3 file `032-*.drawio` parse được bằng PowerShell `[xml]` hoặc `node`, mỗi file đúng 1 `<diagram>`; (c) `postman/ecommerce.postman_collection.v2.json` parse được, folder 32 có 8 subfolder; (d) `git diff master --stat` không có thay đổi dưới `scripts/`, `services/`, `shared/`, `docker/`, `deploy/`, `docker-compose*.yml`, `.env.example`, dashboard/rule `.ndjson`, `catalog.json` (SC-006); (e) `00-tong-quan-lo-trinh.md` không còn "Bộ 4 file"/"cả 6 file" (SC-005).
- [X] T050 Dọn cấu hình: trả `CHAOS_ALLOW_FAULT_INJECTION` và `CHAOS_ALLOW_LATENCY_INJECTION` về `false` (hoặc xoá dòng) trong `.env` cục bộ, `-Restore` mọi lần chạy còn mở, chạy lại stack không kèm override và xác nhận mọi container healthy; dừng tải nền; kiểm tra `.incident-drill/` không bị commit.
- [X] T051 **HỎI NGƯỜI DÙNG trước khi làm**: xoá toàn bộ dữ liệu Elastic sau khi triển khai xong spec này (đã chốt "clean toàn bộ, hỏi lại trước khi xoá"). Chỉ thực hiện khi người dùng xác nhận rõ phạm vi (index/data stream nào, hay xoá volume `local-elasticsearch`). *(Người dùng xác nhận "Xoá sạch Elastic" 2026-10-06; đã xoá volume `ecomerce-local_local-es-data` và dựng lại.)*
- [ ] T052 **HỎI NGƯỜI DÙNG**: ai làm theo hướng dẫn thật (một nhóm bất kỳ và dạng (b)) và khi nào — task MỞ như T062 của 031, không chặn việc coi spec xong. Người viết đã là người chạy nên không thay thế được bước này.

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 → Phase 2 (T004–T008) → mọi user story. T001 chặn mọi task cần stack (T009 trở đi, T018–T025, T029, T037).
- US1 (T009–T017) tuần tự vì một lần chỉ một lần chạy mở. US3 (T018–T025) sau US1 và cũng tuần tự. US2 (T026–T030) sau T017 (cần số đo); T029 cần stack.
- US4 (T031–T035): T031 và T032 làm song song được và độc lập stack nhưng chỉ xanh hoàn toàn sau US1–US3; T033 sau T025, T030, T031, T032.
- US5 (T036–T048): T036→T037; T038, T043–T045, T046 song song được; T039 sau T033 và T037; T040 sau T039; T047 sau T040, T041, T046.
- Phase 8 sau tất cả.

### Parallel Opportunities

- Phase 2: T005, T006, T007 (3 file test khác nhau, sau T004).
- Phase 6–7 không cần stack: T031, T032, T038, T043, T044, T045, T046 có thể làm song song với nhau.
- Mọi task chạy thật trên stack KHÔNG song song.

### Implementation Strategy

- **MVP**: Phase 1–2 + US1 cho một nhóm (T009) + test xanh cho nhóm đó, rồi mở rộng 8 nhóm. Gợi ý thứ tự demo: Phase 1 → 2 → US1 → US2 → US3 → US4 → US5 → Polish.
- Mỗi checkpoint phải có bằng chứng trước khi sang phase tiếp.
