# Bước 021: Thay đổi nghiệp vụ so với bước 020

## Phạm vi

`research.md` Quyết định 0 (rà soát hiện trạng trước khi thiết kế) phát hiện: cả 7
`service-manifest.yaml` đã có sẵn khối `slos:` đầy đủ từ bước 018/019 trở về trước, và dashboard
Kibana đo SLO đã tồn tại và đã xác minh khớp dữ liệu thô từ trước. Phần việc thật của bước 021 không
phải "khai báo" hay "xây dashboard" từ đầu, mà là (a) thêm test canh giữ để khai báo có tính tự bảo
vệ, và (b) chính thức hoá dashboard đã có thành một phần có hợp đồng của đặc tả.

Ranh giới commit được xác định từ lịch sử Git:

- Mốc hoàn tất bước 020: commit `272b1ce`.
- Đặc tả và triển khai bước 021 — mốc hoàn tất, một commit duy nhất: commit `580ea26`.

`tests/ServiceManifestSloConventionTests` (`SloDeclarationTests`, `SloDefaultComplianceTests`,
`PlatformSloDefaults`, `ServiceManifestFixture`/`Model`) là nội dung kiểm thử — cơ chế thực thi
chính của tính năng — bị loại theo đúng phạm vi đã áp dụng, chỉ được nhắc tên.

## 1. Bug thật tìm được bởi test mới: Identity thiếu `classification`

[services/identity/src/Identity.Api/service-manifest.yaml](../../services/identity/src/Identity.Api/service-manifest.yaml)

```yaml
service:
  name: identity
  # 021: dòng này bị THIẾU trước bước 021 — Identity là service duy nhất trong 7 service chưa
  # khai báo classification, dù khối slos: của nó đã đầy đủ 4 giá trị từ trước. Không có test nào
  # đọc lại file này nên không ai phát hiện cho tới khi SloDeclarationTests được viết.
  classification: internal-service-api
```

Đây chính là 1 trong 2 kết quả Test-First khác dự tính ban đầu mà architecture doc ghi nhận (US1 fail
một lần khi chạy thật) — không phải giả định lý thuyết, mà là lỗi thật bị test mới bắt được ngay khi
viết xong.

## 2. Dashboard Kibana đã có được chính thức hoá thành hợp đồng

[docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md](../../docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md)

```markdown
Kể từ SCRUM-29, dashboard này được chính thức hoá làm cơ chế đo lường liên tục (User Story 3) của
đặc tả specs/021-declare-service-slos/ — hợp đồng (bất biến bắt buộc) tại
contracts/continuous-measurement-contract.md, đã xác thực lại trên dữ liệu thật tại tasks.md
T011–T014.
```

Không có panel, query, hay code dashboard nào được viết mới — dashboard `SLO vận hành hằng ngày — 7
service` (8 panel, đo Error-rate/Latency p95/Latency p99 từ traces OTel thật, Availability suy ra
xấp xỉ từ `100% − Error-rate`) đã được dựng và xác minh ở một phiên trước đó. Bước 021 chỉ thêm liên
kết ngược từ tài liệu vận hành đó về đặc tả, biến nó từ "tài liệu vận hành rời rạc" thành "một phần
được công nhận, có hợp đồng" của tính năng.

## Tóm tắt 020 → 021

| Khu vực | Bước 020 | Bước 021 |
|---|---|---|
| SLO trong `service-manifest.yaml` | Đã có đủ 4 giá trị ở 6/7 service (từ 018/019) | Đủ ở cả 7/7 — vá thiếu sót của Identity |
| Test canh giữ SLO | Không có | `SloDeclarationTests`/`SloDefaultComplianceTests` (không đổi service code, chỉ đọc lại manifest) |
| Dashboard Kibana | Đã tồn tại, xác minh khớp dữ liệu, nhưng là tài liệu vận hành rời rạc | Chính thức hoá thành cơ chế đo lường liên tục có hợp đồng của đặc tả |
| Resilience (020) | Không đổi | Không đổi |

**Kết luận:** bước 021 không thêm nghiệp vụ mua hàng, gần như không đổi code service — chỉ vá đúng 1
dòng cấu hình thiếu (`classification` của Identity) mà một test mới phát hiện được. Giá trị thật của
bước này nằm ở việc làm cho các khai báo SLO đã có từ trước trở nên tự bảo vệ được, và công nhận chính
thức một dashboard vận hành đã tồn tại sẵn thay vì xây lại từ đầu.

## 3. Shared project trong bước 021

Bước 021 không tạo, không sửa, và không cần bất kỳ `ProjectReference` nào tới
`ServiceDefaults`/`Tenancy`/`EventContracts`/`Identity`. Giống bước 017 và 019, đây là bước không chạm
shared C# project nào — toàn bộ tác dụng nằm ở một dòng cấu hình YAML và một liên kết tài liệu.
