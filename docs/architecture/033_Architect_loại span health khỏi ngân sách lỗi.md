# Kiến trúc: Loại span health khỏi công thức ngân sách lỗi

*Đối tượng đọc: kỹ sư/kiến trúc sư cần hiểu tại sao và cách loại span health khỏi ngân sách. Spec: [`specs/033-exclude-health-spans/`](../../specs/033-exclude-health-spans/spec.md).*

> **Cập nhật spec 034**: ngoài loại span health, rule và panel ngân sách nay chỉ đếm span `kind = Server` (không đếm Client/Producer). Xem [034 Architect](034_Architect_ngân%20sách%20lỗi%20chỉ%20đếm%20span%20Server.md).

Không đổi SLO, tỷ lệ cho phép, ngưỡng, mốc, cửa sổ hay chu kỳ ngân sách. Chỉ đổi **tập span được đo**: span có `attributes.url.path` bắt đầu bằng `/health` không tính.

## 1. Bằng chứng (F1–F10, đo thật 2026-10-08)

- F1: dữ liệu ban đầu 6804 span, 100% `kind: Server`, 100% là health check của Docker (`curl`, mỗi 5 giây ≈ 15,5 span/phút/service).
- F2: span Server có `attributes.url.path`; span `Client` **không có** (có `attributes.url.full`).
- F3: `NOT (p LIKE "/health*")` với `p = null` → **0 dòng**; `NOT (COALESCE(p, "") LIKE "/health*")` → 1 dòng. Nên điều kiện loại **bắt buộc** dùng `COALESCE`, nếu không sẽ mất luôn span Client và mọi span không có đường dẫn.
- F4: `LIKE "/health*"` khớp `/healthz`, không khớp `/orders/health` hay `/HEALTH/x` (phân biệt hoa thường).
- F6: `ChaosFaultInjectionMiddleware` và `ChaosLatencyInjectionMiddleware` không kiểm tra đường dẫn → tiêm lỗi vào route nghiệp vụ được.
- F7: `scripts/incident-drill.ps1` đã gửi tải tới route nghiệp vụ nên không đổi.
- F9/F10: trước đó mọi rule 027/028 và panel đếm `COUNT(*)` mọi span.

## 2. Thiết kế

| Thành phần | Thay đổi |
|---|---|
| Manifest 7 service | `error-budget-policy.excluded-path-prefixes: [/health]`; contract 029 cập nhật trước (bất biến 10) |
| Rule 027 ×4 và 028 | thêm `| WHERE NOT (COALESCE(attributes.url.path, "") LIKE "/health*")` trước mọi `EVAL`; frozen vẫn đọc `slo-error-budget-events` (sự kiện không có `url.path`, `COALESCE` giữ nguyên) |
| Dashboard Ngân sách tuần | cùng điều kiện trong mọi truy vấn ES|QL và saved search |
| Dashboard Xử lý sự cố | KQL cấp panel `not attributes.url.path : /health*` (**không** đặt dấu nháy: có nháy thì `*` thành chuỗi chữ) cho 6 panel Lens đọc traces |
| Rule mới `health-failure` | `.es-query` ES|QL, 5 phút, `groupBy row`, `health_5xx_pct >= 50`, chỉ cột `service`, không action; export `health-failure-rule.ndjson` |
| Panel mới | `Health lỗi theo service` trong section "Tình trạng SLO", cạnh Phát hiện nhanh |
| Postman | 26 request đổi sang route nghiệp vụ; assertion cờ TẮT 200→401 (route cần token); folder `33 - Health: loại khỏi ngân sách` |

## 3. Quyết định (D1–D8)

- D1 điều kiện loại dùng `COALESCE` (F3); tiền tố lấy từ manifest, nhiều tiền tố nối bằng `AND NOT`.
- D2 Lens dùng KQL cấp panel; D3 dashboard tuần cùng dòng loại; D4 khoá manifest + test đối chiếu rule ↔ manifest.
- D5 chỉ báo health lỗi tách khỏi ngân sách: chỉ 5xx, không tính chậm.
- D6 Postman đổi đường dẫn tiêm; D7 sửa tài liệu tại chỗ; D8 test viết trước và chạy đỏ thật.

## 4. Kết quả xác minh (V1–V5)

V1 (KQL Lens), V2 (ES|QL loại đúng, `COALESCE` giữ 44 span Client), V3 (frozen giữ sự kiện), V5 (không header: các route nghiệp vụ trả 401, identity 200) đều **đúng** — xem [research.md](../../specs/033-exclude-health-spans/research.md). V4 (rule `health-failure` bắn khi dừng DB) và các số đo thật: [QA_Debt.md](../QA/QA_Debt.md) mục 033.

## 5. Giới hạn

Xem [technical-debt.md](technical-debt.md) mục 033: health chậm không báo ở đâu; tiền tố `/health` loại cả endpoint nghiệp vụ trùng tiền tố; `LIKE` phân biệt hoa thường; phụ thuộc `attributes.url.path`; mẫu số nghiệp vụ nhỏ làm phần trăm vọt.

## Sơ đồ

- [Component](../diagrams/033-health-exclusion-component.drawio)
- [Luồng nghiệp vụ](../diagrams/033-health-exclusion-flow-nghiep-vu.drawio)
- [Sequence](../diagrams/033-health-exclusion-sequence.drawio)
