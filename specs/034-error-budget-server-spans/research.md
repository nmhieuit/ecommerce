# Research: Ngân sách lỗi chỉ đếm span Server

**Ngày**: 2026-10-09 | **Spec**: [spec.md](./spec.md) | **Stack kiểm tra**: Elasticsearch 9.4.4 local đang chạy (chỉ đọc `POST /_query`; không tạo/sửa/xoá object nào)

Quy ước: **Đã kiểm chứng** = chạy thật lúc lập plan trên dữ liệu thật. **Chưa kiểm chứng** = giả định, bắt buộc có task kiểm chứng ở [quickstart.md](./quickstart.md); sai thì **dừng và hỏi người dùng** (không tự lùi).

## Dữ kiện đo được

| # | Dữ kiện | Kết quả |
|---|---|---|
| F1 | Phân bố loại span (toàn bộ index) | `Server` 16316, `Client` 321, `Producer` 21. **Không có span nào thiếu `kind`.** Giá trị viết hoa chữ đầu (`Server`). |
| F2 | Đường dẫn theo loại | 16316/16316 span Server có `attributes.url.path`; **0** span Client/Producer có trường này. |
| F3 | Tuần 05–11/10 (UTC+7), đã loại `/health*` | Mỗi (service, loại): `Bff.Api` Server 118 span / 9 lỗi 5xx, Client 201 / 12; `Gateway.Api` Server 115 / 5, Client 110 / 1; `Baskets.Api` Server 100 / 6, Client 2; `Products.Api` Server 90 / 16, Client 2; `Orders.Api` Server 55 / 4, Client 4, **Producer 21**; `Identity.Api` Server 38 / 2; `Parties.Api` Server 11 / 4, Client 2. Tổng hiện được tính: 869 span; chỉ Server: **527** span. Khớp mô tả (BFF 319 → 118, lỗi 21 → 9). |
| F4 | Nguồn sự kiện của rule frozen | `slo-error-budget-events` có 20 tài liệu và **không có cột `kind`**. `FROM traces…, slo-error-budget-events … \| WHERE kind == "Server"` loại **toàn bộ** sự kiện (0 dòng sự kiện, 16343 dòng span). `WHERE kind == "Server" OR _index LIKE "*slo-error-budget-events*"` giữ đủ 20 sự kiện. Cùng bản chất với F3 của 033 (điều kiện ba giá trị làm mất dòng). |
| F5 | Điều kiện kết hợp | `kind == "Server" AND NOT (COALESCE(attributes.url.path, "") LIKE "/health*")` chạy đúng: 527 span (khớp F3). |
| F6 | Hiện trạng 5 rule và truy vấn | 4 rule 027 (`error-budget-50/75/100`: `FROM traces…` + loại `/health*`; `error-budget-frozen`: `FROM traces…, slo-error-budget-events METADATA _index`) và `incident-fast-detection` chưa có điều kiện `kind`. Rule `health-failure` chỉ lọc `/health*` (đều là span Server). |
| F7 | Panel của dashboard Xử lý sự cố | 6 Lens đang có KQL `not attributes.url.path : /health*`: Bảng SLO, 5xx/phút, p95/phút, Traffic + 401/403, Phân bố status code, Top endpoint chậm nhất. Panel `Lỗi gọi hạ lưu — cặp service gọi → đích` đang dùng `kind` (Client) → giữ nguyên. Còn lại giữ nguyên: Phát hiện nhanh, Health lỗi theo service (033), `dotnet.exceptions`, Log lỗi, 2 markdown. |
| F8 | Panel của dashboard Ngân sách tuần | Saved search `slo-error-budget-consumption`, panel "Hạn mức còn lại", 3 vis (error-rate/p95/tiêu hao lũy kế) đọc `traces-generic.otel-default*` → đổi. Hai saved search cảnh báo/cạn đọc alert → giữ. |

## Quyết định

### D1 — Điều kiện chỉ đếm Server trong ES|QL
- **Quyết định**: thêm đúng một dòng `| WHERE kind == "Server"` ngay sau `FROM` (cùng chỗ điều kiện loại `/health*`) trong `error-budget-50/75/100`, `incident-fast-detection` và mọi truy vấn ES|QL tính từ traces của dashboard Ngân sách tuần. Điều kiện loại `/health*` của 033 giữ nguyên, không đổi thứ tự quy ước.
- **Ngoại lệ rule `error-budget-frozen`**: dùng `| WHERE (kind == "Server" OR _index LIKE "*slo-error-budget-events*")` để không làm mất sự kiện cạn (F4). Ngày chỉ có span không-Server vẫn là ngày không traffic và tính là đạt.
- **Lý do**: F1 chứng minh `kind` luôn có và là chuỗi `Server`; F4 chứng minh nếu không có ngoại lệ thì rule frozen mất mọi sự kiện và không bao giờ bắn.
- **Phương án đã loại**: `kind != "Client" AND kind != "Producer"` (bỏ sót loại span mới như `Consumer`/`Internal`, mâu thuẫn FR-001 "chỉ Server"); `attributes.url.path IS NOT NULL` (đoán gián tiếp, vỡ khi Server thiếu trường); lọc theo `name` (phụ thuộc cách đặt tên).

### D2 — Panel Lens của dashboard Xử lý sự cố
- **Quyết định**: 6 panel Lens ở F7 đổi KQL cấp panel thành `kind : Server and not attributes.url.path : /health*` (KQL không nháy, theo V1 của 033). Panel Lỗi gọi hạ lưu, Phát hiện nhanh, Health lỗi, `dotnet.exceptions`, Log lỗi giữ nguyên.
- **Chưa kiểm chứng (V1)**: biểu thức kết hợp chạy đúng trong Lens của Kibana 9.4.4 với KQL có sẵn trong công thức. 033 đã thấy `… and kind : Server` cho 64 span (đúng). Không chạy được thì **dừng và hỏi**.

### D3 — Dashboard Ngân sách tuần
- **Quyết định**: thêm dòng D1 vào truy vấn của `slo-error-budget-consumption`, "Hạn mức còn lại", error-rate theo ngày, p95 theo ngày, tiêu hao lũy kế (cùng chỗ điều kiện loại `/health*`). Công thức giữ khớp rule mốc. Hai panel cảnh báo/cạn không đổi.

### D4 — Không đổi manifest, hiến chương, tỷ lệ, ngưỡng
- **Quyết định tạm của plan**: định nghĩa "chỉ Server" nằm trong rule + panel + test, **không thêm khoá vào `error-budget-policy`** của 7 manifest. Lý do: spec không yêu cầu khai báo ở manifest (khác 033, nơi người dùng chọn khai báo); khoá thêm là phạm vi mới.
- **Phải hỏi lại ở `/speckit-tasks`** (câu 1 bên dưới): người dùng có muốn khai báo loại span được đếm trong manifest (ví dụ một khoá mới trong `error-budget-policy`) để có test đối chiếu manifest ↔ rule như `excluded-path-prefixes` không. Plan, test và contract được viết sao cho thêm khoá này sau cũng chỉ là một task nhỏ.

### D5 — Test canh gác (Nguyên tắc III)
- **Quyết định**: (a) `ErrorBudgetRuleDefinitionTests`: thêm một `[Theory]` cho `error-budget-50/75/100` kiểm đúng một dòng `WHERE kind == "Server"` đứng trước mọi phép tính, và một `[Fact]` cho `error-budget-frozen` kiểm điều kiện Server-hoặc-sự-kiện (bất biến 3 trong contract); (b) `IncidentFastDetectionRuleDefinitionTests`: thêm một `[Fact]` kiểm điều kiện Server (đã có lớp test từ 028/033, không phải tạo lớp mới như spec ghi "chưa có test"; rule 028 chỉ chưa có kiểm tra Server). Không test panel dashboard (người dùng chốt).
- **Đỏ thật**: 5 rule hiện chưa có dòng này nên test mới đỏ ngay mà không cần đổi giá trị giả; ghi số test đỏ trước khi sửa. Test hiện có của 027, 028, 033 phải vẫn xanh.
- **Điều chỉnh so với spec**: mục 3 của Clarifications nói "viết test mới cho `incident-fast-detection` (hiện chưa có test)". Thực tế đã có `IncidentFastDetectionRuleDefinitionTests` (8 test, kể cả loại `/health*` từ 033); nên "test mới" là thêm test vào lớp đó. Ý định của người dùng (có test Server cho rule này) giữ nguyên. Ghi lại ở spec khi triển khai.

### D6 — Dọn trạng thái sau triển khai (FR-008)
- **Quyết định**: sau khi import rule, xoá các sự kiện cạn trong `slo-error-budget-events` (hiện 20 tài liệu) và Disable rồi Enable `error-budget-100`; mỗi thao tác **hỏi lại người dùng ngay trước khi làm**. Trước khi xoá, chạy truy vấn đối chiếu để người dùng thấy sự kiện nào sẽ mất.
- **Chưa kiểm chứng (V3)**: cách xoá tài liệu theo `service`/`event` (xoá theo truy vấn hay xoá index và tạo lại) chọn ở bước tasks khi hỏi người dùng.

### D7 — Postman folder 34
- **Quyết định tạm**: folder kiểm chứng việc đếm theo span Server. Nội dung request, kỳ vọng và tên **chưa chốt, hỏi ở `/speckit-tasks`**. Dữ kiện đã có: header `X-Chaos-Fault: 5xx` chạy trên mọi đường dẫn nghiệp vụ (033 F6); route `/bff/products` đi Gateway → BFF → Products là đường tự nhiên để thấy BFF chỉ được cộng 1.

### D8 — Tài liệu sửa tại chỗ (FR-011)
- Nơi mô tả công thức ngân sách hoặc điều kiện loại `/health*`: `docs/kibana-quan-sat-he-thong/06`, `07`, `08`, `alerts/README.md`, `dashboards/README.md`; `docs/PO|QA|architecture/027–030, 033_*`; contract rule 027/028 và `specs/033-*/contracts/budget-exclusion-contract.md`; sơ đồ drawio 027–030, 033 nhắc công thức; mô tả collection Postman. Danh sách chính xác dựng bằng LỆNH-TÌM ở tasks, chạy lại ở task cuối (SC-006). **Không sửa**: mục cũ `docs/QA/QA_Debt.md`, `docs/dien-tap-chaos-engineering/ket-qua/`, `specs/002-gateway-bff-routing/`.
- Ghi nhận lệch Governance của PR #75 vào `technical-debt.md` (FR-010); **không sửa hiến chương**.

## Danh sách kiểm chứng bắt buộc (điểm dừng khi triển khai)

| Mã | Kiểm chứng | Sai thì |
|---|---|---|
| V1 | Lens: `kind : Server and not attributes.url.path : /health*` kết hợp đúng với KQL trong công thức; Dashboards API chấp nhận | Dừng, hỏi |
| V2 | Với tuần 05–11/10 sau khi sửa: mỗi (service, ngân sách) của 4 rule + `incident-fast-detection` và hai dashboard khớp truy vấn lọc Server (BFF 118 span / 9 lỗi; tổng 527) | Dừng, hỏi |
| V3 | Rule frozen sau khi sửa vẫn đọc đủ sự kiện (20) và vẫn bắn khi service đang đóng băng; bước dọn xoá đúng sự kiện đã báo | Dừng, hỏi |
| V4 | Hai dashboard và hai file rule import sạch bằng một lệnh mỗi file; panel Lỗi gọi hạ lưu vẫn hiện span Client | Dừng, hỏi |
| V5 | Folder Postman 34 (sau khi chốt nội dung): ngân sách mỗi service tăng đúng theo span Server, BFF không nhân đôi | Dừng, hỏi |

## Câu hỏi sẽ hỏi người dùng ở `/speckit-tasks` (không tự đặt)

1. Có khai báo loại span được đếm trong manifest (khoá mới ở `error-budget-policy`) để có test đối chiếu manifest ↔ rule không, hay giữ trong rule + test (D4)?
2. Cách dọn sự kiện cạn (xoá tài liệu theo truy vấn hay xoá/tạo lại index) và thời điểm thực hiện (D6).
3. Nội dung folder Postman 34: tên, số request, kỳ vọng (D7).
4. Tên file PO/QA/Architect, 3 drawio (dự kiến theo khuôn `docs/PO/034_PO_…`).

## Kết quả xác minh (T004–T006), 2026-10-09, Elasticsearch/Kibana 9.4.4

| Mã | Kết quả | Bằng chứng |
|---|---|---|
| V2 (số đối chiếu, trước sửa) | **ĐÚNG** | Tuần hiện tại (từ 05/10 00:00 UTC+7, dữ liệu 2026-10-08T13:59Z → 2026-10-09T03:13Z), đã loại `/health*`: `Bff.Api` Server 118 span / 9 lỗi 5xx, Client 201 / 12; `Gateway.Api` Server 115 / 5, Client 110 / 1; `Orders.Api` có thêm 21 span Producer; tổng chỉ-Server **527** span. Trùng F3. |
| V3 | **ĐÚNG** | ES|QL của `error-budget-frozen` với `WHERE (kind == "Server" OR _index LIKE "*slo-error-budget-events*")`: kết quả cuối giống bản cũ (7 service); kết quả trung gian `SUM(is_event)` = **20** (đủ sự kiện) và `SUM(is_span)` giảm 869 → 527. |
| V1 | **ĐÚNG** | Dashboard thử `tmp-034-probe` (bảng Lens, 7 ngày): không query → 2.4–2.7k span/service (cả health); KQL cũ `not attributes.url.path : /health*` → `Bff.Api` 319 / 21 lỗi, `Gateway.Api` 225 / 6; KQL mới `kind : Server and not attributes.url.path : /health*` → `Baskets.Api` 100/6, `Bff.Api` **118/9**, `Gateway.Api` 115/5, `Identity.Api` 38/2, `Orders.Api` 55/4, `Parties.Api` 11/4, `Products.Api` 90/16: khớp từng dòng với ES|QL chỉ-Server. Đã xoá dashboard thử. |
