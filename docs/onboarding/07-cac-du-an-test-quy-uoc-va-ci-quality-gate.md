# 07 — Các dự án test quy ước & CI quality gate

> Đọc [01-tong-quan-kien-truc.md](01-tong-quan-kien-truc.md) trước nếu chưa quen khái niệm .NET cơ bản. Tài liệu này giải thích 4 dự án nằm trong thư mục `tests/` ở gốc repo — khác hẳn `services/*/tests/` (test của riêng 1 service) — và cách chúng vận hành trong build/CI.
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

## 5. Chạy ở đâu trong build/CI — đã xác minh trực tiếp, không suy đoán

Tôi đã chạy đúng logic phân loại của [`scripts/ci/run-dotnet-tests.sh`](../../scripts/ci/run-dotnet-tests.sh) (dựa vào TÊN project, không phải nội dung) trên tên thật của 4 project:
```
contract  (khớp *ContractTests.csproj):      (none)
integration (khớp *IntegrationTest*):         (none)
unit (còn lại):  ContainerConventionTests.csproj, ContractCoverageTests.csproj,
                 CrossServiceIsolation.Tests.csproj, StructureConventionTests.csproj
```
Cả 4 dự án đều rơi vào tier **`unit`** — không phải vì chúng "giống unit test" về bản chất, mà thuần tuý vì tên file không khớp 2 pattern kia. Cả 4 cũng có mặt trong [`Ecommerce.slnx`](../../Ecommerce.slnx) (đã kiểm tra trực tiếp), nên được compile ở stage `build`, rồi được `scripts/ci/run-dotnet-tests.sh unit` phát hiện và chạy ở stage `unit tests` của [`Jenkinsfile`](../../Jenkinsfile) — 2 stage này **không** nằm sau điều kiện `when` nào, nên luôn chạy trên mọi lần build (khác với 3 stage integration/contract/SonarQube — xem mục dưới).

## 6. Thất bại chặn gì — phần cần xác nhận, không suy đoán

Đây là phần tôi **không thể tự xác minh chỉ bằng đọc code**, và bạn đã xác nhận trực tiếp, nên trình bày đúng như vậy thay vì như 1 sự thật tôi tự kiểm chứng:

- **Trong file Jenkinsfile** (dòng 87): `CI_FAST_ITERATION = 'true'` hiện đang làm 3/5 stage (`integration tests`, `contract tests`, `sonarqube quality gate`) bị `when` chặn không chạy — đây là sự thật đọc trực tiếp từ code, không ảnh hưởng tới 4 dự án trong tài liệu này (chúng chạy ở stage `unit tests`, không bị chặn).
- **[Theo bạn xác nhận]** Trên GitHub, bạn đã **gỡ cả 5 required status check** khỏi cấu hình branch protection để không làm chậm các PR mới — toàn bộ source code (`Jenkinsfile`, `scripts/ci/setup-branch-protection.sh`) vẫn giữ nguyên thiết kế "5 check bắt buộc", nhưng thiết kế đó **hiện không còn được GitHub thực thi**. Branch protection vẫn tồn tại (không bị tắt hoàn toàn) — chỉ riêng phần yêu cầu status check (quality gate) bị gỡ.
- **Hệ quả thực tế:** thất bại của 4 dự án trong tài liệu này (hay bất kỳ stage nào khác) hiện **không tự động chặn merge PR** trên GitHub nữa — Jenkins vẫn có thể chạy và báo đỏ, nhưng đó chỉ còn là tín hiệu để người review tự nhìn vào, không phải rào chặn tự động như thiết kế gốc của spec `013-sonarqube-merge-blocker` (`enforce_admins: true`, `required_status_checks.contexts` liệt kê đủ 5 tên trong `scripts/ci/setup-branch-protection.sh`).
- Tôi đã thử mở trực tiếp `github.com/nmhieuit/ecommerce/settings/branches` bằng trình duyệt để tự xác nhận, nhưng phiên trình duyệt trong môi trường này **chưa đăng nhập GitHub** (thấy nút "Sign in" ở góc phải) nên không truy cập được trang cấu hình riêng tư đó — không có bằng chứng độc lập nào khác ngoài xác nhận trực tiếp của bạn ở trên.

## Bảng tổng kết — thứ tự thực chạy trong pipeline (đã xác minh trực tiếp)

| # | Tên | Thuộc dự án | Stage Jenkins | Bị `CI_FAST_ITERATION` chặn? |
|---|---|---|---|---|
| 1 | `dotnet build` (biên dịch, gồm cả 4 dự án dưới) | — | `build` | Không |
| 2 | `VerticalSliceStructureScanner` | `StructureConventionTests` | `unit tests` | Không |
| 2 | `DockerfileReferenceScanner` | `ContainerConventionTests` | `unit tests` | Không |
| 2 | `ContractCoverageScanner` | `ContractCoverageTests` | `unit tests` | Không |
| 2 | `ConnectionStringScanner` | `CrossServiceIsolation.Tests` | `unit tests` | Không |
| 2 | `TenantGatedConnectionScanner` | `CrossServiceIsolation.Tests` | `unit tests` | Không |
| 2 | `AuthenticatedByDefaultScanner` | `CrossServiceIsolation.Tests` | `unit tests` | Không |
| 2 | `AuthorizationPolicyDeclaredScanner` | `CrossServiceIsolation.Tests` | `unit tests` | Không |
| 3 | Integration tests (Testcontainers) | `services/*/tests/*.IntegrationTests` | `integration tests` | **Có** (hiện đang skip) |
| 4 | Contract tests (Pact) | `services/*/tests/*.ContractTests` | `contract tests` | **Có** (hiện đang skip) |
| 5 | Phân tích + Quality Gate SonarQube | toàn repo | `sonarqube quality gate` | **Có** (hiện đang skip) |

Xếp hạng "#2" của 6 scanner ngang nhau vì `dotnet test` chạy TOÀN BỘ project trong tier `unit` — thứ tự thật giữa chúng phụ thuộc thứ tự `find` liệt kê file trên đĩa của máy CI tại thời điểm chạy (không cố định, không có ý nghĩa ưu tiên nào được thiết kế). Đây không phải điểm tôi suy đoán — đọc thẳng [`run-dotnet-tests.sh`](../../scripts/ci/run-dotnet-tests.sh): script gọi `dotnet test` tuần tự cho từng project trong biến `$projects` (kết quả của `find . -name '*Tests.csproj' | ... | sort`), nên thứ tự thật là theo **tên project sắp xếp bảng chữ cái** (`sort`) — nếu bạn cần thứ tự chính xác 100% cho 1 lần chạy CI cụ thể, cách chắc chắn nhất là đọc log Jenkins của lần chạy đó, tôi không có quyền truy cập log CI thật từ đây.

## Đi đâu tiếp theo

- [06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md](06-giai-doan-4-chat-luong-bao-mat-xac-thuc.md) — bối cảnh đầy đủ của SonarQube/Jenkinsfile/branch protection được xây ban đầu.
- `scripts/ci/setup-branch-protection.sh` — script định nghĩa cấu hình 5-check gốc; chạy lại nếu muốn khôi phục quality gate làm merge blocker.
