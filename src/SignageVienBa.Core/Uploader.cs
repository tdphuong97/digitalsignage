namespace SignageVienBa.Core;

public enum JobStage { Checking, Compressing, Uploading }

public readonly record struct JobProgress(JobStage Stage, double Fraction);

/// <summary>One file from the PC to one TV: check it, compress a video when needed, upload.</summary>
public sealed class Uploader(VideoTools? video, SignageClient client)
{
    public static string TempFolder { get; } = Path.Combine(Path.GetTempPath(), "SignageVienBa");

    /// <summary>The name the file will have on the TV (videos always end up as .mp4).</summary>
    public static string TargetName(string sourcePath) => MediaTypes.Of(sourcePath) switch
    {
        MediaKind.Video => NameRules.SafeName(sourcePath, ".mp4"),
        _ => NameRules.SafeName(sourcePath, Path.GetExtension(sourcePath)),
    };

    /// <summary>Uploads <paramref name="sourcePath"/> as <paramref name="targetName"/>; returns the key.</summary>
    public async Task<string> RunAsync(string sourcePath, string targetName, TvScreen tv,
        IProgress<JobProgress>? progress, CancellationToken ct)
    {
        if (!File.Exists(sourcePath)) throw new SignageException("Không tìm thấy file (đã bị xóa hoặc đổi tên?).");

        var uploadPath = sourcePath;
        string? temp = null;
        try
        {
            switch (MediaTypes.Of(sourcePath))
            {
                case MediaKind.Image:
                    if (new FileInfo(sourcePath).Length > SignageClient.MaxUploadBytes)
                        throw new SignageException("Ảnh lớn hơn 95 MB, không tải lên được. Hãy xuất lại ảnh nhỏ hơn.");
                    break;

                case MediaKind.Video:
                    if (video is null)
                        throw new SignageException("Thiếu bộ nén video (ffmpeg). Hãy cài lại phần mềm.");
                    progress?.Report(new JobProgress(JobStage.Checking, 0));
                    var info = await video.ProbeAsync(sourcePath, ct);
                    if (!TvVideo.IsReady(info, sourcePath))
                    {
                        Directory.CreateDirectory(TempFolder);
                        temp = Path.Combine(TempFolder, Guid.NewGuid().ToString("N") + ".mp4");
                        var p = progress is null ? null : new Progress<double>(f => progress.Report(new JobProgress(JobStage.Compressing, f)));
                        await video.EncodeAsync(sourcePath, temp, info, p, ct);
                        if (new FileInfo(temp).Length > SignageClient.MaxUploadBytes)
                            throw new SignageException("Video sau khi nén vẫn lớn hơn 95 MB. Hãy cắt ngắn video.");
                        uploadPath = temp;
                    }
                    break;

                default:
                    var ext = Path.GetExtension(sourcePath);
                    throw new SignageException(string.IsNullOrEmpty(ext)
                        ? "Không hỗ trợ loại file này. Chỉ nhận ảnh JPG/PNG và video."
                        : $"Không hỗ trợ file {ext.ToLowerInvariant()}. Chỉ nhận ảnh JPG/PNG và video.");
            }

            var key = NameRules.KeyFor(tv, targetName);
            progress?.Report(new JobProgress(JobStage.Uploading, 0));
            var up = progress is null ? null : new Progress<double>(f => progress.Report(new JobProgress(JobStage.Uploading, f)));
            await client.UploadAsync(key, uploadPath, up, ct);
            return key;
        }
        finally
        {
            if (temp is not null) VideoTools.TryDelete(temp);
        }
    }

    /// <summary>Removes compressed files left behind by a crash (older than half a day).</summary>
    public static void CleanTempFolder()
    {
        try
        {
            if (!Directory.Exists(TempFolder)) return;
            foreach (var f in Directory.EnumerateFiles(TempFolder, "*.mp4"))
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(f) > TimeSpan.FromHours(12))
                    VideoTools.TryDelete(f);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
