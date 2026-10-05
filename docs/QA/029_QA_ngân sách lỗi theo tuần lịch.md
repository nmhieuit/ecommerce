# QA: Ngân sách lỗi theo tuần lịch giờ Việt Nam (thay thế tháng lịch của 027)

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. **US1 — một con số, một chu kỳ**: hiến chương 2.0.0 ("99% weekly availability", "5xx below 1%"),
   `PlatformSloDefaults` (99%/1%), 7 `service-manifest.yaml` (`slos` 99%/1%, `error-budget-policy`
   `window: calendar-week`, khả dụng/5xx `1%`, p95 `5%`, p99 `1%`).
2. **US2 — cảnh báo và dashboard theo tuần**: 3 rule `error-budget-50/75/100` lọc từ thứ Hai 00:00 giờ Việt
   Nam, tỷ lệ `0.01`, cửa sổ rule 7 ngày, mỗi 5 phút; 3 panel ngân sách "tuần này" (từ spec 030: dashboard Ngân sách lỗi tuần, khoảng thời gian riêng 30 ngày; lúc 029 là `now-7d`); panel text
   `99%`/tuần; folder Postman 29 đốt ngân sách 7 service.
3. **US3 — đóng băng và phát hiện nhanh**: rule `error-budget-frozen` cửa sổ 14 ngày, ngày đạt SLO khi
   5xx < 1%; rule `incident-fast-detection` bắn khi 5xx ≥ 1% trong 5 phút.
4. **US4 — tài liệu**: 021/027/028 nói tuần lịch và 99%/1%; bộ tài liệu 029.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — đổi cờ tiêm lỗi rồi chạy folder Postman 29; xem kết quả trên dashboard Ngân sách lỗi tuần

Dựng stack: `./scripts/local-up.ps1` (cần `KIBANA_ENCRYPTION_KEY` trong `.env`). Tạo index
`slo-error-budget-events` (lệnh `PUT` ở [`07-canh-bao-ngan-sach-loi.md`](../kibana-quan-sat-he-thong/07-canh-bao-ngan-sach-loi.md)),
import [`alerts/error-budget-rules.ndjson`](../kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson),
[`alerts/incident-fast-detection-rule.ndjson`](../kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson)
và [`dashboards/ngan-sach-loi-tuan.ndjson`](../kibana-quan-sat-he-thong/dashboards/ngan-sach-loi-tuan.ndjson),
Enable 5 rule ([`alerts/README.md`](../kibana-quan-sat-he-thong/alerts/README.md)).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json)
và [`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), environment
**Ecommerce - Local**; folder **`29 - Ngân sách lỗi theo tuần: tiêm 5xx 7 service`** — không cần token.
`29a` chạy theo vòng (Runner hoặc `npx.cmd --yes newman@6.2.2 run ... --folder "29a - Tiêm 5xx 7 service (chạy theo vòng)" -n <số vòng>`),
`29b` chạy một lần sau mỗi đợt.

**Công tắc cấu hình**: `CHAOS_ALLOW_FAULT_INJECTION` trong `.env` (xem `.env.example`; cả 2 file compose,
mặc định `false` = tắt), rồi tạo lại 7 container API (`docker compose -f docker-compose.local.yml up -d
--force-recreate --no-deps gateway-api bff-api products-api baskets-api orders-api parties-api identity-api`)
và chờ healthy. **Khôi phục mặc định**: xoá dòng đó khỏi `.env` và tạo lại 7 container; folder 27a xác
nhận header bị bỏ qua.

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-10-05)** |
|---|---|---|---|---|
| Đầu tuần = thứ Hai 00:00 giờ VN (V1) | (không đổi) | (không có) — Discover ES\|QL với 4 thời điểm giả định ([research.md](../../specs/029-error-budget-weekly/research.md) Quyết định 9) | Cả 4 hàng ra đúng đầu tuần mong đợi | 4/4 đúng; `NOW()` 08:20Z → `2026-10-04T17:00Z` |
| Mức tiêu hao theo tuần, tỷ lệ 1% | (không đổi) | `29b` bước 01 | Mẫu số chỉ gồm span từ đầu tuần; 4 hàng/service | 28 hàng; span sớm nhất `04:03Z` > đầu tuần `2026-10-04T17:00Z` |
| Mốc 50 / 75 / 100 bật trong ≤ 5 phút (SC-002) | `CHAOS_ALLOW_FAULT_INJECTION=true` + 7 container | `29a` theo vòng (16 → 8 → 10 vòng), `29b` bước 01, 02 | Alert từng mốc cho `availability`, `error-rate` của cả 7 service | Mốc 50: 4 phút 33 giây sau đợt 1; mốc 100: 1 phút 54 giây sau đợt 3, cả 7 service; mốc 75 của 4 service bắn cùng lượt với lúc đã quá 100% (lưu lượng thấp) |
| Trạng thái cạn (US3) | (như trên) | `29b` bước 03 | 7 service trong danh sách cạn | 7/7 (6 service lúc 08:51:24Z, Parties.Api lúc 08:36:24Z) |
| Dashboard khớp alert (SC-004) | (không đổi) | (không có) — mở dashboard Ngân sách lỗi tuần | 3 panel ngân sách "tuần này" (khoảng thời gian riêng 30 ngày từ spec 030), số khớp `29b` | Khớp; panel text `99%`/tuần; nhãn cột Error-rate "ngưỡng < 1%" |
| Phát hiện nhanh — dưới 1% không bắn vì 5xx (SC-005) | (như trên) | Gửi 300 request thường + 1 request `X-Chaos-Fault: 5xx` vào `Orders.Api` trong một cửa sổ 5 phút | Không có alert `incident-fast-detection` cho Orders.Api | 1/359 = 0,28% → không bắn |
| Phát hiện nhanh — từ 1% thì bắn | (như trên) | 300 request thường + 10 request `X-Chaos-Fault: 5xx` | Alert Orders.Api trong ≤ 5 phút | 10/366 = 2,73% → alert mới ở lượt chạy kế tiếp |
| Ngày đạt SLO theo ngưỡng mới (FR-008) | (không đổi) | (không có) — sự kiện thử trong index tạm, chạy đúng ES\|QL của rule frozen | Ngày 5xx 0,5% là đạt; ngày 5xx 2% là xấu; sự kiện ngoài 14 ngày không thấy | Đúng cả 5 trường hợp thử; đã xoá index tạm |
| Dọn dẹp | Xoá `CHAOS_ALLOW_FAULT_INJECTION` khỏi `.env`, tạo lại 7 container | `27a` bước 01 | `200` | 7 container in `false`; `27a` → `200` |

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-002/FR-004 — mặc định nền tảng 99%/1% | [`PlatformSloDefaults.cs:18`](../../tests/ServiceManifestSloConventionTests/PlatformSloDefaults.cs#L18) — `ByClassification` · [`SloDefaultComplianceTests.cs:29`](../../tests/ServiceManifestSloConventionTests/SloDefaultComplianceTests.cs#L29) — `EveryService_MatchesPlatformDefault_OrDocumentsAJustifiedAlternative` | `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~SloDefaultComplianceTests` |
| FR-003 — tỷ lệ 1% / 1% / 5% / 1% | [`ErrorBudgetPolicyTests.cs:73`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L73) — `EveryService_DeclaresExactlyTheFourBudgets_WithContractValues` | `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~ErrorBudgetPolicyTests` |
| FR-001 — tuần lịch UTC+7 | [`:105`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests.cs#L105) — `EveryService_UsesACalendarWeekInVietnamTime` | (lệnh như trên) |
| FR-003 — tỷ lệ trong rule `0.01` | [`ErrorBudgetRuleDefinitionTests.cs:120`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L120) — `ThresholdRule_AllowedRatiosMatchTheContract` | `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~ErrorBudgetRuleDefinitionTests` |
| FR-001/FR-006 — rule mốc lọc từ thứ Hai 00:00 giờ VN | [`:145`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L145) — `ThresholdRule_StartsAtMondayMidnightVietnamTime` | (lệnh như trên) |
| FR-006 — cửa sổ rule mốc 7 ngày | [`:167`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L167) — `ThresholdRule_LooksBackSevenDays` | (lệnh như trên) |
| FR-008 — cửa sổ rule đóng băng 14 ngày | [`:225`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L225) — `FrozenRule_LooksBackFourteenDays` | (lệnh như trên) |
| FR-008 — ngày đạt SLO theo tỷ lệ ngân sách | [`:244`](../../tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs#L244) — `FrozenRule_DailySloThresholdsMatchTheBudgets` | (lệnh như trên) |

**Kết quả lượt QA này (2026-10-05)**: `ServiceManifestSloConventionTests` 103/103 xanh (8 test mới/sửa của
029 chạy thấy đỏ trước khi sửa manifest/rule: 21/95 rồi 9/23 rồi 2/25). Folder Postman 29: `29a` 238/238
assertion `500` (3 đợt), `29b` chạy được cả 3 truy vấn. Rule `incident-fast-detection` không có test tự
động (giữ sai lệch Nguyên tắc III của 028).

## Kết luận

**PASS kèm ghi chú.** Hiến chương, manifest, test, rule và dashboard cùng nói tuần lịch giờ Việt Nam và
99%/1%; mốc cảnh báo bật trong vòng một chu kỳ 5 phút cho cả 7 service; trạng thái cạn và ngưỡng phát hiện
nhanh 1% đúng trên Kibana thật. Ghi chú:
(1) lưu lượng thấp làm một service nhảy qua nhiều mốc trong cùng một chu kỳ;
(2) Disable/Enable rule 100 để lại alert cũ trong bảng dashboard khoảng 10 phút;
(3) alert phát hiện nhanh bị Kibana đánh "flapping" giữ active thêm vài lượt;
(4) ranh giới thứ Hai 00:00 thật và hồi phục 3 ngày chưa quan sát được trong một phiên;
(5) cửa sổ 14 ngày của rule đóng băng là giới hạn đã biết.
Chi tiết: [QA_Debt.md](QA_Debt.md) mục 029.
