# Feature Specification: Tài liệu luyện troubleshoot theo nhóm lỗi (hướng dẫn step by step cho lỗi có kịch bản, gợi ý theo triệu chứng cho lỗi bất ngờ)

**Feature Branch**: `feature/032-troubleshoot-practice-docs`

**Created**: 2026-10-06

**Status**: Draft

**Input**: User description: "Tài liệu luyện troubleshoot trong docs/kibana-quan-sat-he-thong/: (a) hướng dẫn step by step cho từng nhóm lỗi có kịch bản; (b) gợi ý theo triệu chứng cho lỗi bất ngờ không báo trước, mở dần từ nhẹ → mạnh → đáp án. (Spec D trong đợt rà soát nợ kỹ thuật; phụ thuộc spec B — dashboard Xử lý sự cố — và spec C — danh mục 8 nhóm lỗi.)"

## Clarifications

### Session 2026-10-06 (phiên rà soát nợ kỹ thuật — đã chốt trước khi viết spec)

- Q: Tài liệu đặt ở đâu? → A: `docs/kibana-quan-sat-he-thong/`, nối tiếp chuỗi 01–08.
- Q: Dạng (a) gồm gì? → A: Mỗi nhóm lỗi có hướng dẫn step by step: triệu chứng, nơi nhìn trên Kibana (dashboard Xử lý sự cố, Discover, log), câu lệnh, cách khôi phục, cách xác nhận đã khỏi.
- Q: Dạng (b) gợi ý thế nào? → A: Cả hai cách: tài liệu gợi ý sắp theo TRIỆU CHỨNG (5xx, trễ, 401, mất traffic…) để đọc không lộ đáp án, VÀ lệnh `-Hint` theo mức của script (spec 031).
- Q: 8 nhóm lỗi? → A: Đích kết nối sai; nghẽn và lỗi theo tỷ lệ; độ trễ (chỉ Orders.Api); phụ thuộc hạ tầng dừng; xác thực hỏng; thiếu tài nguyên; mạng đứt; container chết hoặc khởi động lại.
- Q: Ràng buộc chung? → A: Chỉ Docker Compose; không sửa code service; lỗi diễn tập tính vào ngân sách như thật.
- Q: Elastic xử lý thế nào sau khi xong? → A: Sẽ clean toàn bộ sau khi triển khai xong spec mới; PHẢI hỏi lại trước khi xoá.
- Q: Điều kiện tiên quyết? → A: Spec B (030) và spec C (031) đã triển khai và merge vào `master` (kiểm ngày 2026-10-06). Chỉ còn T062 của 031 (buổi diễn tập mù thật do người dùng thực hiện) để mở, không chặn spec này.

### Session 2026-10-06 (phiên `/speckit-specify`)

- Q: Cấu trúc file? → A: 9 file, đánh số tiếp từ 09: mỗi nhóm một file (09–16) cộng một file gợi ý theo triệu chứng (17). Nhóm 2 gồm hai loại B và C nhưng chỉ có MỘT hướng dẫn chung. Tên file (người dùng đồng ý nguyên đề xuất, vốn là ví dụ): `09-dich-ket-noi-sai.md`, `10-nghen-va-loi-theo-ty-le.md`, `11-do-tre-orders.md`, `12-ha-tang-dung.md`, `13-xac-thuc-hong.md`, `14-thieu-tai-nguyen.md`, `15-mang-dut.md`, `16-container-chet-hoac-khoi-dong-lai.md`, `17-goi-y-theo-trieu-chung.md`.
- Q: Có chạy thật trên stack để lấy số đo làm bằng chứng không? → A: Có, chạy thật cả 8 nhóm; ghi số đo và truy vấn đã kiểm vào tài liệu (như file 06–08, không có ảnh chụp). Việc chạy stack và xoá Elastic sau đó vẫn phải hỏi lại người dùng trước khi làm.
- Q: Mức gợi ý? → A: 3 mức, cùng nghĩa với `-Hint` (1 triệu chứng, 2 tên nhóm, 3 đáp án), nhưng mỗi mức trong tài liệu nặng hơn catalog: có thêm chỉ dẫn cách kiểm (panel/truy vấn) mà `-Hint` không có.
- Q: Tài liệu và `-Hint` dùng chung nguồn nội dung không? → A: Viết riêng. `catalog.json` giữ nguyên cho `-Hint` (27 đoạn đã duyệt ở 031, không sửa). Test chỉ kiểm phủ (có mục cho từng nhóm/loại), không kiểm nội dung khớp. Chấp nhận rủi ro hai nơi có thể lệch và ghi vào `technical-debt.md`.
- Q: Bài tập? → A: Mỗi nhóm có bài tập tự làm và checklist tiêu chí "đã đạt" (như mục "Bài tập tự làm" của file 05).
- Q: Quan hệ với quy trình của 028? → A: Liên kết, không chép: trỏ tới quy trình triage trong README `docs/dien-tap-chaos-engineering/` (mục "Diễn tập sự cố on-call"), `mau-ban-ghi-su-co.md` và truy vấn xác nhận khôi phục 15 phút của file 08; tài liệu mới chỉ viết phần riêng từng nhóm lỗi.
- Q: Có sửa 00-tong-quan-lo-trinh.md và README của `docs/dien-tap-chaos-engineering/` không? → A: Có, đầy đủ: 00 thêm mục 09–17 vào lộ trình, sửa các chỗ lỗi thời ("Bộ 4 file", "Chuẩn bị chung cho cả 6 file", bảng/số liệu khác nếu còn); README diễn tập thêm link sang chuỗi mới.
- Q: Test tự động? → A: Có một test kiểm (1) mọi link nội bộ trong file 09–17 trỏ tới file/đích có thật, (2) mỗi nhóm 1–8 và mỗi loại A–I trong `scripts/incident-drill/catalog.json` có mục hướng dẫn và mục gợi ý theo triệu chứng tương ứng. Viết thành project test mới trong `tests/` (cùng kiểu `DeploymentManifestConventionTests`), chạy cùng `dotnet test`. Sai lệch Nguyên tắc III của 031 ("không test tự động cho script") vẫn giữ nguyên cho script; thời hạn "đến khi spec D hoàn tất" được đóng lại ở spec này theo phạm vi tài liệu.
- Q: Tài liệu đi kèm? → A: Đủ bộ như 031: PO, QA, Architect, 3 sơ đồ drawio, folder Postman, cập nhật `technical-debt.md`, `QA_Debt.md`, `functional-debt.md`; mọi phát hiện chỉ nằm trong QA_Debt. Tên (người dùng đồng ý đề xuất, vốn là ví dụ): `docs/PO/032_PO_tài liệu luyện troubleshoot theo nhóm lỗi.md`, `docs/QA/032_QA_tài liệu luyện troubleshoot theo nhóm lỗi.md`, `docs/architecture/032_Architect_tài liệu luyện troubleshoot theo nhóm lỗi.md`, folder Postman `32 - Luyện troubleshoot`.
- Q: Nhánh và commit? → A: Nhánh mới `feature/032-troubleshoot-practice-docs` trong worktree hiện tại, thư mục `specs/032-troubleshoot-practice-docs`; KHÔNG commit, người dùng tự commit.

### Session 2026-10-06 (phiên `/speckit-clarify`)

- Q: Folder Postman `32 - Luyện troubleshoot` chứa gì? → A: 8 subfolder theo nhóm 1–8, mỗi subfolder có request quan sát triệu chứng và request xác nhận khỏi; request nền trùng với folder 26/28/31 thì tái dùng.
- Q: 3 sơ đồ drawio vẽ gì? → A: Vẽ mới 3 sơ đồ riêng cho spec 032 (không sửa sơ đồ 031): component (9 file tài liệu, catalog.json, script, Kibana, test link/phủ), flow nghiệp vụ (chọn dạng a/b → làm theo hướng dẫn hoặc xin gợi ý → khôi phục → xác nhận 15 phút), sequence (một phiên dạng b mở dần gợi ý).
- Q: Có yêu cầu người thật làm theo hướng dẫn để kiểm tài liệu không? → A: Có, là task mở (như T031 của 028, T062 của 031): người dùng tự làm theo một nhóm bất kỳ và dạng (b) rồi báo kết quả; chưa làm thì vẫn coi spec xong.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Làm theo hướng dẫn step by step cho một nhóm lỗi có kịch bản (Priority: P1)

Người học tiêm một nhóm lỗi bằng `-Inject` (dạng a, biết trước), mở file hướng dẫn của nhóm đó và đi từng bước: nhận ra triệu chứng, tìm trên dashboard Xử lý sự cố / Discover / log, chạy lệnh, khôi phục bằng `-Restore`, xác nhận đã khỏi.

**Why this priority**: Đây là giá trị cốt lõi: biến danh mục 8 nhóm lỗi của 031 thành bài học tự làm được.

**Independent Test**: Chọn một nhóm bất kỳ, tiêm bằng `-Inject`, làm theo file hướng dẫn đúng thứ tự; kết thúc với container đích healthy và xác nhận SLO sạch theo truy vấn của file 08.

**Acceptance Scenarios**:

1. **Given** stack Docker Compose chạy và cờ tiêm lỗi bật, **When** người học tiêm nhóm N và làm theo file hướng dẫn nhóm N, **Then** mỗi bước nêu rõ nhìn ở đâu, gõ lệnh gì, kỳ vọng thấy gì, và các con số kỳ vọng khớp số đo đã ghi trong tài liệu.
2. **Given** nhóm 2, **When** người học đọc hướng dẫn, **Then** hướng dẫn phân biệt loại B và loại C và nói rõ loại B không tạo triệu chứng đo được trong cấu hình hiện tại (giới hạn đã biết của 028/031) thay vì hứa triệu chứng không có.
3. **Given** người học đã khôi phục, **When** làm bước cuối, **Then** có tiêu chí "đã khỏi" cụ thể (container healthy + truy vấn xác nhận khôi phục 15 phút, liên kết tới file 08).

---

### User Story 2 - Gợi ý theo triệu chứng cho lỗi bất ngờ, mở dần nhẹ → mạnh → đáp án (Priority: P1)

Người học chạy `-Start` (dạng b, mù), thấy triệu chứng (ví dụ 5xx, trễ, 401, mất traffic), mở file 17 theo triệu chứng đó và đọc dần từng mức mà không bị lộ đáp án của nhóm khác.

**Why this priority**: Giá trị thứ hai cốt lõi; nếu đọc là lộ đáp án thì bài mù mất tác dụng.

**Independent Test**: Với mỗi triệu chứng trong file 17, đọc mức 1 và 2 mà không thấy tên service, tham số hay mã loại; chỉ mức 3 mới có đáp án; mức nhẹ/mạnh có chỉ dẫn cách kiểm (panel/truy vấn).

**Acceptance Scenarios**:

1. **Given** một lỗi mù đang chạy, **When** người học tra file 17 theo triệu chứng quan sát được, **Then** thấy danh sách các nhóm có thể gây triệu chứng đó mà KHÔNG chỉ ra nhóm nào đang chạy.
2. **Given** người học mở mức 1 → 2 → 3, **When** đọc từng mức, **Then** mức 1 là gợi ý triệu chứng kèm cách kiểm, mức 2 nêu tên nhóm, mức 3 là đáp án đầy đủ và cách khôi phục; cùng nghĩa với `-Hint -Level 1|2|3`.
3. **Given** triệu chứng có thật đã gặp (lỗi lan gateway/BFF khi orders hỏng; token hỏng sau khi tạo lại identity với 401 hoặc 502/504 qua BFF; nhiễu khởi động nguội 5–7 phút; môi trường chậm từng đợt; circuit breaker không trip), **When** tra file 17, **Then** các tình huống này xuất hiện như bẫy/nhiễu cần phân biệt, lấy nguồn từ QA_Debt/technical-debt (không bịa thêm).

---

### User Story 3 - Bài tập tự làm và tiêu chí "đã đạt" sau mỗi nhóm (Priority: P2)

Cuối mỗi file nhóm có bài tập tự làm và checklist "đã đạt" để người học tự đánh giá.

**Why this priority**: Giúp biết mình đã học xong chưa, nhưng không chặn việc làm theo hướng dẫn.

**Independent Test**: Mỗi file 09–16 có mục "Bài tập tự làm" và checklist có thể tự tick; bài tập chạy được trên stack.

**Acceptance Scenarios**:

1. **Given** file nhóm N, **When** người học làm bài tập, **Then** có tiêu chí đo được (đúng nhóm, đúng đích, khôi phục, SLO sạch 15 phút) và không cần xem đáp án ngoài file.

---

### User Story 4 - Chuỗi tài liệu nhất quán và test chống sót (Priority: P2)

Lộ trình 00 phản ánh đủ file 01–17, README diễn tập trỏ sang chuỗi mới, và một test tự động bắt link hỏng hoặc nhóm/loại thiếu hướng dẫn.

**Why this priority**: Tài liệu không dẫn được tới thì không dùng được; test ngăn lệch với catalog.

**Independent Test**: Chạy `dotnet test` project test mới: đạt khi đủ phủ và link đúng; thêm một nhóm giả vào catalog hoặc làm hỏng một link thì test fail.

**Acceptance Scenarios**:

1. **Given** catalog có 8 nhóm và 9 loại, **When** chạy test, **Then** mỗi nhóm 1–8 có file hướng dẫn và mỗi loại A–I có mục trong file 17, mọi link nội bộ trong file 09–17 trỏ tới đích có thật.
2. **Given** 00-tong-quan-lo-trinh.md, **When** đọc, **Then** không còn "Bộ 4 file" hay "cả 6 file" sai số và có mục 09–17 theo đúng thứ tự đọc.

---

### User Story 5 - Tài liệu đi kèm theo nếp 027/028/031 (Priority: P3)

PO, QA, Architect, 3 sơ đồ drawio, folder Postman, cập nhật technical-debt, QA_Debt, functional-debt.

**Why this priority**: Giữ nếp dự án; không thêm hành vi hệ thống.

**Independent Test**: Các file tồn tại đúng tên; QA có bảng "Tự động" liên kết tới test mới; mọi phát hiện nằm trong QA_Debt.

**Acceptance Scenarios**:

1. **Given** bộ tài liệu đi kèm, **When** rà soát, **Then** tên file đúng như đã chốt và nội dung đi kèm theo nếp QA 008+ (Thủ công trước Tự động, comment test bằng tiếng Việt, mỗi test liên kết tới dòng).

---

### Edge Cases

- Nhiễu khởi động nguội 5–7 phút sau khi tạo lại container (QA_Debt 028) làm lẫn với lỗi thật: mỗi hướng dẫn phải nêu cách phân biệt (đợi/so sánh dịch vụ không bị tiêm) trước khi kết luận.
- Loại B không có triệu chứng; loại G chỉ triệu chứng nhẹ dưới tải nền (chưa vượt ngưỡng p95 150 ms ở `--cpus 0.1`/256m); loại F chỉ còn Authority sai (không có biến thể dừng identity-api): hướng dẫn phải nói đúng giới hạn thay vì hứa triệu chứng mạnh hơn.
- Loại D: header phải gửi thẳng tới Orders.Api (không đi xuyên gateway/BFF); hướng dẫn phải nêu route thật đã đo.
- Số đo thay đổi theo máy: tài liệu ghi số đo như "đo ngày X trên máy Y", không như cam kết; kèm cách tự đo lại bằng truy vấn.
- Người học đọc nhầm file nhóm khi đang làm bài mù: file 17 và các file nhóm phải cảnh báo rõ và file 17 không liên kết trực tiếp tới file nhóm ở mức 1–2.
- Elastic đã bị dọn: tài liệu nêu cách xác nhận có dữ liệu trước khi đối chiếu số liệu.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: PHẢI có 9 file mới trong `docs/kibana-quan-sat-he-thong/` đánh số 09–17 theo tên đã chốt; file 09–16 mỗi file một nhóm lỗi theo thứ tự nhóm 1–8 (nhóm 2 chung cho loại B và C), file 17 là gợi ý theo triệu chứng.
- **FR-002**: Mỗi file nhóm (09–16) PHẢI có: triệu chứng người dùng thấy; nơi nhìn trên Kibana (dashboard Xử lý sự cố, Discover, log) kèm truy vấn `_search`/`_count`/ES|QL tự đối chiếu; lệnh tiêm (`-Inject`) và cách khôi phục (`-Restore`); cách xác nhận đã khỏi; mục "Bạn sẽ thấy" theo văn phong file 01–08; bài tập tự làm và checklist "đã đạt".
- **FR-003**: Mỗi file nhóm PHẢI nêu giới hạn đã biết của loại lỗi tương ứng (B không có triệu chứng; G triệu chứng nhẹ; F chỉ Authority sai; D gửi thẳng Orders.Api; E chỉ 5 DB; không có Redis/RabbitMQ), lấy từ QA_Debt/technical-debt/spec 031, không bịa thêm.
- **FR-004**: File 17 PHẢI sắp theo TRIỆU CHỨNG (5xx, trễ, 401, mất traffic, container báo lỗi, v.v.), mỗi triệu chứng có 3 mức mở dần: mức 1 gợi ý triệu chứng kèm cách kiểm, mức 2 nêu tên nhóm, mức 3 đáp án và cách khôi phục; mức 1–2 KHÔNG tiết lộ service, tham số hay mã loại; cùng nghĩa với `-Hint -Level 1|2|3`.
- **FR-005**: File 17 PHẢI nêu các tình huống thật đã gặp làm bẫy/nhiễu (lỗi lan gateway/BFF khi orders hỏng; token hỏng sau khi tạo lại identity → 401 hoặc 502/504 qua BFF; nhiễu khởi động nguội 5–7 phút; môi trường chậm từng đợt; circuit breaker không trip của 025), mỗi tình huống có nguồn trong QA_Debt/technical-debt.
- **FR-006**: Mọi con số, route, truy vấn trong tài liệu PHẢI đã được chạy thật trên stack (cả 8 nhóm) và ghi kèm ngày/điều kiện đo; không có số suy đoán.
- **FR-007**: Tài liệu PHẢI liên kết (không chép) tới quy trình triage và bản ghi sự cố của 028 (README diễn tập, `mau-ban-ghi-su-co.md`) và tới truy vấn xác nhận khôi phục 15 phút ở file 08.
- **FR-008**: Truy vấn dùng dữ liệu `traces-generic.otel-default*` (span Server/Client), `logs-generic.otel-default*`, `metrics-generic.otel-default*`, tên field đã xác minh như file 01–08.
- **FR-009**: PHẢI sửa `00-tong-quan-lo-trinh.md` (thêm mục 09–17, sửa "Bộ 4 file" và "Chuẩn bị chung cho cả 6 file" cùng các số liệu lỗi thời khác) và `docs/dien-tap-chaos-engineering/README.md` (link sang chuỗi mới).
- **FR-010**: PHẢI có project test mới trong `tests/` kiểm: mọi link nội bộ trong file 09–17 có đích thật; mỗi nhóm 1–8 và loại A–I trong `scripts/incident-drill/catalog.json` có mục tương ứng ở file nhóm/file 17. Test KHÔNG kiểm nội dung khớp giữa tài liệu và `catalog.json`.
- **FR-011**: KHÔNG sửa `scripts/incident-drill/catalog.json`, `scripts/incident-drill.ps1`, code service, dashboard, rule cảnh báo hay hạ tầng; spec này chỉ thêm/sửa tài liệu, test tài liệu và tài liệu đi kèm.
- **FR-012**: PHẢI có tài liệu đi kèm theo nếp 027/028/031: PO, QA, Architect (tên đã chốt), 3 sơ đồ drawio, folder Postman `32 - Luyện troubleshoot`, cập nhật `technical-debt.md` (gồm rủi ro hai nơi lệch gợi ý, đóng thời hạn sai lệch Nguyên tắc III của 031), `QA_Debt.md` (mọi phát hiện), `functional-debt.md`; folder Postman gồm 8 subfolder theo nhóm 1–8 (request quan sát triệu chứng và xác nhận khỏi), sơ đồ theo Assumptions.
- **FR-013**: Lỗi do chạy thật để lấy số đo PHẢI tính vào ngân sách lỗi như sự cố thật; việc chạy stack và việc dọn Elastic sau đó PHẢI được hỏi lại người dùng trước khi thực hiện.
- **FR-014**: Tài liệu PHẢI viết bằng tiếng Việt có dấu, cùng văn phong và cấu trúc "Bạn sẽ thấy" như file 01–08.

### Key Entities *(include if feature involves data)*

- **Nhóm lỗi / loại lỗi**: 8 nhóm, 9 loại A–I trong `catalog.json` (đã có từ 031), là khóa phủ của test.
- **File hướng dẫn nhóm**: một file 09–16, chứa triệu chứng, bước kiểm, lệnh, khôi phục, xác nhận, bài tập, checklist.
- **Mục gợi ý theo triệu chứng**: một triệu chứng trong file 17 với ba mức mở dần.
- **Số đo bằng chứng**: giá trị đã đo thật (ngày, điều kiện, truy vấn) gắn với một hướng dẫn.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 8/8 nhóm có file hướng dẫn và mỗi hướng dẫn đã được chạy thật ít nhất một lần với số đo ghi lại; 100% truy vấn trong tài liệu trả kết quả đúng khi chạy lại trên stack có dữ liệu.
- **SC-002**: Người học làm theo một hướng dẫn bất kỳ từ lúc tiêm tới lúc xác nhận khỏi mà không cần xem tài liệu nào ngoài file đó và các liên kết trong file (kiểm bằng lần chạy thật của người viết ở SC-001).
- **SC-003**: 100% triệu chứng trong file 17 có đủ 3 mức; mức 1 và 2 không chứa tên service, tham số hay mã loại ở 100% kiểm tra thủ công.
- **SC-004**: Test tự động đạt khi tài liệu đủ phủ 8 nhóm/9 loại và 0 link nội bộ hỏng; fail khi thiếu nhóm/loại hoặc link hỏng (kiểm bằng một lần làm hỏng cố ý).
- **SC-005**: Không còn "Bộ 4 file" hay "cả 6 file" sai số trong `00-tong-quan-lo-trinh.md`; lộ trình liệt kê đủ 01–17.
- **SC-006**: `git diff` so với `master` không đổi gì ở `scripts/`, `services/`, `deploy/`, `docker/`, dashboard và rule (ngoài tài liệu, test tài liệu, Postman và tài liệu đi kèm).

## Assumptions

- Ràng buộc kế thừa: chỉ Docker Compose; không sửa code service; lỗi diễn tập tính vào ngân sách như thật (028/031).
- Dashboard Xử lý sự cố, rule phát hiện nhanh và rule ngân sách của 027–030, cùng `incident-drill.ps1` và `catalog.json` của 031 đã có và không đổi.
- Các tham số của từng nhóm theo 031: D 2000 ms và 5–50%; G `--cpus` 0.1 và `--memory` 256m; E 5 DB; F Authority sai; H tách khỏi network `backbone`; I `docker kill`.
- 3 sơ đồ drawio vẽ mới cho 032 (đã chốt ở `/speckit-clarify`).
- Folder Postman `32 - Luyện troubleshoot`: 8 subfolder nhóm 1–8 (đã chốt ở `/speckit-clarify`).
- Buổi người thật làm theo hướng dẫn là task mở, không chặn việc coi spec xong (đã chốt ở `/speckit-clarify`).
- Elastic sẽ được dọn sạch sau khi triển khai xong spec này; việc xoá PHẢI được hỏi lại trước khi thực hiện và nằm ngoài các nhiệm vụ của spec.
- Việc chạy stack để đo thật là phần của triển khai và PHẢI được người dùng đồng ý lúc đó.
- Hai nơi nội dung gợi ý (tài liệu và `catalog.json`) được viết riêng và có thể lệch; chấp nhận và ghi vào `technical-debt.md`.
- Số đo phụ thuộc máy; ghi kèm ngày và điều kiện, kèm cách tự đo lại.
- Tên file PO/QA/Architect và folder Postman đã được người dùng đồng ý theo đề xuất (vốn là ví dụ).
