# Research: Tách dashboard SLO thành "Xử lý sự cố" và "Ngân sách lỗi tuần"

**Ngày**: 2026-10-05 | **Spec**: [spec.md](./spec.md) | **Stack kiểm tra**: Kibana/Elasticsearch 9.4.4 đang chạy ở `localhost` (Docker Compose `ecomerce-local`)

Quy ước: **Đã kiểm chứng** = chạy thật lúc lập plan (chỉ đọc, qua `POST /_query` hoặc `_search`; không tạo/sửa object nào trên Kibana). **Chưa kiểm chứng** = giả định, bắt buộc có task kiểm chứng ở [quickstart.md](./quickstart.md); sai thì **dừng và hỏi người dùng** (không tự lùi).

## Dữ kiện đo được trên stack thật

| # | Dữ kiện | Kết quả |
|---|---|---|
| F1 | Span trong `traces-generic.otel-default*` | 1927 span, **chỉ có `kind: Server`**, 7 service. Chưa có span `Client` nào (chưa có lời gọi service → service). `ServiceDefaults` đã bật `AddHttpClientInstrumentation()` nên span Client sẽ có khi có lời gọi. |
| F2 | Trường span dùng được | `attributes.http.response.status_code`, `attributes.correlation.id`, `attributes.server.address`, `status.code`, `kind`, `trace_id`, `duration` (ns). |
| F3 | Log trong `logs-generic.otel-default*` | Chỉ có `severity_text` Information/Warning; **không có Error nào**; trường `trace_id`, `span_id` có trong mapping nhưng `COUNT(trace_id) = 0` trên mọi log hiện có. |
| F4 | Nguồn log hiện có | Chỉ log nền của `Microsoft.EntityFrameworkCore.Database.Command` (Orders.Api) và `MassTransit`. **Chưa có log nào nằm trong một request** (log pipeline ASP.NET không được xuất ở mức mặc định). |
| F5 | Trường correlation id ở log | `CorrelationIdMiddleware` đẩy `CorrelationId` vào logging scope, `IncludeScopes = true`. Mapping log **không có** `attributes.CorrelationId` (vì chưa có log nào nằm trong request), nên **chưa kết luận được** là đủ hay thiếu. |
| F6 | Danh sách tuần có dữ liệu | `DATE_TRUNC(1 week, @timestamp + 7 hours) - 7 hours` → `2026-10-04T17:00:00.000Z` (đúng thứ Hai 00:00 UTC+7), 2168 span. |
| F7 | Tham số ES|QL `?w` với chuỗi ngày | `@timestamp >= TO_DATETIME(?w) AND @timestamp < TO_DATETIME(?w) + 7 days` chạy đúng, trả 2168 span. |
| F8 | Tiêu hao lũy kế theo ngày | `MV_EXPAND` `as_of = [0..6]` rồi `WHERE DATE_DIFF("day", TO_DATETIME(?w), @timestamp) <= as_of` chạy đúng (lỗi lúc đầu: không nhân `as_of * 1 day`, dùng `DATE_DIFF`). |
| F9 | Dashboard hiện tại | 12 panel: 4 Discover session (3 của 027 + 1 của 028), 7 Lens, 1 Markdown. Không có collapsible section, control hay links trong file ndjson hiện tại. Đã có index-pattern cho traces và metrics, **chưa có cho logs**. |
| F10 | Rule `incident-fast-detection` | id `9b0e2c36-678b-4cd7-9de0-7468d623f82d`, tag `incident-fast-detection`, mỗi 5 phút, ES|QL cửa sổ `NOW() - 5 minutes`, ngưỡng `err_pct >= 1`, Bff.Api p95/p99 300/800 ms còn lại 150/500 ms, Gateway.Api bỏ xét độ trễ, chỉ giữ cột `service`. |
| F11 | Test hiện có | `ErrorBudgetRuleDefinitionTests` chỉ đọc `alerts/error-budget-rules.ndjson`. **Không có test nào cho `incident-fast-detection-rule.ndjson`** và không có test đọc file dashboard. |

## Quyết định

### D1 — Cách dựng và lưu hai dashboard
- **Quyết định**: dựng trên Kibana đang chạy (Saved Objects API cho Discover session như 027/028; giao diện cho Lens, điều khiển, link, section), rồi export từng dashboard bằng Saved Objects Export API với `includeReferencesDeep`, mỗi dashboard một file ndjson. Hai file thay cho `slo-van-hanh-hang-ngay.ndjson`.
- **Lý do**: điều khiển, link, section, Lens ES|QL không có định dạng ổn định để viết JSON tay; export từ Kibana thật đảm bảo import lại được (SC-006). Giữ nguyên cách lưu của 021/027/028 (người dùng đã chốt).
- **Phương án đã loại**: viết JSON tay toàn bộ — dễ sai phiên bản schema 9.4.4.

### D2 — Dashboard Xử lý sự cố: mọi panel theo thanh thời gian
- **Quyết định**:
  - Bảng SLO, ô Markdown, `dotnet.exceptions`, phân bố status code, top endpoint chậm nhất: giữ nguyên Lens của dashboard cũ (đã theo thanh thời gian), chép sang dashboard mới.
  - Biểu đồ **5xx theo phút theo service**, **p95 theo phút theo service**, **traffic + 401/403 theo phút theo service**: Lens dùng index-pattern traces có sẵn (date histogram theo phút, tách theo `service.name`, KQL lọc status code), cùng cách các panel 7–8 cũ đã dùng.
  - **Phát hiện nhanh**: giữ saved search `incident-fast-detection-active-alerts` nhưng bỏ `AND @timestamp >= NOW() - 15 minutes`, thêm cột trạng thái alert để phân biệt đang hoạt động với đã tắt (FR-007).
  - **Lỗi gọi hạ lưu**: Discover session ES|QL mới (bảng tổng hợp theo cặp, 20 dòng, không link).
  - **Log lỗi gần nhất**: Discover session (xem D6).
- **Lý do**: panel không có điều kiện thời gian cứng thì Kibana tự áp thanh thời gian. Lens trên index-pattern đã được chứng minh ở dashboard cũ; không cần thử Lens ES|QL cho phần theo phút.
- **Chưa kiểm chứng (V3)**: Discover session ES|QL trên dashboard có áp thanh thời gian lên `FROM … @timestamp` khi truy vấn không có điều kiện thời gian hay không, và `@timestamp` của `.alerts-stack.alerts-default` (thời điểm cập nhật alert) có đủ cho panel Phát hiện nhanh. Nếu không áp, dùng biến thời gian của ES|QL (`?_tstart`, `?_tend`).

### D3 — Dashboard Ngân sách tuần và bộ chọn tuần
- **Quyết định**: một **điều khiển ES|QL kiểu "giá trị từ truy vấn"** trên dashboard, biến `?week_start`, danh sách tuần lấy từ dữ liệu traces:
  ```
  FROM traces-generic.otel-default*
  | EVAL week_start = DATE_TRUNC(1 week, @timestamp + 7 hours) - 7 hours
  | STATS spans = COUNT(*) BY week_start
  | SORT week_start DESC
  ```
  Mặc định chọn tuần mới nhất (tuần hiện tại). Mọi truy vấn tính từ traces lọc `@timestamp >= TO_DATETIME(?week_start) AND @timestamp < TO_DATETIME(?week_start) + 7 days` (đã kiểm chứng F6, F7).
- **Lý do**: tuần lịch giờ VN đã được chứng minh ở 029; danh sách tuần lấy từ dữ liệu nên không phải bảo trì tay; truy vấn chỉ thay `NOW()` bằng biến.
- **Chưa kiểm chứng (V1)**: điều khiển ES|QL kiểu "giá trị từ truy vấn" tồn tại, lưu được vào ndjson và import lại được trên Kibana 9.4.4; giá trị trả về dạng chuỗi ngày (đã kiểm chứng phần ES|QL nhận chuỗi, F7). **Không chạy được thì dừng và hỏi** (Clarifications của spec).
- **Phương án đã loại**: danh sách tuần gõ tay (phải sửa mỗi tuần); hai dashboard "tuần này"/"tuần trước" (người dùng đã chọn bộ chọn).

### D4 — Công thức hạn mức còn lại và tiêu hao lũy kế
- **Quyết định**:
  - Mức tiêu hao: giữ đúng công thức của `slo-error-budget-consumption` (cùng tỷ lệ 0.01/0.01/0.05/0.01, cùng ngưỡng độ trễ), chỉ thay cửa sổ bằng `?week_start`.
  - Hạn mức còn lại: `remaining_pct = 100 − consumed_pct`; `remaining_requests = allowed × total − bad` (theo tổng request tới hiện tại, đã chốt ở spec).
  - Lũy kế theo ngày: dùng kỹ thuật F8 — bung `as_of = [0..6]` rồi lọc `ngày ≤ as_of` để ra mức tiêu hao cuối từng ngày từ thứ Hai. Ngày chưa tới (tuần hiện tại) không có hàng.
  - error-rate / p95 theo ngày trong tuần: nhóm theo `DATE_TRUNC(1 day, @timestamp + 7 hours) - 7 hours`, dùng `?week_start`.
- **Lý do**: ES|QL không có hàm cửa sổ cộng dồn; F8 là cách đã chạy thật. Công thức tiêu hao không đổi nên khớp rule mốc (SC-003).
- **Điểm hỏi ở `/speckit-tasks`**: số `remaining_requests` âm (đã vượt) hiển thị số âm hay 0; làm tròn xuống số nguyên.

### D5 — Panel dạng biểu đồ của dashboard Ngân sách tuần
- **Quyết định**: error-rate/p95 theo ngày và tiêu hao lũy kế là **Lens kiểu ES|QL** (nguồn `?week_start`), vì Lens trên index-pattern không nhận biến ES|QL. Bảng mức tiêu hao và hạn mức còn lại là Discover session ES|QL.
- **Chưa kiểm chứng (V2)**: Lens ES|QL nhận biến `?week_start` của điều khiển, lưu và import lại được. Nếu không: dừng và hỏi (như D3).

### D6 — Panel log lỗi gần nhất và link sang trace
- **Quyết định**: **Discover session cổ điển** (không ES|QL) trên một index-pattern logs mới (`logs-generic.otel-default*`), bộ lọc `severity_number >= 17` (Error và Fatal), sắp xếp `@timestamp` giảm dần, kích thước mẫu 50, cột: `@timestamp`, `resource.attributes.service.name`, message, `trace_id`, `attributes.CorrelationId` (tên trường theo kết quả V4). Trường `trace_id` của index-pattern đặt **định dạng URL** trỏ `/app/apm/link-to/trace/{{value}}` để mỗi dòng là một link.
- **Lý do**: bảng ES|QL trong Discover không có định dạng trường nên không thể làm link theo dòng; Discover session cổ điển dùng định dạng trường của index-pattern và được export cùng index-pattern.
- **Chưa kiểm chứng (V5)**: (a) link `/app/apm/link-to/trace/{{value}}` mở được trace thật từ dữ liệu `generic.otel` (APM của Kibana phải đọc được `traces-*.otel-*`; hiện chỉ gọi thử đường dẫn, trả 200 là vỏ trang chứ chưa chứng minh trace hiện ra); (b) định dạng URL được export và import lại. Không mở được trace thì dừng và hỏi.
- **Phương án đã loại**: ES|QL (không link được); link thủ công qua Markdown (không theo dòng).

### D7 — Correlation id và trace id trong log (FR-019)
- **Quyết định**: kiểm chứng trước (V4): bật chế độ tiêm 5xx hoặc gây một ngoại lệ chưa bắt trong request thật của một service, rồi truy vấn log Error vừa sinh ra xem có `trace_id` và `attributes.CorrelationId`. **Đủ** thì không sửa code, dùng đúng tên trường tìm được. **Thiếu** thì sửa `ServiceDefaults` (ví dụ gắn `correlation.id` vào log từ `Activity`/scope), test viết trước trong `shared/ServiceDefaults.UnitTests`, chạy thấy đỏ rồi mới sửa (Nguyên tắc III), và tạo lại 7 service để kiểm chứng.
- **Lý do**: người dùng chốt "kiểm chứng trước, chỉ sửa nếu thiếu". Hiến chương Nguyên tắc VII yêu cầu log mang định danh tương quan nên nếu thiếu thì đây là sai lệch cần đóng.
- **Rủi ro**: chế độ tiêm 5xx của 027 trả 500 từ middleware nên có thể **không** sinh log Error; cần gây ngoại lệ thật (V4 chọn cách gây cụ thể trong tasks).

### D8 — Lỗi gọi hạ lưu
- **Quyết định**: Discover session ES|QL trên span `kind == "Client"`. Service gọi = `resource.attributes.service.name`; đích = suy ra từ `attributes.server.address` bằng bảng ánh xạ tên host sang tên service (host Docker Compose/Kubernetes → `Orders.Api`…). Một span Client tính là **xấu** khi `status.code == Error` hoặc `attributes.http.response.status_code >= 500` hoặc `duration` vượt ngưỡng p95 đã khai báo của service đích (150 ms, `Bff.Api` 300 ms). Cột gợi ý: service gọi, đích, số span, số span lỗi, số span chậm, % xấu, p95; 20 dòng, sắp theo % xấu giảm dần. Không link.
- **Chưa kiểm chứng (V6)**: hiện chưa có span Client (F1). Cần tạo lưu lượng service → service thật (qua BFF/gateway, cần token hoặc luồng có sẵn của folder Postman) rồi xem `server.address` thật của từng đích để lập bảng ánh xạ. Chốt tên cột và thứ tự ở `/speckit-tasks`.

### D9 — Link qua lại và collapsible section
- **Quyết định**: dùng panel Links của Kibana (kiểu "dashboard link") ở mỗi dashboard trỏ sang dashboard kia; nhóm panel bằng collapsible section ở trạng thái mở. Cả hai dựng bằng giao diện rồi export.
- **Chưa kiểm chứng (V7, V8)**: panel Links và section export/import được trong ndjson 9.4.4 và link trỏ đúng id dashboard sau khi import vào Kibana sạch (id cố định vì ta đặt id mới khi tạo — xem D10). File `06` đã ghi một giới hạn: section lưu trạng thái đóng/mở nhưng không thật sự ẩn nội dung ở chế độ View — dùng luôn mở nên không ảnh hưởng. Link hay section không dùng được thì dừng và hỏi.

### D10 — Id saved object
- **Quyết định**: id mới cho cả hai dashboard (người dùng chốt). Dùng **id cố định đặt tay** (UUID tạo một lần, ghi vào contract) để link qua lại giữa hai dashboard ổn định khi import vào Kibana khác. Id mới này **chưa được tạo** ở bước lập plan; sẽ ghi vào [contracts/dashboards-contract.md](./contracts/dashboards-contract.md) khi tạo.
- **Lý do**: link dashboard tham chiếu theo id; nếu mỗi lần import đổi id thì link gãy.

### D11 — Test canh gác `incident-fast-detection`
- **Quyết định**: thêm `IncidentFastDetectionRuleDefinitionTests` vào `tests/ServiceManifestSloConventionTests`, đọc `docs/kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson` theo khuôn `ErrorBudgetRuleDefinitionTests`. Bất biến (xem [contracts/incident-fast-detection-rule-contract.md](./contracts/incident-fast-detection-rule-contract.md)): tag, chu kỳ 5 phút, cửa sổ 5 phút, ngưỡng 5xx bằng SLO 5xx của manifest (1%), ngưỡng p95/p99 khớp mọi manifest, Gateway chỉ xét 5xx, chỉ trả cột `service`. Mỗi test chạy thấy đỏ trước (bằng cách tạm đổi giá trị trong bản sao hoặc viết test trước khi file khớp), ghi vào comment `Task nguồn: spec 030 — FR-015`.
- Test 027 (`ErrorBudgetRuleDefinitionTests`) **không cần sửa**: nó chỉ đọc file rule ngân sách; việc chuyển panel sang dashboard mới không ảnh hưởng. Chạy lại để xác nhận xanh.
- **Điểm hỏi ở `/speckit-tasks`**: có thêm test canh dashboard (không còn `now-7d`, `NOW() - 15 minutes` trong file dashboard Xử lý sự cố) hay không — ngoài FR-015, nên không tự thêm.

### D12 — Dọn tài liệu trỏ tới dashboard cũ
- **Quyết định**: tìm `e2e06ff5` và `slo-van-hanh-hang-ngay`/"SLO vận hành hằng ngày" trên toàn repo; sửa tại chỗ theo danh sách ở [plan.md](./plan.md). Không sửa bản ghi lịch sử đã loại trừ ở 029 (mục cũ `QA_Debt.md`, `docs/dien-tap-chaos-engineering/ket-qua/`, `specs/002-gateway-bff-routing/`). File `06-dashboard-slo-van-hanh-hang-ngay.md` được đổi/tách thành hướng dẫn cho hai dashboard mới. **Tên file hướng dẫn Kibana mới (đổi tên 06, hay thêm 09) chưa chốt — hỏi ở `/speckit-tasks`.**

## Danh sách kiểm chứng bắt buộc (điểm dừng khi triển khai)

| Mã | Kiểm chứng | Sai thì |
|---|---|---|
| V1 | Điều khiển ES|QL kiểu "giá trị từ truy vấn" cho `?week_start`, export/import được | Dừng, hỏi |
| V2 | Lens ES|QL nhận `?week_start`, export/import được | Dừng, hỏi |
| V3 | Discover session ES|QL trên dashboard áp thanh thời gian; `@timestamp` của index alert đủ dùng | Dùng `?_tstart/?_tend`; không được thì dừng, hỏi |
| V4 | Log Error trong request thật có `trace_id` và correlation id tới Elasticsearch | Sửa `ServiceDefaults` (FR-019), test trước |
| V5 | Link APM `/app/apm/link-to/trace/{{value}}` mở trace thật từ dữ liệu `generic.otel`; định dạng URL export/import được | Dừng, hỏi |
| V6 | Có span Client thật, biết `server.address` từng đích | Dừng, hỏi |
| V7 | Panel Links export/import và trỏ đúng id | Dừng, hỏi |
| V8 | Collapsible section export/import | Dừng, hỏi |

## Câu hỏi sẽ hỏi người dùng ở `/speckit-tasks` (không tự đặt)

1. Tên folder Postman 30 và các folder con.
2. Tên chính xác (tiêu đề) của các panel mới và của section.
3. Tên 3 file drawio và tên file hướng dẫn Kibana (đổi `06`, hay thêm `09`).
4. `remaining_requests` âm hiển thị thế nào; làm tròn.
5. Có thêm test canh dashboard hay không (ngoài FR-015).
6. Id cố định (UUID) của hai dashboard — đề xuất tạo một lần.

## Kết quả xác minh (T004–T012), 2026-10-05, Kibana/Elasticsearch 9.4.4

| Mã | Kết quả | Bằng chứng |
|---|---|---|
| V4 | **ĐÚNG, không sửa `ServiceDefaults`** | Tạm dừng container `ecomerce-local-products-db-1` (vài chục giây, đã chạy lại, `Products.Api` `/health/ready` trở về 200) để `DefaultHealthCheckService` ghi log Error trong request. Truy vấn `FROM logs-generic.otel-default* WHERE severity_number >= 17`: 11/11 log Error có `trace_id` (trường gốc) và `attributes.CorrelationId`. Lưu ý: lần thử này tạo vài phản hồi 503 trên `Products.Api` (tính vào ngân sách tuần như lỗi thật). T010–T011 bỏ qua. |
| V1 | **ĐÚNG** | Điều khiển ES|QL `VALUES_FROM_QUERY` biến `?week_start` hiển thị, chọn được giá trị; danh sách tuần/giờ **không bị thanh thời gian cắt** (thanh 20 phút vẫn liệt kê cả giờ cũ hơn 20 phút). Giá trị hiển thị theo múi giờ trình duyệt (`…+07:00`) nhưng `TO_DATETIME(?week_start)` nhận cả dạng `Z` và dạng offset. |
| V2 | **ĐÚNG** | Lens ES|QL (`vis` loại `xy`, `data_source.type: esql`) và Discover session ES|QL cùng dùng `?week_start` và hiển thị đúng. |
| V3 | **ĐÚNG, kèm hệ quả bắt buộc** | Discover session ES|QL **tự bị thanh thời gian cắt thêm**: thanh 5 phút → mỗi service ~57 span kể cả panel đã có `WHERE @timestamp >= TO_DATETIME(?week_start)`; đặt `time_range` riêng cho panel (`now-30d` → `now`) thì ra ~512 span (cả tuần) và panel hiện nhãn "Last 30 days". **Hệ quả**: mọi panel của dashboard Ngân sách tuần PHẢI có `time_range` riêng đủ rộng để thoát thanh thời gian (xem D3 cập nhật bên dưới). Index `.alerts-stack.alerts-default` cũng bị thanh thời gian cắt như index thường. |
| V5 | **KHÔNG ĐẠT, người dùng đã chốt hướng khác** | `GET /app/apm/link-to/trace/<trace_id>` chuyển sang `/app/apm/traces` hiện "Add data": APM không đọc được dữ liệu `generic.otel` thô. **Người dùng chốt**: link mở **Discover** lọc theo `trace_id` (không APM). |
| V6 | **ĐÚNG** | Sau khi chạy folder Postman `00 - Xác thực` + `00 - Smoke Flow`: có span `Client` thật, `attributes.server.address` = tên service Compose: `identity-api`, `bff-api`, `products-api`, `baskets-api` (cổng 8080). Có `status.code` lỗi ở `Gateway.Api → bff-api` và `Bff.Api → products-api`. Ánh xạ đích: `<tên>-api` → `<Tên>.Api` (ví dụ `orders-api` → `Orders.Api`). Có thêm span Client gọi `identity-api` (lấy cấu hình OIDC) — là lời gọi hạ tầng chứ không phải service nghiệp vụ; panel hạ lưu vẫn tính chúng như mọi cặp. |
| V7 | **ĐÚNG, qua saved object** | Dashboards API không có loại panel `links`. Tạo saved object `links` qua Saved Objects API và thêm panel `type: links` (`panelRefName`) vào `panelsJSON` của dashboard + tham chiếu `links`: Kibana hiển thị và bấm được, chuyển tới dashboard đích kèm khoảng thời gian. |
| V8 | **ĐÚNG** | Dashboards API hỗ trợ section (`{title, collapsed:false, grid:{y}, panels:[…]}`, không có `type`); lưu ở thuộc tính `sections` và `gridData.sectionId`, hiển thị đúng. |

### Cập nhật quyết định sau khi kiểm chứng

- **D1 (cách dựng)**: dựng dashboard bằng **Dashboards REST API của Kibana 9.4.4** (`PUT /api/dashboards/{id}` cho phép đặt id; panel `vis` = Lens cấu hình đơn giản, `discover_session` (ES|QL hoặc cổ điển, nhúng sẵn hoặc `ref_id`), `markdown`, section, `pinned_panels` = điều khiển ES|QL), thay vì thao tác giao diện. Ưu điểm: lặp lại được, không phụ thuộc thao tác tay. Riêng panel Links: bổ sung qua Saved Objects API (V7). Bản export cuối vẫn là Saved Objects Export API.
- **D3 (bộ chọn tuần)**: giữ danh sách tuần lấy từ dữ liệu. **Thêm**: mọi panel của dashboard Ngân sách tuần đặt `time_range` riêng rộng để không bị thanh thời gian cắt. Độ rộng này chưa chốt (hỏi người dùng khi dựng): quyết định tuần xa nhất chọn được.
- **D6 (log lỗi)**: `trace_id` có định dạng URL trỏ **Discover** (data view traces, lọc `trace_id:"{{value}}"`), không trỏ APM.
- **D8 (hạ lưu)**: đích suy ra bằng biểu thức từ `server.address` (`<tên>-api` → `<Tên>.Api`), không cần bảng `CASE`.

### Cập nhật quyết định lúc triển khai (sau V1–V8)

- **D3 (bộ chọn tuần), thay đổi**: điều khiển ES|QL **tĩnh** (`STATIC_VALUES`) biến `?tuan_chon` với 4 giá trị tương đối `Tuần này` (mặc định) / `Tuần trước` / `2 tuần trước` / `3 tuần trước`, thay cho danh sách tuần lấy từ dữ liệu: điều khiển lưu sẵn một giá trị mặc định cố định, nên danh sách theo dữ liệu sẽ mặc định vào một tuần cũ khi sang tuần mới. Mỗi truy vấn tính `week_start = CASE(?tuan_chon == "Tuần này", t0, ?tuan_chon == "Tuần trước", t0 - 7 days, …)` với `t0 = DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours` (ES|QL không có `DATE_ADD` trên 9.4.4). Người dùng đồng ý. Cửa sổ thời gian cố định của mỗi panel: 30 ngày (người dùng chốt) nên tuần xa nhất là 3 tuần trước.
- **D4, bổ sung**: tiêu hao lũy kế = mức cao nhất trong 4 ngân sách, mỗi service một đường (người dùng chốt); chỉ ngày đã tới (`WHERE day <= NOW()`).
- **D8, bổ sung**: panel hạ lưu hiển thị mọi cặp (gọi → đích) có span Client, xếp theo % xấu giảm dần, 20 dòng; cột `spans`, `errors`, `slow`, `bad_pct`, `p95_ms`.
- **D9, thay đổi**: link giữa hai dashboard là **ô Markdown theo id cố định**, không dùng panel Links: panel Links tham chiếu dashboard đích bằng saved object nên hai dashboard tham chiếu nhau, mỗi lần export sâu mỗi file chứa cả hai dashboard; link ngoài theo đường dẫn tương đối bị Kibana vô hiệu (`externalLink --error`). Người dùng chốt.
- **D6, kiểm chứng**: panel log lỗi là Discover session cổ điển trên index-pattern `logs-generic-otel-default`; cột `body.text` (nội dung log), `trace_id` (link Discover), `attributes.CorrelationId`.
