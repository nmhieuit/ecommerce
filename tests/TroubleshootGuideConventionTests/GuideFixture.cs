using System.Text.Json;
using System.Text.RegularExpressions;

namespace TroubleshootGuideConventionTests;

/// <summary>Một nhóm lỗi đọc từ catalog.json (chỉ các trường test cần).</summary>
public sealed record CatalogGroup(int Id, string Name);

/// <summary>Một loại lỗi đọc từ catalog.json (chỉ các trường test cần).</summary>
public sealed record CatalogType(string Code, int GroupId);

/// <summary>
/// Dữ liệu dùng chung cho các test quy ước tài liệu luyện troubleshoot (spec 032): gốc repo, danh mục
/// nhóm/loại lỗi của spec 031 và các file `docs/kibana-quan-sat-he-thong/NN-*.md`.
/// </summary>
public static class GuideFixture
{
    /// <summary>File đánh dấu gốc repo — không dùng `.git` vì worktree có `.git` là file chứ không phải thư mục.</summary>
    private const string RepositoryRootMarker = "Ecommerce.slnx";

    /// <summary>Số thứ tự file hướng dẫn nhóm N là 08 + N (nhóm 1 → file 09 … nhóm 8 → file 16).</summary>
    public const int GroupFileOffset = 8;

    /// <summary>File gợi ý theo triệu chứng.</summary>
    public const int SymptomFileNumber = 17;

    /// <summary>Tên của 7 service (cả dạng `Xxx.Api` và `xxx-api`) — mức 1–2 của file 17 không được nhắc tới.</summary>
    public static readonly string[] ServiceNames =
        ["Gateway", "Bff", "Identity", "Parties", "Products", "Baskets", "Orders"];

    private static readonly Lazy<string> RootLazy = new(LocateRepositoryRoot);

    public static string RepositoryRoot => RootLazy.Value;

    public static string GuideDirectory => Path.Combine(RepositoryRoot, "docs", "kibana-quan-sat-he-thong");

    /// <summary>Gốc repo, tìm bằng cách đi ngược từ thư mục chạy test tới file đánh dấu.</summary>
    private static string LocateRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, RepositoryRootMarker)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            $"Không tìm thấy '{RepositoryRootMarker}' khi đi ngược từ '{AppContext.BaseDirectory}'.");
    }

    private static readonly Lazy<(IReadOnlyList<CatalogGroup> Groups, IReadOnlyList<CatalogType> Types)> CatalogLazy =
        new(ReadCatalog);

    public static IReadOnlyList<CatalogGroup> Groups => CatalogLazy.Value.Groups;

    public static IReadOnlyList<CatalogType> Types => CatalogLazy.Value.Types;

    /// <summary>Đọc `scripts/incident-drill/catalog.json` (chỉ đọc, spec 032 không được sửa file này).</summary>
    private static (IReadOnlyList<CatalogGroup>, IReadOnlyList<CatalogType>) ReadCatalog()
    {
        var path = Path.Combine(RepositoryRoot, "scripts", "incident-drill", "catalog.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        var groups = root.GetProperty("groups").EnumerateArray()
            .Select(g => new CatalogGroup(g.GetProperty("id").GetInt32(), g.GetProperty("name").GetString()!))
            .ToList();
        var types = root.GetProperty("types").EnumerateArray()
            .Select(t => new CatalogType(t.GetProperty("code").GetString()!, t.GetProperty("groupId").GetInt32()))
            .ToList();
        return (groups, types);
    }

    /// <summary>Các file `NN-*.md` trong thư mục hướng dẫn (có thể rỗng khi tài liệu chưa được viết).</summary>
    public static IReadOnlyList<string> FilesNumbered(int number) =>
        Directory.Exists(GuideDirectory)
            ? Directory.EnumerateFiles(GuideDirectory, $"{number:D2}-*.md").Order(StringComparer.Ordinal).ToList()
            : [];

    /// <summary>Đường dẫn file hướng dẫn nhóm; `null` khi không có đúng một file.</summary>
    public static string? GroupFile(int groupId) =>
        FilesNumbered(GroupFileOffset + groupId) is [var only] ? only : null;

    /// <summary>Đường dẫn file gợi ý theo triệu chứng; `null` khi không có đúng một file.</summary>
    public static string? SymptomFile() =>
        FilesNumbered(SymptomFileNumber) is [var only] ? only : null;

    /// <summary>Mọi file 09–17 hiện có (dùng cho test link).</summary>
    public static IReadOnlyList<string> AllGuideFiles() =>
        Enumerable.Range(GroupFileOffset + 1, SymptomFileNumber - GroupFileOffset)
            .SelectMany(FilesNumbered)
            .ToList();

    /// <summary>Bỏ các khối mã (```…```) để ví dụ trong khối mã không bị coi là link hay tiêu đề thật.</summary>
    public static string StripCodeFences(string markdown) =>
        Regex.Replace(markdown, @"^```.*?^```", string.Empty, RegexOptions.Singleline | RegexOptions.Multiline);

    /// <summary>Các tiêu đề `## …` (đã cắt khoảng trắng thừa), bỏ qua khối mã.</summary>
    public static IReadOnlyList<string> SectionHeadings(string markdown) =>
        Regex.Matches(StripCodeFences(markdown), @"^## (.+?)\s*$", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToList();

    public static TheoryData<int> GroupIds()
    {
        var data = new TheoryData<int>();
        foreach (var group in Groups)
        {
            data.Add(group.Id);
        }

        return data;
    }

    public static TheoryData<string> TypeCodes()
    {
        var data = new TheoryData<string>();
        foreach (var type in Types)
        {
            data.Add(type.Code);
        }

        return data;
    }
}
