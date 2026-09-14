# Bước 013: Thay đổi nghiệp vụ so với bước 011

## Phạm vi

Tài liệu này chỉ mô tả phần do chính bước 013 (`specs/013-sonarqube-merge-blocker`) tạo ra, không đi
sâu vào hạ tầng CI nền tảng (Jenkinsfile 5 stage, `docker-compose.ci.yml`, wiring Jenkins↔SonarQube)
vốn đã tồn tại từ trước bước này. Bước 012 (không có file Architect tương ứng trong bộ tài liệu này)
nằm ngoài phạm vi của file này.

Ranh giới commit của riêng bước 013:

- Mốc hoàn tất bước 011: commit `514c6c1`.
- Đặc tả bước 013: commit `759d36c`.
- `Jenkinsfile`/`scripts/ci/setup-branch-protection.sh` đổi comment ghi chú nguồn gốc từ spec 012
  sang 013, không đổi hành vi pipeline: commit `a466bb1`.
- Sửa lỗi CI thật (coverage flag không được truyền qua `turbo`): commit `6847b79` — mốc hoàn tất
  bước 013.

Đây là bước duy nhất trong toàn bộ 001–024 không có nghiệp vụ mua hàng hay service code nào; nội dung
là cấu hình CI/CD (Jenkinsfile, script `gh api`) và cấu hình phía GitHub (branch protection) — được
ghi lại vì đây là phần "code của devops", không phải nội dung kiểm thử bị loại trừ.

## 1. Branch protection: script hoá, không phải infrastructure-as-code

[scripts/ci/setup-branch-protection.sh](../../scripts/ci/setup-branch-protection.sh)

```sh
# 013: chạy một lần bởi admin repo, chạy lại mỗi khi danh sách required check đổi tên.
# Có chủ đích KHÔNG dùng một IaC stack riêng để quản lý — research.md Decision 5:
# dùng IaC cho một cấu hình của một repo là quá mức cần thiết.
gh api \
    --method PUT \
    -H "Accept: application/vnd.github+json" \
    "repos/${REPO}/branches/${BRANCH}/protection" \
    --input - <<'JSON'
{
  "required_status_checks": {
    "strict": true,
    "contexts": [
      "ci/build",
      "ci/unit-tests",
      "ci/integration-tests",
      "ci/contract-tests",
      "ci/sonarqube-quality-gate"
    ]
  },
  "enforce_admins": true,
  "required_pull_request_reviews": {
    "dismiss_stale_reviews": true,
    "require_code_owner_reviews": false,
    "required_approving_review_count": 0
  },
  "restrictions": null,
  "allow_force_pushes": false,
  "allow_deletions": false,
  "required_conversation_resolution": true
}
JSON
```

(Danh sách `contexts` ở trên là 5 check tại thời điểm bước 013; `ci/secret-scan` và
`ci/image-secret-scan` được thêm sau đó ở bước 018 — ngoài phạm vi file này.)

Hai quyết định đáng chú ý được ghi lại trực tiếp trong comment của script:

- `enforce_admins: true` là công tắc loại bỏ đường vòng cho MỌI vai trò, kể cả admin — thiếu nó thì
  pipeline chỉ mang tính khuyến nghị, không phải rào chặn thật.
- `required_approving_review_count: 0` (không phải 1) là chủ đích: repo `nmhieuit/ecommerce` chỉ có
  một collaborator, và GitHub không cho tự approve PR của chính mình — yêu cầu 1 approval sẽ khoá
  merge vĩnh viễn, không phải là cổng chất lượng đúng nghĩa.

## 2. Preflight phân biệt "plan không cho phép" với "token sai"

[scripts/ci/setup-branch-protection.sh](../../scripts/ci/setup-branch-protection.sh)

```sh
# 013: cả hai lỗi đều trả về HTTP 403 — phân biệt bằng nội dung message,
# không suy đoán do token.
if ! probe="$(gh api "repos/${REPO}/branches/${BRANCH}/protection" 2>&1)"; then
    case "$probe" in
        *"Upgrade to GitHub Pro"*|*"upgrade to GitHub Pro"*)
            echo "Branch protection is unavailable on ${REPO}." >&2
            # 013: branch protection không khả dụng trên repo private ở gói free GitHub.
            exit 2
            ;;
        *"Branch not protected"*)
            : # Kỳ vọng ở lần chạy đầu tiên — chưa có gì để báo.
            ;;
    esac
fi
```

Ghi chú trong PLAN REQUIREMENT của script xác nhận: repo từng ở chế độ private không dùng được branch
protection trên gói free (API trả 403 "Upgrade to GitHub Pro..."); repo phải chuyển sang public (hoặc
nâng cấp Pro) trước khi script này có tác dụng.

## 3. Sửa lỗi CI thật: coverage flag không tới được vitest

[Jenkinsfile](../../Jenkinsfile)

```groovy
steps {
    checkStarted(env.CHECK_UNIT)
    sh 'scripts/ci/run-dotnet-tests.sh unit'

    // 013: 'pnpm ... turbo run test -- --coverage' KHÔNG hoạt động — dấu '--' của pnpm chỉ
    // append --coverage vào lệnh 'turbo run test', mà turbo không có flag đó để nhận; cần một
    // dấu '--' thứ hai để forward xuống vitest. 'pnpm exec turbo' chạy thẳng binary với đúng
    // args đã cho, tránh vấn đề lồng '--' kép.
    sh 'pnpm --dir frontend exec turbo run test -- --coverage'
    sh 'scripts/ci/merge-coverage.sh'
}
```

Trước khi sửa, `pnpm --dir frontend turbo run test -- --coverage` fail với
`ERR_PNPM_RECURSIVE_EXEC_FIRST_FAIL`. Lỗi được xác minh trực tiếp trong container Jenkins của CI:
sau khi sửa, suite frontend chạy (49 test) và sinh đúng
`frontend/apps/web/coverage/lcov.info` — đường dẫn mà `sonar-project.properties` cần để nạp coverage
vào SonarQube.

## 4. Cơ chế audit — không có endpoint tự viết

Theo `docs/architecture/013_Architect_...md` §3, FR-009 (audit "ai đổi cấu hình chặn merge, cổng
chất lượng ra sao tại thời điểm merge") được trả lời hoàn toàn bằng hai cơ chế có sẵn của GitHub,
không có code nào được viết thêm cho mục đích này:

- `github.com/settings/security-log` (lọc `repo:nmhieuit/ecommerce`) ghi sự kiện
  `repo.change_merge_setting`.
- `GET /repos/{owner}/{repo}/commits/{sha}/status` trả lịch sử đầy đủ 5 required check cho bất kỳ
  SHA nào, kể cả sau khi PR đã merge.

## Tóm tắt 011 → 013

| Khu vực | Bước 011 | Bước 013 |
|---|---|---|
| Branch protection | Không thuộc phạm vi 011 | Script hoá qua `gh api`, `enforce_admins: true`, 5 required check |
| CI pipeline | Không đổi | Sửa lỗi coverage flag frontend (`pnpm exec turbo`) |
| Ghi chú nguồn gốc trong Jenkinsfile/script | Tham chiếu spec 012 | Cập nhật tham chiếu sang spec 013 |
| Audit trail | Không thuộc phạm vi 011 | 2 cơ chế GitHub có sẵn (security log, commit status API), không code mới |
| Service/shared code | Baskets tham chiếu `EventContracts` (011) | Không đổi |

**Kết luận:** bước 013 không thêm nghiệp vụ hay service code nào. Nó biến pipeline CI đã có từ trước
thành rào chặn merge thật sự (script hoá branch protection, loại bỏ đường vòng cho mọi vai trò) và sửa
một lỗi thật khiến coverage frontend không tới được SonarQube; cơ chế audit dùng nguyên bản tính năng
có sẵn của GitHub, không có endpoint tự viết nào.

## 5. Thành phần dùng chung trong bước 013

Bước 013 không tạo và không sửa bất kỳ shared C# project nào (`ServiceDefaults`, `Tenancy`,
`EventContracts`). Thay đổi duy nhất mang tính "dùng chung" là `scripts/ci/setup-branch-protection.sh`
— một script vận hành dùng chung cho toàn bộ pipeline, không phải thư viện code được service tham
chiếu.
