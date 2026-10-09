# Export saved objects của hai dashboard SLO

Hai file `.ndjson` là bản export thật (Kibana Saved Objects Export API) của hai dashboard thay cho dashboard
`SLO vận hành hằng ngày — 7 service` cũ (spec 030). Đây là "mã nguồn" duy nhất của dashboard được
version-control — bản thân trạng thái Kibana không nằm trong git, nên hai file này là nguồn để dựng lại dashboard
trên một Kibana khác (máy mới, môi trường CI, đồng nghiệp khác) mà không phải click lại từ đầu.

| File | Dashboard | Id |
|---|---|---|
| [`xu-ly-su-co.ndjson`](xu-ly-su-co.ndjson) | `Xử lý sự cố — 7 service`: mọi panel theo thanh thời gian (mặc định 1 giờ, tự làm mới 1 phút) | `e61fc7f3-17fe-428a-a373-da88af0a4a1e` |
| [`ngan-sach-loi-tuan.ndjson`](ngan-sach-loi-tuan.ndjson) | `Ngân sách lỗi tuần — 7 service`: cố định tuần lịch giờ Việt Nam, chọn tuần bằng điều khiển | `2a607bf4-2449-48a1-a2e8-1336ec35a7b7` |

Hướng dẫn dùng từng dashboard: [`06-dashboard-xu-ly-su-co-va-ngan-sach-tuan.md`](../06-dashboard-xu-ly-su-co-va-ngan-sach-tuan.md).
Hợp đồng bất biến của hai file: [`specs/030-incident-and-weekly-dashboards/contracts/dashboards-contract.md`](../../../specs/030-incident-and-weekly-dashboards/contracts/dashboards-contract.md).

Từ spec 033, truy vấn ES|QL của dashboard Ngân sách lỗi tuần và 6 panel Lens đọc traces của dashboard Xử lý sự cố **không tính span có đường dẫn bắt đầu bằng `/health`**; dashboard Xử lý sự cố có thêm panel `Health lỗi theo service`.

Từ spec 034, các truy vấn ES|QL của dashboard Ngân sách lỗi tuần (5 truy vấn) và 6 panel Lens ngân sách/SLO của dashboard Xử lý sự cố **chỉ đếm span `kind = Server`** (không đếm span Client/Producer); panel `Lỗi gọi hạ lưu` vẫn đọc span Client.

Hai file **độc lập**: import theo thứ tự nào cũng được. Mỗi dashboard có một ô Markdown ở đầu trang chứa link sang dashboard
kia theo **id cố định ở bảng trên**, nên link chỉ bấm được khi cả hai đã được import (id được giữ nguyên khi import vào Kibana sạch).

## Cách import

Lệnh dưới dùng `curl.exe` (có sẵn từ Windows 10+) thay vì `Invoke-RestMethod ... -Form`, vì tham số `-Form` chỉ có từ
PowerShell 6.1+ trở lên — môi trường này chạy **Windows PowerShell 5.1**, không hỗ trợ `-Form`.

```powershell
curl.exe -X POST "http://localhost:5601/api/saved_objects/_import?overwrite=true" `
  -H "kbn-xsrf: true" `
  --form "file=@docs/kibana-quan-sat-he-thong/dashboards/xu-ly-su-co.ndjson"

curl.exe -X POST "http://localhost:5601/api/saved_objects/_import?overwrite=true" `
  -H "kbn-xsrf: true" `
  --form "file=@docs/kibana-quan-sat-he-thong/dashboards/ngan-sach-loi-tuan.ndjson"
```

Hoặc qua UI: menu ☰ → **Stack Management** → **Saved Objects** → **Import** → chọn từng file → tick
**Automatically overwrite conflicts** nếu đang cập nhật bản cũ.

Hai file dùng chung các saved object `index-pattern` và saved search (`slo-error-budget-*`,
`incident-fast-detection-active-alerts`): `overwrite=true` ghi đè bản trên Kibana bằng bản trong file.

## Cách export lại (sau khi sửa dashboard)

Export từng dashboard theo đúng id của nó, kèm mọi saved object nó tham chiếu (`includeReferencesDeep`):

```powershell
$KibanaBase = "http://localhost:5601"
function Export-Dashboard($Id, $OutFile) {
  $body = @{
    objects = @(@{ type = "dashboard"; id = $Id })
    includeReferencesDeep = $true
  } | ConvertTo-Json -Depth 5
  Invoke-WebRequest -Uri "$KibanaBase/api/saved_objects/_export" -Method Post `
    -Headers @{ "kbn-xsrf" = "true" } -ContentType "application/json" -Body $body -OutFile $OutFile
}
Export-Dashboard "e61fc7f3-17fe-428a-a373-da88af0a4a1e" "docs/kibana-quan-sat-he-thong/dashboards/xu-ly-su-co.ndjson"
Export-Dashboard "2a607bf4-2449-48a1-a2e8-1336ec35a7b7" "docs/kibana-quan-sat-he-thong/dashboards/ngan-sach-loi-tuan.ndjson"
```

Đừng thêm panel **Links** (tham chiếu dashboard đích bằng saved object) giữa hai dashboard: nó tạo vòng tham chiếu nên mỗi
lần export sâu sẽ kéo cả hai dashboard vào cùng một file. Link giữa hai dashboard là ô Markdown theo id cố định.

## Lưu ý khi sửa panel

- Dashboard **Ngân sách tuần**: mỗi panel đặt khoảng thời gian riêng `now-30d` để không bị thanh thời gian cắt; điều khiển `Tuần`
  (biến ES|QL `?tuan_chon`: `Tuần này` / `Tuần trước` / `2 tuần trước` / `3 tuần trước`) nằm trong cùng file. Panel ES|QL mới thêm vào
  dashboard này phải tự đặt khoảng thời gian riêng và dùng `?tuan_chon`, nếu không sẽ đổi theo thanh thời gian.
- Dashboard **Xử lý sự cố**: không panel nào được đặt khoảng thời gian riêng hay `NOW() - ...` trong truy vấn.
- Panel **Log lỗi gần nhất** dùng index-pattern `logs-generic.otel-default*` (id `logs-generic-otel-default`); cột `trace_id` có định dạng URL mở Discover
  lọc theo trace đó. Kibana APM không đọc được dữ liệu trace OTel thô của dự án nên không dùng link APM.
