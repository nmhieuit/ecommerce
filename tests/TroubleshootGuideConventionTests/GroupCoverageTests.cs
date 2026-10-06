using System.Text.RegularExpressions;

namespace TroubleshootGuideConventionTests;

/// <summary>
/// Spec 032 (tài liệu luyện troubleshoot theo nhóm lỗi), bất biến 1–6 của
/// specs/032-troubleshoot-practice-docs/contracts/guide-convention-test-contract.md: mỗi nhóm/loại trong
/// scripts/incident-drill/catalog.json có file hướng dẫn và mục gợi ý tương ứng, đúng khung đã chốt.
/// Test chỉ kiểm phủ và cấu trúc, KHÔNG kiểm nội dung khớp giữa tài liệu và catalog (quyết định người dùng).
/// </summary>
public class GroupCoverageTests
{
    /// <summary>6 mục bắt buộc của mọi file nhóm (guide-structure-contract.md), ngoài các mục `Loại X`.</summary>
    private static readonly string[] RequiredSections =
        ["Bạn sẽ thấy", "Điều kiện", "Giới hạn đã biết", "Bài tập tự làm", "Đã đạt khi", "Xem thêm"];

    /// <summary>Mục của file 17 không phải triệu chứng (không cần đủ 3 mức).</summary>
    private static readonly string[] NonSymptomSections = ["Cách dùng file này", "Truy vấn dùng chung", "Bẫy và nhiễu thường gặp"];

    private static readonly Regex ServiceNameInHint = new(
        $@"\b(?:{string.Join("|", GuideFixture.ServiceNames)})(?:\.Api|-api)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TypeCodeInHint = new(@"\bLoại\s+[A-I]\b", RegexOptions.Compiled);

    private static readonly Regex LevelMarker = new(@"^\*\*Mức (\d) —", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Kiểm tra: mỗi nhóm trong catalog.json có đúng một file `docs/kibana-quan-sat-he-thong/&lt;08+id&gt;-*.md`
    /// và file đó mở đầu bằng tiêu đề `# &lt;08+id&gt; —`.
    /// Lý do phải test: người học đi theo số thứ tự file; thiếu hoặc trùng file làm lộ trình 00 sai.
    /// Task nguồn: spec 032 FR-001, FR-010 (bất biến 1).
    /// </summary>
    [Theory]
    [MemberData(nameof(GuideFixture.GroupIds), MemberType = typeof(GuideFixture))]
    public void EveryGroup_HasExactlyOneGuideFile_WithNumberedTitle(int groupId)
    {
        var number = GuideFixture.GroupFileOffset + groupId;
        var files = GuideFixture.FilesNumbered(number);

        // Assert.Single: xanh khi có đúng một file `<NN>-*.md`, đỏ khi chưa viết hoặc bị trùng.
        var file = Assert.Single(files);

        // Assert.Matches: xanh khi tiêu đề cấp 1 đúng dạng `# NN —`.
        Assert.Matches($@"(?m)^# {number:D2} —", File.ReadAllText(file));
    }

    /// <summary>
    /// Kiểm tra: mỗi loại lỗi A–I có mục `## Loại &lt;mã&gt;` trong file của nhóm chứa nó.
    /// Lý do phải test: nhóm 2 có hai loại (B, C) dùng chung một file; thiếu mục làm người học không tìm thấy
    /// hướng dẫn của loại đó.
    /// Task nguồn: spec 032 FR-002 (bất biến 2).
    /// </summary>
    [Theory]
    [MemberData(nameof(GuideFixture.TypeCodes), MemberType = typeof(GuideFixture))]
    public void EveryType_HasSectionInItsGroupFile(string code)
    {
        var type = GuideFixture.Types.Single(t => t.Code == code);
        var file = GuideFixture.GroupFile(type.GroupId);

        // Assert.NotNull: xanh khi nhóm chứa loại này đã có đúng một file.
        Assert.NotNull(file);

        // Assert.Matches: xanh khi có tiêu đề `## Loại <mã>`.
        Assert.Matches($@"(?m)^## Loại {code}\b", GuideFixture.StripCodeFences(File.ReadAllText(file!)));
    }

    /// <summary>
    /// Kiểm tra: mọi file nhóm có đủ 6 mục bắt buộc (Bạn sẽ thấy, Điều kiện, Giới hạn đã biết, Bài tập tự làm,
    /// Đã đạt khi, Xem thêm).
    /// Lý do phải test: các mục này làm tài liệu dùng được như bài học (có bài tập, có tiêu chí đã đạt, có
    /// liên kết sang quy trình triage 028); thiếu một mục là hướng dẫn cụt.
    /// Task nguồn: spec 032 FR-002 (bất biến 3).
    /// </summary>
    [Theory]
    [MemberData(nameof(GuideFixture.GroupIds), MemberType = typeof(GuideFixture))]
    public void EveryGuideFile_HasRequiredSections(int groupId)
    {
        var file = GuideFixture.GroupFile(groupId);

        // Assert.NotNull: xanh khi file nhóm tồn tại.
        Assert.NotNull(file);

        var headings = GuideFixture.SectionHeadings(File.ReadAllText(file!));
        var missing = RequiredSections.Where(s => !headings.Contains(s)).ToList();

        // Assert.Empty: xanh khi không thiếu mục nào, đỏ kèm danh sách mục thiếu.
        Assert.Empty(missing);
    }

    /// <summary>
    /// Kiểm tra: file 17 có ít nhất một dòng `**Mức 3 — Đáp án (Loại &lt;mã&gt;)**` cho MỖI loại A–I.
    /// Lý do phải test: nếu một loại không xuất hiện ở mức 3, người làm bài mù gặp loại đó sẽ không có đáp án để
    /// đối chiếu.
    /// Task nguồn: spec 032 FR-004 (bất biến 4).
    /// </summary>
    [Theory]
    [MemberData(nameof(GuideFixture.TypeCodes), MemberType = typeof(GuideFixture))]
    public void SymptomFile_HasLevel3_ForEveryType(string code)
    {
        var file = GuideFixture.SymptomFile();

        // Assert.NotNull: xanh khi file 17 tồn tại.
        Assert.NotNull(file);

        // Assert.Contains: xanh khi có dòng mức 3 của loại này.
        Assert.Contains($"**Mức 3 — Đáp án (Loại {code})**", File.ReadAllText(file!));
    }

    /// <summary>
    /// Kiểm tra: mỗi mục triệu chứng trong file 17 có đúng một mức 1, đúng một mức 2 và ít nhất một mức 3, theo
    /// thứ tự 1 → 2 → 3.
    /// Lý do phải test: thứ tự mở dần nhẹ → mạnh → đáp án là điểm cốt lõi của dạng (b); đảo thứ tự làm lộ đáp án.
    /// Task nguồn: spec 032 FR-004 (bất biến 5).
    /// </summary>
    [Fact]
    public void SymptomFile_EverySymptom_HasLevels1Then2Then3()
    {
        var sections = SymptomSections();

        // Assert.NotEmpty: xanh khi file 17 có ít nhất một mục triệu chứng.
        Assert.NotEmpty(sections);

        foreach (var (title, body) in sections)
        {
            var levels = LevelMarker.Matches(body).Select(m => int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();

            // Assert.True: xanh khi dãy mức là 1, 2, 3 (có thể lặp 3 cho nhiều loại), đỏ kèm tên triệu chứng.
            Assert.True(
                levels.Count >= 3 && levels[0] == 1 && levels[1] == 2 && levels.Skip(2).All(l => l == 3),
                $"Mục '{title}' phải có mức 1, mức 2 rồi ít nhất một mức 3 theo đúng thứ tự; thực tế: [{string.Join(", ", levels)}].");
        }
    }

    /// <summary>
    /// Kiểm tra: khối mức 1 và mức 2 trong file 17 không chứa `Loại &lt;chữ cái&gt;` hay tên một trong 7 service
    /// (`Xxx.Api`, `xxx-api`).
    /// Lý do phải test: đọc triệu chứng không được lộ đáp án; đây chỉ là dấu hiệu rẻ bắt lỗi lỡ tay, việc "không
    /// lộ" đầy đủ vẫn rà thủ công.
    /// Task nguồn: spec 032 FR-004, SC-003 (bất biến 6).
    /// </summary>
    [Fact]
    public void SymptomFile_Levels1And2_DoNotLeakTypeOrService()
    {
        foreach (var (title, body) in SymptomSections())
        {
            foreach (var (level, text) in LevelBlocks(body).Where(b => b.Level is 1 or 2))
            {
                // Assert.DoesNotMatch: xanh khi khối không có mã loại / tên service.
                Assert.DoesNotMatch(TypeCodeInHint, text);
                Assert.True(
                    !ServiceNameInHint.IsMatch(text),
                    $"Mục '{title}', mức {level} không được nhắc tên service: '{ServiceNameInHint.Match(text).Value}'.");
            }
        }
    }

    /// <summary>Các mục triệu chứng của file 17: (tiêu đề, nội dung) — loại các mục giới thiệu và "Bẫy và nhiễu".</summary>
    private static List<(string Title, string Body)> SymptomSections()
    {
        var file = GuideFixture.SymptomFile();
        Assert.NotNull(file);

        var text = GuideFixture.StripCodeFences(File.ReadAllText(file!));
        var parts = Regex.Split(text, @"(?m)^## ").Skip(1);
        return parts
            .Select(p =>
            {
                var newline = p.IndexOf('\n');
                return (Title: p[..newline].Trim(), Body: p[(newline + 1)..]);
            })
            .Where(s => !NonSymptomSections.Contains(s.Title))
            .ToList();
    }

    /// <summary>Tách nội dung một mục triệu chứng thành các khối theo dòng `**Mức N —`.</summary>
    private static List<(int Level, string Text)> LevelBlocks(string body)
    {
        var matches = LevelMarker.Matches(body);
        var blocks = new List<(int, string)>();
        for (var i = 0; i < matches.Count; i++)
        {
            var start = matches[i].Index;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : body.Length;
            blocks.Add((int.Parse(matches[i].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), body[start..end]));
        }

        return blocks;
    }
}
