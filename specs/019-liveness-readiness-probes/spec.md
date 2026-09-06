# Feature Specification: Liveness/readiness probe cho mọi service trên Kubernetes

**Feature Branch**: `code/Liveness-readiness-probes-on-every-service`

**Created**: 2026-09-06

**Status**: Draft

**Input**: User description: "Jira SCRUM-28 [SECURE-3] Liveness/readiness probes on every service — As DevOps, I want liveness and readiness probes on every service so that Kubernetes can detect and route around unhealthy instances automatically. Acceptance Criteria: (1) Given a service is deployed, when I inspect its K8s manifest, then it declares both a liveness and a readiness probe. (2) Given a service is starting up but not yet ready (e.g., DB connection pending), when traffic is routed, then K8s excludes it until the readiness probe passes. (3) Given a service hangs or deadlocks, when the liveness probe fails repeatedly, then K8s restarts the pod. Test Scenarios: 1. Deploy a service and watch its pod go from not-ready to ready — confirm no traffic is routed during the not-ready window. 2. Simulate a hung service (e.g., block its health endpoint) — confirm K8s restarts it after the liveness threshold. 3. Check all 4 services + gateway + BFF — confirm every one has both probes configured, not just some."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mọi service khai báo đầy đủ probe trong manifest triển khai (Priority: P1)

Là một kỹ sư DevOps, tôi muốn manifest triển khai Kubernetes của từng service (parties, products, baskets, orders, identity, gateway, BFF) đều khai báo cả liveness probe lẫn readiness probe trỏ tới endpoint sức khỏe tương ứng của service đó, để Kubernetes có đủ thông tin nhằm tự động phát hiện và định tuyến tránh các instance không khỏe mạnh.

**Why this priority**: Đây là tiền đề bắt buộc của toàn bộ tính năng — nếu manifest không khai báo probe thì hành vi loại trừ traffic (User Story 2) và tự khởi động lại pod treo (User Story 3) không thể xảy ra, vì Kubernetes hoàn toàn không biết phải gọi endpoint nào để kiểm tra.

**Independent Test**: Kiểm tra manifest triển khai của từng service đang chạy trong cluster (hoặc bản manifest sẽ được áp dụng) — xác nhận mỗi service đều có cấu hình `livenessProbe` và `readinessProbe` trỏ đúng endpoint sức khỏe của nó, với tham số thời gian (độ trễ khởi động, chu kỳ kiểm tra, ngưỡng lỗi) phù hợp với đặc tính khởi động thực tế của service.

**Acceptance Scenarios**:

1. **Given** manifest triển khai của một service bất kỳ trong số parties, products, baskets, orders, identity, gateway, BFF, **When** tôi kiểm tra nội dung manifest đó, **Then** manifest khai báo cả liveness probe và readiness probe, mỗi probe trỏ tới endpoint sức khỏe tương ứng đã tồn tại của service (endpoint kiểm tra tiến trình còn sống, và endpoint kiểm tra khả năng phục vụ traffic).
2. **Given** toàn bộ 7 service (5 service nghiệp vụ + gateway + BFF) đã được rà soát, **When** tôi tổng hợp kết quả, **Then** không có service nào thiếu một trong hai loại probe.
3. **Given** một service có thời gian khởi động khác biệt (ví dụ cần thời gian kết nối cơ sở dữ liệu lâu hơn), **When** tôi kiểm tra tham số probe của service đó, **Then** tham số độ trễ khởi động và ngưỡng lỗi được thiết lập đủ dung sai để không kích hoạt sai trong điều kiện khởi động bình thường.

---

### User Story 2 - Loại pod chưa sẵn sàng ra khỏi luồng traffic (Priority: P1)

Là một kỹ sư DevOps, tôi muốn Kubernetes tạm thời loại một pod ra khỏi danh sách nhận traffic khi pod đó chưa vượt qua readiness probe (ví dụ đang chờ kết nối cơ sở dữ liệu), để người dùng cuối không bao giờ nhận request bị dội vào một instance chưa sẵn sàng phục vụ.

**Why this priority**: Đây là giá trị nghiệp vụ trực tiếp của readiness probe — cùng mức ưu tiên với User Story 1 vì nếu hành vi loại trừ traffic không xảy ra đúng, việc khai báo probe (US1) chỉ là cấu hình hình thức, không mang lại lợi ích thực tế nào cho việc vận hành.

**Independent Test**: Triển khai (hoặc khởi động lại) một pod của một service phụ thuộc cơ sở dữ liệu, quan sát pha pod chuyển từ trạng thái chưa sẵn sàng sang sẵn sàng, và xác nhận trong suốt khoảng thời gian chưa sẵn sàng đó, không có request nào được định tuyến tới pod này.

**Acceptance Scenarios**:

1. **Given** một pod của service vừa khởi động và readiness probe của nó chưa pass (ví dụ do đang chờ kết nối cơ sở dữ liệu), **When** có traffic gửi tới service đó, **Then** Kubernetes không định tuyến bất kỳ request nào vào pod này cho tới khi readiness probe pass.
2. **Given** readiness probe của một pod chuyển từ pass sang fail trong khi pod đang chạy (ví dụ mất kết nối cơ sở dữ liệu), **When** trạng thái fail được Kubernetes ghi nhận, **Then** pod đó bị loại khỏi danh sách endpoint nhận traffic của service cho tới khi readiness probe pass trở lại, mà không gây gián đoạn toàn bộ service (các pod khác vẫn nhận traffic bình thường).
3. **Given** một service đang thực hiện rolling update, **When** pod mới được tạo ra, **Then** pod mới chỉ bắt đầu nhận traffic sau khi readiness probe của nó pass, trong khi các pod cũ vẫn tiếp tục phục vụ traffic cho tới khi bị thay thế.

---

### User Story 3 - Tự động khởi động lại pod bị treo hoặc deadlock (Priority: P2)

Là một kỹ sư DevOps, tôi muốn Kubernetes tự động khởi động lại một pod khi liveness probe của pod đó liên tục thất bại (dấu hiệu tiến trình bị treo hoặc deadlock), để hệ thống tự phục hồi mà không cần can thiệp thủ công.

**Why this priority**: Đây là năng lực tự phục hồi bổ sung, phụ thuộc vào việc probe đã được khai báo đúng (User Story 1). Giá trị của nó là giảm thời gian gián đoạn khi xảy ra sự cố treo tiến trình, nhưng hệ thống vẫn vận hành được ở mức chấp nhận được (có loại trừ traffic qua readiness) ngay cả khi hành vi tự khởi động lại này chưa hoàn thiện, nên ưu tiên thấp hơn hai user story trên.

**Independent Test**: Giả lập một service bị treo bằng cách chặn phản hồi của endpoint kiểm tra tiến trình sống, quan sát số lần liveness probe thất bại liên tiếp, và xác nhận Kubernetes khởi động lại pod đó ngay sau khi vượt ngưỡng lỗi đã cấu hình.

**Acceptance Scenarios**:

1. **Given** một pod đang chạy nhưng tiến trình bên trong bị treo (không phản hồi endpoint kiểm tra tiến trình sống), **When** liveness probe thất bại liên tiếp vượt quá ngưỡng lỗi đã cấu hình, **Then** Kubernetes khởi động lại pod đó.
2. **Given** một pod tạm thời chậm phản hồi endpoint kiểm tra tiến trình sống nhưng vẫn hoạt động bình thường trở lại trước khi đạt ngưỡng lỗi, **When** liveness probe pass trở lại, **Then** pod không bị khởi động lại.
3. **Given** cơ sở dữ liệu hoặc một dependency ngoài của service bị gián đoạn tạm thời, **When** tôi kiểm tra hành vi của liveness probe trong thời gian đó, **Then** liveness probe không bị ảnh hưởng bởi sự cố dependency ngoài này (chỉ readiness probe phản ánh sự cố đó), để tránh khởi động lại một tiến trình vốn dĩ vẫn khỏe mạnh.

---

### Edge Cases

- Một dependency không bắt buộc (non-critical) của service tạm thời gián đoạn: readiness probe không được fail chỉ vì dependency không bắt buộc này, chỉ fail khi dependency bắt buộc (ví dụ cơ sở dữ liệu chính của service) không sẵn sàng.
- Service không sở hữu cơ sở dữ liệu riêng (ví dụ lớp cổng/tổng hợp không trạng thái): readiness probe của service này cần phản ánh đúng bản chất "sẵn sàng ngay khi tiến trình chạy", thay vì phụ thuộc vào tình trạng của các service khác mà nó gọi tới — tránh việc một service downstream gặp sự cố kéo theo cả lớp cổng/tổng hợp bị loại khỏi traffic.
- Toàn bộ pod của một service đồng loạt fail readiness cùng lúc (ví dụ cơ sở dữ liệu chung bị sập): cần đảm bảo hành vi quan sát được rõ ràng (log, trạng thái) để đội vận hành nhận biết sự cố ở tầng dependency, không chỉ thấy triệu chứng "không còn pod nào sẵn sàng".
- Bản thân endpoint kiểm tra sức khỏe bị lỗi triển khai (bug khiến probe luôn fail hoặc luôn pass sai): cần có khả năng quan sát (log/response) để phân biệt giữa "service thực sự không khỏe" và "probe báo sai".
- Ngưỡng thời gian probe (độ trễ khởi động, chu kỳ, ngưỡng lỗi) đặt quá chặt so với thời gian khởi động thực tế của một service: gây khởi động lại vòng lặp (crash loop) dù service vẫn đang khởi động bình thường.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Manifest triển khai Kubernetes của mỗi service trong hệ thống (bao gồm tất cả service nghiệp vụ, gateway, và BFF) PHẢI khai báo một liveness probe trỏ tới endpoint kiểm tra tiến trình còn sống của service đó.
- **FR-002**: Manifest triển khai Kubernetes của mỗi service PHẢI khai báo một readiness probe trỏ tới endpoint kiểm tra khả năng phục vụ traffic của service đó.
- **FR-003**: Liveness probe KHÔNG được phụ thuộc vào tình trạng của dependency ngoài (cơ sở dữ liệu, service khác) — probe chỉ phản ánh việc tiến trình có còn phản hồi hay không, để tránh khởi động lại một tiến trình khỏe mạnh chỉ vì dependency ngoài gặp sự cố tạm thời.
- **FR-004**: Readiness probe của một service sở hữu cơ sở dữ liệu riêng PHẢI phản ánh khả năng kết nối tới cơ sở dữ liệu bắt buộc của chính service đó; readiness probe của một service không sở hữu dữ liệu (ví dụ lớp cổng/tổng hợp không trạng thái) PHẢI phản ánh đúng bản chất sẵn sàng ngay khi tiến trình chấp nhận request, không phụ thuộc vào tình trạng của service khác mà nó gọi tới.
- **FR-005**: Khi readiness probe của một pod thất bại, hệ thống PHẢI loại pod đó khỏi danh sách nhận traffic mới cho tới khi probe pass trở lại, mà không ảnh hưởng tới các pod khác của cùng service.
- **FR-006**: Khi liveness probe của một pod thất bại liên tiếp vượt quá ngưỡng lỗi đã cấu hình, hệ thống PHẢI tự động khởi động lại pod đó.
- **FR-007**: Tham số thời gian của mỗi probe (độ trễ khởi động, chu kỳ kiểm tra, thời gian chờ, ngưỡng lỗi liên tiếp) PHẢI được thiết lập theo đặc tính khởi động và phục vụ thực tế của từng service, đủ dung sai để không kích hoạt sai trong điều kiện vận hành bình thường.
- **FR-008**: Việc bổ sung khai báo probe KHÔNG được thay đổi hành vi hay hợp đồng phản hồi của các endpoint kiểm tra sức khỏe hiện có của từng service — chỉ kết nối (wiring) các endpoint đã tồn tại vào manifest triển khai.
- **FR-009**: Trong quá trình rolling update, hệ thống PHẢI đảm bảo pod mới chỉ nhận traffic sau khi readiness probe của nó pass, trong khi pod cũ vẫn tiếp tục phục vụ cho tới khi được thay thế, để không gây gián đoạn dịch vụ trong lúc triển khai.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% service đang chạy trong cluster (toàn bộ service nghiệp vụ, gateway, BFF) có cả liveness probe và readiness probe được khai báo và hoạt động — xác minh được bằng cách kiểm tra manifest hoặc trạng thái pod đang chạy.
- **SC-002**: Trong một lần triển khai hoặc khởi động lại pod bất kỳ, tỷ lệ request bị định tuyến vào một pod chưa sẵn sàng là 0%.
- **SC-003**: Khi một service bị giả lập treo (không phản hồi), thời gian từ lúc treo tới lúc pod được tự động khởi động lại nằm trong một khoảng thời gian có thể dự đoán trước và được tài liệu hóa (không cần can thiệp thủ công).
- **SC-004**: Khi một dependency ngoài (ví dụ cơ sở dữ liệu) của một service gặp sự cố tạm thời, service đó không bị khởi động lại một cách không cần thiết — chỉ bị loại khỏi traffic cho tới khi dependency phục hồi.
- **SC-005**: Trong suốt một đợt rolling update của bất kỳ service nào, không ghi nhận request nào thất bại do bị định tuyến vào pod đang trong quá trình khởi động.

## Assumptions

- Mỗi service đã có sẵn endpoint kiểm tra tiến trình sống và endpoint kiểm tra khả năng phục vụ traffic (được xây dựng ở một hạng mục trước đó của hệ thống); phạm vi của tính năng này là khai báo và kết nối (wiring) các endpoint đó vào cấu hình triển khai Kubernetes, không phải xây dựng lại các endpoint kiểm tra sức khỏe.
- "4 service" trong yêu cầu gốc của Jira được hiểu là các service nghiệp vụ hiện có tại thời điểm thực hiện; phạm vi thực tế của tính năng bao trùm toàn bộ service đang được triển khai trong cluster tại thời điểm áp dụng (bao gồm cả gateway và BFF), không giới hạn cứng ở con số 4.
- Định dạng cụ thể của manifest triển khai (YAML thuần, Helm chart, hay công cụ quản lý cấu hình khác) là chi tiết triển khai, không thuộc phạm vi của đặc tả này; đặc tả chỉ quy định hành vi mà manifest phải đảm bảo.
- Ngưỡng thời gian cụ thể cho từng tham số probe (số giây độ trễ khởi động, chu kỳ, ngưỡng lỗi) sẽ được xác định ở giai đoạn lập kế hoạch/triển khai dựa trên đặc tính đo lường thực tế của từng service, miễn là thỏa mãn nguyên tắc "đủ dung sai, không kích hoạt sai" nêu tại FR-007.
