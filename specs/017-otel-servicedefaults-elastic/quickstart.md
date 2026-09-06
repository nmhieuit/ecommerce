# Quickstart: Kiểm chứng OTel traces/metrics/logs qua ServiceDefaults tới Elastic

Kiểm chứng tính năng này theo ba Acceptance Criteria và ba Test Scenario của vé Jira SCRUM-25. Xem [data-model.md](data-model.md) cho hình dạng đường ống telemetry và [contracts/otel-collector-elasticsearch-export-contract.md](contracts/otel-collector-elasticsearch-export-contract.md) cho hợp đồng collector → Elasticsearch đầy đủ.

## Prerequisites

- Docker Desktop đang chạy. Trên Windows/WSL2, nếu Elasticsearch thoát ngay với log `max virtual memory areas vm.max_map_count [...] is too low`, chạy một lần: `wsl -d docker-desktop sysctl -w vm.max_map_count=262144` (research.md Decision 6).
- Stack chạy qua `docker-compose.local.yml` (mọi cổng được publish, gồm Elasticsearch `9200` và Kibana `5601` mới thêm ở tính năng này):

```bash
./scripts/local-up.ps1        # hoặc ./scripts/local-up.sh
```

- Một token hợp lệ (client `integration-test-ropc`, xem [014-identity-server-auth](../014-identity-server-auth/)).
- Chờ `kibana` chuyển sang healthy trước khi mở UI (lần đầu khởi động Elasticsearch/Kibana có thể mất 30-60s):

```bash
docker compose -f docker-compose.local.yml ps kibana
```

## Validation Scenarios

### Scenario 1 — Không còn log call nào dùng chuỗi nội suy (Jira AC3, Test Scenario 2)

```bash
grep -rnE 'Log(Information|Warning|Error|Debug|Critical|Trace)\(\$"' services/
```

**Expected**: Không trả về dòng nào. Đây là một phát hiện đã đúng từ trước tính năng này (research.md Decision 1) — quickstart ghi lại như một bước hồi quy, không phải một thay đổi mới cần thực hiện.

### Scenario 2 — Đặt một đơn hàng, tìm trace đầy đủ xuyên suốt 4 domain service trong Kibana (Jira AC2, Test Scenario 1)

1. Đặt một đơn hàng qua storefront (`http://localhost:4173`) hoặc thẳng qua BFF:

   ```bash
   CID="quickstart-order-$(date +%s)"
   curl -i -X POST http://localhost:5301/bff/checkout \
     -H "Authorization: Bearer <token>" -H "X-Correlation-Id: $CID"
   ```

2. Mở Kibana (`http://localhost:5601`) → **Observability → Traces** (hoặc **Discover** trên data stream `traces-generic.otel-default`), lọc theo `correlation.id: "$CID"` (hoặc `attributes.correlation.id`, tuỳ chế độ hiển thị của phiên bản Kibana đã ghim).

**Expected**: Thấy span của cả 3 domain service tham gia checkout — `Baskets.Api`, `Orders.Api` — cộng `Bff.Api` làm điều phối, đúng như bảng "Hop lan truyền" của [016-correlation-id-propagation/data-model.md](../016-correlation-id-propagation/data-model.md) đã mô tả cho nhánh đồng bộ. Không có hop nào bị đứt đoạn.

**Đối chiếu với bằng chứng đã có**: `scripts/demo.ps1` vẫn tiếp tục dùng `docker compose logs otel-collector` (exporter `debug`, không đổi — research.md Decision 4) để tự động xác nhận 5 hop (`Gateway.Api`, `Bff.Api`, `Products.Api`, `Baskets.Api`, `Orders.Api`) đã phục vụ một lần demo — Scenario 2 ở đây là cách thứ hai để xem cùng bằng chứng đó, qua Kibana thay vì log thô.

### Scenario 3 — Metrics của từng service quan sát được trong Elasticsearch (Jira AC2)

```bash
curl -s "http://localhost:9200/metrics-generic.otel-default*/_search?size=0" \
  -H 'Content-Type: application/json' \
  -d '{"aggs":{"by_service":{"terms":{"field":"resource.attributes.service.name"}}}}'
```

**Expected**: Kết quả liệt kê cả 7 service (`Gateway.Api`, `Bff.Api`, `Identity.Api`, `Parties.Api`, `Products.Api`, `Baskets.Api`, `Orders.Api`) trong bucket `by_service` — xác nhận metrics của mọi service đều tới được Elasticsearch, không chỉ traces.

### Scenario 4 — Một log entry mang đủ service, tenant, và correlation identifier (Jira AC3)

Dùng đúng `$CID` từ Scenario 2, trong Kibana → **Discover** trên data stream `logs-generic.default` (hoặc `logs-generic.otel-default`, tuỳ phiên bản), truy vấn KQL:

```text
attributes.CorrelationId : "quickstart-order-..."
```

**Expected**: Log entry trả về mang cả ba: `resource.attributes.service.name` (ví dụ `Orders.Api`), `attributes.TenantId` (khi request đã resolve tenant), và `attributes.CorrelationId` khớp `$CID` — đúng data-model.md mục "Định danh mang theo trên mỗi tín hiệu".

### Scenario 5 — Gỡ ServiceDefaults khỏi một service, xác nhận mất telemetry (Jira Test Scenario 3)

Bước thủ công, có chủ đích không tự động hoá (research.md Decision 8):

1. Trong `services/products/src/Products.Api/Program.cs`, tạm comment hai dòng gọi `builder.AddServiceDefaults()` và `app.UseServiceDefaults()`.
2. Rebuild và khởi động lại riêng service đó:

   ```bash
   docker compose -f docker-compose.local.yml up -d --build products-api
   ```

3. Gọi vài request tới Products (`curl http://localhost:5088/products -H "Authorization: Bearer <token>" -H "X-Correlation-Id: no-servicedefaults-test"`).
4. Tra Kibana/Elasticsearch (như Scenario 2-4) theo `no-servicedefaults-test` hoặc `service.name: "Products.Api"` trong khoảng thời gian vừa gọi.

**Expected**: Không có trace/metric/log mới nào từ `Products.Api` xuất hiện trong Elasticsearch cho các request vừa gọi — xác nhận ServiceDefaults thực sự chịu tải cho toàn bộ observability của service đó, không phải một lớp cấu hình có thể bỏ qua.

5. Khôi phục lại hai dòng đã comment, rebuild lại service, xác nhận telemetry trở lại bình thường trước khi tiếp tục làm việc khác.

## Automated Coverage

Tính năng này không thêm test tự động mới (research.md Decision 1, 8) — mọi thay đổi nằm ở hạ tầng (`docker-compose*.yml`, `docker/otel-collector-config*.yaml`). Bộ kiểm chứng tự động hiện có tiếp tục là bằng chứng chính:

- `scripts/demo.ps1` (006-e2e-order-demo) — vẫn xác nhận 5 hop qua exporter `debug`, không đổi.
- Grep Scenario 1 ở trên có thể thêm vào PR gate như một bước kiểm tra tĩnh nếu muốn khoá lại bất biến này (quyết định thuộc `/speckit-tasks`, không bắt buộc bởi spec).
