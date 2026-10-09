# Contract: Loại span health khỏi công thức ngân sách

**Spec**: [../spec.md](../spec.md) | **Research**: [../research.md](../research.md) | **Mở rộng**: `specs/029-error-budget-weekly/contracts/error-budget-policy-manifest-shape.md` (khoá `excluded-path-prefixes`)

Hợp đồng bất biến cho manifest, rule và dashboard. **Nối tiếp (spec 034)**: từ 034, ngoài loại `/health*`, mọi rule và panel ngân sách còn chỉ đếm span `kind = Server`; xem [`specs/034-error-budget-server-spans/contracts/server-span-only-contract.md`](../../034-error-budget-server-spans/contracts/server-span-only-contract.md). **Viết trước** khi sửa manifest, rule, dashboard và test (Nguyên tắc II).

## Manifest (người tiêu thụ: `ErrorBudgetPolicyTests`)

1. Cả 7 `service-manifest.yaml` có trong `error-budget-policy` đúng khoá `excluded-path-prefixes: [/health]`, giống hệt nhau (cùng giá trị, cùng thứ tự).
2. Khoá này ngang hàng `window`, `timezone`; không nằm trong `budgets`.
3. Thiếu khoá hoặc khác giá trị ở bất kỳ manifest nào làm `ErrorBudgetPolicyTests` đỏ đúng service đó.

```yaml
error-budget-policy:
  window: calendar-week
  timezone: UTC+07:00
  excluded-path-prefixes: [/health]   # span có đường dẫn bắt đầu bằng tiền tố này không tính vào ngân sách
  budgets: …
```

## Điều kiện loại trong ES|QL (người tiêu thụ: `ErrorBudgetRuleDefinitionTests`, `IncidentFastDetectionRuleDefinitionTests`)

4. Mỗi truy vấn ES|QL tính từ traces có **đúng một** điều kiện loại cho mỗi tiền tố của manifest, dạng `NOT (COALESCE(attributes.url.path, "") LIKE "<tiền tố>*")` (bắt buộc `COALESCE`: nếu thiếu, span không có trường đường dẫn bị mất — F3).
5. Điều kiện loại áp dụng cho: `error-budget-50`, `error-budget-75`, `error-budget-100`, `error-budget-frozen`, `incident-fast-detection`. Mọi quy tắc khác của chúng không đổi (tỷ lệ cho phép, ngưỡng, mốc, cửa sổ, chu kỳ, cột kết quả).
6. Rule `error-budget-frozen`: điều kiện loại không làm mất sự kiện `slo-error-budget-events` (sự kiện không có `url.path`).
7. Ngày chỉ có span health được coi là không có traffic và tính là đạt SLO (giữ quy tắc 027/029).

## Dashboard (kiểm bằng quickstart, không test tự động)

8. Dashboard `Ngân sách lỗi tuần — 7 service`: mọi panel ES|QL tính từ traces có cùng điều kiện loại (bất biến 4); hai panel cảnh báo/cạn không đổi.
9. Dashboard `Xử lý sự cố — 7 service`: 6 panel Lens đọc traces (Bảng SLO, 5xx/p95/traffic+401/403 theo phút, phân bố status code, top endpoint chậm) loại span health bằng `query` KQL cấp panel; `dotnet.exceptions`, lỗi gọi hạ lưu, log lỗi, Phát hiện nhanh không đổi.
10. Service chỉ có span health: không hiện dòng ở dashboard ngân sách và Bảng SLO; không rule nào bắn.
11. Mọi `time_range` riêng của panel dashboard tuần (`now-30d`) và thanh thời gian của dashboard Xử lý sự cố không đổi.

## Postman (kiểm bằng newman)

12. Mọi request tiêm lỗi/độ trễ trong folder 25, 27, 29a, 30a (26 request) dùng đường dẫn **không phải health**; header giữ nguyên. Assertion "cờ TẮT" sửa theo phản hồi thật của đường dẫn mới.
13. `scripts/incident-drill.ps1` không đổi.

## Kiểm chứng

Theo [../quickstart.md](../quickstart.md). Bất biến 1–7 đọc được từ file (manifest, ndjson rule) và được test tự động canh; 8–13 cần mở dashboard/chạy newman thật.
