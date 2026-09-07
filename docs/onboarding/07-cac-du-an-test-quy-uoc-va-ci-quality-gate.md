# 07 — Các dự án test quy ước & CI quality gate

> Đọc [01-tong-quan-kien-truc.md](01-tong-quan-kien-truc.md) trước nếu chưa quen khái niệm .NET cơ bản. Tài liệu này giải thích 5 dự án nằm trong thư mục `tests/` ở gốc repo — khác hẳn `services/*/tests/` (test của riêng 1 service) — và cách chúng vận hành trong build/CI. (Cập nhật: dự án thứ 5, `DeploymentManifestConventionTests`, mới merge vào `master` sau khi 4 phần đầu của tài liệu này được viết — spec `019-liveness-readiness-probes`.)
>
> **Lưu ý quan trọng về phương pháp:** một số câu hỏi trong tài liệu này KHÔNG thể trả lời chỉ bằng cách đọc code — cần xác nhận từ bạn hoặc từ trạng thái sống trên GitHub mà tôi không truy cập được (không có `gh` CLI, và trình duyệt trong môi trường này chưa đăng nhập GitHub nên không mở được trang settings riêng tư). Những chỗ đó được đánh dấu rõ **[Theo bạn xác nhận]** thay vì viết như một sự thật đã tự kiểm chứng.

## Vì sao 4 dự án này khác với test bình thường

Test bình thường (ví dụ `services/orders/tests/Orders.Api.UnitTests/`) kiểm tra **hành vi của code khi chạy** — gọi 1 hàm, kiểm tra kết quả. 4 dự án dưới `tests/` KHÔNG làm vậy: chúng là **"scanner"** — đọc các file `.cs`/`.csproj`/`Dockerfile` đã commit **như văn bản thuần** (không compile, không chạy service nào), rồi khẳng định một quy ước kiến trúc được tuân thủ ở **mọi nơi cần tuân thủ**, không chỉ ở nơi ai đó nhớ viết test riêng.

Cả 4 dự án đều theo đúng 1 khuôn code giống nhau (mỗi dự án 1 file `*Scanner.cs` chứa logic quét + 1 file `*Tests.cs` gọi scanner và assert), và đều tự định vị gốc repo bằng cùng 1 kỹ thuật:
```csharp
for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
{
    if (File.Exists(Path.Combine(directory.FullName, "Ecommerce.slnx")))
        return Path.Combine(directory.FullName, "services");
}
```
Đi ngược từ thư mục chạy test lên cho tới khi thấy file `Ecommerce.slnx` (đánh dấu gốc repo) — nhờ vậy scanner chạy đúng như nhau dù gọi từ IDE, dòng lệnh, hay CI, bất kể thư mục làm việc hiện tại là gì.

## 1. `tests/StructureConventionTests` — mọi service phải tổ chức theo "capability", không theo lớp kỹ thuật

**Kiểm tra điều gì:** [`VerticalSliceStructureScanner.cs`](../../tests/StructureConventionTests/VerticalSliceStructureScanner.cs) quét mọi `services/*/src/*.Api/`, xem các thư mục con TRỰC TIẾP của nó có tên nào trùng danh sách cấm không:
```csharp
private static readonly string[] TechnicalLayerFolders = ["Controllers", "Services", "Repositories"];
```
Đây chính là quy tắc "vertical slice" (1 chức năng = 1 thư mục `Features/<Tên>/`) đã nhắc ở [02-orders-service-va-cac-api-endpoint.md](02-orders-service-va-cac-api-endpoint.md). Ví dụ vi phạm cụ thể: nếu ai đó tạo `services/orders/src/Orders.Api/Services/OrderPricingService.cs` (thay vì đặt logic đó trong `Features/Orders/`), scanner báo lỗi ngay: *"'Services/' organises code by technical role; a capability's handler and registration belong together under 'Features/<Capability>/'"*. Chú ý: chỉ cấm khi thư mục đó nằm **trực tiếp** dưới `*.Api/` — 1 thư mục tên `Services/` nằm lồng bên trong `Features/Orders/Services/` (phục vụ riêng 1 capability) không vi phạm.

Điểm đáng nhớ: **không có cách nào để 1 service "xin miễn trừ"** quy tắc này qua config/flag. Comment trong code giải thích: nếu 1 service thực sự cần kiến trúc phân lớp (layered) vì có domain logic phức tạp, điều đó phải được viết và review trong `plan.md` của spec đó — bắt buộc sửa trực tiếp vào code scanner (thêm ngoại lệ tường minh) thay vì có 1 cờ bật/tắt, để đó luôn là 1 quyết định được nhìn thấy khi review, không phải 1 cấu hình ai đó âm thầm bật.

**Cơ chế:** hoàn toàn tĩnh — chỉ `Directory.GetDirectories(...)` và so tên chuỗi, không dùng regex, không compile.

## 2. `tests/ContainerConventionTests` — Dockerfile phải copy đủ mọi thư viện `shared/` mà service dùng

**Kiểm tra điều gì:** [`DockerfileReferenceScanner.cs`](../../tests/ContainerConventionTests/DockerfileReferenceScanner.cs) đối chiếu 2 nguồn: (a) `.csproj` của mỗi service khai báo tham chiếu tới thư viện nào trong `shared/` (đọc bằng regex `ProjectReference Include="...shared/<tên>/...\.csproj"`), và (b) `Dockerfile` của chính service đó có dòng `COPY shared/<tên>/ shared/<tên>/` tương ứng hay không.

Câu chuyện đứng sau scanner này rất cụ thể — trích nguyên văn comment: *"Dự án này tồn tại vì 5 trong 6 image của service đã KHÔNG THỂ build suốt 2 tính năng liên tiếp. Mỗi Dockerfile copy `shared/ServiceDefaults`, và mọi service trừ gateway đã tham chiếu `shared/Tenancy` từ tính năng 003 — nhưng không image nào copy nó, và không ai để ý, vì không image nào được build lại kể từ đó."* Ví dụ vi phạm cụ thể: nếu `services/orders/src/Orders.Api/Orders.Api.csproj` thêm 1 `<ProjectReference>` tới `shared/EventContracts` nhưng quên sửa `Dockerfile`, lần build image kế tiếp sẽ fail ở bước `dotnet restore` bên trong container — vì file `.csproj` được copy vào image nhưng thư mục source `shared/EventContracts/` thì không, khiến restore không tìm thấy project được tham chiếu.

**Cơ chế:** tĩnh, dùng regex đọc cả `.csproj` lẫn `Dockerfile` dưới dạng text — comment trong code nói rõ lý do không build thật: *"build thì chậm, cần Docker daemon, và chỉ cho biết CÓ GÌ ĐÓ sai — cách này nói thẳng project nào đang thiếu."*

## 3. `tests/ContractCoverageTests` — 4 "ranh giới" hợp đồng Pact phải luôn đủ cả 2 phía

**Kiểm tra điều gì:** [`ContractCoverageScanner.cs`](../../tests/ContractCoverageTests/ContractCoverageScanner.cs) giữ 1 danh sách **viết cứng** (hardcode) đúng 4 "ranh giới" hợp đồng đã biết trong repo (BFF-products, BFF-baskets, BFF-orders, BasketCheckedOut — đã nhắc ở [05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md § Spec 011](05-giai-doan-3-hop-dong-api-va-ha-tang-kiem-thu.md#spec-011--hợp-đồng-giữa-2-service-chưa-từng-gọi-nhau-qua-http)), và với MỖI ranh giới, kiểm tra CẢ 2 file phải tồn tại: file hợp đồng đã commit (`pacts/bff-orders.json`) VÀ file test phía cung cấp đọc nó (`OrdersProviderPactTests.cs`).

Chi tiết thiết kế đáng chú ý nhất: danh sách 4 ranh giới này **không được tự động dò tìm** từ những file `pacts/*.json` đang có trên đĩa — nó là 1 hằng số viết tay trong code. Comment giải thích lý do: nếu danh sách được dò tự động, 1 ai đó lỡ **xoá** file `pacts/bff-orders.json` sẽ khiến scanner nghĩ "chỉ còn 3 ranh giới, đủ cả 3" — báo cáo **"phủ đầy đủ"** trong khi thực ra vừa mất 1 hợp đồng. Viết cứng nghĩa là thêm/bớt 1 ranh giới bắt buộc phải **sửa code này**, biến nó thành 1 quyết định được review, không phải hậu quả im lặng của việc xoá nhầm 1 file.

Ví dụ vi phạm: nếu file `pacts/orders-basketcheckedout.json` (đã thấy ở Tài liệu 05) bị xoá nhầm, hoặc `BasketCheckedOutProviderPactTests.cs` bị xoá mà không ai để ý, scanner báo: *"has a pact but no provider-side test reading it — 'baskets' would build green while the expectation went unchecked"*.

**Cơ chế:** tĩnh, chỉ `File.Exists(...)` trên 2 đường dẫn cố định mỗi ranh giới — không đọc nội dung file, không parse JSON.

## 4. `tests/CrossServiceIsolation.Tests` — 4 scanner khác nhau, gộp chung 1 dự án

Đây là dự án lớn nhất, gồm 4 scanner độc lập cho 4 quy tắc cô lập/bảo mật khác nhau — 2 cái đã nhắc rải rác ở tài liệu trước, 2 cái còn lại giới thiệu ở đây:

| Scanner | Kiểm tra gì | Ví dụ vi phạm |
|---|---|---|
| `ConnectionStringScanner` | Không service nào có connection string (`appsettings*.json`) trỏ vào database của service khác | `orders`'s `appsettings.json` có `ConnectionStrings:PartiesDb` |
| `TenantGatedConnectionScanner` (mới) | Mọi lời gọi `AddDbContext` trong `Program.cs` phải được canh giữ bởi `RequireTenantId()` trước khi mở kết nối (đã thấy cơ chế này ở [03-giai-doan-1-nen-tang-dich-vu-va-routing.md](03-giai-doan-1-nen-tang-dich-vu-va-routing.md)) | 1 `AddDbContext` mới được thêm mà thiếu dòng `serviceProvider.GetRequiredService<TenantContext>().RequireTenantId()` |
| `AuthenticatedByDefaultScanner` | Mọi service hướng ngoại gọi `AddIdentityValidation(`/`AddToggleGatedIdentity(` đúng 1 lần trong `Program.cs` | 1 service mới quên gọi dòng đăng ký xác thực |
| `AuthorizationPolicyDeclaredScanner` | Mọi route `Map(Get\|Post\|Put\|Delete\|Patch)` phải có `.RequireAuthorization(...)` hoặc `.AllowAnonymous()` đi kèm | 1 route mới thiếu cả 2 |

2 scanner cuối đã giải thích chi tiết ở [06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md § Mọi route nghiệp vụ tự khai báo phân quyền](06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md#mọi-route-nghiệp-vụ-tự-khai-báo-phân-quyền-spec-015) — không lặp lại ở đây. `TenantGatedConnectionScanner.cs` có 1 chi tiết thiết kế đáng nhớ: yêu cầu gốc (ticket) nói "mọi lời gọi DB/repository không yêu cầu tham số tenant" — nhưng research.md của spec đó lập luận cách đọc MẠNH HƠN: quét **đúng 1 điểm nghẽn** (`AddDbContext`) thay vì cố gắng grep mọi nơi tham số tenant có thể bị truyền sai, vì 1 tham số truyền xuyên suốt code vẫn compile được dù ai đó truyền sai giá trị, còn 1 `DbContext` không thể được tạo ra nếu chưa qua `RequireTenantId()` — "không có gì để bypass, nên không có gì để grep."

**Cơ chế:** cả 4 đều tĩnh (regex/chuỗi), không khởi động service hay Docker nào.

## 5. `tests/DeploymentManifestConventionTests` — mọi service phải khai báo đúng liveness/readiness probe (mới, spec 019)

**Kiểm tra điều gì:** khác hẳn 4 dự án trên (chỉ đọc file .NET/Docker tĩnh), dự án này kiểm tra 1 file **ngoài hệ sinh thái .NET hoàn toàn** — [`deploy/ansible/roles/service_deployment/templates/deployment.yaml.j2`](../../deploy/ansible/roles/service_deployment/templates/deployment.yaml.j2), template Jinja2 dùng để Ansible render ra Deployment manifest K8s thật cho cả 7 service. [`ProbeDeclarationTests.cs`](../../tests/DeploymentManifestConventionTests/ProbeDeclarationTests.cs) khẳng định (trích rút gọn):
```csharp
[Theory]
[MemberData(nameof(Services))]
public void AllServices_DeclareBothProbes(string serviceName)
{
    var container = SingleContainer(ManifestFixture.RenderAll(RepoRoot), serviceName);
    Assert.NotNull(container.LivenessProbe);
    Assert.NotNull(container.ReadinessProbe);
}
```
Chi tiết đáng nhớ: có 1 test riêng khẳng định **liveness KHÔNG BAO GIỜ trỏ vào path readiness** (`/health/ready`) — kể cả với 5 service có database. Lý do (đã có nền tảng khái niệm ở [01](01-tong-quan-kien-truc.md#5-gateway-là-ngoại-lệ--vì-sao-nó-không-tự-gọi-addidentityvalidation) và [Tài liệu 03](03-giai-doan-1-nen-tang-dich-vu-va-routing.md) về `/health/live` vs `/health/ready`): nếu liveness dùng nhầm path readiness, 1 lần database chỉ tạm chậm (không phải service chết) sẽ khiến K8s **restart nhầm cả Pod** thay vì chỉ tạm rút nó khỏi load balancer — biến 1 sự cố tạm thời của dependency thành downtime thật của chính service. Test `ReadinessDefaults_DifferByDependencyGroup` cũng khẳng định service có database (`orders`) phải có `failureThreshold` readiness **cao hơn** service không trạng thái (`gateway`) — tái dùng đúng ngưỡng chịu đựng thời gian phục hồi của SQL Server đã thấy ở `docker-compose.yml` ([01](01-tong-quan-kien-truc.md)), không phải 1 con số tự nghĩ ra.

**Cơ chế — KHÁC HẲN 4 dự án kia:** không chỉ đọc text — [`ManifestFixture.cs`](../../tests/DeploymentManifestConventionTests/ManifestFixture.cs) thật sự **tự render** template Jinja2 đó (1 bộ render Jinja2 tối giản viết bằng C#, `ProbeTemplateRenderer.cs`, mô phỏng đúng cách Ansible sẽ thay biến), ghép biến từ [`deploy/ansible/roles/service_deployment/defaults/main.yml`](../../deploy/ansible/roles/service_deployment/defaults/main.yml) (giá trị mặc định theo nhóm — có DB hay không) + [`inventories/services.yml`](../../deploy/ansible/inventories/services.yml) (override riêng từng service nếu có), rồi parse kết quả YAML render ra thành `DeploymentManifest` để assert. Comment gốc: *"đây là nơi DUY NHẤT 1 thay đổi hình dạng của 1 trong 2 file YAML kia phải được phản ánh lại — cùng triết lý `DockerfileReferenceScanner` là nơi duy nhất biết hình dạng dòng `COPY` của Dockerfile."*

**Có 1 lớp kiểm tra THỨ HAI, độc lập, không phải C#:** stage Jenkins `deployment manifest lint` (xem mục 7) chạy [`scripts/ci/lint-deployment-manifests.sh`](../../scripts/ci/lint-deployment-manifests.sh) — dùng **Ansible thật** (`ansible-playbook --check --diff`, không chạm cluster nào) để render, rồi **kubeconform** để validate kết quả khớp đúng OpenAPI schema thật của Kubernetes. 2 lớp bổ sung cho nhau: `DeploymentManifestConventionTests` (C#, nhanh, không cần cài Ansible) kiểm tra ĐÚNG QUY ƯỚC nghiệp vụ (2 probe, path đúng, ngưỡng theo nhóm dependency); `lint-deployment-manifests.sh` (chậm hơn, cần Ansible + kubeconform) kiểm tra bản render THẬT có hợp lệ với K8s hay không — 1 lỗi cú pháp YAML tinh vi có thể lọt qua C# renderer tự viết nhưng không lọt qua Ansible/kubeconform thật.

## 6. Chạy ở đâu trong build/CI — đã xác minh trực tiếp, không suy đoán

Tôi đã chạy đúng logic phân loại của [`scripts/ci/run-dotnet-tests.sh`](../../scripts/ci/run-dotnet-tests.sh) (dựa vào TÊN project, không phải nội dung) trên tên thật của 5 project:
```
contract  (khớp *ContractTests.csproj):      (none)
integration (khớp *IntegrationTest*):         (none)
unit (còn lại):  ContainerConventionTests.csproj, ContractCoverageTests.csproj,
                 CrossServiceIsolation.Tests.csproj, StructureConventionTests.csproj,
                 DeploymentManifestConventionTests.csproj
```
Cả 5 dự án đều rơi vào tier **`unit`** — không phải vì chúng "giống unit test" về bản chất, mà thuần tuý vì tên file không khớp 2 pattern kia. Cả 5 cũng có mặt trong [`Ecommerce.slnx`](../../Ecommerce.slnx) (đã kiểm tra trực tiếp), nên được compile ở stage `build`, rồi được `scripts/ci/run-dotnet-tests.sh unit` phát hiện và chạy ở stage `unit tests` của [`Jenkinsfile`](../../Jenkinsfile) — stage này **không** nằm sau điều kiện `when` nào, nên luôn chạy trên mọi lần build.

## 7. Thất bại chặn gì — phần cần xác nhận, không suy đoán

Đây là phần tôi **không thể tự xác minh chỉ bằng đọc code**, và bạn đã xác nhận trực tiếp, nên trình bày đúng như vậy thay vì như 1 sự thật tôi tự kiểm chứng:

- **Trong file Jenkinsfile hiện tại** (đã đọc lại toàn văn — pipeline giờ có **9 stage**, không còn 5 như lúc phần này viết lần đầu, do 2 spec mới merge sau đó thêm vào: `018-cluster-secret-store` thêm `secret scan`/`image secret scan`; `019-liveness-readiness-probes` thêm `deployment manifest lint`): `CI_FAST_ITERATION = 'true'` làm **4** stage bị `when` chặn không chạy — `integration tests`, `contract tests`, `deployment manifest lint`, `sonarqube quality gate`. Hai stage mới `secret scan`/`image secret scan` **không** nằm sau điều kiện này — comment trong code nói rõ lý do: *"neither needs Docker/Testcontainers, so skipping them buys no iteration speed, and a secret-scanning gate that is sometimes off defeats its own purpose"* — tức 2 stage quét bí mật này được cố tình thiết kế để LUÔN chạy, không phụ thuộc cờ tăng tốc CI.
- **[Theo bạn xác nhận — ở lần đọc trước]** Bạn đã gỡ cả 5 required status check GỐC khỏi branch protection. Tôi **chưa hỏi lại** liệu điều đó có còn đúng cho 2 check mới (`ci/secret-scan`, `ci/image-secret-scan`) — 2 check này ra đời SAU lần bạn xác nhận, nên tôi không có cơ sở khẳng định chúng có đang là required check hay không; đây là điểm cần bạn xác nhận riêng nếu muốn biết chắc.
- Stage `deployment manifest lint` (mục 5) **không publish check GitHub nào cả** — không gọi `checkStarted`/`checkPassed` như 5 stage kia. Comment gốc: *"Not wired into the required-check contract... but a failure here still fails this stage and therefore the build, the same way any other `sh` step does"* — nghĩa là nó vẫn có thể làm **cả pipeline Jenkins đỏ** (build fail), nhưng không xuất hiện thành 1 status check riêng biệt trên PR GitHub như 5+2 cái kia.
- Tôi vẫn không có `gh` CLI hay phiên trình duyệt đã đăng nhập GitHub trong môi trường này để tự kiểm chứng cấu hình branch protection sống — mọi điều ở trên về trạng thái GitHub vẫn chỉ dựa trên xác nhận trực tiếp của bạn từ trước, không phải tôi tự xác minh lại.

## Bảng tổng kết — thứ tự thực chạy trong pipeline (đã xác minh trực tiếp, cập nhật 9 stage)

| # | Tên | Thuộc dự án | Stage Jenkins | Bị `CI_FAST_ITERATION` chặn? | Publish check GitHub? |
|---|---|---|---|---|---|
| 0 | `sonarqube: begin analysis` | — | `sonarqube: begin analysis` | **Có** (skip) | `ci/sonarqube-quality-gate` (pending) |
| 1 | `dotnet build` (biên dịch, gồm cả 5 dự án dưới) | — | `build` | Không | `ci/build` |
| 2 | gitleaks (toàn bộ lịch sử git) | — | `secret scan` | **Không** (luôn chạy) | `ci/secret-scan` |
| 3 | Trivy (image đã build) | — | `image secret scan` | **Không** (luôn chạy) | `ci/image-secret-scan` |
| 4 | `VerticalSliceStructureScanner` | `StructureConventionTests` | `unit tests` | Không | `ci/unit-tests` |
| 4 | `DockerfileReferenceScanner` | `ContainerConventionTests` | `unit tests` | Không | `ci/unit-tests` |
| 4 | `ContractCoverageScanner` | `ContractCoverageTests` | `unit tests` | Không | `ci/unit-tests` |
| 4 | `ConnectionStringScanner` | `CrossServiceIsolation.Tests` | `unit tests` | Không | `ci/unit-tests` |
| 4 | `TenantGatedConnectionScanner` | `CrossServiceIsolation.Tests` | `unit tests` | Không | `ci/unit-tests` |
| 4 | `AuthenticatedByDefaultScanner` | `CrossServiceIsolation.Tests` | `unit tests` | Không | `ci/unit-tests` |
| 4 | `AuthorizationPolicyDeclaredScanner` | `CrossServiceIsolation.Tests` | `unit tests` | Không | `ci/unit-tests` |
| 4 | `ProbeDeclarationTests`/`RolloutStrategyTests`/`ServiceInventoryTests`/`LivenessRestartTests` (render Jinja2 mô phỏng bằng C#) | `DeploymentManifestConventionTests` | `unit tests` | Không | `ci/unit-tests` |
| 5 | Integration tests (Testcontainers) | `services/*/tests/*.IntegrationTests` | `integration tests` | **Có** (skip) | `ci/integration-tests` |
| 6 | Contract tests (Pact) | `services/*/tests/*.ContractTests` | `contract tests` | **Có** (skip) | `ci/contract-tests` |
| 7 | `ansible-lint` + render Ansible thật + `kubeconform` | `deploy/ansible/` | `deployment manifest lint` | **Có** (skip) | *(không có — xem mục trên)* |
| 8 | Phân tích + Quality Gate SonarQube | toàn repo | `sonarqube quality gate` | **Có** (skip) | `ci/sonarqube-quality-gate` |

Xếp hạng "#4" của 8 scanner ngang nhau vì `dotnet test` chạy TOÀN BỘ project trong tier `unit` — thứ tự thật giữa chúng phụ thuộc thứ tự `find` liệt kê file trên đĩa của máy CI tại thời điểm chạy (không cố định, không có ý nghĩa ưu tiên nào được thiết kế). Đây không phải điểm tôi suy đoán — đọc thẳng [`run-dotnet-tests.sh`](../../scripts/ci/run-dotnet-tests.sh): script gọi `dotnet test` tuần tự cho từng project trong biến `$projects` (kết quả của `find . -name '*Tests.csproj' | ... | sort`), nên thứ tự thật là theo **tên project sắp xếp bảng chữ cái** (`sort`) — nếu bạn cần thứ tự chính xác 100% cho 1 lần chạy CI cụ thể, cách chắc chắn nhất là đọc log Jenkins của lần chạy đó, tôi không có quyền truy cập log CI thật từ đây.

## Đi đâu tiếp theo

- [06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md](06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md) — bối cảnh đầy đủ của SonarQube/Jenkinsfile/branch protection được xây ban đầu.
- [11-trien-khai-k8s-va-secret-store.md](11-trien-khai-k8s-va-secret-store.md) — `deploy/k8s/`, `deploy/ansible/`, và `RequiredSecretsValidation.cs` đứng sau dự án test thứ 5 ở tài liệu này.
- `scripts/ci/setup-branch-protection.sh` — script định nghĩa cấu hình 5-check gốc; chạy lại nếu muốn khôi phục quality gate làm merge blocker (chưa tính 2 check `secret-scan`/`image-secret-scan` mới, xem mục 7).
