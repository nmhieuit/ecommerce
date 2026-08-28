# Nghiên cứu: Cổng chất lượng SonarQube chặn merge Pull Request

## Bối cảnh xác nhận qua kiểm tra repo thực tế

- `Jenkinsfile`, `sonar-project.properties`, `docker-compose.ci.yml`, và bốn script trong
  `scripts/ci/` **đã tồn tại** trong repository — được xây dựng ở một nỗ lực trước (spec
  `012-sonarqube-quality-gate`, nay đã bị xoá nhưng hạ tầng nó tạo ra vẫn còn). Tính năng 013
  không viết lại các artefact này; nó đối chiếu chúng với `spec.md` hiện tại và đóng các khoảng
  trống còn để ngỏ.
- Môi trường Jenkins + SonarQube **cục bộ** (Docker Desktop, `docker-compose.ci.yml`) đã được dựng
  và xác nhận hoạt động (`docs/github-jenkins-sonarqube-setup.md`): SonarQube trả `"status":"UP"`,
  ba plugin Jenkins bắt buộc (`github-branch-source`, `github-checks`, `sonar`) đã cài đặt. Đây là
  **môi trường phát triển**, không phải bản production (Kubernetes, theo mẫu Ansible hiện có của
  nền tảng) — dựng bản production vẫn là việc còn lại, đã có sẵn trong hồ sơ theo dõi của ADR-0012.
- Các bước còn lại đòi hỏi nhập token/mật khẩu thật (SonarQube token, GitHub PAT, lần đăng nhập đầu
  của Jenkins) **chưa được thực hiện** — đúng như quy tắc vận hành, một phiên làm việc tự động
  không được nhập thông tin xác thực vào bất kỳ biểu mẫu nào, kể cả khi được yêu cầu.
- `scripts/ci/setup-branch-protection.sh` đã được chạy thử nghiệm nhắm tới `nmhieuit/ecommerce` và
  trả về xác nhận thực nghiệm quan trọng: **GitHub từ chối bật branch protection (và cả
  repository ruleset) trên một repository private ở gói miễn phí**, kể cả với token có quyền quản
  trị — phản hồi là HTTP 403 "Upgrade to GitHub Pro or make this repository public".

## Các quyết định (kế thừa từ nỗ lực trước, đã triển khai trong repo)

### Quyết định 1: Bộ điều phối pipeline và mô hình kích hoạt

**Quyết định**: `Jenkinsfile` khai báo ở gốc repo chạy như một Jenkins **Multibranch Pipeline**,
kết nối tới GitHub qua plugin GitHub Branch Source, để build PR tự kích hoạt và Jenkins báo kết quả
từng stage ngược lại GitHub qua commit status/check.

**Lý do**: Multibranch + GitHub Branch Source là cách tích hợp Jenkins-GitHub chuẩn, không cần mã
webhook tùy biến; tự động phát hiện nhánh PR và báo trạng thái build mà không cần script thêm,
phù hợp với thiên hướng "cơ chế nền tảng chuẩn, deny-by-default" của Nguyên tắc I/VI.

**Đã triển khai**: `Jenkinsfile` (gốc repo).

### Quyết định 2: Cách Jenkins báo kết quả từng stage lên PR (FR-001, FR-002, FR-006)

**Quyết định**: Mỗi trong năm stage (build, unit test, integration test, contract test, cổng chất
lượng SonarQube) là một `stage {}` cấp cao nhất riêng biệt. Hàm `checkStarted`/`checkPassed`/
`checkFailed` trong `Jenkinsfile` gọi bước `publishChecks` để đăng tường minh một GitHub check
riêng cho từng stage (`ci/build`, `ci/unit-tests`, `ci/integration-tests`, `ci/contract-tests`,
`ci/sonarqube-quality-gate`) — đăng tường minh, không dựa vào trạng thái theo tên stage mặc định
của Jenkins, vì branch protection khớp theo đúng chuỗi tên đó.

**Lý do**: Năm check được đặt tên riêng ánh xạ trực tiếp tới FR-001 (thấy được trình tự từng bước)
và FR-006 (biết chính xác bước nào chặn PR) mà không cần mở console log của Jenkins.

**Đã triển khai**: `Jenkinsfile`, xem `contracts/pipeline-stage-contract.md` §1.

### Quyết định 3: Thực thi cổng chất lượng SonarQube như một bước chặn pipeline (FR-003, FR-008 cũ nay tương ứng hành vi fail-closed)

**Quyết định**: Stage cuối gọi `dotnet sonarscanner end` rồi `waitForQualityGate(abortPipeline:
false)` bên trong một khối `timeout(15 phút)`; bất kỳ trạng thái khác `OK` — hoặc chính việc hết
thời gian chờ — đều được chuyển thành lỗi tường minh (`error(...)`) khiến stage thất bại.

**Lý do**: Sonar Scanner CLI trả về thành công ngay khi *tải lên* xong phân tích; cổng chất lượng
được tính toán bất đồng bộ sau đó trên máy chủ SonarQube — `waitForQualityGate()` là cơ chế đúng để
biến *kết quả cổng*, chứ không chỉ việc quét, thành điều kiện chặn pipeline. Bọc trong `timeout()`
thỏa mãn trực tiếp yêu cầu fail-closed: một máy chủ SonarQube không phản hồi/chậm sẽ khiến stage
thất bại thay vì treo vô hạn hoặc âm thầm cho qua.

**Đã triển khai**: `Jenkinsfile`, stage `sonarqube quality gate`.

### Quyết định 4: Đầu vào coverage cho cổng chất lượng trên hai stack (FR-002, FR-005)

**Quyết định**: Backend gộp báo cáo Cobertura từ mọi tầng test (`scripts/ci/merge-coverage.sh`)
thành một file duy nhất (`sonar.cs.cobertura.reportsPaths`); frontend dùng `lcov.info` từ Vitest
(`sonar.javascript.lcov.reportPaths` / `sonar.typescript.lcov.reportPaths`). Cả hai nạp vào **một**
Sonar project (`sonar.projectKey=ecommerce`) để một PR chỉ có **một** kết quả cổng chất lượng duy
nhất, phản ánh cả hai stack.

**Lý do**: Giữ đúng nghĩa đen của FR-002 ("một trạng thái cổng chất lượng cho mỗi commit đầu PR");
tách nhiều Sonar project theo từng service sẽ tạo nhiều kết quả cổng, tái tạo lại đúng sự mơ hồ mà
ticket muốn loại bỏ.

**Đã triển khai**: `sonar-project.properties`, `scripts/ci/merge-coverage.sh`.

### Quyết định 5: Branch protection và yêu cầu "không đường vòng" — quản lý thủ công, không IaC

**Quyết định**: Branch protection được áp dụng một lần bởi quản trị viên repo qua
`scripts/ci/setup-branch-protection.sh` (bọc `gh api`), không quản lý bằng công cụ IaC riêng.

**Lý do**: Dựng một stack Terraform/IaC chỉ để quản lý cấu hình của một repository là hạ tầng không
tương xứng với vấn đề (Nguyên tắc I). Cờ "Do not allow bypassing" của GitHub là cơ chế gốc, được
audit sẵn, thỏa yêu cầu "không đường vòng" mà không cần mã thực thi tùy biến.

**Đã triển khai**: `scripts/ci/setup-branch-protection.sh` — **nhưng chưa áp dụng thành công**, xem
Quyết định 7.

### Quyết định 6: Hiển thị chỉ số trên PR — đã chọn cơ chế, chưa hoàn tất cài đặt

**Quyết định trước đây**: dùng tính năng PR decoration có sẵn của SonarQube, cấu hình chứ không viết
mã mới. Nỗ lực trước để ngỏ việc chọn plugin cụ thể do phụ thuộc phiên bản SonarQube đã cấp phép.
Quyết định 8 dưới đây chốt lựa chọn đó.

## Quyết định mới (giải quyết khoảng trống của spec 013)

### Quyết định 7: Branch protection bị chặn bởi giới hạn gói GitHub — khuyến nghị nâng cấp GitHub Pro

**Vấn đề**: `scripts/ci/setup-branch-protection.sh` xác nhận bằng thực nghiệm rằng
`nmhieuit/ecommerce` (private, gói miễn phí) nhận HTTP 403 cho cả branch protection cổ điển lẫn
repository ruleset. Không có token hay quyền nào sửa được lỗi này — đây là giới hạn của gói dịch vụ,
không phải lỗi cấu hình. Nếu không giải quyết, **FR-003 (không đường vòng) không thể xác minh được
trên GitHub thật**, dù toàn bộ mã pipeline đã đúng.

**Quyết định**: Khuyến nghị chủ repository **nâng cấp lên GitHub Pro** (giữ repo private) thay vì
chuyển repo sang công khai.

**Lý do**: Chi phí GitHub Pro cho tài khoản cá nhân thấp (khoảng vài đô la Mỹ/tháng) và không đòi
hỏi đánh đổi việc giữ mã nguồn private — một giả định đã được xác lập từ trước (ADR-0012 amendment,
lý do chọn SonarQube self-hosted thay vì SonarCloud chính là tránh chi phí SaaS cho repo private).
Việc công khai repo để có branch protection miễn phí sẽ mâu thuẫn trực tiếp với quyết định đó.

**Phương án khác đã xem xét**:
- *Chuyển repo sang công khai*: bị bác bỏ vì mâu thuẫn với quyết định giữ private đã có từ trước.
- *Xây bot merge-check tùy biến thay cho branch protection*: bị bác bỏ, cùng lý do đã nêu ở
  ADR-0012 Option C — kém tin cậy hơn, tự nó lại thành một điểm hỏng mới, và không có gì đảm bảo
  bot không bị bypass theo cách khác.
- *Chấp nhận không có "không đường vòng" thật, chỉ dừng ở mức khuyến nghị*: bị bác bỏ vì đây chính
  là yêu cầu chấp nhận cốt lõi (P1) của SCRUM-22 — bỏ qua nó là bỏ qua lý do tồn tại của tính năng.

**Việc cần làm (không thể tự động hoá)**: đây là quyết định chi tiêu và thay đổi cài đặt tài khoản
— nằm ngoài phạm vi hành động của một phiên làm việc tự động (không được mua dịch vụ hay đổi cài
đặt tài khoản thay người dùng). Chủ repository cần tự nâng cấp gói, sau đó chạy lại
`scripts/ci/setup-branch-protection.sh nmhieuit/ecommerce master`.

**Cập nhật (2026-08-27, quyết định thực tế đã chọn)**: chủ repository đã chọn phương án **chuyển
`nmhieuit/ecommerce` sang công khai (public)** thay vì nâng cấp GitHub Pro — xác nhận trực tiếp qua
trình duyệt: trang repo hiển thị nhãn "Public", và `Settings → Branches` không còn báo lỗi nâng cấp
gói, nút "Add classic branch protection rule" khả dụng bình thường. Đây là phương án đã bị liệt kê
là "bác bỏ" ở trên (vì mâu thuẫn với lý do chọn SonarQube self-hosted thay SonarCloud trong ADR-0012
— lý do đó dựa trên giả định repo private). Ghi nhận trung thực: chủ repository đã cân nhắc lại và
chấp nhận đánh đổi ngược — công khai mã nguồn để có branch protection miễn phí — thay vì trả phí
GitHub Pro để giữ private. Hệ quả kéo theo (ghi nhận, không phải việc cần làm ngay): tiền đề "repo
private nên SonarCloud không có gói miễn phí" của ADR-0012 amendment (2026-08-23) không còn đúng;
nếu muốn, việc chọn lại giữa self-hosted SonarQube và SonarCloud (miễn phí cho repo public) có thể
xem lại như một quyết định riêng trong tương lai — nằm ngoài phạm vi Phase 3 hiện tại.

### Quyết định 8: Cơ chế hiển thị chỉ số trên PR — SonarQube Community Branch Plugin

**Vấn đề**: SonarQube Community Edition (đã chọn ở ADR-0012 amendment vì miễn phí) không có PR
decoration chính thức, nên FR-005 (hiển thị coverage/duplication/code smell ngay trên PR) hiện chưa
được thoả mãn dù cổng chất lượng đã chạy đúng.

**Quyết định**: Cài đặt [SonarQube Community Branch Plugin](https://github.com/mc1arke/sonarqube-community-branch-plugin)
(mã nguồn mở, cộng đồng duy trì) vào instance SonarQube, kết nối với GitHub token đã cấu hình ở bước
nối Jenkins-GitHub (Quyết định 1).

**Lý do**: Đây là lựa chọn duy nhất giữ đúng tinh thần "self-hosted, chi phí bằng không" đã được xác
lập nhất quán xuyên suốt ADR-0012 — nâng cấp lên SonarQube Developer Edition (có PR decoration chính
thức) phát sinh chi phí bản quyền định kỳ mà tính năng gốc (chọn Community Edition) đã cố tránh.

**Phương án khác đã xem xét**:
- *Nâng cấp SonarQube lên Developer Edition*: bị bác bỏ vì chi phí bản quyền định kỳ, mâu thuẫn với
  lý do chọn Community Edition ban đầu.
- *Viết bước Jenkins tùy biến gọi GitHub Checks API để tự đăng bình luận chỉ số*: bị bác bỏ vì đây
  là việc build và bảo trì thêm một cấu phần tùy biến để thay thế một tính năng plugin cộng đồng đã
  có sẵn và được cộng đồng bảo trì — không cân xứng với vấn đề (Nguyên tắc I).

**Rủi ro chấp nhận**: plugin cộng đồng không chính thức, cần cài lại sau mỗi lần nâng cấp SonarQube
(đã ghi trong ADR-0012 amendment) — chấp nhận đánh đổi này để giữ chi phí bằng không.

## Các mục cố ý hoãn lại (không phải NEEDS CLARIFICATION — ngoài phạm vi ticket này)

- Stage quét lỗ hổng bảo mật container image mà hiến pháp yêu cầu thêm sau cổng SonarQube — tiêu
  chí chấp nhận gốc của SCRUM-22 dừng ở cổng SonarQube; việc này được ghi nhận là giới hạn phạm vi
  có thời hạn tại `plan.md` Complexity Tracking, kế thừa từ ADR-0012 Action Item 4.
- Bổ sung contract test cho `gateway` và `parties` không liên quan tới việc nối cổng chất lượng —
  stage contract-tests chạy bất kỳ project contract-test nào đang tồn tại và sẽ tự nhận thêm khi
  các service đó có contract test ở công việc khác.
- Ngưỡng cụ thể của quality profile SonarQube (% coverage, % trùng lặp...) là cấu hình phía máy chủ
  SonarQube, không phải quyết định của mã pipeline (đã ghi trong Giả định của `spec.md`).
