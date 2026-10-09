# Feature Specification: Chính sách ngân sách lỗi (error budget) và ngưỡng cảnh báo gắn với SLO từng service

**Feature Branch**: `claude/scrum-35-backlog-export-cefede`

**Created**: 2026-10-01

**Status**: Draft

**Input**: User description: "Jira SCRUM-35 [OPERATE-5] Define error-budget policy and alerting thresholds — As the SRE-hat-wearer, I want an error-budget policy and alerting thresholds tied to the Phase 4 SLOs so that a sustained breach has an actual, defined consequence instead of a red dashboard nobody acts on (Principle VIII). Acceptance Criteria: (1) Given a service's SLOs, when its error budget is calculated, then the policy defines what \"exhausted\" means numerically (e.g., % of monthly budget consumed). (2) Given the error budget is exhausted, when I check the policy, then it states that reliability work takes priority over new feature work for that service until recovery. (3) Given a budget threshold is crossed, when it happens, then an alert fires — the breach is not discovered by manually checking a dashboard. Test Scenarios: 1. Simulate enough synthetic errors to exhaust a service's monthly error budget — confirm the alert fires at the defined threshold. 2. Review the written policy — confirm it names who/what stops (feature work) when budget is exhausted. 3. Confirm the alert routes somewhere I'll actually see it (not a channel nobody watches)."

## Clarifications

### Session 2026-10-01

- Q: Ngân sách lỗi được tính từ những chỉ tiêu SLO nào? → A: Cả ba nhóm — độ khả dụng, tỷ lệ lỗi 5xx và độ trễ.
- Q: Cửa sổ thời gian tính ngân sách? → A: Tháng lịch (từ ngày 1 đến cuối tháng), ngân sách đặt lại đầy đủ vào đầu mỗi tháng.
  - *Thay bởi spec 029 (2026-10-05)*: tuần lịch giờ Việt Nam (thứ Hai 00:00 → Chủ nhật 23:59), SLO khả dụng 99%/tuần và 5xx dưới 1% (hiến chương 2.0.0) — xem [`specs/029-error-budget-weekly/spec.md`](../029-error-budget-weekly/spec.md).
- Q: Ngưỡng "cạn" và các mốc cảnh báo? → A: Cảnh báo ở các mốc 50%, 75% và 100% ngân sách tháng đã tiêu; "cạn" = đã tiêu 100%.
  - *Thay bởi spec 029 (2026-10-05).*
- Q: Kênh nhận cảnh báo? → A: Chỉ trong Kibana (không đẩy ra email/Slack).
- Q: Làm sao đảm bảo người vận hành thực sự nhìn thấy cảnh báo (tiêu chí kiểm thử 3)? → A: Trạng thái cảnh báo và mức tiêu hao ngân sách được hiển thị ngay trên dashboard Ngân sách lỗi tuần mà người vận hành mở mỗi ngày.
- Q: Với độ trễ, request nào bị tính là "xấu"? → A: Hai ngân sách độ trễ riêng: tối đa 5% request được phép vượt ngưỡng p95, và tối đa 1% request được phép vượt ngưỡng p99.
- Q: Khi ngân sách cạn, ai dừng cái gì và khi nào là hồi phục? → A: Người vận hành (vai SRE/Dev) dừng merge tính năng mới vào service đó, chỉ làm việc nâng độ tin cậy, cho tới khi service đạt SLO liên tục N ngày — không phụ thuộc việc ngân sách đặt lại đầu tháng.
- Q: N bằng bao nhiêu? → A: 3 ngày.
  - *Thay bởi fix/frozen-panel-status (2026-10-09)*: hồi phục theo mức tiêu hao tuần, không còn đếm ngày. Sau một lần cạn: `active` (mức tiêu hao cao nhất trong 4 ngân sách ≥ 100%) → `recovering` (dưới 100% nhưng ≥ 75%, hoặc tuần chưa có request Server) → `recovered` (tuần có ≥ 1 request và dưới 75%; giữ tới lần cạn kế tiếp). Đóng băng ở `active` và `recovering`.
- Q: Phạm vi service? → A: Cả 7 service đã khai báo SLO trong manifest (bao gồm gateway và BFF).
- Q: Độ khả dụng 99.9% được đo bằng gì? → A: Tỷ lệ request thành công (không trả 5xx) trên tổng số request, lấy từ telemetry hiện có.
  - *Thay bởi spec 029 (2026-10-05).*
- Q: Cảnh báo phát ra thế nào khi đã vượt một mốc? → A: Giữ ở trạng thái hoạt động liên tục chừng nào mức tiêu hao còn ở trên mốc.
- Q: Văn bản chính sách đặt ở đâu? → A: Ngay trong manifest của từng service, cạnh phần khai báo SLO.
- Q: Service bị coi là "cạn ngân sách" khi nào? → A: Khi bất kỳ một trong bốn ngân sách (khả dụng, 5xx, độ trễ p95, độ trễ p99) đạt 100%; các mốc 50%/75%/100% được cảnh báo riêng cho từng ngân sách.
- Q (phiên `/speckit-plan`): Ranh giới "tháng lịch" và "ngày" tính theo múi giờ nào? → A: Giờ Việt Nam (UTC+7) — tháng/ngày bắt đầu lúc 00:00 giờ Việt Nam.
  - *Thay bởi spec 029 (2026-10-05).*
- Q (phiên `/speckit-plan`): Khi đếm 3 ngày đạt SLO liên tục, một ngày service không có request nào được tính thế nào? → A: Tính là đạt.
  - *Thay bởi fix/frozen-panel-status (2026-10-09)*: không còn đếm ngày; tuần chưa có request Server nào thì service vẫn `recovering` (vẫn đóng băng).
- Q (phiên `/speckit-implement`): Mã alert của Kibana ghép từ mọi cột kết quả, nên giữ cột % tiêu hao trong alert sẽ làm alert bị tạo lại mỗi lần chạy. Xử lý thế nào? → A: Alert chỉ mang service + ngân sách; mức tiêu hao (%) hiển thị ở bảng mức tiêu hao đặt cạnh bảng alert.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Chính sách ngân sách lỗi được định nghĩa bằng con số và ghi trong manifest (Priority: P1)

Là người đóng vai SRE, tôi muốn manifest của mỗi service ghi rõ chính sách ngân sách lỗi — ngân sách được tính từ những chỉ tiêu nào, trên cửa sổ thời gian nào, thế nào là "cạn" bằng con số cụ thể, và điều gì xảy ra khi cạn — để khi SLO bị vi phạm kéo dài, hệ quả đã được định nghĩa sẵn chứ không phải tranh luận lại mỗi lần.

**Why this priority**: Không có định nghĩa bằng con số thì không thể tính ngân sách, không thể đặt ngưỡng cảnh báo (User Story 2) và cũng không thể áp dụng hệ quả (User Story 3). Đây là tiền đề của toàn bộ tính năng.

**Independent Test**: Mở manifest của từng service trong 7 service, xác nhận mỗi manifest có phần chính sách ngân sách lỗi, ghi đủ: bốn ngân sách (khả dụng, 5xx, độ trễ p95, độ trễ p99) với tỷ lệ cho phép cụ thể, cửa sổ tuần lịch, các mốc 50%/75%/100%, định nghĩa "cạn" = 100%, và hệ quả khi cạn.

**Acceptance Scenarios**:

1. **Given** SLO của một service đã được khai báo trong manifest, **When** tôi đọc phần chính sách ngân sách lỗi của service đó, **Then** chính sách định nghĩa "cạn" bằng con số: một ngân sách bị coi là cạn khi đã tiêu 100% lượng request "xấu" được phép trong tuần lịch hiện tại.
2. **Given** chính sách ngân sách lỗi của một service, **When** tôi kiểm tra cách tính, **Then** chính sách nêu rõ bốn ngân sách riêng và tỷ lệ cho phép của từng cái: độ khả dụng (tỷ lệ request không trả 5xx ≥ 99%), tỷ lệ lỗi 5xx (< 1% số request), độ trễ p95 (tối đa 5% request vượt ngưỡng p95 của service), độ trễ p99 (tối đa 1% request vượt ngưỡng p99 của service).
3. **Given** cả 7 service đã khai báo SLO, **When** tôi rà soát toàn bộ manifest, **Then** không service nào thiếu phần chính sách ngân sách lỗi, và tỷ lệ cho phép của mỗi ngân sách được suy ra đúng từ SLO đã khai báo của chính service đó (bao gồm ngoại lệ nếu có).

---

### User Story 2 - Cảnh báo tự động khi ngân sách vượt mốc, hiển thị ở nơi người vận hành nhìn mỗi ngày (Priority: P1)

Là người đóng vai SRE, tôi muốn một cảnh báo tự động kích hoạt khi bất kỳ ngân sách nào của một service vượt mốc 50%, 75% hoặc 100% trong tuần, và trạng thái cảnh báo đó hiện ngay trên dashboard Ngân sách lỗi tuần tôi vẫn mở, để tôi phát hiện vi phạm nhờ cảnh báo chứ không phải tình cờ tự soi số liệu.

**Why this priority**: Tiêu chí chấp nhận 3 của Jira yêu cầu vi phạm không được phát hiện bằng cách tự kiểm tra dashboard thủ công. Chính sách (User Story 1) không có cảnh báo thì vẫn là "dashboard đỏ không ai hành động" — chính vấn đề story này muốn giải quyết.

**Independent Test**: Tạo đủ lỗi tổng hợp (synthetic) cho một service để tiêu hết ngân sách tuần; xác nhận cảnh báo kích hoạt lần lượt tại các mốc 50%, 75%, 100%, và trạng thái cảnh báo hiện trên dashboard Ngân sách lỗi tuần.

**Acceptance Scenarios**:

1. **Given** một ngân sách của một service đang ở dưới 50%, **When** lượng request xấu khiến mức tiêu hao vượt 50% ngân sách tuần, **Then** một cảnh báo mốc 50% kích hoạt cho đúng service và đúng ngân sách đó, không cần ai mở dashboard để phát hiện.
2. **Given** lỗi tổng hợp được tạo đủ để tiêu hết ngân sách 5xx tuần của một service, **When** mức tiêu hao lần lượt vượt 50%, 75%, 100%, **Then** cảnh báo tương ứng kích hoạt ở đúng từng mốc đã định nghĩa (không sớm hơn, không muộn hơn quá khoảng làm mới dữ liệu của hệ thống).
3. **Given** một cảnh báo đang hoạt động, **When** người vận hành mở dashboard Ngân sách lỗi tuần, **Then** dashboard hiển thị rõ service nào, ngân sách nào, đang ở mốc nào, và mức tiêu hao hiện tại (%) — người vận hành không cần vào một màn hình khác để biết có cảnh báo.
4. **Given** mức tiêu hao của một ngân sách vẫn ở trên mốc đã vượt, **When** thời gian tiếp tục trôi trong tuần, **Then** cảnh báo giữ ở trạng thái hoạt động liên tục, không tự biến mất cho tới khi mức tiêu hao không còn ở trên mốc (ví dụ khi ngân sách đặt lại vào đầu tuần mới).
5. **Given** 4 ngân sách của cùng một service, **When** chỉ ngân sách độ trễ p95 vượt mốc 75%, **Then** chỉ cảnh báo của ngân sách độ trễ p95 kích hoạt; ba ngân sách còn lại giữ nguyên trạng thái của chúng.

---

### User Story 3 - Hệ quả khi ngân sách cạn: ưu tiên độ tin cậy hơn tính năng mới cho tới khi hồi phục (Priority: P2)

Là người đóng vai SRE, tôi muốn chính sách nêu rõ: khi bất kỳ ngân sách nào của một service cạn, người vận hành dừng merge tính năng mới vào service đó và chỉ làm việc nâng độ tin cậy, cho tới khi service hồi phục (`recovered`: tuần đã có request và mức tiêu hao cao nhất trong 4 ngân sách dưới 75%) — để vi phạm kéo dài có hệ quả thật và có điều kiện thoát rõ ràng. *(sửa bởi nhánh fix/frozen-panel-status, 2026-10-09; trước đây: "đạt đủ SLO liên tục 3 ngày".)*

**Why this priority**: Đây là phần "hệ quả" trong Nguyên tắc VIII. Nó phụ thuộc vào việc đã có định nghĩa (User Story 1) và tín hiệu cảnh báo (User Story 2); bản thân việc dừng tính năng là quy trình con người, không cần thêm cơ chế kỹ thuật để có giá trị.

**Independent Test**: Đọc chính sách trong manifest của một service, xác nhận nó nêu rõ ai dừng (người vận hành vai SRE/Dev), dừng cái gì (merge tính năng mới vào service đó), được làm gì (công việc nâng độ tin cậy), và điều kiện hồi phục (ba trạng thái `active` / `recovering` / `recovered` theo mức tiêu hao tuần, ngưỡng hồi phục 75%). *(sửa bởi nhánh fix/frozen-panel-status, 2026-10-09; trước đây: "đạt mọi chỉ tiêu SLO liên tục 3 ngày".)*

**Acceptance Scenarios**:

1. **Given** ngân sách của một service đã cạn, **When** tôi đọc chính sách của service đó, **Then** chính sách nêu rằng công việc nâng độ tin cậy được ưu tiên hơn tính năng mới cho service đó cho tới khi hồi phục, và nêu rõ ai dừng cái gì.
2. **Given** một service đang ở trạng thái cạn ngân sách, **When** tuần hiện tại đã có request và mức tiêu hao cao nhất trong 4 ngân sách xuống dưới 75%, **Then** service chuyển `recovered` và được phép merge tính năng mới trở lại — kể cả khi tuần lịch chưa kết thúc; mức tiêu hao từ 75% tới dưới 100% là `recovering`, vẫn đóng băng. *(sửa bởi nhánh fix/frozen-panel-status, 2026-10-09; trước đây: đạt đủ mọi chỉ tiêu SLO liên tục 3 ngày.)*
3. **Given** một service cạn ngân sách vào cuối tuần, **When** sang tuần mới và ngân sách đặt lại về đầy đủ nhưng tuần mới chưa có request Server nào, **Then** service vẫn `recovering` (chưa hồi phục) — việc đặt lại ngân sách đầu tuần không tự động gỡ trạng thái đóng băng. *(sửa bởi nhánh fix/frozen-panel-status, 2026-10-09; trước đây: chưa đạt SLO liên tục 3 ngày.)*
4. **Given** một service đang ở trạng thái cạn ngân sách, **When** người vận hành mở dashboard Ngân sách lỗi tuần, **Then** dashboard thể hiện rõ service đó đang trong trạng thái "cạn ngân sách — ưu tiên độ tin cậy".

---

### Edge Cases

- Service không có traffic (hoặc rất ít) trong tuần: ngân sách tính theo tỷ lệ request nên không có request thì không có tiêu hao; hệ thống phải thể hiện "không có dữ liệu" thay vì "0% tiêu hao" (thống nhất với FR-006 của đặc tả 021), và không phát cảnh báo sai.
- Lưu lượng rất thấp khiến chỉ một vài request lỗi đã vượt các mốc: chính sách vẫn áp dụng đúng con số đã định nghĩa; trường hợp này cần được ghi nhận là giới hạn đã biết của môi trường thực hành lưu lượng thấp.
- Telemetry bị gián đoạn tạm thời: không được hiểu nhầm khoảng thiếu dữ liệu thành request thành công hay request lỗi; cảnh báo không được kích hoạt hay tắt chỉ vì thiếu dữ liệu.
- Chuyển tuần lịch: ngân sách đặt lại về đầy đủ, cảnh báo mốc của tuần cũ tắt; nhưng trạng thái đóng băng của service (nếu có) vẫn giữ cho tới khi đạt điều kiện hồi phục (tuần mới có request và mức tiêu hao dưới 75%; xem User Story 3, kịch bản 3). *(sửa bởi nhánh fix/frozen-panel-status, 2026-10-09; trước đây: điều kiện hồi phục 3 ngày.)*
- Service có ngoại lệ SLO đã ghi trong manifest (ví dụ BFF với ngưỡng độ trễ nới hơn): ngân sách độ trễ phải tính theo ngưỡng của chính service đó, không theo ngưỡng mặc định.
- Ngân sách khả dụng và ngân sách 5xx đều được tính từ tỷ lệ request trả 5xx nên có thể tiêu hao gần như đồng thời; hai cảnh báo này có thể kích hoạt cùng lúc và điều đó được chấp nhận.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Manifest của mỗi service trong 7 service đã khai báo SLO PHẢI có phần chính sách ngân sách lỗi, đặt cạnh phần khai báo SLO.
- **FR-002**: Chính sách PHẢI định nghĩa bốn ngân sách riêng cho mỗi service, mỗi ngân sách có tỷ lệ request "xấu" được phép trong tuần: (a) độ khả dụng — request trả 5xx bị tính là xấu, mục tiêu ≥ 99% request thành công; (b) tỷ lệ lỗi 5xx — request trả 5xx bị tính là xấu, tối đa dưới 1% số request; (c) độ trễ p95 — request vượt ngưỡng p95 của service bị tính là xấu, tối đa 5% số request; (d) độ trễ p99 — request vượt ngưỡng p99 của service bị tính là xấu, tối đa 1% số request.
- **FR-003**: Ngân sách PHẢI được tính trên cửa sổ tuần lịch (từ thứ Hai 00:00 tới Chủ nhật 23:59, ranh giới lúc 00:00 giờ Việt Nam, UTC+7) và đặt lại về đầy đủ vào đầu mỗi tuần.
- **FR-004**: Chính sách PHẢI định nghĩa "cạn" bằng con số: một ngân sách cạn khi đã tiêu 100% lượng request xấu được phép trong tuần; một service bị coi là cạn ngân sách khi bất kỳ một trong bốn ngân sách của nó cạn.
- **FR-005**: Hệ thống PHẢI tự động tính mức tiêu hao (%) của từng ngân sách của từng service từ telemetry đang thu thập, không cần thao tác thủ công.
- **FR-006**: Hệ thống PHẢI kích hoạt cảnh báo tự động khi mức tiêu hao của bất kỳ ngân sách nào của bất kỳ service nào vượt các mốc 50%, 75% và 100%, riêng cho từng ngân sách và từng service.
- **FR-007**: Cảnh báo đã kích hoạt PHẢI giữ ở trạng thái hoạt động liên tục chừng nào mức tiêu hao còn ở trên mốc tương ứng.
- **FR-008**: Cảnh báo PHẢI được quản lý trong Kibana và trạng thái của chúng (service, ngân sách, mốc, mức tiêu hao hiện tại) PHẢI được hiển thị trên dashboard Ngân sách lỗi tuần hiện có; không yêu cầu đẩy cảnh báo ra kênh bên ngoài (email, Slack).
- **FR-009**: Chính sách PHẢI nêu rõ hệ quả khi service cạn ngân sách: người vận hành (vai SRE/Dev) dừng merge tính năng mới vào service đó và chỉ làm công việc nâng độ tin cậy cho service đó.
- **FR-010**: Chính sách PHẢI nêu rõ điều kiện hồi phục theo **mức tiêu hao cao nhất trong 4 ngân sách của tuần lịch hiện tại** (giờ Việt Nam). Sau một lần cạn, service ở một trong ba trạng thái: `active` khi mức tiêu hao ≥ 100%; `recovering` khi dưới 100% nhưng ≥ 75%, hoặc tuần chưa có request Server nào; `recovered` khi tuần đã có ít nhất 1 request và mức tiêu hao dưới 75%. Đã `recovered` thì giữ nguyên dù mức tiêu hao lên lại 75–99%, chỉ quay lại `active` khi chạm 100% (sự kiện cạn mới). Dừng merge tính năng mới ở cả `active` và `recovering`. Việc ngân sách đặt lại đầu tuần KHÔNG tự động gỡ trạng thái cạn ngân sách. *(Sửa bởi nhánh fix/frozen-panel-status, 2026-10-09; trước đây: hồi phục khi đạt đủ mọi chỉ tiêu SLO liên tục 3 ngày, ngày không có request tính là đạt.)*
- **FR-011**: Dashboard Ngân sách lỗi tuần PHẢI thể hiện service nào đang ở trạng thái "cạn ngân sách — ưu tiên độ tin cậy".
- **FR-012**: Khi một service không có request nào trong tuần (hoặc thiếu dữ liệu do gián đoạn telemetry), hệ thống PHẢI thể hiện "không có dữ liệu" và KHÔNG được kích hoạt hay tắt cảnh báo chỉ vì thiếu dữ liệu.
- **FR-013**: Ngân sách độ trễ PHẢI dùng ngưỡng p95/p99 đã khai báo của chính service đó (kể cả ngoại lệ có lý do), không dùng ngưỡng mặc định thay thế.
- **FR-014**: PHẢI có một cách lặp lại được để tạo lỗi tổng hợp đủ làm cạn ngân sách tuần của một service, nhằm xác minh cảnh báo kích hoạt đúng các mốc đã định nghĩa.
- **FR-015**: Việc thêm chính sách, tính ngân sách và cảnh báo KHÔNG được thay đổi hành vi phản hồi hiện có của bất kỳ endpoint nào.

### Key Entities *(include if feature involves data)*

- **Chính sách ngân sách lỗi của service**: gắn với một service, nằm trong manifest cạnh khai báo SLO; gồm bốn ngân sách, cửa sổ tuần lịch, các mốc cảnh báo, định nghĩa "cạn", hệ quả khi cạn và điều kiện hồi phục.
- **Ngân sách**: một trong bốn loại (khả dụng, 5xx, độ trễ p95, độ trễ p99) của một service; có quy tắc xác định request "xấu" và tỷ lệ request xấu được phép trong tuần.
- **Mức tiêu hao ngân sách**: với mỗi ngân sách của mỗi service, tỷ lệ phần trăm ngân sách đã tiêu trong tuần lịch hiện tại, tính tự động từ telemetry.
- **Cảnh báo ngân sách**: gắn với một service, một ngân sách và một mốc (50%/75%/100%); có trạng thái hoạt động hoặc không hoạt động.
- **Trạng thái cạn ngân sách của service**: bắt đầu khi bất kỳ ngân sách nào của service đạt 100% (`active`); qua `recovering` khi mức tiêu hao tuần dưới 100% nhưng còn ≥ 75% hoặc tuần chưa có request; kết thúc (`recovered`) khi tuần có request và mức tiêu hao dưới 75%, giữ tới lần cạn kế tiếp. *(sửa bởi nhánh fix/frozen-panel-status, 2026-10-09; trước đây: kết thúc khi service đạt đủ SLO liên tục 3 ngày.)*

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% trong 7 service có phần chính sách ngân sách lỗi trong manifest, ghi đủ bốn ngân sách với tỷ lệ cho phép bằng con số, cửa sổ tuần lịch, các mốc 50%/75%/100%, định nghĩa "cạn", hệ quả và điều kiện hồi phục theo mức tiêu hao tuần (ngưỡng 75%, tối thiểu 1 request). *(sửa bởi nhánh fix/frozen-panel-status, 2026-10-09; trước đây: điều kiện hồi phục 3 ngày.)*
- **SC-002**: Khi tạo đủ lỗi tổng hợp để tiêu hết ngân sách tuần của một service, cảnh báo kích hoạt ở cả ba mốc 50%, 75% và 100%, mỗi mốc kích hoạt trong vòng một chu kỳ làm mới dữ liệu kể từ khi mức tiêu hao thực tế vượt mốc.
- **SC-003**: 100% cảnh báo đang hoạt động hiển thị trên dashboard Ngân sách lỗi tuần với đủ thông tin service, ngân sách và mốc, đặt cạnh bảng mức tiêu hao hiện tại (%) của cùng service và ngân sách; người vận hành biết có cảnh báo mà không cần mở màn hình nào khác.
- **SC-004**: Người đọc chính sách trả lời được ba câu hỏi "ai dừng", "dừng cái gì" và "khi nào được tiếp tục" chỉ từ manifest của service, không cần tra tài liệu khác.
- **SC-005**: Không có cảnh báo nào kích hoạt cho một service không có request trong tuần hoặc chỉ do thiếu dữ liệu telemetry.

## Assumptions

- Telemetry (traces OTel qua Elasticsearch) và dashboard Ngân sách lỗi tuần trên Kibana đã có từ đặc tả 021 và Nguyên tắc VII; tính năng này xây trên nền đó, không dựng mới hệ thống thu thập.
- Bảy service và ngưỡng SLO của chúng (kể cả ngoại lệ độ trễ của BFF) đã được khai báo trong manifest theo đặc tả 021; tính năng này không thay đổi các ngưỡng đó.
- Vì độ khả dụng được đo bằng tỷ lệ request không trả 5xx, ngân sách khả dụng (1% request xấu được phép) và ngân sách 5xx (dưới 1%) gần như trùng nhau về dữ liệu nguồn; hai ngân sách vẫn được giữ riêng theo đúng khai báo SLO hiện có.
- "Người vận hành" là một người duy nhất đóng vai SRE/Dev trong dự án thực hành (theo `docs/roadmap.md`); việc dừng merge tính năng mới là cam kết quy trình được ghi trong chính sách, không bắt buộc phải có cơ chế kỹ thuật tự động chặn merge.
- Chu kỳ làm mới dữ liệu cụ thể của việc tính tiêu hao và đánh giá cảnh báo là chi tiết triển khai, sẽ được chốt ở giai đoạn lập kế hoạch.
- Đẩy cảnh báo ra kênh bên ngoài (email, Slack) và kiểm tra uptime tổng hợp nằm ngoài phạm vi đặc tả này.
