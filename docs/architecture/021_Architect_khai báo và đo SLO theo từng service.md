# Kiến trúc: Khai báo và đo lường liên tục SLO theo từng service trong service manifest

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-29 ("[RESILIENCE-4] Declare per-service SLOs in the service manifest"), đặc
tả tại [`specs/021-declare-service-slos/`](../../specs/021-declare-service-slos/). 3 quyết định kiến
trúc (cộng 1 quyết định "Xác nhận hiện trạng") ở
[`research.md`](../../specs/021-declare-service-slos/research.md).

**Trạng thái xác minh**: 17 task, toàn bộ `[X]`. Test-First ở đây cho ra 2 kết quả **khác dự tính ban
đầu** — cả hai đều được ghi lại trung thực thay vì âm thầm sửa lại giả định cho khớp kết luận có sẵn
(mục 2).

## 1. Phát hiện quan trọng nhất: phần lớn công việc "đã làm rồi", chỉ chưa được bảo vệ

`research.md` Quyết định 0 rà soát hiện trạng **trước khi** thiết kế bất kỳ gì, phát hiện:

- Cả 7 `service-manifest.yaml` **đã có sẵn** khối `slos:` đầy đủ 4 giá trị (độ trễ p95/p99, tỷ lệ lỗi,
  độ khả dụng) — từ [018](018_Architect_secrets%20qua%20cluster%20secret%20store.md)/019 trở về
  trước.
- Nhưng **không có bất kỳ test nào** đọc lại các file này (đã grep toàn bộ `tests/` cho
  `service-manifest`/`slo`/`Slo`/`SLO`, không có kết quả) — nghĩa là 1 service mới thiếu SLO, hoặc 1
  giá trị bị sửa lệch mặc định, sẽ không ai phát hiện.
- Dashboard Kibana đo 3/4 chỉ số (Error-rate, Latency p95, Latency p99 từ traces OTel thật, Availability
  suy ra xấp xỉ từ `100% − Error-rate`) **đã tồn tại và đã xác minh khớp dữ liệu thô** — dựng ở
  [`docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md`](../kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md).

**Kết luận đã chốt**: phần việc thật của SCRUM-29 không phải "khai báo" hay "xây dashboard" từ đầu, mà
là (a) làm cho khai báo có tính **tự bảo vệ** (test canh giữ), và (b) **chính thức hoá** dashboard đã
có thành 1 phần được công nhận, có hợp đồng, của tính năng — thay vì 1 tài liệu vận hành rời rạc không
ai đảm bảo còn đúng.

## 2. Test-First cho ra 2 kết quả khác dự tính — đúng giá trị của việc viết test trước

### US1 — kỳ vọng PASS ngay, thực ra FAIL 1 lần

`SloDeclarationTests` (T007) kỳ vọng PASS ngay lập tức (cả 7 manifest "đã đúng sẵn" theo Quyết định
0). Chạy thật: **FAIL** — `services/identity/src/Identity.Api/service-manifest.yaml` thiếu hẳn
`service.classification`, 1 trường mà `data-model.md` xếp là tiền đề để đối chiếu SLO với mặc định.
Đã sửa: thêm `classification: internal-service-api` (khớp đúng 4 giá trị SLO hiện có của `identity`).
Chạy lại: 22/22 PASS.

### US2 — kỳ vọng FAIL vì `bff`, thực ra PASS ngay 7/7

Giả định ban đầu (cả ở `research.md` Quyết định 0 lẫn Quyết định 2 lúc soạn thảo): `bff` mang ngân
sách latency nới hơn (p95 300ms/p99 800ms so với 150ms/500ms của 6 service kia) nên "có 1 ngoại lệ"
cần `slos.justification`. `SloDefaultComplianceTests` (T008) chạy thật: **PASS 7/7 ngay lần đầu**. Lý
do: `bff` mang `classification: client-facing-bff` và khớp đúng hồ sơ mặc định của **chính phân loại
đó** — constitution Principle VIII định nghĩa 2 hồ sơ mặc định song song, bình đẳng (`client-facing-bff`
và `internal-service-api`), không cái nào là "ngoại lệ" của cái kia. `bff` chỉ đơn giản đúng hồ sơ của
đúng phân loại nó mang.

**Hệ quả trực tiếp**: đã sửa lại `research.md` Quyết định 0/2 và `contracts/service-manifest-slo-shape.md`
cho đúng, và **không** thêm `slos.justification` giả vào `bff` chỉ để "có 1 ví dụ" — làm vậy sẽ là dữ
liệu không trung thực, vi phạm đúng tinh thần của tính năng ("ngân sách là cam kết thật"). Trường
`slos.justification` vẫn được giữ trong schema làm cơ chế phòng ngừa cho 1 ngoại lệ thật trong tương
lai (FR-003), hiện chưa có instance nào dùng.

## 3. User Story 3 — không viết code mới, chính thức hoá dashboard đã có (Quyết định 3)

Dashboard `SLO vận hành hằng ngày — 7 service` (đã build ở phiên brainstorm/implementation trước,
xem [`06-dashboard-slo-van-hanh-hang-ngay.md`](../kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md))
được chính thức hoá làm cơ chế đo lường liên tục của tính năng này — không xây lại. Hợp đồng (bất biến
bắt buộc) được tách riêng vào `contracts/continuous-measurement-contract.md` để có 1 danh sách ổn định
đối chiếu được, không cần đọc lại nhật ký xây dựng chi tiết. `quickstart.md` lặp lại đúng 3 kịch bản
test của Jira như quy trình xác thực **định kỳ/thủ công**, không chặn PR — cùng logic đã dùng ở 019
cho cluster `kind` (hành vi runtime cần dữ liệu thật để quan sát, không phù hợp chạy trong mọi CI).

**Số liệu đối chiếu thật** (`T012`, truy vấn Elasticsearch thô cho `Orders.Api`, cửa sổ `now-24h`):

| Chỉ tiêu | Giá trị đo được | Ngưỡng | Kết quả |
|---|---|---|---|
| Tổng request | 9.237 | — | — |
| Error-rate | 0,0108% (1 lỗi 5xx) | ≤ 0,1% | Đạt |
| Latency p95 | 33,6 ms | ≤ 150 ms | Đạt |
| Latency p99 | 393,4 ms | ≤ 500 ms | Đạt, **gần ngưỡng** |

**Bằng chứng "ngân sách bị tiêu hao thể hiện rõ"** (`T013`): thay vì sửa code/rebuild container (tránh
ảnh hưởng phiên khác dùng chung hạ tầng Docker), gửi 300 request đồng thời tới `/health/ready` của
`orders` để tạo tranh chấp tài nguyên thật. Baseline 15 phút trước: p95=157,9ms/p99=440,1ms. Trong 5
phút **ngay sau** khi tạo tải (323 request, tăng mạnh so với ~136/15 phút bình thường): p95 =
**14.975,7 ms** / p99 = **18.519,2 ms** — tăng khoảng **95 lần**, vượt xa ngân sách 150ms/500ms. Không
cần "hoàn tác" vì không có thay đổi cấu hình/code nào — tải chỉ là traffic tạm thời, tự hết sau khi
burst kết thúc.

**Bằng chứng "không có dữ liệu ≠ 0% lỗi"** (`T014`): truy vấn Elasticsearch thô cho `Orders.Api` trong
khoảng thời gian **trước khi môi trường demo tồn tại** → `hits.total.value = 0` — khác hẳn "có traffic
nhưng 0 lỗi" (total > 0, err = 0). Đây chính cơ chế mà tài liệu dashboard đã xác minh cho Lens Table: 0
tài liệu → dòng biến mất khỏi bảng (terms aggregation), không hiển thị `0%` gây hiểu lầm.

## 4. Sơ đồ khai báo

```
service-manifest.yaml (7 file)
  service.classification: internal-service-api | client-facing-bff
  slos.{latency.p95, latency.p99, error-rate.max-5xx-ratio, availability}
  slos.justification (tuỳ chọn — hiện chưa service nào cần dùng)
        │
        ▼ parse bằng YamlDotNet (tests/ServiceManifestSloConventionTests)
  ┌─────────────────────┬──────────────────────────────┐
  ▼                      ▼
SloDeclarationTests    SloDefaultComplianceTests
(đủ 4 giá trị,         (khớp hồ sơ mặc định theo
không placeholder,      classification, trừ khi có
classification hợp lệ)  slos.justification)
        │
        ▼ (US3, không phụ thuộc US1/US2 về implementation)
Dashboard Kibana "SLO vận hành hằng ngày — 7 service" (đã có, chính thức hoá)
  ← Elasticsearch traces-generic.otel-default*, cửa sổ rolling 24h
```

## 5. Giới hạn phạm vi đã biết

- **Không có alert rule tự động** — dashboard chỉ hỗ trợ "tra cứu được" (đúng yêu cầu spec FR-004/
  FR-005), không tự động cảnh báo khi vượt ngân sách. Việc đó thuộc `SCRUM-35`
  (`docs/superpowers/specs/2026-09-08-dashboard-slo-van-hanh-design.md`), ngoài phạm vi đặc tả này.
- **Không có test CI gọi Elasticsearch thật để assert dashboard luôn đúng** — cân nhắc rồi loại, cùng
  lý do 019 loại phương án chạy cluster K8s thật trong mọi CI (chi phí/độ không ổn định không tương
  xứng). Xác thực dashboard vẫn đúng là thao tác định kỳ/thủ công qua `quickstart.md`.
- **`slos.justification` chưa có instance thật nào** — cơ chế đã sẵn sàng trong schema nhưng chưa
  được thực chiến; lần đầu 1 service thật cần ngoại lệ mới là phép thử thật cho cơ chế này.
- **p99 của `Orders.Api` đo được (393,4ms) gần ngưỡng khai báo (500ms)** ngay ở điều kiện vận hành
  bình thường của môi trường demo — đáng theo dõi tiếp, không phải lỗi cần sửa ngay trong phạm vi tính
  năng này.

## 6. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/021-declare-service-slos-component.drawio`](../diagrams/021-declare-service-slos-component.drawio)
- Sơ đồ luồng nghiệp vụ: [`docs/diagrams/021-declare-service-slos-flow-nghiep-vu.drawio`](../diagrams/021-declare-service-slos-flow-nghiep-vu.drawio)
- Sơ đồ trình tự: [`docs/diagrams/021-declare-service-slos-sequence.drawio`](../diagrams/021-declare-service-slos-sequence.drawio)

## 7. Tham khảo thêm

`docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md` giờ tham chiếu ngược lại đặc
tả này làm hợp đồng đo lường liên tục chính thức (T015) — đọc file đó để biết cách vận hành/đọc
dashboard, không lặp lại ở đây.
