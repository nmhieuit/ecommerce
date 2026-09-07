# Contract: Hình dạng khối probe trong manifest Deployment đã render

**Feature**: [../spec.md](../spec.md)

Đây là hợp đồng đầu ra — hình dạng mà `deployment.yaml.j2` PHẢI tạo ra cho mọi service, và là thứ
`tests/DeploymentManifestConventionTests` xác nhận trên manifest đã render (không phải trên cluster
thật). Tương ứng entity **Probe Declaration** trong [data-model.md](../data-model.md).

## Hình dạng kỳ vọng (trích đoạn manifest Kubernetes)

```yaml
containers:
  - name: "{{ service_name }}"
    ports:
      - containerPort: "{{ container_port }}"
    livenessProbe:
      httpGet:
        path: /health/live
        port: "{{ container_port }}"
      initialDelaySeconds: "{{ resolved_initial_delay_seconds }}"
      periodSeconds: "{{ resolved_period_seconds }}"
      timeoutSeconds: "{{ resolved_timeout_seconds }}"
      failureThreshold: "{{ resolved_failure_threshold }}"
    readinessProbe:
      httpGet:
        path: /health/ready
        port: "{{ container_port }}"
      initialDelaySeconds: "{{ resolved_initial_delay_seconds }}"
      periodSeconds: "{{ resolved_period_seconds }}"
      timeoutSeconds: "{{ resolved_timeout_seconds }}"
      failureThreshold: "{{ resolved_failure_threshold }}"
```

`resolved_*` = giá trị từ `probe_timing_overrides` nếu service khai báo, ngược lại là giá trị mặc
định của nhóm `depends_on_database` tương ứng (định nghĩa tại
`deploy/ansible/roles/service_deployment/defaults/main.yml`, theo Quyết định 2 của research.md).

## Bất biến mà mọi manifest render ra PHẢI thỏa (đúng theo Functional Requirements)

| # | Bất biến | Nguồn |
|---|---|---|
| 1 | Cả `livenessProbe` và `readinessProbe` đều có mặt. | FR-001, FR-002 |
| 2 | `livenessProbe.httpGet.path` = `/health/live`, `readinessProbe.httpGet.path` = `/health/ready`. | FR-008, contract specs/001 |
| 3 | Cả hai probe dùng `httpGet` (không dùng `exec`/`tcpSocket`) — khớp cách hai endpoint này được lộ ra (HTTP). | Hệ quả của FR-001/FR-002 |
| 4 | `livenessProbe` không được cấu hình gọi qua bất kỳ đường dẫn nào phản ánh tình trạng dependency ngoài (tức không trỏ tới `/health/ready` hay một path tuỳ biến có check DB). | FR-003 |
| 5 | Với service có `depends_on_database: true`, `readinessProbe` dùng nhóm giá trị thời gian đủ dài để dung sai thời gian phục hồi cơ sở dữ liệu (xem research.md Quyết định 2); với `depends_on_database: false`, dùng nhóm giá trị ngắn hơn. | FR-004, FR-007 |
| 6 | `failureThreshold` > 0 và `periodSeconds` > 0 cho cả hai probe — một probe không có ngưỡng lỗi hợp lệ không thể tạo ra hành vi loại trừ traffic/khởi động lại. | FR-005, FR-006 |

## Người tiêu thụ hợp đồng này

- `tests/DeploymentManifestConventionTests/ProbeDeclarationTests.cs` — assert trực tiếp 6 bất biến
  trên cho cả 7 service.
- `quickstart.md` — dùng các bất biến này làm tiêu chí "đạt" khi chạy kịch bản xác thực thủ công
  trên cluster `kind`.
