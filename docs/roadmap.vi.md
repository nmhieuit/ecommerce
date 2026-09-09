# Ecommerce SDLC Practice Platform — Lộ trình sản phẩm (Product Roadmap)

*(Bản dịch tiếng Việt của [`roadmap.md`](roadmap.md) — bản gốc tiếng Anh vẫn được giữ nguyên, không
sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn đúng.)*

**Mục đích**: 1 bài tập cho người vận hành đơn lẻ (solo-operator), nơi 1 người luân phiên đóng vai
Product Owner, Developer, QA, DevOps, và SRE để thực hành toàn bộ SDLC từ đầu tới cuối, xây dựng trên
kiến trúc được định nghĩa trong [constitution.md](../.specify/memory/constitution.md) (v1.0.0).

**Chiến lược**: lát mỏng trước (thin slice first). 1 luồng sản phẩm duy nhất — **duyệt → giỏ hàng →
checkout → đơn hàng** — được đẩy qua 1 "walking skeleton" đầy đủ, sau đó được bọc thêm 1 kỷ luật SDLC
mới ở mỗi giai đoạn, thay vì xây cả 6 service song song. 1 luồng thứ 2 (logistics/hoá đơn) được cố
tình gác lại cho tới khi mẫu hình này được chứng minh 1 lần.

---

## Giai đoạn 1 — Walking Skeleton
**Trọng tâm vai trò**: Product Owner + Developer
**Mục tiêu**: phiên bản xấu nhất có thể của luồng, đã triển khai và demo được. Chưa có coverage test
có ý nghĩa hay hardening — giai đoạn này chứng minh đường đi tồn tại.

Epic: [`SCRUM-5`](https://nmhieuit.atlassian.net/browse/SCRUM-5) — Walking Skeleton: Duyệt → Giỏ hàng
→ Checkout → Đơn hàng

Story:
- [`SCRUM-10`](https://nmhieuit.atlassian.net/browse/SCRUM-10) Viết 1 trang tóm tắt phạm vi lát mỏng: 1 sản phẩm, 1 giỏ hàng, 1 đơn hàng, 1 tenant (PO)
- [`SCRUM-11`](https://nmhieuit.atlassian.net/browse/SCRUM-11) Dựng khung service `parties`, `products`, `baskets`, `orders` dùng cấu trúc vertical-slice
- [`SCRUM-12`](https://nmhieuit.atlassian.net/browse/SCRUM-12) Stub identity: 1 user giả duy nhất, nhưng **resolve 1 tenant context thật** (đóng cứng vào 1 tenant) để không có đường code nào từng chạm vào persistence mà thiếu nó — điều này cố tình không bị hoãn tới Giai đoạn 3
- [`SCRUM-13`](https://nmhieuit.atlassian.net/browse/SCRUM-13) Nối routing gateway → BFF cho 3 service
- [`SCRUM-14`](https://nmhieuit.atlassian.net/browse/SCRUM-14) React SPA tối thiểu: danh sách sản phẩm, thêm-vào-giỏ, nút checkout, xác nhận đơn hàng
- [`SCRUM-15`](https://nmhieuit.atlassian.net/browse/SCRUM-15) Cho cả bộ khung chạy được cục bộ bằng 1 lệnh, gồm cả container
- [`SCRUM-16`](https://nmhieuit.atlassian.net/browse/SCRUM-16) Demo: đặt 1 đơn hàng từ đầu tới cuối

---

## Giai đoạn 2 — Kỷ luật hợp đồng & Test
**Trọng tâm vai trò**: Developer + QA
**Mục tiêu**: bổ sung lại các nguyên tắc mà bộ khung đã bỏ qua — hợp đồng trước code, red-green-refactor, integration test thật.

Epic: [`SCRUM-6`](https://nmhieuit.atlassian.net/browse/SCRUM-6) — Bổ sung Hợp đồng và Test-First

Story:
- [`SCRUM-17`](https://nmhieuit.atlassian.net/browse/SCRUM-17) Viết spec OpenAPI cho các route BFF bao phủ products/baskets/orders; sinh client code từ chúng
- [`SCRUM-18`](https://nmhieuit.atlassian.net/browse/SCRUM-18) Định nghĩa schema event (`OrderPlaced`, `BasketCheckedOut`) tại 1 vị trí contract dùng chung, có version
- [`SCRUM-19`](https://nmhieuit.atlassian.net/browse/SCRUM-19) Bổ sung TDD cho logic tính giá giỏ hàng và tạo đơn hàng: test thất bại trước, rồi mới tới cài đặt
- [`SCRUM-20`](https://nmhieuit.atlassian.net/browse/SCRUM-20) Thêm integration test trên dependency thật qua Testcontainers (SQL Server, Redis, RabbitMQ)
- [`SCRUM-21`](https://nmhieuit.atlassian.net/browse/SCRUM-21) Thêm contract test do bên tiêu thụ dẫn dắt qua mỗi ranh giới BFF/service
- [`SCRUM-22`](https://nmhieuit.atlassian.net/browse/SCRUM-22) Nối cổng chất lượng SonarQube vào build và biến nó thành 1 điểm chặn merge

---

## Giai đoạn 3 — Bảo mật & Quan sát được
**Trọng tâm vai trò**: DevOps
**Mục tiêu**: thay các stub bằng tư thế bảo mật và khả năng quan sát (observability) thật mà constitution yêu cầu.

Epic: [`SCRUM-7`](https://nmhieuit.atlassian.net/browse/SCRUM-7) — Auth thật, Deny-by-Default, Observability đầy đủ

Story:
- [`SCRUM-23`](https://nmhieuit.atlassian.net/browse/SCRUM-23) Dựng máy chủ định danh; thay user giả của Giai đoạn 1 bằng việc phát hành token thật
- [`SCRUM-24`](https://nmhieuit.atlassian.net/browse/SCRUM-24) Thêm chính sách phân quyền deny-by-default cho mọi endpoint và message handler
- [`SCRUM-25`](https://nmhieuit.atlassian.net/browse/SCRUM-25) Phát trace/metric/log OpenTelemetry qua thành phần ServiceDefaults dùng chung tới Elastic
- [`SCRUM-26`](https://nmhieuit.atlassian.net/browse/SCRUM-26) Lan truyền 1 correlation ID từ edge qua mọi service, message, và frontend
- [`SCRUM-27`](https://nmhieuit.atlassian.net/browse/SCRUM-27) Chuyển mọi secret sang kho secret của cluster; gỡ bỏ mọi thứ đóng cứng hoặc nướng sẵn vào image
- [`SCRUM-28`](https://nmhieuit.atlassian.net/browse/SCRUM-28) Thêm liveness/readiness probe cho mọi service

---

## Giai đoạn 4 — Khả năng chịu lỗi & Hiệu năng
**Trọng tâm vai trò**: SRE
**Mục tiêu**: biến ngân sách hiệu năng của constitution từ mong muốn thành thứ được đo lường và thực thi.

Epic: [`SCRUM-8`](https://nmhieuit.atlassian.net/browse/SCRUM-8) — Ngân sách, Timeout, và Tiêm lỗi (Failure Injection)

Story:
- [`SCRUM-29`](https://nmhieuit.atlassian.net/browse/SCRUM-29) Khai báo SLO (độ trễ, tỷ lệ lỗi, độ sẵn sàng) theo từng service trong manifest của nó
- [`SCRUM-30`](https://nmhieuit.atlassian.net/browse/SCRUM-30) Thêm timeout tường minh cùng chính sách retry/circuit-breaker cho mọi lời gọi ra ngoài qua `Microsoft.Extensions.Resilience`
- [`SCRUM-31`](https://nmhieuit.atlassian.net/browse/SCRUM-31) Verify mẫu hình transactional outbox trên publisher đơn hàng — kill tiến trình giữa lúc publish và xác nhận không có sai lệch
- [`SCRUM-32`](https://nmhieuit.atlassian.net/browse/SCRUM-32) Chạy load/performance test đối chiếu ngân sách đã nêu của constitution (p95/p99 theo từng lớp endpoint)
- [`SCRUM-33`](https://nmhieuit.atlassian.net/browse/SCRUM-33) Rà soát các query không giới hạn, thiếu phân trang, và mẫu hình truy cập N+1
- [`SCRUM-34`](https://nmhieuit.atlassian.net/browse/SCRUM-34) Bài tập chaos: kill 1 pod hoặc tiêm độ trễ và xác nhận circuit breaker cùng dashboard hành xử như kỳ vọng

---

## Giai đoạn 5 — Vận hành
**Trọng tâm vai trò**: SRE
**Mục tiêu**: thực hành các phần của SRE chỉ xuất hiện khi có gì đó đã lên production — sự cố, ngân sách lỗi, rollback an toàn.

Epic: [`SCRUM-9`](https://nmhieuit.atlassian.net/browse/SCRUM-9) — Ứng phó sự cố và Chính sách ngân sách lỗi

Story:
- [`SCRUM-35`](https://nmhieuit.atlassian.net/browse/SCRUM-35) Định nghĩa chính sách ngân sách lỗi và ngưỡng cảnh báo gắn với SLO của Giai đoạn 4
- [`SCRUM-36`](https://nmhieuit.atlassian.net/browse/SCRUM-36) Cố ý kích hoạt 1 sự cố (tiêm 1 lỗi hoặc gây gián đoạn) và chạy 1 lượt ứng phó on-call thật
- [`SCRUM-37`](https://nmhieuit.atlassian.net/browse/SCRUM-37) Viết 1 bản postmortem không quy trách nhiệm và tạo các ticket theo dõi kết quả
- [`SCRUM-38`](https://nmhieuit.atlassian.net/browse/SCRUM-38) Thực hành triển khai qua toggle: ra mắt 1 thay đổi sau 1 feature flag, rồi tắt nó mà không cần redeploy
- [`SCRUM-39`](https://nmhieuit.atlassian.net/browse/SCRUM-39) Chạy 1 lượt review tuân thủ hiến pháp (constitutional compliance) kiểu hàng quý trên hệ thống đang sống

---

## Sau này / Gác lại
- Luồng thứ 2: logistics + hoá đơn (đường đi sau-khi-đặt-hàng)
- UI đa tenant thật sự và test cô lập tenant — Giai đoạn 1 stub 1 tenant đã resolve duy nhất; việc này mở rộng thành chuyển-đổi-tenant thật và verify cô lập, không phải bổ sung lại chính khái niệm đó
- Sự tương đồng (parity) của client mobile-web
- Tách gói (extraction) package design-system dùng chung
- Backlog ADR rộng hơn cho các quyết định có ý nghĩa kiến trúc

---

## Ghi chú ánh xạ Jira
- Mỗi giai đoạn → 1 **Epic**.
- Mỗi gạch đầu dòng → 1 **Story** (các gạch đầu dòng chỉ-hạ-tầng có thể là **Task** thay thế, tuỳ PM quyết định).
- Nhãn (label) gợi ý: `role:po`, `role:dev`, `role:qa`, `role:devops`, `role:sre`, `phase-1`…`phase-5`.
- Component gợi ý: `parties`, `products`, `baskets`, `orders`, `gateway`, `bff`, `web`.
- Trình tự lỏng lẻo, không bị gate chặt: các story ở Giai đoạn *N* thường giả định epic của Giai đoạn *N-1* đang "chạy được (walking)", không phải "đã xong (done)". Các đội thật không đóng hoàn toàn 1 epic trước khi bắt đầu cái tiếp theo; bài tập này cũng vậy.

## Giả định rủi ro nhất
Việc stub resolve tenant thành 1 tenant đóng cứng duy nhất ở Giai đoạn 1 sẽ không gây phải làm lại sau
này. Constitution coi cô lập tenant là 1 ranh giới bảo mật (Principle V), nên biện pháp giảm thiểu
mang tính kiến trúc, không phải lịch trình: Giai đoạn 1 vẫn resolve 1 tenant context thật từ đầu tới
cuối, chỉ là từ 1 nguồn đóng cứng — Giai đoạn 3/Sau này nâng cấp *tenant tới từ đâu*, không phải *có
được resolve hay không*.

## Trạng thái Jira
Cả 5 epic và 30 story đều đã được tạo trong project **SCRUM** (`Product MVPs`) tại
[nmhieuit.atlassian.net](https://nmhieuit.atlassian.net/jira/software/projects/SCRUM/boards). Mỗi
story mang tiêu chí chấp nhận (Given/When/Then) và kịch bản test trong phần mô tả, cùng nhãn `role-*`
và `phase-*` để lọc.

## Bước tiếp theo
Làm Giai đoạn 1 (`SCRUM-5`) trước — mọi thứ khác trên board đều giả định nó đang "chạy được" trước
khi được lấy lên làm.
