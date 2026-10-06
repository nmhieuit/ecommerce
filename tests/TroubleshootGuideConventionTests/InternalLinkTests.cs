using System.Text.RegularExpressions;

namespace TroubleshootGuideConventionTests;

/// <summary>
/// Spec 032, bất biến 7–8 của contracts/guide-convention-test-contract.md: mọi link nội bộ trong file 09–17 và
/// `00-tong-quan-lo-trinh.md` trỏ tới đích có thật, và 00 liệt kê đủ file 09–17.
/// </summary>
public class InternalLinkTests
{
    /// <summary>`[chữ](đích)` hoặc `[chữ](đích "tiêu đề")`; đích không chứa khoảng trắng (khoảng trắng đã mã hoá %20).</summary>
    private static readonly Regex MarkdownLink = new(
        @"(?<!!)\[[^\]]*\]\((?<target>[^)\s]+)(?:\s+""[^""]*"")?\)", RegexOptions.Compiled);

    private static string OverviewFile => Path.Combine(GuideFixture.GuideDirectory, "00-tong-quan-lo-trinh.md");

    /// <summary>
    /// Kiểm tra: mọi link nội bộ (bỏ http/https/mailto) trong file 09–17 và 00 trỏ tới file/thư mục có thật; nếu
    /// có neo `#...` và đích là file .md thì tiêu đề tương ứng phải có thật (cách tạo neo kiểu GitHub).
    /// Lý do phải test: link hỏng làm người học cụt giữa chừng, nhất là các link sang quy trình triage 028 và
    /// truy vấn xác nhận 15 phút của file 08.
    /// Task nguồn: spec 032 FR-010 (bất biến 7).
    /// </summary>
    [Fact]
    public void EveryInternalLink_PointsToSomethingThatExists()
    {
        var files = GuideFixture.AllGuideFiles().Append(OverviewFile).Where(File.Exists).ToList();

        // Assert.NotEmpty: xanh khi có ít nhất một file để kiểm (đỏ khi tài liệu 09–17 chưa viết và 00 không có).
        Assert.NotEmpty(files);

        var broken = new List<string>();
        foreach (var file in files)
        {
            var text = GuideFixture.StripCodeFences(File.ReadAllText(file));
            foreach (Match match in MarkdownLink.Matches(text))
            {
                var problem = CheckTarget(file, match.Groups["target"].Value);
                if (problem is not null)
                {
                    broken.Add($"{Path.GetFileName(file)}: ({match.Groups["target"].Value}) {problem}");
                }
            }
        }

        // Assert.Empty: xanh khi không có link hỏng, đỏ kèm danh sách link hỏng.
        Assert.Empty(broken);
    }

    /// <summary>
    /// Kiểm tra: `00-tong-quan-lo-trinh.md` có link tới đủ 9 file 09–17.
    /// Lý do phải test: người đọc đi theo lộ trình 00; file nào không có trong 00 thì không ai tìm thấy.
    /// Task nguồn: spec 032 FR-009, SC-005 (bất biến 8).
    /// </summary>
    [Fact]
    public void Overview_LinksToEveryGuideFile()
    {
        // Assert.True: xanh khi 00 tồn tại.
        Assert.True(File.Exists(OverviewFile), "Thiếu 00-tong-quan-lo-trinh.md");

        var text = GuideFixture.StripCodeFences(File.ReadAllText(OverviewFile));
        var targets = MarkdownLink.Matches(text)
            .Select(m => Uri.UnescapeDataString(m.Groups["target"].Value.Split('#')[0]))
            .ToHashSet();

        var missing = new List<int>();
        for (var number = GuideFixture.GroupFileOffset + 1; number <= GuideFixture.SymptomFileNumber; number++)
        {
            if (!targets.Any(t => Path.GetFileName(t).StartsWith($"{number:D2}-", StringComparison.Ordinal)))
            {
                missing.Add(number);
            }
        }

        // Assert.Empty: xanh khi 00 trỏ tới mọi file 09–17, đỏ kèm số file thiếu.
        Assert.Empty(missing);
    }

    /// <summary>Trả về mô tả lỗi, hoặc `null` khi link hợp lệ hay là link ngoài.</summary>
    private static string? CheckTarget(string fromFile, string rawTarget)
    {
        if (Regex.IsMatch(rawTarget, @"^(?:https?:|mailto:)", RegexOptions.IgnoreCase))
        {
            return null;
        }

        var hashIndex = rawTarget.IndexOf('#');
        var pathPart = hashIndex >= 0 ? rawTarget[..hashIndex] : rawTarget;
        var anchor = hashIndex >= 0 ? Uri.UnescapeDataString(rawTarget[(hashIndex + 1)..]) : null;

        var targetPath = pathPart.Length == 0
            ? fromFile
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fromFile)!, Uri.UnescapeDataString(pathPart)));

        var exists = File.Exists(targetPath) || Directory.Exists(targetPath);
        if (!exists)
        {
            return "đích không tồn tại";
        }

        if (anchor is { Length: > 0 } && targetPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            && !HeadingAnchors(targetPath).Contains(anchor))
        {
            return $"không có tiêu đề ứng với neo '#{anchor}'";
        }

        return null;
    }

    /// <summary>Neo của mọi tiêu đề trong file, theo cách GitHub tạo (chữ thường, bỏ dấu câu, khoảng trắng → '-').</summary>
    private static HashSet<string> HeadingAnchors(string markdownFile)
    {
        var anchors = new HashSet<string>();
        var counts = new Dictionary<string, int>();
        foreach (Match heading in Regex.Matches(
                     GuideFixture.StripCodeFences(File.ReadAllText(markdownFile)), @"(?m)^#{1,6}\s+(.+?)\s*$"))
        {
            var slug = Regex.Replace(heading.Groups[1].Value.ToLowerInvariant(), @"[^\p{L}\p{N}\s_-]", string.Empty)
                .Replace(' ', '-');
            counts[slug] = counts.GetValueOrDefault(slug) + 1;
            anchors.Add(counts[slug] == 1 ? slug : $"{slug}-{counts[slug] - 1}");
        }

        return anchors;
    }
}
