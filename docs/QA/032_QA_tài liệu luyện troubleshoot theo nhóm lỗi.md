# QA: Tài liệu luyện troubleshoot theo nhóm lỗi

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. **US1 — hướng dẫn từng bước cho 8 nhóm lỗi**: file 09–16 trong `docs/kibana-quan-sat-he-thong/`, mỗi nhóm một
   file (nhóm 2 gộp loại B và C), đã chạy thật bằng `-Inject` rồi `-Restore`.
2. **US2 — gợi ý theo triệu chứng cho lỗi bất ngờ**: file 17, 7 triệu chứng, mỗi triệu chứng ba mức mở dần; mức 1–2
   không lộ mã loại hay tên service.
3. **US3 — bài tập tự làm và tiêu chí "đã đạt"**: mỗi file nhóm có hai mục này, bài tập đã chạy thử với `-DurationSeconds`.
4. **US4 — chuỗi tài liệu nhất quán và test chống sót**: `00-tong-quan-lo-trinh.md` liệt kê 01–17; README diễn tập trỏ
   sang chuỗi mới; project test quy ước tài liệu.
5. **US5 — tài liệu đi kèm**: folder Postman `32`, tài liệu PO/QA/Architect, 3 sơ đồ drawio.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — đổi cờ trong `.env`, chạy `incident-drill.ps1`, bấm Postman folder 32

**Dựng stack**: `docker compose -f docker-compose.local.yml up -d --wait`. Import dashboard và rule
([`dashboards/README.md`](../kibana-quan-sat-he-thong/dashboards/README.md), [`alerts/README.md`](../kibana-quan-sat-he-thong/alerts/README.md);
rule import vào ở trạng thái tắt, phải bật). Tải nền ở một terminal riêng: `./scripts/incident-drill.ps1 -Load`.

**Postman**: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment
**Ecommerce - Local**; chạy `00 - Xác thực & phân quyền (Get Token)` rồi folder **`32 - Luyện troubleshoot: 8 nhóm lỗi, quan sát triệu chứng và xác nhận khỏi`**
theo từng subfolder: tiêm bằng lệnh nêu ở mô tả subfolder → chạy request `01` → `-Restore` → chạy request `02`. Với
newman dùng `-e .incident-drill/load/environment-with-token.json` và chạy từng request riêng (các request 01 chỉ đúng khi lỗi đang bật).

**Công tắc cấu hình**:
- **Bật**: `CHAOS_ALLOW_FAULT_INJECTION=true` trong `.env` (mọi nhóm); nhóm 3 thêm `CHAOS_ALLOW_LATENCY_INJECTION=true` (cả hai mặc định tắt).
- **Khôi phục**: `./scripts/incident-drill.ps1 -Restore -RunId <id>`. Sau đó đặt hai cờ về `false`/xoá dòng và chạy lại stack không kèm override.

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | Đã quan sát (2026-10-06) |
|---|---|---|---|---|
| Nhóm 1 — A đích kết nối sai | Cờ bật; `-Inject -Type A -Target orders-api -DurationSeconds 900` | Subfolder `1` (01, 02) | 01: 503 `self-database` Unhealthy; 02: 200 `Healthy` | Đạt cả hai (2+2 assertion); file 09: orders 53,3% 5xx, chuỗi 15 phút sạch 10:05–10:19 |
| Nhóm 2 — C 5xx theo tỷ lệ | `-Inject -Type C -Target products-api` | Subfolder `2` (01, 02) | 01: 500 với `X-Chaos-Fault`; 02: 200 request thường | Đạt; file 10: 23,1–25,2% 5xx từng phút, 0 log lỗi |
| Nhóm 2 — B cạn pool | `-Inject -Type B -Target gateway-api` | Subfolder `2` (03) | 200, không chậm bất thường (B không có triệu chứng) | Đạt (2 assertion); 100 request song song p95 989 ms khi tiêm, 889 ms khi gỡ |
| Nhóm 3 — D độ trễ | Hai cờ bật; `-Inject -Type D -Target orders-api` | Subfolder `3` (01, 02) | 01: 404 chậm ≥ 1,9 s; 02: 404 nhanh | Đạt; file 11: p95 2009–2437 ms, 0% 5xx |
| Nhóm 4 — E DB dừng | `-Inject -Type E -Target orders-db` | Subfolder `4` (01, 02) | 01: 503; 02: 200 sau khôi phục | Đạt; file 12: 51,6% 5xx, khôi phục 48 giây |
| Nhóm 5 — F Authority sai | `-Inject -Type F -Target gateway-api` | Subfolder `5` (01, 02) | 01 lỗi/treo; 02: 200 sau chờ token | Đạt (chờ 150 giây lấy token mới); file 13: 110/159 span 401 ở đích `orders-api`, healthy |
| Nhóm 6 — G thiếu tài nguyên | `-Inject -Type G -Target products-api` | Subfolder `6` (01, 02) | 200, chậm hơn nền | Đạt; file 14: p95 ≈ 100 ms (nền 8 ms), không OOM |
| Nhóm 7 — H mạng đứt | `-Inject -Type H -Target baskets-api` | Subfolder `7` (01, 02) | 01: 502/504; 02: 200/404 | Đạt; file 15: BFF 504, span Client không mã HTTP p50 1000 ms |
| Nhóm 8 — I container chết | `-Inject -Type I -Target parties-api` | Subfolder `8` (01, 02) | 01: 502/504; 02: 404 | Đạt; file 16: `Exited (137)`, `restartPolicy=no`, khôi phục 9 giây |
| Bài tập tự làm | `-Inject … -DurationSeconds 240/300` với đích khác lần đo | (không có) — telemetry Kibana | Script tự gỡ đúng hạn, số đo khớp "Đáp án tham khảo" | Đúng: A `products-api`, C `baskets-api`, E `products-db`, F `products-api`, G `baskets-api`, H `baskets-api`, I `parties-api` bằng `-DurationSeconds`; D bằng ba request gửi tay; B bằng lệnh bắn 100 request song song |
| Bài mù + gợi ý | `-Start`, `-Hint` mức 1–3, `-Restore`, `-Reveal` | (không có) | Mức 1–2 khớp triệu chứng của file 17; `hint-log.json` đủ 3 dòng; băm khớp | Đúng (loại E, `parties-db`); chỉ kiểm cấu trúc vì người chạy biết đáp án |
| Dọn dẹp | Hai cờ về `false`, `-Restore` mọi lần chạy, chạy lại stack | (không có) | Mọi container healthy | Xem Kết luận |

### Tự động

Project [`TroubleshootGuideConventionTests`](../../tests/TroubleshootGuideConventionTests) (38 test) đọc `catalog.json` làm khoá phủ,
không cần Docker. Chạy: `dotnet test tests/TroubleshootGuideConventionTests`. Test **không** kiểm nội dung khớp giữa tài liệu và catalog.

| Cần xác nhận | Test case (bấm để mở) | Lệnh chạy riêng |
|---|---|---|
| Mỗi nhóm có đúng một file và tiêu đề đánh số | [`GroupCoverageTests.cs:36`](../../tests/TroubleshootGuideConventionTests/GroupCoverageTests.cs#L36) | `dotnet test tests/TroubleshootGuideConventionTests --filter EveryGroup_HasExactlyOneGuideFile_WithNumberedTitle` |
| Mỗi loại A–I có mục `## Loại` trong file nhóm | [`GroupCoverageTests.cs:56`](../../tests/TroubleshootGuideConventionTests/GroupCoverageTests.cs#L56) | `… --filter EveryType_HasSectionInItsGroupFile` |
| File nhóm có đủ 6 mục bắt buộc | [`GroupCoverageTests.cs:77`](../../tests/TroubleshootGuideConventionTests/GroupCoverageTests.cs#L77) | `… --filter EveryGuideFile_HasRequiredSections` |
| File 17 có mức 3 cho mọi loại | [`GroupCoverageTests.cs:99`](../../tests/TroubleshootGuideConventionTests/GroupCoverageTests.cs#L99) | `… --filter SymptomFile_HasLevel3_ForEveryType` |
| Mỗi triệu chứng đủ mức 1→2→3 theo thứ tự | [`GroupCoverageTests.cs:117`](../../tests/TroubleshootGuideConventionTests/GroupCoverageTests.cs#L117) | `… --filter SymptomFile_EverySymptom_HasLevels1Then2Then3` |
| Mức 1–2 không lộ mã loại/tên service | [`GroupCoverageTests.cs:143`](../../tests/TroubleshootGuideConventionTests/GroupCoverageTests.cs#L143) | `… --filter SymptomFile_Levels1And2_DoNotLeakTypeOrService` |
| Mọi link nội bộ và neo có đích thật | [`InternalLinkTests.cs:25`](../../tests/TroubleshootGuideConventionTests/InternalLinkTests.cs#L25) | `… --filter EveryInternalLink_PointsToSomethingThatExists` |
| `00-tong-quan-lo-trinh.md` liệt kê đủ file 09–17 | [`InternalLinkTests.cs:56`](../../tests/TroubleshootGuideConventionTests/InternalLinkTests.cs#L56) | `… --filter Overview_LinksToEveryGuideFile` |

**Kết quả lượt QA này**: 38/38 xanh. Viết trước tài liệu: ban đầu 37 đỏ, 1 xanh. Cố ý làm hỏng 4 kiểu (đổi tên file nhóm, hỏng link,
xoá một dòng mức 3, chèn tên service vào mức 1) thì đỏ đúng bất biến rồi xanh lại sau khi hoàn tác. 16/16 request Postman của folder 32 đạt (newman, từng request).

## Kết luận

**PASS kèm ghi chú.** Cả 8 nhóm có hướng dẫn đã chạy thật, file 17 không lộ đáp án ở mức 1–2, test quy ước tài liệu xanh và bắt được lỗi,
folder Postman 32 đạt 16/16.

Ghi chú: (1) loại B không có triệu chứng, G chỉ nhẹ; (2) H, I làm service biến mất khỏi bảng SLO; (3) log lỗi bị `Identity.Api` lấp và dòng giả
`Bff.Api → Parties.Api`; (4) alert cộng dồn khi tiêm nối tiếp nên chỉ đo được thời gian báo của C và D; (5) chưa có người thật làm theo
hướng dẫn; (6) hai nơi gợi ý (file 17 và `catalog.json`) có thể lệch; (7) script và catalog vẫn không có test tự động.

Chi tiết: [QA_Debt.md](QA_Debt.md) mục 032.
