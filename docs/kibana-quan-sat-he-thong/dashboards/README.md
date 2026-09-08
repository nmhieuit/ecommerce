# Export saved objects của dashboard SLO vận hành hằng ngày

`slo-van-hanh-hang-ngay.ndjson` là bản export thật (Kibana Saved Objects Export API) của dashboard
`SLO vận hành hằng ngày — 7 service` và mọi Lens visualization nó dùng. Đây là "mã nguồn" duy nhất
của dashboard được version-control — bản thân trạng thái Kibana không nằm trong git, nên file này là
nguồn để dựng lại dashboard trên 1 Kibana khác (máy mới, môi trường CI, đồng nghiệp khác) mà không
phải click lại từ đầu theo `docs/kibana-quan-sat-he-thong/06-dashboard-slo-van-hanh-hang-ngay.md`.

## Cách import lại

```powershell
$KibanaBase = "http://localhost:5601"
$form = @{
  file = Get-Item "docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson"
}
Invoke-RestMethod -Uri "$KibanaBase/api/saved_objects/_import?overwrite=true" `
  -Method Post -Headers @{ "kbn-xsrf" = "true" } -Form $form
```

Hoặc qua UI: menu ☰ → **Stack Management** → **Saved Objects** → **Import** → chọn file này → tick
**Automatically overwrite conflicts** nếu đang cập nhật bản cũ.

## Cách export lại (sau khi sửa dashboard trên UI)

```powershell
$KibanaBase = "http://localhost:5601"
$body = @{ type = @("dashboard","lens","index-pattern") } | ConvertTo-Json
Invoke-WebRequest -Uri "$KibanaBase/api/saved_objects/_export" -Method Post `
  -Headers @{ "kbn-xsrf" = "true" } -ContentType "application/json" -Body $body `
  -OutFile "docs/kibana-quan-sat-he-thong/dashboards/slo-van-hanh-hang-ngay.ndjson"
```
