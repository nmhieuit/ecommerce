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
  tính bằng **nanosecond** (đã xác minh ở `docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md`).
- 7 `service-manifest.yaml` đã có khối `slos` (021); `Bff.Api` có ngưỡng độ trễ p95 300ms / p99
  800ms, 6 service còn lại p95 150ms / p99 500ms.
- Dashboard `SLO vận hành hằng ngày — 7 service` (id `e2e06ff5-9cdf-4bea-acc8-5fd60ce26170`) là nơi
  người vận hành mở mỗi ngày; export tại `docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson`.
- Chưa có cơ chế tiêm lỗi 5xx; chỉ có `ChaosLatencyInjectionMiddleware` (025) riêng trong Orders.Api.
- `tests/ServiceManifestSloConventionTests` (021) đã đọc cả 7 manifest bằng YamlDotNet.

## Quyết định 1 — Cơ chế tính tiêu hao và cảnh báo: Kibana rule "Elasticsearch query" dùng ES|QL

**Decision** (Người dùng chốt): Mỗi mốc cảnh báo là một Kibana rule loại **Elasticsearch query**
viết bằng **ES|QL**, tính mức tiêu hao từ đầu tháng tới nay của 4 ngân sách cho cả 7 service trong
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
| `availability` | `status_code >= 500` | 0.1% |
| `error-rate` | `status_code >= 500` | 0.1% |
| `latency-p95` | `duration > ngưỡng p95 của service × 1 000 000` (ns) | 5% |
| `latency-p99` | `duration > ngưỡng p99 của service × 1 000 000` (ns) | 1% |

`mức tiêu hao (%) = (số request xấu / tổng request) / tỷ lệ cho phép × 100`, tính trên các span có
`@timestamp` từ **00:00 ngày 1 của tháng hiện tại theo giờ Việt Nam (UTC+7)** tới thời điểm chạy rule.
Ngưỡng độ trễ theo từng service (CASE theo `resource.attributes.service.name`) phải khớp đúng khối
`slos.latency` của manifest — được bảo vệ bằng test ở Quyết định 8.

Tập span được đếm giống hệt dashboard 021 (mọi span của service trong index traces, không lọc thêm),
để con số trên rule và trên dashboard không bao giờ lệch nhau. Đây là cùng một xấp xỉ mà hợp đồng
`specs/021-declare-service-slos/contracts/continuous-measurement-contract.md` đã chấp nhận.

Service không có span nào trong tháng thì không xuất hiện hàng nào → không có alert (FR-012).

## Quyết định 3 — Ba rule mốc, chu kỳ 5 phút, alert giữ liên tục

**Decision**: 3 rule — `error-budget-50`, `error-budget-75`, `error-budget-100` — mỗi rule chạy
**5 phút** một lần (Người dùng chốt). Mỗi hàng (service, ngân sách) vượt mốc là một alert riêng; alert
giữ trạng thái active chừng nào hàng đó còn được trả về (FR-007, Người dùng chốt "giữ hoạt động liên
tục"). Khi sang tháng mới, mức tiêu hao về 0 → hàng biến mất → alert tự recovered.

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
3. Vì event chỉ ghi lúc chuyển sang active (không ghi lúc recovered), việc ngân sách đặt lại đầu tháng
   không xoá `exhausted_at` → không tự gỡ trạng thái đóng băng (FR-010, User Story 3 kịch bản 3).

**Rationale**: Không cần lưu trạng thái ngoài Elasticsearch; trạng thái tính lại được từ dữ liệu bất
cứ lúc nào; một chuỗi ngày đạt SLO ngay trong tháng vẫn gỡ được đóng băng dù ngân sách tháng vẫn 100%
(User Story 3 kịch bản 2).

**Lưu ý mâu thuẫn tiềm ẩn đã được người dùng chấp nhận**: "ngày không có request tính là đạt" nghĩa là
một service ngừng nhận traffic 3 ngày sẽ tự hồi phục. Đây là lựa chọn của người dùng, khác với tinh
thần FR-012 (vốn chỉ nói về cảnh báo) — ghi lại để không ai coi đây là lỗi.

**Alternatives considered**: Người vận hành tự ghi trạng thái trong manifest — bị loại bởi người dùng.

## Quyết định 5 — Hiển thị trên dashboard SLO hằng ngày

**Decision** (Hệ quả của FR-008/FR-011 + Người dùng chốt "đưa vào dashboard SLO"): thêm vào dashboard
hiện có (không tạo dashboard mới) một nhóm panel "Ngân sách lỗi tháng này" đặt **trên cùng**:

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
connector) và cập nhật `dashboards/slo-van-hanh-hang-ngay.ndjson` (dashboard). Import lại bằng
`_import` như README dashboard hiện có.

**Hệ quả cần ghi trong tài liệu**: Kibana nhập rule ở trạng thái **disabled** và phải tạo lại API key,
nên sau mỗi lần import phải bật (enable) lại 4 rule — bước này nằm trong `quickstart.md`.

## Điểm phải xác minh ngay ở task đầu tiên của giai đoạn triển khai

| # | Điểm | Nếu không đúng |
|---|---|---|
| V1 | Rule "Elasticsearch query" dạng ES|QL trên Kibana 9.4.4 tạo **một alert cho mỗi hàng/nhóm** (cần cho Quyết định 3, 4). | Dừng lại, hỏi người dùng chọn lại cơ chế — không tự đổi phương án. |
| V2 | ES|QL đọc được hai index cùng lúc (`FROM traces-generic.otel-default*, slo-error-budget-events`) và hai tầng `STATS` theo Quyết định 4. | Dừng lại, hỏi người dùng. |
| V3 | Biểu thức ranh giới tháng UTC+7 (`DATE_TRUNC(1 month, NOW() + 7 hours) - 7 hours`) cho đúng 00:00 ngày 1 giờ Việt Nam. | Sửa biểu thức, không đổi quyết định. |
| V4 | Biến môi trường `XPACK_ENCRYPTEDSAVEDOBJECTS_ENCRYPTIONKEY` được image Kibana 9.4.4 ánh xạ vào `xpack.encryptedSavedObjects.encryptionKey`. | Mount `kibana.yml` tối thiểu thay vì biến môi trường. |
| V5 | Data view trên index ẩn `.alerts-stack.alerts-default` dùng được trong Lens/ES|QL panel. | Thêm action Index connector cho cả 3 mốc và cho dashboard đọc `slo-error-budget-events`. |

## Kết quả xác minh (T005–T008) — 2026-10-01, Kibana/Elasticsearch 9.4.4, license `basic`

| # | Kết quả | Bằng chứng |
|---|---|---|
| V1 | **ĐÚNG**, kèm 2 hệ quả (bên dưới). Rule `.es-query` với `searchType: esqlQuery` + `groupBy: "row"` tạo một alert cho mỗi hàng. | Rule tạm `STATS c = COUNT(*) BY service` sinh 7 alert active, `kibana.alert.grouping = {c, service}`. |
| V2 | **ĐÚNG**. `FROM traces-generic.otel-default*, <index sự kiện> METADATA _index` + hai tầng `STATS` chạy được. Phải `COALESCE(resource.attributes.service.name, service)` vì hai index đặt tên field service khác nhau. | Index thử với 1 event `Orders.Api` → chỉ hàng `Orders.Api` có `exhausted_at`, 6 service còn lại `null`. |
| V3 | **ĐÚNG**. `DATE_TRUNC(1 month, NOW() + 7 hours) - 7 hours` = `2026-09-30T17:00:00Z` = 00:00 ngày 1/10 giờ Việt Nam. Ngày dùng `DATE_TRUNC(1 day, @timestamp + 7 hours)`. | `_query` lúc `2026-10-01T04:52Z`. |
| V4 | **ĐÚNG**. Biến `XPACK_ENCRYPTEDSAVEDOBJECTS_ENCRYPTIONKEY` được image ánh xạ; không cần `kibana.yml`. | `GET /api/alerting/_health` → `has_permanent_encryption_key: true`. |
| V5 | **ĐÚNG**. ES|QL `FROM .alerts-stack.alerts-default` đọc được alert active; data view trên index ẩn (`allowHidden: true`) tạo được. | Truy vấn trả `kibana.alert.rule.name`, `kibana.alert.instance.id`, `kibana.alert.start` của rule tạm. |

Mapping thật: `attributes.http.response.status_code` = `long`, `duration` = `long` (ns),
`resource.attributes.service.name` = `keyword`.

**Hệ quả 1 — cửa sổ thời gian của rule**: rule `.es-query` tự lọc `@timestamp` theo
`timeWindowSize/Unit` (lý do alert ghi "in the last 5m"). Rule mốc đặt `timeWindowSize: 31`,
`timeWindowUnit: d` (phủ trọn tháng dài nhất), dòng `WHERE @timestamp >= <đầu tháng UTC+7>` trong ES|QL
cắt lại đúng tháng hiện tại. Rule `error-budget-frozen` cần nhìn lại xa hơn một tháng (lần cạn có thể
ở tháng trước) nên đặt `timeWindowSize: 62`, `timeWindowUnit: d`.

**Hệ quả 2 — mã alert = giá trị mọi cột kết quả** (`kibana.alert.instance.id = "31,Baskets.Api"`).
Nếu kết quả rule có cột `consumed_pct` thì mã alert đổi sau mỗi lần chạy → alert cũ "recovered", alert
mới sinh ra mỗi 5 phút, phá FR-007 và làm event "cạn" ghi lặp. **Người dùng chốt (2026-10-01)**: rule
chỉ trả 2 cột `service`, `budget` (`KEEP service, budget`); bảng alert trên dashboard hiện service /
ngân sách / mốc, còn mức tiêu hao (%) hiện ở bảng "mức tiêu hao" ngay bên cạnh. Contract bất biến 8 và
spec SC-003 đã sửa theo.

**Hệ quả 3 — Kibana chỉ lấy cột định danh alert từ lệnh `STATS` cuối cùng** (phát hiện khi chạy T030,
2026-10-01). Với `groupBy: "row"`, `kibana.alert.grouping` chỉ gồm các cột kết quả sinh ra từ lệnh
`STATS` cuối cùng của truy vấn. Cột `budget` sinh bởi `EVAL`/`MV_EXPAND` sau `STATS ... BY service` bị
bỏ, nên mã alert chỉ còn `Baskets.Api` — hai ngân sách của cùng service bị gộp vào một alert (sai
FR-006). Cách sửa đã kiểm chứng: thêm `| STATS consumed_pct = MAX(consumed_pct) BY service, budget`
ngay trước `WHERE consumed_pct >= N | KEEP service, budget`. Sau sửa, mã alert là
`Baskets.Api,latency-p99`; rule 50 sinh 4 alert, rule 75 sinh 2 alert, khớp từng hàng với bảng mức tiêu
hao lúc 05:02Z; các alert kiểu cũ tự `recovered`.
