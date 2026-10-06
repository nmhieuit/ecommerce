# Contract: Script `scripts/incident-drill.ps1` — phần mở rộng của 031

**Feature**: [../spec.md](../spec.md) (FR-003–FR-009, FR-015, FR-018, FR-019) | **Nền**:
[`specs/028-incident-oncall-drill/contracts/incident-drill-script-contract.md`](../../028-incident-oncall-drill/contracts/incident-drill-script-contract.md)
| **Người tiêu thụ**: người vận hành; kiểm chứng bằng [../quickstart.md](../quickstart.md) và folder
Postman 31 (không có test tự động — xem plan.md, Complexity Tracking).

Hợp đồng này **bổ sung** hợp đồng 028. Bất biến 1, 3, 4, 9, 12 của 028 giữ nguyên; các bất biến bị đổi
được liệt kê ở mục "Thay đổi so với 028".

## Bất biến mới

| # | Bất biến |
|---|---|
| 20 | Mọi lệnh tiêm (`-Start`, `-Inject`, chế độ không mù) từ chối khi `.env` không có `CHAOS_ALLOW_FAULT_INJECTION=true`; thoát mã khác 0, không ghi file, không chạy lệnh docker nào. |
| 21 | Loại D từ chối khi `.env` không có `CHAOS_ALLOW_LATENCY_INJECTION=true`; thông báo nêu rõ cờ nào thiếu. |
| 22 | Script nạp `scripts/incident-drill/catalog.json` trước khi làm gì khác; danh mục sai (xem [fault-catalog-contract.md](./fault-catalog-contract.md) bất biến 8, 10) → dừng. |
| 23 | `-Inject -Type <X> -Target <T>`: nếu `T` không thuộc `targets` của loại `X` (hoặc không thuộc biến thể nào của F) thì từ chối kèm lý do, không thay đổi gì. `-Group` kèm `-Type` mà loại không thuộc nhóm → từ chối. |
| 24 | Nếu tồn tại bất kỳ `.incident-drill/*/state.json` có `status ∈ {pending, injected, failed}` thì `-Start` và `-Inject` từ chối và chỉ ra `runId` đang mở. Lần chạy 028 cũ (không có `state.json`) bị bỏ qua. |
| 25 | `-Inject` in rõ đã tiêm gì (nhóm, loại, đích, tham số) — không niêm phong theo nghĩa che giấu; vẫn ghi `sealed.json` và `hash.txt` (`blind=false`, `mode=scripted`) để `-Restore` và `-Reveal` dùng chung cơ chế. |
| 26 | `-Inject -DurationSeconds <n>`: hết `n` giây kể từ lúc tiêm xong, tiến trình nền tự thực hiện khôi phục như `-Restore`. Không có `-DurationSeconds` thì lỗi giữ tới khi `-Restore`. Không có kịch bản nhiều bước. |
| 27 | `-Restore -RunId <id>`: chạy thao tác khôi phục của loại (research Quyết định 3), chờ container đích healthy tối đa 10 phút, rồi ghi `restored-at.txt` và `state.status = restored`. Quá hạn → `state.status = failed`, thoát mã khác 0, không thử lại. Chạy lại `-Restore` trên lần chạy đã `restored` là no-op, thoát mã 0. |
| 28 | `-Restore` trên lần chạy còn `pending` (chưa tiêm): hủy tiến trình nền (`injectorPid`), đánh dấu `restored`, báo "chưa tiêm gì". |
| 29 | Với loại C và D, vòng gửi header dừng khi (a) file `stop.flag` xuất hiện, hoặc (b) Id container đích đổi (bất biến 8 của 028). `-Restore` tạo `stop.flag`. |
| 30 | Khôi phục theo loại: A, B, F = tạo lại 7 container không override; C, D = dừng vòng gửi; E = `docker start` đích rồi chờ DB healthy và service chủ `/health/ready` 200 (không dùng health của docker: container service vẫn `healthy` khi ready trả 503); G, I = tạo lại container đích từ compose; H = `docker network connect` kèm alias gốc rồi chờ healthy. |
| 31 | `-Hint -RunId <id> -Level 1|2|3`: `Level` ngoài 1–3 → từ chối. Mức 1 và 2 đọc `hints.1`/`hints.2` của loại trong danh mục, KHÔNG in service, tham số hay mã loại. Mức 3 kiểm băm `sealed.json` như `-Reveal` (sai băm → lỗi), rồi in đáp án từ `sealed.json` + `hints.3`. |
| 32 | Mỗi lần `-Hint` thành công ghi thêm `{at, level}` vào `hint-log.json`, kể cả khi bỏ cách mức. KHÔNG ghi vào Kibana Case, KHÔNG sửa `sealed.json`, `injected-at.txt` hay mốc nào của bản ghi sự cố. |
| 33 | `-Start` bốc mù theo quy tắc: nhóm đều 1/8 → loại đều trong nhóm → đích đều trong các đích áp dụng được → tham số → độ trễ 0–30 phút; chỉ in `runId` và mã băm (bất biến 2 của 028 giữ nguyên). |
| 34 | `-Reveal` giữ hành vi 028 và thêm: in `state.json` (trạng thái, thời điểm tiêm/khôi phục) và `hint-log.json` (số lần và mức đã mở). Chạy được cho lần chạy 028 cũ (không có `state.json`/`hint-log.json`). |
| 35 | Thao tác tiêm chỉ gồm file override trong `.incident-drill/<runId>/` và lệnh `docker` ngoài; script KHÔNG sửa file đã commit, KHÔNG sửa `docker-compose.local.yml`, `.env.example` hay code `services/`/`shared/`. |
| 36 | `-Load` giữ nguyên hành vi 028 (bất biến 12). |

## Thay đổi so với 028

| Bất biến 028 | Thay đổi |
|---|---|
| 5 (tạo lại cả 7 container) | Chỉ áp dụng cho loại có `masksByRecreatingAll = true` (A, B, C, D, F). E, G, H, I không tạo lại cả 7 (không che đích — chấp nhận, ghi giới hạn). |
| 6 (`faultType` ∈ {A, B, C}) | Mở rộng thành {A…I}; B vẫn chỉ gateway; áp dụng cho đích theo [fault-catalog-contract.md](./fault-catalog-contract.md) bất biến 4. |
| 7 (`errorRatePct` ∈ [5, 50] khi và chỉ khi C) | Mở rộng: khi và chỉ khi `faultType ∈ {C, D}`. |
| 10 (khôi phục chuẩn) | Thêm lệnh `-Restore` là đường chuẩn cho mọi loại; cách cũ (đổi cờ + `up -d --build --wait`) vẫn dùng được cho A/B/C/F. |
| 11 (chế độ không mù) | Giữ nguyên (`-Start -Service -FaultType`, `mode = legacy-nonblind`); `-Inject` là đường mới cho dạng (a). |
