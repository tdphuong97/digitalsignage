using System.Text;
using SignageVienBa.Core;

namespace SignageVienBa.Tests;

public class NameRulesTests
{
    [Fact]
    public void SafeName_keeps_vietnamese_and_normalises_to_nfc()
    {
        var nfd = "Khai trương".Normalize(NormalizationForm.FormD);
        var name = NameRules.SafeName($@"D:\video\{nfd}.MOV", ".mp4");
        Assert.Equal("Khai trương.mp4", name);
        Assert.True(name.IsNormalized(NormalizationForm.FormC));
    }

    [Theory]
    [InlineData(@"C:\a\Sự kiện #1?.jpg", ".jpg", "Sự kiện -1-.jpg")]
    [InlineData(@"C:\a\giảm 50%.PNG", ".PNG", "giảm 50-.png")]
    [InlineData(@"C:\a\  nhiều    khoảng   trắng  .jpg", ".jpg", "nhiều khoảng trắng.jpg")]
    [InlineData(@"C:\a\....jpg", ".jpg", "noi-dung.jpg")]
    public void SafeName_replaces_characters_that_break_urls(string path, string ext, string expected) =>
        Assert.Equal(expected, NameRules.SafeName(path, ext));

    [Fact]
    public void SafeName_shortens_very_long_names()
    {
        var name = NameRules.SafeName(@"C:\" + new string('a', 300) + ".mp4", ".mp4");
        Assert.Equal(120 + 4, name.Length);
    }

    [Fact]
    public void Unique_adds_a_number_ignoring_case()
    {
        var taken = new List<string> { "a.mp4", "A (2).mp4" };
        Assert.Equal("a (3).mp4", NameRules.Unique("a.mp4", taken));
        Assert.Equal("b.mp4", NameRules.Unique("b.mp4", taken));
    }

    [Fact]
    public void Keys_use_the_screen_prefix_except_for_tv1()
    {
        Assert.Equal("a.mp4", NameRules.KeyFor(new TvScreen(1), "a.mp4"));
        Assert.Equal("screen2/a.mp4", NameRules.KeyFor(new TvScreen(2), "a.mp4"));
        Assert.Equal("video/a.mp4", NameRules.DisplayName(new TvScreen(1), "video/a.mp4"));
        Assert.Equal("video/a.mp4", NameRules.DisplayName(new TvScreen(2), "screen2/video/a.mp4"));
    }

    [Fact]
    public void EncodeKey_encodes_segments_and_keeps_slashes()
    {
        Assert.Equal("screen2/Khai%20tr%C6%B0%C6%A1ng.mp4", NameRules.EncodeKey("screen2/Khai trương.mp4"));
        Assert.Equal("a%2Bb%26c.jpg", NameRules.EncodeKey("a+b&c.jpg"));
    }
}

public class MediaTypesTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("signage-test-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData("a.JPG", MediaKind.Image)]
    [InlineData("a.png", MediaKind.Image)]
    [InlineData("a.MOV", MediaKind.Video)]
    [InlineData("a.mkv", MediaKind.Video)]
    [InlineData("a.heic", MediaKind.Unsupported)]
    [InlineData("a", MediaKind.Unsupported)]
    public void Of_classifies_by_extension(string path, MediaKind kind) => Assert.Equal(kind, MediaTypes.Of(path));

    [Fact]
    public void Expand_walks_folders_keeps_explicit_files_and_skips_hidden_or_unsupported()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "sub"));
        File.WriteAllText(Path.Combine(_dir, "b.mp4"), "x");
        File.WriteAllText(Path.Combine(_dir, "sub", "a.jpg"), "x");
        File.WriteAllText(Path.Combine(_dir, "notes.docx"), "x");
        var hidden = Path.Combine(_dir, "hidden.png");
        File.WriteAllText(hidden, "x");
        File.SetAttributes(hidden, FileAttributes.Hidden);
        var loose = Path.Combine(Path.GetTempPath(), $"loose-{Guid.NewGuid():N}.docx");
        File.WriteAllText(loose, "x");
        try
        {
            var result = MediaTypes.Expand([_dir, loose, Path.Combine(_dir, "missing.mp4")]).ToList();
            Assert.Equal(
                [Path.Combine(_dir, "b.mp4"), Path.Combine(_dir, "sub", "a.jpg"), loose],
                result);
        }
        finally { File.Delete(loose); }
    }

    [Fact]
    public void TargetName_turns_every_video_into_mp4()
    {
        Assert.Equal("clip.mp4", Uploader.TargetName(@"C:\x\clip.MOV"));
        Assert.Equal("ảnh.jpg", Uploader.TargetName(@"C:\x\ảnh.JPG"));
    }
}
