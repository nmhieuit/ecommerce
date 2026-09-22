using System.Security.Cryptography;

namespace EventContracts.UnitTests;

/// <summary>
/// Proves a published schema version cannot be silently edited (spec FR-003, FR-006, SC-002).
/// </summary>
/// <remarks>
/// <para>
/// <b>If one of these tests is failing for you, do not update the constant.</b> That is the
/// failure working as designed. A committed schema version is frozen: the sanctioned way to change
/// an event's shape is to add <c>{Event}.v{N+1}.schema.json</c> plus a matching
/// <c>{Event}V{N+1}</c> record, leaving the published version untouched, and then to add a *new*
/// constant here for the *new* file. Editing the constant below to match your edit defeats the
/// only thing standing between a consumer and an unannounced contract change.
/// </para>
/// <para>
/// The check is deliberately blunt: it hashes the whole document, so it fires on any edit at all,
/// including a cosmetic one. Classifying breaking versus non-breaking JSON Schema changes is a hard
/// problem this feature does not attempt (research.md Decision 3) — an occasional forced version
/// bump for a typo fix is a much cheaper mistake than a missed breaking change. Consumer-aware
/// compatibility analysis is ADR-0006/SCRUM-21's job, not this test's.
/// </para>
/// </remarks>
public sealed class SchemaImmutabilityTests
{
    /// <summary>SHA-256 of <c>OrderPlaced.v1.schema.json</c> as first published.</summary>
    private const string OrderPlacedV1Sha256 =
        "3518223B9534D182A8CD11564E671BE3E1420A9A027973CFD42DEE221CADD601";

    /// <summary>SHA-256 of <c>BasketCheckedOut.v1.schema.json</c> as first published.</summary>
    private const string BasketCheckedOutV1Sha256 =
        "4BCE5A5DF0A3B296F94AEAC6A08CAB711C593B377342BBF36AE910ECF0172DCF";

    /// <summary>
    /// Kiểm tra: SHA-256 của `OrderPlaced.v1.schema.json` (chuẩn hoá xuống dòng) bằng đúng hằng số
    /// đã đóng băng lúc công bố.
    /// Lý do: FR-003/FR-006/SC-002: 1 phiên bản đã công bố là bất biến — mọi chỉnh sửa (kể cả thêm
    /// trường bắt buộc mà không tạo phiên bản mới) làm test đỏ và chặn merge ở tầng unit của CI.
    /// Nếu test này đỏ thì KHÔNG sửa hằng số; phải thêm `OrderPlaced.v{N+1}.schema.json` và record
    /// mới. Cố ý thô (hash cả tài liệu) thay vì phân loại thay đổi phá vỡ/không phá vỡ (research.md
    /// Decision 3).
    /// Task nguồn: spec 008 (event schema có version) — T012, US2 (FR-003, FR-006, SC-002).
    /// </summary>
    [Fact]
    public void OrderPlaced_V1_Schema_Content_Is_Frozen()
    {
        // Phần kiểm chứng (Assert.True) nằm trong hàm phụ AssertSchemaUnchanged bên dưới.
        AssertSchemaUnchanged(EmbeddedSchema.OrderPlacedV1ResourceName, OrderPlacedV1Sha256);
    }

    /// <summary>
    /// Kiểm tra: SHA-256 của `BasketCheckedOut.v1.schema.json` bằng đúng hằng số đã đóng băng lúc
    /// công bố.
    /// Lý do: như test `OrderPlaced` bên trên, áp dụng cho event thứ hai — mỗi event có hằng số
    /// hash riêng để thay đổi 1 event không lọt qua khi chỉ cập nhật event kia.
    /// Task nguồn: spec 008 (event schema có version) — T012, US2 (FR-003, FR-006, SC-002).
    /// </summary>
    [Fact]
    public void BasketCheckedOut_V1_Schema_Content_Is_Frozen()
    {
        // Phần kiểm chứng (Assert.True) nằm trong hàm phụ AssertSchemaUnchanged bên dưới.
        AssertSchemaUnchanged(EmbeddedSchema.BasketCheckedOutV1ResourceName, BasketCheckedOutV1Sha256);
    }

    private static void AssertSchemaUnchanged(string resourceName, string expectedSha256)
    {
        var actual = Convert.ToHexString(
            SHA256.HashData(EmbeddedSchema.ReadNormalisedBytes(resourceName)));

        // Assert.True(điều kiện, thông báo): xanh khi điều kiện đúng, thông báo hiện khi đỏ. Điều kiện:
        // SHA-256 hiện tại của file schema bằng hằng số đã chốt lúc công bố (so chuỗi phân biệt
        // hoa/thường). Đỏ khi ai đó sửa schema đã công bố; thông báo nêu cả mã kỳ vọng lẫn thực tế.
        Assert.True(
            string.Equals(expectedSha256, actual, StringComparison.Ordinal),
            $"'{resourceName}' has changed since it was published (expected SHA-256 " +
            $"{expectedSha256}, got {actual}). A published schema version is immutable: ship the " +
            "change as a new version file and a new record type instead of editing this one, and " +
            "add a new constant here for the new file. Do not update the constant above.");
    }
}
