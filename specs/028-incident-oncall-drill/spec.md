# Feature Specification: Diễn tập sự cố thật và phản ứng on-call (tiêm lỗi mù, phát hiện, xử lý, xác nhận khôi phục bằng telemetry)

**Feature Branch**: `claude/scrum-36-backlog-export-ac1561`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Jira SCRUM-36 [OPERATE-5] Trigger a real incident and run on-call response — As the SRE-hat-wearer, I want to deliberately trigger an incident (bug or outage injection) and run a genuine on-call response so that incident response is rehearsed under real conditions, not just described. Acceptance Criteria: (1) Given the incident is triggered, when the alert fires, then I follow a real triage flow: detect, assess severity, communicate status, mitigate. (2) Given the incident is active, when I respond, then I produce a timeline of what was observed and done, with timestamps. (3) Given the incident is resolved, when I check system state, then service is confirmed restored via telemetry, not just \"it looks fine.\" Test Scenarios: 1. Inject a real failure (e.g., corrupt config, exhaust a connection pool) without pre-announcing the exact cause to future-me. 2. Time from alert-fire to first mitigation action — record it as a baseline metric. 3. Confirm the incident timeline captures detection time, mitigation time, and resolution time distinctly."

## Clarifications

### Session 2026-10-01

- Q: Sự cố được tiêm vào bằng loại hỏng hóc nào? → A: Cả bốn loại — hỏng cấu hình, cạn connection pool, lỗi 5xx bằng cơ chế tiêm lỗi của đặc tả 027, và bug thật trong code.
- Q: Làm sao để không báo trước nguyên nhân cho chính người vận hành? → A: Một script chọn ngẫu nhiên kịch bản, service và thời điểm tiêm; lựa chọn được niêm phong cho tới khi kết thúc sự cố.
- Q: Sự cố được tiêm vào service nào? → A: Ngẫu nhiên trong 7 service đã khai báo SLO.
- Q: Diễn tập chạy trên môi trường nào? → A: Docker Compose local.
- Q: Cảnh báo nào dùng để phát hiện sự cố? → A: Thêm một rule phát hiện nhanh mới; giữ nguyên 4 rule ngân sách lỗi của đặc tả 027.
- Q: Loại "bug thật trong code" không sinh 5xx thì phát hiện thế nào? → A: Bug được chọn phải có biểu hiện là 5xx hoặc độ trễ vượt ngưỡng.
- Q: Lỗi do diễn tập có được tính vào ngân sách lỗi tháng không? → A: Có, tính như sự cố thật; nếu cạn ngân sách thì áp dụng đúng chính sách của đặc tả 027 (kể cả đóng băng tính năng).
  - *Thay bởi spec 029 (2026-10-05)*: ngân sách lỗi tính theo tuần lịch giờ Việt Nam; lỗi diễn tập vẫn tính như sự cố thật — xem [`specs/029-error-budget-weekly/spec.md`](../029-error-budget-weekly/spec.md).
- Q: Phân loại mức độ nghiêm trọng thế nào? → A: Ba mức — SEV1: luồng đặt hàng hỏng hoàn toàn; SEV2: một chức năng giảm cấp rõ rệt; SEV3: ảnh hưởng nhỏ hoặc có cách vòng.
- Q: Rule phát hiện nhanh bắn khi nào? → A: Khi tỷ lệ 5xx ≥ 0.1% hoặc p95/p99 vượt ngưỡng đã khai báo của service, đo trên cửa sổ 5 phút gần nhất.
  - *Thay bởi spec 029 (2026-10-05)*: SLO 5xx đổi thành dưới 1% (hiến chương 2.0.0), rule bắn khi tỷ lệ 5xx ≥ 1%; ngưỡng độ trễ giữ nguyên.
- Q: Bước thông báo trạng thái sự cố thực hiện ở đâu? → A: Kibana Cases — mở một Case cho sự cố, cập nhật trạng thái bằng comment.
- Q: Dòng thời gian sự cố lưu ở đâu? → A: Dùng chung thư mục kết quả diễn tập chaos (`docs/dien-tap-chaos-engineering/ket-qua/`) với mẫu mở rộng.
- Q: Tiêu chí xác nhận khôi phục bằng telemetry? → A: 5xx và độ trễ của service quay về trong ngưỡng SLO liên tục 15 phút, và rule phát hiện nhanh hết hoạt động.
- Q: Dòng thời gian bắt buộc ghi những mốc nào? → A: Thời điểm tiêm lỗi, thời điểm alert bắn, thời điểm xác định severity, thời điểm xác định nguyên nhân (ngoài các mốc phát hiện, giảm thiểu, giải quyết). Ban đầu người dùng nêu "thời điểm merge PR vào master được tính là resolved" — đã được thay bằng câu trả lời về ba mốc bên dưới.
- Q: Chỉ số baseline (thời gian từ alert bắn tới hành động giảm thiểu đầu tiên) ghi ở đâu? → A: Trong Kibana Case của sự cố.
- Q: Traffic trong lúc diễn tập đến từ đâu? → A: Dùng lại công cụ tạo tải của đặc tả 026.
- Q: Ranh giới với SCRUM-37? → A: Postmortem không đổ lỗi và ticket follow-up nằm ngoài phạm vi; thuộc SCRUM-37.
- Q: Ba mốc phát hiện / giảm thiểu / giải quyết được phân biệt thế nào? → A: Giảm thiểu = thời điểm merge PR vào master (hành động giảm thiểu đầu tiên chính là PR sửa lỗi, không có giảm thiểu tạm thời); Giải quyết = thời điểm telemetry xác nhận đạt SLO liên tục 15 phút trên bản đã merge.
- Q: Với lỗi tiêm bằng cấu hình (không nằm trong code), PR để merge là gì? → A: Một PR phòng ngừa tái diễn (ví dụ kiểm tra cấu hình khi khởi động, giới hạn pool).
- Q: Sau khi merge, hệ thống được đưa về trạng thái không lỗi thế nào? → A: Người vận hành pull master, build và chạy lại service bị ảnh hưởng; cấu hình tiêm lỗi được gỡ trong lúc đó.
- Q: Rule phát hiện nhanh có hiển thị trên dashboard SLO hằng ngày không? → A: Có, thêm panel.
- Q: Các cơ chế tiêm lỗi mới có chặn bằng cờ không? → A: Dùng chung cờ `Chaos:AllowFaultInjection` của đặc tả 027 (mặc định tắt).
- Q: Khoảng thời gian chọn ngẫu nhiên thời điểm tiêm? → A: Trong 0–30 phút kể từ lúc khởi chạy script.
- Q: Lựa chọn niêm phong được tiết lộ khi nào? → A: Chỉ sau khi sự cố đã được giải quyết (đạt SLO 15 phút).
- Q: Trạng thái trên Kibana Case cập nhật khi nào? → A: Tại mỗi mốc của dòng thời gian, cộng thêm cập nhật định kỳ trong lúc sự cố còn mở (phương án đã chọn nêu ví dụ "mỗi 30 phút").
- Q (phiên `/speckit-plan`): Tải nền chạy bằng gì, khi bài NBomber của 026 hiện luôn nhận 401? → A: Chạy folder Postman 26 (có lấy token) bằng newman.
- Q (phiên `/speckit-plan`): Parties và identity không nằm trên luồng tải của 026, xử lý thế nào? → A: Thêm traffic cho hai service này để cả 7 service đều có traffic; giữ "ngẫu nhiên trong 7".
- Q (phiên `/speckit-plan`): Script kích hoạt lỗi thế nào? → A: Tạo lại container kèm biến môi trường; để không lộ service qua thời gian chạy container, tạo lại cả 7 container, chỉ container đích nhận cấu hình sai.
- Q (phiên `/speckit-plan`): Có thêm code vào service không? → A: Không chỉnh code; chỉ bật/tắt cấu hình và truyền tham số sai. Kéo theo:
  - "Hỏng cấu hình" = đích kết nối sai: connection string sai với 5 service có DB, địa chỉ một service hạ lưu sai với BFF, đích cluster BFF sai với gateway.
  - "Cạn connection pool" = pool rất nhỏ: `Max Pool Size` nhỏ trong connection string với 5 service có DB, giới hạn kết nối của reverse proxy với gateway; BFF không có kịch bản này (không có tham số cấu hình).
  - "Lỗi 5xx của 027" = script tự gửi request có header `X-Chaos-Fault: 5xx` vào service đích, với tỷ lệ chọn ngẫu nhiên 5–50%.
  - "Bug thật trong code" được thay bằng một nhóm cấu hình sai khác gây 5xx/trễ (người lập kế hoạch đề xuất, người dùng duyệt ở phiên `/speckit-tasks`).
  - Cờ `Chaos:AllowFaultInjection` chặn ở script (script từ chối chạy nếu cờ trong `.env` không bật); middleware 027 vẫn tự kiểm tra cờ của nó.
- Q (phiên `/speckit-plan`): Rule phát hiện nhanh chạy bao lâu một lần? → A: 5 phút, giống 4 rule của 027.
- Q (phiên `/speckit-plan`): Kibana Case tạo thế nào? → A: Người vận hành tạo tay trên UI khi nhận cảnh báo.
- Q (phiên `/speckit-plan`): Niêm phong thế nào? → A: Ghi lựa chọn vào một file bị gitignore; script chỉ in ra mã băm SHA-256 của nội dung (dán vào Case) để chứng minh không sửa sau; mở bằng lệnh reveal.
- Q (phiên `/speckit-plan`): Script viết bằng gì? → A: Chỉ PowerShell (`.ps1`).
- Q (phiên `/speckit-plan`): Có viết test không? → A: Không viết test cho đặc tả này — "đây là diễn tập, không có code mới nên không cần test"; hạn của sai lệch: tới khi SCRUM-37 xong.
- Q (phiên `/speckit-plan`): Cơ chế tiêm lỗi có ngày gỡ không (Nguyên tắc X)? → A: Không — công cụ SRE lâu dài như 025/027, người sở hữu `owner-devops`.
- Q (phiên `/speckit-tasks`): Duyệt các đề xuất của plan? → A: Duyệt tên host sai (`incident-missing-db`, `http://incident-missing-host:8080`), con số pool (`Max Pool Size=1`; `MaxConnectionsPerServer=1` cho gateway), route nhận header của loại C, và folder Postman 28 gọi parties bằng guid ngẫu nhiên (kỳ vọng `404`). **Bỏ loại "nhóm cấu hình sai khác"** — chỉ còn ba loại: đích kết nối sai, cạn connection pool, lỗi 5xx của 027.
- Q (phiên `/speckit-tasks`): Chu kỳ cập nhật định kỳ trên Kibana Case? → A: 30 phút (xác nhận).
- Q (phiên `/speckit-tasks`): Script có chế độ chỉ định sẵn service + loại lỗi để xác minh/QA không? → A: Có, tham số riêng; khi dùng in rõ là chế độ không mù; buổi diễn tập thật không dùng.
- Q (phiên `/speckit-implement`, sau khi đo mức nền): Identity luôn vượt SLO vì tải nền lấy token mỗi vòng → A: Lấy token mỗi 30 phút.
- Q (phiên `/speckit-implement`): Gateway luôn vượt ngưỡng độ trễ (ngưỡng gateway 150/500 ms chặt hơn BFF 300/800 ms mà nó chuyển tiếp tới) → A: Rule phát hiện nhanh xét gateway chỉ theo 5xx.
- Q (phiên `/speckit-implement`): Tải nền làm nhiều service vượt SLO dù không có sự cố → A: Giảm tải nền (nghỉ 1000 ms giữa các request) rồi đo lại.
- Q (phiên `/speckit-implement`): "Cạn pool" bắn ở gateway nhưng không bắn ở service có DB → A: Loại cạn pool chỉ áp dụng cho gateway.
- Q (phiên `/speckit-implement`): Tạo lại cả 7 container gây độ trễ khởi động nguội ở mọi service → A: Giữ; quy trình triage ghi rõ alert thoáng qua do khởi động nguội là nhiễu.
- Q (phiên `/speckit-implement`): Nhiễu khởi động nguội đo thật kéo dài 5–7 phút (tới 2 lần chạy rule) — tiêu chí phân biệt? → A: Giữ tiêu chí: alert active qua ≥ 2 lần chạy rule là sự cố; chấp nhận đôi khi nhiễu bị tính là sự cố (giới hạn đã biết).
- Q (phiên `/speckit-implement`): Sau khi tạo lại identity, token cũ của tải nền hỏng (401, hoặc 502/504 qua BFF) → A: Tải nền tự lấy token lại khi gặp 401 hoặc khi container identity được tạo lại.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Tiêm một sự cố thật mà người vận hành không biết trước nguyên nhân (Priority: P1)

Là người đóng vai SRE, tôi muốn khởi chạy một script tự chọn ngẫu nhiên loại hỏng hóc, service bị ảnh hưởng và thời điểm tiêm, rồi niêm phong lựa chọn đó, để khi sự cố xảy ra tôi phải phản ứng như với một sự cố thật chứ không phải diễn lại một kịch bản đã biết trước.

**Why this priority**: Không có sự cố thật thì không có gì để phát hiện, xử lý hay ghi lại. Việc không biết trước nguyên nhân là điều kiện để buổi diễn tập là "điều kiện thật" theo đúng mục tiêu của Jira.

**Independent Test**: Bật cờ tiêm lỗi, chạy tải nền (folder Postman 26 qua newman, cộng traffic parties/identity), khởi chạy script; xác nhận trong vòng 30 phút một service trong 7 service bắt đầu trả 5xx hoặc trễ vượt ngưỡng, trong khi người vận hành không đọc được lựa chọn của script cho tới khi sự cố được giải quyết.

**Acceptance Scenarios**:

1. **Given** cờ `Chaos:AllowFaultInjection` đang bật và tải nền đang chạy, **When** tôi khởi chạy script diễn tập, **Then** script chọn ngẫu nhiên một loại hỏng hóc áp dụng được (đích kết nối sai, cạn connection pool, lỗi 5xx của đặc tả 027), một trong 7 service và một thời điểm trong 0–30 phút, rồi tiêm đúng lựa chọn đó vào thời điểm đã chọn chỉ bằng cấu hình/tham số sai.
2. **Given** script đã chọn xong, **When** tôi xem đầu ra của script trong lúc sự cố còn mở, **Then** đầu ra không tiết lộ loại hỏng hóc, service hay thời điểm tiêm; lựa chọn được niêm phong và ghi lại kèm thời điểm tiêm thực tế.
3. **Given** cờ `Chaos:AllowFaultInjection` đang tắt (mặc định), **When** tôi khởi chạy script, **Then** script từ chối chạy, không có lỗi nào được tiêm và mọi service phản hồi như bình thường.
4. **Given** sự cố đã được giải quyết, **When** tôi mở lựa chọn đã niêm phong, **Then** tôi đọc được loại hỏng hóc, service và thời điểm tiêm thực tế để đối chiếu với nguyên nhân tôi tự tìm ra.

---

### User Story 2 - Cảnh báo phát hiện nhanh bắn và người vận hành đi đúng quy trình triage (Priority: P1)

Là người đóng vai SRE, tôi muốn một cảnh báo phát hiện nhanh bắn ngay khi service vượt SLO trong 5 phút gần nhất, và từ đó tôi đi theo một quy trình triage thật: phát hiện, đánh giá mức độ nghiêm trọng, thông báo trạng thái trên Kibana Case, và giảm thiểu bằng một PR được merge vào master.

**Why this priority**: Đây chính là tiêu chí chấp nhận 1 của Jira. Bốn rule ngân sách lỗi của đặc tả 027 đo tiêu hao theo tuần nên không bắn kịp cho một sự cố đơn lẻ; cần một tín hiệu phát hiện nhanh để buổi diễn tập bắt đầu từ cảnh báo chứ không phải từ việc tự soi dashboard.

**Independent Test**: Tiêm một lỗi đã biết (ngoài buổi diễn tập mù) vào một service; xác nhận rule phát hiện nhanh bắn trong 5 phút, hiện trên panel mới của dashboard SLO hằng ngày; mở Kibana Case, ghi severity, các cập nhật trạng thái và hành động giảm thiểu.

**Acceptance Scenarios**:

1. **Given** sự cố đã được tiêm và tải nền đang chạy, **When** tỷ lệ 5xx của service ≥ 1% hoặc p95/p99 vượt ngưỡng đã khai báo của chính service đó trong cửa sổ 5 phút gần nhất, **Then** rule phát hiện nhanh bắn cho đúng service đó (riêng gateway chỉ xét tỷ lệ 5xx).
2. **Given** rule phát hiện nhanh đang hoạt động, **When** tôi mở dashboard SLO hằng ngày, **Then** panel phát hiện nhanh cho biết service nào đang vượt SLO trong 5 phút gần nhất.
3. **Given** cảnh báo đã bắn, **When** tôi bắt đầu triage, **Then** tôi mở một Kibana Case cho sự cố và ghi một comment tại mỗi mốc: phát hiện, xác định severity (SEV1/SEV2/SEV3 theo tiêu chí đã định nghĩa), xác định nguyên nhân, giảm thiểu, giải quyết; cộng thêm cập nhật định kỳ trong lúc sự cố còn mở.
4. **Given** tôi đã tìm ra nguyên nhân, **When** tôi giảm thiểu, **Then** hành động giảm thiểu đầu tiên là merge vào master một PR phòng ngừa tái diễn, rồi pull master, build và chạy lại service bị ảnh hưởng với cấu hình tiêm lỗi đã gỡ.
5. **Given** sự cố đang hoạt động, **When** tôi ghi Kibana Case, **Then** Case ghi chỉ số baseline "thời gian từ lúc alert bắn tới hành động giảm thiểu đầu tiên (merge PR)".

---

### User Story 3 - Dòng thời gian sự cố có timestamp với các mốc tách bạch (Priority: P1)

Là người đóng vai SRE, tôi muốn một bản ghi sự cố trong thư mục kết quả diễn tập chaos, theo một mẫu mở rộng bắt buộc đủ trường, ghi lại những gì đã quan sát và đã làm kèm timestamp, với các mốc tiêm lỗi, alert bắn, phát hiện, xác định severity, xác định nguyên nhân, giảm thiểu và giải quyết tách bạch nhau.

**Why this priority**: Đây là tiêu chí chấp nhận 2 và kịch bản kiểm thử 3 của Jira. Không có dòng thời gian thì không đo được baseline và SCRUM-37 (postmortem) không có đầu vào.

**Independent Test**: Sau một buổi diễn tập, mở bản ghi sự cố; xác nhận đủ các mốc bắt buộc, mỗi mốc có timestamp riêng, và mốc phát hiện, giảm thiểu, giải quyết là ba thời điểm phân biệt.

**Acceptance Scenarios**:

1. **Given** sự cố đang hoạt động, **When** tôi phản ứng, **Then** tôi ghi vào bản ghi sự cố những gì đã quan sát và đã làm, mỗi dòng có timestamp.
2. **Given** sự cố đã được giải quyết, **When** tôi rà soát bản ghi, **Then** bản ghi có đủ các mốc: tiêm lỗi (lấy từ lựa chọn niêm phong), alert bắn (lấy từ lịch sử rule), phát hiện, xác định severity, xác định nguyên nhân, giảm thiểu (merge PR vào master), giải quyết (đạt SLO liên tục 15 phút).
3. **Given** bản ghi đã đủ trường, **When** tôi so sánh các mốc, **Then** phát hiện, giảm thiểu và giải quyết là ba timestamp phân biệt, theo đúng thứ tự thời gian.
4. **Given** bản ghi sự cố mới, **When** tôi lưu nó, **Then** nó nằm trong thư mục kết quả diễn tập chaos và được thêm vào lịch sử chạy của thư mục đó, cùng chỗ với các bản ghi của đặc tả 025.

---

### User Story 4 - Xác nhận khôi phục bằng telemetry, không phải "trông có vẻ ổn" (Priority: P2)

Là người đóng vai SRE, tôi muốn chỉ đóng sự cố khi telemetry cho thấy service đạt SLO liên tục 15 phút và rule phát hiện nhanh hết hoạt động, để việc khôi phục là bằng chứng đo được.

**Why this priority**: Đây là tiêu chí chấp nhận 3 của Jira. Nó phụ thuộc vào việc đã giảm thiểu (User Story 2) và đã có bản ghi (User Story 3).

**Independent Test**: Sau khi chạy lại service từ master, theo dõi dashboard SLO hằng ngày; xác nhận chỉ ghi mốc giải quyết khi 5xx và độ trễ trong ngưỡng liên tục 15 phút và rule phát hiện nhanh đã hết hoạt động, kèm bằng chứng telemetry trong bản ghi.

**Acceptance Scenarios**:

1. **Given** PR đã được merge và service đã chạy lại từ master, **When** 5xx và độ trễ của service quay về trong ngưỡng SLO liên tục 15 phút và rule phát hiện nhanh không còn hoạt động, **Then** tôi ghi mốc giải quyết kèm bằng chứng telemetry (ảnh chụp hoặc link Kibana).
2. **Given** service chưa đạt SLO đủ 15 phút liên tục, **When** tôi kiểm tra trạng thái, **Then** sự cố chưa được coi là giải quyết, dù service "trông có vẻ ổn".
3. **Given** sự cố đã giải quyết, **When** tôi đóng Kibana Case, **Then** lựa chọn niêm phong mới được mở để đối chiếu.

---

### Edge Cases

- Không có traffic (tải nền dừng hoặc chưa chạy): rule phát hiện nhanh không có dữ liệu để đánh giá; không được coi thiếu dữ liệu là đạt SLO khi xác nhận khôi phục, và không được bắn cảnh báo chỉ vì thiếu dữ liệu.
- Script chọn tiêm lỗi vào gateway hoặc BFF: sự cố lan sang mọi luồng đi qua đó; severity vẫn được đánh giá theo tiêu chí SEV1–SEV3 đã định nghĩa.
- "Cạn connection pool" chỉ áp dụng cho gateway; script chọn service khác thì chỉ bốc trong "đích kết nối sai" và "lỗi 5xx của 027".
- Tạo lại cả 7 container ở thời điểm tiêm gây một khoảng gián đoạn ngắn trên mọi service; đây là nhiễu có chủ đích để che service đích, và cũng tiêu hao ngân sách lỗi của mọi service.
- Hỏng hóc chỉ làm sai dữ liệu nghiệp vụ mà không sinh 5xx/trễ nằm ngoài phạm vi.
- Sự cố diễn tập tiêu hao ngân sách lỗi tuần như sự cố thật; nếu một ngân sách đạt 100%, service vào trạng thái "cạn ngân sách — ưu tiên độ tin cậy" theo chính sách của đặc tả 027 và chỉ thoát khi đạt điều kiện hồi phục 3 ngày của đặc tả đó.
- Cờ `Chaos:AllowFaultInjection` tắt: script không tiêm được gì; đây không phải một sự cố và không tạo bản ghi sự cố.
- Rule phát hiện nhanh bắn đồng thời với các rule mốc ngân sách của đặc tả 027: chấp nhận được, hai loại cảnh báo phục vụ mục đích khác nhau.
- Người vận hành bế tắc, không tìm ra nguyên nhân: lựa chọn niêm phong vẫn chỉ được mở sau khi đã giải quyết (theo câu trả lời đã chốt).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: PHẢI có một script diễn tập (PowerShell) chọn ngẫu nhiên: (a) một loại hỏng hóc áp dụng được cho service đích, trong ba loại — đích kết nối sai, cạn connection pool (chỉ áp dụng cho gateway), lỗi 5xx bằng cơ chế tiêm lỗi của đặc tả 027 (tỷ lệ ngẫu nhiên 5–50%); (b) một trong 7 service đã khai báo SLO; (c) một thời điểm tiêm trong khoảng 0–30 phút kể từ lúc khởi chạy. Mọi hỏng hóc được tạo chỉ bằng cấu hình hoặc tham số sai, KHÔNG sửa code của service.
- **FR-002**: Script PHẢI niêm phong lựa chọn (loại hỏng hóc, service, thời điểm tiêm thực tế, tỷ lệ 5xx nếu có) vào một file không được commit, chỉ in ra mã băm SHA-256 của nội dung đó; lựa chọn chỉ được mở (lệnh reveal) sau khi sự cố đã được giải quyết.
- **FR-003**: Script PHẢI từ chối chạy khi cờ `Chaos:AllowFaultInjection` (biến `CHAOS_ALLOW_FAULT_INJECTION` trong `.env`) không bật; khi cờ tắt, KHÔNG có lỗi nào được tiêm. Lúc tiêm, script PHẢI tạo lại cả 7 container service, chỉ container đích nhận cấu hình sai.
- **FR-004**: Diễn tập PHẢI chạy được trên Docker Compose local, với traffic nền chạy bằng folder Postman 26 qua newman, cộng thêm traffic tới parties và identity để cả 7 service đều có traffic; token chỉ được lấy lại mỗi 30 phút.
- **FR-005**: PHẢI có một rule phát hiện nhanh trong Kibana, bắn cho từng service khi tỷ lệ 5xx ≥ 1% hoặc p95/p99 vượt ngưỡng đã khai báo của chính service đó (kể cả ngoại lệ có lý do), đo trên cửa sổ 5 phút gần nhất; riêng gateway chỉ xét tỷ lệ 5xx (người dùng chốt, vì ngưỡng độ trễ của gateway chặt hơn BFF mà nó chuyển tiếp tới); 4 rule ngân sách lỗi của đặc tả 027 giữ nguyên.
- **FR-006**: Dashboard SLO hằng ngày PHẢI có panel thể hiện trạng thái rule phát hiện nhanh (service nào đang vượt SLO trong 5 phút gần nhất).
- **FR-007**: PHẢI có quy trình triage được viết ra gồm các bước: phát hiện, đánh giá severity, thông báo trạng thái, giảm thiểu; với tiêu chí severity: SEV1 — luồng đặt hàng hỏng hoàn toàn; SEV2 — một chức năng giảm cấp rõ rệt; SEV3 — ảnh hưởng nhỏ hoặc có cách vòng.
- **FR-008**: Thông báo trạng thái PHẢI được thực hiện trên một Kibana Case mở cho sự cố, với một comment tại mỗi mốc của dòng thời gian và các cập nhật định kỳ trong lúc sự cố còn mở.
- **FR-009**: Kibana Case PHẢI ghi chỉ số baseline "thời gian từ lúc alert bắn tới hành động giảm thiểu đầu tiên".
- **FR-010**: Hành động giảm thiểu đầu tiên PHẢI là merge vào master một PR phòng ngừa tái diễn; sau đó người vận hành pull master, build và chạy lại service bị ảnh hưởng với cấu hình tiêm lỗi đã gỡ.
- **FR-011**: PHẢI có một mẫu bản ghi sự cố mở rộng từ mẫu kết quả diễn tập chaos của đặc tả 025, đặt trong cùng thư mục, bắt buộc đủ trường; bản ghi của mỗi lần diễn tập PHẢI được lưu trong thư mục kết quả đó và thêm vào lịch sử chạy.
- **FR-012**: Bản ghi sự cố PHẢI chứa dòng thời gian những gì đã quan sát và đã làm, mỗi dòng có timestamp, và PHẢI có đủ các mốc riêng: tiêm lỗi, alert bắn, phát hiện, xác định severity, xác định nguyên nhân, giảm thiểu (merge PR vào master), giải quyết (đạt SLO liên tục 15 phút).
- **FR-013**: Mốc giải quyết CHỈ được ghi khi telemetry cho thấy 5xx và độ trễ của service bị ảnh hưởng nằm trong ngưỡng SLO liên tục 15 phút và rule phát hiện nhanh không còn hoạt động; bản ghi PHẢI đính kèm bằng chứng telemetry.
- **FR-014**: Lỗi do diễn tập gây ra PHẢI được tính vào ngân sách lỗi tuần như sự cố thật; KHÔNG được loại trừ khỏi phép tính ngân sách của đặc tả 027.
- **FR-015**: Khi cờ tiêm lỗi tắt, các thay đổi của tính năng này KHÔNG được thay đổi hành vi phản hồi hiện có của bất kỳ endpoint nào.

### Key Entities *(include if feature involves data)*

- **Lựa chọn niêm phong**: loại hỏng hóc, service bị tiêm, thời điểm tiêm thực tế; ẩn với người vận hành cho tới khi sự cố được giải quyết.
- **Kịch bản hỏng hóc**: một trong ba loại (đích kết nối sai, cạn connection pool, lỗi 5xx của đặc tả 027); chỉ bằng cấu hình/tham số; script chặn bằng cờ `Chaos:AllowFaultInjection`.
- **Cảnh báo phát hiện nhanh**: gắn với một service; hoạt động khi service vượt SLO (5xx hoặc p95/p99) trong 5 phút gần nhất.
- **Sự cố (Kibana Case)**: mở khi cảnh báo bắn; có severity SEV1–SEV3, các comment trạng thái tại mỗi mốc và định kỳ, và chỉ số baseline.
- **Bản ghi sự cố**: file trong thư mục kết quả diễn tập chaos; chứa dòng thời gian có timestamp với các mốc bắt buộc và bằng chứng khôi phục.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Trong một buổi diễn tập, người vận hành không biết loại hỏng hóc, service và thời điểm tiêm cho tới khi sự cố được giải quyết; việc này được xác nhận bằng cách đối chiếu nguyên nhân tự tìm ra với lựa chọn niêm phong.
- **SC-002**: Cảnh báo phát hiện nhanh bắn trong vòng 5 phút (cộng một chu kỳ đánh giá rule) kể từ khi service bị tiêm lỗi bắt đầu vượt SLO; sự cố được phát hiện nhờ cảnh báo, không phải nhờ tự mở dashboard.
- **SC-003**: 100% bản ghi sự cố có đủ các mốc bắt buộc, mỗi mốc có timestamp, và phát hiện, giảm thiểu, giải quyết là ba thời điểm phân biệt.
- **SC-004**: Mỗi buổi diễn tập ghi được một giá trị baseline "thời gian từ alert bắn tới hành động giảm thiểu đầu tiên" trong Kibana Case.
- **SC-005**: Không sự cố nào được đóng khi chưa có bằng chứng telemetry cho thấy service đạt SLO liên tục 15 phút.
- **SC-006**: Khi cờ tiêm lỗi tắt, không có cảnh báo phát hiện nhanh nào bắn do tính năng này và mọi endpoint phản hồi như trước.

## Assumptions

- Telemetry (traces OTel qua Elasticsearch), dashboard SLO hằng ngày trên Kibana, 4 rule ngân sách lỗi và cơ chế tiêm lỗi 5xx (`X-Chaos-Fault`, cờ `Chaos:AllowFaultInjection`) đã có từ các đặc tả 021 và 027; tính năng này xây trên nền đó.
- Ngưỡng SLO của 7 service (kể cả ngoại lệ độ trễ của BFF) đã khai báo trong manifest theo đặc tả 021; tính năng này không thay đổi các ngưỡng đó.
- Folder Postman 26 (có bước lấy token) chạy được bằng newman trên Docker Compose local và, cùng traffic bổ sung cho parties/identity, tạo đủ traffic để rule phát hiện nhanh có dữ liệu đánh giá. Bài NBomber của 026 không dùng được vì luôn nhận 401 (QA_Debt mục 026).
- "Người vận hành" là một người duy nhất đóng vai SRE/Dev (theo `docs/roadmap.md`); quy trình triage và cập nhật Kibana Case là quy trình con người, không bắt buộc tự động hóa.
- Chu kỳ cập nhật định kỳ trên Kibana Case là mỗi 30 phút (người dùng xác nhận ở phiên `/speckit-tasks`).
- Postmortem không đổ lỗi và ticket follow-up nằm ngoài phạm vi; thuộc SCRUM-37.
- Đẩy cảnh báo ra kênh bên ngoài (email, Slack, hệ thống paging) nằm ngoài phạm vi; giữ nhất quán với đặc tả 027.
