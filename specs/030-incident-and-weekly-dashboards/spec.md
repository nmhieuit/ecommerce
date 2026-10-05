# Feature Specification: Tách dashboard SLO thành "Xử lý sự cố" và "Ngân sách lỗi tuần"

**Feature Branch**: `feature/030-incident-and-weekly-dashboards`

**Created**: 2026-10-05

**Status**: Draft

**Input**: User description: "Tách dashboard Kibana 'SLO vận hành hằng ngày — 7 service' thành 2 dashboard: (1) Xử lý sự cố — mọi panel theo thanh thời gian, phục vụ troubleshoot lỗi đang diễn ra; (2) Ngân sách lỗi tuần — cố định theo tuần lịch giờ Việt Nam để biết hạn mức SLO còn lại. Bỏ dashboard cũ. (Spec B trong đợt rà soát nợ kỹ thuật; phụ thuộc spec A — ngân sách lỗi theo tuần lịch.) [...] LUÔN HỎI LẠI, KHÔNG SUY DIỄN [...] Không suy diễn những điểm trên; mọi câu trả lời ghi vào mục Clarifications của spec.md."

## Clarifications

### Đã chốt trước trong phiên rà soát nợ kỹ thuật (người dùng nêu sẵn trong mô tả, không hỏi lại)

- Dashboard cũ trộn ba loại cửa sổ thời gian trên cùng một màn hình (đã xác minh từng panel):
  - ngân sách: cửa sổ tuần lịch cố định (từ spec 029) và lọc cứng `NOW() - 15 minutes` ở các panel cảnh báo;
  - error-rate / p95 theo ngày: cứng `now-7d`;
  - bảng SLO và 4 panel đào sâu: theo thanh thời gian.
- Dashboard **Xử lý sự cố** (mọi panel theo thanh thời gian) gồm:
  - Bảng SLO + ô Markdown ngưỡng (panel 5–6 cũ);
  - panel đào sâu: `dotnet.exceptions`, phân bố status code, top endpoint chậm nhất (panel 9–11 cũ);
  - Phát hiện nhanh (028), **bỏ** lọc cứng `NOW() - 15 minutes` để đi theo thanh thời gian;
  - 5xx và p95 theo phút, theo service;
  - lỗi gọi hạ lưu (span Client lỗi/chậm theo cặp service gọi → đích);
  - traffic + 401/403 theo phút, theo service;
  - log lỗi gần nhất (logs mức Error, kèm trace id / correlation id).
- Dashboard **Ngân sách lỗi tuần** (cố định tuần lịch giờ Việt Nam, chỉ cửa sổ tuần lịch) gồm:
  - mức tiêu hao tuần (4 ngân sách × 7 service);
  - hạn mức còn lại (100% − mức tiêu hao, và số request xấu còn được phép);
  - cảnh báo mốc + cạn ngân sách theo tuần;
  - error-rate / p95 theo ngày trong tuần (thay panel 7–8 cũ);
  - tiêu hao lũy kế theo ngày từ thứ Hai.
- **Bỏ dashboard cũ** (id `e2e06ff5-9cdf-4bea-acc8-5fd60ce26170`). Cập nhật mọi tài liệu, quickstart, QA, contract đang trỏ tới id/tên cũ (021, 027, 028).
- Giữ cách lưu bằng file export ndjson trong repo, mỗi dashboard một file.
- Thêm test canh gác ngưỡng cho rule `incident-fast-detection` (đóng sai lệch Nguyên tắc III của 028) và giữ test 027 khớp chu kỳ tuần.
- Elastic: sẽ dọn toàn bộ sau khi triển khai xong, **phải hỏi lại người dùng trước khi xoá**. Tuần đầu sau khi dọn chưa đủ dữ liệu cho dashboard ngân sách.
- Spec A (spec 029, ngân sách lỗi theo tuần lịch) đã triển khai xong và đã vào `master` (PR #70): 56/56 task đã đánh dấu xong. Vì vậy spec này tiếp tục được.

### Session 2026-10-05 (phiên `/speckit-specify`)

Các giá trị người dùng chọn từ phương án gợi ý; những giá trị gợi ý nêu là "ví dụ" nay đã được người dùng xác nhận.

- Q: Tên 2 dashboard? → A: `Xử lý sự cố — 7 service` và `Ngân sách lỗi tuần — 7 service` (có hậu tố "— 7 service").
- Q: Id saved object? → A: Tạo id mới cho cả hai. Id cũ không giữ lại cho dashboard nào.
- Q: Khoảng thời gian mặc định của dashboard Xử lý sự cố? → A: 1 giờ gần nhất.
- Q: Tự làm mới (auto-refresh) của dashboard Xử lý sự cố? → A: Có, chu kỳ 1 phút.
- Q: Panel cũ "Tổng 401 + 403" (12) trùng với panel mới "Traffic + 401/403 theo phút"? → A: Bỏ panel cũ, giữ panel mới.
- Q: Panel "lỗi gọi hạ lưu" hiển thị thế nào? → A: Bảng tổng hợp theo cặp (service gọi → đích), 20 dòng. Bảng tổng hợp không có trace id nên **không** có link sang trace.
- Q: Panel "log lỗi gần nhất"? → A: 50 dòng, các cột: thời gian, service, message, trace id, correlation id. Mỗi dòng có link sang trace. (Ban đầu người dùng chọn Kibana APM/Trace; V5 lúc `/speckit-implement` cho thấy APM không đọc được dữ liệu trace OTel thô của dự án, người dùng chốt đổi sang link mở Discover lọc theo trace id.)
- Q: Dashboard Ngân sách tuần có cho xem tuần trước? → A: Có, chọn tuần bằng một điều khiển trên dashboard. Nếu điều khiển này không chạy được trên Kibana 9.4.4 khi kiểm chứng thì **dừng và hỏi lại người dùng**, không tự lùi sang phương án khác.
- Q: Dùng collapsible section để nhóm panel? → A: Dùng, luôn ở trạng thái mở (giới hạn đã biết ở file `06`: Kibana 9.4.4 không thật sự ẩn nội dung panel).
- Q: Dựng panel bằng gì? → A: Panel dạng bảng/danh sách dùng Discover session ES|QL (như 027/028, tạo qua Saved Objects API); panel biểu đồ dùng Lens.
- Q: Liên kết qua lại giữa 2 dashboard? → A: Có, một chạm hai chiều (mỗi dashboard có link sang dashboard còn lại).
- Q: Tài liệu đi kèm làm những gì? → A: Làm đủ cả bốn nhóm:
  - PO và `functional-debt.md`;
  - QA và `QA_Debt.md`;
  - Architect, `technical-debt.md` và 3 sơ đồ drawio;
  - folder Postman.
- Q: Tên file PO/QA/Architect? → A: `docs/PO/030_PO_hai dashboard xử lý sự cố và ngân sách tuần.md`; QA và Architect cùng phần tên: `docs/QA/030_QA_hai dashboard xử lý sự cố và ngân sách tuần.md`, `docs/architecture/030_Architect_hai dashboard xử lý sự cố và ngân sách tuần.md`.
- Q: Làm việc ở checkout/nhánh nào, có commit không? → A: Nhánh mới `feature/030-incident-and-weekly-dashboards` tạo trong worktree hiện tại (`split-slo-dashboard-8a1a85`, từ `master`). **Không commit**: người dùng tự xem và commit.
- Q: Tên thư mục spec? → A: `specs/030-incident-and-weekly-dashboards/`.
- Q: Khi chọn một tuần đã qua, panel cảnh báo mốc / cạn ngân sách (trạng thái hiện tại của rule) hiển thị thế nào? → A: Luôn là trạng thái hiện tại. Chỉ các panel tính từ traces (mức tiêu hao, hạn mức còn lại, theo ngày, lũy kế) đổi theo tuần đã chọn.
- Q: "Số request xấu còn được phép" tính thế nào? → A: Theo tổng request đã có tới hiện tại của tuần: tỷ lệ cho phép × tổng request tuần tới giờ − số request xấu đã có. Không dự phóng cả tuần.
- Q: "Log mức Error" gồm những mức nào? → A: Error trở lên (Error + Fatal), không gồm Warning.
- Q: Log hiện không có trường correlation id (phát hiện lúc `/speckit-plan`) — xử lý thế nào? → A: Ban đầu người dùng chọn sửa `ServiceDefaults`. Sau khi kiểm lại, `CorrelationIdMiddleware` đã đẩy `CorrelationId` vào logging scope (`IncludeScopes = true`), chỉ là Elasticsearch chưa có log nào nằm trong một request nên chưa biết trường có tới được Elasticsearch không. Người dùng chốt: **kiểm chứng trước** bằng một log Error trong request thật; chỉ khi thật sự thiếu mới sửa `ServiceDefaults` (test viết trước).
- Q: Thế nào là "chậm" ở span Client của panel lỗi gọi hạ lưu? → A: Vượt ngưỡng p95 đã khai báo của service đích (150ms, riêng `Bff.Api` 300ms).
- Q: Panel Links (tham chiếu dashboard đích bằng saved object) tạo vòng tham chiếu A↔B nên mỗi file export chứa cả hai dashboard; link ngoài tương đối bị Kibana vô hiệu. Link hai chiều làm thế nào? → A: Ô Markdown ở đầu mỗi dashboard chứa link `/app/dashboards#/view/<id dashboard kia>` theo id cố định; hai file ndjson độc lập (chọn từ phương án gợi ý lúc `/speckit-implement`).
- Q: Điều khiển chọn tuần lấy danh sách từ dữ liệu hay giá trị tương đối? → A: Giá trị tương đối `Tuần này / Tuần trước / 2 tuần trước / 3 tuần trước`, mặc định `Tuần này` (điều khiển lưu sẵn một giá trị mặc định cố định, danh sách theo dữ liệu sẽ mặc định vào một tuần cũ khi sang tuần mới). Người dùng đồng ý.
- Q: Cửa sổ thời gian cố định của mỗi panel dashboard Ngân sách tuần (để thoát thanh thời gian)? → A: 30 ngày (`now-30d`); vì vậy tuần xa nhất chọn được là 3 tuần trước.
- Q: Panel "Tiêu hao lũy kế theo ngày từ thứ Hai" hiển thị ngân sách nào? → A: Mức cao nhất trong 4 ngân sách, mỗi service một đường.
- Q: Folder Postman 30 chứa gì? → A: Request sinh lỗi và traffic cho dashboard Xử lý sự cố, cộng các truy vấn đọc ngân sách tuần để đối chiếu số dashboard với Elasticsearch. Tên folder và cách chia folder con sẽ hỏi lại ở `/speckit-tasks`, không suy diễn.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Dashboard Xử lý sự cố: một màn hình đi theo thanh thời gian để tìm nguyên nhân lỗi đang diễn ra (Priority: P1)

Là người đóng vai SRE đang trực, khi một service lỗi hoặc chậm, tôi muốn mở một dashboard mà **mọi** panel đều đi theo khoảng thời gian tôi chọn trên thanh thời gian. Từ đó tôi thu hẹp từ "service nào đang vượt SLO" xuống "lỗi gì, ở endpoint nào, do service hạ lưu nào, kèm trace nào" mà không phải nhớ panel nào bị cố định ở cửa sổ nào.

**Why this priority**: Đây là lý do chính của việc tách. Dashboard cũ trộn cửa sổ thời gian nên khi người trực chọn "15 phút gần nhất", một nửa số panel không đổi và gây hiểu nhầm đúng lúc xử lý sự cố.

**Independent Test**: Mở dashboard `Xử lý sự cố — 7 service`, đổi thanh thời gian giữa 15 phút, 1 giờ và 24 giờ. Xác nhận cả 100% panel đổi theo và không panel nào giữ cửa sổ cố định. Tiêm lỗi 5xx vào một service rồi xác nhận service đó hiện ở bảng SLO, ở panel Phát hiện nhanh, ở biểu đồ 5xx theo phút, và nhìn thấy log lỗi kèm trace id tương ứng.

**Acceptance Scenarios**:

1. **Given** dashboard Xử lý sự cố đang mở, **When** tôi đổi khoảng thời gian trên thanh thời gian, **Then** mọi panel (bảng SLO, ngưỡng, đào sâu, Phát hiện nhanh, 5xx và p95 theo phút, lỗi gọi hạ lưu, traffic + 401/403 theo phút, log lỗi) cùng hiển thị dữ liệu của khoảng đó; không panel nào còn dùng cửa sổ cố định như `now-7d` hay "15 phút gần nhất".
2. **Given** một service đang trả 5xx với tỷ lệ cao, **When** tôi mở dashboard với khoảng mặc định 1 giờ, **Then** tôi thấy service đó ở bảng SLO với số đo vượt ngưỡng, thấy nó trong panel Phát hiện nhanh nếu rule đang bắn, và thấy đường 5xx theo phút của chính service đó tăng lên.
3. **Given** một service gọi service khác (span Client) bị lỗi hoặc chậm, **When** tôi xem panel lỗi gọi hạ lưu, **Then** tôi thấy bảng tổng hợp theo cặp service gọi → đích, tối đa 20 dòng, đủ để biết cặp nào đang hỏng.
4. **Given** có log mức Error trong khoảng thời gian đã chọn, **When** tôi xem panel log lỗi gần nhất, **Then** tôi thấy tối đa 50 dòng mới nhất với thời gian, service, message, trace id, correlation id, và mỗi dòng có thể mở sang Discover lọc đúng trace id để xem mọi span của trace.
5. **Given** dashboard vừa được mở lần đầu, **When** tôi chưa chỉnh gì, **Then** khoảng thời gian là 1 giờ gần nhất và dashboard tự làm mới mỗi 1 phút.
6. **Given** panel cũ "Tổng 401 + 403", **When** spec này hoàn thành, **Then** panel đó không còn; thông tin 401/403 nằm trong panel "Traffic + 401/403 theo phút theo service".

---

### User Story 2 - Dashboard Ngân sách lỗi tuần: biết hạn mức SLO của tuần còn lại bao nhiêu (Priority: P1)

Là người đóng vai SRE hoặc PO, tôi muốn một dashboard cố định theo tuần lịch giờ Việt Nam (thứ Hai 00:00 → Chủ nhật 23:59, UTC+7) cho biết mỗi service đã tiêu bao nhiêu ngân sách, còn được phép bao nhiêu, và đã tiêu thế nào theo từng ngày. Con số không đổi khi tôi vô tình kéo thanh thời gian.

**Why this priority**: Đây là nửa còn lại của việc tách. Ngân sách là khái niệm theo tuần (spec 029); để chung với dashboard điều tra sự cố thì con số ngân sách bị lẫn với cửa sổ thời gian của người dùng.

**Independent Test**: Mở dashboard `Ngân sách lỗi tuần — 7 service`, đổi thanh thời gian ở nhiều khoảng khác nhau và xác nhận số liệu không đổi. Tạo lỗi tổng hợp cho một service và xác nhận mức tiêu hao, hạn mức còn lại, tiêu hao lũy kế theo ngày đều thay đổi nhất quán, và khớp với điều kiện làm cảnh báo mốc bắn.

**Acceptance Scenarios**:

1. **Given** dashboard Ngân sách tuần đang mở ở tuần hiện tại, **When** tôi đổi thanh thời gian bất kỳ, **Then** mọi số liệu ngân sách vẫn tính trên đúng cửa sổ tuần lịch giờ Việt Nam và không đổi theo thanh thời gian.
2. **Given** một service trong tuần hiện tại, **When** tôi xem mức tiêu hao, **Then** tôi thấy phần trăm ngân sách đã tiêu cho từng ngân sách trong 4 ngân sách (khả dụng, 5xx, p95, p99), cho cả 7 service.
3. **Given** cùng service đó, **When** tôi xem hạn mức còn lại, **Then** tôi thấy 100% trừ mức tiêu hao và số request xấu còn được phép trước khi ngân sách cạn.
4. **Given** cảnh báo mốc 50/75/100% đang bắn hoặc service đang ở trạng thái cạn ngân sách, **When** tôi xem dashboard, **Then** các cảnh báo và trạng thái cạn của tuần đó hiển thị ở panel riêng (theo cửa sổ tuần lịch, không theo "15 phút gần nhất").
5. **Given** tuần đang diễn ra, **When** tôi xem error-rate và p95, **Then** chúng hiển thị theo từng ngày trong tuần (thay cho "7 ngày gần nhất" cũ), và tiêu hao lũy kế theo ngày hiển thị từ thứ Hai tới hôm nay.
6. **Given** điều khiển chọn tuần trên dashboard, **When** tôi chọn một tuần trước, **Then** các panel áp dụng được hiển thị dữ liệu của tuần lịch đó; tuần nào cũng bắt đầu thứ Hai 00:00 và kết thúc Chủ nhật 23:59 giờ Việt Nam.
7. **Given** stack Elastic vừa được dọn sạch, **When** tuần đầu chưa đủ dữ liệu, **Then** panel hiển thị "không có dữ liệu" thay vì con số sai (giữ FR-013 của 029).

---

### User Story 3 - Bỏ dashboard cũ và mọi tài liệu, hợp đồng, test đang trỏ tới nó nói đúng hai dashboard mới (Priority: P2)

Là người đọc tài liệu và người chạy QA, tôi muốn dashboard cũ biến mất hẳn và mọi tài liệu, quickstart, QA, contract của 021, 027, 028 trỏ tới đúng dashboard mới. Tôi cũng muốn hai dashboard có link qua lại. Có như vậy thì không ai mở nhầm id/tên đã bỏ hay làm theo hướng dẫn đã lỗi thời.

**Why this priority**: Tách xong mà để dashboard cũ hay tài liệu cũ thì hai nơi cùng nói về một việc và sẽ lệch nhau. Nhưng việc này chỉ có nghĩa khi hai dashboard mới đã có (US1, US2).

**Independent Test**: Tìm trên toàn repo id `e2e06ff5-…` và tên "SLO vận hành hằng ngày — 7 service". Xác nhận không còn trong hiện vật và tài liệu đang dùng (trừ bản ghi lịch sử). Import hai file ndjson vào một Kibana sạch và xác nhận dashboard cũ không tồn tại, hai dashboard mới mở được và link qua lại hoạt động.

**Acceptance Scenarios**:

1. **Given** repo sau khi hoàn thành, **When** tôi tìm id và tên dashboard cũ, **Then** không còn tham chiếu nào trong hiện vật và tài liệu đang dùng của 021, 027, 028, 029 và các file Kibana `05`–`08`, `00`, `dashboards/README.md`, collection Postman. Phần còn lại là bản ghi lịch sử đã loại trừ.
2. **Given** thư mục dashboards, **When** tôi liệt kê, **Then** có đúng hai file ndjson, mỗi dashboard một file, và không còn `slo-van-hanh-hang-ngay.ndjson`.
3. **Given** dashboard Ngân sách tuần đang hiển thị service cạn ngân sách, **When** tôi bấm link trên dashboard, **Then** tôi sang dashboard Xử lý sự cố; và từ dashboard Xử lý sự cố, link ngược lại đưa tôi về Ngân sách tuần.
4. **Given** rule `incident-fast-detection` (028), **When** bộ test chạy, **Then** có test canh gác ngưỡng của rule này (5xx ≥ 1%, độ trễ vượt ngưỡng đã khai báo, chu kỳ, gateway chỉ xét 5xx). Mỗi test chạy thấy đỏ trước khi sửa (Nguyên tắc III).
5. **Given** test định nghĩa cảnh báo ngân sách của 027, **When** bộ test chạy, **Then** vẫn xanh và vẫn khớp chu kỳ tuần, kể cả với việc các panel ngân sách chuyển sang dashboard mới.

---

### User Story 4 - Bộ tài liệu đi kèm theo khuôn 027/028 (Priority: P3)

Là PO, QA và kiến trúc sư, tôi muốn spec này có đủ tài liệu đi kèm như 027/028 để hiểu, kiểm thử và bảo trì hai dashboard mà không phải đọc code.

**Why this priority**: Không đổi hành vi, nhưng đây là chuẩn của dự án; thiếu thì khó nghiệm thu và khó bàn giao.

**Independent Test**: Mở từng file tài liệu liệt kê ở FR-016 và xác nhận có đủ.

**Acceptance Scenarios**:

1. **Given** spec hoàn thành, **When** tôi kiểm tra, **Then** có file PO, QA, Architect với tên đã chốt ở Clarifications, mục 030 trong `functional-debt.md`, `QA_Debt.md`, `technical-debt.md`, và 3 sơ đồ drawio (thành phần, luồng nghiệp vụ, trình tự).
2. **Given** file QA, **When** tôi đọc, **Then** có bảng Thủ công trước và bảng Tự động sau (mỗi test link tới dòng khai báo), phát hiện chỉ ghi ở `QA_Debt.md`, không có khối "Nguồn đối chiếu".
3. **Given** collection Postman, **When** tôi mở, **Then** có folder 30 phục vụ kiểm thử hai dashboard (nội dung: request sinh lỗi/traffic và truy vấn đọc ngân sách tuần, xem Clarifications).

---

### Edge Cases

- **Dashboard Ngân sách tuần nhìn tuần đã qua**: cảnh báo đang hoạt động và trạng thái cạn là trạng thái hiện tại, không có lịch sử từng tuần theo cách của panel mức tiêu hao. Panel nào áp dụng điều khiển chọn tuần và panel nào chỉ hiện trạng thái hiện tại đã chốt ở FR-009: chỉ panel tính từ traces đổi theo tuần chọn, hai panel cảnh báo/cạn luôn là trạng thái hiện tại và phải ghi rõ trên dashboard.
- **Điều khiển chọn tuần không chạy trên Kibana 9.4.4**: theo Clarifications, dừng và hỏi lại người dùng, không tự lùi.
- **Khoảng thời gian rất ngắn hoặc rất ít dữ liệu** trên dashboard Xử lý sự cố: p95/p99 trên mẫu nhỏ nhiễu. Hiển thị đúng số đo có được, không che.
- **Khoảng thời gian rất dài (ví dụ 7 ngày trở lên)**: biểu đồ theo phút sẽ rất nhiều điểm; chấp nhận hiển thị theo khoảng thời gian Kibana tự làm tròn, không đổi nghĩa "theo phút" khi khoảng ngắn.
- **Không có span Client, không có log lỗi** trong khoảng đã chọn: panel hiển thị trống/"không có dữ liệu", không báo lỗi.
- **Log lỗi không có trace id** (ví dụ log phát sinh ngoài một request): dòng vẫn hiện, ô link sang trace để trống.
- **Link qua lại giữa hai dashboard khi chọn khoảng thời gian**: mở dashboard kia với khoảng thời gian mặc định của nó, không bắt buộc mang khoảng thời gian sang.
- **Dashboard cũ đang được mở hoặc bookmark**: sau khi bỏ, id cũ không còn; tài liệu hướng dẫn id mới.
- **Elastic vừa dọn sạch**: tuần đầu thiếu dữ liệu; các panel ngân sách nhạy với mẫu nhỏ (giới hạn đã biết ở 029).
- **Panel Phát hiện nhanh (028) khi bỏ lọc 15 phút**: danh sách có thể gồm nhiều cảnh báo đã tắt/đang bắn trong khoảng đã chọn; phải phân biệt trạng thái để không nhầm cảnh báo cũ với cảnh báo đang hoạt động.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Hệ thống PHẢI có hai dashboard thay cho dashboard cũ: `Xử lý sự cố — 7 service` và `Ngân sách lỗi tuần — 7 service`, mỗi dashboard có id saved object mới. Dashboard cũ (id `e2e06ff5-9cdf-4bea-acc8-5fd60ce26170`) PHẢI bị bỏ.
- **FR-002**: Mọi panel của dashboard Xử lý sự cố PHẢI đi theo thanh thời gian. KHÔNG panel nào được dùng cửa sổ cố định (`now-7d`, "15 phút gần nhất", đầu tuần/tháng).
- **FR-003**: Dashboard Xử lý sự cố PHẢI gồm các panel: Bảng SLO 7 service và ô Markdown ngưỡng; `dotnet.exceptions` theo service; phân bố status code; top endpoint chậm nhất; Phát hiện nhanh (028); 5xx và p95 theo phút theo service; lỗi gọi hạ lưu; traffic + 401/403 theo phút theo service; log lỗi gần nhất. Panel "Tổng 401 + 403" cũ KHÔNG còn.
- **FR-004**: Dashboard Xử lý sự cố PHẢI mặc định 1 giờ gần nhất và tự làm mới mỗi 1 phút.
- **FR-005**: Panel lỗi gọi hạ lưu PHẢI là bảng tổng hợp theo cặp (service gọi → đích) từ span Client lỗi hoặc chậm (chậm = vượt ngưỡng p95 đã khai báo của service đích), giới hạn 20 dòng. Bảng này KHÔNG có link sang trace.
- **FR-006**: Panel log lỗi gần nhất PHẢI hiển thị tối đa 50 log mức Error trở lên (Error và Fatal) mới nhất với các cột: thời gian, service, message, trace id, correlation id. Mỗi dòng có trace id PHẢI có link mở Discover (data view traces) lọc theo đúng trace id đó. (Không dùng Kibana APM: APM không đọc được dữ liệu trace OTel thô của dự án, xem Clarifications.)
- **FR-007**: Panel Phát hiện nhanh (028) trên dashboard Xử lý sự cố PHẢI bỏ lọc cứng "15 phút gần nhất" và đi theo thanh thời gian, nhưng PHẢI cho phép phân biệt cảnh báo đang hoạt động với cảnh báo đã tắt.
- **FR-008**: Mọi số liệu ngân sách của dashboard Ngân sách tuần PHẢI tính theo cửa sổ tuần lịch giờ Việt Nam (thứ Hai 00:00 → Chủ nhật 23:59, UTC+7), KHÔNG đổi theo thanh thời gian. Dashboard PHẢI gồm: mức tiêu hao tuần (4 ngân sách × 7 service); hạn mức còn lại (100% − mức tiêu hao và số request xấu còn được phép); cảnh báo mốc và cạn ngân sách của tuần; error-rate và p95 theo ngày trong tuần; tiêu hao lũy kế theo ngày từ thứ Hai.
- **FR-009**: Dashboard Ngân sách tuần PHẢI có điều khiển chọn tuần với 4 giá trị tương đối `Tuần này` (mặc định), `Tuần trước`, `2 tuần trước`, `3 tuần trước`; mỗi panel của dashboard dùng khoảng thời gian cố định riêng 30 ngày nên không đổi theo thanh thời gian. Chỉ các panel tính từ traces (mức tiêu hao, hạn mức còn lại, error-rate/p95 theo ngày, tiêu hao lũy kế) đổi theo tuần đã chọn. Hai panel cảnh báo mốc và cạn ngân sách luôn là trạng thái hiện tại và PHẢI ghi rõ điều đó trên dashboard để không bị hiểu nhầm là của tuần đã chọn.
- **FR-010**: Hạn mức còn lại PHẢI gồm hai số cho mỗi service và mỗi ngân sách: phần trăm còn lại (100% − mức tiêu hao) và số request xấu còn được phép. Số request xấu còn được phép = tỷ lệ cho phép × tổng request của tuần tới hiện tại − số request xấu đã có; KHÔNG dự phóng cả tuần.
- **FR-011**: Mỗi dashboard PHẢI có link sang dashboard còn lại, hoạt động hai chiều: ô Markdown theo id cố định, hai file ndjson độc lập (không dùng panel Links vì vòng tham chiếu).
- **FR-012**: Panel dạng bảng/danh sách PHẢI dựng bằng Discover session ES|QL; panel biểu đồ PHẢI dựng bằng Lens. Phần nhóm panel PHẢI dùng collapsible section ở trạng thái luôn mở.
- **FR-013**: Mỗi dashboard PHẢI được lưu bằng một file export ndjson riêng trong repo và import lại được vào một Kibana sạch mà không phải thao tác tay thêm.
- **FR-014**: Hiện vật dashboard cũ PHẢI bị xoá khỏi repo, và mọi tài liệu, quickstart, QA, contract của 021, 025, 026, 027, 028 và 029 (mọi tài liệu đang trỏ tới, theo lệnh tìm id/tên cũ trên repo) trỏ tới id/tên cũ PHẢI được sửa tại chỗ sang hai dashboard mới. Bản ghi lịch sử đã loại trừ ở 029 (mục cũ trong `QA_Debt.md`, kết quả diễn tập, `specs/002-gateway-bff-routing/`) KHÔNG được sửa.
- **FR-015**: Kiểm thử tự động PHẢI có test canh gác định nghĩa rule `incident-fast-detection` (ngưỡng 5xx, ngưỡng độ trễ, chu kỳ, quy tắc gateway chỉ xét 5xx) và test của 027 PHẢI tiếp tục khớp chu kỳ tuần. Mỗi test mới/sửa PHẢI chạy thấy đỏ trước khi sửa phần tương ứng (Nguyên tắc III). Comment test theo khuôn hiện có, tiếng Việt.
- **FR-016**: Spec này PHẢI có bộ tài liệu đi kèm theo khuôn 027/028 (file PO, QA, Architect theo tên ở Clarifications; mục 030 trong `functional-debt.md`, `QA_Debt.md`, `technical-debt.md`; 3 sơ đồ drawio; folder Postman 30).
- **FR-017**: Thay đổi KHÔNG được đổi định nghĩa SLO, ngưỡng, tỷ lệ cho phép, chu kỳ 5 phút hay hành vi các rule của 027/028/029; chỉ đổi nơi hiển thị và cách lọc thời gian của dashboard. Ngoại lệ duy nhất, có điều kiện: nếu kiểm chứng cho thấy log trong một request không mang correlation id tới Elasticsearch thì được sửa phần ghi log của `ServiceDefaults` để nó mang (FR-019); việc đó không được đổi phản hồi của bất kỳ endpoint nào.
- **FR-019**: Trước khi dựng panel log lỗi, PHẢI kiểm chứng trên hệ thống thật rằng một log Error phát sinh trong một request có mang correlation id và trace id tới Elasticsearch. Nếu thiếu, PHẢI sửa `ServiceDefaults` để log mang correlation id, test viết trước và chạy thấy đỏ (Nguyên tắc III); nếu đủ thì không sửa code.
- **FR-018**: Việc dọn toàn bộ Elastic sau triển khai CHỈ được thực hiện sau khi người dùng xác nhận lại ngay trước lúc xoá.

### Key Entities *(include if feature involves data)*

- **Dashboard Xử lý sự cố**: một màn hình duy nhất, mọi panel theo thanh thời gian, mặc định 1 giờ, tự làm mới 1 phút.
- **Dashboard Ngân sách tuần**: một màn hình cố định theo tuần lịch giờ Việt Nam, có điều khiển chọn tuần.
- **Tuần lịch giờ Việt Nam**: khoảng thứ Hai 00:00 → Chủ nhật 23:59 (UTC+7), định nghĩa từ spec 029.
- **Mức tiêu hao ngân sách tuần**: phần trăm mỗi ngân sách (khả dụng, 5xx, p95, p99) của mỗi service đã tiêu trong tuần.
- **Hạn mức còn lại**: 100% − mức tiêu hao, cùng số request xấu còn được phép.
- **Cặp gọi hạ lưu**: một cặp (service gọi, đích) được tổng hợp từ span Client lỗi hoặc chậm.
- **Log lỗi**: bản ghi log mức Error, có trace id và correlation id để lần theo sự cố.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% panel của dashboard Xử lý sự cố đổi theo thanh thời gian: đổi khoảng thời gian thì không panel nào giữ nguyên cửa sổ cố định.
- **SC-002**: Khi một service bị tiêm lỗi, người trực xác định được service nào, loại lỗi nào và có ít nhất một trace/log liên quan chỉ trên dashboard Xử lý sự cố, trong vòng 5 phút kể từ khi mở dashboard, không phải mở thêm công cụ khác ngoài link sang Discover theo trace id.
- **SC-003**: Số liệu ngân sách trên dashboard Ngân sách tuần không đổi (sai lệch 0) khi thay đổi thanh thời gian, và khớp với điều kiện làm cảnh báo mốc bắn cùng thời điểm.
- **SC-004**: Trên dashboard Ngân sách tuần, với một tuần đã chọn, người dùng đọc được mức tiêu hao, hạn mức còn lại và xu hướng theo ngày của cả 7 service chỉ bằng một màn hình.
- **SC-005**: Tìm trên toàn repo không còn tham chiếu tới id `e2e06ff5-…` hay tên "SLO vận hành hằng ngày — 7 service" trong hiện vật và tài liệu đang dùng; chỉ còn ở bản ghi lịch sử đã loại trừ.
- **SC-006**: Import hai file ndjson vào Kibana sạch dựng được cả hai dashboard, link qua lại hoạt động hai chiều, và không phải thao tác tay thêm.
- **SC-007**: Test canh gác `incident-fast-detection` và test định nghĩa cảnh báo ngân sách của 027 chạy xanh; mỗi test mới/sửa đã được chạy thấy đỏ trước khi sửa.
- **SC-008**: Bộ tài liệu đi kèm có đủ các file đã chốt, và không có phát hiện nào ghi ngoài `QA_Debt.md`.

## Assumptions

- Spec 029 đã triển khai và đã vào `master`: chu kỳ tuần lịch, SLO 99%/1%, 4 rule ngân sách, rule `incident-fast-detection`, 3 saved search ngân sách và 1 saved search Phát hiện nhanh đã có. Spec này chỉ đổi cách tổ chức và lọc thời gian của dashboard, không đổi cơ chế cảnh báo.
- Cách xác định thứ Hai 00:00 giờ Việt Nam đã được kiểm chứng ở 029 và được dùng lại cho các panel tuần của dashboard mới.
- Dữ liệu nguồn: `traces-generic.otel-default*` (span Server và Client), `logs-generic.otel-default*`, `metrics-generic.otel-default*`. Việc log có đủ trace id và correlation id ở mọi dòng Error cần được kiểm chứng ở giai đoạn lập kế hoạch.
- Việc điều khiển chọn tuần hoạt động trên Kibana 9.4.4, và cách lưu link qua lại, cách dựng panel Lens theo phút theo service, là chi tiết triển khai cần kiểm chứng thật; khi điều khiển chọn tuần không chạy được thì dừng và hỏi (Clarifications).
- Folder Postman 30 gồm request sinh lỗi/traffic và truy vấn đọc ngân sách tuần (xem Clarifications). Tên folder và cách chia folder con PHẢI được hỏi người dùng ở `/speckit-tasks`, không suy diễn từ folder 27/29.
- "Người vận hành" vẫn là một người đóng vai SRE/Dev (như 027/029).
- Phạm vi: không thêm cảnh báo mới, không đổi rule, không đổi SLO; không làm dashboard cho người dùng cuối hay cho chỉ số kinh doanh.
