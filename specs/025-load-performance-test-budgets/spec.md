# Feature Specification: Kiểm thử tải/hiệu năng luồng nghiệp vụ trọng yếu đối chiếu ngân sách hiệu năng của hiến chương

**Feature Branch**: `code/Load-performance-test-against-constitution-budgets`

**Created**: 2026-09-12

**Status**: Draft

**Input**: User description: "Jira SCRUM-32 [RESILIENCE-4] Load/performance test against constitution budgets — As the SRE-hat-wearer, I want a load test of the critical browse→basket→checkout→order path run against the constitution's stated performance budgets so that a regression is objective, not a red dashboard nobody owns (Principle VIII). Acceptance Criteria: (1) Given the critical path, when it's load tested, then p95/p99 latency per endpoint class is measured against the declared SLOs from the prior story. (2) Given a budget is breached, when the test completes, then the run is marked failed, not just 'noted.' (3) Given this is a critical user path, when future changes are made, then this performance test is automated and re-runnable, not a one-off manual exercise. Test Scenarios: 1. Run the load test against the current slice and record baseline p95/p99 numbers. 2. Intentionally introduce a slow DB query, re-run the test — confirm it fails against budget. 3. Fix the regression and re-run — confirm the test passes again, proving it's a real gate."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Đo lường hiệu năng thật của luồng trọng yếu và đối chiếu với ngân sách đã khai báo (Priority: P1)

Là người đóng vai SRE, tôi muốn có một bài kiểm thử tải chạy trên toàn bộ luồng nghiệp vụ trọng yếu duyệt sản phẩm → giỏ hàng → thanh toán → đặt hàng (browse→basket→checkout→order), đo lường độ trễ p95 và p99 riêng cho từng nhóm endpoint tham gia luồng này, và đối chiếu trực tiếp các số đo đó với ngân sách SLO đã khai báo cho từng nhóm endpoint, để biết chắc luồng trọng yếu có đang nằm trong ngân sách hiệu năng đã cam kết hay không, thay vì suy đoán từ cảm nhận.

**Why this priority**: Đây là tiền đề bắt buộc của toàn bộ tính năng — nếu không có số đo thật đối chiếu với ngân sách, không có gì để đánh giá "đạt" hay "vi phạm" (User Story 2), và cũng không có gì để tự động hóa lặp lại (User Story 3). Không có giá trị nào của tính năng tồn tại được nếu thiếu phần này.

**Independent Test**: Chạy bài kiểm thử tải trên luồng trọng yếu ở trạng thái hiện tại của hệ thống, xác nhận nhận được số đo p95/p99 cụ thể cho từng nhóm endpoint tham gia, và mỗi số đo đó có thể đối chiếu trực tiếp với ngân sách SLO tương ứng đã khai báo cho nhóm endpoint đó.

**Acceptance Scenarios**:

1. **Given** luồng nghiệp vụ trọng yếu browse→basket→checkout→order đang hoạt động, **When** bài kiểm thử tải được chạy, **Then** hệ thống trả về độ trễ p95 và p99 đo được riêng cho từng nhóm endpoint tham gia luồng, không phải một con số trung bình gộp chung.
2. **Given** kết quả đo được của một nhóm endpoint, **When** tôi đối chiếu với ngân sách SLO đã khai báo cho nhóm endpoint đó, **Then** tôi xác định được ngay nhóm endpoint đó đang nằm trong ngân sách hay đã vượt ngân sách.
3. **Given** đây là lần chạy đầu tiên trên một lát cắt hệ thống, **When** bài kiểm thử tải hoàn tất, **Then** số đo p95/p99 của lần chạy đó được ghi lại như một mốc nền (baseline) để so sánh cho các lần chạy sau.

---

### User Story 2 - Vi phạm ngân sách khiến lần chạy kiểm thử thất bại rõ ràng (Priority: P1)

Là người đóng vai SRE, tôi muốn một lần chạy kiểm thử tải mà bất kỳ nhóm endpoint nào trong luồng trọng yếu vượt ngân sách SLO đã khai báo phải kết thúc ở trạng thái THẤT BẠI dứt khoát, không phải một ghi chú cảnh báo trong log mà không ai buộc phải xử lý, để một hồi quy hiệu năng luôn có hậu quả cụ thể (chặn phát hành) thay vì chìm vào một dashboard đỏ mà không ai sở hữu.

**Why this priority**: Đo lường (User Story 1) mà không gắn với một hậu quả rõ ràng khi vi phạm thì ngân sách chỉ là con số trang trí. Đây là phần biến số đo thành một cổng chặn (gate) thật sự, nên có mức ưu tiên ngang với User Story 1.

**Independent Test**: Cố ý đưa vào một hồi quy hiệu năng (ví dụ một truy vấn cơ sở dữ liệu bị làm chậm) trên một nhóm endpoint thuộc luồng trọng yếu, chạy lại bài kiểm thử tải, xác nhận lần chạy đó kết thúc ở trạng thái thất bại rõ ràng vì vượt ngân sách — không phải trạng thái thành công kèm cảnh báo.

**Acceptance Scenarios**:

1. **Given** một nhóm endpoint có độ trễ đo được vượt ngân sách SLO đã khai báo, **When** bài kiểm thử tải hoàn tất, **Then** toàn bộ lần chạy được đánh dấu là THẤT BẠI, không phải "thành công có ghi chú" hay chỉ hiển thị cảnh báo.
2. **Given** một hồi quy hiệu năng được đưa vào có chủ đích (ví dụ một truy vấn chậm), **When** bài kiểm thử tải được chạy lại ngay sau đó, **Then** lần chạy thất bại, chứng minh cổng chặn phát hiện đúng vi phạm thật.
3. **Given** hồi quy hiệu năng vừa được khắc phục, **When** bài kiểm thử tải được chạy lại lần nữa, **Then** lần chạy trở lại trạng thái thành công, chứng minh đây là một cổng chặn có thể tin cậy chứ không phải luôn báo thất bại hoặc luôn báo thành công bất kể tình trạng thật.

---

### User Story 3 - Kiểm thử tải tự động và có thể chạy lại lặp lại theo mỗi thay đổi (Priority: P2)

Là người đóng vai SRE, tôi muốn bài kiểm thử tải trên luồng trọng yếu là một quy trình tự động, có thể kích hoạt lặp lại nhiều lần theo mỗi thay đổi liên quan mà không cần chuẩn bị thủ công lại từ đầu mỗi lần, để việc bảo vệ ngân sách hiệu năng là một cổng chặn thường trực, không phải một hoạt động kiểm thử thủ công chỉ làm một lần rồi bỏ quên.

**Why this priority**: Đây là phần hoàn thiện giá trị lâu dài của tính năng ("một quy trình tự động, không phải thủ công một lần") — nhưng ngay cả khi phần tự động hóa lặp lại theo pipeline này chưa hoàn thiện, User Story 1 và 2 vẫn tạo ra giá trị vận hành (một bài kiểm thử có thể chạy tay để xác nhận ngân sách khi cần). Vì vậy ưu tiên thấp hơn hai user story trên.

**Independent Test**: Kích hoạt bài kiểm thử tải nhiều lần liên tiếp (ví dụ giả lập nhiều lần thay đổi mã nguồn liên quan đến luồng trọng yếu) mà không cần thao tác chuẩn bị thủ công nào ngoài việc kích hoạt chạy, xác nhận mỗi lần chạy đều tự hoàn tất và trả về kết quả pass/fail nhất quán theo cùng một tiêu chí ngân sách.

**Acceptance Scenarios**:

1. **Given** bài kiểm thử tải đã tồn tại cho luồng trọng yếu, **When** tôi kích hoạt chạy lại nhiều lần liên tiếp, **Then** mỗi lần chạy tự hoàn tất và trả về kết quả mà không cần tôi phải chuẩn bị lại kịch bản hay dữ liệu kiểm thử theo cách thủ công.
2. **Given** một thay đổi mã nguồn ảnh hưởng đến luồng trọng yếu vừa được đưa vào, **When** thay đổi đó cần được xác nhận không gây hồi quy hiệu năng, **Then** bài kiểm thử tải có thể được kích hoạt lại ngay để đưa ra kết quả pass/fail cho riêng thay đổi đó.
3. **Given** bài kiểm thử tải được chạy theo lịch trình định kỳ trên môi trường giống production, **When** một lần chạy theo lịch phát hiện vi phạm ngân sách, **Then** kết quả thất bại đó có khả năng chặn phát hành ngay cả khi các cổng kiểm tra khác của PR đã xanh (theo đúng quy trình cổng hiệu năng đã nêu ở hiến chương dự án).

---

### Edge Cases

- Một nhóm endpoint tham gia luồng trọng yếu nhưng chưa có ngân sách SLO được khai báo (ví dụ hạng mục khai báo SLO chưa phủ hết mọi service): cần làm rõ bài kiểm thử tải xử lý ra sao đối với nhóm endpoint thiếu ngân sách — bỏ qua đối chiếu, cảnh báo thiếu khai báo, hay chặn hẳn lần chạy.
- Tải giả lập trong bài kiểm thử không phản ánh đúng tỷ lệ traffic thật giữa các bước của luồng (ví dụ giỏ hàng được gọi nhiều hơn thanh toán trong thực tế) khiến số đo p95/p99 bị lệch so với vận hành thật.
- Môi trường chạy kiểm thử tải khác biệt đáng kể với production (tài nguyên, dữ liệu, cấu hình mở rộng) khiến việc đối chiếu với ngân sách SLO không còn ý nghĩa so sánh trực tiếp.
- Nhiều lần chạy kiểm thử tải diễn ra đồng thời hoặc trùng với tải vận hành thật, gây nhiễu số đo và dẫn đến kết quả thất bại giả (false positive) không phản ánh đúng hiệu năng thật của mã nguồn.
- Một lần chạy thất bại vì lý do hạ tầng của chính bài kiểm thử (ví dụ công cụ tạo tải bị treo) chứ không phải vì luồng trọng yếu thật sự vượt ngân sách: cần phân biệt rõ giữa "cổng chặn hiệu năng thật sự kích hoạt" và "bài kiểm thử tự nó gặp lỗi".

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Hệ thống PHẢI cung cấp một bài kiểm thử tải chạy trên toàn bộ luồng nghiệp vụ trọng yếu duyệt sản phẩm → giỏ hàng → thanh toán → đặt hàng (browse→basket→checkout→order).
- **FR-002**: Bài kiểm thử tải PHẢI đo lường độ trễ p95 và độ trễ p99 riêng biệt cho từng nhóm endpoint tham gia luồng trọng yếu, không gộp chung thành một con số trung bình duy nhất.
- **FR-003**: Với mỗi nhóm endpoint đã có ngân sách SLO được khai báo, số đo p95/p99 của bài kiểm thử tải PHẢI được đối chiếu trực tiếp với ngân sách đã khai báo tương ứng cho nhóm endpoint đó.
- **FR-004**: Khi bất kỳ chỉ tiêu (p95 hoặc p99) của bất kỳ nhóm endpoint nào trong luồng trọng yếu vượt ngân sách đã khai báo, toàn bộ lần chạy kiểm thử PHẢI được đánh dấu là THẤT BẠI — không được kết thúc ở trạng thái thành công kèm cảnh báo hay chỉ ghi chú.
- **FR-005**: Kết quả của mỗi lần chạy kiểm thử tải PHẢI cho phép ghi lại và tra cứu lại số đo p95/p99 làm mốc nền (baseline), phục vụ so sánh cho các lần chạy sau.
- **FR-006**: Bài kiểm thử tải PHẢI có khả năng được kích hoạt chạy lại nhiều lần mà không cần chuẩn bị thủ công lại môi trường, dữ liệu, hay kịch bản kiểm thử ở mỗi lần chạy.
- **FR-007**: Bài kiểm thử tải PHẢI được tích hợp vào một quy trình tự động (chạy theo lịch trình định kỳ trên môi trường giống production, hoặc theo mỗi thay đổi liên quan) thay vì chỉ là một thao tác thủ công một lần.
- **FR-008**: Khi một lần chạy theo lịch trình tự động phát hiện vi phạm ngân sách trên luồng trọng yếu, kết quả đó PHẢI có khả năng chặn phát hành, kể cả khi các cổng kiểm tra khác của PR đã đạt.
- **FR-009**: Hệ thống PHẢI chứng minh được khả năng phát hiện đúng hồi quy hiệu năng thật: khi một hồi quy được đưa vào có chủ đích, lần chạy kế tiếp phải thất bại; khi hồi quy được khắc phục, lần chạy kế tiếp phải thành công trở lại.
- **FR-010**: Việc bổ sung bài kiểm thử tải KHÔNG được thay đổi hành vi phản hồi hiện có của luồng nghiệp vụ trọng yếu trong môi trường vận hành thật — đây là một công cụ kiểm thử và quan sát thêm vào, không phải một thay đổi chức năng nghiệp vụ.

### Key Entities *(include if feature involves data)*

- **Luồng nghiệp vụ trọng yếu**: chuỗi các bước duyệt sản phẩm → giỏ hàng → thanh toán → đặt hàng, gồm tập hợp các nhóm endpoint tham gia mà bài kiểm thử tải nhắm tới.
- **Ngân sách SLO theo nhóm endpoint**: giá trị p95/p99 đã khai báo cho từng nhóm endpoint (theo hạng mục khai báo SLO trước đó), dùng làm ngưỡng đối chiếu cho mỗi lần chạy kiểm thử tải.
- **Kết quả một lần chạy kiểm thử tải**: gồm số đo p95/p99 thực tế theo từng nhóm endpoint, trạng thái pass/fail tổng thể của lần chạy, thời điểm chạy, và (khi có) mốc nền để so sánh với các lần chạy trước.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Sau lần chạy kiểm thử tải đầu tiên trên luồng trọng yếu, số đo p95/p99 nền (baseline) cho từng nhóm endpoint tham gia được ghi nhận và có thể tra cứu lại.
- **SC-002**: 100% lần chạy kiểm thử tải có ít nhất một nhóm endpoint vượt ngân sách SLO đã khai báo đều kết thúc ở trạng thái thất bại rõ ràng — không có trường hợp nào vượt ngân sách mà vẫn báo cáo thành công.
- **SC-003**: Khi một hồi quy hiệu năng được đưa vào có chủ đích trên luồng trọng yếu, lần chạy kiểm thử tải kế tiếp phát hiện và báo thất bại mà không cần điều tra thủ công để nhận ra vấn đề.
- **SC-004**: Sau khi hồi quy hiệu năng được khắc phục, lần chạy kiểm thử tải kế tiếp trở lại trạng thái thành công mà không cần thay đổi cấu hình hay kịch bản kiểm thử.
- **SC-005**: Bài kiểm thử tải có thể được kích hoạt lại nhiều lần liên tiếp (ví dụ theo mỗi thay đổi mã nguồn liên quan) mà không cần bất kỳ thao tác chuẩn bị thủ công nào ngoài việc kích hoạt chạy.

## Assumptions

- Ngân sách SLO theo từng nhóm endpoint tham gia luồng trọng yếu đã được khai báo sẵn từ hạng mục trước đó (khai báo SLO theo service trong manifest); phạm vi của tính năng này là đo lường và đối chiếu, không phải khai báo lại ngân sách.
- Luồng nghiệp vụ trọng yếu là browse→basket→checkout→order như mô tả trong yêu cầu gốc, dựa trên các endpoint hiện có của luồng đặt hàng đầu-cuối đã được hiện thực trong hệ thống.
- Môi trường chạy bài kiểm thử tải là một môi trường giống production (production-like) đủ để ngân sách SLO còn giữ ý nghĩa so sánh, không nhất thiết là chạy trực tiếp trên môi trường production thật.
- "Nhóm endpoint" trong đặc tả này tương ứng với cách phân loại đã dùng khi khai báo ngân sách SLO (ví dụ theo service hoặc theo loại luồng đọc/ghi), không phải đối chiếu ngân sách cho từng endpoint đơn lẻ một cách tách biệt.
- Cơ chế tích hợp cụ thể vào pipeline tự động (công cụ CI/CD, tần suất lịch trình chính xác, công cụ tạo tải) là chi tiết triển khai nằm ngoài phạm vi đặc tả; yêu cầu chỉ ràng buộc rằng bài kiểm thử phải tái sử dụng được và có khả năng chặn phát hành khi vi phạm.
