# Feature Specification: Khai báo và đo lường liên tục SLO theo từng service trong service manifest

**Feature Branch**: `code/Declare-per-service-SLOs-in-manifest`

**Created**: 2026-09-09

**Status**: Draft

**Input**: User description: "Jira SCRUM-29 [RESILIENCE-4] Declare per-service SLOs in the service manifest — As the SRE-hat-wearer, I want each service to declare its latency, error-rate, and availability SLOs in its manifest so that performance is a stated budget, not an aspiration (Principle VIII). Acceptance Criteria: (1) Given a service manifest, when I inspect it, then it declares p95/p99 latency, error-rate, and availability targets. (2) Given no service documents a justified alternative, when I compare its SLOs to the constitution defaults, then they match (BFF read p95≤300ms/p99≤800ms; internal API p95≤150ms/p99≤500ms; 99.9% availability; <0.1% 5xx). (3) Given the SLOs are declared, when telemetry from Phase 3 is queried, then they are measured continuously, not just documented. Test Scenarios: 1. Pull up each service's manifest and confirm SLOs are present and non-placeholder. 2. Cross-check declared SLOs against a live dashboard fed by OTel metrics. 3. Intentionally introduce a slow endpoint — confirm the SLO dashboard shows the budget being consumed."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mọi service khai báo đầy đủ ngân sách SLO trong manifest (Priority: P1)

Là người đóng vai SRE, tôi muốn manifest của mỗi service (toàn bộ service nghiệp vụ, gateway, và BFF) khai báo rõ ràng ba nhóm chỉ tiêu hiệu năng — độ trễ p95/p99, tỷ lệ lỗi, và độ khả dụng — để hiệu năng của từng service trở thành một ngân sách được cam kết bằng con số cụ thể, thay vì một kỳ vọng chung chung không thể kiểm chứng.

**Why this priority**: Đây là tiền đề bắt buộc của toàn bộ tính năng. Nếu manifest không khai báo ngân sách, sẽ không có gì để so khớp với chuẩn nền tảng (User Story 2) và cũng không có gì để đo lường liên tục (User Story 3) — hai giá trị còn lại của tính năng phụ thuộc hoàn toàn vào việc khai báo này tồn tại trước.

**Independent Test**: Mở manifest của từng service đang có trong hệ thống, xác nhận mỗi manifest đều có đủ ba nhóm chỉ tiêu (độ trễ p95, độ trễ p99, tỷ lệ lỗi, độ khả dụng) với giá trị số cụ thể, không có ô để trống hay giá trị giữ chỗ (placeholder).

**Acceptance Scenarios**:

1. **Given** manifest của một service bất kỳ trong hệ thống, **When** tôi mở và đọc nội dung manifest đó, **Then** manifest khai báo đầy đủ độ trễ p95, độ trễ p99, ngưỡng tỷ lệ lỗi, và mục tiêu độ khả dụng, mỗi giá trị là một con số cụ thể chứ không phải để trống hay ghi giá trị giữ chỗ.
2. **Given** toàn bộ service đang chạy trong hệ thống (service nghiệp vụ, gateway, BFF) đã được rà soát, **When** tôi tổng hợp kết quả, **Then** không có service nào thiếu bất kỳ chỉ tiêu nào trong bốn chỉ tiêu (độ trễ p95, độ trễ p99, tỷ lệ lỗi, độ khả dụng).
3. **Given** một service mới được bổ sung vào hệ thống sau này, **When** service đó được đưa vào vận hành, **Then** manifest của service đó cũng phải khai báo đầy đủ bốn chỉ tiêu như mọi service khác, không có ngoại lệ mặc định.

---

### User Story 2 - Ngân sách SLO khai báo khớp chuẩn nền tảng, hoặc có lý do ngoại lệ được ghi rõ (Priority: P1)

Là người đóng vai SRE, tôi muốn giá trị SLO mà mỗi service khai báo phải khớp với bộ chuẩn mặc định của nền tảng (theo phân loại "BFF hướng client" hoặc "API nội bộ"), trừ khi service đó ghi rõ lý do vì sao cần một ngân sách khác, để việc so sánh hiệu năng giữa các service luôn dựa trên một thước đo thống nhất và mọi sai khác đều minh bạch, có căn cứ.

**Why this priority**: Khai báo (User Story 1) mà giá trị sai lệch tùy tiện so với chuẩn chung thì ngân sách trở nên vô nghĩa — mỗi service tự đặt ra tiêu chuẩn riêng khiến không ai còn biết đâu là mức hiệu năng "chấp nhận được" của toàn hệ thống. Vì vậy mức ưu tiên ngang với User Story 1.

**Independent Test**: Với từng service, so sánh bốn chỉ tiêu đã khai báo (độ trễ p95/p99, tỷ lệ lỗi, độ khả dụng) với bộ giá trị mặc định tương ứng với phân loại của service đó; nếu có sai khác, xác nhận sai khác đó đi kèm một ghi chú lý do ngay trong manifest.

**Acceptance Scenarios**:

1. **Given** một service không ghi chú lý do ngoại lệ nào, **When** tôi so sánh bốn chỉ tiêu SLO đã khai báo của service đó với bộ giá trị mặc định của nền tảng theo đúng phân loại của service (BFF hướng client: độ trễ p95 ≤ 300ms, p99 ≤ 800ms; API nội bộ: độ trễ p95 ≤ 150ms, p99 ≤ 500ms; độ khả dụng 99.9% hàng tháng; tỷ lệ lỗi 5xx dưới 0.1%), **Then** bốn giá trị đó khớp đúng với bộ mặc định, không có sai lệch.
2. **Given** một service khai báo ngân sách khác với mặc định (ví dụ một luồng đọc của BFF hợp lý cần độ trễ nới lỏng hơn do phải gọi tuần tự nhiều service phía sau), **When** tôi kiểm tra manifest của service đó, **Then** manifest ghi rõ, ngay tại chỗ khai báo, lý do vì sao cần mức ngân sách khác — không phải một sai khác âm thầm không giải thích.
3. **Given** một service được phân loại sai (ví dụ một API nội bộ nhưng lại mang ngân sách của BFF hướng client mà không có lý do), **When** tôi rà soát toàn bộ manifest, **Then** trường hợp này được phát hiện là một sai lệch không có căn cứ, cần được sửa lại đúng theo mặc định hoặc bổ sung lý do.

---

### User Story 3 - Ngân sách SLO đã khai báo được đo lường liên tục từ dữ liệu vận hành thật (Priority: P2)

Là người đóng vai SRE, tôi muốn giá trị thực tế của bốn chỉ tiêu SLO mỗi service — được tính từ dữ liệu telemetry thật đang thu thập trong hệ thống — luôn có thể tra cứu được và được cập nhật liên tục, để một ngân sách được khai báo trở thành một tín hiệu vận hành sống, phản ánh đúng những gì đang thực sự xảy ra, thay vì một con số nằm im trong tài liệu không ai kiểm chứng lại.

**Why this priority**: Đây là phần hoàn thiện giá trị cốt lõi của tính năng ("ngân sách được nêu ra, không phải một kỳ vọng suông") — nhưng hệ thống vẫn có giá trị vận hành ở mức chấp nhận được ngay cả khi phần đo lường liên tục này chưa sẵn sàng, miễn là User Story 1 và 2 đã hoàn tất (ngân sách vẫn được ghi nhận rõ ràng để tham chiếu thủ công). Vì vậy ưu tiên thấp hơn hai user story trên.

**Independent Test**: Với một service bất kỳ đã có ngân sách SLO khai báo, tra cứu giá trị thực tế hiện tại của độ trễ p95/p99, tỷ lệ lỗi, và độ khả dụng của service đó trong một khoảng thời gian gần đây; xác nhận giá trị tra cứu được phản ánh đúng dữ liệu vận hành thật, đối chiếu được với giá trị đã khai báo, và tự cập nhật theo thời gian mà không cần thao tác thủ công thu thập lại số liệu.

**Acceptance Scenarios**:

1. **Given** một service đã khai báo ngân sách SLO trong manifest, **When** tôi tra cứu giá trị thực tế hiện tại của bốn chỉ tiêu đó từ dữ liệu vận hành, **Then** tôi nhận được giá trị đo được thật (không phải giá trị khai báo lặp lại), tính trên một khoảng thời gian gần đây, kèm khả năng đối chiếu trực tiếp với ngưỡng đã khai báo.
2. **Given** một endpoint của service bị làm chậm đi một cách có chủ đích (ví dụ để kiểm thử), **When** tôi tra cứu lại giá trị đo được của service đó ngay sau đó, **Then** giá trị đo được phản ánh rõ sự suy giảm này — độ trễ đo được tiến gần hoặc vượt ngưỡng đã khai báo, cho thấy ngân sách đang bị tiêu hao thật, không phải một con số tĩnh không đổi.
3. **Given** một service không có traffic nào trong khoảng thời gian đang tra cứu, **When** tôi xem giá trị đo được của service đó, **Then** hệ thống thể hiện rõ đây là "không có dữ liệu" chứ không hiển thị nhầm thành "0% lỗi" hay một giá trị khiến người xem hiểu lầm rằng service đang đạt ngân sách hoàn hảo.

---

### Edge Cases

- Một service có nhiều nhóm endpoint với đặc tính khác nhau (ví dụ endpoint đọc thông thường và một endpoint tổng hợp nặng gọi nhiều service phía sau): cần làm rõ ngân sách chỉ tiêu áp dụng ở cấp service hay có thể khai báo riêng cho từng endpoint, tránh một endpoint đặc thù kéo méo con số trung bình của cả service.
- Service phụ thuộc vào một hoặc nhiều service khác qua lời gọi ra ngoài (ví dụ BFF gọi các service nghiệp vụ phía sau): ngân sách độ trễ của lời gọi ra ngoài đó và ngân sách độ trễ của chính service không được nhầm lẫn với nhau khi rà soát.
- Một service ghi lý do ngoại lệ nhưng lý do đó không còn hợp lý theo thời gian (ví dụ điều kiện ban đầu đã thay đổi): cần có khả năng phát hiện các ngoại lệ đã khai báo từ lâu để rà soát lại định kỳ, tránh ngoại lệ trở thành vĩnh viễn một cách mặc định.
- Dữ liệu telemetry bị gián đoạn tạm thời (ví dụ do sự cố hạ tầng thu thập log/metric): giá trị đo được liên tục cần phân biệt rõ giữa "service đang vi phạm ngân sách thật" và "không có dữ liệu để đánh giá", tránh cảnh báo sai.
- Một service vượt ngân sách trong một khoảng thời gian ngắn rồi tự phục hồi (ví dụ một đợt tải cao nhất thời): cần cách quan sát xu hướng theo thời gian, không chỉ một lát cắt tức thời, để phân biệt vi phạm thoáng qua với vi phạm kéo dài đáng báo động.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Manifest của mỗi service trong hệ thống (toàn bộ service nghiệp vụ, gateway, và BFF) PHẢI khai báo đủ bốn chỉ tiêu: độ trễ p95, độ trễ p99, ngưỡng tỷ lệ lỗi, và mục tiêu độ khả dụng — mỗi chỉ tiêu là một giá trị số cụ thể, không được bỏ trống hay để giá trị giữ chỗ.
- **FR-002**: Giá trị của bốn chỉ tiêu SLO khai báo PHẢI khớp với bộ giá trị mặc định của nền tảng tương ứng với phân loại của service đó (service hướng client kiểu BFF, hoặc service API nội bộ), trừ khi service ghi rõ một lý do ngoại lệ.
- **FR-003**: Khi một service khai báo ngân sách khác với mặc định nền tảng, lý do của ngoại lệ đó PHẢI được ghi ngay tại vị trí khai báo trong manifest, ở dạng bất kỳ ai đọc manifest cũng thấy được mà không cần tra cứu tài liệu khác.
- **FR-004**: Với mỗi service đã khai báo ngân sách SLO, hệ thống PHẢI cho phép tra cứu giá trị thực tế hiện tại của cả bốn chỉ tiêu, được tính liên tục từ dữ liệu vận hành thật (telemetry) đang thu thập trong hệ thống, chứ không phải một bản ghi tĩnh nhập tay một lần.
- **FR-005**: Giá trị đo được liên tục PHẢI cho phép đối chiếu trực tiếp với ngưỡng đã khai báo cho cùng service và cùng chỉ tiêu, để xác định ngay được service có đang nằm trong ngân sách hay không.
- **FR-006**: Khi một service không có đủ dữ liệu vận hành trong khoảng thời gian đang xét (ví dụ không có traffic), hệ thống PHẢI thể hiện rõ tình trạng "không có dữ liệu", KHÔNG được hiển thị một giá trị (như 0% lỗi) có thể khiến người xem hiểu lầm rằng service đang đạt ngân sách.
- **FR-007**: Khi hiệu năng thực tế của một service thay đổi (ví dụ một endpoint trở nên chậm hơn), giá trị đo được liên tục PHẢI phản ánh sự thay đổi đó trong một khoảng thời gian hợp lý, đủ để phát hiện ngân sách đang bị tiêu hao mà không cần chờ đến chu kỳ rà soát thủ công.
- **FR-008**: Việc bổ sung khai báo và đo lường SLO KHÔNG được thay đổi hành vi phản hồi hiện có của bất kỳ endpoint nào — đây là một lớp quan sát và cam kết thêm vào, không phải một thay đổi chức năng nghiệp vụ.

### Key Entities *(include if feature involves data)*

- **Khai báo ngân sách SLO của service**: gắn với một service cụ thể; gồm bốn chỉ tiêu (độ trễ p95, độ trễ p99, ngưỡng tỷ lệ lỗi, mục tiêu độ khả dụng) và, nếu có sai khác so với mặc định, một lý do ngoại lệ đi kèm.
- **Bộ giá trị mặc định theo phân loại nền tảng**: hai bộ giá trị chuẩn — một cho service hướng client kiểu BFF, một cho service API nội bộ — dùng làm cơ sở so sánh cho mọi khai báo ngân sách SLO trừ khi có ngoại lệ được ghi rõ.
- **Giá trị đo được liên tục**: với mỗi service và mỗi chỉ tiêu, một giá trị được tính từ dữ liệu vận hành thật trong một khoảng thời gian gần đây, luôn đối chiếu được với ngưỡng đã khai báo tương ứng, và tự cập nhật theo thời gian.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% service đang chạy trong hệ thống có đủ bốn chỉ tiêu SLO (độ trễ p95, độ trễ p99, tỷ lệ lỗi, độ khả dụng) được khai báo bằng giá trị cụ thể trong manifest — xác minh được bằng cách kiểm tra trực tiếp từng manifest, không có ô trống hay giá trị giữ chỗ.
- **SC-002**: 100% service không có lý do ngoại lệ ghi trong manifest mang giá trị SLO khớp đúng bộ mặc định của nền tảng theo phân loại của mình; 100% service có sai khác so với mặc định đều có lý do ngoại lệ đọc được ngay trong manifest.
- **SC-003**: Với bất kỳ service nào đã khai báo SLO, người vận hành có thể tra cứu được giá trị đo thực tế hiện tại của cả bốn chỉ tiêu trong vòng vài giây, thông qua một điểm tra cứu duy nhất, không cần tự truy vấn hay tổng hợp dữ liệu thô.
- **SC-004**: Khi hiệu năng thực tế của một service vượt ngưỡng đã khai báo, sự vượt ngưỡng đó thể hiện rõ trên kết quả tra cứu trong cùng ngày phát sinh, không cần thao tác thu thập số liệu thủ công.
- **SC-005**: Không có trường hợp nào một service không có traffic trong khoảng thời gian tra cứu lại hiển thị nhầm thành "0% lỗi" hoặc bất kỳ giá trị nào ngụ ý service đang đạt ngân sách hoàn hảo — tình trạng "không có dữ liệu" luôn được phân biệt rõ với "không có lỗi".

## Assumptions

- Hạ tầng thu thập telemetry (vết truy vấn, chỉ số đo, log) của toàn hệ thống đã tồn tại sẵn từ một hạng mục trước đó (Nguyên tắc VII của hiến chương dự án); phạm vi của tính năng này là khai báo ngân sách và xây dựng khả năng đo lường/tra cứu liên tục dựa trên dữ liệu đã có, không phải xây mới toàn bộ hệ thống thu thập telemetry.
- "Service manifest" trong đặc tả này là tài liệu cấu hình mô tả của từng service (đã dùng để ghi nhận quyền sở hữu dữ liệu, endpoint, và các đặc tính khác) — khác với manifest triển khai Kubernetes vốn thuộc phạm vi của một hạng mục riêng.
- Bộ giá trị mặc định theo phân loại nền tảng (BFF hướng client và API nội bộ) là các con số đã được thống nhất ở cấp hiến chương dự án; đặc tả này không định nghĩa lại các con số đó, chỉ yêu cầu mọi khai báo phải đối chiếu và tuân theo, trừ ngoại lệ có lý do.
- "Đo lường liên tục" được hiểu là giá trị tra cứu phản ánh một cửa sổ thời gian gần đây được tự động tính lại (ví dụ cửa sổ trượt theo ngày), không phải một lần chụp số liệu thủ công duy nhất; cách hiện thực cụ thể (công cụ, tần suất làm mới chính xác) là chi tiết triển khai nằm ngoài phạm vi đặc tả này.
- Điểm tra cứu giá trị đo được liên tục (SC-003) có thể là bất kỳ hình thức nào miễn thỏa mãn các yêu cầu chức năng đã nêu (ví dụ một dashboard vận hành); hình thức cụ thể là chi tiết triển khai, không thuộc phạm vi đặc tả này.
