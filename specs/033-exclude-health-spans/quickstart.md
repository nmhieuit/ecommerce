# Quickstart: Kiểm chứng việc loại span health khỏi ngân sách

**Spec**: [spec.md](./spec.md) | **Contracts**: [budget-exclusion-contract.md](./contracts/budget-exclusion-contract.md), [health-failure-rule-contract.md](./contracts/health-failure-rule-contract.md) | **Research**: [research.md](./research.md)

Hướng dẫn chạy thật để chứng minh tính năng. Không chứa mã triển khai. Giá trị chưa chốt (tên rule, đường dẫn tiêm lỗi…) điền khi tạo.

## Điều kiện

- Docker Compose `ecomerce-local` chạy: Elasticsearch `localhost:9200`, Kibana `localhost:5601` (9.4.4), 7 service, OTel collector.
- Hai dashboard và 5 rule của 027/028 đã import (`dashboards/README.md`, `alerts/README.md`).
- `dotnet`, `npx.cmd` (newman 6.2.2). Lệnh PowerShell 5.1 dùng `curl.exe`.

## Kịch bản 0 — Kiểm chứng kỹ thuật (làm trước, điểm dừng)

Chạy V1–V5 ở [research.md](./research.md). Ghi kết quả (đúng/sai, bằng chứng, thời điểm) vào mục "Kết quả xác minh" ở cuối research.md. **Sai bất kỳ mục nào: dừng và hỏi người dùng.**

## Kịch bản 1 — Test đỏ trước, xanh sau (US4, FR-010)

1. Viết test (khoá manifest, điều kiện loại ở 5 rule, rule health lỗi mới).
2. `dotnet test tests/ServiceManifestSloConventionTests` — thấy ĐỎ đúng các test mới (manifest chưa có khoá, rule chưa có điều kiện, file rule mới chưa có). Ghi số test đỏ.
3. Sửa contract → 7 manifest → 5 rule → rule mới; chạy lại thấy XANH, gồm test hiện có của 027/028/029/030.

## Kịch bản 2 — Stack chỉ có health: ngân sách không động (US1, SC-001)

1. Tạo lại 7 container API (health check vẫn chạy mỗi 5 giây; lần đầu chậm khi khởi động nguội).
2. Sau ≥ 10 phút (hai chu kỳ rule): 4 rule 027 và rule 028 không bắn; `slo-error-budget-events` không có sự kiện mới.
3. Truy vấn mức tiêu hao (có điều kiện loại) không trả dòng nào cho service chỉ có health.

## Kịch bản 3 — Có request nghiệp vụ: số khớp truy vấn (US1, US2, SC-002)

1. Tạo vài request nghiệp vụ (folder Postman `00 - Smoke Flow` hoặc `30a` bước không tiêm lỗi).
2. Mở `Ngân sách lỗi tuần — 7 service`: mức tiêu hao/hạn mức còn lại chỉ tính request nghiệp vụ; so từng (service, ngân sách) với truy vấn Elasticsearch loại `/health*` (sai lệch ≤ 2 điểm %).
3. Mở `Xử lý sự cố — 7 service`: Bảng SLO và biểu đồ theo phút không có span health; service chỉ có health không hiện dòng.

## Kịch bản 4 — Chỉ báo health lỗi (US3, SC-003)

1. Tạm dừng DB của một service (ví dụ `products-db`) hơn 5 phút: `/health/ready` trả 503.
2. Panel health lỗi hiện service đó với `health_5xx_pct` cao; rule health lỗi bắn cho đúng service đó trong một chu kỳ; không rule ngân sách nào bắn.
3. Bật lại DB. Health chỉ chậm (200) hoặc dưới 50% span lỗi thì rule không bắn.

## Kịch bản 5 — Postman tiêm lỗi vẫn đốt ngân sách (US5, SC-005)

1. Bật cờ `CHAOS_ALLOW_FAULT_INJECTION`/`CHAOS_ALLOW_LATENCY_INJECTION` và tạo lại container.
2. Chạy folder `29a`/`30a` theo vòng: ngân sách 5xx của 7 service tăng và mốc 50/75/100 bật; folder 25: p95 `Orders.Api` tăng; folder 27/25 phần "cờ TẮT": assertion mới đúng.
3. `scripts/incident-drill.ps1` vẫn làm rule 028 bắn. Khôi phục cờ về mặc định.

## Kịch bản 6 — Tài liệu và dọn tham chiếu (US5, SC-006)

Chạy LỆNH-TÌM (ở tasks) tìm tài liệu còn mô tả công thức "tính mọi span" hoặc tiêm lỗi vào `/health*`: chỉ còn bản ghi lịch sử đã loại trừ. Mở từng file tài liệu 033 ở FR-014.
