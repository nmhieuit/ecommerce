# Feature Specification: Lan truyền Correlation ID từ Edge đến Frontend

**Feature Branch**: `016-correlation-id-propagation`

**Created**: 2026-09-06

**Status**: Draft

**Input**: User description: "https://nmhieuit.atlassian.net/browse/SCRUM-26 — [SECURE-3] Correlation ID propagation edge-to-frontend. As the SRE-hat-wearer, I want a correlation ID generated at the edge and propagated across every synchronous call, message, and through the frontend so that any failure is traceable across service boundaries (Principle VII). Acceptance Criteria: (1) Given a request enters at the gateway, when a correlation ID is not already present, then one is generated there. (2) Given the correlation ID exists, when the request/event travels through BFF, services, and RabbitMQ messages, then it is present at every hop's logs. (3) Given the SPA makes a request, when I inspect the network tab, then the correlation ID is visible client-side too, not just server-side. Test Scenarios: (1) Place an order and grep Elastic logs across all services for a single correlation ID — confirm it appears at every hop, including the async OrderPlaced event consumer. (2) Send a request without a pre-existing correlation ID — confirm the gateway generates one. (3) Send two concurrent requests — confirm their correlation IDs never cross-contaminate in logs. (Yêu cầu bổ sung: viết đặc tả bằng tiếng Việt có dấu.)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Truy vết end-to-end một luồng xử lý bằng một Correlation ID duy nhất (Priority: P1)

Là người đóng vai SRE, tôi muốn một correlation ID được sinh ra ngay tại điểm vào (gateway) và được lan truyền nguyên vẹn qua mọi lời gọi đồng bộ, mọi message bất đồng bộ, để khi có sự cố xảy ra ở bất kỳ đâu trong chuỗi xử lý, tôi có thể tra cứu toàn bộ hành trình của một request bằng đúng một mã định danh.

**Why this priority**: Đây là giá trị cốt lõi mà tính năng hướng tới — khả năng truy vết một sự cố xuyên suốt ranh giới giữa các dịch vụ (Principle VII). Nếu không có correlation ID được sinh và lan truyền nhất quán qua cả đường đồng bộ lẫn bất đồng bộ, mọi khả năng quan sát khác (kể cả hiển thị phía client) đều không có nền tảng để dựa vào.

**Independent Test**: Có thể kiểm thử độc lập bằng cách đặt một đơn hàng, sau đó tra log tập trung (Elastic) của toàn bộ dịch vụ tham gia xử lý đơn hàng đó theo một correlation ID duy nhất, và xác nhận correlation ID đó xuất hiện ở mọi hop — kể cả tại consumer xử lý sự kiện OrderPlaced một cách bất đồng bộ.

**Acceptance Scenarios**:

1. **Given** một request đi vào gateway mà không mang sẵn một correlation ID hợp lệ, **When** gateway xử lý request đó, **Then** gateway sinh ra một correlation ID mới và gắn nó vào request trước khi request được xử lý tiếp.
2. **Given** một correlation ID đã tồn tại tại gateway, **When** request hoặc sự kiện phát sinh từ nó đi qua BFF, các service, và các message trên RabbitMQ, **Then** correlation ID đó xuất hiện trong log ghi nhận tại từng hop trên đường đi.
3. **Given** một đơn hàng được đặt làm phát sinh một sự kiện OrderPlaced được xử lý bất đồng bộ, **When** log Elastic của toàn hệ thống được tra theo correlation ID của request đặt hàng đó, **Then** log của consumer xử lý sự kiện OrderPlaced đó cũng chứa đúng correlation ID này, không bị đứt đoạn khi chuyển từ đồng bộ sang bất đồng bộ.

---

### User Story 2 - Correlation ID hiển thị được ở phía client (SPA) (Priority: P2)

Là người đóng vai SRE (hoặc người hỗ trợ người dùng), tôi muốn nhìn thấy correlation ID của một request ngay trên tab mạng của trình duyệt khi SPA gọi API, để tôi có thể đối chiếu sự cố mà người dùng gặp phải ở phía client với log phía server mà không cần phải suy đoán hoặc tra cứu qua trung gian.

**Why this priority**: Đây là phần mở rộng của khả năng quan sát ra tới tận đầu cuối người dùng, giúp rút ngắn thời gian chẩn đoán sự cố được người dùng báo cáo. Nó phụ thuộc vào User Story 1 (correlation ID phải tồn tại và được lan truyền nhất quán trước) nhưng bản thân là một lát cắt giá trị riêng biệt, có thể triển khai và kiểm thử sau khi phần backend đã hoàn chỉnh.

**Independent Test**: Có thể kiểm thử độc lập bằng cách mở công cụ kiểm tra mạng của trình duyệt khi SPA thực hiện một lời gọi đến BFF, và xác nhận correlation ID của lời gọi đó hiển thị được trực tiếp trên trình duyệt, không cần truy cập vào log phía server để biết giá trị này.

**Acceptance Scenarios**:

1. **Given** SPA gửi một request đến BFF, **When** tab mạng của trình duyệt được kiểm tra, **Then** correlation ID của request đó hiển thị được ngay trên trình duyệt, không chỉ tồn tại ở phía server.
2. **Given** một request từ SPA gặp lỗi ở phía server, **When** người dùng hoặc người hỗ trợ xem chi tiết request đó trên trình duyệt, **Then** correlation ID hiển thị kèm theo đủ để dùng làm khoá tra cứu log phía server cho đúng request đó.

---

### User Story 3 - Không lẫn lộn Correlation ID giữa các yêu cầu đồng thời (Priority: P3)

Là người đóng vai SRE, tôi cần đảm bảo rằng khi nhiều request được xử lý đồng thời, correlation ID của mỗi request luôn tách biệt rõ ràng trong log, để việc tra cứu theo correlation ID luôn cho ra đúng và chỉ đúng luồng xử lý liên quan, không lẫn dữ liệu của luồng khác.

**Why this priority**: Đây là điều kiện đảm bảo độ tin cậy của hai user story trên dưới tải thực tế — một cơ chế sinh/lan truyền correlation ID chỉ đúng khi xử lý tuần tự nhưng lẫn lộn khi có tải đồng thời sẽ khiến việc truy vết sai lệch, nguy hiểm hơn cả việc không có correlation ID. Vì đây là một thuộc tính đảm bảo chất lượng của cơ chế đã có ở P1/P2 hơn là một khả năng mới, nó được xếp ưu tiên thấp nhất trong ba câu chuyện.

**Independent Test**: Có thể kiểm thử độc lập bằng cách gửi đồng thời hai (hoặc nhiều) request vào hệ thống, sau đó thu thập log liên quan đến cả hai và xác nhận correlation ID của mỗi request là duy nhất, không xuất hiện lẫn trong log của request còn lại.

**Acceptance Scenarios**:

1. **Given** hai request được gửi đồng thời đến hệ thống, **When** log của cả hai request được thu thập từ mọi hop liên quan, **Then** mỗi request có một correlation ID riêng biệt, không trùng với correlation ID của request kia.
2. **Given** correlation ID của một request cụ thể trong số nhiều request đồng thời, **When** log được lọc theo correlation ID đó, **Then** kết quả chỉ chứa các bản ghi log thuộc về đúng request đó, không lẫn bản ghi của request đồng thời khác.

---

### Edge Cases

- Điều gì xảy ra khi request đi vào gateway đã mang sẵn một correlation ID hợp lệ (ví dụ do một hệ thống upstream gắn vào)? Hệ thống PHẢI giữ nguyên giá trị đó xuyên suốt toàn bộ luồng xử lý, không được sinh giá trị mới ghi đè lên nó.
- Điều gì xảy ra khi client gửi kèm một giá trị tự xưng là correlation ID nhưng không hợp lệ về định dạng, hoặc chứa nội dung bất thường (ví dụ ký tự điều khiển nhằm giả mạo log)? Gateway PHẢI coi đó như chưa có correlation ID hợp lệ và sinh một giá trị mới, không được đưa thẳng giá trị chưa qua kiểm tra vào log hoặc truyền tiếp cho các hop phía sau.
- Điều gì xảy ra khi một message trên RabbitMQ bị xử lý lại (retry/redelivery) do lỗi tạm thời? Correlation ID gốc PHẢI được giữ nguyên qua mọi lần thử lại, không bị sinh mới ở mỗi lần redelivery.
- Điều gì xảy ra khi một service gọi sang service khác nhưng bỏ sót việc lan truyền correlation ID (ví dụ do thiếu sót khi triển khai)? Đây PHẢI được xem là một khoảng trống về khả năng quan sát cần khắc phục trong phạm vi tính năng này, không phải một ngoại lệ được chấp nhận.
- Điều gì xảy ra với các lời gọi hoặc sự kiện đi tới hệ thống bên ngoài nền tảng (ví dụ nhà cung cấp thanh toán bên thứ ba)? Việc lan truyền correlation ID ra ngoài ranh giới nền tảng nằm ngoài phạm vi của tính năng này (xem Assumptions).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Gateway PHẢI kiểm tra mỗi request đi vào; nếu request chưa mang một correlation ID hợp lệ, gateway PHẢI sinh một correlation ID mới và gắn vào request trước khi request được xử lý tiếp.
- **FR-002**: Nếu request đi vào gateway đã mang sẵn một correlation ID hợp lệ, hệ thống PHẢI giữ nguyên giá trị đó xuyên suốt toàn bộ luồng xử lý, không được ghi đè bằng một giá trị mới.
- **FR-003**: Correlation ID PHẢI được truyền nguyên vẹn qua mọi lời gọi đồng bộ giữa gateway, BFF, và các service tham gia xử lý cùng một request.
- **FR-004**: Correlation ID PHẢI được đính kèm vào mọi message/sự kiện bất đồng bộ được phát hành lên RabbitMQ, và được consumer xử lý sự kiện đó bảo toàn khi ghi log.
- **FR-005**: Mọi log entry được ghi bởi gateway, BFF, service, hoặc consumer xử lý sự kiện — liên quan đến một request hoặc một chuỗi sự kiện cụ thể — PHẢI chứa correlation ID tương ứng với luồng xử lý đó.
- **FR-006**: Correlation ID PHẢI được trả về hoặc đính kèm theo phản hồi tới SPA theo cách có thể quan sát trực tiếp qua công cụ kiểm tra mạng của trình duyệt, không chỉ tồn tại ở phía server.
- **FR-007**: Hệ thống PHẢI đảm bảo mỗi request/luồng xử lý độc lập nhận một correlation ID duy nhất, không trùng lặp hoặc lẫn lộn với correlation ID của một request/luồng xử lý khác đang chạy đồng thời.
- **FR-008**: Correlation ID của một message bị retry hoặc redelivery bởi cơ chế messaging (ví dụ do lỗi xử lý tạm thời) PHẢI giữ nguyên giá trị gốc, không được sinh mới ở mỗi lần thử lại.
- **FR-009**: Gateway PHẢI kiểm tra định dạng của bất kỳ correlation ID nào do client cung cấp trước khi sử dụng; giá trị không hợp lệ về định dạng PHẢI bị bỏ qua và thay bằng một correlation ID mới do hệ thống sinh ra.

### Key Entities

- **Correlation ID**: Một định danh duy nhất được gán cho một request hoặc một luồng xử lý — bao gồm cả các sự kiện bất đồng bộ phát sinh từ request đó — dùng làm khoá liên kết mọi log ghi nhận trong toàn bộ vòng đời xử lý của luồng đó.
- **Luồng xử lý (Request/Event Trace)**: Chuỗi các bước xử lý — gateway, BFF, một hoặc nhiều service, message bất đồng bộ và consumer của nó — cùng chia sẻ một correlation ID duy nhất từ đầu đến cuối.
- **Bản ghi log (Log Entry)**: Dữ liệu log có cấu trúc được ghi tại mỗi hop trong luồng xử lý; PHẢI mang theo correlation ID của luồng xử lý mà bản ghi đó thuộc về.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Với một đơn hàng bất kỳ được đặt, SRE có thể tra cứu toàn bộ hành trình xử lý (gateway → BFF → services → sự kiện bất đồng bộ) chỉ bằng một correlation ID duy nhất trong vòng dưới 5 phút.
- **SC-002**: 100% request đi vào gateway có một correlation ID hợp lệ được gắn kèm trước khi rời khỏi gateway.
- **SC-003**: 100% log entry được ghi tại mọi hop, cả đồng bộ lẫn bất đồng bộ, của một luồng xử lý đều chứa correlation ID tương ứng — không phát hiện hop nào bị thiếu khi rà soát toàn bộ luồng của một đơn hàng mẫu.
- **SC-004**: SRE hoặc nhà phát triển có thể xem correlation ID của một request bất kỳ ngay trên tab mạng của trình duyệt, không cần truy cập log phía server để biết giá trị này.
- **SC-005**: Khi nhiều request được gửi đồng thời, 0% trường hợp correlation ID của các request bị lẫn lộn với nhau trong log khi rà soát.

## Assumptions

- Phạm vi tính năng bao gồm cùng tập hợp dịch vụ đã tham gia các tính năng nền tảng trước đó — gateway, bff, baskets, orders, parties, products — cùng các consumer xử lý sự kiện bất đồng bộ (ví dụ OrderPlaced) bên trong các dịch vụ đó.
- Correlation ID sử dụng định dạng GUID/UUID chuẩn, phù hợp với quy ước log hiện có của nền tảng; đây không phải dữ liệu nhạy cảm (không chứa PII) nên có thể hiển thị an toàn ở phía client theo Nguyên tắc VI (Secure by Default) của hiến pháp nền tảng.
- Cơ chế kỹ thuật cụ thể để mang correlation ID qua các hop (ví dụ tên header HTTP cụ thể, cách đính kèm vào message RabbitMQ, cách tích hợp với thành phần ServiceDefaults/OpenTelemetry) là quyết định thiết kế thuộc phạm vi `plan.md`, không thuộc phạm vi đặc tả này.
- Tính năng này cụ thể hoá một phần của Nguyên tắc VII (Observable by Default) trong hiến pháp nền tảng — yêu cầu "một correlation ID được sinh tại edge và lan truyền qua mọi lời gọi đồng bộ và mọi message, kể cả qua frontend" — và xây dựng trên nền quan sát được (structured logging, OpenTelemetry) mà nguyên tắc đó đã thiết lập cho toàn bộ nền tảng.
- Các lời gọi hoặc sự kiện đi tới hệ thống bên ngoài nền tảng (ví dụ nhà cung cấp thanh toán bên thứ ba) nằm ngoài phạm vi của tính năng này.
