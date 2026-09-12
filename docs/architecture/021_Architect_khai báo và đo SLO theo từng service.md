# Kiến trúc: Khai báo và đo lường liên tục SLO theo từng service trong service manifest

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-29 ("[RESILIENCE-4] Declare per-service SLOs in the service manifest"), đặc
tả tại [`specs/021-declare-service-slos/`](../../specs/021-declare-service-slos/). 3 quyết định kiến
trúc (cộng 1 quyết định "Xác nhận hiện trạng") ở
[`research.md`](../../specs/021-declare-service-slos/research.md).

**Trạng thái xác minh**: 17 task, toàn bộ `[X]`. Test-First ở đây cho ra 2 kết quả khác dự tính ban
đầu, cả hai đều được ghi lại trung thực thay vì âm thầm sửa lại giả định cho khớp kết luận có sẵn —
xem [technical-debt.md](technical-debt.md).

## 1. Phát hiện quan trọng nhất: phần lớn công việc "đã làm rồi", chỉ chưa được bảo vệ

`research.md` Quyết định 0 rà soát hiện trạng **trước khi** thiết kế bất kỳ gì, phát hiện:

- Cả 7 `service-manifest.yaml` **đã có sẵn** khối `slos:` đầy đủ 4 giá trị (độ trễ p95/p99, tỷ lệ lỗi,
  độ khả dụng) — từ [018](018_Architect_secrets%20qua%20cluster%20secret%20store.md)/019 trở về
  trước.
- Nhưng **không có bất kỳ test nào** đọc lại các file này — nghĩa là 1 service mới thiếu SLO, hoặc 1
  giá trị bị sửa lệch mặc định, sẽ không ai phát hiện.
- Dashboard Kibana đo 3/4 chỉ số (Error-rate, Latency p95, Latency p99 từ traces OTel thật, Availability
  suy ra xấp xỉ từ `100% − Error-rate`) **đã tồn tại và đã xác minh khớp dữ liệu thô** — dựng ở
  [`docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md`](../kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md).

**Kết luận đã chốt**: phần việc thật của SCRUM-29 không phải "khai báo" hay "xây dashboard" từ đầu, mà
là (a) làm cho khai báo có tính **tự bảo vệ** (test canh giữ), và (b) **chính thức hoá** dashboard đã
có thành 1 phần được công nhận, có hợp đồng, của tính năng — thay vì 1 tài liệu vận hành rời rạc không
ai đảm bảo còn đúng.

## 2. User Story 3 — không viết code mới, chính thức hoá dashboard đã có (Quyết định 3)

Dashboard `SLO vận hành hằng ngày — 7 service` (đã build ở phiên brainstorm/implementation trước,
xem [`06-dashboard-slo-van-hanh-hang-ngay.md`](../kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md))
được chính thức hoá làm cơ chế đo lường liên tục của tính năng này — không xây lại. Hợp đồng (bất biến
bắt buộc) được tách riêng vào `contracts/continuous-measurement-contract.md` để có 1 danh sách ổn định
đối chiếu được, không cần đọc lại nhật ký xây dựng chi tiết. `quickstart.md` lặp lại đúng 3 kịch bản
test của Jira như quy trình xác thực **định kỳ/thủ công**, không chặn PR — cùng logic đã dùng ở 019
cho cluster `kind` (hành vi runtime cần dữ liệu thật để quan sát, không phù hợp chạy trong mọi CI). Số
liệu đối chiếu thật (Orders.Api, load-test chứng minh ngân sách bị tiêu hao, bằng chứng "không có dữ
liệu ≠ 0% lỗi"): xem [technical-debt.md](technical-debt.md).

## 3. Sơ đồ khai báo

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

## 4. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/021-declare-service-slos-component.drawio`](../diagrams/021-declare-service-slos-component.drawio)
- Sơ đồ luồng nghiệp vụ: [`docs/diagrams/021-declare-service-slos-flow-nghiep-vu.drawio`](../diagrams/021-declare-service-slos-flow-nghiep-vu.drawio)
- Sơ đồ trình tự: [`docs/diagrams/021-declare-service-slos-sequence.drawio`](../diagrams/021-declare-service-slos-sequence.drawio)

## 5. Tham khảo thêm

`docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md` giờ tham chiếu ngược lại đặc
tả này làm hợp đồng đo lường liên tục chính thức — đọc file đó để biết cách vận hành/đọc dashboard,
không lặp lại ở đây.

2 kết quả Test-First khác dự tính (US1 fail 1 lần, US2 sai giả định ban đầu về `bff`), số liệu xác
thực thật, và giới hạn phạm vi đã biết (không có alert rule, chưa có test CI gọi Elasticsearch thật):
xem [technical-debt.md](technical-debt.md).
