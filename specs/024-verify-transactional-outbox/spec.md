# Feature Specification: Xác minh outbox pattern giao dịch trên dịch vụ phát sự kiện đơn hàng

**Feature Branch**: `code/Verify-transactional-outbox-on-the-order-publisher`

**Created**: 2026-09-11

**Status**: Draft

**Input**: User description: "Jira SCRUM-31 [RESILIENCE-4] Verify transactional outbox on the order publisher — As QA, I want to verify the transactional outbox pattern on the order publisher so that state change and event publication can never diverge, even under crash conditions (Principle IV). Acceptance Criteria: (1) Given an order is created, when the OrderPlaced event is published, then the order write and the outbox write happen in the same transaction. (2) Given the process crashes after the DB commit but before the message is sent, when it restarts, then the outbox relay still publishes the event (at-least-once). (3) Given a consumer receives the same event twice, when it processes it, then the duplicate is a no-op (idempotent consumer). Test Scenarios: 1. Kill the orders service process immediately after commit, before the outbox relay runs — restart and confirm the event still gets published. 2. Manually replay an OrderPlaced event twice into the consumer — confirm no duplicate side effects. 3. Force a DB rollback on order creation — confirm no event is published for the failed order."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ghi đơn hàng và ghi outbox không bao giờ lệch nhau (Priority: P1)

Là người đóng vai QA, tôi muốn xác nhận rằng mỗi khi một đơn hàng được tạo thành công, bản ghi đơn hàng và bản ghi outbox (chứa sự kiện OrderPlaced chờ phát) luôn được ghi cùng lúc trong một giao dịch cơ sở dữ liệu duy nhất, để không bao giờ tồn tại tình huống đơn hàng đã được ghi nhận mà sự kiện tương ứng bị thiếu, hoặc ngược lại.

**Why this priority**: Đây là tiền đề của toàn bộ cơ chế outbox. Nếu việc ghi đơn hàng và ghi outbox không đảm bảo tính nguyên tử (atomic) với nhau, thì khả năng phục hồi sau sự cố (User Story 2) và tính idempotent của consumer (User Story 3) đều trở nên vô nghĩa vì dữ liệu gốc đã không nhất quán ngay từ đầu.

**Independent Test**: Tạo một đơn hàng thành công qua luồng nghiệp vụ bình thường, sau đó kiểm tra trực tiếp cơ sở dữ liệu để xác nhận bản ghi đơn hàng và bản ghi outbox tương ứng cùng tồn tại, không cần chờ sự kiện thực sự được phát ra ngoài.

**Acceptance Scenarios**:

1. **Given** một yêu cầu tạo đơn hàng hợp lệ, **When** giao dịch tạo đơn hàng được commit thành công, **Then** bản ghi đơn hàng và bản ghi outbox chứa sự kiện OrderPlaced tương ứng cùng được ghi nhận trong cùng một giao dịch cơ sở dữ liệu đó.
2. **Given** giao dịch tạo đơn hàng bị rollback vì bất kỳ lý do gì (lỗi nghiệp vụ, lỗi hệ thống), **When** tôi kiểm tra lại cơ sở dữ liệu sau đó, **Then** không có bản ghi đơn hàng nào và cũng không có bản ghi outbox nào được tạo ra cho yêu cầu đó — cả hai cùng biến mất hoặc cùng không tồn tại.
3. **Given** nhiều yêu cầu tạo đơn hàng được gửi đồng thời, **When** tôi rà soát toàn bộ các đơn hàng đã ghi nhận thành công, **Then** mỗi đơn hàng đều có đúng một bản ghi outbox tương ứng, không có đơn hàng nào thiếu hoặc có nhiều hơn một bản ghi outbox trùng lặp.

---

### User Story 2 - Sự kiện đơn hàng không bị mất kể cả khi tiến trình sập ngay sau khi ghi dữ liệu (Priority: P1)

Là người đóng vai QA, tôi muốn xác nhận rằng ngay cả khi tiến trình của dịch vụ đơn hàng bị dừng đột ngột đúng vào thời điểm giữa lúc giao dịch tạo đơn hàng đã commit và lúc sự kiện OrderPlaced thực sự được gửi ra message broker, hệ thống khi khởi động lại vẫn tự động phát sự kiện đó ra ngoài, để không có đơn hàng nào "biến mất" khỏi luồng xử lý sau của hệ thống chỉ vì một sự cố về tiến trình.

**Why this priority**: Đây là giá trị cốt lõi mà outbox pattern hướng tới — khả năng chịu lỗi khi crash xảy ra đúng vào "điểm mù" giữa ghi dữ liệu và gửi message. Đây là kịch bản khó tái hiện thủ công và dễ bị bỏ sót nhất nếu không có một bài kiểm thử chuyên biệt, nên được ưu tiên ngang với User Story 1.

**Independent Test**: Với một cơ chế mô phỏng cho phép dừng tiến trình dịch vụ đơn hàng ngay sau khi giao dịch tạo đơn hàng commit nhưng trước khi outbox relay kịp gửi message, khởi động lại dịch vụ và xác nhận sự kiện OrderPlaced của đơn hàng đó cuối cùng vẫn được gửi ra message broker, không cần thao tác thủ công nào khác ngoài việc khởi động lại tiến trình.

**Acceptance Scenarios**:

1. **Given** một đơn hàng đã được tạo và giao dịch đã commit thành công nhưng tiến trình dịch vụ dừng đột ngột trước khi outbox relay gửi message tương ứng, **When** dịch vụ khởi động lại, **Then** outbox relay tự động phát hiện bản ghi outbox chưa gửi và gửi sự kiện OrderPlaced đó ra message broker mà không cần can thiệp thủ công.
2. **Given** một sự kiện đã được outbox relay gửi ra thành công, **When** tôi kiểm tra lại trạng thái bản ghi outbox tương ứng, **Then** bản ghi đó được đánh dấu là đã gửi, để lần quét tiếp theo của outbox relay không gửi lại sự kiện đó thêm một lần nữa (trừ trường hợp bị buộc phát lại do lỗi mạng, vốn được xử lý ở khía cạnh "phát ít nhất một lần").
3. **Given** dịch vụ khởi động lại nhiều lần liên tiếp trong lúc vẫn còn bản ghi outbox chưa gửi, **When** outbox relay chạy sau mỗi lần khởi động, **Then** sự kiện đó cuối cùng vẫn được gửi thành công đúng một lần theo góc nhìn nghiệp vụ (hiệu lực xử lý chỉ một lần), dù có thể có tối đa một vài lần gửi trùng ở tầng vận chuyển.

---

### User Story 3 - Consumer nhận trùng sự kiện không gây ra tác dụng phụ trùng lặp (Priority: P2)

Là người đóng vai QA, tôi muốn xác nhận rằng khi consumer của sự kiện OrderPlaced nhận được cùng một sự kiện hai lần (do cơ chế "phát ít nhất một lần" của outbox relay), lần xử lý thứ hai không tạo ra thêm bất kỳ tác dụng phụ nào ngoài mong muốn, để tính đảm bảo "không mất sự kiện" của User Story 2 không đổi lấy rủi ro xử lý trùng lặp ở phía nhận.

**Why this priority**: Idempotency ở consumer là điều kiện bắt buộc để "phát ít nhất một lần" (at-least-once) trở nên an toàn khi sử dụng trong thực tế. Tuy nhiên hệ thống vẫn có giá trị vận hành ở mức chấp nhận được nếu hai giá trị cốt lõi ở User Story 1 và 2 đã được đảm bảo, nên ưu tiên thấp hơn.

**Independent Test**: Phát thủ công (replay) cùng một sự kiện OrderPlaced hai lần liên tiếp vào consumer đang chạy độc lập, sau đó kiểm tra trạng thái/dữ liệu do consumer đó quản lý để xác nhận không có tác dụng phụ nào bị nhân đôi.

**Acceptance Scenarios**:

1. **Given** một sự kiện OrderPlaced đã được consumer xử lý thành công một lần, **When** cùng sự kiện đó (cùng định danh) được gửi lại lần thứ hai vào consumer, **Then** lần xử lý thứ hai không tạo thêm bất kỳ bản ghi, trạng thái, hay tác dụng phụ nào mới — kết quả cuối cùng giống hệt như khi sự kiện chỉ được xử lý một lần.
2. **Given** hai bản sao giống hệt nhau của cùng một sự kiện OrderPlaced đến gần như đồng thời, **When** consumer xử lý cả hai, **Then** chỉ một trong hai lần xử lý tạo ra tác dụng phụ thực sự, lần còn lại được nhận diện là trùng lặp và bỏ qua an toàn.
3. **Given** một sự kiện OrderPlaced đến consumer nhưng có nội dung khác về mặt thời điểm phát sinh so với một sự kiện trước đó đã xử lý cho cùng đơn hàng, **When** consumer xử lý sự kiện này, **Then** consumer phân biệt được đây là sự kiện mới hợp lệ (không phải bản sao trùng lặp) dựa trên định danh sự kiện, không nhầm lẫn giữa "trùng lặp" và "sự kiện hợp lệ tiếp theo cho cùng đơn hàng".

---

### Edge Cases

- Outbox relay tự nó gặp sự cố kéo dài (ví dụ mất kết nối tới message broker trong nhiều giờ): cần xác nhận các bản ghi outbox chưa gửi vẫn được giữ lại an toàn và được gửi bù ngay khi kết nối phục hồi, không bị bỏ sót hay xóa nhầm.
- Nhiều instance của dịch vụ đơn hàng (hoặc nhiều instance của outbox relay) chạy song song: cần xác nhận không có instance nào gửi trùng cùng một bản ghi outbox một cách không kiểm soát (ví dụ do thiếu cơ chế khóa/nhận diện instance đang xử lý).
- Consumer bị lỗi giữa chừng khi đang xử lý một sự kiện (ví dụ crash trước khi ghi nhận đã xử lý xong): cần xác nhận sự kiện đó được xử lý lại từ đầu một cách an toàn (idempotent) ở lần nhận tiếp theo, không bị coi là "đã xử lý" một cách sai lệch.
- Thứ tự sự kiện đến consumer không được đảm bảo tuyệt đối (ví dụ do phát lại): cần xác nhận việc xử lý idempotent không phụ thuộc vào giả định về thứ tự đến của các sự kiện.
- Giao dịch tạo đơn hàng thành công nhưng có lỗi xảy ra ngay trong bước ghi bản ghi outbox (ví dụ vi phạm ràng buộc dữ liệu ở bảng outbox): cần xác nhận toàn bộ giao dịch (cả phần ghi đơn hàng) bị rollback theo, không để lại đơn hàng "mồ côi" không có outbox tương ứng.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Order publisher PHẢI ghi bản ghi đơn hàng và bản ghi outbox chứa sự kiện OrderPlaced tương ứng trong cùng một giao dịch cơ sở dữ liệu duy nhất — hai thao tác ghi này PHẢI cùng thành công hoặc cùng thất bại, không được phép chỉ một trong hai xảy ra.
- **FR-002**: Khi giao dịch tạo đơn hàng bị rollback vì bất kỳ lý do gì, hệ thống PHẢI đảm bảo không có bản ghi outbox nào (và do đó không có sự kiện OrderPlaced nào) được tạo ra cho yêu cầu đó.
- **FR-003**: Outbox relay PHẢI quét và phát các bản ghi outbox chưa gửi ra message broker một cách tự động, liên tục, kể cả sau khi dịch vụ order publisher khởi động lại từ một lần dừng đột ngột (bao gồm trường hợp dừng ngay sau khi giao dịch tạo đơn hàng commit nhưng trước khi message được gửi).
- **FR-004**: Cơ chế phát sự kiện của outbox relay PHẢI đảm bảo mỗi sự kiện đã cam kết (committed) trong outbox cuối cùng được gửi ra ít nhất một lần (at-least-once) — không được phép có sự kiện bị mất vĩnh viễn dù xảy ra sự cố tiến trình hay hạ tầng.
- **FR-005**: Consumer của sự kiện OrderPlaced PHẢI xử lý theo cơ chế idempotent — khi nhận được cùng một sự kiện (nhận diện qua định danh duy nhất của sự kiện) nhiều hơn một lần, các lần xử lý sau lần đầu tiên KHÔNG được tạo thêm bất kỳ tác dụng phụ nào ngoài lần xử lý đầu.
- **FR-006**: Phải có khả năng thiết lập và chạy được các kịch bản kiểm thử xác minh ba khía cạnh trên (tính nguyên tử của việc ghi, khả năng không mất sự kiện khi crash, tính idempotent của consumer) một cách tự động và lặp lại được, để tích hợp vào pipeline kiểm thử liên tục thay vì chỉ kiểm tra thủ công một lần.
- **FR-007**: Việc bổ sung khả năng xác minh và các đảm bảo trên KHÔNG được thay đổi hành vi phản hồi nghiệp vụ hiện có của việc tạo đơn hàng đối với người dùng cuối — đây là một lớp đảm bảo tính nhất quán và khả năng kiểm chứng bổ sung, không phải một thay đổi tính năng nghiệp vụ.

### Key Entities *(include if feature involves data)*

- **Bản ghi outbox (Outbox record)**: gắn với một đơn hàng cụ thể, được ghi trong cùng giao dịch với bản ghi đơn hàng; chứa payload của sự kiện OrderPlaced chờ phát và trạng thái gửi (chưa gửi / đã gửi).
- **Sự kiện OrderPlaced**: sự kiện tích hợp được phát ra khi một đơn hàng được tạo thành công, mang một định danh duy nhất để consumer nhận diện và loại bỏ trùng lặp.
- **Outbox relay**: tiến trình nền có nhiệm vụ quét bảng outbox, phát các bản ghi chưa gửi ra message broker, và đánh dấu chúng là đã gửi sau khi thành công; PHẢI hoạt động lại bình thường sau khi dịch vụ chứa nó khởi động lại.
- **Kịch bản kiểm thử xác minh outbox**: tập hợp các kịch bản kiểm thử tự động hóa (ghi nguyên tử, phục hồi sau crash, idempotency của consumer, rollback) dùng để chứng minh liên tục rằng ba đảm bảo trên vẫn đúng theo thời gian, không chỉ đúng tại một thời điểm kiểm tra thủ công.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% đơn hàng được tạo thành công đều có đúng một bản ghi outbox tương ứng được ghi trong cùng giao dịch — không có trường hợp đơn hàng tồn tại mà thiếu outbox record hoặc ngược lại, xác minh được qua kịch bản kiểm thử tự động chạy lặp lại nhiều lần liên tiếp.
- **SC-002**: Trong kịch bản mô phỏng dừng tiến trình ngay sau khi commit và trước khi gửi message, 100% sự kiện OrderPlaced đã cam kết đều được outbox relay phát ra thành công sau khi hệ thống khởi động lại, không có sự kiện nào bị mất vĩnh viễn qua nhiều lần lặp lại kịch bản.
- **SC-003**: Khi phát lại thủ công cùng một sự kiện OrderPlaced hai lần vào consumer, 0% trường hợp phát sinh thêm tác dụng phụ trùng lặp ở lần xử lý thứ hai, xác minh được qua kịch bản kiểm thử tự động.
- **SC-004**: Khi giao dịch tạo đơn hàng bị rollback, 100% các lần thử đều không ghi nhận bất kỳ sự kiện OrderPlaced nào được phát ra cho đơn hàng đó.
- **SC-005**: Toàn bộ các kịch bản kiểm thử xác minh nêu trên (ghi nguyên tử, phục hồi sau crash, idempotency, rollback) có thể chạy tự động trong pipeline kiểm thử liên tục, cho ra kết quả xác định (deterministic) ở mỗi lần chạy, không cần can thiệp thủ công để lặp lại kịch bản.

## Assumptions

- Order publisher (dịch vụ đơn hàng) là nguồn phát sự kiện OrderPlaced hiện có hoặc đang được hoàn thiện trong hệ thống; đặc tả này tập trung vào các đảm bảo hành vi (ghi nguyên tử, không mất sự kiện, idempotent ở consumer) mà cơ chế outbox của order publisher PHẢI thỏa mãn và khả năng xác minh liên tục các đảm bảo đó, bất kể tại thời điểm hiện tại cơ chế outbox đã được hiện thực đầy đủ hay còn cần bổ sung để đáp ứng các yêu cầu này.
- "Order publisher" trong đặc tả này là (các) thành phần của dịch vụ đơn hàng chịu trách nhiệm tạo đơn hàng và phát sự kiện OrderPlaced; "consumer" là (các) dịch vụ tiêu thụ sự kiện đó hiện có hoặc sẽ có trong hệ thống — đặc tả không định nghĩa lại logic nghiệp vụ cụ thể của từng consumer, chỉ yêu cầu tính idempotent khi nhận trùng.
- Môi trường kiểm thử cho phép mô phỏng có kiểm soát việc dừng đột ngột tiến trình dịch vụ đơn hàng (giữa thời điểm commit và thời điểm gửi message), phát lại thủ công một sự kiện, và buộc rollback giao dịch tạo đơn hàng, mà không ảnh hưởng tới dữ liệu hoặc hệ thống thật.
- Message broker và cơ chế phát sự kiện tích hợp (theo Principle IV của hiến chương dự án — mặc định bất đồng bộ qua RabbitMQ/MassTransit) đã sẵn có ở cấp hạ tầng; đặc tả này không yêu cầu thay đổi lựa chọn hạ tầng đó, chỉ yêu cầu các đảm bảo hành vi nêu trên đối với luồng phát sự kiện OrderPlaced.
