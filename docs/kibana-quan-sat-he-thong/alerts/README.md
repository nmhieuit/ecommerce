# Export saved objects của bộ rule cảnh báo ngân sách lỗi

`error-budget-rules.ndjson` là bản export thật (Kibana Saved Objects Export API) của 4 rule cảnh báo
ngân sách lỗi (tag `slo-error-budget`) và connector Index mà rule `error-budget-100` (sự kiện `exhausted`) và
`error-budget-frozen` (sự kiện `recovered`, từ nhánh fix/frozen-panel-status) dùng — đặc tả tại
[`specs/027-error-budget-alerting/`](../../../specs/027-error-budget-alerting/spec.md), chu kỳ tuần lịch theo
[`specs/029-error-budget-weekly/`](../../../specs/029-error-budget-weekly/spec.md), hợp đồng tại
[`contracts/error-budget-alert-rules-contract.md`](../../../specs/029-error-budget-weekly/contracts/error-budget-alert-rules-contract.md).
Đây là "mã nguồn" duy nhất của bộ rule được version-control; `tests/ServiceManifestSloConventionTests/ErrorBudgetRuleDefinitionTests.cs`
đọc file này để kiểm tra ngưỡng trong rule luôn khớp `service-manifest.yaml`.

Cách dựng từng rule bằng tay và lý do thiết kế: [`../07-canh-bao-ngan-sach-loi.md`](../07-canh-bao-ngan-sach-loi.md).

## Điều kiện trước

Kibana phải có khoá mã hoá saved objects (`KIBANA_ENCRYPTION_KEY` trong `.env`), nếu không Alerting
không cho tạo hay chạy rule. Kiểm tra:

```powershell
Invoke-RestMethod http://localhost:5601/api/alerting/_health | Select-Object has_permanent_encryption_key
```

## Cách import lại

Cùng lý do với README của dashboard: dùng `curl.exe` vì Windows PowerShell 5.1 không có `-Form`.

```powershell
curl.exe -X POST "http://localhost:5601/api/saved_objects/_import?overwrite=true" `
  -H "kbn-xsrf: true" `
  --form "file=@docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson"
```

**Bắt buộc sau mỗi lần import**: Kibana nhập rule ở trạng thái **disabled** và phải tạo lại API key
cho rule. Vào ☰ → **Stack Management** → **Rules**, lọc tag `slo-error-budget`, chọn cả 4 rule →
**Enable**. Chưa enable thì không có cảnh báo nào bắn.

Kiểm tra sau ~5 phút: mỗi rule phải có "Last run" mới. Rule có thể đứng ở trạng thái `pending` mãi sau
import: task của nó chạy đúng lúc rule đang bị tắt trong quá trình import nên Task Manager tự tắt task
("Disabling task … as it indicated it should disable itself"). Cách gỡ: **Disable** rồi **Enable** lại đúng
rule đó. Lưu ý: việc này (cũng như có lúc khi sửa rule `error-budget-100`) có thể ghi thêm sự kiện
`exhausted` cho các alert vừa chuyển sang active — xem `07-canh-bao-ngan-sach-loi.md`.

## Cách export lại (sau khi sửa rule trên UI)

Export theo đúng id của 4 rule (type `alert`) và connector (type `action`) — id thật (cũng ghi trong
[`../07-canh-bao-ngan-sach-loi.md`](../07-canh-bao-ngan-sach-loi.md)):

```powershell
$KibanaBase = "http://localhost:5601"
$body = @{
  objects = @(
    @{ type = "alert";  id = "4169d562-aca7-47af-8da1-63511967b07f" }
    @{ type = "alert";  id = "f724cf89-13ac-4f9e-af09-f89e5e436917" }
    @{ type = "alert";  id = "a3581b2a-3eda-4a2f-8f4d-a9af04c8fac3" }
    @{ type = "alert";  id = "a16eed47-01ed-40ce-a8be-c264bde1b771" }
    @{ type = "action"; id = "709d5d97-0731-4d70-b244-fd8cf5f4195d" }
  )
  includeReferencesDeep = $true
} | ConvertTo-Json -Depth 5
Invoke-WebRequest -Uri "$KibanaBase/api/saved_objects/_export" -Method Post `
  -Headers @{ "kbn-xsrf" = "true" } -ContentType "application/json" -Body $body `
  -OutFile "docs/kibana-quan-sat-he-thong/alerts/error-budget-rules.ndjson"
```

## Rule phát hiện nhanh `incident-fast-detection` (spec 028, SCRUM-36)

`incident-fast-detection-rule.ndjson` là bản export thật của rule phát hiện nhanh: vượt SLO trong 5
phút gần nhất, tag `incident-fast-detection`. Rule này dùng để diễn tập sự cố on-call. Đặc tả tại
[`specs/028-incident-oncall-drill/`](../../../specs/028-incident-oncall-drill/spec.md), cách dựng và lý
do thiết kế ở [`../08-phat-hien-nhanh-va-xu-ly-su-co.md`](../08-phat-hien-nhanh-va-xu-ly-su-co.md).

## Rule health lỗi `health-failure` (spec 033)

`health-failure-rule.ndjson` là bản export của rule báo service không sẵn sàng: từ 50% span health (`/health*`) của một service trả 5xx trong 5 phút gần nhất, tag `health-failure`, chu kỳ 5 phút, không có action. Rule này **không** thuộc ngân sách; các rule ngân sách và `incident-fast-detection` loại span health. Cách import/export giống hai rule trên. Đặc tả: [`specs/033-exclude-health-spans/`](../../../specs/033-exclude-health-spans/spec.md).

Từ spec 034, 4 rule ngân sách và `incident-fast-detection` **chỉ đếm span Server** (`kind == "Server"`); `error-budget-frozen` dùng `kind == "Server" OR _index LIKE "*slo-error-budget-events*"` để giữ sự kiện cạn. Rule `health-failure` không đổi. Đặc tả: [`specs/034-error-budget-server-spans/`](../../../specs/034-error-budget-server-spans/spec.md).

Rule nằm ở **file riêng**, không gộp vào `error-budget-rules.ndjson`: `ErrorBudgetRuleDefinitionTests`
của 027 đếm đúng 4 rule trong file đó. Không có test nào canh ngưỡng của rule này (sai lệch Nguyên tắc
III của spec 028), nên sửa `slos` trong manifest thì phải sửa rule bằng tay rồi export lại.

Import (cùng điều kiện trước và cùng lý do dùng `curl.exe` như phần trên):

```powershell
curl.exe -X POST "http://localhost:5601/api/saved_objects/_import?overwrite=true" `
  -H "kbn-xsrf: true" `
  --form "file=@docs/kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson"
```

**Bắt buộc sau import**: rule được nhập ở trạng thái **disabled**. Vào ☰ → **Stack Management** →
**Rules**, lọc tag `incident-fast-detection` → **Enable**.

Export lại sau khi sửa trên UI:

```powershell
$KibanaBase = "http://localhost:5601"
$body = @{
  objects = @(@{ type = "alert"; id = "9b0e2c36-678b-4cd7-9de0-7468d623f82d" })
  includeReferencesDeep = $true
} | ConvertTo-Json -Depth 5
Invoke-WebRequest -Uri "$KibanaBase/api/saved_objects/_export" -Method Post `
  -Headers @{ "kbn-xsrf" = "true" } -ContentType "application/json" -Body $body `
  -OutFile "docs/kibana-quan-sat-he-thong/alerts/incident-fast-detection-rule.ndjson"
```
