# Technical Debt — ghi chú/khám phá/giới hạn của toàn bộ spec 001-021

*Đối tượng đọc: kỹ sư phần mềm / software architect. File này gom lại mọi "lưu ý hay khám phá" (blocker
giữa chừng, bug thật tìm được khi triển khai/xác thực, giới hạn phạm vi đã biết, amendment đính chính)
đã bị tách ra khỏi các file `0NN_Architect_*.md` để những file đó chỉ còn thuần góc nhìn kiến trúc, xúc
tích. Không có thông tin nào bị mất trong lần tách này — chỉ di chuyển, và gộp lại những nội dung trùng
nhau giữa nhiều spec thành 1 mục duy nhất.*

## 1. Blocker & thay đổi phạm vi giữa chừng

- **[002](002_Architect_định%20tuyến%20gateway-BFF.md)** — Lúc viết `spec.md`, giả định sai: 4 domain
  service (từ 001) chỉ có 2 health probe, không có endpoint dữ liệu nào để BFF proxy tới — FR-002/003/004
  không thể thoả mãn đầu-cuối. Quyết định phạm vi: bổ sung 1 bề mặt đọc tối thiểu cho cả 4 service thay
  vì trì hoãn dữ liệu thật sang feature khác. Phase 1-2 (khung Gateway/BFF) giữ nguyên số task cũ; phần
  đánh số lại bắt đầu sau T013.
- **[004](004_Architect_SPA%20mua%20sắm%20tối%20thiểu.md)** — `spec.md` Assumptions ghi: lúc viết spec,
  BFF chỉ có 3 route đọc (list products, get basket, get order) — chưa có add-to-basket/checkout/seed
  data. Feature buộc phải bao gồm cả backend tối thiểu (FR-019–023: basket line item+quantity,
  add-to-basket, place-order, seed catalog) — không chỉ xây UI gọi vào 1 backend đã đầy đủ sẵn.
- **[019](019_Architect_liveness%20readiness%20probe%20cho%20mọi%20service.md)** — Thiết kế ban đầu
  định gọi `ansible-playbook --check` cục bộ ngay trong bộ test C#. Đã đổi hướng khi implement: tự render
  Jinja2 hoàn toàn bằng C#, vì Ansible không chạy native được trên máy phát triển Windows của phiên đó.

## 2. Bug thật phát hiện khi triển khai/xác thực

- **[002](002_Architect_định%20tuyến%20gateway-BFF.md)** — Correlation ID không sống sót qua chặng
  gateway: `CorrelationIdMiddleware` ghi ID vào `HttpContext.Items` và header response nhưng không ghi
  vào header của chính request, trong khi YARP chỉ forward header đã có sẵn → BFF tự sinh ID khác không
  liên quan. Vá bằng đúng 1 dòng, có test hồi quy riêng, xác nhận không phá 14 project test khác dùng
  chung middleware. Cũng phát hiện: cold-start "hơn 3 giây" ban đầu là do đo nhầm `/health/live` thay vì
  `/health/ready` — đo lại đúng cách ra 1.07s, trong ngân sách 3s.
- **[003](003_Architect_danh%20tính%20giả%20lập%20và%20tenant.md)** — T039 chạy toàn luồng phát hiện 2
  việc: `dotnet ef` không tự discover được `DbContext` (design-time discovery đi qua DI và bị chặn bởi
  tenant gate) → mỗi service cần thêm `IDesignTimeDbContextFactory`; và local run cần
  `ASPNETCORE_ENVIRONMENT=Development`, thiếu thì connection string Development không bao giờ load.
- **[005](005_Architect_chạy%20local%20một%20lệnh.md)** — Chế độ debug publish thêm cổng nội bộ không
  thể hoạt động như thiết kế ban đầu: 1 Compose profile chỉ quyết định service có khởi động hay không,
  không thể thêm cổng cho service đã khởi động sẵn → chuyển sang file override
  `docker-compose.debug.yml`. Bản override đầu tiên cũng vô dụng: công bố cổng 5301 nhưng
  `/openapi/v1.json` trả 404 vì BFF chỉ map document đó ở Development trong khi stack chạy Production —
  phải đổi luôn `ASPNETCORE_ENVIRONMENT` và khôi phục hostname Development. Sau sửa: cả document lẫn
  `/bff/products` đều 200.
- **[006](006_Architect_demo%20đặt%20hàng%20end-to-end.md)** — 5 lỗi/phát hiện thật: (1) script
  PowerShell dừng nhầm vì cảnh báo Node vô hại trên stderr bị `$ErrorActionPreference='Stop'` biến thành
  lỗi terminating — vá bằng helper `Invoke-Native` chuyển về `Continue`, dùng exit code làm tín hiệu duy
  nhất; (2) bản đầu của helper đó trộn output và exit code thành 1 mảng, so sánh sai theo cả 2 hướng —
  sửa bằng biến script-scoped riêng; (3) bộ lọc bằng chứng mỗi-hop phải loại health check, nếu không
  Parties.Api "trông bận rộn" dù không nằm trên đường đi đặt hàng (461/461 span đo được đều là health
  check) — có test ngược xác nhận assertion thật sự fail khi thiếu 1 component; (4) 1 ảnh chụp checkout
  "nói dối" — trùng byte-for-byte với ảnh giỏ hàng (viền focus không lưu vào screenshot) — thay bằng ảnh
  giỏ hàng trống; (5) T039 chẩn đoán sai: dừng `orders-api` báo đúng lỗi (exit 1) nhưng thông báo sai
  nguyên nhân ("không phải demo mode") — 3 loại lỗi trông giống nhau từ ngoài (chưa publish/đã
  chết/up-nhưng-không-khoẻ), sửa bằng cách hỏi thẳng Compose/Docker. Kết quả đầy đủ: 9/9 kịch bản
  `quickstart` PASS, build+test toàn bộ 247 test 0 fail, repeat-run 10s (ngân sách 90s).
- **[013](013_Architect_cổng%20chất%20lượng%20CI.md)** — Nhiều phát hiện hạ tầng CI thật: chọn
  `strategyId=2` (head-ref) thay vì `1` (merge-ref) vì GitHub tính required-check theo head SHA — build
  merge-ref khiến check treo mãi ở "Expected — Waiting"; `strategyId=3` (build cả 2) từng thử nhưng bị
  loại vì tranh chấp tài nguyên Docker trên cùng agent, khiến Testcontainers SQL Server timeout. Checks
  API (`publishChecks`) âm thầm thất bại 403 với mọi Personal Access Token (chỉ token cài đặt GitHub App
  mới dùng được) — sửa bằng `githubNotify` (Status API), không cần đổi cấu hình branch protection vì
  required-check khớp theo tên context. Yêu cầu tiên quyết bất ngờ: GitHub từ chối bật branch protection
  trên repo **private** ở gói miễn phí (403 "Upgrade to GitHub Pro or make this repository public") bất
  kể quyền admin — chủ repo chọn chuyển sang public thay vì trả phí. Sáu vấn đề hạ tầng CI đã xác minh
  hoạt động đúng trên container thật (nhưng thay đổi tương ứng còn nằm trong PR #8 chưa merge, tính đến
  2026-09-01 — clone `master` hôm nay vẫn gặp lại): (1) Java agent của Community Branch Plugin phải đặt
  ở CẢ 2 biến môi trường (`SONAR_WEB_JAVAADDITIONALOPTS` và `SONAR_CE_JAVAADDITIONALOPTS`) cùng lúc,
  thiếu 1 SonarQube từ chối khởi động; (2) image Jenkins mặc định không có .NET SDK/Node/Docker CLI —
  phải build từ `docker/ci/jenkins.Dockerfile`; (3) cache phiên bản pnpm đã pin (corepack) phải nằm
  ngoài `/var/jenkins_home` — path đó là volume, cache build lúc `root` vô hình với user `jenkins` lúc
  chạy, cần `COREPACK_HOME` trỏ ra ngoài volume; (4) test Testcontainers cần Docker daemon thật — mount
  `/var/run/docker.sock` + thêm user `jenkins` vào group `root` (1 cấp quyền thật, cần cân nhắc đánh đổi
  có chủ đích); (5) Ryuk không hoàn tất bắt tay trong mô hình docker-outside-of-docker (chỉ chung daemon,
  không chung network namespace) → `TESTCONTAINERS_RYUK_DISABLED=true` (đánh đổi: không còn tự dọn
  container crash giữa chừng); (6) Testcontainers phải trỏ `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal`
  thay vì `localhost` vì port container nằm trên máy ảo Docker Desktop. Cả 2 bug đầu (Checks API,
  strategyId) đã xác minh khắc phục trên 2 PR thật: `#2` (cả 5 check xanh, đã merge) và `#3` (1 unit
  test cố tình hỏng — `ci/unit-tests` báo đúng lý do, nút merge vô hiệu hoá, không có đường vòng nào
  kể cả cho chủ sở hữu repo).
- **[014](014_Architect_máy%20chủ%20định%20danh%20thật.md)** — T047 chạy quickstart phát hiện 3 lỗ hổng
  cấu hình thật mà `dotnet test` không bắt được (vì bộ test dùng `TestJwtBearer`, bỏ qua hoàn toàn fetch
  JWKS/discovery thật): (1) 5/6 service hoàn toàn thiếu `Identity:Authority` — `JwtBearerOptions.Authority`
  sẽ `null` trong triển khai thật, không xác thực được token nào; (2) `docker-compose.yml` (lệnh
  `up.ps1`) chưa từng thêm `identity-db`/`identity-migrate`/`identity-api`; (3) `docker-compose.local.yml`
  thiếu override `Identity__Authority` bằng hostname nội bộ Docker cho cả 6 service — đúng lỗi mà comment
  đầu file này đã cảnh báo trước. Cả 3 đã vá; build + `CrossServiceIsolation.Tests`/
  `StructureConventionTests`/`ContainerConventionTests` pass lại. Bằng chứng thực nghiệm toggle không cần
  redeploy: sửa trực tiếp `IdentityServerAuthCutover` từ `true`→`false` trong container gateway đang chạy
  (`docker exec`, không restart), dừng tạm `bff-api`, request không token qua gateway → `502` (không phải
  `401`) — chứng minh chính gateway ngừng đòi token; bật lại `bff-api`, cùng request nhận `401` từ BFF
  (xác thực độc lập của nó). Hai bộ test xác thực (`JwtBearerAuthenticationTests`,
  `IndependentTokenValidationTests`) từng bị 401 sai do thiếu `.UseTestJwtBearer()` ở helper dùng chung —
  không liên quan 3 lỗ hổng trên, đã vá riêng trước T047.
- **[015](015_Architect_phân%20quyền%20từ%20chối%20theo%20mặc%20định.md)** — Sanity check chủ động: thêm
  tạm 1 route không khai báo phân quyền, chạy scanner → FAIL đúng như kỳ vọng nêu rõ route/file vi phạm;
  gỡ route, chạy lại → PASS — bằng chứng scanner bắt được vi phạm thật, không pass vô nghĩa. Về các dòng
  fail trong bảng test cuối (`Gateway.Api.IntegrationTests` 17/31, `Bff.Api.IntegrationTests` 27/48, 1
  fail mỗi Pact contract test của Baskets/Orders/Products): root cause là Docker Desktop bị dừng/khởi
  động lại giữa phiên làm việc, độ trễ bắc cầu HTTP giữa 2 `WebApplicationFactory` ~4.5s vượt ngân sách
  timeout 1s/3s có từ trước (không đổi bởi 015) — xác nhận KHÔNG phải hồi quy bằng 3 bằng chứng: cùng
  tập test fail y hệt ở lượt `dotnet test` đầu phiên (trước khi có code Phase 3-5 của 015); 1 route hoàn
  toàn không đụng logic phân quyền vẫn fail cùng dấu hiệu độ trễ; `BffTestHost.cs` (có từ trước 015) đã
  tự ghi chú hiện tượng này trong doc-comment.
- **[016](016_Architect_lan%20truyền%20correlation%20ID%20từ%20edge%20đến%20frontend.md)** — Trước
  tính năng này, `CorrelationIdMiddleware` tồn tại độc lập ở từng service nhưng `TenantPropagationHandler`
  (BFF→domain service) chưa relay correlation ID — mọi domain service gọi qua BFF (tức toàn bộ đường đi
  thật của storefront) tự sinh ID riêng, log Gateway/BFF và log 4 domain service không nối được với
  nhau. Vá bằng đúng 1 dòng relay trong `TenantPropagationHandler.SendAsync(...)`.
- **[017](017_Architect_phát%20telemetry%20OTel%20qua%20ServiceDefaults%20tới%20Elastic.md)** — 3 bằng
  chứng thật: (1) truy vấn Elasticsearch thô (không qua Kibana UI) cho 1 correlation ID mẫu trả về 46 log
  entry khớp, mỗi entry mang đủ `service.name`/`TenantId=contoso`/`CorrelationId` chính xác; (2) thử
  nghiệm 2 chiều — comment 2 dòng đăng ký `ServiceDefaults` ở `Products.Api`, rebuild+restart: health
  check vẫn khoẻ (không phụ thuộc ServiceDefaults), nhưng 2 request `200 OK` mới không để lại trace/
  metric/log nào (0 kết quả) dù service vẫn phục vụ bình thường; khôi phục lại 2 dòng, rebuild+restart:
  telemetry phục hồi hoàn toàn với `service.instance.id` mới; (3) xác nhận không phá bất biến 004
  SC-010 (chỉ 2 port publish ra ngoài) — `docker compose config` vẫn chỉ cho ra 2 `published:` port dù
  đã thêm elasticsearch/kibana.
- **[018](018_Architect_secrets%20qua%20cluster%20secret%20store.md)** — Bản đầu của
  `RequiredSecretsValidation` chỉ check "non-blank" — nhưng `appsettings.json` (Production) cố tình giữ
  1 connection string hợp lệ chỉ có host/database, không credential, nên fail-fast sẽ không bao giờ kích
  hoạt khi cluster quên inject credential thật. Vá bằng `HasCredential(...)` (resolve `null` trừ khi có
  thật `Password=`/`Pwd=`/`Trusted_Connection=true`), viết RED test trước, xác nhận FAIL rồi GREEN
  (13/13 pass). Ruleset mặc định của `gitleaks` không có rule khớp `Password=<entropy thấp>` kiểu
  connection string ADO.NET — báo "no leaks found" ngay cả TRƯỚC KHI dọn sạch mật khẩu dev cũ, khiến
  SC-001 "đạt" giả tạo — thêm rule tuỳ biến `connection-string-password`. Xác thực end-to-end thật trên
  cluster K8s thử nghiệm (Vault dev-mode + External Secrets Operator qua Helm): phát hiện thật lúc apply
  — `external-secret.yaml` dùng `apiVersion: external-secrets.io/v1beta1` nhưng CRD của ESO ở phiên bản
  chart hiện tại chỉ **serve** `v1` → lỗi "no matches for kind". Sửa `apiVersion` sang `v1` ở cả 5
  manifest + contract document (sửa tại nguồn). Sau đó: `ExternalSecret` báo `SecretSynced`/`Ready=True`,
  `Secret` K8s khớp chính xác giá trị đã seed, pod thật đọc đúng biến môi trường, rotate secret trong
  Vault (không redeploy) cập nhật ngay. Giới hạn môi trường gặp phải: cluster `kind` không chia sẻ image
  store với `docker build` cục bộ (`ErrImageNeverPull`) — dùng pod `busybox` public + `envFrom` để xác
  thực biến môi trường thay thế.
- **[019](019_Architect_liveness%20readiness%20probe%20cho%20mọi%20service.md)** — Xác thực trên cluster
  `kind` thật — thành công 1 phần, thất bại 1 phần: `gateway` (stateless) readiness pass gần như ngay;
  `orders` (không có SQL Server trong cluster test) readiness fail thật với `503`/timeout nhưng
  **liveness KHÔNG fail**, 0 restart sau ~90s — bằng chứng thật cho FR-003/004/SC-004; rolling restart
  trên `gateway`: pod mới `0/1` tồn tại song song 2 pod cũ vẫn `1/1 Running` tới khi pod mới `Ready` —
  bằng chứng thật FR-009/SC-002/005. Chưa mô phỏng được: kịch bản tiến trình treo thật (liveness restart)
  vì không có công cụ an toàn nào sẵn có để chặn phản hồi từ bên trong container đang chạy — hành vi này
  hiện chỉ đảm bảo ở mức cấu trúc (58 test xUnit), chưa qua cluster thật.
- **[020](020_Architect_timeout%20retry%20circuit%20breaker%20cho%20cuộc%20gọi%20ra%20ngoài.md)** — Bug
  thật tìm được khi xác thực thủ công (T016, gateway thật + `curl` lặp lại, đọc log debug YARP):
  `AvailableDestinationsPolicy` mặc định của YARP là `HealthyOrPanic` — tự động coi MỌI destination
  "khả dụng" khi không còn destination nào khỏe mạnh. Với cluster chỉ có 1 destination (`bff`), circuit
  breaker "mở" nhưng gateway **vẫn tiếp tục thử kết nối thật** (vẫn `502`, không fail-fast). Phải đặt
  tường minh `AvailableDestinationsPolicy: "HealthyAndUnknown"` mới fail-fast thật — đo được `503` trong
  ~20ms sau khi sửa, có test riêng và marker được thêm vào scanner rà soát. Đây cũng chính là điểm
  [002](002_Architect_định%20tuyến%20gateway-BFF.md) tham chiếu ngược lại — cùng 1 bug, chặng
  gateway→BFF. Phát hiện thứ hai (T021): kế hoạch ban đầu định đọc HTTP method từ
  `args.Outcome.Result?.RequestMessage`, nhưng 1 lỗi transport (connection refused — không có
  `HttpResponseMessage`) khiến `Outcome.Result` là `null`. Phải đọc method gốc từ
  `args.Context.GetRequestMessage()` (`Polly.HttpResilienceContextExtensions`) thay vì từ `Outcome`. Kết
  quả xác thực có nhiễu môi trường đã tách bạch rõ: `Gateway.Api.IntegrationTests` 12/34 fail cả trước
  lẫn sau thay đổi (xác nhận bằng `git stash -u` trên `master` gốc, cùng lỗi
  `Expected:NotFound/BadGateway, Actual:Unauthorized`) — không phải hồi quy, nghi ngờ liên quan cách
  `TestJwtBearer` cấp token trong sandbox; `Parties.Api.UnitTests`/`Products.Api.UnitTests` fail vì thiếu
  secret cục bộ (vấn đề môi trường từ 018, không liên quan). Grep xác nhận lại: không có
  `AddMassTransit|IPublishEndpoint|IBus` nào trong `services/` — SCRUM-31 vẫn chưa bắt đầu.
- **[021](021_Architect_khai%20báo%20và%20đo%20SLO%20theo%20từng%20service.md)** — Test-First cho 2 kết
  quả khác dự tính: (1) US1 kỳ vọng PASS ngay (cả 7 manifest "đã đúng sẵn") nhưng thực tế FAIL —
  `service-manifest.yaml` của `identity` thiếu hẳn `service.classification`; sửa xong, chạy lại 22/22
  PASS. (2) US2 kỳ vọng FAIL vì giả định `bff` là "1 ngoại lệ" cần `slos.justification` (do ngân sách
  latency nới hơn), nhưng thực tế PASS 7/7 ngay lần đầu — vì `bff` khớp đúng hồ sơ mặc định của chính
  phân loại `client-facing-bff` (Principle VIII định nghĩa 2 hồ sơ mặc định song song, bình đẳng, không
  cái nào là ngoại lệ của cái kia). Đã sửa lại `research.md`/`contracts/service-manifest-slo-shape.md`
  cho đúng, và **cố tình không** thêm `slos.justification` giả vào `bff` chỉ để "có 1 ví dụ" — tránh dữ
  liệu không trung thực. Số liệu đối chiếu thật (Orders.Api, cửa sổ 24h): 9.237 request, error-rate
  0,0108% (đạt, ngưỡng ≤0,1%), p95 33,6ms (đạt, ≤150ms), p99 393,4ms (đạt nhưng gần ngưỡng 500ms). Bằng
  chứng "ngân sách bị tiêu hao": gửi 300 request đồng thời tới `/health/ready` — baseline p95=157,9ms/
  p99=440,1ms, ngay sau burst p95=14.975,7ms/p99=18.519,2ms (tăng ~95 lần). Bằng chứng "không có dữ liệu
  ≠ 0% lỗi": truy vấn khoảng thời gian trước khi môi trường demo tồn tại → `hits.total.value=0`, khác
  hẳn "có traffic nhưng 0 lỗi" — đúng cơ chế Lens Table (0 tài liệu → dòng biến mất, không hiển thị `0%`
  gây hiểu lầm).

## 3. Giới hạn phạm vi đã biết

- **[001](001_Architect_dựng%20khung%204%20dịch%20vụ.md)** — Tenant-keyed schema/connection resolution
  (Principle V) không thuộc phạm vi — mỗi `DbContext` trỏ 1 connection mặc định duy nhất, có cấu trúc
  sẵn để 1 connection resolver theo tenant thay thế sau (giao cho SCRUM-12/003). Outbox table của Orders
  (Principle IV) chưa tạo — sẽ xuất hiện cùng event đầu tiên thật. Chạy đồng thời 4 service bằng 1 lệnh
  không thuộc phạm vi (đó là SCRUM-15/005) — phạm vi ở đây chỉ là mỗi service tự chạy độc lập được.
- **[004](004_Architect_SPA%20mua%20sắm%20tối%20thiểu.md), [005](005_Architect_chạy%20local%20một%20lệnh.md)** —
  Schema-per-tenant separation mà 003 đã đặc tả và đánh dấu hoàn thành trên giấy **thực tế chưa từng
  được triển khai** — `HasDefaultSchema` không xuất hiện ở đâu trong mã nguồn, mọi migration nhắm vào
  schema `dbo` mặc định. Lần đầu nêu ra ở 004 (thêm dữ liệu nghiệp vụ thuộc-về-tenant đầu tiên ngay trên
  nền khoảng cách này, việc đóng lại được mô tả "contained" nhưng để ngỏ cho maintainer quyết định —
  chưa có quyết định). Nêu lại lần 2 ở 005 (chạy chung 1 SQL Server khiến dễ nhận ra hơn, không làm tệ
  hơn — vẫn chưa có quyết định). Riêng 005: checkout kiểu event-driven (SCRUM-18/31) cố ý không xây vì
  chưa có hạ tầng messaging — deviation có chủ đích, không phải bị quên; image storefront đóng cứng địa
  chỉ backend lúc build — chỉ phù hợp cho đúng stack này, cấu hình theo runtime là việc cần làm khi
  triển khai thật sự; số liệu tài nguyên đo được (~1.3GB lúc rảnh, ~2.2GB lúc cao điểm, ngưỡng sàn 6GB)
  đo khi image nền đã tải sẵn — máy hoàn toàn sạch còn phải tải thêm ~2GB, chưa được đo trong con số trên.
- **[006](006_Architect_demo%20đặt%20hàng%20end-to-end.md)** — T042 (đính video vào Jira) chưa hoàn
  thành, chờ người thực hiện thủ công. Demo chỉ chứng minh single-tenant — chứng minh cách ly giữa 2
  tenant song song nằm ngoài phạm vi. Không có event/outbox/messaging nào được thêm ở feature này —
  thuộc SCRUM-18, để dành riêng (cùng mạch với nhóm "broker/messaging chưa đấu nối" bên dưới, chỉ khác
  Jira ticket vì đây là lần nêu sớm nhất).
- **[007](007_Architect_hợp%20đồng%20OpenAPI%20cho%20BFF.md)** — Nếu bất kỳ task xác minh nào (T004-T009)
  phát hiện sai lệch thật giữa route và spec, đó là 1 defect thật ngoài giả định phạm vi — cần dừng định
  phạm vi lại, không âm thầm vá trong 1 task "chỉ xác minh". Không có sai lệch nào ghi nhận ở lượt này —
  cả 4 checkpoint PASS. Phạm vi chỉ giới hạn products/baskets/orders — route parties, checkout,
  health-check ngoài phạm vi, theo đúng Assumptions.
- **Broker/messaging chưa đấu nối, chờ SCRUM-31** —
  [005](005_Architect_chạy%20local%20một%20lệnh.md),
  [006](006_Architect_demo%20đặt%20hàng%20end-to-end.md),
  [008](008_Architect_event%20schema%20có%20version.md),
  [010](010_Architect_hạ%20tầng%20kiểm%20thử%20container%20thật.md),
  [011](011_Architect_kiểm%20thử%20hợp%20đồng%20tiêu%20dùng.md),
  [016](016_Architect_lan%20truyền%20correlation%20ID%20từ%20edge%20đến%20frontend.md),
  [020](020_Architect_timeout%20retry%20circuit%20breaker%20cho%20cuộc%20gọi%20ra%20ngoài.md) — Chưa có
  publisher/consumer thật nào cho `OrderPlaced`/`BasketCheckedOut`. 008: event contract giới hạn ở tầng
  hợp đồng, không đấu nối broker ("No existing service is touched by any task"). 005: Redis/RabbitMQ
  chạy sẵn theo khai báo phụ thuộc nhưng không service nào kết nối — health check khoẻ chỉ chứng minh
  "có mặt", không chứng minh "đang dùng trong luồng thật" (ADR-0011: checkout vẫn đồng bộ). 010: fixture
  Redis/RabbitMQ đứng chờ tính năng tương lai; chịu lỗi broker toàn diện ngoài phạm vi, thuộc SCRUM-31
  (Amendment 2026-09-10: SCRUM-30/020 chỉ đóng phần HTTP outbound — Gateway→BFF, BFF→4 service, JwtBearer
  backchannel — 020 tự grep xác nhận lại `AddMassTransit|IPublishEndpoint|IBus` vẫn không tồn tại trong
  `services/`, broker resilience vẫn chờ SCRUM-31, chưa bắt đầu). 011: event boundary verify không cần
  broker thật/không cần delivery đầu-cuối — khi SCRUM-31 đấu nối hạ tầng thật, cặp contract test này là
  điểm khởi đầu, không phải điểm kết thúc. 016: không xây publisher/consumer mới cho correlation ID —
  payload event mang theo ID, sẵn sàng cho khi publisher thật được nối. 020: call-site #5 (service→
  broker) xác nhận lại vẫn không tồn tại bằng grep, không đổi bởi tính năng này.
- **[008](008_Architect_event%20schema%20có%20version.md)** — Chỉ đúng 2 event (`OrderPlaced`,
  `BasketCheckedOut`) nằm trong phạm vi — event khác trong tương lai cần lặp lại đúng khuôn mẫu này
  riêng, không tự động áp dụng. Khoảng thời gian deprecation cụ thể (bao nhiêu ngày/chu kỳ release) là
  chi tiết chính sách/tài liệu, không phải con số cố định trong đặc tả — chỉ yêu cầu nó tồn tại, được
  ghi lại, và được tôn trọng.
- **[009](009_Architect_TDD%20hồi%20tố%20giỏ%20hàng%20và%20đơn%20hàng.md)** — Khoảng cách thật sự là
  lịch sử commit, không phải mã nguồn: implementation và unit test của cả 2 service nằm gộp trong cùng
  1 commit lớn, không đi theo đúng thứ tự đỏ-trước-xanh ở CẤP COMMIT mà Principle III yêu cầu. Cố tình
  KHÔNG `git rebase` để tách giả tạo mỗi commit gộp thành 1 cặp test-rồi-implementation — sẽ bịa ra 1
  trình tự sự kiện chưa từng xảy ra, bị loại trừ theo chính chính sách vận hành repo (cần uỷ quyền tường
  minh, chưa được cấp; kể cả có uỷ quyền cũng làm sai lệch tác giả thay vì sửa được gì). Khoảng cách được
  đóng lại hướng về tương lai (ghi chú kỷ luật + quy trình kiểm tra cho reviewer), không sửa quá khứ.
  Phạm vi chỉ ở mức unit test (domain logic cô lập), khớp đúng acceptance criteria gốc của Jira — coverage
  integration/contract cho 2 service này thuộc phạm vi quản lý riêng của Principle III, không bị định
  phạm vi lại ở đây. File net-new duy nhất của feature này là `docs/engineering/test-first-commits.md` —
  không có thay đổi domain logic nào tồn tại sau khi feature hoàn thành.
- **[010](010_Architect_hạ%20tầng%20kiểm%20thử%20container%20thật.md)** — Việc gộp 4 bản copy-paste của
  `SqlServerFixture.cs` (baskets/orders/parties/products) vào thư viện chung `shared/IntegrationTestSupport`
  KHÔNG nằm trong phạm vi feature này — ghi nhận là follow-up, chưa quyết định.
- **[011](011_Architect_kiểm%20thử%20hợp%20đồng%20tiêu%20dùng.md)** — Phạm vi chỉ dừng ở 4 boundary của
  "thin slice" (BFF↔products, BFF↔baskets, BFF↔orders, cặp event baskets↔orders) — mở rộng ra boundary
  khác là việc tương lai, ngoài phạm vi này.
- **[013](013_Architect_cổng%20chất%20lượng%20CI.md)** — Tiền đề "phải tránh phí SaaS vì repo private"
  không còn đúng 100% từ khi repo chuyển sang public vì lý do khác — điểm có thể xem lại, không cần làm
  ngay. Đây là instance phát triển (dev), chạy Docker Desktop cục bộ — bản production (K8s qua Ansible)
  vẫn là việc riêng, chưa thực hiện.
- **[014](014_Architect_máy%20chủ%20định%20danh%20thật.md)** — Màn hình đăng nhập tương tác
  (Authorization Code + PKCE, redirect trình duyệt thật) chưa được xây — client `ecommerce-web-spa` đã
  đăng ký sẵn nhưng Duende's Razor Pages quickstart UI là việc riêng, ngoài phạm vi SCRUM-23 (Assumptions:
  đăng ký/quản lý tài khoản người dùng ngoài phạm vi). Để vẫn kiểm thử được phát hành/kiểm tra token đầu-
  cuối mà không cần UI đó, `Config.cs` đăng ký thêm client `integration-test-ropc` (Resource Owner
  Password, có secret, đánh dấu rõ không dùng cho production) — quyết định phạm vi tường minh, không
  phải lỗ hổng bảo mật bị bỏ sót.
- **[015](015_Architect_phân%20quyền%20từ%20chối%20theo%20mặc%20định.md)** — US3 không thêm quy tắc
  nghiệp vụ mới — rà soát phát hiện các kiểm tra phía server tương ứng đã tồn tại từ trước; việc thật sự
  của US3 là bổ sung đúng 1 test bằng chứng còn thiếu. `gateway`/`identity` cố ý ngoài
  `AuthorizationPolicyDeclaredScanner.AuthorizingServices` — gateway chỉ có 1 route catch-all không có
  granularity để khai báo riêng; `identity` phát hành token, không phục vụ endpoint nghiệp vụ nào. Chưa
  có RBAC chi tiết — chính sách `ApiScope` hiện nhị phân, khớp đúng phạm vi SCRUM-24
  (`AddIdentity<ApplicationUser,IdentityRole>()` đã sẵn từ 014 nhưng chưa seed vai trò nào). Chưa có
  message handler nào để kiểm chứng `ScanConsumers()` thật sự chặn được vi phạm — guard mới chứng minh
  được nó "chạy qua toàn bộ services" (structural), chưa chứng minh "bắt được vi phạm thật".
- **[016](016_Architect_lan%20truyền%20correlation%20ID%20từ%20edge%20đến%20frontend.md)** — Sandbox của
  phiên triển khai này không có Docker daemon — các bài test cần Testcontainers không chạy được, không
  phải hồi quy của 016. Xác minh dựa vào unit test không cần Docker + rà soát mã nguồn. Khác 014/015,
  016 không có 1 lượt chạy end-to-end thật trên stack đầy đủ trong chính phiên triển khai của nó — bằng
  chứng end-to-end thật (nối với Elastic) chỉ xuất hiện đầy đủ ở tính năng kế tiếp, 017.
- **[017](017_Architect_phát%20telemetry%20OTel%20qua%20ServiceDefaults%20tới%20Elastic.md)** — Chỉ chạy
  trên máy phát triển qua Docker Compose — chưa phải hạ tầng vận hành thật cho môi trường sản phẩm chính
  thức. Docker Desktop trên Windows/macOS cần chỉnh `vm.max_map_count` thủ công 1 lần trước khi
  Elasticsearch khởi động được — không tự động hoá được qua Compose.
- **[018](018_Architect_secrets%20qua%20cluster%20secret%20store.md)** — Cluster K8s thử nghiệm dùng để
  xác thực là TẠM THỜI, không phải hạ tầng production. ADR-0007 Amendment (2026-09-06) ghi rõ: 3 Action
  Item gốc (deploy Vault HA thật, cài ESO vào cluster thật, định nghĩa dynamic-credential policy) vẫn còn
  nguyên chưa làm — không có Vault, không có ESO nào đang chạy cho môi trường vận hành thật. Mã ứng dụng
  giờ phụ thuộc đúng hình dạng `Secret` object mô tả, nhưng chưa có gì THẬT SỰ tạo ra object đó ngoài
  `kubectl apply` thủ công vào cluster thử nghiệm tạm thời.
- **[019](019_Architect_liveness%20readiness%20probe%20cho%20mọi%20service.md)** — `ansible-lint`/
  `kubeconform` (lớp kiểm tra 2) CHƯA từng chạy thật trong chính phiên triển khai — Ansible không chạy
  native trên Windows (`WinError 87`), WSL thiếu quyền `sudo` để cài `python3-venv`/`pip`.
  `scripts/ci/lint-deployment-manifests.sh` viết đúng và kỳ vọng chạy được trên agent Linux thật của
  Jenkins — kỳ vọng chưa có bằng chứng thực thi. Cluster `kind` dùng để xác thực là TẠM THỜI, đã xoá
  ngay sau khi xong — giống 018, không có cluster K8s thật nào tồn tại lâu dài. Kịch bản "liveness tự
  khởi động lại pod treo" (US3) chưa được xác thực động — chỉ có bằng chứng cấu trúc.
- **[020](020_Architect_timeout%20retry%20circuit%20breaker%20cho%20cuộc%20gọi%20ra%20ngoài.md)** — Bước
  6 của `quickstart.md` (quan sát sự kiện Polly qua OTel Collector/Elastic thật) chưa thực hiện được
  trong phiên triển khai — không có OTel Collector chạy sẵn trong sandbox; chỉ xác nhận ở mức cấu hình
  (`AddSource("Polly")`/`AddMeter("Polly")` build sạch), chưa có bằng chứng runtime thật qua Elastic đặc
  thù cho Polly (khác dữ liệu Elastic thật đã có ở
  [017](017_Architect_phát%20telemetry%20OTel%20qua%20ServiceDefaults%20tới%20Elastic.md)). Chỉ phủ 3/5
  loại điểm gọi — service→service đồng bộ không tồn tại nên không có gì để bọc; service→broker
  (SCRUM-31) vẫn hoàn toàn ngoài phạm vi (xem nhóm broker/messaging ở trên). Không xây cơ chế
  idempotency-key — thu hẹp retry theo method chỉ là biện pháp giảm thiểu trong phạm vi, không giải
  quyết triệt để nếu 1 client tự ý gửi lại `POST` thủ công.
- **[021](021_Architect_khai%20báo%20và%20đo%20SLO%20theo%20từng%20service.md)** — Không có alert rule
  tự động — dashboard chỉ hỗ trợ "tra cứu được", không tự động cảnh báo khi vượt ngân sách; thuộc
  SCRUM-35 (`docs/superpowers/specs/2026-09-08-dashboard-slo-van-hanh-design.md`), ngoài phạm vi. Không
  có test CI gọi Elasticsearch thật để assert dashboard luôn đúng — cân nhắc rồi loại, cùng lý do 019
  loại phương án chạy cluster K8s thật trong mọi CI. `slos.justification` chưa có instance thật nào —
  cơ chế sẵn sàng trong schema nhưng chưa thực chiến. p99 của `Orders.Api` đo được (393,4ms) gần ngưỡng
  khai báo (500ms) ngay ở điều kiện vận hành bình thường — đáng theo dõi tiếp, không phải lỗi cần sửa
  ngay.

## 4. Amendment — đính chính khi thực tế lệch spec gốc

- **[014](014_Architect_máy%20chủ%20định%20danh%20thật.md), [015](015_Architect_phân%20quyền%20từ%20chối%20theo%20mặc%20định.md)** —
  AMENDED so với ADR-0008: toggle KHÔNG dùng Unleash thật. ADR-0008 đã *chọn* Unleash ở cấp kiến trúc,
  nhưng cả 3 Action Item (deploy self-hosted Unleash, tích hợp SDK, CI check hạn dùng toggle) chưa được
  triển khai ở bất kỳ đâu — xây hạ tầng Unleash chỉ để phục vụ 1 toggle của riêng 1 feature là vượt phạm
  vi hợp lý. Cả `IdentityServerAuthCutover` (014) lẫn `AuthorizationRequireApiScope` (015) đọc từ
  `FeatureToggles` qua `IOptionsMonitor`, đánh giá lại mỗi request — hot-reload 1 ConfigMap/file có tác
  dụng ngay, không cần restart pod. Thay nguồn đọc bằng `IFeatureManager` do Unleash cấp sau này chỉ là
  1 thay đổi 1 dòng tại điểm đọc.
