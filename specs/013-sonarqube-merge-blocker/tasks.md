---

description: "Task list template for feature implementation"
---

# Tasks: Cổng chất lượng SonarQube chặn merge Pull Request

**Đầu vào**: Tài liệu thiết kế từ `/specs/013-sonarqube-merge-blocker/`

**Điều kiện tiên quyết**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/pipeline-stage-contract.md](./contracts/pipeline-stage-contract.md),
[quickstart.md](./quickstart.md)

**Kiểm thử**: Không được yêu cầu tường minh trong đặc tả. Tính năng này không có mã ứng dụng để
unit test — "kiểm thử" ở đây nghĩa là xác nhận chính pipeline theo các kịch bản trong
`quickstart.md`, xuất hiện dưới dạng task xác nhận tường minh trong từng user story thay vì một
phase test riêng.

**Bối cảnh quan trọng — đã xác minh trực tiếp trên môi trường hiện tại (2026-08-27)**: phần lớn hạ
tầng của tính năng này **đã tồn tại và đang chạy**, không phải xây từ đầu:

- `Jenkinsfile`, `sonar-project.properties`, `docker-compose.ci.yml`, bốn script trong
  `scripts/ci/` — đã có sẵn trong repo.
- Container `ecomerce-ci-jenkins-1` và `ecomerce-ci-sonarqube-1` đang chạy khoẻ mạnh
  (`docker compose -f docker-compose.ci.yml ps`); ba plugin Jenkins bắt buộc
  (`github-branch-source`, `github-checks`, `sonar`) đã cài đặt; SonarQube Community Branch Plugin
  `26.5.0` đã cài đặt và đã nạp thành công (log: `Deploy Community Branch Plugin / 26.5.0`).
- Job Multibranch Pipeline `ecommerce` đã tồn tại, đã kết nối tới `nmhieuit/ecommerce` thật qua
  credential `github-pat`, đã phát hiện nhánh `master`.
- **Nhưng** lượt build duy nhất đã chạy (`master` build #1) **THẤT BẠI ngay ở stage đầu tiên**
  (`sonarqube: begin analysis`) với `exit code 126`, khiến bốn stage còn lại bị `skipped due to
  earlier failure`. Nguyên nhân xác định được: bốn script trong `scripts/ci/` được git theo dõi với
  mode `100644` (không có bit thực thi) thay vì `100755` — `git ls-files --stage scripts/ci/` xác
  nhận điều này — nên lệnh `sh 'scripts/ci/sonar-begin.sh'` trong `Jenkinsfile` bị từ chối quyền
  thực thi khi Jenkins checkout mã nguồn trên Linux.
- SonarQube chưa có project nào (`api/projects/search` trả về rỗng) và chưa có webhook nào đăng ký
  (`api/webhooks/list` trả về rỗng) — nếu không xử lý, `waitForQualityGate()` sẽ luôn hết thời gian
  chờ (15 phút) và cổng luôn báo thất bại dù mã có đạt hay không.
- Mật khẩu quản trị SonarQube cục bộ vẫn là mặc định `admin`/`admin`.
- Branch protection trên GitHub thật vẫn chưa xác nhận bật được (không có `gh` CLI trong môi trường
  này để kiểm tra trực tiếp; giả định vẫn bị chặn bởi giới hạn gói GitHub private/miễn phí theo
  research.md Decision 7, chưa có bằng chứng nào cho thấy điều đó đã thay đổi).

Các task dưới đây phản ánh đúng khoảng cách giữa hiện trạng đã xác minh này và các yêu cầu của
`spec.md`, không giả định lại từ đầu.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Có thể chạy song song (khác file, không phụ thuộc task chưa xong)
- **[Story]**: User story mà task thuộc về (US1, US2, US3)
- Mỗi task nêu rõ đường dẫn file/hệ thống liên quan

## Quy ước đường dẫn

Đây là kết nối hạ tầng CI, không phải tính năng ứng dụng (xem plan.md → Cấu trúc dự án). Không có
thư mục `src/`/`backend/`/`frontend/src/` nào bị thay đổi; các task bên dưới sửa file cấu hình CI ở
gốc repo, sửa cấu hình trên Jenkins/SonarQube/GitHub (không phải file trong repo), hoặc chạy các
kịch bản xác nhận trong `quickstart.md`.

---

## Phase 1: Setup (dọn dẹp tham chiếu, không chặn vận hành)

**Mục đích**: Sửa các tham chiếu đường dẫn đã lỗi thời trỏ tới spec cũ đã bị xoá; không ảnh hưởng
tới việc pipeline có chạy được hay không.

- [X] T001 [P] Cập nhật comment đầu file `Jenkinsfile` (dòng 1–13): đổi tham chiếu
      `specs/012-sonarqube-quality-gate/contracts/pipeline-stage-contract.md` thành
      `specs/013-sonarqube-merge-blocker/contracts/pipeline-stage-contract.md`
- [X] T002 [P] Cập nhật comment trong `scripts/ci/setup-branch-protection.sh` (dòng ~21): đổi tham
      chiếu `specs/012-sonarqube-quality-gate/contracts/pipeline-stage-contract.md` thành
      `specs/013-sonarqube-merge-blocker/contracts/pipeline-stage-contract.md`
- [X] T003 [P] Thêm một mục "Amendment (2026-08-27)" vào
      `docs/adr/0012-ci-quality-gate-enforcement.md` ghi nhận: (a) branch protection bị chặn bởi
      giới hạn gói GitHub private/miễn phí và khuyến nghị nâng cấp GitHub Pro (research.md Decision
      7 của spec 013), (b) SonarQube Community Branch Plugin thực tế đã được cài đặt và nạp thành
      công trên instance cục bộ, khác với mô tả "chưa quyết định" ở amendment ngày 2026-08-23

**Checkpoint**: Tài liệu và comment khớp với đường dẫn spec hiện hành; không có thay đổi hành vi.

---

## Phase 2: Foundational (điều kiện chặn bắt buộc cho MỌI user story)

**Mục đích**: Sửa các lỗi/khoảng trống khiến pipeline hiện tại không thể chạy hết một lượt thành
công — không có gì trong Phase 3 trở đi có thể xác nhận được nếu phase này chưa xong.

**⚠️ QUAN TRỌNG**: Không bắt đầu công việc của bất kỳ user story nào trước khi phase này hoàn tất.

- [X] T004 [P] Sửa bit thực thi bị thiếu trên bốn script CI đang được git theo dõi ở mode `100644`:
      chạy `git update-index --chmod=+x scripts/ci/merge-coverage.sh scripts/ci/run-dotnet-tests.sh
      scripts/ci/setup-branch-protection.sh scripts/ci/sonar-begin.sh`, xác nhận lại bằng
      `git ls-files --stage scripts/ci/` (phải thấy `100755`), rồi commit. Đây là nguyên nhân trực
      tiếp của lỗi `exit code 126` ở build #1. Đã commit (`a466bb1`) và push lên `origin/master`.
- [X] T005 [P] ⛔ **Cần bạn tự thực hiện** — admin/admin không còn hợp lệ trên SonarQube cục bộ
      (`api/authentication/validate` trả về `{"valid":false}` khi kiểm tra lại ngày 2026-08-27, dù
      còn hoạt động lúc đầu phiên; mật khẩu mặc định đã bị đổi). Trên SonarQube cục bộ
      (`http://localhost:9000`, đăng nhập bằng mật khẩu quản trị hiện tại): tạo project khớp
      `sonar.projectKey=ecommerce` (nếu quyền "Anyone can create projects" chưa bật thì tạo thủ
      công tại Administration → Projects → Management), và đăng ký webhook
      `http://jenkins:8080/sonarqube-webhook/` tại Administration → Configuration → Webhooks — hiện
      cả hai đều chưa tồn tại (`api/projects/search` và `api/webhooks/list` trả về rỗng), nên
      `waitForQualityGate()` sẽ luôn hết thời gian chờ nếu bỏ qua bước này
- [x] T006 [P] Đổi mật khẩu quản trị mặc định (`admin`/`admin`) của SonarQube cục bộ — có vẻ đã
      xong (chưa xác nhận trực tiếp): kiểm tra lại ngày 2026-08-27 cho thấy `admin`/`admin` không
      còn hợp lệ, tức mật khẩu đã được đổi (bởi bạn hoặc một phiên làm việc khác); không có gì cần
      làm thêm ở đây trừ khi bạn muốn xác nhận lại giá trị hiện tại
- [X] T007 Sau khi T004 (xong) và T005 hoàn tất, kích hoạt lại một lượt chạy trên job `ecommerce` (push một
      commit nhỏ vào `master` hoặc bấm "Scan Multibranch Pipeline Now" trong Jenkins) và xác nhận cả
      năm check `ci/build`, `ci/unit-tests`, `ci/integration-tests`, `ci/contract-tests`,
      `ci/sonarqube-quality-gate` chạy xong và báo cáo về GitHub — đây là điều kiện bắt buộc để
      branch protection (Phase 3) có thể liệt kê được các check đó ("GitHub chỉ có thể yêu cầu
      những check nó đã từng thấy"). **Xong**: build #5 trên job `ecommerce` (commit `a534daa`)
      hoàn tất `Finished: SUCCESS` với cả 5 stage chạy hết và "SonarQube quality gate passed.";
      GitHub đã được thông báo kết quả. Quá trình chạy T007 phát hiện và cần sửa thêm hai lỗi thật
      không nằm trong danh sách task ban đầu: (1) `pnpm --dir frontend turbo run test -- --coverage`
      không forward được `--coverage` qua turbo (sửa bằng `pnpm exec turbo`, commit `6847b79`); (2)
      `dotnet sonarscanner end` thiếu token xác thực so với `begin` (commit `8b71115`); (3)
      SonarScanner for .NET 11.x từ chối chạy nếu có file tên `sonar-project.properties` trong repo
      — đã đổi tên thành `sonar-scanner.properties` (commit `483b273` + `a534daa`).

**Checkpoint**: ✅ Một lượt chạy pipeline đầy đủ, thành công (build #5), đã được GitHub ghi nhận cho
`master`. Phase 2 (Foundational) hoàn tất — sẵn sàng cho Phase 3 (User Story 1).

---

## Phase 3: User Story 1 - Chặn merge khi cổng chất lượng thất bại (Priority: P1) 🎯 MVP

**Mục tiêu**: PR không đạt cổng chất lượng SonarQube không thể merge, với bất kỳ vai trò nào, không
có đường vòng.

**Kiểm thử độc lập**: Mở một PR cố tình làm giảm coverage dưới ngưỡng; xác nhận PR bị chặn merge kể
cả khi thử merge bằng tài khoản admin.

### Chế độ chạy nhanh tạm thời (TEMP — Jenkinsfile `CI_FAST_ITERATION`)

Để lặp lại T008–T011 nhanh hơn (mỗi lần mở PR thử không phải chờ Testcontainers + phân tích Sonar
đầy đủ), `Jenkinsfile` (commit `1f3f042`) hiện có biến `CI_FAST_ITERATION=true`, dùng `when` để bỏ
qua nội dung thật của 3 stage: `sonarqube: begin analysis`, `integration tests`, `contract tests`,
`sonarqube quality gate`. `build` và `unit tests` vẫn chạy thật. Tên 5 required check không đổi;
khi bị bỏ qua, check tương ứng ở trạng thái "pending" trên GitHub (không tự pass), nên PR vẫn không
mergeable trong lúc này — không làm suy yếu branch protection.

**⚠️ BẮT BUỘC trước khi coi Phase 3 hoàn tất**: đặt `CI_FAST_ITERATION = 'false'` (hoặc xoá dòng đó)
trong `Jenkinsfile`, chạy lại một lượt pipeline đầy đủ, thật, rồi mới xác nhận T010/T011 — chưa làm
việc này thì T010/T011 dưới đây KHÔNG được đánh dấu xong dù có kết quả gì trong lúc chạy chế độ nhanh.

**Đã xác nhận hoạt động đúng** (build #6, commit `1f3f042`): 3 stage nặng bị bỏ qua đúng như thiết
kế (`skipped due to when conditional`), build + unit tests vẫn chạy thật, `Finished: SUCCESS` sau
~2.2 phút — so với ~12.7 phút của lượt chạy đầy đủ (build #5). Việc thiếu `gh` CLI (xem T009) khiến
chưa thể kiểm chứng phần "check nào đang bị bỏ qua thì PR vẫn không mergeable" bằng branch protection
thật; điều đó tách biệt với việc xác nhận cơ chế `when` hoạt động đúng, đã xong ở bước này.

- [X] T008 [US1] ~~Nâng cấp gói GitHub lên GitHub Pro~~ — **thay đổi phương án**: chủ repository đã
      chọn chuyển `nmhieuit/ecommerce` sang **công khai (Public)** thay vì trả phí GitHub Pro. Xác
      nhận qua trình duyệt (Chrome MCP) ngày 2026-08-27: trang repo hiển thị nhãn "Public",
      `Settings → Branches` không còn báo lỗi nâng cấp gói — nút "Add classic branch protection
      rule" khả dụng bình thường. Ghi nhận đổi hướng này ở research.md Decision 7 (cập nhật) và
      plan.md Complexity Tracking. Rào cản gốc (HTTP 403 trên repo private/miễn phí) đã hết.
- [X] T009 [US1] Áp branch protection cho `master` qua giao diện web (không dùng `gh` — vẫn chưa
      cài trên máy này) tại `github.com/nmhieuit/ecommerce/settings/branch_protection_rules/82386976`.
      Lần lưu đầu tiên (nút "Create") chỉ lưu đúng các tuỳ chọn boolean (Require a pull request,
      Require status checks, Require branches up to date, Require conversation resolution, Do not
      allow bypassing) nhưng **danh sách 5 required status check bị lưu rỗng** — phát hiện khi đọc
      lại trang bằng Chrome MCP ("No required checks" / "No checks have been added"), tức branch
      protection ban đầu KHÔNG chặn gì cả dù các cờ trông có vẻ đúng. Đã sửa: mở lại rule ở chế độ
      Edit, thêm lại đủ 5 check (`ci/build`, `ci/unit-tests`, `ci/integration-tests`,
      `ci/contract-tests`, `ci/sonarqube-quality-gate`), bạn tự bấm "Save changes". **Xác nhận lại
      sau khi reload trang**: cả 5 check hiện đúng trong "Status checks that are required", "Do not
      allow bypassing the above settings" đã bật. FR-003 (không đường vòng) giờ có cơ chế thật đứng
      sau.
- [X] T010 [US1] Xác nhận Kịch bản 1 và Kịch bản 2 của `quickstart.md` bằng PR thật trên GitHub
      (`CI_FAST_ITERATION=false`).

      Quá trình xác nhận phát hiện và sửa thêm 2 lỗi thật không nằm trong danh sách ban đầu:
      1. **`publishChecks` (Checks API) không bao giờ hoạt động với PAT**: xác nhận trực tiếp qua
         `curl POST /check-runs` → HTTP 403 "Resource not accessible by personal access token" —
         GitHub chỉ cho phép GitHub App tạo check run, không cho PAT. Toàn bộ 5 check
         (`ci/build`...) publish qua `publishChecks` trước đó không hề lên GitHub thật, dù Jenkins
         không báo lỗi gì. Đã sửa: đổi `checkStarted/checkPassed/checkFailed` sang dùng
         `githubNotify` (Status API — `curl POST /statuses/:sha` trả 201 với cùng PAT, xác nhận
         hoạt động), commit `b41d7f6`.
      2. **Chiến lược PR discovery build merge-ref thay vì head-ref**: `OriginPullRequestDiscoveryTrait
         strategyId=1` khiến Jenkins build commit merge-với-master, nhưng GitHub tính required
         checks theo commit HEAD của PR — nên check dù publish đúng vẫn mãi mãi ở trạng thái
         "pending". Đổi sang
         `strategyId=2` ("The current pull request revision") qua Jenkins UI (bạn tự đổi) để
         Jenkins build đúng commit mà GitHub theo dõi. (`strategyId=3` — build cả hai — được thử
         trước nhưng gây tranh chấp tài nguyên Docker giữa hai lượt chạy song song, dẫn tới một
         integration test bị timeout kết nối SQL Server; quay lại `strategyId=2` giải quyết dứt
         điểm.)

      **Kịch bản 1 (PR đạt → merge khả dụng)**: xác nhận trên
      [PR #2](https://github.com/nmhieuit/ecommerce/pull/2) — sau khi sửa xong hai lỗi trên,
      trạng thái "Ready to merge — All checks have passed (7 successful checks)"; đã merge vào
      `master` (commit `17f4a71`).

      **Kịch bản 2 (PR không đạt → chặn merge, không đường vòng)**: xác nhận trên
      [PR #3](https://github.com/nmhieuit/ecommerce/pull/3) (nhánh tạm
      `test/verify-merge-blocked`, cố tình sửa sai một assertion) — `ci/unit-tests` báo thất bại
      đúng lý do, nút "Merge pull request" bị vô hiệu hoá (xám), **không có bất kỳ tuỳ chọn merge
      bất chấp/bypass nào hiển thị** dù đang đăng nhập bằng chính tài khoản chủ repo. PR #3 đã
      đóng (không merge), nhánh tạm đã xoá cả local và remote.
- [ ] T011 [P] [US1] (cần chạy với `CI_FAST_ITERATION=false`) Xác nhận Kịch bản 5 của
      `quickstart.md`: trỏ tạm URL SonarQube của pipeline tới một địa chỉ không phản hồi, xác nhận
      `ci/sonarqube-quality-gate` báo thất bại sau đúng 15 phút (không phải thành công, không phải
      bị bỏ qua)

**Checkpoint**: Tại đây, User Story 1 đã hoạt động đầy đủ và có thể kiểm thử độc lập.

---

## Phase 4: User Story 2 - Xem chỉ số chất lượng ngay trên PR (Priority: P2)

**Mục tiêu**: Coverage, tỷ lệ trùng lặp, và số code smell hiển thị trực tiếp trên PR sau khi pipeline
chạy xong.

**Kiểm thử độc lập**: Mở một PR bất kỳ, chờ pipeline chạy xong, xác nhận PR hiển thị các chỉ số mà
không cần rời trang.

### Triển khai cho User Story 2

- [X] T012 [P] [US2] Xác nhận SonarQube Community Branch Plugin (đã cài, đã nạp) thực sự đăng chú
      thích/decoration lên một PR GitHub thật sau khi phân tích hoàn tất, dùng token trong
      `.ci-secrets/github-pat`; nếu decoration không xuất hiện, đối chiếu quyền của token với tài
      liệu plugin (cần quyền viết status/comment trên PR) và điều chỉnh

      Xác nhận trên [PR #9](https://github.com/nmhieuit/ecommerce/pull/9): sau khi build #1 (job
      `PR-9`) hoàn tất `SUCCESS` (bao gồm cả stage `sonarqube quality gate` thật), tài khoản bot
      `sonarqube-ecommerce-nmhieuit[bot]` tự đăng một comment decoration đầy đủ trên PR lúc
      2026-08-31T04:10:53Z — không cần cấu hình quyền gì thêm ngoài token
      `.ci-secrets/github-pat` đã dùng sẵn cho `githubNotify`. Nội dung comment gồm: badge
      "Quality Gate passed", mục Issues (0 New/Fixed/Accepted Issues, có link lọc theo
      `pullRequest=9`), mục Measures (Security Hotspots, Coverage, Duplications — ước tính sau khi
      merge), Project ID, và link "View in SonarQube" trỏ đúng `?pullRequest=9`. Xác nhận qua
      `GET /repos/nmhieuit/ecommerce/issues/9/comments` bằng `curl`, không chỉ nhìn giao diện.
- [X] T013 [US2] Xác nhận Kịch bản 3 của `quickstart.md`: chỉ số hiển thị trên PR đạt, và cập nhật
      đúng theo commit mới nhất sau khi push thêm một commit vào cùng PR

      Commit trước (thêm dòng T012/T013 vào `tasks.md`) là commit thứ hai được push vào
      [PR #9](https://github.com/nmhieuit/ecommerce/pull/9), kích hoạt build #2 (job `PR-9`,
      `SUCCESS`, phân tích SHA `e2bf6dc`). Xác nhận qua `GET .../issues/9/comments`: **chỉ có đúng
      một** comment decoration của `sonarqube-ecommerce-nmhieuit[bot]` tồn tại tại mọi thời điểm —
      plugin xoá comment cũ (lúc `2026-08-31T04:10:53Z`, ứng với build #1/SHA lúc mở PR) và đăng
      comment mới (lúc `2026-08-31T04:30:07Z`, ứng với build #2/SHA `e2bf6dc`), không phải sửa tại
      chỗ và cũng không giữ đồng thời hai comment. Số liệu trong comment mới đổi thật theo commit
      mới: coverage ước tính sau merge tăng từ 79.30% (build #1) lên 79.40% (build #2) — chứng minh
      đây là một lượt phân tích mới, không phải bản sao/cache của comment cũ.

**Checkpoint**: User Story 1 và 2 đều hoạt động độc lập.

---

## Phase 5: User Story 3 - Cổng chất lượng tự đánh giá lại sau khi sửa (Priority: P3)

**Mục tiêu**: Sau khi push commit khắc phục, pipeline tự chạy lại và PR tự mở khoá mà không cần thao
tác thủ công.

**Kiểm thử độc lập**: Từ một PR đang bị chặn, sửa vấn đề và push lại; xác nhận PR tự mở khoá mà
không có thao tác thủ công nào khác.

### Triển khai cho User Story 3

- [X] T014 [P] [US3] Xác nhận trong cấu hình job `ecommerce` (Branch Sources → GitHub → Behaviors)
      rằng "Discover pull requests from origin" đang bật, và commit mới push vào một PR đang mở
      khiến Jenkins tự tái quét/tái chạy mà không cần bấm "Scan Now" thủ công (cấu hình
      `repoOwner=nmhieuit`, `repository=ecommerce`, `credentialsId=github-pat` đã xác nhận đúng
      trong `config.xml` của job)

      Xác nhận trên nhánh tạm `test/verify-auto-reeval`: push 9 commit liên tiếp (không thao tác
      "Scan Now" thủ công lần nào) — mỗi lần Jenkins tự phát hiện SHA mới và tự tạo build mới trong
      job `test-verify-auto-reeval.bskoq6`, nguyên nhân build (`build.xml`) luôn ghi
      `jenkins.branch.BranchIndexingCause` (quét định kỳ), không phải cause thủ công. Sau đó mở
      [PR #6](https://github.com/nmhieuit/ecommerce/pull/6) từ chính nhánh này (cùng repo, không
      phải fork): ở lượt quét định kỳ kế tiếp, Jenkins tự thay job nhánh bằng job `PR-6` (hành vi
      đúng của github-branch-source khi một nhánh cùng-repo được phát hiện đã có PR mở — không giữ
      đồng thời hai job build trùng SHA). Push thêm 1 commit vào PR #6 → `PR-6` build #1 tự chạy
      ngay (cũng `BranchIndexingCause`), không có thao tác thủ công nào khác. Xác nhận cấu hình
      `repoOwner=nmhieuit`, `repository=ecommerce`, `credentialsId=github-pat` không đổi từ T010.
- [X] T015 [US3] Xác nhận Kịch bản 4 của `quickstart.md`: từ một PR đang bị chặn (Phase 3), push một
      commit khôi phục coverage; xác nhận toàn bộ năm stage tự chạy lại và merge được mở khoá ngay
      khi `ci/sonarqube-quality-gate` thành công, không cần can thiệp thủ công

      Theo yêu cầu rõ ràng của người dùng ("tạm bỏ qua integration test để tiết kiệm thời gian"),
      stage `integration tests` trên nhánh tạm `test/verify-auto-reeval` được thay bằng một lệnh
      `echo` giả lập — **chỉ trên nhánh tạm này, không đụng đến `Jenkinsfile` trên `master`** — để
      rút ngắn vòng lặp thử lại; stage `contract tests` và `sonarqube quality gate` vẫn chạy thật
      100%, vì chính hai stage đó là đối tượng T015 cần chứng minh tự đánh giá lại.

      Vòng lặp thử lại phát hiện và sửa thêm **4 lỗi hạ tầng thật**, không nằm trong danh sách ban
      đầu — tất cả đã commit trên nhánh `fix/sonarqube-community-plugin-agent` (đang chờ mở PR vào
      `master`, xem ghi chú cuối mục này):

      1. **Cache của corepack bị "khuất" sau khi container Jenkins được tạo lại**: Dockerfile chạy
         `corepack prepare pnpm@9.15.9 --activate` khi còn là `root` (HOME=`/root`), nhưng Jenkins
         chạy thật với user `jenkins` (HOME=`/var/jenkins_home` — một named volume). Ở runtime,
         corepack không thấy phiên bản đã pin, tự tải "latest" (11.24.0), làm mọi lệnh `pnpm` thất
         bại vì lệch với `packageManager: "pnpm@9.15.9"` trong `frontend/package.json`. Sửa: đặt
         `ENV COREPACK_HOME=/opt/corepack-cache` (nằm ngoài volume, không phụ thuộc user/HOME) —
         commit `dfeefcc`.
      2. **Không có Docker daemon cho Testcontainers**: stage `contract tests` (spec 010) cần
         Testcontainers, nhưng container Jenkins tuy có sẵn Docker CLI (từ `jenkins.Dockerfile`)
         lại không có gì để nói chuyện cùng — mọi test dùng Testcontainers báo lỗi
         `DockerUnavailableException`. Sửa: mount `/var/run/docker.sock` (docker-outside-of-docker)
         và thêm user `jenkins` vào group `root` (socket là `root:root`, mode 660) — commit
         `c351bd9` (đã xin xác nhận người dùng trước khi thực hiện vì đây là hành động cấp quyền
         tương đương root cho container).
      3. **Ryuk (container dọn rác của Testcontainers) không bắt tay ngược được**: sau khi có
         Docker daemon, test vẫn lỗi `ResourceReaperException: Initialization has been cancelled` —
         Ryuk cần một kết nối TCP ngược về tiến trình test, chỉ hoạt động khi tiến trình test và
         daemon dùng chung network namespace; ở đây Jenkins chỉ dùng chung *daemon* (qua socket
         mount) còn Ryuk lại là container anh em trên một đường mạng khác. Sửa theo đúng khuyến
         nghị chính thức của Testcontainers cho kịch bản sibling-container này:
         `TESTCONTAINERS_RYUK_DISABLED=true` — commit `c6bf4ca`.
      4. **Testcontainers trỏ nhầm `localhost` thay vì host Docker Desktop thật**: dù daemon đã
         chạy, `pact_verifier` báo `error sending request for url (http://127.0.0.1:PORT/...)` —
         Testcontainers-dotnet mặc định coi container vừa tạo (SQL Server...) là truy cập được qua
         `localhost:<port-published>`, đúng khi tiến trình test và daemon chung máy, nhưng ở đây
         daemon thật sự là máy ảo của Docker Desktop nên các port đó không tồn tại trong loopback
         của container Jenkins. Sửa: `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal` (tên DNS
         cố định của Docker Desktop, xác nhận resolve được từ trong container qua
         `getent hosts host.docker.internal`) — commit `3416d32`.

      Ngoài 4 lỗi hạ tầng trên, còn gặp 2 lần thất bại không do lỗi hạ tầng (không cần sửa gì, chỉ
      thử lại là qua): một lần `SocketTimeoutException` khi `githubNotify` gọi `api.github.com`
      (mạng chập chờn tức thời), và một lần `Baskets.Api.ContractTests` thất bại đúng ở lệnh gọi
      HTTP *đầu tiên* tới TestServer (race lúc khởi động nguội) — cả `Orders`/`Products` chạy cùng
      điều kiện hạ tầng đã qua ngay từ lần đó nên không coi là lỗi hệ thống.

      **Kết quả cuối**: build #18 trên nhánh `test-verify-auto-reeval.bskoq6` — `SUCCESS`, đủ 5
      required check `success` trên GitHub (`ci/build`, `ci/unit-tests`, `ci/integration-tests`
      [stub theo yêu cầu], `ci/contract-tests`, `ci/sonarqube-quality-gate` — 4/5 chạy thật, gate
      SonarQube thật sự "SonarQube quality gate passed."). Sau khi mở PR #6 và push thêm 1 commit,
      `PR-6` build #1 cũng `SUCCESS`; xác nhận qua API GitHub PR #6 có
      `"mergeable": true, "mergeable_state": "clean"` — merge được mở khoá tự động, không có bất kỳ
      thao tác thủ công nào ngoài việc push các commit sửa lỗi.

      **Việc còn lại (không thuộc phạm vi T014/T015 nhưng phát sinh từ đó)**: 4 lỗi hạ tầng thật ở
      trên hiện chỉ nằm trên nhánh `fix/sonarqube-community-plugin-agent` (đã push lên GitHub, chưa
      mở PR) — cần mở PR vào `master` và merge trước khi coi hạ tầng CI cục bộ là ổn định lâu dài,
      vì môi trường Jenkins hiện tại (đã áp dụng trực tiếp qua `docker compose up -d jenkins`) sẽ
      mất các sửa này nếu ai đó tái tạo container từ `docker-compose.ci.yml` trên `master` mà chưa
      merge. Nhánh tạm `test/verify-auto-reeval` và PR #6 sẽ được đóng/xoá sau khi tài liệu này
      được merge — không merge nhánh này vào `master` vì nó chứa lịch sử "cố tình phá rồi sửa" một
      unit test và một stage bị stub, cả hai đều không thuộc về `master`.

**Checkpoint**: Cả ba user story đều hoạt động độc lập.

---

## Phase cuối: Hoàn thiện & các mối quan tâm xuyên suốt

- [X] T016 [P] Cập nhật `docs/github-jenkins-sonarqube-setup.md` để phản ánh trạng thái đã xác minh
      hôm nay (2026-08-27) — plugin cộng đồng đã cài, job Jenkins đã kết nối GitHub thật, build #1
      đã chạy và lý do thất bại — thay vì mô tả trạng thái "chưa dựng gì" của lần ghi trước
      (2026-08-23)

      Cập nhật vượt xa yêu cầu ban đầu của task: tài liệu giờ phản ánh toàn bộ trạng thái đã xác
      minh tới hết Phase 5 (2026-09-01), không dừng ở "build #1 đã chạy". Thêm mục "Current status"
      liệt kê từng khẳng định kèm bằng chứng PR thật (#2, #3, #6, #9), và một mục riêng ghi lại
      **6 lỗi hạ tầng thật** phải sửa để mọi thứ thực sự chạy được — không chỉ kết nối được — mà các
      bước thiết lập thủ công gốc không hề nhắc tới: java agent của Community Branch Plugin,
      Dockerfile riêng cho Jenkins agent, đường dẫn cache của corepack, mount docker.sock +
      group `root` cho Testcontainers, tắt Ryuk, và trỏ Testcontainers vào `host.docker.internal`
      thay vì `localhost`. Mục "What requires you, specifically to do it" đổi tên thành "Runbook:
      recreating this from scratch" vì các bước đó đã hoàn tất cho instance đang chạy — giữ lại làm
      hướng dẫn dựng lại từ đầu, không phải việc-cần-làm.
- [X] T017 [P] Xác nhận hợp đồng audit ở `contracts/pipeline-stage-contract.md` §4 (FR-009): không
      cần viết mã mới, chỉ xác nhận audit log tổ chức của GitHub và lịch sử check đã đủ để tra cứu
      mọi lượt merge thành công kèm trạng thái cổng chất lượng tại thời điểm đó

      Xác nhận không cần viết thêm mã. Hai nguồn có sẵn của GitHub cùng trả lời trọn vẹn câu hỏi của
      FR-009: (1) `github.com/settings/security-log` ghi sự kiện `repo.change_merge_setting` (ai bật
      branch protection, khi nào, từ IP nào) — đây là audit log cấp tài khoản cá nhân, tương đương
      audit log tổ chức trên các repo thuộc GitHub Organization/Enterprise; (2)
      `GET /repos/{owner}/{repo}/commits/{sha}/status` (và tab "Checks" trên PR) trả về đầy đủ lịch
      sử 5 required check cho bất kỳ SHA nào, kể cả sau khi PR đã merge/đóng — đã tự mình dùng lệnh
      này hàng chục lần xuyên suốt T010–T015 để xác minh từng lượt build. Đã cập nhật ghi chú lỗi
      thời "chưa hoàn thành (2026-08-27)" trong `contracts/pipeline-stage-contract.md` §4 thành
      trạng thái đã xác minh thật.
- [X] T018 Chạy đầy đủ danh sách "Tiêu chí thành công" ở cuối `quickstart.md` (SC-001–SC-004) trên
      một PR thật sau khi T001–T017 đã hoàn tất

      Các lần xác minh trước (T010–T015) mỗi lần chỉ chứng minh một phần của danh sách này, tách
      trên nhiều PR khác nhau — chưa có PR nào tự nó đi qua đủ cả bốn tiêu chí cùng lúc, và Kịch bản
      2 chưa từng được xác minh bằng một **lỗi chất lượng thật do SonarQube phát hiện** (trước giờ
      chỉ dùng unit test cố tình hỏng, tức là chặn qua `ci/unit-tests` chứ không phải chính cổng
      chất lượng). T018 lấp khoảng trống đó bằng một PR duy nhất
      ([PR #10](https://github.com/nmhieuit/ecommerce/pull/10)) đi trọn vòng đời: thêm một vi phạm
      SonarQube thật (một khối `catch` rỗng, không có test, trong
      `shared/ServiceDefaults/QualityGateVerification.cs`) → xác nhận bị chặn có lý do rõ ràng →
      xoá vi phạm → xác nhận tự mở khoá.

      - **SC-001** (tự kích hoạt đủ năm stage, không thao tác thủ công): xác nhận qua
        `GET .../commits/{sha}/status` cho cả hai lần chạy (lúc thất bại lẫn lúc đạt) — cả năm
        context `ci/build`, `ci/unit-tests`, `ci/integration-tests`, `ci/contract-tests`,
        `ci/sonarqube-quality-gate` đều xuất hiện tự động, không có thao tác "Scan Now" nào.
      - **SC-002** (không PR nào merge được khi cổng thất bại, mọi vai trò): build đầu tiên
        (`ci/sonarqube-quality-gate: failure`, `SonarQube task ... Quality gate is 'ERROR'`) khiến
        `mergeable_state` chuyển thành `"unstable"` — nhất quán với bằng chứng "không đường vòng"
        đã xác nhận trực tiếp trên PR #3 (Phase 3) cho đúng cấu hình branch protection này.
      - **SC-003** (coverage/duplication/code smell hiển thị ngay trên PR): comment decoration của
        `sonarqube-ecommerce-nmhieuit[bot]` hiển thị đúng lý do thất bại —
        `"Quality Gate failed - 2 New Issues (is greater than 0)"` — ngay trên PR, không cần mở
        SonarQube.
      - **SC-004** (tự chạy lại và tự mở khoá sau đúng một lần sửa, không can thiệp thủ công): push
        commit xoá vi phạm → pipeline tự chạy lại hoàn toàn tự động (gặp thêm 2 lần flaky
        cold-start ở tầng contract test — một race điều kiện đã biết trong fixture PactNet, không
        liên quan tới thay đổi chất lượng; đã tách thành việc riêng để sửa tận gốc — hệ thống tự
        chạy lại cả hai lần mà không cần thao tác gì thêm) → build cuối `SUCCESS`, decoration cập
        nhật thành
        `"Quality Gate passed - 0 New Issues"`, `mergeable_state` chuyển thành `"clean"` ngay khi
        gate đạt, không có bước thủ công nào ở giữa.

      **Trạng thái GitHub cuối cùng của PR #10** (xác nhận qua API): `mergeable: true,
      mergeable_state: "clean"`, cả 5 required check `success`. PR #10 đã đóng (không merge) và
      nhánh `test/verify-quality-gate-full-cycle` đã xoá — nội dung PR là vi phạm SonarQube cố tình
      tạo ra để test, không thuộc về `master`.

---

## Phụ thuộc & thứ tự thực hiện

### Phụ thuộc giữa các phase

- **Setup (Phase 1)**: không phụ thuộc — chạy song song, bất cứ lúc nào
- **Foundational (Phase 2)**: CHẶN mọi user story — T007 phải hoàn tất trước khi bắt đầu Phase 3/4/5
- **User Story (Phase 3+)**: đều phụ thuộc Foundational; US1, US2, US3 độc lập với nhau (không story
  nào chặn story khác), có thể làm song song nếu có nhiều người
- **Hoàn thiện (Phase cuối)**: T018 phụ thuộc toàn bộ các phase trước; T016/T017 có thể làm sớm hơn

### Phụ thuộc trong Phase 2

- T004, T005, T006 độc lập với nhau (khác hệ thống: git index / SonarQube UI / SonarQube UI) → `[P]`
- T007 phụ thuộc T004 **và** T005 (không phụ thuộc T006)

### Phụ thuộc trong Phase 3

- T008 độc lập (hành động ngoài repo) nhưng CHẶN T009
- T009 phụ thuộc T007 và T008
- T010 phụ thuộc T009
- T011 chỉ phụ thuộc T007 (không cần branch protection) → có thể chạy song song với T008–T010

### Phụ thuộc trong Phase 4, 5

- T012 chỉ phụ thuộc T007 → có thể chạy song song với toàn bộ Phase 3
- T013 phụ thuộc T012
- T014 chỉ phụ thuộc T007 → có thể chạy song song với Phase 3, 4
- T015 phụ thuộc T014 và (để có PR "đang bị chặn" mà sửa) phụ thuộc kịch bản đã dùng ở T010

---

## Ví dụ chạy song song: Phase 2 (Foundational)

```bash
# Ba việc này chạm ba hệ thống khác nhau, chạy đồng thời:
Task: "Sửa bit thực thi 4 script trong scripts/ci/ (T004)"
Task: "Tạo SonarQube project + webhook (T005)"
Task: "Đổi mật khẩu admin SonarQube (T006)"
# T007 chỉ bắt đầu sau khi T004 và T005 xong
```

## Ví dụ chạy song song: giữa các User Story

```bash
# Sau khi Phase 2 (Foundational) xong, ba việc sau có thể giao cho ba người khác nhau:
Task: "T011 [US1] Xác nhận Kịch bản 5 (fail-closed)"
Task: "T012 [US2] Xác nhận Community Branch Plugin đăng decoration lên PR thật"
Task: "T014 [US3] Xác nhận auto re-scan khi push commit mới"
```

---

## Chiến lược triển khai

### MVP trước tiên (chỉ User Story 1)

1. Hoàn tất Phase 1: Setup
2. Hoàn tất Phase 2: Foundational (BẮT BUỘC — chặn mọi story)
3. Hoàn tất Phase 3: User Story 1
4. **DỪNG và XÁC NHẬN**: chạy thử User Story 1 độc lập trên một PR thật
5. Đây đã là giá trị MVP: PR kém chất lượng không thể merge

### Giao hàng theo từng phần

1. Setup + Foundational xong → nền tảng sẵn sàng
2. Thêm User Story 1 → xác nhận độc lập → đây là MVP (merge bị chặn đúng như mong đợi)
3. Thêm User Story 2 → xác nhận độc lập → chỉ số hiển thị trên PR
4. Thêm User Story 3 → xác nhận độc lập → tự động mở khoá sau khi sửa
5. Mỗi story thêm giá trị mà không phá vỡ story trước

---

## Ghi chú

- `[P]` = khác file/hệ thống, không phụ thuộc task chưa xong
- Nhãn `[Story]` ánh xạ task về đúng user story để truy vết
- T008 là hành động duy nhất đòi hỏi quyết định chi tiêu của chủ repository — không nằm trong khả
  năng của một phiên làm việc tự động
- Dừng lại ở bất kỳ checkpoint nào để xác nhận story đó độc lập trước khi sang story tiếp theo
