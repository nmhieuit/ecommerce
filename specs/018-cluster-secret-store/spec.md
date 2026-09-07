# Feature Specification: Secrets qua Cluster Secret Store, loại bỏ cấu hình hardcode

**Feature Branch**: `018-cluster-secret-store`

**Created**: 2026-09-06

**Status**: Draft

**Input**: User description: "Jira SCRUM-27 [SECURE-3] Secrets via cluster secret store, remove hardcoded config — As DevOps, I want all secrets sourced from the cluster secret store at runtime so that nothing sensitive lives in source, config files, or images (Principle VI). Acceptance Criteria: (1) Given any service configuration, when I inspect it, then no connection strings, keys, or tokens are hardcoded in source, appsettings files, or Dockerfiles. (2) Given a service starts in the cluster, when it needs a secret, then it's injected at runtime from the cluster secret store. (3) Given I scan git history, when I search for credential patterns, then none are found committed anywhere. Test Scenarios: 1. Run a secret-scanning tool (e.g., gitleaks) across the repo history — expect zero findings. 2. Inspect a built container image's filesystem — confirm no secrets are baked in. 3. Rotate a secret in the cluster store without redeploying — confirm the service picks it up per the platform's runtime-injection model."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Loại bỏ secret hardcode khỏi source, config và image (Priority: P1)

Là một kỹ sư DevOps, tôi muốn rà soát và loại bỏ toàn bộ chuỗi kết nối, khóa API, token đang bị hardcode trong mã nguồn, file `appsettings*.json`, và Dockerfile của từng service, để không có thông tin nhạy cảm nào tồn tại trong repository hoặc trong image đã build.

**Why this priority**: Đây là tiền đề bắt buộc — nếu secret vẫn còn hardcode ở bất kỳ đâu thì việc inject secret tại runtime (User Story 2) không còn ý nghĩa bảo mật. Đây cũng là điều kiện để thỏa Principle VI (Secure by Default) của constitution.

**Independent Test**: Chạy công cụ secret-scanning (vd: gitleaks) trên toàn bộ working tree và lịch sử git của repository — kết quả phải trả về zero findings đối với các pattern connection string, API key, token, mật khẩu.

**Acceptance Scenarios**:

1. **Given** bất kỳ file cấu hình nào của một service (source code, `appsettings.json`, `appsettings.*.json`, Dockerfile), **When** tôi kiểm tra nội dung file đó, **Then** không có connection string, khóa, hay token nào được hardcode trong đó.
2. **Given** công cụ secret-scanning được chạy trên toàn bộ lịch sử git của repository, **When** quá trình quét hoàn tất, **Then** không có credential pattern nào được phát hiện ở bất kỳ commit nào.
3. **Given** một secret trước đây từng bị commit vào lịch sử git, **When** secret đó được phát hiện, **Then** secret đó phải được xoay vòng (rotate) ngay và được coi là đã lộ (compromised), bất kể có xóa được khỏi lịch sử hay không.

---

### User Story 2 - Inject secret vào service tại thời điểm chạy trong cluster (Priority: P1)

Là một kỹ sư DevOps, tôi muốn mỗi service khi khởi động trong cluster sẽ nhận secret của nó (connection string, khóa ký JWT, credential message broker, v.v.) được cấp phát từ cluster secret store tại thời điểm chạy, để service hoạt động đúng chức năng mà không cần bất kỳ giá trị nhạy cảm nào được đóng gói sẵn trong image hay commit trong repository.

**Why this priority**: Đây là hành vi runtime cốt lõi mà toàn bộ tính năng hướng tới — không có cơ chế inject secret hoạt động được thì User Story 1 chỉ dừng lại ở việc dọn dẹp, không giải quyết được bài toán vận hành thực tế của service.

**Independent Test**: Triển khai một service vào môi trường cluster thử nghiệm (không có bất kỳ secret nào baked-in image hoặc trong file cấu hình đã commit) và xác nhận service khởi động thành công, kết nối được tới dependency của nó (database, message broker, v.v.) bằng secret nhận được từ cluster secret store.

**Acceptance Scenarios**:

1. **Given** một service được triển khai vào cluster mà không có secret nào đóng gói sẵn trong image hay trong file cấu hình đã commit, **When** service khởi động và cần một secret để kết nối tới dependency của nó, **Then** secret đó được cấp cho service tại thời điểm chạy từ cluster secret store, và service khởi động thành công.
2. **Given** một service đang chạy trong cluster, **When** secret store tạm thời không thể cấp secret khi service đang khởi động, **Then** service phải thất bại theo cách rõ ràng, có thể quan sát được (log/lỗi khởi động), thay vì khởi động ở trạng thái không xác định với dependency bị lỗi ngầm.
3. **Given** filesystem của một container image đã build, **When** filesystem đó được kiểm tra, **Then** không tìm thấy secret nào được baked vào bất kỳ layer nào của image.

---

### User Story 3 - Xoay vòng secret mà không cần redeploy (Priority: P2)

Là một kỹ sư DevOps, tôi muốn có thể xoay vòng (rotate) một secret trong cluster secret store mà không cần redeploy service liên quan, để việc thay đổi credential định kỳ hoặc sau sự cố bảo mật không làm gián đoạn dịch vụ và không đòi hỏi một chu trình triển khai đầy đủ.

**Why this priority**: Đây là năng lực vận hành nâng cao, phụ thuộc vào việc User Story 1 và 2 đã hoàn thành. Nó nâng tư thế bảo mật từ "secret không hardcode" lên "secret có thể xoay vòng an toàn", nhưng bản thân tính năng cốt lõi (loại bỏ hardcode, inject runtime) vẫn có giá trị độc lập nếu chưa có rotation.

**Independent Test**: Xoay vòng giá trị của một secret đang tồn tại trong cluster secret store, không thực hiện redeploy service tương ứng, và xác nhận service lấy được (hoặc tự áp dụng) giá trị mới theo đúng mô hình runtime-injection của nền tảng, trong một khoảng thời gian được xác định trước.

**Acceptance Scenarios**:

1. **Given** một service đang chạy và sử dụng một secret hiện có, **When** giá trị của secret đó được xoay vòng trong cluster secret store mà không redeploy service, **Then** service nhận và áp dụng được giá trị mới theo mô hình runtime-injection của nền tảng, trong thời gian tối đa được quy định (xem SC-004).
2. **Given** một secret vừa được xoay vòng, **When** giá trị cũ hết hiệu lực, **Then** mọi kết nối mới từ service tới dependency của nó sử dụng giá trị mới; không có gián đoạn dịch vụ (service outage) do quá trình xoay vòng gây ra.

---

### Edge Cases

- Điều gì xảy ra khi một secret được service yêu cầu không tồn tại trong cluster secret store (bị xóa nhầm, đặt sai tên, hoặc chưa được provision)?
- Service phản ứng thế nào khi cluster secret store tạm thời không khả dụng (network timeout, secret store đang bảo trì) tại thời điểm service khởi động, và khi service đang chạy?
- Điều gì xảy ra khi giá trị secret được cấp có định dạng không hợp lệ (vd: connection string sai cú pháp, khóa ký JWT sai độ dài)?
- Làm sao phát hiện và xử lý trường hợp một secret bị hardcode "trá hình" — vd: giá trị được base64-encode, hoặc đặt trong biến môi trường mặc định ngay trong Dockerfile thay vì được inject?
- Điều gì xảy ra với secret đã từng bị commit vào lịch sử git trước khi tính năng này được triển khai — chúng có được coi là đã lộ (compromised) và bắt buộc phải rotate hay không?
- Pipeline build image vô tình cache hoặc ghi log ra giá trị secret trong quá trình build (build log, layer cache) thì được phát hiện và ngăn chặn như thế nào?
- Nhiều service dùng chung một loại secret (vd: cùng kết nối một RabbitMQ instance) — việc xoay vòng secret đó có cần đồng bộ giữa các service hay có thể xoay độc lập từng service?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Hệ thống PHẢI không chứa bất kỳ connection string, khóa API, hay token nào được hardcode trong mã nguồn, file `appsettings.json`/`appsettings.*.json`, hoặc Dockerfile của bất kỳ service nào trong repository.
- **FR-002**: Mỗi service PHẢI nhận secret cần thiết để hoạt động (connection string database, credential message broker, khóa ký JWT, v.v.) được cấp phát tại thời điểm chạy từ cluster secret store khi service khởi động trong cluster, thay vì đọc từ giá trị tĩnh đã commit.
- **FR-003**: Hệ thống PHẢI hỗ trợ xoay vòng (rotate) một secret trong cluster secret store mà không yêu cầu redeploy service sử dụng secret đó; service liên quan PHẢI áp dụng được giá trị mới theo mô hình runtime-injection của nền tảng trong khoảng thời gian tối đa quy định tại SC-004.
- **FR-004**: Toàn bộ lịch sử git của repository (tất cả commit, không chỉ trạng thái hiện tại) PHẢI được quét bằng công cụ secret-scanning và không được phát hiện bất kỳ credential pattern nào (connection string, API key, token, mật khẩu).
- **FR-005**: Filesystem của bất kỳ container image nào được build từ repository PHẢI không chứa secret nào ở bất kỳ layer nào, kể cả trong build cache hoặc build history của image.
- **FR-006**: Pipeline CI/CD PHẢI tự động chạy secret-scanning trên mỗi lần build và PHẢI chặn merge/build khi phát hiện có secret bị hardcode, theo đúng nguyên tắc PR gate không có ngoại lệ của constitution.
- **FR-007**: Khi một service không nhận được secret bắt buộc (secret không tồn tại, hoặc secret store không khả dụng) tại thời điểm khởi động, service đó PHẢI dừng khởi động (fail-fast) và ghi log lỗi rõ ràng, thay vì khởi động ở trạng thái không xác định hoặc silently bỏ qua dependency bị thiếu.
- **FR-008**: Bất kỳ secret nào từng được phát hiện đã tồn tại trong lịch sử git trước khi tính năng này được triển khai PHẢI được coi là đã lộ (compromised) và PHẢI được xoay vòng, bất kể việc secret đó có được xóa khỏi lịch sử git hay không.
- **FR-009**: Cấu hình local development (docker-compose, `.env` không commit) được miễn trừ khỏi yêu cầu inject-từ-cluster-secret-store, nhưng PHẢI tiếp tục không chứa secret nào được commit vào repository dưới dạng giá trị mặc định thật.

### Key Entities

- **Secret**: Một giá trị nhạy cảm (connection string, khóa ký, credential, token) mà một service cần để hoạt động; có tên định danh, service sở hữu/tiêu thụ, và trạng thái hiệu lực (đang dùng, đã xoay vòng, đã thu hồi).
- **Cluster Secret Store**: Nguồn lưu trữ và cấp phát secret tại runtime cho các service chạy trong cluster; tách biệt hoàn toàn khỏi source code và container image.
- **Service Configuration**: Tập hợp cấu hình của một service (file `appsettings*.json`, biến môi trường, Dockerfile) sau khi loại bỏ mọi giá trị secret hardcode, chỉ còn tham chiếu đến secret cần inject chứ không chứa giá trị thật.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Kết quả quét secret-scanning (vd: gitleaks) trên toàn bộ lịch sử git của repository trả về 0 phát hiện (zero findings) liên quan đến connection string, API key, token, hay mật khẩu.
- **SC-002**: 100% container image được build từ các service trong repository không chứa secret nào có thể phát hiện được khi kiểm tra filesystem của image.
- **SC-003**: 100% service khởi động thành công trong môi trường cluster mà không có bất kỳ secret nào đóng gói sẵn trong image hoặc trong file cấu hình đã commit.
- **SC-004**: Sau khi một secret được xoay vòng trong cluster secret store, service liên quan áp dụng được giá trị mới trong vòng tối đa 5 phút mà không cần redeploy, và không xảy ra gián đoạn dịch vụ (service outage) trong suốt quá trình đó.
- **SC-005**: Pipeline CI/CD chặn 100% các lần build/PR có chứa secret hardcode được phát hiện qua secret-scanning tự động, không có trường hợp ngoại lệ được bỏ qua.

## Assumptions

- Cơ chế cấp phát secret cho cluster đã được quyết định trước tại `docs/adr/0007-secrets-delivery.md` (Accepted): External Secrets Operator (ESO) đồng bộ từ một Vault tự vận hành (self-hosted HashiCorp Vault) làm nguồn secret gốc; tính năng này triển khai theo hướng đó thay vì đề xuất lại cơ chế mới.
- Phạm vi của tính năng bao gồm toàn bộ service backend hiện có trong repository (parties, products, baskets, orders, logistics, invoices, identity) cũng như các thành phần dùng chung (shared components) có tham chiếu tới secret.
- Cấu hình cho môi trường local development (docker-compose, file `.env` không commit, giá trị placeholder yếu như `guest/guest` cho RabbitMQ local) không thuộc phạm vi bắt buộc "inject từ cluster secret store" vì không chạy trong cluster; các giá trị này vẫn phải tuân thủ nguyên tắc không commit secret thật vào repository.
- "Secret" trong tính năng này bao gồm: connection string database, credential message broker (RabbitMQ), credential cache (Redis nếu có xác thực), khóa ký/xác thực JWT của identity server, và bất kỳ API key/token nào dùng để gọi dịch vụ bên ngoài.
- Việc xóa secret đã lộ khỏi lịch sử git (git history rewrite) là một hành động rủi ro cao đối với một repository đang hoạt động; do đó tính năng này ưu tiên xoay vòng (rotate) secret đã lộ hơn là bắt buộc phải viết lại lịch sử git — việc viết lại lịch sử (nếu cần) được coi là quyết định vận hành riêng, nằm ngoài phạm vi bắt buộc của tính năng.
- (Bổ sung khi implement, xem research.md Decision 6) SC-001/FR-004 "0 phát hiện trên toàn bộ lịch sử git" được đo bằng gitleaks kèm cơ chế **baseline** (`--baseline-path .gitleaks-baseline.json`): các phát hiện lịch sử đã biết, đã xác nhận, từ trước khi tính năng này tồn tại được chốt lại một lần (đúng 9 phát hiện, xem file baseline) và loại trừ khỏi kết quả; bất kỳ phát hiện mới nào không có trong baseline vẫn chặn CI. Đây không phải allowlist hoá theo mẫu chung — chỉ loại trừ đúng các fingerprint (commit + file + dòng) đã biết.
- Cơ sở hạ tầng để vận hành cluster secret store (triển khai Vault có HA, cài đặt ESO) là một phần công việc hạ tầng nền tảng đã được ghi nhận trong ADR-0007 nhưng chưa được xây dựng; tính năng này giả định hạ tầng đó sẽ được chuẩn bị song song hoặc là điều kiện tiên quyết triển khai, không phải một hạng mục cần đặc tả lại từ đầu trong tài liệu này.
