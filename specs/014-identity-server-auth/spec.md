# Feature Specification: Triển khai máy chủ định danh, thay thế xác thực giả lập

**Feature Branch**: `014-identity-server-auth`

**Created**: 2026-09-01

**Status**: Draft

**Input**: User description: "https://nmhieuit.atlassian.net/browse/SCRUM-23 — [SECURE-3] Stand up identity server, replace stubbed auth. As DevOps, I want a real identity server issuing tokens so that Phase 1's fake user is replaced with genuine authentication, per the platform's centralized identity model. Acceptance Criteria: (1) Given the identity server is running, when a user logs in, then it issues a token containing verified identity and tenant claims. (2) Given a request carries a token, when it reaches the gateway, then the token is validated at the gateway AND independently at each service — the gateway is not a trust boundary services rely on. (3) Given the Phase 1 tenant-resolution stub, when this story completes, then only the resolution source changed (token claim instead of hardcode) — propagation mechanism is untouched. Test Scenarios: (1) Log in through the identity server and confirm a valid JWT/token is issued with tenant claim. (2) Send a request with a tampered token directly to a downstream service (bypassing the gateway) — confirm the service independently rejects it. (3) Send a request with an expired token — confirm it's rejected with a clear 401, not a silent failure." (Yêu cầu bổ sung: viết đặc tả bằng tiếng Việt có dấu.)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Xác thực thực sự thay thế người dùng giả lập (Priority: P1)

Là một người dùng nền tảng, tôi muốn đăng nhập qua một máy chủ định danh thực sự và nhận được một token chứa danh tính đã xác minh cùng thông tin tenant, để phiên làm việc của tôi phản ánh việc xác thực thực sự thay vì người dùng giả lập được gán cứng ở Phase 1.

**Why this priority**: Đây là giá trị cốt lõi của tính năng — nếu không có việc phát hành token thực sự, mọi phần còn lại đều vô nghĩa. Đây là nền tảng thay thế cho phần giả lập mà mọi yêu cầu khác đều dựa vào.

**Independent Test**: Có thể kiểm thử độc lập bằng cách đăng nhập qua máy chủ định danh với thông tin đăng nhập hợp lệ, xác nhận một token được phát hành có chứa danh tính và thông tin tenant đã xác minh, sau đó xác nhận một yêu cầu ở tầng dưới sử dụng token đó được xử lý như một danh tính thực sự, đã được giải quyết — chứ không phải người dùng giả lập của Phase 1.

**Acceptance Scenarios**:

1. **Given** máy chủ định danh đang hoạt động và người dùng có thông tin đăng nhập hợp lệ, **When** họ đăng nhập, **Then** một token được phát hành chứa danh tính đã xác minh và thông tin tenant của họ.
2. **Given** một token hợp lệ từ lần đăng nhập thành công, **When** token đó được gửi đến nền tảng, **Then** các yêu cầu của người dùng được xử lý như đến từ một danh tính thực sự, đã được xác minh — không phải người dùng giả lập của Phase 1.

---

### User Story 2 - Phòng thủ theo chiều sâu: xác thực độc lập tại gateway và từng dịch vụ (Priority: P2)

Là một người vận hành nền tảng, tôi cần mỗi yêu cầu có token được xác thực cả tại gateway lẫn độc lập bên trong từng dịch vụ, để một gateway bị xâm nhập, bị bỏ qua, hoặc cấu hình sai không bao giờ có thể khiến một yêu cầu chưa được xác minh chạm tới dịch vụ.

**Why this priority**: Đây là hiện thực hóa nguyên tắc "gateway không phải là ranh giới tin cậy" của nền tảng, và là thuộc tính bảo mật khiến máy chủ định danh trở nên đáng tin cậy — nhưng nó phụ thuộc vào việc phát hành token ở User Story 1 đã tồn tại trước đó.

**Independent Test**: Có thể kiểm thử độc lập bằng cách gửi một yêu cầu mang token đã bị giả mạo trực tiếp đến một dịch vụ ở tầng dưới, bỏ qua hoàn toàn gateway, và xác nhận dịch vụ đó tự từ chối yêu cầu mà không cần gateway đã chặn từ trước.

**Acceptance Scenarios**:

1. **Given** một yêu cầu mang theo token hợp lệ, **When** yêu cầu đó đến gateway, **Then** gateway xác thực token trước khi chuyển tiếp.
2. **Given** một yêu cầu đến được một dịch vụ ở tầng dưới, **When** dịch vụ xử lý yêu cầu đó, **Then** dịch vụ tự xác thực token một cách độc lập, bất kể yêu cầu có đi qua gateway hay không.
3. **Given** một token bị giả mạo được gửi trực tiếp đến một dịch vụ, bỏ qua gateway, **When** dịch vụ nhận được yêu cầu đó, **Then** dịch vụ tự từ chối yêu cầu.

---

### User Story 3 - Token hết hạn bị từ chối rõ ràng, không âm thầm thất bại (Priority: P3)

Là một người dùng nền tảng, tôi muốn một token đã hết hạn bị từ chối với một lỗi rõ ràng, minh bạch, để tôi biết cần đăng nhập lại thay vì gặp phải một thất bại âm thầm, khó hiểu.

**Why this priority**: Điều này quan trọng đối với trải nghiệm người dùng và khả năng gỡ lỗi, nhưng là một trường hợp biên có rủi ro thấp hơn so với việc thay thế cơ chế xác thực cốt lõi (User Story 1) và xác thực phòng thủ theo chiều sâu (User Story 2).

**Independent Test**: Có thể kiểm thử độc lập bằng cách gửi một token đã hết hạn đến hệ thống và xác nhận hệ thống trả về một từ chối rõ ràng, minh bạch — không phải một thất bại âm thầm hay một lỗi mơ hồ.

**Acceptance Scenarios**:

1. **Given** một token đã hết hạn, **When** token đó được gửi đến gateway hoặc một dịch vụ, **Then** yêu cầu bị từ chối với một phản hồi "không được phép" rõ ràng.
2. **Given** một token hết hạn bị từ chối, **When** phản hồi đó được kiểm tra, **Then** phản hồi rõ ràng, minh bạch và không giống với một thất bại chung chung hay âm thầm.

---

### Edge Cases

- Điều gì xảy ra khi một token không có thông tin tenant, hoặc thông tin tenant bị sai định dạng? Trường hợp này PHẢI được xem như chưa xác định được tenant — việc truy cập lưu trữ dữ liệu KHÔNG được phép tiếp tục (kế thừa đảm bảo cách ly tenant đã có từ trước).
- Điều gì xảy ra khi máy chủ định danh tạm thời không khả dụng? Các lượt đăng nhập mới sẽ thất bại, nhưng các token đã được phát hành trước đó, vẫn còn hiệu lực, tiếp tục được xác thực độc lập tại gateway và từng dịch vụ (việc xác thực không được yêu cầu gọi trực tiếp về máy chủ định danh cho mỗi yêu cầu).
- Điều gì xảy ra khi một yêu cầu hoàn toàn không mang token? Yêu cầu đó PHẢI bị từ chối theo cùng cách một token không hợp lệ hoặc bị giả mạo bị từ chối — không có danh tính mặc định hay dự phòng nào cả.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Hệ thống PHẢI cung cấp một máy chủ định danh xác thực người dùng và phát hành token khi đăng nhập thành công, thay thế người dùng giả lập được gán cứng ở Phase 1.
- **FR-002**: Mọi token được phát hành PHẢI chứa danh tính đã xác minh của người dùng và thông tin tenant của họ.
- **FR-003**: Gateway PHẢI xác thực token của mọi yêu cầu đến trước khi chuyển tiếp yêu cầu đó.
- **FR-004**: Mỗi dịch vụ ở tầng dưới PHẢI tự xác thực token của yêu cầu một cách độc lập, không dựa vào việc gateway đã xác thực từ trước.
- **FR-005**: Một dịch vụ ở tầng dưới PHẢI từ chối một token bị giả mạo hoặc không hợp lệ ngay cả khi yêu cầu đến trực tiếp, bỏ qua gateway.
- **FR-006**: Hệ thống PHẢI từ chối các token đã hết hạn với một sự từ chối rõ ràng, minh bạch, thay vì thất bại âm thầm hoặc lỗi chung chung.
- **FR-007**: Sau khi tính năng này hoàn thành, định danh tenant dùng để truy cập lưu trữ dữ liệu PHẢI được lấy từ thông tin tenant trong token đã xác minh, thay vì phần gán cứng ở Phase 1.
- **FR-008**: Cơ chế lan truyền định danh tenant đã được xác định từ gateway → BFF → các dịch vụ PHẢI giữ nguyên, không thay đổi bởi tính năng này — chỉ có nguồn xác định thay đổi, theo đúng thiết kế ban đầu của phần giả lập ở Phase 1.
- **FR-009**: Việc truy cập lưu trữ dữ liệu PHẢI tiếp tục yêu cầu một định danh tenant đã được xác định, không có tenant mặc định hay dự phòng nào, đúng như đã được thực thi cho phần giả lập ở Phase 1.
- **FR-010**: Một yêu cầu có token không chứa thông tin tenant, hoặc thông tin tenant không thể phân tích hay sai định dạng, PHẢI được xem như chưa xác định được tenant — việc truy cập lưu trữ dữ liệu KHÔNG được phép tiếp tục.
- **FR-011**: Một yêu cầu hoàn toàn không có token PHẢI bị từ chối theo cùng cách một token không hợp lệ bị từ chối (mặc định từ chối, không bao giờ có danh tính mặc định).

### Key Entities

- **Máy chủ định danh (Identity Server)**: Dịch vụ tập trung xác thực người dùng và phát hành token chứa danh tính cùng thông tin tenant đã xác minh, thay thế phần giả lập được gán cứng ở Phase 1.
- **Token**: Thông tin xác thực được phát hành khi đăng nhập; chứa danh tính người dùng đã xác minh và thông tin tenant, có thời hạn sử dụng giới hạn, và được gửi kèm trong mọi yêu cầu tiếp theo.
- **Danh tính người dùng (User Identity)**: Danh tính đã xác minh của một người dùng đã đăng nhập, thay thế người dùng giả lập được sử dụng trước tính năng này.
- **Thông tin tenant (Tenant Claim)**: Định danh tenant được gắn trong token — nay trở thành nguồn xác định tenant, cung cấp dữ liệu cho cùng cơ chế lan truyền gateway → BFF → các dịch vụ mà phần giả lập ở Phase 1 đã thiết lập.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% các lượt đăng nhập thành công tạo ra một token chứa danh tính và thông tin tenant đã xác minh.
- **SC-002**: 100% các token bị giả mạo được gửi trực tiếp đến một dịch vụ ở tầng dưới, bỏ qua gateway, bị chính dịch vụ đó từ chối một cách độc lập.
- **SC-003**: 100% các token đã hết hạn bị từ chối với một phản hồi "không được phép" rõ ràng, không có trường hợp thất bại âm thầm nào, trên toàn bộ các dịch vụ.
- **SC-004**: Việc chuyển nguồn xác định tenant từ phần gán cứng ở Phase 1 sang thông tin tenant trong token không đòi hỏi bất kỳ thay đổi nào đối với cơ chế lan truyền tenant hoặc việc thực thi kiểm tra tenant ở tầng lưu trữ dữ liệu của bất kỳ dịch vụ nào.
- **SC-005**: Mọi dịch vụ có thể truy cập từ bên ngoài tiếp tục từ chối 100% các yêu cầu không xác định được tenant, không có ngoại lệ nào — cùng một đảm bảo mà Phase 1 đã cung cấp, nay được hỗ trợ bởi token thực sự.

## Assumptions

- Việc đăng ký và quản lý tài khoản người dùng (đăng ký mới, đặt lại mật khẩu, quản lý hồ sơ) nằm ngoài phạm vi của tính năng này; tính năng giả định tài khoản người dùng đã tồn tại sẵn và chỉ tập trung vào việc xác thực, phát hành và xác minh token.
- Token được xác thực bằng một cơ chế (ví dụ: xác minh chữ ký) không yêu cầu gọi trực tiếp về máy chủ định danh cho mỗi yêu cầu, để việc máy chủ định danh không khả dụng không làm mất hiệu lực các token đã được phát hành và vẫn còn hạn sử dụng.
- Tính năng này thay thế hoàn toàn phần giả lập được gán cứng ở Phase 1; nó không bổ sung các tính năng tài khoản mới cho người dùng ngoài việc đăng nhập và đăng xuất.
- Cơ chế lan truyền tenant (gateway → BFF → các dịch vụ) và việc thực thi kiểm tra tenant ở tầng lưu trữ dữ liệu đã được thiết lập bởi tính năng giả lập định danh trước đó ([003-stub-identity-tenant-context](../003-stub-identity-tenant-context/spec.md)) giữ nguyên không đổi; chỉ có nguồn xác định thay đổi từ gán cứng sang thông tin trong token.
- Một token có thời hạn sử dụng giới hạn (access token ngắn hạn); khi hết hạn, người dùng cần xác thực lại thay vì được tự động gia hạn âm thầm trong phạm vi tính năng này.
