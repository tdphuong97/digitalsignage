using System.Diagnostics;
using SignageVienBa.Core;

namespace SignageVienBa.Tests;

/// <summary>Runs only when SIGNAGE_FFMPEG_DIR points at a folder with ffmpeg.exe and ffprobe.exe.</summary>
public sealed class FfmpegFactAttribute : FactAttribute
{
    public FfmpegFactAttribute()
    {
        var dir = Environment.GetEnvironmentVariable("SIGNAGE_FFMPEG_DIR");
        if (string.IsNullOrEmpty(dir) || !File.Exists(Path.Combine(dir, "ffmpeg.exe")))
            Skip = "Set SIGNAGE_FFMPEG_DIR to run real ffmpeg tests.";
    }
}

/// <summary>Synthetic clips made by ffmpeg itself, shared by the real-ffmpeg and end-to-end tests.</summary>
internal static class Clips
{
    public static VideoTools Tools => VideoTools.Locate() ?? throw new InvalidOperationException("ffmpeg not found");

    public static string Make(string dir, string name, int w, int h, int fps, double seconds, bool audio)
    {
        var path = Path.Combine(dir, name);
        var args = new List<string> { "-hide_banner", "-loglevel", "error", "-y",
            "-f", "lavfi", "-i", $"testsrc2=size={w}x{h}:rate={fps}:duration={seconds}" };
        if (audio) args.AddRange(["-f", "lavfi", "-i", $"sine=frequency=440:duration={seconds}"]);
        args.AddRange(["-c:v", "libx264", "-pix_fmt", "yuv420p", "-preset", "ultrafast"]);
        if (audio) args.AddRange(["-c:a", "aac", "-shortest"]);
        args.Add(path);

        var psi = new ProcessStartInfo(Tools.FfmpegPath) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException(err);
        return path;
    }
}

public class FfmpegTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("signage-ff-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [FfmpegFact]
    public async Task Landscape_60fps_clip_becomes_portrait_1080x1920_30fps_with_audio()
    {
        var src = Clips.Make(_dir, "ngang có dấu.mov", 1920, 1080, 60, 3, audio: true);
        var tools = Clips.Tools;
        var info = await tools.ProbeAsync(src, CancellationToken.None);
        Assert.Equal(60, info.Fps, 1);
        Assert.False(TvVideo.IsReady(info, src));

        var outPath = Path.Combine(_dir, "out.mp4");
        var reports = new List<double>();
        await tools.EncodeAsync(src, outPath, info, new SyncProgress(reports), CancellationToken.None);

        var result = await tools.ProbeAsync(outPath, CancellationToken.None);
        Assert.Equal((1080, 1920), (result.Width, result.Height));
        Assert.Equal("h264", result.VideoCodec);
        Assert.Equal("yuv420p", result.PixelFormat);
        Assert.Equal(30, result.Fps, 1);
        Assert.Equal("aac", result.AudioCodec);
        Assert.Equal(3, result.DurationSeconds, 0);
        Assert.True(TvVideo.IsReady(result, outPath));
        // A 3 s clip can finish before ffmpeg's first progress tick; mid-way reports are covered by the cancel test.
        Assert.Equal(1.0, reports[^1]);
        Assert.True(reports.SequenceEqual(reports.Order()), "progress went backwards");
    }

    [FfmpegFact]
    public async Task Tall_phone_clip_without_audio_fits_inside_the_frame()
    {
        var src = Clips.Make(_dir, "doc.mp4", 1080, 2340, 30, 2, audio: false);
        var tools = Clips.Tools;
        var info = await tools.ProbeAsync(src, CancellationToken.None);
        var outPath = Path.Combine(_dir, "out.mp4");
        await tools.EncodeAsync(src, outPath, info, null, CancellationToken.None);

        var result = await tools.ProbeAsync(outPath, CancellationToken.None);
        Assert.Equal((1080, 1920), (result.Width, result.Height));
        Assert.Null(result.AudioCodec);
    }

    [FfmpegFact]
    public async Task Cancel_stops_ffmpeg_and_leaves_no_output()
    {
        var src = Clips.Make(_dir, "dai.mp4", 1920, 1080, 30, 30, audio: false);
        var tools = Clips.Tools;
        var info = await tools.ProbeAsync(src, CancellationToken.None);
        var outPath = Path.Combine(_dir, "out.mp4");
        using var cts = new CancellationTokenSource();
        var started = new TaskCompletionSource();
        var progress = new SyncCallback(f => { if (f > 0) started.TrySetResult(); });
        var encode = tools.EncodeAsync(src, outPath, info, progress, cts.Token);
        await Task.WhenAny(started.Task, Task.Delay(20_000));
        Assert.True(started.Task.IsCompleted, "ffmpeg never reported progress");
        var clock = Stopwatch.StartNew();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => encode);
        // Returning quickly means ffmpeg was killed, not waited for (30 s of 1080p takes far longer).
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"cancel took {clock.Elapsed}");
        Assert.False(File.Exists(outPath));
    }

    [FfmpegFact]
    public async Task Broken_file_gives_a_plain_message()
    {
        var bad = Path.Combine(_dir, "hong.mp4");
        File.WriteAllText(bad, "not a video");
        var e = await Assert.ThrowsAsync<SignageException>(() => Clips.Tools.ProbeAsync(bad, CancellationToken.None));
        Assert.Contains("Không đọc được video", e.Message);
    }
}

internal sealed class SyncCallback(Action<double> action) : IProgress<double>
{
    public void Report(double value) => action(value);
}
