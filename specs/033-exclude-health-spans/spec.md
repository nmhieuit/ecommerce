# Feature Specification: Loại span health khỏi công thức ngân sách lỗi

**Feature Branch**: `feature/033-exclude-health-spans`

**Created**: 2026-10-08

**Status**: Draft

**Input**: User description: "Loại span health live và ready (GET /health/live, GET /health/ready) khỏi công thức ngân sách lỗi: các rule 027 (error-budget-50/75/100, error-budget-frozen), rule phát hiện nhanh 028 (incident-fast-detection) và các panel dashboard Ngân sách lỗi tuần (spec 030) hiện tính mọi span Server, nên health check của Docker (curl mỗi 5 giây) chiếm toàn bộ mẫu số và 1 span chậm lúc khởi động nguội làm ngân sách vọt 14–18% dù chưa có request nghiệp vụ nào. Tạo branch mới cho các thay đổi. Luôn hỏi lại, không suy diễn."

## Clarifications

### Bằng chứng nền (đã điều tra trước khi viết spec, không hỏi lại)

- Trên stack local sau khi xoá volume Elastic và tạo lại 7 container API (2026-10-08): 100% span trong Elasticsearch do health check của Docker tạo ra (`curl` gọi `GET /health/ready` mỗi 5 giây; `Gateway.Api` còn có `GET /health/live`), khoảng 15,5 span mỗi phút mỗi service.
- 7 container API được tạo lại lúc 11:51Z. Lần gọi `/health/ready` đầu tiên của 5 service chậm 0,8–3,7 giây (khởi động nguội), vượt ngưỡng p95/p99. Chỉ 1 span xấu trên khoảng 150–550 span đã làm ngân sách `latency-p99` vọt 14–18% và bật rồi tắt `error-budget-50` và `incident-fast-detection`.
- Công thức ngân sách hiện tính **mọi** span Server, không loại health. Cờ `Chaos__AllowFaultInjection` / `Chaos__AllowLatencyInjection` không phải nguyên nhân: chúng chỉ tác dụng khi request mang header `X-Chaos-Fault` / `X-Chaos-Latency-Ms`, còn health check chỉ chạy `curl` trần.
- Spec này phụ thuộc spec 030 (hai dashboard) và PR #75 trên `master` (ngưỡng độ trễ p95 500 ms / p99 700 ms; `Bff.Api` 700/1000; `Gateway.Api` 800/1100).

### Session 2026-10-08 (phiên `/speckit-specify`)

Các giá trị người dùng chọn từ phương án gợi ý; mọi giá trị gợi ý nêu là "ví dụ" nay đã được người dùng xác nhận.

- Q: Nhánh mới tạo từ đâu? → A: Từ `master` mới nhất (đã gộp 030, 031, 032 và PR #75). Worktree hiện tại chuyển sang nhánh mới; nhánh `feature/030-…` đã commit nên không mất gì.
- Q: Tên nhánh và thư mục spec? → A: Nhánh `feature/033-exclude-health-spans`, thư mục `specs/033-exclude-health-spans/`.
- Q: Phạm vi loại span health? → A: Cả bốn nhóm: 4 rule 027 (`error-budget-50/75/100`, `error-budget-frozen`); rule 028 `incident-fast-detection`; các panel tính từ traces của dashboard `Ngân sách lỗi tuần — 7 service`; các panel liên quan tới span của dashboard `Xử lý sự cố — 7 service`. (Người dùng ban đầu nêu 3 nhóm đầu; nhóm dashboard Xử lý sự cố được thêm khi trả lời.)
- Q: Nhận diện "span health" bằng gì? → A: **Theo tiền tố đường dẫn `/health`**: loại mọi span có đường dẫn bắt đầu bằng `/health` (gồm `/health/live`, `/health/ready` và endpoint health thêm sau này), mọi phương thức HTTP. (Rộng hơn mô tả ban đầu chỉ nêu hai endpoint.)
- Q: Span health trả 5xx thì sao, vì loại hẳn sẽ làm service không sẵn sàng không còn đốt ngân sách khả dụng/5xx? → A: **Loại hẳn khỏi cả 4 ngân sách**, nhưng phải có **chỉ báo riêng cho health lỗi**.
- Q: "Chỉ báo riêng" cụ thể là gì? → A: **Một panel và một rule cảnh báo mới.** Chỉ tính health trả 5xx (**không** tính health chậm). Điều kiện bắn: **từ 50% số span health của một service trả 5xx trong 5 phút**.
- Q: Các request/script đang tiêm lỗi vào `/health/live` (Postman folder 25, 27, 29a, 30a và `scripts/incident-drill.ps1`) xử lý thế nào, vì sau thay đổi chúng không còn đốt ngân sách? → A: **Đổi sang đường dẫn không phải health.** Tên đường dẫn cụ thể sẽ hỏi lại ở `/speckit-plan` hoặc `/speckit-tasks`, không suy diễn.
- Q (phát hiện lúc `/speckit-plan`): `scripts/incident-drill.ps1` có cần đổi đường dẫn tiêm lỗi không? → A: **Không.** Script đã gửi tải và header tiêm lỗi tới route nghiệp vụ (`/bff/products`, `/orders/{guid}`, …); chỉ chờ container sẵn sàng bằng `GET /health/ready`, việc này không đi vào ngân sách. Chỉ 26 request Postman (folder 25: 8, 27: 3, 29a: 7, 30a: 8) đang gửi header tiêm lỗi vào `/health/live` cần đổi. FR-011 và User Story 5 sửa theo.
- Q: Service chỉ có span health (chưa có request nghiệp vụ) hiển thị thế nào? → A: **Không hiện dòng cho service đó** trên dashboard ngân sách và Bảng SLO; panel có thể trống hoàn toàn khi chưa có request nghiệp vụ; không cảnh báo.
- Q: Quy tắc loại span được khai báo ở đâu? → A: **Trong khối `error-budget-policy` của cả 7 manifest**, bằng khoá `excluded-path-prefixes: [/health]`. Không sửa hiến chương. Test đối chiếu rule với manifest.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ngân sách chỉ tính request nghiệp vụ, không tính health check (Priority: P1)

Là người đóng vai SRE, tôi muốn các cảnh báo ngân sách lỗi (mốc 50/75/100%, cạn ngân sách) và cảnh báo phát hiện nhanh chỉ tính các request nghiệp vụ, không tính span của health check. Có như thế, một service vừa khởi động lại hay vừa được dựng lại stack không bị coi là "đã tiêu ngân sách" chỉ vì lần health check đầu tiên chậm, và mẫu số không bị health check chiếm hết.

**Why this priority**: Đây là nguyên nhân gốc đã đo được: health check chiếm toàn bộ mẫu số và một span chậm lúc khởi động nguội làm ngân sách vọt 14–18% và làm bật cảnh báo giả. Mọi phần khác (dashboard, chỉ báo health) phụ thuộc định nghĩa này.

**Independent Test**: Trên stack chưa có request nghiệp vụ nào, tạo lại các container API (health check vẫn chạy mỗi 5 giây, lần đầu chậm). Xác nhận mọi rule ngân sách và rule phát hiện nhanh không bắn, `slo-error-budget-events` không có sự kiện mới, và truy vấn mức tiêu hao không trả dòng nào cho service chỉ có health.

**Acceptance Scenarios**:

1. **Given** một service chỉ có span health (không có request nghiệp vụ), **When** một span health chậm hơn ngưỡng p99 hoặc trả 5xx, **Then** không ngân sách nào của service đó tăng và không rule 027/028 nào bắn.
2. **Given** một service có cả request nghiệp vụ và span health, **When** tính mức tiêu hao, **Then** mẫu số chỉ gồm span không có đường dẫn bắt đầu bằng `/health`, và các span xấu cũng chỉ lấy từ tập đó.
3. **Given** rule `error-budget-frozen` xét một ngày chỉ có span health, **When** tính "ngày đạt SLO", **Then** ngày đó được coi là không có traffic và tính là đạt (giữ quy tắc 027 và 029).
4. **Given** một span nghiệp vụ trả 5xx hoặc vượt ngưỡng độ trễ, **When** tính ngân sách, **Then** nó vẫn được tính như trước (hành vi cho request nghiệp vụ không đổi).
5. **Given** một service có đường dẫn endpoint health khác bắt đầu bằng `/health` (ví dụ endpoint health thêm sau này), **When** tính ngân sách, **Then** span đó cũng bị loại.

---

### User Story 2 - Hai dashboard không tính health check (Priority: P1)

Là người đóng vai SRE, tôi muốn các panel của hai dashboard `Ngân sách lỗi tuần — 7 service` và `Xử lý sự cố — 7 service` không tính span health, để con số trên dashboard khớp đúng với điều kiện làm cảnh báo bắn, và Bảng SLO phản ánh trải nghiệm request thật chứ không phải health check.

**Why this priority**: Nếu rule bỏ health mà dashboard còn tính, người vận hành thấy hai con số mâu thuẫn trên cùng một màn hình (đã là tiêu chí SC-003 của 030).

**Independent Test**: Mở hai dashboard trên stack chỉ có health check: dashboard ngân sách không hiện dòng nào cho service chỉ có health, các biểu đồ theo phút/ngày và Bảng SLO không có số liệu từ health; thêm request nghiệp vụ thì số liệu xuất hiện và khớp với truy vấn Elasticsearch.

**Acceptance Scenarios**:

1. **Given** stack chỉ có health check, **When** mở `Ngân sách lỗi tuần — 7 service`, **Then** các panel tính từ traces (mức tiêu hao, hạn mức còn lại, error-rate/p95 theo ngày, tiêu hao lũy kế) không hiện dòng cho service chỉ có health; hai panel cảnh báo/cạn không có alert.
2. **Given** stack chỉ có health check, **When** mở `Xử lý sự cố — 7 service`, **Then** Bảng SLO, 5xx/p95/traffic theo phút, phân bố status code và top endpoint chậm nhất không tính span health.
3. **Given** có request nghiệp vụ, **When** so số trên dashboard ngân sách với truy vấn Elasticsearch loại `/health*`, **Then** khớp từng (service, ngân sách) như SC-003 của 030.
4. **Given** một service chỉ có span health, **When** xem Bảng SLO, **Then** không có dòng cho service đó (không hiện 0%).

---

### User Story 3 - Chỉ báo riêng cho health lỗi: panel và rule cảnh báo (Priority: P2)

Là người đóng vai SRE, vì health đã bị loại khỏi ngân sách nên tôi cần một cách riêng để biết một service không sẵn sàng (health trả 5xx). Tôi muốn một panel trên dashboard Xử lý sự cố và một rule cảnh báo bắn khi từ 50% span health của một service trả 5xx trong 5 phút.

**Why this priority**: Loại health khỏi ngân sách làm mất tín hiệu "service không sẵn sàng". Chỉ báo riêng bù lại tín hiệu đó mà không làm nhiễu ngân sách. Ưu tiên P2 vì ngân sách đúng (US1, US2) có giá trị độc lập.

**Independent Test**: Làm một service trả 5xx ở `/health/ready` (ví dụ tạm dừng DB của nó) trong hơn 5 phút: panel hiện service và tỷ lệ health 5xx; rule bắn cho đúng service đó trong một chu kỳ đánh giá; không rule ngân sách nào bắn. Health chậm (không trả 5xx) không bắn.

**Acceptance Scenarios**:

1. **Given** một service có ≥ 50% span health trả 5xx trong 5 phút gần nhất, **When** rule chạy, **Then** rule bắn cho đúng service đó và không bắn cho service khác.
2. **Given** một service có dưới 50% span health trả 5xx (ví dụ chỉ vài lần lỗi thoáng qua lúc khởi động), **When** rule chạy, **Then** rule không bắn.
3. **Given** health check của một service chỉ chậm nhưng trả 200, **When** rule chạy, **Then** không bắn (chỉ báo này chỉ tính 5xx).
4. **Given** service health lỗi, **When** mở dashboard Xử lý sự cố, **Then** panel chỉ báo hiện service, số span health 5xx và tỷ lệ theo khoảng thời gian đã chọn; không có panel ngân sách nào đổi.
5. **Given** rule health lỗi bắn, **When** xem ngân sách, **Then** mức tiêu hao của service đó không tăng vì health lỗi.

---

### User Story 4 - Quy tắc loại span được khai báo trong manifest và có test canh gác (Priority: P2)

Là kỹ sư nền tảng, tôi muốn tiền tố đường dẫn bị loại khỏi ngân sách được khai báo ở một chỗ nhìn thấy được (khối `error-budget-policy` của cả 7 manifest) và có test báo đỏ khi rule/manifest lệch nhau, theo đúng cách ngưỡng độ trễ đang được canh gác.

**Why this priority**: Rule chép tay điều kiện loại nên sẽ trôi lệch. Khai báo trong manifest và test là cách giữ nó nhất quán, theo Nguyên tắc III (test trước).

**Independent Test**: Sửa tiền tố ở một manifest hoặc bỏ điều kiện loại khỏi một rule trong file export: bộ test quy ước báo đỏ đúng service/rule và đúng chỗ lệch.

**Acceptance Scenarios**:

1. **Given** 7 manifest, **When** đọc khối `error-budget-policy`, **Then** cả 7 có `excluded-path-prefixes: [/health]` giống hệt nhau.
2. **Given** file export 4 rule 027 và rule 028, **When** bộ test chạy, **Then** mỗi rule loại đúng tiền tố đã khai báo trong manifest; sửa tiền tố ở manifest hoặc ở rule làm test báo đỏ.
3. **Given** một manifest thiếu khoá `excluded-path-prefixes` hoặc có giá trị khác 6 manifest còn lại, **When** bộ test chạy, **Then** test báo đỏ đúng service đó.
4. **Given** rule health lỗi mới, **When** bộ test chạy, **Then** có test canh ngưỡng 50%, cửa sổ 5 phút, tiền tố health và cột định danh alert.

---

### User Story 5 - Kịch bản Postman tiêm lỗi và tài liệu dùng đường dẫn không phải health (Priority: P3)

Là người diễn tập, tôi muốn các kịch bản đốt ngân sách bằng Postman (folder 25, 27, 29a, 30a) vẫn đốt được ngân sách sau thay đổi, bằng cách gửi header tiêm lỗi/độ trễ tới đường dẫn không phải health; và mọi tài liệu mô tả công thức ngân sách nói đúng "không tính health".

**Why this priority**: Không cập nhật thì các kịch bản kiểm thử và diễn tập gãy âm thầm (tiêm lỗi vào `/health/live` không còn đốt ngân sách). Không đổi hành vi cảnh báo nên P3.

**Independent Test**: Chạy folder 29a/30a theo vòng với cờ tiêm lỗi bật: ngân sách 5xx của cả 7 service tăng và các mốc 50/75/100 bật; chạy `scripts/incident-drill.ps1`: rule 028 bắn; folder 25 làm p95 của `Orders.Api` tăng.

**Acceptance Scenarios**:

1. **Given** cờ `CHAOS_ALLOW_FAULT_INJECTION` bật, **When** chạy folder 29a/30a, **Then** mỗi request gây 5xx trên đường dẫn không phải health và đốt ngân sách của đúng service.
2. **Given** cờ `CHAOS_ALLOW_LATENCY_INJECTION` bật, **When** chạy folder 25 hoặc bước độ trễ của 30a, **Then** span chậm không phải health, nên p95/p99 của service đó tăng.
3. **Given** `scripts/incident-drill.ps1` (đã dùng route nghiệp vụ), **When** chạy, **Then** rule 028 vẫn bắn như trước và chỉ lần chờ `/health/ready` không vào ngân sách.
4. **Given** tài liệu PO/QA/Architect, `07`/`08`/`06`, contract manifest 029 và contract rule, **When** đọc, **Then** có nói span `/health*` bị loại và nêu khoá `excluded-path-prefixes`.

---

### Edge Cases

- **Service chỉ có span health**: không hiện dòng ở dashboard ngân sách và Bảng SLO; panel có thể trống hoàn toàn khi chưa có request nghiệp vụ; không rule nào bắn (giữ FR-013 của 029).
- **Tuần đầu hoặc sau khi tạo lại container**: mẫu số nghiệp vụ nhỏ nên vài request xấu nghiệp vụ vẫn làm phần trăm vọt cao (giới hạn đã biết ở 029/030, vẫn còn); spec này chỉ loại phần do health.
- **Health trả 5xx kéo dài nhưng không có request nghiệp vụ**: ngân sách không đổi; chỉ rule/panel health lỗi báo.
- **Health lỗi thoáng qua lúc khởi động**: vài span 5xx đầu (DB chưa sẵn sàng, retry 20 lần của Docker) thường dưới 50% trong cửa sổ 5 phút nên không bắn; nếu một service khởi động nguội trả 5xx quá nửa số span trong 5 phút, rule vẫn bắn (cảnh báo hợp lệ).
- **Health chậm khi khởi động nguội**: không được báo ở đâu (chỉ báo health chỉ tính 5xx); đã chấp nhận theo lựa chọn người dùng.
- **Endpoint khác có tên bắt đầu bằng `/health` nhưng là request nghiệp vụ**: bị loại theo tiền tố (chấp nhận do người dùng chọn quy tắc theo tiền tố).
- **Request có header tiêm lỗi gửi vào `/health*`**: bị loại khỏi ngân sách như mọi span health; vì thế kịch bản tiêm lỗi phải dùng đường dẫn khác.
- **Gateway/BFF**: span health của chúng cũng bị loại; span Client (lời gọi hạ lưu) không đổi.
- **Lịch sử đã có trong Elasticsearch**: truy vấn mới loại health cả với dữ liệu cũ nên mức tiêu hao tính lại ngay, không cần xoá dữ liệu.
- **Sự kiện "cạn" cũ và alert đang active**: rule được sửa/import lại có thể ghi lại sự kiện "cạn" cho alert vừa active (giới hạn đã biết của 027/029); vì health bị loại, các alert do health gây ra sẽ tắt ở lượt chạy kế tiếp.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Công thức ngân sách lỗi của cả 7 service PHẢI loại mọi span có đường dẫn bắt đầu bằng `/health` (mọi phương thức HTTP, gồm `/health/live` và `/health/ready`) khỏi cả mẫu số lẫn các span xấu, cho cả 4 ngân sách (khả dụng, 5xx, p95, p99).
- **FR-002**: Việc loại span health PHẢI áp dụng cho 4 rule 027 (`error-budget-50`, `error-budget-75`, `error-budget-100`, `error-budget-frozen`) và rule 028 `incident-fast-detection`. Mọi quy tắc khác của chúng (tỷ lệ cho phép, ngưỡng độ trễ, mốc 50/75/100, cửa sổ rule, chu kỳ 5 phút, cột kết quả) KHÔNG đổi.
- **FR-003**: Rule `error-budget-frozen` PHẢI tính một ngày chỉ có span health là ngày không có traffic và tính là đạt SLO (giữ quy tắc 027/029); các span health không được tính vào số span của ngày.
- **FR-004**: Mọi panel tính từ traces của dashboard `Ngân sách lỗi tuần — 7 service` (mức tiêu hao, hạn mức còn lại, error-rate theo ngày, p95 theo ngày, tiêu hao lũy kế) PHẢI loại span health và PHẢI khớp công thức của rule mốc ở FR-001.
- **FR-005**: Mọi panel của dashboard `Xử lý sự cố — 7 service` đọc từ span (Bảng SLO, 5xx theo phút, p95 theo phút, traffic + 401/403 theo phút, phân bố status code, top endpoint chậm nhất) PHẢI loại span health. Panel không đọc span health (lỗi gọi hạ lưu từ span Client, log lỗi, `dotnet.exceptions`, Phát hiện nhanh đọc alert) giữ nguyên.
- **FR-006**: Service chỉ có span health PHẢI không hiện dòng nào trên dashboard ngân sách và Bảng SLO; panel có thể trống hoàn toàn khi chưa có request nghiệp vụ; không rule nào được bắn vì thiếu dữ liệu (giữ FR-013 của 029).
- **FR-007**: Cả 7 manifest PHẢI khai báo trong khối `error-budget-policy` khoá `excluded-path-prefixes: [/health]`, giống hệt nhau. Contract `error-budget-policy-manifest-shape.md` của 029 PHẢI được cập nhật theo.
- **FR-008**: PHẢI có panel chỉ báo health lỗi trên dashboard `Xử lý sự cố — 7 service`, theo thanh thời gian: cho mỗi service, số span health và số/tỷ lệ span health trả 5xx. KHÔNG tính health chậm.
- **FR-009**: PHẢI có một rule cảnh báo mới bắn cho một service khi từ 50% số span health của service đó trả 5xx trong 5 phút gần nhất. Rule KHÔNG tính health chậm và KHÔNG ảnh hưởng ngân sách. Tên rule, tag, chu kỳ chạy và tên panel chưa chốt (xem Assumptions), sẽ hỏi lại ở giai đoạn plan/tasks.
- **FR-010**: Kiểm thử tự động PHẢI bảo vệ: (a) cả 7 manifest có `excluded-path-prefixes` đúng giá trị và giống nhau; (b) 4 rule 027 và rule 028 loại đúng tiền tố đã khai báo ở manifest; (c) rule health lỗi mới: ngưỡng 50%, cửa sổ 5 phút, tiền tố health, cột định danh alert. Mỗi test mới/sửa PHẢI chạy thấy đỏ trước khi sửa manifest/rule tương ứng (Nguyên tắc III). Test hiện có của 027, 028 PHẢI vẫn xanh sau khi sửa.
- **FR-011**: Các request Postman tiêm lỗi hoặc độ trễ vào `/health/live` (folder 25, 27, 29a, 30a; 26 request) PHẢI được đổi sang đường dẫn không phải health để vẫn đốt được ngân sách. Tên đường dẫn cụ thể chưa chốt, sẽ hỏi lại, không suy diễn.
- **FR-012**: Tài liệu và hiện vật đang dùng của 027, 028, 029, 030 PHẢI được sửa tại chỗ để mô tả công thức loại span health: tài liệu Kibana `06`, `07`, `08`, `alerts/README.md`, `dashboards/README.md`; contract manifest 029, contract rule 027/028; PO/QA/Architect liên quan; sơ đồ drawio nhắc công thức ngân sách; mô tả collection Postman. KHÔNG sửa bản ghi lịch sử (mục cũ `QA_Debt.md`, `docs/dien-tap-chaos-engineering/ket-qua/`, `specs/002-gateway-bff-routing/`).
- **FR-013**: Thay đổi KHÔNG được đổi SLO, tỷ lệ cho phép, ngưỡng độ trễ, mốc cảnh báo, chu kỳ, cửa sổ rule, chu kỳ ngân sách, hiến chương, hay hành vi phản hồi của bất kỳ endpoint nào; chỉ đổi tập span được tính và thêm chỉ báo health lỗi.
- **FR-014**: Spec này PHẢI có bộ tài liệu đi kèm theo khuôn 027–030: file PO, QA, Architect; mục 033 trong `functional-debt.md`, `QA_Debt.md`, `technical-debt.md`; 3 sơ đồ drawio; folder Postman 33. Tên file và nội dung folder Postman chưa chốt, sẽ hỏi lại ở `/speckit-tasks`.
- **FR-015**: Việc dọn Elastic hay xoá bất kỳ object Kibana đang chạy PHẢI được hỏi lại người dùng ngay trước khi thực hiện.

### Key Entities *(include if feature involves data)*

- **Span health**: span Server có đường dẫn bắt đầu bằng `/health` (health check của Docker/Kubernetes); không phải request nghiệp vụ; bị loại khỏi ngân sách.
- **Request nghiệp vụ**: mọi span Server còn lại; là tập duy nhất được tính vào ngân sách.
- **Tiền tố bị loại**: giá trị khai báo trong `error-budget-policy.excluded-path-prefixes` của mỗi manifest (hiện `/health`); rule và panel phải dùng đúng giá trị đó.
- **Ngân sách tuần của service (4 ngân sách)**: như 029/030, nay chỉ tính trên request nghiệp vụ.
- **Chỉ báo health lỗi**: với mỗi service, số và tỷ lệ span health trả 5xx trong khoảng đã chọn; nguồn cho panel và rule cảnh báo mới.
- **Rule cảnh báo health lỗi**: bắn khi từ 50% span health của một service trả 5xx trong 5 phút; không đụng ngân sách.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Trên stack chỉ có health check (chưa có request nghiệp vụ), sau khi tạo lại cả 7 container API: 0 dòng mức tiêu hao cho 7 service ở dashboard ngân sách, 0 alert của 4 rule 027 và rule 028, 0 sự kiện mới trong `slo-error-budget-events` (so với trước: 5 service ở 14–18% `latency-p99` và 9 alert bật rồi tắt).
- **SC-002**: Với request nghiệp vụ, mức tiêu hao trên dashboard khớp từng (service, ngân sách) với truy vấn Elasticsearch loại `/health*` và với điều kiện bắn của rule mốc cùng thời điểm (sai lệch ≤ 2 điểm % do dữ liệu tăng giữa hai lần đọc, như 030).
- **SC-003**: Một service có ≥ 50% span health trả 5xx trong 5 phút được báo bởi rule health lỗi trong vòng một chu kỳ đánh giá; một service có dưới 50% hoặc chỉ health chậm không bị báo; không rule ngân sách nào bắn vì health.
- **SC-004**: Cả 7/7 manifest có `excluded-path-prefixes: [/health]` giống hệt nhau; 5/5 rule (4 rule 027 + rule 028) loại đúng tiền tố đó; bộ test quy ước chạy xanh, và mỗi test mới/sửa đã chạy thấy đỏ trước khi sửa.
- **SC-005**: Chạy folder 29a/30a theo vòng với cờ bật đốt được ngân sách 5xx của cả 7 service và bật các mốc 50/75/100 bằng đường dẫn không phải health; folder 25 làm p95 của `Orders.Api` tăng; `scripts/incident-drill.ps1` vẫn làm rule 028 bắn (không cần sửa).
- **SC-006**: Tìm trên toàn repo không còn tài liệu hay hiện vật đang dùng nào mô tả công thức ngân sách "tính mọi span" hoặc hướng dẫn tiêm lỗi vào `/health*` để đốt ngân sách; ngoại lệ chỉ là các bản ghi lịch sử đã loại trừ ở FR-012.
- **SC-007**: Hai dashboard import vào Kibana sạch bằng một lệnh mỗi file, hiển thị đúng sau thay đổi, và panel chỉ báo health lỗi nằm trên `Xử lý sự cố — 7 service`.

## Assumptions

- Spec 030 (hai dashboard, id cố định, file ndjson độc lập) và PR #75 (ngưỡng độ trễ mới) đã có trên `master`; spec này xây trên đó, không đổi cấu trúc hai dashboard ngoài các panel liên quan.
- Cách nhận diện đường dẫn (trường đường dẫn của span) và cách viết điều kiện loại trong ES|QL và Lens là chi tiết triển khai, cần kiểm chứng trên Kibana thật ở giai đoạn plan; nếu không chạy được thì dừng và hỏi lại người dùng.
- Tên rule cảnh báo health lỗi mới, tag, chu kỳ chạy (chưa chốt; ví dụ 5 phút như các rule hiện có), tên panel, tên file export và cách đưa rule vào file export chưa được chốt; **PHẢI hỏi lại người dùng** ở `/speckit-plan` hoặc `/speckit-tasks`, không suy diễn.
- Đường dẫn không phải health dùng để tiêm lỗi/độ trễ (FR-011) chưa chốt; PHẢI hỏi lại, kèm kiểm chứng rằng middleware tiêm lỗi chạy trên đường dẫn đó.
- Tên file PO/QA/Architect, tên 3 file drawio, tên folder Postman 33 chưa chốt; hỏi lại ở `/speckit-tasks`.
- Hiến chương không đổi (người dùng chọn khai báo ở manifest, không phải hiến chương). Khoá `excluded-path-prefixes` là phần mở rộng của contract manifest 029.
- Health chậm không được báo ở bất kỳ đâu sau thay đổi (chấp nhận theo lựa chọn của người dùng); nếu cần sẽ là spec khác.
- "Người vận hành" vẫn là một người đóng vai SRE/Dev; dừng merge khi cạn ngân sách là cam kết quy trình, không có cơ chế chặn tự động (như 027).
