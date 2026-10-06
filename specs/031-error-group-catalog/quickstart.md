# Quickstart: Danh mục 8 nhóm lỗi để luyện troubleshoot

**Feature**: [spec.md](./spec.md) | Tham chiếu: [data-model.md](./data-model.md), [contracts/](./contracts/)

Hướng dẫn kiểm chứng tính năng trên Docker Compose local. Đây là bài chạy tay (không có test tự động —
xem plan.md, Complexity Tracking). Mỗi bước nêu kỳ vọng; sai kỳ vọng thì ghi vào `docs/QA/QA_Debt.md`.

## Điều kiện tiên quyết

- Stack local đang chạy khoẻ: `docker compose -f docker-compose.local.yml up -d --build --wait`.
- `.env` có `CHAOS_ALLOW_FAULT_INJECTION=true` và `CHAOS_ALLOW_LATENCY_INJECTION=true` CHỈ trong lúc
  diễn tập (bước 5 dọn lại).
- Tải nền chạy trong một terminal riêng: `./scripts/incident-drill.ps1 -Load` (như 028).
- Elastic stack đang nhận telemetry (như 028); dashboard `Xử lý sự cố — 7 service` đã import.
- Chưa có lần chạy nào chưa khôi phục (xem `.incident-drill/*/state.json`).

## Bước 1 — Danh mục (User Story 1)

Mở `scripts/incident-drill/catalog.json`. **Kỳ vọng**: 8 nhóm, 9 loại A–I; không có chữ `docker` hay
`${` nào; hints mức 1 và 2 không chứa tên service/DB hay mã loại
([contracts/fault-catalog-contract.md](./contracts/fault-catalog-contract.md) bất biến 1–3).

## Bước 2 — Dạng (a): tiêm có chủ đích từng nhóm (User Story 2)

Với từng loại, tiêm vào một đích hợp lệ rồi khôi phục. Ví dụ:

```powershell
./scripts/incident-drill.ps1 -Inject -Type D -Target orders-api -DurationSeconds 600
./scripts/incident-drill.ps1 -Inject -Type E -Target orders-db
./scripts/incident-drill.ps1 -Inject -Type G -Target products-api
./scripts/incident-drill.ps1 -Inject -Type H -Target baskets-api
./scripts/incident-drill.ps1 -Inject -Type I -Target parties-api
./scripts/incident-drill.ps1 -Inject -Type F -Target gateway-api        # Authority sai
./scripts/incident-drill.ps1 -Restore -RunId <runId>
```

Mỗi lần chỉ một lần chạy mở (bất biến 24). **Kỳ vọng**:
- Script in rõ nhóm, loại, đích, tham số đã tiêm; triệu chứng đo được (telemetry hoặc `docker ps`/Postman
  folder 31 subfolder tương ứng).
- `-Restore` xong và container đích healthy trong ≤ 10 phút; `state.json.status = restored`.
- `-DurationSeconds`: hết thời lượng, script tự khôi phục.
- Từ chối đúng: loại sai đích (`-Type B -Target orders-api`, `-Type D -Target products-api`) và khi
  `CHAOS_ALLOW_FAULT_INJECTION` tắt (không có thay đổi nào).

## Bước 3 — Dạng (b): bốc thăm mù và gợi ý (User Story 3, 4)

```powershell
./scripts/incident-drill.ps1 -Start
./scripts/incident-drill.ps1 -Hint -RunId <runId> -Level 1
./scripts/incident-drill.ps1 -Hint -RunId <runId> -Level 2
./scripts/incident-drill.ps1 -Hint -RunId <runId> -Level 3
./scripts/incident-drill.ps1 -Restore -RunId <runId>
./scripts/incident-drill.ps1 -Reveal -RunId <runId>
```

**Kỳ vọng**: `-Start` chỉ in `runId` và mã băm; mức 1, 2 không lộ service/tham số/mã loại; mức 3 lộ đáp
án; `hint-log.json` có đúng 3 dòng; `-Reveal` khớp băm và in nhật ký gợi ý. Lặp ít nhất 20 lần
(`-Restore` giữa các lần) để kiểm SC-003: phủ ≥ 6/8 nhóm.

## Bước 4 — Gỡ Kubernetes của 025 (User Story 5)

```bash
rg -i "kubectl|kill-pod|kill pod|giết pod|cluster kind" specs/025-chaos-pod-kill-latency docs/dien-tap-chaos-engineering
git diff master -- deploy/ansible Jenkinsfile scripts/ci/lint-deployment-manifests.sh tests/DeploymentManifestConventionTests
```

**Kỳ vọng**: lệnh `rg` không có kết quả (trừ chỗ trỏ sang 031); `git diff` trống; `ket-qua/` còn
`2026-09-14-inject-latency.md`, không còn hai bản ghi kill-pod.

## Bước 5 — Dọn dẹp

- `-Restore` mọi lần chạy còn mở; xác nhận `docker ps` mọi container healthy.
- Đặt `CHAOS_ALLOW_FAULT_INJECTION` và `CHAOS_ALLOW_LATENCY_INJECTION` về `false` trong `.env`, chạy lại
  stack không kèm override.
- Xoá Elastic sau triển khai: **hỏi người dùng trước khi xoá** (đã chốt).

## Folder Postman 31 (User Story 6)

Chạy theo từng subfolder D–I (xem `docs/QA/031_QA_*.md`): tiêm lỗi bằng lệnh ở Bước 2, chạy request "gây
triệu chứng" (kỳ vọng lỗi), khôi phục, chạy request "kiểm khôi phục" (kỳ vọng bình thường).
