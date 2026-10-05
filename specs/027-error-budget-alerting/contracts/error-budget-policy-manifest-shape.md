# Contract: Hình dạng khối `error-budget-policy` trong `service-manifest.yaml`

**Feature**: [../spec.md](../spec.md) | **Người tiêu thụ**: `tests/ServiceManifestSloConventionTests/ErrorBudgetPolicyTests`

## Hình dạng bắt buộc (giống hệt nhau ở cả 7 service)

```yaml
error-budget-policy:
  # SCRUM-35 / constitution Principle VIII: hệ quả thật cho một SLO bị vi phạm kéo dài.
  window: calendar-week
  timezone: UTC+07:00
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

| # | Bất biến | Nguồn |
|---|---|---|
| 1 | Cả 7 manifest có khối `error-budget-policy`, đặt sau khối `slos`. | FR-001 |
| 2 | `budgets` có đúng 4 khoá trên, mỗi khoá có `bad-request` và `allowed-bad-ratio` đúng giá trị trên. | FR-002 |
| 3 | `window = calendar-week`, `timezone = UTC+07:00`. | FR-003 |
| 4 | `alert-thresholds` đúng `[50%, 75%, 100%]`; `exhausted-when = any-budget-at-100%`. | FR-004, FR-006 |
| 5 | `on-exhausted.who`, `.stops`, `.does` không rỗng. | FR-009, SC-004 |
| 6 | `recovery.consecutive-days-meeting-slo = 3`, `no-traffic-day-counts-as-met = true`, `budget-reset-clears-freeze = false`. | FR-010 |
| 7 | Khối này không khai báo ngưỡng độ trễ; ngưỡng luôn lấy từ `slos.latency`. | FR-013 |
