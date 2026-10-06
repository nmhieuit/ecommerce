# Research: Danh mục 8 nhóm lỗi để luyện troubleshoot

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Mỗi quyết định thuộc một trong ba loại, ghi rõ ở từng mục:
- **Người dùng chốt**: chốt trực tiếp trong phiên rà soát nợ, `/speckit-specify` hoặc `/speckit-plan` (2026-10-05).
- **Hệ quả**: hệ quả kỹ thuật bắt buộc của các lựa chọn đó.
- **Đề xuất — chờ duyệt ở `/speckit-tasks`**: người lập kế hoạch đề xuất; người dùng duyệt trước khi triển khai (như 028).

Mục "Điểm phải xác minh" liệt kê những gì chưa thể khẳng định chỉ bằng đọc code/compose.

## Hiện trạng đã kiểm tra (trước khi ra quyết định)

- **Script 028** `scripts/incident-drill.ps1` (~470 dòng): `-Start` bốc service → loại (A/B/C) → độ trễ
  0–30 phút, niêm phong `sealed.json` + SHA-256; tiến trình nền ẩn (`-RunInjector`) ghi file override,
  tạo lại cả 7 container (mỗi service một lệnh `up -d --force-recreate --no-deps`), ghi
  `injected-at.txt`; loại C gửi header `X-Chaos-Fault: 5xx` theo tốc độ tính từ span nền, dừng khi Id
  container đích đổi. `-Reveal` kiểm băm rồi xoá override. `-Load` chạy newman folder 00 + 26 + 28.
- **Compose** `docker-compose.local.yml`: project `ecomerce-local`; một network `backbone` (tên thật
  `ecomerce-local_backbone`); 5 DB `products-db`, `baskets-db`, `orders-db`, `parties-db`,
  `identity-db`; `redis`, `rabbitmq`; 7 service app `products-api` … `gateway-api` + `storefront`.
  Container tên `ecomerce-local-<service>-1`.
- **Restart policy**: chỉ 5 container migrate có `restart: on-failure:3`; 7 service app **không có**
  `restart:` (compose ghi rõ: để service sống và báo 503 khi DB dừng). Hệ quả: `docker kill` làm service
  chết hẳn; `docker stop` một DB để service sống với `/health/ready` 503.
- **Health check DB** của compose được thiết kế cho việc dừng/khởi động DB có chủ đích ("Restarting a
  database on purpose is the whole point of this file") — chờ DB phục hồi đủ sâu trước khi coi là healthy.
- **Redis/RabbitMQ**: không service nào có `ConnectionStrings__Redis`; RabbitMQ chỉ orders-api dùng khi
  `ORDERS_RABBITMQ_CONNECTION` được đặt (mặc định rỗng) → loại khỏi nhóm 4 (người dùng chốt).
- **Authority**: `Identity__Authority` có ở 6 service (products, baskets, orders, parties, bff,
  gateway), mặc định `http://identity-api:8080`. identity-api không có biến này.
- **Latency 025** (`ChaosLatencyInjectionMiddleware`): chỉ Orders.Api; chờ không chặn thread
  (`IChaosDelay.DelayAsync`); header `X-Chaos-Latency-Ms`, kẹp tối đa `MaxInjectedLatencyMs = 30000`;
  cần `Chaos:AllowLatencyInjection=true`. Compose truyền qua `CHAOS_ALLOW_LATENCY_INJECTION` (mặc định
  `false`). Container chỉ nhận cờ lúc được tạo → muốn bật cờ phải tạo lại container.
- **Phạm vi k8s của 025**: quét `kubectl|kill-pod|giết pod|cluster kind` thấy 158 kết quả ở 37 file;
  trong đó thuộc 025: specs/025 (tasks 32, spec 10, research 5, quickstart 5, data-model 5, plan 3,
  contracts 2, checklist 2), `docs/dien-tap-chaos-engineering/` (README 3, mẫu 3, 3 bản ghi), PO/QA/
  Architect/Development 025, `docs/spec-summary-vi/025-*.json`, `docs/diagrams/025-*.drawio`,
  `QA_Debt.md` mục 025 (dòng ~770–782), `technical-debt.md` (mục 025). **Không thuộc 025, GIỮ**:
  specs/018, specs/019, ADR-0007, `docs/onboarding/11-trien-khai-k8s-va-secret-store.md`,
  `Directory.Packages.props` (chú thích về test manifest), mục 018/019 trong technical-debt/QA_Debt.

## Quyết định 1 — Không sửa code service; chỉ cấu hình/tham số hoặc công cụ Docker ngoài

**Decision** (Người dùng chốt): Không file nào dưới `services/` hay `shared/` bị sửa. Lỗi nhóm 1/2/3/5
(biến thể Authority) dùng cấu hình sai hoặc header của middleware có sẵn; lỗi nhóm 4/5 (dừng
identity)/6/7/8 dùng lệnh `docker` ngoài.

**Hệ quả**:
- Cờ `CHAOS_ALLOW_FAULT_INJECTION` chặn ở script cho **mọi** nhóm (kể cả nhóm chỉ dùng lệnh docker,
  vốn middleware không bảo vệ được); nhóm 3 còn cần `CHAOS_ALLOW_LATENCY_INJECTION`.
- Không thêm cờ mới (người dùng chốt).

**Alternatives considered**: cờ riêng `CHAOS_ALLOW_INFRA_FAULTS` cho nhóm docker ngoài — bị loại vì
người dùng chọn dùng chung một cờ.

## Quyết định 2 — Danh mục dữ liệu tách khỏi bộ chuyển đổi Compose

**Decision** (Người dùng chốt: file dữ liệu riêng + bộ chuyển đổi Compose trong script;
`scripts/incident-drill/catalog.json`): Danh mục là JSON thuần, đọc bằng `ConvertFrom-Json`; script đọc
qua `$PSScriptRoot`. Bộ chuyển đổi gồm hai nhóm hàm có cùng giao diện cho mỗi loại:

| Hàm | Vai trò |
|---|---|
| `Invoke-ComposeInject -Sealed` | Thực hiện thao tác tiêm của loại đó trên Compose. |
| `Invoke-ComposeRestore -Sealed` | Hoàn tác và chờ container đích healthy (tối đa 10 phút). |

Danh mục chỉ dùng từ vựng trừu tượng cho loại khôi phục (`restoreKind`) và tham số (`delayMs`,
`cpuLimit`, `memoryLimitMb`, `errorRatePctRange`, `targetKind`); bộ chuyển đổi ánh xạ sang lệnh. Thêm
bộ chuyển đổi CD/Kubernetes sau này = thêm tập hàm cùng giao diện chọn theo biến "nơi tiêm"; danh mục
không đổi.

**Rationale**: Phần "loại lỗi" (cái gì hỏng, triệu chứng, gợi ý) bền hơn "nơi tiêm" (cách làm trên
Compose). Tách theo dữ liệu/hàm mà không tạo thêm file `.ps1` (người dùng chọn gộp adapter trong script).

**Alternatives considered**: danh mục để trong script — kém khả năng mở rộng; danh mục chỉ là tài liệu
md — không tách được ở mức thực thi; danh mục ở `docs/` — phụ thuộc đường dẫn tài liệu.

## Quyết định 3 — Danh mục nhóm lỗi (9 loại) và bộ chuyển đổi Compose

**Decision** (Người dùng chốt ánh xạ nhóm→mã và tham số; thao tác Compose là Hệ quả):

| Nhóm | Mã | Đích áp dụng | Tham số (Người dùng chốt) | Tiêm trên Compose | Khôi phục (`-Restore`) | Tạo lại cả 7 (che)? |
|---|---|---|---|---|---|---|
| 1 | **A** đích kết nối sai | 7 service (như 028) | host/URL sai như 028 | file override `incident-missing-db` / `incident-missing-host`, recreate 7 | `up -d --force-recreate --no-deps` 7 service không override | Có (như 028) |
| 2 | **B** cạn pool | `gateway-api` | `MaxConnectionsPerServer=1` | override, recreate 7 | như A | Có |
| 2 | **C** 5xx theo tỷ lệ | 7 service | tỷ lệ 5–50% | recreate 7 (nhận cờ từ `.env`) + vòng gửi header `X-Chaos-Fault: 5xx` | dừng vòng gửi (cờ `stop`) | Có |
| 3 | **D** độ trễ | `orders-api` | 2000 ms, tỷ lệ 5–50% | recreate 7 (nhận cờ latency + fault từ `.env`) + vòng gửi header `X-Chaos-Latency-Ms: 2000` | dừng vòng gửi | Có |
| 4 | **E** hạ tầng dừng | 5 DB | dừng bằng `docker stop` | `docker stop ecomerce-local-<db>-1` | `docker start`, chờ DB healthy rồi service chủ ready | Không |
| 5 | **F** xác thực hỏng | 6 service dùng Authority (products, baskets, orders, parties, bff, gateway) | `Identity__Authority` sai | override `Identity__Authority=http://incident-missing-host:8080`, recreate 7 | như A | Có |
| 6 | **G** thiếu tài nguyên | 7 service app | `--cpus 0.1`, `--memory 256m` (96m bị OOM-kill — V4) | `docker update --cpus 0.1 --memory 256m --memory-swap 256m <container>` | tạo lại container đích từ compose (`up -d --force-recreate --no-deps`) | Không |
| 7 | **H** mạng đứt | 7 service app | tách khỏi `backbone` | `docker network disconnect ecomerce-local_backbone <container>` | `docker network connect --alias <service> ecomerce-local_backbone <container>`, chờ healthy | Không |
| 8 | **I** container chết | 7 service app | `docker kill` | `docker kill <container>` | tạo lại container đích từ compose | Không |

Nhóm 5 không còn biến thể dừng identity-api (bỏ sau V3, người dùng chốt ở `/speckit-implement`); `identity-api` không là đích của F.
Nhóm "2" gồm hai loại B và C; `-Group 2` bốc một trong hai (B chỉ khi đích là gateway).

**Quy tắc bốc thăm mù `-Start`** (Đề xuất — chờ duyệt ở `/speckit-tasks`): chọn nhóm đều ngẫu nhiên 1/8
→ loại đều ngẫu nhiên trong nhóm (nhóm 2: B hoặc C, mỗi loại 1/2) → đích đều ngẫu nhiên trong các đích
áp dụng được của loại đó (B luôn là gateway) → tham số → độ trễ 0–30 phút. Thay đổi so với 028 (028 bốc
service trước, rồi loại, nên mỗi service 1/7): giờ mỗi nhóm 1/8. Xác suất để 20 lần bốc phủ ≥ 6 nhóm
trên 0,95 (đáp ứng SC-003).

**Hệ quả**:
- Nhóm tạo lại cả 7 (A, B, C, D, F) giữ nhiễu che đích của 028; các nhóm còn lại không che (chấp nhận,
  ghi giới hạn).
- Nhóm 3 phải tạo lại cả 7 vì container chỉ nhận cờ lúc được tạo (giống loại C của 028).
- Khôi phục A/B/F như 028: tạo lại 7 container không override. Cờ `.env` đã bật nên container vẫn có cờ.

## Quyết định 4 — Lệnh mới và cú pháp (Người dùng chốt)

**Decision**: Giữ nguyên `-Start`, `-Reveal`, `-Load`, `-Service`, `-FaultType`, `-DelaySeconds`. Thêm:

| Lệnh | Việc làm |
|---|---|
| `-Inject -Type <A–I> [-Group <1–8>] [-Target <đích>] [-DurationSeconds <n>]` | Dạng (a): kiểm cờ; kiểm không còn lần chạy chưa khôi phục; kiểm loại áp dụng được cho đích; tạo `runId`; ghi `sealed.json` (`blind: false`, `mode: scripted`), `state.json`; tiêm ngay (độ trễ 0) bằng tiến trình nền; in rõ đã tiêm gì. `-Group` không kèm `-Type` thì bốc một loại trong nhóm; `-Target` bỏ trống thì bốc đích hợp lệ. `-DurationSeconds` khai báo thì tiến trình nền tự `-Restore` khi hết thời lượng. |
| `-Restore -RunId <id>` | Chạy `Invoke-ComposeRestore` cho loại của lần chạy; chờ healthy ≤ 10 phút; ghi `restored-at.txt`, `state.json.status = restored`. Nếu lần chạy còn `pending` thì hủy tiến trình nền chờ và đánh dấu `restored` (không có gì để gỡ). |
| `-Hint -RunId <id> -Level 1|2|3` | Mức 1: gợi ý triệu chứng; mức 2: nêu nhóm; mức 3: đáp án (kiểm băm như `-Reveal`). Luôn ghi `hint-log.json`. |
| `-Start` (mở rộng) | Bốc thăm mù từ cả 8 nhóm theo Quyết định 3; vẫn chỉ in `runId` và mã băm. |
| `-Reveal` (mở rộng) | Như 028, thêm in `hint-log.json` và `state.json`. |

**Hành vi đã duyệt** (Người dùng chốt, phiên `/speckit-plan`):
1. `-Hint` bỏ cách mức được phép; ghi đúng mức đã xin.
2. Còn lần chạy chưa khôi phục (`state.status ∈ {pending, injected, failed}` ở bất kỳ `.incident-drill/*/state.json`)
   thì `-Start`/`-Inject` từ chối. Lần chạy cũ của 028 không có `state.json` được bỏ qua.
3. Khôi phục G và I = tạo lại container đích từ compose.
4. Khôi phục H = `network connect` kèm alias gốc rồi chờ healthy.

## Quyết định 5 — Trạng thái từng lần chạy và niêm phong

**Decision** (Hệ quả của việc `sealed.json` phải bất biến để kiểm băm): trạng thái thay đổi được tách
sang file riêng.

| File | Nội dung | Ghi chú |
|---|---|---|
| `sealed.json` | `runId`, `blind`, `mode`, `group`, `faultType` (A–I), `target`, `service`, `faultDetail`, `delaySeconds`, `durationSeconds`, `errorRatePct`, `latencyMs`, `plannedInjectAt` | băm SHA-256 như 028; thêm trường mới, trường cũ giữ tên |
| `hash.txt` | mã băm | như 028 |
| `state.json` | `status` (`pending`/`injected`/`restored`/`failed`), `injectorPid`, `injectedAt`, `restoredAt`, `failure` | KHÔNG băm (thay đổi) |
| `hint-log.json` | mảng `{at, level}` | |
| `injected-at.txt` | như 028 | |
| `restored-at.txt` | thời điểm khôi phục | mới |
| `stop.flag` | file rỗng; vòng gửi header (C, D) thấy file này thì dừng | mới |
| `injector.log`, `docker-compose.incident.yml` | như 028 | |

`service` là service app bị ảnh hưởng chính (với E là service chủ DB, ví dụ `orders-db` → `orders-api`);
`target` là đích thật được tiêm (DB hoặc service). Với lần chạy của 028 (`faultType` A/B/C, không có
`target`), `-Reveal` vẫn chạy được.

## Quyết định 6 — Tiêm độ trễ loại D (Hệ quả của header 025)

**Decision**: Giống loại C: vòng gửi trong tiến trình nền, nhưng mỗi request kéo dài ≥ 2 s nên PHẢI gửi
bất đồng bộ (không chờ phản hồi mới gửi tiếp) để giữ đúng tốc độ gửi `r/(1−r) × tốc độ nền` — nếu gửi
tuần tự, tốc độ tối đa chỉ ~0,5 request/giây và tỷ lệ 50% của service nhiều traffic không đạt được.
Route nhận header (**Người dùng chốt lại ở `/speckit-implement` sau V9**): gửi thẳng
`GET http://localhost:5041/orders/{guid ngẫu nhiên}` kèm `Authorization: Bearer <token>`,
`X-Tenant-Id: contoso` và `X-Chaos-Latency-Ms: 2000`. Token lấy từ
`.incident-drill/load/environment-with-token.json` do `-Load` sinh (cần `-Load` đang chạy hoặc đã chạy). Đo: 404 sau ≈ 2024 ms (không có header: 404 sau ≈ 100 ms). Phương án ban đầu (qua gateway `:5300/bff/orders/{guid}`) bị loại vì header không tới orders: gateway trả 404 sau 9 ms.

**Điểm cần đo**: V7.

## Quyết định 7 — Gợi ý ba mức (Người dùng chốt số mức, vị trí lưu và ghi log)

- Mức 1: triệu chứng và hướng nhìn (ví dụ "nhìn tầng nào trước"); mức 2: nêu nhóm lỗi; mức 3: đáp án
  đầy đủ như `-Reveal` (kiểm băm). Nội dung nằm trong `catalog.json` (`hints[1..3]`) theo từng loại.
- Mức 1 và 2 KHÔNG chứa tên service, tham số hay mã loại. Danh mục chứa văn bản với dấu giữ chỗ (ví dụ
  `{service}`) cho mức 3; mức 3 điền từ `sealed.json`.
- Mỗi lần mở ghi `hint-log.json`; không ghi vào Kibana Case; không đổi mốc thời gian của bản ghi sự cố.
- **Đề xuất — chờ duyệt ở `/speckit-tasks`**: văn bản cụ thể ba mức cho từng loại (viết ở task danh mục,
  người dùng duyệt như 028 duyệt tên host).

## Quyết định 8 — Gỡ phần Kubernetes của 025 (Người dùng chốt: gỡ sạch + 4 nhóm mở rộng)

**Decision**: Danh sách thao tác (chi tiết file/dòng ở `/speckit-tasks`):

| Nhóm thao tác | Phạm vi |
|---|---|
| Sửa | specs/025 (spec, plan, research, data-model, tasks, quickstart, contracts, checklist) — bỏ User Story kill-pod, kịch bản 1, SC-002, FR kill-pod, tasks kill-pod, điều kiện cluster; giữ phần tiêm độ trễ |
| Sửa | `docs/dien-tap-chaos-engineering/README.md`, `mau-ket-qua.md` |
| Sửa | `docs/PO|QA|architecture|development/025_*.md`, `docs/spec-summary-vi/025-*.json`, `docs/diagrams/025-*.drawio` |
| Sửa | `docs/QA/QA_Debt.md` mục 025, `docs/architecture/technical-debt.md` mục 025: gỡ câu/mục kill-pod, giữ phần độ trễ |
| Xoá | `ket-qua/2026-09-12-kill-pod.md`, `ket-qua/2026-09-14-kill-pod.md` |
| Giữ | `ket-qua/2026-09-14-inject-latency.md`; toàn bộ 018/019, ADR-0007, `onboarding/11-*`, `deploy/ansible/**`, Jenkinsfile, `lint-deployment-manifests.sh`, `DeploymentManifestConventionTests`, hiến chương |

**Kiểm chứng gỡ xong** (SC-005): `rg -i "kubectl|kill-pod|kill pod|giết pod|cluster kind"` trên
specs/025, docs/dien-tap-chaos-engineering, docs/*/025_*, docs/spec-summary-vi/025-*, docs/diagrams/025-*
không còn kết quả (trừ chỗ trỏ sang 031 nếu có); `git diff master -- deploy/ansible Jenkinsfile
scripts/ci/lint-deployment-manifests.sh tests/DeploymentManifestConventionTests` trống.

**Rủi ro**: spec 025 đã đóng; sửa nội dung lịch sử làm mất dấu vết kill-pod. Người dùng đã chấp nhận
("gỡ sạch"); mỗi file sửa nên có một ghi chú ngắn "kịch bản kill-pod đã gỡ, thay bằng nhóm 8 của 031".

## Quyết định 9 — Folder Postman 31 (Người dùng chốt)

**Decision**: Một folder `31 - Danh mục nhóm lỗi` trong `postman/ecommerce.postman_collection.v2.json`,
6 subfolder `D … I`; mỗi subfolder gồm request gây/quan sát triệu chứng (kỳ vọng lỗi) và request kiểm
khôi phục (kỳ vọng bình thường). Theo nếp QA 008+: thao tác tiêm do người vận hành điều khiển bằng
`.env`/compose/`incident-drill.ps1 -Inject` ngoài Postman; Postman chỉ đo. Chi tiết request ở
`/speckit-tasks`.

## Điểm phải xác minh ở task đầu tiên của giai đoạn triển khai

Mỗi điểm chạy trên stack thật (Docker Compose local), ghi kết quả vào mục "Kết quả xác minh" của
research.md như 028.

| # | Điểm | Nếu sai |
|---|---|---|
| V1 | **E**: `docker stop` một DB → service chủ vẫn sống, `/health/ready` 503 trong vài giây; `docker start` → service ready lại **không cần restart**; đo thời gian phục hồi | Dừng, hỏi người dùng |
| V2 | **F**: `Identity__Authority` sai ở một trong 6 service → triệu chứng đo được (401/500/timeout) trên route nào; gateway/BFF sai thì lan ra sao | Dừng, hỏi |
| V3 | **F-ii** (ĐÃ BỎ biến thể): `docker stop identity-api` → có triệu chứng đo được trong `-Load` không (token cũ có thể vẫn hợp lệ nhờ khoá ký đã cache); sau bao lâu | **Nếu không có triệu chứng: dừng và hỏi người dùng** (có thể bỏ biến thể ii) |
| V4 | **G**: `docker update --cpus 0.1 --memory 256m --memory-swap 256m` được chấp nhận; container có bị OOM-kill (exit 137) không; triệu chứng p95 | **Nếu OOM-kill: dừng và hỏi người dùng** (triệu chứng thuộc nhóm 8; có thể tăng memory) |
| V5 | **H**: `network disconnect` → triệu chứng; `network connect --alias <service>` đưa service về healthy, cổng publish `localhost:<port>` hoạt động lại | Dừng, hỏi |
| V6 | **I**: `docker kill` → thoát mã 137, không tự sống lại; `up -d --force-recreate --no-deps <svc>` đưa về healthy trong ≤ 10 phút | Dừng, hỏi |
| V7 | **D**: gửi bất đồng bộ có giữ được tốc độ mục tiêu; tốc độ nền của orders-api đủ để đạt 5–50%; cờ latency có hiệu lực sau khi tạo lại container | Điều chỉnh cách gửi; hỏi nếu không đạt |
| V9 | **D**: header `X-Chaos-Latency-Ms` gửi tới gateway có tới được orders-api qua gateway → BFF không (đo thời gian phản hồi `GET /bff/orders/{guid}` có tăng ≈ 2 s không) | **KHÔNG tới** → đã hỏi và người dùng chốt gửi thẳng orders-api kèm token + `X-Tenant-Id` |
| V8 | PowerShell 5.1: ghi/đọc `hint-log.json`, `state.json` không BOM, `ConvertFrom-Json` đủ độ sâu; `-Restore` hủy được tiến trình nền đang chờ | Sửa script |

## Kết quả xác minh

Đo ngày 2026-10-05 (phiên `/speckit-implement`) trên stack Docker Compose local, tải nền `-Load` đang chạy.

| # | Điểm | Lệnh / số đo | Kết luận |
|---|---|---|---|
| V1 | **E** DB dừng | `docker stop` `orders-db`: `/health/ready` của orders-api trả `503` trong ≤ 6 s, nhưng health của chính container orders-api vẫn `healthy` (liveness). `docker start`: DB `healthy` sau ≈ 48 s, orders-api ready lại sau ≈ 68 s, `RestartCount=0`. `products-db`: 503 trong ≤ 8 s, ready lại sau 18 s | **Đúng dự kiến.** Khôi phục PHẢI chờ `/health/ready` 200 (không dùng health của docker). Kéo dài tới ≈ 70 s vì khởi động lại SQL Server |
| V2 | **F** Authority sai | `products-api` với `Identity__Authority=http://incident-missing-host:8080`: gọi trực tiếp treo ≈ 20 s rồi `401`; qua gateway `/bff/products` 200 → `502` sau 77 ms; `/health/ready` vẫn 200. `gateway-api` sai: `401` sau ≈ 23,7 s. `bff-api` sai: `504` sau ≈ 10 s. Khôi phục bằng tạo lại không override: healthy | **Đúng dự kiến**, triệu chứng đo được và khác nhau theo đích |
| V3 | **F-ii** dừng identity-api | 10 phút dưới `-Load`: `/bff/products` luôn 200, `/bff/orders/{guid}` luôn 404 (đúng kỳ vọng), độ trễ không đổi. Chỉ discovery/cấp token lỗi kết nối. Khởi động lại: healthy sau ≈ 9 s | **Sai dự kiến → đã hỏi người dùng: BỎ biến thể F-ii** (nhóm 5 chỉ còn Authority sai) |
| V4 | **G** thiếu tài nguyên | `docker update --cpus 0.1 --memory 96m --memory-swap 96m` được chấp nhận nhưng `products-api` bị `OOMKilled=true`, exit 137, container `exited`; gateway `504` sau 3 s. Ngừng dùng ≈ 79 MiB. Thử `--cpus 0.1` với 128m/192m/256m: không OOM trong ≈ 6 request; độ trễ 87–408 / 73–205 / 95–310 ms (nền ≈ 45 ms) | **Sai dự kiến → đã hỏi người dùng: chốt 256m** (+ `--memory-swap 256m`). Phải đo lại dưới tải nền ở T029 |
| V5 | **H** mạng đứt | `docker network disconnect ecomerce-local_backbone` `baskets-api`: gateway `504` sau 3 s, gọi trực tiếp `:5188/health/ready` treo tới timeout 15 s, health docker vẫn `healthy`, cổng vẫn được liệt kê. `docker network connect --alias baskets-api`: gateway 404 (đúng) sau 3 s. Alias gốc `[ecomerce-local-baskets-api-1, baskets-api]`, sau connect chỉ còn `baskets-api` | **Đúng dự kiến.** Khôi phục nên truyền cả hai alias: `--alias <service> --alias ecomerce-local-<service>-1` |
| V6 | **I** container chết | `docker kill parties-api`: exited 137, `OOMKilled=false`, restart policy `no`, vẫn `exited` sau > 45 s; gateway `504` sau 3 s. `up -d --force-recreate --no-deps parties-api`: healthy sau 11 s, Id container đổi | **Đúng dự kiến** |
| V7 | **D** tốc độ gửi | Tốc độ nền `Orders.Api` ≈ 1,46 span/s. Gửi bất đồng bộ bằng `System.Net.Http.HttpClient.SendAsync` ở 1,5 req/s trong 20 s: gửi 30 (1,47/s), 30 hoàn tất (404), ES có 30 span `duration ≥ 1,9 s` (`duration` tính bằng nano giây) trên tổng 189 span cửa sổ 2 phút (≈ 50 % trong 20 s). Gửi tuần tự chỉ đạt ≈ 0,5 req/s | **Đúng dự kiến**; PHẢI gửi bất đồng bộ cho tỷ lệ cao |
| V8 | PowerShell 5.1 | `ConvertTo-Json -InputObject $mảng` giữ mảng một phần tử (`[ {...} ]`); dùng pipe thì làm phẳng. `ConvertFrom-Json` trả `Object[]`; bọc `@(...)` khi đọc. Ghi không BOM bằng `[System.IO.File]::WriteAllText` + `UTF8Encoding($false)`; `[]` đọc ra 0 phần tử. `Stop-Process -Id` hủy được tiến trình nền đang `Start-Sleep` | **Đúng dự kiến**; luôn dùng `-InputObject @(...)` và `@(...)` khi đọc |
| V9 | **D** header qua gateway | `GET :5300/bff/orders/{guid}` có `X-Chaos-Latency-Ms: 2000`: `404` sau 9 ms (không trễ); BFF trực tiếp `502`. Gọi thẳng orders: không token → `401` sau 2092 ms; chỉ token → `500 MissingTenantContext` sau 2009 ms (lỗi giả); token + `X-Tenant-Id: contoso` → `404` sau 2024 ms (≈ 100 ms khi không có header) | **Sai dự kiến → đã hỏi người dùng: gửi thẳng `:5041/orders/{guid}` kèm token + `X-Tenant-Id: contoso`** (header không đi xuyên gateway/BFF) |

## Tác động lên nợ/giới hạn đã biết (ghi vào technical-debt, QA_Debt)

- Nhóm E, G, H, I không che đích: `docker ps` có thể lộ (người dùng chấp nhận).
- Nhiễu khởi động nguội 5–7 phút và lỗi lan theo chuỗi phụ thuộc (đã biết từ 028) áp dụng thêm cho E và F.
- Token hỏng sau khi tạo lại identity (028) áp dụng thêm cho khôi phục I (đích identity-api). Dừng identity-api không có triệu chứng trên đường dữ liệu (V3) nên không là loại lỗi; ghi vào technical-debt.
- Max Pool Size=1 không gây lỗi ở service có DB (028) — vẫn không có loại B cho 5 service có DB.
- Redis/RabbitMQ không phải đích của nhóm 4 (không có triệu chứng).
- Không có test tự động cho script (Complexity Tracking).
