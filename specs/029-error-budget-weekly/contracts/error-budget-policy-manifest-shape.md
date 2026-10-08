# Contract: Khối `slos` (phần khả dụng/5xx) và `error-budget-policy` trong `service-manifest.yaml`, chu kỳ tuần

**Feature**: [../spec.md](../spec.md) (khoá `excluded-path-prefixes` thêm bởi spec 033) | **Thay thế**: `specs/027-error-budget-alerting/contracts/error-budget-policy-manifest-shape.md` (sẽ được sửa tại chỗ cho khớp contract này)

**Người tiêu thụ**: `tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests`, `SloDefaultComplianceTests` (qua `PlatformSloDefaults`)

## Phần `slos` thay đổi (cả 7 service)

```yaml
slos:
  availability: 99%            # weekly
  error-rate:
    max-5xx-ratio: 1%
  latency:                     # giữ nguyên giá trị hiện có của từng service
    p95: ...
    p99: ...
```

## Hình dạng bắt buộc của `error-budget-policy` (giống hệt nhau ở cả 7 service)

```yaml
error-budget-policy:
  # Constitution Principle VIII (2.0.0): ngân sách lỗi tính theo tuần lịch giờ Việt Nam,
  # thứ Hai 00:00 → Chủ nhật 23:59 (specs/029-error-budget-weekly).
  window: calendar-week
  timezone: UTC+07:00
  excluded-path-prefixes: [/health]   # spec 033: span có đường dẫn bắt đầu bằng tiền tố này KHÔNG tính vào ngân sách
  budgets:
    availability:
      bad-request: http-5xx
      allowed-bad-ratio: 1%
    error-rate:
      bad-request: http-5xx
      allowed-bad-ratio: 1%
    latency-p95:
      bad-request: slower-than-slo-p95   # ngưỡng lấy từ slos.latency.p95 của chính manifest này
      allowed-bad-ratio: 5%
    latency-p99:
      bad-request: slower-than-slo-p99   # ngưỡng lấy từ slos.latency.p99 của chính manifest này
      allowed-bad-ratio: 1%
  alert-thresholds: [50%, 75%, 100%]
  exhausted-when: any-budget-at-100%
  on-exhausted:
    who: Người vận hành (vai SRE/Dev) của service này
    stops: Merge tính năng mới vào service này
    does: Chỉ làm công việc nâng độ tin cậy cho service này
  recovery:
    consecutive-days-meeting-slo: 3
    no-traffic-day-counts-as-met: true
    budget-reset-clears-freeze: false
```

## Bất biến

| # | Bất biến | Nguồn | Đổi so với 027 |
|---|---|---|---|
| 1 | Cả 7 manifest có khối `error-budget-policy`, đặt sau khối `slos`. | FR-001 (027) | — |
| 2 | `budgets` có đúng 4 khoá; `allowed-bad-ratio` = `1%`, `1%`, `5%`, `1%`. | FR-003 | `0.1%` → `1%` (2 khoá) |
| 3 | `window = calendar-week`, `timezone = UTC+07:00`. | FR-001 | `calendar-month` → `calendar-week` |
| 4 | `alert-thresholds` đúng `[50%, 75%, 100%]`; `exhausted-when = any-budget-at-100%`. | FR-005 | — |
| 5 | `on-exhausted.who`, `.stops`, `.does` không rỗng. | FR-005 | — |
| 6 | `recovery` = `3` / `true` / `false`. | FR-005 | — |
| 7 | Khối này không khai báo ngưỡng độ trễ. | FR-013 (027) | — |
| 8 | `slos.availability = 99%` và `slos.error-rate.max-5xx-ratio = 1%` với cả hai loại service, khớp `PlatformSloDefaults`. Không manifest nào cần `slos.justification` vì lý do này. | FR-002, FR-004 | `99.9%`/`0.1%` → `99%`/`1%` |
| 9 | Với khả dụng và 5xx, `allowed-bad-ratio` = 100% − `slos.availability` = `slos.error-rate.max-5xx-ratio`. | FR-003 | mới (nêu rõ quan hệ 1 − SLO) |
| 10 | `excluded-path-prefixes` có đúng giá trị `[/health]`, giống hệt nhau ở cả 7 manifest, đặt ngang hàng `window`/`timezone` (không nằm trong `budgets`). Rule và panel ngân sách loại span theo đúng tiền tố này. | FR-007 (033) | mới (spec 033; xem `specs/033-exclude-health-spans/contracts/budget-exclusion-contract.md`) |
