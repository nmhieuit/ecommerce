# ADR-0007: Phân phối secrets vào cluster (Secrets Delivery)

*(Bản dịch tiếng Việt của [`0007-secrets-delivery.md`](0007-secrets-delivery.md) — bản gốc tiếng Anh
vẫn được giữ nguyên, không sửa; nếu 2 bản lệch nhau, bản gốc tiếng Anh là nguồn đúng.)*

**Trạng thái:** Đã chấp thuận | **Ngày:** 2026-08-14 | **Người quyết định:** Platform maintainers

## Bối cảnh

Principle VI yêu cầu secrets phải được tiêm (inject) lúc chạy (runtime) từ kho secret của cluster,
không bao giờ được đóng cứng trong image, source code, hay file cấu hình. Nền tảng tự host K8s, cấp
phát bởi Ansible — không gắn với KMS của 1 nhà cung cấp cloud cụ thể.

## Quyết định

Dùng **External Secrets Operator (ESO)**, dựa trên 1 instance **HashiCorp Vault** tự host làm nguồn
sự thật (source of truth).

## Các phương án đã cân nhắc

### Phương án A: External Secrets Operator + Vault
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Trung bình — 2 thành phần, nhưng là 1 cặp đã được hiểu rõ |
| Chi phí | Chi phí vận hành để chạy Vault HA |
| Khả năng mở rộng | Cao |
| Độ quen thuộc của đội | Thấp lúc đầu |

**Ưu điểm:** Manifest ứng dụng chỉ tham chiếu tới object K8s Secret thông thường, đã được hiểu rõ —
pod không cần tích hợp Vault client/sidecar gốc; ESO lo việc đồng bộ/xoay vòng (rotation) với Vault;
Vault mang lại các đặc tính bảo mật thật sự (credential ngắn hạn, động cho SQL Server/RabbitMQ, chính
sách truy cập theo từng tenant/service, ghi log audit) ánh xạ trực tiếp vào mô hình cô lập theo tenant
của Principle V; tách rời "secrets nằm ở đâu" khỏi "pod tiêu thụ chúng ra sao", nên kho lưu trữ phía
sau có thể đổi sau này mà không đụng vào manifest ứng dụng.
**Nhược điểm:** 2 hệ thống cần chạy và giữ luôn sẵn sàng; bản thân Vault cần thiết lập HA, unsealing,
storage backend, và backup/DR đúng cách — 1 gánh nặng vận hành thật sự mới cho 1 đội Ansible/K8s.

### Phương án B: HashiCorp Vault + Agent Injector/CSI driver (không có ESO)
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Cao — mọi pod cần cấu hình sidecar/init biết về Vault |
| Chi phí | Chi phí vận hành Vault tương tự Phương án A |
| Khả năng mở rộng | Cao |
| Độ quen thuộc của đội | Thấp |

**Ưu điểm:** Cùng các đặc tính bảo mật của Vault như Phương án A, với secrets hoàn toàn không bao giờ
trở thành object K8s Secret tĩnh (được tiêm trực tiếp vào filesystem/env của pod lúc chạy).
**Nhược điểm:** Manifest triển khai của TỪNG service đều cần annotation/cấu hình sidecar đặc thù cho
Vault — gắn chặt vào Vault theo từng service nhiều hơn mô hình "chỉ là 1 K8s Secret bình thường" của
ESO, và gánh nặng review cao hơn mỗi khi thêm 1 service mới.

### Phương án C: Chỉ dùng secrets gốc của K8s, không có kho lưu trữ ngoài
| Khía cạnh | Đánh giá |
|---|---|
| Độ phức tạp | Thấp |
| Chi phí | Không |
| Khả năng mở rộng | Cao |
| Độ quen thuộc của đội | Cao |

**Ưu điểm:** Thiết lập đơn giản nhất có thể — không cần chạy gì mới.
**Nhược điểm:** Không có credential động/ngắn hạn, không có dấu vết audit tập trung cho việc truy cập
secret, không có cơ chế xoay vòng ngoài việc cập nhật thủ công; secrets khi lưu trữ chỉ an toàn tương
đương mã hoá etcd, đây là 1 mối lo riêng cần làm đúng; không cải thiện đáng kể so với mức tối thiểu
"tiêm lúc chạy" mà constitution yêu cầu.

## Phân tích đánh đổi

Phương án C về mặt câu chữ thoả mãn constitution nhưng không thoả tinh thần của nó — "tiêm lúc chạy"
mà không có xoay vòng hay audit là 1 tư thế bảo mật yếu cho 1 hệ thống xử lý PII và dữ liệu liên quan
thanh toán (Principle VI cũng bắt buộc mã hoá PII và các biện pháp giảm thiểu OWASP). Giữa 2 phương án
dựa trên Vault, ESO được chọn vì nó giữ mọi manifest ứng dụng đơn giản và không phụ thuộc trực tiếp
vào Vault, gói gọn độ phức tạp đặc thù-Vault vào 1 operator duy nhất thay vì trải rộng nó ra khắp cấu
hình triển khai của từng service.

## Hệ quả

- Vault phải được triển khai với HA và backup/DR thật ngay từ ngày đầu — trì hoãn việc này rồi di
  chuyển sau sẽ tốn kém.
- Chính sách xoay vòng secret (secret nào là động, secret nào là tĩnh) cần được định nghĩa theo từng
  loại dependency (SQL Server, RabbitMQ, khoá ký của máy chủ định danh).

## Việc cần làm

1. [ ] Triển khai Vault tự host với storage backend HA qua Ansible
2. [ ] Cài ESO và định nghĩa `SecretStore`/`ExternalSecret` đầu tiên cho 1 service làm thí điểm
3. [ ] Định nghĩa chính sách credential động cho việc truy cập SQL Server và RabbitMQ

## Bổ sung (2026-09-06): phần hợp đồng phía ứng dụng đã ra mắt; bản thân Vault/ESO thì chưa

`specs/018-cluster-secret-store` (Jira SCRUM-27) đã hiện thực nửa phần của quyết định này mà KHÔNG
đòi hỏi hạ tầng nêu trên phải tồn tại trước: file `appsettings.Development.json` đã commit của mọi
service backend không còn mang mật khẩu database dạng literal nữa (trước đây là
`Password=Change_Me_Local_Dev_Only!`, đã gỡ bỏ để chuyển sang dùng workflow `dotnet user-secrets`
cục bộ); `shared/ServiceDefaults/RequiredSecretsValidation.cs` bổ sung validate lúc khởi động theo
kiểu fail-fast (`AddRequiredSecretsValidation`), để 1 service chưa từng nhận được secret của nó sẽ từ
chối khởi động thay vì chạy lay lắt với 1 connection string thiếu credential; 1 stage
`ci/secret-scan` bằng gitleaks và 1 stage `ci/image-secret-scan` bằng Trivy đã được thêm vào pipeline
Jenkins (`specs/018-cluster-secret-store/contracts/ci-secret-scan-stage-contract.md`); và
`deploy/k8s/<service>/{external-secret.yaml,secret.example.yaml}` đã được viết ra như hợp đồng
manifest có thể review được mà quyết định ESO của ADR này ngụ ý, cho orders/baskets/parties/products/
identity.

Danh sách Việc cần làm ở trên vẫn giữ nguyên và vẫn còn hoàn toàn mở — không có instance Vault nào và
không có việc cài đặt ESO nào tồn tại ở bất kỳ đâu cho nền tảng này tính tới thời điểm bổ sung này.
Code ứng dụng giờ phụ thuộc đúng vào hình dạng object `Secret` mà Việc cần làm mục 2 mô tả, nhưng chưa
có gì thực sự tạo ra 1 object như vậy ngoài việc `kubectl apply` thủ công dựa trên 1 file ví dụ
placeholder. Xem `specs/018-cluster-secret-store/research.md` Quyết định 1 để biết lý do giới hạn
phạm vi, và `deploy/k8s/README.md` để biết những gì tồn tại và chưa tồn tại trong repository này hôm
nay.
