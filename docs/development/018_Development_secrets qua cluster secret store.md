# Bước 018: Thay đổi nghiệp vụ so với bước 017

## Phạm vi

Bước 018 loại bỏ credential hardcode khỏi `appsettings.Development.json` của 5 service có database
(Baskets, Orders, Parties, Products, Identity), thêm cơ chế fail-fast khi thiếu secret, và thêm 2
stage CI quét secret. Toàn bộ nằm trong một commit duy nhất.

Ranh giới commit được xác định từ lịch sử Git:

- Mốc hoàn tất bước 017: commit `06248c5`.
- Đặc tả và triển khai bước 018 — mốc hoàn tất: commit `46f0a61`.

Nội dung kiểm thử (`RequiredSecretsValidationTests.cs`, `RequiredSecretsFailFastTests.cs`,
`RequiredSecretsTestSupport.cs`) bị loại theo đúng phạm vi đã áp dụng.

## 1. `RequiredSecretsValidation.cs` — fail-fast, không chỉ check rỗng

[shared/ServiceDefaults/RequiredSecretsValidation.cs](../../shared/ServiceDefaults/RequiredSecretsValidation.cs)

```csharp
// 018: một secret service không thể khởi động thiếu nó. Name cũng là bề mặt hợp đồng — phải
// khớp key dùng ở appsettings*.json/biến môi trường VÀ deploy/k8s/<service>/external-secret.yaml.
public sealed record RequiredSecret(string Name, Func<IConfiguration, string?> Resolve)
{
    public static RequiredSecret ConnectionString(string name) =>
        new($"ConnectionStrings:{name}", configuration =>
        {
            var value = configuration.GetConnectionString(name);

            // 018: appsettings.json (Production) CỐ TÌNH vẫn giữ 1 connection string hợp lệ chỉ
            // có host/database, không credential — string đó không bao giờ rỗng dù cluster chưa
            // inject secret nào. Resolve về null (coi là thiếu) trừ khi thật sự có credential.
            return HasCredential(value) ? value : null;
        });

    private static bool HasCredential(string? connectionString) =>
        connectionString is not null &&
        (Contains(connectionString, "Password=") ||
         Contains(connectionString, "Pwd=") ||
         Contains(connectionString, "Integrated Security=true") ||
         Contains(connectionString, "Trusted_Connection=true"));
}

// 018: fail-fast — secret thiếu/rỗng là lỗi khởi động, không phải lỗi hoãn tới lần đầu gọi
// dependency. Message không bao giờ chứa GIÁ TRỊ thật của secret, chỉ TÊN secret bị thiếu.
public sealed class RequiredSecretsValidator(IConfiguration configuration) : IValidateOptions<RequiredSecretsOptions>
{
    public ValidateOptionsResult Validate(string? name, RequiredSecretsOptions options)
    {
        var missing = options.Secrets
            .Where(secret => string.IsNullOrWhiteSpace(secret.Resolve(configuration)))
            .Select(secret => secret.Name)
            .ToArray();

        if (missing.Length == 0)
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"Missing required secret(s): {string.Join(", ", missing)}. Each must be supplied at " +
            "runtime from the cluster secret store, never hardcoded.");
    }
}
```

## 2. `ServiceDefaultsExtensions` — mọi service khai báo secret cần theo cùng một cách

[shared/ServiceDefaults/ServiceDefaultsExtensions.cs](../../shared/ServiceDefaults/ServiceDefaultsExtensions.cs)

```csharp
// 018: đăng ký RequiredSecretsValidator và gọi ValidateOnStart() — generic host validate mọi
// secret đã khai báo TRƯỚC KHI app.Run() nhận request nào, fail với lỗi có cấu trúc nêu đúng
// tên secret thiếu.
public static TBuilder AddRequiredSecretsValidation<TBuilder>(this TBuilder builder, params RequiredSecret[] secrets)
    where TBuilder : IHostApplicationBuilder
{
    builder.Services.AddSingleton<IValidateOptions<RequiredSecretsOptions>, RequiredSecretsValidator>();
    builder.Services.AddOptions<RequiredSecretsOptions>()
        .Configure(options => options.Secrets = secrets)
        .ValidateOnStart();

    return builder;
}
```

[services/orders/src/Orders.Api/Program.cs](../../services/orders/src/Orders.Api/Program.cs)

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// 018: fail fast lúc khởi động nếu cluster chưa inject credential database của service này,
// thay vì khởi động xong rồi mới phát hiện ở request đầu tiên.
builder.AddRequiredSecretsValidation(RequiredSecret.ConnectionString("OrdersDb"));
```

Cùng dòng `AddRequiredSecretsValidation(RequiredSecret.ConnectionString("<Tên>Db"))` được thêm vào
`Program.cs` của Baskets, Parties, Products, Identity — mỗi service khai báo đúng một secret của
chính nó.

## 3. `appsettings.Development.json` không còn credential thật

[services/orders/src/Orders.Api/appsettings.Development.json](../../services/orders/src/Orders.Api/appsettings.Development.json)

```jsonc
// 018: trước đây có "ConnectionStrings": { "OrdersDb": "...;Password=Change_Me_Local_Dev_Only!;..." }
// commit thẳng trong repo — 018 xoá hẳn, thay bằng hướng dẫn dùng dotnet user-secrets.
"//ConnectionStrings": "specs/018-cluster-secret-store FR-001/FR-009: no credential is committed here. Running via docker-compose needs no action (ConnectionStrings__OrdersDb is already injected). Running `dotnet run` directly, set your own local secret once: dotnet user-secrets set \"ConnectionStrings:OrdersDb\" \"...Password=<your local MSSQL_SA_PASSWORD>...\" --project services/orders/src/Orders.Api. Without it, the service fails fast at startup."
```

Cùng cách xoá này áp dụng cho `appsettings.Development.json` của Baskets, Parties, Products, Identity.

## 4. `deploy/k8s/<service>/external-secret.yaml` — hợp đồng, không phải cấu hình sống

[deploy/k8s/orders/external-secret.yaml](../../deploy/k8s/orders/external-secret.yaml)

```yaml
apiVersion: external-secrets.io/v1
kind: ExternalSecret
metadata:
  name: orders-secrets
  namespace: ecommerce
spec:
  # 018: <=5 phút theo SC-004 (rotate trong Vault, không redeploy).
  refreshInterval: 5m
  secretStoreRef:
    name: vault-backend
    kind: ClusterSecretStore
  data:
    # 018: secretKey dùng đúng dạng biến môi trường của RequiredSecret.Name
    # (ConnectionStrings:OrdersDb -> ConnectionStrings__OrdersDb) — đây là bề mặt hợp đồng
    # PHẢI khớp giữa code và manifest.
    - secretKey: ConnectionStrings__OrdersDb
      remoteRef:
        key: secret/orders
        property: connectionstring
```

[deploy/k8s/orders/secret.example.yaml](../../deploy/k8s/orders/secret.example.yaml)

```yaml
# 018: CHỈ ĐỂ THAM KHẢO — không bao giờ apply lên cluster nào. Ghi lại hình dạng K8s Secret mà
# External Secrets Operator tạo ra, để reviewer xác nhận tên key mà không cần quyền truy cập Vault.
stringData:
  ConnectionStrings__OrdersDb: "REPLACE_ME"
```

5 cặp `external-secret.yaml`/`secret.example.yaml` (Baskets, Identity, Orders, Parties, Products)
được tạo theo đúng khuôn mẫu này. Đây là manifest khai báo — cluster Vault + External Secrets
Operator thật cho môi trường production chưa tồn tại (xem giới hạn phạm vi trong architecture doc).

## 5. CI thêm 2 stage secret-scanning, luôn chạy

[Jenkinsfile](../../Jenkinsfile)

```groovy
env {
    // 018: KHÔNG bị CI_FAST_ITERATION bỏ qua — cả hai đều không cần Docker/Testcontainers nên
    // skip không mua được tốc độ, và một gate secret-scan thỉnh thoảng tắt thì mất hết ý nghĩa.
    CHECK_SECRET_SCAN = 'ci/secret-scan'
    CHECK_IMAGE_SECRET_SCAN = 'ci/image-secret-scan'
}

// 018: quét toàn bộ lịch sử git, độc lập với stage 'build' — chạy bất kể build có qua hay không.
stage('secret scan') {
    steps {
        checkStarted(env.CHECK_SECRET_SCAN)
        sh 'scripts/ci/run-secret-scan.sh'
    }
}

// 018: cần image do stage 'build' tạo ra, nên chạy sau 'build'. Đây là chỗ duy nhất pipeline
// tự docker build image — trước đó pipeline chưa từng build image container ở đâu cả.
stage('image secret scan') {
    steps {
        checkStarted(env.CHECK_IMAGE_SECRET_SCAN)
        sh 'scripts/ci/run-image-secret-scan.sh'
    }
}
```

`scripts/ci/setup-branch-protection.sh` (013) được cập nhật thêm `ci/secret-scan` và
`ci/image-secret-scan` vào danh sách `contexts` — nhưng theo đúng comment trong chính script (đã
trích ở file 013), việc chạy lại script để áp dụng thật lên repository là hành động thấy được bởi mọi
collaborator, **không** được tự động thực hiện, để một repository administrator chạy thủ công.

## Tóm tắt 017 → 018

| Khu vực | Bước 017 | Bước 018 |
|---|---|---|
| Credential trong repo | `appsettings.Development.json` có password thật, commit thẳng | Đã xoá — chỉ hướng dẫn `dotnet user-secrets` |
| Khởi động thiếu secret | Service khởi động bình thường, lỗi khi chạm database | Fail-fast: `ValidateOnStart()` chặn trước `app.Run()` |
| K8s secret delivery | Chưa có | 5 cặp `external-secret.yaml`/`secret.example.yaml` (khai báo, chưa apply production) |
| CI | 5 stage (013): build/unit/integration/contract/quality-gate | Thêm 2 stage luôn chạy: `secret-scan` (gitleaks), `image-secret-scan` (Trivy) |
| `RequiredSecret` API dùng chung | Chưa có | `RequiredSecret.ConnectionString(name)` — 5 service cùng khai báo qua `AddRequiredSecretsValidation` |

**Kết luận:** bước 018 không thêm nghiệp vụ mua hàng mới. Nó đóng nửa "application-side" của
ADR-0007 (Vault + External Secrets Operator): loại credential hardcode khỏi repo, thêm cơ chế
fail-fast khi thiếu secret ở cả 5 service có database, và thêm 2 lớp phòng thủ CI (quét secret trong
git history lẫn trong image đã build) chạy vô điều kiện, không bị tắt bởi `CI_FAST_ITERATION`.

## 6. Shared project trong bước 018

Bước 018 không tạo shared project mới. Nó mở rộng `shared/ServiceDefaults` (001) với
`RequiredSecretsValidation.cs` và một extension method mới (`AddRequiredSecretsValidation`) trên
`ServiceDefaultsExtensions`. Không service nào cần thêm `ProjectReference` mới — `ServiceDefaults` đã
được cả 6 service tham chiếu từ bước 001.

[services/orders/src/Orders.Api/Orders.Api.csproj](../../services/orders/src/Orders.Api/Orders.Api.csproj)

```xml
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>

    <!-- 018: hỗ trợ workflow dotnet user-secrets đã ghi trong appsettings.Development.json,
         để `dotnet run` cục bộ (ngoài docker-compose) cấp được ConnectionStrings:OrdersDb
         mà không commit credential nào. -->
    <UserSecretsId>b995af8f-f244-44b5-8570-7046d0d74505</UserSecretsId>
</PropertyGroup>
```

Không có `ProjectReference` production mới nào — `<UserSecretsId>` chỉ là cấu hình cho công cụ
`dotnet user-secrets`, không phải tham chiếu tới shared project. Cùng thay đổi này lặp lại ở
Baskets, Parties, Products, Identity.

Đây là mẫu hình thứ hai (sau 015 với `shared/Identity`) mà một bước mở rộng năng lực của shared
project đã tồn tại mà không service nào cần đổi cách nó tham chiếu shared project đó.
