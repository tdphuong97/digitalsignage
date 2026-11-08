using SignageVienBa.Core;

namespace SignageVienBa.Tests;

public class DiagnosisTests
{
    [Fact]
    public void Empty_tv_is_a_warning()
    {
        var o = Diagnosis.TvContent(new TvScreen(2), []);
        Assert.Equal(CheckStatus.Warning, o.Status);
        Assert.Contains("TV 2", o.Detail);
    }

    [Fact]
    public void Normal_content_is_ok()
    {
        var o = Diagnosis.TvContent(new TvScreen(1), ["01-Chào.jpg", "video/02-Tin.mp4", "03.PNG"]);
        Assert.Equal(CheckStatus.Ok, o.Status);
        Assert.StartsWith("3 nội dung", o.Detail);
    }

    [Fact]
    public void Leftovers_and_files_the_tv_cannot_show_are_named_without_the_screen_prefix()
    {
        var o = Diagnosis.TvContent(new TvScreen(2),
            ["screen2/01.jpg", "screen2/video/TVNews09.tmp.mp4", "screen2/clip.mov", "screen2/ghi chú.docx"]);
        Assert.Equal(CheckStatus.Warning, o.Status);
        Assert.Contains("file nén dở video/TVNews09.tmp.mp4", o.Detail);
        Assert.Contains("file TV không chiếu được clip.mov, ghi chú.docx", o.Detail);
        Assert.DoesNotContain("screen2/", o.Detail);
        Assert.Contains("Xóa", o.Fix);
    }

    [Fact]
    public void Long_lists_are_shortened()
    {
        var o = Diagnosis.TvContent(new TvScreen(1), ["a.mov", "b.mov", "c.mov", "d.mov", "e.mov"]);
        Assert.Contains("a.mov, b.mov, c.mov và 2 file khác", o.Detail);
    }

    [Theory]
    [InlineData(500L * 1024 * 1024, CheckStatus.Error)]
    [InlineData(2L * 1024 * 1024 * 1024, CheckStatus.Warning)]
    [InlineData(40L * 1024 * 1024 * 1024, CheckStatus.Ok)]
    public void Disk_space_thresholds(long free, CheckStatus expected) =>
        Assert.Equal(expected, Diagnosis.DiskSpace(free).Status);

    [Fact]
    public void Disk_space_is_shown_in_gb_or_mb()
    {
        Assert.Contains("40 GB", Diagnosis.DiskSpace(40L * 1024 * 1024 * 1024).Detail);
        Assert.Contains("500 MB", Diagnosis.DiskSpace(500L * 1024 * 1024).Detail);
    }

    [Fact]
    public void Old_sync_is_an_error_only_while_enabled()
    {
        Assert.Equal(CheckStatus.Ok, Diagnosis.OldSync([]).Status);
        Assert.Equal(CheckStatus.Ok, Diagnosis.OldSync([(@"\Signage TV1", false)]).Status);
        var o = Diagnosis.OldSync([(@"\Signage TV1", false), (@"\Signage TV2", true)]);
        Assert.Equal(CheckStatus.Error, o.Status);
        Assert.Contains(@"\Signage TV2", o.Detail);
        Assert.DoesNotContain(@"\Signage TV1", o.Detail);
    }

    [Fact]
    public void Report_lists_every_row_with_its_fix()
    {
        var text = Diagnosis.Report("1.0.3", "https://signage.example.dev/", "10.0.26300", new DateTime(2026, 11, 8, 9, 5, 0),
        [
            ("Kết nối máy chủ", CheckOutcome.Ok("Máy chủ trả lời sau 120 ms.")),
            ("Nội dung TV 2", CheckOutcome.Warn("Có file nén dở.", "Bấm Xóa.")),
            ("Mã API", CheckOutcome.Fail("Mã API không đúng.", "Vào Cài đặt.")),
        ]);
        Assert.StartsWith("Chẩn đoán Signage Viên Ba 1.0.3 · 08/11/2026 09:05", text);
        Assert.Contains("[OK] Kết nối máy chủ: Máy chủ trả lời sau 120 ms.", text);
        Assert.Contains("[CẢNH BÁO] Nội dung TV 2: Có file nén dở.", text);
        Assert.Contains("    Cách xử lý: Bấm Xóa.", text);
        Assert.Contains("[LỖI] Mã API: Mã API không đúng.", text);
    }
}
