# Quickstart: kiểm chứng tài liệu luyện troubleshoot (032)

Tham chiếu: [spec.md](./spec.md), [contracts/](./contracts/), [data-model.md](./data-model.md).

## Điều kiện

- Stack chạy bằng `docker-compose.local.yml`; `.env` có `CHAOS_ALLOW_FAULT_INJECTION=true` (nhóm 3 còn cần
  `CHAOS_ALLOW_LATENCY_INJECTION=true`).
- Elasticsearch/Kibana có dữ liệu `traces-generic.otel-default*`, `logs-generic.otel-default*`,
  `metrics-generic.otel-default*`.
- Hỏi người dùng trước khi bật stack và trước khi dọn Elastic.

## 1. Test quy ước tài liệu (không cần Docker)

```bash
dotnet test tests/TroubleshootGuideConventionTests
```

Kỳ vọng: đỏ khi chưa có file 09–17; xanh khi đủ. Làm hỏng cố ý một link hoặc đổi tên một file nhóm → test đỏ.

## 2. Một nhóm có kịch bản (dạng a) theo file hướng dẫn

```powershell
./scripts/incident-drill.ps1 -Inject -Type E -Target orders-db -DurationSeconds 600
```

Làm theo file nhóm tương ứng (ví dụ `12-ha-tang-dung.md`) từng bước; kết thúc bằng
`./scripts/incident-drill.ps1 -Restore -RunId <runId>` rồi truy vấn 15 phút ở file 08. Kỳ vọng: số đo thấy khớp
số ghi trong file (trong sai số theo máy) và container đích healthy.

## 3. Lỗi bất ngờ (dạng b) theo file 17

```powershell
./scripts/incident-drill.ps1 -Start
./scripts/incident-drill.ps1 -Hint -RunId <runId> -Level 1
```

Tra file 17 theo triệu chứng thấy được, mở dần mức 1 → 2 → 3, đối chiếu `-Reveal`. Kỳ vọng: mức 1–2 không lộ đáp án.

## 4. Postman

Chạy folder `32 - Luyện troubleshoot`, subfolder nhóm vừa tiêm: request quan sát triệu chứng thấy bất thường khi đang
tiêm và request xác nhận khỏi thấy bình thường sau `-Restore`.

## 5. Rà soát cuối

- `git diff master --stat` không có thay đổi dưới `scripts/`, `services/`, `shared/`, `docker/`, `deploy/` (SC-006).
- `00-tong-quan-lo-trinh.md` không còn "Bộ 4 file"/"cả 6 file", liệt kê đủ 01–17 (SC-005).
