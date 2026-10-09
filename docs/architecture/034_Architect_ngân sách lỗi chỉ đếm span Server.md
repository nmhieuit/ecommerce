# Kiến trúc: Ngân sách lỗi chỉ đếm span Server

*Đối tượng đọc: kỹ sư/kiến trúc sư cần hiểu tại sao và cách ngân sách lỗi chỉ đếm span Server. Spec: [`specs/034-error-budget-server-spans/`](../../specs/034-error-budget-server-spans/spec.md).*

Không đổi SLO, tỷ lệ cho phép, ngưỡng, mốc, cửa sổ, chu kỳ ngân sách, loại `/health*` (033) hay hiến chương. Chỉ đổi **tập span được đo**: chỉ span `kind = Server` (request mà chính service nhận); span `Client` (lời gọi hạ lưu) và `Producer` (publish thông điệp) không tính.

## 1. Bằng chứng (F1–F8, đo thật 2026-10-09)

- F1: `kind` toàn index: `Server` 16316, `Client` 321, `Producer` 21; không span nào thiếu `kind`.
- F2: 16316/16316 span Server có `attributes.url.path`; span Client/Producer không có.
- F3: tuần 05–11/10 (đã loại `/health*`): `Bff.Api` Server 118 span / 9 lỗi 5xx, Client 201 / 12; `Gateway.Api` Server 115 / 5, Client 110 / 1; `Orders.Api` thêm 21 span Producer. Tổng được tính: 869 → 527 (chỉ Server).
- F4: `slo-error-budget-events` không có cột `kind`: `WHERE kind == "Server"` trần loại **cả 20 sự kiện**; `WHERE (kind == "Server" OR _index LIKE "*slo-error-budget-events*")` giữ đủ.
- F5–F8: điều kiện kết hợp chạy đúng; rule hiện chưa có điều kiện `kind`; panel Lens/ES|QL cần đổi và giữ (xem bên dưới).
- Postman 34a (2026-10-09, `products-api` dừng): một request qua Gateway → BFF cho 1 span Server 5xx và 1 span Client 5xx ở Gateway, 1 span Server 5xx ở BFF và 3 span Client của BFF (kết nối bị từ chối, thử lại, không có mã trạng thái). Công thức cũ đếm 2 lỗi ở Gateway; mới đếm 1.

## 2. Thiết kế

| Thành phần | Thay đổi |
|---|---|
| Rule `error-budget-50/75/100`, `incident-fast-detection` | thêm `\| WHERE kind == "Server"` ngay sau điều kiện loại `/health*`, trước mọi `EVAL` |
| Rule `error-budget-frozen` | thêm `\| WHERE (kind == "Server" OR _index LIKE "*slo-error-budget-events*")` (giữ sự kiện cạn, F4) |
| Rule `health-failure` | không đổi (chỉ đọc `/health*`) |
| Dashboard Ngân sách tuần | 5 truy vấn ES\|QL (saved search tiêu hao, hạn mức còn lại, error-rate/p95 theo ngày, tiêu hao lũy kế) có cùng dòng Server |
| Dashboard Xử lý sự cố | KQL cấp panel `kind : Server and not attributes.url.path : /health*` cho 6 panel Lens; giữ `Lỗi gọi hạ lưu` (Client), Phát hiện nhanh, Health lỗi, `dotnet.exceptions`, Log lỗi |
| Test | `BudgetRule_CountsOnlyServerSpans_BeforeAnyCalculation`, `FrozenRule_CountsOnlyServerSpans_ButKeepsTheExhaustionEvents`, `Rule_CountsOnlyServerSpans_BeforeAnyCalculation` |
| Manifest | không đổi (định nghĩa nằm ở rule + panel + test) |
| Postman | folder `34 - Ngân sách: chỉ đếm span Server` (34a lỗi hạ lưu thật, 34b đối chiếu theo loại span) |

## 3. Quyết định (D1–D8)

- D1 `kind == "Server"` (không dùng `!= Client`: bỏ sót loại span mới); rule frozen có ngoại lệ sự kiện.
- D2 Lens dùng KQL `kind : Server and not attributes.url.path : /health*` (không nháy); D3 dashboard tuần cùng dòng với rule.
- D4 không thêm khoá vào manifest; D5 test đỏ trước (5 test mới), thêm vào lớp có sẵn của 028.
- D6 dọn sự kiện bằng delete-by-query giữ index; Disable/Enable rule 100, mỗi bước hỏi trước.
- D7 Postman: header `X-Chaos-Fault` trả 500 ngay ở service đầu nên không tạo được lỗi qua 3 tầng; 34a dùng lỗi hạ lưu thật bằng cách dừng `products-api`.
- D8 sửa tài liệu tại chỗ; ghi nhận lệch Governance của PR #75 vào nợ kỹ thuật, không sửa hiến chương.

## 4. Kết quả xác minh (V1–V5)

V1 (KQL Lens khớp ES|QL từng dòng: `Bff.Api` 118/9), V2 (28/28 mức tiêu hao của rule khớp phép tính độc lập chỉ-Server), V3 (frozen giữ 20 sự kiện), V4 (hai dashboard import sạch, panel hạ lưu không đổi), V5 (34b 14/14 xanh) đều **đúng** — xem [research.md](../../specs/034-error-budget-server-spans/research.md) và [QA_Debt.md](../QA/QA_Debt.md) mục 034.

## 5. Giới hạn

Xem [technical-debt.md](technical-debt.md) mục 034: span thiếu `kind` bị loại; loại span lạ (`Internal`, `Consumer`) không được đếm; lỗi hạ lưu bị service gọi nuốt không còn trừ ngân sách service gọi; mẫu số nhỏ ở tuần đầu vẫn làm phần trăm vọt; lệch Governance của PR #75.

## Sơ đồ

- [Component](../diagrams/034-server-span-only-component.drawio)
- [Luồng nghiệp vụ](../diagrams/034-server-span-only-flow-nghiep-vu.drawio)
- [Sequence](../diagrams/034-server-span-only-sequence.drawio)
