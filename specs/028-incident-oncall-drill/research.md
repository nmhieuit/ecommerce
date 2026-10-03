# Research: Diễn tập sự cố thật và phản ứng on-call (SCRUM-36)

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Mỗi quyết định dưới đây thuộc một trong ba loại, ghi rõ ở từng mục:
- **Người dùng chốt**: chốt trực tiếp trong phiên `/speckit-specify` hoặc `/speckit-plan` ngày 2026-10-01.
- **Hệ quả**: hệ quả kỹ thuật bắt buộc của các lựa chọn đó.
- **Đề xuất — đã duyệt (2026-10-01)**: người dùng đã chọn "tôi đề xuất, bạn duyệt"; phải được duyệt ở phiên
  `/speckit-tasks` trước khi triển khai.

Mục "Điểm phải xác minh" liệt kê những gì chưa thể khẳng định chỉ bằng đọc code.

## Hiện trạng đã kiểm tra (trước khi ra quyết định)

- **Cơ chế tiêm lỗi có sẵn**: `ChaosFaultInjectionMiddleware` (027) nằm trong `shared/ServiceDefaults`
  nên có ở cả 7 service. Nó trả `500` khi cấu hình `Chaos:AllowFaultInjection = true` **và** request có
  header `X-Chaos-Fault: 5xx`. Compose truyền cờ qua `CHAOS_ALLOW_FAULT_INJECTION` (mặc định `false`)
  cho cả 7 service.
- **Biến cấu hình có thể làm hỏng mà không sửa code** (`docker-compose.local.yml`):
  - 5 service có DB (products, baskets, orders, parties, identity) nhận
    `ConnectionStrings__*Db: ${<SVC>_DB_CONNECTION:-...;Connect Timeout=3;ConnectRetryCount=0}`.
  - BFF nhận `Services__{Products,Baskets,Orders,Parties}Api__BaseUrl` qua `*_API_URL`. BFF không có
    tham số cấu hình nào cho pool HTTP.
  - Gateway nhận `ReverseProxy__Clusters__bff-cluster__Destinations__bff__Address` qua `BFF_API_URL`.
    YARP đọc thêm `ReverseProxy:Clusters:<id>:HttpClient:MaxConnectionsPerServer` từ cấu hình.
- **Công cụ tải 026**: bài NBomber `tests/CriticalPathLoadTests` không gắn token, nên trên stack hiện
  tại 60/60 request nhận `401` và test sập trước khi kịp ghi báo cáo (`docs/QA/QA_Debt.md` mục 026).
  Folder Postman `26 - Ngân sách hiệu năng luồng trọng yếu` chạy được vì có token; nó lấy token qua
  folder `00 - Xác thực & phân quyền (Get Token)`.
- **Luồng 4 bước của 026** chạm gateway, BFF, products, baskets, orders. Nó **không** chạm parties,
  và chỉ chạm identity lúc lấy token.
- **Newman**: chưa cài trên máy (`npx --no-install newman` báo lỗi). Node `v22.20.0` có sẵn.
- **Identity**: Duende IdentityServer dùng `AddOperationalStore` (SQL Server).
- **Kibana**: 9.4.4, license Basic, có khoá mã hoá saved objects từ 027. 4 rule ngân sách lỗi
  (tag `slo-error-budget`) chạy mỗi 5 phút. Dashboard `SLO vận hành hằng ngày — 7 service` được export
  ở `docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson`.
- **Ngưỡng độ trễ trong manifest**: BFF p95 300 ms / p99 800 ms; 6 service còn lại p95 150 ms /
  p99 500 ms. Ngưỡng 5xx là 0.1% cho cả 7 service.
- **Bản ghi diễn tập chaos**: `docs/dien-tap-chaos-engineering/` đã có `README.md` (mục "Lịch sử
  chạy"), `mau-ket-qua.md` và thư mục `ket-qua/` (025).

## Quyết định 1 — Không sửa code service; mọi hỏng hóc chỉ bằng cấu hình/tham số

**Decision** (Người dùng chốt): Không file nào dưới `services/` hay `shared/` bị sửa. Hỏng hóc được
tạo bằng cách tạo lại container với biến môi trường sai, hoặc bằng request mang header của 027.

**Hệ quả**:
- Loại "bug thật trong code" bị thay bằng một nhóm cấu hình sai khác; sau đó loại thay thế này cũng bị người dùng bỏ ở phiên `/speckit-tasks`, nên chỉ còn ba loại A, B, C.
- Cờ `Chaos:AllowFaultInjection` chỉ chặn được ở script. Middleware 027 vẫn tự kiểm tra cờ của nó.
- BFF không có kịch bản "cạn connection pool".

**Alternatives considered**: thêm code tiêm lỗi vào service. Bị loại vì người dùng không muốn sửa
code, nên cũng không viết test (xem Complexity Tracking trong plan.md).

## Quyết định 2 — Script `scripts/incident-drill.ps1`

**Decision** (Người dùng chốt: chỉ `.ps1`; tạo lại container + env; tạo lại cả 7 container; niêm
phong bằng file bị gitignore + SHA-256; reveal sau khi giải quyết; chặn bằng cờ ở script):

| Lệnh | Việc làm |
|---|---|
| `-Start` | (1) Đọc `.env`; nếu `CHAOS_ALLOW_FAULT_INJECTION` khác `true` thì dừng với thông báo, không tiêm gì.<br>(2) Chọn ngẫu nhiên service → loại hỏng hóc áp dụng được cho service đó → tham số (Quyết định 3) → độ trễ tiêm 0–30 phút → tỷ lệ 5xx 5–50% nếu là loại C.<br>(3) Ghi `.incident-drill/<runId>/sealed.json` và tính SHA-256 của file.<br>(4) In ra **chỉ** `runId` và mã băm.<br>(5) Khởi một tiến trình PowerShell nền, cửa sổ ẩn, để chờ rồi tiêm; lệnh `-Start` kết thúc ngay. |
| *(tiến trình nền)* | Chờ hết độ trễ, rồi:<br>- Ghi `docker-compose.incident.yml` vào `.incident-drill/<runId>/`, chỉ chứa biến môi trường sai cho service đích.<br>- Chạy `docker compose -f docker-compose.local.yml -f <file override> up -d --force-recreate --no-deps` cho cả 7 service.<br>- Ghi thời điểm tiêm thực tế vào `.incident-drill/<runId>/injected-at.txt`.<br>- Với loại C: tiếp tục gửi request mang header cho tới khi container đích bị tạo lại (Quyết định 4). |
| `-Start -Service <svc> -FaultType <A|B|C> [-DelaySeconds <n>]` | Chế độ xác minh không mù (người dùng chốt ở phiên `/speckit-tasks`): dùng cho xác minh V2/V4 và QA thủ công; in rõ là không mù; `sealed.json` có `"blind": false`. |
| `-Reveal -RunId <id>` | Tính lại SHA-256 của `sealed.json` và so với mã băm đã in. In lựa chọn và thời điểm tiêm thực tế. Xoá file override. |

- `.incident-drill/` được thêm vào `.gitignore` (FR-002).
- Script không in gì trong lúc chờ hay lúc tiêm. Thời điểm tiêm chỉ nằm trong file niêm phong.
- Mã băm được dán vào Kibana Case ngay khi mở Case. Nhờ vậy, lúc reveal chứng minh được lựa chọn
  không bị sửa sau.

**Rationale**: Chỉ có một người vận hành, nên "không biết trước" dựa vào việc script tự chọn và không
in lựa chọn ra. File override sinh tạm cho phép đặt biến chỉ cho container đích mà không sửa compose
đã commit. Tạo lại cả 7 container khiến uptime trong `docker ps` không lộ service đích.

**Hệ quả cần ghi trong tài liệu**:
- Tạo lại cả 7 container gây một khoảng gián đoạn ngắn trên mọi service. Rule phát hiện nhanh có thể
  bắn cho service không bị tiêm (nhiễu có chủ đích), và ngân sách lỗi của mọi service bị tiêu hao (FR-014).
- Việc niêm phong dựa vào kỷ luật không tự mở `sealed.json` (người dùng chọn băm, không chọn mã hoá).

## Quyết định 3 — Danh mục hỏng hóc theo service

| Loại | Áp dụng cho | Tham số sai | Nguồn |
|---|---|---|---|
| **A. Đích kết nối sai** | products, baskets, orders, parties, identity | `<SVC>_DB_CONNECTION` trỏ tới host không tồn tại (`Server=incident-missing-db;...`), giữ `Connect Timeout=3` | Người dùng chốt; tên host là Đề xuất — đã duyệt (2026-10-01) |
|  | BFF | một trong `PRODUCTS_/BASKETS_/ORDERS_/PARTIES_API_URL` (chọn ngẫu nhiên, niêm phong) trỏ tới `http://incident-missing-host:8080` | như trên |
|  | gateway | `BFF_API_URL=http://incident-missing-host:8080` | như trên |
| **B. Cạn connection pool** | ~~5 service có DB~~ | ~~`Max Pool Size=1`~~ — **bỏ** sau V2 (không gây lỗi dưới tải nền; người dùng chốt 2026-10-02) | Người dùng chốt |
|  | gateway | `ReverseProxy__Clusters__bff-cluster__HttpClient__MaxConnectionsPerServer=1` | như trên |
|  | BFF | *không áp dụng* | Người dùng chốt |
| **C. Lỗi 5xx của 027** | cả 7 | script gửi request mang `X-Chaos-Fault: 5xx` vào service đích với tỷ lệ ngẫu nhiên 5–50% (Quyết định 4) | Người dùng chốt |
| ~~D. Nhóm cấu hình sai khác~~ | — | **Bị loại** (người dùng chốt ở phiên `/speckit-tasks`, 2026-10-01). Đề xuất cũ: bỏ `TrustServerCertificate=true`. | Người dùng chốt |

Thứ tự bốc thăm: chọn service đều ngẫu nhiên trong 7, rồi chọn đều ngẫu nhiên trong các loại áp dụng
được cho service đó. Nhờ vậy service nào cũng có xác suất 1/7.

## Quyết định 4 — Loại C: gửi request mang header của 027

**Decision**:
- Container đích chạy với cờ `true` lấy từ `.env`; cả 7 service đều có cờ `true`, nhưng chỉ service
  đích nhận header.
- Tiến trình nền gửi request trực tiếp vào cổng publish của service đích, trên một route có traffic thật
  (bảng dưới). Middleware 027 chặn trước khi tới xác thực nên không cần token.
- Tốc độ gửi: đo số span server của service đích trong 5 phút trước khi tiêm (truy vấn
  `_query` ES|QL), rồi gửi `r / (1 − r) ×` tốc độ đó, với `r` là tỷ lệ 5–50% đã niêm phong.
- Dừng khi `docker inspect` cho thấy container đích đã có Id mới, tức người vận hành đã build lại từ
  master (FR-010).

| Service | Route (Đề xuất — đã duyệt (2026-10-01)) |
|---|---|
| gateway | `GET :5300/bff/products` |
| BFF | `GET :5301/bff/products` |
| products | `GET /products` |
| baskets | `GET /baskets/current` |
| orders | `GET /orders/{guid ngẫu nhiên}` |
| parties | `GET /parties/{guid ngẫu nhiên}` |
| identity | `GET /.well-known/openid-configuration` |

## Quyết định 5 — Tải nền: newman chạy folder Postman 00 + 26 + folder mới 28

**Decision** (Người dùng chốt: folder 26 qua newman, thêm traffic cho parties/identity):
- Thêm folder Postman `28 - Diễn tập sự cố: tải nền bổ sung (parties, identity)` vào
  `postman/ecommerce.postman_collection.v2.json`, gồm:
  - `GET {{gatewayUrl}}/bff/parties/{{$guid}}`, test status `404`;
  - `GET {{identityUrl}}/.well-known/openid-configuration`, test status `200`.
  - **Đề xuất — đã duyệt (2026-10-01)**: environment không có biến `partyId`, nên dùng guid ngẫu nhiên. `404` vẫn
    đi qua gateway → BFF → parties, tạo span thật cho parties, và không tính là request xấu.
- Chạy liên tục bằng:
  `npx newman run postman/ecommerce.postman_collection.v2.json -e postman/local.postman_environment.v2.json --folder "00 - …" --folder "26 - …" --folder "28 - …" -n <số vòng lớn> --delay-request <ms>`.
  Folder 00 chạy ở mỗi vòng nên token luôn mới, kể cả sau khi identity bị tạo lại. **Sửa khi triển khai (người dùng chốt 2026-10-02)**: lấy token mỗi vòng làm identity luôn vượt SLO (`POST /connect/token` p50 ~310 ms), nên script có chế độ `-Load`: lấy token (folder 00) một lần mỗi 30 phút, chạy folder 26 + 28 bằng `npx.cmd --yes newman@6.2.2` với environment đã có token; nghỉ 1000 ms giữa các request.
- Lệnh đầy đủ nằm trong `quickstart.md` và tài liệu diễn tập.

**Phụ thuộc mới**: `npx newman` sẽ tải gói `newman` từ npm lần đầu chạy, nên **phải hỏi người dùng
trước khi tải** ở giai đoạn triển khai.

**Hệ quả**: mỗi vòng folder 26 tạo một đơn hàng thật trong `orders-db` (QA_Debt 026 đã ghi). Tài liệu
diễn tập ghi cách dọn dữ liệu sau buổi diễn tập.

**Alternatives considered**: vá NBomber 026 (chạm phạm vi 026); viết script tải riêng. Cả hai bị
người dùng loại.

## Quyết định 6 — Rule phát hiện nhanh `incident-fast-detection`

**Decision** (Người dùng chốt: vượt SLO trong 5 phút, chu kỳ 5 phút, chỉ trong Kibana):
- Một rule `.es-query` dạng ES|QL, `groupBy: "row"`, `schedule.interval: 5m`, `timeWindowSize: 5`,
  `timeWindowUnit: m`, tag `incident-fast-detection`, không có action.
- Với mỗi service trong 5 phút gần nhất, rule tính:
  - `err_pct = 5xx / tổng × 100`;
  - `p95`, `p99` của `duration` (đổi ns sang ms);
  - ngưỡng theo service bằng `CASE` khớp manifest.
- Hàng được trả về khi `err_pct >= 0.1` hoặc `p95 > ngưỡng p95` hoặc `p99 > ngưỡng p99`. **Sửa khi triển khai (người dùng chốt 2026-10-02)**: riêng `Gateway.Api` chỉ xét `err_pct` — ngưỡng 150/500 ms của gateway chặt hơn ngưỡng 300/800 ms của BFF mà nó chuyển tiếp tới, nên gateway vượt độ trễ ngay ở mức nền.
- Kết thúc bằng `STATS ... BY service | KEEP service`. Lý do: Hệ quả 2 và 3 của research 027 cho thấy
  mã alert ghép từ mọi cột kết quả, và chỉ lấy cột của lệnh `STATS` cuối cùng.
- Service không có span nào trong 5 phút thì không có hàng nào, nên không có alert (Edge Case "không
  traffic").
- Export vào **file riêng** `docs/kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson`.
  Không gộp vào `error-budget-rules.ndjson`, để `ErrorBudgetRuleDefinitionTests` của 027 (đếm đúng
  4 rule) không bị ảnh hưởng.

**Hệ quả**: không có test giữ ngưỡng trong rule khớp manifest (người dùng chọn không viết test). Rủi ro
trôi dạt được ghi vào technical-debt, hạn tới khi SCRUM-37 xong.

## Quyết định 7 — Panel trên dashboard SLO hằng ngày

**Decision** (Người dùng chốt "có, thêm panel"): thêm một bảng "Phát hiện nhanh — vượt SLO trong
5 phút gần nhất" vào dashboard hiện có, đặt cạnh nhóm panel ngân sách lỗi của 027.
- Bảng đọc alert đang active từ `.alerts-stack.alerts-default`, lọc tag `incident-fast-detection`.
- Cột: service, thời điểm bắt đầu.
- Cập nhật `dashboards/slo-van-hanh-hang-ngay.ndjson` bằng export Saved Objects, giống 027.

## Quyết định 8 — Triage, Kibana Case và bản ghi sự cố

**Decision** (Người dùng chốt):
- Người vận hành **tạo tay** một Kibana Case khi nhận cảnh báo. Case ghi:
  - mã băm niêm phong;
  - severity SEV1/SEV2/SEV3 theo bảng trong spec FR-007;
  - một comment tại mỗi mốc, cộng cập nhật định kỳ mỗi 30 phút (giá trị lấy từ ví dụ của phương án,
    xem Assumptions trong spec);
  - chỉ số baseline "alert bắn → merge PR".
- Mẫu bản ghi mới `docs/dien-tap-chaos-engineering/mau-ban-ghi-su-co.md` mở rộng `mau-ket-qua.md`, với
  các trường bắt buộc ở [contracts/incident-record-contract.md](./contracts/incident-record-contract.md).
  Bản ghi lưu ở `ket-qua/<YYYY-MM-DD>-su-co-<runId>.md` và được thêm vào "Lịch sử chạy" của `README.md`.
- Quy trình triage (các bước và bảng severity) được viết trong `docs/dien-tap-chaos-engineering/README.md`,
  mục mới "Diễn tập sự cố on-call (SCRUM-36)".

## Quyết định 9 — Xác nhận khôi phục

**Decision** (Người dùng chốt: đạt SLO liên tục 15 phút + rule hết active; giảm thiểu = merge PR;
giải quyết = SLO 15 phút):
- Mốc giải quyết là thời điểm cuối của 15 phút liên tục mà 5xx < 0.1%, p95 và p99 trong ngưỡng, có
  traffic, và rule `incident-fast-detection` không còn active cho service đó.
- Bằng chứng: truy vấn ES|QL theo từng phút (có sẵn trong tài liệu Kibana mới) và ảnh chụp/link Kibana,
  đính kèm bản ghi sự cố.

## Điểm phải xác minh ở task đầu tiên của giai đoạn triển khai

| # | Điểm | Nếu không đúng |
|---|---|---|
| V1 | Kibana Cases dùng được trên license Basic (tạo Case, thêm comment) ở Kibana 9.4.4. | **Dừng, hỏi người dùng** chọn kênh thông báo khác. Không tự đổi. |
| V2 | Với tải nền newman, loại B (`Max Pool Size=1`; `MaxConnectionsPerServer=1` cho gateway) thật sự gây 5xx hoặc trễ vượt ngưỡng đủ để rule bắn. | **Dừng, hỏi người dùng** về con số pool hoặc tốc độ tải. |
| V3 | Tạo lại container identity không làm mất khoá ký token theo cách khiến mọi request khác bị 401 kéo dài sau vòng newman kế tiếp. | Ghi vào tài liệu là nhiễu đã biết, rồi hỏi người dùng có loại identity khỏi bước tạo lại hay không. |
| V4 | Middleware 027 chạy trên route ở bảng Quyết định 4 trước khi xác thực, và span 500 được xuất sang Elasticsearch. | Đổi route, giữ quyết định. |
| V5 | *(bỏ — loại D đã bị loại)* | — |
| V6 | ES|QL `PERCENTILE(duration, 95)` theo `service` với `groupBy: row` tạo đúng một alert cho mỗi service vượt SLO. | Sửa truy vấn, giữ quyết định. |

## Kết quả xác minh (T005–T007) — 2026-10-01, Kibana/Elasticsearch 9.4.4, license `basic`

Chạy trên stack `ecomerce-local` đang chạy (dựng từ checkout chính; người dùng chốt dùng chung stack
này, `.env` chép sang worktree).

| # | Kết quả | Bằng chứng |
|---|---|---|
| Tải nền (T005) | **ĐÚNG**. Newman (6.2.2) chạy folder 00 + 26 + 28, `--delay-request 200`. Sau ~6 phút, cả 7 service có span. | ES|QL 5 phút gần nhất: Baskets 175, Bff 369, Gateway 288, Identity 122, Orders 114, Parties 78, Products 115 span. |
| V1 (T006) | **ĐÚNG**. Kibana Cases (owner `cases`) tạo được, thêm comment được, đóng được trên license Basic. | Case thử `92706c1a-…` (tag `incident-drill-v1`) — tạo → 1 comment → `closed`; để lại ở trạng thái đóng. |
| V6 (T007) | **ĐÚNG**. Rule `.es-query` ES|QL `STATS p95 = PERCENTILE(duration, 95) BY service … KEEP service`, `groupBy: row` sinh một alert cho mỗi service; mã alert = tên service. | Rule tạm `v6-tam-028` (chu kỳ 1 phút, cửa sổ 5 phút) → 7 alert `active` (`Baskets.Api` … `Products.Api`); rule tạm đã xoá. |

**Hệ quả 1 — trường severity của Kibana Case** chỉ có `low/medium/high/critical`, không có SEV1–3.
Cách ghi SEV vào Case cần người dùng chốt trước khi viết quy trình (T024).

**Phát hiện ngoài phạm vi** (ghi vào QA_Debt mục 028):
- Request `04 Có token nhưng thiếu scope thì bị chặn (403)` của folder Postman 00 nhận `401 unauthorized`,
  không phải `403 forbidden_scope`, ở mọi vòng.
- Vòng newman đầu tiên trên stack nguội có `504` ở checkout và `/bff/parties`; từ vòng 2 thì xanh.

## Kết quả xác minh (T016–T018) — 2026-10-01/02

| # | Kết quả | Bằng chứng |
|---|---|---|
| Loại A (T016) | **ĐÚNG** cho products, bff, gateway. | 5xx 38/56 (products, 3 phút), 49/243 (bff), 93/226 (gateway). |
| Bất biến 5 | **ĐÚNG sau khi sửa**: một lệnh `up` gộp 7 service làm BFF/gateway kẹt `Created` (chờ service đích healthy theo `depends_on`) và Compose in đích danh service hỏng. Sửa: 7 lệnh `up --no-deps` riêng, chạy liền nhau. | injector.log của run `20261001-213042` (lỗi) và `20261001-213507` (đúng). |
| V2 — loại B trên orders (T017) | **SAI** ở tải nền 1 tiến trình newman. | 5 phút sau khi tiêm `Max Pool Size=1`: 0 lỗi, p95 25 ms, p99 67 ms. |

**Phát hiện — mức nền không khoẻ (chưa tiêm lỗi)**:
- Với folder 00 lấy token mỗi vòng: identity `POST /connect/token` p50 311 ms, p95 883 ms → identity luôn vượt p95 150 ms; gateway `POST` p95 538 ms → gateway luôn vượt (ngưỡng gateway 150/500 chặt hơn BFF 300/800 mà nó chuyển tiếp tới). Người dùng chốt: lấy token mỗi 30 phút (chế độ `-Load` của script) và rule xét gateway chỉ theo 5xx.
- Với chế độ `-Load` (token mỗi 30 phút, 1 tiến trình, nghỉ 200 ms), sáng 2026-10-02, hai cửa sổ 5 phút liên tiếp: 5xx nền gateway 0.84–2.9%, BFF 0.32–1.1%, baskets 0.28–0.83%, products 0–0.5%; p95 vượt ngưỡng ở hầu hết service. Elasticsearch có lúc dùng 267% CPU. Rule phát hiện nhanh như thiết kế sẽ bắn cho nhiều service khi không có sự cố.
- Hệ quả phụ: `--delay-request 0` bị newman từ chối (giá trị phải dương); `npx` gọi từ PowerShell báo "could not determine executable to run" — script dùng `npx.cmd --yes newman@6.2.2`.

**Tiếp T017–T018 (2026-10-02), các quyết định người dùng chốt trong phiên `/speckit-implement`:**

| Điểm | Kết quả đo | Người dùng chốt |
|---|---|---|
| Mức nền với `-Load` nghỉ 1000 ms | Hai cửa sổ 5 phút: 0 lỗi 5xx ở cả 7 service; p95 ≤ 133 ms, p99 ≤ 208 ms (gateway). Truy vấn rule (bản gateway chỉ xét 5xx) trả 0 hàng. | Giảm tải nền (1000 ms). |
| V2 — loại B | orders `Max Pool Size=1`: 0 lỗi, p95 25 ms. gateway `MaxConnectionsPerServer=1`: 45.1% span 5xx (BFF 18.5%). | Loại B chỉ cho gateway. |
| Nhiễu khởi động nguội | Phút tạo lại 7 container (09:26–09:27): p95 các service không bị tiêm vọt 2–17 s; 09:28–09:29 về bình thường. | Giữ tạo lại 7 container; quy trình triage ghi rõ alert thoáng qua do khởi động nguội là nhiễu. |
| V4 — loại C | parties 31.8% (niêm phong 46%), identity 33.3% (niêm phong 39%). Lần đầu ra 75.7% vì mức sàn 1 request/giây → bỏ mức sàn. | — |
| V3 — token sau khi tạo lại identity | Token cũ: 401 ở gateway, hoặc qua gateway nhưng bị service hạ lưu vừa tạo lại từ chối → BFF 502/504. | Tải nền tự lấy token lại khi gặp 401 hoặc khi Id container identity-api đổi. Sau sửa: token mới trong 27 s. |
| Khôi phục loại C | Chạy lại compose không đổi cấu hình thì không tạo lại đích → vòng gửi header không dừng. | (hệ quả) Khôi phục chuẩn cho mọi loại: cờ `CHAOS_ALLOW_FAULT_INJECTION` về tắt rồi chạy lại compose → cả 7 container được tạo lại. |
