# Research: Tài liệu luyện troubleshoot theo nhóm lỗi (032)

Các quyết định đã chốt ở Clarifications không nhắc lại; file này ghi quyết định thiết kế của plan.

## Quyết định 1 — Test quy ước tài liệu là project xUnit riêng, không dùng package mới

- **Decision**: `tests/TroubleshootGuideConventionTests`, tìm gốc repo bằng marker `Ecommerce.slnx` (như các suite
  quy ước khác, hỗ trợ worktree), đọc `scripts/incident-drill/catalog.json` bằng `System.Text.Json`, quét link
  bằng regex trên Markdown. Không thêm package (không Markdig).
- **Rationale**: Cùng nếp `ServiceManifestSloConventionTests`; chạy nhanh, không Docker. Link nội bộ trong tài
  liệu này chỉ có dạng `[text](path)` và neo `#...`, regex đủ.
- **Alternatives**: Script bash trong `scripts/ci` (bị loại theo câu 11); thêm Markdig (thừa cho nhu cầu).

## Quyết định 2 — Cách test biết "nhóm N có file hướng dẫn" và "loại X có mục trong file 17"

- **Decision**: Quy ước `NN = 08 + groupId` (nhóm 1→09 … nhóm 8→16); test đọc `groups` và `types` từ catalog, kiểm
  file `NN-*.md` tồn tại, có tiêu đề `# ` và có mục `## Loại <code>` cho mỗi loại thuộc nhóm đó. File 17: mỗi loại
  có ít nhất một dòng bắt đầu `**Mức 3 — Đáp án (Loại <code>)**`. Chi tiết:
  [contracts/guide-convention-test-contract.md](./contracts/guide-convention-test-contract.md).
- **Rationale**: Dấu hiệu máy đọc được nhưng vẫn là văn bản tự nhiên; không cần front-matter. Không kiểm nội dung
  khớp catalog (theo câu 10).
- **Alternatives**: Front-matter YAML (làm tài liệu kém thân thiện); đếm số tiêu đề (dễ gãy).

## Quyết định 3 — "Không lộ đáp án" ở mức 1–2 kiểm thủ công, test chỉ kiểm dấu hiệu rẻ

- **Decision**: SC-003 là kiểm thủ công (checklist review). Test chỉ kiểm: trong khối mức 1 và mức 2 không có mã
  loại dạng `Loại X` và không có tên service trong danh sách 7 service.
- **Rationale**: "Không lộ" là phán đoán ngữ nghĩa; dấu hiệu rẻ bắt lỗi lỡ tay hiển nhiên.
- **Alternatives**: Không kiểm gì; kiểm bằng NLP (quá mức).

## Quyết định 4 — Khung file nhóm cố định

- **Decision**: Mọi file 09–16 theo cùng khung ([contracts/guide-structure-contract.md](./contracts/guide-structure-contract.md));
  nhóm 2 có hai mục `## Loại B` và `## Loại C` trong cùng file.
- **Rationale**: Khớp văn phong "Bạn sẽ thấy" của 01–08; giảm lệch giữa 8 file.

## Quyết định 5 — Thứ tự thực hiện

- **Decision**: (1) hợp đồng + test đỏ (chưa có file 09–17); (2) với mỗi nhóm: chạy thật `-Inject` → đo → viết file
  → `-Restore` → xác nhận; (3) file 17 từ các lần đo và QA_Debt; (4) sửa 00 + README; (5) tài liệu đi kèm; (6) test
  xanh. Xin phép người dùng trước khi bật stack và trước khi dọn Elastic.
- **Rationale**: Đúng Nguyên tắc III cho phần test; số đo đi cùng lúc viết giảm sai lệch.
- **Alternatives**: Đo cả 8 nhóm trước rồi viết (dễ quên điều kiện đo).

## Quyết định 6 — Nguồn triệu chứng thật cho file 17

- **Decision**: Chỉ dùng các triệu chứng có trong `docs/QA/QA_Debt.md` và `docs/architecture/technical-debt.md`
  (mục 025, 027, 028, 031) cùng số đo của chính các lần chạy ở 032: lỗi lan gateway/BFF khi orders hỏng, token hỏng
  sau khi tạo lại identity (401 hoặc 502/504 qua BFF), nhiễu khởi động nguội 5–7 phút, môi trường chậm từng đợt,
  circuit breaker không trip (025). Mỗi bẫy ghi nguồn.
- **Rationale**: Yêu cầu của người dùng: "không bịa thêm".

## Quyết định 7 — Sửa 00-tong-quan-lo-trinh.md

- **Decision**: Rà lại toàn file khi sửa; chỗ đã biết lỗi thời: dòng 3 "Bộ 4 file", dòng 5 "4 file đều", dòng 28
  "Cần trọn vẹn 4 file trước", mục "Chuẩn bị chung cho cả 6 file". Thêm mục 09–17 vào lộ trình. Quy trình triage
  chỉ link, không chép.
- **Rationale**: Câu 6: sửa đầy đủ.

## Quyết định 8 — Nợ cần ghi

- **Decision**: `technical-debt.md`: hai nơi gợi ý có thể lệch; sai lệch Nguyên tắc III của script 031 còn mở (hạn
  của 031 hết ở 032, chưa có hạn mới — chờ người dùng quyết); số đo phụ thuộc máy. `QA_Debt.md`: mọi phát hiện khi
  chạy thật. `functional-debt.md`: mục 032.

## Kết quả xác minh

### T002 — điều kiện tiên quyết trên đĩa (2026-10-06, chưa cần stack)

- `scripts/incident-drill/catalog.json`: 8 `groups` (1 Đích kết nối sai, 2 Nghẽn và lỗi theo tỷ lệ, 3 Độ trễ, 4 Phụ thuộc hạ tầng dừng, 5 Xác thực hỏng, 6 Thiếu tài nguyên, 7 Mạng đứt, 8 Container chết hoặc khởi động lại) và 9 `types` A–I; A→1, B→2, C→2, D→3, E→4, F→5, G→6, H→7, I→8. Khớp giả định của spec.
- `scripts/incident-drill.ps1` có đủ tham số `-Start`, `-Reveal`, `-Inject`, `-Restore`, `-Hint`, `-Load`, `-Type`, `-Group`, `-Target`, `-DurationSeconds`, `-Level`, `-RunId`.
- Postman: có folder 26, 27, 28 và `31 - Danh mục nhóm lỗi: kiểm chứng thủ công 6 nhóm D–I`.

### T003 — nguồn triệu chứng thật cho file 17

| Triệu chứng/bẫy | Nguồn |
|---|---|
| Lỗi lan gateway/BFF khi một service phía sau hỏng; gateway `502`, BFF `502/504` | `QA_Debt.md` dòng 67, 685 (mục 002, 020); `technical-debt.md` dòng 109, 488 |
| Token hỏng sau khi tạo lại identity → `401`, `502`, `504` qua BFF | `QA_Debt.md` dòng 395, 520, 883; `technical-debt.md` dòng 256, 488 |
| Nhiễu khởi động nguội 5–7 phút sau khi tạo lại container | `QA_Debt.md` dòng 124, 195, 395, 819; `technical-debt.md` dòng 460, 488 |
| Môi trường chậm từng đợt | `QA_Debt.md` dòng 820; `technical-debt.md` dòng 461 |
| Circuit breaker không trip khi request tuần tự / header chaos không tới orders | `QA_Debt.md` dòng 645–655, 776; `technical-debt.md` dòng 166, 215–221 |
| Nhóm G chỉ triệu chứng nhẹ; 96m làm OOM-kill | `QA_Debt.md` dòng 874–875 |
| Nhóm D phải gửi thẳng orders-api | `QA_Debt.md` dòng 767, 876 |
| Nhóm B không có triệu chứng | `QA_Debt.md` mục 031 (dòng ~879) |

### T008 — test quy ước ĐỎ đúng lý do (2026-10-06)

`dotnet test tests/TroubleshootGuideConventionTests`: **Failed 37, Passed 1, Total 38**. 37 test đỏ đều do `Assert.NotNull() Failure` /
thiếu file 09–17 và mục tương ứng (chưa viết tài liệu), không do lỗi biên dịch hay không đọc được catalog; 1 test xanh là
`EveryInternalLink_PointsToSomethingThatExists` (hiện chỉ quét `00-tong-quan-lo-trinh.md`, mọi link của file này còn đúng). Hai test
`Overview_LinksToEveryGuideFile` và các test phủ nhóm/loại đỏ như kỳ vọng. Lần build đầu gặp CA1305 (`int.Parse` không có culture),
đã sửa bằng `CultureInfo.InvariantCulture`.

### T009 — nhóm 1 (loại A) chạy thật, 2026-10-06 (giờ Việt Nam)

- `-Load` bật từ ~09:50; `.env` của worktree sao từ `.env` của checkout chính (không commit, đã gitignore) cộng hai cờ chaos. Kibana trống (0 dashboard, 0 rule) nên đã import `dashboards/xu-ly-su-co.ndjson` và `alerts/incident-fast-detection-rule.ndjson`, bật rule bằng `POST /api/alerting/rule/<id>/_enable` (rule import vào ở trạng thái tắt).
- `-Inject -Type A -Target orders-api` bắt đầu 09:55:18; `state.json` `injected` lúc 09:58:07 (cả 7 container tạo lại). `-Restore` 10:03:19 → 10:04:10 (51 s), `restored`.
- Số đo (4 phút tới 10:03): Orders.Api 152/285 5xx (53,3%), p95 851 ms; Bff.Api 245/996 (24,6%); Gateway.Api 260/805 (32,3%); Baskets 1/244, Parties 1/111, Identity 0/117, Products 0/239. Lỗi theo route: Orders `/orders` 500 ×115, `/health/ready` 503 ×37; Bff `/bff/checkout` 502 ×115, 504 ×10; Gateway 502 ×115, 504 ×15. Cặp gọi: Bff→orders-api 500 ×137; Gateway→bff-api 502 ×137, 504 ×32. Log Orders.Api 5 phút: 442 Error, 21 Warning; 129 chứa "Name or service not known". `/health/ready` 503, check `self-database` Unhealthy "...(provider: TCP Provider, error: 35 ...)".
- Từng phút của Orders.Api: nền 09:45–09:54 p95 2–52 ms, 0%; 09:58–10:03 5xx 46–89%; 10:04 `total = 37`, p95 15750 ms (khởi động nguội); 10:05 `dat = true`.
- **Phát hiện cho QA_Debt**: (1) panel "Log lỗi gần nhất" bị `Identity.Api` lấp: 1057/1734 dòng Error trong 30 phút là `Error unprotecting the IdentityServer signing key` (khoá bảo vệ dữ liệu mất khi tạo lại container), gấp ~2 lần log lỗi của Orders.Api (482); (2) bảng "Lỗi gọi hạ lưu" luôn có dòng `Bff.Api → Parties.Api` `bad_pct = 100` do tải nền gọi đối tác không tồn tại → 404 (125/125 span Client là 404, `status.code = Error`), nền chứ không phải lỗi tiêm; (3) rule `incident-fast-detection` bật ngay sau khôi phục, lần chạy 10:05 báo `active` cả 7 service (start 10:05:00.865).

### T010 — nhóm 2 (loại B và C) chạy thật, 2026-10-06

- Loại B (`gateway-api`, `MaxConnectionsPerServer=1` đã vào env container): tiêm 10:20:36→10:21:03, restore 26–31 s. Tải nền: `Gateway.Api` từng phút `dat = true` (p95 50–85 ms) trong khi tiêm. Bắn song song bằng `curl.exe -Z` qua `:5300/bff/products`: B tiêm → 30×3: 90×200 p95 272 ms; 100×3: 300×200 p95 989 ms; đã gỡ → 30×3 p95 516; 100×3 p95 889. Không có 5xx, hiệu số p95 đổi dấu ⇒ B không có triệu chứng đo được (củng cố giới hạn đã ghi ở QA_Debt 031). Lần đo bằng script node trước đó (554/2574 ms khi tiêm, 409/1638 ms đã gỡ, một lần mỗi điều kiện, sau 8 phút) cho chiều ngược với `curl` ⇒ nhiễu của máy; tài liệu chỉ dùng số `curl`.
- Loại C (`products-api`): tiêm 10:37:38→10:38:11, tỷ lệ in ra 14%, tốc độ nền đo lúc tiêm 2,98 span/s, gửi 0,485 req/s; đạt thực tế 23,1–25,2%/phút (163/634 = 25,7% trong 6 phút), p95 7–19 ms, log lỗi `Products.Api` = 0 dòng, 5xx không lan (163/163 ở `Products.Api`). 500 có `server.address = localhost`, `user_agent.original = ...WindowsPowerShell/5.1...`; 200 có `server.address = products-api`, không có UA. Restore 0,5 s (10:46:28); phút 10:47 `dat = true`. Alert `incident-fast-detection`: `Products.Api` active từ 10:39:59 (~1 phút 48 giây sau tiêm); `Parties.Api`/`Identity.Api` active từ 10:34:59 và `Bff.Api`/`Baskets.Api` từ 10:39:59 là nhiễu khởi động nguội.
- **Phát hiện cho QA_Debt**: (4) tỷ lệ 5xx đạt thực tế (~24%) lệch tỷ lệ script in ra (14%) vì tốc độ nền đo lúc tiêm (2,98 span/s) cao hơn tốc độ nền thật sau đó (~1,95 span/s); (5) `-DurationSeconds` tính từ lúc `injected` (11:09:53→11:14:54 với 300 s), không phải từ lúc gõ lệnh (11:09:20); (6) trong 3 phút sau khi tạo lại, 9+2 span `/baskets/current` trả 401 (token của tải nền hỏng sau khi tạo lại identity).
- Bài tập nhóm 2 đã chạy thử: C vào `baskets-api`, `-DurationSeconds 300`, tự gỡ đúng hạn (11:14:54).

### T011 — nhóm 3 (loại D) chạy thật, 2026-10-06

- `-Inject -Type D -Target orders-api`: 11:15:21→`injected` 11:15:53; tỷ lệ in ra 47%, 1,206 req/s, nền 1,36 span/s; container có `Chaos__AllowFaultInjection=true` và `Chaos__AllowLatencyInjection=true`. Restore 11:24:39→11:24:41 (1,5 s).
- Từng phút `Orders.Api` 11:15–11:23: p95 9388 (khởi động nguội), 2039, 2019, 2015, 2013, 2009, 2077, 2194, 2437 ms; 0% 5xx; nền 11:14: 90 span, p95 34. 5 phút tới 11:24: 311 span 404 `/orders/{orderId:guid}` (`server.address=localhost`, p50 2010 ms, max 4004) so với 98×201 `/orders` và 98×200 (`server.address=orders-api`, p50 23 và 4 ms); `Orders.Api` p95 2172/p99 2453, Bff p95 378, Gateway 416; log lỗi Orders 0; `/health/ready` 200. Trung bình theo route 10 phút: `/orders/{orderId:guid}` 1424 ms (đứng đầu), `/bff/basket/items` 119 ms.
- Header `X-Chaos-Latency-Ms`: trực tiếp `:5041` + `X-Tenant-Id` → `404 2,03 s`; không header → `404 0,013 s`; qua gateway `:5300/bff/orders/<guid>` → `404 0,040 s` (không tới orders — khớp V9/QA_Debt 025).
- Alert: `Orders.Api` và `Gateway.Api` active từ 11:20:32 (~4 phút 39 giây sau tiêm); Gateway chỉ 2/641 5xx (nhiễu).
- Sau restore: 11:25 p95 190 ms (đuôi), 11:26 `dat = true` (`total` 17).
- **Phát hiện cho QA_Debt**: (7) ước lượng tốc độ nền lệch ở loại D: in 47% nhưng thực tế 56% span chậm (311/554).

### T012 — nhóm 4 (loại E) chạy thật, 2026-10-06

- `-Inject -Type E -Target orders-db`: 11:28:07→`injected` 11:28:13 (6 s, không tạo lại container); `orders-db` `Exited (137)`; `orders-api` sau ~5 phút `Up 17 minutes (unhealthy)`, `FailingStreak = 49`; `/health/ready` 503 (`self-database` Unhealthy). Restore 11:33:28→11:34:16 (48 s), `orders-api` tự về `healthy` không khởi động lại; phút 11:35 `dat = true`.
- Từng phút `Orders.Api`: 5xx 41,41 / 50,93 / 51,38 / 49,52 / 53,00% (11:28–11:32), p95 ~1000 ms rồi 15 ms; 4 phút tới 11:33: Orders 211/409 (51,6%), Bff 356/1419 (25,1%), Gateway 368/1146 (32,1%), service khác 0%. Log container orders-api: 603 `Name or service not known`, 16 `Connection refused` (giống nhóm 1).
- Alert: `Orders.Api`/`Gateway.Api` active từ 11:20:32 (nhóm 3) không recovered trước khi E bắt đầu ⇒ không đo được thời gian báo của riêng E.
- Bài tập chạy thử: E vào `products-db` với `-DurationSeconds 240`: `Products.Api` 5xx 87–100%/phút, `/health/ready` 503, tự gỡ 11:41:59 (240 s sau `injected` 11:36:58), `products-db` `Up ... (healthy)`.
- **Phát hiện cho QA_Debt**: (8) E và A gần như không phân biệt được bằng log (cùng `Name or service not known`/`network-related`), chỉ phân biệt bằng `docker ps -a`; (9) `-Restore` cho E chờ ~48 s và không cần khởi động lại service; (10) khi alert của nhóm trước còn `active` thì không đo được thời gian báo của nhóm kế (alert dùng chung `kibana.alert.start`).

### T013 — nhóm 5 (loại F) chạy thật, 2026-10-06

- `-Inject -Type F -Target orders-api`: 11:42:21→`injected` 11:43:09; container `Identity__Authority=http://incident-missing-host:8080`. Restore 11:51:20→11:52:17 (57 s); Orders `dat = true` từ phút 11:53 (87 span, p95 38 ms), giữ tới 11:57.
- Từng phút `Orders.Api` 11:43–11:50: span 17/30/1/9/18/28/48/57; p95 35959/27745/462/15287/16753/3904/10042/1945 ms; 0% 5xx. 5 phút tới 11:51: Orders 159 span, 110×401 (`/orders`), p95 10581 ms; Bff 882 span 116 5xx (93× `/bff/checkout` 502); Gateway 695 span 228 5xx (502 ×91, 504 ×23…); các service khác 0 401; 404 của Gateway (108) và Parties (55) là nền. Log Orders: 38 Warning, 5 Error; docker logs: 45× `HttpRequestException: Name or service not known (incident-missing-host:8080)`, 15× GET `.well-known/openid-configuration`, 15× `OnRetry ... IdentityBackchannel`. `Up 8 minutes (healthy)`, `/health/ready` 200.
- Alert `Orders.Api`/`Gateway.Api` active liên tục từ 11:20:32 (nhóm 3→4→5), không đo riêng được cho F.
- Bài tập chạy thử: F vào `products-api` `-DurationSeconds 300`: injected 11:59:41, restored 12:05:05; `Products.Api` 144/213 span 401, p95 12810 ms; Bff 98 5xx + 77×401; healthy, ready 200.
- **Phát hiện cho QA_Debt**: (11) F: container `healthy` và `/health/ready` 200 dù mọi request xác thực hỏng (cấu hình định danh không nằm trong health check); (12) panel "Log lỗi gần nhất" (`severity_number >= 17`) gần như trống với F (5–7 Error / 5 phút) trong khi 38 dòng Warning mới có nội dung `openid-configuration`; (13) p95 hàng chục giây vì thử lại tải cấu hình OpenID, mẫu rất thưa (1–57 span/phút) nên tỷ lệ phần trăm kém tin cậy; (14) `-DurationSeconds 300` với F xong sau 5 phút 24 giây (cộng thời gian tạo lại container).

### T014 — nhóm 6 (loại G) chạy thật, 2026-10-06

- `-Inject -Type G -Target products-api`: 12:05:26→`injected` 12:05:28 (2 s; `docker update`, KHÔNG tạo lại container); `NanoCpus=100000000`, `Memory=MemorySwap=268435456`, `OOMKilled=false`, `RestartCount=0`, healthy. Restore 12:11:32→12:11:41 (9 s, tạo lại chỉ products-api; `cpus=0 mem=0`).
- Từng phút `Products.Api` (12:00–12:04 là dư âm của bài tập F): sau tiêm p95 244, 200, 100, 100, 89, 81 ms (12:05–12:10); p99 299, 347, 115, 135, 98, 96; 0% 5xx. 5 phút tới 12:11: Products p50 4 / p95 103 / p99 196 ms; Baskets p95 23; Orders 25; Parties 9; Identity 20; Bff 173; Gateway 174; `/products` 360×200 p95 102 ms; log ≥ Warning của Products = 0. Nền p95 Products 8 ms (35–40 phút trước). `docker stats`: products 109,9 MiB / 256 MiB, baskets 122,7 MiB / 14,64 GiB.
- Alert: cả 7 service `active` liên tục (start 10:34:59 / 10:39:59 / 11:20:32 UTC+7) tới 12:07 — không đo được thời gian báo riêng; G có thể không báo vì p95 5 phút (103 ms) < 150 ms.
- Bài tập chạy thử: G vào `baskets-api` `-DurationSeconds 300`: injected 12:11:48, restored 12:17:02; p95 16–25 → 42–133 ms (đỉnh 133 ở 12:12), không vượt 150.
- **Phát hiện cho QA_Debt**: (15) chạy các lần tiêm liên tiếp làm cả 7 alert `incident-fast-detection` `active` liên tục từ 10:34:59 tới ≥ 12:07 (tới 1 giờ rưỡi), nhiễu tích luỹ; (16) G: tiêm bằng `docker update` không tạo lại container nên không có khởi động nguội, nhưng triệu chứng chỉ ×10 độ trễ (p95 ~100 ms) và không vượt SLO sau hai phút đầu ⇒ khó phát hiện bằng cảnh báo; (17) `-DurationSeconds 300` với G xong sau 5 phút 14 giây (12:11:48→12:17:02).

### T015 — nhóm 7 (loại H) chạy thật, 2026-10-06

- `-Inject -Type H -Target products-api`: 12:17:23→`injected` 12:17:25 (2 s); `NetworkSettings.Networks = {}`, `:5088/health/ready` → `000` (không kết nối); container `Up 10 minutes (unhealthy)` (health check cuối trả 503). Restore 12:22:36→12:23:24 (48 s): `ecomerce-local_backbone`, `aliases=[products-api ecomerce-local-products-api-1]`, `healthy`, ready 200.
- 4 phút tới 12:22: chỉ 6 service có span, `Products.Api` không có hàng; `Bff.Api` 55/211 5xx (26,1%), p95 3006 ms (`/bff/products` 504 ×28, `/bff/basket/items` 504 ×27); `Gateway.Api` 54/210 (25,7%), 504 ×54. Span Client `Bff.Api → products-api`: 165 span `status.code=Error`, mã HTTP null, `TaskCanceledException`, p50 1000 ms, p95 1003 ms; log Bff 218 Error: `OnTimeout ... AttemptTimeout`/`TotalRequestTimeout`, `Downstream call to ProductsApi failed with 504`.
- `Products.Api` từng phút: 12:12–12:16 `dat = true` (p95 9–12 ms); 12:17 31 span, p99 233641 ms (một span dài); 12:18–12:21 không dòng; 12:22 34 span 17,65% 5xx; 12:23 94 span 4,26%; 12:24 `dat = true`. `Bff.Api` `dat = true` từ 12:23.
- Bài tập chạy thử: H vào `baskets-api` `-DurationSeconds 300`: injected 12:25:43, restored 12:31:41; 3 phút tới 12:30 chỉ 6 service có span (không có Baskets), Bff 54/144 5xx (504: `/bff/checkout` ×36, `/bff/basket/items` ×18), Gateway 504 ×54, baskets `unhealthy`.
- **Phát hiện cho QA_Debt**: (18) service bị tách (H) biến mất khỏi bảng SLO và không hàng alert nào cho nó (giới hạn đã nêu trong ô "Ngưỡng SLO"): phát hiện chỉ qua service gọi; (19) phút `12:17` có span `Products.Api` p99 233641 ms (một span dài bất thường khi vừa bị tách); (20) span Client timeout không có mã HTTP (`attributes.http.response.status_code` null) nên truy vấn "Lỗi gọi hạ lưu" lọc theo `>= 500` không thấy chúng — phải lọc theo `status.code = Error`.

### T016 — nhóm 8 (loại I) chạy thật, 2026-10-06

- `-Inject -Type I -Target orders-api`: 12:32:01→`injected` 12:32:04 (3 s). `docker ps -a`: `Exited (137)`; `running=false exit=137 oom=false restartPolicy=no`; `:5041/health/live` → `000`; container không tự sống lại. Restore 12:38:19→12:38:28 (9 s), `Up 6 seconds (healthy)`.
- 4 phút tới 12:38: 6 service có span, không có `Orders.Api` (dòng cuối 12:31). `Bff.Api` 101/347 5xx (29,1%), p95 1013 ms (`/bff/checkout` 504 ×100); `Gateway.Api` 100/344 (29,1%), 504 ×99. Span Client Bff→orders-api: 101 Error, mã HTTP null, `TaskCanceledException`, p50 1001 ms; docker logs Bff: 108 dòng `Sending HTTP request POST http://orders-api:8080/orders`.
- Bài tập chạy thử: I vào `parties-api` `-DurationSeconds 240`: injected 12:38:36, restored 12:42:43; 3 phút: Parties 8 span (p95 1918 ms), Bff `/bff/parties/{partyId:guid}` 504 ×30, Gateway 504 ×30; sau khôi phục `Up 29 seconds (healthy)`.
- **Phát hiện cho QA_Debt**: (21) nhóm 8 và nhóm 7 gần như trùng triệu chứng trong telemetry (cùng timeout 1 s, 504, service biến mất); chỉ `docker ps -a` / `docker inspect` phân biệt; (22) `-DurationSeconds 240` với I xong sau 4 phút 7 giây (12:38:36→12:42:43).

### T018 — bài tập nhóm 1 chạy thử, 2026-10-06

- `-Inject -Type A -Target products-api -DurationSeconds 300`: injected 12:43:52, restored 12:49:19 (5 phút 27 giây gồm tạo lại 7 container). Lệnh PowerShell lấy host (`Select-String -Pattern 'Server=[^;,]+'`) in `Server=incident-missing-db` (không in mật khẩu). 4 phút: `Products.Api` 358/370 5xx (96,8%; `/products` 500 ×318, `/health/ready` 503 ×40), Bff 502 `/bff/products` ×51 và `/bff/basket/items` ×55, Gateway 502 ×104; container `Up 4 minutes (unhealthy)`, ready 503.
- Sửa file 09: "container không bao giờ healthy" → `health: starting` rồi `unhealthy` (đã đo).
- Sự cố thao tác: một lệnh `node -e "..."` có dấu backtick trong chuỗi nháy kép của bash làm hỏng đoạn "Bài tập tự làm" của file 09; đã khôi phục bằng file đoạn viết bằng công cụ Write rồi chèn bằng `sed`. Từ nay không dùng `node -e` với backtick.

### T025 — test bất biến 1–3 xanh cho cả 8 file nhóm (2026-10-06)

`dotnet test tests/TroubleshootGuideConventionTests`: Failed 12, Passed 26, Total 38. 26 test xanh = 8 file nhóm × (file tồn tại + mục `## Loại` + đủ 6 mục bắt buộc) và `Overview_LinksToEveryGuideFile`. 12 test đỏ đều do file 17 chưa viết (9 loại thiếu mức 3, mức 1→2→3, rò rỉ mức 1–2) và các link tới `17-goi-y-theo-trieu-chung.md` chưa có đích.

### T026–T030 — file 17 (2026-10-06)

- T026: danh sách 7 triệu chứng đã được người dùng duyệt ("Duyệt cả 7 triệu chứng"): 1 5xx lan (A,E); 2 5xx một phần không lan (C); 3 chậm ~2 s không lỗi (D); 4 401 một service, sức khoẻ xanh (F); 5 chậm nhẹ ×10 chưa vượt SLO (G); 6 service biến mất, phía gọi timeout 504 (H,I); 7 không thấy gì (B); cộng mục "Bẫy và nhiễu thường gặp".
- Thiết kế: mức 1 KHÔNG link sang file nhóm (09–16 ghi sẵn nhóm/đáp án ở tiêu đề) — truy vấn dùng chung Q1–Q9 nằm ngay trong file 17 (mục "Truy vấn dùng chung"; thêm vào danh sách mục không phải triệu chứng của test và hợp đồng). 8 khối `esql` của file 17 và mọi khối của file 09–16 chạy được trên Elasticsearch (không lỗi cú pháp, script `runq.js` trong scratchpad).
- T029 (bài mù thật, chỉ kiểm cấu trúc vì người viết không mù): `-Start` → runId `20261006-132014`, băm `17CE1723…9A25`, bốc loại E vào `parties-db`, `delaySeconds` 120, `-Hint` mức 1/2/3 trả: mức 1 "service báo chưa sẵn sàng (503) dù tiến trình vẫn chạy; lỗi lan lên các tầng phía trên…", mức 2 "Nhóm 4 — Phụ thuộc hạ tầng dừng…", mức 3 "Loại E … parties-db, parties-api"; `hint-log.json` 3 dòng; `-Restore` (huỷ vì chưa tiêm); `-Reveal` khớp băm, in 3 lần mở gợi ý. So với file 17: mức 1/2/3 của `-Hint` khớp triệu chứng 1 (nhánh E); file 17 nặng hơn (có chỉ dẫn cách kiểm, mức 2 nêu "nhóm 1 hoặc nhóm 4") — đúng quyết định.
- T030: `dotnet test` toàn bộ project: Passed 38/38; rà thủ công mức 1–2 của 7 triệu chứng bằng regex (orders|products|baskets|parties|identity|Loại|docker kill|incident-missing|chaos): sạch, trừ chữ "cùng loại" (nghĩa thường) ở triệu chứng 5 mức 1.

### T017 — kiểm tra chéo 8 file (2026-10-06)

- Mọi khối `esql` của file 09–17 (4+3+3+1+3+2+2+3+8 = 29 khối) chạy được trên Elasticsearch (không lỗi cú pháp), bằng script `runq.js` (scratchpad). Mẫu số đo/lệnh ghi trong từng file đều chép từ lần chạy thật cùng ngày (xem T009–T016, T018).
- Link: test `InternalLinkTests` xanh (mọi link nội bộ và neo của file 09–17 và 00 có đích); mọi file nhóm liên kết (không chép) tới README 028, `mau-ban-ghi-su-co.md` và truy vấn 15 phút của file 08 ở mục "Xem thêm".
- Bí mật: `grep -i 'password=|secret|Bearer ey|eyJ'` trên file 09–17: không có kết quả; lệnh `docker inspect` lấy `Server=` dùng `Select-String 'Server=[^;,]+'` nên không in mật khẩu.
- Sự cố công cụ: lệnh PowerShell 5.1 `Get-Content`/`Set-Content` (không `-Encoding`) làm hỏng dấu tiếng Việt của `tasks.md` (mojibake); đã viết lại `tasks.md` từ nội dung gốc và ghi quy tắc "không dùng Get/Set-Content cho file tiếng Việt" vào phần ràng buộc.

### T037 — folder Postman 32 bằng newman (2026-10-06 13:58–14:42)

16/16 request đạt, chạy từng request qua `mk-mini.js` + `newman@6.2.2 -e .incident-drill/load/environment-with-token.json` theo chu trình tiêm → 01 → `-Restore` → 02: A (503/200), C (500/200), B (200), D (chậm/nhanh), E (503/200), F, G, H, I. Sự cố công cụ: `.ps1` UTF-8 không BOM bị PowerShell 5.1 đọc theo ANSI; splat mảng thay vì hashtable; `Write-Host` không bắt được (lấy `runId` từ thư mục `.incident-drill`); `Tee-Object` trong hàm làm `return $id` thành mảng; hai runner chạy song song làm lẫn trạng thái — đã dừng bằng TaskStop và `-Restore` các lần chạy còn mở.
