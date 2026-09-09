# ADR-0001: Sản phẩm nhà cung cấp định danh (Identity Provider)

*(Bản dịch tiếng Việt của [`0001-identity-provider.md`](0001-identity-provider.md) — bản gốc tiếng
Anh vẫn được giữ nguyên, không sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn đúng.)*

**Trạng thái:** Đã chấp thuận (Accepted)
**Ngày:** 2026-08-14
**Người quyết định:** Platform maintainers

## Bối cảnh

Constitution (Principle VI, Technology Constraints) cố định *mô hình* — 1 máy chủ định danh trung tâm
phát hành token, được xác thực độc lập ở gateway và ở từng service — nhưng không cố định sản phẩm cụ
thể. Các ràng buộc ảnh hưởng tới lựa chọn này:

- Principle VII yêu cầu mọi service phát telemetry qua 1 thành phần C# `ServiceDefaults` dùng chung,
  "không cấu hình riêng lẻ theo từng service."
- Principle V yêu cầu danh tính tenant được resolve tại edge và mang theo dưới dạng claim tường minh
  qua mọi chặng.
- Nền tảng tự host trên Kubernetes, cấp phát bởi Ansible — không gắn với 1 nhà cung cấp cloud cụ thể.
- Mọi thành phần khác trong hạm đội đều là container C#/.NET, triển khai giống hệt nhau qua cùng 1
  pipeline Jenkins.

## Quyết định

Dùng **Duende IdentityServer**, triển khai như 1 service ASP.NET Core theo cùng mô hình
container/pipeline/Ansible với mọi service khác.

## Các phương án đã cân nhắc

### Phương án A: Duende IdentityServer
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Trung bình — code-first, nhưng multi-tenancy (ánh xạ client/tenant, claim tuỳ biến) phải tự xây |
| Chi phí | Bản quyền thương mại, giá theo mức doanh thu |
| Khả năng mở rộng | Cao — ASP.NET Core không trạng thái, scale như mọi service khác |
| Độ quen thuộc của đội | Cao — cùng ngôn ngữ, cùng mô hình triển khai với phần còn lại của nền tảng |

**Ưu điểm:** Triển khai giống hệt mọi service khác (cùng pipeline Jenkins, cùng Ansible playbook, cùng
manifest K8s); dùng trực tiếp thành phần telemetry `ServiceDefaults` dùng chung, nên không trở thành hệ
thống duy nhất phải tự viết observability riêng; việc phát hành claim tenant (`IProfileService`) và
cấu hình client theo từng tenant là các điểm mở rộng C# thông thường mà đội đã quen; không đưa thêm 1
runtime mới (JVM...) vào hạm đội.
**Nhược điểm:** Chi phí bản quyền thương mại tăng theo doanh thu công ty; không có khái niệm "realm"
dựng sẵn cho cô lập tenant — multi-tenancy phải được mô hình hoá tường minh trong code/cấu hình thay vì
cấu hình sẵn có.

### Phương án B: Keycloak
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Trung bình — admin UI trưởng thành, nhưng bề mặt vận hành Java/JVM |
| Chi phí | Miễn phí/mã nguồn mở |
| Khả năng mở rộng | Cao — đã được kiểm chứng ở quy mô lớn |
| Độ quen thuộc của đội | Thấp — nền tảng Java, ngoài bộ kỹ năng chỉ-C# của nền tảng |

**Ưu điểm:** Miễn phí; đã được kiểm chứng ở quy mô lớn; mô hình realm-per-tenant là 1 tính năng
multi-tenancy hạng nhất, cấu hình được qua admin UI, ánh xạ trực tiếp vào Principle V; hỗ trợ giao
thức rộng (OIDC, SAML) nếu 1 tenant nào đó sau này cần SSO doanh nghiệp.
**Nhược điểm:** Runtime không-phải-.NET duy nhất trong hạm đội — không dùng được thành phần C#
`ServiceDefaults` dùng chung, nên cần tự xây observability riêng, mâu thuẫn với "không cấu hình riêng
lẻ theo từng service" của Principle VII; đưa thêm kiến thức vận hành JVM (tinh chỉnh heap, GC, nhịp độ
nâng cấp) mà đội không cần cho phần còn lại; logic claim tuỳ biến vượt ngoài khả năng của protocol
mapper cần 1 plugin Java SPI.

### Phương án C: Azure Entra External ID / Auth0 / Okta (CIAM được quản lý)
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Thấp — chủ yếu là cấu hình |
| Chi phí | Chi phí định kỳ theo SaaS/MAU |
| Khả năng mở rộng | Cao (do nhà cung cấp quản lý) |
| Độ quen thuộc của đội | Trung bình |

**Ưu điểm:** Gánh nặng vận hành thấp nhất — không cần chạy hạ tầng định danh nào cả; nhà cung cấp lo
uptime/vá lỗi/scale.
**Nhược điểm:** Đưa vào 1 dependency ngoài và chi phí định kỳ mà phần còn lại của nền tảng tự host,
cấp phát bởi Ansible/K8s này không có; riêng Azure Entra gắn nền tảng với 1 nhà cung cấp cloud mà nó
chưa hề cam kết; độ linh hoạt về claim tuỳ biến/thương hiệu theo từng tenant bị hạn chế hơn 1 phương án
tự host, code-first.

## Phân tích đánh đổi

Yếu tố quyết định là **tính nhất quán vận hành**, không phải danh sách tính năng: mọi thành phần khác
của nền tảng này — service, gateway, BFF — đều là container C# được quan sát qua đúng 1 thành phần
telemetry dùng chung và triển khai qua đúng 1 pipeline. Runtime Java của Keycloak phá vỡ âm thầm tính
nhất quán đó ở đúng lớp (định danh) mà MỌI request đều đi qua. Chi phí bản quyền của Duende là có
thật, nhưng đây là phương án duy nhất giữ được định danh bên trong bộ kỹ năng, mô hình triển khai, và
đảm bảo observability sẵn có của nền tảng.

## Hệ quả

- Code máy chủ định danh do chính đội nền tảng sở hữu và bảo trì, không phải nhà cung cấp — nâng cấp,
  CVE, và tuân thủ đặc tả OIDC là trách nhiệm của đội.
- Multi-tenancy (client-theo-từng-tenant, phát hành claim tenant) phải được thiết kế và review tường
  minh — không có sẵn khái niệm realm để dựa vào.
- Phải dự trù và theo dõi ngân sách cho bản quyền thương mại; xem lại nếu chi phí bản quyền trở nên
  không tương xứng với doanh thu nền tảng.

## Việc cần làm

1. [ ] Mua bản quyền Duende IdentityServer phù hợp với mức doanh thu hiện tại
2. [X] Thiết kế mô hình ánh xạ tenant → client/claim và đưa đi review như 1 thay đổi đường đi resolve
   tenant (constitution yêu cầu review bởi người duy trì service sở hữu cho việc này) — đã hoàn thành ở
   [014-identity-server-auth/data-model.md](../../specs/014-identity-server-auth/data-model.md):
   `TenantClaimsProfileService` phát hành `tenant_id` từ `ApplicationUser.TenantId` (1 tenant cho mỗi
   Identity User), và `Config.cs` ánh xạ mỗi client vào scope `ecommerce-api` của nó
3. [ ] Nối telemetry `ServiceDefaults` vào project máy chủ định danh
