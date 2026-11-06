using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SignageVienBa.Core;

public sealed record VideoInfo(
    double DurationSeconds,
    int Width,
    int Height,
    string VideoCodec,
    string PixelFormat,
    double Fps,
    int Rotation,
    string? AudioCodec,
    long SizeBytes)
{
    public bool HasAudio => AudioCodec is not null;
}

/// <summary>What the two portrait TVs get: 1080x1920 H.264, at most 30 fps, small enough to upload.</summary>
public static class TvVideo
{
    public const int Width = 1080;
    public const int Height = 1920;
    public const int MaxFps = 30;
    public const int TargetMB = 85;
    public const int AudioKbps = 128;
    public const int MaxVideoKbps = 8000;
    public const int MinVideoKbps = 200;

    /// <summary>Already right for the TVs: upload as it is, no re-encoding.</summary>
    public static bool IsReady(VideoInfo v, string path) =>
        string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase) &&
        v.VideoCodec == "h264" &&
        v.Width == Width && v.Height == Height && v.Rotation % 360 == 0 &&
        v.PixelFormat == "yuv420p" &&
        v.Fps is > 0 and <= MaxFps + 0.5 &&
        (v.AudioCodec is null or "aac" or "mp3") &&
        v.SizeBytes <= 90L * 1024 * 1024;

    /// <summary>
    /// Video bitrate cap in kbps so the file lands under <see cref="TargetMB"/>, never above
    /// <see cref="MaxVideoKbps"/> (more is wasted on a 1080p sign and strains TV decoders).
    /// </summary>
    public static int VideoKbps(VideoInfo v)
    {
        var audio = v.HasAudio ? AudioKbps : 0;
        var budget = (int)Math.Floor(TargetMB * 8192.0 / v.DurationSeconds) - audio;
        if (budget < MinVideoKbps)
        {
            var maxMinutes = (int)(TargetMB * 8192.0 / (MinVideoKbps + audio) / 60);
            throw new SignageException(
                $"Video dài {v.DurationSeconds / 60:0} phút, quá dài để nén dưới {TargetMB} MB. " +
                $"Hãy cắt ngắn còn khoảng {maxMinutes} phút trở xuống.");
        }
        return Math.Min(MaxVideoKbps, budget);
    }

    /// <summary>
    /// The picture scaled to fit inside 1080x1920 (never cropped), centred on a blurred,
    /// zoomed copy of itself. The blur runs at quarter size, which is much faster.
    /// </summary>
    public static string FilterGraph(VideoInfo v)
    {
        var fps = v.Fps > MaxFps + 0.5 || v.Fps <= 0 ? $"fps={MaxFps}," : "";
        return
            $"[0:v]{fps}split=2[a][b];" +
            $"[a]scale={Width / 4}:{Height / 4}:force_original_aspect_ratio=increase,crop={Width / 4}:{Height / 4}," +
            $"boxblur=luma_radius=10:luma_power=2,scale={Width}:{Height},setsar=1[bg];" +
            $"[b]scale={Width}:{Height}:force_original_aspect_ratio=decrease:force_divisible_by=2,setsar=1[fg];" +
            "[bg][fg]overlay=(W-w)/2:(H-h)/2,format=yuv420p[out]";
    }

    public static IReadOnlyList<string> EncodeArgs(string input, string output, VideoInfo v)
    {
        var kbps = VideoKbps(v);
        var args = new List<string>
        {
            "-hide_banner", "-nostdin", "-y",
            "-i", input,
            "-filter_complex", FilterGraph(v),
            "-map", "[out]",
        };
        if (v.HasAudio) args.AddRange(["-map", "0:a:0"]);
        args.AddRange([
            "-c:v", "libx264", "-preset", "medium", "-crf", "21",
            "-profile:v", "high", "-level:v", "4.0",
            "-x264-params", "ref=1:bframes=2",
            "-maxrate", $"{kbps}k", "-bufsize", $"{kbps * 2}k",
            "-g", "60",
        ]);
        args.AddRange(v.HasAudio
            ? ["-c:a", "aac", "-b:a", $"{AudioKbps}k", "-ac", "2", "-ar", "48000"]
            : ["-an"]);
        args.AddRange([
            "-sn", "-dn", "-map_metadata", "-1",
            "-max_muxing_queue_size", "1024",
            "-movflags", "+faststart",
            "-progress", "pipe:1", "-nostats", "-loglevel", "error",
            output,
        ]);
        return args;
    }
}

/// <summary>Runs ffprobe and ffmpeg.</summary>
public sealed class VideoTools(string ffmpegPath, string ffprobePath)
{
    public string FfmpegPath { get; } = ffmpegPath;
    public string FfprobePath { get; } = ffprobePath;

    /// <summary>
    /// Finds ffmpeg: SIGNAGE_FFMPEG_DIR, then the "ffmpeg" folder next to the app, then the app
    /// folder, then PATH. Returns null when it is missing.
    /// </summary>
    public static VideoTools? Locate()
    {
        var dirs = new List<string?>
        {
            Environment.GetEnvironmentVariable("SIGNAGE_FFMPEG_DIR"),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg"),
            AppContext.BaseDirectory,
        };
        dirs.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
        foreach (var dir in dirs)
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var ffmpeg = Path.Combine(dir, "ffmpeg.exe");
            var ffprobe = Path.Combine(dir, "ffprobe.exe");
            if (File.Exists(ffmpeg) && File.Exists(ffprobe)) return new VideoTools(ffmpeg, ffprobe);
        }
        return null;
    }

    public async Task<VideoInfo> ProbeAsync(string path, CancellationToken ct)
    {
        var (exit, stdout, stderr) = await RunAsync(FfprobePath,
            ["-v", "error", "-show_format", "-show_streams", "-of", "json", path], null, ct);
        if (exit != 0)
            throw new SignageException("Không đọc được video này (file hỏng hoặc định dạng lạ). " + LastLine(stderr));
        return ParseProbe(stdout, new FileInfo(path).Length);
    }

    public static VideoInfo ParseProbe(string json, long sizeBytes)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        JsonElement? video = null, audio = null;
        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var s in streams.EnumerateArray())
            {
                var type = Str(s, "codec_type");
                var isCover = s.TryGetProperty("disposition", out var d) &&
                              d.TryGetProperty("attached_pic", out var pic) && pic.GetInt32() == 1;
                if (type == "video" && !isCover && video is null) video = s;
                else if (type == "audio" && audio is null) audio = s;
            }
        }
        if (video is not { } v) throw new SignageException("File này không có hình (không phải video).");

        double duration = 0;
        if (root.TryGetProperty("format", out var format)) duration = Num(Str(format, "duration"));
        if (duration <= 0) duration = Num(Str(v, "duration"));
        if (duration <= 0) throw new SignageException("Không đọc được thời lượng của video này.");

        var fps = Rate(Str(v, "avg_frame_rate"));
        if (fps <= 0) fps = Rate(Str(v, "r_frame_rate"));

        var rotation = 0;
        if (v.TryGetProperty("side_data_list", out var sideData))
            foreach (var sd in sideData.EnumerateArray())
                if (sd.TryGetProperty("rotation", out var r) && r.ValueKind == JsonValueKind.Number)
                    rotation = r.GetInt32();
        if (rotation == 0 && v.TryGetProperty("tags", out var tags))
            rotation = (int)Num(Str(tags, "rotate"));

        return new VideoInfo(
            duration,
            v.TryGetProperty("width", out var w) ? w.GetInt32() : 0,
            v.TryGetProperty("height", out var h) ? h.GetInt32() : 0,
            Str(v, "codec_name") ?? "",
            Str(v, "pix_fmt") ?? "",
            fps,
            rotation,
            audio is { } a ? Str(a, "codec_name") ?? "unknown" : null,
            sizeBytes);
    }

    /// <summary>Encodes for the TVs into <paramref name="output"/>; progress is 0..1.</summary>
    public async Task EncodeAsync(string input, string output, VideoInfo info, IProgress<double>? progress, CancellationToken ct)
    {
        var args = TvVideo.EncodeArgs(input, output, info);
        void OnLine(string line)
        {
            // ffmpeg writes out_time_us (and the misnamed out_time_ms, also microseconds).
            var eq = line.IndexOf('=');
            if (eq < 0) return;
            var key = line[..eq];
            if (key is not ("out_time_us" or "out_time_ms")) return;
            if (!long.TryParse(line[(eq + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var us)) return;
            progress?.Report(Math.Clamp(us / 1_000_000.0 / info.DurationSeconds, 0, 1));
        }

        int exit;
        string stderr;
        try
        {
            (exit, _, stderr) = await RunAsync(FfmpegPath, args, OnLine, ct, lowPriority: true);
        }
        catch
        {
            TryDelete(output);
            throw;
        }
        if (exit != 0 || !File.Exists(output) || new FileInfo(output).Length == 0)
        {
            TryDelete(output);
            throw new SignageException("Nén video thất bại. " + LastLine(stderr));
        }
        progress?.Report(1);
    }

    private static async Task<(int Exit, string Stdout, string Stderr)> RunAsync(
        string exe, IReadOnlyList<string> args, Action<string>? onStdoutLine, CancellationToken ct, bool lowPriority = false)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = Process.Start(psi) ?? throw new SignageException("Không chạy được " + Path.GetFileName(exe));
        if (lowPriority)
        {
            try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (InvalidOperationException) { }
        }
        using var kill = ct.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        });

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var outTask = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                if (onStdoutLine is null) stdout.AppendLine(line);
                else onStdoutLine(line);
            }
        });
        var errTask = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync() is { } line)
            {
                stderr.AppendLine(line);
                if (stderr.Length > 16_000) stderr.Remove(0, stderr.Length - 8_000);
            }
        });
        await Task.WhenAll(outTask, errTask);
        await process.WaitForExitAsync(CancellationToken.None);
        ct.ThrowIfCancellationRequested();
        return (process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static string LastLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "";

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) ? p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString() : null;

    private static double Num(string? s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static double Rate(string? s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        var parts = s.Split('/');
        if (parts.Length != 2) return Num(s);
        var den = Num(parts[1]);
        return den == 0 ? 0 : Num(parts[0]) / den;
    }

    internal static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
