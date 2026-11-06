using SignageVienBa.Core;

namespace SignageVienBa.Tests;

/// <summary>
/// Against a real Worker (run it locally with tests\e2e\start-local-worker.ps1, never production):
/// set SIGNAGE_E2E_URL, SIGNAGE_E2E_KEY and SIGNAGE_FFMPEG_DIR.
/// </summary>
public sealed class E2EFactAttribute : FactAttribute
{
    public E2EFactAttribute()
    {
        var url = Environment.GetEnvironmentVariable("SIGNAGE_E2E_URL");
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIGNAGE_E2E_KEY")))
            Skip = "Set SIGNAGE_E2E_URL and SIGNAGE_E2E_KEY (local Worker) to run end-to-end tests.";
        else if (url.Contains("workers.dev", StringComparison.OrdinalIgnoreCase))
            Skip = "Refusing to run end-to-end tests against a deployed Worker.";
    }
}

[Trait("Category", "E2E")]
public class EndToEndTests : IDisposable
{
    private static readonly string Url = Environment.GetEnvironmentVariable("SIGNAGE_E2E_URL") ?? "";
    private static readonly string Key = Environment.GetEnvironmentVariable("SIGNAGE_E2E_KEY") ?? "";
    private readonly string _dir = Directory.CreateTempSubdirectory("signage-e2e-").FullName;
    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public void Dispose()
    {
        _http.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    [E2EFact]
    public async Task Video_is_compressed_uploaded_listed_played_and_deleted_on_tv2()
    {
        var client = new SignageClient(_http, Url, Key);
        var tv2 = new TvScreen(2);
        var src = Clips.Make(_dir, "Khai trương #1.mov", 1280, 720, 30, 2, audio: true);
        var name = Uploader.TargetName(src);
        Assert.Equal("Khai trương -1.mp4", name);

        var stages = new List<JobStage>();
        var key = await new Uploader(Clips.Tools, client).RunAsync(src, name, tv2,
            new SyncJobProgress(p => { lock (stages) if (stages.LastOrDefault() != p.Stage || stages.Count == 0) stages.Add(p.Stage); }),
            CancellationToken.None);

        Assert.Equal("screen2/Khai trương -1.mp4", key);
        Assert.Equal([JobStage.Checking, JobStage.Compressing, JobStage.Uploading], stages.Distinct());
        Assert.Contains(key, await client.ListAsync(tv2, CancellationToken.None));
        Assert.DoesNotContain(key, await client.ListAsync(new TvScreen(1), CancellationToken.None));

        // The slideshow fetches '/' + key unescaped; the browser escapes it like FileUri does.
        using var resp = await _http.GetAsync(client.FileUri(key));
        resp.EnsureSuccessStatusCode();
        Assert.Equal("video/mp4", resp.Content.Headers.ContentType?.MediaType);
        var played = Path.Combine(_dir, "played.mp4");
        await File.WriteAllBytesAsync(played, await resp.Content.ReadAsByteArrayAsync());
        var info = await Clips.Tools.ProbeAsync(played, CancellationToken.None);
        Assert.Equal((1080, 1920), (info.Width, info.Height));

        await client.DeleteAsync(key, CancellationToken.None);
        Assert.DoesNotContain(key, await client.ListAsync(tv2, CancellationToken.None));
    }

    [E2EFact]
    public async Task Image_is_uploaded_as_is_to_tv1_and_replaces_a_file_with_the_same_name()
    {
        var client = new SignageClient(_http, Url, Key);
        var tv1 = new TvScreen(1);
        var img = Path.Combine(_dir, "Ảnh sảnh.JPG");
        await File.WriteAllBytesAsync(img, [0xFF, 0xD8, 0xFF, 1, 2, 3]);
        var uploader = new Uploader(Clips.Tools, client);
        var key = await uploader.RunAsync(img, Uploader.TargetName(img), tv1, null, CancellationToken.None);
        Assert.Equal("Ảnh sảnh.jpg", key);

        await File.WriteAllBytesAsync(img, [0xFF, 0xD8, 0xFF, 9, 9, 9, 9]);
        await uploader.RunAsync(img, Uploader.TargetName(img), tv1, null, CancellationToken.None);
        Assert.Single(await client.ListAsync(tv1, CancellationToken.None), k => k == key);
        Assert.Equal(await File.ReadAllBytesAsync(img), await _http.GetByteArrayAsync(client.FileUri(key)));

        await client.DeleteAsync(key, CancellationToken.None);
    }

    [E2EFact]
    public async Task Wrong_key_is_reported_and_check_passes_with_the_right_one()
    {
        await new SignageClient(_http, Url, Key).CheckAsync(CancellationToken.None);
        var e = await Assert.ThrowsAsync<SignageException>(() =>
            new SignageClient(_http, Url, "sai-ma").CheckAsync(CancellationToken.None));
        Assert.Contains("Mã API không đúng", e.Message);
    }
}

internal sealed class SyncJobProgress(Action<JobProgress> action) : IProgress<JobProgress>
{
    public void Report(JobProgress value) => action(value);
}
