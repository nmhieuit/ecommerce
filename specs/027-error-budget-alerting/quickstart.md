# Quickstart: Xác thực chính sách ngân sách lỗi và cảnh báo (SCRUM-35)

**Feature**: [spec.md](./spec.md) | Lặp lại đúng 3 kịch bản kiểm thử của Jira SCRUM-35.

Bất biến được kiểm tra: [contracts/error-budget-alert-rules-contract.md](./contracts/error-budget-alert-rules-contract.md)
(5–10), [contracts/chaos-fault-injection-contract.md](./contracts/chaos-fault-injection-contract.md) (3, 5).

## Chuẩn bị

1. `.env` có `KIBANA_ENCRYPTION_KEY` (≥ 32 ký tự; `cp .env.example .env` là đủ cho local) và đặt
   `CHAOS_ALLOW_FAULT_INJECTION=true` **chỉ trong lúc diễn tập**.
2. Dựng stack có publish port:

   ```powershell
   ./scripts/local-up.ps1
   ```

3. Tạo index sự kiện (một lần, trước khi import rule — xem `docs/kibana-quan-sat-he-thong/07-canh-bao-ngan-sach-loi.md`):

   ```powershell
   curl.exe -X PUT "http://localhost:9200/slo-error-budget-events" -H "Content-Type: application/json" -d '{\"mappings\":{\"properties\":{\"@timestamp\":{\"type\":\"date\"},\"service\":{\"type\":\"keyword\"},\"budget\":{\"type\":\"keyword\"},\"event\":{\"type\":\"keyword\"}}}}'
   ```

4. Import rule + connector và dashboard (Kibana `http://localhost:5601`):

   ```powershell
   curl.exe -X POST "http://localhost:5601/api/saved_objects/_import?overwrite=true" -H "kbn-xsrf: true" --form "file=@docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson"
   curl.exe -X POST "http://localhost:5601/api/saved_objects/_import?overwrite=true" -H "kbn-xsrf: true" --form "file=@docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson"
   ```

5. Kibana nhập rule ở trạng thái **disabled**: vào **Stack Management → Rules**, lọc tag
   `slo-error-budget`, bật (Enable) cả 4 rule.

**Kết quả mong đợi**: 4 rule enabled, chu kỳ 5 phút, sau ~5 phút mỗi rule có lượt chạy mới (rule nào kẹt
`pending` thì Disable rồi Enable lại — xem `docs/kibana-quan-sat-he-thong/alerts/README.md`); dashboard `SLO vận hành hằng ngày — 7 service` có
nhóm "Ngân sách lỗi tháng này" ở trên cùng.

## Kịch bản 1 — Tạo lỗi tổng hợp tới khi cạn ngân sách tháng, cảnh báo bắn đúng mốc

1. Lấy tổng số span tháng này của `Orders.Api` (`N`) từ bảng mức tiêu hao trên dashboard hoặc truy vấn
   Elasticsearch. Ngân sách 5xx cạn khi số 5xx ≈ `N / 999`.
2. Gửi lỗi tổng hợp theo từng đợt — mỗi đợt khoảng 1/4 lượng cần thiết — tới Orders.Api (port 5041):

   ```powershell
   1..$batch | ForEach-Object { curl.exe -s -o NUL -H "X-Chaos-Fault: 5xx" http://localhost:5041/health/live }
   ```

3. Sau mỗi đợt, chờ tối đa 5 phút và xem dashboard.

**Kết quả mong đợi**:
- Khi mức tiêu hao `availability`/`error-rate` của `Orders.Api` vượt 50%, 75%, rồi 100%, bảng alert
  active hiện lần lượt từng mốc trong vòng ≤ 5 phút — không cần ai chủ động tra số (bất biến 6, 8).
- Các ngân sách `latency-p95`/`latency-p99` và 6 service còn lại giữ nguyên trạng thái (spec User
  Story 2 kịch bản 5).
- Alert vẫn active ở các lần chạy sau chừng nào mức tiêu hao còn trên mốc (bất biến 6).
- Discover trên `traces-generic.otel-default*` thấy các span `Orders.Api` với
  `attributes.http.response.status_code = 500` (contract tiêm lỗi, bất biến 5).
- Index `slo-error-budget-events` có document `event: exhausted` cho `Orders.Api`, và bảng
  "cạn — ưu tiên độ tin cậy" hiện `Orders.Api` (bất biến 8, 9).

Tắt diễn tập: đặt lại `CHAOS_ALLOW_FAULT_INJECTION=false` rồi chạy lại `./scripts/local-up.ps1`.

## Kịch bản 2 — Đọc chính sách đã viết

Mở `service-manifest.yaml` của từng service trong 7 service, xem khối `error-budget-policy`.

**Kết quả mong đợi**: trả lời được "ai dừng", "dừng cái gì", "khi nào được tiếp tục" chỉ từ manifest
(SC-004); hình dạng đúng [contracts/error-budget-policy-manifest-shape.md](./contracts/error-budget-policy-manifest-shape.md).
Tự động hoá phần này:

```powershell
dotnet test tests/ServiceManifestSloConventionTests
```

## Kịch bản 3 — Cảnh báo tới nơi người vận hành thật sự nhìn thấy

Mở dashboard SLO hằng ngày như mọi ngày, **không** vào Stack Management.

**Kết quả mong đợi**: alert của Kịch bản 1 hiện ngay ở nhóm panel trên cùng, đủ service, ngân sách,
mốc và mức tiêu hao hiện tại (SC-003).

## Kiểm tra không phá cái đã có

- 5 bất biến của `specs/021-declare-service-slos/contracts/continuous-measurement-contract.md` vẫn
  đúng trên dashboard đã sửa (bất biến 10).
- `dotnet test shared/ServiceDefaults.UnitTests` xanh; với `CHAOS_ALLOW_FAULT_INJECTION=false`, gửi
  header `X-Chaos-Fault: 5xx` vẫn nhận response bình thường (contract tiêm lỗi, bất biến 2).

## Kiểm tra theo thời gian (không làm được trong một phiên)

- Hồi phục trong tháng sau 3 ngày đạt SLO (bất biến 9) và giữ đóng băng qua ranh giới tháng: quan sát
  thật theo ngày, ghi kết quả vào tài liệu QA của tính năng.
