# Research: Loại span health khỏi công thức ngân sách lỗi

**Ngày**: 2026-10-08 | **Spec**: [spec.md](./spec.md) | **Stack kiểm tra**: Elasticsearch/Kibana 9.4.4 local đang chạy (chỉ đọc; không tạo/sửa object nào)

Quy ước: **Đã kiểm chứng** = chạy thật lúc lập plan bằng `POST /_query` trên dữ liệu thật hoặc `ROW` tổng hợp. **Chưa kiểm chứng** = giả định, bắt buộc có task kiểm chứng ở [quickstart.md](./quickstart.md); sai thì **dừng và hỏi người dùng** (không tự lùi).

## Dữ kiện đo được

| # | Dữ kiện | Kết quả |
|---|---|---|
| F1 | Dữ liệu hiện có | 6804 span, **100% là `kind: Server`**, **100% có `attributes.url.path` bắt đầu bằng `/health`** (`curl/8.5.0`, health check của Docker). Chưa có span nghiệp vụ nào nên chưa thử được nhánh "không phải health" trên dữ liệu thật. |
| F2 | Trường đường dẫn của span Server | `attributes.url.path` (ví dụ `/health/ready`), `attributes.http.route`, `name` = `GET /health/ready`. Span `Client` **không có** `attributes.url.path` (có `attributes.url.full`). |
| F3 | Logic ba giá trị của ES|QL (`ROW`, tổng hợp) | `WHERE NOT (p LIKE "/health*")` với `p = null` → **0 dòng** (dòng bị mất). `WHERE NOT (COALESCE(p, "") LIKE "/health*")` với `p = null` → **1 dòng** (giữ). Hệ quả: điều kiện loại **bắt buộc** dùng `COALESCE`, nếu không sẽ loại luôn các span không có trường đường dẫn (span `Client`, và mọi span chưa có `url.path`). |
| F4 | `LIKE` và tiền tố | `"/healthz"` khớp `"/health*"`; `"/orders/health"` **không** khớp; `"/HEALTH/x"` **không** khớp (phân biệt hoa thường). Quy tắc "tiền tố `/health`" của spec đúng ngữ nghĩa `LIKE "/health*"`. |
| F5 | Truy vấn chỉ báo health lỗi | `FROM traces… \| WHERE @timestamp > NOW() - 5 minutes AND COALESCE(attributes.url.path, "") LIKE "/health*" \| EVAL service…, is_5xx… \| STATS health_spans, health_5xx BY service \| EVAL health_5xx_pct` chạy được: 7 service, ~54 span/5 phút/service, 0% 5xx. |
| F6 | Cơ chế tiêm lỗi | `ChaosFaultInjectionMiddleware` (`shared/ServiceDefaults`) và `ChaosLatencyInjectionMiddleware` (`Orders.Api`) **không kiểm tra đường dẫn**, chỉ kiểm tra cờ môi trường và header; chạy trước xác thực. Nên header tiêm lỗi có tác dụng trên mọi đường dẫn. |
| F7 | Script diễn tập | `scripts/incident-drill.ps1` đã gửi tải và header tiêm lỗi tới **route nghiệp vụ** (`/bff/products`, `/orders/{guid}`, …); chỉ chờ container sẵn sàng bằng `GET /health/ready`. **Không cần sửa** (đính chính giả định lúc specify). |
| F8 | Postman đang tiêm vào health | 26 request: folder 25 (8: 1 ở 25a, 7 ở 25b), folder 27 (3: 1 ở 27a, 2 ở 27b), 29a (7), 30a (8). Chúng gửi `X-Chaos-Fault`/`X-Chaos-Latency-Ms` tới `…/health/live`. |
| F9 | Khối `error-budget-policy` hiện có | Mô hình YAML test (`ServiceManifestModel.cs`): `window`, `timezone`, `budgets`, `alert-thresholds`, `exhausted-when`, `on-exhausted`, `recovery`; chưa có khoá loại đường dẫn. |
| F10 | Tên span trên các rule | Mọi truy vấn rule 027/028 và panel ES|QL lọc từ cùng `FROM traces-generic.otel-default*` và đếm `COUNT(*)` mọi span (cả Server và Client). Rule frozen còn `FROM traces…, slo-error-budget-events` (sự kiện không có `url.path`). |

## Quyết định

### D1 — Điều kiện loại trong ES|QL
- **Quyết định**: thêm một dòng `| WHERE NOT (COALESCE(attributes.url.path, "") LIKE "/health*")` ngay sau `FROM` (và sau điều kiện thời gian nếu có) trong 4 rule 027, rule 028 và mọi truy vấn ES|QL của hai dashboard tính từ traces. Tiền tố lấy từ manifest (`excluded-path-prefixes`); nhiều tiền tố nối bằng `AND NOT (… LIKE "<p>*")`.
- **Rule frozen**: dùng cùng dòng đó; vì sự kiện `slo-error-budget-events` không có `url.path`, `COALESCE` cho chuỗi rỗng nên sự kiện **luôn được giữ** (F3). Không cần tách nhánh.
- **Lý do**: F3 chứng minh bắt buộc `COALESCE`; F4 chứng minh `LIKE "/health*"` đúng ngữ nghĩa tiền tố.
- **Phương án đã loại**: `STARTS_WITH(attributes.url.path, "/health")` (cũng mất dòng khi null, cần cùng `COALESCE`, không rõ hơn); lọc theo `name` (phân biệt phương thức, spec chọn theo tiền tố đường dẫn).

### D2 — Panel Lens của dashboard Xử lý sự cố
- **Quyết định**: 6 panel Lens đọc traces (Bảng SLO, 5xx theo phút, p95 theo phút, traffic + 401/403 theo phút, phân bố status code, top endpoint chậm nhất) thêm `query` KQL cấp panel `not attributes.url.path : /health*` (cú pháp chính xác chốt khi kiểm chứng). Panel `dotnet.exceptions` (metrics), lỗi gọi hạ lưu (span Client), log lỗi và Phát hiện nhanh (đọc alert) giữ nguyên.
- **Chưa kiểm chứng (V1)**: Dashboards API chấp nhận `query` trên mọi panel `vis` (đã thấy ở panel "Tổng 401 + 403" cũ); KQL `not … : /health*` loại đúng span health và **giữ** span không có trường `attributes.url.path` (NOT trong KQL giữ tài liệu thiếu trường); kết hợp được với KQL đã có trong công thức. Không chạy được thì **dừng và hỏi**.

### D3 — Dashboard Ngân sách tuần
- **Quyết định**: thêm đúng dòng D1 vào truy vấn của mọi panel tính từ traces: `slo-error-budget-consumption` (saved search), "Hạn mức còn lại", error-rate theo ngày, p95 theo ngày, tiêu hao lũy kế. Hai panel cảnh báo/cạn đọc alert nên không đổi. Công thức giữ khớp rule mốc.
- **Service chỉ có health**: sau D1 truy vấn không có dòng cho service đó → bảng không hiện dòng; panel có thể trống (người dùng chốt).

### D4 — Khoá manifest `excluded-path-prefixes`
- **Quyết định**: thêm vào khối `error-budget-policy` của cả 7 manifest: `excluded-path-prefixes: [/health]`, đặt ngang hàng `window`/`timezone`. Mô hình test thêm `ExcludedPathPrefixes` (`List<string>?`, alias `excluded-path-prefixes`). `ErrorBudgetPolicyTests` bắt buộc cả 7 giống hệt nhau và bằng danh sách mong đợi. Contract manifest 029 cập nhật **trước** khi sửa manifest/test (Nguyên tắc II).
- **Test đối chiếu rule**: `ErrorBudgetRuleDefinitionTests` (4 rule) và `IncidentFastDetectionRuleDefinitionTests` đọc điều kiện loại trong ES|QL của rule và so với `excluded-path-prefixes` của manifest (một tiền tố → một dòng `NOT (COALESCE(…) LIKE "<p>*")`).
- **Lý do**: người dùng chốt khai báo ở manifest (không hiến chương); test canh lệch giống cách ngưỡng độ trễ đang được canh.

### D5 — Chỉ báo health lỗi: panel và rule mới
- **Rule**: `.es-query` ES|QL mới, cửa sổ 5 phút, `groupBy: row`, kết quả chỉ giữ cột `service` (ràng buộc alert của 027/028: cột số làm mã alert đổi). Truy vấn theo F5, thêm `| WHERE health_5xx_pct >= 50 | KEEP service`. Chỉ tính 5xx (không tính chậm). Tên rule, tag, chu kỳ chạy, tên file export: **chưa được người dùng chốt, hỏi ở `/speckit-tasks`**; đề xuất để hỏi: chu kỳ 5 phút như các rule hiện có; tag riêng (không dùng `slo-error-budget` để panel/rule ngân sách không nhặt nhầm).
- **Panel**: Discover session ES|QL nhúng sẵn trong dashboard Xử lý sự cố, theo thanh thời gian, mỗi service một dòng: `health_spans`, `health_5xx`, `health_5xx_pct`, sắp theo tỷ lệ giảm dần; tiền tố health lấy từ manifest. Tên panel hỏi ở tasks.
- **Test**: lớp mới (tên đề xuất `HealthFailureRuleDefinitionTests`) khoá: loại/chu kỳ/cửa sổ 5 phút/`groupBy`/tag; ngưỡng `>= 50`; điều kiện chỉ lấy tiền tố health (không đảo); chỉ giữ cột `service`; `thresholdComparator ">"` và `threshold [0]`.
- **Lưu ý khởi động**: Docker retry 20 lần khi khởi động; vài span 5xx đầu thường dưới 50% trong 5 phút nên không bắn; nếu quá nửa thì bắn (đúng ý "service không sẵn sàng kéo dài").

### D6 — Postman: đổi đường dẫn tiêm lỗi (26 request)
- **Quyết định**: giữ header và logic folder; chỉ đổi đường dẫn từ `/health/live` sang đường dẫn không phải health. F6 chứng minh middleware chạy trên mọi đường dẫn. Hệ quả cần xử lý: các request "cờ TẮT" (25a, 27a) đang kỳ vọng `200` từ `/health/live`; đường dẫn mới có thể trả `401`/`404` khi cờ tắt, nên **assertion cờ TẮT phải sửa theo** (kiểm chứng cụ thể trong task). Tên đường dẫn thay thế: **người dùng chưa chốt, hỏi ở `/speckit-tasks`** kèm các ứng viên đã kiểm chứng. Ứng viên: đường dẫn nghiệp vụ không cần token của từng service (kiểm chứng từng cái có phản hồi dự đoán được khi cờ tắt), hoặc một đường dẫn thăm dò cố định chỉ dùng cho tiêm lỗi.
- `scripts/incident-drill.ps1`: không đổi (F7).

### D7 — Tài liệu cần sửa tại chỗ
- Nơi nhắc tiêm lỗi vào `/health/live` hoặc mô tả công thức "mọi span": `docs/QA/025_*`, `docs/QA/027_*`, `docs/QA/029_*`, `docs/QA/030_*`, `docs/architecture/027_*`, `028_*`, `029_*`, `030_*`, `docs/development/025_*`, `docs/PO/027_*`–`030_*`, `docs/kibana-quan-sat-he-thong/06`, `07`, `08`, `12`, `alerts/README.md`, `dashboards/README.md`, `specs/025–030` (quickstart/research/tasks/contract liên quan), sơ đồ `025`/`027`/`028`/`029`/`030` nhắc `/health/live`, mô tả Postman. Danh sách chính xác dựng lại bằng LỆNH-TÌM ở tasks. Không sửa bản ghi lịch sử (mục cũ `QA_Debt.md`, `docs/dien-tap-chaos-engineering/ket-qua/`, `specs/002-gateway-bff-routing/`).

### D8 — Thứ tự làm và Nguyên tắc III
- Test mới/sửa viết **trước** khi sửa manifest/rule: vì rule và manifest hiện chưa có điều kiện/khoá loại, test đỏ **thật** (không cần đổi giá trị giả); ghi số test đỏ. Sau đó sửa contract → manifest → rule → export → dashboard.

## Danh sách kiểm chứng bắt buộc (điểm dừng khi triển khai)

| Mã | Kiểm chứng | Sai thì |
|---|---|---|
| V1 | Lens `query` KQL loại `/health*` (giữ span thiếu trường), kết hợp với KQL trong công thức; Dashboards API chấp nhận | Dừng, hỏi |
| V2 | ES|QL có điều kiện loại (D1) chạy đúng cho từng rule và panel trên dữ liệu có cả span nghiệp vụ lẫn health (sinh bằng request nghiệp vụ thật) | Dừng, hỏi |
| V3 | Rule frozen giữ sự kiện `slo-error-budget-events` khi thêm điều kiện loại | Dừng, hỏi |
| V4 | Rule health lỗi bắn đúng khi ≥ 50% span health trả 5xx (tạm dừng DB của một service), không bắn khi < 50% hoặc health chỉ chậm; không rule ngân sách nào bắn | Dừng, hỏi |
| V5 | Đường dẫn thay thế tiêm lỗi: có 500 khi cờ bật, và phản hồi cờ tắt dự đoán được (để sửa assertion 25a/27a) | Dừng, hỏi |

## Câu hỏi sẽ hỏi người dùng ở `/speckit-tasks` (không tự đặt)

1. Tên rule cảnh báo health lỗi, tag, chu kỳ chạy, tên file export.
2. Tên panel chỉ báo health lỗi và vị trí trên dashboard Xử lý sự cố.
3. Đường dẫn không phải health dùng để tiêm lỗi/độ trễ; cách sửa assertion "cờ TẮT" ở 25a/27a.
4. Tên file PO/QA/Architect, 3 drawio, folder Postman 33 và nội dung folder.

## Kết quả xác minh (T004–T008), 2026-10-08, Elasticsearch/Kibana 9.4.4

| Mã | Kết quả | Bằng chứng |
|---|---|---|
| V2 | **ĐÚNG** | Sau khi tạo request nghiệp vụ thật (token + 16 lần gọi gateway `/bff/products` và `/bff/basket`), cửa sổ 15 phút: tổng 1320 span = 1212 health + 108 không phải health (64 Server + 44 Client). Có `COALESCE`: giữ đủ 108 (kể cả 44 span Client không có `url.path`). **Không có `COALESCE`** (`NOT (attributes.url.path LIKE …)`): chỉ còn 9–64 span Server, mất toàn bộ span Client. |
| V3 | **ĐÚNG** | Index tạm `tmp-033-events` với 1 sự kiện "cạn" của `Orders.Api`; ES|QL của `error-budget-frozen` (đổi tên index) có hoặc không có điều kiện loại ngay sau `FROM` cho cùng kết quả: `Orders.Api`, `exhausted_at` giữ nguyên. Đã xoá index tạm. |
| V1 | **ĐÚNG, kèm lưu ý cú pháp** | Dashboard thử `tmp-033-probe` (Dashboards API chấp nhận `query` trên `vis`): `not attributes.url.path : /health*` → 108 (đúng, giữ span Client); `… and kind : Server` → 64 (kết hợp được); `attributes.url.path : /health*` → 1207 (health). **KQL có dấu nháy `"/health*"` KHÔNG hoạt động** (= 1315, không loại gì vì không còn là wildcard). Dùng đúng dạng **không nháy** `not attributes.url.path : /health*`. Đã xoá dashboard thử. |
| V5 (không header) | **ĐÚNG** | Phản hồi của từng route khi header tiêm lỗi bị bỏ qua (tương đương cờ TẮT): `/bff/products` (gateway 5300, bff 5301), `/products` (5088), `/baskets/current` (5188), `/orders/{guid}` (5041), `/parties/{guid}` (5204) đều **401**; `/.well-known/openid-configuration` (identity 5205) **200**. Dùng để sửa assertion 25a/27a (orders → 401). Phần có header (cờ BẬT → 500/trễ) kiểm ở T047 sau khi hỏi người dùng. |

Phụ tác: lúc tạo request nghiệp vụ có 2 phản hồi `504` ở lần gọi nguội đầu tiên qua gateway (span 5xx nghiệp vụ thật, tính vào ngân sách theo công thức mới).
