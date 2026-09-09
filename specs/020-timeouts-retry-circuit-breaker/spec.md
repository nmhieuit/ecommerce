# Feature Specification: Timeout, retry và circuit breaker cho mọi cuộc gọi ra ngoài (outbound call)

**Feature Branch**: `code/Timeouts-retry-and-circuit-breaker-on-every-outbound-call`

**Created**: 2026-09-09

**Status**: Draft

**Input**: User description: "Jira SCRUM-30 [RESILIENCE-4] Timeouts, retry, and circuit-breaker on every outbound call — As the Developer, I want every outbound HTTP or messaging call wrapped with an explicit timeout plus retry/circuit-breaker policy via Microsoft.Extensions.Resilience so that unbounded waits cannot exist anywhere in the system (Principle VIII). Acceptance Criteria: (1) Given any outbound call (BFF→service, service→service, service→broker), when I inspect its configuration, then it has an explicit timeout. (2) Given a downstream dependency is slow or down, when the circuit breaker trips, then subsequent calls fail fast instead of queuing up. (3) Given a transient failure occurs, when the retry policy engages, then it retries with backoff rather than hammering the dependency. Test Scenarios: 1. Grep for outbound HTTP client registrations — confirm every one has a resilience policy attached, none use defaults/unbounded waits. 2. Kill the products service and hammer the BFF — confirm the circuit breaker opens and requests fail fast after the threshold. 3. Introduce artificial latency on one dependency — confirm the caller's timeout fires rather than hanging indefinitely."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mọi cuộc gọi ra ngoài đều có timeout tường minh (Priority: P1)

Là một Developer, tôi muốn mọi cuộc gọi ra ngoài (BFF→service, service→service, service→broker) đều được cấu hình một timeout tường minh, để không có cuộc gọi nào trong hệ thống có thể chờ vô thời hạn khi dependency phía dưới không phản hồi.

**Why this priority**: Đây là tiền đề bắt buộc của toàn bộ tính năng — nếu một cuộc gọi không có timeout tường minh thì circuit breaker (User Story 2) và retry (User Story 3) không có cơ sở để kích hoạt, vì bản thân cuộc gọi có thể treo vô thời hạn trước khi các chính sách đó kịp can thiệp.

**Independent Test**: Rà soát toàn bộ nơi khởi tạo/đăng ký client gọi ra ngoài (HTTP client, messaging client) trong mã nguồn của BFF và từng service — xác nhận mỗi nơi đều gắn một timeout tường minh, không còn nơi nào dùng cấu hình mặc định hoặc chờ vô thời hạn.

**Acceptance Scenarios**:

1. **Given** một cuộc gọi ra ngoài bất kỳ (BFF gọi service, service gọi service khác, hoặc service gửi message tới broker), **When** tôi kiểm tra cấu hình của cuộc gọi đó, **Then** cuộc gọi có một timeout tường minh được khai báo, không dựa vào giá trị mặc định của hạ tầng/thư viện.
2. **Given** toàn bộ các điểm đăng ký client gọi ra ngoài trong hệ thống đã được rà soát, **When** tôi tổng hợp kết quả, **Then** không còn điểm nào thiếu timeout hoặc dùng cấu hình chờ vô thời hạn.
3. **Given** một dependency phía dưới có độ trễ nhân tạo vượt quá timeout đã cấu hình, **When** caller thực hiện cuộc gọi, **Then** cuộc gọi kết thúc đúng lúc timeout hết hạn thay vì treo cho tới khi dependency phản hồi.

---

### User Story 2 - Circuit breaker chặn cuộc gọi dồn ứ khi dependency chậm hoặc gián đoạn (Priority: P1)

Là một Developer, tôi muốn mỗi cuộc gọi ra ngoài được bảo vệ bởi một circuit breaker tự động ngắt mạch khi dependency phía dưới liên tục chậm hoặc gián đoạn, để các cuộc gọi tiếp theo thất bại nhanh (fail fast) thay vì dồn ứ chờ đợi và kéo theo sự cố dây chuyền sang caller phía trên.

**Why this priority**: Cùng mức ưu tiên với User Story 1 vì đây là giá trị vận hành cốt lõi của tính năng — nếu không có circuit breaker, một dependency gặp sự cố sẽ khiến mọi caller tiếp tục chờ hết timeout cho từng request, làm cạn kiệt tài nguyên (luồng xử lý, kết nối) của caller và lan sự cố ngược lên các tầng phía trên (ví dụ BFF, rồi tới người dùng cuối).

**Independent Test**: Giả lập một service phụ thuộc ngừng phản hồi hoặc bị tắt, dồn dập gửi request từ caller (ví dụ BFF) tới service đó, và xác nhận sau ngưỡng lỗi đã cấu hình, circuit breaker mở ra khiến các request tiếp theo thất bại ngay lập tức mà không chờ hết timeout.

**Acceptance Scenarios**:

1. **Given** một dependency phía dưới đang chậm hoặc ngừng hoạt động, **When** số lượng lỗi/timeout liên tiếp từ dependency đó vượt ngưỡng đã cấu hình, **Then** circuit breaker chuyển sang trạng thái mở (open).
2. **Given** circuit breaker của một dependency đang ở trạng thái mở, **When** caller thực hiện thêm cuộc gọi tới dependency đó, **Then** cuộc gọi thất bại ngay lập tức (fail fast) mà không chờ hết timeout và không gửi request thật tới dependency.
3. **Given** circuit breaker đang ở trạng thái mở trong một khoảng thời gian đã cấu hình, **When** khoảng thời gian đó kết thúc, **Then** circuit breaker chuyển sang trạng thái thử nghiệm (half-open), cho phép một số ít request thăm dò để xác định dependency đã phục hồi hay chưa, trước khi đóng mạch hoàn toàn trở lại.

---

### User Story 3 - Retry có backoff cho lỗi tạm thời (Priority: P2)

Là một Developer, tôi muốn mỗi cuộc gọi ra ngoài tự động thử lại (retry) theo chính sách backoff khi gặp lỗi tạm thời, để hệ thống tự phục hồi khỏi các sự cố thoáng qua mà không cần can thiệp thủ công, đồng thời không dội thêm tải lên một dependency đang gặp sự cố.

**Why this priority**: Đây là năng lực tự phục hồi bổ sung, phụ thuộc vào timeout (User Story 1) và phối hợp với circuit breaker (User Story 2) để tránh retry biến thành một cơn bão request dội vào dependency đang gặp sự cố. Hệ thống vẫn vận hành được ở mức chấp nhận được (fail fast qua circuit breaker) ngay cả khi hành vi retry chưa hoàn thiện, nên ưu tiên thấp hơn hai user story trên.

**Independent Test**: Giả lập một lỗi tạm thời, thoáng qua ở một dependency (ví dụ dependency thất bại 1-2 lần rồi phục hồi), thực hiện cuộc gọi từ caller, và xác nhận caller tự động thử lại với khoảng cách giữa các lần thử tăng dần (backoff) và cuối cùng nhận được kết quả thành công mà không cần can thiệp thủ công.

**Acceptance Scenarios**:

1. **Given** một cuộc gọi ra ngoài gặp lỗi được xác định là tạm thời (transient), **When** chính sách retry được kích hoạt, **Then** caller tự động thử lại cuộc gọi thay vì trả lỗi ngay cho tầng gọi nó.
2. **Given** nhiều lần thử lại liên tiếp đối với cùng một dependency, **When** tôi quan sát khoảng thời gian giữa các lần thử, **Then** khoảng thời gian đó tăng dần theo chiến lược backoff, không thử lại ngay lập tức và liên tục (không dội tải/hammering vào dependency).
3. **Given** một lỗi không phải lỗi tạm thời (ví dụ lỗi nghiệp vụ do dữ liệu đầu vào không hợp lệ), **When** cuộc gọi thất bại vì lý do đó, **Then** hệ thống không thực hiện retry mà trả lỗi ngay cho tầng gọi nó, tránh thử lại vô ích với một lỗi chắc chắn sẽ lặp lại.

---

### Edge Cases

- Cuộc gọi ghi dữ liệu không idempotent (ví dụ tạo đơn hàng, gửi message tạo tài nguyên mới): retry không được gây trùng lặp side effect (ví dụ tạo hai đơn hàng cho một yêu cầu) — cần đảm bảo an toàn khi thử lại đối với các thao tác loại này.
- Nhiều caller cùng lúc retry cùng một dependency đang gặp sự cố: cần có yếu tố ngẫu nhiên (jitter) trong backoff để tránh các lần retry của nhiều caller đồng loạt dội vào dependency cùng một thời điểm, làm sự cố nặng thêm.
- Cuộc gọi lồng nhau qua nhiều tầng (BFF gọi service A, service A gọi service B): tổng thời gian chờ tối đa (timeout cộng dồn qua các tầng) không được vượt quá ngân sách thời gian phản hồi tổng thể của request gốc.
- Circuit breaker ở trạng thái half-open nhưng request thăm dò cũng thất bại: circuit breaker phải quay lại trạng thái mở thay vì đóng mạch nhầm, và không được thử nghiệm bằng toàn bộ lưu lượng đang chờ.
- Cuộc gọi gửi message tới broker (bất đồng bộ) khác với cuộc gọi HTTP đồng bộ (yêu cầu/phản hồi): timeout, retry và circuit breaker vẫn phải áp dụng cho hành động gửi message, dù ngữ nghĩa "thất bại" và "chờ" khác với một request HTTP.
- Một điểm gọi ra ngoài mới được thêm vào sau này (dependency mới, endpoint mới) không được gắn chính sách resilience: cần có cách phát hiện (rà soát/kiểm tra tự động) để không bỏ sót cuộc gọi thiếu cấu hình khi hệ thống mở rộng.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Mọi cuộc gọi ra ngoài (BFF gọi service, service gọi service khác, service gửi message tới broker) PHẢI có một timeout tường minh được khai báo, không được dựa vào giá trị chờ mặc định/vô thời hạn của hạ tầng hay thư viện.
- **FR-002**: Mọi cuộc gọi ra ngoài PHẢI được bảo vệ bởi một circuit breaker, tự động chuyển sang trạng thái mở sau khi số lỗi/timeout liên tiếp (hoặc tỷ lệ lỗi) tới dependency đó vượt ngưỡng đã cấu hình.
- **FR-003**: Khi circuit breaker của một dependency đang ở trạng thái mở, hệ thống PHẢI làm các cuộc gọi tiếp theo tới dependency đó thất bại ngay lập tức (fail fast), không gửi request thật và không chờ hết timeout.
- **FR-004**: Circuit breaker PHẢI tự động chuyển sang trạng thái thử nghiệm (half-open) sau một khoảng thời gian đã cấu hình, cho phép một số ít request thăm dò trước khi quyết định đóng mạch trở lại hay tiếp tục mở.
- **FR-005**: Mọi cuộc gọi ra ngoài PHẢI được bảo vệ bởi một chính sách retry, tự động thử lại khi gặp lỗi được xác định là tạm thời, sử dụng khoảng cách tăng dần giữa các lần thử (backoff) thay vì thử lại ngay lập tức.
- **FR-006**: Chính sách retry KHÔNG được thử lại đối với lỗi không tạm thời (ví dụ lỗi nghiệp vụ/dữ liệu đầu vào không hợp lệ), và KHÔNG được gây trùng lặp side effect đối với các thao tác không idempotent.
- **FR-007**: Hệ thống PHẢI cung cấp cách rà soát được (ví dụ kiểm tra tự động hoặc thủ công có thể lặp lại) để xác nhận mọi điểm gọi ra ngoài trong toàn hệ thống đều đã gắn đầy đủ timeout, retry và circuit breaker — không bỏ sót điểm gọi nào, kể cả khi có điểm gọi mới được thêm vào sau này.
- **FR-008**: Sự kiện liên quan tới resilience (timeout xảy ra, circuit breaker mở/đóng, một lần retry được thực hiện) PHẢI được ghi nhận lại (log/telemetry) để đội vận hành phân biệt được giữa "dependency thực sự gặp sự cố" và "lỗi tạm thời tự phục hồi".
- **FR-009**: Việc bổ sung timeout, retry và circuit breaker KHÔNG được thay đổi hợp đồng phản hồi (response contract) hiện có của các cuộc gọi ra ngoài trong điều kiện vận hành bình thường (dependency khỏe mạnh) — chỉ bổ sung hành vi bảo vệ khi có sự cố.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% điểm gọi ra ngoài trong hệ thống (BFF→service, service→service, service→broker) có timeout, retry và circuit breaker được cấu hình tường minh — xác minh được bằng cách rà soát toàn bộ điểm đăng ký client gọi ra ngoài, không còn điểm nào dùng cấu hình mặc định/chờ vô thời hạn.
- **SC-002**: Khi một dependency phía dưới ngừng phản hồi, thời gian caller chờ trước khi nhận lỗi không vượt quá timeout đã cấu hình cho cuộc gọi đó, trong 100% số lần gọi trong quá trình kiểm thử.
- **SC-003**: Sau khi circuit breaker mở do một dependency gặp sự cố, các request tiếp theo tới dependency đó nhận được phản hồi lỗi trong thời gian rất ngắn (không đáng kể so với timeout gốc), thay vì chờ hết timeout — đo được qua kiểm thử giả lập sự cố.
- **SC-004**: Khi một lỗi tạm thời xảy ra rồi tự phục hồi, caller nhận được kết quả thành công thông qua retry tự động mà không cần can thiệp thủ công, trong khi số lần gọi thực tế tới dependency trong một cửa sổ thời gian ngắn không tăng đột biến (nhờ backoff).
- **SC-005**: Trong một đợt kiểm thử giả lập sự cố (một service phụ thuộc bị dừng), các caller phía trên (ví dụ BFF) không bị cạn kiệt tài nguyên (luồng xử lý, kết nối) hay treo theo dây chuyền — chúng tiếp tục phục vụ được các request không liên quan tới dependency gặp sự cố.

## Assumptions

- Phạm vi của tính năng bao trùm mọi cuộc gọi ra ngoài do BFF và các service nghiệp vụ (baskets, orders, parties, products, identity) khởi tạo, bao gồm cả gọi HTTP đồng bộ lẫn gửi message bất đồng bộ tới broker; không bao gồm các request đi vào hệ thống từ bên ngoài (đó là phạm vi của các tính năng khác như gateway/BFF timeout cho client).
- Thư viện/công cụ hiện thực cụ thể cho timeout, retry và circuit breaker là chi tiết triển khai (do định hướng kiến trúc/constitution của hệ thống quy định), không thuộc phạm vi quyết định của đặc tả này; đặc tả chỉ quy định hành vi mà các cuộc gọi ra ngoài phải đảm bảo.
- Giá trị cụ thể cho từng tham số (thời lượng timeout, ngưỡng lỗi mở mạch, số lần retry, hệ số backoff) sẽ được xác định ở giai đoạn lập kế hoạch/triển khai cho từng cuộc gọi, dựa trên đặc tính thực tế của dependency tương ứng, miễn là thỏa mãn nguyên tắc "không chờ vô thời hạn, fail fast khi mở mạch, backoff khi retry" nêu tại các yêu cầu chức năng ở trên.
- Danh sách lỗi được coi là "tạm thời" (transient) — ví dụ timeout, lỗi kết nối, lỗi 5xx — so với lỗi không tạm thời (ví dụ lỗi 4xx do dữ liệu đầu vào) sẽ được xác định cụ thể ở giai đoạn lập kế hoạch, dựa trên phân loại lỗi chuẩn cho từng loại giao thức (HTTP, messaging).
