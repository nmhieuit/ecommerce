# Export saved objects của dashboard SLO vận hành hằng ngày

`slo-van-hanh-hang-ngay.ndjson` là bản export thật (Kibana Saved Objects Export API) của dashboard
`SLO vận hành hằng ngày — 7 service` và mọi Lens visualization nó dùng. Đây là "mã nguồn" duy nhất
của dashboard được version-control — bản thân trạng thái Kibana không nằm trong git, nên file này là
nguồn để dựng lại dashboard trên 1 Kibana khác (máy mới, môi trường CI, đồng nghiệp khác) mà không
phải click lại từ đầu theo `docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md`.

## Cách import lại

Lệnh dưới dùng `curl.exe` (có sẵn từ Windows 10+) thay vì `Invoke-RestMethod ... -Form`, vì tham số
`-Form` chỉ có từ PowerShell 6.1+ trở lên — môi trường này chạy **Windows PowerShell 5.1** (đã xác
nhận bằng `$PSVersionTable`), không hỗ trợ `-Form`.

```powershell
curl.exe -X POST "http://localhost:5601/api/saved_objects/_import?overwrite=true" `
  -H "kbn-xsrf: true" `
  --form "file=@docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson"
```

Hoặc qua UI: menu ☰ → **Stack Management** → **Saved Objects** → **Import** → chọn file này → tick
**Automatically overwrite conflicts** nếu đang cập nhật bản cũ.

## Cách export lại (sau khi sửa dashboard trên UI)

Export theo đúng object (chỉ dashboard này + các saved object nó thực sự tham chiếu, dùng
`includeReferencesDeep` thay vì lọc theo `type` — cách lọc theo `type` cũ vô tình kéo theo cả
"First Dashboard"/"First Visualization" không liên quan và 1 index-pattern logs không dùng tới):

```powershell
$KibanaBase = "http://localhost:5601"
$body = @{
  objects = @(@{ type = "dashboard"; id = "e2e06ff5-9cdf-4bea-acc8-5fd60ce26170" })
  includeReferencesDeep = $true
} | ConvertTo-Json -Depth 5
Invoke-WebRequest -Uri "$KibanaBase/api/saved_objects/_export" -Method Post `
  -Headers @{ "kbn-xsrf" = "true" } -ContentType "application/json" -Body $body `
  -OutFile "docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson"
```
