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
- [ ] T009 [US1] ⛔ **Chặn — thiếu GitHub CLI (`gh`)**: không tìm thấy `gh` ở bất kỳ đâu trên máy
      này (kiểm tra cả Git Bash và PowerShell), nên `scripts/ci/setup-branch-protection.sh` — vốn
      bọc `gh api` — chưa thể tự chạy được trong phiên này, dù T008 đã xong (repo đã public, không
      còn 403). Do repo giờ là public, có thể làm bằng một trong ba cách: (a) bạn tự cài `gh`
      (`winget install --id GitHub.cli`) rồi chạy
      `gh auth login && scripts/ci/setup-branch-protection.sh nmhieuit/ecommerce master`; (b) báo
      tôi cài `gh` giúp (thao tác cài phần mềm — sẽ xin xác nhận trước); hoặc (c) cấu hình trực
      tiếp qua giao diện web `github.com/nmhieuit/ecommerce/settings/branches` → "Add classic
      branch protection rule" trên nhánh `master`, liệt kê đủ 5 check
      (`ci/build`, `ci/unit-tests`, `ci/integration-tests`, `ci/contract-tests`,
      `ci/sonarqube-quality-gate`) và bật "Do not allow bypassing the above settings" — nội dung
      tương đương những gì script làm qua API, có thể thao tác qua trình duyệt đã mở sẵn (sẽ xin
      xác nhận trước khi bấm lưu vì đây là thay đổi cấu hình repo thật). Sau khi xong, xác nhận cả
      năm check được liệt kê là required và tuỳ chọn bypass bị tắt.
- [ ] T010 [US1] (cần chạy với `CI_FAST_ITERATION=false`) Xác nhận Kịch bản 1 và Kịch bản 2 của
      `quickstart.md`: một PR đạt thì merge khả dụng sau khi `ci/sonarqube-quality-gate` thành
      công; một PR có unit test hỏng hoặc coverage dưới ngưỡng thì bị chặn merge và không vai trò
      nào (kể cả admin) có tuỳ chọn "merge bất chấp"
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

- [ ] T012 [P] [US2] Xác nhận SonarQube Community Branch Plugin (đã cài, đã nạp) thực sự đăng chú
      thích/decoration lên một PR GitHub thật sau khi phân tích hoàn tất, dùng token trong
      `.ci-secrets/github-pat`; nếu decoration không xuất hiện, đối chiếu quyền của token với tài
      liệu plugin (cần quyền viết status/comment trên PR) và điều chỉnh
- [ ] T013 [US2] Xác nhận Kịch bản 3 của `quickstart.md`: chỉ số hiển thị trên PR đạt, và cập nhật
      đúng theo commit mới nhất sau khi push thêm một commit vào cùng PR

**Checkpoint**: User Story 1 và 2 đều hoạt động độc lập.

---

## Phase 5: User Story 3 - Cổng chất lượng tự đánh giá lại sau khi sửa (Priority: P3)

**Mục tiêu**: Sau khi push commit khắc phục, pipeline tự chạy lại và PR tự mở khoá mà không cần thao
tác thủ công.

**Kiểm thử độc lập**: Từ một PR đang bị chặn, sửa vấn đề và push lại; xác nhận PR tự mở khoá mà
không có thao tác thủ công nào khác.

### Triển khai cho User Story 3

- [ ] T014 [P] [US3] Xác nhận trong cấu hình job `ecommerce` (Branch Sources → GitHub → Behaviors)
      rằng "Discover pull requests from origin" đang bật, và commit mới push vào một PR đang mở
      khiến Jenkins tự tái quét/tái chạy mà không cần bấm "Scan Now" thủ công (cấu hình
      `repoOwner=nmhieuit`, `repository=ecommerce`, `credentialsId=github-pat` đã xác nhận đúng
      trong `config.xml` của job)
- [ ] T015 [US3] Xác nhận Kịch bản 4 của `quickstart.md`: từ một PR đang bị chặn (Phase 3), push một
      commit khôi phục coverage; xác nhận toàn bộ năm stage tự chạy lại và merge được mở khoá ngay
      khi `ci/sonarqube-quality-gate` thành công, không cần can thiệp thủ công

**Checkpoint**: Cả ba user story đều hoạt động độc lập.

---

## Phase cuối: Hoàn thiện & các mối quan tâm xuyên suốt

- [ ] T016 [P] Cập nhật `docs/github-jenkins-sonarqube-setup.md` để phản ánh trạng thái đã xác minh
      hôm nay (2026-08-27) — plugin cộng đồng đã cài, job Jenkins đã kết nối GitHub thật, build #1
      đã chạy và lý do thất bại — thay vì mô tả trạng thái "chưa dựng gì" của lần ghi trước
      (2026-08-23)
- [ ] T017 [P] Xác nhận hợp đồng audit ở `contracts/pipeline-stage-contract.md` §4 (FR-009): không
      cần viết mã mới, chỉ xác nhận audit log tổ chức của GitHub và lịch sử check đã đủ để tra cứu
      mọi lượt merge thành công kèm trạng thái cổng chất lượng tại thời điểm đó
- [ ] T018 Chạy đầy đủ danh sách "Tiêu chí thành công" ở cuối `quickstart.md` (SC-001–SC-004) trên
      một PR thật sau khi T001–T017 đã hoàn tất

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
