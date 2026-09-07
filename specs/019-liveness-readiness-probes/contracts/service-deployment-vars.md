# Contract: Biến đầu vào của role `service_deployment`

**Feature**: [../spec.md](../spec.md)

Đây là "hợp đồng" mà mọi service hiện có và mọi service tương lai phải tuân theo để được đưa vào
`deploy/ansible/inventories/services.yml` và được template Deployment dùng chung render ra manifest
đúng. Tương ứng với entity **Service Deployment Profile** trong [data-model.md](../data-model.md).

## Vị trí

`deploy/ansible/inventories/services.yml` — một entry cho mỗi service, dùng `service_name` làm khóa.

## Schema (YAML)

```yaml
services:
  orders:                          # = service_name, PHẢI khớp tên thư mục dưới services/
    container_port: 8080           # bắt buộc, integer
    depends_on_database: true      # bắt buộc, boolean
    # liveness_path và readiness_path: KHÔNG khai báo trừ khi cố ý khác mặc định —
    # mặc định là /health/live và /health/ready, khớp hợp đồng specs/001/contracts/health-check.md
    probe_timing_overrides:        # tùy chọn — chỉ khai báo khi nhóm mặc định theo
                                    # depends_on_database không phù hợp, và phải kèm lý do
                                    # bằng comment ngay tại đây
      initial_delay_seconds: 20
      period_seconds: 5
      timeout_seconds: 5
      failure_threshold: 20

  gateway:
    container_port: 8080
    depends_on_database: false
```

## Ràng buộc bắt buộc (dùng bởi `ProbeDeclarationTests`)

1. Danh sách khóa của `services` PHẢI đúng bằng tập 7 service hiện có: `parties`, `products`,
   `baskets`, `orders`, `identity`, `gateway`, `bff` — không thiếu, không thừa (spec SC-001).
2. `container_port` PHẢI là số nguyên dương, khớp cổng thực tế container của service lắng nghe.
3. `depends_on_database` PHẢI có mặt và là boolean — không có giá trị mặc định ngầm, vì đây là
   trường quyết định nhóm giá trị thời gian probe nào được áp dụng (research.md, Quyết định 2).
4. Nếu `probe_timing_overrides` xuất hiện, cả 4 trường con (`initial_delay_seconds`,
   `period_seconds`, `timeout_seconds`, `failure_threshold`) PHẢI được khai báo đầy đủ — không cho
   override một phần để tránh trạng thái lai không rõ nguồn gốc giá trị.
5. Không service nào được đặt `liveness_path`/`readiness_path` khác `/health/live`/`/health/ready`
   trừ khi hợp đồng health-check ở specs/001 thay đổi trước — vì bản thân override này đồng nghĩa
   phá vỡ FR-008 của tính năng.

## Người tiêu thụ hợp đồng này

- `deploy/ansible/roles/service_deployment/templates/deployment.yaml.j2` — đọc các biến này để
  render khối probe.
- `tests/DeploymentManifestConventionTests` — đọc cùng file này để biết cần render/kiểm tra bao
  nhiêu service và với tham số gì (đảm bảo test không "mù" trước một service bị thiếu entry, theo
  đúng nguyên tắc `TheScan_Examined_EveryService` đã áp dụng cho Dockerfile).
