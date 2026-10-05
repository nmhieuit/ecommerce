# Research: Ngân sách lỗi theo tuần lịch giờ Việt Nam

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Mỗi quyết định dưới đây thuộc một trong hai loại:
- **Người dùng chốt**: chốt trực tiếp trong phiên `/speckit-specify` hoặc `/speckit-plan` ngày 2026-10-05 (xem spec.md mục Clarifications).
- **Hệ quả**: hệ quả kỹ thuật bắt buộc của các lựa chọn đó.

Không có quyết định nào do người lập kế hoạch tự chọn. Mục "Điểm phải xác minh" liệt kê những gì chưa thể khẳng định chỉ bằng đọc tài liệu.

## Hiện trạng đã kiểm tra (đọc code thật trên `695bccb`)

- **7 manifest**:
  - khối `slos` có `availability: 99.9%   # monthly` và `error-rate.max-5xx-ratio: 0.1%`;
  - khối `error-budget-policy` có `window: calendar-month`, `timezone: UTC+07:00`, `allowed-bad-ratio` lần lượt `0.1% / 0.1% / 5% / 1%`.
- **`docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson`**: 4 rule `.es-query` ES|QL, chu kỳ `5m`, tag `slo-error-budget`, cùng connector Index `slo-error-budget-events`.
  - Ba rule mốc: `WHERE @timestamp >= DATE_TRUNC(1 month, NOW() + 7 hours) - 7 hours`, `allowed = CASE(... 0.001, 0.001, 0.05, 0.01)`, `timeWindowSize: 31`, `timeWindowUnit: d`.
  - Rule `error-budget-frozen`: không có `WHERE @timestamp` (chỉ dựa vào cửa sổ rule), `timeWindowSize: 62`. Ngày "không đạt" khi `bad_5xx / spans >= 0.001`, hoặc `bad_p95 / spans > 0.05`, hoặc `bad_p99 / spans > 0.01`.
- **`alerts/incident-fast-detection-rule.ndjson` (028)**: `WHERE err_pct >= 0.1 OR latency_breach`, trong đó `err_pct` tính bằng phần trăm.
- **`dashboards/ngan-sach-loi-tuan.ndjson`**:
  - 3 Discover session `slo-error-budget-consumption` / `-active-alerts` / `-frozen`, tiêu đề chứa "tháng này";
  - 3 panel `eb027-*` có `time_range now-62d`;
  - panel text ghi "`99.9%`/tháng".
- **`tests/ServiceManifestSloConventionTests`**:
  - `PlatformSloDefaults.cs` lặp lại 99.9% / 0.1% của hiến chương, và `SloDefaultComplianceTests` bắt manifest lệch mặc định phải có `slos.justification`;
  - `ErrorBudgetPolicyTests` kiểm `calendar-month` và tỷ lệ;
  - `ErrorBudgetRuleDefinitionTests` kiểm chu kỳ, mốc, ngưỡng độ trễ, tỷ lệ, cột `KEEP`. Test này **không** kiểm ranh giới kỳ hay cửa sổ rule.
- **Hiến chương**: `1.0.0`, Ratified/Last Amended `2026-08-13`. Nguyên tắc VIII có "99.9% monthly availability." và "5xx responses below 0.1% of requests.".
- **Elastic**: volume đã bị người dùng xoá, nên không còn dữ liệu traces/sự kiện của chu kỳ tháng.

## Quyết định 1 — Ranh giới tuần lịch UTC+7 trong ES|QL

**Decision** (Hệ quả của FR-001/FR-006): ba rule mốc và Discover session mức tiêu hao lọc

```text
WHERE @timestamp >= DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours
```

Đây là đúng cách đã kiểm chứng cho tháng ở 027 (V3), chỉ đổi đơn vị `1 month` → `1 week`.

**Rationale**: Dịch sang giờ Việt Nam, làm tròn về đầu tuần rồi dịch ngược. Cách này giữ nguyên hình dạng truy vấn mà các test hiện có đang phân tích. Làm tròn tuần của Elasticsearch theo lịch ISO, nên tuần bắt đầu từ thứ Hai. Điểm này **phải xác minh** (V1), không được coi là đúng chỉ vì đọc tài liệu.

**Alternatives considered**: Tham số múi giờ của ES|QL hoặc truy vấn — chưa kiểm chứng trên 9.4.4 và sẽ làm lệch khỏi biểu thức 027 đã kiểm chứng. Chỉ dùng nếu V1 sai, và khi đó phải hỏi người dùng (xem bảng xác minh).

## Quyết định 2 — Con số mới của SLO và ngân sách

**Decision** (Người dùng chốt):

| Đối tượng | Cũ | Mới |
|---|---|---|
| Hiến chương — availability | `99.9% monthly availability.` | `99% weekly availability.` |
| Hiến chương — 5xx | `5xx responses below 0.1% of requests.` | `5xx responses below 1% of requests.` |
| Manifest `slos.availability` | `99.9%   # monthly` | `99%   # weekly` |
| Manifest `slos.error-rate.max-5xx-ratio` | `0.1%` | `1%` |
| `allowed-bad-ratio` khả dụng / 5xx | `0.1%` / `0.1%` | `1%` / `1%` |
| `allowed-bad-ratio` p95 / p99 | `5%` / `1%` | giữ nguyên |
| ES|QL `allowed` (3 rule mốc + Discover session) | `0.001, 0.001, 0.05, 0.01` | `0.01, 0.01, 0.05, 0.01` |
| Rule frozen — ngày không đạt (5xx) | `>= 0.001` | `>= 0.01` |
| Rule `incident-fast-detection` | `err_pct >= 0.1` | `err_pct >= 1` |
| `PlatformSloDefaults` (2 hồ sơ) | `99.9%`, `0.1%` | `99%`, `1%` |
| Ngưỡng độ trễ p95/p99 mọi service | — | giữ nguyên |

**Hệ quả**:
- Vì mặc định hiến chương đổi, 7 manifest vẫn tuân mặc định và không cần `slos.justification`; `SloDefaultComplianceTests` giữ logic, chỉ đổi giá trị mong đợi.
- Rule 028: loại C tiêm 5xx với tỷ lệ 5–50%, vẫn ≥ 1% nên vẫn bắn. Loại A/B (đích sai, cạn pool) gây 5xx ở mức hàng chục phần trăm theo QA_Debt 028, nên không bị ảnh hưởng.

## Quyết định 3 — Cửa sổ rule

**Decision** (Người dùng chốt):
- 3 rule mốc: `timeWindowSize: 7`, `timeWindowUnit: d`.
- `error-budget-frozen`: `timeWindowSize: 14`, `timeWindowUnit: d`.
- Cả 4 rule giữ chu kỳ `5m`.
- `incident-fast-detection` không đổi cửa sổ hay chu kỳ.

**Hệ quả**:
- Từ thứ Hai 00:00 tới Chủ nhật 23:59 giờ Việt Nam là 6 ngày 23 giờ 59 phút, nhỏ hơn 7 ngày, nên cửa sổ 7 ngày đủ phủ tuần hiện tại. Điểm này cần xác minh (V2), vì Kibana tự lọc `@timestamp` theo cửa sổ rule (027, Hệ quả 1).
- Rule frozen chỉ thấy sự kiện "cạn" trong 14 ngày. Một service đóng băng và không đạt SLO liên tục quá 14 ngày có thể tự mất trạng thái đóng băng. Đây là giới hạn đã biết, ghi vào technical-debt (spec Edge Cases).

## Quyết định 4 — Dashboard: sửa tạm, không tách

**Decision** (Người dùng chốt):
- **3 Discover session**:
  - tiêu đề "tháng này" → "tuần này" (cả `title` của saved search và `embeddableConfig.title` của panel);
  - truy vấn mức tiêu hao dùng biểu thức Quyết định 1 và tỷ lệ Quyết định 2;
  - `time_range` cả 3 panel → `now-7d`.
- **Panel text**: "`99.9%`/tháng" → "`99%`/tuần".
- **Phần còn lại**: id, vị trí, các panel khác và bố cục giữ nguyên (tách dashboard là việc của spec B).

**Hệ quả**:
- Bảng alert và bảng cạn vẫn giữ bộ lọc "alert có `@timestamp` trong 15 phút gần nhất" (QA_Debt 027). `now-7d` chỉ là giới hạn ngoài.
- Bảng cạn hiện alert của rule frozen, vốn đã tự nhìn lại 14 ngày, nên `now-7d` của panel không làm mất service đang đóng băng.

## Quyết định 5 — Tuần chuyển tiếp

**Decision** (Người dùng chốt): bắt đầu tính từ thứ Hai 00:00 giờ Việt Nam của tuần chứa ngày triển khai. Không chuyển đổi sự kiện hay trạng thái nào.

**Hệ quả**: biểu thức Quyết định 1 tự cho kết quả này, không cần ngày bắt đầu cố định. Elastic đã trống, và quickstart dựng stack mới trước khi tạo index sự kiện, nên không có sự kiện tháng nào còn sót. Việc dọn toàn bộ Elastic sau triển khai chỉ làm khi người dùng xác nhận lại (FR-018).

## Quyết định 6 — Test viết trước (Nguyên tắc III)

**Decision** (Người dùng chốt "sửa + thêm kiểm tra tuần"). Viết hoặc sửa test, chạy thấy **đỏ**, rồi mới sửa manifest/rule:

- **`PlatformSloDefaults.cs`**: hai hồ sơ đổi sang `99%` / `1%`. Hệ quả: `SloDefaultComplianceTests` đỏ cho 7 manifest cho tới khi `slos` được sửa.
- **`ErrorBudgetPolicyTests`**: mong đợi `window = calendar-week`, tỷ lệ khả dụng/5xx `1%`; đổi tên test `EveryService_UsesACalendarMonthInVietnamTime` theo tuần.
- **`ErrorBudgetRuleDefinitionTests`**:
  - `AllowedBadRatios` đổi sang `0.01 / 0.01 / 0.05 / 0.01`;
  - **thêm** 3 kiểm tra mới, chi tiết ở [contracts/error-budget-alert-rules-contract.md](./contracts/error-budget-alert-rules-contract.md):
    - (a) mỗi rule mốc có đúng dòng lọc đầu tuần của Quyết định 1;
    - (b) rule mốc có cửa sổ `7 d`, rule frozen có cửa sổ `14 d`;
    - (c) rule frozen dùng ngưỡng 5xx theo ngày `0.01`, và ngưỡng p95/p99 theo ngày `0.05` / `0.01` khớp tỷ lệ ngân sách.
- **Rule 028**: không thêm test (người dùng chọn không). Ngưỡng `incident-fast-detection` chỉ được kiểm bằng quickstart.

Hành vi trên Kibana thật được xác thực bằng [quickstart.md](./quickstart.md), không chặn PR, giống 027.

## Quyết định 7 — Hiến chương

**Decision** (Người dùng chốt):
- Sửa trong spec này. MAJOR `1.0.0 → 2.0.0`, `Last Amended: 2026-10-05` (giữ `Ratified: 2026-08-13`).
- Sync Impact Report ở đầu file ghi:
  - hai dòng đổi trong Nguyên tắc VIII;
  - lý do MAJOR: định nghĩa lại mặc định nền tảng theo cách không tương thích ngược;
  - tác động chuyển đổi: 7 manifest, test mặc định của 021, 4 rule 027, rule 028, dashboard;
  - danh sách template đã rà.

**Hệ quả**: Governance yêu cầu "approval from the platform maintainers". Người dùng là người duy trì duy nhất và sẽ tự commit, mở PR; plan không tự phê duyệt thay. Cần rà `.specify/templates/*.md` và file hướng dẫn agent xem có lặp lại "99.9%"/"monthly" không.

## Quyết định 8 — Kịch bản đốt ngân sách tuần chạy trên cả 7 service

**Decision** (Người dùng chốt): quickstart đốt ngân sách 5xx tuần của **cả 7 service** bằng folder Postman 029 (mỗi service một request `X-Chaos-Fault: 5xx`, như folder 027), chạy nhiều vòng bằng newman.

**Hệ quả**:
- Với tỷ lệ 1%, ngân sách cạn khi số 5xx ≈ `N / 99`, trong đó `N` là tổng span của service trong tuần.
- Lưu lượng thấp có thể làm nhiều mốc bắn cùng một chu kỳ (giới hạn đã chốt "chỉ ghi lại").
- Sau kịch bản, cả 7 service vào trạng thái cạn/đóng băng cho tới khi dọn Elastic. Đây là điều đã được chấp nhận khi chọn phương án.

## Quyết định 9 — Kiểm chứng ranh giới tuần

**Decision** (Người dùng chốt): trong phiên triển khai, chạy biểu thức đầu tuần với các thời điểm giả định cố định trong Discover (ES|QL), thay `NOW()` bằng hằng thời gian:

| Thời điểm giả định (UTC) | Giờ Việt Nam | Đầu tuần mong đợi (UTC) |
|---|---|---|
| `2026-10-04T16:59:00Z` | CN 04/10 23:59 | `2026-09-27T17:00:00Z` |
| `2026-10-04T17:00:00Z` | T2 05/10 00:00 | `2026-10-04T17:00:00Z` |
| `2026-10-08T10:00:00Z` | T5 08/10 17:00 | `2026-10-04T17:00:00Z` |
| `2026-10-11T16:59:00Z` | CN 11/10 23:59 | `2026-10-04T17:00:00Z` |

Ranh giới thật (00:00 thứ Hai 12/10 giờ Việt Nam) ghi là "chưa quan sát được trong một phiên", như cách 027 ghi "Kiểm tra theo thời gian".

## Quyết định 10 — Phạm vi sửa tài liệu

**Decision** (Người dùng chốt): sửa tại chỗ toàn bộ hiện vật và tài liệu đang dùng của 021, 027, 028. Không sửa:
- mục cũ trong `docs/QA/QA_Debt.md` (luôn giữ nguyên);
- `docs/dien-tap-chaos-engineering/ket-qua/`;
- `specs/002-gateway-bff-routing/`.

**Bằng chứng đo thật theo chính sách tháng/0.1%** (Người dùng chốt "xoá bằng chứng cũ", phạm vi "vận hành + spec 027/028"):
- **Xoá**:
  - các đoạn kết quả đo cũ trong tài liệu vận hành (`07`, `08`, `alerts/README.md`), ví dụ bảng diễn tập "1 lỗi = 80,39%" của file `07`;
  - trong spec 027/028: mục "Kết quả xác minh" của research.md, các ghi chú "**Kết quả 2026-10-0x**" trong tasks.md, các đoạn kết quả trong quickstart.md.
- **Giữ ở chỗ khác**: những bằng chứng này chỉ còn trong QA_Debt và lịch sử git.
- **Không thuộc phạm vi xoá** (chỉ sửa con số/chu kỳ): PO/QA/Architect 027/028, `functional-debt.md`, `technical-debt.md`.

**Hệ quả**: 027 research có những hệ quả kỹ thuật vẫn đang đúng, mà các rule hiện tại dựa vào. Khi xoá mục "Kết quả xác minh", những hệ quả này PHẢI được giữ lại dưới dạng quyết định hoặc ràng buộc, không còn là bằng chứng đo. Cụ thể:
- Hệ quả 1: cửa sổ rule tự lọc `@timestamp`;
- Hệ quả 2: mã alert = mọi cột, nên chỉ `KEEP` cột định danh;
- Hệ quả 3: cột định danh lấy từ `STATS` cuối;
- `COALESCE` tên field service;
- mapping `duration` tính bằng ns.

Nếu không giữ, test `ThresholdRule_ReturnsOnlyTheAlertIdentityColumns` sẽ mất căn cứ.

Bộ tài liệu mới của 029 theo FR-017. Danh sách file cần sửa lấy từ lệnh tìm toàn repo ở plan.md, mục "Phạm vi sửa tài liệu".

## Điểm phải xác minh ngay ở task đầu tiên của giai đoạn triển khai

| # | Điểm | Nếu không đúng |
|---|---|---|
| V1 | `DATE_TRUNC(1 week, <t> + 7 hours) - 7 hours` cho đúng thứ Hai 00:00 giờ Việt Nam với 4 thời điểm ở Quyết định 9 (tức làm tròn tuần bắt đầu thứ Hai). | **Dừng, hỏi người dùng** chọn cách khác (vd cộng/trừ ngày theo `DATE_EXTRACT("day_of_week", ...)`), không tự đổi. |
| V2 | Rule `.es-query` với `timeWindowSize: 7 d` vẫn thấy span từ thứ Hai 00:00 giờ Việt Nam khi chạy gần cuối Chủ nhật, tức Kibana không cắt hụt đầu tuần. | Dừng, hỏi người dùng (spec Assumptions: không tự nới cửa sổ). |
| V3 | Sửa truy vấn của rule `error-budget-100` trên stack sạch không ghi sự kiện "cạn" giả (QA_Debt 027: sửa rule làm Kibana đặt lại trạng thái alert). | Ghi phát hiện vào QA_Debt 029. Vì Elastic sẽ được dọn, chỉ cần không sửa rule sau khi đã có service cạn. |

## Kết quả xác minh (T004–T005) — 2026-10-05, Kibana/Elasticsearch 9.4.4, stack `ecomerce-local` đang chạy

Người dùng chốt dùng stack đang chạy thay vì dựng stack mới. Stack này dựng từ repo chính và dùng volume `ecomerce-local_local-es-data`, có dữ liệu traces từ `2026-10-03T11:02Z`. Index `slo-error-budget-events` được xoá rồi tạo lại sau bước này (FR-011). `GET /api/alerting/_health` → `has_permanent_encryption_key: true`.

| # | Kết quả | Bằng chứng |
|---|---|---|
| V1 | **ĐÚNG.** `DATE_TRUNC(1 week, <t> + 7 hours) - 7 hours` cho đúng thứ Hai 00:00 giờ Việt Nam: làm tròn tuần của ES\|QL bắt đầu từ thứ Hai. | Chạy qua `POST /_query` của Elasticsearch (cùng engine ES\|QL với Discover), `ROW t = TO_DATETIME(...)`: `2026-10-04T16:59Z → 2026-09-27T17:00Z`; `2026-10-04T17:00Z → 2026-10-04T17:00Z`; `2026-10-08T10:00Z → 2026-10-04T17:00Z`; `2026-10-11T16:59Z → 2026-10-04T17:00Z`. Với `NOW()` lúc `2026-10-05T08:20Z` → `2026-10-04T17:00Z`. Cả 4 hàng khớp bảng Quyết định 9. |
| V2 | **ĐÚNG, có giới hạn.** Bộ lọc thời gian mà Kibana tự áp tỉ lệ đúng với `timeWindowSize`. Cửa sổ 7 ngày phủ ít nhất 2 ngày về trước, xa hơn đầu tuần hiện tại (khoảng 15 giờ trước lúc đo). | Hai rule tạm `.es-query`, cùng truy vấn `FROM traces-generic.otel-default* \| STATS earliest = MIN(@timestamp) ...` (không có `WHERE`), `groupBy: row`. Cửa sổ `7 d` → alert `2026-10-03T11:02:47Z` (span cũ nhất có trong index). Cửa sổ `1 d` → alert `2026-10-05T04:03:05Z`. Đã xoá cả hai rule. **Giới hạn**: index chỉ có dữ liệu từ 03/10, nên không quan sát trực tiếp được việc cửa sổ 7 ngày phủ trọn 6 ngày 23 giờ 59 phút (trường hợp tối Chủ nhật). Kết luận dựa trên việc bộ lọc tỉ lệ với cửa sổ, cộng ràng buộc kỹ thuật "cửa sổ rule tự lọc `@timestamp`" của 027. Ghi vào QA_Debt 029. |
