# ADR-0012: Thực thi cổng chất lượng CI (CI Quality Gate Enforcement)

*(Bản dịch tiếng Việt của
[`0012-ci-quality-gate-enforcement.md`](0012-ci-quality-gate-enforcement.md) — bản gốc tiếng Anh vẫn
được giữ nguyên, không sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn đúng.)*

**Trạng thái:** Đã chấp thuận | **Ngày:** 2026-08-22 | **Người quyết định:** Platform maintainers

## Bối cảnh

Mục Development Workflow của constitution yêu cầu mọi pull request phải qua build → unit test →
integration test (Testcontainers) → contract test → cổng chất lượng SonarQube → quét lỗ hổng image
container, "không ngoại lệ, không override, không miễn trừ", và Principle III gọi cổng chất lượng
SonarQube là cơ quan có thẩm quyền về coverage phải pass trước khi merge.

Cho tới quyết định này, không điều nào ở trên tồn tại dưới dạng phần mềm đang chạy thật. ADR 0001,
0008, và 0010 nhắc tới "pipeline Jenkins" như 1 thực thể được giả định có sẵn, nhưng chưa từng có
`Jenkinsfile`, chưa từng có cấu hình SonarQube, và chưa từng có branch protection nào được tạo — cổng
kiểm soát chỉ là 1 quy tắc viết ra giấy mà không có gì thực thi nó. ADR này bao phủ việc dựng pipeline
CI thực sự đầu tiên cho repository và, quan trọng hơn, chọn cơ chế khiến kết quả của nó trở thành 1
**điểm chặn merge** thay vì chỉ là lời khuyên.

Có 2 điều cần quyết định: pipeline chạy và báo cáo như thế nào, và điều gì khiến 1 kết quả fail thực
sự chặn được merge cho tất cả mọi người, kể cả những người quản trị repository.

## Quyết định

**1 `Jenkinsfile` khai báo ở gốc repository chạy như 1 job Jenkins Multibranch Pipeline, publish 1
check GitHub có tên riêng cho mỗi stage; branch protection của GitHub liệt kê các tên check đó như
các required status check với `enforce_admins` được bật.**

Cụ thể:

- 5 stage publish các check `ci/build`, `ci/unit-tests`, `ci/integration-tests`,
  `ci/contract-tests`, và `ci/sonarqube-quality-gate`. Các tên này được publish tường minh qua GitHub
  Checks plugin thay vì kế thừa từ tên stage của Jenkins, vì branch protection khớp theo đúng chuỗi
  ký tự đó.
- Stage SonarQube gọi `waitForQualityGate()` bên trong 1 `timeout()` và biến bất kỳ trạng thái không
  phải `OK` nào — cùng chính bản thân việc timeout — thành 1 lỗi stage. CLI của scanner thoát thành
  công ngay khi kết quả phân tích được *tải lên*; cổng kiểm soát được tính toán bất đồng bộ sau đó,
  nên chỉ riêng mã thoát của scanner sẽ để lọt 1 cổng kiểm soát đang fail.
- Branch protection được áp dụng bằng 1 hành động của quản trị viên chỉ làm 1 lần, viết thành script
  `scripts/ci/setup-branch-protection.sh`, không được quản lý như infrastructure-as-code.

## Các phương án đã cân nhắc

### Phương án A: Multibranch Pipeline + required status check với admin bypass bị vô hiệu hoá (đã chọn)

| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Thấp — 1 `Jenkinsfile`, 1 job, 1 thay đổi cấu hình |
| Code mới cần bảo trì | Không có gì ngoài chính pipeline |
| Độ mạnh của việc thực thi | Tuyệt đối — GitHub vô hiệu hoá điều khiển merge cho mọi vai trò |
| Khả năng audit | Chính audit log của tổ chức trên GitHub ghi lại mọi thay đổi với cài đặt này |

**Ưu điểm:** Dùng các tính năng nền tảng hạng nhất, có audit, xuyên suốt từ đầu tới cuối; GitHub
Branch Source plugin tự động phát hiện các nhánh pull request và tự kích hoạt lại ở mỗi lần push mà
không cần code webhook nào của riêng chúng ta; `enforce_admins` là công tắc duy nhất thực sự xoá bỏ
đường vòng (override) mà constitution cấm. **Nhược điểm:** Việc thực thi nằm trong cài đặt của
repository chứ không phải trong repository, nên nó vô hình với `git log` — người review không thể
thấy nó vẫn còn hiệu lực bằng cách đọc 1 diff.

### Phương án B: Pipeline + branch protection được quản lý như infrastructure-as-code (Terraform GitHub provider)

| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Trung bình-Cao — 1 stack IaC mới, state backend, và pipeline apply |
| Code mới cần bảo trì | 1 cấu hình Terraform và credential của nó |
| Độ mạnh của việc thực thi | Giống hệt Phương án A (nó cấu hình đúng cùng 1 cài đặt) |
| Khả năng audit | Tốt hơn — thay đổi cài đặt xuất hiện dưới dạng diff có thể review |

**Ưu điểm:** Khép đúng điểm yếu của Phương án A: cài đặt bảo vệ trở thành 1 artifact có thể review,
có version, và sự trôi dạt (drift) phát hiện được bằng 1 lượt chạy plan. **Nhược điểm:** Đưa vào cả
1 toolchain IaC, kho lưu trạng thái (state storage) của nó, và cách xử lý credential của nó chỉ để
quản lý cài đặt của 1 repository duy nhất — hạ tầng không tương xứng với vấn đề (Principle I), và
stack mới đó tự nó sẽ cần 1 pipeline và 1 người sở hữu riêng.

### Phương án C: 1 bot kiểm tra merge tự xây (GitHub App hoặc Actions workflow) thực thi quy tắc bằng code

| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Cao — 1 service phải viết, host, bảo mật, và giám sát |
| Code mới cần bảo trì | 1 bot cùng credential và câu chuyện sẵn sàng của nó |
| Độ mạnh của việc thực thi | Yếu hơn — 1 bot bị sập sẽ chặn tất cả hoặc không chặn gì cả |
| Khả năng audit | Chỉ những gì chính bot đó chọn để ghi log |

**Ưu điểm:** Logic thực thi hiển thị được trong repository, và các quy tắc tuỳ ý đều diễn đạt được.
**Nhược điểm:** Cài lại 1 tính năng GitHub vốn đã cung cấp đúng đắn (required status check); thêm 1
thành phần mà chính sự cố ngừng hoạt động của nó trở thành 1 sự cố ngừng merge; và đường vòng
(bypass) của nó là bất cứ điều gì mà code và quyền token của nó tình cờ cho phép, đây là 1 đảm bảo
yếu hơn 1 cài đặt của nền tảng.

## Phân tích đánh đổi

Câu hỏi mang tính quyết định không phải là phương án nào thực thi được quy tắc — cả 3 đều cấu hình
hoặc mô phỏng lại đúng cùng 1 điểm chặn — mà là mỗi phương án tốn bao nhiêu để giữ được sự đáng tin
cậy. Phương án C thua hoàn toàn: viết 1 service để nhân đôi 1 tính năng nền tảng đã có sẵn thêm vào 1
kiểu sự cố ngừng hoạt động và 1 bề mặt đường vòng tinh vi hơn chính cái nó thay thế.

Giữa A và B, đánh đổi thực sự là sự vô hình của Phương án A so với gánh nặng của Phương án B. Lợi thế
của Phương án B là thật: 1 thay đổi cài đặt âm thầm bật lại đường vòng là kiểu lỗi duy nhất mà tính
năng này không thể phát hiện được từ bên trong repository. Điều này được chấp nhận vì audit log của tổ
chức trên GitHub đã ghi lại thay đổi đó (FR-005 của spec yêu cầu audit như phương án dự phòng, không
phải để ngăn chặn bằng 1 cơ chế thứ 2), và vì 1 stack IaC được đưa vào chỉ để quản lý cài đặt của 1
repository sẽ cần nhiều quản trị hơn giá trị nó mang lại. Nếu cài đặt bảo vệ từng cần thiết trên nhiều
repository, quyết định này nên được xem lại như 1 tu chính thay vì tự quản lý thủ công từng cái.

1 đánh đổi nhỏ hơn nhưng sắc bén nằm trong Phương án A: SonarScanner for .NET nhận cài đặt dưới dạng
tham số dòng lệnh `/d:` và, khác với CLI scanner tổng quát, không đọc `sonar-project.properties`.
Việc tách cài đặt scanner giữa 1 file properties và `Jenkinsfile` sẽ để lại 2 nơi phải sửa cho 1 thứ,
nên `scripts/ci/sonar-begin.sh` dịch file properties đã commit thành tham số scanner và file
properties vẫn là nguồn sự thật duy nhất.

## Hệ quả

- **Tên stage là 1 hợp đồng, không phải 1 nhãn.** Đổi tên check của 1 stage trong `Jenkinsfile` mà
  không cập nhật branch protection sẽ âm thầm loại stage đó khỏi việc thực thi — 1 lỗi trông giống
  như 1 PR xanh. Các tên được liệt kê tại
  `specs/012-sonarqube-quality-gate/contracts/pipeline-stage-contract.md` §1 và phải được đổi ở cả 2
  nơi trong cùng 1 pull request.
- **Agent Jenkins có thêm các yêu cầu cứng**: SDK .NET 10, Node 22 kèm corepack, và 1 Docker daemon
  truy cập được, vì tầng integration chạy Testcontainers (spec 010).
- **Cổng chất lượng fail theo hướng đóng (fail closed).** 1 server SonarQube không truy cập được hoặc
  chậm sẽ timeout và làm fail check thay vì pass hoặc bỏ qua, nên downtime của SonarQube chặn merge.
  Đây là hướng lỗi có chủ đích, và nó khiến độ sẵn sàng của SonarQube trở thành 1 dependency trên
  đường đi của merge.
- **Việc phát hiện test dựa trên đĩa, không liệt kê thủ công.** `scripts/ci/run-dotnet-tests.sh` phân
  loại mọi `*Tests.csproj` vào 1 tầng theo tên, nên bộ test của 1 service mới tự động gia nhập CI mà
  không cần sửa pipeline. 1 project test đặt tên ngoài quy ước sẽ chạy ở tầng unit theo mặc định.
- **Branch protection không hiển thị trong repository.** Xác nhận cổng kiểm soát vẫn còn hiệu lực
  nghĩa là phải đọc cài đặt GitHub hoặc audit log, không phải đọc diff.

## Việc cần làm

1. [ ] Chạy `scripts/ci/setup-branch-protection.sh <owner/repo> <branch>` với tư cách quản trị viên
   repository và xác nhận cả 5 check đều là bắt buộc với `enforce_admins` được bật
2. [ ] Tạo job Jenkins Multibranch Pipeline và kết nối server SonarQube (kèm webhook trở lại Jenkins
   mà `waitForQualityGate()` phụ thuộc vào), theo mục Prerequisites của
   `specs/012-sonarqube-quality-gate/quickstart.md`
3. [ ] Verify 5 kịch bản trong `specs/012-sonarqube-quality-gate/quickstart.md` với các pull request
   thật, bao gồm cả hành vi fail-closed khi SonarQube không truy cập được
4. [ ] SCRUM-TBD: thêm quét lỗ hổng image container của constitution như stage thứ 6 và check bắt
   buộc thứ 6; cho tới lúc đó pipeline này chỉ hiện thực 5 trên 6 cổng kiểm soát bắt buộc (xem
   `specs/012-sonarqube-quality-gate/plan.md` Complexity Tracking)

## Bổ sung (2026-08-23): Quyết định backend phân tích (FR-005, FR-009, FR-014)

`spec.md` đã được mở rộng để gộp vào 1 quyết định trước đây tách riêng — check
`ci/sonarqube-quality-gate` ở trên thực sự báo cáo về backend phân tích nào — vì 1 cổng kiểm soát nối
vào hư không thì chưa thực thi được điều gì cả. Mục này chính là bản ghi quyết định đó.

**Quyết định: SonarQube tự host (Community Edition), không dùng SonarCloud.**

| Khía cạnh | SonarQube tự host | SonarCloud (SaaS) |
|---|---|---|
| Chi phí định kỳ | Không có (Community Edition) ngoài chi phí compute cho hosting | Cần gói trả phí cho 1 repository **private** |
| Gánh nặng hosting/bảo trì | Nền tảng tự lo nâng cấp, backup, uptime — nhất quán với mọi thành phần khác trong repo này, vốn chạy tự host trên Kubernetes qua Ansible | Không có — do nhà cung cấp vận hành |
| Lộ metadata source | Ở lại bên trong mạng nội bộ | Metadata source rời khỏi mạng nội bộ tới 1 bên thứ 3 |
| Trang trí PR trên GitHub | **Không có sẵn** trong Community Edition; cần Branch Plugin do cộng đồng duy trì (không chính thức, phải cài lại mỗi lần nâng cấp SonarQube) hoặc bản quyền Developer Edition trả phí | Có sẵn native ở mọi bậc |

**Lý do**: `nmhieuit/ecommerce` là 1 repository private, nên bậc miễn phí của SonarCloud không áp
dụng — chi phí SaaS định kỳ là yếu tố quyết định để loại nó, trên 1 nền tảng có mẫu hình đã tuyên bố
(constitution, ADR 0001/0008/0010) là tự host mọi thứ. Đánh đổi được chấp nhận để lấy điều đó là bản
quyền: Community Edition không có trang trí PR chính thức, nên pipeline này hoặc chạy Branch Plugin
cộng đồng hoặc chấp nhận rằng metric nằm trên server SonarQube mà không được trang trí lên PR cho tới
khi plugin đó được cài và verify (FR-009). Điều này được ghi lại ở đây thay vì âm thầm giả định, theo
FR-009.

**Trạng thái cấp phát tính tới 2026-08-23**: chưa có instance SonarQube tự host nào tồn tại ở bất kỳ
đâu cho repository này trước ngày này. 1 instance cục bộ (SonarQube Community Edition + Jenkins LTS)
đã được dựng trong Docker Desktop qua `docker-compose.ci.yml` ở gốc repository, để mở đường cho việc
nối Jenkins↔SonarQube và cấu hình credential/webhook GitHub được mô tả trong `quickstart.md`. Đây là
1 **instance phát triển**, không phải bản triển khai production được ngụ ý bởi mục Edge Cases của
spec (1 workload Kubernetes với database riêng, cấp phát qua mẫu hình Ansible sẵn có của nền tảng) —
việc dựng instance production đó vẫn là 1 việc theo dõi riêng, gắn với FR-006. Instance cục bộ đủ để
verify việc nối Jenkins↔SonarQube↔GitHub từ đầu tới cuối; bản thân nó không phải là "instance đã cấp
phát" mà FR-006 mô tả cho việc dùng ở production.

## Bổ sung (2026-08-27): Branch protection bị chặn bởi gói GitHub, không phải bởi cấu hình

Được verify lại trong lúc lên kế hoạch cho `specs/013-sonarqube-merge-blocker` (spec kế thừa của
`012-sonarqube-quality-gate`, đã bị xoá sau khi quyết định của ADR này đã ra mắt dưới dạng code
pipeline hoạt động thật). 2 điều đã đổi kể từ bản bổ sung ở trên, 1 điều tệ hơn và 1 điều tốt hơn.

**Khoảng trống trang trí PR ở trên đã được khép lại.** Kiểm tra trực tiếp instance SonarQube cục bộ
đang chạy xác nhận SonarQube Community Branch Plugin đã được cài đặt
(`sonarqube-extensions/plugins/sonarqube-community-branch-plugin-26.5.0.jar`) và được nạp
(`web.log`: `Loaded core extensions: Community Branch Plugin`). Lựa chọn "chạy Branch Plugin cộng
đồng hoặc chấp nhận metric không được trang trí" từ bản bổ sung ngày 2026-08-23 đã được thực hiện
trên thực tế: plugin cộng đồng, không tốn chi phí định kỳ, nhất quán với phần còn lại của quyết định
này.

**Branch protection — cơ chế mà toàn bộ ADR này nói về — hiện không thể bật được.**
`scripts/ci/setup-branch-protection.sh`, chạy trên `nmhieuit/ecommerce`, nhận HTTP 403 "Upgrade to
GitHub Pro or make this repository public to enable this feature" cho cả branch protection kiểu cổ
điển lẫn API repository ruleset mới hơn, bất kể quyền admin của token. Đây là 1 **giới hạn của gói
GitHub**, không phải 1 lỗi trong script hay 1 vấn đề về quyền: GitHub không cung cấp protected branch
trên 1 repository private ở gói miễn phí, cho bất kỳ tài khoản nào. Cho tới khi việc này được giải
quyết, `enforce_admins` — "công tắc duy nhất thực sự xoá bỏ đường vòng mà constitution cấm" (mục
Quyết định, ở trên) — không thể bật được, và cả 5 check `ci/*` mà ADR này mô tả vẫn chỉ mang tính
khuyến nghị, đúng chính kết quả mà Phương án A được chọn để tránh.

**Khuyến nghị: nâng cấp tài khoản lên GitHub Pro, giữ repository ở chế độ private.** Việc chuyển
repository sang public cũng sẽ xoá lỗi 403, nhưng nó mở lại đúng đánh đổi chi phí mà ADR này đã giải
quyết ở bản bổ sung trên — bậc miễn phí của SonarCloud đã bị loại chính vì `nmhieuit/ecommerce` là
private, và tự host được chọn để giữ sự riêng tư đó mà không tốn chi phí định kỳ. Đánh đổi sự riêng tư
để lấy 1 bậc miễn phí *khác* hoạt động sẽ xoá bỏ quyết định đó để đổi lấy 1 khoản tiết kiệm nhỏ hơn
(GitHub Pro là 1 khoản phí cố định, thấp, theo từng ghế) so với chi phí SaaS mà nó vốn đang tránh. Đây
là 1 thay đổi có trả phí, ở cấp tài khoản: theo quy tắc vận hành của môi trường này, 1 phiên tự động
không mua dịch vụ hay thay đổi cài đặt tài khoản/thanh toán, nên việc nâng cấp gói và chạy lại
`scripts/ci/setup-branch-protection.sh nmhieuit/ecommerce master` được ghi lại ở đây như bước còn lại
chính xác dành cho chủ sở hữu repository, không phải do phiên này thực hiện.

Cho tới khi việc nâng cấp đó xảy ra, mục "Quyết định" của ADR này mô tả việc thực thi *dự định* của
pipeline, đã được verify hoạt động từ đầu tới cuối trên instance Jenkins/SonarQube cục bộ, nhưng chưa
phải 1 điểm chặn merge thật trên `github.com/nmhieuit/ecommerce`. Xem
`specs/013-sonarqube-merge-blocker/research.md` (Quyết định 7) và `tasks.md` (T008–T009) để biết việc
theo dõi tiếp theo.

## Bổ sung (2026-08-29): repository đã chuyển sang public thay vì nâng cấp, và 2 lỗi thực thi lộ ra trên PR thật

Chủ sở hữu repository đã giải quyết bản bổ sung ở trên bằng cách chuyển `nmhieuit/ecommerce` thành
**public** thay vì nâng cấp lên GitHub Pro — ngược lại với khuyến nghị của ADR này, được chấp nhận
1 cách có ý thức (xem cập nhật Quyết định 7 trong
`specs/013-sonarqube-merge-blocker/research.md`). Branch protection với tương đương
`enforce_admins` ("Do not allow bypassing the above settings") giờ đã có hiệu lực trên `master`.

Việc bật nó lên với các pull request thật đã lập tức lộ ra 2 lỗi trong thiết kế ở mục "Quyết định",
không lỗi nào nhìn thấy được khi chỉ test với instance cục bộ vì việc test đó chưa bao giờ đi qua
quá trình đánh giá required-status-check thật sự của GitHub:

1. **`publishChecks` (Checks API) hoàn toàn không thể xác thực bằng personal access token.**
   `POST /repos/.../check-runs` với PAT của job này trả về HTTP 403 "Resource not accessible by
   personal access token" — đã xác nhận trực tiếp với API, không phải suy đoán. GitHub giới hạn việc
   tạo check-run chỉ cho token cài đặt của GitHub App. Mọi lời gọi `checkStarted`/`checkPassed`/
   `checkFailed` trong `Jenkinsfile` đã âm thầm thất bại kể từ khi pipeline của ADR này được xây lần
   đầu; Jenkins chưa bao giờ hiện lỗi, và cả 5 check `ci/*` chưa bao giờ tới được GitHub trên bất kỳ
   commit nào, chưa từng một lần. Status API kiểu cổ điển (`POST /repos/.../statuses/:sha`) không có
   giới hạn như vậy — đã xác nhận với cùng token, HTTP 201 — nên 3 hàm giờ gọi `githubNotify`
   (GitHub plugin) thay vì `publishChecks` (github-checks plugin). Việc khớp required-check dựa theo
   tên `context` bất kể API nào tạo ra nó, nên không cần thay đổi branch protection, chỉ cần sửa
   Jenkinsfile.
2. **Việc phát hiện PR đang build commit merge-ref, không phải commit head của PR.**
   `OriginPullRequestDiscoveryTrait strategyId=1` ("merging the pull request with the current
   target branch") nghĩa là Jenkins test và báo cáo trên 1 commit merge tổng hợp mà quá trình đánh
   giá required-status-check của GitHub cho PR đó không bao giờ xem tới — nó theo dõi SHA head thật
   của PR. Các check đã được gửi thành công (sau khi lỗi 1 được sửa) nhưng trên sai commit, nên chúng
   mãi ở trạng thái "Expected — Waiting for status to be reported". Đã đổi sang `strategyId=2`
   ("the current pull request revision"). `strategyId=3` (cả hai) được thử trước và bị loại: build
   đồng thời merge ref và head ref trên agent Jenkins single-host này làm 1 Testcontainers SQL Server
   khởi động thiếu tài nguyên ở 1 trong 2 lượt chạy song song, làm fail 1 integration test không liên
   quan.

Cả 2 lần sửa chỉ nằm trong Jenkins/Jenkinsfile; không có cài đặt branch protection nào thay đổi. Đã
verify với 2 pull request thật: [nmhieuit/ecommerce#2](https://github.com/nmhieuit/ecommerce/pull/2)
(cả 5 check xanh, đã merge) và #3 (1 unit test cố ý làm hỏng — `ci/unit-tests` fail đúng với lý do
thật, "Merge pull request" bị vô hiệu hoá, và không có điều khiển đường vòng nào xuất hiện trên PR,
kể cả với chủ sở hữu repository). Xem `specs/013-sonarqube-merge-blocker/tasks.md` T010 để biết dấu
vết đầy đủ.
