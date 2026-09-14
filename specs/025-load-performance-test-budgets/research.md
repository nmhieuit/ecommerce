# Research: Kiểm thử tải/hiệu năng luồng nghiệp vụ trọng yếu

**Feature**: [spec.md](./spec.md) | **Ngày**: 2026-09-12

## Quyết định 0 — Nguồn chân lý cho ngân sách tham chiếu

**Quyết định**: Dùng lại đúng 2 hồ sơ mặc định nền tảng (`client-facing-bff`, `internal-service-api`)
mà hạng mục 021 đã hiện thực hoá thành dữ liệu máy-đọc-được trong từng `service-manifest.yaml`, đọc
qua `ServiceManifestSloConventionTests.ServiceManifestFixture` đã có. Không định nghĩa lại con số ở
bất kỳ đâu khác.

**Rationale**: 021 đã giải quyết đúng vấn đề "một nguồn chân lý duy nhất cho ngân sách SLO". Định
nghĩa lại con số trong dự án kiểm thử tải sẽ tái tạo chính rủi ro trôi dạt (drift) mà 021 vừa đóng
lại — hai nơi có thể lệch nhau âm thầm theo thời gian.

**Alternatives considered**: Hard-code 300ms/800ms trực tiếp trong test kiểm thử tải — bị bác bỏ vì
lý do trên.

## Quyết định 1 — Công cụ tạo tải

**Quyết định**: NBomber, **ghim đúng phiên bản 4.1.2** (không dùng phiên bản mới nhất 6.x).

**Rationale**: Agent CI hiện tại (theo `Jenkinsfile`) chỉ có sẵn .NET 10 SDK, Node 22, và Docker —
không có Go, JVM, hay Python runtime nào khác được liệt kê. NBomber không đòi hỏi cài thêm công cụ
ngoài .NET SDK đã có, cho phép kết quả kiểm thử xuất hiện trong cùng luồng `dotnet test`/`trx` mà
pipeline đã dùng cho mọi tier khác, và cho phép logic đối chiếu ngân sách (đọc ngưỡng từ
`ServiceManifestFixture`) được viết bằng chính C# thay vì một ngôn ngữ cấu hình riêng.

**Phát hiện quan trọng khi triển khai — giấy phép**: bản NBomber mới nhất trên NuGet (dòng 5.x/6.x,
hiện tại 6.6.0) được phát hành dưới "NBomber Business License" — theo đúng trang giấy phép chính
thức của NBomber, **"FREE only for personal use. You can't use the FREE versions for an
organization"**. Ecommerce Platform là mã nguồn của một tổ chức, nên dùng thẳng bản mới nhất sẽ vi
phạm giấy phép ngay khi chạy trong CI — đúng loại rủi ro mà chính repo này đã từng ghi nhận và né
tránh cho `MassTransit` (xem comment tại `Directory.Packages.props`: "Every other dependency in this
repository is free/OSS; introducing a paid one is a business decision no plan/implementation step
has authority to make on its own"). Dòng NBomber 4.x — trước khi đổi mô hình giấy phép — vẫn là
Apache-2.0, miễn phí cho mọi mục đích, kể cả dùng trong tổ chức; bản 4.1.2 là bản 4.x cuối cùng được
phát hành (30/03/2023), xác nhận trực tiếp trên trang giấy phép NuGet của chính phiên bản đó
("Apache License 2.0"). Ghim đúng `4.1.2` — không dùng `4.*` nổi (floating) — theo đúng tinh thần ghim
chính xác mà `MassTransit`/`Testcontainers.MsSql` trong cùng file đã làm, để một lần `dotnet restore`
sau này không vô tình kéo lên dòng 5.x/6.x có giấy phép khác.

**Alternatives considered**:
- **NBomber bản mới nhất (5.x/6.x)**: bị bác bỏ vì lý do giấy phép nêu trên — đây là một quyết định
  kinh doanh (mua giấy phép Business/Enterprise) mà một bước lập kế hoạch/triển khai không có thẩm
  quyền tự quyết định, đúng tiền lệ đã có với MassTransit.
- **k6**: DX và báo cáo tốt, nhưng là binary Go riêng — cần thêm bước cài đặt/hình ảnh Docker mới
  trên agent Jenkins, một công cụ mà "Agent requirements" của Jenkinsfile hiện không liệt kê.
- **JMeter**: chạy trên JVM, kịch bản XML — lệch hẳn quy ước C#-first và báo cáo `trx` hiện có của
  repo.
- **Locust**: Python — cùng vấn đề "thêm runtime mới cho agent CI" như k6.

## Quyết định 2 — Phạm vi đo lường theo nhóm endpoint

**Quyết định**: NBomber lái tải trực tiếp qua đúng 4 route BFF của luồng trọng yếu (qua gateway,
giống hệt cách một shopper thật gọi) và đối chiếu số đo với ngân sách lớp `client-facing-bff` — đây
là phần cổng chặn TỰ ĐỘNG (FR-004), không phụ thuộc hệ thống ngoài. Việc đối chiếu lớp
`internal-service-api` (4 service nghiệp vụ phía sau BFF) tận dụng lại dashboard đo liên tục mà 021
đã dựng (đọc từ cùng dữ liệu OTel mà traffic của NBomber tạo ra trong lúc chạy) như một bước xác nhận
bổ sung trong `quickstart.md`, không xây một đường đo song song mới.

**Rationale**: Không có shopper thật nào gọi thẳng service nội bộ — lái tải trực tiếp vào 4 service
phía sau sẽ không phản ánh đúng luồng người dùng mà Jira SCRUM-32 mô tả, và sẽ cần thêm cơ chế
auth/test double không cần thiết chỉ để mô phỏng một client không có thật. Cổng chặn tự động chỉ nên
phụ thuộc vào chính phép đo mà bài kiểm thử tải trực tiếp tạo ra (client-facing-bff), không phụ thuộc
Elastic/Kibana có sẵn hay không — giữ đúng logic 021 đã chọn cho phần đo liên tục ("không chặn PR/gate
bằng một hệ thống ngoài có thể tự nó chập chờn").

**Alternatives considered**: Cho NBomber gọi thẳng cả 4 service nội bộ song song để tự đo lớp
`internal-service-api` — bị bác bỏ vì lý do trên.

## Quyết định 3 — Vị trí trong pipeline

**Quyết định**: Thêm tier "performance" mới cho `scripts/ci/run-dotnet-tests.sh`, loại trừ dự án
kiểm thử tải khỏi tier "unit" hiện có, và chạy tier mới từ một pipeline riêng theo lịch trình
(`Jenkinsfile.performance`) nhắm vào môi trường giống production (`docker-compose.demo.yml`) — tách
biệt hoàn toàn khỏi `Jenkinsfile` chặn PR hiện có.

**Rationale**: Khớp nguyên văn mục "Development Workflow and Quality Gates" của hiến chương:
"Performance gate: performance tests for critical paths run on a scheduled pipeline against a
production-like environment, and a budget regression blocks the release even when the PR gate is
green" — rõ ràng đây là một cổng chặn KHÁC với PR gate, không phải một stage được thêm vào 5 stage
bắt buộc hiện có (specs/013). Khảo sát thực tế còn phát hiện một rủi ro cụ thể: `run-dotnet-tests.sh`
tự động gom mọi `*Tests.csproj` chưa khớp `ContractTests`/`IntegrationTest` vào tier "unit" — tier
chạy trên MỌI PR mà không cần Docker/stack sống. Nếu không loại trừ tường minh, dự án kiểm thử tải
mới (cần stack sống, chạy chậm hơn nhiều bậc) sẽ tự động lọt vào đó, làm hỏng chính cổng chặn PR đang
được branch protection thực thi.

**Alternatives considered**:
- Thêm một stage mới thẳng vào `Jenkinsfile` hiện có, chạy trên mọi PR — bị bác bỏ vì trái đúng câu
  chữ hiến chương ("scheduled pipeline", không phải PR gate) và làm PR gate chậm đi đáng kể (kiểm thử
  tải cần thời gian chạy lâu hơn nhiều so với unit/integration/contract).
- Đặt dự án kiểm thử tải ở một thư mục ngoài `tests/` để né glob của script — bị bác bỏ vì phá vỡ quy
  ước vị trí `tests/*Tests.csproj` toàn repo, gây khó phát hiện dự án trong solution/IDE.

## Quyết định 4 — Mô phỏng hồi quy hiệu năng có chủ đích (Jira Test Scenario 2/3)

**Quyết định**: Tài liệu hoá trong `quickstart.md` đúng kỹ thuật thủ công mà 021 đã dùng để giả lập
độ trễ: thêm tạm một `Task.Delay` vào một handler đọc dữ liệu của service baskets hoặc orders, chạy
lại bài kiểm thử tải, xác nhận FAIL, hoàn tác, chạy lại xác nhận PASS trở lại. Không xây cơ chế
fault-injection thường trực (bật/tắt bằng cấu hình) mới.

**Rationale**: Đúng phạm vi 3 kịch bản kiểm thử nêu trong Jira SCRUM-32 — không hơn. "Failure
Injection" là một phần khác, riêng biệt của cùng epic SCRUM-8 ([Phase 4] Budgets, Timeouts, and
Failure Injection), chưa được yêu cầu bởi ticket này.

**Alternatives considered**: Xây middleware chaos injection có thể bật/tắt bằng biến môi trường — bị
bác bỏ vì vượt phạm vi, để dành cho hạng mục Failure Injection riêng nếu và khi nó được lên kế hoạch.

## Quyết định 5 — Bổ sung khai báo còn thiếu trong manifest `bff`

**Quyết định**: Bổ sung 2 mục `endpoints` còn thiếu trong
`services/bff/src/Bff.Api/service-manifest.yaml` — `POST /bff/basket/items` và `POST /bff/checkout`
— dùng đúng ngân sách `client-facing-bff` hiện có của mọi route khác trong cùng file (p95 300ms/p99
800ms), trước khi `CriticalPathLoadTests` có thể tham chiếu ngưỡng cho 2 bước này.

**Rationale**: Khảo sát mã nguồn (`BasketsEndpoints.cs`, `CheckoutEndpoints.cs`) cho thấy 2 route này
đã tồn tại và hoạt động từ trước, nhưng manifest — vốn là tài liệu mô tả, viết thủ công — chưa được
cập nhật theo kịp. Đây là khoảng trống thật do khảo sát phát hiện, không phải giả định; FR-003 của
spec yêu cầu đối chiếu ngân sách cho từng bước của luồng trọng yếu, nên khoảng trống này chặn đường
nếu không được lấp trước.

**Alternatives considered**: Dùng ngân sách cấp `slos` của service (cũng chính là 300ms/800ms) làm
ngưỡng ngầm định cho 2 route thiếu, không sửa manifest — bị bác bỏ vì để lại một khoảng trống tài
liệu thật không được sửa, và làm cho việc đối chiếu "theo từng nhóm endpoint" của FR-003 phải suy diễn
ngầm thay vì đọc trực tiếp, đi ngược tinh thần "manifest là nguồn chân lý tường minh" mà 021 đã xác
lập.

**Ghi chú triển khai**: `ServiceManifestSloConventionTests.ServiceManifestModel` (021) chỉ parse khối
`service`/`slos` cấp service, KHÔNG parse khối `endpoints:` — mở rộng model đó là việc của một feature
khác, ngoài phạm vi ở đây. `CriticalPathStepBudgets` (T007) vì vậy đọc đúng khối `slos.latency` cấp
service của `bff` (giá trị giống hệt 4 mục `endpoints` vừa bổ sung, vì cả 4 bước đều thuộc lớp
`client-facing-bff` — research.md Quyết định 2) làm ngưỡng cho cả 4 bước. T006 vẫn cần thiết: nó đóng
khoảng trống TÀI LIỆU (con người đọc manifest thấy đủ cả 4 route), độc lập với việc bài kiểm thử tải
đọc ngưỡng từ đâu.

## Quyết định 6 — Xác thực khi gọi qua gateway (ĐÃ SỬA sau khi chạy thật — xem "Phát hiện khi chạy thật trên stack sống" bên dưới)

**Quyết định ban đầu (SAI, giữ lại có gạch ngang để không mất dấu vết)**: ~~`GatewayClient` gọi thẳng
gateway mà KHÔNG tự đính kèm bearer token nào — giống hệt cách `scripts/demo.ps1` và
`walkthrough.spec.ts` đang gọi qua gateway thành công ngày hôm nay.~~

**Phát hiện khi chạy thật trên stack sống (tasks.md T017, `docker compose -f docker-compose.yml -f
docker-compose.demo.yml up`)**: giả định trên SAI. `GET /bff/products` qua gateway, không kèm token,
trả về **401** thật. Đọc lại mã nguồn sau khi thấy lỗi xác nhận nguyên nhân:

- `TenantHeaderPropagationMiddleware`/`SubjectHeaderPropagationMiddleware` của gateway chỉ chuyển tiếp
  2 header (`X-Tenant-Id`, `X-Subject-Id`) từ danh tính stub cục bộ của gateway — KHÔNG bao giờ đính
  kèm hay chuyển tiếp bearer token nào xuống hạ lưu.
- `services/bff/src/Bff.Api/Program.cs` gọi `AddIdentityValidation()`/`UseIdentityValidation()`
  KHÔNG ĐIỀU KIỆN — BFF luôn xác thực JWT thật, không có cơ chế stub/toggle bỏ qua như gateway có.
- Vì vậy: dù gateway ở chế độ stub (không cần token để đi qua CHÍNH gateway), request vẫn bị BFF từ
  chối ngay khi không có token — bất kể `IdentityServerAuthCutover` là gì.

Lấy token thật qua `/connect/token` (client `integration-test-ropc`) cũng không khả thi ngay: cần một
user đã tồn tại, mà `SeedData.cs` "không giữ sẵn thông tin đăng nhập nào theo thiết kế" (comment gốc
trong file) — không có tài khoản demo, không có UI đăng nhập, không có endpoint tự đăng ký. Đây là
một khoảng trống thật của TOÀN NỀN TẢNG (không riêng gì tính năng này) — **đã tạo task riêng để theo
dõi** (xem ghi chú cuối file này), không sửa trong phạm vi tính năng 025.

**Phát hiện phụ, cũng từ lần chạy thật này**: `identity-api` trên một stack mới dựng có dấu hiệu
race/deadlock khi Duende IdentityServer tự sinh signing key lần đầu (`KeyManager.CreateAndStoreNewKeyAsync`)
dưới tải đồng thời — nhiều request tới `identity-api` (kể cả `/.well-known/openid-configuration`) bị
treo/timeout với lỗi "Operation cancelled by user" trong log container. Có thể liên quan hoặc độc lập
với khoảng trống ở trên — cũng đã đưa vào task riêng để điều tra tiếp, không thuộc phạm vi tính năng
này.

**Quyết định thực tế cho tính năng 025 (giữ nguyên hiện trạng, không tự chế cơ chế xác thực mới)**:
`GatewayClient` VẪN không tự đính token (mã nguồn không đổi) — vì không có cách nào an toàn, đúng thiết
kế hiện có để lấy một token thật từ một client HTTP bên ngoài. Tính năng 025 không có thẩm quyền quyết
định thêm một cơ chế seed-user-demo hay tương tự vào `identity-api` (đó là thay đổi kiến trúc bảo mật,
ngoài phạm vi một bài kiểm thử tải). Hệ quả: bài kiểm thử tải này **chạy đúng cơ chế đã thiết kế (bất
biến 1–6 của contracts/load-test-run-contract.md đều đúng), nhưng sẽ FAIL do 401 cho đến khi nền tảng
có một cách hợp lệ để lấy token thật** — đây là hành vi ĐÚNG theo FR-004 (một vi phạm/lỗi thật khiến
lần chạy thất bại rõ ràng), không phải một lỗi của bài kiểm thử tải. Khi nền tảng có cơ chế lấy token
(task đã tạo), `GatewayClient` cần một thay đổi nhỏ, có ghi chú rõ vị trí (xem code comment trong
`GatewayClient.cs`) để gọi cơ chế đó.

**Alternatives considered**: Tự thêm một endpoint/flag "seed demo user" vào `identity-api` chỉ để bài
kiểm thử tải này chạy được — bị bác bỏ: đây là thay đổi bảo mật/kiến trúc của một service khác, không
phải chi tiết triển khai của một bài kiểm thử tải, và đi ngược quyết định có chủ đích trong chính
`SeedData.cs` ("seeding a demo account with a known password here would put it on every environment
this image runs in").
