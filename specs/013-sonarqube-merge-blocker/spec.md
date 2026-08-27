# Đặc tả tính năng: Cổng chất lượng SonarQube chặn merge Pull Request

**Nhánh tính năng**: `013-sonarqube-merge-blocker`

**Ngày tạo**: 2026-08-27

**Trạng thái**: Draft

**Đầu vào**: Mô tả từ người dùng: "https://nmhieuit.atlassian.net/browse/SCRUM-22. Dùng tiếng Việt có dấu"

**Nguồn**: Jira [SCRUM-22](https://nmhieuit.atlassian.net/browse/SCRUM-22) — "[CONTRACT-2] Wire SonarQube quality gate as a merge blocker"

> Là DevOps, tôi muốn cổng chất lượng SonarQube được nối vào pipeline build và được thực thi như một điều kiện chặn merge, để các tiêu chuẩn về độ phủ kiểm thử (coverage) và chất lượng mã nguồn được máy móc kiểm soát tự động, thay vì chỉ dựa vào việc review bằng mắt.

## Kịch bản người dùng & Kiểm thử *(bắt buộc)*

### User Story 1 - Chặn merge khi cổng chất lượng thất bại (Priority: P1)

Là một reviewer/maintainer, khi một Pull Request (PR) không đạt cổng chất lượng SonarQube, tôi muốn việc merge PR đó vào nhánh được bảo vệ (protected branch) bị chặn hoàn toàn, không có đường vòng (override) nào, để mã không đạt chuẩn không thể lọt vào nhánh chính.

**Vì sao ưu tiên này**: Đây là giá trị cốt lõi của tính năng — nếu cổng chất lượng có thể bị bỏ qua, toàn bộ mục tiêu "máy móc kiểm soát chất lượng thay vì review bằng mắt" sẽ vô nghĩa. Không có phần này thì các phần còn lại chỉ mang tính hiển thị.

**Kiểm thử độc lập**: Mở một PR cố tình làm giảm độ phủ kiểm thử xuống dưới ngưỡng cho phép, xác nhận nút merge bị vô hiệu hóa/PR bị chặn merge trên nền tảng quản lý mã nguồn, kể cả khi thử merge với quyền quản trị (admin).

**Kịch bản chấp nhận**:

1. **Given** một PR được mở nhắm tới nhánh được bảo vệ, **When** pipeline chạy tới bước cổng chất lượng SonarQube và cổng đó thất bại, **Then** PR bị chặn merge và không có đường vòng nào để merge được.
2. **Given** một PR đang bị chặn merge do cổng chất lượng thất bại, **When** một người có quyền quản trị (admin) cố gắng merge trực tiếp, **Then** hành động đó bị từ chối bởi cấu hình bảo vệ nhánh (không tồn tại tùy chọn "merge bất chấp" cho vai trò này).

---

### User Story 2 - Xem chỉ số chất lượng ngay trên PR (Priority: P2)

Là một nhà phát triển, sau khi pipeline chạy xong, tôi muốn thấy các chỉ số chất lượng (độ phủ kiểm thử, tỷ lệ trùng lặp mã, số lượng code smell) hiển thị trực tiếp trên PR, để hiểu vì sao cổng chất lượng đạt hay không đạt mà không cần rời khỏi PR để tra cứu.

**Vì sao ưu tiên này**: Việc chặn merge (US1) chỉ có giá trị đầy đủ khi người dùng hiểu được lý do bị chặn. Nếu không thấy chỉ số, nhà phát triển sẽ mất thời gian đoán mò hoặc phải tự vào hệ thống SonarQube để tra cứu.

**Kiểm thử độc lập**: Mở một PR bất kỳ, chờ pipeline chạy xong bước cổng chất lượng, xác nhận PR hiển thị trạng thái kèm số liệu (coverage, duplication, code smells) mà không cần rời trang PR.

**Kịch bản chấp nhận**:

1. **Given** cổng chất lượng đã chạy xong (đạt hoặc không đạt), **When** tôi xem trạng thái của PR, **Then** tôi thấy các chỉ số chất lượng (độ phủ kiểm thử, tỷ lệ trùng lặp, số code smell) hiển thị ngay trên PR.

---

### User Story 3 - Cổng chất lượng tự đánh giá lại sau khi sửa (Priority: P3)

Là một nhà phát triển, sau khi tôi khắc phục vấn đề khiến cổng chất lượng thất bại và đẩy (push) commit mới, tôi muốn pipeline tự động chạy lại và đánh giá lại cổng chất lượng, để PR được mở khóa mà không cần ai can thiệp thủ công.

**Vì sao ưu tiên này**: Đây là phần hoàn thiện vòng lặp phản hồi — nếu không tự động đánh giá lại, US1 sẽ biến quy trình sửa lỗi thành một việc thủ công tốn thời gian, làm giảm giá trị của việc tự động hóa.

**Kiểm thử độc lập**: Từ một PR đang bị chặn do cổng chất lượng thất bại, sửa vấn đề gây thất bại (ví dụ tăng độ phủ kiểm thử) và push lại, xác nhận pipeline tự chạy lại toàn bộ chuỗi và PR được mở khóa mà không cần thao tác thủ công nào khác.

**Kịch bản chấp nhận**:

1. **Given** một PR đang bị chặn merge do cổng chất lượng thất bại, **When** tôi push commit mới khắc phục vấn đề, **Then** pipeline tự động chạy lại từ đầu và cổng chất lượng được đánh giá lại.
2. **Given** cổng chất lượng đánh giá lại và đạt, **When** tôi kiểm tra PR, **Then** PR không còn bị chặn merge nữa.

---

### Edge Cases

- Pipeline thất bại ở một bước trước cổng chất lượng (build, unit test, integration test, hoặc contract test) thì sao? → PR vẫn bị chặn merge, nhưng trạng thái hiển thị phải phân biệt rõ ràng "thất bại ở bước X" với "thất bại ở cổng chất lượng SonarQube", để người dùng không nhầm lẫn nguyên nhân.
- Việc phân tích SonarQube tự nó gặp lỗi hạ tầng (timeout, mất kết nối tới máy chủ SonarQube) thay vì mã không đạt chuẩn thì sao? → Được coi là cổng chất lượng chưa đạt (chưa xác nhận "pass"), PR vẫn bị chặn merge cho tới khi có kết quả phân tích thành công.
- PR ở trạng thái draft (bản nháp) có bị áp dụng cổng chặn merge không? → Pipeline vẫn chạy để nhà phát triển thấy sớm tình trạng chất lượng, nhưng việc chặn merge chỉ thực sự có ý nghĩa khi PR được chuyển sang "sẵn sàng review" và có yêu cầu merge.
- Một nhánh không nhắm tới nhánh được bảo vệ (ví dụ PR giữa hai nhánh phụ) thì có bắt buộc chạy cổng chất lượng không? → Ngoài phạm vi của tính năng này; chặn merge chỉ áp dụng cho các PR nhắm tới nhánh được bảo vệ (nhánh chính) theo mô hình trunk-based development của nền tảng.

## Yêu cầu *(bắt buộc)*

### Yêu cầu chức năng

- **FR-001**: Hệ thống PHẢI tự động khởi chạy pipeline khi một PR được mở hoặc cập nhật (push commit mới) nhắm tới nhánh được bảo vệ.
- **FR-002**: Pipeline PHẢI thực thi các bước theo đúng trình tự: build → unit test → integration test → contract test → cổng chất lượng SonarQube.
- **FR-003**: Hệ thống PHẢI chặn merge PR khi cổng chất lượng SonarQube thất bại, và không được cung cấp bất kỳ cơ chế nào (kể cả cho vai trò quản trị) để merge bất chấp kết quả đó.
- **FR-004**: Hệ thống PHẢI chặn merge PR khi bất kỳ bước nào trước cổng chất lượng (build, unit test, integration test, contract test) thất bại.
- **FR-005**: Hệ thống PHẢI hiển thị trên PR các chỉ số chất lượng mã nguồn (độ phủ kiểm thử, tỷ lệ trùng lặp mã, số lượng code smell) sau khi cổng chất lượng chạy xong.
- **FR-006**: Hệ thống PHẢI phân biệt rõ trên trạng thái PR giữa "thất bại ở bước pipeline khác" và "thất bại ở cổng chất lượng SonarQube", để người xem biết chính xác nguyên nhân bị chặn.
- **FR-007**: Hệ thống PHẢI tự động chạy lại toàn bộ chuỗi pipeline (bao gồm cổng chất lượng) mỗi khi có commit mới được push vào PR đang mở.
- **FR-008**: Hệ thống PHẢI mở khóa merge ngay khi lần chạy lại gần nhất của cổng chất lượng đạt, mà không cần thao tác thủ công.
- **FR-009**: Hệ thống PHẢI ghi nhận (log) mọi lần merge thành công, bao gồm việc xác nhận rằng cổng chất lượng đã đạt tại thời điểm merge, để phục vụ kiểm tra/audit sau này.

### Thực thể chính *(nếu tính năng có liên quan tới dữ liệu)*

- **Kết quả cổng chất lượng (Quality Gate Result)**: Trạng thái đạt/không đạt của một lần phân tích SonarQube gắn với một commit/PR cụ thể, kèm các chỉ số (coverage, duplication, code smells).
- **Trạng thái kiểm tra PR (PR Status Check)**: Trạng thái tổng hợp hiển thị trên PR cho biết bước nào của pipeline đã chạy, đã đạt hay thất bại, dùng để quyết định PR có được phép merge hay không.

## Tiêu chí thành công *(bắt buộc)*

### Kết quả đo lường được

- **SC-001**: 100% các PR nhắm tới nhánh được bảo vệ tự động kích hoạt đầy đủ chuỗi pipeline (build → unit test → integration test → contract test → cổng chất lượng) mà không cần ai kích hoạt thủ công.
- **SC-002**: 0% số PR có cổng chất lượng thất bại được merge thành công vào nhánh được bảo vệ, kể cả khi người thử merge có quyền quản trị.
- **SC-003**: Người xem PR có thể biết chỉ số chất lượng (coverage, duplication, code smell) ngay trên PR, không cần rời khỏi trang PR hoặc đăng nhập vào hệ thống SonarQube.
- **SC-004**: Sau khi khắc phục nguyên nhân khiến cổng chất lượng thất bại và push lại, PR được mở khóa merge trong đúng một lần chạy pipeline tiếp theo, không cần can thiệp thủ công.

## Giả định

- Ngưỡng cụ thể của cổng chất lượng (% coverage tối thiểu, % trùng lặp tối đa, số code smell tối đa...) được kế thừa từ cấu hình cổng chất lượng SonarQube đã có sẵn của nền tảng, không phải là nội dung cần định nghĩa lại trong tính năng này.
- "Nhánh được bảo vệ" tương ứng với nhánh chính (main/trunk) theo mô hình trunk-based development mà nền tảng đang áp dụng; các nhánh phụ khác không nằm trong phạm vi chặn merge của tính năng này.
- Nền tảng quản lý mã nguồn và hệ thống CI (Jenkins) đã được kết nối với SonarQube ở mức hạ tầng; tính năng này tập trung vào hành vi nghiệp vụ (trình tự chạy, chặn merge, hiển thị chỉ số, tự đánh giá lại) chứ không mô tả lại cấu hình hạ tầng.
- "Không có đường vòng" (no override path) được hiểu là vô hiệu hóa hoàn toàn khả năng merge bất chấp cổng chất lượng cho mọi vai trò, kể cả quản trị viên, theo đúng tiêu chí chấp nhận gốc trong Jira.
