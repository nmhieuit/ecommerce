# Feature Specification: Ngân sách lỗi chỉ đếm span Server

**Feature Branch**: `feature/034-error-budget-server-spans`

**Created**: 2026-10-09

**Status**: Draft

**Input**: User description: "Ngân sách lỗi chỉ đếm span Server (request mà chính service nhận), không đếm span Client/Producer (lời gọi đi ra hạ lưu, publish message). Rule error-budget-50/75/100/frozen, panel 'Mức tiêu hao ngân sách (%) — tuần đã chọn', 'Hạn mức còn lại' và rule incident-fast-detection hiện đếm mọi span trong traces-generic.otel-default* (chỉ loại /health*), kể cả kind = Client và Producer, nên một lỗi đi qua Gateway → BFF → Products trừ ngân sách ở cả 3 service và bị đếm 2 lần trong BFF. Luôn hỏi lại, không suy diễn; giá trị lấy từ 'ví dụ' phải hỏi lại. Đọc lại cách làm của spec 027, 029 và 030 rồi làm tương tự các task cần thiết."

## Clarifications

### Bằng chứng nền (đã điều tra ngày 2026-10-09, không hỏi lại)

- Các rule `error-budget-50/75/100/frozen`, rule `incident-fast-detection`, panel "Mức tiêu hao ngân sách (%) — tuần đã chọn" và "Hạn mức còn lại" đếm **mọi** span trong `traces-generic.otel-default*` (chỉ loại `/health*` từ spec 033), kể cả span có `kind = Client` và `Producer`.
- Hệ quả 1: một lỗi đi qua Gateway → BFF → Products trừ ngân sách ở cả 3 service.
- Hệ quả 2: trong BFF, lỗi bị đếm hai lần (span Server nhận request + span Client gọi hạ lưu).
- Số liệu tuần 05–11/10: 12/21 lỗi 5xx của `Bff.Api` là span Client; `Bff.Api` có 201 span Client trên 319 span được tính.
- Giữ nguyên (người dùng đã nêu): công thức tỷ lệ (số lỗi được phép = tỷ lệ cho phép × tổng request; mốc 50/75/100 tính lại mỗi lần rule chạy), tuần lịch UTC+7, tỷ lệ 1% / 1% / 5% / 1%, ngưỡng độ trễ hiện hành (PR #75), loại `/health*` (spec 033), lỗi diễn tập tính như thật.
- **Không** thêm sàn số request tối thiểu (người dùng đã cân nhắc và bỏ).

### Đã chốt trong mô tả (không hỏi lại)

- Chỉ đếm span Server cho **mọi** ngân sách: khả dụng, tỷ lệ 5xx, độ trễ p95, độ trễ p99.

### Session 2026-10-09 (phiên `/speckit-specify`)

- Q: "Chỉ đếm span Server" áp cho những rule nào? → A: **4 rule 027 (`error-budget-50/75/100/frozen`) và `incident-fast-detection`**. Rule `health-failure` giữ nguyên (chỉ đọc `/health*`, vốn là span Server). Ngân sách và phát hiện nhanh dùng cùng một định nghĩa.
- Q: Panel đọc span của hai dashboard có đổi theo không? → A: **Có, mọi panel ngân sách và các panel ngân sách trên `Xử lý sự cố — 7 service`**. Dashboard `Ngân sách lỗi tuần — 7 service`: mọi panel đọc span chỉ đếm Server. Dashboard `Xử lý sự cố — 7 service`: Bảng SLO, 5xx/p95/traffic theo phút, phân bố status code, top endpoint chậm nhất chỉ đếm Server. Panel lỗi gọi hạ lưu (dựa vào span Client), log lỗi, `dotnet.exceptions`, Phát hiện nhanh (đọc alert) **giữ nguyên**.
- Q: Test canh gác thế nào? → A: **Thêm kiểm tra "rule chỉ đếm Server" vào `ErrorBudgetRuleDefinitionTests` cho 4 rule 027, và viết test mới cho `incident-fast-detection`** (hiện chưa có test). Mỗi test mới phải chạy thấy đỏ trước khi sửa rule (Nguyên tắc III). Không đối chiếu panel dashboard bằng test. (Ghi chú khi triển khai: `incident-fast-detection` thực tế đã có lớp `IncidentFastDetectionRuleDefinitionTests` với 8 test; test Server mới được thêm vào lớp đó, nên "chưa có test" trong mô tả chỉ đúng với điều kiện Server.)
- Q: Trạng thái cạn/đóng băng hiện tại của 7 service (do bài thử 29a/30a ngày 09/10)? → A: **Sau khi triển khai, xoá sự kiện cạn trong `slo-error-budget-events` và Disable/Enable rule `error-budget-100`** để về sạch trạng thái đóng băng giả. Mọi thao tác xoá phải hỏi lại người dùng ngay trước khi thực hiện.
- Q: Lệch Governance (PR #75 sửa ngưỡng độ trễ trong hiến chương nhưng không tăng phiên bản, vẫn 2.0.0) xử lý trong spec này không? → A: **Không, là việc riêng.** Spec này chỉ ghi nhận vào Clarifications và `technical-debt.md`, không sửa hiến chương.
- Q: Bộ tài liệu đi kèm? → A: **Cả bốn nhóm**: PO/QA/Architect; nhật ký nợ (`functional-debt.md`, `QA_Debt.md`, `technical-debt.md`); 3 sơ đồ drawio; folder Postman 34 và sửa mô tả collection. Tên file, tên drawio và nội dung folder Postman chưa chốt, hỏi lại ở `/speckit-tasks`.
- Q: Nhánh và thư mục spec? → A: Nhánh `feature/034-error-budget-server-spans` tạo từ `master` (`f86fb8e`); thư mục `specs/034-error-budget-server-spans/`.
- Q: Có commit không? → A: **Không commit.** Người dùng tự xem và commit.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ngân sách và phát hiện nhanh chỉ tính request mà chính service nhận (Priority: P1)

Là người đóng vai SRE, tôi muốn mức tiêu hao ngân sách của một service chỉ phản ánh các request mà chính service đó nhận (span Server), không cộng thêm các lời gọi đi ra hạ lưu hay thông điệp được publish (span Client/Producer). Có như vậy, một lỗi ở Products không trừ ngân sách của Gateway và BFF, và một lỗi 5xx của BFF không bị đếm hai lần.

**Why this priority**: Đây là nguyên nhân gốc đã đo được: 12/21 lỗi 5xx của `Bff.Api` tuần 05–11/10 là span Client, và 201/319 span được tính của BFF là span Client. Ngân sách hiện bị thổi phồng và phản ánh sai trách nhiệm từng service; mọi phần khác (dashboard, test, dọn trạng thái) phụ thuộc định nghĩa này.

**Independent Test**: Gửi một lỗi 5xx đi qua Gateway → BFF → Products. Xác nhận ngân sách 5xx của mỗi service chỉ tăng đúng theo số span Server lỗi của chính nó: BFF tăng 1 (không phải 2) cho một request, và một lỗi chỉ ở hạ lưu không làm tăng ngân sách của service gọi nếu service đó không trả lỗi cho người gọi. So sánh với truy vấn Elasticsearch lọc span Server.

**Acceptance Scenarios**:

1. **Given** một request qua Gateway → BFF → Products trả 5xx ở mọi tầng, **When** tính ngân sách 5xx, **Then** mỗi service được cộng đúng 1 span lỗi (span Server của nó); BFF không bị cộng thêm span Client.
2. **Given** BFF nhận request thành công nhưng một lời gọi đi ra hạ lưu của nó trả 5xx (và BFF tự xử lý), **When** tính ngân sách, **Then** span Client lỗi đó không làm tăng ngân sách 5xx hay khả dụng của BFF.
3. **Given** một span Client hoặc Producer chậm hơn ngưỡng p95/p99, **When** tính ngân sách độ trễ, **Then** span đó không nằm trong mẫu số lẫn tập span xấu của bất kỳ ngân sách nào.
4. **Given** rule `incident-fast-detection` đánh giá 5 phút gần nhất, **When** chỉ có span Client/Producer lỗi, **Then** rule không bắn; khi span Server lỗi vượt ngưỡng hiện hành, rule vẫn bắn như trước.
5. **Given** rule `error-budget-frozen` xét một ngày, **When** tính "ngày đạt SLO", **Then** chỉ span Server được tính; ngày không có span Server tính là không có traffic và tính là đạt (giữ quy tắc 027, 029).
6. **Given** một span Server nghiệp vụ trả 5xx hoặc vượt ngưỡng độ trễ, **When** tính ngân sách, **Then** nó vẫn được tính như trước (hành vi cho span Server không đổi); loại `/health*` của spec 033 vẫn áp dụng.

---

### User Story 2 - Hai dashboard khớp với điều kiện bắn của rule (Priority: P1)

Là người đóng vai SRE, tôi muốn các panel ngân sách trên hai dashboard `Ngân sách lỗi tuần — 7 service` và `Xử lý sự cố — 7 service` cùng chỉ đếm span Server, để con số trên màn hình khớp đúng với điều kiện bắn của rule và Bảng SLO không còn đếm lời gọi hạ lưu.

**Why this priority**: Nếu rule đổi mà dashboard còn đếm Client/Producer, người vận hành thấy hai con số mâu thuẫn trên cùng một màn hình (đã là tiêu chí SC-003 của 030 và 033).

**Independent Test**: Trên cùng một tuần dữ liệu, so từng (service, ngân sách) giữa dashboard và truy vấn Elasticsearch chỉ lọc span Server loại `/health*`; so tiếp với điều kiện bắn của rule mốc cùng thời điểm.

**Acceptance Scenarios**:

1. **Given** dữ liệu có cả span Server và Client, **When** mở `Ngân sách lỗi tuần — 7 service`, **Then** mọi panel đọc span (mức tiêu hao, hạn mức còn lại, error-rate/p95 theo ngày, tiêu hao lũy kế) chỉ đếm Server và khớp từng (service, ngân sách) với rule mốc.
2. **Given** cùng dữ liệu, **When** mở `Xử lý sự cố — 7 service`, **Then** Bảng SLO, 5xx/p95/traffic theo phút, phân bố status code và top endpoint chậm nhất chỉ đếm Server.
3. **Given** cùng dữ liệu, **When** xem panel lỗi gọi hạ lưu trên `Xử lý sự cố — 7 service`, **Then** panel vẫn hiển thị span Client như trước (không đổi).
4. **Given** span Server mới xuất hiện, **When** làm mới dashboard, **Then** số liệu cập nhật như trước, không phụ thuộc vào span Client.

---

### User Story 3 - Có test canh gác định nghĩa "chỉ đếm Server" (Priority: P2)

Là kỹ sư nền tảng, tôi muốn có test báo đỏ khi một rule ngân sách hoặc rule phát hiện nhanh bỏ điều kiện Server, theo đúng cách ngưỡng độ trễ và loại `/health*` đang được canh gác.

**Why this priority**: Rule chép tay điều kiện nên sẽ trôi lệch; `incident-fast-detection` hiện chưa có test nên lệch định nghĩa sẽ không ai thấy. Theo Nguyên tắc III (test trước).

**Independent Test**: Bỏ điều kiện Server khỏi một rule trong file export: bộ test báo đỏ đúng rule và đúng chỗ lệch.

**Acceptance Scenarios**:

1. **Given** file export 4 rule 027, **When** bộ test chạy, **Then** `ErrorBudgetRuleDefinitionTests` kiểm cả 4 rule có điều kiện chỉ đếm span Server; bỏ điều kiện khỏi bất kỳ rule nào làm test đỏ.
2. **Given** file export `incident-fast-detection`, **When** bộ test chạy, **Then** có test mới kiểm điều kiện chỉ đếm Server, và bỏ điều kiện làm test đỏ.
3. **Given** mỗi test mới hoặc sửa, **When** chạy trước khi sửa rule, **Then** test thấy đỏ; sau khi sửa rule thì xanh (Nguyên tắc III).
4. **Given** test hiện có của 027, 028, 033 (kể cả `HealthFailureRuleTests`), **When** chạy sau thay đổi, **Then** vẫn xanh.

---

### User Story 4 - Về sạch trạng thái cạn/đóng băng giả sau triển khai (Priority: P2)

Là người đóng vai SRE, sau khi định nghĩa đổi, tôi muốn trạng thái cạn/đóng băng hiện tại của 7 service (do bài thử 29a/30a ngày 09/10 đếm theo công thức cũ) được dọn để các con số mới không bị lẫn với lịch sử tính sai.

**Why this priority**: Sự kiện cạn cũ nằm trong `slo-error-budget-events` làm rule `error-budget-frozen` có thể coi service vẫn đóng băng dù ngân sách tính lại đã đúng. Không blocker cho định nghĩa nên P2.

**Independent Test**: Sau khi triển khai và dọn (đã được người dùng xác nhận ngay trước khi xoá), không service nào đang ở trạng thái cạn/đóng băng nếu ngân sách tính theo span Server chưa đạt 100%; khi chưa có request mới, không rule ngân sách nào active.

**Acceptance Scenarios**:

1. **Given** sự kiện cạn cũ do công thức cũ, **When** người dùng xác nhận dọn, **Then** các sự kiện đó bị xoá khỏi `slo-error-budget-events` và rule `error-budget-100` được Disable rồi Enable.
2. **Given** chưa xác nhận, **When** đến bước dọn, **Then** không xoá gì; hệ thống hỏi lại người dùng ngay trước thao tác.
3. **Given** sau dọn, **When** rule chạy, **Then** mức tiêu hao tính theo span Server của tuần hiện tại; service chỉ vượt 100% khi chính span Server của nó vượt ngưỡng.

---

### User Story 5 - Kịch bản Postman và tài liệu mô tả đúng định nghĩa mới (Priority: P3)

Là người diễn tập, tôi muốn có folder Postman 34 kiểm chứng việc đếm theo span Server và tài liệu mô tả đúng công thức "chỉ đếm span Server", để diễn tập và đối chiếu không dựa vào mô tả cũ.

**Why this priority**: Không đổi hành vi cảnh báo, chỉ đảm bảo kịch bản kiểm chứng và tài liệu nhất quán nên P3.

**Independent Test**: Chạy folder Postman 34 (nội dung chốt ở bước tasks) rồi đối chiếu số ngân sách của từng service với truy vấn lọc span Server; đọc tài liệu và sơ đồ để thấy không còn câu "tính mọi span".

**Acceptance Scenarios**:

1. **Given** folder Postman 34, **When** chạy với cờ tiêm lỗi bật, **Then** ngân sách mỗi service tăng đúng theo số span Server lỗi của nó, không nhân đôi ở BFF.
2. **Given** tài liệu PO/QA/Architect 034, `functional-debt.md`, `QA_Debt.md`, `technical-debt.md` và 3 sơ đồ drawio, **When** đọc, **Then** có mô tả công thức chỉ đếm Server và ghi nhận lệch Governance của PR #75 vào `technical-debt.md`.
3. **Given** tài liệu Kibana `06`, `07`, `08`, `alerts/README.md`, `dashboards/README.md`, contract rule 027/028 và mô tả collection Postman, **When** đọc, **Then** nêu rõ chỉ span Server được tính.

---

### Edge Cases

- **Span không xác định được loại (Server hay không)**: cách đối xử với span thiếu trường loại sẽ chốt ở giai đoạn plan sau khi kiểm chứng trên Kibana thật; không suy diễn ở spec.
- **BFF/Gateway**: span Server của chúng vẫn tính bình thường; chỉ span Client/Producer đi ra bị loại. Lỗi hạ lưu mà BFF/Gateway trả lại cho người gọi vẫn được tính ở span Server của chúng (trách nhiệm của chính service đó với người gọi).
- **Lỗi chỉ xuất hiện ở hạ lưu và được nuốt**: không còn trừ ngân sách service gọi; vẫn thấy ở panel lỗi gọi hạ lưu (dựa vào span Client, giữ nguyên).
- **Service chỉ phát span Client/Producer, không có span Server**: không có dữ liệu để tính; không hiện dòng và không rule nào bắn (giữ FR-013 của 029 và quy ước của 033).
- **Tuần đầu hoặc mẫu số nhỏ**: vài request Server xấu vẫn làm phần trăm vọt cao (giới hạn đã biết ở 029/030); người dùng đã bỏ ý định thêm sàn số request tối thiểu.
- **Lịch sử đã có trong Elasticsearch**: truy vấn mới lọc span Server cả với dữ liệu cũ nên mức tiêu hao tính lại ngay; không cần xoá span.
- **Sự kiện cạn cũ và alert đang active**: rule được sửa/import lại có thể ghi lại sự kiện "cạn" cho alert vừa active (giới hạn đã biết của 027/029); xử lý bằng bước dọn ở User Story 4.
- **Rule `health-failure` và các panel/rule không đọc ngân sách**: giữ nguyên.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Công thức ngân sách lỗi của cả 7 service PHẢI chỉ đếm span Server (request mà chính service nhận) cho cả mẫu số lẫn tập span xấu, cho cả 4 ngân sách (khả dụng, 5xx, p95, p99); span Client, Producer và mọi loại span khác PHẢI bị loại.
- **FR-002**: Việc chỉ đếm span Server PHẢI áp dụng cho 4 rule 027 (`error-budget-50`, `error-budget-75`, `error-budget-100`, `error-budget-frozen`) và rule 028 `incident-fast-detection`. Mọi quy tắc khác (tỷ lệ cho phép 1%/1%/5%/1%, ngưỡng độ trễ PR #75, mốc 50/75/100, cửa sổ rule, chu kỳ 5 phút, loại `/health*`, cột kết quả) KHÔNG đổi.
- **FR-003**: Rule `error-budget-frozen` PHẢI chỉ tính span Server khi xét "ngày đạt SLO"; ngày không có span Server là ngày không có traffic và tính là đạt (giữ quy tắc 027, 029).
- **FR-004**: Mọi panel đọc span của dashboard `Ngân sách lỗi tuần — 7 service` PHẢI chỉ đếm span Server và PHẢI khớp công thức của rule mốc ở FR-001.
- **FR-005**: Các panel đọc span của dashboard `Xử lý sự cố — 7 service` thuộc nhóm ngân sách/SLO (Bảng SLO, 5xx theo phút, p95 theo phút, traffic + 401/403 theo phút, phân bố status code, top endpoint chậm nhất) PHẢI chỉ đếm span Server. Panel lỗi gọi hạ lưu (dựa vào span Client), log lỗi, `dotnet.exceptions` và Phát hiện nhanh (đọc alert) PHẢI giữ nguyên.
- **FR-006**: Service không có span Server PHẢI không hiện dòng nào trên dashboard ngân sách và Bảng SLO và không rule nào được bắn vì thiếu dữ liệu (giữ FR-013 của 029 và quy ước 033).
- **FR-007**: `ErrorBudgetRuleDefinitionTests` PHẢI được bổ sung kiểm tra cả 4 rule 027 có điều kiện chỉ đếm Server; PHẢI có test mới cho `incident-fast-detection` kiểm điều kiện đó. Mỗi test mới/sửa PHẢI chạy thấy đỏ trước khi sửa rule (Nguyên tắc III). Test hiện có của 027, 028, 033 PHẢI vẫn xanh sau khi sửa.
- **FR-008**: Sau khi triển khai, PHẢI có bước dọn trạng thái cạn/đóng băng: xoá sự kiện cạn trong `slo-error-budget-events` và Disable/Enable rule `error-budget-100`. Thao tác xoá hay Disable/Enable PHẢI được hỏi lại người dùng ngay trước khi thực hiện.
- **FR-009**: Thay đổi KHÔNG được đổi SLO, tỷ lệ cho phép, ngưỡng độ trễ, mốc cảnh báo, chu kỳ, cửa sổ rule, chu kỳ ngân sách, hiến chương, hành vi phản hồi của bất kỳ endpoint nào, hay quy tắc loại `/health*` của 033; chỉ đổi tập span được tính. KHÔNG thêm sàn số request tối thiểu.
- **FR-010**: Hiến chương KHÔNG được sửa trong spec này. Lệch phiên bản của PR #75 (ngưỡng độ trễ đã đổi mà phiên bản vẫn 2.0.0) PHẢI được ghi nhận trong `technical-debt.md` như việc riêng.
- **FR-011**: Tài liệu và hiện vật đang dùng của 027, 028, 029, 030, 033 PHẢI được sửa tại chỗ để mô tả công thức chỉ đếm span Server: tài liệu Kibana `06`, `07`, `08`, `alerts/README.md`, `dashboards/README.md`; contract rule 027/028; PO/QA/Architect liên quan; sơ đồ drawio nhắc công thức ngân sách; mô tả collection Postman. KHÔNG sửa bản ghi lịch sử (mục cũ của `QA_Debt.md`, `docs/dien-tap-chaos-engineering/ket-qua/`, `specs/002-gateway-bff-routing/`).
- **FR-012**: Spec này PHẢI có bộ tài liệu đi kèm theo khuôn 027–033: file PO, QA, Architect; mục 034 trong `functional-debt.md`, `QA_Debt.md`, `technical-debt.md` (mọi phát hiện QA chỉ ghi trong `QA_Debt.md`); 3 sơ đồ drawio; folder Postman 34. Tên file, tên drawio và nội dung folder Postman chưa chốt, hỏi lại ở `/speckit-tasks`.
- **FR-013**: Việc dọn Elastic hay xoá bất kỳ object Kibana đang chạy PHẢI được hỏi lại người dùng ngay trước khi thực hiện.

### Key Entities *(include if feature involves data)*

- **Span Server**: span ghi lại request mà chính service nhận; là tập duy nhất được tính vào ngân sách (cùng loại `/health*` của 033).
- **Span Client / Producer**: span ghi lại lời gọi đi ra hạ lưu hoặc việc publish thông điệp; không được tính vào ngân sách; vẫn dùng cho panel lỗi gọi hạ lưu.
- **Ngân sách tuần của service (4 ngân sách)**: như 029/030/033, nay chỉ tính trên span Server nghiệp vụ.
- **Sự kiện cạn (`slo-error-budget-events`)**: bản ghi ngân sách đạt 100% làm căn cứ trạng thái đóng băng; các sự kiện sinh ra từ công thức cũ cần được dọn sau triển khai.
- **Rule phát hiện nhanh (`incident-fast-detection`)**: rule 028 xét 5 phút gần nhất; nay cùng định nghĩa span Server với ngân sách.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Với một lỗi đi qua Gateway → BFF → Products, mỗi service được cộng đúng 1 span lỗi vào ngân sách 5xx của nó (BFF không bị cộng 2); với tuần 05–11/10, số lỗi 5xx của `Bff.Api` được tính giảm từ 21 xuống 9 (loại 12 span Client) và số span được tính giảm từ 319 xuống 118 (loại 201 span Client) khi chạy lại trên cùng dữ liệu.
- **SC-002**: Span Client hoặc Producer lỗi/chậm không làm tăng ngân sách nào và không làm `incident-fast-detection` bắn; span Server lỗi/chậm vượt ngưỡng hiện hành vẫn làm rule bắn như trước.
- **SC-003**: Mức tiêu hao trên cả hai dashboard khớp từng (service, ngân sách) với truy vấn Elasticsearch lọc span Server loại `/health*` và với điều kiện bắn của rule mốc cùng thời điểm (sai lệch ≤ 2 điểm % do dữ liệu tăng giữa hai lần đọc, như 030/033).
- **SC-004**: 5/5 rule (4 rule 027 + `incident-fast-detection`) có test canh gác điều kiện chỉ đếm Server; bộ test quy ước chạy xanh và mỗi test mới/sửa đã chạy thấy đỏ trước khi sửa rule.
- **SC-005**: Sau bước dọn đã được người dùng xác nhận, 0 service ở trạng thái đóng băng/cạn giả do công thức cũ; khi chưa có request mới, 0 alert ngân sách active.
- **SC-006**: Tìm trên toàn repo không còn tài liệu hay hiện vật đang dùng nào mô tả công thức ngân sách "tính mọi span"; ngoại lệ chỉ là các bản ghi lịch sử đã loại trừ ở FR-011.
- **SC-007**: Hai dashboard và hai file rule import vào Kibana sạch bằng một lệnh mỗi file, hiển thị đúng sau thay đổi; panel lỗi gọi hạ lưu vẫn hiện span Client như trước.

## Assumptions

- Spec 033 (loại `/health*`), spec 030 (hai dashboard, id cố định, file ndjson độc lập) và PR #75 (ngưỡng độ trễ) đã có trên `master`; spec này xây trên đó, không đổi cấu trúc hai dashboard ngoài các panel liên quan.
- Cách nhận diện span Server (trường loại span trong dữ liệu trace), cách xử lý span thiếu trường loại, và cách viết điều kiện trong ES|QL và Lens là chi tiết triển khai, cần kiểm chứng trên Kibana thật ở giai đoạn plan; nếu không chạy được thì dừng và hỏi lại người dùng.
- Một số panel trên `Xử lý sự cố — 7 service` hiện đã lọc theo loại span (panel lỗi gọi hạ lưu); danh sách panel chính xác cần đổi và cần giữ sẽ được đối chiếu ở giai đoạn plan và hỏi lại nếu có panel mơ hồ.
- Tên file PO/QA/Architect (dự kiến theo khuôn `docs/PO/034_PO_…`), tên 3 file drawio, tên folder Postman 34 và nội dung các request của folder đó chưa chốt; hỏi lại ở `/speckit-tasks`, không suy diễn.
- Hiến chương không đổi trong spec này (người dùng chọn xử lý lệch phiên bản của PR #75 như việc riêng).
- Bước dọn trạng thái (FR-008) thực hiện sau khi triển khai và chỉ khi người dùng xác nhận ngay trước thao tác.
- Giới hạn mẫu số nhỏ của môi trường local (029/030) vẫn còn; spec này chỉ ghi nhận, không thêm logic.
- "Người vận hành" vẫn là một người đóng vai SRE/Dev; dừng merge khi cạn ngân sách là cam kết quy trình, không có cơ chế chặn tự động (như 027).
