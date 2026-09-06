# Feature Specification: Phát telemetry OTel (traces/metrics/logs) qua ServiceDefaults tới Elastic

**Feature Branch**: `017-otel-servicedefaults-elastic`

**Created**: 2026-09-06

**Status**: Draft

**Input**: User description: "https://nmhieuit.atlassian.net/browse/SCRUM-25 — [SECURE-3] OTel traces/metrics/logs via ServiceDefaults to Elastic. As the SRE-hat-wearer, I want every service emitting OpenTelemetry traces, metrics, and structured logs through a shared ServiceDefaults component so that observability is consistent, not hand-rolled per service (Principle VII). Acceptance Criteria: (1) Given a service starts, when it initializes, then it wires OTel via the shared ServiceDefaults component, not a bespoke per-service setup. (2) Given a request flows through the slice, when I query Elastic, then I can see traces, metrics, and structured logs for every hop. (3) Given a log statement is written, when I inspect it, then it is structured (not an interpolated string) and carries service, tenant, and correlation identifiers. Test Scenarios: (1) Place one order and find its full trace across all 4 services in Elastic/Kibana. (2) Grep the codebase for string-interpolated log calls — expect none. (3) Remove ServiceDefaults from one service temporarily — confirm it loses telemetry, proving the shared component is actually load-bearing. (Yêu cầu bổ sung: dùng tiếng Việt có dấu cho toàn bộ prompt/đặc tả trong thread này.)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Truy vết đầy đủ một luồng xử lý xuyên suốt mọi service trong Elastic/Kibana (Priority: P1)

Là người đóng vai SRE, tôi muốn khi một luồng xử lý (ví dụ đặt một đơn hàng) đi qua toàn bộ các service tham gia, tôi có thể tra cứu trong Elastic/Kibana và thấy đầy đủ traces, metrics, và structured logs cho từng hop trên đường đi, để tôi có thể chẩn đoán sự cố mà không cần truy cập trực tiếp vào từng service.

**Why this priority**: Đây là giá trị cốt lõi mà tính năng hướng tới — khả năng quan sát end-to-end trong Elastic (Principle VII). Không có khả năng này thì việc có structured log hay có ServiceDefaults dùng chung cũng không mang lại lợi ích thực tế cho việc vận hành.

**Independent Test**: Có thể kiểm thử độc lập bằng cách đặt một đơn hàng, sau đó tra cứu trong Elastic/Kibana và xác nhận có thể tìm thấy trace đầy đủ của đơn hàng đó xuyên suốt tất cả các service tham gia xử lý (bao gồm cả các hop bất đồng bộ), kèm theo metrics và structured logs tương ứng.

**Acceptance Scenarios**:

1. **Given** một đơn hàng được đặt và đi qua toàn bộ các service tham gia xử lý, **When** SRE tra cứu Elastic/Kibana theo correlation ID của đơn hàng đó, **Then** SRE tìm thấy trace đầy đủ, liên tục, bao phủ tất cả các hop (bao gồm cả hop xử lý bất đồng bộ) mà không bị đứt đoạn ở bất kỳ service nào.
2. **Given** cùng luồng xử lý đặt đơn hàng đó, **When** SRE tra cứu metrics trong Elastic, **Then** SRE thấy các chỉ số (độ trễ, tỷ lệ lỗi, throughput...) được phát ra cho từng service tham gia.
3. **Given** cùng luồng xử lý đó, **When** SRE tra cứu structured logs trong Elastic, **Then** SRE thấy log entry tương ứng với từng hop, đủ chi tiết để hiểu diễn biến xử lý mà không cần xem log cục bộ trên từng service.

---

### User Story 2 - Log có cấu trúc, mang định danh service/tenant/correlation (Priority: P2)

Là người đóng vai SRE, tôi muốn mọi log statement trong hệ thống được ghi dưới dạng có cấu trúc (không phải chuỗi nội suy) và mang theo định danh service, tenant, và correlation ID, để tôi có thể lọc, tra cứu, và liên kết log một cách chính xác và tự động thay vì phải đọc và suy đoán từ chuỗi văn bản.

**Why this priority**: Đây là điều kiện để log thực sự hữu ích cho quan sát tự động (tìm kiếm, lọc, cảnh báo) trong Elastic — nếu không có định dạng có cấu trúc và các định danh này, khối lượng log lớn từ nhiều tenant/service sẽ không thể tra cứu hiệu quả, dù trace và metric đã sẵn có theo User Story 1.

**Independent Test**: Có thể kiểm thử độc lập bằng cách rà soát (ví dụ grep) toàn bộ mã nguồn để xác nhận không còn lời gọi log nào dùng chuỗi nội suy, và bằng cách kiểm tra một log entry bất kỳ trong Elastic để xác nhận nó ở dạng có cấu trúc và mang đủ ba định danh service, tenant, correlation.

**Acceptance Scenarios**:

1. **Given** một lời gọi log bất kỳ trong mã nguồn của bất kỳ service nào, **When** mã nguồn được rà soát, **Then** lời gọi đó ghi log dưới dạng có cấu trúc (structured/templated), không phải chuỗi được nội suy trực tiếp (string interpolation).
2. **Given** một log entry bất kỳ được ghi ra trong quá trình xử lý một request hoặc một message, **When** log entry đó được kiểm tra trong Elastic, **Then** nó chứa định danh service, định danh tenant, và correlation ID tương ứng với luồng xử lý mà nó thuộc về.
3. **Given** dữ liệu nhạy cảm (PII) có thể xuất hiện trong ngữ cảnh xử lý, **When** log được ghi ra, **Then** log entry đó không chứa PII dưới bất kỳ hình thức nào.

---

### User Story 3 - ServiceDefaults là thành phần dùng chung, chịu tải thực sự cho toàn bộ observability (Priority: P3)

Là người đóng vai SRE, tôi muốn mọi service khởi tạo OpenTelemetry (traces, metrics, structured logs) thông qua một thành phần ServiceDefaults dùng chung duy nhất — chứ không phải mỗi service tự cấu hình riêng lẻ — để việc quan sát hệ thống luôn nhất quán, và để tôi có thể xác nhận rằng thành phần dùng chung này thực sự cần thiết (load-bearing) chứ không phải một lớp cấu hình có thể bỏ qua mà không ảnh hưởng gì.

**Why this priority**: Đây là điều kiện đảm bảo tính bền vững lâu dài của hai user story trên — nếu từng service tự cấu hình OTel theo cách riêng, cấu hình sẽ dần trôi dạt (drift) và không còn nhất quán theo thời gian, dù ở thời điểm hiện tại trace/metric/log vẫn hoạt động. Vì đây là một thuộc tính về cách hiện thực hoá hơn là một khả năng quan sát mới, nó được xếp ưu tiên thấp nhất trong ba câu chuyện.

**Independent Test**: Có thể kiểm thử độc lập bằng cách rà soát cấu hình khởi tạo của từng service để xác nhận tất cả đều dùng chung một thành phần ServiceDefaults, và bằng cách tạm thời gỡ bỏ ServiceDefaults khỏi một service rồi xác nhận service đó mất khả năng phát traces/metrics/structured logs — chứng minh thành phần dùng chung này thực sự chịu tải, không phải trang trí.

**Acceptance Scenarios**:

1. **Given** một service bất kỳ khởi động, **When** quá trình khởi tạo của service đó được kiểm tra, **Then** service đó wiring OpenTelemetry thông qua thành phần ServiceDefaults dùng chung, không có cấu hình OTel riêng lẻ, tự viết cho service đó.
2. **Given** ServiceDefaults bị gỡ bỏ tạm thời khỏi một service cụ thể (phục vụ kiểm thử), **When** service đó xử lý request, **Then** service đó không còn phát traces, metrics, hay structured logs tới Elastic — xác nhận ServiceDefaults là thành phần chịu tải thực sự cho toàn bộ observability của service đó.
3. **Given** ServiceDefaults được khôi phục lại cho service đó, **When** service khởi động lại, **Then** service đó phát telemetry trở lại bình thường, nhất quán với các service khác.

---

### Edge Cases

- Điều gì xảy ra khi một service khởi động nhưng ServiceDefaults không thể kết nối tới điểm thu nhận (collector/endpoint) của Elastic (ví dụ do lỗi mạng tạm thời)? Service PHẢI vẫn khởi động và phục vụ request bình thường (không được sập chỉ vì lỗi telemetry), nhưng phải có cơ chế thử lại hoặc đệm để không mất vĩnh viễn dữ liệu quan sát của giai đoạn gián đoạn đó nếu có thể.
- Điều gì xảy ra với các log được ghi ra trước khi ServiceDefaults khởi tạo xong (log ở giai đoạn bootstrap của tiến trình)? Đây PHẢI được coi là ngoại lệ đã biết và được ghi nhận rõ trong phạm vi kỹ thuật, không được lẫn với các log nghiệp vụ bình thường.
- Điều gì xảy ra khi một service cũ hoặc mới thêm vào sau này bỏ sót việc tích hợp ServiceDefaults? Đây PHẢI được xem là một khoảng trống về khả năng quan sát cần khắc phục trong phạm vi tính năng này, không phải một ngoại lệ được chấp nhận.
- Điều gì xảy ra khi khối lượng traces/metrics quá lớn (ví dụ tải cao) khiến chi phí lưu trữ hoặc băng thông trở thành vấn đề? Việc lấy mẫu (sampling) cho traces là một quyết định kỹ thuật thuộc phạm vi `plan.md`, miễn là nó không làm mất khả năng truy vết đầy đủ ít nhất một luồng xử lý mẫu theo Success Criteria bên dưới.
- Điều gì xảy ra khi một log statement cần ghi dữ liệu có khả năng chứa PII (ví dụ thông tin khách hàng)? Log entry đó PHẢI được che/lọc (redact) trước khi ghi, theo Nguyên tắc VI (Secure by Default) của hiến pháp nền tảng — không được đánh đổi khả năng quan sát để lộ PII.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Mọi service trong nền tảng PHẢI khởi tạo OpenTelemetry (traces, metrics, structured logs) thông qua một thành phần ServiceDefaults dùng chung duy nhất; không service nào được tự cấu hình OTel riêng lẻ, hand-rolled cho chính nó.
- **FR-002**: Thành phần ServiceDefaults PHẢI xuất traces, metrics, và structured logs tới Elastic stack cho mọi service sử dụng nó.
- **FR-003**: Với một luồng xử lý xuyên suốt nhiều service (ví dụ đặt một đơn hàng), SRE PHẢI có thể tra cứu trong Elastic/Kibana và thấy trace, metrics, và structured log tương ứng cho từng hop tham gia xử lý luồng đó, kể cả hop xử lý bất đồng bộ.
- **FR-004**: Mọi log entry được ghi bởi bất kỳ service nào trong nền tảng PHẢI ở dạng có cấu trúc (structured/templated logging), không được là chuỗi bị nội suy trực tiếp (string interpolation).
- **FR-005**: Mọi log entry được ghi ra trong quá trình xử lý một request hoặc một message PHẢI mang theo định danh service, định danh tenant, và correlation ID tương ứng với luồng xử lý mà nó thuộc về.
- **FR-006**: Hệ thống PHẢI cho phép rà soát toàn bộ mã nguồn (ví dụ bằng công cụ tìm kiếm văn bản) để xác nhận không còn lời gọi log nào sử dụng chuỗi nội suy trong bất kỳ service nào.
- **FR-007**: Nếu ServiceDefaults bị gỡ bỏ hoặc không được tích hợp vào một service, service đó PHẢI mất khả năng phát traces, metrics, và structured logs tới Elastic một cách có thể quan sát rõ ràng — chứng minh ServiceDefaults là thành phần chịu tải thực sự, không phải lớp cấu hình có thể bỏ qua.
- **FR-008**: Traces, metrics, và structured logs PHẢI không chứa PII dưới bất kỳ hình thức nào, theo Nguyên tắc VI (Secure by Default) của hiến pháp nền tảng.
- **FR-009**: Metrics phát ra bởi ServiceDefaults PHẢI đủ để đo các SLO đã khai báo theo Nguyên tắc VIII (Performance and Resilience Budgets) — độ trễ (p95/p99), tỷ lệ lỗi, khả dụng — cho từng service.
- **FR-010**: Correlation ID được sinh và lan truyền theo cơ chế đã thiết lập ở tính năng lan truyền Correlation ID (016) PHẢI được ServiceDefaults đưa vào traces, metrics (dưới dạng thuộc tính/tag khi phù hợp), và mọi structured log entry một cách nhất quán.

### Key Entities

- **Trace / Span**: Đại diện cho một đơn vị công việc trong một luồng xử lý; nhiều span của cùng một luồng gắn kết với nhau qua correlation ID, tạo thành một trace liên tục xuyên suốt các service tham gia.
- **Metric**: Số liệu định lượng (độ trễ, tỷ lệ lỗi, throughput, và các chỉ số nghiệp vụ/hệ thống khác) được từng service phát ra thông qua ServiceDefaults.
- **Structured Log Entry**: Bản ghi log dạng có cấu trúc (không phải chuỗi nội suy), mang định danh service, định danh tenant, và correlation ID của luồng xử lý mà nó thuộc về.
- **ServiceDefaults**: Thành phần dùng chung, được mọi service tích hợp, chịu trách nhiệm khởi tạo và cấu hình OpenTelemetry (traces, metrics, structured logs) một cách nhất quán và xuất dữ liệu đó tới Elastic.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Với một đơn hàng bất kỳ được đặt và đi qua toàn bộ các service tham gia, SRE tìm thấy trace đầy đủ của đơn hàng đó trong Elastic/Kibana trong vòng dưới 5 phút tra cứu, không bị đứt đoạn ở bất kỳ hop nào.
- **SC-002**: Khi rà soát toàn bộ mã nguồn của mọi service, 0 lời gọi log dạng chuỗi nội suy được phát hiện.
- **SC-003**: 100% structured log entry được lấy mẫu kiểm tra trong Elastic đều mang đủ ba định danh: service, tenant, và correlation ID.
- **SC-004**: Khi ServiceDefaults bị gỡ tạm thời khỏi một service (phục vụ kiểm thử), 100% telemetry (traces, metrics, structured logs) của service đó biến mất khỏi Elastic ngay sau đó — xác nhận thành phần này là load-bearing.
- **SC-005**: Mọi service đang chạy đều expose đủ metrics để đo các SLO đã khai báo (độ trễ p95/p99, tỷ lệ lỗi) theo Nguyên tắc VIII của hiến pháp nền tảng, xác nhận được qua Elastic mà không cần công cụ đo bổ sung nào khác.
- **SC-006**: Không phát hiện PII trong bất kỳ mẫu trace hoặc log nào được rà soát trong Elastic.

## Assumptions

- Phạm vi tính năng bao gồm các service nghiệp vụ đã tham gia các tính năng nền tảng trước đó (ví dụ parties, products, baskets, orders) cùng gateway/bff nếu các thành phần này cũng tích hợp ServiceDefaults; danh sách chính xác "mọi hop" của một luồng xử lý cụ thể là chi tiết kỹ thuật thuộc phạm vi `plan.md`.
- Correlation ID sử dụng trong tính năng này là correlation ID đã được sinh và lan truyền theo cơ chế thiết lập ở tính năng 016 (Lan truyền Correlation ID từ Edge đến Frontend); tính năng này không định nghĩa lại cơ chế sinh/lan truyền đó mà chỉ đảm bảo nó được đưa vào traces/metrics/structured logs một cách nhất quán qua ServiceDefaults.
- Cơ chế kỹ thuật cụ thể (thư viện OpenTelemetry .NET, cấu hình exporter tới Elastic — APM Server hoặc OTLP, định dạng structured logging cụ thể, chiến lược sampling cho traces khi tải cao) là quyết định thiết kế thuộc phạm vi `plan.md`, không thuộc phạm vi đặc tả này.
- Tính năng này cụ thể hoá phần còn lại của Nguyên tắc VII (Observable by Default) trong hiến pháp nền tảng — mọi service phát traces/metrics/structured logs qua ServiceDefaults dùng chung tới Elastic — bổ sung cho phần correlation ID đã hoàn thành ở tính năng 016.
- Việc che/lọc PII khỏi log và trace tuân theo các quy tắc đã có của Nguyên tắc VI (Secure by Default); tính năng này không định nghĩa lại danh sách trường dữ liệu nào được coi là PII.
