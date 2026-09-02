# Feature Specification: Phân quyền từ chối theo mặc định trên mọi endpoint/handler

**Feature Branch**: `015-deny-by-default-authz`

**Created**: 2026-09-02

**Status**: Draft

**Input**: User description: "https://nmhieuit.atlassian.net/browse/SCRUM-24 — [SECURE-3] Deny-by-default authorization on every endpoint/handler. As DevOps, I want every endpoint and message handler to declare an explicit authorization policy so that nothing is reachable by accident (Principle VI). Acceptance Criteria: (1) Given any HTTP endpoint or message handler in the slice, when I inspect it, then it has an explicit authorization policy attached. (2) Given an endpoint is added without an authorization decision, when the build runs, then the build or review fails — there is no silent-allow default. (3) Given client-side validation exists in the SPA, when I test it, then server-side validation independently enforces the same rules (client-side is UX only). Test Scenarios: (1) Grep all controllers/handlers for missing [Authorize]-equivalent attributes — expect zero. (2) Call an endpoint with a token lacking the required policy claim — confirm 403, not 200. (3) Bypass the SPA's client-side validation (e.g., via direct API call) — confirm the server independently rejects invalid input." (Yêu cầu bổ sung: viết đặc tả bằng tiếng Việt có dấu.)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mọi endpoint và handler đều có quyết định phân quyền rõ ràng (Priority: P1)

Là một người vận hành nền tảng, tôi muốn mọi HTTP endpoint và mọi message handler đều gắn một chính sách phân quyền rõ ràng, để không có bề mặt nào có thể bị truy cập một cách tình cờ do bị bỏ sót.

**Why this priority**: Đây là giá trị cốt lõi của tính năng — nếu một endpoint hay handler tồn tại mà không có quyết định phân quyền tường minh, mọi cơ chế thực thi hay kiểm thử khác dựng trên nó đều vô nghĩa. Đây là trạng thái đích mà các user story còn lại tồn tại để bảo vệ.

**Independent Test**: Có thể kiểm thử độc lập bằng cách rà soát toàn bộ HTTP endpoint và message handler trong phạm vi, xác nhận mỗi endpoint/handler đều có một chính sách phân quyền được gắn tường minh (yêu cầu một policy/claim cụ thể, hoặc một miễn trừ được khai báo rõ ràng), sau đó gửi một yêu cầu mang token hợp lệ nhưng thiếu claim theo đúng chính sách yêu cầu và xác nhận yêu cầu đó bị từ chối.

**Acceptance Scenarios**:

1. **Given** một HTTP endpoint hoặc message handler bất kỳ trong phạm vi, **When** endpoint/handler đó được rà soát, **Then** nó có một chính sách phân quyền được gắn tường minh — không có endpoint/handler nào không có quyết định phân quyền.
2. **Given** một endpoint yêu cầu một policy/claim cụ thể, **When** một yêu cầu mang token hợp lệ nhưng thiếu claim theo đúng chính sách đó đến endpoint, **Then** yêu cầu bị từ chối với phản hồi 403 (Forbidden), không được xử lý như một yêu cầu 200 hợp lệ.
3. **Given** một endpoint hoặc handler được chủ đích miễn trừ khỏi việc yêu cầu phân quyền (ví dụ: health check), **When** endpoint/handler đó được rà soát, **Then** việc miễn trừ đó được khai báo tường minh và có thể kiểm chứng được, không phải một sự bỏ sót ngầm định.

---

### User Story 2 - Build hoặc review chặn mọi endpoint/handler thiếu quyết định phân quyền (Priority: P2)

Là một người vận hành nền tảng, tôi cần quy trình build hoặc review tự động phát hiện và chặn bất kỳ endpoint hay message handler mới nào được thêm vào mà chưa có quyết định phân quyền tường minh, để trạng thái "mọi endpoint đều có chính sách phân quyền" ở User Story 1 không bị xói mòn theo thời gian bởi sai sót của con người.

**Why this priority**: Đây là cơ chế thực thi giữ cho đảm bảo ở User Story 1 luôn đúng khi hệ thống tiếp tục phát triển — không có nó, đảm bảo ban đầu chỉ đúng tại một thời điểm và sẽ suy thoái dần. Nó phụ thuộc vào việc User Story 1 đã định nghĩa thế nào là "có quyết định phân quyền" trước đó.

**Independent Test**: Có thể kiểm thử độc lập bằng cách thêm một endpoint hoặc message handler mới không gắn kèm bất kỳ chính sách phân quyền hay khai báo miễn trừ nào, sau đó chạy build hoặc quy trình review và xác nhận nó bị chặn lại — không được phép hợp nhất (merge) vào nhánh chính.

**Acceptance Scenarios**:

1. **Given** một endpoint hoặc message handler mới được thêm vào mà không có chính sách phân quyền hay khai báo miễn trừ nào, **When** build hoặc quy trình review chạy, **Then** build hoặc review đó thất bại — không có mặc định cho phép âm thầm nào xảy ra.
2. **Given** một endpoint hoặc message handler mới được thêm vào có gắn kèm một chính sách phân quyền tường minh (hoặc một khai báo miễn trừ tường minh), **When** build hoặc quy trình review chạy, **Then** build hoặc review đó không bị chặn bởi lý do phân quyền.
3. **Given** danh sách toàn bộ controller/handler trong hệ thống, **When** danh sách đó được rà soát tự động để tìm các endpoint/handler thiếu chính sách phân quyền tương đương, **Then** kết quả rà soát không tìm thấy trường hợp nào bị thiếu.

---

### User Story 3 - Kiểm tra dữ liệu phía máy chủ hoạt động độc lập với kiểm tra phía SPA (Priority: P3)

Là một người vận hành nền tảng, tôi muốn mọi quy tắc nghiệp vụ được SPA kiểm tra ở phía client cũng được máy chủ thực thi lại một cách độc lập, để việc kiểm tra phía client chỉ đóng vai trò trải nghiệm người dùng chứ không phải là ranh giới bảo mật hay toàn vẹn dữ liệu duy nhất.

**Why this priority**: Đây là một khía cạnh quan trọng của việc "phòng thủ theo chiều sâu" nhưng là một rủi ro hẹp hơn và cục bộ hơn (một quy tắc dữ liệu bị bỏ sót ở một endpoint) so với việc toàn bộ bề mặt endpoint bị thiếu phân quyền — vì vậy nó có mức ưu tiên thấp hơn hai user story còn lại, dù vẫn thuộc phạm vi bắt buộc của tính năng.

**Independent Test**: Có thể kiểm thử độc lập bằng cách bỏ qua hoàn toàn kiểm tra phía client của SPA và gọi trực tiếp đến API với dữ liệu vi phạm một quy tắc nghiệp vụ mà SPA vốn kiểm tra, sau đó xác nhận máy chủ tự từ chối yêu cầu đó mà không cần đến sự trợ giúp của kiểm tra phía client.

**Acceptance Scenarios**:

1. **Given** một quy tắc nghiệp vụ được kiểm tra ở phía client trong SPA, **When** quy tắc đó được rà soát đối chiếu với phía máy chủ, **Then** tồn tại một kiểm tra tương đương được máy chủ thực thi một cách độc lập.
2. **Given** kiểm tra phía client của SPA bị bỏ qua bằng cách gọi trực tiếp đến API với dữ liệu không hợp lệ, **When** yêu cầu đó đến máy chủ, **Then** máy chủ tự từ chối yêu cầu dựa trên kiểm tra phía máy chủ của chính nó.
3. **Given** một yêu cầu hợp lệ theo đúng quy tắc nghiệp vụ được gửi trực tiếp đến API, bỏ qua SPA, **When** máy chủ xử lý yêu cầu đó, **Then** yêu cầu được chấp nhận — kiểm tra phía máy chủ không chặn nhầm dữ liệu hợp lệ.

---

### Edge Cases

- Điều gì xảy ra với một message handler xử lý sự kiện nội bộ giữa các dịch vụ, vốn không mang theo token người dùng như một HTTP endpoint? Handler đó vẫn PHẢI có một quyết định phân quyền/tin cậy tường minh (ví dụ: khai báo rõ nguồn phát hành sự kiện được tin cậy), không được xử lý sự kiện dựa trên một giả định ngầm định.
- Điều gì xảy ra khi một endpoint được chủ đích thiết kế để không yêu cầu phân quyền (ví dụ: health check, endpoint đăng nhập)? Trường hợp này PHẢI được thể hiện bằng một khai báo miễn trừ tường minh, có thể rà soát được, chứ không phải bằng việc không gắn gì cả.
- Điều gì xảy ra khi một yêu cầu mang token hợp lệ, đã xác thực thành công (đúng danh tính), nhưng thiếu claim/policy mà endpoint yêu cầu? Yêu cầu đó PHẢI bị từ chối với 403 (đã xác thực nhưng không đủ quyền), không được nhầm lẫn với 401 (chưa xác thực) hay được xử lý như 200.
- Điều gì xảy ra khi một quy tắc nghiệp vụ chỉ được thực thi ở phía client của SPA mà không có kiểm tra tương đương ở phía máy chủ? Đây PHẢI được xem là một khoảng trống cần khắc phục trong phạm vi tính năng này, không phải một thiết kế chấp nhận được.
- Điều gì xảy ra khi có nhiều chính sách phân quyền áp dụng đồng thời cho cùng một endpoint (ví dụ do kế thừa cấu hình)? Kết quả đánh giá PHẢI luôn nghiêng về từ chối khi có sự không chắc chắn hoặc mâu thuẫn giữa các chính sách, không bao giờ nghiêng về cho phép.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Hệ thống PHẢI đảm bảo mọi HTTP endpoint trong phạm vi có một quyết định phân quyền tường minh gắn kèm — hoặc một chính sách (policy/claim) cụ thể được yêu cầu, hoặc một khai báo miễn trừ tường minh — không có endpoint nào được để trống quyết định này.
- **FR-002**: Hệ thống PHẢI đảm bảo mọi message handler trong phạm vi có một quyết định phân quyền/tin cậy tường minh gắn kèm, tương tự như yêu cầu đối với HTTP endpoint ở FR-001.
- **FR-003**: Khi một yêu cầu đến một endpoint yêu cầu một policy/claim cụ thể mà danh tính gửi yêu cầu không đáp ứng, hệ thống PHẢI từ chối yêu cầu đó với phản hồi 403 (Forbidden), không xử lý yêu cầu như thể được phép.
- **FR-004**: Quy trình build hoặc review PHẢI thất bại khi một HTTP endpoint hoặc message handler mới được thêm vào mà không có quyết định phân quyền tường minh theo FR-001/FR-002 — không tồn tại mặc định cho phép âm thầm.
- **FR-005**: Danh sách các endpoint/handler được miễn trừ khỏi yêu cầu phân quyền PHẢI là một danh sách tường minh, có thể rà soát được, không phải một tập hợp ngầm định suy ra từ việc thiếu cấu hình.
- **FR-006**: Đối với mỗi quy tắc nghiệp vụ được SPA kiểm tra ở phía client, hệ thống PHẢI có một kiểm tra tương đương được thực thi độc lập ở phía máy chủ.
- **FR-007**: Máy chủ PHẢI từ chối dữ liệu vi phạm quy tắc nghiệp vụ bất kể yêu cầu đến từ SPA hay được gửi trực tiếp đến API, bỏ qua hoàn toàn kiểm tra phía client.
- **FR-008**: Việc rà soát toàn bộ HTTP endpoint và message handler để tìm trường hợp thiếu quyết định phân quyền PHẢI có thể được thực hiện lặp lại một cách tự động, cho kết quả nhất quán, thay vì chỉ dựa vào việc rà soát thủ công của người review.

### Key Entities

- **Chính sách phân quyền (Authorization Policy)**: Quyết định tường minh gắn với một endpoint hoặc handler, xác định danh tính nào (theo policy/claim) được phép truy cập; có thể là một yêu cầu cụ thể hoặc một khai báo miễn trừ tường minh.
- **Endpoint/Handler**: Một HTTP endpoint hoặc một message handler xử lý sự kiện/tin nhắn — đơn vị nhỏ nhất mà chính sách phân quyền được gắn vào.
- **Quyết định miễn trừ (Exemption Declaration)**: Khai báo tường minh rằng một endpoint/handler cụ thể không yêu cầu phân quyền, kèm theo lý do có thể rà soát được.
- **Cổng chặn build/review (Build/Review Gate)**: Cơ chế thực thi tự động phát hiện endpoint/handler thiếu quyết định phân quyền và chặn việc hợp nhất thay đổi đó.
- **Quy tắc nghiệp vụ (Validation Rule)**: Một ràng buộc dữ liệu hoặc điều kiện nghiệp vụ được kiểm tra ở phía client (SPA) và PHẢI có một kiểm tra tương đương độc lập ở phía máy chủ.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% HTTP endpoint và message handler trong phạm vi có một quyết định phân quyền tường minh (chính sách cụ thể hoặc khai báo miễn trừ) khi được rà soát — không phát hiện trường hợp nào bị thiếu.
- **SC-002**: 100% yêu cầu mang token hợp lệ nhưng thiếu claim theo đúng chính sách yêu cầu của endpoint nhận được phản hồi 403, không có trường hợp nào bị xử lý như 200.
- **SC-003**: 100% các lần thử thêm một endpoint hoặc message handler mới mà không có quyết định phân quyền đều bị build hoặc review chặn lại trước khi hợp nhất vào nhánh chính.
- **SC-004**: 100% quy tắc nghiệp vụ được SPA kiểm tra ở phía client có một kiểm tra tương đương được máy chủ thực thi độc lập, được xác nhận bằng cách bỏ qua kiểm tra phía client và gọi trực tiếp đến API.
- **SC-005**: 0% yêu cầu mang dữ liệu không hợp lệ được máy chủ chấp nhận khi kiểm tra phía client của SPA bị bỏ qua hoàn toàn.

## Assumptions

- Phạm vi tính năng bao gồm cùng tập hợp dịch vụ có thể truy cập từ bên ngoài đã được xác lập ở tính năng máy chủ định danh trước đó ([014-identity-server-auth](../014-identity-server-auth/spec.md)): baskets, bff, gateway, orders, parties, products — cùng với các message handler (consumer) bên trong các dịch vụ đó.
- Việc xác thực danh tính (xác minh token thuộc về ai) đã được giải quyết bởi tính năng [014-identity-server-auth](../014-identity-server-auth/spec.md); tính năng này xây dựng tiếp trên nền đó, tập trung vào việc quyết định một danh tính đã xác thực được phép làm gì (phân quyền), không định nghĩa lại cơ chế xác thực.
- "Chính sách phân quyền" được hiểu theo mô hình dựa trên policy/claim, phù hợp với cách diễn đạt trong tiêu chí chấp nhận và với nguyên tắc "Secure by Default" của hiến pháp nền tảng; định nghĩa cụ thể của từng chính sách theo từng endpoint là một quyết định thiết kế nằm ngoài phạm vi đặc tả này.
- Các endpoint health check/liveness/readiness tiếp tục là một miễn trừ tường minh, có thể rà soát được như đã thiết lập ở tính năng trước đó, không phải là một khoảng trống mà tính năng này cần khắc phục.
- Dịch vụ định danh (identity) — vốn phát hành token thay vì phục vụ các endpoint nghiệp vụ — nằm ngoài phạm vi của các chính sách phân quyền cấp endpoint nghiệp vụ được mô tả ở đây, tương tự cách dịch vụ này đã được loại trừ khỏi phạm vi xác thực độc lập ở tính năng trước đó.
- Cơ chế cụ thể để phát hiện và chặn endpoint/handler thiếu quyết định phân quyền tại thời điểm build/review (ví dụ: kiểm tra tĩnh, quy tắc phân tích mã nguồn) là một quyết định kỹ thuật thuộc phạm vi lập kế hoạch (`plan.md`), không thuộc phạm vi đặc tả này.
