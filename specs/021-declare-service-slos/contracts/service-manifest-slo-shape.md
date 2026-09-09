# Contract: Hình dạng khối `slos` trong `service-manifest.yaml`

**Feature**: [../spec.md](../spec.md)

Đây là hợp đồng đầu vào — hình dạng mà khối `slos:` (và trường `service.classification`) PHẢI có
trong mọi `services/*/src/*/service-manifest.yaml`, và là thứ
`tests/ServiceManifestSloConventionTests` xác nhận trên cả 7 file. Tương ứng entity **Khai báo ngân
sách SLO của service** và **Hồ sơ mặc định nền tảng** trong [data-model.md](../data-model.md).

## Hình dạng kỳ vọng — service khớp mặc định (7/7 service hiện tại)

```yaml
service:
  name: <tên service>
  classification: internal-service-api   # hoặc client-facing-bff

slos:
  availability: 99.9%
  error-rate:
    max-5xx-ratio: 0.1%
  latency:
    p95: 150ms   # 300ms nếu classification: client-facing-bff
    p99: 500ms   # 800ms nếu classification: client-facing-bff
```

**Lưu ý đã sửa sau khi triển khai (T008)**: `bff` mang `classification: client-facing-bff` nên hồ sơ
mặc định của chính nó là p95 300ms/p99 800ms — đây KHÔNG phải một ngoại lệ, mà là mặc định đúng của
đúng phân loại đó (constitution Principle VIII định nghĩa hai hồ sơ mặc định song song, không phải
một cái là "nới lỏng" của cái kia). Nhận định ban đầu ở research.md Quyết định 0/2 rằng "`bff` có một
ngoại lệ cần chuyển từ comment thành trường có cấu trúc" là sai — đã kiểm chứng bằng
`SloDefaultComplianceTests`: cả 7/7 service, kể cả `bff`, đều khớp đúng mặc định của chính phân loại
của mình, không có `slos.justification` nào cần thiết ở hiện tại.

## Hình dạng — service có ngoại lệ thật (ví dụ minh hoạ, hiện chưa có service nào ở trạng thái này)

```yaml
service:
  name: <tên service>
  classification: internal-service-api

slos:
  availability: 99.9%
  error-rate:
    max-5xx-ratio: 0.1%
  latency:
    p95: 250ms   # khác mặc định 150ms của internal-service-api
    p99: 500ms
  justification: >-
    <lý do vì sao ngân sách này khác mặc định của CHÍNH classification của service này — bắt buộc
    khi có bất kỳ giá trị nào lệch mặc định của phân loại đó>
```

## Bất biến mà mọi manifest PHẢI thỏa (đúng theo Functional Requirements)

| # | Bất biến | Nguồn |
|---|---|---|
| 1 | Khối `slos:` tồn tại với đủ 4 giá trị: `availability`, `error-rate.max-5xx-ratio`, `latency.p95`, `latency.p99`. | FR-001 |
| 2 | Không giá trị nào trong 4 giá trị trên là rỗng, `null`, hay một placeholder rõ ràng (ví dụ `TODO`, `TBD`). | FR-001 |
| 3 | `service.classification` là một trong hai giá trị đã biết (`client-facing-bff`, `internal-service-api`). | Tiền đề cho bất biến 4 |
| 4 | Nếu cả 4 giá trị khớp đúng hồ sơ mặc định của `service.classification` (bảng ở data-model.md), `slos.justification` có thể vắng mặt. | FR-002 |
| 5 | Nếu bất kỳ giá trị nào trong 4 giá trị khác hồ sơ mặc định, `slos.justification` PHẢI hiện diện, không rỗng, không phải placeholder. | FR-003 |
| 6 | `service.name` khớp đúng tên thư mục service dưới `services/` — không có manifest "mồ côi" không tương ứng service nào đang chạy. | SC-001 |

## Người tiêu thụ hợp đồng này

- `tests/ServiceManifestSloConventionTests/SloDeclarationTests.cs` — assert bất biến 1–3 cho cả 7
  service.
- `tests/ServiceManifestSloConventionTests/SloDefaultComplianceTests.cs` — assert bất biến 4–5 bằng
  cách so khớp với `PlatformSloDefaults.cs`.
- `quickstart.md` — dùng các bất biến này làm tiêu chí "đạt" ở bước xác thực tĩnh, trước khi chuyển
  sang xác thực dashboard (US3).
