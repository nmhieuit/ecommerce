# QA: Diễn tập sự cố thật và phản ứng on-call

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát

1. **US1 — tiêm sự cố mù, chỉ bằng cấu hình**: `scripts/incident-drill.ps1 -Start`
   - từ chối chạy khi cờ `CHAOS_ALLOW_FAULT_INJECTION` tắt;
   - bốc thăm service (1/7), loại lỗi (A đích sai / B cạn pool chỉ gateway / C 5xx của 027), thời điểm 0–30 phút;
   - niêm phong vào `.incident-drill/` kèm SHA-256;
   - tạo lại cả 7 container, chỉ đích nhận cấu hình sai;
   - `-Reveal` kiểm băm.
2. **US2 — phát hiện nhanh và triage**: rule Kibana `incident-fast-detection` (5 phút; gateway chỉ xét
   5xx), bảng "Phát hiện nhanh" trên dashboard SLO hằng ngày, quy trình SEV1–3 + Kibana Case trong
   `docs/dien-tap-chaos-engineering/README.md`.
3. **US3 — bản ghi sự cố**: mẫu `mau-ban-ghi-su-co.md`, các mốc tách bạch.
4. **US4 — xác nhận khôi phục**: truy vấn theo phút, 15 phút liên tục đạt SLO, rule hết active.
5. Tải nền: `-Load` (folder Postman 00 + 26 + 28, token mỗi 30 phút hoặc khi gặp 401 / identity bị tạo
   lại, nghỉ 1000 ms).

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — đổi cờ trong `.env`, chạy script ở chế độ không mù, xem dashboard

**Dựng stack**: `docker compose -f docker-compose.local.yml up -d --wait` (cần `KIBANA_ENCRYPTION_KEY`
trong `.env`).

**Import Kibana**: import
[`alerts/incident-fast-detection-rule.ndjson`](../kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson)
và [`dashboards/slo-van-hanh-hang-ngay.ndjson`](../kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson),
rồi Enable rule tag `incident-fast-detection` ([`alerts/README.md`](../kibana-quan-sat-he-thong/alerts/README.md)).

**Postman**: folder **`26 - Ngân sách hiệu năng luồng trọng yếu (browse → giỏ → checkout → đơn)`** và
**`28 - Diễn tập sự cố: tải nền bổ sung (parties, identity)`**, chạy liên tục bằng
`./scripts/incident-drill.ps1 -Load` ở một terminal riêng.

**Công tắc cấu hình**:
- **Bật**: thêm `CHAOS_ALLOW_FAULT_INJECTION=true` vào `.env`. Script tự tạo lại container khi tiêm.
- **Khôi phục mặc định**: xoá dòng đó khỏi `.env` rồi `docker compose -f docker-compose.local.yml up -d --wait`.
  Cờ đổi nên cả 7 container được tạo lại không kèm override. Chỉ chạy lại compose mà không đổi cờ thì
  loại C không được gỡ.

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-10-01/02)** |
|---|---|---|---|---|
| Bất biến 1 — cờ tắt | Mặc định (không có dòng cờ) | (không có) — `-Start` | `exit 1`, không có `.incident-drill/`, uptime không đổi | Đúng |
| Mức nền khoẻ | Mặc định; `-Load` chạy ≥ 10 phút | 26 + 28 (qua `-Load`) | Cả 7 service có span; rule 0 alert | 0 lỗi 5xx, p95 ≤ 133 ms, truy vấn rule 0 hàng (2 cửa sổ 5 phút) |
| Loại A — products/bff/gateway | Cờ bật; `-Start -Service <svc> -FaultType A -DelaySeconds 0` | 26 + 28 | Chỉ đích nhận biến sai; đích trả 5xx | 5xx: products 38/56, bff 49/243 (downstream `OrdersApi`), gateway 93/226 |
| Loại B — gateway | Cờ bật; `-Service gateway-api -FaultType B` | 26 + 28 | Gateway 5xx | 45.1% span gateway 5xx (BFF 18.5%) |
| Loại C — parties, identity | Cờ bật; `-FaultType C` | 26 + 28 | 5xx ≈ tỷ lệ niêm phong; dừng gửi khi đích được tạo lại | parties 31.8% (46%), identity 33.3% (39%); dừng sau khi khôi phục |
| Tải nền tự lấy token | (như loại C trên identity) | 26 + 28 | Token mới sau khi identity được tạo lại | identity tạo lại 10:39:28 → token mới 10:39:55 |
| US2 — rule bắn, bảng hiện | Cờ bật; loại A trên orders | 26 + 28 | Alert `Orders.Api` ≤ 10 phút; bảng dashboard hiện | Bắn 11:02 (tiêm 11:00:25); bảng hiện (ảnh chụp trình duyệt) |
| US2 — Case theo hướng dẫn | (không đổi) | (không có) — Kibana UI | Tiêu đề `[SEVn]`, severity ánh xạ, tag, comment mốc | Case `ab9ea3ce-…`: `[SEV2]`, `high`, tag `incident-drill`/`sev2`, 2 comment |
| US4 — 15 phút đạt SLO | Khôi phục mặc định | 26 + 28 | Chuỗi 15 phút `dat = true`, rule hết active | Alert recovered 11:32; giải quyết 12:15 (chuỗi 12:01–12:15) |
| Reveal | (không đổi) | (không có) — `-Reveal -RunId` | Băm khớp; xoá override | Khớp ở mọi lần chạy |

### Tự động

Không có test tự động. Đây là sai lệch Nguyên tắc III do người dùng chốt ("đây là diễn tập, không có
code mới"), hạn tới khi SCRUM-37 xong; xem
[`specs/028-incident-oncall-drill/plan.md`](../../specs/028-incident-oncall-drill/plan.md), mục
Complexity Tracking. Script, rule và mẫu bản ghi chỉ được kiểm chứng bằng bảng thủ công ở trên.

## Kết luận

**PASS kèm ghi chú.** Cả ba loại hỏng hóc tạo được lỗi thật chỉ bằng cấu hình. Rule bắn trong khoảng
1,5 phút cho service bị tiêm, bảng hiện trên dashboard hằng ngày, Case và quy trình làm theo được, và
truy vấn 15 phút cho ra mốc giải quyết rõ ràng.

Ghi chú:
1. Nhiễu khởi động nguội 5–7 phút làm rule bắn cho cả 7 service ở lần chạy đầu.
2. Môi trường local chậm từng đợt: rule bắn lại cho service không bị tiêm, và khôi phục mất ~48 phút.
3. Lỗi lan sang BFF/gateway.
4. Buổi diễn tập mù chưa chạy.
5. Không có test tự động.

Chi tiết: [QA_Debt.md](QA_Debt.md) mục 028.
