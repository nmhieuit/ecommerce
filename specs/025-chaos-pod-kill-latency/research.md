# Research: Diễn tập chaos engineering — tiêm độ trễ

> **Cập nhật (spec 031, 2026-10-06)**: phần giết pod/Kubernetes đã gỡ; xem [spec 031](../031-error-group-catalog/spec.md) nhóm 8 (container chết).

**Feature**: [spec.md](./spec.md)

Mục tiêu của Phase 0 không phải khảo sát công nghệ chưa biết — kịch bản chaos còn lại của SCRUM-34
dùng công cụ vận hành đã có sẵn trong repo (Elastic/Kibana). Mục tiêu thật là **rà soát
xem những gì SCRUM-34 giả định đã tồn tại (circuit breaker, dashboard SLO) thực sự có
đúng như mong đợi hay không**, để plan không đặt nền trên một giả định sai. Mỗi mục dưới đây là một
phát hiện xác minh được trong mã nguồn/tài liệu hiện có, không phải một lựa chọn công nghệ.

## Quyết định 0 — (ĐÃ GỠ) Kịch bản kill-pod (US1)

**Đã gỡ ở spec 031.** Kịch bản giết pod trên Kubernetes không còn được dùng. Điều đáng giữ lại về resilience của BFF: `BasketsApiClient` đã có `AddStandardResilienceHandler()` (từ 020: `AttemptTimeout=1s`, `TotalRequestTimeout=3s`, `MaxRetryAttempts=2`, `CircuitBreaker.SamplingDuration=10s`) và telemetry Polly đã lên OTel → Elastic (từ 017/020), dùng chung cho quan sát circuit breaker/retry ở các kịch bản còn lại và ở nhóm 8 của spec 031.

## Quyết định 1 — Kịch bản tiêm độ trễ (US2) cần một cơ chế mới, tối thiểu, có thể tắt ngay không cần redeploy

**Decision**: Thêm một middleware mới `ChaosLatencyInjectionMiddleware` trong
`services/orders/src/Orders.Api/Features/Chaos/`, chỉ áp dụng cho Orders.Api. Middleware trì hoãn
response một khoảng thời gian đọc từ header HTTP `X-Chaos-Latency-Ms` của chính request đó, và CHỈ
khi cấu hình `Chaos:AllowLatencyInjection` (bool, mặc định `false`) đang bật.

**Rationale**:
- Rà soát toàn bộ repo (`grep -ril "chaos\|toxiproxy\|fault.inject\|latency.inject"`) xác nhận
  **không có công cụ fault-injection nào tồn tại từ trước** — spec.md Assumptions đã dự liệu đúng
  điều này ("công cụ fault-injection chuyên dụng hoặc chèn thủ công một khoảng sleep"). `specs/021-declare-service-slos/quickstart.md`
  Bước 4 xác nhận tiền lệ: lần trước, việc "làm chậm một endpoint" được thực hiện bằng cách **sửa
  tạm thời mã nguồn** (thêm `Task.Delay`) rồi hoàn tác — chấp nhận được cho một lần xác minh thủ
  công, nhưng SCRUM-34 rõ ràng đóng khung đây là một *bài tập lặp lại định kỳ* (User Story 3 yêu cầu
  ghi nhận từng lần chạy) — sửa mã nguồn mỗi lần chạy bài tập là không thực tế và vi phạm tinh thần
  "reversible" của Principle X. Vì vậy chọn xây một cơ chế tối thiểu, thường trực nhưng mặc định tắt,
  thay vì lặp lại cách làm thủ công một lần của 021.
- Hai lớp gate (cấu hình môi trường `AllowLatencyInjection` + header per-request) tách rõ "môi
  trường này có được phép chạy bài tập chaos không" (quyết định vận hành, chỉ bật ở môi trường diễn
  tập, không bao giờ bật ở production — spec FR-006) khỏi "ngay bây giờ có đang tiêm độ trễ không"
  (quyết định tại chỗ của người thực hiện bài tập, không cần restart container để bật/tắt — bắt đầu và
  dừng tiêm chỉ là gửi/không gửi header). Điều này thỏa Principle X ("Rolling back... MUST NOT
  require a code change or a redeploy") theo đúng tinh thần: dừng tiêm giữa chừng không cần đổi mã
  hay khởi động lại container.
- Giới hạn trên (`MaxInjectedLatencyMs = 30_000`) áp cho giá trị header, chặn một sai sót thao tác
  (gõ nhầm số 0) biến bài tập có kiểm soát thành treo vô hạn — nhất quán với chính tinh thần
  Principle VIII mà SCRUM-34 đang kiểm chứng ("unbounded waits cannot exist"), kể cả trong công cụ
  dùng để kiểm chứng nó.
- Không dùng khối `FeatureToggles` hiện có trong `appsettings.json` (dùng cho rollout tính năng
  nghiệp vụ, ví dụ `AuthorizationRequireApiScope` của 015) — cố tình tách thành khối `Chaos` riêng
  vì đây không phải một rollout có ngày gỡ bỏ (Principle X: "Each toggle MUST have a named owner and
  a removal date") mà là một công cụ vận hành thường trực, tương tự cách `//Chaos` sẽ ghi rõ lý do
  không có ngày gỡ (xem Constitution Check trong plan.md, mục X).

**Alternatives considered**:
- *Toxiproxy/công cụ fault-injection chuyên dụng*: bị loại vì cần thêm một thành phần hạ tầng mới
  (container proxy) chỉ để phục vụ một kịch bản; middleware trong tiến trình đã đủ để tạo độ trễ
  quan sát được, đúng tinh thần "công cụ tối thiểu" mà spec.md Assumptions đã chấp nhận.
- *Sửa mã nguồn tạm thời mỗi lần chạy (như 021 đã làm)*: bị loại vì SCRUM-34 đóng khung đây là bài
  tập lặp lại định kỳ có ghi nhận từng lần (User Story 3) — sửa/hoàn tác mã nguồn qua git mỗi lần
  chạy không phù hợp với một hoạt động vận hành lặp lại, và không tương thích với Test-First
  (Principle III) vì "mã nguồn" đó không tồn tại lâu đủ để có một test bảo vệ nó.
- *Bật tiêm độ trễ qua biến môi trường thay vì header*: bị loại vì đổi biến môi trường trên
  container (đổi biến môi trường của compose) buộc tạo lại container — chính là hành vi "redeploy"
  mà Principle X muốn tránh cho việc bật/tắt; header per-request tránh hoàn toàn việc đó.

## Quyết định 2 — Dashboard SLO đã có (021) tái sử dụng nguyên trạng cho việc quan sát ngân sách bị tiêu hao

**Decision**: User Story 2 dùng lại dashboard Kibana "Xử lý sự cố — 7 service"
(`docs/kibana-quan-sat-he-thong/06-dashboard-xu-ly-su-co-va-ngan-sach-tuan.md`,
`dashboards/xu-ly-su-co.ndjson`) đã được 021-declare-service-slos dựng và xác minh —
không xây dashboard mới.

**Rationale**: Dashboard này đã hiển thị latency p95/p99 thực tế đối chiếu ngưỡng khai báo của
`orders` từ dữ liệu OTel thật, và `specs/021-declare-service-slos/quickstart.md` Bước 4 đã tự chứng
minh nó phản ứng đúng khi một endpoint bị làm chậm (dù bằng cách khác — sửa mã tạm thời). Tiêm độ
trễ qua middleware mới (Quyết định 1) tạo ra đúng loại dữ liệu OTel mà dashboard này đã tiêu thụ —
không có gì để xây thêm.

**Alternatives considered**: Thêm panel riêng cho "chaos exercise đang chạy" trên dashboard — bị
loại vì làm phình to một dashboard vận hành hằng ngày bằng một trạng thái chỉ đúng vài phút mỗi
quý; đủ dùng cửa sổ thời gian (time range) hẹp quanh lúc chạy bài tập trên dashboard đã có.

## Quyết định 3 — Bản ghi kết quả (US3) là tài liệu markdown theo mẫu, không phải tích hợp Jira

**Decision**: Thêm một thư mục tài liệu mới `docs/dien-tap-chaos-engineering/` chứa: (1) một runbook
mô tả cách chạy từng kịch bản (tham chiếu `quickstart.md` của tính năng để tránh trùng lặp), (2) một
mẫu (`mau-ket-qua.md`) cho bản ghi kết quả mỗi lần chạy, và (3) một `README.md` liệt kê (index) các
lần chạy trước đó kèm liên kết.

**Rationale**: Repo không có tích hợp lập trình nào với Jira (không thư viện, không secret, không
job CI nào gọi Jira API) — spec FR-008 chỉ yêu cầu bug ticket "được tạo ra" và "liên kết" tới bản ghi
kết quả khi phát hiện sai lệch, không yêu cầu tự động hoá việc tạo ticket. Tạo ticket Jira thủ công
và dán liên kết vào bản ghi kết quả (trường `jira_ticket` trong mẫu) thỏa đúng FR-008 mà không cần
xây tích hợp mới ngoài phạm vi 9 yêu cầu chức năng. Đặt tại `docs/` (không phải `specs/025.../`) vì
đây là artifact vận hành sống, tái sử dụng qua nhiều lần chạy về sau — cùng lý do
`docs/kibana-quan-sat-he-thong/` (không phải `specs/017.../`) là nơi 017 đặt tài liệu vận hành của
nó.

**Alternatives considered**: Lưu bản ghi kết quả trong `specs/025-chaos-pod-kill-latency/` — bị loại
vì thư mục `specs/[feature]/` theo quy ước của repo là hồ sơ *thiết kế một lần* của tính năng
(spec/plan/tasks), không phải nơi tích luỹ dữ liệu vận hành phát sinh mỗi lần bài tập được chạy lại
sau này.

## Quyết định 4 — Không cần dự án test mới; một unit test bổ sung vào `Orders.Api.UnitTests` đã có

**Decision**: Viết `ChaosLatencyInjectionMiddlewareTests` trong dự án
`services/orders/tests/Orders.Api.UnitTests` đã tồn tại, KHÔNG tạo dự án test mới.

**Rationale**: Middleware không có phụ thuộc ngoài (không DB, không broker) nên không cần
Testcontainers/integration test theo Principle III — một unit test thuần (gọi middleware trực tiếp
với `RequestDelegate` giả, đo thời gian bằng `TimeProvider` giả lập thay vì `Task.Delay` thật để test
chạy nhanh và tất định) là đủ để bảo vệ 4 bất biến: mặc định tắt không có gì thay đổi; bật cấu hình
nhưng không có header thì không có gì thay đổi; bật cấu hình + có header hợp lệ thì trì hoãn đúng
khoảng đã yêu cầu; header vượt trần bị chặn ở trần an toàn. Đây đúng khuôn mẫu
`RetryMethodPolicyTests` mà 020 đã dùng cho một mối lo tương tự (unit test thuần cho một quy tắc cấu
hình, không cần hạ tầng thật).

**Alternatives considered**: Test tích hợp qua `WebApplicationFactory<Program>` thật — cân nhắc
nhưng không bắt buộc cho Phase 1 vì middleware không phụ thuộc pipeline xác thực/tenant phía sau nó
(đặt trước `UseIdentityValidation` — xem data-model.md); để lại như một lựa chọn mở rộng ở tasks.md
nếu cần xác nhận vị trí middleware trong pipeline thật.

---

Không còn `[NEEDS CLARIFICATION]` nào từ Technical Context của plan.md — cả 4 quyết định trên đều
dựa trên xác minh trực tiếp trong mã nguồn/tài liệu hiện có của repo, không phải giả định.
