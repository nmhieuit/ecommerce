---

description: "Danh sách task triển khai diễn tập sự cố thật và phản ứng on-call (SCRUM-36)"
---

# Tasks: Diễn tập sự cố thật và phản ứng on-call (tiêm lỗi mù, phát hiện, xử lý, xác nhận khôi phục bằng telemetry)

**Input**: Tài liệu thiết kế tại `specs/028-incident-oncall-drill/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: KHÔNG. Người dùng chốt "đây là diễn tập, không có code mới nên không cần test". Đây là sai
lệch Nguyên tắc III, đã ghi ở plan.md Complexity Tracking, hạn tới khi SCRUM-37 xong. Mọi kiểm chứng
đều chạy thật theo [quickstart.md](./quickstart.md), và chỉ đánh `[X]` khi đã có bằng chứng.

**Ràng buộc chung**:
- KHÔNG sửa file nào dưới `services/` hay `shared/`.
- KHÔNG sửa `docker-compose*.yml` hay `.env.example` để tiêm lỗi.
- `CHAOS_ALLOW_FAULT_INJECTION` về `false` sau mỗi buổi chạy.

**Organization**: Nhóm theo user story của spec.md (US1–US4).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Chạy song song được (khác file, không phụ thuộc task chưa xong)
- **[Story]**: User story mà task phục vụ (US1–US4)

## Path Conventions

- Script: `scripts/incident-drill.ps1`; file sinh ra: `.incident-drill/<runId>/` (không commit).
- 7 service compose: `gateway-api`, `bff-api`, `products-api`, `baskets-api`, `orders-api`, `parties-api`, `identity-api` trong `docker-compose.local.yml`.
- Kibana: `http://localhost:5601`; tài liệu và export dưới `docs/kibana-quan-sat-he-thong/`.
- Quy trình và bản ghi sự cố: `docs/dien-tap-chaos-engineering/`.

---

## Phase 1: Setup

**Purpose**: Chỗ chứa file cục bộ, công cụ tải, và stack chạy được.

- [X] T001 [P] Thêm dòng `.incident-drill/` kèm comment "file niêm phong và compose override tạm của diễn tập sự cố — spec 028, không commit" vào `.gitignore`.
- [X] T002 [P] Chạy `npx newman --version` (người dùng đã đồng ý trước việc tải gói `newman`), ghi phiên bản vào ghi chú của task này.
- [X] T003 Dựng stack bằng `docker compose -f docker-compose.local.yml up -d --build --wait`, xác nhận 7 service `healthy` và 4 rule `slo-error-budget` của 027 đang enable. Nếu stack hỏng vì lý do không thuộc 028 thì dừng và báo người dùng.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Tải nền phủ cả 7 service; kiểm chứng V1 (Kibana Cases trên Basic) và V6 (ES|QL percentile, alert theo hàng).

**⚠️ CRITICAL**: chặn mọi user story; kịch bản nào cũng cần traffic nền.

- [X] T004 Thêm folder `28 - Diễn tập sự cố: tải nền bổ sung (parties, identity)` vào `postman/ecommerce.postman_collection.v2.json`, theo đúng khuôn các folder 25–27. Hai request:
  - `01 Parties qua BFF — GET {{gatewayUrl}}/bff/parties/{{$guid}}`: có header `Authorization: Bearer {{accessToken}}` như folder 26, test status `404`;
  - `02 Identity discovery — GET {{identityUrl}}/.well-known/openid-configuration`: test status `200`.

  Mô tả folder giải thích bằng tiếng Việt vì sao cần folder này (research.md Quyết định 5). Dùng đúng tên biến token mà folder 26 đang dùng; mở folder 26 để chép.
- [X] T005 Chạy lệnh newman của [quickstart.md](./quickstart.md) Kịch bản 2 trong 6 phút, rồi chạy ES|QL `FROM traces-generic.otel-default* | WHERE @timestamp > NOW() - 5 minutes | STATS c = COUNT(*) BY resource.attributes.service.name` trên Kibana. Xác nhận đủ 7 service có span, và ghi số đếm vào ghi chú. Nếu thiếu service nào thì dừng và báo người dùng.
- [X] T006 Kiểm chứng V1: trên Kibana 9.4.4 license Basic, tạo thử một Case (Observability hoặc Stack Management → Cases), thêm một comment, rồi đóng Case. Ghi kết quả. **Nếu không dùng được: DỪNG toàn bộ US2–US4 và hỏi người dùng chọn kênh thông báo khác. Không tự đổi.**
- [X] T007 Kiểm chứng V6: tạo tạm một rule `.es-query` ES|QL, `groupBy: row`, chu kỳ 1 phút, truy vấn `STATS p95 = PERCENTILE(duration, 95) BY service = resource.attributes.service.name | WHERE p95 > 0 | KEEP service`. Xác nhận rule sinh một alert cho mỗi service. Xoá rule tạm sau khi xong.
- [X] T008 Ghi kết quả T005–T007 vào mục mới "Kết quả xác minh (T005–T007)" ở cuối `specs/028-incident-oncall-drill/research.md`, gồm: đúng/sai, bằng chứng, phương án dự phòng nếu có.

**Checkpoint**: có traffic nền ở cả 7 service; Cases dùng được; cơ chế rule đã chứng minh.

---

## Phase 3: User Story 1 - Tiêm một sự cố thật mà người vận hành không biết trước nguyên nhân (Priority: P1) 🎯 MVP

**Goal**: `scripts/incident-drill.ps1` đúng [contracts/incident-drill-script-contract.md](./contracts/incident-drill-script-contract.md) (bất biến 1–11).

**Independent Test**: [quickstart.md](./quickstart.md) Kịch bản 0 (cờ tắt) và Kịch bản 4 (từng loại A/B/C ở chế độ không mù).

### Implementation for User Story 1

- [X] T009 [US1] Tạo `scripts/incident-drill.ps1`. Phong cách theo `scripts/up.ps1`: `param` block, comment tiếng Việt, `$ErrorActionPreference = 'Stop'`. Tham số:
  - `-Start`, `-Reveal`, `-RunId`;
  - `-Service`, `-FaultType` (`A`/`B`/`C`), `-DelaySeconds` cho chế độ không mù.

  Hiện thực bất biến 1: đọc `.env` ở repo root; nếu `CHAOS_ALLOW_FAULT_INJECTION` khác `true` thì `exit 1` với thông báo tiếng Việt, không ghi file, không gọi docker.
- [X] T010 [US1] Trong `scripts/incident-drill.ps1`, hiện thực bốc thăm và niêm phong (bất biến 2, 3, 6, 7, 11; data-model.md mục 1):
  - chọn service đều trong 7, rồi chọn loại đều trong các loại áp dụng được (BFF không có B);
  - `delaySeconds` 0–1800; `errorRatePct` 5–50 khi loại C; với BFF loại A, chọn ngẫu nhiên 1 trong 4 downstream;
  - ghi `.incident-drill/<runId>/sealed.json` (có `blind`) và `hash.txt`;
  - chỉ in `runId` và SHA-256 (`Get-FileHash -Algorithm SHA256`);
  - chế độ không mù in thêm dòng "CHẾ ĐỘ KHÔNG MÙ — không dùng cho buổi diễn tập".
- [X] T011 [US1] Trong `scripts/incident-drill.ps1`, hiện thực sinh file `.incident-drill/<runId>/docker-compose.incident.yml`. File chỉ có khối `services.<đích>.environment` theo research.md Quyết định 3:
  - **A, DB**: connection string đầy đủ với `Server=incident-missing-db`, dựng lại từ các biến `.env` giống mẫu trong `docker-compose.local.yml`; không hard-code mật khẩu trong script, đọc `MSSQL_SA_PASSWORD` từ `.env`.
  - **A, BFF**: `Services__<X>Api__BaseUrl=http://incident-missing-host:8080`.
  - **A, gateway**: `ReverseProxy__Clusters__bff-cluster__Destinations__bff__Address=http://incident-missing-host:8080`.
  - **B, DB**: connection string gốc + `Max Pool Size=1`.
  - **B, gateway**: `ReverseProxy__Clusters__bff-cluster__HttpClient__MaxConnectionsPerServer=1`.
  - **C**: không có override; cờ lấy từ `.env`.

  Lấy đúng tên khoá `ConnectionStrings__<X>Db` của từng service từ `docker-compose.local.yml`.
- [X] T012 [US1] Trong `scripts/incident-drill.ps1`, hiện thực tiến trình nền (`Start-Process powershell -WindowStyle Hidden` gọi lại chính script với tham số nội bộ `-RunInjector -RunId`):
  - chờ `delaySeconds`;
  - chạy `docker compose -f docker-compose.local.yml -f <override> up -d --force-recreate --no-deps` cho cả 7 service trong một lệnh (bất biến 5);
  - ghi `injected-at.txt` (giờ +07:00).

  Không in gì ra console của người vận hành.
  **Điều chỉnh khi chạy T016 (2026-10-01)**: một lệnh `up` gộp 7 service làm BFF/gateway kẹt ở `Created` (chờ service đích healthy theo `depends_on`) và Compose in đích danh service hỏng — đổi thành 7 lệnh `up --no-deps` riêng chạy liền nhau (~40 s).
- [X] T013 [US1] Trong `scripts/incident-drill.ps1`, hiện thực loại C (research.md Quyết định 4, bất biến 8):
  - đo tốc độ span server của service đích trong 5 phút trước khi tiêm, qua `POST http://localhost:9200/_query` (ES|QL);
  - gửi `Invoke-WebRequest` có header `X-Chaos-Fault: 5xx` vào route đã duyệt trên cổng publish của service đích, với tốc độ `r/(1−r) ×` tốc độ nền;
  - dừng khi `docker inspect -f '{{.Id}}'` của container đích khác Id ghi lúc tiêm.

  Lấy cổng publish từ `docker-compose.local.yml`.
- [X] T014 [US1] Trong `scripts/incident-drill.ps1`, hiện thực `-Reveal -RunId` (bất biến 9):
  - tính lại SHA-256 và so với `hash.txt`; lệch thì `exit 1`;
  - khớp thì in `sealed.json` và `injected-at.txt`, rồi xoá `docker-compose.incident.yml`.
- [X] T015 [US1] Chạy [quickstart.md](./quickstart.md) Kịch bản 0 (cờ `false`): xác nhận `exit 1`, không có thư mục mới dưới `.incident-drill/`, uptime container không đổi. Ghi bằng chứng vào ghi chú.
- [X] T016 [US1] Bật cờ, giữ tải nền. Chạy chế độ không mù cho loại A trên `products-api`, loại A trên `bff-api`, và loại A trên `gateway-api` (`-DelaySeconds 0`). Mỗi lần, xác nhận:
  - cả 7 container có uptime mới;
  - chỉ service đích nhận biến sai (`docker inspect`);
  - service đích trả 5xx/trễ trong Discover;
  - `-Reveal` khớp mã băm;
  - chạy lại compose không override thì hết lỗi (bất biến 10).

  Ghi bằng chứng vào ghi chú.
- [X] T017 [US1] Kiểm chứng V2: chế độ không mù loại B trên `orders-api` và trên `gateway-api` dưới tải nền. Xác nhận có 5xx hoặc p95/p99 vượt ngưỡng trong cửa sổ 5 phút. **Nếu không: DỪNG và hỏi người dùng về con số pool hoặc tốc độ tải. Không tự đổi.**
- [X] T018 [US1] Kiểm chứng V4 và V3:
  - **V4**: chế độ không mù loại C trên `parties-api` và `identity-api`. Xác nhận span `500` của đúng service trong Elasticsearch, tỷ lệ xấp xỉ `errorRatePct`, và việc gửi dừng sau khi tạo lại container.
  - **V3**: sau khi tạo lại `identity-api`, quan sát newman ở vòng kế tiếp có hết `401` không. Nếu `401` kéo dài thì ghi lại và hỏi người dùng có loại identity khỏi bước tạo lại hay không.

  Ghi kết quả V2, V3, V4 vào mục "Kết quả xác minh" của `specs/028-incident-oncall-drill/research.md`. Đặt lại `CHAOS_ALLOW_FAULT_INJECTION=false`.

**Checkpoint**: tiêm lỗi mù chạy được, chỉ bằng cấu hình, gỡ được bằng chạy lại compose (MVP).

---

## Phase 4: User Story 2 - Cảnh báo phát hiện nhanh và quy trình triage (Priority: P1)

**Goal**: rule `incident-fast-detection` đúng [contracts/fast-detection-rule-contract.md](./contracts/fast-detection-rule-contract.md); bảng trên dashboard; quy trình triage và Kibana Case được viết ra.

**Independent Test**: [quickstart.md](./quickstart.md) Kịch bản 1, cộng một lần tiêm không mù khiến rule bắn và bảng hiện service.

### Implementation for User Story 2

- [X] T019 [US2] Tạo `docs/kibana-quan-sat-he-thong/08-phat-hien-nhanh-va-xu-ly-su-co.md`, cùng văn phong file `07`.
  - Viết truy vấn ES|QL của rule: 5 phút gần nhất, theo service, `err_pct`, `p95`/`p99` đổi ns sang ms, `CASE` ngưỡng `Bff.Api` 300/800 và còn lại 150/500, `WHERE` theo bất biến 3, rồi `STATS ... BY service | KEEP service` theo bất biến 5.
  - Chạy truy vấn trong Discover khi tải nền khoẻ và xác nhận 0 hàng.
  - Ghi lại ngưỡng lấy từ `service-manifest.yaml` nào.
- [X] T020 [US2] Trên Kibana UI tạo rule `incident-fast-detection` theo bất biến 1, 2, 7: `.es-query` ES|QL, `groupBy: row`, chu kỳ 5m, cửa sổ 5m, tag `incident-fast-detection`, không action. Ghi cấu hình vào file `08`.
- [X] T021 [US2] Thêm bảng "Phát hiện nhanh — vượt SLO trong 5 phút gần nhất" vào dashboard `SLO vận hành hằng ngày — 7 service` (id `e2e06ff5-9cdf-4bea-acc8-5fd60ce26170`), cạnh nhóm "Ngân sách lỗi tuần này".
  - Nguồn: alert active tag `incident-fast-detection` từ `.alerts-stack.alerts-default`; cột service và `kibana.alert.start`.
  - Dựng bằng Discover session ES|QL qua Saved Objects API, giống cách T031 của 027 đã làm.
  - Ghi cách dựng vào file `08`.
- [X] T022 [US2] Export rule ra `docs/kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson`, và export lại dashboard ra `docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson`. **Không** sửa `alerts/error-budget-rules.ndjson`.
- [X] T023 [US2] Cập nhật `docs/kibana-quan-sat-he-thong/alerts/README.md`: thêm mục cho `incident-fast-detection-rule.ndjson` (lệnh import, lệnh export theo id, ghi chú "Enable lại rule tag `incident-fast-detection` sau import").
- [X] T024 [US2] Thêm mục "Diễn tập sự cố on-call (SCRUM-36)" vào `docs/dien-tap-chaos-engineering/README.md`, gồm:
  1. Chuẩn bị: cờ, tải nền newman, `-Start`.
  2. Quy trình triage: phát hiện → đánh giá severity theo bảng SEV1–SEV3 của [contracts/incident-record-contract.md](./contracts/incident-record-contract.md) → thông báo trên Kibana Case → giảm thiểu.
  3. Cách tạo Kibana Case tay: tiêu đề, dán mã băm, severity, comment tại mỗi mốc và mỗi 30 phút, comment baseline `alert→merge` (data-model.md mục 3).
  4. Giảm thiểu: merge PR phòng ngừa vào master → `git pull` → chạy lại compose không override.
  5. Nhắc rằng postmortem thuộc SCRUM-37.
- [X] T025 [US2] Kiểm chứng: import lại hai file ndjson (quickstart Kịch bản 1), enable rule; tiêm không mù loại A trên `orders-api`. Xác nhận:
  - rule bắn cho `Orders.Api` trong ≤ 10 phút;
  - bảng dashboard hiện `Orders.Api`;
  - một Case thử tạo được theo đúng hướng dẫn T024.

  Ghi thời điểm vượt SLO, thời điểm alert bắn và ảnh/giá trị panel vào file `08`. Gỡ lỗi, đóng Case thử, đặt lại cờ `false`.

**Checkpoint**: sự cố được phát hiện nhờ cảnh báo trên dashboard người vận hành mở mỗi ngày.

---

## Phase 5: User Story 3 - Dòng thời gian sự cố có timestamp với các mốc tách bạch (Priority: P1)

**Goal**: mẫu bản ghi đúng [contracts/incident-record-contract.md](./contracts/incident-record-contract.md).

**Independent Test**: điền thử mẫu từ dữ liệu của T025; mọi trường có mặt, các mốc đúng thứ tự theo bất biến 2–3.

### Implementation for User Story 3

- [X] T026 [P] [US3] Tạo `docs/dien-tap-chaos-engineering/mau-ban-ghi-su-co.md` theo khuôn `mau-ket-qua.md`:
  - hướng dẫn sao chép thành `ket-qua/<YYYY-MM-DD>-su-co-<runId>.md`;
  - đủ 14 trường bắt buộc của contract, mỗi trường kèm comment HTML giải thích;
  - bảng `dong_thoi_gian` (thời điểm +07:00 | đã quan sát | đã làm);
  - nguồn của từng mốc: `injected-at.txt`, `kibana.alert.start`, PR, bằng chứng 15 phút.
- [X] T027 [US3] Cập nhật đoạn mở đầu và mục "Lịch sử chạy" của `docs/dien-tap-chaos-engineering/README.md`: nêu rằng bản ghi sự cố (`*-su-co-*.md`) cũng được liệt kê ở đây, kèm kết luận ngắn (khớp / không khớp niêm phong, severity, baseline).

**Checkpoint**: có khuôn bản ghi bắt buộc đủ mốc.

---

## Phase 6: User Story 4 - Xác nhận khôi phục bằng telemetry (Priority: P2)

**Goal**: cách đo "đạt SLO liên tục 15 phút" được viết ra và chạy được (research.md Quyết định 9).

**Independent Test**: sau khi gỡ lỗi ở T025, truy vấn theo phút cho thấy 15 phút liên tục đạt SLO, và rule hết active.

### Implementation for User Story 4

- [X] T028 [US4] Thêm vào `docs/kibana-quan-sat-he-thong/08-phat-hien-nhanh-va-xu-ly-su-co.md` mục "Xác nhận khôi phục 15 phút": một truy vấn ES|QL cho một service, `BUCKET(@timestamp, 1 minute)`, cột tổng / 5xx / p95 / p99 / đạt (bool), trên 30 phút gần nhất, kèm cách đọc ra "15 phút liên tục đạt và có traffic".
- [X] T029 [US4] Thêm bước "Xác nhận giải quyết" vào mục diễn tập sự cố của `docs/dien-tap-chaos-engineering/README.md`: chạy truy vấn T028, chụp/ghi link, kiểm tra rule không còn active cho service, rồi mới ghi `moc_giai_quyet`, đóng Case và chạy `-Reveal`.
- [X] T030 [US4] Kiểm chứng trên dữ liệu thật của lần gỡ lỗi ở T025: chạy truy vấn T028 cho `Orders.Api`, xác định thời điểm cuối của 15 phút liên tục đạt SLO. Ghi bằng chứng vào file `08`.

**Checkpoint**: khôi phục được chứng minh bằng số đo, không bằng "trông có vẻ ổn".

---

## Phase 7: Buổi diễn tập mù thật (US1–US4 liền mạch)

- [ ] T031 **HỎI NGƯỜI DÙNG trước khi làm**: ai thực hiện buổi diễn tập mù theo [quickstart.md](./quickstart.md) Kịch bản 3. Claude đã viết script nên không "mù". Hỏi thêm: ai mở và merge PR phòng ngừa vào master. Khi buổi diễn tập xong, kiểm tra bản ghi `docs/dien-tap-chaos-engineering/ket-qua/<YYYY-MM-DD>-su-co-<runId>.md`:
  - đủ trường, đúng thứ tự mốc theo contract;
  - đã được thêm vào "Lịch sử chạy";
  - `CHAOS_ALLOW_FAULT_INJECTION=false` sau buổi diễn tập.
  **Người dùng chốt (2026-10-02): người dùng tự thực hiện sau, theo `docs/dien-tap-chaos-engineering/README.md`. Task này để mở.**

---

## Phase 8: Polish & Cross-Cutting Concerns (tài liệu theo khuôn 027)

- [X] T032 [P] Tạo `docs/architecture/028_Architect_diễn tập sự cố thật và phản ứng on-call.md` theo cấu trúc và văn phong `docs/architecture/027_Architect_chính sách ngân sách lỗi và ngưỡng cảnh báo.md`, gồm mục `## Sơ đồ` trỏ tới 3 file của T037–T039.
- [X] T033 Cập nhật `docs/architecture/technical-debt.md`, thêm mục 028:
  - sai lệch Nguyên tắc III (không test), hạn tới khi SCRUM-37 xong;
  - rủi ro ngưỡng trong `incident-fast-detection` trôi khỏi manifest;
  - nhiễu do tạo lại cả 7 container;
  - niêm phong dựa vào kỷ luật;
  - BFF không có kịch bản cạn pool;
  - các phát hiện V1–V6.
- [X] T034 [P] Tạo `docs/QA/028_QA_diễn tập sự cố thật và phản ứng on-call.md` theo khuôn QA 024–027:
  - phần Thủ công đứng trước;
  - bảng `Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | Đã quan sát`;
  - điều khiển bằng `CHAOS_ALLOW_FAULT_INJECTION` (BẬT/TẮT và cách khôi phục), các lệnh `scripts/incident-drill.ps1` ở chế độ không mù, folder Postman `26` + `28`;
  - phần Tự động ghi "không có test tự động — sai lệch Nguyên tắc III, xem plan.md";
  - không có khối "Nguồn đối chiếu"; mọi phát hiện chỉ ở QA_Debt.
- [X] T035 Thêm mục `## 028 — …` vào `docs/QA/QA_Debt.md` với mọi phát hiện từ T005–T030, và cập nhật số đếm trong tiêu đề file nếu có. Phụ thuộc T034.
- [X] T036 Tạo tài liệu PO `docs/PO/028_PO_<tên hướng người dùng>.md`. **HỎI LẠI NGƯỜI DÙNG tên file (phần sau `028_PO_`) trước khi tạo, đề xuất 2–3 phương án** theo kiểu tên của 025/027.
  - Khuôn `docs/PO/027_PO_chính sách ngân sách lỗi và ngưỡng cảnh báo.md`, ~45–55 dòng, không tên công cụ kỹ thuật.
  - Các mục: `## Vấn đề trước đây` → `## Giải pháp: …` → `## Trải nghiệm thực tế diễn ra như thế nào` → `## Lợi ích kinh doanh`.
  **Người dùng chốt tên (2026-10-02)**: `028_PO_diễn tập sự cố thật và phản ứng trực sự cố.md`.
- [X] T037 [P] Tạo `docs/diagrams/028-incident-oncall-drill-component.drawio` theo mẫu `docs/diagrams/027-error-budget-alerting-component.drawio`, `<diagram>` duy nhất, nhãn tiếng Việt. Các khối:
  - script `incident-drill.ps1` và `.incident-drill/` (sealed.json, hash, override);
  - Docker Compose với 7 container;
  - newman với folder 00/26/28;
  - OTel → Elasticsearch;
  - rule `incident-fast-detection`;
  - alerts-as-data → bảng dashboard;
  - Kibana Case;
  - `docs/dien-tap-chaos-engineering/` (quy trình, mẫu, ket-qua).
- [X] T038 [P] Tạo `docs/diagrams/028-incident-oncall-drill-flow-nghiep-vu.drawio` theo mẫu `027-...-flow-nghiep-vu.drawio`, dùng ngôn ngữ nghiệp vụ: sự cố bí mật → cảnh báo → đánh giá mức độ → thông báo → tìm nguyên nhân → giảm thiểu (merge sửa phòng ngừa) → xác nhận 15 phút đạt cam kết → mở niêm phong đối chiếu → bản ghi (postmortem thuộc bước sau).
- [X] T039 [P] Tạo `docs/diagrams/028-incident-oncall-drill-sequence.drawio` theo mẫu `027-...-sequence.drawio`, là trình tự thật của T025/T030, với số đo thật làm nhãn phụ.
- [X] T040 Cập nhật `docs/PO/functional-debt.md`: thêm mục 028, link tới file PO của T036, vào cả `## 1. Điều đặc biệt` và `## 2. Giới hạn hiện tại`, bằng ngôn ngữ nghiệp vụ, đối chiếu QA_Debt và technical-debt mục 028. Tiêu đề đang ghi "toàn bộ 25 tính năng": **hỏi lại người dùng con số trước khi sửa**. Phụ thuộc T033, T035, T036.
  **Người dùng chốt (2026-10-02)**: tiêu đề sửa thành "toàn bộ 26 tính năng".
- [X] T041 [P] Thêm mục file `08` vào `docs/kibana-quan-sat-he-thong/00-tong-quan-lo-trinh.md`.
- [X] T042 Kiểm tra cuối:
  - (a) mọi link tương đối trong các file của T032–T036, T040 và trong mục 028 của technical-debt trỏ tới file có thật (giải mã `%20`/tiếng Việt);
  - (b) 3 file `.drawio` parse được bằng `python -c "import xml.etree.ElementTree as E; E.parse(...)"`, mỗi file đúng 1 `<diagram>`;
  - (c) `git status` không có file nào dưới `services/`, `shared/`, `.incident-drill/` hay `docker-compose*.yml` bị sửa.

  Ghi kết quả vào ghi chú. Phụ thuộc T032–T041.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1**: không phụ thuộc.
- **Phase 2**: sau Phase 1; chặn mọi user story. T006 sai thì dừng US2–US4.
- **Phase 3 (US1)**: sau Phase 2.
- **Phase 4 (US2)**: sau Phase 2. Lần kiểm chứng T025 cần script của US1 (T009–T014).
- **Phase 5 (US3)**: T026 độc lập. T027 sau T024 (cùng file README).
- **Phase 6 (US4)**: sau T025 (cần dữ liệu gỡ lỗi thật); T029 sau T024, T027 (cùng file README).
- **Phase 7**: sau US1–US4; cần người dùng quyết định.
- **Phase 8**: sau các phase trên; T037–T039 chỉ cần nội dung đã có.

### Within Each User Story

- T009 → T010 → T011 → T012 → T013 → T014 (cùng một file script, tuần tự) → T015–T018.
- T019 → T020 → T021 → T022 → T023; T024 song song với T019–T023 (khác file) → T025.
- T028 → T029 → T030.

### Parallel Opportunities

- T001 ∥ T002.
- T026 ∥ toàn bộ Phase 4.
- T019–T023 (tài liệu/Kibana) ∥ T009–T014 (script), cho tới T025.
- T032, T034, T037, T038, T039, T041: khác file, song song.

---

## Parallel Example: Phase 8

```text
Task: "T037 docs/diagrams/028-incident-oncall-drill-component.drawio"
Task: "T038 docs/diagrams/028-incident-oncall-drill-flow-nghiep-vu.drawio"
Task: "T039 docs/diagrams/028-incident-oncall-drill-sequence.drawio"
Task: "T032 docs/architecture/028_Architect_diễn tập sự cố thật và phản ứng on-call.md"
Task: "T034 docs/QA/028_QA_diễn tập sự cố thật và phản ứng on-call.md"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 → Phase 2 → Phase 3: tiêm lỗi mù chỉ bằng cấu hình, gỡ được. Đáp ứng Jira test scenario 1.
2. **DỪNG và kiểm tra**: quickstart Kịch bản 0 và 4.

### Incremental Delivery

1. US1: tiêm lỗi.
2. US2: cảnh báo, quy trình triage, Case. Đáp ứng tiêu chí chấp nhận 1 và test scenario 2.
3. US3: bản ghi có các mốc tách bạch. Đáp ứng tiêu chí chấp nhận 2 và test scenario 3.
4. US4: xác nhận 15 phút. Đáp ứng tiêu chí chấp nhận 3.
5. Phase 7: buổi diễn tập mù thật (người dùng quyết định ai làm).
6. Phase 8: tài liệu theo khuôn 027.

---

## Notes

- Task cần Docker/Kibana thật (T003, T005–T008, T015–T018, T025, T030, T031) chỉ đánh `[X]` khi đã chạy thật và có bằng chứng.
- `CHAOS_ALLOW_FAULT_INJECTION` về `false` sau mỗi lần tiêm.
- Mỗi lần chạy folder 26 tạo đơn hàng thật; dọn theo quickstart mục "Dọn dẹp".
- Commit theo Conventional Commits sau mỗi nhóm task hợp lý.
