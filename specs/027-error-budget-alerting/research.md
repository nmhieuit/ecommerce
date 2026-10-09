# Research: Chính sách ngân sách lỗi và ngưỡng cảnh báo (SCRUM-35)

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Mọi quyết định dưới đây hoặc do người dùng chốt trực tiếp trong phiên `/speckit-plan` ngày
2026-10-01 (ghi rõ "Người dùng chốt"), hoặc là hệ quả kỹ thuật bắt buộc của các lựa chọn đó (ghi rõ
"Hệ quả"). Mục "Điểm phải xác minh" liệt kê những gì chưa thể khẳng định chỉ bằng đọc tài liệu.

## Hiện trạng đã kiểm tra (trước khi ra quyết định)

- Elasticsearch/Kibana `9.4.4` chỉ chạy trong `docker-compose.yml` và `docker-compose.local.yml`
  (không có trong `k8s/`), `xpack.security.enabled: "false"`, license mặc định **Basic**.
- Kibana **chưa** cấu hình `xpack.encryptedSavedObjects.encryptionKey` — thiếu khoá này thì Kibana
  Alerting không cho tạo/chạy rule.
- Dữ liệu nguồn: index `traces-generic.otel-default*`; tên service ở
  `resource.attributes.service.name`; mã HTTP ở `attributes.http.response.status_code`; `duration`
  tính bằng **nanosecond** (đã xác minh ở `docs/kibana-quan-sat-he-thong/06-dashboard-xu-ly-su-co-va-ngan-sach-tuan.md`).
- 7 `service-manifest.yaml` đã có khối `slos` (021); `Bff.Api` có ngưỡng độ trễ p95 300ms / p99
  800ms, 6 service còn lại p95 150ms / p99 500ms.
- Dashboard `Ngân sách lỗi tuần — 7 service` (id `2a607bf4-2449-48a1-a2e8-1336ec35a7b7`) là nơi
  người vận hành mở mỗi ngày; export tại `docs/kibana-quan-sat-he-thong/dashboards/ngan-sach-loi-tuan.ndjson`.
- Chưa có cơ chế tiêm lỗi 5xx; chỉ có `ChaosLatencyInjectionMiddleware` (025) riêng trong Orders.Api.
- `tests/ServiceManifestSloConventionTests` (021) đã đọc cả 7 manifest bằng YamlDotNet.

## Quyết định 1 — Cơ chế tính tiêu hao và cảnh báo: Kibana rule "Elasticsearch query" dùng ES|QL

**Decision** (Người dùng chốt): Mỗi mốc cảnh báo là một Kibana rule loại **Elasticsearch query**
viết bằng **ES|QL**, tính mức tiêu hao từ đầu tuần tới nay của 4 ngân sách cho cả 7 service trong
một truy vấn, trả về một hàng cho mỗi cặp (service, ngân sách) đang ở trên mốc.

**Rationale**: Chạy được trên license Basic đang dùng; đúng yêu cầu "quản lý trong Kibana" (FR-008);
dùng chung dữ liệu traces mà dashboard 021 đã dùng nên số liệu nhất quán.

**Alternatives considered**:
- Tính năng SLO + burn-rate rule có sẵn của Kibana — cần license Platinum, bản thử hết hạn sau 30
  ngày thì ngừng chạy.
- Script định kỳ ngoài Kibana ghi kết quả vào Elasticsearch — lệch với FR-008 (cảnh báo quản lý trong
  Kibana), thêm một tiến trình phải vận hành.

## Quyết định 2 — Cách tính một ngân sách

**Decision** (Hệ quả của spec FR-002/FR-003/FR-013):

| Ngân sách | Request "xấu" | Tỷ lệ xấu cho phép |
|---|---|---|
| `availability` | `status_code >= 500` | 1% |
| `error-rate` | `status_code >= 500` | 1% |
| `latency-p95` | `duration > ngưỡng p95 của service × 1 000 000` (ns) | 5% |
| `latency-p99` | `duration > ngưỡng p99 của service × 1 000 000` (ns) | 1% |

`mức tiêu hao (%) = (số request xấu / tổng request) / tỷ lệ cho phép × 100`, tính trên các span có
`@timestamp` từ **thứ Hai 00:00 của tuần hiện tại theo giờ Việt Nam (UTC+7)** tới thời điểm chạy rule.
Ngưỡng độ trễ theo từng service (CASE theo `resource.attributes.service.name`) phải khớp đúng khối
`slos.latency` của manifest — được bảo vệ bằng test ở Quyết định 8.

Tập span được đếm giống hệt dashboard 021 (mọi span của service trong index traces; từ spec 033 loại span có đường dẫn bắt đầu bằng `/health`; từ spec 034 chỉ đếm span `kind = Server`),
để con số trên rule và trên dashboard không bao giờ lệch nhau. Đây là cùng một xấp xỉ mà hợp đồng
`specs/021-declare-service-slos/contracts/continuous-measurement-contract.md` đã chấp nhận.

Service không có span nào trong tuần thì không xuất hiện hàng nào → không có alert (FR-012).

## Quyết định 3 — Ba rule mốc, chu kỳ 5 phút, alert giữ liên tục

**Decision**: 3 rule — `error-budget-50`, `error-budget-75`, `error-budget-100` — mỗi rule chạy
**5 phút** một lần (Người dùng chốt). Mỗi hàng (service, ngân sách) vượt mốc là một alert riêng; alert
giữ trạng thái active chừng nào hàng đó còn được trả về (FR-007, Người dùng chốt "giữ hoạt động liên
tục"). Khi sang tuần mới, mức tiêu hao về 0 → hàng biến mất → alert tự recovered.

**Rationale**: 3 rule thay vì 12 (4 ngân sách × 3 mốc) vì một truy vấn ES|QL đã tính được cả 4 ngân
sách; tên rule nói rõ mốc, alert nói rõ service và ngân sách.

**Alternatives considered**: 12 rule (một rule mỗi cặp ngân sách × mốc) — trùng lặp truy vấn, khó giữ
nhất quán khi sửa.

## Quyết định 4 — Trạng thái "cạn ngân sách — ưu tiên độ tin cậy" suy ra tự động

**Decision** (Người dùng chốt "tự động từ lịch sử alert"):

1. Rule `error-budget-100` có một action **Index connector** (license Basic) với tần suất "khi đổi
   trạng thái": mỗi khi một alert (service, ngân sách) chuyển sang active, ghi một document
   `{ @timestamp, service, budget, event: "exhausted" }` vào index riêng `slo-error-budget-events`.
2. Rule thứ 4 `error-budget-frozen` (ES|QL, 5 phút) đọc đồng thời `slo-error-budget-events` và
   traces, cho mỗi service tính:
   - `exhausted_at` = lần cạn gần nhất (MAX `@timestamp` của event);
   - `last_bad_day` = ngày (giờ Việt Nam) gần nhất **có traffic** và không đạt đủ 4 chỉ tiêu SLO;
     ngày không có request được tính là đạt (Người dùng chốt);
   - `frozen` khi `exhausted_at` tồn tại và số ngày trọn vẹn tính từ sau
     `MAX(ngày(exhausted_at), last_bad_day)` tới hết hôm qua **< 3**.
   Mỗi service đang `frozen` là một alert active của rule này.
3. Vì event chỉ ghi lúc chuyển sang active (không ghi lúc recovered), việc ngân sách đặt lại đầu tuần
   không xoá `exhausted_at` → không tự gỡ trạng thái đóng băng (FR-010, User Story 3 kịch bản 3).

**Rationale**: Không cần lưu trạng thái ngoài Elasticsearch; trạng thái tính lại được từ dữ liệu bất
cứ lúc nào; một chuỗi ngày đạt SLO ngay trong tuần vẫn gỡ được đóng băng dù ngân sách tuần vẫn 100%
(User Story 3 kịch bản 2).

**Lưu ý mâu thuẫn tiềm ẩn đã được người dùng chấp nhận**: "ngày không có request tính là đạt" nghĩa là
một service ngừng nhận traffic 3 ngày sẽ tự hồi phục. Đây là lựa chọn của người dùng, khác với tinh
thần FR-012 (vốn chỉ nói về cảnh báo) — ghi lại để không ai coi đây là lỗi.

**Alternatives considered**: Người vận hành tự ghi trạng thái trong manifest — bị loại bởi người dùng.

## Quyết định 5 — Hiển thị trên dashboard Ngân sách lỗi tuần

**Decision** (Hệ quả của FR-008/FR-011 + Người dùng chốt "đưa vào dashboard SLO"): thêm vào dashboard
hiện có (không tạo dashboard mới) một nhóm panel "Ngân sách lỗi tuần này" đặt **trên cùng**:

1. Bảng mức tiêu hao: 7 service × 4 ngân sách, giá trị %, tô màu theo mốc 50/75/100 (cùng truy vấn
   ES|QL với rule, không theo cửa sổ thời gian của dashboard).
2. Bảng cảnh báo đang active: đọc alerts-as-data `.alerts-stack.alerts-default`, lọc theo tag
   `slo-error-budget`, cột service / ngân sách / mốc / mức tiêu hao.
3. Bảng "cạn ngân sách — ưu tiên độ tin cậy": alert active của rule `error-budget-frozen`.

Hợp đồng 021 (5 bất biến cũ) giữ nguyên; hợp đồng mới ở `contracts/error-budget-alert-rules-contract.md`.

## Quyết định 6 — Khoá mã hoá Kibana qua `.env`

**Decision** (Người dùng chốt): biến mới `KIBANA_ENCRYPTION_KEY` ở **Vùng 2 (biến bảo mật)** của
`.env.example`, truyền vào Kibana qua `XPACK_ENCRYPTEDSAVEDOBJECTS_ENCRYPTIONKEY` ở cả
`docker-compose.yml` và `docker-compose.local.yml`. Khoá dài tối thiểu 32 ký tự (yêu cầu của Kibana).
Giá trị trong `.env.example` chỉ dành cho Docker Desktop local, đúng quy ước sẵn có của file đó.

**Alternatives considered**: ghi cứng giá trị trong compose — bị loại (vi phạm Principle VI: secret
không nằm trong file cấu hình).

## Quyết định 7 — Tiêm lỗi 5xx dùng chung trong ServiceDefaults

**Decision** (Người dùng chốt "ServiceDefaults" + "middleware theo mẫu 025"):
`ChaosFaultInjectionMiddleware` trong `shared/ServiceDefaults`, được `UseServiceDefaults()` gắn ngay
sau `CorrelationIdMiddleware`, nên cả 7 service đều có. Hai lớp chặn giống 025:
- cấu hình `Chaos:AllowFaultInjection` (mặc định `false`, truyền qua biến compose
  `CHAOS_ALLOW_FAULT_INJECTION`, mặc định `false`);
- header `X-Chaos-Fault: 5xx` theo từng request — chỉ khi cả hai cùng bật thì middleware trả `500`
  ngay, không gọi phần còn lại của pipeline.

Span server của request vẫn do instrumentation ASP.NET Core tạo nên mã 500 đi qua đúng đường
OTel → Elasticsearch thật (lý do người dùng chọn phương án này thay vì ghi span giả).

**Alternatives considered**: chỉ đặt ở Orders.Api — người dùng chọn ServiceDefaults; ghi span giả vào
Elasticsearch — bỏ qua đường OTel thật.

## Quyết định 8 — Test viết trước (Principle III)

**Decision** (Người dùng chốt "có, viết test trước"):
- `tests/ServiceManifestSloConventionTests` thêm `ErrorBudgetPolicyTests`: cả 7 manifest có khối
  `error-budget-policy` đúng hình dạng và đúng con số của `contracts/error-budget-policy-manifest-shape.md`.
- Cùng dự án thêm `ErrorBudgetRuleDefinitionTests`: đọc file export rule
  (`docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson`) và kiểm tra ngưỡng độ trễ theo
  từng service trong ES|QL khớp manifest, đủ 4 rule, chu kỳ 5 phút, đúng mốc — chặn trôi dạt giữa rule
  và manifest. *(Đề xuất của người lập kế hoạch — người dùng đã xác nhận "Có" ở phiên `/speckit-tasks`.)*
- `shared/ServiceDefaults.UnitTests` thêm `ChaosFaultInjectionMiddlewareTests` cho các bất biến của
  `contracts/chaos-fault-injection-contract.md`.

Hành vi runtime trên Kibana thật (rule bắn đúng mốc, dashboard hiển thị) được xác thực bằng
`quickstart.md`, không chặn PR — cùng cách 021/025 đã làm.

## Quyết định 9 — Provisioning rule bằng export ndjson

**Decision** (Người dùng chốt): rule, connector Index và các panel mới được tạo trên UI, rồi export
bằng Saved Objects API vào `docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson` (rule +
connector) và cập nhật `dashboards/ngan-sach-loi-tuan.ndjson` (dashboard). Import lại bằng
`_import` như README dashboard hiện có.

**Hệ quả cần ghi trong tài liệu**: Kibana nhập rule ở trạng thái **disabled** và phải tạo lại API key,
nên sau mỗi lần import phải bật (enable) lại 4 rule — bước này nằm trong `quickstart.md`.

## Điểm phải xác minh ngay ở task đầu tiên của giai đoạn triển khai

| # | Điểm | Nếu không đúng |
|---|---|---|
| V1 | Rule "Elasticsearch query" dạng ES|QL trên Kibana 9.4.4 tạo **một alert cho mỗi hàng/nhóm** (cần cho Quyết định 3, 4). | Dừng lại, hỏi người dùng chọn lại cơ chế — không tự đổi phương án. |
| V2 | ES|QL đọc được hai index cùng lúc (`FROM traces-generic.otel-default*, slo-error-budget-events`) và hai tầng `STATS` theo Quyết định 4. | Dừng lại, hỏi người dùng. |
| V3 | Biểu thức ranh giới tuần UTC+7 (`DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours`) cho đúng thứ Hai 00:00 giờ Việt Nam. | Sửa biểu thức, không đổi quyết định. |
| V4 | Biến môi trường `XPACK_ENCRYPTEDSAVEDOBJECTS_ENCRYPTIONKEY` được image Kibana 9.4.4 ánh xạ vào `xpack.encryptedSavedObjects.encryptionKey`. | Mount `kibana.yml` tối thiểu thay vì biến môi trường. |
| V5 | Data view trên index ẩn `.alerts-stack.alerts-default` dùng được trong Lens/ES|QL panel. | Thêm action Index connector cho cả 3 mốc và cho dashboard đọc `slo-error-budget-events`. |

## Ràng buộc kỹ thuật (rút ra khi kiểm chứng trên Kibana/Elasticsearch 9.4.4, license `basic`)

Bằng chứng đo chi tiết của lần kiểm chứng gốc nằm ở `docs/QA/QA_Debt.md` (mục 027) và lịch sử git; ở đây
chỉ giữ những ràng buộc mà rule hiện tại và test dựa vào. Điểm V1–V5 ở bảng trên đều đã được xác nhận đúng.

- **Mapping**: `attributes.http.response.status_code` = `long`, `duration` = `long` (nanosecond),
  `resource.attributes.service.name` = `keyword`.
- **Đọc hai index cùng lúc**: `FROM traces-generic.otel-default*, slo-error-budget-events METADATA _index`
  chạy được với hai tầng `STATS`; phải `COALESCE(resource.attributes.service.name, service)` vì hai index
  đặt tên field service khác nhau. Index sự kiện phải được tạo trước với mapping `keyword` — connector
  Index không tự tạo mapping đúng (nếu để Kibana tự tạo thì `service`/`budget`/`event` thành `text`).
- **Ranh giới kỳ**: dịch sang giờ Việt Nam, làm tròn, dịch ngược — `DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours`
  (spec 029; làm tròn tuần của ES|QL bắt đầu từ thứ Hai); ngày dùng `DATE_TRUNC(1 day, @timestamp + 7 hours)`.
- **Ràng buộc 1 — cửa sổ thời gian của rule**: rule `.es-query` tự lọc `@timestamp` theo
  `timeWindowSize/Unit` trước khi chạy ES|QL. Rule mốc đặt `7 d` (phủ trọn một tuần lịch), dòng
  `WHERE @timestamp >= <đầu tuần UTC+7>` cắt lại đúng tuần hiện tại; rule `error-budget-frozen` đặt `14 d`
  (lần cạn có thể ở tuần trước) — spec 029.
- **Ràng buộc 2 — mã alert = giá trị mọi cột kết quả**: kết quả rule có cột số thay đổi theo thời gian (vd
  `consumed_pct`) thì mã alert đổi sau mỗi lần chạy → alert cũ "recovered", alert mới sinh ra mỗi 5 phút,
  phá FR-007 và làm sự kiện "cạn" ghi lặp. Vì vậy rule chỉ trả cột định danh (`KEEP service, budget`;
  `KEEP service` cho rule frozen); mức tiêu hao (%) hiện ở bảng riêng trên dashboard (người dùng chốt).
- **Ràng buộc 3 — cột định danh lấy từ lệnh `STATS` cuối cùng**: với `groupBy: "row"`,
  `kibana.alert.grouping` chỉ gồm các cột sinh ra từ lệnh `STATS` cuối; cột `budget` sinh bởi
  `EVAL`/`MV_EXPAND` sau `STATS ... BY service` bị bỏ và hai ngân sách của cùng service gộp vào một alert.
  Cách sửa: thêm `| STATS consumed_pct = MAX(consumed_pct) BY service, budget` ngay trước
  `WHERE consumed_pct >= N | KEEP service, budget`.
