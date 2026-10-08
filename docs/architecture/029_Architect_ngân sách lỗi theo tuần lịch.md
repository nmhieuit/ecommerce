# Kiến trúc: Ngân sách lỗi theo tuần lịch giờ Việt Nam (thay thế tháng lịch của 027)

> **Cập nhật spec 033**: rule và panel ES|QL/Lens loại span `/health*` bằng `COALESCE(attributes.url.path, "")`; khoá `excluded-path-prefixes` trong manifest. Xem [033 Architect](033_Architect_loại%20span%20health%20khỏi%20ngân%20sách%20lỗi.md).

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: spec A trong đợt rà soát nợ kỹ thuật dashboard/tiêm lỗi, đặc tả tại
[`specs/029-error-budget-weekly/`](../../specs/029-error-budget-weekly/). 10 quyết định, 3 điểm xác minh
trên Kibana thật (V1–V3) ở [`research.md`](../../specs/029-error-budget-weekly/research.md). Không thêm
cơ chế mới: giữ nguyên rule, connector, index sự kiện, panel và công cụ tiêm lỗi của
[027](027_Architect_chính%20sách%20ngân%20sách%20lỗi%20và%20ngưỡng%20cảnh%20báo.md), chỉ đổi **chu kỳ**,
**con số** và **phạm vi dữ liệu**.

**Trạng thái xác minh**: test quy ước xanh (103/103); rule, dashboard, diễn tập đốt ngân sách 7 service
và ngưỡng 1% của rule phát hiện nhanh chạy trên Kibana/Elasticsearch 9.4.4 thật. Ranh giới thứ Hai
00:00 thật chưa quan sát được trong một phiên — xem [technical-debt.md](technical-debt.md).

## 1. Một con số, bốn nơi phải nói giống nhau

| Nơi | Giá trị sau 029 | Ai canh |
|---|---|---|
| Hiến chương Nguyên tắc VIII (`.specify/memory/constitution.md`, **2.0.0**) | "99% weekly availability." · "5xx responses below 1% of requests." | Người review PR (sửa đổi MAJOR) |
| `PlatformSloDefaults` (test 021) | `99%` / `1%` cho cả hai hồ sơ | `SloDefaultComplianceTests` |
| 7 `service-manifest.yaml` | `slos`: `99%   # weekly`, `max-5xx-ratio: 1%`; `error-budget-policy`: `window: calendar-week`, khả dụng/5xx `1%` | `ErrorBudgetPolicyTests` |
| Rule Kibana (export ndjson) | `allowed` `0.01 / 0.01 / 0.05 / 0.01`; ngày đạt SLO 5xx `< 0.01`; phát hiện nhanh `err_pct >= 1` | `ErrorBudgetRuleDefinitionTests` (trừ rule phát hiện nhanh — không có test) |

Tỷ lệ ngân sách vẫn bằng **1 − SLO**: người dùng chọn nới cả SLO lẫn ngân sách (×10 cho khả dụng/5xx)
thay vì chỉ nới ngân sách, để "cạn ngân sách" không xảy ra muộn hơn "vi phạm SLO". Ngưỡng độ trễ p95/p99
không đổi.

## 2. Ranh giới tuần trong ES|QL (Quyết định 1)

```esql
| WHERE @timestamp >= DATE_TRUNC(1 week, NOW() + 7 hours) - 7 hours
```

Cùng cách đã dùng cho tháng ở 027: dịch sang giờ Việt Nam, làm tròn, dịch ngược. Làm tròn tuần của ES|QL
bắt đầu từ **thứ Hai** — đã kiểm bằng 4 thời điểm quanh ranh giới (V1), ví dụ `2026-10-04T16:59Z` (Chủ
nhật 23:59 giờ VN) → `2026-09-27T17:00Z`, `2026-10-04T17:00Z` (thứ Hai 00:00 giờ VN) →
`2026-10-04T17:00Z`. Test `ThresholdRule_StartsAtMondayMidnightVietnamTime` canh đúng dòng này trong cả 3
rule mốc.

## 3. Cửa sổ rule: 7 ngày và 14 ngày (Quyết định 3)

Rule `.es-query` tự lọc `@timestamp` theo `timeWindowSize` **trước** khi chạy ES|QL (ràng buộc kế thừa từ
027). Vì vậy:

```
rule mốc 50/75/100 : cửa sổ 7 d  ─┬─ WHERE đầu tuần UTC+7 cắt lại đúng tuần hiện tại
                                  └─ tuần lịch dài tối đa 6 ngày 23 giờ 59 phút ⇒ 7 d đủ phủ
rule error-budget-frozen : 14 d   ─── không có WHERE thời gian; chỉ thấy sự kiện "cạn" và ngày xấu
                                       trong 14 ngày gần nhất
```

Hệ quả có chủ đích của 14 ngày (người dùng chốt): service cạn rồi không đạt SLO liên tục quá 14 ngày có
thể tự mất trạng thái đóng băng khi sự kiện "cạn" trôi ra ngoài cửa sổ — ghi ở technical-debt. Cả hai
cửa sổ được test canh (`ThresholdRule_LooksBackSevenDays`, `FrozenRule_LooksBackFourteenDays`).

## 4. Đóng băng và hồi phục dùng SLO mới (Quyết định 2)

Logic suy ra trạng thái "cạn — ưu tiên độ tin cậy" không đổi so với 027; chỉ đổi ngưỡng "ngày đạt SLO":

```
ngày không đạt ⇔ có traffic  ∧ ( 5xx/spans ≥ 0.01  ∨  vượt-p95/spans > 0.05  ∨  vượt-p99/spans > 0.01 )
```

Ba hằng số này được test `FrozenRule_DailySloThresholdsMatchTheBudgets` so trực tiếp với bảng tỷ lệ ngân
sách, không chép tay. Ngân sách đặt lại thứ Hai 00:00 vẫn **không** gỡ đóng băng — sự kiện "cạn" chỉ ghi
khi alert 100 chuyển sang active.

## 5. Chuyển tiếp từ tháng sang tuần (Quyết định 5)

Biểu thức ở mục 2 tự bắt đầu tính từ thứ Hai của tuần triển khai; không cần ngày bắt đầu cố định. Sự kiện
"cạn" của chế độ tháng bị xoá bằng cách xoá và tạo lại index `slo-error-budget-events` (mapping `keyword`).
Lưu ý vận hành phát hiện lúc chuyển: nếu một alert 100 đang active **liên tục** từ trước lúc xoá index,
không có sự kiện mới nào được ghi và service cạn mà không bị đóng băng — Disable rồi Enable rule
`error-budget-100` để Kibana coi alert là mới và ghi lại sự kiện.

## 6. Dashboard: sửa tạm, không tách (Quyết định 4)

3 Discover session của 027 đổi sang "tuần này", `time_range now-7d` (từ spec 030: dashboard Ngân sách lỗi tuần, khoảng riêng 30 ngày); panel text ghi `99%`/tuần; nhãn cột
Error-rate của bảng SLO 021 đổi thành "ngưỡng < 1%" (ngoại lệ FR-010 do người dùng chốt). Id, vị trí và
mọi panel khác giữ nguyên — tách dashboard là việc của spec B.

*Từ spec 030 (spec B): dashboard SLO đã được tách thành `Xử lý sự cố — 7 service` và `Ngân sách lỗi tuần — 7 service`; xem [`030_Architect_hai dashboard xử lý sự cố và ngân sách tuần.md`](030_Architect_hai%20dashboard%20xử%20lý%20sự%20cố%20và%20ngân%20sách%20tuần.md).*

## 7. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/029-error-budget-weekly-component.drawio`](../diagrams/029-error-budget-weekly-component.drawio)
- Sơ đồ luồng nghiệp vụ: [`docs/diagrams/029-error-budget-weekly-flow-nghiep-vu.drawio`](../diagrams/029-error-budget-weekly-flow-nghiep-vu.drawio)
- Sơ đồ trình tự: [`docs/diagrams/029-error-budget-weekly-sequence.drawio`](../diagrams/029-error-budget-weekly-sequence.drawio)

## 8. Tham khảo thêm

Cách dựng rule/panel và truy vấn đầy đủ:
[`07-canh-bao-ngan-sach-loi.md`](../kibana-quan-sat-he-thong/07-canh-bao-ngan-sach-loi.md); rule phát hiện
nhanh: [`08-phat-hien-nhanh-va-xu-ly-su-co.md`](../kibana-quan-sat-he-thong/08-phat-hien-nhanh-va-xu-ly-su-co.md).
Bằng chứng kiểm chứng và các phát hiện: [`docs/QA/QA_Debt.md`](../QA/QA_Debt.md) mục 029. Giới hạn đã biết
(đóng băng > 14 ngày, lưu lượng thấp theo tuần, rule phát hiện nhanh không có test, ranh giới thật chưa quan
sát): [technical-debt.md](technical-debt.md).
