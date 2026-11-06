using System.Security.Cryptography;
using System.Text.Json;

namespace SignageVienBa.Core;

/// <summary>A newer version, as the newest release's latest.json describes it.</summary>
public sealed class UpdateInfo
{
    public string Version { get; init; } = "";
    public string? Notes { get; init; }
    /// <summary>The release page, for reading what changed or downloading by hand.</summary>
    public string? Page { get; init; }
    public UpdatePackage? Package { get; init; }
}

public sealed class UpdatePackage
{
    public string Url { get; init; } = "";
    public long Size { get; init; }
    public string Sha256 { get; init; } = "";
}

/// <summary>
/// New versions are GitHub releases (build.ps1 -Publish). Each carries latest.json naming the installer
/// with its size and SHA-256; the "latest/download" address always points at the newest release's copy.
/// </summary>
public sealed class Updater(HttpClient http, string feedUrl)
{
    /// <summary>The GitHub repository releases are published to (build\publish.ps1 reads it from here).</summary>
    public const string Repo = "tdphuong97/digitalsignage";
    public static readonly string DefaultFeed = $"https://github.com/{Repo}/releases/latest/download/latest.json";

    /// <summary>SIGNAGE_UPDATE_FEED overrides the feed, for testing an update without publishing one.</summary>
    public static string Feed =>
        Environment.GetEnvironmentVariable("SIGNAGE_UPDATE_FEED") is { Length: > 0 } feed ? feed : DefaultFeed;

    public static string DownloadFolder { get; } = Path.Combine(Path.GetTempPath(), "SignageVienBa", "updates");

    /// <summary>The newer version, or null when <paramref name="currentVersion"/> is already the newest.</summary>
    public async Task<UpdateInfo?> CheckAsync(string currentVersion, CancellationToken ct)
    {
        JsonElement root;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            using var res = await http.GetAsync(feedUrl, cts.Token);
            if (!res.IsSuccessStatusCode)
                throw new UpdateException($"Không kiểm tra được bản mới (máy chủ trả lời {(int)res.StatusCode}).");
            root = JsonDocument.Parse(await res.Content.ReadAsStringAsync(cts.Token)).RootElement.Clone();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            throw new UpdateException("Không kiểm tra được bản mới. Hãy thử lại khi có mạng.", ex);
        }

        var version = Text(root, "version");
        if (version is null || !IsNewer(version, currentVersion)) return null;
        UpdatePackage? package = null;
        if (root.TryGetProperty("windows", out var p) && Text(p, "url") is { } url && Text(p, "sha256") is { } sha)
            package = new UpdatePackage
            {
                Url = url,
                Sha256 = sha,
                Size = p.TryGetProperty("size", out var size) && size.ValueKind == JsonValueKind.Number ? size.GetInt64() : 0,
            };
        return new UpdateInfo { Version = version, Notes = Text(root, "notes"), Page = Text(root, "page"), Package = package };
    }

    private static string? Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>1.10.0 is newer than 1.9.3: versions compare part by part, as numbers.</summary>
    public static bool IsNewer(string candidate, string current) =>
        System.Version.TryParse(candidate, out var a) && System.Version.TryParse(current, out var b) && Normal(a) > Normal(b);

    private static Version Normal(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    /// <summary>
    /// Downloads the installer into <paramref name="folder"/> and returns its path once its size and SHA-256
    /// match latest.json; anything else is deleted. Progress goes from 0 to 1.
    /// </summary>
    public async Task<string> DownloadAsync(UpdateInfo info, string folder, IProgress<double>? progress, CancellationToken ct)
    {
        var package = info.Package ?? throw new UpdateException("Bản mới chưa có bộ cài cho máy này.");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"SignageVienBa-Setup-{info.Version}.exe");
        var part = path + ".part";
        try
        {
            using (var res = await http.GetAsync(package.Url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                if (!res.IsSuccessStatusCode)
                    throw new UpdateException($"Không tải được bản mới (máy chủ trả lời {(int)res.StatusCode}).");
                var total = package.Size > 0 ? package.Size : res.Content.Headers.ContentLength ?? 0;
                await using var input = await res.Content.ReadAsStreamAsync(ct);
                await using var output = File.Create(part);
                var buffer = new byte[256 * 1024];
                long done = 0;
                int n;
                var last = 0.0;
                while ((n = await input.ReadAsync(buffer, ct)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, n), ct);
                    done += n;
                    if (total <= 0) continue;
                    var f = Math.Min(0.99, (double)done / total);
                    if (f - last >= 0.01) { last = f; progress?.Report(f); }
                }
            }

            await using (var check = File.OpenRead(part))
            {
                if (package.Size > 0 && check.Length != package.Size)
                    throw new UpdateException("Bản mới tải về không đủ, hãy thử lại.");
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(check, ct));
                if (!hash.Equals(package.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new UpdateException("Bản mới tải về bị hỏng hoặc không đúng, phần mềm đã bỏ nó đi.");
            }
            File.Move(part, path, overwrite: true);
            progress?.Report(1);
            return path;
        }
        catch (OperationCanceledException)
        {
            TryDelete(part);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(part);
            throw ex as UpdateException ?? new UpdateException("Không tải được bản mới. Hãy thử lại khi có mạng.", ex);
        }
    }

    /// <summary>Removes installers left from earlier updates.</summary>
    public static void CleanDownloads()
    {
        try
        {
            if (!Directory.Exists(DownloadFolder)) return;
            foreach (var f in Directory.EnumerateFiles(DownloadFolder)) TryDelete(f);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

public sealed class UpdateException(string message, Exception? inner = null) : Exception(message, inner);
