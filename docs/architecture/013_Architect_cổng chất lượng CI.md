# Kiến trúc: Cổng chất lượng SonarQube làm rào chặn merge

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-22, đặc tả tại
[`specs/013-sonarqube-merge-blocker/`](../../specs/013-sonarqube-merge-blocker/), xây trên nền hạ
tầng đã dựng ở `specs/012-sonarqube-quality-gate` (đã xoá thư mục, nhưng mã pipeline nó tạo ra vẫn
còn và vẫn đang chạy). Quyết định kiến trúc gốc: [ADR-0012](../adr/0012-ci-quality-gate-enforcement.md).

**Trạng thái xác minh**: toàn bộ 17/17 nhiệm vụ trong `tasks.md` đã hoàn thành, xác minh bằng các
Pull Request thật trên `github.com/nmhieuit/ecommerce` — không phải mô tả lý thuyết. Danh sách PR
dùng làm bằng chứng: #2, #3, #6, #7, #8, #9, #10, #12.

## 1. Kiến trúc tổng thể

```
GitHub (public repo)  <──── quét định kỳ (5 phút) ────  Jenkins Multibranch Pipeline
        │                                                        │
        │  required status checks + branch protection            │  5 stage tuần tự
        │  (không đường vòng, mọi vai trò)                        │  (Jenkinsfile)
        │                                                        ▼
        └──── comment decoration ────  SonarQube Community Edition
                                        + Community Branch Plugin
```

Ba hệ thống độc lập được nối với nhau hoàn toàn bằng **cơ chế nền tảng có sẵn** — không có webhook
tuỳ biến, không có service trung gian tự viết:

- **GitHub** là nơi lưu trữ mã nguồn, quản lý Pull Request, và là nơi thực thi quyết định chặn/mở
  merge (branch protection) — đây là điểm mấu chốt: quyết định "được merge hay không" nằm ở phía
  GitHub, không phải ở Jenkins hay bất kỳ script nào tự viết.
- **Jenkins** đóng vai trò điều phối: phát hiện thay đổi, chạy tuần tự 5 bước kiểm tra, báo kết quả
  từng bước ngược lại GitHub.
- **SonarQube** phân tích chất lượng mã nguồn và tính toán kết quả cổng chất lượng; một plugin cộng
  đồng đăng kết quả đó trực tiếp lên PR.

**Vì sao tự host SonarQube Community Edition thay vì dùng SonarCloud** (dịch vụ đám mây): repository
từng ở chế độ private, và SonarCloud tính phí cho repo private trong khi Community Edition tự host
thì miễn phí, nhất quán với việc toàn bộ nền tảng đã tự host mọi thứ khác (Kubernetes qua Ansible).
Xem đầy đủ phân tích đánh đổi tại ADR-0012, mục "Amendment (2026-08-23)". *(Ghi chú: sau đó repo đã
chuyển sang public vì lý do khác — xem mục 6 bên dưới — nên tiền đề "phải tránh phí SaaS vì repo
private" của quyết định này không còn đúng 100%; đây là điểm có thể xem lại trong tương lai, không
phải việc cần làm ngay.)*

**Giới hạn hiện tại**: đây là một **instance phát triển (dev)**, chạy trong Docker Desktop cục bộ qua
`docker-compose.ci.yml` — không phải hạ tầng production. Bản production (Kubernetes, theo mẫu Ansible
sẵn có của nền tảng) vẫn là việc riêng, chưa thực hiện (ghi nhận tại ADR-0012 Action Item liên quan).

## 2. Mô tả từng thành phần

### 2.1. Jenkins Multibranch Pipeline job

- Dùng plugin **GitHub Branch Source** để tự phát hiện nhánh và Pull Request — không có webhook tự
  viết nào kích hoạt Jenkins; một `PeriodicFolderTrigger` (chu kỳ 5 phút) chủ động quét GitHub.
- Chiến lược phát hiện PR là `OriginPullRequestDiscoveryTrait` với **`strategyId=2`** ("The current
  pull request revision" — build đúng commit đầu/head của PR), **không phải** `strategyId=1`
  (merge-ref). Lý do: GitHub tính trạng thái required-check theo đúng head SHA của PR; nếu Jenkins
  build một commit merge-ref tổng hợp (không tồn tại trên GitHub theo con mắt của required-check),
  check sẽ treo mãi ở trạng thái "Expected — Waiting for status to be reported". Đây là một lỗi thật
  đã gặp và sửa (xem mục 6).
- `strategyId=3` (build cả hai) từng được thử nhưng bị loại: chạy song song merge-ref và head-ref
  trên cùng một Jenkins agent (một host) làm tranh chấp tài nguyên Docker, khiến một Testcontainers
  SQL Server khởi động timeout ở một trong hai lượt chạy song song.

### 2.2. `Jenkinsfile` — 5 stage tuần tự

| Stage | Tên check GitHub | Thất bại khi nào |
|---|---|---|
| Build | `ci/build` | `dotnet build` hoặc build frontend (pnpm/Turbo) lỗi |
| Unit test | `ci/unit-tests` | Bất kỳ project `*.Api.UnitTests` thất bại |
| Integration test | `ci/integration-tests` | Bất kỳ project `*.Api.IntegrationTests` thất bại (Testcontainers — SQL Server, Redis, RabbitMQ thật) |
| Contract test | `ci/contract-tests` | Bất kỳ project `*.Api.ContractTests` thất bại (Pact) |
| Cổng chất lượng | `ci/sonarqube-quality-gate` | `waitForQualityGate()` trả về khác `OK`, hoặc hết `timeout(15 phút)` |

Mỗi stage gọi `checkStarted`/`checkPassed`/`checkFailed` — các hàm này dùng bước **`githubNotify`**
(Status API, `POST /repos/.../statuses/:sha`), **không phải** `publishChecks` (Checks API). Đây là
một lỗi thật đã phát hiện và sửa: Checks API từ chối Personal Access Token với HTTP 403 "Resource not
accessible by personal access token" — chỉ token cài đặt của một GitHub App mới dùng được API đó.
Status API không có hạn chế này. Vì required-check của GitHub khớp theo **tên context**, không phân
biệt API nào tạo ra nó, việc đổi từ `publishChecks` sang `githubNotify` không cần sửa gì ở cấu hình
branch protection.

Stage cuối gọi `dotnet sonarscanner end` rồi `waitForQualityGate(abortPipeline: false)` bên trong
`timeout(15 phút)`. Điểm quan trọng: `dotnet sonarscanner end` chỉ báo thành công khi **tải phân
tích lên** xong — kết quả cổng chất lượng được SonarQube tính **bất đồng bộ** sau đó và báo lại qua
webhook. Nếu chỉ dựa vào exit code của scanner, một cổng chất lượng thất bại vẫn có thể trông như
thành công. `waitForQualityGate()` là cơ chế đúng để biến *kết quả cổng*, chứ không phải *việc tải
lên*, thành điều kiện chặn.

### 2.3. GitHub branch protection

- Nhánh `master` yêu cầu đủ 5 check trên là "required status checks".
- Tuỳ chọn **"Do not allow bypassing the above settings"** được bật — đây là công tắc duy nhất loại
  bỏ đường vòng cho mọi vai trò, kể cả chủ sở hữu repository. Không có mã tự viết nào thực thi việc
  chặn này; toàn bộ nằm trong cơ chế gốc của GitHub.
- **Yêu cầu tiên quyết bất ngờ**: GitHub từ chối bật branch protection (kể cả repository ruleset
  thay thế) trên repository **private** ở gói miễn phí, trả về HTTP 403 "Upgrade to GitHub Pro or
  make this repository public", bất kể quyền quản trị của token. Đây là giới hạn gói dịch vụ, không
  phải lỗi cấu hình hay quyền. Chủ repository đã chọn giải pháp **chuyển repo sang public** thay vì
  nâng cấp trả phí (xem `research.md` Decision 7 và ADR-0012 "Amendment (2026-08-29)" để biết đầy đủ
  bối cảnh đánh đổi).

### 2.4. SonarQube Community Edition + Community Branch Plugin

- Quality Gate mặc định "Sonar way" với 4 điều kiện trên **mã mới** (new code) của mỗi PR:
  `new_violations > 0` (bất kỳ vấn đề mới nào cũng đủ để fail), `new_coverage < 80`,
  `new_duplicated_lines_density > 3`, `new_security_hotspots_reviewed < 100`.
- SonarQube Community Edition **không có** tính năng PR decoration chính thức (đó là tính năng trả
  phí ở bản Developer Edition). Plugin cộng đồng
  [Community Branch Plugin (mc1arke)](https://github.com/mc1arke/sonarqube-community-branch-plugin)
  được cài để lấp khoảng trống này, dùng chung Personal Access Token đã cấu hình cho Jenkins — không
  cần cấp thêm quyền nào khác.
- Plugin này **xoá comment decoration cũ và đăng comment mới** ở mỗi lượt phân tích (không sửa tại
  chỗ, không giữ đồng thời nhiều comment) — nghĩa là tại mọi thời điểm PR chỉ có đúng một comment
  decoration, luôn phản ánh commit mới nhất. Đã xác minh trực tiếp: số liệu coverage ước tính đổi
  thật giữa hai lần phân tích liên tiếp trên cùng PR (79.30% → 79.40%), chứng minh đây là phân tích
  mới chứ không phải bản sao/cache.

### 2.5. Hạ tầng cục bộ: `docker-compose.ci.yml` + `docker/ci/jenkins.Dockerfile`

Hai container trên một mạng Docker riêng (`ci-backbone`): `jenkins` (build từ Dockerfile riêng, xem
mục 3) và `sonarqube` (image `sonarqube:community`). Mỗi container có volume riêng để dữ liệu sống
sót qua `docker compose down` (không kèm `-v`). Groovy init script
(`docker/ci/jenkins-init/`) tự khôi phục cấu hình kết nối SonarQube server mỗi lần Jenkins khởi động
lại, để không phải nhập lại qua giao diện sau mỗi `down -v`.

## 3. Sáu vấn đề hạ tầng không hiển nhiên — đã xác minh, đang chờ hợp nhất

Sáu vấn đề dưới đây **đều đã được xác minh hoạt động đúng thật** trên một container Jenkins đang
chạy (áp dụng trực tiếp qua `docker compose up -d`, dùng để chạy toàn bộ các kiểm thử T012–T018).
**Tuy nhiên, thay đổi tương ứng trong `docker-compose.ci.yml` và `docker/ci/jenkins.Dockerfile` hiện
nằm trong [Pull Request #8](https://github.com/nmhieuit/ecommerce/pull/8), vẫn đang mở, chưa merge
vào `master`.** Nói cách khác: nếu ai đó `git clone` repo hôm nay và dựng CI từ đúng những gì có trên
`master`, họ sẽ gặp lại chính các lỗi dưới đây — đây không phải rủi ro lý thuyết mà là trạng thái git
thật tại thời điểm viết tài liệu này (2026-09-01). Việc merge PR #8 là điều kiện để tài liệu vận hành
[`docs/github-jenkins-sonarqube-setup.md`](../github-jenkins-sonarqube-setup.md) phản ánh đúng
`master`, không chỉ đúng container đang chạy.

1. **Java agent của Community Branch Plugin, thiếu ở hai tiến trình riêng biệt.** SonarQube từ chối
   khởi động hoàn toàn ("Fail to load plugin Community Branch Plugin ... Please check the Java Agent
   has been correctly set") nếu thiếu biến môi trường `SONAR_WEB_JAVAADDITIONALOPTS` — rồi lại từ
   chối lần nữa cho tiến trình `SONAR_CE_JAVAADDITIONALOPTS` (Compute Engine, nơi thực sự tính kết
   quả cổng chất lượng) sau khi tiến trình đầu đã sửa. Cả hai phải được đặt trong cùng một thay đổi.
2. **Image Jenkins mặc định (`jenkins/jenkins:lts-jdk17`) không có bất kỳ công cụ nào `Jenkinsfile`
   cần** — không .NET SDK, không Node/pnpm, không Docker CLI. `docker/ci/jenkins.Dockerfile` build
   sẵn toàn bộ; `docker-compose.ci.yml` phải `build:` từ file này, không pull image gốc.
3. **Cache phiên bản pnpm đã pin (`corepack`) phải nằm ngoài `/var/jenkins_home`.** Đường dẫn đó là
   một volume; bất kỳ thứ gì corepack cache vào đó lúc build image (chạy với quyền `root`) sẽ vô hình
   với user `jenkins` lúc chạy thật (khác `HOME`, và volume che khuất luôn cache đã build). Không có
   `ENV COREPACK_HOME=/opt/corepack-cache` trỏ ra ngoài volume, mọi lệnh `pnpm` sẽ âm thầm tải phiên
   bản mới nhất thay vì phiên bản đã pin trong `frontend/package.json`.
4. **Các test dùng Testcontainers (tầng integration và contract) cần một Docker daemon thật.**
   Container Jenkins cần mount `/var/run/docker.sock` (mô hình docker-outside-of-docker) và thêm
   user `jenkins` vào group `root` (socket là `root:root`, quyền 660) — nếu không, mọi test loại này
   báo lỗi `DockerUnavailableException`. Đây là một hành động cấp quyền thật (kiểm soát tương đương
   root với Docker host) — không nên áp dụng mà không cân nhắc đánh đổi đó một cách có chủ đích.
5. **Ryuk (container dọn dẹp của Testcontainers) không thể hoàn tất bắt tay trong mô hình này.** Ryuk
   cần một kết nối TCP ngược về tiến trình test, chỉ hoạt động khi tiến trình test và Docker daemon
   dùng chung network namespace. Ở đây chúng chỉ dùng chung *daemon* (qua socket mount); Ryuk trở
   thành container anh em trên một đường mạng khác, khiến mọi test dùng Testcontainers báo lỗi
   `ResourceReaperException: Initialization has been cancelled`. Giải pháp theo đúng khuyến nghị
   chính thức của Testcontainers cho tình huống sibling-container này:
   `TESTCONTAINERS_RYUK_DISABLED=true`. Đánh đổi: container test không còn được Ryuk tự dọn khi một
   lượt test crash giữa chừng — cần thỉnh thoảng kiểm tra và dọn thủ công container còn sót lại.
6. **Testcontainers phải được trỏ vào host Docker thật, không phải `localhost`.** Khi daemon được
   truy cập qua socket mount, các port container tạo ra thực sự nằm trên máy ảo của Docker Desktop,
   không nằm trong loopback riêng của container Jenkins — nên cách địa chỉ hoá mặc định
   `localhost:<port>` của Testcontainers-dotnet (và cả lệnh gọi callback provider-state của PactNet)
   sẽ timeout. `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal` (tên DNS cố định của Docker
   Desktop cho chính máy ảo đó, truy cập được từ mọi container) giải quyết việc này.

## 4. Cơ chế audit (FR-009)

Không có endpoint audit tuỳ biến nào được xây dựng. Hai cơ chế sẵn có của GitHub trả lời đầy đủ câu
hỏi "ai đổi cấu hình chặn merge, và cổng chất lượng ra sao tại thời điểm mỗi lần merge":

- **Ai đổi cấu hình, khi nào**: `github.com/settings/security-log` (lọc theo
  `repo:nmhieuit/ecommerce`) ghi lại sự kiện `repo.change_merge_setting`, kèm actor, thời gian, IP —
  đây là audit log cấp tài khoản cá nhân; một repository thuộc GitHub Organization/Enterprise có
  endpoint audit-log tổ chức phong phú hơn, nhưng không cần thiết ở quy mô một tài khoản cá nhân.
- **Trạng thái cổng chất lượng tại thời điểm mỗi lần merge**: `GET
  /repos/{owner}/{repo}/commits/{sha}/status` trả về đầy đủ lịch sử 5 required check cho bất kỳ SHA
  nào, kể cả sau khi PR đã merge và đóng. Tab "Checks" trên giao diện PR hiển thị cùng dữ liệu này
  cho người không dùng API.

## 5. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/013-quality-gate-component.drawio`](../diagrams/013-quality-gate-component.drawio)
- Sơ đồ trình tự (đầy đủ luồng, gồm cả nhánh thất bại → sửa → tự động chạy lại):
  [`docs/diagrams/013-quality-gate-sequence.drawio`](../diagrams/013-quality-gate-sequence.drawio)

## 6. Lịch sử các lỗi thật đã gặp — bài học cho lần tích hợp tương tự sau này

Hai lỗi dưới đây **chỉ lộ ra khi thử trên GitHub thật**, không thấy được khi chỉ kiểm thử với instance
SonarQube/Jenkins cục bộ đơn lẻ — vì việc kiểm thử cục bộ trước đó chưa từng đi qua bước GitHub thật
sự đánh giá required-status-check:

1. `publishChecks` (Checks API) âm thầm thất bại với mọi Personal Access Token — 5 check chưa từng
   thật sự lên GitHub dù Jenkins không báo lỗi gì. Sửa bằng cách đổi sang `githubNotify` (mục 2.2).
2. Chiến lược PR discovery build sai commit (merge-ref thay vì head-ref) khiến check treo vĩnh viễn
   ở trạng thái chờ. Sửa bằng cách đổi `strategyId` từ 1 sang 2 (mục 2.1).

Cả hai đã được xác minh khắc phục trên hai PR thật: `#2` (cả năm check xanh, đã merge) và `#3` (một
unit test cố tình hỏng — `ci/unit-tests` báo đúng lý do, nút merge bị vô hiệu hoá, không có tuỳ chọn
vượt qua nào xuất hiện, kể cả cho chủ sở hữu repository).
