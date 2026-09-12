# Research: Diễn tập chaos engineering — giết pod / tiêm độ trễ

**Feature**: [spec.md](./spec.md)

Mục tiêu của Phase 0 không phải khảo sát công nghệ chưa biết — cả hai kịch bản chaos của SCRUM-34
đều dùng công cụ vận hành đã có sẵn trong repo (kubectl, Elastic/Kibana). Mục tiêu thật là **rà soát
xem những gì SCRUM-34 giả định đã tồn tại (circuit breaker, dashboard SLO, cluster K8s) thực sự có
đúng như mong đợi hay không**, để plan không đặt nền trên một giả định sai. Mỗi mục dưới đây là một
phát hiện xác minh được trong mã nguồn/tài liệu hiện có, không phải một lựa chọn công nghệ.

## Quyết định 0 — Kịch bản kill-pod (US1) không cần thêm mã ứng dụng nào

**Decision**: User Story 1 hiện thực hoàn toàn bằng lệnh vận hành (`kubectl delete pod`) + quan sát
hạ tầng/telemetry đã có. Không sửa `services/baskets`, không sửa BFF.

**Rationale (xác minh được)**:
- `deploy/ansible/roles/service_deployment/templates/deployment.yaml.j2` không khai báo trường
  `replicas` cho bất kỳ service nào → Kubernetes mặc định 1 replica cho `baskets` (và mọi service
  khác) hiện nay. Đây là một phát hiện thật, không phải giả định: nghĩa là "K8s reschedules it" ở
  Acceptance Criteria 1 của SCRUM-34 là **cold-start lại từ đầu**, không phải failover sang một pod
  dự phòng đang chạy sẵn — trong khoảng pod cũ đã chết và pod mới chưa `READY`, basket service có
  một cửa sổ gián đoạn thật. Đây chính xác là điều User Story 1 muốn quan sát (không che giấu, không
  "sửa" thành đa replica — làm vậy sẽ đổi phạm vi từ "diễn tập chaos" sang "xây high-availability",
  ngoài phạm vi 9 yêu cầu chức năng của spec.md).
- `services/bff/src/Bff.Api/DownstreamClients/DownstreamClientRegistrationExtensions.cs` (từ
  020-timeouts-retry-circuit-breaker) đã đăng ký `BasketsApiClient` với `AddStandardResilienceHandler()`:
  `AttemptTimeout=1s`, `TotalRequestTimeout=3s`, `MaxRetryAttempts=2` (delay 200ms, chỉ áp dụng cho
  method an toàn GET/HEAD), `CircuitBreaker.SamplingDuration=10s`. Đây là ngân sách có sẵn mà FR-004
  của spec.md ("circuit breaker/retry engage") tham chiếu tới — không cần thêm cấu hình mới.
- `shared/ServiceDefaults/ServiceDefaultsExtensions.cs` đã có `.AddSource("Polly")` và
  `.AddMeter("Polly")` (từ 020) — mọi lần thử, retry, và đổi trạng thái circuit breaker của
  `BasketsApiClient` đã phát ra telemetry theo đúng activity source "Polly", chảy qua pipeline OTel
  → Elastic có sẵn từ 017-otel-servicedefaults-elastic. `specs/020-timeouts-retry-circuit-breaker/quickstart.md`
  Bước 6 đã xác nhận cách quan sát: log có cấu trúc gắn nguồn `"Polly"` (qua Elastic nếu OTel
  Collector đang chạy, hoặc `dotnet-counters monitor --process-id <pid> Polly` khi chạy cục bộ không
  có Elastic) — tái sử dụng đúng kỹ thuật này cho User Story 1 thay vì phát minh một cách quan sát
  mới.

**Alternatives considered**: Xây một dashboard Kibana riêng cho "trạng thái circuit breaker" (tương
tự dashboard SLO của 021) — bị loại vì SCRUM-34 chỉ yêu cầu circuit breaker "quan sát được", không
yêu cầu một dashboard thường trực; 020 đã chứng minh log/telemetry có cấu trúc là đủ để quan sát
trong lúc diễn tập. Thêm một dashboard mới cho một tính năng chỉ chạy vài lần một quý là phức tạp
không tương xứng (constitution Principle I tinh thần "đơn giản tương xứng với miền").

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
  (quyết định tại chỗ của người thực hiện bài tập, không cần restart pod để bật/tắt — bắt đầu và
  dừng tiêm chỉ là gửi/không gửi header). Điều này thỏa Principle X ("Rolling back... MUST NOT
  require a code change or a redeploy") theo đúng tinh thần: dừng tiêm giữa chừng không cần đổi mã
  hay khởi động lại pod.
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
  Deployment (`kubectl set env`) kích hoạt một rolling restart của pod — chính là hành vi "redeploy"
  mà Principle X muốn tránh cho việc bật/tắt; header per-request tránh hoàn toàn việc đó.

## Quyết định 2 — Dashboard SLO đã có (021) tái sử dụng nguyên trạng cho việc quan sát ngân sách bị tiêu hao

**Decision**: User Story 2 dùng lại dashboard Kibana "SLO vận hành hằng ngày — 7 service"
(`docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md`,
`dashboards/slo-van-hanh-hang-ngay.ndjson`) đã được 021-declare-service-slos dựng và xác minh —
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
