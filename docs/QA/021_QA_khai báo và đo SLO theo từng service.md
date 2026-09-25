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

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| SC-001 — discovery đúng 7 service | [`SloDeclarationTests.cs:24`](../../tests/ServiceManifestSloConventionTests/SloDeclarationTests.cs#L24) — `Discovery_FindsExactlyTheSevenExpectedServices` | `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~SloDeclarationTests` |
| FR-001/US1 — đủ 4 giá trị, không rỗng, không placeholder | [`:48`](../../tests/ServiceManifestSloConventionTests/SloDeclarationTests.cs#L48) — `EveryService_DeclaresAllFourSloValues_NonEmptyAndNotPlaceholder` (7 ca) | (lệnh như trên) |
| FR-002 — classification hợp lệ | [`:80`](../../tests/ServiceManifestSloConventionTests/SloDeclarationTests.cs#L80) — `EveryService_HasAKnownClassification` (7 ca) | (lệnh như trên) |
| SC-001 bất biến 6 — tên khai báo khớp thư mục | [`:107`](../../tests/ServiceManifestSloConventionTests/SloDeclarationTests.cs#L107) — `EveryService_DeclaredNameMatchesItsDirectory` (7 ca) | (lệnh như trên) |
| FR-002/FR-003/US2 — khớp mặc định hoặc có justification | [`SloDefaultComplianceTests.cs:29`](../../tests/ServiceManifestSloConventionTests/SloDefaultComplianceTests.cs#L29) — `EveryService_MatchesPlatformDefault_OrDocumentsAJustifiedAlternative` (7 ca) | `dotnet test tests/ServiceManifestSloConventionTests --filter FullyQualifiedName~SloDefaultComplianceTests` |

**Kết quả lượt QA này (2026-09-24)**: `ServiceManifestSloConventionTests` **29/29 PASS** (1 discovery + 7×4 ca), trước và sau khi dịch comment.

### Thủ công

| Bước (quickstart) | Cách làm | Kỳ vọng theo tài liệu | **Đã quan sát** |
|---|---|---|---|
| Bước 2 — cơ chế bảo vệ có chặn thật | Đổi `p95: 150ms → 50ms` trong `services/orders/src/Orders.Api/service-manifest.yaml`, không kèm justification; chạy test; `git checkout --` | Đỏ đúng service `orders` | Đúng 1 đỏ (`SloDefaultComplianceTests … "orders"`), 28 test còn lại xanh; revert → 29/29 xanh, `git status` sạch |
| Import dashboard vào Kibana 9.4.4 | `POST /api/saved_objects/_import` file `.ndjson` | Import thành công | `successCount: 3` (2 data view + dashboard `e2e06ff5-…`); công thức thật: `count(kql='…status_code >= 500') / count()`, `percentile(duration, 95\|99) / 1000000` |
| Bước 3–4 — dashboard đối chiếu thực tế/ngưỡng | Trình duyệt tích hợp bị từ chối `localhost:5601` nên không render được; thay bằng truy vấn Elasticsearch đúng các phép tính đó | Số đo phản ánh suy giảm thật | Bucket 20 phút `Bff.Api` lúc ngắt `baskets-api`: `n=167, err=91.6 %, p95=3270 ms`, ngay sau đó `err=0.8 %` — đo nhạy đúng (bất biến 4 / US3-KB2 / FR-007) |
| Bước 5 — không có dữ liệu | Khoảng thời gian 2020 (trước khi hệ thống tồn tại) | "Không có dữ liệu" | 0 hit, "No results found" — đúng; nhưng service **idle thật** cho kết quả KHÁC kỳ vọng (xem QA_Debt) |

Để tự render dashboard: mở `http://localhost:5601/app/dashboards#/view/e2e06ff5-9cdf-4bea-acc8-5fd60ce26170`, time range **Last 24 hours**.

## Kết luận

**PASS kèm ghi chú nghiêm trọng.** 4 nguồn nhất quán với nhau và với mã/manifest thật; 29/29 test xanh; cơ chế bảo vệ manifest chặn đúng (mutate → đỏ đúng service → revert); hằng số mặc định
khớp hiến chương. Nửa "khai báo" (US1/US2) đạt đầy đủ. Nửa "đo liên tục" (US3) nhạy với suy giảm thật nhưng có ghi chú: (1) span health-probe khiến service idle hiện "0 % lỗi / ~1 ms"
thay vì "không có dữ liệu" (vi phạm FR-006/SC-005 ở trường hợp thật); (2) công thức đếm mọi loại span thay vì chỉ `kind: Server`, lệch rõ ở Gateway/BFF (19.19 % vs 11.02 %);
(3) ngưỡng dashboard là chữ cứng trong tên cột, không test nào giữ khớp với manifest. Chi tiết và hướng vá: [QA_Debt.md](QA_Debt.md) mục 021.
Lưu ý môi trường: đã import dashboard vào Kibana cục bộ của stack QA (không ảnh hưởng repo).
