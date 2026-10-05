# Quickstart: Xác thực ngân sách lỗi theo tuần lịch giờ Việt Nam

**Feature**: [spec.md](./spec.md)

Bất biến được kiểm tra:
- [contracts/error-budget-alert-rules-contract.md](./contracts/error-budget-alert-rules-contract.md): 5–10, 14;
- [contracts/error-budget-policy-manifest-shape.md](./contracts/error-budget-policy-manifest-shape.md);
- [contracts/constitution-amendment.md](./contracts/constitution-amendment.md).

## Chuẩn bị (stack mới, Elastic trống)

1. `.env` có `KIBANA_ENCRYPTION_KEY` (≥ 32 ký tự). Đặt `CHAOS_ALLOW_FAULT_INJECTION=true` **chỉ trong lúc diễn tập**.
2. Dựng stack:

   ```powershell
   ./scripts/local-up.ps1
   ```

3. Tạo index sự kiện `slo-error-budget-events` (lệnh `PUT` nguyên văn ở `docs/kibana-quan-sat-he-thong/07-canh-bao-ngan-sach-loi.md`).
4. Import `alerts/error-budget-rules.ndjson`, `alerts/incident-fast-detection-rule.ndjson` và `dashboards/slo-van-hanh-hang-ngay.ndjson` theo `docs/kibana-quan-sat-he-thong/alerts/README.md`.
5. Bật (Enable) 5 rule. Rule nào kẹt `pending` thì Disable rồi Enable lại (cách gỡ ở `alerts/README.md`).

**Kết quả mong đợi**:
- 4 rule tag `slo-error-budget` chạy 5 phút một lần. Rule mốc có cửa sổ 7 ngày, rule frozen 14 ngày.
- Dashboard có 3 panel ngân sách "tuần này", khoảng thời gian `now-7d`.
- Panel text ghi `99%`/tuần.

## Kịch bản 0 — Kiểm chứng biểu thức đầu tuần (research V1)

Trong Discover chế độ ES|QL, chạy biểu thức đầu tuần với 4 thời điểm giả định ở [research.md](./research.md) Quyết định 9 (thay `NOW()` bằng hằng thời gian).

**Kết quả mong đợi**: cả 4 hàng ra đúng cột "Đầu tuần mong đợi" (bất biến 5). **Sai một hàng → dừng, hỏi người dùng** (research V1).

## Kịch bản 1 — Đốt ngân sách 5xx tuần của cả 7 service, cảnh báo bắn đúng mốc

1. Lấy tổng span tuần này `N` của từng service từ bảng mức tiêu hao. Với tỷ lệ 1%, ngân sách 5xx cạn khi số 5xx ≈ `N / 99`.
2. Chạy folder Postman 029 (mỗi service một request `X-Chaos-Fault: 5xx`) bằng newman, theo từng đợt khoảng 1/4 lượng cần thiết (số vòng `-n` tính từ `N` của service có `N` lớn nhất).

   ```powershell
   npx.cmd --yes newman@6.2.2 run postman/ecommerce.postman_collection.v2.json -e postman/local.postman_environment.v2.json --folder "<tên folder 029>" -n <số vòng>
   ```

3. Sau mỗi đợt chờ tối đa 5 phút và xem dashboard.

**Kết quả mong đợi**:
- Ngân sách `availability`/`error-rate` của từng service lần lượt vượt 50%, 75%, 100%. Bảng alert hiện từng mốc trong ≤ 5 phút (bất biến 6). Lưu lượng thấp có thể làm nhiều mốc bắn cùng chu kỳ: ghi lại, không coi là lỗi.
- Discover thấy span `status_code = 500` của 7 service.
- `slo-error-budget-events` có event `exhausted` cho 7 service, và bảng "cạn" hiện 7 service (bất biến 9).
- Bảng mức tiêu hao và bảng alert khớp nhau ở cùng thời điểm (SC-004).

## Kịch bản 2 — Ngưỡng 1% của rule phát hiện nhanh (bất biến 14)

Với `Orders.Api` (port 5041) và tải nền đang chạy, gửi request có header `X-Chaos-Fault: 5xx` ở hai mức trong 5 phút:
- (a) dưới 1% tổng request;
- (b) trên 1% tổng request.

**Kết quả mong đợi**: (a) không có alert `incident-fast-detection` vì 5xx (trừ khi độ trễ vượt ngưỡng); (b) alert bắn trong ≤ 5 phút.

## Kịch bản 3 — Logic đóng băng với ngưỡng mới

Như bước T041 của 027: ghi các event `exhausted` thử với `@timestamp` cách đây 2, 5 và 13 ngày cho các service giả.

**Kết quả mong đợi**:
- Service có ngày xấu (5xx ≥ 1%) sau ngày cạn thì vẫn đóng băng.
- Service có 3 ngày đạt liền (kể cả ngày 5xx 0.5%) thì được gỡ.
- Event ngoài 14 ngày không được tính (ghi lại giới hạn).

## Kịch bản 4 — Đọc chính sách và hiến chương

```powershell
dotnet test tests/ServiceManifestSloConventionTests
```

Mở `constitution.md` và 1 manifest bất kỳ.

**Kết quả mong đợi**:
- Test xanh.
- Hiến chương 2.0.0 có hai dòng mới.
- Manifest khai `99%   # weekly`, `max-5xx-ratio: 1%`, `window: calendar-week`.

## Kiểm tra không phá cái đã có

- 5 bất biến của `specs/021-declare-service-slos/contracts/continuous-measurement-contract.md` vẫn đúng (bất biến 10).
- `dotnet test shared/ServiceDefaults.UnitTests` xanh.
- Bố cục dashboard không đổi ngoài 3 panel ngân sách và panel text.

## Sau khi xong

- Đặt `CHAOS_ALLOW_FAULT_INJECTION=false`.
- **Dọn toàn bộ Elastic: hỏi lại người dùng trước khi xoá** (FR-018).

## Kiểm tra theo thời gian (không làm được trong một phiên)

- Ranh giới thật 00:00 thứ Hai 12/10 giờ Việt Nam: mức tiêu hao bắt đầu lại, alert mốc tuần cũ tắt, trạng thái đóng băng giữ (SC-003).
- Hồi phục thật sau 3 ngày đạt SLO.

Ghi kết quả vào tài liệu QA 029 khi quan sát được.
