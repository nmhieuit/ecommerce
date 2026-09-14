# Feature Specification: Diễn tập chaos engineering — giết một pod / tiêm độ trễ để kiểm chứng resilience

**Feature Branch**: `code/Chaos-exercise-kill-pod-inject-latency`

**Created**: 2026-09-12

**Status**: Draft

**Input**: User description: "Jira SCRUM-34 [RESILIENCE-4] Chaos exercise: kill a pod / inject latency — As the SRE-hat-wearer, I want to deliberately kill a pod or inject latency into the running system and observe the resilience policies and dashboards react so that the safety nets from earlier stories are proven, not just assumed. Acceptance Criteria: (1) Given the system is running under light synthetic load, when I kill the basket service's pod, then K8s reschedules it and the circuit breaker/retry policy visibly engages in the BFF. (2) Given latency is injected into the orders service, when the load exceeds the declared SLO, then the OTel dashboards show the budget being consumed in near real time. (3) Given the exercise completes, when I write up the outcome, then it either confirms the safety nets worked or becomes a bug ticket if they didn't. Test Scenarios: 1. kubectl delete pod on a running service instance — observe recovery time and whether the BFF degrades gracefully or errors out. 2. Use a fault-injection tool (or manual sleep injection) to add 2s latency to one service — confirm the circuit breaker trips per its configured threshold. 3. Watch the Elastic/OTel dashboard live during the exercise — confirm the SLO burn is visible, not just inferred after the fact."

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.

  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - Giết pod của basket service và quan sát hệ thống tự phục hồi (Priority: P1)

Là người đóng vai SRE, tôi muốn chủ động xóa (kill) pod đang chạy của basket service trong lúc hệ thống đang chịu một tải tổng hợp (synthetic load) nhẹ, để quan sát trực tiếp xem Kubernetes có tái lập lịch (reschedule) pod đó và các chính sách circuit breaker/retry ở BFF có thực sự kích hoạt hay không, thay vì chỉ tin rằng chúng đã được cấu hình đúng.

**Why this priority**: Đây là kịch bản chaos cơ bản nhất và trực tiếp kiểm chứng lại đúng năng lực đã được xây dựng ở các story trước (timeout/retry/circuit breaker, liveness/readiness probes) bằng một sự cố thật thay vì suy đoán trên giấy. Nếu năng lực tự phục hồi khi mất một pod không được xác nhận, mọi lưới an toàn khác đều chỉ là giả định chưa kiểm chứng.

**Independent Test**: Có thể kiểm thử độc lập bằng cách tạo tải tổng hợp nhẹ liên tục gọi vào basket service qua BFF, sau đó chạy lệnh xóa một pod đang chạy của basket service, và xác nhận: (a) Kubernetes tái lập lịch pod mới trong thời gian phục hồi đã quan sát được, (b) trong lúc pod cũ chưa sẵn sàng trở lại, circuit breaker/retry ở BFF quan sát được rõ ràng (qua log/dashboard) là đang engage, và (c) người dùng cuối gọi qua BFF thấy hệ thống giảm cấp một cách có kiểm soát (graceful degradation) chứ không nhận lỗi không rõ nguyên nhân.

**Acceptance Scenarios**:

1. **Given** hệ thống đang chạy dưới một tải tổng hợp nhẹ liên tục gọi tới basket service, **When** tôi xóa (kill) pod đang chạy của basket service, **Then** Kubernetes tái lập lịch (reschedule) một pod thay thế và chính sách circuit breaker/retry ở BFF engage một cách quan sát được (visible) trong lúc pod thay thế chưa sẵn sàng.
2. **Given** pod thay thế của basket service đã sẵn sàng trở lại (ready), **When** tôi tiếp tục theo dõi tải tổng hợp đang chạy, **Then** tỷ lệ lỗi trả về cho người gọi giảm về mức bình thường và circuit breaker đóng mạch (closed) trở lại mà không cần can thiệp thủ công.
3. **Given** pod bị xóa trong lúc đang xử lý một số request, **When** tôi quan sát các request đó ở BFF, **Then** các request đang xử lý dở nhận được phản hồi thất bại rõ ràng (không treo vô thời hạn) hoặc được retry sang instance khác theo đúng chính sách đã cấu hình.

---

### User Story 2 - Tiêm độ trễ vào orders service và quan sát ngân sách SLO bị tiêu hao trên dashboard theo thời gian thực (Priority: P1)

Là người đóng vai SRE, tôi muốn chủ động tiêm thêm độ trễ vào orders service khi tải vượt quá SLO đã khai báo, để quan sát trên dashboard OTel/Elastic xem việc tiêu hao ngân sách SLO có hiển thị gần theo thời gian thực hay không, xác nhận rằng ngân sách SLO là một tín hiệu vận hành sống chứ không phải một con số nằm im trong tài liệu.

**Why this priority**: Cùng mức ưu tiên với User Story 1 vì đây là kịch bản chaos còn lại được liệt kê tường minh trong yêu cầu gốc, và trực tiếp kiểm chứng lại năng lực đã xây dựng ở story khai báo SLO theo manifest — nếu độ trễ tăng bất thường mà dashboard không phản ánh kịp thời, đội vận hành sẽ phát hiện sự cố quá muộn.

**Independent Test**: Có thể kiểm thử độc lập bằng cách dùng công cụ tiêm lỗi (fault-injection) hoặc chèn sleep thủ công để thêm một khoảng độ trễ cố định (ví dụ 2 giây) vào orders service, tạo tải đủ lớn để độ trễ tăng thêm này khiến p95/p99 vượt ngưỡng SLO đã khai báo, và xác nhận dashboard OTel/Elastic thể hiện rõ ràng, gần theo thời gian thực, việc ngân sách SLO đang bị tiêu hao (burn) trong lúc bài tập đang diễn ra — không phải chỉ suy ra được sau khi xem lại log.

**Acceptance Scenarios**:

1. **Given** orders service đang phục vụ tải bình thường trong ngưỡng SLO đã khai báo, **When** tôi tiêm thêm một khoảng độ trễ cố định vào orders service khiến độ trễ vượt ngưỡng đã khai báo, **Then** dashboard OTel/Elastic cập nhật và thể hiện việc ngân sách SLO (error budget) đang bị tiêu hao trong khoảng thời gian gần với thời điểm thực tế xảy ra, không có độ trễ hiển thị đáng kể khiến người xem hiểu lầm hệ thống vẫn khỏe mạnh.
2. **Given** độ trễ đã được tiêm vào orders service vượt ngưỡng SLO liên tục trong một khoảng thời gian, **When** tôi theo dõi dashboard trong suốt khoảng thời gian đó, **Then** dashboard thể hiện xu hướng tiêu hao ngân sách liên tục tương ứng với thời lượng và mức độ vi phạm ngưỡng, chứ không chỉ một cảnh báo bật/tắt rời rạc.
3. **Given** việc tiêm độ trễ đã dừng lại và orders service trở về độ trễ bình thường, **When** tôi tiếp tục theo dõi dashboard, **Then** dashboard thể hiện ngân sách SLO ngừng bị tiêu hao thêm và các chỉ số độ trễ quan sát được quay về trong ngưỡng đã khai báo.

---

### User Story 3 - Ghi lại kết quả bài tập chaos thành một kết luận kiểm chứng được (xác nhận đạt, hoặc bug ticket) (Priority: P2)

Là người đóng vai SRE, tôi muốn sau mỗi lần thực hiện xong một bài tập chaos (kill pod hoặc tiêm độ trễ), kết quả quan sát được phải được ghi lại thành một kết luận rõ ràng — hoặc xác nhận các lưới an toàn hoạt động đúng như kỳ vọng, hoặc trở thành một bug ticket nếu không — để mỗi bài tập chaos đều tạo ra một bằng chứng có thể tra cứu lại, thay vì chỉ là một buổi quan sát rồi bị lãng quên.

**Why this priority**: Đây là bước hoàn thiện giá trị của tính năng — nếu không ghi nhận lại, kết quả của User Story 1 và 2 chỉ tồn tại trong trí nhớ của người thực hiện bài tập, không thể tái sử dụng làm bằng chứng cho các buổi kiểm toán về sau hoặc để theo dõi các bug phát sinh. Tuy nhiên hệ thống vẫn tạo ra giá trị vận hành ở mức chấp nhận được ngay cả khi bước ghi nhận này chưa được chuẩn hóa hoàn toàn, miễn là hai bài tập chaos chính đã thực hiện được, nên ưu tiên thấp hơn.

**Independent Test**: Có thể kiểm thử độc lập bằng cách hoàn thành một bài tập chaos bất kỳ (kill pod hoặc tiêm độ trễ) rồi xác nhận có một bản ghi kết quả (write-up) được tạo ra ngay sau đó, nêu rõ: bài tập nào đã thực hiện, quan sát được gì, và kết luận là "lưới an toàn hoạt động đúng" hay "phát hiện sai lệch — đã mở bug ticket kèm liên kết".

**Acceptance Scenarios**:

1. **Given** một bài tập chaos (kill pod hoặc tiêm độ trễ) đã thực hiện xong và mọi hành vi quan sát được khớp đúng với kỳ vọng của các lưới an toàn liên quan, **When** tôi hoàn tất bài tập, **Then** một bản ghi kết quả được tạo ra xác nhận rõ ràng rằng lưới an toàn đã hoạt động đúng như thiết kế, kèm theo bằng chứng quan sát được (ví dụ ảnh chụp dashboard, thời gian phục hồi đo được).
2. **Given** một bài tập chaos đã thực hiện xong nhưng hành vi quan sát được sai lệch so với kỳ vọng (ví dụ circuit breaker không engage, hoặc dashboard không phản ánh kịp việc tiêu hao SLO), **When** tôi hoàn tất bài tập, **Then** một bug ticket được tạo ra mô tả rõ sai lệch quan sát được, kèm liên kết tới bản ghi kết quả của bài tập chaos đó.
3. **Given** nhiều bài tập chaos đã được thực hiện theo thời gian, **When** tôi cần tra cứu lại lịch sử các bài tập đã chạy, **Then** tôi tìm được danh sách các bản ghi kết quả trước đó kèm kết luận tương ứng của từng lần.

---

### Edge Cases

- Khi pod bị xóa là pod duy nhất đang chạy của basket service (không có replica dự phòng) tại thời điểm chạy bài tập, hệ thống xử lý ra sao trong khoảng thời gian gián đoạn cho tới khi pod mới sẵn sàng?
- Khi độ trễ được tiêm vượt xa timeout đã cấu hình cho orders service (ví dụ tiêm 30 giây trong khi timeout là 2 giây), circuit breaker có mở đúng theo ngưỡng cấu hình, hay lỗi timeout dồn dập gây quá tải cho caller trước khi circuit breaker kịp phản ứng?
- Khi hai bài tập chaos (kill pod và tiêm độ trễ) được chạy chồng lấn thời gian lên nhau ngoài dự kiến, dashboard và cơ chế resilience có phân biệt được nguyên nhân của từng loại sự cố hay không, hay chỉ hiển thị một tình trạng suy giảm chung không rõ nguồn gốc?
- Khi bài tập chaos được chạy nhưng tải tổng hợp nền không đủ lớn để tạo ra request thực sự chạm vào pod/service bị ảnh hưởng, làm sao phát hiện được rằng bài tập chưa thực sự kiểm chứng được điều gì (kết quả "không có dữ liệu" chứ không phải "đạt")?
- Khi bài tập chaos vô tình được chạy nhắm vào môi trường production thay vì môi trường dành riêng cho diễn tập, cơ chế nào ngăn hoặc cảnh báo trước khi gây ảnh hưởng thật tới người dùng cuối?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Hệ thống MUST cho phép người thực hiện bài tập chủ động xóa (kill) một pod đang chạy của một service nghiệp vụ chỉ định (tối thiểu basket service) trong một môi trường dành riêng cho diễn tập, trong khi hệ thống đang chịu một tải tổng hợp (synthetic load) được tạo trước.
- **FR-002**: Hệ thống MUST cho phép người thực hiện bài tập chủ động tiêm thêm một khoảng độ trễ (latency) có thể cấu hình vào một service nghiệp vụ chỉ định (tối thiểu orders service), đủ để làm độ trễ quan sát được vượt ngưỡng SLO đã khai báo của service đó.
- **FR-003**: Khi một pod bị xóa, hệ thống điều phối hạ tầng (Kubernetes) MUST tự động tái lập lịch (reschedule) một pod thay thế mà không cần can thiệp thủ công.
- **FR-004**: Trong khoảng thời gian pod thay thế chưa sẵn sàng, chính sách circuit breaker/retry ở tầng BFF MUST engage theo đúng cấu hình đã có (đã được xây dựng ở tính năng timeout/retry/circuit breaker), và trạng thái engage đó MUST quan sát được (qua log hoặc dashboard) trong lúc bài tập đang diễn ra.
- **FR-005**: Khi độ trễ được tiêm khiến p95/p99 của service bị ảnh hưởng vượt ngưỡng SLO đã khai báo trong manifest của service đó, dashboard giám sát (OTel/Elastic) MUST thể hiện việc ngân sách SLO đang bị tiêu hao trong một khoảng trễ ngắn gần với thời gian thực (near real time), không phải chỉ suy ra được sau khi xem lại dữ liệu lịch sử.
- **FR-006**: Hệ thống MUST giới hạn phạm vi chạy các bài tập chaos (kill pod, tiêm độ trễ) trong một môi trường dành riêng cho diễn tập, tách biệt khỏi môi trường phục vụ người dùng thật (production).
- **FR-007**: Sau khi một bài tập chaos hoàn tất, MUST tạo ra một bản ghi kết quả (write-up) nêu rõ: bài tập nào đã chạy, thời điểm chạy, các quan sát thu được (thời gian phục hồi, hành vi circuit breaker, hành vi dashboard), và kết luận cuối cùng.
- **FR-008**: Nếu kết quả quan sát được của một bài tập chaos sai lệch so với hành vi kỳ vọng của các lưới an toàn liên quan, hệ thống/quy trình MUST tạo một bug ticket mô tả sai lệch đó và liên kết ngược lại bản ghi kết quả của bài tập.
- **FR-009**: Người thực hiện bài tập MUST có khả năng xem lại danh sách các bản ghi kết quả của những bài tập chaos đã chạy trước đó, kèm kết luận tương ứng của từng lần.

### Key Entities *(include if feature involves data)*

- **Bài tập chaos (Chaos Exercise Run)**: Đại diện cho một lần thực hiện một kịch bản chaos cụ thể (kill pod hoặc tiêm độ trễ); có thuộc tính gồm loại kịch bản, service mục tiêu, thời điểm bắt đầu/kết thúc, tham số tiêm lỗi (ví dụ độ trễ bao nhiêu giây), và trạng thái tải tổng hợp nền tại thời điểm chạy.
- **Bản ghi kết quả (Exercise Outcome Write-up)**: Đại diện cho kết luận của một bài tập chaos; có thuộc tính gồm liên kết tới bài tập tương ứng, các quan sát thu được (thời gian phục hồi, bằng chứng dashboard), kết luận (đạt / phát hiện sai lệch), và liên kết tới bug ticket nếu có.
- **Bug Ticket (khi phát hiện sai lệch)**: Đại diện cho một sai lệch được phát hiện qua bài tập chaos; có thuộc tính gồm mô tả sai lệch, liên kết ngược tới bản ghi kết quả đã phát hiện ra nó, và trạng thái xử lý.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Sau khi xóa một pod của một service nghiệp vụ đang chịu tải nhẹ, hệ thống phục hồi khả năng phục vụ đầy đủ (pod thay thế sẵn sàng, tỷ lệ lỗi về mức bình thường) trong vòng thời gian phục hồi có thể đo lường và lặp lại được giữa các lần chạy bài tập.
- **SC-002**: Trong 100% số lần chạy bài tập kill-pod, hành vi engage của circuit breaker/retry ở BFF được quan sát và xác nhận được ngay trong lúc bài tập diễn ra (không phải suy đoán sau khi xem log).
- **SC-003**: Khi độ trễ được tiêm khiến một service vượt ngưỡng SLO đã khai báo, việc tiêu hao ngân sách SLO xuất hiện trên dashboard trong vòng một khoảng thời gian ngắn, có thể đo được, kể từ thời điểm độ trễ thực sự vượt ngưỡng — không có trường hợp nào việc tiêu hao chỉ được phát hiện sau khi bài tập đã kết thúc.
- **SC-004**: 100% các bài tập chaos đã chạy đều có một bản ghi kết quả tra cứu được, với kết luận rõ ràng là "xác nhận đạt" hoặc "đã mở bug ticket kèm liên kết" — không có bài tập nào kết thúc mà không có kết luận ghi nhận lại.
- **SC-005**: Không có bài tập chaos nào trong hoạt động diễn tập định kỳ gây ảnh hưởng quan sát được tới người dùng thật ở môi trường production.

## Assumptions

- Bài tập chaos được thực hiện trong một môi trường dành riêng cho diễn tập (staging/demo), có cấu hình Kubernetes, BFF, và các chính sách resilience (timeout/retry/circuit breaker) tương đương môi trường thật, dựa trên các tính năng resilience đã có sẵn từ các story trước (timeout/retry/circuit breaker, khai báo SLO, OTel/Elastic dashboard).
- "Tải tổng hợp nhẹ" (light synthetic load) là tải được tạo có chủ đích bằng công cụ kiểm thử tải sẵn có của dự án, đủ để tạo ra traffic thực sự chạm vào service mục tiêu trong lúc bài tập diễn ra, không cần định nghĩa một ngưỡng RPS cụ thể trong đặc tả này.
- Việc "tiêm độ trễ" có thể thực hiện bằng công cụ fault-injection chuyên dụng hoặc bằng cách chèn thủ công một khoảng sleep vào service mục tiêu; đặc tả này không ràng buộc công cụ cụ thể, miễn là kết quả tạo ra độ trễ quan sát được đúng như tham số đã định.
- Bản ghi kết quả bài tập chaos có thể là một tài liệu/ticket trong hệ thống theo dõi công việc hiện có của đội (ví dụ Jira) hoặc một tài liệu markdown trong kho mã nguồn; đặc tả này không ràng buộc định dạng lưu trữ cụ thể.
- Bài tập chaos được thực hiện thủ công, có người vận hành trực tiếp theo dõi trong lúc chạy (không yêu cầu tự động hóa hoàn toàn thành một pipeline chaos-as-code trong phạm vi đặc tả này).
