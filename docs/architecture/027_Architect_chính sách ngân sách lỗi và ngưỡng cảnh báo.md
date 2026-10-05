# Kiến trúc: Chính sách ngân sách lỗi (error budget) và ngưỡng cảnh báo gắn với SLO từng service

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-35 ("[OPERATE-5] Define error-budget policy and alerting thresholds"), đặc tả
tại [`specs/027-error-budget-alerting/`](../../specs/027-error-budget-alerting/). 9 quyết định kiến
trúc, 5 điểm xác minh trên Kibana thật (V1–V5) và 3 "hệ quả" phát hiện lúc dựng ở
[`research.md`](../../specs/027-error-budget-alerting/research.md).

**Trạng thái xác minh**: test quy ước + unit test xanh; rule, dashboard và diễn tập làm cạn ngân sách
chạy trên Kibana/Elasticsearch 9.4.4 thật. Phần "hồi phục thật sau 3 ngày" cần quan sát nhiều ngày, còn
mở — xem [technical-debt.md](technical-debt.md).

## 1. Ba mảnh ghép, mỗi mảnh một nơi

| Mảnh | Nằm ở đâu | Ai đọc |
|---|---|---|
| **Chính sách** — 4 ngân sách (khả dụng 1%, 5xx 1%, vượt p95 5%, vượt p99 1%), tuần lịch UTC+7, mốc 50/75/100, "cạn" = bất kỳ ngân sách nào 100%, hệ quả và điều kiện hồi phục 3 ngày | Khối `error-budget-policy` trong 7 `service-manifest.yaml`, ngay sau `slos` | Con người; `ErrorBudgetPolicyTests` canh hình dạng |
| **Đo và cảnh báo** — 4 rule Kibana "Elasticsearch query" dạng ES\|QL, mỗi 5 phút | Kibana; export tại [`alerts/error-budget-rules.ndjson`](../kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson) | `ErrorBudgetRuleDefinitionTests` canh ngưỡng khớp manifest |
| **Nhìn thấy** — 3 panel trên cùng dashboard Ngân sách lỗi tuần của 021 | Discover session ES\|QL; export trong [`dashboards/ngan-sach-loi-tuan.ndjson`](../kibana-quan-sat-he-thong/dashboards/ngan-sach-loi-tuan.ndjson) | Người vận hành, mỗi ngày |

Ngưỡng độ trễ **chỉ có một nguồn**: `slos.latency` của manifest. Khối chính sách không chép lại; rule
chép tay vào ES|QL nhưng test đọc file export và đỏ ngay khi lệch (đã thử phá: đổi 300 ms → 350 ms, 3 test đỏ).

## 2. Vì sao ES|QL rule trên license Basic (Quyết định 1)

Kibana SLO + burn-rate cần Platinum; script ngoài Kibana lệch yêu cầu "quản lý trong Kibana". Một truy
vấn ES|QL tính được cả 4 ngân sách × 7 service từ chính index traces mà dashboard 021 dùng, nên số trên
alert và trên dashboard không bao giờ lệch nhau. Ba ràng buộc của Kibana 9.4.4 định hình truy vấn —
đều phát hiện bằng chạy thật, không có trong tài liệu Elastic:

1. Rule tự lọc `@timestamp` theo cửa sổ của nó → cửa sổ rule 7 ngày (14 ngày cho rule đóng băng),
   dòng `WHERE` cắt lại đúng từ thứ Hai 00:00 giờ Việt Nam.
2. Mã alert = giá trị **mọi** cột kết quả → kết quả chỉ giữ `service, budget`; % tiêu hao nằm ở bảng
   riêng, nếu không alert bị tạo lại mỗi 5 phút.
3. Mã alert chỉ lấy cột của lệnh `STATS` **cuối** → thêm `STATS ... BY service, budget` trước `KEEP`.

## 3. Trạng thái "cạn ngân sách — ưu tiên độ tin cậy" không lưu ở đâu cả (Quyết định 4)

```
rule error-budget-100 ──(alert (service,budget) chuyển sang active)──▶ connector Index
                                                                         │
                                                       slo-error-budget-events (append-only)
                                                                         │
traces-generic.otel-default* ──┐                                         │
                               ▼                                         ▼
               rule error-budget-frozen: FROM traces, events  →  theo (service, ngày UTC+7)
                 exhausted_at  = lần cạn gần nhất
                 last_bad_day  = ngày có traffic không đạt SLO gần nhất (ngày không traffic = đạt)
                 frozen ⇔ (hôm nay − MAX(ngày cạn, ngày xấu) − 1) < 3
```

Trạng thái được **tính lại** mỗi 5 phút từ dữ liệu, không có cờ nào phải bật/tắt bằng tay. Sự kiện chỉ
ghi khi alert chuyển sang active nên việc ngân sách đặt lại đầu tuần không gỡ đóng băng (FR-010).

## 4. Công cụ làm cạn ngân sách: `ChaosFaultInjectionMiddleware` (Quyết định 7)

Trong `shared/ServiceDefaults`, gắn ngay sau `CorrelationIdMiddleware` nên cả 7 service đều có. Hai lớp
chặn như 025: cấu hình `Chaos:AllowFaultInjection` (compose `CHAOS_ALLOW_FAULT_INJECTION`, mặc định
`false`) và header `X-Chaos-Fault: 5xx` từng request. Trả `500` và không chạy tiếp pipeline, nên lỗi đi
qua đúng đường OTel thật mà rule đếm. Đặt trước xác thực: chỉ có thể làm request thất bại, không mở được
quyền truy cập nào.

## 5. Điều kiện hạ tầng

Kibana Alerting bắt buộc `xpack.encryptedSavedObjects.encryptionKey`: compose truyền
`KIBANA_ENCRYPTION_KEY` (Vùng 2 của `.env.example`) qua `XPACK_ENCRYPTEDSAVEDOBJECTS_ENCRYPTIONKEY`.
Index `slo-error-budget-events` phải tạo trước khi import rule; import xong phải Enable lại 4 rule —
[`alerts/README.md`](../kibana-quan-sat-he-thong/alerts/README.md). Đã gặp thật: sau import, 2/4 rule kẹt
`pending` vì task chạy đúng lúc rule đang bị tắt giữa chừng import và Task Manager tự tắt task; gỡ bằng
Disable rồi Enable lại đúng rule đó.

## 6. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/027-error-budget-alerting-component.drawio`](../diagrams/027-error-budget-alerting-component.drawio)
- Sơ đồ luồng nghiệp vụ: [`docs/diagrams/027-error-budget-alerting-flow-nghiep-vu.drawio`](../diagrams/027-error-budget-alerting-flow-nghiep-vu.drawio)
- Sơ đồ trình tự: [`docs/diagrams/027-error-budget-alerting-sequence.drawio`](../diagrams/027-error-budget-alerting-sequence.drawio)

## 7. Tham khảo thêm

Cách dựng từng rule/panel và truy vấn đầy đủ:
[`07-canh-bao-ngan-sach-loi.md`](../kibana-quan-sat-he-thong/07-canh-bao-ngan-sach-loi.md). Bằng chứng diễn tập
trên Kibana thật: [`docs/QA/QA_Debt.md`](../QA/QA_Debt.md) mục 027 và 029. Chu kỳ tuần lịch và SLO 99%/1%:
[`029_Architect_ngân sách lỗi theo tuần lịch.md`](029_Architect_ngân%20sách%20lỗi%20theo%20tuần%20lịch.md).
Giới hạn đã biết (Kibana mất trạng thái alert khi máy quá tải, sửa rule 100 ghi lại sự kiện "cạn", lưu
lượng thấp làm vượt mốc rất nhanh, rule kẹt `pending` sau import, hồi phục 3 ngày chưa quan sát trên dữ
liệu chạy liên tục): xem [technical-debt.md](technical-debt.md).
