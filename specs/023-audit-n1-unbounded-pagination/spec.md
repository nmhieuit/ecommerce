# Feature Specification: Rà soát N+1 query, truy vấn không giới hạn và thiếu phân trang trên toàn bộ dịch vụ

**Feature Branch**: `code/Audit-for-N+1-queries-unbounded-queries-missing-pagination`

**Created**: 2026-09-11

**Status**: Draft

**Input**: User description: "Jira SCRUM-33 [RESILIENCE-4] Audit for N+1 queries, unbounded queries, missing pagination — As the Developer, I want to audit the slice for N+1 access patterns, unbounded queries, and missing pagination so that these are treated as defects, not future optimization opportunities (Principle VIII). Acceptance Criteria: (1) Given any collection endpoint (e.g., product listing), when I call it, then it paginates rather than returning an unbounded set. (2) Given a query loads related data, when I profile it with EF Core logging, then no N+1 pattern is present. (3) Given a query is bounded, when I check its implementation, then the bound is enforced server-side, not just a client-side page-size hint. Test Scenarios: 1. Seed 500 products and call the listing endpoint without a page parameter — confirm it still returns a bounded page, not all 500. 2. Enable EF Core query logging and load a basket with multiple items — confirm one query (or a deliberate batch), not one query per item. 3. Attempt to request an absurdly large page size — confirm the server caps it."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mọi endpoint trả về danh sách đều tự động phân trang, không cần client yêu cầu (Priority: P1)

Là người đóng vai Developer, tôi muốn mọi endpoint trả về một danh sách (ví dụ danh sách sản phẩm) tự động trả về một trang có giới hạn kích thước ngay cả khi caller không truyền tham số phân trang, để một endpoint không bao giờ có thể trả về toàn bộ tập dữ liệu không giới hạn chỉ vì caller quên hoặc không biết truyền tham số.

**Why this priority**: Đây là rủi ro rõ ràng và dễ khai thác nhất — một endpoint danh sách không giới hạn có thể làm sập service hoặc gây nghẽn hạ tầng chỉ với một lời gọi đơn giản khi dữ liệu tăng trưởng. Không có phân trang mặc định thì hai user story còn lại (chặn N+1, ép giới hạn phía server) không còn nhiều ý nghĩa vì bản thân endpoint đã có thể trả về không giới hạn ngay từ đầu.

**Independent Test**: Gieo một lượng dữ liệu lớn (ví dụ 500 bản ghi) cho một endpoint danh sách bất kỳ, gọi endpoint đó mà không truyền bất kỳ tham số phân trang nào, xác nhận kết quả trả về là một trang có kích thước giới hạn, không phải toàn bộ tập dữ liệu.

**Acceptance Scenarios**:

1. **Given** một endpoint trả về danh sách (ví dụ danh sách sản phẩm) đã có sẵn một lượng lớn bản ghi trong hệ thống, **When** tôi gọi endpoint đó mà không truyền tham số phân trang nào, **Then** kết quả trả về vẫn là một trang có kích thước giới hạn theo mặc định, không phải toàn bộ tập dữ liệu.
2. **Given** toàn bộ endpoint trả về danh sách trong hệ thống (trên mọi service nghiệp vụ) đã được rà soát, **When** tôi tổng hợp kết quả rà soát, **Then** không có endpoint danh sách nào thiếu cơ chế phân trang.
3. **Given** một endpoint danh sách mới được thêm vào hệ thống sau này, **When** endpoint đó được đưa vào sử dụng, **Then** endpoint đó cũng phải tuân theo cùng quy tắc phân trang mặc định như mọi endpoint danh sách khác, không có ngoại lệ mặc định.

---

### User Story 2 - Truy vấn dữ liệu liên quan không phát sinh mẫu hình N+1 (Priority: P1)

Là người đóng vai Developer, tôi muốn mọi truy vấn tải dữ liệu liên quan (ví dụ tải một basket cùng các item bên trong) chỉ phát sinh một truy vấn duy nhất hoặc một lô truy vấn có chủ đích, thay vì một truy vấn riêng cho từng bản ghi liên quan, để số lượng truy vấn tới cơ sở dữ liệu không tăng tuyến tính theo kích thước dữ liệu trả về.

**Why this priority**: Mẫu hình N+1 là một dạng lỗi tiêu tốn tài nguyên âm thầm — không gây lỗi rõ ràng ngay lập tức nhưng làm suy giảm hiệu năng ngày càng nặng khi dữ liệu tăng, và khó phát hiện nếu không chủ động rà soát (profiling) truy vấn. Vì đây là một defect theo đúng nghĩa (không phải cơ hội tối ưu tùy chọn), mức ưu tiên ngang với User Story 1.

**Independent Test**: Bật ghi log truy vấn của EF Core, thực hiện một thao tác tải dữ liệu có quan hệ (ví dụ tải một basket có nhiều item), xác nhận số lượng truy vấn phát sinh là một truy vấn duy nhất hoặc một lô truy vấn có chủ đích, không tỷ lệ thuận với số lượng item.

**Acceptance Scenarios**:

1. **Given** ghi log truy vấn của EF Core đã được bật, **When** tôi tải một basket có nhiều item bên trong, **Then** hệ thống phát sinh một truy vấn duy nhất (hoặc một lô truy vấn có chủ đích, có kiểm soát), không phải một truy vấn riêng cho từng item.
2. **Given** toàn bộ đường dẫn truy vấn tải dữ liệu liên quan trong hệ thống đã được rà soát bằng log truy vấn, **When** tôi tổng hợp kết quả, **Then** không có đường dẫn nào phát sinh số lượng truy vấn tăng tuyến tính theo số bản ghi liên quan.
3. **Given** một mẫu hình N+1 được phát hiện trong quá trình rà soát, **When** mẫu hình đó được ghi nhận, **Then** nó được xử lý như một defect cần sửa, không phải một mục "để tối ưu sau" trong backlog.

---

### User Story 3 - Giới hạn kích thước trang được ép buộc ở phía server, không chỉ là gợi ý từ client (Priority: P2)

Là người đóng vai Developer, tôi muốn giới hạn kích thước trang tối đa của mọi endpoint danh sách được kiểm tra và ép buộc ngay tại phía server, để một caller không thể vượt qua cơ chế phân trang chỉ bằng cách tự khai một kích thước trang rất lớn trong tham số gọi.

**Why this priority**: Nếu phân trang (User Story 1) chỉ dựa vào tham số client tự khai mà không có giới hạn cứng phía server, toàn bộ giá trị bảo vệ của phân trang có thể bị vô hiệu hóa bằng một lời gọi đơn giản. Đây là lớp phòng vệ bổ sung, có giá trị vận hành thấp hơn một chút so với việc bảo đảm phân trang và chặn N+1 tồn tại trước, nên ưu tiên thấp hơn hai user story trên.

**Independent Test**: Gọi một endpoint danh sách bất kỳ với tham số kích thước trang được khai báo lớn bất thường (vượt xa mức hợp lý), xác nhận số bản ghi trả về vẫn bị giới hạn ở một mức trần cố định phía server, không phản ánh đúng giá trị caller yêu cầu.

**Acceptance Scenarios**:

1. **Given** một endpoint danh sách có tham số kích thước trang do client truyền vào, **When** tôi gọi endpoint đó với một giá trị kích thước trang lớn bất thường, **Then** số bản ghi trả về bị giới hạn ở một mức trần cố định do server quy định, không vượt quá mức trần đó bất kể giá trị client truyền vào.
2. **Given** toàn bộ endpoint danh sách có tham số kích thước trang trong hệ thống đã được rà soát, **When** tôi kiểm tra từng implementation, **Then** mọi endpoint đều có mức trần kích thước trang được ép buộc trong code phía server, không chỉ dựa vào giá trị mặc định gợi ý ở tài liệu API.
3. **Given** một endpoint không truyền tham số kích thước trang nào, **When** tôi gọi endpoint đó, **Then** endpoint áp dụng kích thước trang mặc định hợp lý, vẫn nằm trong giới hạn đã ép buộc phía server.

---

### Edge Cases

- Một endpoint danh sách hỗ trợ lọc hoặc sắp xếp phức tạp: cần xác nhận việc ép giới hạn kích thước trang và ngăn N+1 vẫn đúng khi kết hợp với điều kiện lọc/sắp xếp, không chỉ đúng ở trường hợp gọi đơn giản không tham số.
- Một truy vấn tải dữ liệu liên quan nhiều cấp (ví dụ basket → item → thông tin sản phẩm liên quan): cần rà soát toàn bộ chuỗi tải dữ liệu, không chỉ cấp quan hệ đầu tiên, vì N+1 có thể ẩn ở một cấp sâu hơn.
- Một endpoint trả về danh sách rỗng hoặc chỉ có một bản ghi: cơ chế phân trang và kiểm tra N+1 vẫn phải hoạt động đúng ở các trường hợp biên này, không chỉ ở tập dữ liệu lớn.
- Một truy vấn dùng cơ chế tải theo lô (batch loading) có chủ đích để tránh N+1: cần phân biệt rõ đây là một lô truy vấn có kiểm soát, không nhầm lẫn với N+1, khi tính vào kết quả rà soát.
- Client truyền một giá trị kích thước trang âm, bằng 0, hoặc không phải số hợp lệ: cần xác nhận endpoint xử lý các giá trị này một cách an toàn (ví dụ dùng giá trị mặc định hoặc từ chối rõ ràng), không để lọt qua cơ chế ép giới hạn hoặc gây lỗi không kiểm soát.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Mọi endpoint trả về một danh sách (collection endpoint) trên mọi service nghiệp vụ và BFF PHẢI trả về dữ liệu theo trang có giới hạn kích thước, kể cả khi caller không truyền bất kỳ tham số phân trang nào.
- **FR-002**: Mọi truy vấn tải dữ liệu có quan hệ (ví dụ tải một entity cùng các entity liên quan) PHẢI được rà soát bằng công cụ ghi log truy vấn (ví dụ EF Core logging) để xác nhận không phát sinh mẫu hình N+1 — số lượng truy vấn phát sinh KHÔNG được tỷ lệ thuận với số bản ghi liên quan trả về.
- **FR-003**: Khi một truy vấn tải dữ liệu có quan hệ phát sinh nhiều truy vấn theo chủ đích (batch loading) thay vì một truy vấn duy nhất, việc phát sinh nhiều truy vấn đó PHẢI là một quyết định thiết kế có kiểm soát, không phải hệ quả không lường trước của cách truy cập dữ liệu.
- **FR-004**: Mọi endpoint danh sách có hỗ trợ tham số kích thước trang do client truyền vào PHẢI ép buộc một mức trần kích thước trang tối đa ngay trong code phía server; giá trị client truyền vào vượt mức trần PHẢI bị giới hạn lại, không được trả về nhiều hơn mức trần.
- **FR-005**: Mọi mẫu hình N+1, truy vấn không giới hạn, hoặc thiếu phân trang được phát hiện trong quá trình rà soát PHẢI được ghi nhận là một defect cần khắc phục, không được xếp vào loại "cơ hội tối ưu trong tương lai" hay để tồn đọng không xử lý.
- **FR-006**: Việc rà soát PHẢI bao trùm toàn bộ service nghiệp vụ hiện có trong hệ thống (bao gồm tối thiểu products, baskets, orders, parties) cùng lớp BFF, không giới hạn ở một service đơn lẻ.
- **FR-007**: Việc khắc phục các defect phát hiện được (bổ sung phân trang, ép giới hạn kích thước trang, loại bỏ N+1) KHÔNG được thay đổi hợp đồng dữ liệu nghiệp vụ hiện có của endpoint (ví dụ cấu trúc bản ghi trả về), trừ phần bổ sung liên quan trực tiếp đến phân trang (ví dụ thông tin tổng số trang, con trỏ trang tiếp theo).

### Key Entities *(include if feature involves data)*

- **Kết quả rà soát endpoint danh sách**: gắn với một endpoint cụ thể; ghi nhận endpoint đó có phân trang mặc định hay không, có ép giới hạn kích thước trang phía server hay không, và trạng thái đã khắc phục hay chưa.
- **Kết quả rà soát truy vấn dữ liệu liên quan**: gắn với một đường dẫn truy vấn cụ thể (ví dụ một thao tác nghiệp vụ tải dữ liệu có quan hệ); ghi nhận số lượng truy vấn phát sinh quan sát được, có phát hiện mẫu hình N+1 hay không, và trạng thái đã khắc phục hay chưa.
- **Defect phát hiện từ rà soát**: một bản ghi khiếm khuyết cụ thể (phân trang thiếu, N+1, hoặc giới hạn kích thước trang không được ép buộc) gắn với endpoint hoặc đường dẫn truy vấn liên quan, được xử lý theo quy trình defect thông thường của dự án.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% endpoint trả về danh sách trên toàn bộ service nghiệp vụ và BFF trả về một trang có kích thước giới hạn khi được gọi mà không truyền tham số phân trang — xác minh được bằng cách gọi trực tiếp từng endpoint sau khi đã gieo đủ dữ liệu (ví dụ 500 bản ghi).
- **SC-002**: 100% đường dẫn truy vấn tải dữ liệu có quan hệ đã rà soát bằng log truy vấn không còn phát sinh mẫu hình N+1 — số lượng truy vấn không tỷ lệ thuận với số bản ghi liên quan trả về.
- **SC-003**: 100% endpoint danh sách có tham số kích thước trang do client truyền vào có mức trần kích thước trang được ép buộc trong code phía server, xác minh được bằng cách gọi endpoint với một giá trị kích thước trang lớn bất thường và quan sát kết quả trả về vẫn bị giới hạn.
- **SC-004**: Toàn bộ defect phát hiện được trong quá trình rà soát (phân trang thiếu, N+1, giới hạn không ép buộc) được ghi nhận đầy đủ và có trạng thái xử lý rõ ràng, không có defect nào bị bỏ sót không theo dõi.
- **SC-005**: Sau khi khắc phục, không có endpoint danh sách hay đường dẫn truy vấn nào trong phạm vi rà soát còn vi phạm một trong ba tiêu chí (phân trang mặc định, không N+1, giới hạn ép buộc phía server).

## Assumptions

- Phạm vi rà soát bao gồm toàn bộ service nghiệp vụ hiện có trong hệ thống (products, baskets, orders, parties) cùng lớp BFF; các service hạ tầng không có endpoint trả về danh sách dữ liệu nghiệp vụ (ví dụ identity, gateway) không thuộc phạm vi rà soát chính, trừ khi phát hiện endpoint tương tự trong quá trình rà soát.
- Cơ chế phân trang cụ thể (ví dụ dựa trên số trang/kích thước trang hay con trỏ) không bị ràng buộc bởi đặc tả này; mỗi endpoint có thể giữ nguyên cơ chế phân trang hiện có miễn thỏa mãn yêu cầu về giới hạn kích thước và ép buộc phía server.
- Mức trần kích thước trang tối đa cụ thể theo từng endpoint là chi tiết triển khai, được quyết định dựa trên đặc tính dữ liệu của endpoint đó; đặc tả này chỉ yêu cầu một mức trần phải tồn tại và được ép buộc, không quy định con số cụ thể.
- Công cụ ghi log truy vấn dùng để rà soát N+1 là EF Core logging đã có sẵn trong stack hiện tại của dự án; đặc tả này không yêu cầu xây dựng công cụ profiling mới.
- Kết quả rà soát và trạng thái khắc phục defect được theo dõi bằng quy trình quản lý công việc hiện có của dự án (ví dụ backlog Jira); đặc tả này không định nghĩa một hệ thống theo dõi defect mới.
