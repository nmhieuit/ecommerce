# Hợp đồng: Cấu hình service sau khi loại bỏ secret hardcode

**Feature**: [../spec.md](../spec.md) | Liên quan: FR-001, FR-002, FR-007, FR-009

Hợp đồng này quy định hình dạng bắt buộc của `appsettings.json` / `appsettings.Development.json` cho mọi service backend sau khi feature này hoàn thành. Đây là hợp đồng nội bộ giữa mã ứng dụng và cơ chế cấp phát secret (K8s Secret do ESO đồng bộ), không phải API/event contract công khai.

## Quy tắc bắt buộc

1. **Không giá trị secret literal**: `appsettings.json` và `appsettings.*.json` (mọi biến thể, đã commit) KHÔNG được chứa giá trị mật khẩu, khóa, hay token thật dưới bất kỳ hình thức nào (plain text, base64, hay bất kỳ encoding nào).
2. **Connection string chỉ chứa phần không nhạy cảm**: `ConnectionStrings:*` trong file đã commit chỉ được chứa host, port, database name, và tham số không nhạy cảm khác (vd: `Encrypt=True`). Phần credential (`User Id`, `Password`) PHẢI vắng mặt trong file — được ASP.NET Core hợp nhất (merge) từ biến môi trường tại runtime theo cơ chế cấu hình phân lớp chuẩn (configuration layering) của .NET.
3. **Tên biến môi trường là hợp đồng, không phải giá trị**: mỗi `RequiredSecret.Name` (xem [data-model.md](../data-model.md)) tương ứng một biến môi trường theo naming convention chuẩn của .NET (vd: `ConnectionStrings__Default`, `Jwt__SigningKey`). Đổi tên biến này là một breaking change đối với `ExternalSecretBinding` tương ứng và PHẢI cập nhật đồng thời trong cùng một PR (tương tự nguyên tắc "stage name is a contract" của ADR-0012 §Consequences).
4. **Local dev (docker-compose, `.env`, .NET User Secrets) được miễn trừ khỏi yêu cầu "inject từ cluster secret store"** nhưng vẫn PHẢI tuân thủ quy tắc 1 — không giá trị secret thật nào được commit, kể cả giá trị "yếu"/placeholder dùng cho dev.
5. **Fail-fast khi thiếu**: nếu một `RequiredSecret` không resolve được giá trị hợp lệ khi service khởi động, service PHẢI dừng khởi động và log lỗi có cấu trúc nêu tên secret còn thiếu (không log giá trị).

## Ví dụ (minh hoạ, không phải nội dung file thật)

**Trước (vi phạm)**:
```json
"ConnectionStrings": {
  "Default": "Server=orders-db;Database=Orders;User Id=sa;Password=Change_Me_Local_Dev_Only!;"
}
```

**Sau (tuân thủ)**:
```json
"ConnectionStrings": {
  "Default": "Server=orders-db;Database=Orders;Encrypt=True;"
}
```
với `User Id`/`Password` được cấp qua biến môi trường `ConnectionStrings__Default__UserId` / `...__Password`, hoặc qua một biến môi trường chứa toàn bộ connection string hoàn chỉnh do K8s Secret cung cấp (tuỳ cách team chọn triển khai cụ thể ở tasks.md — hợp đồng ở đây chỉ ràng buộc: **không có credential trong file đã commit**, không quy định chính xác cách chia nhỏ biến môi trường).

## Kiểm chứng hợp đồng

- `ci/secret-scan` (gitleaks) và `ci/image-secret-scan` (Trivy) là cơ chế thực thi tự động cho quy tắc 1–2 (xem [ci-secret-scan-stage-contract.md](./ci-secret-scan-stage-contract.md)).
- Integration test khởi động service với biến môi trường bắt buộc bị thiếu, xác nhận service dừng khởi động đúng theo quy tắc 5 (xem [../quickstart.md](../quickstart.md)).
