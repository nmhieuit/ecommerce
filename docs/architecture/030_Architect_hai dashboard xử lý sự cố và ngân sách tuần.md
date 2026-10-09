# Kiến trúc: Tách dashboard SLO thành "Xử lý sự cố" và "Ngân sách lỗi tuần"

> **Cập nhật spec 033**: rule và panel ES|QL/Lens loại span `/health*` bằng `COALESCE(attributes.url.path, "")`; khoá `excluded-path-prefixes` trong manifest. Xem [033 Architect](033_Architect_loại%20span%20health%20khỏi%20ngân%20sách%20lỗi.md).
> **Cập nhật spec 034**: rule và panel ngân sách chỉ đếm span `kind = Server` (request mà chính service nhận), không đếm span Client/Producer; rule frozen giữ sự kiện cạn bằng `_index`. Xem [034 Architect](034_Architect_ngân%20sách%20lỗi%20chỉ%20đếm%20span%20Server.md).

*Đối tượng đọc: kỹ sư/kiến trúc sư cần hiểu tại sao hai dashboard được dựng như vậy và những ràng buộc của
Kibana 9.4.4 đã quyết định thiết kế. Spec: [`specs/030-incident-and-weekly-dashboards/`](../../specs/030-incident-and-weekly-dashboards/spec.md).*

Spec 030 thay dashboard `SLO vận hành hằng ngày — 7 service` (12 panel, trộn ba loại cửa sổ thời gian:
tuần lịch cố định, `NOW() - 15 minutes` cứng, `now-7d` cứng, và theo thanh thời gian) bằng hai dashboard mỗi cái
một file ndjson, id mới cố định. Không đổi SLO, ngưỡng, tỷ lệ cho phép hay hành vi của bất kỳ rule 027/028/029 nào;
chỉ đổi nơi hiển thị và cách lọc thời gian, cộng thêm test canh gác rule 028.

## 1. Hai dashboard và ranh giới trách nhiệm

| | `Xử lý sự cố — 7 service` | `Ngân sách lỗi tuần — 7 service` |
|---|---|---|
| Id | `e61fc7f3-17fe-428a-a373-da88af0a4a1e` | `2a607bf4-2449-48a1-a2e8-1336ec35a7b7` |
| File | `dashboards/xu-ly-su-co.ndjson` | `dashboards/ngan-sach-loi-tuan.ndjson` |
| Thời gian | thanh thời gian (mặc định `now-1h`, tự làm mới 1 phút) | tuần lịch UTC+7 qua điều khiển; mỗi panel `time_range` riêng `now-30d` |
| Panel | Bảng SLO, ngưỡng, Phát hiện nhanh, 5xx/p95/traffic+401/403 theo phút, `dotnet.exceptions`, status code, top endpoint chậm, lỗi gọi hạ lưu, log lỗi | mức tiêu hao, hạn mức còn lại, cảnh báo mốc, cạn ngân sách, error-rate/p95 theo ngày, tiêu hao lũy kế |
| Nguồn | traces, metrics, logs, `.alerts-stack.alerts-default` | traces, `.alerts-stack.alerts-default` |

Hai file độc lập (import theo thứ tự nào cũng được). Mỗi dashboard có một ô Markdown ở đầu trang với link `/app/dashboards#/view/<id kia>` — xem mục 5.

## 2. Dựng bằng Dashboards REST API (Kibana 9.4.4)

Dashboard được dựng bằng `PUT /api/dashboards/{id}` (cho phép đặt id): panel `vis` = Lens cấu hình đơn giản (cả nguồn data view lẫn ES|QL),
`discover_session` (nhúng sẵn hoặc `ref_id` tới saved search), `markdown`, section, và `pinned_panels` = điều khiển ES|QL. Lý do thay vì thao tác UI: lặp lại
được, không phụ thuộc thao tác tay (027/028 từng ghi UI Lens tự động hoá không ổn định). Bản lưu vào repo vẫn là Saved Objects Export
(`includeReferencesDeep`), như 021/027/028.

## 3. Hệ quả của thanh thời gian với ES|QL (V3)

Discover session ES|QL trên dashboard **tự bị thanh thời gian cắt thêm** (lọc theo `@timestamp` của index), kể cả khi truy vấn đã có `WHERE @timestamp`. Đo thật:
thanh 5 phút cho ~57 span/service dù truy vấn đã lọc cả tuần; đặt `time_range` riêng `now-30d` thì ra ~512 span (cả tuần). Do đó:
- Dashboard **Xử lý sự cố** không đặt `time_range` riêng cho panel nào và không dùng `NOW() - …` trong truy vấn.
- Dashboard **Ngân sách tuần** đặt `time_range` riêng rộng (30 ngày) cho **mọi** panel (người dùng chốt 30 ngày), truy vấn tự giới hạn bằng `week_start`.

## 4. Bộ chọn tuần (V1/V2)

Điều khiển ES|QL **tĩnh** (`STATIC_VALUES`), biến `?tuan_chon`, 4 giá trị tương đối `Tuần này` (mặc định) / `Tuần trước` / `2 tuần trước` / `3 tuần trước`. Một danh sách tuần
lấy từ dữ liệu chạy được nhưng điều khiển lưu sẵn một giá trị mặc định cố định, nên dashboard sẽ mặc định vào một tuần cũ khi sang tuần mới.
Truy vấn tính đầu tuần: `t0 = DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours`, `week_start = CASE(?tuan_chon == …, t0, t0 - 7 days, …)`
(ES|QL 9.4.4 không có `DATE_ADD`), lọc `@timestamp >= week_start AND @timestamp < week_start + 7 days`. Cùng công thức tiêu hao với rule mốc 027
(đã đối chiếu 28/28 dòng với truy vấn gốc). Hai panel cảnh báo/cạn lọc từ đầu tuần *hiện tại* và ghi rõ "(trạng thái hiện tại)" — alert là trạng thái, không có lịch sử từng tuần.
Cửa sổ 30 ngày giới hạn xem lại tối đa 3 tuần trước.

## 5. Link hai chiều bằng ô Markdown (V7)

Panel **Links** hoạt động (tham chiếu dashboard đích bằng saved object) nhưng hai dashboard tham chiếu nhau nên mỗi lần export sâu mỗi file chứa cả hai dashboard
(hai file giống hệt), trái "mỗi dashboard một file"; link ngoài đường dẫn tương đối bị Kibana vô hiệu. Người dùng chốt: ô Markdown `[…](/app/dashboards#/view/<id>)` theo id cố định. Giới hạn:
link chỉ đúng khi id được giữ nguyên (Kibana mặc định/sạch); import vào space khác làm Kibana đổi id.

## 6. Panel log lỗi và link sang trace (V4/V5)

Log trong một request **đã** mang `trace_id` (trường gốc) và `attributes.CorrelationId` (`CorrelationIdMiddleware` đẩy vào logging scope), nên không phải sửa `ServiceDefaults`.
Panel là Discover session cổ điển trên index-pattern `logs-generic.otel-default*` (id `logs-generic-otel-default`): lọc `severity_number >= 17` (Error + Fatal), 50 dòng. Trường `trace_id` có định dạng URL mở
Discover (data view traces) lọc theo `trace_id`. Kibana APM không đọc được dữ liệu OTel thô (`generic.otel`): trang APM hiện "Add data", nên không dùng link APM. Bảng ES|QL trong Discover không có định dạng trường
nên không làm link theo dòng được — vì vậy panel này không dùng ES|QL.

## 7. Lỗi gọi hạ lưu (V6)

Discover session ES|QL trên span `Client`: gọi = `resource.attributes.service.name`, đích suy ra từ `attributes.server.address` (`<tên>-api` → `<Tên>.Api`). Span "xấu" = `status.code = Error` hoặc 5xx hoặc vượt p95 của service đích
(150 ms, `Bff.Api` 300 ms). Bảng tổng hợp theo cặp, 20 dòng, xếp theo % xấu giảm dần, không link (không có trace id). Span Client tới `identity-api` (lấy cấu hình OIDC) cũng có mặt như mọi cặp.

## 8. Test canh gác rule `incident-fast-detection` (đóng sai lệch Nguyên tắc III của 028)

`IncidentFastDetectionRuleDefinitionTests` (7 test) đọc `alerts/incident-fast-detection-rule.ndjson` và khoá: loại/chu kỳ 5m/cửa sổ 5m/tag/`groupBy`; ngưỡng so sánh `> 0`; cửa sổ truy vấn 5 phút; ngưỡng 5xx = SLO 5xx của mọi manifest; ngưỡng p95/p99
theo từng service = manifest; Gateway chỉ xét 5xx; chỉ giữ cột `service`. Mỗi test đã được chứng minh có thể đỏ (đổi một giá trị trong file rule, đúng một test đỏ, hoàn tác). Contract:
[`incident-fast-detection-rule-contract.md`](../../specs/030-incident-and-weekly-dashboards/contracts/incident-fast-detection-rule-contract.md).

## 9. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/030-incident-and-weekly-dashboards-component.drawio`](../diagrams/030-incident-and-weekly-dashboards-component.drawio)
- Sơ đồ luồng nghiệp vụ: [`docs/diagrams/030-incident-and-weekly-dashboards-flow-nghiep-vu.drawio`](../diagrams/030-incident-and-weekly-dashboards-flow-nghiep-vu.drawio)
- Sơ đồ trình tự: [`docs/diagrams/030-incident-and-weekly-dashboards-sequence.drawio`](../diagrams/030-incident-and-weekly-dashboards-sequence.drawio)

## 10. Tham khảo thêm

Hướng dẫn từng dashboard: [`06-dashboard-xu-ly-su-co-va-ngan-sach-tuan.md`](../kibana-quan-sat-he-thong/06-dashboard-xu-ly-su-co-va-ngan-sach-tuan.md); import/export:
[`dashboards/README.md`](../kibana-quan-sat-he-thong/dashboards/README.md). Bằng chứng kiểm chứng V1–V8:
[`specs/030-incident-and-weekly-dashboards/research.md`](../../specs/030-incident-and-weekly-dashboards/research.md). Giới hạn đã biết: [technical-debt.md](technical-debt.md) mục 030 và
[`docs/QA/QA_Debt.md`](../QA/QA_Debt.md) mục 030.
