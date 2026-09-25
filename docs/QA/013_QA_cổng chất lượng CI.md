# QA: Cổng chất lượng SonarQube làm rào chặn merge

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

Khác mọi spec trước: đây là cấu hình CI/CD thật (Jenkins + SonarQube + GitHub branch protection), đã được xác minh
bằng PR thật lúc viết feature (2026-08-27 → 09-01, `tasks.md` T001–T018). QA ở đây không dựng lại Jenkins/SonarQube
(cần setup tương tác có mật khẩu/token thật) mà kiểm tra cơ chế đó **có còn hoạt động thật hôm nay hay không**, bằng API
công khai của GitHub (không cần token).

## Luồng happy-case đã rà soát (theo thiết kế, có bằng chứng PR thật lúc viết feature)

1. Mọi PR nhắm `master` tự kích hoạt 5 stage tuần tự: `ci/build` → `ci/unit-tests` → `ci/integration-tests` →
   `ci/contract-tests` → `ci/sonarqube-quality-gate`.
2. Cổng chất lượng thất bại thì PR bị chặn merge, không có đường vòng cho bất kỳ vai trò nào (`enforce_admins: true`).
3. Chỉ số chất lượng (coverage/duplication/code smell) hiển thị ngay trên PR qua comment của SonarQube Community
   Branch Plugin.
4. Sửa xong, push lại thì cả chuỗi tự chạy lại và PR tự mở khoá.
5. Audit "ai đổi cấu hình chặn merge, cổng chất lượng ra sao lúc merge" trả lời được bằng security log + `commits/{sha}/status` của GitHub.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

Muốn chạy lại Kịch bản 1–5 của `quickstart.md` cần dựng lại toàn bộ hạ tầng tương tác (Jenkins wizard, đăng nhập
SonarQube, PAT mới) — ngoài khả năng phiên QA này.

### Tự động — kiểm tra tình trạng chặn merge hiện tại bằng API công khai (không cần token)

| Cần xác nhận | Lệnh | Kỳ vọng nếu cổng đang hoạt động | **Đã quan sát (2026-09-23)** |
|---|---|---|---|
| SC-001 — PR gần nhất có tự kích hoạt 5 check | `curl -s https://api.github.com/repos/nmhieuit/ecommerce/commits/master/status` | `total_count: 5` (`ci/build`…`ci/sonarqube-quality-gate`) | **`total_count: 0`**; 13 commit merge gần nhất (PR #32 → #45) đều 0; mốc chuyển tiếp: PR #17 (`bb61212`, 2026-09-02) còn 3 check `success`, PR #18 trở đi (≥ 27 PR) không có check nào |
| SC-002 — có rule nào đang chặn merge `master` | `curl -s https://api.github.com/repos/nmhieuit/ecommerce/rules/branches/master` | Mảng không rỗng, có `required_status_checks` 5 context | **`[]`** — không rule nào đang áp dụng |
| Trang branch báo "protected" | Mở `https://github.com/nmhieuit/ecommerce/branches` | Tooltip "protected" cạnh `master` | Có tooltip nhưng gây hiểu nhầm (rule tồn tại nhưng không còn chặn gì) |
| Jenkins/SonarQube cục bộ | `docker ps -a` | Container `jenkins`/`sonarqube` chạy | Rỗng; volume dữ liệu còn |
| Token CI còn hạn | `curl -H "Authorization: Bearer <.ci-secrets/github-pat>" …/branches/master/protection` | `200` | `401 Bad credentials` |

### Thủ công — cấu hình tĩnh vẫn đúng như tài liệu (đọc file)

| Cần xác nhận | File (bấm để mở) |
|---|---|
| 5 stage đúng thứ tự, `checkStarted`/`checkPassed`/`checkFailed` dùng `githubNotify` | [`Jenkinsfile`](../../Jenkinsfile) |
| `enforce_admins: true`, `required_approving_review_count: 0`, 5 context bắt buộc | [`scripts/ci/setup-branch-protection.sh`](../../scripts/ci/setup-branch-protection.sh) |
| Java agent Community Branch Plugin ở cả 2 biến, mount `docker.sock`, `TESTCONTAINERS_*` | [`docker-compose.ci.yml`](../../docker-compose.ci.yml) |
| Dockerfile Jenkins agent riêng (.NET/Node/Docker CLI, `COREPACK_HOME`) | [`docker/ci/jenkins.Dockerfile`](../../docker/ci/jenkins.Dockerfile) |

**Kết quả lượt QA này**: cả 4 file khớp mô tả của `architecture/013` và `development/013` — không sai lệch ở tầng cấu hình tĩnh.
Không có test tự động C#/TS cho spec này (thuần cấu hình CI).

## Kết luận

**FAIL.** Cấu hình tĩnh đúng tài liệu và cơ chế từng hoạt động đúng lúc viết feature, nhưng hôm nay **cơ chế chặn merge — giá trị
cốt lõi duy nhất của 013 — không còn hoạt động**: từ ~2026-09-03 không PR nào trên `master` có check CI, `rules/branches/master`
trả `[]`, ≥ 27 PR vẫn merge bình thường (phủ định FR-003/SC-002). Kèm 3 ghi chú: (1) Jenkins/SonarQube cục bộ không chạy;
(2) token CI cục bộ hết hạn; (3) runbook `docs/github-jenkins-sonarqube-setup.md` bị xoá nhầm ở commit `8fcbbdf`. Chi tiết,
bằng chứng và hướng xử lý: [QA_Debt.md](QA_Debt.md) mục 013.
