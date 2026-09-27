# QA: Khai báo và đo lường liên tục SLO theo từng service trong service manifest

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

Spec này (research.md Quyết định 0) phát hiện phần lớn việc "đã làm rồi, chỉ chưa được bảo vệ": cả 7 `service-manifest.yaml` đã có khối `slos:` đầy đủ, dashboard Kibana đã tồn tại.
Việc thật là (a) thêm test canh giữ khai báo, (b) chính thức hoá dashboard làm cơ chế đo liên tục (US3, không viết code mới).

## Luồng happy-case đã rà soát

1. 7 manifest khai đủ `availability 99.9%`, `max-5xx-ratio 0.1%`, `p95/p99` — 6 service `internal-service-api` (150/500 ms), `bff` `client-facing-bff` (300/800 ms); không service nào cần `slos.justification`.
2. `PlatformSloDefaults` trong test phản chiếu hiến chương Principle VIII — đã đối chiếu `.specify/memory/constitution.md` dòng 167–171: `300/800 ms`, `150/500 ms`, `99.9%`, `0.1%` — khớp.
3. `identity` có `classification: internal-service-api` (dòng từng thiếu, test mới bắt được lúc viết spec) — còn nguyên.
4. Dashboard `SLO vận hành hằng ngày — 7 service` (8 panel) đo error-rate/p95/p99 từ `traces-generic.otel-default*`, Availability suy ra `100% − error-rate`.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — tắt/bật service phía sau rồi bấm Postman

Dựng stack: `docker compose -f docker-compose.local.yml up -d --wait gateway-api bff-api products-api baskets-api orders-api elasticsearch kibana otel-collector` (kéo theo identity-api và DB).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**; chạy `00 - Xác thực & phân quyền (Get Token) → 01`, rồi folder
**`21 - Đo SLO trên Elasticsearch (dashboard SLO)`** bằng **Runner**: bước 01 → 03 khi `baskets-api` BẬT; **TẮT** `baskets-api` rồi chạy 04 → 07; **BẬT** lại rồi chạy 08.
Các bước ES dùng đúng phép tính của dashboard (error-rate, p95, p99 từ `traces-generic.otel-default*`) nhưng chỉ đếm span `kind: Server` từ mốc thời gian của bước 02, nên không lẫn dữ liệu cũ.

**Công tắc** (hạ tầng, không sửa mã): `docker compose -f docker-compose.local.yml stop baskets-api` / `start baskets-api`.

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-27)** |
|---|---|---|---|---|
| Điều kiện — dashboard có trong Kibana *(ngoại lệ: import bằng `curl`, Postman không đính kèm file gọn)* | `curl -X POST "localhost:5601/api/saved_objects/_import?overwrite=true" -H "kbn-xsrf: true" --form file=@docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson` | `21` bước 01 | Dashboard `SLO vận hành hằng ngày — 7 service` tồn tại | Import `successCount: 3`; bước 01 `200`, đúng tên |
| US3-KB1/bất biến 1, 2 — số đo thật khi khoẻ | **BẬT** `baskets-api` (mặc định) | `21` bước 02, 03 | Có request thật, 0 lỗi 5xx, p95 ≤ 300 ms, p99 ≤ 800 ms (ngưỡng manifest `bff`) | `n=1`, `5xx=0`, **p95 = p99 = 15–19 ms** |
| US3-KB2/bất biến 4/FR-007 — suy giảm hiện ngay | **TẮT** `baskets-api` | `21` bước 04, 05 | 5xx > 0 và p95 vượt ngưỡng, không thu thập lại thủ công | `GET /bff/basket` `504` sau 3 giây; đo lại cùng truy vấn: `n=2`, **`5xx=1`, p95 = 2858 ms, p99 = 2978 ms** (vượt 300/800 ms) |
| Bất biến 3/FR-006 — service không có traffic | (không có) — Orders.Api không được gọi | `21` bước 06 | "Không có dữ liệu" | **Khác kỳ vọng**: `5` span Server nhưng **0** ngoài `/health/*` — dashboard sẽ tính "0 % lỗi, ~1 ms" từ span probe (xem QA_Debt) |
| Bất biến 3 — khoảng thời gian trước khi hệ thống tồn tại | (không có) — cả năm 2020 | `21` bước 07 | 0 span | `0` — đúng |
| **BẬT** lại — phục hồi | `start baskets-api` | `21` bước 08 | `200` | Lần đầu `200` sau ~3 giây (kết nối lạnh) rồi `200` nhanh (~20–140 ms). Chạy cả folder khi TẮT: chỉ bước 08 đỏ; khi BẬT: chỉ 3 assertion của bước 04/05 đỏ (đúng, vì cần baskets TẮT) |
| Bước 2 quickstart — cơ chế bảo vệ khai báo có chặn thật *(ngoại lệ: sửa file manifest, không có công tắc chạy)* | Đổi `p95: 150ms → 50ms` ở dòng 37 của `services/orders/src/Orders.Api/service-manifest.yaml`, không kèm justification; `dotnet test tests/ServiceManifestSloConventionTests`; `git checkout --` file đó | (không có) | Đỏ đúng service `orders` | Đúng 1 đỏ (`SloDefaultComplianceTests … "orders"`, thông báo nêu `internal-service-api`, `p95=150ms` và thiếu `slos.justification`), 28 test còn lại xanh; revert → 29/29 xanh, `git status` sạch |
| Dọn dẹp | `start baskets-api` | (không có) | Không dữ liệu dư | Đã bật lại; dashboard nằm trong Kibana cục bộ, không ảnh hưởng repo |

Để tự xem dashboard: mở `http://localhost:5601/app/dashboards#/view/e2e06ff5-9cdf-4bea-acc8-5fd60ce26170`, time range **Last 24 hours**.

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| SC-001 — discovery đúng 7 service | [`SloDeclarationTests.cs:24`](../../tests/ServiceManifestSloConventionTests/SloDeclarationTests.cs#L24) — `Discovery_FindsExactlyTheSevenExpectedServices` | `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~SloDeclarationTests` |
| FR-001/US1 — đủ 4 giá trị, không rỗng, không placeholder | [`:48`](../../tests/ServiceManifestSloConventionTests/SloDeclarationTests.cs#L48) — `EveryService_DeclaresAllFourSloValues_NonEmptyAndNotPlaceholder` (7 ca) | (lệnh như trên) |
| FR-002 — classification hợp lệ | [`:80`](../../tests/ServiceManifestSloConventionTests/SloDeclarationTests.cs#L80) — `EveryService_HasAKnownClassification` (7 ca) | (lệnh như trên) |
| SC-001 bất biến 6 — tên khai báo khớp thư mục | [`:107`](../../tests/ServiceManifestSloConventionTests/SloDeclarationTests.cs#L107) — `EveryService_DeclaredNameMatchesItsDirectory` (7 ca) | (lệnh như trên) |
| FR-002/FR-003/US2 — khớp mặc định hoặc có justification | [`SloDefaultComplianceTests.cs:29`](../../tests/ServiceManifestSloConventionTests/SloDefaultComplianceTests.cs#L29) — `EveryService_MatchesPlatformDefault_OrDocumentsAJustifiedAlternative` (7 ca) | `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~SloDefaultComplianceTests` |

**Kết quả lượt QA này (2026-09-27)**: `ServiceManifestSloConventionTests` **29/29 PASS** (1 discovery + 7×4 ca), trước và sau khi dịch comment.

## Kết luận

**PASS kèm ghi chú nghiêm trọng.** 4 nguồn nhất quán với nhau và với mã/manifest thật; 29/29 test xanh; cơ chế bảo vệ manifest chặn đúng (mutate → đỏ đúng service → revert); hằng số mặc định khớp hiến chương. Nửa "khai báo" (US1/US2) đạt đầy đủ.
Nửa "đo liên tục" (US3) nhạy với suy giảm thật — Postman + Elasticsearch đo được `5xx`, p95 từ 15 ms lên 2858 ms ngay khi `baskets-api` tắt — nhưng có ghi chú: (1) span health-probe khiến service idle hiện "0 % lỗi / ~1 ms" thay vì "không có dữ liệu" (vi phạm FR-006/SC-005 ở trường hợp thật, bước 06);
(2) công thức đếm mọi loại span thay vì chỉ `kind: Server`, lệch rõ ở Gateway/BFF; (3) ngưỡng dashboard là chữ cứng trong tên cột, không test nào giữ khớp với manifest. Chi tiết và hướng vá: [QA_Debt.md](QA_Debt.md) mục 021.
